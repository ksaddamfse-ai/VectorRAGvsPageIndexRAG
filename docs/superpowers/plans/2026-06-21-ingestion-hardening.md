# Ingestion Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Harden `RagIngestionService` with deterministic chunk IDs, configurable embedding batch size, and skip-before-embed dedup.

**Architecture:** All changes scoped to ingestion pipeline (RagIngestionService + RagController). Config is a single new property on `VectorStoreRegistryEntry`. Deterministic GUIDs replace random `Guid.NewGuid()` for idempotent re-ingestion. Before embedding, existing chunk keys are fetched from Qdrant; only missing chunks are embedded+upserted.

**Tech Stack:** .NET 10, MEVD, Qdrant, SHA256

---

### Task 1: Config — EmbeddingBatchSize

**Files:**
- Modify: `Settings/VectorStoreRegistryEntry.cs:7`
- Modify: `appsettings.json:75-79`

- [x] **Step 1: Add property to VectorStoreRegistryEntry**

```csharp
// Settings/VectorStoreRegistryEntry.cs — add after ChunkOverlap
public int EmbeddingBatchSize { get; set; } = 500;
```

- [x] **Step 2: Add to appsettings.json**

```json
// appsettings.json — add under Qdrant entry (line 75), after ChunkOverlap: 51
"EmbeddingBatchSize": 500
```

Also add under `AzureAISearch`:
```json
"EmbeddingBatchSize": 500
```

- [x] **Step 3: Build check**

Run: `dotnet build`
Expected: Build succeeds with no errors.

- [x] **Step 4: Commit**

```bash
git add Settings/VectorStoreRegistryEntry.cs appsettings.json
git commit -m "feat: add EmbeddingBatchSize config"
```

---

### Task 2: Data Model — Deterministic Chunk IDs

**Files:**
- Modify: `Models/RagChunk.cs:1-11`

- [x] **Step 1: Replace random GUID with deterministic ComputeId**

Current `RagChunk.cs`:
```csharp
namespace VectorRAGvsPageIndexRAG.Models;

public class RagChunk
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Text { get; set; } = "";
    public string Source { get; set; } = "";
    public int ChunkIndex { get; set; }
    public int TotalChunks { get; set; }
    public float[] Vector { get; set; } = [];
}
```

Replace with:
```csharp
using System.Security.Cryptography;
using System.Text;

namespace VectorRAGvsPageIndexRAG.Models;

public class RagChunk
{
    public string Id { get; set; } = "";
    public string Text { get; set; } = "";
    public string Source { get; set; } = "";
    public int ChunkIndex { get; set; }
    public int TotalChunks { get; set; }
    public float[] Vector { get; set; } = [];

    public static string ComputeId(string text, string source)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{source}\0{text}"));
        return new Guid(hash.AsSpan(0, 16)).ToString();
    }
}
```

- [x] **Step 2: Build check**

Run: `dotnet build`
Expected: Build succeeds.

- [x] **Step 3: Commit**

```bash
git add Models/RagChunk.cs
git commit -m "feat: deterministic chunk IDs via SHA256-based GUID"
```

---

### Task 3: Interface + DTO — Add collectionName

**Files:**
- Modify: `Services/Interfaces/IRagIngestionService.cs:7`
- Modify: `DTOs/RagIngestionResponse.cs:3`

- [x] **Step 1: Update IRagIngestionService**

```csharp
// Services/Interfaces/IRagIngestionService.cs
namespace VectorRAGvsPageIndexRAG.Services.Interfaces;

public interface IRagIngestionService
{
    Task<RagIngestionResult> IngestAsync(string text, string fileName, string collectionName = "PDFs");
}

public record RagIngestionResult(string FileName, int ChunkCount, List<RagChunk> Chunks, string CollectionName);
```

- [x] **Step 2: Update RagIngestionResponse DTO**

```csharp
// DTOs/RagIngestionResponse.cs
namespace VectorRAGvsPageIndexRAG.DTOs;

public record RagIngestionResponse(string FileName, int ChunkCount, string CollectionName, List<RagChunkResponse> Chunks);
```

