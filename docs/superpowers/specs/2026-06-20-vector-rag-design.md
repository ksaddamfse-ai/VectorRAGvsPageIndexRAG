# Vector RAG — Design Spec

## Overview

Classic vector RAG pipeline. PDF uploaded → text extracted via PdfPig → chunked via Semantic Kernel TextChunker → embedded via IEmbeddingGenerator → stored in Qdrant (gRPC). Query: question embedded → Qdrant vector similarity search → top-k chunks sent as context to LLM for answer.

## Endpoints

| Method | Route | Purpose |
|---|---|---|
| POST | `/api/rag/documents` | Upload PDF, chunk, embed, upsert to Qdrant |
| GET | `/api/rag/query` | Question → embed → Qdrant search → LLM answer |

## Ingestion Flow

1. Client sends PDF via multipart form (`IFormFile`)
2. `DocumentProcessor.ExtractText()` extracts raw text (PdfPig)
3. `TextChunker.SplitPlainTextLines()` + `SplitPlainTextParagraphs()` from Semantic Kernel — configurable chunk size (default 512) and overlap (default 51)
4. Each chunk wrapped in `RagChunk` model (Id, Text, Source, ChunkIndex, TotalChunks)
5. All chunk texts sent to `IEmbeddingGenerator<string, Embedding<float>>` in one batch
6. Actual vector size determined at runtime from first embedding output (not hardcoded)
7. Qdrant collection auto-created with `CreateCollectionAsync` on first ingest (vector size + Cosine distance), catches `AlreadyExists` on subsequent ingests
8. Points upserted via `QdrantClient.UpsertAsync` with payload fields: `text`, `source`, `chunk_index`, `total_chunks`
9. Returns `{ fileName, chunkCount, chunks: [{ id, text, chunkIndex }] }`

## Query Flow

1. Client sends `{ question, provider, model, topK }` via GET query params
2. Question embedded via same `IEmbeddingGenerator`
3. `QdrantClient.SearchAsync` on default collection with query vector, limit = topK
4. Results mapped to `RagChunkResult` (Text, Source, Score)
5. If no results: return "No relevant context found."
6. Top-k chunk texts joined as context → sent to LLM with "Answer using only the context below" prompt
7. Returns `{ answer, chunks: [{ text, source, score }] }`

## Storage

- Qdrant (external — gRPC port 6334, configurable)
- Collection name from config (`VectorStoreRegistry:ActiveProvider:DefaultCollectionName`, default `"documents"`)
- Collection auto-created with runtime-derived vector size + Cosine distance

## Key Design Decisions

- No SK Qdrant connector — YAGNI, Qdrant.Client directly
- Vector size from actual embedding output, not config — survives model changes
- All endpoints in single `RagController`
- DI: `IEmbeddingGenerator` singleton, `QdrantClient` singleton, services as singletons

## Dependencies

- `Qdrant.Client` (1.18.1) — gRPC client
- `Microsoft.SemanticKernel.Core` (1.31.0) — TextChunker only
- `UglyToad.PdfPig` (0.1.14) — PDF text extraction
- `Microsoft.Extensions.AI` (10.3.0) — IEmbeddingGenerator + IChatClient abstractions
