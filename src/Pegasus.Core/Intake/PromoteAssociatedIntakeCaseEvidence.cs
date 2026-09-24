using System.Security.Cryptography;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Intake;

/// <summary>
/// Files the retained evidence from a system-associated receipt on its already
/// matched Case: its source email, its documents and its selected photographs.
/// A receipt filed here is not held; holding custody and Case custody use
/// different operation identities, and confirmation in the holding area is
/// not filing.
/// </summary>
public sealed class PromoteAssociatedIntakeCaseEvidence(
    IIntakeArtifactStore artifactStore,
    ICaseArtifactCustody custody,
    IAutomaticCaseEvidencePromotionStore promotionStore)
{
    private static readonly ActionActor SystemWorkerActor =
        ActionActor.SystemWorker("intake-processing");

    public async Task<AutomaticCaseEvidencePromotionOutcome> ExecuteAsync(
        IntakeReceipt receipt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        if (receipt.CurrentCaseId is not { } caseId)
        {
            return AutomaticCaseEvidencePromotionOutcome.NotApplicable;
        }

        // A matched follow-up is filed whether or not it carries photographs:
        // its email and documents belong on the Case too (operator, 23 September 2026).
        // A receipt that retained nothing has nothing to file.
        if (receipt.AssetRecords.Count == 0)
        {
            return AutomaticCaseEvidencePromotionOutcome.NotApplicable;
        }
        var selectedPhotographIds = InstructionEvidenceImages.Select(receipt.AssetRecords)
            .Select(asset => asset.Id)
            .ToHashSet();
        var assets = SelectAssets(receipt, selectedPhotographIds);
        if (assets.Length == 0)
        {
            return AutomaticCaseEvidencePromotionOutcome.NotApplicable;
        }

        var preparation = await promotionStore.PrepareAsync(
            new(
                receipt.Id,
                caseId,
                assets.Select(asset => asset.Id).ToArray(),
                selectedPhotographIds.Count != 0),
            cancellationToken);
        if (preparation.Disposition != AutomaticCaseEvidencePromotionPreparationDisposition.Ready)
        {
            return preparation.Disposition == AutomaticCaseEvidencePromotionPreparationDisposition.Deferred
                ? AutomaticCaseEvidencePromotionOutcome.Deferred
                : AutomaticCaseEvidencePromotionOutcome.NotApplicable;
        }

        var pending = false;
        var failed = false;
        foreach (var asset in assets)
        {
            var content = await artifactStore.ReadAsync(asset.StorageKey, cancellationToken)
                ?? throw new FileNotFoundException($"The retained intake asset '{asset.Id}' is unavailable.");
            Verify(asset, content.Span);
            await using var stream = new MemoryStream(content.ToArray(), writable: false);
            var retained = await custody.RetainAsync(
                new(
                    SystemWorkerActor,
                    preparation.CaseId,
                    receipt.Id,
                    asset.Id.ToString("N"),
                    AutomaticCaseEvidencePromotionOperationKey.For(
                        preparation.CaseId, receipt.Id, asset.Id),
                    asset.FileName,
                    asset.MediaType,
                    asset.ContentLength,
                    asset.ContentHash,
                    stream,
                    RoleFor(asset),
                    DocumentSource.Intake,
                    preparation.ExpectedCaseVersion,
                    IsAutomaticIntakeEvidencePromotion: true),
                cancellationToken);
            pending |= retained.Disposition is CaseArtifactCustodyDisposition.Pending
                or CaseArtifactCustodyDisposition.Unknown;
            failed |= retained.Disposition == CaseArtifactCustodyDisposition.Failed;
        }

        return failed ? AutomaticCaseEvidencePromotionOutcome.Failed
            : pending ? AutomaticCaseEvidencePromotionOutcome.Pending
            : AutomaticCaseEvidencePromotionOutcome.Confirmed;
    }

    private static IntakeAssetRecord[] SelectAssets(
        IntakeReceipt receipt,
        HashSet<Guid> selectedPhotographs)
    {
        if (IntakeFileIdentity.SourceAsset(receipt) is null)
        {
            throw new IntakeArtifactIntegrityException();
        }
        return IntakeFileIdentity.Ordered(receipt)
            .Where(asset => asset.Kind == IntakeAssetKind.Source
                && asset.Disposition == IntakeAssetDisposition.Source
                || asset.Kind == IntakeAssetKind.Attachment
                && asset.Disposition == IntakeAssetDisposition.Attachment
                && !InstructionEvidenceImages.IsImage(asset.MediaType)
                || selectedPhotographs.Contains(asset.Id))
            .Where(asset => asset.ContentLength > 0
                && asset.ContentHash.Length == 64
                && !string.IsNullOrWhiteSpace(asset.StorageKey)
                && !string.IsNullOrWhiteSpace(asset.FileName)
                && !string.IsNullOrWhiteSpace(asset.MediaType))
            .DistinctBy(asset => asset.Id)
            .ToArray();
    }

    private static DocumentSemanticRole RoleFor(IntakeAssetRecord asset) =>
        asset.Kind == IntakeAssetKind.Source ? DocumentSemanticRole.OriginalSource
        : InstructionEvidenceImages.IsImage(asset.MediaType) ? DocumentSemanticRole.Image
        : DocumentSemanticRole.Correspondence;

    private static void Verify(IntakeAssetRecord asset, ReadOnlySpan<byte> content)
    {
        if (content.Length != asset.ContentLength
            || !string.Equals(
                Convert.ToHexString(SHA256.HashData(content)), asset.ContentHash,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new IntakeArtifactIntegrityException();
        }
    }
}

public enum AutomaticCaseEvidencePromotionOutcome
{
    NotApplicable,
    Deferred,
    Pending,
    Failed,
    Confirmed
}

public enum AutomaticCaseEvidencePromotionPreparationDisposition
{
    NotApplicable,
    Deferred,
    Ready
}

public sealed record AutomaticCaseEvidencePromotionPreparation(
    AutomaticCaseEvidencePromotionPreparationDisposition Disposition,
    Guid CaseId = default,
    long ExpectedCaseVersion = 0);

public sealed record AutomaticCaseEvidencePromotionRequest(
    Guid IntakeReceiptId,
    Guid CaseId,
    IReadOnlyList<Guid> AssetIds,
    bool HasSelectedPhotographs);

public sealed record AutomaticCaseEvidencePromotionPlan(
    IReadOnlyList<Guid> AssetIds,
    bool HasSelectedPhotographs);

/// <summary>Durably establishes one Case-filing plan before content is handed to custody.</summary>
public interface IAutomaticCaseEvidencePromotionStore
{
    Task<AutomaticCaseEvidencePromotionPreparation> PrepareAsync(
        AutomaticCaseEvidencePromotionRequest request,
        CancellationToken cancellationToken);
}

public static class AutomaticCaseEvidencePromotionOperationKey
{
    public static string For(Guid caseId, Guid receiptId, Guid assetId) =>
        $"case-intake:{caseId:N}:{receiptId:N}:{assetId:N}";

    public static string Plan(Guid receiptId) => $"case-intake-promotion:{receiptId:N}";
}
