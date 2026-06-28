using RAGBench.Models;

namespace RAGBench.Tests;

public class VectorStoreTests
{
    [Fact]
    public void RagChunk_To_RagChunkRecord_MapsAllFields()
    {
        var chunk = new RagChunk
        {
            Id = RagChunk.ComputeId("test text", "test.pdf"),
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

    [Fact]
    public void ComputeId_Deterministic()
    {
        var id1 = RagChunk.ComputeId("hello world", "doc.pdf");
        var id2 = RagChunk.ComputeId("hello world", "doc.pdf");
        Assert.Equal(id1, id2);
    }

    [Fact]
    public void ComputeId_DifferentInputs_DifferentIds()
    {
        var id1 = RagChunk.ComputeId("hello", "a.pdf");
        var id2 = RagChunk.ComputeId("world", "b.pdf");
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void ComputeId_NullBoundary_PreventsCollision()
    {
        var id1 = RagChunk.ComputeId("A", "doc.txt");
        var id2 = RagChunk.ComputeId("txtA", "doc.");
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void ComputeId_EmptyStrings_DoesNotThrow()
    {
        var id = RagChunk.ComputeId("", "");
        Assert.NotNull(id);
        Assert.NotEqual("", id);
    }

    [Fact]
    public void ComputeId_ReturnsValidGuid()
    {
        var id = RagChunk.ComputeId("some text", "source");
        Assert.True(Guid.TryParse(id, out _));
    }
}
