# PageIndex Ingestion + Query Refactor Design

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Completely rewrite PageIndex ingest and query logic. Add page number tracking to tree nodes, replace `node_texts` table with `page_texts`, enable two-phase retrieval with drill-down for large documents, and citations. Keep SQLite, keep `GroupName`, keep request body structure.

**Architecture:** TreeNode gets `StartPage`/`EndPage` (page numbers, not character offsets). SqlitePageIndexDatabase stores raw page text instead of per-node text. Ingestion extracts page text from PdfPig and stores it. Query uses two-phase retrieval: Phase 1 picks top-level sections, Phase 2 drills into sub-sections. Summaries generated in parallel (not sequential). Citations enabled. Skeleton truncation prevents context overflow for large documents.

**Tech Stack:** .NET 10, SQLite, PdfPig, Microsoft.Extensions.AI

---

## Data Model Changes

### TreeNode ( Models/TreeNode.cs )

**Remove:** `Text` property (no longer stored per-node)

**Add:**
```csharp
[JsonPropertyName("start_page")]
public int? StartPage { get; set; }

[JsonPropertyName("end_page")]
public int? EndPage { get; set; }
```

**Keep:** `Title`, `NodeId`, `Summary`, `Children`

### DocumentTree ( Models/DocumentTree.cs )

**No changes.** Keep: `Title`, `NodeId`, `Summary`, `DocId`, `FileName`, `GroupName`, `TotalPages`, `Children`

### SQLite Schema

**Drop table:** `node_texts`

**Rename table:** `document_trees` → `documents`

**Add table:** `page_texts`

```sql
CREATE TABLE IF NOT EXISTS documents (
    doc_id TEXT PRIMARY KEY,
    file_name TEXT NOT NULL,
    group_name TEXT NOT NULL DEFAULT '',
    tree_json TEXT NOT NULL,
    page_count INTEGER NOT NULL DEFAULT 0,
    created_at TEXT NOT NULL DEFAULT (datetime('now'))
);
CREATE INDEX IF NOT EXISTS idx_group_name ON documents(group_name);

CREATE TABLE IF NOT EXISTS page_texts (
    doc_id TEXT NOT NULL,
    page_number INTEGER NOT NULL,
    text TEXT NOT NULL,
    PRIMARY KEY (doc_id, page_number)
);
```

---

## File Map

| File | Action | What Changes |
|------|--------|-------------|
| `Models/TreeNode.cs` | Modify | Remove `Text`, add `StartPage`/`EndPage` |
| `Models/DocumentTree.cs` | No change | — |
| `Services/PdfStructureParser.cs` | Modify | Set `StartPage`/`EndPage` on nodes, remove `Text` from tree output |
| `Services/DocumentTreeBuilder.cs` | Modify | Parallel summaries, extract page text, pass page text to service |
| `Services/Interfaces/IPageIndexDatabase.cs` | Modify | Simplify to match new schema |
| `Services/SqlitePageIndexDatabase.cs` | Rewrite | New schema, new methods |
| `Services/Interfaces/IPageIndexService.cs` | No change | Signature stays same |
| `Services/PageIndexService.cs` | Rewrite | New ingest flow, new query flow with page-based retrieval |
| `Controllers/PageIndexController.cs` | No change | — |
| `DTOs/PageIndexIngestionResponse.cs` | No change | — |
| `DTOs/PageIndexQueryRequest.cs` | No change | — |
| `DTOs/PageIndexQueryResponse.cs` | Modify | Add `PageCitations` |
| `Settings/PageIndexSettings.cs` | Modify | Add `MaxSkeletonDepth`, `MaxTokensPerQuery` |
| `Program.cs` | No change | — |

---

## Ingestion Flow (New)

```
PDF upload
  → PdfPig: extract all page texts (List<(int pageNumber, string text)>)
  → PdfStructureParser.Parse(): deterministic tree with StartPage/EndPage per node
  → DocumentTreeBuilder: parallel LLM summaries (Task.WhenAll)
  → PageIndexService:
      → Store document in `documents` table
      → Store each page's text in `page_texts` table
  → Return response
```

**Key changes from old flow:**
1. Page text extracted from PdfPig at ingest time (not at query time)
2. `node_texts` eliminated — text lives in `page_texts` by page number
3. Summaries parallelized (was sequential)
4. TreeNode has page numbers (can cite sources)

---

## Query Flow (New — Two-Phase Retrieval)

### Phase 1: Top-Level Navigation
```
Question + GroupName
  → Load all document trees for the group
  → Build skeleton (title + summary per node, truncated to MaxSkeletonDepth)
  → LLM picks top-level node IDs
  → For each selected node:
      → Check if node has children
      → If yes → Phase 2 (drill-down)
      → If no → retrieve page text directly
```

### Phase 2: Drill-Down (for nodes with children)
```
For each selected node with children:
  → Build sub-skeleton (children of that node)
  → LLM picks specific sub-node IDs
  → Retrieve page text for final selection
  → Collect citations
```

### Final Answer
```
  → Concatenate all retrieved page texts
  → LLM answers with context
  → Return answer + citations (node title + page range)
```

