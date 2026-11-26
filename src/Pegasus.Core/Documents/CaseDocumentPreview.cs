using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Documents;

/// <summary>
/// Which occurrence of a Case's documents a preview is asked for.
/// </summary>
public sealed record CaseDocumentPreviewQuery(
    Guid CaseId,
    Guid OccurrenceId,
    Guid VersionId,
    ActionActor Actor);

/// <summary>
/// The identity a preview needs to serve one occurrence's bytes: the logical
/// content address, the custody hash and length the read verifies against, and
/// the custody state the version is in.
/// </summary>
/// <remarks>
/// A gallery tile is a preview, not a download. The audited
/// <see cref="IDownloadCaseDocument"/> path writes one
/// <c>ActionHistory</c> row per call, so a Case of sixty photographs wrote
/// sixty custody-download records for one page view, each behind its own
/// buffered Box read. The tiles now resolve the occurrence here and read the
/// bytes through <see cref="IReadLogicalDocumentVersion"/>, which verifies the
/// same SHA-256 on every read and records nothing. A real download keeps the
/// audited path exactly as it was.
///
/// <see cref="CustodyStatus"/> is carried rather than filtered on, because a
/// Case opened while custody is still in flight has to be told apart from one
/// asking for a file that does not exist: the first is "not yet", answered
/// with a retry, and the second is 404.
/// </remarks>
public sealed record CaseDocumentPreview(
    Guid CaseId,
    Guid OccurrenceId,
    Guid DocumentId,
    Guid VersionId,
    string FileName,
    string MediaType,
    long ContentLength,
    string Sha256,
    DocumentCustodyStatus CustodyStatus);

/// <summary>
/// Resolves one Case occurrence to the logical content identity a preview
/// reads, applying the same Case-membership rule the audited download applies.
/// </summary>
public interface IReadCaseDocumentPreview
{
    Task<CaseDocumentPreview?> ExecuteAsync(
        CaseDocumentPreviewQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// One derived thumbnail read: the resolved preview identity, so the
/// implementation reads the verified full bytes through the ordinary logical
/// read before it derives anything.
/// </summary>
public sealed record CaseDocumentThumbnailRequest(
    ActionActor Actor,
    Guid CaseId,
    Guid DocumentId,
    Guid VersionId,
    string Sha256,
    long ContentLength,
    string MediaType,
    CaseAssetRotation Rotation = CaseAssetRotation.None,
    CaseAssetCrop? Crop = null)
{
    /// <summary>
    /// The prepared region the tile shows (v26 § Crop and tag): the occurrence's
    /// whole-turn rotation and the crop as fractions of the rotated source, or
    /// the whole image when nothing is prepared. Download keeps the original.
    /// </summary>
    public bool IsPrepared => Rotation != CaseAssetRotation.None || Crop is { IsFull: false };
}

public sealed record CaseDocumentThumbnail(
    Stream Content,
    string MediaType,
    long ContentLength,
    string SourceSha256) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}

/// <summary>
/// A gallery-sized rendering of one image version, derived from the verified
/// full bytes and cached as its own entry kind.
/// </summary>
/// <remarks>
/// A tile of a 6 MB photograph asked for the whole photograph.
/// Sixty of them is the page load. The derived rendering is bounded by
/// <see cref="CaseDocumentThumbnails.LongestEdge"/>, so a tile costs kilobytes
/// and the viewer keeps the full image.
///
/// An implementation answers <c>null</c> when it cannot produce one — a media
/// type it does not decode, or bytes it cannot read as an image — and the
/// caller serves the full content instead. A thumbnail is never a reason for a
/// tile to fail.
/// </remarks>
public interface IReadCaseDocumentThumbnail
{
    Task<CaseDocumentThumbnail?> OpenAsync(
        CaseDocumentThumbnailRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// The rendering half of a thumbnail, for bytes that are not a Case document
/// version (a pre-Case intake image): the same derivation the Case gallery
/// uses — the whole-turn rotation, then the crop as fractions of the rotated
/// source, bounded by <see cref="CaseDocumentThumbnails.LongestEdge"/> and
/// encoded as <see cref="CaseDocumentThumbnails.MediaType"/>.
/// </summary>
/// <remarks>
/// Answers <c>null</c> when the bytes cannot be rendered; the caller then
/// serves the original, as <see cref="IReadCaseDocumentThumbnail"/> does.
/// </remarks>
public interface IRenderImageThumbnail
{
    Task<byte[]?> RenderAsync(
        ReadOnlyMemory<byte> content,
        CaseAssetRotation rotation,
        CaseAssetCrop crop,
        CancellationToken cancellationToken);
}

/// <summary>
/// The one owner of what a thumbnail is: how large, what it is encoded as, and
/// which media types have one at all.
/// </summary>
public static class CaseDocumentThumbnails
{
    /// <summary>
    /// The one current derived-thumbnail representation. This changes whenever
    /// rendering can change its bytes, so the gallery address, cache variant
    /// and validator all advance together.
    /// </summary>
    public const string RendererIdentity = "r2";

    /// <summary>The longest edge of a derived thumbnail, in pixels.</summary>
    public const int LongestEdge = 480;

    /// <summary>What a derived thumbnail is encoded as.</summary>
    public const string MediaType = "image/jpeg";

    /// <summary>The request value that asks for the derived rendering.</summary>
    public const string ThumbSizeToken = "thumb";

    /// <summary>
    /// The variant one rendering is cached and validated under: the plain
    /// gallery rendering, or the prepared region of the rotated source. A
    /// changed crop is a different variant, so a tile can never be served an
    /// earlier crop from the cache.
    /// </summary>
    public static string VariantToken(CaseAssetRotation rotation, CaseAssetCrop? crop)
    {
        var region = crop ?? CaseAssetCrop.Full;
        if (rotation == CaseAssetRotation.None && region.IsFull)
        {
            return $"thumb-{LongestEdge}-{RendererIdentity}";
        }

        return string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"thumb-{LongestEdge}-{RendererIdentity}-r{(int)rotation}-c{region.Left:0.#######}-{region.Top:0.#######}-{region.Width:0.#######}-{region.Height:0.#######}");
    }

    /// <summary>
    /// Whether this media type has a derived thumbnail. SVG is excluded for the
    /// same reason the inline rule excludes it: it is never rendered from this
    /// origin, so nothing derives from it either.
    /// </summary>
    public static bool IsThumbnailable(string? mediaType) =>
        mediaType is not null
        && mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
        && !mediaType.StartsWith("image/svg", StringComparison.OrdinalIgnoreCase);
}

/// <summary>How the viewer shows one Case document, where it shows it at all.</summary>
public enum CaseDocumentPreviewKind
{
    /// <summary>Not shown over the page: the file is a download.</summary>
    None,
    Image,
    Pdf,
    Video,

