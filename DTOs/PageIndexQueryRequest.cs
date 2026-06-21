namespace VectorRAGvsPageIndexRAG.DTOs;

public record PageIndexQueryRequest(
    string DocId,
    string Question,
    string Provider,
    string Model);
