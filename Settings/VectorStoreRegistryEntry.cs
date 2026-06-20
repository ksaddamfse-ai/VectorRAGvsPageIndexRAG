namespace VectorRAGvsPageIndexRAG.Settings;

public class VectorStoreRegistryEntry
{
    public string Type { get; init; } = "";
    public string DefaultCollectionName { get; init; } = "documents";
    public int ChunkSize { get; init; } = 512;
    public int ChunkOverlap { get; init; } = 51;
    public string? Host { get; init; }
    public int Port { get; init; } = 6333;
    public string? Endpoint { get; init; }
    public string? ApiKey { get; init; }
}
