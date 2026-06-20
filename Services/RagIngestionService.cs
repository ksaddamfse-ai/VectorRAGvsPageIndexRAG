using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Text;
using VectorRAGvsPageIndexRAG.Settings;

namespace VectorRAGvsPageIndexRAG.Services;

public class RagIngestionService(
    IEmbeddingGenerator<string, Embedding<float>> embedder,
    IVectorStore vectorStore,
    IOptions<VectorStoreRegistryEntry> vsConfig,
    ILogger<RagIngestionService> logger)
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

        var embeddings = await embedder.GenerateAsync(chunks.Select(c => c.Text));
        for (int i = 0; i < chunks.Count; i++)
            chunks[i].Vector = embeddings[i].Vector;

        var collection = vectorStore.GetCollection<string, RagChunk>(cfg.DefaultCollectionName);
        await collection.CreateCollectionIfNotExistsAsync();
        await collection.UpsertAsync(chunks);

        logger.LogInformation("Ingested {File}: {Count} chunks", fileName, chunks.Count);
        return new RagIngestionResult(fileName, chunks.Count, chunks);
    }
}

public record RagIngestionResult(string FileName, int ChunkCount, List<RagChunk> Chunks);
