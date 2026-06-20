namespace VectorRAGvsPageIndexRAG.DTOs;

public record RagIngestionResponse(string FileName, int ChunkCount, List<RagChunkResponse> Chunks);
