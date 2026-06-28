using System.Security.Cryptography;
using System.Text;

namespace RAGBench.Models;

public class RagChunk
{
    public string Id { get; set; } = "";
    public string Text { get; set; } = "";
    public string Source { get; set; } = "";
    public int ChunkIndex { get; set; }
    public int TotalChunks { get; set; }
    public float[] Vector { get; set; } = [];

    public static string ComputeId(string text, string source)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{source}\0{text}"));
        return new Guid(hash.AsSpan(0, 16)).ToString();
    }
}
