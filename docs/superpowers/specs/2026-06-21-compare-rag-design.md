# Compare RAG — Design Spec

## Overview

Side-by-side comparison endpoint that runs Vector RAG and PageIndex RAG queries in parallel on the same document + question. Returns both results in a single response. Changes existing query endpoints from POST+[FromBody] to GET+[FromQuery] for Swagger dropdown support.

## Endpoints

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/compare/query` | Run both RAG queries in parallel, return side-by-side |

## Changes to Existing Endpoints

| Method | Route | Change |
|---|---|---|
| GET | `/api/rag/query` | Was POST+[FromBody], now GET+[FromQuery] |
| GET | `/api/pageindex/query` | Was POST+[FromBody], now GET+[FromQuery] |

## Query Flow

1. Client sends `{ docId, question, provider, model, topK }` via GET query params
2. Constructs `RagQueryRequest` and `PageIndexQueryRequest` from common params
3. Fires both queries in parallel via `Task.WhenAll`
4. Returns `{ rag: { answer, chunks }, pageIndex: { answer, citations } }`

## Swagger Dropdown Support

- `ProviderDropdownFilter` (IParameterFilter) matches query param names case-insensitively so provider/model params show dropdowns in Swagger
- Both param naming conventions work: `provider` and `Provider`

## New Files

| File | Purpose |
|---|---|
| `DTOs/CompareQueryRequest.cs` | `(DocId, Question, Provider, Model, TopK)` |
| `DTOs/CompareQueryResponse.cs` | `(Rag: { Answer, Chunks }, PageIndex: { Answer, Citations })` |
| `Controllers/CompareController.cs` | Single GET endpoint |

## Modified Files

| File | Change |
|---|---|
| `Controllers/RagController.cs` | Query endpoint: POST+[FromBody] → GET+[FromQuery], use `RagQueryRequest` |
| `Controllers/PageIndexController.cs` | Query endpoint: POST+[FromBody] → GET+[FromQuery], use `PageIndexQueryRequest` |
| `Filters/ProviderDropdownFilter.cs` | Case-insensitive param name matching |
| `Filters/ProviderModelSchemaFilter.cs` | Remove body-schema handling, now GET params |

## Error Handling

- If `docId` or `question` missing → 400
- If Qdrant is down → Vector RAG returns empty result gracefully (caught by service)
- If doc not found in SQLite → PageIndex returns `"Document not found."` in answer field
- Parallel tasks: one failure does not block the other
