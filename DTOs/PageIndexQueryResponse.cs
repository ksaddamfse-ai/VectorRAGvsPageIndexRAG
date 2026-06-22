namespace VectorRAGvsPageIndexRAG.DTOs;

public record PageCitation(string NodeTitle, string DocId, int StartPage, int EndPage);

public record PageIndexQueryResponse(
    string Answer,
    List<PageIndexCitation> Citations,
    List<PageCitation> PageCitations);
