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
        catch (Exception ex) when (ex is Qdrant.Client.QdrantException or VectorStoreException)
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
