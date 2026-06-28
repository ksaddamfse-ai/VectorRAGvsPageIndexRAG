# Project Learnings

## Design Decisions

### Endpoint Simplification (GroupName Default)
- **PageIndex query**: `GroupName` is required with default "PDFs", no `DocId` parameter
- **Compare query**: Same change — `GroupName` replaces `DocId`
- **RAG collection**: Default `CollectionName = "PDFs"` across all endpoints
- **Why:** Group-based queries are the primary use case. Single-doc lookup adds complexity without value.
- **Swagger defaults**: Provider defaults to "GoogleAI", model to "gemini-3.5-flash"

### GoogleAI Model Parameter
- `GenerativeAIChatClient` constructor requires `($"models/{chatModel}")` as second parameter
- Without this, GoogleAI would always use default model "gemini-1.5-flash" regardless of config

### Test PDF Corpus
- Created 3 synthetic PDFs for RAG comparison testing:
  - **Technical Report**: API documentation (10 pages, 8 sections, code snippets, tables)
  - **Resume**: ML engineer CV (5 pages, 3 employers, 12+ skills, email in header)
  - **Legal Contract**: Software license (9 pages, 9 sections, numbered clauses, GDPR)
- Generated via `Tools/PdfGenerator` using PdfPig's `PdfDocumentBuilder`
- Output to `test-pdfs/` (committed to repo)

### Vector Store Abstraction
- Use `VectorStore` from Microsoft.Extensions.VectorData (MEVD)
- Config-driven ActiveProvider switch (Qdrant now, Azure AI Search later)
- Zero provider coupling in application code
- MEVD is same MAF pattern as `IEmbeddingGenerator`/`IChatClient`
- Port 6334 for Qdrant gRPC (not 6333 which is HTTP REST)

### Embedding as Default Singleton
- Single `IEmbeddingGenerator` registered, not keyed
- ActiveEmbeddingProvider in config controls which embedding model is used
- All ingest/query operations automatically use the same model
- **Why not keyed:** Both ingest and query MUST use same model for vector space compatibility

### API Design
- Ingest and query are separate endpoints (admin bulk vs user real-time)
- PDF-only for v1, multipart upload
- Query reuses existing IChatClientFactory — no new abstraction
- Chunks returned in query response for transparency/debugging

### Vector Size Derived from Embedding Output
- `VectorSize` is NOT in config — derived from actual embedding output at runtime
- `GenerateAsync` returns `Embedding<float>` with `.Vector.Length` = real dimension
- MEVD: VectorStoreCollectionDefinition with VectorStoreVectorProperty.Dimensions = actual size
- EnsureCollectionExistsAsync handles create-or-skip
- **Why:** Config drift risk. The embedding model *determines* the vector size. Duplicating it in config is a maintenance liability.
- Switching embedding models: collection auto-created with correct size for the new model. If old collection exists with different size, delete it first (or use different collection name).

## Implementation Notes

### TextChunker Usage
- SK's TextChunker.SplitPlainTextLines → SplitPlainTextParagraphs gives configurable chunk sizes with overlap
- Chunk index + total chunks stored per chunk for provenance
- PDF text extraction via PdfPig — pure .NET, no native dependencies
- Uses `#pragma warning disable SKEXP0050` for TextChunker (experimental SK API)

### Qdrant Client API
- Using QdrantVectorStore from Microsoft.SemanticKernel.Connectors.Qdrant (MEVD wrapper)
- VectorStore is abstract class from MEVD.Abstractions — services depend on this, not Qdrant types
- RagChunkRecord model with MEVD attributes ([VectorStoreKey], [VectorStoreData], [VectorStoreVector])
- Runtime vector size via VectorStoreCollectionDefinition with VectorStoreVectorProperty
- The [VectorStoreVector] attribute requires Dimensions > 0 even though we override with definition at runtime. Workaround: set attribute to [VectorStoreVector(1)], definition overrides at EnsureCollectionExistsAsync
- UpsertAsync has single-record and IEnumerable overloads (UpsertBatchAsync does not exist)
- VectorSearchResult.Score is double? — handle with ?? 0

