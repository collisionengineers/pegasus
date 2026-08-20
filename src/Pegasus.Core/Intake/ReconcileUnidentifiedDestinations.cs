using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake.Unidentified;

namespace Pegasus.Core.Intake;

public sealed record ReconcileUnidentifiedDestinationsResult(
    int Candidates,
    int Resolved,
    int Failures);

/// <summary>
/// The one owner of INTK-007's supersession rule: an open Unidentified item
/// whose origin receipt has since reached a real destination (a formal Case,
/// or a registered Image intake) is resolved to that destination, which the
/// resolution history records permanently. <see cref="ResolveForReceiptAsync"/>
/// runs inside the receipt's own processing/replay pass
/// (<see cref="ProcessQueuedIntake"/>); <see cref="ExecuteAsync"/> is the
/// reconciliation sweep for receipts promoted OUTSIDE their own pass — a
/// sibling group member's registration, a staff action, or a historic stale
/// open row — which no processing pass would ever revisit on its own.
/// </summary>
public sealed class ReconcileUnidentifiedDestinations(
    IUnidentifiedStore unidentifiedStore,
    IResolveUnidentified resolveUnidentified,
    IIntakeReceiptQueries receiptQueries,
    IImageIntakeQueries imageIntakeQueries,
    TimeProvider timeProvider)
{
    public async Task<ReconcileUnidentifiedDestinationsResult> ExecuteAsync(
        int maximumItems,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);

        var open = await unidentifiedStore.ListAsync(UnidentifiedState.Open, cancellationToken);
        var candidates = 0;
        var resolved = 0;
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
                    && await ResolveForReceiptAsync(receipt, cancellationToken))
                {
                    resolved++;
                }
            }
            catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
            {
                failures++;
            }
        }

        return new(candidates, resolved, failures);
    }

    /// <summary>
    /// Resolves the receipt's open Unidentified item when the receipt now has
    /// a real destination; returns whether a resolution was written. A
    /// receipt that is still legitimately unidentified
    /// (<see cref="ProcessIntake.IsUnidentifiedEligible"/>) is never
    /// force-closed, and a receipt with no open item is a no-op. Failures
    /// propagate — callers decide whether the write is advisory.
    /// </summary>
    public async Task<bool> ResolveForReceiptAsync(
        IntakeReceipt receipt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        if (ProcessIntake.IsUnidentifiedEligible(receipt))
        {
            return false;
        }

        UnidentifiedResolutionTargetKind targetKind;
        string targetId;
        string? targetReference;
        if (receipt.Decision == IntakeDecision.CaseCreated && receipt.CurrentCaseId is { } caseId)
        {
            targetKind = UnidentifiedResolutionTargetKind.InstructionCase;
            targetId = caseId.ToString("N");
            targetReference = receipt.AcceptedCaseReference ?? receipt.ManualLinkedCaseReference;
        }
        else if (receipt.Decision == IntakeDecision.ImageIntakeRegistered)
        {
            var detail = await imageIntakeQueries.GetByOriginReceiptAsync(receipt.Id, cancellationToken);
            if (detail is null)
            {
                return false;
            }

            targetKind = UnidentifiedResolutionTargetKind.ImageIntake;
            targetId = detail.Record.Id.ToString("N");
            targetReference = detail.Record.ImageIntakeReference;
        }
        else
        {
            return false;
        }

        var existing = await unidentifiedStore.GetByOriginAsync(
            UnidentifiedOrigin.Receipt(receipt.Id), cancellationToken);
        if (existing is not { State: UnidentifiedState.Open })
        {
            return false;
        }

        await resolveUnidentified.ExecuteAsync(
            new(
                existing.Id,
                existing.Version,
                // UnidentifiedValidation.ValidateResolve requires Staff or
                // Automation (unlike registration, which also accepts
                // SystemWorker); this automatic reconciliation is authorised
                // automation, not registration.
                ActionActor.Automation("intake-processing"),
                $"intake-unidentified-reconcile:{receipt.Id:N}:{receipt.Version}",
                $"The receipt now has a {targetKind} destination; the Unidentified item is superseded.",
                targetKind,
                targetId,
                targetReference,
                timeProvider.GetUtcNow()),
            cancellationToken);
        return true;
    }
}
