namespace VectorRAGvsPageIndexRAG.DTOs;

public record CompareQueryRequest(
    string DocId,
    string Question,
    string Provider,
    string Model,
    int TopK,
    string CollectionName = "");
