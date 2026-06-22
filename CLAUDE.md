# Vector RAG vs Page Index RAG

ASP.NET Core 10 Web API comparing Vector RAG and Page Index RAG strategies for PDF question-answering.

## Project Structure

```
Controllers/    - RagController, PageIndexController, CompareController, ChatController
DTOs/           - Request/Response records for all endpoints
Models/         - DocumentTree, TreeNode, RagChunk, RagChunkRecord
Services/       - Business logic + interfaces
  Interfaces/   - IChatClientFactory, IDocumentProcessor, IRagIngestionService, IRagQueryService, IPageIndexService, IPageIndexDatabase
Settings/       - ProviderRegistryEntry, VectorStoreRegistryEntry, PageIndexSettings
Filters/        - Swagger filters (ProviderDropdownFilter, ProviderModelSchemaFilter)
Tools/PdfGenerator/ - Test PDF generator (console app)
Tests/          - xUnit tests (separate project)
test-pdfs/      - Sample PDFs for testing
```

## Build & Run

```bash
dotnet restore
dotnet build VectorRAGvsPageIndexRAG.sln
dotnet run
```

Swagger UI at `https://localhost:51095/swagger`.

## Tests

```bash
dotnet test Tests/VectorRAGvsPageIndexRAG.Tests/VectorRAGvsPageIndexRAG.Tests.csproj
```

## Endpoints

| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/rag/documents` | Ingest PDF to Vector RAG (Qdrant) |
| GET | `/api/rag/query` | Query Vector RAG |
| POST | `/api/pageindex/documents` | Ingest PDF to PageIndex (SQLite) |
| GET | `/api/pageindex/query` | Query PageIndex |
| GET | `/api/compare/query` | Run both RAG strategies side-by-side |
| POST | `/api/chat` | Direct LLM chat (no RAG) |

## Architecture

- **Vector RAG**: PDF → text extraction (PdfPig) → chunking (SK TextChunker) → embedding (NvidiaNim) → Qdrant vector search → LLM answer
- **Page Index RAG**: PDF → deterministic structure parsing (font heuristics) → LLM summaries only → SQLite tree storage → LLM navigates tree skeleton → fetches sections → answers
- **Compare endpoint**: Runs both strategies in parallel with timing
- Uses Microsoft.Extensions.AI (MAF) abstractions: `IChatClient`, `IEmbeddingGenerator`, `VectorStore`

## Key Dependencies

- `Microsoft.Extensions.AI` + `Microsoft.Extensions.AI.OpenAI` - LLM/embedding abstractions
- `Microsoft.SemanticKernel.Connectors.Qdrant` - MEVD vector store wrapper
- `Microsoft.SemanticKernel.Core` - TextChunker for chunking
- `PdfPig` - PDF text extraction and font metadata
- `Qdrant.Client` - Qdrant gRPC client
- `Microsoft.Data.Sqlite` - PageIndex storage
- `Swashbuckle.AspNetCore` - Swagger

## Configuration

- `appsettings.json` - ProviderRegistry, EmbeddingRegistry, VectorStoreRegistry, ProviderContextWindows, PageIndex
- API keys via dotnet user-secrets or appsettings
- Default collection name: "PDFs"
- Qdrant port: 6334 (gRPC, not 6333)

## Design Principles

- Deterministic PDF parsing (font heuristics) - LLM only for summaries, not structure
- MEVD abstraction for vector store - swap Qdrant for Azure AI Search via config
- Token budgeting in RAG query - packs chunks by relevance score within model context window
- Group-based queries for PageIndex (not per-document)
- Idempotent chunk ingestion via deterministic SHA256 IDs

## Test PDFs

Regenerate with: `dotnet run --project Tools/PdfGenerator/VectorRAGvsPageIndexRAG.Tools.PdfGenerator.csproj`

- `test-pdfs/technical-report.pdf` - CloudSync API docs (10 pages)
- `test-pdfs/resume.pdf` - ML engineer CV (5 pages)
- `test-pdfs/legal-contract.pdf` - Enterprise software license (9 pages)