    /// <summary>A Word document, shown as a PDF rendering of its stored version.</summary>
    WordAsPdf
}

/// <summary>
/// The one owner of which Case documents the viewer shows, and as what.
/// </summary>
/// <remarks>
/// A Case document is arbitrary operator-supplied content, so retained HTML
/// served inline would execute as same-origin script: only images, PDFs and
/// the two admitted video media types are ever dispositioned for display.
/// SVG is an image that executes script when it is navigated to, so it stays
/// a download. A Word document is never served inline itself; the viewer is
/// given a PDF rendering of the stored version, and the file a reader saves
/// is still the Word document (operator, 11 November 2026, R949). A PDF
/// that arrived as <c>application/octet-stream</c>, as AX's instructions do,
/// is known by its <c>.pdf</c> name and shown as the PDF it is (operator,
/// 25 November 2026, R1246).
/// </remarks>
public static class CaseDocumentPreviews
{
    /// <summary>
    /// The one current PDF rendering of a Word document. This changes whenever
    /// the rendering can change its bytes, so the address, the cache variant
    /// and the validator all advance together.
    /// </summary>
    public const string RenditionIdentity = "p1";

    /// <summary>The variant a PDF rendering is cached and validated under.</summary>
    public const string RenditionVariant = "pdf-" + RenditionIdentity;

    /// <summary>What a rendering is served as.</summary>
    public const string RenditionMediaType = PdfMediaType;

    private const string PdfMediaType = "application/pdf";

    /// <summary>
    /// How long one request waits for a rendering that is still being made
    /// before it answers "not yet": the budget a render behind a busy state
    /// has (docs/engineering.md, Simplicity).
    /// </summary>
    public static readonly TimeSpan RenditionWait = TimeSpan.FromSeconds(3);

