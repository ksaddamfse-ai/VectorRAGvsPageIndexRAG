using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using VectorRAGvsPageIndexRAG.DTOs;
using VectorRAGvsPageIndexRAG.Models;
using VectorRAGvsPageIndexRAG.Settings;

namespace VectorRAGvsPageIndexRAG.Services;

public class PageIndexService(
    DocumentTreeBuilder treeBuilder,
    IChatClientFactory clientFactory,
    IOptions<PageIndexSettings> settings,
    ILogger<PageIndexService> logger)
{
    private readonly string _dbPath = settings.Value.DbPath;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    private async Task EnsureTablesAsync()
    {
        if (_initialized) return;
        await _initLock.WaitAsync();
        try
        {
            if (_initialized) return;
            using var db = new SqliteConnection($"Data Source={_dbPath}");
            await db.OpenAsync();

            var cmd = db.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS document_trees (
                    doc_id TEXT PRIMARY KEY,
                    file_name TEXT NOT NULL,
                    tree_json TEXT NOT NULL,
                    created_at TEXT NOT NULL DEFAULT (datetime('now'))
                );

                CREATE TABLE IF NOT EXISTS node_texts (
                    doc_id TEXT NOT NULL,
                    node_id TEXT NOT NULL,
                    text TEXT NOT NULL,
                    PRIMARY KEY (doc_id, node_id)
                );
                """;
            await cmd.ExecuteNonQueryAsync();
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<PageIndexIngestionResponse> IngestAsync(IFormFile file,
        string provider = "NvidiaNim", string model = "meta/llama-3.3-70b-instruct")
    {
        await EnsureTablesAsync();

        using var stream = file.OpenReadStream();
        var text = DocumentProcessor.ExtractText(stream);

        var tree = await treeBuilder.BuildTreeAsync(text, file.FileName, provider, model);

        var treeJson = JsonSerializer.Serialize(tree);

        using var db = new SqliteConnection($"Data Source={_dbPath}");
        await db.OpenAsync();

        using var tx = db.BeginTransaction();

        var insertTree = db.CreateCommand();
        insertTree.CommandText = """
            INSERT INTO document_trees (doc_id, file_name, tree_json)
            VALUES (@id, @name, @json)
            """;
        insertTree.Parameters.AddWithValue("@id", tree.DocId);
        insertTree.Parameters.AddWithValue("@name", tree.FileName);
        insertTree.Parameters.AddWithValue("@json", treeJson);
        await insertTree.ExecuteNonQueryAsync();

        var insertText = db.CreateCommand();
        insertText.CommandText = """
            INSERT OR IGNORE INTO node_texts (doc_id, node_id, text)
            VALUES (@docId, @nodeId, @text)
            """;
        var docIdParam = insertText.CreateParameter();
        docIdParam.ParameterName = "@docId";
        var nodeIdParam = insertText.CreateParameter();
        nodeIdParam.ParameterName = "@nodeId";
        var textParam = insertText.CreateParameter();
        textParam.ParameterName = "@text";
        insertText.Parameters.Add(docIdParam);
        insertText.Parameters.Add(nodeIdParam);
        insertText.Parameters.Add(textParam);

        foreach (var (nodeId, nodeText) in FlattenNodes(tree.Children))
        {
            docIdParam.Value = tree.DocId;
            nodeIdParam.Value = nodeId;
            textParam.Value = nodeText;
            await insertText.ExecuteNonQueryAsync();
        }

        tx.Commit();

        logger.LogInformation("PageIndex ingested {File}: {DocId} ({Nodes} nodes)",
            file.FileName, tree.DocId, tree.Children.Count);

        return new PageIndexIngestionResponse(
            tree.DocId, tree.FileName, "completed", tree.TotalPages, treeJson);
    }

    public async Task<PageIndexQueryResponse> QueryAsync(PageIndexQueryRequest request)
    {
        await EnsureTablesAsync();

        using var db = new SqliteConnection($"Data Source={_dbPath}");
        await db.OpenAsync();

        var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT tree_json FROM document_trees WHERE doc_id = @id";
        cmd.Parameters.AddWithValue("@id", request.DocId);
        var json = await cmd.ExecuteScalarAsync() as string;

        if (json is null)
            return new PageIndexQueryResponse("Document not found.", []);

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

        var selectText = db.CreateCommand();
        var placeholders = string.Join(",", nodeIds.Select((_, i) => $"@id{i}"));
        selectText.CommandText = $"SELECT node_id, text FROM node_texts WHERE doc_id = @docId AND node_id IN ({placeholders})";
        selectText.Parameters.AddWithValue("@docId", request.DocId);
        for (int i = 0; i < nodeIds.Count; i++)
            selectText.Parameters.AddWithValue($"@id{i}", nodeIds[i]);

        var selectedTexts = new Dictionary<string, string>();
        using var reader = await selectText.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            selectedTexts[reader.GetString(0)] = reader.GetString(1);

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

    private static IEnumerable<(string nodeId, string text)> FlattenNodes(List<TreeNode> nodes, string parentId = "")
    {
        foreach (var node in nodes)
        {
            var fullId = string.IsNullOrEmpty(parentId) ? node.NodeId : $"{parentId}.{node.NodeId}";
            if (!string.IsNullOrWhiteSpace(node.Text))
                yield return (node.NodeId, node.Text);

            foreach (var child in FlattenNodes(node.Children, fullId))
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
