using System.Security.Cryptography;
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
            .OrderBy(asset => asset.Kind == IntakeAssetKind.Attachment ? 0 : 1)
            .ThenBy(asset => asset.FileName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(asset => asset.Id)
            .DistinctBy(asset => asset.ContentHash, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static bool IsImage(string? mediaType) =>
        mediaType is not null
        && mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
}

/// <summary>One evidence image of a case's instruction receipts.</summary>
public sealed record CaseEvidenceImage(
    Guid ReceiptId,
    Guid AssetId,
    string FileName,
    string MediaType,
    long ContentLength);

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

/// <summary>
/// Downloads one retained asset of a receipt, hash-verified against its
/// recorded content the same way the source download is. The receipt id
/// scopes the lookup so an asset can never be fetched under another
/// receipt's identity.
/// </summary>
public sealed class DownloadIntakeAsset(
    IIntakeReceiptQueries receiptQueries,
    IIntakeArtifactStore artifactStore) : IDownloadIntakeAsset
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

        StaffAuthorization.Require(query.Actor, StaffAccessRight.PerformCasework);
        var receipt = await receiptQueries.GetAsync(query.ReceiptId, cancellationToken);
        var asset = receipt?.AssetRecords
            .SingleOrDefault(record => record.Id == query.AssetId);
        if (asset is null)
        {
            return null;
        }

        var content = await artifactStore.ReadAsync(asset.StorageKey, cancellationToken)
            ?? throw new IntakeArtifactIntegrityException();
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
