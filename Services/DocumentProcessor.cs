using System.Text;
using UglyToad.PdfPig;
using RAGBench.Services.Interfaces;

namespace RAGBench.Services;

public class DocumentProcessor : IDocumentProcessor
{
    public string ExtractText(Stream pdfStream)
    {
        using var pdf = PdfDocument.Open(pdfStream);
        var sb = new StringBuilder();
        foreach (var page in pdf.GetPages())
            sb.AppendLine(page.Text);
        return sb.ToString();
    }
}
