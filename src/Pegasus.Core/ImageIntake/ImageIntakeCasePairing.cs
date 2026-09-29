using System.Diagnostics;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Custody;

namespace Pegasus.Core.ImageIntake;

/// <summary>The reasons automatic association leaves a registered image record to staff.</summary>
public enum ImageIntakeAutomationWithheld
{
    /// <summary>A known Principal has no eligible Case with this registration.</summary>
    PrincipalDisagrees,

    /// <summary>More than one eligible Case, or candidate, fits the registration.</summary>
    RegistrationAmbiguous
}

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

    /// <summary>
    /// A known intake Principal is a hard scope on the candidate Cases, the
    /// way an established Principal is on the mail path
    /// ([FRD-09](../../../docs/frd/frd-09-provider-and-intermediary-routes.md)):
    /// only that Principal's Cases are candidates, and they are restricted
    /// BEFORE any uniqueness count. An unknown Principal leaves the full set.
    /// </summary>
    public static IReadOnlyList<ImageIntakeCaseCandidate> ScopeToPrincipal(
        IReadOnlyList<ImageIntakeCaseCandidate> candidates,
        Guid? principalId)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        return principalId is null
            ? candidates
            : candidates.Where(candidate => candidate.PrincipalId == principalId).ToArray();
    }

    public static ImageIntakeCaseCandidate? SelectRegisteredTarget(
        IReadOnlyList<ImageIntakeCaseCandidate> candidates,
        string registration,
        Guid? principalId,
        int groupExpectedMemberCount,
        IntakeSourceChannel sourceChannel,
        DateTimeOffset registeredAtUtc)
    {
        var scoped = ScopeToPrincipal(candidates, principalId);
        // A registered identity is immutable: no near-miss completion here.
        // Preserve single-image exact precedence and the group's stricter
        // complete-candidate-count rule from its original routing decision.
        var exact = scoped.Where(candidate => candidate.ConfirmedRegistration == registration).ToArray();
        return exact.Length == 1
            && (groupExpectedMemberCount <= 1 || scoped.Count == 1)
            // Manual upload (operator, 28 September 2026): a Case that already
            // existed when the images registered stays the staff decision the
            // upload offered; only a Case created afterwards pairs by itself.
            && (sourceChannel != IntakeSourceChannel.ManualUpload || exact[0].CreatedAtUtc > registeredAtUtc)
                ? exact[0]
                : null;
    }

    /// <summary>
    /// Why automatic association is withheld for a registered image record,
    /// from the same scope and counts <see cref="SelectRegisteredTarget"/>
    /// applies; null when there is nothing to explain (a target is selected,
    /// no Case carries the registration, or manual upload leaves it to staff).
    /// </summary>
    public static ImageIntakeAutomationWithheld? ExplainWithheld(
        IReadOnlyList<ImageIntakeCaseCandidate> candidates,
        string registration,
        Guid? principalId,
        int groupExpectedMemberCount)
    {
        var scoped = ScopeToPrincipal(candidates, principalId);
        var exact = scoped.Count(candidate => candidate.ConfirmedRegistration == registration);
        if (exact == 0)
        {
            return principalId is not null
                && candidates.Any(candidate => candidate.ConfirmedRegistration == registration)
                    ? ImageIntakeAutomationWithheld.PrincipalDisagrees
                    : null;
        }

        return exact > 1 || (groupExpectedMemberCount > 1 && scoped.Count != 1)
            ? ImageIntakeAutomationWithheld.RegistrationAmbiguous
            : null;
    }

    /// <summary>
    /// A manual group whose decision staff have started (any member carries a
    /// staff association decision, current or reversed) keeps their
    /// per-member links: it merges only once every member is linked and gains
    /// no automatic sibling link.
    /// </summary>
    public static bool AwaitsStaffCompletion(
        IntakeSourceChannel sourceChannel,
        bool staffStarted,
        bool everyMemberLinked) =>
        sourceChannel == IntakeSourceChannel.ManualUpload && staffStarted && !everyMemberLinked;

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
            var channel = originReceipt.SourceIdentity.Channel;
            if (channel == IntakeSourceChannel.ManualUpload)
            {
                // A manual group confirmation owns one explicit, reasoned
                // link per member.  Do not turn the first member link into
                // automatic sibling links: that would replace the page's
                // submitted operation identities and versions halfway through
                // its one staff decision.  Once every member is linked, the
                // existing pairing/merge path below remains the sole owner of
                // the Image-initiated Case lifecycle.  An untouched manual
                // group pairs only with a Case created after it registered.
                var manualImages = await imageIntakeStore.ListImagesAsync(detail.Record.Id, cancellationToken);
                var manualMemberIds = manualImages.Select(image => image.ReceiptId)
                    .Prepend(detail.Record.Origin.ReceiptId)
                    .Distinct()
                    .ToArray();
                var manualMembers = await Task.WhenAll(manualMemberIds.Select(
                    memberId => receiptQueries.GetAsync(memberId, cancellationToken)));
                if (AwaitsStaffCompletion(channel,
                        manualMembers.Any(member => member?.ManualAssociationActorKind == ActorKind.Staff),
                        manualMembers.All(member => member?.CurrentCaseId is not null)))
                {
                    return new(1, 0, 0);
                }
            }
            var staffOriginVersion = originReceipt.AssociationWasStaffDecision
                ? originReceipt.ManualAssociationVersion : null;
            var targetId = detail.AssociatedCaseId;
            if (targetId is null)
            {
                var eligible = await caseCandidates.FindEligibleByRegistrationAsync(
                    detail.Record.NormalizedVehicleRegistration, cancellationToken);
                var target = SelectRegisteredTarget(eligible, detail.Record.NormalizedVehicleRegistration,
                    detail.Record.PrincipalId, detail.GroupExpectedMemberCount, channel, detail.RegisteredAtUtc);
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
                        detail.Record.PrincipalId, detail.GroupExpectedMemberCount, channel, detail.RegisteredAtUtc);
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
