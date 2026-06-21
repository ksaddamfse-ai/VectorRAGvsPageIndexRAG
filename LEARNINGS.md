# Project Learnings

## Design Decisions

### 2026-06-20: Vector Store Abstraction
- Use `VectorStore` from Microsoft.Extensions.VectorData.Abstractions (MEVD)
- Config-driven ActiveProvider switch (Qdrant now, Azure AI Search later)
- Zero provider coupling in application code
- MEVD is same MAF pattern as `IEmbeddingGenerator`/`IChatClient`

### 2026-06-20: Embedding as Default Singleton
- Single `IEmbeddingGenerator` registered, not keyed
- ActiveEmbeddingProvider in config controls which embedding model is used
- All ingest/query operations automatically use the same model
- **Why not keyed:** Both ingest and query MUST use same model for vector space compatibility

### 2026-06-20: API Design
- Ingest and query are separate endpoints (admin bulk vs user real-time)
- PDF-only for v1, multipart upload
- Query reuses existing IChatClientFactory — no new abstraction
- Chunks returned in query response for transparency/debugging

### 2026-06-20: Vector Size Derived from Embedding Output
- `VectorSize` is NOT in config — derived from actual embedding output at runtime
- `GenerateAsync` returns `Embedding<float>` with `.Vector.Length` = real dimension
- MEVD: VectorStoreCollectionDefinition with VectorStoreVectorProperty.Dimensions = actual size
- EnsureCollectionExistsAsync handles create-or-skip
- **Why:** Config drift risk. The embedding model *determines* the vector size. Duplicating it in config is a maintenance liability.
- Switching embedding models: collection auto-created with correct size for the new model. If old collection exists with different size, delete it first (or use different collection name).

## Implementation Notes

### 2026-06-20: TextChunker Usage
- SK's TextChunker.SplitPlainTextLines ? SplitPlainTextParagraphs gives configurable chunk sizes with overlap
- Chunk index + total chunks stored per chunk for provenance
- PDF text extraction via PdfPig � pure .NET, no native dependencies

### 2026-06-20: Qdrant Client API
- Using QdrantVectorStore from Microsoft.SemanticKernel.Connectors.Qdrant (MEVD wrapper)
- VectorStore is abstract class from MEVD.Abstractions — services depend on this, not Qdrant types
- RagChunkRecord model with MEVD attributes ([VectorStoreKey], [VectorStoreData], [VectorStoreVector])
- Runtime vector size via VectorStoreCollectionDefinition with VectorStoreVectorProperty

### 2026-06-21: MEVD Vector Store Abstraction
- Package: Microsoft.SemanticKernel.Connectors.Qdrant 1.74.0-preview — wraps Qdrant.Client as QdrantVectorStore
- Package: Microsoft.Extensions.VectorData.Abstractions 10.1.0 (transitive)
- Services depend on VectorStore (abstract class) — zero Qdrant types in application code
- DI: switch on VectorStoreRegistry:ActiveProvider; adding Azure AI Search means one more branch
- VectorStoreVectorProperty constructor takes (name, type, dimensions) — need 3-param overload to specify type
- VectorStoreCollection.GetCollection<string, RagChunkRecord>(name) for reads, with definition for create
- UpsertAsync has single-record and IEnumerable overloads (UpsertBatchAsync does not exist)
- VectorSearchResult.Score is double? — handle with ?? 0
- The [VectorStoreVector] attribute requires Dimensions > 0 even though we override with definition at runtime. Workaround: set attribute to [VectorStoreVector(1)], definition overrides at EnsureCollectionExistsAsync
- Use #pragma warning disable SKEXP0050 for TextChunker (experimental SK API)

### 2026-06-21: SQLite Single-Connection Pattern
- SqlitePageIndexDatabase: refactored from per-method connections → single connection held for DI singleton lifetime
- **Why:** Multiple connections to same .db file caused "database is locked" errors (even with Pooling=False and WAL mode)
- The singleton connection is opened in constructor via `Open()`, implementing `IDisposable` for cleanup
- Registered as singleton in DI; DI manages disposal
- Transactional writes (InsertDocumentTreeAsync, InsertNodeTextsAsync) use `_db.BeginTransaction()` on the shared connection
- Connected reads (GetDocumentTreeJsonAsync, GetNodeTextsAsync) don't need explicit transactions
- **Potential issue:** Long-running read queries block writes. If this becomes a problem, add WAL mode via `PRAGMA journal_mode=WAL` on startup.

