using Microsoft.Extensions.VectorData;

namespace VectorRAGvsPageIndexRAG.Settings;

public class RagChunk
{
    [VectorStoreRecordKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [VectorStoreRecordData]
    public string Text { get; set; } = "";

    [VectorStoreRecordData]
    public string Source { get; set; } = "";

    [VectorStoreRecordData]
    public int ChunkIndex { get; set; }

    [VectorStoreRecordData]
    public int TotalChunks { get; set; }

    [VectorStoreRecordVector]
    public ReadOnlyMemory<float> Vector { get; set; }
}
