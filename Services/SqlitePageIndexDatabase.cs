using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using VectorRAGvsPageIndexRAG.Models;
using VectorRAGvsPageIndexRAG.Services.Interfaces;
using VectorRAGvsPageIndexRAG.Settings;
using System.Text.Json;

namespace VectorRAGvsPageIndexRAG.Services;

public class SqlitePageIndexDatabase : IPageIndexDatabase, IDisposable
{
    private readonly SqliteConnection _db;

    public SqlitePageIndexDatabase(IOptions<PageIndexSettings> settings)
    {
        var connStr = $"Data Source={settings.Value.DbPath}";
        _db = new SqliteConnection(connStr);
        _db.Open();
    }

    public void Dispose() => _db.Dispose();

    public async Task InitializeAsync()
    {
        var initCmd = _db.CreateCommand();
        initCmd.CommandText = """
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
        await initCmd.ExecuteNonQueryAsync();
    }

    public async Task InsertDocumentTreeAsync(DocumentTree tree, string treeJson)
    {
        using var tx = _db.BeginTransaction();

        var insertTree = _db.CreateCommand();
        insertTree.CommandText = """
            INSERT INTO document_trees (doc_id, file_name, tree_json)
            VALUES ($id, $name, $json)
            """;
        insertTree.Parameters.AddWithValue("$id", tree.DocId);
        insertTree.Parameters.AddWithValue("$name", tree.FileName);
        insertTree.Parameters.AddWithValue("$json", treeJson);
        await insertTree.ExecuteNonQueryAsync();

        tx.Commit();
    }

    public async Task InsertNodeTextAsync(string docId, string nodeId, string text)
    {
        var insertText = _db.CreateCommand();
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
        using var tx = _db.BeginTransaction();

        var insertText = _db.CreateCommand();
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
        var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT tree_json FROM document_trees WHERE doc_id = $id";
        cmd.Parameters.AddWithValue("$id", docId);
        var json = await cmd.ExecuteScalarAsync() as string;

        return json;
    }

    public async Task<Dictionary<string, string>> GetNodeTextsAsync(string docId, List<string> nodeIds)
    {
        var selectText = _db.CreateCommand();
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
}
