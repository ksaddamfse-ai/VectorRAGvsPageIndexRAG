namespace VectorRAGvsPageIndexRAG.DTOs;

public record PageIndexQueryRequest(
    string Question,
    string Provider,
    string Model,
    string GroupName = "PDFs");
