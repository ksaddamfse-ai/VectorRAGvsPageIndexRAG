using System.Text.Json;
using Microsoft.Extensions.AI;
using VectorRAGvsPageIndexRAG.DTOs;
using VectorRAGvsPageIndexRAG.Models;
using VectorRAGvsPageIndexRAG.Services.Interfaces;

namespace VectorRAGvsPageIndexRAG.Services;

public class PageIndexService(
    DocumentTreeBuilder treeBuilder,
    IChatClientFactory clientFactory,
    IDocumentProcessor documentProcessor,
    IPageIndexDatabase database,
    ILogger<PageIndexService> logger) : IPageIndexService
{

    public async Task<PageIndexIngestionResponse> IngestAsync(IFormFile file,
        string provider = "NvidiaNim", string model = "meta/llama-3.3-70b-instruct")
    {
        using var stream = file.OpenReadStream();
        var text = documentProcessor.ExtractText(stream);

        var tree = await treeBuilder.BuildTreeAsync(text, file.FileName, provider, model);

        var treeJson = JsonSerializer.Serialize(tree);

        await database.InitializeAsync();
        await database.InsertDocumentTreeAsync(tree, treeJson);
        await database.InsertNodeTextsAsync(tree.DocId, FlattenNodes(tree.Children));

        logger.LogInformation("PageIndex ingested {File}: {DocId} ({Nodes} nodes)",
            file.FileName, tree.DocId, tree.Children.Count);

        return new PageIndexIngestionResponse(
            tree.DocId, tree.FileName, "completed", tree.TotalPages, treeJson);
    }

    public async Task<PageIndexQueryResponse?> QueryAsync(PageIndexQueryRequest request)
    {
        var json = await database.GetDocumentTreeJsonAsync(request.DocId);

        if (json is null)
            return null;

        var tree = JsonSerializer.Deserialize<DocumentTree>(json)!;

        var skeleton = BuildSkeleton(tree);

        var client = clientFactory.GetClient($"{request.Provider}__{request.Model}")
            ?? throw new InvalidOperationException(
                $"Client not found for {request.Provider}/{request.Model}");

        var navPrompt = $"""
            You are a document navigator. Below is the table of contents of a document.
            Each node has a title and summary.

            {skeleton}

            Question: {request.Question}

            Return ONLY a JSON array of node IDs that are relevant to the question.
            Example: ["sec_01", "sub_sec_02a"]
            """;

        var navResponse = await client.GetResponseAsync(navPrompt);
        var navContent = navResponse?.Text;
        if (string.IsNullOrWhiteSpace(navContent))
            return new PageIndexQueryResponse("No relevant sections found.", []);

        var nodeIds = ParseNodeIds(navContent);
        if (nodeIds.Count == 0)
            return new PageIndexQueryResponse("No relevant sections found.", []);

        var selectedTexts = await database.GetNodeTextsAsync(request.DocId, nodeIds);

        if (selectedTexts.Count == 0)
            return new PageIndexQueryResponse("No relevant sections found.", []);

        var context = string.Join("\n\n", selectedTexts.Select(kv => kv.Value));

        var answerPrompt = $"""
            Answer the question using ONLY the context below.

            Context:
            {context}

            Question: {request.Question}
            """;

        var answerResponse = await client.GetResponseAsync(answerPrompt);

        var citations = FindCitations(tree.Children, nodeIds)
            .Select(n => new PageIndexCitation(n.Title, 0, n.Text))
            .ToList();

        return new PageIndexQueryResponse(answerResponse?.Text ?? "No answer generated.", citations);
    }

    private static IEnumerable<(string nodeId, string text)> FlattenNodes(List<TreeNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (!string.IsNullOrWhiteSpace(node.Text))
                yield return (node.NodeId, node.Text);

            foreach (var child in FlattenNodes(node.Children))
                yield return child;
        }
    }

    private static string BuildSkeleton(DocumentTree tree)
    {
        var lines = new List<string> { $"Document: {tree.Title}" };
        BuildSkeletonLines(lines, tree.Children, "  ");
        return string.Join("\n", lines);
    }

    private static void BuildSkeletonLines(List<string> lines, List<TreeNode> nodes, string indent)
    {
        foreach (var node in nodes)
        {
            lines.Add($"{indent}- {node.NodeId}: {node.Title} — {node.Summary}");
            if (node.Children.Count > 0)
                BuildSkeletonLines(lines, node.Children, indent + "  ");
        }
    }

    private static List<string> ParseNodeIds(string raw)
    {
        try
        {
            var start = raw.IndexOf('[');
            var end = raw.LastIndexOf(']');
            if (start >= 0 && end > start)
                raw = raw[start..(end + 1)];

            var ids = JsonSerializer.Deserialize<List<string>>(raw);
            return ids ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static List<TreeNode> FindCitations(List<TreeNode> nodes, List<string> nodeIds)
    {
        var results = new List<TreeNode>();
        foreach (var node in nodes)
        {
            if (nodeIds.Contains(node.NodeId) && !string.IsNullOrWhiteSpace(node.Text))
                results.Add(node);
            results.AddRange(FindCitations(node.Children, nodeIds));
        }
        return results;
    }
}
