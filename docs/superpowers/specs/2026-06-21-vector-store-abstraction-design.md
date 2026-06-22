# Vector Store Abstraction: MEVD Integration

**Date:** 2026-06-21
**Status:** Implemented
**Driver:** The project used `Qdrant.Client` directly despite config already listing a second vector store provider (AzureAI Search). This spec introduces `Microsoft.Extensions.VectorData` (MEVD) — the official .NET MAF vector store abstraction.

## Motivation

- Eliminate hard dependency on `Qdrant.Client` in application services
- Enable provider swap without changing business logic
- Follow same MAF pattern already used (`IEmbeddingGenerator`, `IChatClient`)
- Config already defines a second provider (`AzureAISearch`); abstraction removes the only blocker

## Architecture

```
Before:
RagIngestionService → QdrantClient (direct)
RagQueryService     → QdrantClient (direct)
Program.cs          → new QdrantClient(...)

After:
RagIngestionService → VectorStore (abstract, from MEVD.Abstractions)
RagQueryService     → VectorStore (abstract)
Program.cs          → new QdrantVectorStore(new QdrantClient(...))
                      ↑ swappable to AzureAISearchVectorStore
```

### Key types

| Layer | Type | Source |
|-------|------|--------|
| Abstraction | `VectorStore` (abstract class) | `Microsoft.Extensions.VectorData.Abstractions` |
| Collection | `VectorStoreCollection<TKey,TRecord>` (abstract class) | `Microsoft.Extensions.VectorData.Abstractions` |
| Qdrant impl | `QdrantVectorStore` | `Microsoft.SemanticKernel.Connectors.Qdrant` |
| Search result | `VectorSearchResult<TRecord>` | `Microsoft.Extensions.VectorData.Abstractions` |

### Data model

`Models/RagChunkRecord.cs` — MEVD-annotated type for persistence:

```csharp
public class RagChunkRecord
{
    [VectorStoreKey]
    public Guid Key { get; set; }

    [VectorStoreData]
    public string Text { get; set; } = "";

    [VectorStoreData]
    public string Source { get; set; } = "";

    [VectorStoreData]
    public int ChunkIndex { get; set; }

    [VectorStoreData]
    public int TotalChunks { get; set; }

    [VectorStoreVector(1)]
    public float[]? Vector { get; set; }
}
```

- `[VectorStoreVector(1)]` with placeholder dimension — actual size supplied via `VectorStoreCollectionDefinition` at runtime
- `Key` is `Guid` (not string) — deterministic chunk IDs parsed to GUID

### Runtime dimension handling

Since vector size is only known after embedding generation:

1. Generate embeddings for all missing chunks
2. Read `embeddings[0].Vector.Length` → `vectorSize`
3. Build `VectorStoreCollectionDefinition` with `VectorStoreVectorProperty("Vector", typeof(float[]), vectorSize)`
4. Call `GetCollection<Guid, RagChunkRecord>(name, definition)`
5. Call `EnsureCollectionExistsAsync()` (creates if missing, no-op if exists)
6. Upsert records

For query path, no definition needed — just `GetCollection<Guid, RagChunkRecord>(name)`.

## Service Changes

### RagIngestionService

- Constructor: `QdrantClient qdrant` → `VectorStore vectorStore`
- Removed imports: `Qdrant.Client`, `Qdrant.Client.Grpc`, `Grpc.Core`
- New flow: embed → build definition → get collection → ensure exists → upsert records
- `VectorStoreCollectionDefinition` built once per `IngestAsync` call with actual vector size
- Each `RagChunk` mapped to `RagChunkRecord` before upsert

### RagQueryService

- Constructor: `QdrantClient qdrant` → `VectorStore vectorStore`
- Removed imports: `Qdrant.Client`, `Qdrant.Client.Grpc`
- New flow: get collection → `SearchAsync(queryVector, top: topK)` → map results
- Collection definition not needed for read path
- Query vector: `new ReadOnlyMemory<float>(embedding.Vector.ToArray())`

## DI Registration (Program.cs)

Register `VectorStore` (not raw `QdrantClient`):

```csharp
builder.Services.AddSingleton<VectorStore>(_ => activeVectorStoreProvider switch
{
    "Qdrant" => new QdrantVectorStore(
        new QdrantClient(
            host: activeVectorStoreSection["Host"] ?? "localhost",
            port: activeVectorStoreSection.GetValue<int>("Port")),
        ownsClient: true),
    _ => throw new InvalidOperationException(
        $"Unknown vector store provider: {activeVectorStoreProvider}")
});
```

`IOptions<VectorStoreRegistryEntry>` registration kept for chunk config (ChunkSize, ChunkOverlap, DefaultCollectionName, EmbeddingBatchSize).

## Provider Swap (future)

```csharp
"AzureAISearch" => new AzureAISearchVectorStore(
    new SearchIndexClient(new Uri(cfg["Endpoint"]!), new AzureKeyCredential(cfg["ApiKey"]!))),
```

No changes to `RagIngestionService` or `RagQueryService` — they only depend on `VectorStore`.

## Dependencies

| Package | Version | Type |
|---------|---------|------|
| `Microsoft.SemanticKernel.Connectors.Qdrant` | 1.74.0-preview | New |
| `Microsoft.Extensions.VectorData.Abstractions` | (transitive) | New |
| `Qdrant.Client` | 1.18.1 (transitive) | Existing |

## Not Changed

- `VectorStoreRegistryEntry` config model (chunk config lives here)
- `appsettings.json` config structure
- `RagChunk` model (used in response DTOs, has Vector property for in-flight embeddings)
- All controllers, DTOs, other services
- `IRagIngestionService` / `IRagQueryService` interfaces
