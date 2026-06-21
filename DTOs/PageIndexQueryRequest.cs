namespace VectorRAGvsPageIndexRAG.DTOs;

public record PageIndexQueryRequest(
    string DocId,
    string Question,
    string Provider = "GoogleAI",
    string Model = "gemini-3.5-flash");
