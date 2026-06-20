namespace VectorRAGvsPageIndexRAG.DTOs;

public record CompareQueryRequest(
    string DocId,
    string Question,
    string Provider = "NvidiaNim",
    string Model = "meta/llama-3.3-70b-instruct",
    int TopK = 5);
