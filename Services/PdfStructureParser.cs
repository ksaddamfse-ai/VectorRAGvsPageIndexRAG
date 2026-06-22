using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using VectorRAGvsPageIndexRAG.Models;

namespace VectorRAGvsPageIndexRAG.Services;

public partial class PdfStructureParser
{
    private const double HeaderFontRatio = 1.2;
    private const double ParagraphGapRatio = 1.5;

    public DocumentTree Parse(Stream pdfStream, string fileName)
    {
        using var pdf = PdfDocument.Open(pdfStream);
        var pages = pdf.GetPages().ToList();

        // Phase 1: Collect all words with font metadata
        var allWords = new List<WordInfo>();
        foreach (var page in pages)
        {
            var words = page.GetWords();
            foreach (var word in words)
            {
                var fontSize = word.Letters.Count > 0 ? word.Letters[0].FontSize : 0;
                allWords.Add(new WordInfo
                {
                    Text = word.Text,
                    Left = word.BoundingBox.Left,
                    Top = word.BoundingBox.Top,
                    Bottom = word.BoundingBox.Bottom,
                    FontSize = fontSize,
                    PageNumber = page.Number
                });
            }
        }

        if (allWords.Count == 0)
            return new DocumentTree { Title = Path.GetFileNameWithoutExtension(fileName) };

        // Phase 2: Calculate font statistics
        var fontSizes = allWords.Where(w => w.FontSize > 0).Select(w => w.FontSize).ToList();
        var medianFontSize = fontSizes.Count > 0
            ? fontSizes.OrderBy(x => x).ElementAt(fontSizes.Count / 2)
            : 12.0;

        var headerThreshold = medianFontSize * HeaderFontRatio;

        // Phase 3: Detect section headers by font size
        var headerCandidates = allWords
            .Where(w => w.FontSize >= headerThreshold)
            .GroupBy(w => new { w.PageNumber, w.Top })
            .Select(g => new HeaderCandidate
            {
                PageNumber = g.Key.PageNumber,
                Top = g.Key.Top,
                Text = string.Join(" ", g.Select(w => w.Text)),
                FontSize = g.First().FontSize
            })
            .OrderBy(h => h.PageNumber)
            .ThenBy(h => h.Top)
            .ToList();

        // Phase 4: Build text blocks (paragraphs)
        var textBlocks = BuildTextBlocks(allWords, pages.Count);

        // Phase 5: Build tree structure
        var tree = BuildTree(headerCandidates, textBlocks, fileName, pages.Count);

        return tree;
    }

    private static List<TextBlock> BuildTextBlocks(List<WordInfo> words, int totalPages)
    {
        var blocks = new List<TextBlock>();
        var pageGroups = words.GroupBy(w => w.PageNumber).OrderBy(g => g.Key);

        foreach (var pageGroup in pageGroups)
        {
            var pageWords = pageGroup
                .OrderBy(w => w.Top)
                .ThenBy(w => w.Left)
                .ToList();

            if (pageWords.Count == 0) continue;

            var currentBlock = new TextBlock
            {
                PageNumber = pageGroup.Key,
                StartTop = pageWords[0].Top
            };

            double? lastBottom = null;
            var lineBuffer = new List<WordInfo>();

            foreach (var word in pageWords)
            {
                if (lastBottom.HasValue)
                {
                    var gap = word.Top - lastBottom.Value;
                    var lineHeight = word.Bottom - word.Top;
                    var isParagraphBreak = lineHeight > 0 && gap > lineHeight * ParagraphGapRatio;

                    if (isParagraphBreak && lineBuffer.Count > 0)
                    {
                        currentBlock.Words.AddRange(lineBuffer);
                        currentBlock.EndTop = lastBottom.Value;
                        if (currentBlock.Words.Count > 0)
                            blocks.Add(currentBlock);

                        currentBlock = new TextBlock
                        {
                            PageNumber = pageGroup.Key,
                            StartTop = word.Top
                        };
                        lineBuffer.Clear();
                    }
                }

                lineBuffer.Add(word);
                lastBottom = word.Bottom;
            }

            if (lineBuffer.Count > 0)
            {
                currentBlock.Words.AddRange(lineBuffer);
                currentBlock.EndTop = lastBottom ?? pageWords[^1].Bottom;
                blocks.Add(currentBlock);
            }
        }

        return blocks;
    }

