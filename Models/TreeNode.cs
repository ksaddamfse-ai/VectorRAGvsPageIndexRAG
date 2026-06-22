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

    [JsonPropertyName("start_page")]
    public int? StartPage { get; set; }

    [JsonPropertyName("end_page")]
    public int? EndPage { get; set; }

    [JsonPropertyName("children")]
    public List<TreeNode> Children { get; set; } = [];
}
