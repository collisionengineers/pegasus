using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.Unidentified;

namespace Pegasus.Web.Presentation;

/// <summary>
/// What the confirmation surface offers for one file's outcome — a report of
/// what already happened automatically, or the staff decision that is
/// genuinely open. Never a raw enum, GUID, or internal state name.
/// </summary>
public enum UploadOutcomeKind
{
    /// <summary>Still Received or Processing; nothing to confirm yet.</summary>
    Working,

    /// <summary>Automatically associated with a case at the INT-28 bar. Reported, not re-offered.</summary>
    Attached,

    /// <summary>Automatically registered as a new Image-initiated Case. Reported, not re-offered.</summary>
    ImageCaseRegistered,

    /// <summary>Automation abstained (no usable/unique VRM, or ambiguous instruction match with no candidate); routed to Unidentified for a staff decision there.</summary>
    NeedsReview,

    /// <summary>Automation abstained short of the unique-match bar, but named candidates. The staff decision this ticket offers: attach, with the operator free to choose a different case.</summary>
    PossibleMatch,

    /// <summary>No automatic match at all. The staff decision this ticket offers: create a case from what was uploaded.</summary>
    ReadyToCreate,

    /// <summary>Cannot become a case (blocked, unsupported, technical failure).</summary>
    CannotBecomeCase,

    /// <summary>The file itself failed to process.</summary>
    Failed
}

public sealed record UploadOutcomeAction(string Label, string Url);

/// <summary>
/// The staff decision to add the uploaded material to an existing case, found
/// by the confirmation surface's case search. Carried only where that
/// decision is genuinely open (no case located, or automation abstained short
/// of its bar) — an association automation already made at the accepted bar
/// is reported, never re-offered. The receipt named here is the one the link
/// applies to: for a registered Image-initiated Case that is its origin
/// receipt, so the existing link path also runs the merge transition.
/// </summary>
public sealed record UploadOutcomeAttach(Guid ReceiptId);

public sealed record UploadOutcomeView(
    UploadOutcomeKind Kind,
    string StateLabel,
    string Message,
    UploadOutcomeAction? PrimaryAction,
    UploadOutcomeAction? SecondaryAction,
    UploadOutcomeAttach? Attach = null)
{
    /// <summary>Whether this state is worth polling again — mirrors the existing Received/Processing refresh rule.</summary>
    public bool IsStillWorking => Kind == UploadOutcomeKind.Working;

    /// <summary>
    /// The label passed to <c>Shared/_StatusChip</c> for this outcome's tone
    /// and icon — always one of that partial's existing recognised words
    /// ("the single place a business or query state chooses its visual
    /// treatment"), never a new one added just for this surface, and never a
    /// settled term reused for a different meaning ("needs sorting" is used
    /// only for the branch that genuinely routes through the Unidentified/
    /// needs-sorting mechanism). Null while still working — that state stays
    /// plain text, matching the page's existing pre-confirmation copy.
    /// </summary>
    public string? ChipLabel => Kind switch
    {
        UploadOutcomeKind.Working => null,
        UploadOutcomeKind.Attached or UploadOutcomeKind.ImageCaseRegistered => "Success",
        UploadOutcomeKind.NeedsReview => "Needs sorting",
        UploadOutcomeKind.PossibleMatch or UploadOutcomeKind.ReadyToCreate => "Pending",
        UploadOutcomeKind.CannotBecomeCase or UploadOutcomeKind.Failed => "Failed",
        _ => null
    };
}

