using System.Security.Cryptography;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Intake;

/// <summary>
/// The one owner of which of a receipt's retained assets count as the
/// instruction's evidence photographs: every deliberately attached image
/// file, plus embedded PDF images large enough to be photographs rather than
/// letterhead art. Inline images (signature graphics) never qualify, and one
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
                IntakeAssetKind.Attachment => IsImage(asset.MediaType),
                IntakeAssetKind.EmbeddedImage =>
                    asset.ContentLength >= EmbeddedPhotographMinimumBytes,
                _ => false
            })
            .Where(IsPhotographShaped)
            .OrderBy(asset => asset.Kind == IntakeAssetKind.Attachment ? 0 : 1)
            .ThenBy(asset => asset.FileName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(asset => asset.Id)
            .DistinctBy(asset => asset.ContentHash, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Whether an image is shaped like a photograph rather than a banner.
    /// Fails open: an image whose dimensions were not recorded is judged on
    /// the other rules alone, because refusing to show a genuine
    /// photograph is the worse error of the two.
    /// </summary>
    public static bool IsPhotographShaped(IntakeAssetRecord asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        if (asset.WidthPixels is not { } width
            || asset.HeightPixels is not { } height
            || width <= 0
            || height <= 0)
        {
            return true;
        }

        var longest = Math.Max(width, height);
        var shortest = Math.Min(width, height);
        return (double)longest / shortest < MaximumPhotographSideRatio;
    }

    public static bool IsImage(string? mediaType) =>
        mediaType is not null
        && mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
}

/// <summary>One evidence image of a case's instruction receipts.</summary>
/// <summary>
/// One photograph on the case's Evidence gallery.
///
/// DOCS-007 made Box the record: once intake's files are registered as case
/// documents, <see cref="OccurrenceId"/> and <see cref="VersionId"/> are set and
/// the image is served from Box through the case-document route. They are null
/// only for a case accepted before those records existed, which still renders
/// from its retained intake asset — the transition is additive, and a case
/// stops rendering the day its staging blobs age out, not the day this shipped.
/// </summary>
public sealed record CaseEvidenceImage(
    Guid ReceiptId,
    Guid AssetId,
    string FileName,
    string MediaType,
    long ContentLength,
    Guid? OccurrenceId = null,
    Guid? VersionId = null)
{
    /// <summary>Whether this image is served from Box rather than from the staging blob.</summary>
    public bool IsCaseDocument => OccurrenceId is not null && VersionId is not null;
}

public interface ICaseEvidenceImageQueries
{
    Task<IReadOnlyList<CaseEvidenceImage>> ListForCaseAsync(
        Guid caseId,
        CancellationToken cancellationToken);
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

public sealed record IntakeAssetMetadataQuery(
    Guid ReceiptId,
    Guid AssetId,
    ActionActor Actor);

public interface IGetIntakeAssetMetadata
{
    Task<IntakeFileMetadata?> ExecuteAsync(
        IntakeAssetMetadataQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The exact metadata of one retained asset, authorized at the same boundary
/// its bytes are and carrying no storage key. A connector asks for this before
/// it asks for content, and verifies the content it receives against the hash
/// and length it was given here.
/// </summary>
public sealed class GetIntakeAssetMetadata(IIntakeReceiptQueries receiptQueries)
    : IGetIntakeAssetMetadata
{
    public async Task<IntakeFileMetadata?> ExecuteAsync(
        IntakeAssetMetadataQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        StaffAuthorization.Require(query.Actor, StaffAccessRight.PerformCasework);
        if (query.ReceiptId == Guid.Empty || query.AssetId == Guid.Empty)
        {
            return null;
        }

        var receipt = await receiptQueries.GetAsync(query.ReceiptId, cancellationToken);
        var asset = receipt?.AssetRecords.SingleOrDefault(record => record.Id == query.AssetId);
        return asset is null ? null : IntakeFileIdentity.Describe(receipt!, asset);
    }
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
        // exactly the ordinary operational casework surface. A request-link,
        // provider or system-worker actor fails closed here rather than at a
        // surface that might forget to ask.
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
