using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using VectorRAGvsPageIndexRAG.Models;
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

    private sealed class LlmTreeRoot
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = "";

        [JsonPropertyName("node_id")]
        public string NodeId { get; set; } = "";

        [JsonPropertyName("summary")]
        public string Summary { get; set; } = "";

        [JsonPropertyName("total_pages")]
        public int TotalPages { get; set; }

        [JsonPropertyName("children")]
        public List<TreeNode> Children { get; set; } = [];
    }

    public async Task<DocumentTree> BuildTreeAsync(string text, string fileName,
        string provider = "NvidiaNim", string model = "meta/llama-3.3-70b-instruct")
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
        var root = JsonSerializer.Deserialize<LlmTreeRoot>(cleaned, JsonOptions)
            ?? throw new InvalidOperationException("Failed to parse tree JSON");

        var docId = $"pi-{Guid.NewGuid():N}"[..12];

        return new DocumentTree
        {
            DocId = docId,
            FileName = fileName,
            Title = root.Title,
            NodeId = root.NodeId,
            Summary = root.Summary,
            TotalPages = root.TotalPages,
            Children = root.Children
        };
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