public interface IUploadOutcomeQueries
{
    /// <param name="status">The queued status already read for this file.</param>
    /// <param name="submissionGroupId">
    /// The submission group this file belongs to, when known, so a grouped
    /// image upload that was kept intact under one Unidentified reference
    /// (no usable/conflicting VRM across the whole group) is still found even
    /// though the Unidentified item is registered against the group, not the
    /// member. Null for a single-file upload.
    /// </param>
    Task<UploadOutcomeView> BuildAsync(
        QueuedIntakeStatus status,
        Guid? submissionGroupId,
        ActionActor actor,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Builds the confirmation-surface decision for one uploaded file from the
/// data the intake pipeline already recorded. Reads only — every action it
/// offers routes to the existing page that performs it (<c>/Received/{id}</c>
/// i.e. Intake/Details for attach-with-override and reversal,
/// <c>/Cases/Create</c> for a new Instruction-initiated case,
/// <c>/VehicleImages/{id}</c> and <c>/Unidentified/{id}</c> for their own
/// existing surfaces) rather than re-implementing any of those mutations.
/// </summary>
/// <remarks>
/// Automatic association is channel-agnostic and already ran by the time this
/// is called (<c>AssociateCaseIfUnambiguousAsync</c> in Core): a definitive,
/// unique case match is always attached before a receipt reaches Complete, so
/// <see cref="UploadOutcomeKind.Attached"/> is always a report of something
/// automation already did, never a decision this surface makes. The one
/// staff decision this ticket adds is offered when automation fell short of
/// that bar: <see cref="UploadOutcomeKind.PossibleMatch"/> (ambiguous
/// candidates) or <see cref="UploadOutcomeKind.ReadyToCreate"/> (no match at
/// all). A grouped image upload can terminal-decide its members
/// independently (INTK-011); this builder makes no group-wide assumption —
/// it is evaluated once per member.
/// </remarks>
public sealed class UploadOutcomeQueries(
    IGetIntake getIntake,
    IImageIntakeQueries imageIntakeQueries,
    IUnidentifiedStore unidentifiedStore) : IUploadOutcomeQueries
{
    public async Task<UploadOutcomeView> BuildAsync(
        QueuedIntakeStatus status,
        Guid? submissionGroupId,
        ActionActor actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(status);

        switch (status.Status)
        {
            case QueuedIntakeStatusKind.Received:
                return new(
                    UploadOutcomeKind.Working,
                    "Received",
                    "The file is safely received and waiting for background processing.",
                    null,
                    null);
            case QueuedIntakeStatusKind.Processing:
                return new(
                    UploadOutcomeKind.Working,
                    "Processing",
                    "The file is being processed.",
                    null,
                    null);
            case QueuedIntakeStatusKind.Failed:
                return new(
                    UploadOutcomeKind.Failed,
                    "Failed",
                    OperatorLabels.IntakeFailure(status.FailureCode) + ".",
                    null,
                    null);
        }

        var receiptId = status.ProcessedReceiptId ?? status.StagedReceiptId;
        var receipt = await getIntake.ExecuteAsync(new(receiptId, actor), cancellationToken);
        if (receipt is null)
        {
            // The record has not caught up with the queue status yet; the
            // page will refresh again shortly (RefreshAutomatically is still
            // driven by status.Status, which is Complete here — the caller
            // simply re-renders "processing" for one more refresh).
            return new(UploadOutcomeKind.Working, "Processing", "The file is being processed.", null, null);
        }

        // The receipt's own CurrentCaseId is Core's reconciliation of the
        // accepted and staff-linked associations — the same fact the queued
        // status derives its CaseId from — so it alone decides whether a
        // case is already settled before any open decision is offered.
        if (receipt.CurrentCaseId is { } caseId)
        {
            var reference = receipt.CurrentCaseReference;
            // A staff link (the confirmation surface's own add-to-case
            // decision, or the received-item screen) must not be reported as
            // automation's doing — the report-not-reoffer rule cuts both
            // ways: what it says happened automatically really did.
            var byStaff = receipt.AssociationWasStaffDecision;
            return new(
                UploadOutcomeKind.Attached,
                "Associated with a case",
                (byStaff, reference) switch
                {
                    (true, null) => "This was added to a case.",
                    (true, { } linked) => $"This was added to case {linked}.",
                    (false, null) => "This was automatically associated with a case.",
                    (false, { } matched) => $"This was automatically associated with case {matched}."
                },
                new("Open case", $"/Cases/Details/{caseId:D}"),
                new("Not the right case?", $"/Received/{receiptId:D}"));
        }

        // Not gated on the ImageIntakeRegistered decision alone: a member of
        // a group registered as one unit can briefly keep its own pre-group
        // NeedsSorting decision, while the registration (resolved through the
        // group membership) is already this file's settled truth. The
        // group-membership fallback only exists for grouped uploads, so a
        // single-file upload that was not registered skips the lookup; and
        // the Core-owned image-only-material rule (not a media-type sniff)
        // keeps an instruction document in a mixed group from being
        // mislabelled with the images' registration.
        if (receipt.Decision == IntakeDecision.ImageIntakeRegistered
            || (submissionGroupId is not null
                && ImageIntakeLifecycleRules.IsImageOnlyMaterial(receipt)))
        {
            var detail = await imageIntakeQueries.GetByOriginReceiptAsync(receipt.Id, cancellationToken);
            if (detail is { State: ImageInitiatedCaseState.MergedIntoInstructionCase, MergedIntoCaseId: { } mergedCaseId })
            {
                // A group member whose own receipt carries no case link still
                // reflects the group's settled destination once its
                // registered vehicle-image case has been merged into a Case
                // — reporting "registered as new" here would be stale.
                return new(
                    UploadOutcomeKind.Attached,
                    "Associated with a case",
                    detail.MergedIntoCaseReference is { } mergedReference
                        ? $"This was added to case {mergedReference}."
                        : "This was added to a case.",
                    new("Open case", $"/Cases/Details/{mergedCaseId:D}"),
                    null);
            }
            if (detail is not null)
            {
                return new(
                    UploadOutcomeKind.ImageCaseRegistered,
                    "Registered as a new vehicle-image case",
                    $"No matching case was found, so this was registered as a new vehicle-image case, {detail.Record.ImageIntakeReference}.",
                    new("View", $"/VehicleImages/{detail.Record.Id:D}"),
                    null,
                    detail.State == ImageInitiatedCaseState.AwaitingInstruction
                        ? new UploadOutcomeAttach(detail.Record.Origin.ReceiptId)
                        : null);
            }
        }

        var unidentified = await unidentifiedStore.GetByOriginAsync(
            UnidentifiedOrigin.Receipt(receipt.Id), cancellationToken);
        if (unidentified is null && submissionGroupId is { } groupId)
        {
            unidentified = await unidentifiedStore.GetByOriginAsync(
                UnidentifiedOrigin.SubmissionGroup(groupId), cancellationToken);
        }
        if (unidentified is { State: UnidentifiedState.Open })
        {
            return new(
                UploadOutcomeKind.NeedsReview,
                "Needs review",
                "This could not be matched automatically and needs a staff decision.",
                new("Review", $"/Unidentified/{unidentified.Id:D}"),
                null);
        }

        if (receipt.CaseMatchDecision?.Outcome == CaseMatchOutcome.Ambiguous)
        {
            return new(
                UploadOutcomeKind.PossibleMatch,
                "Possible matching cases found",
                "More than one case could match this. Review the candidates and choose where it belongs.",
                new("Review and attach", $"/Received/{receipt.Id:D}"),
                null,
                new UploadOutcomeAttach(receipt.Id));
        }

        // Mirrors Cases/Create.cshtml.cs's own eligibility check exactly:
        // OcrRequired is the hand-keyed case (little or no text was
        // extracted), allowed through alongside CanBecomeCase rather than
        // folded into it, so the two surfaces can never disagree about what
        // is still eligible to become a case.
        if (receipt.Decision == IntakeDecision.OcrRequired
            || IntakeDecisionPolicy.CanBecomeCase(receipt.Decision))
        {
            return new(
                UploadOutcomeKind.ReadyToCreate,
                "No matching case found",
                "No existing case matched this. Create one from what was uploaded.",
                new("Create a case", $"/Cases/Create?receiptId={receipt.Id:D}"),
                null,
                new UploadOutcomeAttach(receipt.Id));
        }

        return new(
            UploadOutcomeKind.CannotBecomeCase,
            "Could not become a case",
            OperatorLabels.IntakeCannotBecomeCaseReason(receipt.Decision),
            new("View", $"/Received/{receipt.Id:D}"),
            null);
    }
}
