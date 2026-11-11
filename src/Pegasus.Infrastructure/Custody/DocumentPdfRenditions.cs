using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Infrastructure.Custody;

/// <summary>
/// The renderer for a profile with no Box custody: no rendering can be made,
/// so a Word document there is a download, as it was.
/// </summary>
public sealed class NoDocumentPdfRenderer : IRenderDocumentAsPdf
{
    public Task<byte[]?> RenderAsync(
        ActionActor actor,
        CaseDocumentPreview preview,
        CancellationToken cancellationToken) => Task.FromResult<byte[]?>(null);
}

/// <summary>
/// The PDF rendering of one Word document version: the cached variant where
/// there is one, otherwise Box's PDF representation of the stored version,
/// recorded for the next view.
/// </summary>
/// <remarks>
/// The rendering is a variant of the document content cache, beside the
/// gallery thumbnails (<see cref="DocumentThumbnailCache"/>): derived,
/// rebuildable, and removed by the same cleanup. A miss asks Box through
/// <see cref="CachedDocumentContentStore.ReadPdfRepresentationAsync"/>, which
/// applies the durable read's authorization and custody checks.
/// </remarks>
internal sealed class CaseDocumentPdfRenditionReader(
    CachedDocumentContentStore store,
    DocumentThumbnailCache cache,
    ILogger<CaseDocumentPdfRenditionReader>? logger = null) : IRenderDocumentAsPdf
{
    public async Task<byte[]?> RenderAsync(
        ActionActor actor,
        CaseDocumentPreview preview,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(preview);
        if (CaseDocumentPreviews.KindOf(preview.MediaType, preview.FileName) != CaseDocumentPreviewKind.WordAsPdf)
        {
            return null;
        }
        return await DerivedRenderings.OpenAsync(
            cache,
            actor,
            preview.VersionId,
            CaseDocumentPreviews.RenditionVariant,
            token => store.ReadPdfRepresentationAsync(
                new(
                    Actor: actor,
                    DocumentId: preview.DocumentId,
                    VersionId: preview.VersionId,
                    IntakeAssetId: null,
                    CaseId: preview.CaseId,
                    IntakeReceiptId: null,
                    ExpectedSha256: preview.Sha256,
                    ExpectedContentLength: preview.ContentLength),
                CaseDocumentPreviews.RenditionWait,
                token),
            "document.rendition.render.gate",
            logger ?? NullLogger<CaseDocumentPdfRenditionReader>.Instance,
            cancellationToken);
    }
}

internal sealed partial class CachedDocumentContentStore
{
    /// <summary>
    /// Box's PDF representation of one authorised document version, or
    /// <c>null</c> when Box offers none: the version is no longer the file's
    /// current one, Box could not make one of it, or what came back is not a
    /// PDF within the content limit.
    /// </summary>
    /// <remarks>
    /// The version is resolved exactly as <see cref="OpenAsync"/> resolves it:
    /// the actor's staff right, the Case the document belongs to, confirmed
    /// custody only and the durable hash and length the caller named. Asking
    /// Box where the representation is takes no read-gate slot, because that
    /// call waits while Box makes it; the download takes one, with the same
    /// 429/5xx retry as every other managed read. A representation still being
    /// made after <paramref name="wait"/> is a <see cref="TimeoutException"/>.
    /// </remarks>
    internal async Task<byte[]?> ReadPdfRepresentationAsync(
        ReadLogicalDocumentVersionRequest request,
        TimeSpan wait,
        CancellationToken cancellationToken)
    {
        ValidateRequest(request);
        RequireStaffRight(request.Actor);
        var (source, _) = await ResolveAuthorizedSourceAsync(request, cancellationToken);
        if (source.Length != request.ExpectedContentLength
            || !FixedHashEquals(source.Sha256, request.ExpectedSha256))
        {
            throw new InvalidDataException("The requested logical content identity does not match durable metadata.");
        }
        try
        {
            var address = await box.FindOwnedVersionPdfAsync(
                source.BoxFileId, source.BoxVersionId, source.ExpectedParentId, wait, cancellationToken);
            if (address is null)
            {
                return null;
            }
            byte[] rendering;
            try
            {
                rendering = await BoxDocumentContentStore.ReadGatedWithRetryAsync(
                    async token =>
                    {
                        await using var remote = await box.DownloadPdfRepresentationAsync(
                            address, IntakeEnvelopeLimits.MaximumContentLength, token);
                        var content = new byte[remote.Length];
                        await remote.ReadExactlyAsync(content, token);
                        return content;
                    },
                    cancellationToken);
            }
            catch (InvalidDataException)
            {
                // Longer than any document Pegasus admits: not a rendering to keep.
                return null;
            }
            return IntakeUploadFilePolicy.HeaderMatches(null, CaseDocumentPreviews.RenditionMediaType, rendering) == true
                ? rendering
                : null;
        }
        catch (HttpRequestException exception)
            when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // The same answer the content read gives for a missing version.
            throw new FileNotFoundException("The exact managed Box version is unavailable.", exception);
        }
    }
}
