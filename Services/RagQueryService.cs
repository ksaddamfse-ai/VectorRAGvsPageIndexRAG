using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;
using RAGBench.DTOs;
using RAGBench.Models;
using RAGBench.Services.Interfaces;
using RAGBench.Settings;

namespace RAGBench.Services;

public class RagQueryService(
    IChatClientFactory clientFactory,
    IEmbeddingGenerator<string, Embedding<float>> embedder,
    VectorStore vectorStore,
    IOptions<VectorStoreRegistryEntry> vsConfig,
    IOptions<Dictionary<string, int>> contextWindows) : IRagQueryService
{
    private const int ReservedOutputTokens = 2048;
    private const double SafetyMargin = 0.07;

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

        var client = clientFactory.GetClient($"{request.Provider}__{request.Model}");

        if (client is null)
            return new RagQueryResponse(
                $"Provider '{request.Provider}' model '{request.Model}' not found.", chunks);

        var packedChunks = PackChunks(chunks, request.Provider, request.Model, request.Question);
        var context = string.Join("\n---\n", packedChunks.Select(c => c.Text));

        var answer = await client.GetResponseAsync(
            $"Answer the question using only the context below.\n\nContext:\n{context}\n\nQuestion: {request.Question}");

        return new RagQueryResponse(answer.Text ?? "No answer generated.", chunks);
    }

    private List<RagChunkResult> PackChunks(List<RagChunkResult> chunks, string provider, string model, string question)
    {
        var key = $"{provider}__{model}";
        if (!contextWindows.Value.TryGetValue(key, out var modelWindow))
            return chunks;

        var systemPrompt = "Answer the question using only the context below.";
        var budget = modelWindow
            - EstimateTokens(systemPrompt)
            - EstimateTokens(question)
            - (int)(modelWindow * SafetyMargin)
            - ReservedOutputTokens;

        if (budget <= 0)
            return chunks;

        var ranked = chunks.OrderByDescending(c => c.Score).ToList();
        var packed = new List<RagChunkResult>();
        var used = 0;

        foreach (var chunk in ranked)
        {
            var chunkTokens = EstimateTokens(chunk.Text);
            if (used + chunkTokens > budget)
                continue;
            packed.Add(chunk);
            used += chunkTokens;
        }

        return packed.Count == 0 ? [ranked[0]] : packed;
    }

    private static int EstimateTokens(string text) => text.Length / 4 + 1;
}
