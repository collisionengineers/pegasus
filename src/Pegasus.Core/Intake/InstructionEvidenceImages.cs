using System.Security.Cryptography;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Intake;

/// <summary>
/// The one owner of which of a receipt's retained assets count as the
/// instruction's evidence photographs: every deliberately attached image
/// file, a directly uploaded image source, plus embedded PDF images large
/// enough to be photographs rather than letterhead art. Inline images
/// (signature graphics) never qualify, and one
/// photograph carried twice — attached and embedded, or repeated across
/// pages — appears once, preferring the attached copy. Custody promotion and
/// the case evidence gallery both resolve through this selection.
/// </summary>
public static class InstructionEvidenceImages
{
    /// <summary>
    /// Corpus-measured floor for an embedded image to read as a photograph:
    /// the letters' repeated letterhead art tops out under 29 KB while
    /// genuine damage photographs start above 60 KB.
    /// </summary>
    public const long EmbeddedPhotographMinimumBytes = 40_000;

    /// <summary>
    /// Corpus-measured shape test, and the one that actually separates a
    /// banner from a photograph. A byte floor alone does not: QDOS26008's
    /// two false positives were a 110,783-byte PNG at 1990x437 and a
    /// 77,972-byte JPEG at 2214x248 — both well over the floor, and one of
    /// them a JPEG, so neither size nor format would have caught them. The
    /// same 1990x437 letterhead appears in five unrelated reports across
    /// the corpus.
    ///
    /// Every genuine photograph on that receipt measured between 1.09 and
    /// 1.15, and the widest thing in the corpus sample that might be a
    /// photograph measured 2.22, so 3.0 sits in open space: wide enough to
    /// leave a panoramic photograph alone, narrow enough to catch every
    /// banner measured (3.19, 3.30, 4.55, 8.93, 9.08).
    /// </summary>
    public const double MaximumPhotographSideRatio = 3.0;

