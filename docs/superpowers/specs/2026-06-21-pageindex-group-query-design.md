# PageIndex Group Query — Design Spec

**Date:** 2026-06-21
**Status:** Implemented
**Goal:** Enable PageIndex to group documents under a shared group name, then query across all documents in the group with a combined skeleton for cross-document comparison.

---

## 1. Motivation

Vector RAG returns top-K chunks from across documents — fragments that may miss entire candidates. PageIndex's tree structure preserves full document context. By grouping documents under a `groupName` and querying the group, the LLM sees the complete hierarchy of all documents, enabling accurate cross-document comparison (e.g., "which candidate is most skilled?").

---

## 2. Config Defaults

Both RAG (`collectionName`) and PageIndex (`groupName`) default to `"PDFs"`.

- `VectorStoreRegistryEntry.DefaultCollectionName` = `"PDFs"`
- `appsettings.json` — `DefaultCollectionName` under Qdrant/AzureAISearch = `"PDFs"`
- `ProviderModelSchemaFilter` — Swagger default for `collectionName` and `groupName` = `"PDFs"`

---

## 3. Data Model

### DocumentTree

`GroupName` property added:
```csharp
// Models/DocumentTree.cs
[JsonIgnore]
public string GroupName { get; set; } = "";
```

### SQLite Schema

```sql
CREATE TABLE IF NOT EXISTS document_trees (
    doc_id TEXT PRIMARY KEY,
    file_name TEXT NOT NULL,
    tree_json TEXT NOT NULL,
    group_name TEXT NOT NULL DEFAULT '',
    created_at TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE INDEX IF NOT EXISTS idx_group_name ON document_trees(group_name);
```

`IPageIndexDatabase` method:
```csharp
Task<List<(string DocId, string TreeJson)>> GetDocumentTreesByGroupAsync(string groupName);
```

---

## 4. PageIndex Ingestion — groupName

### Controller

`PageIndexController.Ingest` accepts `groupName`:
```
POST /api/pageindex/documents?groupName=PDFs
```

### Service

`PageIndexService.IngestAsync` accepts `string groupName = "PDFs"`, sets `tree.GroupName`.

### Database

`InsertDocumentTreeAsync` stores `group_name` alongside existing fields.

---

## 5. PageIndex Group Query — Combined Skeleton

### Query Mode

`GET /api/pageindex/query` uses `groupName` (not `docId`):
```
GET /api/pageindex/query?groupName=PDFs&question=most skilled candidate?
```

### Flow

1. `BuildCombinedSkeletonAsync(groupName)` fetches all trees where `group_name = groupName`
2. Builds combined skeleton with source filename prefixes:
```
Document: resume_john.pdf
  - node_001: Section Title — Summary text

Document: resume_jane.pdf
  - node_001: Section Title — Summary text
```
3. LLM picks node_ids from across all documents
4. `GetNodeTextsByNodeIdsAsync` fetches selected node texts (all documents)
5. LLM answers with comparison

### Implementation

- `PageIndexService.BuildCombinedSkeletonAsync` — builds combined skeleton from multiple trees
- `IPageIndexDatabase.GetDocumentTreesByGroupAsync` — fetches all trees for a group
- `IPageIndexDatabase.GetNodeTextsByNodeIdsAsync` — fetches node texts by node_id across all docs

---

## 6. Compare Endpoint

`CompareController` uses `groupName` for PageIndex and `collectionName` for Vector RAG:
```
GET /api/compare/query?question=X&groupName=PDFs&collectionName=PDFs
```

`CompareQueryRequest` has `GroupName` and `CollectionName` fields. Compare service creates `PageIndexQueryRequest` with `GroupName`.

---

## 7. Files

| File | Change |
|------|--------|
| `Models/DocumentTree.cs` | Added `GroupName` property (JsonIgnore) |
| `Services/Interfaces/IPageIndexDatabase.cs` | Added `GetDocumentTreesByGroupAsync`, `GetNodeTextsByNodeIdsAsync` |
| `Services/SqlitePageIndexDatabase.cs` | Schema with `group_name` column + index, new query methods |
| `Services/PageIndexService.cs` | `groupName` param on ingest, group query via `BuildCombinedSkeletonAsync` |
| `Services/Interfaces/IPageIndexService.cs` | `IngestAsync` signature with `groupName` |
| `Controllers/PageIndexController.cs` | `groupName` param on ingest + query (GET) |
| `DTOs/PageIndexQueryRequest.cs` | `GroupName` field (default "PDFs") |
| `Controllers/CompareController.cs` | `groupName` + `collectionName` params |
| `DTOs/CompareQueryRequest.cs` | `GroupName` + `CollectionName` fields |
| `Filters/ProviderModelSchemaFilter.cs` | Defaults for `groupName` and `collectionName` = "PDFs" |
