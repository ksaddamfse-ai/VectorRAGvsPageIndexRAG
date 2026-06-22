using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using VectorRAGvsPageIndexRAG.Models;
using VectorRAGvsPageIndexRAG.Services.Interfaces;
namespace VectorRAGvsPageIndexRAG.Services;

public class DocumentTreeBuilder(
    IChatClientFactory clientFactory,
    PdfStructureParser parser,
    ILogger<DocumentTreeBuilder> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public async Task<DocumentTree> BuildTreeAsync(Stream pdfStream, string fileName,
        string provider, string model)
    {
        // Phase 1: Deterministic parsing — structure + text
        var tree = parser.Parse(pdfStream, fileName);

        logger.LogInformation("Parsed tree for {File}: {Nodes} nodes", fileName, tree.Children.Count);

        // Phase 2: LLM only for summaries (understanding, not structure)
        var client = clientFactory.GetClient($"{provider}__{model}")
            ?? throw new InvalidOperationException($"Client not found for {provider}/{model}");

        await GenerateSummariesAsync(client, tree.Children);

        tree.DocId = $"pi-{Guid.NewGuid():N}"[..12];
        tree.FileName = fileName;
        return tree;
    }

    private async Task GenerateSummariesAsync(IChatClient client, List<TreeNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (!string.IsNullOrWhiteSpace(node.Text))
            {
                var prompt = $"""
                    Summarize this text in one sentence:

                    {node.Text}
                    """;

                try
                {
                    var response = await client.GetResponseAsync(prompt);
                    node.Summary = response?.Text?.Trim() ?? "";
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to generate summary for node {NodeId}", node.NodeId);
                    node.Summary = "";
                }
            }

            if (node.Children.Count > 0)
                await GenerateSummariesAsync(client, node.Children);
        }
    }
}
