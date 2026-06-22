# PageIndex Group Query — Design Spec

**Date:** 2026-06-21
**Goal:** Enable PageIndex to group documents (e.g., resumes) under a shared group name, then query across all documents in the group with a combined skeleton for cross-document comparison.

---

## 1. Motivation

Vector RAG returns top-K chunks from across documents — fragments that may miss entire candidates. PageIndex's tree structure preserves full document context. By grouping documents under a `groupName` and querying the group, the LLM sees the complete hierarchy of all documents, enabling accurate cross-document comparison (e.g., "which candidate is most skilled?").

---

## 2. Config Defaults

Both RAG (`collectionName`) and PageIndex (`groupName`) default to `"PDFs"`.

- `VectorStoreRegistryEntry.DefaultCollectionName` = `"PDFs"` (already done)
- `appsettings.json` — `DefaultCollectionName` under Qdrant/AzureAISearch = `"PDFs"` (already done)
- `ProviderModelSchemaFilter` — Swagger default for `collectionName` = `"PDFs"` (already done)
- PageIndex's `groupName` default = `"PDFs"` (new)

---

## 3. Data Model

### DocumentTree

Add `GroupName` property:
```csharp
// Models/DocumentTree.cs
[JsonIgnore]
public string GroupName { get; set; } = "";
```

### SQLite Schema

```sql
ALTER TABLE document_trees ADD COLUMN group_name TEXT NOT NULL DEFAULT '';
CREATE INDEX IF NOT EXISTS idx_group_name ON document_trees(group_name);
```

`IPageIndexDatabase` gets a new method:
```csharp
Task<List<(string DocId, string TreeJson)>> GetDocumentTreesByGroupAsync(string groupName);
```

---

## 4. PageIndex Ingestion — groupName

### Controller

`PageIndexController.Ingest` gets the param:
```
POST /api/pageindex/documents?groupName=PDFs
```

### Service

`PageIndexService.IngestAsync` accepts `string? groupName = "PDFs"`, sets `tree.GroupName`.

### Database

`InsertDocumentTreeAsync` stores `group_name` alongside existing fields.

---

## 5. PageIndex Group Query — Combined Skeleton

### New Query Mode

Existing `GET /api/pageindex/query` accepts either `docId` or `groupName`:
```
GET /api/pageindex/query?groupName=PDFs&question=most skilled candidate?
```

### Flow

1. Fetch all trees where `group_name = groupName` from SQLite
2. Build a combined skeleton with source filename prefixes:

```
Document: resume_john.pdf
  - sec_01: Summary — 5 years SWE at Google
  - sub_01a: Skills — Python, Kubernetes, AWS

Document: resume_jane.pdf
  - sec_01: Summary — 3 years ML at Meta
  - sub_01a: Skills — PyTorch, TensorFlow, GCP
```

3. LLM picks node_ids from across all documents
4. Fetch the selected node texts (all documents)
5. LLM answers with comparison

### Implementation

`PageIndexService.GetSkeletonAsync` — new method that builds the combined skeleton string from multiple trees.

`IPageIndexDatabase.GetDocumentTreesByGroupAsync` — fetches all trees for a group.

---

## 6. Compare Endpoint

`CompareController` — add `groupName` param. Creates `PageIndexQueryRequest` with `groupName` instead of `docId`.

`PageIndexQueryRequest` — add optional `GroupName` field (default `""`). Query service checks: if `GroupName` is set, use group query; else use single-doc query.

---

## 7. Files Changed

| File | Change |
|------|--------|
| `Models/DocumentTree.cs` | Add `GroupName` property |
| `Services/Interfaces/IPageIndexDatabase.cs` | Add `GetDocumentTreesByGroupAsync` |
| `Services/SqlitePageIndexDatabase.cs` | Schema migration + new query method |
| `Services/PageIndexService.cs` | Add `groupName` to ingest, add group query logic |
| `Services/Interfaces/IPageIndexService.cs` | Update `IngestAsync` signature |
| `Controllers/PageIndexController.cs` | Add `groupName` param to ingest + query |
| `DTOs/PageIndexQueryRequest.cs` | Add optional `GroupName` field |
| `Controllers/CompareController.cs` | Add `groupName` param |
| `Settings/VectorStoreRegistryEntry.cs` | Default = `"PDFs"` (done) |
| `appsettings.json` | Default = `"PDFs"` (done) |
| `Filters/ProviderModelSchemaFilter.cs` | Default = `"PDFs"` (done) |
