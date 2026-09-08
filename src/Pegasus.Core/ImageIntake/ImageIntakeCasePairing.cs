using System.Diagnostics;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Custody;

namespace Pegasus.Core.ImageIntake;

public sealed record ImageIntakePairingResult(
    int Candidates,
    int Merged,
    int Failures,
    string? FirstFailure = null);

/// <summary>
/// One owner for both arrival orders, registered-receipt replay and the existing
/// reconciliation timer. Registration and association remain durable if a later
/// step fails; the next pass retries only the unfinished work.
/// </summary>
public interface IImageIntakeCasePairing
{
    Task<ImageIntakePairingResult> PairAcceptedCaseAsync(Guid caseId, CancellationToken cancellationToken);

    Task<ImageIntakePairingResult> PairRegisteredReceiptAsync(Guid receiptId, CancellationToken cancellationToken);

    Task<ImageIntakePairingResult> ReconcileAsync(int maximumItems, CancellationToken cancellationToken);

}

public sealed class ImageIntakeCasePairing(
    IImageIntakeStore imageIntakeStore,
    IImageIntakeCaseCandidates caseCandidates,
    IIntakeMutationStore intakeMutationStore,
    TimeProvider timeProvider,
    ICommittedExternalWorkPublisher committedExternalWorkPublisher,
    IIntakeReceiptQueries receiptQueries) : IImageIntakeCasePairing
{
    private static readonly ActivitySource Telemetry = new("Pegasus.Core.ImageIntake");

    public static ImageIntakeCaseCandidate? SelectRegisteredTarget(
        IReadOnlyList<ImageIntakeCaseCandidate> candidates,
        string registration,
        Guid? principalId,
        int groupExpectedMemberCount)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        // A registered identity is immutable: no near-miss completion here.
        // Preserve single-image exact precedence and the group's stricter
        // complete-candidate-count rule from its original routing decision.
        var exact = candidates.Where(candidate => candidate.ConfirmedRegistration == registration).ToArray();
        return exact.Length == 1
            && (groupExpectedMemberCount <= 1 || candidates.Count == 1)
            && (principalId is null || exact[0].PrincipalId == principalId)
                ? exact[0]
                : null;
    }

    public Task<ImageIntakePairingResult> PairAcceptedCaseAsync(Guid caseId, CancellationToken cancellationToken) =>
        caseId == Guid.Empty
            ? Task.FromResult(new ImageIntakePairingResult(0, 0, 0))
            : PairPendingAsync(50, caseId, cancellationToken);

    public Task<ImageIntakePairingResult> ReconcileAsync(int maximumItems, CancellationToken cancellationToken) =>
        PairPendingAsync(maximumItems, null, cancellationToken);

    private async Task<ImageIntakePairingResult> PairPendingAsync(
        int maximumItems,
        Guid? caseId,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        var pending = await imageIntakeStore.ListPendingPairingAsync(maximumItems, caseId, cancellationToken);
        var merged = 0;
        var failures = 0;
        string? firstFailure = null;
        foreach (var intake in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await PairRegisteredReceiptAsync(intake.OriginReceiptId, cancellationToken);
            merged += result.Merged;
            failures += result.Failures;
            firstFailure ??= result.FirstFailure;
        }

        return new(pending.Count, merged, failures, firstFailure);
    }

    public async Task<ImageIntakePairingResult> PairRegisteredReceiptAsync(
        Guid receiptId,
        CancellationToken cancellationToken)
    {
        using var activity = Telemetry.StartActivity("image_intake_pairing");
        activity?.SetTag("intake.receipt_id", receiptId);
        try
        {
            var detail = await imageIntakeStore.GetByOriginReceiptAsync(receiptId, cancellationToken);
            if (detail is null || detail.State != ImageInitiatedCaseState.AwaitingInstruction)
            {
                return new(0, 0, 0);
            }

            var actor = ActionActor.SystemWorker(ImageIntakeAutomation.ActorId);
            var originReceipt = await receiptQueries.GetAsync(detail.Record.Origin.ReceiptId, cancellationToken)
                ?? throw new KeyNotFoundException("The registered image origin is unavailable.");
            var staffOriginVersion = originReceipt.AssociationWasStaffDecision
                ? originReceipt.ManualAssociationVersion : null;
            var targetId = detail.AssociatedCaseId;
            if (targetId is null)
            {
                var eligible = await caseCandidates.FindEligibleByRegistrationAsync(
                    detail.Record.NormalizedVehicleRegistration, cancellationToken);
                var target = SelectRegisteredTarget(eligible, detail.Record.NormalizedVehicleRegistration,
                    detail.Record.PrincipalId, detail.GroupExpectedMemberCount);
                if (target is null)
                {
                    return new(1, 0, 0);
                }

                targetId = target.CaseId;
            }

            var images = await imageIntakeStore.ListImagesAsync(detail.Record.Id, cancellationToken);
            var receiptIds = images.Select(image => image.ReceiptId)
                .Prepend(detail.Record.Origin.ReceiptId).Distinct();
            foreach (var memberId in receiptIds)
            {
                var member = await receiptQueries.GetAsync(memberId, cancellationToken)
                    ?? throw new KeyNotFoundException("The registered image receipt is unavailable.");
                if (member.CurrentCaseId == targetId)
                {
                    continue;
                }
                if (member.CurrentCaseId is not null || member.ManualAssociationVersion is not null)
                {
                    throw new IntakeAssociationConflictException("A group member has a different association decision.");
                }

                // Each prior member link advances the Case version. Refresh it,
                // and let the store recheck current uniqueness inside its write.
                long caseVersion;
                string reason;
                if (staffOriginVersion is not null)
                {
                    var currentGroup = await imageIntakeStore.GetByOriginReceiptAsync(
                        detail.Record.Origin.ReceiptId, cancellationToken);
                    if (currentGroup is null || currentGroup.AssociatedCaseId != targetId
                        || currentGroup.AssociatedCaseVersion is null)
                    {
                        throw new IntakeAssociationConflictException("The current staff group destination changed.");
                    }
                    caseVersion = currentGroup.AssociatedCaseVersion.Value;
                    reason = "Complete the current reasoned staff group association.";
                }
                else
                {
                    var eligible = await caseCandidates.FindEligibleByRegistrationAsync(
                        detail.Record.NormalizedVehicleRegistration, cancellationToken);
                    var target = SelectRegisteredTarget(eligible, detail.Record.NormalizedVehicleRegistration,
                        detail.Record.PrincipalId, detail.GroupExpectedMemberCount);
                    if (target is null || target.CaseId != targetId)
                    {
                        throw new IntakeAssociationConflictException("The current image destination is no longer unique.");
                    }
                    caseVersion = target.CaseVersion;
                    reason = $"Automatic association: the confirmed registration matches case {target.CaseReference} unambiguously.";
                }
                await intakeMutationStore.AutoLinkAsync(
                    new(memberId, targetId.Value, caseVersion, actor,
                        $"image-intake-associate:{memberId:N}", reason, staffOriginVersion),
                    timeProvider.GetUtcNow(), cancellationToken);
            }

            await SyncMergeAfterLinkAsync(detail.Record.Origin.ReceiptId, targetId.Value, actor,
                staffOriginVersion, cancellationToken);
            var current = await imageIntakeStore.GetByOriginReceiptAsync(detail.Record.Origin.ReceiptId, cancellationToken);
            var merged = current is not null && current.State == ImageInitiatedCaseState.MergedIntoInstructionCase
                && current.MergedIntoCaseId == targetId;
            if (!merged)
            {
                return new(1, 0, 0);
            }
            activity?.SetTag("image_intake.pairing", "merged");
            return new(1, 1, 0);
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            var failure = exception.GetType().Name;
            activity?.SetTag("image_intake.failure_type", failure);
            activity?.SetStatus(ActivityStatusCode.Error, "pairing_failed");
            return new(1, 0, 1, failure);
        }
    }

    private async Task SyncMergeAfterLinkAsync(
        Guid originReceiptId,
        Guid caseId,
        ActionActor actor,
        long? staffOriginVersion,
        CancellationToken cancellationToken)
    {
        if (originReceiptId == Guid.Empty || caseId == Guid.Empty)
        {
            return;
        }

        var detail = await imageIntakeStore.GetByOriginReceiptAsync(originReceiptId, cancellationToken);
        if (detail is null || detail.State != ImageInitiatedCaseState.AwaitingInstruction)
        {
            return;
        }

        var originId = detail.Record.Origin.ReceiptId;
        var merged = await imageIntakeStore.MergeAsync(
            new(detail.Record.Id, caseId, actor,
                $"image-intake-merge:{originId:N}",
                $"The Image-initiated case {detail.Record.ImageIntakeReference} was merged into the linked formal Case.",
                detail.LifecycleVersion, staffOriginVersion), cancellationToken);
        if (merged.PendingExternalWorkId is { } workItemId)
        {
            await committedExternalWorkPublisher.PublishAsync(workItemId, cancellationToken);
        }
    }
}
