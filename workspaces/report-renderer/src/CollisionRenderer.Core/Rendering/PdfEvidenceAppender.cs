using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace CollisionRenderer.Core.Rendering;

/// <summary>
/// Appends user-supplied advert evidence PDFs after the Chromium-rendered pack.
/// This deliberately does not participate in layout; Core still renders the
/// branded document through HTML/CSS and Chromium, then attaches captured PDFs.
/// </summary>
internal static class PdfEvidenceAppender
{
    public static byte[] Append(byte[] mainPdf, IEnumerable<string?> evidencePdfs)
    {
        var sources = evidencePdfs
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!)
            .ToList();

        if (sources.Count == 0)
        {
            return mainPdf;
        }

        using var output = new PdfDocument();
        AppendDocument(output, () => new MemoryStream(mainPdf, writable: false), "rendered evidence pack");

        foreach (var source in sources)
        {
            AppendDocument(output, () => OpenSource(source), SourceLabel(source));
        }

        using var ms = new MemoryStream();
        output.Save(ms, closeStream: false);
        return ms.ToArray();
    }

    private static void AppendDocument(PdfDocument output, Func<Stream> streamFactory, string label)
    {
        try
        {
            using var stream = streamFactory();
            using var input = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
            for (var i = 0; i < input.PageCount; i++)
            {
                output.AddPage(input.Pages[i]);
            }
        }
        catch (Exception ex) when (ex is PdfReaderException or InvalidOperationException or IOException or FormatException)
        {
            throw new InvalidOperationException($"Could not append PDF evidence '{label}': {ex.Message}", ex);
        }
    }

    private static Stream OpenSource(string source)
    {
        if (source.StartsWith("data:application/pdf;base64,", StringComparison.OrdinalIgnoreCase))
        {
            var comma = source.IndexOf(',');
            var bytes = Convert.FromBase64String(source[(comma + 1)..]);
            return new MemoryStream(bytes, writable: false);
        }

        return File.OpenRead(source);
    }

    private static string SourceLabel(string source)
    {
        if (source.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return "embedded PDF";
        }

        try
        {
            return Path.GetFileName(source);
        }
        catch
        {
            return source;
        }
    }
}