### SQLite Connection Pattern
- `SqliteConnection` is NOT thread-safe. Concurrent `ExecuteNonQueryAsync` from two requests throws `InvalidOperationException` (re-entrant call).
- Fix: Open a new `SqliteConnection` per method call (connection pooling makes this cheap)
- Connection string: `"Data Source={path};Cache=Shared"` — `Cache=Shared` enables cross-connection visibility
- `Busy Timeout=5000` set via `PRAGMA busy_timeout=5000` after opening connection (not in connection string)
- `InitializeAsync` sets `PRAGMA journal_mode=WAL;` — writers no longer block readers
- Service stays as Singleton (DI lifetime) but is stateless — holds only connection string config, not a live connection
- Removed `IDisposable` from `IPageIndexDatabase` — no shared connection to dispose

### Token Budgeting in Query (Context Packing)
- **Problem:** `RagQueryService.QueryAsync` concatenated all top-K chunks into prompt with no token limit check. A 500-page PDF + broad question could blow the model's context window silently.
- **Fix:** Greedy packing by relevance score with hard token budget:
  1. Budget = `modelContextWindow - systemPromptTokens - questionTokens - reservedOutputTokens - safetyMargin (~7%)`
  2. Walk chunks sorted by similarity score descending, pack whole chunks until budget is exhausted
  3. Skip (don't truncate) chunks that don't fit — partial sentences cost tokens without adding signal
  4. Reorder: best chunk first, second-best last (mitigates "lost in the middle" — Liu et al., 2023)
- Token counting uses `text.Length / 4 + 1` (simple heuristic, not actual tokenizer)
- Model context windows stored in config under `ProviderContextWindows` — a `Dictionary<string, int>` mapping `"provider__model"` to context window size

### Deterministic PDF Structure Parser (Replaces LLM Offsets)
- **Problem:** LLMs cannot reliably estimate character offsets — they tokenize text into subwords, not characters. Title-based `IndexOf` also fails because titles repeat in prose.
- **Solution:** Rule-based parsing using PdfPig font metadata:
  1. `page.GetWords()` returns `Word` objects with `Letters[0].FontSize` (font size in unscaled PDF units)
  2. Calculate median font size across all words
  3. Section headers: font size ≥ 1.2× median (plus numbered patterns boost confidence)
  4. Paragraphs: vertical gaps ≥ 1.5× line height between words
  5. Nesting: font size ordering determines parent-child (smaller font = deeper nesting)
- **Architecture:** `PdfStructureParser` does structure + text extraction; LLM only generates summaries (understanding, not structure)
- **Benefits:** Deterministic, fast, no API calls for structure, works offline
- **Tradeoff:** May miss semantic structure in PDFs with inconsistent fonts — fall back to spacing heuristics

### CompareController Exception Resilience
- `SafeRun<T>` helper wraps each query branch in try/catch, capturing exceptions as error strings
- **Effect:** If Qdrant is down (RAG branch throws), the PageIndex result still survives — both results returned independently

### Test Project / Web SDK Compile Leak (Fix)
- **Problem:** `Microsoft.NET.Sdk.Web` default glob (`**/*.cs`) picks up test project files when test project is a subdirectory of the main project. Causes duplicate assembly attributes + xunit not found errors in solution builds.
- **Root cause:** SDK-style projects have `EnableDefaultCompileItems=true` — the glob traverses all subdirectories including sibling project folders.
- **Fix:**
  1. Move test project to `Tests\VectorRAGvsPageIndexRAG.Tests\` (peer directory via `Tests\` container)
  2. Main `.csproj` explicitly removes test and tools files via:
     ```xml
     <Compile Remove="Tests\**\*" />
     <Compile Remove="Tools\**\*" />
     <Content Remove="Tests\**\*" />
     <Content Remove="Tools\**\*" />
     <EmbeddedResource Remove="Tests\**\*" />
     <EmbeddedResource Remove="Tools\**\*" />
     ```
  3. Solution gets a `Tests` solution folder with `NestedProjects`
- Build now works with plain `dotnet build VectorRAGvsPageIndexRAG.sln` — no `--no-dependencies` needed

### PdfPig Font Metadata API
- `Word` class has `FontName` (string?) and `Letters` (IReadOnlyList<Letter>) but NOT `Font.Size`
- To get font size: `word.Letters[0].FontSize` (from first letter)
- `Letter.FontSize` is in unscaled PDF units — not points or pixels
- `page.GetWords()` returns words in content stream order (may not be reading order)
- `BoundingBox` on `Word` is `PdfRectangle` with `.Left`, `.Top`, `.Bottom`, `.Right`

### EmbeddingGenerator API
- IEmbeddingGenerator from Microsoft.Extensions.AI
- Register via AddEmbeddingGenerator with factory: new OpenAIClient(...).GetEmbeddingClient(model).AsIEmbeddingGenerator()
- Single string input returns Embedding<float> directly (not a list)
- Collection input (IEnumerable<string>) returns IReadOnlyList<Embedding<float>>

### Deterministic Chunk IDs + Batch Embedding
- Chunk IDs derived from SHA256(source + "\0" + text), truncated to 16 bytes -> GUID
- **Why:** Enables idempotent re-ingestion — same text = same ID, upsert overwrites
- `GetAsync` (IAsyncEnumerable) before embedding finds existing chunks; only missing chunks are embedded
- `EmbeddingBatchSize` config controls how many chunks are sent per embedding API call
- **Edge case:** First ingestion (no collection yet) — `GetAsync` throws `QdrantException`, caught; all chunks treated as new

## Provider Integration Decisions

### OpenCode Zen
- OpenAI-compatible API at https://opencode.ai/zen/v1
- Zero code change — reuses existing OpenAIClient default path (same as OpenRouter, NvidiaNim)
- Just a config entry: Type OpenAI, BaseUrl https://opencode.ai/zen/v1
- Model: deepseek-v4-flash-free (free tier via OpenCode gateway)

### GoogleAI (Gemini)
- Chose Google_GenerativeAI.Microsoft v3.6.6 (community, MIT, stable) over Google.Cloud.VertexAI.Extensions (Google first-party, beta, requires GCP project)
- Key difference: Google_GenerativeAI.Microsoft uses simple API key auth — matches existing Anthropic pattern
- VertexAI Extensions is beta and requires a Google Cloud project with Vertex AI API enabled — unnecessary overhead for this experiment
- Google_GenerativeAI.Microsoft provides GenerativeAIChatClient implementing IChatClient — plugs directly into our keyed DI registration
- Set as default provider — simpler onboarding (no Qdrant needed for tree-based RAG demos)

### Gemini as Default Provider
- Changed defaults from NvidiaNim/meta/llama-3.3-70b-instruct to GoogleAI/gemini-3.5-flash
- Files affected: ProviderModelSchemaFilter.cs, CompareQueryRequest.cs, PageIndexQueryRequest.cs, DocumentTreeBuilder.cs
- Reason: Gemini requires no Qdrant instance for PageIndex RAG demos, and GoogleAI API key is simpler to obtain than NvidiaNim

## Benchmark Results

| Metric | Vector RAG | PageIndex RAG |
|--------|-----------|---------------|
| Correct | 3/5 | 3/5 |
| Partial | 1/5 | 0/5 |
| Missed | 1/5 | 2/5 |
| Mean latency | 8.84s | 12.23s |
| Median latency | 5.32s | 9.01s |

**PageIndex RAG** performed better on contextual questions (AI tools: got full list vs partial) due to hierarchical tree structure. **Vector RAG** was faster and more consistent (1.5–6s vs 3–30s), but missed context split across chunk boundaries (previous employer). Neither found the email — header section was poorly captured by both strategies.

## NuGet Packages

| Package | Version | Purpose |
|---------|---------|---------|
| Anthropic | 12.29.1 | Anthropic Claude API client |
| Azure.AI.OpenAI | 2.1.0 | Azure OpenAI + OpenAI-compatible clients |
| Google_GenerativeAI.Microsoft | 3.6.6 | Google AI (Gemini) chat client |
| Microsoft.Data.Sqlite | 10.0.9 | SQLite for PageIndex storage |
| Microsoft.Extensions.AI | 10.7.0 | MAF abstractions (IChatClient, IEmbeddingGenerator) |
| Microsoft.Extensions.AI.OpenAI | 10.7.0 | OpenAI adapter for MAF |
| Microsoft.SemanticKernel.Connectors.Qdrant | 1.74.0-preview | MEVD Qdrant VectorStore wrapper |
| Microsoft.SemanticKernel.Core | 1.77.0 | TextChunker (SplitPlainTextLines/Paragraphs) |
| Swashbuckle.AspNetCore | 6.8.0 | Swagger/OpenAPI |
| PdfPig | 0.1.14 | PDF text extraction and font metadata |
| Qdrant.Client | 1.18.1 | Qdrant gRPC client (underlying SK connector) |

### PageIndex Refactor: Page Numbers vs Character Offsets
- **Problem:** LLMs cannot reliably estimate character offsets — they tokenize text into subwords, not characters
- **Solution:** Use page numbers (`StartPage`/`EndPage`) instead of character offsets on TreeNode
- **Why page numbers work:** LLMs can read `<physical_index_6>` tags and identify page 6, but can't count to character 45,231
- **PageIndex approach:** Uses `start_index`/`end_page` as physical page numbers with verification loop
- **Our approach:** PdfStructureParser sets page numbers deterministically (no LLM needed for page assignment)
- **Benefit:** Citations possible — "This answer is from pages 4-5"

### PageIndex Refactor: Two-Phase Retrieval for Large Documents
- **Problem:** Large documents (1000+ pages) produce trees with 100+ nodes — skeleton exceeds LLM context
- **Solution:** Two-phase retrieval: Phase 1 picks top-level sections, Phase 2 drills into sub-sections
- **Config:** `MaxSkeletonDepth` (default: 3) limits skeleton depth, `MaxTokensPerQuery` (default: 20000) limits context
- **Small docs:** Phase 1 picks leaf node → skip Phase 2 → same as single-pass
- **Large docs:** Phase 1 picks parent → Phase 2 drills into children → accurate retrieval
- **PageIndex equivalent:** Their agentic loop (think → fetch → think → fetch) achieves same result but with more LLM calls

### PageIndex Refactor: page_texts Table Replaces node_texts
- **Old:** `node_texts` stored text per-node (redundant with tree, no page references)
- **New:** `page_texts` stores text per-page (single source of truth, enables citations)
- **Query:** `SELECT text FROM page_texts WHERE doc_id=? AND page_number BETWEEN start AND end`
- **Benefit:** No duplication, cross-doc bug fixed (always filtered by doc_id), citations enabled
- **Tradeoff:** Slightly more complex retrieval (page range vs direct node lookup)

### PageIndex Refactor: Parallel Summary Generation
- **Old:** Sequential `foreach` loop — 15 nodes × 2s = 30 seconds
- **New:** `Task.WhenAll` — 15 nodes × 2s (parallel) = ~3 seconds
- **PageIndex equivalent:** Their `asyncio.gather` achieves same parallelism
- **Benefit:** 10x faster ingestion, same total token cost

### PageIndex Refactor: Multi-PDF Group Queries
- **PageIndex approach:** Single-document only — agent manually picks which doc to search
- **Our approach:** Group-based — combined skeleton shows all docs, LLM picks from any
- **Benefit:** Cross-document search is automatic, not manual
- **Bug fixed:** Old `GetNodeTextsByNodeIdsAsync` didn't filter by `doc_id` — cross-doc node ID collisions

### PageIndex Refactor: Deterministic Parsing vs LLM-Driven
- **PageIndex:** LLM generates tree structure (expensive, non-deterministic, requires API key)
- **Our approach:** PdfPig font heuristics generate structure (free, deterministic, works offline)
- **Benefit:** Parse 1000 PDFs for free vs expensive LLM calls
- **Tradeoff:** May miss semantic structure in PDFs with inconsistent fonts

## Config Structure

### ProviderRegistry
- Each provider has: Type, Enabled, ApiKey, BaseUrl, Models[]
- Type determines DI path: OpenAI (default), AzureOpenAI, Anthropic, GoogleAI
- Service key format: `{provider}__{model}` (double underscore separator)

### EmbeddingRegistry
- ActiveEmbeddingProvider selects which embedding model is used
- Each embedding provider has: Type, ApiKey, BaseUrl, Model
- Currently supports: NvidiaNim (nvidia/nv-embed-v1), Ollama (nomic-embed-text)

### VectorStoreRegistry
- ActiveProvider selects active vector store (currently "Qdrant")
- Each vector store has: Type, DefaultCollectionName, ChunkSize, ChunkOverlap, EmbeddingBatchSize, plus provider-specific fields
- Qdrant: Host, Port (6334 for gRPC)
- AzureAISearch: Endpoint, ApiKey

### ProviderContextWindows
- Maps `"provider__model"` to context window size (int)
- Used by RagQueryService for token budgeting
