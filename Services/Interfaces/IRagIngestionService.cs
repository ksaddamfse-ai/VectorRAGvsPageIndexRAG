using VectorRAGvsPageIndexRAG.Models;

namespace VectorRAGvsPageIndexRAG.Services.Interfaces;

public interface IRagIngestionService
{
    Task<RagIngestionResult> IngestAsync(string text, string fileName);
}

public record RagIngestionResult(string FileName, int ChunkCount, List<RagChunk> Chunks);
