using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using VectorRAGvsPageIndexRAG.Settings;

namespace VectorRAGvsPageIndexRAG.Services;

public class RagQueryService(
    IChatClientFactory clientFactory,
    IEmbeddingGenerator<string, Embedding<float>> embedder,
    QdrantClient qdrant,
    IOptions<VectorStoreRegistryEntry> vsConfig)
{
    public async Task<RagQueryResponse> QueryAsync(RagQueryRequest request)
    {
        var queryEmbedding = await embedder.GenerateAsync(request.Question);
        var vector = queryEmbedding.Vector.ToArray();

        await qdrant.CreateCollectionAsync(vsConfig.Value.DefaultCollectionName, new VectorParams
        {
            Size = (ulong)vsConfig.Value.VectorSize,
            Distance = Distance.Cosine
        });

        var results = await qdrant.SearchAsync(
            vsConfig.Value.DefaultCollectionName,
            vector,
            limit: (ulong)request.TopK);

        var chunks = results.Select(r => new RagChunkResult(
            r.Payload["text"].StringValue,
            r.Payload["source"].StringValue,
            r.Score)).ToList();

        if (chunks.Count == 0)
            return new RagQueryResponse("No relevant context found.", []);

        var context = string.Join("\n---\n", chunks.Select(c => c.Text));
        var client = clientFactory.GetClient($"{request.Provider}__{request.Model}");

        if (client is null)
            return new RagQueryResponse(
                $"Provider '{request.Provider}' model '{request.Model}' not found.", chunks);

        var answer = await client.GetResponseAsync(
            $"Answer the question using only the context below.\n\nContext:\n{context}\n\nQuestion: {request.Question}");

        return new RagQueryResponse(answer.Text ?? "No answer generated.", chunks);
    }
}

public record RagQueryRequest(
    string Question,
    string Provider,
    string Model,
    int TopK = 5);

public record RagQueryResponse(
    string Answer,
    List<RagChunkResult> Chunks);

public record RagChunkResult(
    string Text,
    string Source,
    double Score);