### 2026-06-21: Benchmark Results

| Metric | Vector RAG | PageIndex RAG |
|--------|-----------|---------------|
| Correct | 3/5 | 3/5 |
| Partial | 1/5 | 0/5 |
| Missed | 1/5 | 2/5 |
| Mean latency | 8.84s | 12.23s |
| Median latency | 5.32s | 9.01s |

**PageIndex RAG** performed better on contextual questions (AI tools: got full list vs partial) due to hierarchical tree structure. **Vector RAG** was faster and more consistent (1.5–6s vs 3–30s), but missed context split across chunk boundaries (previous employer). Neither found the email — header section was poorly captured by both strategies.

### 2026-06-21: Test Project / Web SDK Compile Leak (Fix)
- **Problem:** `Microsoft.NET.Sdk.Web` default glob (`**/*.cs`) picks up test project files when test project is a subdirectory of the main project. Causes duplicate assembly attributes + xunit not found errors in solution builds.
- **Root cause:** SDK-style projects have `EnableDefaultCompileItems=true` — the glob traverses all subdirectories including sibling project folders.
- **Fix (MyFirstAIApp pattern):**
  1. Move test project to `Tests\VectorRAGvsPageIndexRAG.Tests\` (peer directory via `Tests\` container)
  2. Main `.csproj` explicitly removes test files via:
     ```xml
     <Compile Remove="Tests\**\*" />
     <Content Remove="Tests\**\*" />
     <EmbeddedResource Remove="Tests\**\*" />
     ```
  3. Solution gets a `Tests` solution folder with `NestedProjects`
- Build now works with plain `dotnet build VectorRAGvsPageIndexRAG.sln` — no `--no-dependencies` needed

### 2026-06-20: EmbeddingGenerator API
- IEmbeddingGenerator from Microsoft.Extensions.AI
- Register via AddEmbeddingGenerator with factory: new OpenAIClient(...).GetEmbeddingClient(model).AsIEmbeddingGenerator()
- Single string input returns Embedding<float> directly (not a list)
- Collection input (IEnumerable<string>) returns IReadOnlyList<Embedding<float>>

## Provider Integration Decisions

### 2026-06-21: OpenCode Zen
- OpenAI-compatible API at https://opencode.ai/zen/v1
- Zero code change � reuses existing OpenAIClient default path (same as OpenRouter, NvidiaNim)
- Just a config entry: Type OpenAI, BaseUrl https://opencode.ai/zen/v1
- Model: deepseek-v4-flash-free (free tier via OpenCode gateway)

### 2026-06-21: GoogleAI (Gemini)
- Chose Google_GenerativeAI.Microsoft v3.6.6 (community, MIT, stable) over Google.Cloud.VertexAI.Extensions (Google first-party, beta, requires GCP project)
- Key difference: Google_GenerativeAI.Microsoft uses simple API key auth � matches existing Anthropic pattern
- VertexAI Extensions is beta and requires a Google Cloud project with Vertex AI API enabled � unnecessary overhead for this experiment
- Google_GenerativeAI.Microsoft provides GenerativeAIChatClient implementing IChatClient � plugs directly into our keyed DI registration
- Set as default provider � simpler onboarding (no Qdrant needed for tree-based RAG demos)

### 2026-06-21: Gemini as Default Provider
- Changed defaults from NvidiaNim/meta/llama-3.3-70b-instruct to GoogleAI/gemini-3.5-flash
- Files affected: ProviderModelSchemaFilter.cs, CompareQueryRequest.cs, PageIndexQueryRequest.cs, DocumentTreeBuilder.cs
- Reason: Gemini requires no Qdrant instance for PageIndex RAG demos, and GoogleAI API key is simpler to obtain than NvidiaNim

### 2026-06-21: Deterministic Chunk IDs + Batch Embedding
- Chunk IDs derived from SHA256(source + "\0" + text), truncated to 16 bytes -> GUID
- **Why:** Enables idempotent re-ingestion — same text = same ID, upsert overwrites
- `GetAsync` (IAsyncEnumerable) before embedding finds existing chunks; only missing chunks are embedded
- `EmbeddingBatchSize` config controls how many chunks are sent per embedding API call
- **Edge case:** First ingestion (no collection yet) — `GetAsync` throws, caught by try/catch; all chunks treated as new
- `collectionName` optional query param on `POST /api/rag/documents`, defaults to `DefaultCollectionName`