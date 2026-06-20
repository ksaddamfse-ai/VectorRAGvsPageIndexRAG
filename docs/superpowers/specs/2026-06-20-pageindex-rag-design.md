# PageIndex RAG — Design Spec

## Overview

Add a second set of RAG endpoints using the **PageIndex (vectorless RAG)** paradigm. Instead of chunking + embedding + vector search, the system builds a hierarchical document tree via LLM at ingest time, stores it in SQLite, and reasons over the tree at query time to retrieve relevant sections by node ID.

## Endpoints

| Method | Route | Purpose |
|---|---|---|
| POST | `/api/pageindex/documents` | Upload PDF, build tree, store in SQLite, return docId + tree JSON |
| POST | `/api/pageindex/query` | Navigate tree by LLM reasoning → fetch node texts → LLM answer |

## Ingestion Flow

1. Client sends PDF via multipart form
2. `DocumentProcessor.ExtractText()` extracts raw text (PdfPig)
3. `DocumentTreeBuilder.BuildTreeAsync()` sends text to LLM with a prompt that asks it to build a hierarchical JSON tree with section titles, summaries, and full raw text for leaf nodes
4. Tree is stored in SQLite in two tables:
   - `document_trees` — doc_id, file_name, tree_json (lightweight tree with summaries only)
   - `node_texts` — doc_id, node_id, text (raw text per node)
5. Returns `{ docId, fileName, status, pageCount, treeJson }`

## Query Flow

1. Client sends `{ docId, question, provider, model }`
2. Load tree JSON from SQLite
3. **Step 1 — Navigation**: Send only titles + summaries to LLM. LLM reasons over the tree and returns relevant node IDs
4. Fetch raw text from `node_texts` for those node IDs
5. **Step 2 — Answer**: Send raw text + question to LLM for final answer
6. Returns `{ answer, citations: [{ title, pageIndex, text }] }`

## SQLite Schema

```sql
CREATE TABLE IF NOT EXISTS document_trees (
    doc_id TEXT PRIMARY KEY,
    file_name TEXT NOT NULL,
    tree_json TEXT NOT NULL,
    created_at TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS node_texts (
    doc_id TEXT NOT NULL,
    node_id TEXT NOT NULL,
    text TEXT NOT NULL,
    PRIMARY KEY (doc_id, node_id)
);
```

## New Files

| File | Purpose |
|---|---|
| `Settings/PageIndexSettings.cs` | `DbPath` config |
| `Models/DocumentTree.cs` | Root tree model (title, nodeId, summary, children) |
| `Models/TreeNode.cs` | Node model (title, nodeId, summary, text, children) |
| `DTOs/PageIndexIngestionResponse.cs` | `(DocId, FileName, Status, PageCount, TreeJson)` |
| `DTOs/PageIndexQueryRequest.cs` | `(DocId, Question, Provider, Model)` |
| `DTOs/PageIndexQueryResponse.cs` | `(Answer, Citations)` |
| `DTOs/PageIndexCitation.cs` | `(Title, PageIndex, Text)` |
| `Services/Interfaces/IDocumentTreeBuilder.cs` | Tree building interface |
| `Services/Interfaces/IPageIndexService.cs` | Ingestion + query interface |
| `Services/DocumentTreeBuilder.cs` | Calls LLM to build hierarchical tree |
| `Services/PageIndexService.cs` | Orchestrates full pipeline with SQLite |
| `Controllers/PageIndexController.cs` | Two endpoints |

## Modified Files

| File | Change |
|---|---|
| `Program.cs` | Register `PageIndexSettings`, `IDocumentTreeBuilder`, `IPageIndexService` |
| `appsettings.json` | Add `PageIndex:DbPath` section |
| `Filters/ProviderModelSchemaFilter.cs` | Add `PageIndexQueryRequest` handling for Swagger dropdowns |
| `VectorRAGvsPageIndexRAG.csproj` | Add `Microsoft.Data.Sqlite` (10.0.9) |

## Default LLM Provider

Both endpoints default to `NvidiaNim` / `meta/llama-3.3-70b-instruct` with optional override via query param / request body.

## Tradeoffs vs Vector RAG

| Dimension | Vector RAG | PageIndex RAG |
|---|---|---|
| Ingestion | Embedding API per chunk + Qdrant upsert | One LLM call per doc |
| Query | One embedding + one LLM call | Two LLM calls |
| Latency | ~1-3s | ~3-8s |
| Retrieval logic | Semantic similarity | LLM reasoning over structure |
| Chunking | 512-char chunks | Natural sections |
| Storage | Qdrant (external gRPC) | SQLite (local file) |
