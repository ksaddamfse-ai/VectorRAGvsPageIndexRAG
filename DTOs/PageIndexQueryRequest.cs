namespace VectorRAGvsPageIndexRAG.DTOs;

public record PageIndexQueryRequest(
    string DocId,
    string Question,
    string Provider = "NvidiaNim",
    string Model = "meta/llama-3.3-70b-instruct");
