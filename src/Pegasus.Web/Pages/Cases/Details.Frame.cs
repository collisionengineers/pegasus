using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.AiWork;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// The v26 frame of the Case record: the ribbon's facts and chips, the
/// Actions menu's state-dependent items, per-section availability and the
/// aside (Figures and Next action). The
/// section-owned members live in their own partial files beside this one; the
/// original <c>Details.cshtml.cs</c> keeps the handlers it already had.
/// </summary>
public sealed partial class DetailsModel
{
    /// <summary>The Case's Draft ready AI jobs, oldest first, for the Next action panel.</summary>
    public IReadOnlyList<AiDraft> AiDrafts { get; private set; } = [];

    /// <summary>Whether this staff member may take the unassigned Case for themself (P8).</summary>
    public bool CanAssignToMe { get; private set; }

    /// <summary>
    /// The lease is this browser's and the Case is not archived: the page-wide
    /// edit session is open. A Completed or Query Case offers no session of
    /// its own; Return to Engineer runs from the Actions menu without one.
    /// </summary>
    public bool IsEditing => !string.IsNullOrWhiteSpace(LeaseToken) && CurrentWorkflow?.Archive is null;

    /// <summary>A colleague holds the Case's edit lease.</summary>
    public bool ColleagueIsEditing =>
        !ViewerHoldsEditAuthority && CurrentEditLease is not null && EditAuthorityHolder is not null;

    /// <summary>
    /// Create audit is listed in the Actions menu of every Inspection + Audit
    /// Case, in every state (operator, 1 October 2026); the type is fixed
    /// identity, so no other Case lists it.
    /// </summary>
    public bool OffersCreateAudit =>
        Case is { } details && details.Summary.CaseType == CaseType.InspectionAndAudit;

    /// <summary>
    /// Why the listed Create audit is greyed out, stated on hover, or null
    /// when it is live: a colleague holds the lease, or Core's shared Audit
    /// policy refuses it (Held, closed, archived, an Audit already, no
    /// Engineer). The command runs under the session's lease or one claimed
    /// for it (operator, 29 September 2026), so it is live in and out of an
    /// edit session.
    /// </summary>
    public string? CreateAuditCondition
    {
        get
        {
            if (Case is not { } details)
            {
                return null;
            }
            if (ColleagueIsEditing && EditAuthorityHolder is { } holder)
            {
                return $"{EditModeDisplay.HolderName(holder)} is editing";
            }
            return AuditPolicy.Refusal(details.Summary.CaseType, details.Workflow, Works) is { } refusal
                ? AuditPolicy.Message(refusal)
                : null;
        }
    }

    /// <summary>The listed Create audit is live: its dialog renders and its button opens it.</summary>
    public bool CanCreateAudit => OffersCreateAudit && CreateAuditCondition is null;

    /// <summary>The Audit reference the dialog announces: <c>a.{Case/PO}</c>.</summary>
    public string? ProposedAuditReference =>
        Case is { } details
            ? CaseReferenceFormat.AuditReport(details.Workflow.Identity.Reference)
            : null;

    /// <summary>The Engineer sections that follow the assessment access rule.</summary>
    private static readonly HashSet<string> EngineerSectionKeys =
        new(StringComparer.Ordinal) { "damage", "valuation", "estimate", "settlement", "report" };

    /// <summary>Whether the section's controls are live in the current session.</summary>
    public bool SectionIsEditable(string key) =>
        EngineerSectionKeys.Contains(key) ? CanEditEngineering : CanEditCaseData;

    /// <summary>
    /// The one availability sentence a section states in its head while an
    /// edit session (this viewer's or a colleague's) keeps it reading, or
    /// while the Inspection view reads (v29 P3: every section but Files and
    /// Notes, which are the Case's own); null when the section edits, or when
    /// nothing is being edited at all.
    /// </summary>
    public string? SectionAvailability(string key)
    {
        if (CurrentWorkflow is null)
        {
            return null;
        }
        if (IsInspectionView && key is not ("files" or "notes"))
        {
            return CaseWorkspaceLabels.Frame.ReadOnlyAuditCreated;
        }
        if (ColleagueIsEditing && EditAuthorityHolder is { } holder)
        {
            return $"{EditModeDisplay.HolderName(holder)} is editing";
        }
        if (!IsEditing || SectionIsEditable(key))
        {
            return null;
        }
        if (IsPostReportReadOnly)
        {
            return CaseWorkspaceLabels.Frame.ReturnToEngineerToEdit;
        }
        return null;
    }

    /// <summary>
    /// Whether a section head offers Edit: outside an edit session, while no
    /// one holds the Case's lease, on a Case this viewer could edit, for a
    /// section that has controls at all. A held lease is taken over from the
    /// ribbon only; a lazily loaded section does not resolve the holder, so it
    /// asks whether any lease is live rather than whose it is. The Files and
    /// Notes sections act through their own immediate posts. The Inspection
    /// view offers no Edit anywhere (v29 P3).
    /// </summary>
    public bool SectionOffersEdit(string key) =>
        !IsEditing
        && CurrentEditLease is null
        && !IsPostReportReadOnly
        && CurrentWorkflow?.Archive is null
        && !IsInspectionView
        && key is not ("files" or "notes");

