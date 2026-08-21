using System.Security.Cryptography;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Intake;

public sealed class DownloadIntakeSource(
    IIntakeReceiptQueries receiptQueries,
    IIntakeArtifactStore artifactStore) : IDownloadIntakeSource
{
    public async Task<IntakeSourceDownload?> ExecuteAsync(
        DownloadIntakeSourceQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.ReceiptId == Guid.Empty)
        {
            return null;
        }

        StaffAuthorization.Require(query.Actor, StaffAccessRight.PerformCasework);
        var receipt = await receiptQueries.GetAsync(query.ReceiptId, cancellationToken);
        if (receipt is null)
        {
            return null;
        }

        var source = receipt.AssetRecords
            .Where(asset => asset.Kind == IntakeAssetKind.Source
                && asset.Disposition == IntakeAssetDisposition.Source)
            .Take(2)
            .ToArray();
        if (source.Length != 1)
        {
            throw new IntakeArtifactIntegrityException();
        }

        var sourceAsset = source[0];
        var content = await artifactStore.ReadAsync(sourceAsset.StorageKey, cancellationToken)
            ?? throw new IntakeArtifactIntegrityException();
        var actualHash = Convert.ToHexString(SHA256.HashData(content.Span));
        if (content.Length != sourceAsset.ContentLength
            || content.Length != receipt.SourceLength
            || !FixedTimeHashEquals(actualHash, sourceAsset.ContentHash)
            || !FixedTimeHashEquals(actualHash, receipt.SourceHash))
        {
            throw new IntakeArtifactIntegrityException();
        }

        return new(
            content,
            SafeFileName(receipt.SourceFileName),
            sourceAsset.MediaType,
            content.Length,
            actualHash);
    }

    private static string SafeFileName(string value)
    {
        var normalised = value.Replace('\\', '/');
        var leaf = normalised[(normalised.LastIndexOf('/') + 1)..];
        var safe = new string(leaf
            .Where(character => !char.IsControl(character)
                && character is not '"' and not '\'' and not ';')
            .Take(180)
            .ToArray())
            .Trim();
        return string.IsNullOrWhiteSpace(safe) ? "intake-source.bin" : safe;
    }

    internal static bool FixedTimeHashEquals(string left, string right)
    {
        if (left.Length != 64
            || right.Length != 64
            || !left.All(char.IsAsciiHexDigit)
            || !right.All(char.IsAsciiHexDigit))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(left),
            Convert.FromHexString(right));
    }
}
