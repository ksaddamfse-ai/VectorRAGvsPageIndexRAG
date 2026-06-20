using System.Text.Json.Serialization;

namespace VectorRAGvsPageIndexRAG.Models;

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
    public int TotalPages { get; set; }
}
