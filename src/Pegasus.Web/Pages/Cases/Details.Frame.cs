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
/// Actions menu's state-dependent items, per-section availability, the aside
/// (Figures and Next action), the Audit link, and the working-set glyph. The
/// section-owned members live in their own partial files beside this one; the
/// original <c>Details.cshtml.cs</c> keeps the handlers it already had.
/// </summary>
public sealed partial class DetailsModel
{
    /// <summary>The Audit Case created from this Case, when one exists.</summary>
    public CaseAuditLink? AuditCase { get; private set; }

    /// <summary>The original Case this Audit Case was created from, when it is one.</summary>
    public CaseAuditLink? OriginalCase { get; private set; }

    /// <summary>Whether any report generation on the Case has a confirmed artifact.</summary>
    public bool HasGeneratedReport { get; private set; }

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
    /// Create audit (13 September): offered when the Case is Inspection + Audit,
    /// has no Audit yet, is not Created in error or archived, and a report has
    /// been generated — the same facts <see cref="CreateAuditCase"/> checks.
    /// The command carries the edit lease, so the item is offered inside the
    /// edit session.
    /// </summary>
    public bool CanCreateAudit =>
        Case is { } details
        && details.Summary.CaseType == CaseType.InspectionAndAudit
        && AuditCase is null
        && OriginalCase is null
        && details.Workflow.State != CaseLifecycleState.CreatedInError
        && details.Workflow.Archive is null
        && HasGeneratedReport
        && IsEditing;

    /// <summary>
    /// The Audit reference the dialog announces, derived from the recorded
    /// outcome exactly as Core derives it; null while no outcome is recorded.
    /// </summary>
    public string? ProposedAuditReference =>
        Case is { } details
        && AuditCasePolicy.AssessmentFor(Assessment?.Field(AssessmentVocabulary.Outcome)?.Value) is not null
            ? AuditIdentity.Create(details.Workflow.Identity.Reference)
            : null;

    /// <summary>The Engineer sections that follow the assessment access rule.</summary>
    private static readonly HashSet<string> EngineerSectionKeys =
        new(StringComparer.Ordinal) { "damage", "valuation", "estimate", "settlement", "report" };

    /// <summary>Whether the section's controls are live in the current session.</summary>
    public bool SectionIsEditable(string key) =>
        EngineerSectionKeys.Contains(key) ? CanEditEngineering : CanEditCaseData;

    /// <summary>
    /// The one availability sentence a section states in its head while an
    /// edit session (this viewer's or a colleague's) keeps it reading; null
    /// when the section edits, or when nothing is being edited at all.
    /// </summary>
    public string? SectionAvailability(string key)
    {
        if (CurrentWorkflow is null)
        {
            return null;
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
    /// Files and Notes sections act through their own immediate posts.
    /// </summary>
    public bool SectionOffersEdit(string key) =>
        !IsEditing
        && !ColleagueIsEditing
        && !IsPostReportReadOnly
        && CurrentWorkflow?.Archive is null
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
    public string? CaseTypeChip => Case?.Summary.CaseType switch
    {
        CaseType.Audit => "Audit",
        CaseType.InspectionAndAudit => "Inspection + Audit",
        _ => null
    };

    /// <summary>The working-set glyph the record announces (v26 § Working set).</summary>
    public string? WorkingSetGlyph =>
        ColleagueIsEditing
            ? WorkingSetRecord.Glyphs.LeaseColleague
            : GlassSession is { } session && GlassRepairEstimateSessionPolicy.OccupiesAccount(session.State)
                ? WorkingSetRecord.Glyphs.GlassOpen
                : null;

    /// <summary>
    /// The frame's extra reads: the Audit link, whether a report was ever
    /// generated, the Case's AI drafts and the self-assignment rule.
    /// </summary>
    private async Task DescribeFrameAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        if (Case is not { } details)
        {
            return;
        }

        var caseId = details.Workflow.CaseId;
        AuditCase = await auditLinks.GetAuditCaseAsync(caseId, cancellationToken);
        OriginalCase = details.Summary.CaseType == CaseType.Audit
            ? await auditLinks.GetOriginalCaseAsync(caseId, cancellationToken)
            : null;
        HasGeneratedReport = details.Summary.CaseType == CaseType.InspectionAndAudit
            && await reportGenerated.HasGeneratedReportAsync(caseId, cancellationToken);
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
    /// Create audit: a new duplicate Case linked to this one. On success the
    /// operator lands on the new Case; a refusal states Core's reason on this one.
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
            var result = await createAuditCase.ExecuteAsync(
                new(id, expectedVersion, actor, RequireOperationKey(operationKey), editLeaseToken),
                cancellationToken);
            ClearLeaseState();
            TempData["CaseStatus"] = $"Case {result.AuditCase.Reference} was created.";
            return RedirectToPage("/Cases/Details", new { id = result.AuditCase.CaseId });
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
            TempData["CaseError"] = exception is AuditCaseCreationException refusal
                ? refusal.Message
                : "The audit case was not created because the case changed or edit mode was lost.";
            return RedirectToDetails(id);
        }
    }

    /// <summary>
    /// Every lease-carrying store mutation consumes the lease. An immediate
    /// post (a valuation, an estimate action, a lookup) is not the end of the
    /// operator's edit session (v25 decision F), so after one succeeds the
    /// base reclaims a fresh lease with these two readers.
    /// </summary>
    protected override (IGetCase Cases, IAcquireCaseEditLease Leases)? LeaseReclaim => (getCase, acquireLease);
}
