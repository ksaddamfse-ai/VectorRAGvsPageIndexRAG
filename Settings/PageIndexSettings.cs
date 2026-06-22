namespace VectorRAGvsPageIndexRAG.Settings;

public class PageIndexSettings
{
    public string DbPath { get; set; } = "pageindex.db";
    public int MaxSkeletonDepth { get; set; } = 3;
    public int MaxTokensPerQuery { get; set; } = 20000;
}
