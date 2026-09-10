using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Pages.Cases.Documents;

/// <summary>
/// One authorised read of one Case document: the saved download, and the
/// preview a gallery tile or the viewer displays.
/// </summary>
/// <remarks>
/// DOCS-015. The two are different reads, not one read with a different
/// header. A download is a custody act: it goes through
/// <see cref="IDownloadCaseDocument"/>, which records it, and it is never
/// cached. A preview is a page element: it resolves the occurrence through
/// <see cref="IReadCaseDocumentPreview"/> and reads the bytes through
/// <see cref="IReadLogicalDocumentVersion"/>, which verifies the same custody
/// hash and writes no history row, because sixty tiles are one page view and
/// not sixty downloads.
///
/// A version's content never changes, so a preview is cacheable by its
/// SHA-256 and answers <c>If-None-Match</c> with 304 rather than sending the
/// bytes again. That, and <c>size=thumb</c>, are what stopped a fresh
/// sixty-image Case from asking Box for sixty full photographs on every visit
/// — the load that produced the broken tiles this route now avoids: a
/// throttled or unavailable read is a 503 with <c>Retry-After</c>, which the
/// page retries, instead of a 404, which the browser draws as a broken image.
/// </remarks>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
public sealed partial class DownloadModel(
    IDownloadCaseDocument downloadCaseDocument,
    IReadCaseDocumentPreview readCaseDocumentPreview,
    IReadLogicalDocumentVersion readLogicalDocumentVersion,
    IReadCaseDocumentThumbnail readCaseDocumentThumbnail,
    ILogger<DownloadModel> logger) : StaffPageModel
{
    /// <summary>
    /// How long a browser may keep one preview. The content of a document
    /// version is immutable, so the only bound is how long a cached copy of an
    /// authorised private response is acceptable at all.
    /// </summary>
    private const int PreviewCacheSeconds = 604_800;

    /// <summary>
    /// How long a caller waits before retrying a read Box refused for now.
    /// </summary>
    private const int TransientRetrySeconds = 5;

    public async Task<IActionResult> OnGetAsync(
        Guid caseId,
        Guid occurrenceId,
        Guid versionId,
        CancellationToken cancellationToken,
        bool inline = false,
        string? size = null)
    {
        if (caseId == Guid.Empty || occurrenceId == Guid.Empty || versionId == Guid.Empty)
        {
            return NotFound();
        }
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        return inline
            ? await PreviewAsync(caseId, occurrenceId, versionId, actor, size, cancellationToken)
            : await SaveAsync(caseId, occurrenceId, versionId, actor, cancellationToken);
    }

    /// <summary>
    /// The audited download: the same read this route has always made, with a
    /// transient content-store failure now told apart from a missing file.
    /// </summary>
    private async Task<IActionResult> SaveAsync(
        Guid caseId,
        Guid occurrenceId,
        Guid versionId,
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        try
        {
            var download = await downloadCaseDocument.ExecuteAsync(
                new(
                    caseId,
                    occurrenceId,
                    versionId,
                    actor,
                    $"web-download:{Guid.NewGuid():N}"),
                cancellationToken);
            if (download is null)
            {
                return NotFound();
            }
            if (!TryValidateResponse(
                download.FileName,
                download.MediaType,
                download.ContentLength,
                download.Sha256,
                out var fileName,
                out var mediaType,
                out var sha256))
            {
                await download.DisposeAsync();
                LogUnsafeDocumentResponse(logger, caseId, occurrenceId, versionId);
                return NotFound();
            }

            Response.Headers.CacheControl = "private, no-store";
            Response.Headers.XContentTypeOptions = "nosniff";
            Response.Headers["X-Content-SHA256"] = sha256;
            Response.ContentLength = download.ContentLength;
            return File(download.Content, mediaType, fileName);
        }
        catch (Exception exception) when (IsMissing(exception))
        {
            LogDocumentDownloadDenied(logger, caseId, occurrenceId, versionId, exception);
            return NotFound();
        }
        catch (Exception exception) when (IsTransient(exception))
        {
            LogTransientDocumentRead(logger, caseId, occurrenceId, versionId, exception);
            return TransientlyUnavailable();
        }
    }

    /// <summary>
    /// The preview: no history row, cacheable by content hash, and answered
    /// with the derived thumbnail when <c>size=thumb</c> asks for one.
    /// </summary>
    private async Task<IActionResult> PreviewAsync(
        Guid caseId,
        Guid occurrenceId,
        Guid versionId,
        ActionActor actor,
        string? size,
        CancellationToken cancellationToken)
    {
        CaseDocumentPreview? preview;
        try
        {
            preview = await readCaseDocumentPreview.ExecuteAsync(
                new(caseId, occurrenceId, versionId, actor), cancellationToken);
        }
        catch (Exception exception) when (IsMissing(exception))
        {
            LogDocumentDownloadDenied(logger, caseId, occurrenceId, versionId, exception);
            return NotFound();
        }
        if (preview is null)
        {
            return NotFound();
        }
        if (!TryValidateResponse(
            preview.FileName,
            preview.MediaType,
            preview.ContentLength,
            preview.Sha256,
            out var fileName,
            out var mediaType,
            out var sha256))
        {
            LogUnsafeDocumentResponse(logger, caseId, occurrenceId, versionId);
            return NotFound();
        }
        if (!IsInlineSafe(mediaType))
        {
            // Never displayed from this origin, so an inline request for it is
            // the saved download it always was — audit row included.
            return await SaveAsync(caseId, occurrenceId, versionId, actor, cancellationToken);
        }
        if (preview.CustodyStatus != DocumentCustodyStatus.Confirmed)
        {
            // DOCS-015: a Case opened while custody is still in flight. The
            // file exists and is arriving, which is a wait, not a 404 — the
            // gallery draws it as a placeholder and comes back for it.
            return preview.CustodyStatus == DocumentCustodyStatus.Pending
                ? TransientlyUnavailable()
                : NotFound();
        }

        var wantsThumbnail = string.Equals(
                size, CaseDocumentThumbnails.ThumbSizeToken, StringComparison.OrdinalIgnoreCase)
            && CaseDocumentThumbnails.IsThumbnailable(mediaType);
        if (TryMatchHeldRepresentation(sha256, wantsThumbnail, out var held))
        {
            // The bytes are already held. Restate the caching terms, because a
            // 304 refreshes them, and send nothing else.
            SetPreviewCaching(held, cacheable: true);
            return StatusCode(StatusCodes.Status304NotModified);
        }

        try
        {
            if (wantsThumbnail)
            {
                var thumbnail = await readCaseDocumentThumbnail.OpenAsync(
                    new(
                        actor,
                        caseId,
                        preview.DocumentId,
                        versionId,
                        sha256,
                        preview.ContentLength,
                        mediaType),
                    cancellationToken);
                if (thumbnail is not null)
                {
                    // The rendering is derived, so the custody hash of the
                    // source is not the hash of these bytes and is not claimed
                    // as one; the ETag says which source it was derived from.
                    SetPreviewCaching(ThumbnailETag(sha256), cacheable: true);
                    Response.ContentLength = thumbnail.ContentLength;
                    SetInlineDisposition(fileName);
                    return File(thumbnail.Content, thumbnail.MediaType);
                }
                // No rendering could be produced. The full image is the answer,
                // under its own content ETag rather than the thumbnail's.
            }

            var content = await readLogicalDocumentVersion.OpenAsync(
                new(
                    Actor: actor,
                    DocumentId: preview.DocumentId,
                    VersionId: versionId,
                    IntakeAssetId: null,
                    CaseId: caseId,
                    IntakeReceiptId: null,
                    ExpectedSha256: sha256,
                    ExpectedContentLength: preview.ContentLength),
                cancellationToken);
            SetPreviewCaching(ContentETag(sha256), IsCacheablePreview(mediaType));
            Response.Headers["X-Content-SHA256"] = sha256;
            Response.ContentLength = content.ContentLength;
            SetInlineDisposition(fileName);
            return File(content.Content, mediaType);
        }
        catch (Exception exception) when (IsMissing(exception))
        {
            LogDocumentDownloadDenied(logger, caseId, occurrenceId, versionId, exception);
            return NotFound();
        }
        catch (Exception exception) when (IsTransient(exception))
        {
            LogTransientDocumentRead(logger, caseId, occurrenceId, versionId, exception);
            return TransientlyUnavailable();
        }
    }

    /// <summary>
    /// DOCS-011: the same authorised read, dispositioned for a preview rather
    /// than a save. Naming the file to <c>File(...)</c> is what forces
    /// <c>attachment</c>, so the inline branch sets the header itself and
    /// passes no name — the idiom the retained-asset routes use.
    /// </summary>
    private void SetInlineDisposition(string fileName)
    {
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.ContentDisposition =
            new ContentDispositionHeaderValue("inline") { FileName = fileName }.ToString();
    }

    private void SetPreviewCaching(string etag, bool cacheable)
    {
        Response.Headers.CacheControl = cacheable
            ? $"private, max-age={PreviewCacheSeconds}, immutable"
            : "private, no-store";
        if (cacheable)
        {
            Response.Headers.ETag = etag;
        }
    }

    /// <summary>
    /// Which previews a browser may keep. An image is the page element this is
    /// about: a tile, and the viewer's own copy of it.
    /// </summary>
    private static bool IsCacheablePreview(string mediaType) =>
        MediaTypeHeaderValue.TryParse(mediaType, out var parsed)
        && parsed.Type.Equals("image", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A strong validator, because the bytes are exactly what the hash names.
    /// </summary>
    private static string ContentETag(string sha256) => $"\"{sha256}\"";

    private static string ThumbnailETag(string sha256) => $"\"{sha256}-thumb\"";

    /// <summary>
    /// Whether the caller already holds this representation, and which one.
    /// A thumbnail URL accepts the full image's validator too, because that is
    /// what it answers with when no rendering can be produced.
    /// </summary>
    private bool TryMatchHeldRepresentation(
        string sha256,
        bool wantsThumbnail,
        out string held)
    {
        held = string.Empty;
        var offered = Request.Headers.IfNoneMatch;
        if (offered.Count == 0)
        {
            return false;
        }
        string[] accepted = wantsThumbnail
            ? [ThumbnailETag(sha256), ContentETag(sha256)]
            : [ContentETag(sha256)];
        foreach (var header in offered)
        {
            foreach (var candidate in (header ?? string.Empty).Split(','))
            {
                var value = candidate.Trim();
                if (value.StartsWith("W/", StringComparison.Ordinal))
                {
                    value = value[2..];
                }
                var match = Array.Find(
                    accepted,
                    expected => string.Equals(expected, value, StringComparison.Ordinal));
                if (match is not null)
                {
                    held = match;
                    return true;
                }
            }
        }
        return false;
    }

    private StatusCodeResult TransientlyUnavailable()
    {
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.RetryAfter = TransientRetrySeconds.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
        return StatusCode(StatusCodes.Status503ServiceUnavailable);
    }

    /// <summary>
    /// The file is not there, or this reader may not have it. A missing Box
    /// object arrives as <see cref="FileNotFoundException"/>, which is an
    /// <see cref="IOException"/>, so it is classified before the transient
    /// rule below claims it.
    /// </summary>
    private static bool IsMissing(Exception exception) =>
        exception is ArgumentException
            or InvalidOperationException
            or InvalidDataException
            or UnauthorizedAccessException
            or FileNotFoundException
            or DirectoryNotFoundException;

    /// <summary>
    /// Box or the cache said "later": the rate limit, a provider failure, or a
    /// read that did not complete.
    /// </summary>
    private static bool IsTransient(Exception exception) =>
        exception is IOException or TimeoutException
        || exception is HttpRequestException http
            && (http.StatusCode == System.Net.HttpStatusCode.TooManyRequests
                || (int?)http.StatusCode >= 500)
        || exception is Azure.RequestFailedException { Status: 429 or >= 500 };

    private static bool TryValidateResponse(
        string candidateFileName,
        string candidateMediaType,
        long contentLength,
        string candidateSha256,
        out string fileName,
        out string mediaType,
        out string sha256)
    {
        fileName = Path.GetFileName(candidateFileName);
        mediaType = candidateMediaType;
        sha256 = candidateSha256.ToLowerInvariant();
        return IsSafeFileName(candidateFileName, fileName)
            && MediaTypeHeaderValue.TryParse(mediaType, out _)
            && contentLength >= 0
            && sha256.Length == 64
            && sha256.All(char.IsAsciiHexDigit);
    }

    /// <summary>
    /// Which media types may be rendered inline from this origin. A case
    /// document is arbitrary operator-supplied content, so retained HTML served
    /// inline would execute as same-origin script; only images, PDFs and the
    /// two admitted video media types are ever dispositioned for display. This restates
    /// for custody content the rule the retained-image routes already apply.
    /// Everything else keeps the attachment disposition.
    /// </summary>
    private static bool IsInlineSafe(string mediaType) =>
        MediaTypeHeaderValue.TryParse(mediaType, out var parsed)
        && (parsed.MediaType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)
            || parsed.MediaType.Equals(Pegasus.Core.Intake.IntakeUploadFilePolicy.Mp4MediaType, StringComparison.OrdinalIgnoreCase)
            || parsed.MediaType.Equals(Pegasus.Core.Intake.IntakeUploadFilePolicy.MovMediaType, StringComparison.OrdinalIgnoreCase)
            || (parsed.Type.Equals("image", StringComparison.OrdinalIgnoreCase)
                // SVG is an image that executes script when it is navigated to,
                // and the document link this route now serves is navigable --
                // with no script, or on a middle-click. It stays a download.
                && !parsed.MediaType.Equals("image/svg+xml", StringComparison.OrdinalIgnoreCase)));

    private static bool IsSafeFileName(string original, string fileName) =>
        !string.IsNullOrWhiteSpace(fileName)
        && fileName.Length <= 255
        && fileName is not "." and not ".."
        && string.Equals(fileName, original, StringComparison.Ordinal)
        && !fileName.Contains('/', StringComparison.Ordinal)
        && !fileName.Contains('\\', StringComparison.Ordinal)
        && !fileName.Any(char.IsControl);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Case document download was denied for case {CaseId}, occurrence {OccurrenceId}, version {VersionId}.")]
    private static partial void LogDocumentDownloadDenied(
        ILogger logger,
        Guid caseId,
        Guid occurrenceId,
        Guid versionId,
        Exception exception);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Case document content was temporarily unavailable for case {CaseId}, occurrence {OccurrenceId}, version {VersionId}.")]
    private static partial void LogTransientDocumentRead(
        ILogger logger,
        Guid caseId,
        Guid occurrenceId,
        Guid versionId,
        Exception exception);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Case document download returned unsafe metadata for case {CaseId}, occurrence {OccurrenceId}, version {VersionId}.")]
    private static partial void LogUnsafeDocumentResponse(
        ILogger logger,
        Guid caseId,
        Guid occurrenceId,
        Guid versionId);
}
