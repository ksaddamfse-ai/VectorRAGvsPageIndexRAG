using VectorRAGvsPageIndexRAG.Models;

namespace VectorRAGvsPageIndexRAG.Services.Interfaces;

public interface IPageIndexDatabase
{
    Task InitializeAsync();
    Task InsertDocumentAsync(DocumentTree tree, string treeJson, int pageCount);
    Task InsertPageTextsAsync(string docId, IEnumerable<(int pageNumber, string text)> pageTexts);
    Task<List<(string DocId, string TreeJson)>> GetDocumentTreesByGroupAsync(string groupName);
    Task<Dictionary<int, string>> GetPageTextsAsync(string docId, int startPage, int endPage);
}
