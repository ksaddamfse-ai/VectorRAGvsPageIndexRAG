# Vector RAG vs Page Index RAG

ASP.NET Core 10 Web API comparing Vector RAG and Page Index RAG strategies.

## Vector RAG

PDF ingestion → chunking → embedding → Qdrant vector store → query with LLM context.

## PageIndex RAG (Vectorless)

PDF ingestion → LLM builds hierarchical document tree → stored in SQLite → query navigates tree via LLM reasoning.

### Endpoints

| Method | Path | Description |
|---|---|---|
| `POST` | `/api/rag/documents` | Vector: ingest PDF, chunk, embed, store in Qdrant |
| `POST` | `/api/rag/query` | Vector: embed question → search → LLM answer |
| `POST` | `/api/pageindex/documents` | PageIndex: ingest PDF, build tree via LLM, store in SQLite |
| `POST` | `/api/pageindex/query` | PageIndex: LLM navigates tree → fetch sections → LLM answer |
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
