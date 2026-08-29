using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Core.Triage;

namespace Pegasus.Core.Intake;

public sealed record ReconcileUnidentifiedDestinationsResult(
    int Candidates,
    int Resolved,
    int Corrected,
    int Failures);

/// <summary>
/// The one owner of INTK-007's supersession rule: an open Unidentified item
/// whose origin receipt has since reached a real destination (a formal Case,
/// a registered Image intake, or an opened Triage) is resolved to that
/// destination, which the
/// resolution history records permanently. <see cref="SynchronizeForReceiptAsync"/>
/// runs inside the receipt's own processing/replay pass
/// (<see cref="ProcessQueuedIntake"/>); <see cref="ExecuteAsync"/> is the
/// reconciliation sweep for receipts promoted OUTSIDE their own pass — a
/// sibling group member's registration, a staff action, or a historic stale
/// open row — which no processing pass would ever revisit on its own. It also
/// reopens and re-targets an automation resolution when a later manual case
/// association changes the receipt's effective destination.
/// </summary>
public sealed class ReconcileUnidentifiedDestinations(
    IUnidentifiedStore unidentifiedStore,
    IResolveUnidentified resolveUnidentified,
    IIntakeReceiptQueries receiptQueries,
    IImageIntakeQueries imageIntakeQueries,
    ITriageQueries triageQueries,
    TimeProvider timeProvider)
{
    private static readonly ActionActor ReconciliationActor =
        ActionActor.Automation("intake-processing");

    public async Task<ReconcileUnidentifiedDestinationsResult> ExecuteAsync(
        int maximumItems,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);

        var open = await unidentifiedStore.ListAsync(UnidentifiedState.Open, cancellationToken);
        var candidates = 0;
        var resolved = 0;
        var corrected = 0;
        var failures = 0;
        foreach (var item in open)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (item.Origin.Kind != UnidentifiedOriginKind.Receipt)
            {
                // Nothing registers a group-origin item today (verified: the
                // only reader of that origin shape is the upload confirmation
                // surface); whatever eventually writes one owns its
                // resolution shape too.
                continue;
            }

            if (candidates >= maximumItems)
            {
                break;
            }

            candidates++;
            try
            {
                var receipt = await receiptQueries.GetAsync(item.Origin.Id, cancellationToken);
                if (receipt is not null
                    && await SynchronizeForReceiptAsync(receipt, cancellationToken))
                {
                    resolved++;
                }
            }
            catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
            {
                failures++;
            }
        }

        var recheck = await unidentifiedStore.ListResolutionsToRecheckAsync(maximumItems, cancellationToken);
        foreach (var item in recheck)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var receipt = await receiptQueries.GetAsync(item.Origin.Id, cancellationToken);
                if (receipt is not null
                    && await SynchronizeForReceiptAsync(receipt, cancellationToken))
                {
                    corrected++;
                }
            }
            catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
            {
                failures++;
            }
        }

        return new(candidates, resolved, corrected, failures);
    }

    /// <summary>
    /// Synchronizes the receipt's Unidentified item to its effective
    /// destination. An open item is resolved only when it has a real
    /// destination; an automation resolution is reopened and optionally
    /// re-targeted when a later manual case association changes it. A receipt
    /// that is still legitimately unidentified is never force-closed. Failures
    /// propagate — callers decide whether the write is advisory.
    /// </summary>
    public async Task<bool> SynchronizeForReceiptAsync(
        IntakeReceipt receipt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        // Every receipt reaching this method now performs the indexed origin
        // lookup: a resolution can need correcting even while the receipt is
        // still Unidentified-eligible, so the former in-memory short-circuit
        // cannot stay in front of it.
        var existing = await unidentifiedStore.GetByOriginAsync(
            UnidentifiedOrigin.Receipt(receipt.Id), cancellationToken);
        if (existing is null)
        {
            return false;
        }

        if (existing.State == UnidentifiedState.Open)
        {
            if (receipt.CurrentCaseId is null
                && ProcessIntake.IsUnidentifiedEligible(receipt))
            {
                return false;
            }

            var destination = await DestinationForAsync(receipt, cancellationToken);
            if (destination is null)
            {
                return false;
            }

            await ResolveAsync(existing, receipt, destination, cancellationToken);
            return true;
        }

        if (existing.State != UnidentifiedState.Resolved
            || existing.ResolvedBy is not { Kind: ActorKind.Automation } actor
            || actor.SubjectId != ReconciliationActor.SubjectId)
        {
            return false;
        }

        var revisedDestination = await DestinationForAsync(receipt, cancellationToken);
        if (revisedDestination is not null
            && existing.ResolutionTargetKind == revisedDestination.Kind
            && existing.ResolutionTargetId == revisedDestination.Id
            && existing.ResolutionTargetReference == revisedDestination.Reference)
        {
            return false;
        }

        var reopenReason = revisedDestination is null
            ? "The receipt destination was withdrawn; reopen the Unidentified item."
            : "The receipt destination changed; re-target the Unidentified item.";
        var reopened = await unidentifiedStore.ReopenAsync(
            new(
                existing.Id,
                existing.Version,
                ReconciliationActor,
                $"intake-unidentified-reopen:{receipt.Id:N}:{receipt.Version}",
                reopenReason,
                timeProvider.GetUtcNow()),
            cancellationToken);
        if (revisedDestination is null)
        {
            return true;
        }

        await ResolveAsync(reopened.Item, receipt, revisedDestination, cancellationToken);
        return true;
    }

    private async Task<UnidentifiedDestination?> DestinationForAsync(
        IntakeReceipt receipt,
        CancellationToken cancellationToken)
    {
        // Preserve established Image intake and Triage precedence. The trailing
        // manual-link branch serves only receipts that previously had no route.
        if (receipt.Decision == IntakeDecision.CaseCreated
            && receipt.CurrentCaseId is { } caseId)
        {
            return new(
                UnidentifiedResolutionTargetKind.InstructionCase,
                caseId.ToString("N"),
                receipt.CurrentCaseReference);
        }
        if (receipt.Decision == IntakeDecision.ImageIntakeRegistered)
        {
            var detail = await imageIntakeQueries.GetByOriginReceiptAsync(receipt.Id, cancellationToken);
            return detail is null
                ? null
                : new(
                    UnidentifiedResolutionTargetKind.ImageIntake,
                    detail.Record.Id.ToString("N"),
                    detail.Record.ImageIntakeReference);
        }
        if (ProcessIntake.IsTriageRequest(receipt)
            && await triageQueries.GetByOriginReceiptAsync(receipt.Id, cancellationToken) is { } triage)
        {
            return new(
                UnidentifiedResolutionTargetKind.Triage,
                triage.Id.ToString("N"),
                triage.NormalizedVehicleRegistration);
        }
        if (receipt.CurrentCaseId is { } linkedCaseId)
        {
            return new(
                UnidentifiedResolutionTargetKind.InstructionCase,
                linkedCaseId.ToString("N"),
                receipt.CurrentCaseReference);
        }

        return null;
    }

    private Task<UnidentifiedResolveResult> ResolveAsync(
        UnidentifiedItem item,
        IntakeReceipt receipt,
        UnidentifiedDestination destination,
        CancellationToken cancellationToken) =>
        resolveUnidentified.ExecuteAsync(
            new(
                item.Id,
                item.Version,
                ReconciliationActor,
                $"intake-unidentified-reconcile:{receipt.Id:N}:{receipt.Version}",
                $"The receipt now has a {destination.Kind} destination; the Unidentified item is superseded.",
                destination.Kind,
                destination.Id,
                destination.Reference,
                timeProvider.GetUtcNow()),
            cancellationToken);

    private sealed record UnidentifiedDestination(
        UnidentifiedResolutionTargetKind Kind,
        string Id,
        string? Reference);
}
