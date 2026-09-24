using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.AiWork;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
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
    /// edit session is open. Post-report read-only Cases still open it so the
    /// Return to Engineer action can be taken ("Enable return").
    /// </summary>
    public bool IsEditing => !string.IsNullOrWhiteSpace(LeaseToken) && CurrentWorkflow?.Archive is null;

    /// <summary>A colleague holds the Case's edit lease.</summary>
    public bool ColleagueIsEditing =>
        !ViewerHoldsEditAuthority && CurrentEditLease is not null && EditAuthorityHolder is not null;

    /// <summary>
    /// The one control that can end this viewer's own second window's lease:
    /// the lease is theirs but this browser no longer carries the token.
    /// </summary>
    public bool OwnLeaseHeldElsewhere => ViewerHoldsEditAuthority && !IsEditing && CanRecoverLease;

    /// <summary>
    /// Create audit (v29 P5): offered where Core's shared Audit policy finds
    /// no refusal (an Inspection + Audit Case whose report is sent, with no
    /// Audit yet and an assigned Engineer). The command carries the edit
    /// lease, so the item is offered inside the edit session.
    /// </summary>
    // STAGE2-CONTRACT: AuditPolicy.Refusal(CaseType, CaseWorkflowRecord, CaseWorkSet?) (AUDIT, Lifecycle/CreateAudit.cs).
    public bool CanCreateAudit =>
        Case is { } details
        && AuditPolicy.Refusal(details.Summary.CaseType, details.Workflow, Works) is null
        && IsEditing;

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
    /// Whether a section head offers Edit: outside an edit session, on a Case
    /// this viewer could edit, for a section that has controls at all. The
    /// Files and Notes sections act through their own immediate posts. The
    /// Inspection view offers no Edit anywhere (v29 P3).
    /// </summary>
    public bool SectionOffersEdit(string key) =>
        !IsEditing
        && !ColleagueIsEditing
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
    /// The frame's extra reads: the Case's AI drafts and the self-assignment rule.
    /// </summary>
    private async Task DescribeFrameAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        if (Case is not { } details)
        {
            return;
        }

        var caseId = details.Workflow.CaseId;
        AiDrafts = await aiDrafts.ListForCaseAsync(caseId, cancellationToken);
        CanAssignToMe = CaseLifecycleRules.CanAssignToSelf(details.Workflow);
    }

    /// <summary>
    /// The one-line Next action the aside states: the AI draft rows come first
    /// (rendered by the view), then the next permitted lifecycle action.
    /// </summary>
    public (string Label, string SectionKey) NextAction
    {
        get
        {
            var workflow = Case!.Workflow;
            if (workflow.Archive is not null || CaseLifecycleRules.IsTerminal(workflow.State))
            {
                return ("None", "notes");
            }
            if (workflow.State == CaseLifecycleState.Review)
            {
                return (CaseWorkspaceLabels.HandToEngineer, "overview");
            }
            if (workflow.State is CaseLifecycleState.NotReady or CaseLifecycleState.Held)
            {
                return (OutstandingRequirements.Count > 0
                    ? OutstandingRequirements[0].Title
                    : OperatorLabels.CaseStage(workflow.State), "overview");
            }
            if (ReportDraftNotReady && ReportDraftReasons.Count > 0)
            {
                // One line in the aside: the first missing requirement and how
                // many follow; the Report section states the whole list.
                var first = ReportDraftReasons[0].Requirement;
                return (ReportDraftReasons.Count > 1 ? $"{first} · {ReportDraftReasons.Count - 1} more" : first, "valuation");
            }
            if (CurrentReportGeneration is null
                || CurrentReportGeneration.State == Pegasus.Core.Reports.CaseReportGenerationState.Stale)
            {
                return (CaseWorkspaceLabels.ReportDelivery.GenerateReport, "report");
            }
            if (workflow.ReportSentEvidence is not null)
            {
                return ("Mark completed", "overview");
            }
            if (CurrentDeliveryPreparation is not null)
            {
                return (CaseWorkspaceLabels.ReportDelivery.SendPreparedReport, "report");
            }
            return (CaseWorkspaceLabels.ReportDelivery.PrepareDelivery, "report");
        }
    }

    /// <summary>The Figures aside's repair cost inc VAT from the current estimate.</summary>
    public decimal? RepairCostIncVat =>
        AcceptedSpecification is { } estimate
            ? Pegasus.Core.Reports.ReportRepairCosts.For(estimate).Total
            : null;

    /// <summary>
    /// Create audit (v29 P5): the Case gains its Audit work and from then on
    /// reads its Audit view. It posts in place with the lease handling of the
    /// other Actions-menu lifecycle actions and lands back on this Case's
    /// default view with no notice; a refusal states Core's reason.
    /// </summary>
    public async Task<IActionResult> OnPostCreateAuditAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string editLeaseToken,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            await createAudit.ExecuteAsync(
                new(id, expectedVersion, actor, RequireOperationKey(operationKey), editLeaseToken),
                cancellationToken);
            ClearLeaseState();
        }
        catch (StaffAuthorizationException)
        {
            ClearLeaseState();
            return Forbid();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCaseCommandFailed(logger, id, "create_audit", exception);
            HandleLeaseFailure(id, editLeaseToken, exception);
            TempData["CaseError"] = exception is AuditCreationException refusal
                ? refusal.Message
                : CaseCommandRefused;
        }

        return RedirectToDetails(id);
    }

    /// <summary>
    /// Every lease-carrying store mutation consumes the lease. An immediate
    /// post (a valuation, an estimate action, a lookup) is not the end of the
    /// operator's edit session (v25 decision F), so after one succeeds the
    /// base reclaims a fresh lease with these two readers.
    /// </summary>
    protected override (IGetCase Cases, IAcquireCaseEditLease Leases)? LeaseReclaim => (getCase, acquireLease);
}
