using System.Text.Json.Serialization;

namespace VectorRAGvsPageIndexRAG.Models;

public class TreeNode
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("node_id")]
    public string NodeId { get; set; } = "";

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = "";

    [JsonPropertyName("text")]
    public string Text { get; set; } = "";

    [JsonPropertyName("children")]
    public List<TreeNode> Children { get; set; } = [];
}
