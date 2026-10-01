using Pegasus.Core.Documents;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Pegasus.Infrastructure.Intake;

/// <summary>
/// Page text of a PDF for an MCP client, read with the same PdfPig extractor
/// the intake reader uses, within a character budget. Claude's hosted
/// connector cannot take a PDF as binary content, so its text is what reaches
/// the model; a page with too little readable text is named as needing OCR
/// rather than passed off as blank.
/// </summary>
internal sealed class PdfPigPageTextExtractor : IExtractPdfPageText
{
    /// <summary>The intake reader's readable-text floor for a text page.</summary>
    private const int MinimumReadableCharacters = 80;

    public Task<PdfTextExtraction?> ExtractAsync(
        ReadOnlyMemory<byte> content,
        int maximumCharacters,
        CancellationToken cancellationToken)
    {
        if (content.IsEmpty || maximumCharacters <= 0)
        {
            return Task.FromResult<PdfTextExtraction?>(null);
        }

        try
        {
            using var document = PdfDocument.Open(content.ToArray());
            var pages = new List<PdfPageText>();
            var remaining = maximumCharacters;
            for (var number = 1; number <= document.NumberOfPages && remaining > 0; number++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var page = document.GetPage(number);
                var text = ContentOrderTextExtractor.GetText(page) ?? string.Empty;
                var readable = text.Count(character => !char.IsWhiteSpace(character));
                var truncated = text.Length > remaining;
                if (truncated)
                {
                    text = text[..remaining];
                }
                remaining -= text.Length;
                pages.Add(new PdfPageText(number, text, readable < MinimumReadableCharacters, truncated));
            }

            return Task.FromResult<PdfTextExtraction?>(
                new PdfTextExtraction(document.NumberOfPages, pages));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Task.FromResult<PdfTextExtraction?>(null);
        }
    }
}
