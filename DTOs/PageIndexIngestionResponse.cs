namespace VectorRAGvsPageIndexRAG.DTOs;

public record PageIndexIngestionResponse(
    string DocId,
    string FileName,
    string Status,
    int PageCount,
    string TreeJson);
