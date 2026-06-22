# Vector RAG vs Page Index RAG

[![CI](https://github.com/saddam/VectorRAGvsPageIndexRAG/actions/workflows/ci.yml/badge.svg)](https://github.com/saddam/VectorRAGvsPageIndexRAG/actions/workflows/ci.yml)

ASP.NET Core 10 Web API comparing Vector RAG and Page Index RAG strategies.

## How Each Approach Works

### Vector RAG

PDF text is chunked, embedded into vectors, and stored in Qdrant. Queries embed the question, find similar chunks via cosine search, and send them as context to an LLM.

### Page Index RAG (Vectorless)

PDF structure is parsed deterministically using font heuristics (no LLM for layout). An LLM generates summaries for each node. The hierarchical tree is stored in SQLite. Queries use the LLM to navigate the tree skeleton, fetch relevant node texts, and answer.

## Flow Diagrams

### Vector RAG Ingestion

```mermaid
flowchart LR
    A[PDF] --> B[PdfPig\nExtract Text]
    B --> C[TextChunker\nSplit Paragraphs]
    C --> D[Embedder\nBatch Embed]
    D --> E[(Qdrant\nVectors + Metadata)]
```

### Vector RAG Query

```mermaid
flowchart LR
    A[Question] --> B[Embedder\nEmbed Question]
    B --> C[Qdrant\nCosine Search]
    C --> D[PackChunks\nToken Budget]
    D --> E[LLM\nAnswer with Context]
```

### Page Index Ingestion

```mermaid
flowchart LR
    A[PDF] --> B[PdfStructureParser\nFont Heuristics]
    B --> C[DocumentTreeBuilder\nLLM Summaries]
    C --> D[(SQLite\ntree_json + node_texts)]
```

### Page Index Query

```mermaid
flowchart LR
    A[Question] --> B[LLM\nNavigate Skeleton]
    B --> C[SQLite\nFetch Node Texts]
    C --> D[LLM\nAnswer with Context]
```

### Compare Endpoint

```mermaid
flowchart LR
    A[Question] --> B[Vector RAG]
    A --> C[PageIndex RAG]
    B --> D[(Qdrant)]
    B --> E[LLM]
    C --> F[(SQLite)]
    C --> G[LLM x2]
    D --> H[Response\n+ Timing]
    E --> H
    F --> H
    G --> H
```

## Endpoints

| Method | Path | Query Params | Description |
|--------|------|--------------|-------------|
| `POST` | `/api/rag/documents` | `collectionName` (default: `PDFs`) | Ingest PDF: extract text, chunk, embed, store in Qdrant |
| `GET` | `/api/rag/query` | `question`, `provider`, `model`, `topK` (default: 2), `collectionName` (default: `PDFs`) | Embed question, cosine search, LLM answer |
| `POST` | `/api/pageindex/documents` | `provider`, `model`, `groupName` (default: `PDFs`) | Deterministic PDF parse + LLM summaries, store in SQLite |
| `GET` | `/api/pageindex/query` | `question`, `provider`, `model`, `groupName` | LLM navigates tree skeleton, fetches sections, answers |
| `GET` | `/api/compare/query` | `question`, `provider`, `model`, `topK` (default: 2), `groupName`, `collectionName` | Run both RAG strategies side-by-side with timing |
| `POST` | `/api/chat` | `question`, `provider` (default: `OpenRouter`), `model` (default: `openrouter/free`) | Direct LLM chat (no RAG pipeline) |

## Quick Start

```bash
# Start Qdrant
docker run -p 6333:6333 -p 6334:6334 qdrant/qdrant

# Set API keys via user secrets
dotnet user-secrets set "ProviderRegistry:OpenCode:ApiKey" "sk-..."
dotnet user-secrets set "EmbeddingRegistry:NvidiaNim:ApiKey" "nvapi-..."

# Run
dotnet run
```

Open Swagger UI at `https://localhost:51095/swagger`.

## Test PDFs

| File | Pages | Content |
|------|-------|---------|
| `test-pdfs/technical-report.pdf` | 10 | CloudSync API docs (sections, tables, code samples) |
| `test-pdfs/resume.pdf` | 5 | Dr. Sarah Chen ML engineer CV (skills, experience) |
| `test-pdfs/legal-contract.pdf` | 9 | Enterprise software license (clauses, GDPR, termination) |

Regenerate with: `dotnet run --project Tools/PdfGenerator/VectorRAGvsPageIndexRAG.Tools.PdfGenerator.csproj`

## Curl Examples

**RAG Ingest:**
```bash
curl -X 'POST' \
  'https://localhost:51095/api/rag/documents?collectionName=PDFs' \
  -H 'accept: text/plain' \
  -H 'Content-Type: multipart/form-data' \
  -F 'file=@test-pdfs/technical-report.pdf;type=application/pdf'
```

**PageIndex Ingest:**
```bash
curl -X 'POST' \
  'https://localhost:51095/api/pageindex/documents?provider=OpenCode&model=deepseek-v4-flash-free&groupName=PDFs' \
  -H 'accept: text/plain' \
  -H 'Content-Type: multipart/form-data' \
  -F 'file=@test-pdfs/technical-report.pdf;type=application/pdf'
```

**Compare Query:**
```bash
curl -X 'GET' \
  'https://localhost:51095/api/compare/query?question=What%20is%20the%20CloudSync%20API%20rate%20limit%3F&provider=OpenCode&model=deepseek-v4-flash-free&groupName=PDFs&collectionName=PDFs' \
  -H 'accept: text/plain'
```

## Results

Run against `test-pdfs/` using OpenCode / `deepseek-v4-flash-free`:

| Question | Vector RAG (ms) | PageIndex (ms) | Vector Answer | PageIndex Answer |
|----------|----------------:|---------------:|---------------|------------------|
| What is the CloudSync API rate limit? | 10,306 | 160,873 | Free: 100/min, Pro: 1,000/min, Enterprise: 10,000/min, Premium: 50,000/min | 1000 requests per minute per client ID |
| What programming languages does the candidate know? | 4,780 | 23,779 | Python, Java, C++, R, SQL, JavaScript, Go | Context does not contain information about programming languages |
| What are the termination clauses in this contract? | 19,351 | 86,677 | Section 5.1: Agreement continues for Subscription Term. Section 5.2: Either party may terminate for cause upon 30 days notice | Section 5.1: Agreement commences on Effective Date. Section 5.2: Either party may terminate for material breach |
| Compare performance metrics across all sections | 63,762 | 174,437 | ML Engineer: 95% accuracy, 40% latency reduction. Data Scientist: 89% AUC churn model | Rate Limiting: 100-50,000 req/min by tier. Throughput: 10M+ daily users. Latency: 40% reduction via TensorRT |
| What is the meaning of life? | 5,214 | 4,290 | No information about the meaning of life in context | No relevant sections found |

### Key Observations

| Aspect | Vector RAG | Page Index RAG |
|--------|------------|----------------|
| **Ingestion speed** | Fast (~1-2s per PDF) | Slow (~90-215s per PDF, LLM per node) |
| **Query latency** | 5-64s (embed + search + LLM) | 4-175s (2 LLM calls: navigate + answer) |
| **Factual accuracy** | Good — retrieves exact chunks | Good — navigates to correct sections |
| **Multi-document queries** | Struggles (chunks from all docs mixed) | Better (tree structure preserved per doc) |
| **Out-of-scope handling** | Gracefully says "no info" | Gracefully says "no relevant sections" |
| **Infrastructure** | Requires Qdrant + embedding API | SQLite only (zero external infra) |
| **Embedding dependency** | Yes (NvidiaNim/external API) | No embeddings needed |

## Configuration

| Section | Purpose |
|---------|---------|
| `ProviderRegistry` | Chat LLM providers (OpenRouter, NvidiaNim, FoundryLocal, Ollama, OpenCode, GoogleAI) |
| `EmbeddingRegistry` | Embedding models (NvidiaNim, Ollama), `ActiveEmbeddingProvider` selects active |
| `VectorStoreRegistry` | Vector DB (Qdrant, AzureAISearch), `ActiveProvider` selects active |
| `PageIndex` | SQLite path (`DbPath: "pageindex.db"`) |
| `ProviderContextWindows` | Context window sizes per provider/model for token budgeting |

## Design Decisions

- **Deterministic PDF parsing**: Font size heuristics (>=1.2x median = header), vertical gaps (>=1.5x line height = paragraph). LLM only generates summaries.
- **SQLite over a second vector DB**: PageIndex uses SQLite — zero external infra. Tree navigation via LLM reasoning, not similarity search.
- **MEVD abstraction**: Uses `VectorStore` from Microsoft.Extensions.VectorData — swapping vector DBs means changing one DI registration, not rewriting services.
- **gRPC port 6334**: Qdrant gRPC is on 6334, not 6333 (HTTP REST).
- **Vector size derived from embedding output**: No config duplication — embedding model determines vector size at runtime.
- **Token budgeting**: `PackChunks()` uses greedy fill with char-count estimation (`text.Length / 4`) to fit context into model window.
