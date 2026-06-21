namespace VectorRAGvsPageIndexRAG.Settings;

public class VectorStoreRegistryEntry
{
    public string Type { get; set; } = "";
    public string DefaultCollectionName { get; set; } = "documents";
    public int ChunkSize { get; set; } = 512;
    public int ChunkOverlap { get; set; } = 51;
    public int EmbeddingBatchSize { get; set; } = 500;
    public string? Host { get; set; }
    public int Port { get; set; } = 6334;
    public string? Endpoint { get; set; }
    public string? ApiKey { get; set; }
}
