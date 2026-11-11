using Pegasus.Core.Documents;

namespace Pegasus.Core.Tests.Documents;

/// <summary>
/// The one owner of which Case documents the viewer shows, and as what: an
/// image, a PDF or an admitted video is shown as itself, a Word document as a
/// PDF rendering of its stored version, and everything else is a download.
/// Only the first three are ever served inline from this origin.
/// </summary>
public sealed class CaseDocumentPreviewKindTests
{
    private const string DocxMediaType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private const string DocMediaType = "application/msword";

    [Fact]
    public void TheRenditionNamesTheCurrentRenderingAndIsAPdf()
    {
        Assert.Equal("p1", CaseDocumentPreviews.RenditionIdentity);
        Assert.Equal("pdf-p1", CaseDocumentPreviews.RenditionVariant);
        Assert.Equal("application/pdf", CaseDocumentPreviews.RenditionMediaType);
    }

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("application/pdf; charset=binary")]
    [InlineData("APPLICATION/PDF")]
    public void APdfIsShownAsAPdf(string mediaType)
    {
        Assert.Equal(CaseDocumentPreviewKind.Pdf, CaseDocumentPreviews.KindOf(mediaType, "estimate.pdf"));
        Assert.Equal(CaseDocumentPreviewKind.Pdf, CaseDocumentPreviews.KindOf(mediaType, null));
    }

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("IMAGE/JPEG")]
    public void AnImageIsShownAsAnImage(string mediaType)
    {
        Assert.Equal(CaseDocumentPreviewKind.Image, CaseDocumentPreviews.KindOf(mediaType, "photograph.jpg"));
    }

    [Fact]
    public void AnSvgImageIsNeverShown()
    {
        Assert.Equal(CaseDocumentPreviewKind.None, CaseDocumentPreviews.KindOf("image/svg+xml", "drawing.svg"));
    }

    [Theory]
    [InlineData("video/mp4")]
    [InlineData("video/quicktime")]
    public void AnAdmittedVideoIsShownAsAVideo(string mediaType)
    {
        Assert.Equal(CaseDocumentPreviewKind.Video, CaseDocumentPreviews.KindOf(mediaType, "walkround.mp4"));
    }

    [Theory]
    [InlineData(DocxMediaType, "letter.docx")]
    [InlineData(DocxMediaType, null)]
    [InlineData(DocxMediaType, "")]
    [InlineData(DocxMediaType, "letter.pdf")]
    [InlineData(DocMediaType, "letter.doc")]
    [InlineData(DocMediaType, "LETTER.DOC")]
    [InlineData(DocMediaType, null)]
    [InlineData("APPLICATION/MSWORD", "letter.doc")]
    public void AWordMediaTypeIsShownAsAPdfRenderingWhateverItsFileName(string mediaType, string? fileName)
    {
        Assert.Equal(CaseDocumentPreviewKind.WordAsPdf, CaseDocumentPreviews.KindOf(mediaType, fileName));
    }

    [Theory]
    [InlineData("letter.docx")]
    [InlineData("LETTER.DOC")]
    [InlineData("report.Docx")]
    public void AnOctetStreamNamedAsAWordDocumentIsShownAsAPdfRendering(string fileName)
    {
        Assert.Equal(
            CaseDocumentPreviewKind.WordAsPdf,
            CaseDocumentPreviews.KindOf("application/octet-stream", fileName));
    }

    [Theory]
    [InlineData("notes.txt")]
    [InlineData("estimate.pdf")]
    [InlineData("letter")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void AnOctetStreamNotNamedAsAWordDocumentIsADownload(string? fileName)
    {
        Assert.Equal(
            CaseDocumentPreviewKind.None,
            CaseDocumentPreviews.KindOf("application/octet-stream", fileName));
    }

    [Theory]
    [InlineData("text/html", "page.html")]
    [InlineData("text/html", "letter.docx")]
    [InlineData("text/plain", "notes.txt")]
    [InlineData("application/zip", "bundle.zip")]
    [InlineData(null, "letter.docx")]
    [InlineData("", "letter.docx")]
    [InlineData("not a type", "letter.docx")]
    public void AnyOtherOrMalformedMediaTypeIsADownload(string? mediaType, string? fileName)
    {
        Assert.Equal(CaseDocumentPreviewKind.None, CaseDocumentPreviews.KindOf(mediaType, fileName));
    }

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("application/pdf")]
    [InlineData("application/pdf; charset=binary")]
    [InlineData("video/mp4")]
    [InlineData("video/quicktime")]
    public void AnImageAPdfOrAnAdmittedVideoIsInlineSafe(string mediaType)
    {
        Assert.True(CaseDocumentPreviews.IsInlineSafe(mediaType));
    }

    [Theory]
    [InlineData(DocxMediaType)]
    [InlineData(DocMediaType)]
    [InlineData("application/octet-stream")]
    [InlineData("image/svg+xml")]
    [InlineData("text/html")]
    [InlineData("text/plain")]
    [InlineData("not a type")]
    [InlineData("")]
    [InlineData(null)]
    public void AWordDocumentAndEverythingElseIsNeverServedInline(string? mediaType)
    {
        Assert.False(CaseDocumentPreviews.IsInlineSafe(mediaType));
    }
}
