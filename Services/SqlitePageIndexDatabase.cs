using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using VectorRAGvsPageIndexRAG.Models;
using VectorRAGvsPageIndexRAG.Services.Interfaces;
using VectorRAGvsPageIndexRAG.Settings;

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
            CREATE TABLE IF NOT EXISTS documents (
                doc_id TEXT PRIMARY KEY,
                file_name TEXT NOT NULL,
                tree_json TEXT NOT NULL,
                group_name TEXT NOT NULL DEFAULT '',
                page_count INTEGER NOT NULL DEFAULT 0,
                created_at TEXT NOT NULL DEFAULT (datetime('now'))
            );
            CREATE INDEX IF NOT EXISTS idx_group_name ON documents(group_name);
            CREATE TABLE IF NOT EXISTS page_texts (
                doc_id TEXT NOT NULL,
                page_number INTEGER NOT NULL,
                text TEXT NOT NULL,
                PRIMARY KEY (doc_id, page_number)
            );
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task InsertDocumentAsync(DocumentTree tree, string treeJson, int pageCount)
    {
        using var conn = CreateConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO documents (doc_id, file_name, tree_json, group_name, page_count)
            VALUES ($id, $name, $json, $group, $pageCount)
            """;
        cmd.Parameters.AddWithValue("$id", tree.DocId);
        cmd.Parameters.AddWithValue("$name", tree.FileName);
        cmd.Parameters.AddWithValue("$json", treeJson);
        cmd.Parameters.AddWithValue("$group", tree.GroupName);
        cmd.Parameters.AddWithValue("$pageCount", pageCount);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task InsertPageTextsAsync(string docId, IEnumerable<(int pageNumber, string text)> pageTexts)
    {
        using var conn = CreateConnection();
        using var tx = conn.BeginTransaction();

        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO page_texts (doc_id, page_number, text)
            VALUES ($docId, $page, $text)
            """;
        var docIdParam = cmd.CreateParameter();
        docIdParam.ParameterName = "$docId";
        var pageParam = cmd.CreateParameter();
        pageParam.ParameterName = "$page";
        var textParam = cmd.CreateParameter();
        textParam.ParameterName = "$text";
        cmd.Parameters.Add(docIdParam);
        cmd.Parameters.Add(pageParam);
        cmd.Parameters.Add(textParam);

        foreach (var (pageNumber, text) in pageTexts)
        {
            docIdParam.Value = docId;
            pageParam.Value = pageNumber;
            textParam.Value = text;
            await cmd.ExecuteNonQueryAsync();
        }

        tx.Commit();
    }

    public async Task<List<(string DocId, string TreeJson)>> GetDocumentTreesByGroupAsync(string groupName)
    {
        using var conn = CreateConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT doc_id, tree_json FROM documents WHERE group_name = $group ORDER BY created_at";
        cmd.Parameters.AddWithValue("$group", groupName);

        var results = new List<(string, string)>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            results.Add((reader.GetString(0), reader.GetString(1)));
        return results;
    }

    public async Task<Dictionary<int, string>> GetPageTextsAsync(string docId, int startPage, int endPage)
    {
        using var conn = CreateConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT page_number, text FROM page_texts
            WHERE doc_id = $docId AND page_number BETWEEN $start AND $end
            ORDER BY page_number
            """;
        cmd.Parameters.AddWithValue("$docId", docId);
        cmd.Parameters.AddWithValue("$start", startPage);
        cmd.Parameters.AddWithValue("$end", endPage);

        var result = new Dictionary<int, string>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result[reader.GetInt32(0)] = reader.GetString(1);
        return result;
    }
}
