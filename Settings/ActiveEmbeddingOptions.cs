namespace VectorRAGvsPageIndexRAG.Settings;

public class ActiveEmbeddingOptions
{
    public string ProviderKey { get; set; } = "";
    public string Model { get; set; } = "";
    public int VectorSize { get; set; } = 1024;
}
