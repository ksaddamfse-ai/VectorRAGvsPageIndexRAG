using VectorRAGvsPageIndexRAG.Models;

namespace VectorRAGvsPageIndexRAG.Services.Interfaces;

public interface IPageIndexDatabase
{
    Task InitializeAsync();

    Task InsertDocumentTreeAsync(DocumentTree tree, string treeJson);

    Task InsertNodeTextAsync(string docId, string nodeId, string text);

    Task InsertNodeTextsAsync(string docId, IEnumerable<(string nodeId, string text)> nodeTexts);

    Task<string?> GetDocumentTreeJsonAsync(string docId);

    Task<Dictionary<string, string>> GetNodeTextsAsync(string docId, List<string> nodeIds);
}
