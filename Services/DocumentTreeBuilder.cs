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

        var pageTextDict = pageTexts.ToDictionary(p => p.PageNumber, p => p.Text);
        await GenerateSummariesAsync(client, tree.Children, pageTextDict);

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

    private async Task GenerateSummariesAsync(IChatClient client, List<TreeNode> nodes,
        Dictionary<int, string> pageTextDict)
    {
        var tasks = nodes.Select(node => GenerateSingleSummaryAsync(client, node, pageTextDict)).ToList();
        await Task.WhenAll(tasks);

        foreach (var node in nodes)
        {
            if (node.Children.Count > 0)
                await GenerateSummariesAsync(client, node.Children, pageTextDict);
        }
    }

    private async Task GenerateSingleSummaryAsync(IChatClient client, TreeNode node,
        Dictionary<int, string> pageTextDict)
    {
        if (node.StartPage == null || node.EndPage == null)
            return;

        var sectionText = string.Join("\n",
            Enumerable.Range(node.StartPage.Value, node.EndPage.Value - node.StartPage.Value + 1)
                .Where(pageTextDict.ContainsKey)
                .Select(page => $"[Page {page}] {pageTextDict[page]}"));

        if (string.IsNullOrWhiteSpace(sectionText))
            return;

        var truncated = sectionText.Length > 3000 ? sectionText[..3000] + "..." : sectionText;
        var prompt = $"""
            Summarize this section in one sentence:

            {node.Title}

            {truncated}
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
