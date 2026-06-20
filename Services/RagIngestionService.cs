#pragma warning disable SKEXP0050

using Grpc.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.Text;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using VectorRAGvsPageIndexRAG.Settings;

namespace VectorRAGvsPageIndexRAG.Services;

public class RagIngestionService(
    IEmbeddingGenerator<string, Embedding<float>> embedder,
    QdrantClient qdrant,
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
        var actualVectorSize = embeddings[0].Vector.Length;
        for (int i = 0; i < chunks.Count; i++)
            chunks[i].Vector = embeddings[i].Vector.ToArray();

        var points = chunks.Select(c => new PointStruct
        {
            Id = new PointId { Uuid = c.Id },
            Vectors = c.Vector,
            Payload =
            {
                ["text"] = c.Text,
                ["source"] = c.Source,
                ["chunk_index"] = c.ChunkIndex,
                ["total_chunks"] = c.TotalChunks
            }
        }).ToList();

        try
        {
            await qdrant.CreateCollectionAsync(cfg.DefaultCollectionName, new VectorParams
            {
                Size = (ulong)actualVectorSize,
                Distance = Distance.Cosine
            });
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.AlreadyExists) { }

        await qdrant.UpsertAsync(cfg.DefaultCollectionName, points);

        logger.LogInformation("Ingested {File}: {Count} chunks (vector size: {Size})",
            fileName, chunks.Count, actualVectorSize);
        return new RagIngestionResult(fileName, chunks.Count, chunks);
    }
}

public record RagIngestionResult(string FileName, int ChunkCount, List<RagChunk> Chunks);