    /// <summary>
    /// How this document is shown. A PDF or a Word document is known by its
    /// media type, or by its file name where it arrived as
    /// <c>application/octet-stream</c>, as a mailed attachment often does.
    /// </summary>
    public static CaseDocumentPreviewKind KindOf(string? mediaType, string? fileName)
    {
        if (BareMediaType(mediaType) is not { } type)
        {
            return CaseDocumentPreviewKind.None;
        }
        if (type.Equals(PdfMediaType, StringComparison.OrdinalIgnoreCase))
        {
            return CaseDocumentPreviewKind.Pdf;
        }
        if (type.Equals(IntakeUploadFilePolicy.Mp4MediaType, StringComparison.OrdinalIgnoreCase)
            || type.Equals(IntakeUploadFilePolicy.MovMediaType, StringComparison.OrdinalIgnoreCase))
        {
            return CaseDocumentPreviewKind.Video;
        }
        if (type.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return type.Equals("image/svg+xml", StringComparison.OrdinalIgnoreCase)
                ? CaseDocumentPreviewKind.None
                : CaseDocumentPreviewKind.Image;
        }
        if (IsWord(type))
        {
            return CaseDocumentPreviewKind.WordAsPdf;
        }
        if (!type.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(fileName))
        {
            return CaseDocumentPreviewKind.None;
        }
        var named = IntakeUploadFilePolicy.MediaTypeFor(fileName);
        if (string.Equals(named, PdfMediaType, StringComparison.OrdinalIgnoreCase))
        {
            return CaseDocumentPreviewKind.Pdf;
        }
        return IsWord(named) ? CaseDocumentPreviewKind.WordAsPdf : CaseDocumentPreviewKind.None;
    }

    /// <summary>
    /// The media type a preview is served and drawn as: <c>application/pdf</c>
    /// for a PDF stored under another type, and the stored media type
    /// otherwise. The saved download keeps the stored type.
    /// </summary>
    public static string ShownMediaType(string mediaType, string? fileName) =>
        KindOf(mediaType, fileName) == CaseDocumentPreviewKind.Pdf
        && !string.Equals(BareMediaType(mediaType), PdfMediaType, StringComparison.OrdinalIgnoreCase)
            ? PdfMediaType
            : mediaType;

    /// <summary>
    /// Whether the stored bytes themselves may be dispositioned for display
    /// from this origin: an image, a PDF or an admitted video, and nothing else.
    /// </summary>
    public static bool IsInlineSafe(string? mediaType) =>
        KindOf(mediaType, fileName: null)
            is CaseDocumentPreviewKind.Image or CaseDocumentPreviewKind.Pdf or CaseDocumentPreviewKind.Video;

    /// <summary>
    /// The <c>type/subtype</c> of a media type, without its parameters, or
    /// <c>null</c> when the value is not one.
    /// </summary>
    private static string? BareMediaType(string? mediaType)
    {
        if (string.IsNullOrWhiteSpace(mediaType))
        {
            return null;
        }
        var parameters = mediaType.IndexOf(';', StringComparison.Ordinal);
        var type = (parameters < 0 ? mediaType : mediaType[..parameters]).Trim();
        var slash = type.IndexOf('/', StringComparison.Ordinal);
        return slash > 0
            && slash < type.Length - 1
            && type.LastIndexOf('/') == slash
            && !type.Any(char.IsWhiteSpace)
                ? type
                : null;
    }

    private static bool IsWord(string? mediaType) =>
        string.Equals(mediaType, IntakeUploadFilePolicy.DocxMediaType, StringComparison.OrdinalIgnoreCase)
        || string.Equals(mediaType, IntakeUploadFilePolicy.DocMediaType, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// The PDF rendering of one Case document the browser cannot draw itself (a
/// Word document), made from the stored version.
/// </summary>
/// <remarks>
/// Answers <c>null</c> when no rendering can be made of this document, and
/// throws <see cref="TimeoutException"/> when one is still being made after
/// <see cref="CaseDocumentPreviews.RenditionWait"/>; the caller answers that
/// with a retry. The rendering is derived data: the document a reader saves
/// is always the stored original.
/// </remarks>
public interface IRenderDocumentAsPdf
{
    Task<byte[]?> RenderAsync(
        ActionActor actor,
        CaseDocumentPreview preview,
        CancellationToken cancellationToken);

    /// <summary>
    /// The PDF rendering of one retained intake file the browser cannot draw
    /// itself (a Word document attached to an e-mail, viewed before a Case as
    /// after; R1275), made from its confirmed Box copy. Answers as
    /// <see cref="RenderAsync"/> does. It is not kept: the derived renderings
    /// are keyed by a Case document version, which an intake file is not.
    /// </summary>
    Task<byte[]?> RenderIntakeAssetAsync(
        ActionActor actor,
        IntakeAssetPreview asset,
        CancellationToken cancellationToken);
}

/// <summary>
/// One retained intake file a rendering is asked for: the receipt it belongs
/// to, the asset, and the hash and length the read verifies against.
/// </summary>
public sealed record IntakeAssetPreview(
    Guid ReceiptId,
    Guid AssetId,
    string FileName,
    string MediaType,
    long ContentLength,
    string Sha256);