- [x] **Step 3: Build check**

Run: `dotnet build`
Expected: Build fails because `RagIngestionService` and `RagController` don't implement the new signature yet. This is expected — Tasks 4 and 5 will fix.

- [x] **Step 4: Commit broken state (optional — skip this, fix in Tasks 4+5)**

---

### Task 4: Service — Rewrite IngestAsync with dedup + batch

**Files:**
- Modify: `Services/RagIngestionService.cs:1-75`
- Reference: `Settings/VectorStoreRegistryEntry.cs` (EmbeddingBatchSize)
- Reference: `Models/RagChunk.cs` (ComputeId)

- [x] **Step 1: Rewrite RagIngestionService**

Full file:
```csharp
#pragma warning disable SKEXP0050

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Text;
using VectorRAGvsPageIndexRAG.Models;
using VectorRAGvsPageIndexRAG.Services.Interfaces;
using VectorRAGvsPageIndexRAG.Settings;

namespace VectorRAGvsPageIndexRAG.Services;

public class RagIngestionService(
    IEmbeddingGenerator<string, Embedding<float>> embedder,
    VectorStore vectorStore,
    IOptions<VectorStoreRegistryEntry> vsConfig,
    ILogger<RagIngestionService> logger) : IRagIngestionService
{
    public async Task<RagIngestionResult> IngestAsync(string text, string fileName, string collectionName = "PDFs")
    {
        var cfg = vsConfig.Value;
        var collName = string.IsNullOrWhiteSpace(collectionName) ? cfg.DefaultCollectionName : collectionName;

        var lines = TextChunker.SplitPlainTextLines(text, cfg.ChunkSize);
        var paragraphs = TextChunker.SplitPlainTextParagraphs(lines, cfg.ChunkSize, cfg.ChunkOverlap);
        var chunks = paragraphs.Select((p, i) => new RagChunk
        {
            Id = RagChunk.ComputeId(p, fileName),
            Text = p,
            Source = fileName,
            ChunkIndex = i,
            TotalChunks = paragraphs.Count
        }).ToList();

        if (chunks.Count == 0)
        {
            logger.LogWarning("No chunks produced for document {File}", fileName);
            return new RagIngestionResult(fileName, 0, [], collName);
        }

        var allGuids = chunks.Select(c => Guid.Parse(c.Id)).ToList();
        var existingSet = new HashSet<Guid>();
        try
        {
            var collection = vectorStore.GetCollection<Guid, RagChunkRecord>(collName);
            await foreach (var record in collection.GetAsync(allGuids, null, cancellationToken: default))
                existingSet.Add(record.Key);
        }
        catch (Qdrant.Client.QdrantException)
        {
            // Collection doesn't exist yet — all chunks are new
        }

        var missing = chunks.Where(c => !existingSet.Contains(Guid.Parse(c.Id))).ToList();

        if (missing.Count > 0)
        {
            var batchSize = cfg.EmbeddingBatchSize;
            for (int i = 0; i < missing.Count; i += batchSize)
            {
                var batch = missing.Skip(i).Take(batchSize).ToList();
                var embeddings = await embedder.GenerateAsync(batch.Select(c => c.Text));
                if (embeddings.Count != batch.Count)
                    throw new InvalidOperationException($"Expected {batch.Count} embeddings, got {embeddings.Count}");
                for (int j = 0; j < batch.Count; j++)
                    batch[j].Vector = embeddings[j].Vector.ToArray();
            }

            var vectorSize = missing[0].Vector.Length;
            var definition = new VectorStoreCollectionDefinition
            {
                Properties = new List<VectorStoreProperty>
                {
                    new VectorStoreKeyProperty("Key", typeof(Guid)),
                    new VectorStoreDataProperty("Text", typeof(string)),
                    new VectorStoreDataProperty("Source", typeof(string)),
                    new VectorStoreDataProperty("ChunkIndex", typeof(int)),
                    new VectorStoreDataProperty("TotalChunks", typeof(int)),
                    new VectorStoreVectorProperty("Vector", typeof(float[]), vectorSize)
                }
            };

            var collection = vectorStore.GetCollection<Guid, RagChunkRecord>(collName, definition);
            await collection.EnsureCollectionExistsAsync();

            var records = missing.Select(c => new RagChunkRecord
            {
                Key = Guid.Parse(c.Id),
                Text = c.Text,
                Source = c.Source,
                ChunkIndex = c.ChunkIndex,
                TotalChunks = c.TotalChunks,
                Vector = c.Vector
            }).ToList();

            await collection.UpsertAsync(records);
        }

        logger.LogInformation("Ingested {File}: {Count} chunks ({Existing} existing, {New} new)",
            fileName, chunks.Count, chunks.Count - missing.Count, missing.Count);
        return new RagIngestionResult(fileName, chunks.Count, chunks, collName);
    }
}
```