    /// <summary>The state chip's text, with the hold's review date when one is set.</summary>
    public string StateChipText
    {
        get
        {
            var workflow = Case!.Workflow;
            var stage = OperatorLabels.CaseStage(workflow.State);
            return workflow.State == CaseLifecycleState.Held && workflow.HoldReviewOn is { } reviewOn
                ? $"{stage} · review on {reviewOn:d MMM}"
                : stage;
        }
    }

    /// <summary>The Case type chip; absent for a plain Inspection.</summary>
    /// <summary>A standalone Audit Case carries the Original report section (v28 P51).</summary>
    public bool IsAuditCase => Case?.Summary.CaseType == CaseType.Audit;

    /// <summary>Damage and Valuation read inside Vehicle (v28 P26): their hosts stay, their links go, the Vehicle link speaks for them.</summary>
    public static bool IsNestedSection(string key) => key is "damage" or "valuation";

    /// <summary>Whether the section row shows this key on this Case.</summary>
    public bool SectionIsShown(string key) => !IsNestedSection(key) && (key != "original-report" || IsAuditCase);

    /// <summary>The link the section row marks current: a nested section is its parent's.</summary>
    public string SectionLinkKey => IsNestedSection(Section) ? "vehicle" : Section;

    public string? CaseTypeChip => Case?.Summary.CaseType switch
    {
        CaseType.Audit => "Audit",
        CaseType.InspectionAndAudit => "Inspection + Audit",
        _ => null
    };

    /// <summary>
    /// The report blockers the Next action lists (issue 899): while the viewed
    /// work's report is not ready, every blocker, each linking to the section
    /// that clears it (FRD-13). The Inspection view lists the Inspection
    /// report's own blockers while that report is still to be sent (operator,
    /// 2 October 2026); the Audit view lists the Audit's while its assessment
    /// is writable. Empty otherwise.
    /// </summary>
    public IReadOnlyList<AssessmentReadinessItem> NextActionBlockers =>
        IsInspectionView
            ? InspectionReportDraftPreparation is { CanGenerate: false } inspection ? inspection.Reasons : []
            : !AssessmentIsReadOnly && ReportDraftNotReady ? ReportDraftReasons : [];

    /// <summary>The viewed work's current generation: the Inspection's in the Inspection view, else the current work's.</summary>
    public CaseReportGenerationRecord? ViewedReportGeneration =>
        IsInspectionView ? InspectionReportGeneration : CurrentReportGeneration;

    /// <summary>
    /// The one-line Next action the aside states: the AI draft rows come first
    /// (rendered by the view), then the viewed work's next step
    /// (<see cref="CaseNextAction"/>). While the report is not ready there is
    /// no line: the <see cref="NextActionBlockers"/> list is the next action,
    /// except in a read-only Audit view, where the line names Report not
    /// ready. The Inspection view states the Inspection report's own step and
    /// nothing once that report is sent (operator, 2 October 2026).
    /// </summary>
    public CaseNextActionStep? NextAction
    {
        get
        {
            if (IsInspectionView)
            {
                var inspection = CaseNextAction.OfPastWork(
                    NextActionBlockers,
                    BlockerSectionKey,
                    InspectionReportGeneration,
                    CurrentDeliveryPreparation,
                    Works?.Primary.ReportSentEvidence);
                return inspection is { Blocker: not null } ? null : inspection;
            }
            var details = Case!;
            var next = CaseNextAction.Of(
                details.Workflow,
                details.Summary.CaseType,
                Works,
                OutstandingRequirements.Count > 0 ? OutstandingRequirements[0].Title : null,
                ReportDraftNotReady ? ReportDraftReasons : [],
                BlockerSectionKey,
                CurrentReportGeneration,
                CurrentDeliveryPreparation);
            if (next.Blocker is null)
            {
                return next;
            }
            // A writable view lists the blockers in place of this line.
            return AssessmentIsReadOnly ? new(CaseWorkspaceLabels.Report.NotReady, "report") : null;
        }
    }

    /// <summary>The Figures aside's repair cost inc VAT from the current estimate.</summary>
    public decimal? RepairCostIncVat =>
        CurrentSpecification is { } estimate
            ? Pegasus.Core.Reports.ReportRepairCosts.For(estimate).Total
            : null;

    /// <summary>
    /// Create audit (v29 P5): the Case gains its Audit work and from then on
    /// reads its Audit view. It posts in place with the lease handling of the
    /// other Actions-menu lifecycle actions and lands back on this Case's
    /// default view with no notice; a refusal states Core's reason.
    /// </summary>
    public Task<IActionResult> OnPostCreateAuditAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string? editLeaseToken,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandUnderLeaseAsync(
            id,
            expectedVersion,
            editLeaseToken,
            "create_audit",
            async (actor, lease) =>
            {
                await createAudit.ExecuteAsync(
                    new(id, expectedVersion, actor, RequireOperationKey(operationKey), lease),
                    cancellationToken);
                return null;
            },
            exception => exception is AuditCreationException refusal
                ? refusal.Message
                : CaseCommandRefused);

    /// <summary>
    /// Every lease-carrying store mutation consumes the lease. An immediate
    /// post (a valuation, an estimate action, a lookup) is not the end of the
    /// operator's edit session (v25 decision F), so after one succeeds the
    /// base reclaims a fresh lease with these two readers.
    /// </summary>
    protected override (ICaseWorkflowQueries Workflows, IAcquireCaseEditLease Leases)? LeaseReclaim => (caseWorkflows, acquireLease);
}
