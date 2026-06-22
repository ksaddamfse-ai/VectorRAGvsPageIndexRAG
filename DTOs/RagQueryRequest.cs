namespace VectorRAGvsPageIndexRAG.DTOs;

public record RagQueryRequest(
    string Question,
    string Provider,
    string Model,
    int TopK = 2,
    string CollectionName = "PDFs");
