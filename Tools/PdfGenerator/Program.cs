using RAGBench.Tools.PdfGenerator;

var outputDir = Path.Combine(Directory.GetCurrentDirectory(), "test-pdfs");
Directory.CreateDirectory(outputDir);

Console.WriteLine("Generating test PDFs...");

var technicalReport = new TechnicalReport();
technicalReport.Generate(Path.Combine(outputDir, "technical-report.pdf"));
Console.WriteLine("  ✓ technical-report.pdf");

var resume = new Resume();
resume.Generate(Path.Combine(outputDir, "resume.pdf"));
Console.WriteLine("  ✓ resume.pdf");

var legalContract = new LegalContract();
legalContract.Generate(Path.Combine(outputDir, "legal-contract.pdf"));
Console.WriteLine("  ✓ legal-contract.pdf");

Console.WriteLine($"\nDone! PDFs saved to: {outputDir}");
