# Vector RAG vs Page Index RAG — Agent Context

## Project

ASP.NET Core 10 Web API. Compares Vector RAG and Page Index RAG.

## Architecture

- `Services/RagIngestionService.cs` — PDF → chunk → embed → Qdrant
- `Services/RagQueryService.cs` — question → embed → Qdrant search → LLM answer
- `Services/DocumentProcessor.cs` — PDF text extraction via PdfPig
- `Controllers/RagController.cs` — POST /api/rag/documents + /api/rag/query
- `Program.cs` — DI: keyed IChatClient, default IEmbeddingGenerator, QdrantClient singleton
- `Microsoft.Extensions.AI` for embeddings + chat abstraction
- `Qdrant.Client` for vector store (gRPC, port 6334)
- `Microsoft.SemanticKernel.Text.TextChunker` for document splitting

## Key Design Decisions

- Embedding model is a DI singleton controlled by `ActiveEmbeddingProvider` in config
- Vector size derived from actual embedding output at runtime (not in config)
- Qdrant collection auto-created with correct vector size on first ingest
- Reuses `IChatClientFactory` from existing infrastructure
- No IVectorStore abstraction — Qdrant.Client directly (YAGNI until second provider)
- Port 6334 for gRPC (not 6333 which is HTTP REST)

## Config

- `appsettings.json` has ProviderRegistry, EmbeddingRegistry, VectorStoreRegistry
- API keys via dotnet user-secrets or appsettings

## Build

- Target: net10.0
- `dotnet build` to compile, `dotnet run` to serve at localhost:51095
