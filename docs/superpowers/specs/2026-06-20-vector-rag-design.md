# Vector RAG — Design Spec

**Status:** Implemented

## Overview

Classic vector RAG pipeline. PDF uploaded → text extracted via PdfPig → chunked via Semantic Kernel TextChunker → embedded via IEmbeddingGenerator → stored in Qdrant (gRPC) via MEVD abstraction. Query: question embedded → Qdrant vector similarity search → top-k chunks sent as context to LLM for answer.

## Endpoints

| Method | Route | Purpose |
|---|---|---|
| POST | `/api/rag/documents?collectionName=PDFs` | Upload PDF, chunk, embed, upsert to Qdrant |
| GET | `/api/rag/query` | Question → embed → Qdrant search → LLM answer |

## Ingestion Flow

1. Client sends PDF via multipart form (`IFormFile`)
2. `DocumentProcessor.ExtractText()` extracts raw text (PdfPig)
3. `TextChunker.SplitPlainTextLines()` + `SplitPlainTextParagraphs()` from Semantic Kernel — configurable chunk size (default 512) and overlap (default 51)
4. Each chunk wrapped in `RagChunk` model with deterministic ID via `RagChunk.ComputeId(text, source)` (SHA256 truncated to GUID)
5. All chunk texts sent to `IEmbeddingGenerator<string, Embedding<float>>` in batches of `EmbeddingBatchSize` (default 500)
6. Actual vector size determined at runtime from first embedding output (not hardcoded)
7. Qdrant collection auto-created with `EnsureCollectionExistsAsync` on first ingest (vector size + Cosine distance), no-op if exists
8. Duplicate chunks skipped via deterministic ID check against existing records
9. Points upserted via MEVD `VectorStoreCollection.UpsertAsync` with payload fields: `Text`, `Source`, `ChunkIndex`, `TotalChunks`
10. Returns `RagIngestionResponse(FileName, ChunkCount, CollectionName, Chunks: [{ Id, Text, ChunkIndex }])`

## Query Flow

1. Client sends `{ question, provider, model, topK, collectionName }` via GET query params
2. Question embedded via same `IEmbeddingGenerator`
3. MEVD `VectorStoreCollection.SearchAsync` on default collection with query vector, limit = topK
4. Results mapped to `RagChunkResult` (Text, Source, Score)
5. If no results: return "No relevant context found."
6. Top-k chunk texts packed by token budget (context window - system prompt - question - safety margin - output tokens) and joined as context
7. Sent to LLM with "Answer using only the context below" prompt
8. Returns `RagQueryResponse(Answer, Chunks: [{ Text, Source, Score }])`

## Storage

- Qdrant (external — gRPC port 6334, configurable) via MEVD `VectorStore` abstraction
- Collection name from config (`VectorStoreRegistry:ActiveProvider:DefaultCollectionName`, default `"PDFs"`)
- Collection auto-created with runtime-derived vector size + Cosine distance
- `RagChunkRecord` with MEVD annotations: `[VectorStoreKey] Key (Guid)`, `[VectorStoreData] Text/Source/ChunkIndex/TotalChunks`, `[VectorStoreVector] Vector (float[])`

## Key Design Decisions

- MEVD abstraction (`Microsoft.Extensions.VectorData`) — not raw `QdrantClient`
- Vector size from actual embedding output, not config — survives model changes
- Deterministic chunk IDs (SHA256) — skip-before-embed dedup
- All endpoints in single `RagController`
- DI: `IEmbeddingGenerator` singleton, `VectorStore` singleton, services as singletons
- Token budgeting: `RagQueryService.PackChunks` by relevance score with hard token budget from model context window config

## Dependencies

- `Microsoft.SemanticKernel.Connectors.Qdrant` (1.74.0-preview) — MEVD Qdrant connector
- `Qdrant.Client` (1.18.1) — gRPC client (transitive)
- `Microsoft.SemanticKernel.Core` (1.77.0) — TextChunker only
- `UglyToad.PdfPig` (0.1.14) — PDF text extraction
- `Microsoft.Extensions.AI` (10.7.0) — IEmbeddingGenerator + IChatClient abstractions
