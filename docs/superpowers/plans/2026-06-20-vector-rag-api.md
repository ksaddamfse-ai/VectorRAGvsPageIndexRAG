# Vector RAG API Implementation Plan

**Goal:** Build PDF ingest + vector search query API using Qdrant, configurable embedding providers, and existing chat provider infrastructure.

**Architecture:** Four new services (document extraction, orchestration for ingest/query) behind two REST endpoints. Embedding generator and vector store are config-driven singletons. Chat client reuses existing IChatClientFactory. Qdrant.Client used directly for vector operations.

**Tech Stack:** .NET 10, Microsoft.Extensions.AI, Qdrant.Client, PdfPig, Swashbuckle
