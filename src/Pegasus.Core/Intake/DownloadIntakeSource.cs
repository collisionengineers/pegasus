using System.Security.Cryptography;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Intake;

public sealed class DownloadIntakeSource(
    IIntakeReceiptQueries receiptQueries,
    IReadLogicalDocumentVersion contentReader) : IDownloadIntakeSource
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
        await using var logical = await contentReader.OpenAsync(
            new(
                query.Actor,
                DocumentId: null,
                VersionId: null,
                IntakeAssetId: sourceAsset.Id,
                CaseId: receipt.CurrentCaseId,
                IntakeReceiptId: receipt.Id,
                ExpectedSha256: sourceAsset.ContentHash,
                ExpectedContentLength: sourceAsset.ContentLength),
            cancellationToken);
        if (logical.ContentLength > int.MaxValue)
        {
            throw new IntakeArtifactIntegrityException();
        }

        using var retained = new MemoryStream((int)logical.ContentLength);
        await CopyExactlyAsync(
            logical.Content,
            retained,
            logical.ContentLength,
            cancellationToken);
        var content = retained.ToArray();
        var actualHash = Convert.ToHexString(SHA256.HashData(content));
        if (content.LongLength != sourceAsset.ContentLength
            || content.LongLength != receipt.SourceLength
            || !FixedTimeHashEquals(actualHash, logical.Sha256)
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

    private static async Task CopyExactlyAsync(
        Stream source,
        Stream destination,
        long expectedLength,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long copied = 0;
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            copied = checked(copied + read);
            if (copied > expectedLength)
            {
                throw new IntakeArtifactIntegrityException();
            }
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        if (copied != expectedLength)
        {
            throw new IntakeArtifactIntegrityException();
        }
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