    private static DocumentTree BuildTree(
        List<HeaderCandidate> headers,
        List<TextBlock> textBlocks,
        string fileName,
        int totalPages)
    {
        var root = new DocumentTree
        {
            Title = Path.GetFileNameWithoutExtension(fileName),
            NodeId = "root",
            TotalPages = totalPages
        };

        if (headers.Count == 0)
        {
            var allText = string.Join("\n\n", textBlocks.Select(b => b.ToText()));
            root.Children.Add(new TreeNode
            {
                Title = "Full Document",
                NodeId = "node_001",
                Text = allText,
                Summary = ""
            });
            return root;
        }

        var nodeStack = new Stack<(TreeNode Node, double FontSize)>();
        int nodeCounter = 0;

        // Preamble before first header
        var firstHeader = headers[0];
        var preambleBlocks = textBlocks
            .Where(b => b.PageNumber < firstHeader.PageNumber ||
                       (b.PageNumber == firstHeader.PageNumber && b.StartTop < firstHeader.Top))
            .ToList();

        if (preambleBlocks.Count > 0)
        {
            root.Children.Add(new TreeNode
            {
                Title = "Preamble",
                NodeId = $"node_{++nodeCounter:D3}",
                Text = string.Join("\n\n", preambleBlocks.Select(b => b.ToText())),
                Summary = ""
            });
        }

        foreach (var header in headers)
        {
            var newNode = new TreeNode
            {
                Title = header.Text,
                NodeId = $"node_{++nodeCounter:D3}",
                Summary = ""
            };

            // Find text blocks for this section (until next header)
            var nextHeader = headers.FirstOrDefault(h =>
                h.PageNumber > header.PageNumber ||
                (h.PageNumber == header.PageNumber && h.Top > header.Top));

            var sectionBlocks = textBlocks.Where(b =>
                (b.PageNumber > header.PageNumber ||
                 (b.PageNumber == header.PageNumber && b.StartTop >= header.Top)) &&
                (nextHeader == null ||
                 b.PageNumber < nextHeader.PageNumber ||
                 (b.PageNumber == nextHeader.PageNumber && b.StartTop < nextHeader.Top)))
                .ToList();

            if (sectionBlocks.Count > 0)
            {
                var headerText = header.Text;
                var filteredBlocks = sectionBlocks.Where(b =>
                {
                    var blockText = b.ToText();
                    return !blockText.StartsWith(headerText, StringComparison.OrdinalIgnoreCase);
                }).ToList();

                if (filteredBlocks.Count > 0)
                    newNode.Text = string.Join("\n\n", filteredBlocks.Select(b => b.ToText()));
            }

            // Nesting: pop stack while current header is smaller/equal
            while (nodeStack.Count > 0 && header.FontSize <= nodeStack.Peek().FontSize)
                nodeStack.Pop();

            if (nodeStack.Count > 0)
                nodeStack.Peek().Node.Children.Add(newNode);
            else
                root.Children.Add(newNode);

            nodeStack.Push((newNode, header.FontSize));
        }

        return root;
    }

    private class WordInfo
    {
        public string Text { get; set; } = "";
        public double Left { get; set; }
        public double Top { get; set; }
        public double Bottom { get; set; }
        public double FontSize { get; set; }
        public int PageNumber { get; set; }
    }

    private class HeaderCandidate
    {
        public int PageNumber { get; set; }
        public double Top { get; set; }
        public string Text { get; set; } = "";
        public double FontSize { get; set; }
    }

    private class TextBlock
    {
        public int PageNumber { get; set; }
        public double StartTop { get; set; }
        public double EndTop { get; set; }
        public List<WordInfo> Words { get; set; } = [];

        public string ToText()
        {
            var lines = Words
                .GroupBy(w => Math.Round(w.Top, 0))
                .OrderBy(g => g.Key)
                .Select(g => string.Join(" ", g.OrderBy(w => w.Left).Select(w => w.Text)))
                .ToList();

            return string.Join("\n", lines);
        }
    }
}
