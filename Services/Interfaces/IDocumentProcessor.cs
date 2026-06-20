namespace VectorRAGvsPageIndexRAG.Services.Interfaces;

public interface IDocumentProcessor
{
    string ExtractText(Stream pdfStream);
}
