# PageIndex RAG — Design Spec

**Status:** Implemented

## Overview

Vectorless RAG using deterministic PDF structure parsing + LLM summaries. PDF uploaded → text extracted via PdfPig → font heuristics build hierarchical document tree → LLM generates summaries for each node → stored in SQLite. Query: LLM reasons over combined skeleton → fetches node texts → LLM generates answer.

## Endpoints

| Method | Route | Purpose |
|---|---|---|
| POST | `/api/pageindex/documents?provider=X&model=Y&groupName=PDFs` | Upload PDF, parse tree + LLM summaries, store in SQLite |
| GET | `/api/pageindex/query?groupName=PDFs&question=X` | Navigate combined skeleton → fetch node texts → LLM answer |

## Ingestion Flow

1. Client sends PDF via multipart form
2. `PdfStructureParser.Parse()` deterministic parsing:
   - Collects all words with font metadata (size, position, page)
   - Calculates median font size → header threshold = median × 1.2
   - Detects section headers by font size grouping
   - Builds text blocks (paragraphs) by vertical gap detection (gap > 1.5× line height)
   - Builds tree structure from headers with font-size nesting
3. `DocumentTreeBuilder.GenerateSummariesAsync()` sends only text to LLM for one-sentence summaries per node
4. Tree stored in SQLite in two tables:
   - `document_trees` — doc_id, file_name, tree_json, group_name (with index on group_name)
   - `node_texts` — doc_id, node_id, text (raw text per node)
5. Returns `PageIndexIngestionResponse(DocId, FileName, Status, PageCount, TreeJson)`

## Query Flow

1. Client sends `{ groupName, question, provider, model }` via GET query params
2. `BuildCombinedSkeletonAsync` fetches all trees for the group from SQLite
3. Builds combined skeleton with source filename prefixes (e.g., "Document: resume.pdf\n  - node_001: Title — Summary")
4. **Step 1 — Navigation**: Send combined skeleton to LLM. LLM reasons over the tree and returns relevant node IDs as JSON array
5. Fetch raw text from `node_texts` for those node IDs via `GetNodeTextsByNodeIdsAsync`
6. **Step 2 — Answer**: Send raw text + question to LLM for final answer
7. Returns `PageIndexQueryResponse(Answer, Citations: [])` — citations empty for group queries

## SQLite Schema

```sql
CREATE TABLE IF NOT EXISTS document_trees (
    doc_id TEXT PRIMARY KEY,
    file_name TEXT NOT NULL,
    tree_json TEXT NOT NULL,
    group_name TEXT NOT NULL DEFAULT '',
    created_at TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE INDEX IF NOT EXISTS idx_group_name ON document_trees(group_name);

CREATE TABLE IF NOT EXISTS node_texts (
    doc_id TEXT NOT NULL,
    node_id TEXT NOT NULL,
    text TEXT NOT NULL,
    PRIMARY KEY (doc_id, node_id)
);
```

## Files

| File | Purpose |
|---|---|
| `Settings/PageIndexSettings.cs` | `DbPath` config |
| `Models/DocumentTree.cs` | Root tree model (title, nodeId, summary, children, DocId, FileName, GroupName, TotalPages) |
| `Models/TreeNode.cs` | Node model (title, nodeId, summary, text, children) |
| `DTOs/PageIndexIngestionResponse.cs` | `(DocId, FileName, Status, PageCount, TreeJson)` |
| `DTOs/PageIndexQueryRequest.cs` | `(Question, Provider, Model, GroupName)` |
| `DTOs/PageIndexQueryResponse.cs` | `(Answer, Citations)` |
| `DTOs/PageIndexCitation.cs` | `(Title, PageIndex, Text)` |
| `Services/Interfaces/IPageIndexDatabase.cs` | SQLite storage interface |
| `Services/Interfaces/IPageIndexService.cs` | Ingestion + query interface |
| `Services/PdfStructureParser.cs` | Deterministic PDF structure via font heuristics |
| `Services/DocumentTreeBuilder.cs` | Parser + LLM summaries → DocumentTree |
| `Services/SqlitePageIndexDatabase.cs` | SQLite implementation of IPageIndexDatabase |
| `Services/PageIndexService.cs` | Orchestrates full pipeline |
| `Controllers/PageIndexController.cs` | Two endpoints (POST ingest, GET query) |

## Default LLM Provider

Both endpoints default to `NvidiaNim` / `meta/llama-3.3-70b-instruct` with optional override via query param.

## Tradeoffs vs Vector RAG

| Dimension | Vector RAG | PageIndex RAG |
|---|---|---|
| Ingestion | Embedding API per chunk + Qdrant upsert | Deterministic parsing + LLM summaries per doc |
| Query | One embedding + one LLM call | Two LLM calls (navigation + answer) |
| Latency | ~1-3s | ~3-8s |
| Retrieval logic | Semantic similarity | LLM reasoning over structure |
| Chunking | 512-char chunks | Natural sections (font heuristics) |
| Storage | Qdrant (external gRPC) | SQLite (local file) |
