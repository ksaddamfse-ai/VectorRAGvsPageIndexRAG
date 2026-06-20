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
```

## Key Services

| Interface | Implementation | Responsibility |
|---|---|---|
| `IRagIngestionService` | `RagIngestionService` | PDF → chunk → embed → Qdrant |
| `IRagQueryService` | `RagQueryService` | question → embed → Qdrant search → LLM answer |
| `IDocumentProcessor` | `DocumentProcessor` | PDF text extraction via PdfPig |
| `IChatClientFactory` | `ChatClientFactory` | Keyed IChatClient resolution |

## Key Design Decisions

- Embedding model is a DI singleton controlled by `ActiveEmbeddingProvider` in config
- Vector size derived from actual embedding output at runtime (not in config)
- Qdrant collection auto-created with correct vector size on first ingest
- Reuses `IChatClientFactory` from existing infrastructure
- Qdrant.Client directly (no IVectorStore abstraction — YAGNI until second provider)
- Port 6334 for gRPC (not 6333 which is HTTP REST)

## DI Registration (Program.cs)

- Keyed `IChatClient` per provider/model from `ProviderRegistry`
- Default `IEmbeddingGenerator` singleton from `EmbeddingRegistry.ActiveEmbeddingProvider`
- `QdrantClient` singleton from `VectorStoreRegistry.ActiveProvider`
- Application services registered as singletons via interfaces

## Config

- `appsettings.json` has ProviderRegistry, EmbeddingRegistry, VectorStoreRegistry
- API keys via dotnet user-secrets or appsettings

## Build

- Target: net10.0
- `dotnet build` to compile, `dotnet run` to serve at localhost:51095
