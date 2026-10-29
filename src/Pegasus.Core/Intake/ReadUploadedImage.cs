using System.Security.Cryptography;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Intake;

/// <summary>
/// An uploaded image's own bytes for the upload review, read from what intake
/// already holds: the staged copy while the file waits, and the retained
/// source once processing has promoted it. The review shows the image at once,
/// independent of processing and of custody (operator, 28 October 2026, R463).
/// Every read re-verifies the recorded SHA-256, and only an image is answered.
/// </summary>
public sealed class ReadUploadedImage(
    IQueuedIntakeStatusQueries statuses,
    IIntakeReceiptQueries receipts,
    IIntakeArtifactStore artifacts)
{
    /// <returns>The image, or null when the upload or its bytes are not held, or it is not an image.</returns>
    public async Task<IntakeSourceDownload?> ExecuteAsync(
        Guid stagedReceiptId,
        ActionActor actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        if (stagedReceiptId == Guid.Empty)
        {
            return null;
        }

        var staged = await statuses.GetStagedReceiptAsync(stagedReceiptId, cancellationToken);
        if (staged is null || !InstructionEvidenceImages.IsImage(staged.MediaType))
        {
            return null;
        }

        // Once processed, the staged copy is deleted after the retained source
        // is verified, so the retained source is read instead.
        var (storageKey, sha256) = (staged.StorageKey, staged.SourceHash);
        var status = await statuses.GetAsync(stagedReceiptId, cancellationToken);
        if (status?.ProcessedReceiptId is { } processedReceiptId
            && await receipts.GetAsync(processedReceiptId, cancellationToken) is { } receipt
            && IntakeFileIdentity.SourceAsset(receipt) is { } source)
        {
            (storageKey, sha256) = (source.StorageKey, source.ContentHash);
        }

        if (await artifacts.ReadAsync(storageKey, cancellationToken) is not { } content)
        {
            return null;
        }

        var actualHash = Convert.ToHexString(SHA256.HashData(content.Span));
        if (!DownloadIntakeSource.FixedTimeHashEquals(actualHash, sha256))
        {
            throw new IntakeArtifactIntegrityException();
        }

        return new(
            content,
            DownloadIntakeSource.SafeFileName(staged.SourceFileName),
            staged.MediaType,
            content.Length,
            actualHash);
    }
}
