# Vector RAG vs Page Index RAG

[![CI](https://github.com/saddam/VectorRAGvsPageIndexRAG/actions/workflows/ci.yml/badge.svg)](https://github.com/saddam/VectorRAGvsPageIndexRAG/actions/workflows/ci.yml)

ASP.NET Core 10 Web API comparing Vector RAG and Page Index RAG strategies.

## Vector RAG

PDF ingestion → chunking → embedding → Qdrant vector store → query with LLM context.

## PageIndex RAG (Vectorless)

PDF ingestion → LLM builds hierarchical document tree → stored in SQLite → query navigates tree via LLM reasoning.

### Endpoints

| Method | Path | Description |
|---|---|---|
| `POST` | `/api/rag/documents` | Vector: ingest PDF, chunk, embed, store in Qdrant |
| `GET` | `/api/rag/query?question=&provider=&model=` | Vector: embed question → search → LLM answer |
| `POST` | `/api/pageindex/documents?provider=&model=` | PageIndex: ingest PDF, build tree via LLM, store in SQLite |
| `GET` | `/api/pageindex/query?docId=&question=&provider=&model=` | PageIndex: LLM navigates tree → fetch sections → LLM answer |
| `GET` | `/api/compare/query` | Compare both strategies side-by-side with same question |
| `POST` | `/api/chat` | Direct LLM chat (no RAG) |

### Quick Start

```bash
# Start Qdrant
docker run -p 6333:6333 -p 6334:6334 qdrant/qdrant

# Set API keys via user secrets (optional, for non-local providers)
dotnet user-secrets set "ProviderRegistry:OpenRouter:ApiKey" "sk-..."
dotnet user-secrets set "EmbeddingRegistry:NvidiaNim:ApiKey" "nvapi-..."

# Run
dotnet run
```

Open Swagger UI at `https://localhost:51095/swagger`.

### Configuration

**`appsettings.json`:**

```json
{
  "ProviderRegistry": {
    // Chat LLM providers: OpenRouter, NvidiaNim, FoundryLocal (local)
  },
  "EmbeddingRegistry": {
    // Embedding models: NvidiaNim, Ollama
    "ActiveEmbeddingProvider": "NvidiaNim"
  },
  "VectorStoreRegistry": {
    // Vector database: Qdrant, AzureAISearch (future)
    "ActiveProvider": "Qdrant"
  }
}
```

### Vector RAG Architecture

```
Ingest:  PDF → PdfPig → SK TextChunker → IEmbeddingGenerator → Qdrant
Query:   Question → IEmbeddingGenerator → Qdrant Search → IChatClient → Answer
```

- Vector store: [Qdrant.Client](https://www.nuget.org/packages/Qdrant.Client) (gRPC, port 6334)
- Embedding & Chat: [Microsoft.Extensions.AI](https://www.nuget.org/packages/Microsoft.Extensions.AI)
- Chunking: [Semantic Kernel](https://www.nuget.org/packages/Microsoft.SemanticKernel.Core) TextChunker
- PDF: [PdfPig](https://www.nuget.org/packages/PdfPig)

### PageIndex RAG Architecture

```
Ingest:  PDF → PdfPig → LLM tree builder → SQLite (tree_json + node_texts)
Query:   Q + docId → LLM navigates skeleton → fetch node texts → LLM answer
```

- Storage: [Microsoft.Data.Sqlite](https://www.nuget.org/packages/Microsoft.Data.Sqlite) (local, no external infra)
- Tree building: single LLM call per doc creates hierarchical section tree
- Retrieval: LLM reasons over titles+summaries, fetches only matching leaf texts
- Provider defaults: NvidiaNim / meta/llama-3.3-70b-instruct

### Configuration

| Section | Purpose |
|---|---|
| `ProviderRegistry` | Chat LLM providers (OpenRouter, NvidiaNim, FoundryLocal) |
| `EmbeddingRegistry` | Embedding models (NvidiaNim, Ollama), `ActiveEmbeddingProvider` selects active |
| `VectorStoreRegistry` | Vector DB (Qdrant, AzureAISearch), `ActiveProvider` selects active |
| `PageIndex` | SQLite path (`DbPath: "pageindex.db"`) |

Vector size is derived from the active embedding model's output at runtime. No config duplication.

## Design Decisions

### SQLite over a second vector DB

PageIndex RAG uses SQLite (via Microsoft.Data.Sqlite) for hierarchical document trees � no vector store needed. A second vector database (e.g., pgvector) would add operational complexity with zero benefit since the retrieval strategy is fundamentally different: tree navigation via LLM reasoning, not similarity search. SQLite also means zero external infrastructure for this mode.

### No IVectorStore abstraction (yet)

The original plan used IVectorStore from Microsoft.Extensions.VectorData.Abstractions, but the Qdrant connector NuGet package was unavailable. Using Qdrant.Client directly was the pragmatic choice. The abstraction will be added when a second vector store provider is needed � not before (YAGNI).

### gRPC port 6334 (not 6333)

Qdrant exposes two ports: 6333 for HTTP REST and 6334 for gRPC. The gRPC client (Qdrant.Client) connects on 6334. Using 6333 (the REST port) would fail silently.

### Vector size derived from embedding output

Vector size is NOT in config � it's determined at runtime from the actual embedding output. Embedding<float>.Vector.Length gives the real dimension. Config drift risk is eliminated: the embedding model determines the vector size, so duplicating it in config is a maintenance liability. Switching embedding models auto-creates the Qdrant collection with the correct size.

## Results

Run against *[PDF name]* using *[questions]* with GoogleAI/gemini-3.5-flash:

| Question | Vector RAG (ms) | PageIndex RAG (ms) | Vector Answer Quality (1-5) | PageIndex Answer Quality (1-5) | Est. Tokens (Vector) | Est. Tokens (PageIndex) |
|---|---|---|---|---|---|---|
| *TODO* | � | � | � | � | � | � |
| *TODO* | � | � | � | � | � | � |
| *TODO* | � | � | � | � | � | � |

*Results placeholder � run GET /api/compare/query on a real document to fill this table.*