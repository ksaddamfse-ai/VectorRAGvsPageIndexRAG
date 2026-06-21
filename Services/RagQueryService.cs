using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;
using VectorRAGvsPageIndexRAG.DTOs;
using VectorRAGvsPageIndexRAG.Models;
using VectorRAGvsPageIndexRAG.Services.Interfaces;
using VectorRAGvsPageIndexRAG.Settings;

namespace VectorRAGvsPageIndexRAG.Services;

public class RagQueryService(
    IChatClientFactory clientFactory,
    IEmbeddingGenerator<string, Embedding<float>> embedder,
    VectorStore vectorStore,
    IOptions<VectorStoreRegistryEntry> vsConfig) : IRagQueryService
{
    public async Task<RagQueryResponse> QueryAsync(RagQueryRequest request)
    {
        var queryEmbedding = await embedder.GenerateAsync(request.Question);
        var queryVector = new ReadOnlyMemory<float>(queryEmbedding.Vector.ToArray());

        var collName = string.IsNullOrWhiteSpace(request.CollectionName)
            ? vsConfig.Value.DefaultCollectionName : request.CollectionName;
        var collection = vectorStore.GetCollection<Guid, RagChunkRecord>(collName);
        var chunks = new List<RagChunkResult>();
        await foreach (var result in collection.SearchAsync(queryVector, top: request.TopK))
            chunks.Add(new RagChunkResult(result.Record.Text, result.Record.Source, result.Score ?? 0));

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
