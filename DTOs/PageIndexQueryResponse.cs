namespace VectorRAGvsPageIndexRAG.DTOs;

public record PageIndexQueryResponse(string Answer, List<PageIndexCitation> Citations);
