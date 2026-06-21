using System.Text.Json;
using Microsoft.Extensions.AI;
using VectorRAGvsPageIndexRAG.Models;
using VectorRAGvsPageIndexRAG.Services.Interfaces;
namespace VectorRAGvsPageIndexRAG.Services;

public class DocumentTreeBuilder(
    IChatClientFactory clientFactory,
    ILogger<DocumentTreeBuilder> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public async Task<DocumentTree> BuildTreeAsync(string text, string fileName,
        string provider, string model)
    {
        var client = clientFactory.GetClient($"{provider}__{model}")
            ?? throw new InvalidOperationException($"Client not found for {provider}/{model}");

        var template = """
            You are a document structure analyzer. Given the full text of a document,
            build a hierarchical JSON tree representing its table of contents.

            Rules:
            - Each node must have: title, node_id (unique), summary (1 sentence)
            - Leaf nodes must include the "text" field with the full raw text of that section
            - Parent nodes should NOT include text (only children have text)
            - Estimate total_pages based on content length (~1 page per 300 words)

            Return ONLY valid JSON matching this schema:
            {
              "title": "document title",
              "node_id": "root_001",
              "summary": "brief description",
              "total_pages": 10,
              "children": [
                {
                  "title": "Section title",
                  "node_id": "sec_01",
                  "summary": "section summary",
                  "text": "full raw text of leaf section",
                  "children": []
                }
              ]
            }
            """;

        var prompt = $"{template}\n\nDocument:\n{text}";

        logger.LogInformation("Building tree for {File} using {Provider}/{Model}", fileName, provider, model);

        var response = await client.GetResponseAsync(prompt);
        var content = response?.Text;
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("LLM returned empty tree");

        var cleaned = SanitizeJson(content);
        var tree = JsonSerializer.Deserialize<DocumentTree>(cleaned, JsonOptions)
            ?? throw new InvalidOperationException("Failed to parse tree JSON");

        tree.DocId = $"pi-{Guid.NewGuid():N}"[..12];
        tree.FileName = fileName;
        return tree;
    }

    private static string SanitizeJson(string raw)
    {
        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        if (start < 0 || end < 0)
            throw new InvalidOperationException("No JSON object found in LLM response");
        return raw[start..(end + 1)];
    }
}
