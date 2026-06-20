namespace VectorRAGvsPageIndexRAG.Models;

public class RagChunk
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Text { get; set; } = "";
    public string Source { get; set; } = "";
    public int ChunkIndex { get; set; }
    public int TotalChunks { get; set; }
    public float[] Vector { get; set; } = [];
}