- [x] **Step 2: Build check**

Run: `dotnet build`
Expected: Build fails — `RagController` still uses old `IngestAsync` signature. This is fixed in Task 5.

- [x] **Step 3: Commit**

```bash
git add Services/RagIngestionService.cs
git commit -m "feat: add dedup + batch embedding to RagIngestionService"
```

---

### Task 5: Controller — Add collectionName query param

**Files:**
- Modify: `Controllers/RagController.cs:17-32`

- [x] **Step 1: Add query param to Ingest action**

```csharp
[HttpPost("documents")]
[ProducesResponseType<RagIngestionResponse>(StatusCodes.Status201Created)]
[ProducesResponseType<string>(StatusCodes.Status400BadRequest)]
public async Task<IActionResult> Ingest(IFormFile file, [FromQuery] string collectionName = "PDFs")
{
    if (file == null || file.Length == 0)
        return BadRequest("No file provided.");

    if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        return BadRequest("Only PDF files are supported.");

    using var stream = file.OpenReadStream();
    var text = documentProcessor.ExtractText(stream);
    var result = await ingestionService.IngestAsync(text, file.FileName, collectionName);

    return CreatedAtAction(nameof(Ingest), new RagIngestionResponse(
        result.FileName,
        result.ChunkCount,
        result.CollectionName,
        result.Chunks.Select(c => new RagChunkResponse(c.Id, c.Text, c.ChunkIndex)).ToList()));
}
```

- [x] **Step 2: Full build check**

Run: `dotnet build`
Expected: Build succeeds.

---

### Task 6: Final verification

- [x] **Step 1: Verify build**

Run: `dotnet build`
Expected: 0 errors.

- [x] **Step 2: Update LEARNINGS.md**

Append to LEARNINGS.md:
```markdown
### 2026-06-21: Deterministic Chunk IDs + Batch Embedding
- Chunk IDs derived from SHA256(source + "\0" + text), truncated to 16 bytes → GUID
- **Why:** Enables idempotent re-ingestion — same text = same ID, upsert overwrites
- GetAsync before embedding finds existing chunks; only missing chunks are embedded
- EmbeddingBatchSize config controls how many chunks are sent per embedding API call
- **Edge case:** First ingestion (no collection yet) — GetAsync throws, caught by try/catch; all chunks treated as new
- collectionName optional query param on POST /api/rag/documents, defaults to DefaultCollectionName
```

- [x] **Step 3: Commit everything**

```bash
git add Controllers/RagController.cs DTOs/RagIngestionResponse.cs Services/Interfaces/IRagIngestionService.cs LEARNINGS.md
git commit -m "feat: add collectionName query param and finish ingestion hardening"
```

---

### Self-Review

**Spec coverage:**
- EmbeddingBatchSize config: Task 1
- Deterministic GUIDs: Task 2
- collectionName on interface + DTO: Task 3
- Dedup + batch embedding in service: Task 4
- Query param on controller: Task 5
- Response includes CollectionName: Task 3 + Task 5

**Placeholder scan:** No TBD, TODO, or "fill in details". Every step has complete code.

**Type consistency:** `ComputeId(string text, string source)` returns `string` → used in Task 4 as `GUID.Parse(c.Id)`. `IngestAsync` signature: `(string text, string fileName, string collectionName = "PDFs")` — consistent across interface (Task 3), service (Task 4), and controller (Task 5).