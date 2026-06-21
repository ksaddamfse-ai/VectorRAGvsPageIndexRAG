using VectorRAGvsPageIndexRAG.Services;

namespace VectorRAGvsPageIndexRAG.Tests;

public class DocumentProcessorTests
{
    [Fact]
    public void ExtractText_NullStream_Throws()
    {
        var processor = new DocumentProcessor();
        Assert.Throws<ArgumentNullException>(() => processor.ExtractText(null!));
    }

    [Fact]
    public void ExtractText_EmptyStream_Throws()
    {
        var processor = new DocumentProcessor();
        using var stream = new MemoryStream();
        Assert.ThrowsAny<Exception>(() => processor.ExtractText(stream));
    }
}
