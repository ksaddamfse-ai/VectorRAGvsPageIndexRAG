namespace RAGBench.DTOs;

public record RagQueryResponse(string Answer, List<RagChunkResult> Chunks);
