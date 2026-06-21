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
    public async Task<RagIngestionResult> IngestAsync(string text, string fileName)
    {
        var cfg = vsConfig.Value;
        var lines = TextChunker.SplitPlainTextLines(text, cfg.ChunkSize);
        var paragraphs = TextChunker.SplitPlainTextParagraphs(lines, cfg.ChunkSize, cfg.ChunkOverlap);
        var chunks = paragraphs.Select((p, i) => new RagChunk
        {
            Id = Guid.NewGuid().ToString(),
            Text = p,
            Source = fileName,
            ChunkIndex = i,
            TotalChunks = paragraphs.Count
        }).ToList();

        if (chunks.Count == 0)
        {
            logger.LogWarning("No chunks produced for document {File}", fileName);
            return new RagIngestionResult(fileName, 0, []);
        }

        var embeddings = await embedder.GenerateAsync(chunks.Select(c => c.Text));
        var vectorSize = embeddings[0].Vector.Length;
        for (int i = 0; i < chunks.Count; i++)
            chunks[i].Vector = embeddings[i].Vector.ToArray();

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

        var collection = vectorStore.GetCollection<Guid, RagChunkRecord>(cfg.DefaultCollectionName, definition);
        await collection.EnsureCollectionExistsAsync();

        var records = chunks.Select(c => new RagChunkRecord
        {
            Key = Guid.Parse(c.Id),
            Text = c.Text,
            Source = c.Source,
            ChunkIndex = c.ChunkIndex,
            TotalChunks = c.TotalChunks,
            Vector = c.Vector
        }).ToList();

        await collection.UpsertAsync(records);

        logger.LogInformation("Ingested {File}: {Count} chunks", fileName, chunks.Count);
        return new RagIngestionResult(fileName, chunks.Count, chunks);
    }
}
