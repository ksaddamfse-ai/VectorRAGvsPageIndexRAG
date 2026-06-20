using System.Text;
using UglyToad.PdfPig;

namespace VectorRAGvsPageIndexRAG.Services;

public static class DocumentProcessor
{
    public static string ExtractText(Stream pdfStream)
    {
        using var pdf = PdfDocument.Open(pdfStream);
        var sb = new StringBuilder();
        foreach (var page in pdf.GetPages())
            sb.AppendLine(page.Text);
        return sb.ToString();
    }
}
