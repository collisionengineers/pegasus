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
/// open row — which no processing pass would ever revisit on its own.
///
/// The rule runs in both directions: a receipt that acquires a destination has
/// its open item resolved to it, and a resolution this reconciliation itself
/// wrote is reopened — and re-targeted when a destination remains — once a
/// later manual case association changes the receipt's effective destination.
/// A resolution written by anyone else is authoritative and is never revisited.
/// </summary>
public sealed class ReconcileUnidentifiedDestinations(
    IUnidentifiedStore unidentifiedStore,
    IResolveUnidentified resolveUnidentified,
    IIntakeReceiptQueries receiptQueries,
    IImageIntakeQueries imageIntakeQueries,
    ITriageQueries triageQueries,
    TimeProvider timeProvider)
{
    /// <summary>
    /// The automation identity every resolution written here carries. Public
    /// because <see cref="IUnidentifiedStore.ListResolutionsToRecheckAsync"/>
    /// must select exactly the resolutions this owner wrote — a staff
    /// resolution is authoritative and is never re-derived — so the identity
    /// is written once, here, rather than repeated as a literal in persistence.
    /// </summary>
    public const string AutomationActorId = "intake-processing";

    private static readonly ActionActor ReconciliationActor =
        ActionActor.Automation(AutomationActorId);

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

        // The inverse direction: resolutions this reconciliation wrote whose
        // manual case association has moved since. Listed after the open loop
        // so a resolution written this pass is examined once here and leaves
        // the queue in the same pass. Bounded like the open loop, and one
        // failure never stops the sweep. Every recheck row counts as a
        // candidate, so "examined nothing this pass" is observable as
        // Candidates = 0 and a queue that never advances shows as a non-zero
        // candidate count pass after pass.
        var recheck = await unidentifiedStore.ListResolutionsToRecheckAsync(maximumItems, cancellationToken);
        foreach (var item in recheck)
        {
            cancellationToken.ThrowIfCancellationRequested();
            candidates++;
            try
            {
                if (!IsOwnResolution(item))
                {
                    // The query selects only this reconciliation's own
                    // resolutions; anything else handed back gets no write.
                    continue;
                }

                var receipt = await receiptQueries.GetAsync(item.Origin.Id, cancellationToken);
                if (receipt is null)
                {
                    continue;
                }

                if (await SynchronizeForReceiptAsync(receipt, cancellationToken))
                {
                    corrected++;
                }

                // A recheck that leaves the destination alone writes nothing
                // else, so recording the association version it was examined
                // against is what completes it. Without that the row would be
                // re-selected every pass and, being among the oldest, crowd
                // every later stale resolution out of the bounded page
                // (INTK-048). The version recorded is the one this pass read,
                // so an association that moves mid-pass is picked up next time
                // rather than marked reconciled unseen.
                if (receipt.ManualAssociationVersion is { } associationVersion)
                {
                    await unidentifiedStore.MarkResolutionRecheckedAsync(
                        item.Id, associationVersion, cancellationToken);
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
    /// Synchronizes the receipt's Unidentified item, if it has one, with the
    /// receipt's effective destination; returns whether a transition was
    /// written. An open item is resolved when the receipt now has a real
    /// destination — including a receipt still carrying an unidentified-eligible
    /// decision that a member of staff has manually linked to a Case, which is
    /// the whole of INTK-048. A resolved item is reopened when the destination
    /// this reconciliation recorded has been withdrawn, and reopened then
    /// re-resolved when it has changed — only for a resolution this
    /// reconciliation itself wrote; an item resolved by staff or any other
    /// actor is left alone. A receipt that is still legitimately unidentified
    /// and has no case association is never force-closed, and a receipt with no
    /// item is a no-op. Failures propagate — callers decide whether the write
    /// is advisory.
    /// </summary>
    public async Task<bool> SynchronizeForReceiptAsync(
        IntakeReceipt receipt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        // Every receipt reaching this method now performs the indexed origin
        // lookup: a resolution can need correcting even while the receipt is
        // still Unidentified-eligible, so the former in-memory short-circuit
        // cannot stay in front of it. The eligibility test survives inside the
        // Open branch, re-gated on the receipt having no case association.
        var existing = await unidentifiedStore.GetByOriginAsync(
            UnidentifiedOrigin.Receipt(receipt.Id), cancellationToken);
        if (existing is null)
        {
            return false;
        }

        if (existing.State == UnidentifiedState.Open)
        {
            // A manual link does not rewrite the immutable processing decision,
            // so a manually linked receipt stays `NeedsSorting` and would be
            // turned away by the eligibility test alone. Only a receipt that is
            // both eligible AND unassociated is legitimately still unidentified.
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

            await ResolveAsync(existing, destination, cancellationToken);
            return true;
        }

        if (!IsOwnResolution(existing))
        {
            // A staff resolution, or any other actor's, is a decision this
            // reconciliation has no authority to revisit.
            return false;
        }

        var effective = await DestinationForAsync(receipt, cancellationToken);
        if (effective is not null && Records(existing, effective))
        {
            return false;
        }

        // The resolution no longer names the effective destination: reopen,
        // then re-resolve when a destination remains. Two appended history
        // rows; the withdrawn destination stays on the record.
        var reopened = await unidentifiedStore.ReopenAsync(
            new(
                existing.Id,
                existing.Version,
                ReconciliationActor,
                OperationKey("reopen", existing),
                ReopenReason,
                timeProvider.GetUtcNow()),
            cancellationToken);
        if (effective is null)
        {
            return true;
        }

        await ResolveAsync(reopened.Item, effective, cancellationToken);
        return true;
    }

    /// <summary>
    /// One reason string for every reopen this owner writes. The reason takes
    /// part in the store's replay equality check, so a retry of the same
    /// transition must regenerate it byte for byte; a single constant is what
    /// guarantees that without a test standing over it.
    /// </summary>
    private const string ReopenReason =
        "The receipt's effective destination no longer matches this resolution; the Unidentified item is reopened to follow it.";

    /// <summary>
    /// The receipt's effective destination under the existing precedence: the
    /// formal Case the receipt was allocated to, else the Image intake it
    /// registered, else the Triage its request opened, else — last, so nothing
    /// above it is displaced — a Case a member of staff manually linked it to.
    /// One chain for the open and the resolved branches alike, so the two can
    /// never disagree about where the material belongs.
    /// </summary>
    private async Task<UnidentifiedDestination?> DestinationForAsync(
        IntakeReceipt receipt,
        CancellationToken cancellationToken)
    {
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

        // A Triage request held in Unidentified for want of a registration,
        // whose registration has since been read — the operator's own "until a
        // vehicle registration is known, then open the Triage" transition,
        // which a staff re-evaluation reaches. It has a real destination now,
        // so its open item is stale; without this the item stays open beside
        // the Triage and the same material sits in two queues (INTK-033).
        if (ProcessIntake.IsTriageRequest(receipt)
            && await triageQueries.GetByOriginReceiptAsync(receipt.Id, cancellationToken) is { } triage)
        {
            return new(
                UnidentifiedResolutionTargetKind.Triage,
                triage.Id.ToString("N"),
                triage.NormalizedVehicleRegistration);
        }

        // Trailing, deliberately: a receipt with no route of its own that a
        // member of staff has linked to a Case. Placed last so an established
        // Image intake or Triage keeps precedence over the manual link.
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
        UnidentifiedDestination destination,
        CancellationToken cancellationToken) =>
        resolveUnidentified.ExecuteAsync(
            new(
                item.Id,
                item.Version,
                // UnidentifiedValidation.ValidateResolve requires Staff or
                // Automation (unlike registration, which also accepts
                // SystemWorker); this automatic reconciliation is authorised
                // automation, not registration.
                ReconciliationActor,
                OperationKey("reconcile", item),
                $"The receipt now has a {destination.Kind} destination; the Unidentified item is superseded.",
                destination.Kind,
                destination.Id,
                destination.Reference,
                timeProvider.GetUtcNow()),
            cancellationToken);

    /// <summary>
    /// The key for one transition out of <paramref name="item"/>'s current
    /// version. It is the item's own version, never the origin receipt's,
    /// because a destination change need not mutate the receipt: unlinking and
    /// relinking move the association, and opening a Triage for a receipt
    /// already linked to a case leaves the receipt untouched, so a
    /// receipt-keyed re-resolve rebuilt the key its own first resolution had
    /// taken, was rejected as a conflicting replay, and left the item open
    /// beside its live destination with every later sweep failing on the same
    /// taken key (INTK-048).
    ///
    /// The item's version is also what makes a genuine retry idempotent: it is
    /// the expected version the transition is applied at, so a retry of the
    /// same logical correction rebuilds the same key and replays, while each
    /// further transition — reopen, then re-resolve — moves to a version of its
    /// own and cannot collide with the one before it. No clock, GUID or counter
    /// takes part.
    /// </summary>
    private static string OperationKey(string transition, UnidentifiedItem item) =>
        $"intake-unidentified-{transition}:{item.Id:N}:{item.Version}";

    private static bool IsOwnResolution(UnidentifiedItem item) =>
        item.State == UnidentifiedState.Resolved
        && item.ResolvedBy is { Kind: ActorKind.Automation } resolver
        && string.Equals(resolver.SubjectId, AutomationActorId, StringComparison.Ordinal);

    private static bool Records(UnidentifiedItem item, UnidentifiedDestination destination) =>
        item.ResolutionTargetKind == destination.Kind
        && string.Equals(item.ResolutionTargetId, destination.Id, StringComparison.Ordinal)
        && string.Equals(item.ResolutionTargetReference, destination.Reference, StringComparison.Ordinal);

    private sealed record UnidentifiedDestination(
        UnidentifiedResolutionTargetKind Kind,
        string Id,
        string? Reference);
}
