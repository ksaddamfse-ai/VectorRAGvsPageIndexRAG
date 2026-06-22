using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using UglyToad.PdfPig;
using VectorRAGvsPageIndexRAG.Models;
using VectorRAGvsPageIndexRAG.Services.Interfaces;

namespace VectorRAGvsPageIndexRAG.Services;

public class DocumentTreeBuilder(
    IChatClientFactory clientFactory,
    PdfStructureParser parser,
    ILogger<DocumentTreeBuilder> logger)
{
    public async Task<(DocumentTree Tree, List<(int PageNumber, string Text)> PageTexts)> BuildTreeAsync(
        Stream pdfStream, string fileName, string provider, string model)
    {
        var pageTexts = ExtractPageTexts(pdfStream);

        using var ms = new MemoryStream();
        pdfStream.Position = 0;
        await pdfStream.CopyToAsync(ms);
        ms.Position = 0;

        var tree = parser.Parse(ms, fileName);

        logger.LogInformation("Parsed tree for {File}: {Nodes} nodes", fileName, tree.Children.Count);

        var client = clientFactory.GetClient($"{provider}__{model}")
            ?? throw new InvalidOperationException($"Client not found for {provider}/{model}");

        await GenerateSummariesAsync(client, tree.Children);

        tree.DocId = $"pi-{Guid.NewGuid():N}"[..12];
        tree.FileName = fileName;
        return (tree, pageTexts);
    }

    private static List<(int PageNumber, string Text)> ExtractPageTexts(Stream pdfStream)
    {
        using var pdf = PdfDocument.Open(pdfStream);
        var result = new List<(int, string)>();

        foreach (var page in pdf.GetPages())
        {
            var text = page.Text;
            result.Add((page.Number, text));
        }

        return result;
    }

    private async Task GenerateSummariesAsync(IChatClient client, List<TreeNode> nodes)
    {
        var tasks = nodes.Select(node => GenerateSingleSummaryAsync(client, node)).ToList();
        await Task.WhenAll(tasks);

        foreach (var node in nodes)
        {
            if (node.Children.Count > 0)
                await GenerateSummariesAsync(client, node.Children);
        }
    }

    private async Task GenerateSingleSummaryAsync(IChatClient client, TreeNode node)
    {
        if (node.StartPage == null || node.EndPage == null)
            return;

        var prompt = $"""
            Summarize this section in one sentence:

            {node.Title}
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
}
