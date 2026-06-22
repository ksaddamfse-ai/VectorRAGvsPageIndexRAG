# Vector RAG vs Page Index RAG — Agent Context

## Project

ASP.NET Core 10 Web API. Compares Vector RAG and Page Index RAG.

## Architecture (Clean Architecture)

```
Controllers/          — Presentation layer
DTOs/                 — Application DTOs (request/response models)
Models/               — Domain entities
Services/             — Application services + interfaces
  Interfaces/         — Service abstractions
Settings/             — Configuration models
Filters/              — Swagger filters
Tools/PdfGenerator/   — Test PDF generator (standalone console app)
test-pdfs/            — Sample PDFs for testing (commit these)
```

## Key Services

| Interface | Implementation | Responsibility |
|---|---|---|
| `IRagIngestionService` | `RagIngestionService` | PDF → chunk → embed → Qdrant |
| `IRagQueryService` | `RagQueryService` | question → embed → Qdrant search → LLM answer |
| `IDocumentProcessor` | `DocumentProcessor` | PDF text extraction via PdfPig |
| `IChatClientFactory` | `ChatClientFactory` | Keyed IChatClient resolution |
| `PdfStructureParser` | `PdfStructureParser` | Deterministic PDF structure via font heuristics |
| `DocumentTreeBuilder` | `DocumentTreeBuilder` | Parser + LLM summaries → DocumentTree |

## Key Design Decisions

- Embedding model is a DI singleton controlled by `ActiveEmbeddingProvider` in config
- Vector size derived from actual embedding output at runtime (not in config)
- Qdrant collection auto-created with correct vector size on first ingest
- Reuses `IChatClientFactory` from existing infrastructure
- Qdrant.Client via MEVD abstraction (`VectorStore` base class from Microsoft.Extensions.VectorData)
- Port 6334 for gRPC (not 6333 which is HTTP REST)
- **Deterministic PDF parsing**: Font size heuristics (≥1.2× median = header), vertical gaps (≥1.5× line height = paragraph). LLM only generates summaries.
- **Endpoint simplification**: PageIndex/Compare use `GroupName` (default "PDFs"), not `DocId`
- **Swagger defaults**: Provider = "GoogleAI", Model = "gemini-3.5-flash"
- **GoogleAI model**: Must pass `($"models/{chatModel}")` to `GenerativeAIChatClient` constructor

## DI Registration (Program.cs)

- Keyed `IChatClient` per provider/model from `ProviderRegistry`
- Default `IEmbeddingGenerator` singleton from `EmbeddingRegistry.ActiveEmbeddingProvider`
- `QdrantClient` singleton from `VectorStoreRegistry.ActiveProvider`
- `PdfStructureParser` singleton
- Application services registered as singletons via interfaces

## Config

- `appsettings.json` has ProviderRegistry, EmbeddingRegistry, VectorStoreRegistry, ProviderContextWindows
- API keys via dotnet user-secrets or appsettings
- Default collection: "PDFs" (RAG and PageIndex)

## Endpoints

| Method | Path | Purpose |
|--------|------|---------|
| POST | `/api/rag/documents` | Ingest PDF to Vector RAG |
| GET | `/api/rag/query` | Query Vector RAG |
| POST | `/api/pageindex/documents` | Ingest PDF to PageIndex |
| GET | `/api/pageindex/query` | Query PageIndex (by groupName) |
| GET | `/api/compare/query` | Compare both RAG approaches |

## Test PDFs

Always use PDFs from `test-pdfs/` when testing endpoints:

| File | Pages | Content |
|------|-------|---------|
| `test-pdfs/technical-report.pdf` | 10 | CloudSync API docs (sections, tables, code) |
| `test-pdfs/resume.pdf` | 5 | Dr. Sarah Chen ML engineer CV (skills, experience) |
| `test-pdfs/legal-contract.pdf` | 9 | Enterprise software license (clauses, GDPR) |

Generated via: `dotnet run --project Tools/PdfGenerator/VectorRAGvsPageIndexRAG.Tools.PdfGenerator.csproj`

## Curl Examples

Always use PDFs from `test-pdfs/` when testing endpoints.

**RAG Ingest (Vector RAG):**
```bash
curl -X 'POST' \
  'https://localhost:51095/api/rag/documents?collectionName=PDFs' \
  -H 'accept: text/plain' \
  -H 'Content-Type: multipart/form-data' \
  -F 'file=@test-pdfs/technical-report.pdf;type=application/pdf'
```

**PageIndex Ingest (Deterministic PDF parser + LLM summaries):**
```bash
curl -X 'POST' \
  'https://localhost:51095/api/pageindex/documents?provider=OpenCode&model=deepseek-v4-flash-free&groupName=PDFs' \
  -H 'accept: text/plain' \
  -H 'Content-Type: multipart/form-data' \
  -F 'file=@test-pdfs/technical-report.pdf;type=application/pdf'
```

**Compare Query (runs both approaches side-by-side):**
```bash
curl -X 'GET' \
  'https://localhost:51095/api/compare/query?question=What%20is%20the%20CloudSync%20API%20rate%20limit%3F&provider=OpenCode&model=deepseek-v4-flash-free&groupName=PDFs&collectionName=PDFs' \
  -H 'accept: text/plain'
```

## Build

- Target: net10.0
- `dotnet build VectorRAGvsPageIndexRAG.sln` to compile all projects
- `dotnet test Tests\VectorRAGvsPageIndexRAG.Tests\VectorRAGvsPageIndexRAG.Tests.csproj` to run tests
- `dotnet run` to serve at localhost:51095
- Test project lives under `Tests\` to avoid Web SDK glob picking up test `.cs` files; main `.csproj` has `<Compile Remove="Tests\**\*">`
- PDF generator lives under `Tools\` — excluded from main project via `<Compile Remove="Tools\**\*">`

## Learnings

Record all provider integration decisions and design choices in LEARNINGS.md — why a specific NuGet package was chosen, what alternatives existed, and any config/code implications. This keeps design rationale discoverable and prevents repeated deliberation.
