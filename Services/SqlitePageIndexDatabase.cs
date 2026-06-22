using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using VectorRAGvsPageIndexRAG.Models;
using VectorRAGvsPageIndexRAG.Services.Interfaces;
using VectorRAGvsPageIndexRAG.Settings;
using System.Text.Json;

namespace VectorRAGvsPageIndexRAG.Services;

public class SqlitePageIndexDatabase : IPageIndexDatabase
{
    private readonly string _connectionString;

    public SqlitePageIndexDatabase(IOptions<PageIndexSettings> settings)
    {
        _connectionString = $"Data Source={settings.Value.DbPath};Cache=Shared";
    }

    private SqliteConnection CreateConnection()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA busy_timeout=5000;";
        cmd.ExecuteNonQuery();
        return conn;
    }

    public async Task InitializeAsync()
    {
        using var conn = CreateConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL;";
        await cmd.ExecuteNonQueryAsync();

        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS document_trees (
                doc_id TEXT PRIMARY KEY,
                file_name TEXT NOT NULL,
                tree_json TEXT NOT NULL,
                group_name TEXT NOT NULL DEFAULT '',
                created_at TEXT NOT NULL DEFAULT (datetime('now'))
            );
            CREATE INDEX IF NOT EXISTS idx_group_name ON document_trees(group_name);
            CREATE TABLE IF NOT EXISTS node_texts (
                doc_id TEXT NOT NULL,
                node_id TEXT NOT NULL,
                text TEXT NOT NULL,
                PRIMARY KEY (doc_id, node_id)
            );
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task InsertDocumentTreeAsync(DocumentTree tree, string treeJson)
    {
        using var conn = CreateConnection();
        using var tx = conn.BeginTransaction();

        var insertTree = conn.CreateCommand();
        insertTree.CommandText = """
            INSERT INTO document_trees (doc_id, file_name, tree_json, group_name)
            VALUES ($id, $name, $json, $group)
            """;
        insertTree.Parameters.AddWithValue("$id", tree.DocId);
        insertTree.Parameters.AddWithValue("$name", tree.FileName);
        insertTree.Parameters.AddWithValue("$json", treeJson);
        insertTree.Parameters.AddWithValue("$group", tree.GroupName);
        await insertTree.ExecuteNonQueryAsync();

        tx.Commit();
    }

    public async Task InsertNodeTextAsync(string docId, string nodeId, string text)
    {
        using var conn = CreateConnection();
        var insertText = conn.CreateCommand();
        insertText.CommandText = """
            INSERT OR IGNORE INTO node_texts (doc_id, node_id, text)
            VALUES ($docId, $nodeId, $text)
            """;
        insertText.Parameters.AddWithValue("$docId", docId);
        insertText.Parameters.AddWithValue("$nodeId", nodeId);
        insertText.Parameters.AddWithValue("$text", text);
        await insertText.ExecuteNonQueryAsync();
    }

    public async Task InsertNodeTextsAsync(string docId, IEnumerable<(string nodeId, string text)> nodeTexts)
    {
        using var conn = CreateConnection();
        using var tx = conn.BeginTransaction();

        var insertText = conn.CreateCommand();
        insertText.CommandText = """
            INSERT OR IGNORE INTO node_texts (doc_id, node_id, text)
            VALUES ($docId, $nodeId, $text)
            """;
        var docIdParam = insertText.CreateParameter();
        docIdParam.ParameterName = "$docId";
        var nodeIdParam = insertText.CreateParameter();
        nodeIdParam.ParameterName = "$nodeId";
        var textParam = insertText.CreateParameter();
        textParam.ParameterName = "$text";
        insertText.Parameters.Add(docIdParam);
        insertText.Parameters.Add(nodeIdParam);
        insertText.Parameters.Add(textParam);

        foreach (var (nodeId, nodeText) in nodeTexts)
        {
            docIdParam.Value = docId;
            nodeIdParam.Value = nodeId;
            textParam.Value = nodeText;
            await insertText.ExecuteNonQueryAsync();
        }

        tx.Commit();
    }

    public async Task<string?> GetDocumentTreeJsonAsync(string docId)
    {
        using var conn = CreateConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT tree_json FROM document_trees WHERE doc_id = $id";
        cmd.Parameters.AddWithValue("$id", docId);
        return await cmd.ExecuteScalarAsync() as string;
    }

    public async Task<List<(string DocId, string TreeJson)>> GetDocumentTreesByGroupAsync(string groupName)
    {
        using var conn = CreateConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT doc_id, tree_json FROM document_trees WHERE group_name = $group ORDER BY created_at";
        cmd.Parameters.AddWithValue("$group", groupName);

        var results = new List<(string, string)>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            results.Add((reader.GetString(0), reader.GetString(1)));
        return results;
    }

    public async Task<Dictionary<string, string>> GetNodeTextsAsync(string docId, List<string> nodeIds)
    {
        using var conn = CreateConnection();
        var selectText = conn.CreateCommand();
        var placeholders = string.Join(",", nodeIds.Select((_, i) => $"$id{i}"));
        selectText.CommandText = $"SELECT node_id, text FROM node_texts WHERE doc_id = $docId AND node_id IN ({placeholders})";
        selectText.Parameters.AddWithValue("$docId", docId);
        for (int i = 0; i < nodeIds.Count; i++)
            selectText.Parameters.AddWithValue($"$id{i}", nodeIds[i]);

        var selectedTexts = new Dictionary<string, string>();
        using var reader = await selectText.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            selectedTexts[reader.GetString(0)] = reader.GetString(1);

        return selectedTexts;
    }

    public async Task<Dictionary<string, string>> GetNodeTextsByNodeIdsAsync(List<string> nodeIds)
    {
        using var conn = CreateConnection();
        var selectText = conn.CreateCommand();
        var placeholders = string.Join(",", nodeIds.Select((_, i) => $"$id{i}"));
        selectText.CommandText = $"SELECT node_id, text FROM node_texts WHERE node_id IN ({placeholders})";
        for (int i = 0; i < nodeIds.Count; i++)
            selectText.Parameters.AddWithValue($"$id{i}", nodeIds[i]);

        var selectedTexts = new Dictionary<string, string>();
        using var reader = await selectText.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            selectedTexts[reader.GetString(0)] = reader.GetString(1);

        return selectedTexts;
    }
}
