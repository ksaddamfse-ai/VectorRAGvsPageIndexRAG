using Microsoft.Extensions.VectorData;

namespace VectorRAGvsPageIndexRAG.Models;

public class RagChunkRecord
{
    [VectorStoreKey]
    public Guid Key { get; set; }

    [VectorStoreData]
    public string Text { get; set; } = "";

    [VectorStoreData]
    public string Source { get; set; } = "";

    [VectorStoreData]
    public int ChunkIndex { get; set; }

    [VectorStoreData]
    public int TotalChunks { get; set; }

    [VectorStoreVector(1)]
    public float[]? Vector { get; set; }
}
