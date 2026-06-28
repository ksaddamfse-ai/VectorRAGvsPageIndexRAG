namespace RAGBench.DTOs;

public record RagIngestionResponse(string FileName, int ChunkCount, string CollectionName, List<RagChunkResponse> Chunks);
