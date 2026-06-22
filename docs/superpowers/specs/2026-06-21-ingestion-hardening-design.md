# Ingestion Hardening — Design Spec

**Date:** 2026-06-21
**Status:** Implemented
**Goal:** Harden `RagIngestionService` with deterministic chunk IDs, embedding batch controls, and skip-before-embed dedup.

---

## 1. Config

`VectorStoreRegistryEntry.cs` — added:
```csharp
public int EmbeddingBatchSize { get; set; } = 500;
```

`appsettings.json` — added under both `Qdrant` and `AzureAISearch`:
```json
"EmbeddingBatchSize": 500
```

---

## 2. Data Model

`RagChunk.cs` — deterministic ID via SHA256:
```csharp
public static string ComputeId(string text, string source)
{
    var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{source}\0{text}"));
    return new Guid(hash.AsSpan(0, 16)).ToString();
}
```

- Null separator `\0` prevents boundary collisions
- Truncate SHA256 to 16 bytes → fits GUID
- `RagChunkRecord.cs` — `Key` is `Guid` (not string), `[VectorStoreKey]` annotated

---

## 3. API Changes

`RagController.Ingest`:
```
POST /api/rag/documents?collectionName=custom-name
```
- `collectionName` optional, defaults to `"PDFs"`
- Query param = frontend-friendly (no FormData restructuring)

`RagIngestionResponse`:
```csharp
public record RagIngestionResponse(
    string FileName,
    int ChunkCount,
    string CollectionName,
    List<RagChunkResponse> Chunks
);
```

---

## 4. Service Logic

`RagIngestionService.IngestAsync(text, fileName, collectionName)` — implemented flow:

1. Chunk text via `TextChunker.SplitPlainTextLines` + `SplitPlainTextParagraphs`
2. Compute deterministic GUID for each chunk via `RagChunk.ComputeId`
3. Guard: empty chunks → log+return early
4. Load existing chunk keys from MEVD collection via `GetAsync(allGuids)`:
   ```csharp
   await foreach (var record in collection.GetAsync(allGuids))
       existingSet.Add(record.Key);
   ```
5. Filter to missing: `chunks.Where(c => !existingSet.Contains(Guid.Parse(c.Id)))`
6. Batch-embed missing chunks in groups of `EmbeddingBatchSize`:
   ```csharp
   for (int i = 0; i < missing.Count; i += batchSize):
       var batch = missing.Skip(i).Take(batchSize);
       var embeddings = await embedder.GenerateAsync(batch.Select(c => c.Text));
   ```
7. Assign vectors to missing chunks only
8. Build `VectorStoreCollectionDefinition` with actual vector size from first embedding
9. Upsert only missing chunks via MEVD `collection.UpsertAsync(records)`
10. Log + return with total chunk count

---

## 5. Files

| File | Change |
|------|--------|
| `Models/RagChunk.cs` | `Id` via `ComputeId()` (SHA256 → GUID), added `Vector` property |
| `Models/RagChunkRecord.cs` | MEVD annotations: `[VectorStoreKey] Key (Guid)`, `[VectorStoreVector] Vector (float[])` |
| `Services/Interfaces/IRagIngestionService.cs` | `IngestAsync` returns `RagIngestionResult` with `CollectionName` |
| `Services/RagIngestionService.cs` | Full implementation: dedup check, batch embedding, MEVD collection creation |
| `Controllers/RagController.cs` | `[FromQuery] collectionName = "PDFs"`, returns `RagIngestionResponse` |
| `DTOs/RagIngestionResponse.cs` | Added `CollectionName` field |
| `DTOs/RagChunkResponse.cs` | `(Id, Text, ChunkIndex)` |
| `Settings/VectorStoreRegistryEntry.cs` | Added `EmbeddingBatchSize` property |
| `appsettings.json` | Added `EmbeddingBatchSize: 500` under Qdrant/AzureAISearch |
