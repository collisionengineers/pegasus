using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pegasus.Core.Intake;

/// <summary>
/// Turns a completed OCR result into readable source text in the reader's own
/// shape, so every reader of an <see cref="IntakeSourceReadResult"/> — the
/// instruction reader and the third-party report reader alike — reads an OCR
/// page exactly as it reads an embedded-text page: by the document's label and
/// the page number. The OCR provenance (response hash, lines, tables) travels
/// in the locator, never in the label, so a page number is a page number
/// whoever read it.
/// </summary>
public static class IntakeOcrText
{
    /// <summary>The locator role that marks a fragment as OCR output rather than embedded text.</summary>
    public const string DocumentRole = "ocr";

    private static readonly JsonSerializerOptions ProvenanceJsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>The reader key an OCR reading is recorded under: the provider and its model.</summary>
    public static string ReaderKey(IntakeOcrResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return $"{result.Provider}/{result.ModelId}";
    }

    /// <summary>
    /// One document's OCR output alone: the read result a reader sees when the
    /// document is read from its OCR text and nothing else.
    /// </summary>
    /// <param name="documentLabel">
    /// The reader's label for the document (<c>uploaded report.pdf</c>, or
    /// <c>uploaded mail.eml, attachment 2: report.pdf</c>), so each page's label
    /// is <c>{documentLabel}, page N</c>.
    /// </param>
    public static IntakeSourceReadResult ReadResult(
        string documentLabel,
        string sourceSha256,
        IntakeOcrResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new(
            IntakeSourceReadStatus.Readable,
            Fragments(documentLabel, sourceSha256, result),
            [],
            [],
            false,
            ReaderKey: ReaderKey(result),
            ReaderVersion: result.ApiVersion);
    }

    /// <summary>
    /// One fragment per OCR page that holds text, in page order. A blank page is
    /// a page with nothing on it, not a failure, so it yields no fragment.
    /// </summary>
    public static IReadOnlyList<IntakeContentFragment> Fragments(
        string documentLabel,
        string sourceSha256,
        IntakeOcrResult result)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceSha256);
        ArgumentNullException.ThrowIfNull(result);
        var responseSha = result.ResponseSha256
            ?? throw new ArgumentException("A completed OCR result carries its response hash.", nameof(result));
        return result.PageResults
            .Where(page => !string.IsNullOrWhiteSpace(page.Text))
            .OrderBy(page => page.Number)
            .Select(page => new IntakeContentFragment(
                IntakeEvidenceSource.DocumentContent,
                $"{documentLabel}, page {page.Number}",
                page.Text,
                new(
                    IntakeLocatorKind.Page,
                    Page: page.Number,
                    Region: JsonSerializer.Serialize(
                        new OcrPageProvenance(page.Lines, page.Tables, responseSha),
                        ProvenanceJsonOptions),
                    Sha256: sourceSha256,
                    DocumentRole: DocumentRole)))
            .ToArray();
    }

    /// <summary>
    /// The ordinary reading of a whole source with one document's qualified
    /// pages replaced by their OCR text. Everything else — the e-mail body, the
    /// readable attachments, the document's own readable pages — stays as the
    /// reader produced it. The document's qualified pages leave the OCR
    /// candidates; whatever still needs OCR keeps the result marked.
    /// </summary>
    public static IntakeSourceReadResult Merge(
        IntakeSourceReadResult ordinary,
        string documentLabel,
        string sourceSha256,
        IReadOnlyCollection<int> qualifiedPages,
        IntakeOcrResult result)
    {
        ArgumentNullException.ThrowIfNull(ordinary);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentLabel);
        ArgumentNullException.ThrowIfNull(qualifiedPages);
        var qualified = qualifiedPages.ToHashSet();
        var pagePrefix = documentLabel + ",";
        var readable = ordinary.Content
            .Where(fragment =>
                !fragment.SourceLabel.StartsWith(pagePrefix, StringComparison.Ordinal)
                || fragment.Locator?.Page is not { } page
                || !qualified.Contains(page))
            .ToArray();
        var remaining = ordinary.ScannedPdfPages
            .Where(candidate =>
                !string.Equals(candidate.SourceLabel, documentLabel, StringComparison.Ordinal)
                || !qualified.Contains(candidate.PageNumber))
            .ToArray();
        return ordinary with
        {
            Content = [.. readable, .. Fragments(documentLabel, sourceSha256, result)],
            RequiresOcr = remaining.Length > 0,
            OcrCandidates = remaining
        };
    }

    /// <summary>Whether a fragment's locator says its text came from OCR.</summary>
    public static bool IsOcrLocator(IntakeSourceLocator? locator) =>
        locator is { Page: not null }
        && string.Equals(locator.DocumentRole, DocumentRole, StringComparison.Ordinal);

    /// <summary>
    /// What the locator's region carries for an OCR page: the provider's lines
    /// and tables, and the hash of the response they came from.
    /// </summary>
    public sealed record OcrPageProvenance(
        IReadOnlyList<IntakeOcrLine> Lines,
        IReadOnlyList<IntakeOcrTable> Tables,
        string ResponseSha256);
}