    public static IReadOnlyList<IntakeAssetRecord> Select(
        IEnumerable<IntakeAssetRecord> assets)
    {
        ArgumentNullException.ThrowIfNull(assets);
        return assets
            .Where(asset => asset.Kind switch
            {
                IntakeAssetKind.Source =>
                    asset.Disposition == IntakeAssetDisposition.Source
                    && IsImage(asset.MediaType),
                IntakeAssetKind.Attachment =>
                    asset.Disposition == IntakeAssetDisposition.Attachment
                    && IsImage(asset.MediaType),
                IntakeAssetKind.EmbeddedImage =>
                    asset.Disposition == IntakeAssetDisposition.Embedded
                    && IsImage(asset.MediaType)
                    && asset.ContentLength >= EmbeddedPhotographMinimumBytes,
                _ => false
            })
            .Where(IsPhotographShaped)
            .OrderBy(asset => asset.Kind switch
            {
                IntakeAssetKind.Attachment => 0,
                IntakeAssetKind.Source => 1,
                _ => 2
            })
            .ThenBy(asset => asset.FileName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(asset => asset.Id)
            .DistinctBy(asset => asset.ContentHash, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Decides whether a reader candidate becomes a separately retained
    /// receipt asset. Sources and non-image document attachments remain part
    /// of the receipt; this gate excludes only image material that cannot be
    /// vehicle evidence (inline/signature graphics, small embedded document
    /// art and banner-shaped images). It is intentionally applied before
    /// storage so excluded document assets never enter custody.
    /// </summary>
    public static bool IsRetentionCandidate(IntakeAssetCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (candidate.Kind == IntakeAssetKind.Source)
        {
            return true;
        }

        if (candidate.Kind == IntakeAssetKind.InlineImage
            || candidate.Disposition == IntakeAssetDisposition.Inline)
        {
            return false;
        }

        if (!IsImage(candidate.MediaType))
        {
            return candidate.Kind != IntakeAssetKind.EmbeddedImage;
        }

        return candidate.Kind switch
        {
            IntakeAssetKind.Attachment =>
                candidate.Disposition == IntakeAssetDisposition.Attachment
                && IsPhotographShaped(candidate.WidthPixels, candidate.HeightPixels),
            IntakeAssetKind.EmbeddedImage =>
                candidate.Disposition == IntakeAssetDisposition.Embedded
                && candidate.Content.Length >= EmbeddedPhotographMinimumBytes
                && IsPhotographShaped(candidate.WidthPixels, candidate.HeightPixels),
            _ => false
        };
    }

    /// <summary>
    /// Whether retained material contains at least one selected photograph.
    /// Image automation uses this rather than inferring eligibility from the
    /// receipt source MIME type, because an email or PDF can carry the
    /// photographs while remaining attached as source evidence itself.
    /// </summary>
    public static bool HasSelectedPhotographs(IntakeReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        return Select(receipt.AssetRecords).Count != 0;
    }

    /// <summary>
    /// The evidence images whose durable bytes can be served now. Only a
    /// confirmed hand-over has an authorized content location; Pending,
    /// Failed and Unknown all remain visible as receipt metadata but never
    /// become a download/gallery link.
    /// </summary>
    public static IReadOnlyList<IntakeAssetRecord> Servable(
        IEnumerable<IntakeAssetRecord> assets) =>
        [.. Select(assets).Where(asset => asset.CustodyState == IncomingArtifactCustodyState.Confirmed)];

    /// <summary>
    /// Whether an image is shaped like a photograph rather than a banner.
    /// Fails open: an image whose dimensions were not recorded is judged on
    /// the other rules alone, because refusing to show a genuine
    /// photograph is the worse error of the two.
    /// </summary>
    public static bool IsPhotographShaped(IntakeAssetRecord asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        return IsPhotographShaped(asset.WidthPixels, asset.HeightPixels);
    }

    private static bool IsPhotographShaped(int? widthPixels, int? heightPixels)
    {
        if (widthPixels is not { } width
            || heightPixels is not { } height
            || width <= 0
            || height <= 0)
        {
            return true;
        }

        var longest = Math.Max(width, height);
        var shortest = Math.Min(width, height);
        return (double)longest / shortest < MaximumPhotographSideRatio;
    }

    /// <summary>
    /// The case-file image policy accepts only inert raster formats. In
    /// particular, an SVG is not evidence-gallery content: it may contain
    /// active markup and document graphics rather than a vehicle photograph.
    /// </summary>
    public static bool IsImage(string? mediaType) =>
        string.Equals(mediaType, "image/jpeg", StringComparison.OrdinalIgnoreCase)
        || string.Equals(mediaType, "image/png", StringComparison.OrdinalIgnoreCase);
}

public sealed record DownloadIntakeAssetQuery(
    Guid ReceiptId,
    Guid AssetId,
    ActionActor Actor);

public interface IDownloadIntakeAsset
{
    Task<IntakeSourceDownload?> ExecuteAsync(
        DownloadIntakeAssetQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Downloads one retained asset of a receipt, hash-verified against its
/// recorded content the same way the source download is. The receipt id
/// scopes the lookup so an asset can never be fetched under another
/// receipt's identity.
///
/// When the logical-document reader is composed, the bytes are served through
/// it by asset identity, so no storage key crosses this boundary and the reader
/// resolves the custody or cache address itself. Until that adapter exists the
/// hash-verified artifact path below is the whole of the behaviour, and the
/// integrity check is identical either way — the difference is where the bytes
/// come from, never whether they are verified.
/// </summary>
public sealed class DownloadIntakeAsset(
    IIntakeReceiptQueries receiptQueries,
    IIntakeArtifactStore artifactStore,
    IReadLogicalDocumentVersion? logicalDocumentReader = null) : IDownloadIntakeAsset
{
    public async Task<IntakeSourceDownload?> ExecuteAsync(
        DownloadIntakeAssetQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.ReceiptId == Guid.Empty || query.AssetId == Guid.Empty)
        {
            return null;
        }

        // Staff casework, or the Automation Actor, which ADR-0011 grants
        // exactly the ordinary operational casework surface. A provider or
        // system-worker actor fails closed here rather than at a surface that
        // might forget to ask.
        StaffAuthorization.Require(query.Actor, StaffAccessRight.PerformCasework);
        var receipt = await receiptQueries.GetAsync(query.ReceiptId, cancellationToken);
        var asset = receipt?.AssetRecords
            .SingleOrDefault(record => record.Id == query.AssetId);
        if (asset is null)
        {
            return null;
        }

        if (logicalDocumentReader is not null)
        {
            await using var logical = await logicalDocumentReader.OpenAsync(
                new(
                    query.Actor,
                    DocumentId: null,
                    VersionId: null,
                    IntakeAssetId: asset.Id,
                    CaseId: null,
                    IntakeReceiptId: query.ReceiptId,
                    asset.ContentHash,
                    asset.ContentLength),
                cancellationToken);
            using var buffer = new MemoryStream();
            await logical.Content.CopyToAsync(buffer, cancellationToken);
            var bytes = buffer.ToArray();
            return Verified(bytes, asset);
        }

        var content = await artifactStore.ReadAsync(asset.StorageKey, cancellationToken)
            ?? throw new IntakeArtifactIntegrityException();
        return Verified(content, asset);
    }

    private static IntakeSourceDownload Verified(
        ReadOnlyMemory<byte> content,
        IntakeAssetRecord asset)
    {
        var actualHash = Convert.ToHexString(SHA256.HashData(content.Span));
        if (content.Length != asset.ContentLength
            || !DownloadIntakeSource.FixedTimeHashEquals(actualHash, asset.ContentHash))
        {
            throw new IntakeArtifactIntegrityException();
        }

        return new(
            content,
            asset.FileName,
            asset.MediaType,
            content.Length,
            actualHash);
    }
}
