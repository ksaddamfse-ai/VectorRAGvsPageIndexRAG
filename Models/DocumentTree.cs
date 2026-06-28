using System.Text.Json.Serialization;

namespace RAGBench.Models;

public class DocumentTree
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("node_id")]
    public string NodeId { get; set; } = "";

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = "";

    [JsonPropertyName("children")]
    public List<TreeNode> Children { get; set; } = [];

    [JsonIgnore]
    public string DocId { get; set; } = "";

    [JsonIgnore]
    public string FileName { get; set; } = "";

    [JsonIgnore]
    public string GroupName { get; set; } = "";

    [JsonPropertyName("total_pages")]
    public int TotalPages { get; set; }
}
