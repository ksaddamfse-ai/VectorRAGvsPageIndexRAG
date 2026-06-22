# Compare RAG — Design Spec

**Status:** Implemented

## Overview

Side-by-side comparison endpoint that runs Vector RAG and PageIndex RAG queries in parallel on the same document + question. Returns both results in a single response with timing information. Uses `groupName` for PageIndex and `collectionName` for Vector RAG.

## Endpoints

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/compare/query` | Run both RAG queries in parallel, return side-by-side |

## Query Flow

1. Client sends `{ question, provider, model, topK, groupName, collectionName }` via GET query params
2. Constructs `RagQueryRequest` and `PageIndexQueryRequest` from common params
3. Fires both queries in parallel via `Task.WhenAll` with `SafeRun` wrapper for error isolation
4. Returns `CompareQueryResponse(Rag, PageIndex, TotalTimeMs)` with timing info per service

## Response Structure

```csharp
CompareQueryResponse(
    Rag: RagResult(Answer, CollectionName, Chunks, Error?, TimeMs),
    PageIndex: PageIndexResult(Answer, Citations, Error?, TimeMs),
    TotalTimeMs: long
)
```

- If a service fails, `Error` field contains the error message, `Answer` falls back to error text
- If no collection specified, defaults to `"documents"` for Vector RAG result display
- One failure does not block the other

## Swagger Dropdown Support

- `ProviderDropdownFilter` (IParameterFilter) matches query param names case-insensitively so provider/model params show dropdowns in Swagger
- `ProviderModelSchemaFilter` (ISchemaFilter) sets defaults: provider = "GoogleAI", model = "gemini-3.5-flash", topK = 2, collectionName = "PDFs", groupName = "PDFs"

## Files

| File | Purpose |
|---|---|
| `DTOs/CompareQueryRequest.cs` | `(Question, Provider, Model, TopK, GroupName, CollectionName)` |
| `DTOs/CompareQueryResponse.cs` | `(Rag: RagResult, PageIndex: PageIndexResult, TotalTimeMs)` |
| `Controllers/CompareController.cs` | Single GET endpoint with parallel execution |

## Error Handling

- If `question` or `groupName` missing → 400
- If Qdrant is down → Vector RAG returns error gracefully (caught by `SafeRun`)
- If doc not found in SQLite → PageIndex returns `"Document not found."` in answer field
- Parallel tasks: one failure does not block the other
