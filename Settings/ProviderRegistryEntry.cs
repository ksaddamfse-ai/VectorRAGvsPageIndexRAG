namespace RAGBench.Settings;

public class ProviderRegistryEntry
{
    public bool Enabled { get; init; } = true;
    public string Type { get; init; } = "OpenAI";
    public string? ApiKey { get; init; }
    public string BaseUrl { get; init; } = "";
    public List<string> Models { get; init; } = [];
}
