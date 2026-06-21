namespace VectorRAGvsPageIndexRAG.DTOs;

public record CompareQueryRequest(
    string DocId,
    string Question,
    string Provider = "GoogleAI",
    string Model = "gemini-3.5-flash",
    int TopK = 5);
