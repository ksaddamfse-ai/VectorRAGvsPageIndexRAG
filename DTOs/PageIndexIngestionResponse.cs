namespace RAGBench.DTOs;

public record PageIndexIngestionResponse(
    string DocId,
    string FileName,
    string Status,
    int PageCount,
    string TreeJson);
