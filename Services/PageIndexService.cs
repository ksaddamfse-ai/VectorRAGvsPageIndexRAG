using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using VectorRAGvsPageIndexRAG.DTOs;
using VectorRAGvsPageIndexRAG.Models;
using VectorRAGvsPageIndexRAG.Services.Interfaces;
using VectorRAGvsPageIndexRAG.Settings;

namespace VectorRAGvsPageIndexRAG.Services;

public class PageIndexService(
    DocumentTreeBuilder treeBuilder,
    IChatClientFactory clientFactory,
    IPageIndexDatabase database,
    IOptions<PageIndexSettings> settings,
    ILogger<PageIndexService> logger) : IPageIndexService
{
    public async Task<PageIndexIngestionResponse> IngestAsync(IFormFile file,
        string provider = "NvidiaNim", string model = "meta/llama-3.3-70b-instruct",
        string groupName = "PDFs")
    {
        using var stream = file.OpenReadStream();

        var (tree, pageTexts) = await treeBuilder.BuildTreeAsync(stream, file.FileName, provider, model);
        tree.GroupName = groupName;

        var treeJson = JsonSerializer.Serialize(tree);

        await database.InitializeAsync();
        await database.InsertDocumentAsync(tree, treeJson, pageTexts.Count);
        await database.InsertPageTextsAsync(tree.DocId, pageTexts);

        logger.LogInformation("PageIndex ingested {File}: {DocId} ({Nodes} nodes, {Pages} pages, group={Group})",
            file.FileName, tree.DocId, tree.Children.Count, pageTexts.Count, groupName);

        return new PageIndexIngestionResponse(
            tree.DocId, tree.FileName, "completed", tree.TotalPages, treeJson);
    }

    public async Task<PageIndexQueryResponse?> QueryAsync(PageIndexQueryRequest request)
    {
        var trees = await database.GetDocumentTreesByGroupAsync(request.GroupName);
        if (trees.Count == 0)
            return null;

        var client = clientFactory.GetClient($"{request.Provider}__{request.Model}")
            ?? throw new InvalidOperationException(
                $"Client not found for {request.Provider}/{request.Model}");

        var allNodes = new List<(string DocId, TreeNode Node)>();
        foreach (var (docId, treeJson) in trees)
        {
            var tree = JsonSerializer.Deserialize<DocumentTree>(treeJson)!;
            CollectNodes(docId, tree.Children, allNodes);
        }

        var maxDepth = settings.Value.MaxSkeletonDepth;
        var skeleton = BuildTruncatedSkeleton(trees, maxDepth);

        var navPrompt = $"""
            You are a document navigator. Below is the table of contents of a document.
            Each node has a title, summary, and page numbers.

            {skeleton}

            Question: {request.Question}

            Return ONLY a JSON array of node IDs that are relevant to the question.
            Example: ["node_001", "node_003"]
            """;

        var navResponse = await client.GetResponseAsync(navPrompt);
        var navContent = navResponse?.Text;
        if (string.IsNullOrWhiteSpace(navContent))
            return new PageIndexQueryResponse("No relevant sections found.", [], []);

        var selectedIds = ParseNodeIds(navContent);
        if (selectedIds.Count == 0)
            return new PageIndexQueryResponse("No relevant sections found.", [], []);

        var (context, citations) = await RetrieveContextAsync(allNodes, selectedIds, trees);

        if (string.IsNullOrWhiteSpace(context))
            return new PageIndexQueryResponse("No relevant sections found.", [], []);

        var answerPrompt = $"""
            Answer the question using ONLY the context below.

            Context:
            {context}

            Question: {request.Question}
            """;

        var answerResponse = await client.GetResponseAsync(answerPrompt);

        return new PageIndexQueryResponse(
            answerResponse?.Text ?? "No answer generated.",
            [],
            citations);
    }

    private async Task<(string Context, List<PageCitation> Citations)> RetrieveContextAsync(
        List<(string DocId, TreeNode Node)> allNodes,
        List<string> selectedIds,
        List<(string DocId, string TreeJson)> trees)
    {
        var maxTokens = settings.Value.MaxTokensPerQuery;
        var usedChars = 0;
        var contextParts = new List<string>();
        var citations = new List<PageCitation>();

        var selectedNodes = allNodes.Where(n => selectedIds.Contains(n.Node.NodeId)).ToList();

        foreach (var (docId, node) in selectedNodes)
        {
            var pagesToRetrieve = CollectPageRange(node);

            if (pagesToRetrieve.Count > 0)
            {
                var pageTexts = await database.GetPageTextsAsync(docId, pagesToRetrieve.Min(), pagesToRetrieve.Max());
                foreach (var kvp in pageTexts.OrderBy(kv => kv.Key))
                {
                    var page = kvp.Key;
                    var text = kvp.Value;
                    var estimatedTokens = text.Length / 4 + 1;
                    if (usedChars + text.Length > maxTokens * 4)
                        break;

                    contextParts.Add($"[Page {page}]\n{text}");
                    usedChars += text.Length;
                }
            }

            citations.Add(new PageCitation(
                node.Title,
                docId,
                node.StartPage ?? 0,
                node.EndPage ?? 0));
        }

        return (string.Join("\n\n", contextParts), citations);
    }

    private static HashSet<int> CollectPageRange(TreeNode node)
    {
        var pages = new HashSet<int>();
        if (node.StartPage.HasValue && node.EndPage.HasValue)
        {
            for (int p = node.StartPage.Value; p <= node.EndPage.Value; p++)
                pages.Add(p);
        }
        return pages;
    }

    private static string BuildTruncatedSkeleton(
        List<(string DocId, string TreeJson)> trees, int maxDepth)
    {
        var lines = new List<string>();
        foreach (var (docId, treeJson) in trees)
        {
            var tree = JsonSerializer.Deserialize<DocumentTree>(treeJson)!;
            lines.Add($"Document: {tree.FileName} (doc_id={docId})");
            BuildSkeletonLines(lines, tree.Children, "  ", 1, maxDepth);
            lines.Add("");
        }
        return string.Join("\n", lines);
    }

    private static void BuildSkeletonLines(
        List<string> lines, List<TreeNode> nodes, string indent, int depth, int maxDepth)
    {
        foreach (var node in nodes)
        {
            var pageInfo = node.StartPage.HasValue ? $" (pages {node.StartPage}-{node.EndPage})" : "";
            lines.Add($"{indent}- {node.NodeId}: {node.Title}{pageInfo} — {node.Summary}");

            if (node.Children.Count > 0 && depth < maxDepth)
                BuildSkeletonLines(lines, node.Children, indent + "  ", depth + 1, maxDepth);
        }
    }

    private static void CollectNodes(string docId, List<TreeNode> nodes, List<(string DocId, TreeNode Node)> result)
    {
        foreach (var node in nodes)
        {
            result.Add((docId, node));
            CollectNodes(docId, node.Children, result);
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
}