### Large Document Example (1000 pages):
```
Question: "What are the revenue projections?"

Phase 1: LLM sees top-level skeleton (depth 3)
  - 1. Introduction (pages 1-5)
  - 2. Financial Overview (pages 6-50)
  - 3. Market Analysis (pages 51-150)
  LLM picks: "2. Financial Overview"

Phase 2: LLM sees sub-tree of node 2
  - 2.1 Revenue (pages 6-20)
  - 2.2 Costs (pages 21-35)
  - 2.3 Profit (pages 36-50)
  LLM picks: "2.1 Revenue"

Final: Retrieve pages 6-20, answer with context
```

**Key changes from old flow:**
1. Text retrieved by page range (not by node_id lookup)
2. Two-phase retrieval handles large documents (drill-down)
3. Skeleton truncation prevents context overflow
4. Citations populated (node title + page range)
5. `doc_id` always included in queries (fixes cross-doc bug)

---

## Configuration

### Settings/PageIndexSettings.cs

```csharp
public class PageIndexSettings
{
    public string DbPath { get; set; } = "pageindex.db";
    public int MaxSkeletonDepth { get; set; } = 3;        // Max depth for skeleton building
    public int MaxTokensPerQuery { get; set; } = 20000;   // Max tokens for retrieved context
}
```

### appsettings.json

```json
{
  "PageIndex": {
    "DbPath": "pageindex.db",
    "MaxSkeletonDepth": 3,
    "MaxTokensPerQuery": 20000
  }
}
```

---

## Task Breakdown

### Task 1: Update TreeNode model

**Files:**
- Modify: `Models/TreeNode.cs`

Remove `Text` property. Add `StartPage` and `EndPage`.

---

### Task 2: Update PdfStructureParser

**Files:**
- Modify: `Services/PdfStructureParser.cs`

Set `StartPage`/`EndPage` on each `TreeNode` based on which page the section's text blocks span. Remove `Text` from tree output (text will be stored in `page_texts` instead).

---

### Task 3: Update DocumentTreeBuilder

**Files:**
- Modify: `Services/DocumentTreeBuilder.cs`

Return page texts alongside the tree. Parallelize summary generation with `Task.WhenAll`. Extract page text from PdfPig (using `page.GetWords()` or raw text extraction).

---

### Task 4: Update PageIndexSettings

**Files:**
- Modify: `Settings/PageIndexSettings.cs`

Add `MaxSkeletonDepth` (default: 3) and `MaxTokensPerQuery` (default: 20000).

---

### Task 5: Rewrite IPageIndexDatabase

**Files:**
- Modify: `Services/Interfaces/IPageIndexDatabase.cs`

New interface:
```csharp
public interface IPageIndexDatabase
{
    Task InitializeAsync();
    Task InsertDocumentAsync(DocumentTree tree, string treeJson, int pageCount);
    Task InsertPageTextsAsync(string docId, IEnumerable<(int pageNumber, string text)> pageTexts);
    Task<List<(string DocId, string TreeJson)>> GetDocumentTreesByGroupAsync(string groupName);
    Task<Dictionary<int, string>> GetPageTextsAsync(string docId, int startPage, int endPage);
}
```

---

### Task 6: Rewrite SqlitePageIndexDatabase

**Files:**
- Modify: `Services/SqlitePageIndexDatabase.cs`

New schema. New methods. Drop `node_texts`. Add `page_texts`. Rename `document_trees` → `documents`.

---

### Task 7: Update PageIndexQueryResponse

**Files:**
- Modify: `DTOs/PageIndexQueryResponse.cs`

Add `PageCitations` field:
```csharp
public record PageIndexQueryResponse(
    string Answer,
    List<PageIndexCitation> Citations,
    List<PageCitation> PageCitations);

public record PageCitation(string NodeTitle, string DocId, int StartPage, int EndPage);
```

---

### Task 8: Rewrite PageIndexService

**Files:**
- Modify: `Services/PageIndexService.cs`

New ingest: store document + page texts.
New query: two-phase retrieval with drill-down.
- Phase 1: Build truncated skeleton, LLM picks top-level nodes
- Phase 2: For nodes with children, build sub-skeleton, LLM drills down
- Final: Retrieve page texts, answer with context, return citations

---

### Task 9: Delete dead code

**Files:**
- Remove unused methods from old interface/service

Clean up: `InsertNodeTextAsync`, `GetNodeTextsAsync`, `GetNodeTextsByNodeIdsAsync`, `GetDocumentTreeJsonAsync`, `FlattenNodes`, `FindCitations`

---

### Task 10: Build + test

- [ ] Run `dotnet build` — 0 errors
- [ ] Ingest test PDF via curl
- [ ] Query small PDF (5 pages) — verify single-phase works
- [ ] Query large PDF scenario — verify drill-down works
- [ ] Verify citations include page numbers

---

## Verification

```bash
# Build
dotnet build VectorRAGvsPageIndexRAG.sln

# Ingest
curl -X POST 'https://localhost:51095/api/pageindex/documents?provider=OpenCode&model=deepseek-v4-flash-free&groupName=PDFs' \
  -H 'Content-Type: multipart/form-data' \
  -F 'file=@test-pdfs/technical-report.pdf;type=application/pdf'

# Query — should return answer + page citations
curl -X GET 'https://localhost:51095/api/pageindex/query?question=What%20is%20the%20rate%20limit%3F&provider=OpenCode&model=deepseek-v4-flash-free&groupName=PDFs'
```
