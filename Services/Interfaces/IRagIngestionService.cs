using RAGBench.Models;

namespace RAGBench.Services.Interfaces;

public interface IRagIngestionService
{
    Task<RagIngestionResult> IngestAsync(string text, string fileName, string collectionName = "PDFs");
}

public record RagIngestionResult(string FileName, int ChunkCount, List<RagChunk> Chunks, string CollectionName);
