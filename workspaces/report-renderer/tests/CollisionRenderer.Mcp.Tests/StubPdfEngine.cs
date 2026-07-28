using CollisionRenderer.Core;
using CollisionRenderer.Core.Rendering;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace CollisionRenderer.Mcp.Tests;

/// <summary>
/// A browser-free engine that returns a real, openable 1-page PDF so the evidence-pack
/// path (which appends captured PDFs via PdfSharp) works without Chromium. Page counts
/// are read back with PdfSharp so an appended pack reports table + capture pages honestly.
/// </summary>
internal sealed class StubPdfEngine : IPdfEngine
{
    private readonly byte[] _pdf = MakeOnePagePdf();

    public string EngineVersion => "stub/1.0";

    public Task<byte[]> RenderHtmlToPdfAsync(string html, PdfPageSettings settings, CancellationToken ct = default) =>
        Task.FromResult(_pdf);

    public int CountPages(byte[] pdf)
    {
        using var doc = PdfReader.Open(new MemoryStream(pdf), PdfDocumentOpenMode.Import);
        return doc.PageCount;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    internal static byte[] MakeOnePagePdf()
    {
        using var doc = new PdfDocument();
        doc.AddPage();
        using var ms = new MemoryStream();
        doc.Save(ms, closeStream: false);
        return ms.ToArray();
    }
}
