namespace VectorRAGvsPageIndexRAG.DTOs;

public record CompareQueryRequest(
    string Question,
    string Provider,
    string Model,
    int TopK = 2,
    string GroupName = "PDFs",
    string CollectionName = "PDFs");
