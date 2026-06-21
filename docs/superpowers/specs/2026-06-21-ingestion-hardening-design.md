# Ingestion Hardening — Design Spec

**Date:** 2026-06-21
**Goal:** Harden `RagIngestionService` with deterministic chunk IDs, embedding batch controls, and skip-before-embed dedup (Core 3).

---

## 1. Config

`VectorStoreRegistryEntry.cs` — add:
```csharp
public int EmbeddingBatchSize { get; set; } = 500;
```

`appsettings.json` — add under both `Qdrant` and `AzureAISearch`:
```json
"EmbeddingBatchSize": 500
```

---

## 2. Data Model

`RagChunk.cs` — replace random GUID with deterministic ID:

```csharp
public static string ComputeId(string text, string source)
{
    var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{source}\0{text}"));
    return new Guid(hash.AsSpan(0, 16)).ToString();
}
```

- Null separator `\0` prevents boundary collisions (`"doc.txt"+"A"` vs `"doc"+"txtA"`)
- Truncate SHA256 to 16 bytes → fits GUID
- `RagChunkRecord.cs` — no schema changes. `Key` stays `Guid`.

---

## 3. API Changes

`RagController.Ingest`:
```
POST /api/rag/documents?collectionName=custom-name
```
- `collectionName` optional, defaults to `VectorStoreRegistryEntry.DefaultCollectionName`
- Query param = frontend-friendly (no FormData restructuring)

`RagIngestionResponse` gains:
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

`RagIngestionService.IngestAsync(text, fileName, collectionName?)` — revised flow:

1. Chunk text (unchanged)
2. Compute deterministic GUID for each chunk
3. Guard: empty chunks → log+return early (unchanged)
4. Load existing chunk keys from Qdrant:
   ```
   var existing = await collection.GetBatchAsync(allGuids);
   var existingSet = existing.Select(r => r.Key).ToHashSet();
   ```
5. Filter to missing:
   ```
   var missing = chunks.Where(c => !existingSet.Contains(Guid.Parse(c.Id))).ToList();
   ```
6. Batch-embed missing chunks in groups of `EmbeddingBatchSize`:
   ```
   for each batch:
       var embeddings = await embedder.GenerateAsync(batch.Select(c => c.Text));
   ```
7. Assign vectors to missing chunks only
8. Upsert only missing chunks (existing already stored):
   ```
   if (missing.Count > 0)
       await collection.UpsertAsync(records);
   ```
9. Log + return with total chunk count

---

## 5. Files Changed

| File | Change |
|------|--------|
| `Models/RagChunk.cs` | Replace `Id = Guid.NewGuid()` with deterministic `ComputeId()` |
| `Services/Interfaces/IRagIngestionService.cs` | Add `collectionName` param to `IngestAsync` |
| `Services/RagIngestionService.cs` | Full rewrite: dedup check, batch embedding, new param |
| `Controllers/RagController.cs` | Add `[FromQuery] collectionName` param, pass to service |
| `DTOs/RagIngestionResponse.cs` | Add `CollectionName` field |
| `Settings/VectorStoreRegistryEntry.cs` | Add `EmbeddingBatchSize` property |
| `appsettings.json` | Add `EmbeddingBatchSize` under Qdrant entry |
| `LEARNINGS.md` | Note deterministic GUID + batch approach |
