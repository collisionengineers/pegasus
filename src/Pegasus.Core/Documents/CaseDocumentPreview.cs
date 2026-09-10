using Pegasus.Core.Identity;

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
/// DOCS-015: a gallery tile is a preview, not a download. The audited
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
    string MediaType);

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
/// DOCS-015: a tile of a 6 MB photograph asked for the whole photograph.
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
/// The one owner of what a thumbnail is: how large, what it is encoded as, and
/// which media types have one at all.
/// </summary>
public static class CaseDocumentThumbnails
{
    /// <summary>The longest edge of a derived thumbnail, in pixels.</summary>
    public const int LongestEdge = 480;

    /// <summary>What a derived thumbnail is encoded as.</summary>
    public const string MediaType = "image/jpeg";

    /// <summary>The request value that asks for the derived rendering.</summary>
    public const string ThumbSizeToken = "thumb";

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
