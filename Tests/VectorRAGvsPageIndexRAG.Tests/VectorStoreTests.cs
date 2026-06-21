using VectorRAGvsPageIndexRAG.Models;

namespace VectorRAGvsPageIndexRAG.Tests;

public class VectorStoreTests
{
    [Fact]
    public void RagChunk_To_RagChunkRecord_MapsAllFields()
    {
        var chunk = new RagChunk
        {
            Id = Guid.NewGuid().ToString(),
            Text = "test text",
            Source = "test.pdf",
            ChunkIndex = 0,
            TotalChunks = 3,
            Vector = [0.1f, 0.2f, 0.3f]
        };

        var record = new RagChunkRecord
        {
            Key = Guid.Parse(chunk.Id),
            Text = chunk.Text,
            Source = chunk.Source,
            ChunkIndex = chunk.ChunkIndex,
            TotalChunks = chunk.TotalChunks,
            Vector = chunk.Vector
        };

        Assert.Equal(chunk.Id, record.Key.ToString());
        Assert.Equal(chunk.Text, record.Text);
        Assert.Equal(chunk.Source, record.Source);
        Assert.Equal(chunk.ChunkIndex, record.ChunkIndex);
        Assert.Equal(chunk.TotalChunks, record.TotalChunks);
        Assert.Equal(chunk.Vector, record.Vector);
    }
}
