namespace RAGBench.Services.Interfaces;

public interface IDocumentProcessor
{
    string ExtractText(Stream pdfStream);
}
