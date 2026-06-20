# Vector RAG vs Page Index RAG

ASP.NET Core 10 Web API comparing Vector RAG and Page Index RAG strategies.

## Current: Vector RAG

PDF ingestion → chunking → embedding → Qdrant vector store → query with LLM context.

### Endpoints

| Method | Path | Description |
|---|---|---|
| `POST` | `/api/rag/documents` | Ingest PDF: chunk, embed, store in Qdrant |
| `POST` | `/api/rag/query` | Ask a question: embed → search → LLM answer |
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

### Architecture

```
Ingest:
  PDF → PdfPig → SK TextChunker → IEmbeddingGenerator → Qdrant

Query:
  Question → IEmbeddingGenerator → Qdrant Search → IChatClient → Answer
```

- Vector store access via [Qdrant.Client](https://www.nuget.org/packages/Qdrant.Client)
- Embedding & Chat via [Microsoft.Extensions.AI](https://www.nuget.org/packages/Microsoft.Extensions.AI)
- Chunking via [Semantic Kernel](https://www.nuget.org/packages/Microsoft.SemanticKernel.Core) TextChunker
- PDF parsing via [PdfPig](https://www.nuget.org/packages/PdfPig)

### Provider Switching

| Config Key | Change |
|---|---|
| `ActiveProvider` | Qdrant ↔ AzureAISearch (add connector + NuGet) |
| `ActiveEmbeddingProvider` | NvidiaNim ↔ Ollama ↔ custom |
| Provider in `/api/rag/query` | OpenRouter ↔ NvidiaNim ↔ FoundryLocal |

Vector size is derived from the active embedding model's output at runtime. No config duplication.

### Learning Journal

Project learnings and decisions are tracked in `LEARNINGS.md` (gitignored).
