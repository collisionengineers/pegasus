using Pegasus.Core.Actors;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Notifications;
using Pegasus.Core.Tasks;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Lifecycle;


public sealed class PutCaseOnHold(ICaseWorkflowStore store, TimeProvider timeProvider) : IPutCaseOnHold
{
    private readonly ICaseWorkflowStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly TimeProvider _timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public async Task<CaseWorkflowRecord> ExecuteAsync(PutCaseOnHoldRequest request, CancellationToken cancellationToken)
    {
        CaseLifecycleRules.ValidateMutation(request);
        CaseLifecycleRules.ValidateHoldReviewDate(request.ReviewOn, _timeProvider.GetUtcNow());
        return await _store.HoldAsync(request, cancellationToken);
    }
}

public sealed class ReleaseCaseHold(ICaseWorkflowStore store)
{
    private readonly ICaseWorkflowStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<CaseWorkflowRecord> ExecuteAsync(CaseMutationRequest request, CancellationToken cancellationToken)
    {
        CaseLifecycleRules.ValidateMutation(request);
        return await _store.ReleaseHoldAsync(request, cancellationToken);
    }
}

public sealed class ReturnCaseToReview(ICaseWorkflowStore store)
{
    private readonly ICaseWorkflowStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<CaseWorkflowRecord> ExecuteAsync(
        ReturnCaseToReviewRequest request,
        CancellationToken cancellationToken)
    {
        CaseLifecycleRules.ValidateReturnToReview(request);
        return await _store.ReturnToReviewAsync(request, cancellationToken);
    }
}

public sealed class AssignCaseEngineer(
    ICaseWorkflowStore store,
    ICaseWorkflowConfiguration configuration,
    IStaffAccountQueries staffAccounts,
    ICaseStaffNotifier? notifier = null,
    Pegasus.Core.Assessment.IScheduleAutomaticGuideValuation? automaticValuation = null) : IAssignCaseEngineer
{
    private readonly ICaseWorkflowStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly ICaseWorkflowConfiguration _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    private readonly IStaffAccountQueries _staffAccounts = staffAccounts ?? throw new ArgumentNullException(nameof(staffAccounts));

    public async Task<CaseWorkflowRecord> ExecuteAsync(
        AssignCaseEngineerRequest request,
        CancellationToken cancellationToken)
    {
        var workflowConfiguration = await _configuration.GetCurrentAsync(cancellationToken);
        CaseLifecycleRules.ValidateAssignment(request, workflowConfiguration);
        // The history reason is composed here for every caller (operator, 15
        // October 2026, R41), before the replay check: the operation's hash
        // covers it, so a replay composes the same words.
        request = request with
        {
            Reason = await CaseEngineerAssignmentHistory.ReasonAsync(
                _staffAccounts, request.EngineerId, request.Actor, cancellationToken)
        };
        // The store reads the Case once under its lock: it refuses the state
        // (CaseLifecycleRules.RequireCanBeAssigned) and the Engineer's
        // eligibility, and resolves the Sign-off Engineer, none of it for a replay.
        var assignment = await _store.AssignEngineerAsync(
            request, CaseLifecycleState.ReportPreparation, cancellationToken);
        var assigned = assignment.Workflow;
        if (!assignment.IsReplay && notifier is not null)
        {
            // Work Centre D10 cause 2: the engineer is told, unless they took it themself.
            await notifier.NotifyAsync(
                StaffNotificationCause.CaseAssigned,
                request.CaseId,
                assigned.Identity.Reference,
                assigned.AssignedEngineerId,
                request.Actor,
                section: null,
                registration: null,
                cancellationToken);
        }

        // The Case's Glass's valuation is requested when the Case reaches
        // With Engineer (R465, 28 October 2026), not when a With Engineer
        // Case is handed on.
        if (assignment is { IsReplay: false, EnteredWithEngineer: true } && automaticValuation is not null)
        {
            await automaticValuation.ScheduleAsync(request.CaseId, cancellationToken);
        }

        return assigned;
    }
}

/// <summary>
/// The history reason Assign Engineer records, composed once for every caller
/// (operator, 15 October 2026, R41; no full stops, R161 a): "Taken by {name}"
/// when the Engineer is the actor, "Assigned to {name}" for another Engineer,
/// and "Set to Unassigned" when the Case is handed to nobody.
/// </summary>
public static class CaseEngineerAssignmentHistory
{
    public const string SetToUnassigned = "Set to Unassigned";

    public static string Reason(Guid? engineerId, ActionActor actor, string engineerName)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return engineerId is not { } id
            ? SetToUnassigned
            : id == StaffNotificationPolicy.StaffId(actor)
                ? $"Taken by {engineerName}"
                : $"Assigned to {engineerName}";
    }

    /// <summary>The reason, with the Engineer's name resolved as every history reader names staff.</summary>
    public static async Task<string> ReasonAsync(
        IStaffAccountQueries staffAccounts,
        Guid? engineerId,
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(staffAccounts);
        if (engineerId is not { } id)
        {
            return SetToUnassigned;
        }

        var names = await ActorDisplayNames.ResolveStaffNamesAsync(staffAccounts, [id], cancellationToken);
        return Reason(id, actor, ActorDisplayNames.Resolve(ActorKind.Staff, id.ToString("D"), names));
    }
}

public static class CaseSignOffEngineerResolver
{
    public static SignOffEngineerProfile? Resolve(
        Guid? persistedSignOffEngineerId,
        Guid? assignedEngineerId,
        IReadOnlyList<SignOffEngineerProfile> eligibleProfiles)
    {
        ArgumentNullException.ThrowIfNull(eligibleProfiles);
        return Find(persistedSignOffEngineerId, eligibleProfiles)
            ?? Find(assignedEngineerId, eligibleProfiles)
            ?? eligibleProfiles.SingleOrDefault(profile => profile.IsDefault);
    }

    /// <summary>
    /// The one eligibility rule for a chosen Sign-off Engineer, shared by the
    /// named selection command and the Case save that carries the same choice.
    /// </summary>
    public static void RequireEligible(
        IReadOnlyList<SignOffEngineerProfile> eligibleProfiles,
        Guid signOffEngineerId)
    {
        ArgumentNullException.ThrowIfNull(eligibleProfiles);
        if (signOffEngineerId == Guid.Empty)
        {
            throw new ArgumentException(
                "A Sign-off Engineer identifier is required.",
                nameof(signOffEngineerId));
        }

        if (Find(signOffEngineerId, eligibleProfiles) is null)
        {
            throw new InvalidOperationException("The selected Sign-off Engineer is not eligible.");
        }
    }

    private static SignOffEngineerProfile? Find(
        Guid? staffId,
        IReadOnlyList<SignOffEngineerProfile> profiles) => staffId is null
            ? null
            : profiles.FirstOrDefault(profile => profile.StaffId == staffId.Value);
}

public sealed class StartCaseWork(
    ICaseWorkflowStore store,
    Pegasus.Core.Assessment.IScheduleAutomaticGuideValuation? automaticValuation = null)
{
    private readonly ICaseWorkflowStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<CaseWorkflowRecord> ExecuteAsync(CaseMutationRequest request, CancellationToken cancellationToken)
    {
        CaseLifecycleRules.ValidateMutation(request);
        var started = await _store.StartWorkAsync(request, cancellationToken);
        // Starting work brings the Case from Review to With Engineer, where
        // its Glass's valuation is requested (R465, 28 October 2026).
        if (automaticValuation is not null)
        {
            await automaticValuation.ScheduleAsync(request.CaseId, cancellationToken);
        }

        return started;
    }
}

public static class CaseEngineerEligibilityPolicy
{
    /// <summary>The refusal for an Engineer whose account the store read inside its own transaction.</summary>
    public static void Require(CaseEngineerEligibility eligibility)
    {
        if (!eligibility.AccountExists)
        {
            throw new InvalidOperationException("The assigned staff account does not exist.");
        }

        if (!eligibility.IsEnabled)
        {
            throw new InvalidOperationException("The assigned staff account is disabled.");
        }
    }
}


public sealed class RecordCaseReportApproval(ICaseWorkflowStore store) : IRecordCaseReportApproval
{
    private readonly ICaseWorkflowStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<CaseWorkflowRecord> ExecuteAsync(
        RecordCaseReportApprovalRequest request,
        CancellationToken cancellationToken)
    {
        CaseLifecycleRules.ValidateReportApproval(request);
        return await _store.RecordReportApprovalAsync(request, cancellationToken);
    }
}

public sealed class LinkReportEvidence(ICaseWorkflowStore store) : ILinkReportEvidence
{
    private readonly ICaseWorkflowStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<LinkReportEvidenceResult> ExecuteAsync(
        LinkReportEvidenceRequest request,
        CancellationToken cancellationToken)
    {
        CaseLifecycleRules.ValidateReportEvidence(request, request.EvidenceId);
        var result = await _store.LinkReportEvidenceAsync(request, cancellationToken)
            ?? throw new InvalidDataException("The report-evidence store returned no result.");
        ReportSentTasks.RequireCreatedFor(request.CaseId, result.TasksCreated);
        return result;
    }
}

/// <summary>
/// The tasks a recorded Sent item creates (CASE-20). They are created by the store in the
/// transaction that records the Sent item, from the after-send list the delivery froze, so a
/// result may carry only open, unassigned tasks of the Case it linked. The edit lease is
/// bypassed on purpose: the tasks are a consequence of a recorded Sent item, not a staff edit.
/// </summary>
internal static class ReportSentTasks
{
    public static void RequireCreatedFor(Guid caseId, IReadOnlyList<CaseTaskRecord>? tasks)
    {
        if (tasks is null
            || tasks.Any(task => task is null
                || task.CaseId != caseId
                || task.State != CaseTaskState.Open
                || task.AssigneeId is not null
                || string.IsNullOrWhiteSpace(task.Description)))
        {
            throw new InvalidDataException(
                "The report-evidence store returned invalid tasks for the recorded Sent item.");
        }
    }
}

public sealed class AutoLinkReportEvidence(IAutoLinkReportEvidenceStore store)
    : IAutoLinkReportEvidence
{
    private readonly IAutoLinkReportEvidenceStore _store =
        store ?? throw new ArgumentNullException(nameof(store));

    public async Task<AutoLinkReportEvidenceResult> ExecuteAsync(
        AutoLinkReportEvidenceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.CaseId == Guid.Empty || request.EvidenceId == Guid.Empty)
        {
            throw new ArgumentException(
                "Automatic report-evidence linking requires stable Case and evidence identities.",
                nameof(request));
        }

        ArgumentNullException.ThrowIfNull(request.Actor);
        StaffAuthorization.Require(request.Actor, StaffAccessRight.ExecuteSystemWork);
        if (request.Actor.Kind != ActorKind.SystemWorker)
        {
            throw new UnauthorizedAccessException(
                "Automatic report-evidence linking requires a system-worker actor.");
        }

        RequireText(request.OperationKey, 100, nameof(request));
        RequireText(request.Reason, 500, nameof(request));
        var result = await _store.TryAutoLinkAsync(request, cancellationToken)
            ?? throw new InvalidDataException(
                "The automatic report-evidence store returned no result.");
        ValidateResult(request, result);
        return result;
    }

    private static void ValidateResult(
        AutoLinkReportEvidenceRequest request,
        AutoLinkReportEvidenceResult result)
    {
        if (!Enum.IsDefined(result.Disposition))
        {
            throw new InvalidDataException(
                "The automatic report-evidence store returned an unknown disposition.");
        }

        if (result.Disposition == AutoLinkReportEvidenceDisposition.Linked)
        {
            if (result.NotLinkedReasonCode is not null
                || result.Link is not { } link
                || link.CaseId != request.CaseId
                || link.EvidenceId != request.EvidenceId
                // The current work's evidence moves the Case to Report sent, unless
                // the send does not end the work (an Inspection + Audit Case with no
                // Audit yet stays With Engineer); the Inspection's, once the Audit
                // exists, leaves the state alone.
                || (link.OfCurrentWork && link.State != (link.EndsTheWork
                    ? CaseLifecycleState.ReportSent
                    : CaseLifecycleState.ReportPreparation))
                || !Enum.IsDefined(link.State)
                || link.Version < 0)
            {
                throw new InvalidDataException(
                    "The automatic report-evidence store returned an invalid committed link.");
            }

            ReportSentTasks.RequireCreatedFor(request.CaseId, result.TasksCreated);
            return;
        }

        if (result.TasksCreated is not { Count: 0 })
        {
            throw new InvalidDataException(
                "The automatic report-evidence store returned tasks for a Sent item it did not link.");
        }

        if (result.Link is not null
            || string.IsNullOrWhiteSpace(result.NotLinkedReasonCode)
            || result.NotLinkedReasonCode.Length != result.NotLinkedReasonCode.Trim().Length
            || result.NotLinkedReasonCode.Length > 100
            || result.NotLinkedReasonCode.Any(char.IsControl))
        {
            throw new InvalidDataException(
                "The automatic report-evidence store returned an invalid non-link reason.");
        }
    }

    private static void RequireText(string value, int maximumLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length != value.Trim().Length
            || value.Length > maximumLength
            || value.Any(char.IsControl))
        {
            throw new ArgumentException(
                "The automatic report-evidence value is invalid.",
                parameterName);
        }
    }
}

public sealed class UnlinkReportEvidence(ICaseWorkflowStore store) : IUnlinkReportEvidence
{
    private readonly ICaseWorkflowStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<CaseWorkflowRecord> ExecuteAsync(
        UnlinkReportEvidenceRequest request,
        CancellationToken cancellationToken)
    {
        CaseLifecycleRules.ValidateReportEvidence(request, request.EvidenceId);
        return await _store.UnlinkReportEvidenceAsync(request, cancellationToken);
    }
}

public sealed class CloseCase(ICaseWorkflowStore store) : ICloseCase
{
    private readonly ICaseWorkflowStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<CaseWorkflowRecord> ExecuteAsync(CloseCaseRequest request, CancellationToken cancellationToken)
    {
        CaseLifecycleRules.ValidateClose(request);
        return await _store.CloseAsync(request, cancellationToken);
    }
}

public sealed class ReopenCase(ICaseWorkflowStore store) : IReopenCase
{
    private readonly ICaseWorkflowStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<CaseWorkflowRecord> ExecuteAsync(ReopenCaseRequest request, CancellationToken cancellationToken)
    {
        CaseLifecycleRules.ValidateReopen(request);
        return await _store.ReopenAsync(request, cancellationToken);
    }
}

public sealed class ReturnCaseToEngineer(ICaseWorkflowStore store)
{
    private readonly ICaseWorkflowStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<CaseWorkflowRecord> ExecuteAsync(
        ReturnCaseToEngineerRequest request,
        CancellationToken cancellationToken)
    {
        CaseLifecycleRules.ValidateMutation(request);
        return await _store.ReturnToEngineerAsync(request, cancellationToken);
    }
}

public static class CaseLifecycleRules
{
    /// <summary>
    /// The one sentence for a Case whose report work needs an Engineer and has
    /// none: Create audit's refusal and the report blocker (operator, 15
    /// October 2026, #1213 ruling 3: an Engineer must take it first).
    /// </summary>
    public const string AssignedEngineerRequired = "Report preparation requires an assigned Engineer.";

    public static bool IsTerminal(CaseLifecycleState state) => state is
        CaseLifecycleState.PrincipalCancelled or
        CaseLifecycleState.CollisionEngineersRejected or
        CaseLifecycleState.CreatedInError or
        CaseLifecycleState.SourceEmailUnlinked;

    /// <summary>
    /// Place on Hold runs only from Not ready, Review and With Engineer (the
    /// FRD-13 transition table).
    /// </summary>
    public static bool CanBeHeld(CaseLifecycleState state) => state is
        CaseLifecycleState.NotReady or
        CaseLifecycleState.Review or
        CaseLifecycleState.ReportPreparation;

    /// <summary>
    /// Assign Engineer and Take run only in Review and With Engineer (#1213,
    /// the FRD-13 transition table).
    /// </summary>
    public static bool CanBeAssigned(CaseLifecycleState state) => state is
        CaseLifecycleState.Review or
        CaseLifecycleState.ReportPreparation;

    /// <summary>
    /// Return to work runs from every closed disposition except Created in
    /// error (the FRD-13 transition table).
    /// </summary>
    public static bool CanBeReopened(CaseLifecycleState state) =>
        IsTerminal(state) && state != CaseLifecycleState.CreatedInError;

    // The state refusals of the Case commands. The transition store asks them of
    // the workflow it has read under its lock, before the archive, lease and
    // version checks, and not for a replay of an operation it already committed.

    public static void RequireCanBeHeld(CaseLifecycleState state)
    {
        if (!CanBeHeld(state))
        {
            throw new InvalidOperationException("A case can be held only from Not ready, Review or With Engineer.");
        }
    }

    public static void RequireCanBeAssigned(CaseLifecycleState state)
    {
        if (!CanBeAssigned(state))
        {
            throw new InvalidOperationException(
                "A staff member can be assigned only while the case is in Review or With Engineer.");
        }
    }

    public static void RequireCanStartCaseWork(CaseLifecycleState state, Guid? assignedEngineerId)
    {
        if (state != CaseLifecycleState.Review || assignedEngineerId is null)
        {
            throw new InvalidOperationException(
                "Case work can start only from Review after a staff member is assigned.");
        }
    }

    public static void RequireCanReturnToEngineer(CaseLifecycleState state)
    {
        if (state != CaseLifecycleState.ReportSent)
        {
            throw new InvalidOperationException("A case can return to Engineer only from Report sent.");
        }
    }

    public static void RequireCanBeArchived(CaseLifecycleState state)
    {
        if (!IsTerminal(state))
        {
            throw new InvalidOperationException("Only a terminal case can be archived.");
        }
    }

    /// <summary>What the store read of the Case's custody and external work, asked in Core's order: custody first.</summary>
    public static void RequireArchiveReadiness(CaseArchiveReadiness readiness)
    {
        ArgumentNullException.ThrowIfNull(readiness);
        if (!readiness.IsCustodyConfirmed)
        {
            throw new InvalidOperationException(
                "A case can be archived only after its required custody is confirmed.");
        }

        if (readiness.HasBlockingExternalWork)
        {
            throw new InvalidOperationException(
                "A case cannot be archived while required durable work is incomplete or unrecognized work exists.");
        }
    }

    public static void RequireCanBeReleased(CaseLifecycleState state)
    {
        if (state != CaseLifecycleState.Held)
        {
            throw new InvalidOperationException("Only a held case can be released.");
        }
    }

    /// <summary>
    /// Return to Review is offered on With Engineer and Report sent, where it
    /// sends the Case back for another Engineer handoff (operator, 11 October 2026).
    /// </summary>
    public static void RequireCanReturnToReview(CaseLifecycleState state)
    {
        if (state is not (CaseLifecycleState.ReportPreparation or CaseLifecycleState.ReportSent))
        {
            throw new InvalidOperationException(
                "A case can enter Review only from With Engineer or Report sent.");
        }
    }

    /// <summary>
    /// The current work's report is approved from Report preparation. The
    /// Inspection report of a Case that has its Audit is approved on its own
    /// work, whatever the Case's state (operator, 1 October 2026).
    /// </summary>
    public static void RequireCanApproveReport(CaseWorkSelector work, CaseLifecycleState state)
    {
        if (work == CaseWorkSelector.Current && state != CaseLifecycleState.ReportPreparation)
        {
            throw new InvalidOperationException("A report can be approved only while report preparation is active.");
        }
    }

    /// <summary>
    /// The current work's report evidence is linked from Report preparation.
    /// The Inspection report's evidence, once the Case has its Audit, is linked
    /// on its own work in any state (operator, 1 October 2026).
    /// </summary>
    public static void RequireCanLinkReportEvidence(CaseWorkSelector work, CaseLifecycleState state)
    {
        if (work == CaseWorkSelector.Current && state != CaseLifecycleState.ReportPreparation)
        {
            throw new InvalidOperationException(
                "Exact report-Sent evidence can enter post-report work only from Report preparation.");
        }
    }

    public static void RequireCanUnlinkReportEvidence(CaseLifecycleState state)
    {
        if (IsTerminal(state))
        {
            throw new InvalidOperationException(
                "A closed case must be reasonedly reopened before report-Sent evidence can be unlinked.");
        }

        if (state == CaseLifecycleState.Held)
        {
            throw new InvalidOperationException(
                "A held case must be released before report-Sent evidence can be unlinked.");
        }

        if (state != CaseLifecycleState.ReportPreparation)
        {
            throw new InvalidOperationException(
                "Report-Sent evidence can be unlinked only while report preparation is active.");
        }
    }

    public static void RequireCanBeReopened(CaseLifecycleState state)
    {
        if (!CanBeReopened(state))
        {
            throw new InvalidOperationException("Only a closed case other than Created in error can be reopened.");
        }
    }

    /// <summary>
    /// A case nobody is still working: its report is sent or it ended in a terminal
    /// outcome. The one question asked when a staff account is about to be removed, so an
    /// open case never points at an Engineer who no longer exists.
    /// </summary>
    public static bool IsClosed(CaseLifecycleState state) =>
        state == CaseLifecycleState.ReportSent || IsTerminal(state);

    /// <summary>
    /// Whether a report's Sent item ends the Case's work (operator, 13 October
    /// 2026): it does unless the Case is Inspection + Audit and has no Audit yet.
    /// The Inspection report's send then leaves the Case With Engineer, and only
    /// the Audit report's send, or a closure, takes it off the Cases page.
    /// </summary>
    public static bool SendEndsTheWork(CaseType caseType, bool hasAudit) =>
        caseType != CaseType.InspectionAndAudit || hasAudit;

    /// <summary>
    /// <see cref="SendEndsTheWork(CaseType, bool)"/> asked of the Case as read:
    /// an Inspection + Audit Case has its Audit once it carries the Audit
    /// reference Create audit gives it.
    /// </summary>
    public static bool SendEndsTheWork(CaseWorkflowRecord workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        return SendEndsTheWork(workflow.CaseType, workflow.Identity.AuditReference is not null);
    }

    /// <summary>
    /// The Case's report is sent, for queries and the AI Query response: Report
    /// sent, or With Engineer holding the Inspection report's Sent item while
    /// the Audit is still to come (operator, 13 October 2026: a query stays
    /// raisable in that window).
    /// </summary>
    public static bool ReportIsSent(
        CaseLifecycleState state,
        bool hasSentEvidence,
        CaseType caseType,
        bool hasAudit) =>
        state == CaseLifecycleState.ReportSent
        || (state == CaseLifecycleState.ReportPreparation
            && hasSentEvidence
            && !SendEndsTheWork(caseType, hasAudit));

    /// <summary><see cref="ReportIsSent(CaseLifecycleState, bool, CaseType, bool)"/> asked of the Case as read.</summary>
    public static bool ReportIsSent(CaseWorkflowRecord workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        return ReportIsSent(
            workflow.State,
            workflow.ReportSentEvidence is not null,
            workflow.CaseType,
            workflow.Identity.AuditReference is not null);
    }

    /// <summary>
    /// The terminal states as the names they are persisted under, for store
    /// queries that cannot call <see cref="IsTerminal"/> across a database
    /// boundary. Derived from <see cref="IsTerminal"/> rather than restated, so
    /// the two cannot drift: a state that is terminal here but missing from a
    /// hand-written copy elsewhere is silently non-terminal for whatever that
    /// copy guards.
    /// </summary>
    public static string[] TerminalStateNames() =>
    [
        .. Enum.GetValues<CaseLifecycleState>()
            .Where(IsTerminal)
            .Select(state => state.ToString())
    ];

    public static void ValidateMutation(CaseMutationRequest request) =>
        ValidateMutation(request, requireReason: true);

    /// <summary>
    /// The mutation envelope. A reason is mandatory for every lifecycle action except
    /// where the caller says otherwise: the Case data save records what changed as
    /// its own account, so a typed reason there is optional and bounded, never required.
    /// </summary>
    public static void ValidateMutation(CaseMutationRequest request, bool requireReason)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateCaseAndVersion(request.CaseId, request.ExpectedVersion);
        ValidateActorAndOperation(request.Actor, request.OperationKey);
        if (requireReason)
        {
            RequireText(request.Reason, "A reason is required.", 500, nameof(request));
        }
        else if (request.Reason is { Length: > 500 })
        {
            throw new ArgumentOutOfRangeException(nameof(request), "The value cannot exceed 500 characters.");
        }

        RequireText(
            request.EditLeaseToken,
            "An active edit lease token is required.",
            CaseEditAuthority.LeaseTokenLength,
            nameof(request));
    }


    /// <summary>
    /// Entry to Review is gated on the persisted completeness facts,
    /// re-read by the transition store inside its own transaction. Nothing a
    /// client posts is evidence of readiness, so there is nothing to validate
    /// here beyond the ordinary mutation envelope.
    /// </summary>
    public static void ValidateReturnToReview(ReturnCaseToReviewRequest request) =>
        ValidateMutation(request);

    /// <summary>
    /// A hold's review date is optional; when given it is a Europe/London calendar
    /// date no earlier than the office's today, so a hold cannot be placed with a
    /// review that was already due when it was written.
    /// </summary>
    public static void ValidateHoldReviewDate(DateOnly? reviewOn, DateTimeOffset now)
    {
        if (reviewOn is { } date && date < LondonCalendar.DateAt(now))
        {
            throw new ArgumentOutOfRangeException(
                nameof(reviewOn),
                "The review date must be today or later.");
        }
    }

    /// <summary>
    /// Assign Engineer takes no typed reason (operator, 15 October 2026,
    /// #1213 ruling 1): Core composes the history reason. A null Engineer is
    /// Unassigned; a given one is a real identifier.
    /// </summary>
    public static void ValidateAssignment(
        AssignCaseEngineerRequest request,
        CaseWorkflowConfiguration configuration)
    {
        ValidateMutation(request, requireReason: false);
        if (request.EngineerId == Guid.Empty)
        {
            throw new ArgumentException("An Engineer identifier is required.", nameof(request));
        }

        ValidateWorkflowConfiguration(configuration);
    }

    public static void ValidateReportApproval(RecordCaseReportApprovalRequest request)
    {
        ValidateMutation(request);
        ArgumentNullException.ThrowIfNull(request.Approval);
        if (request.Approval.ApprovalId == Guid.Empty)
        {
            throw new ArgumentException("A report approval identity is required.", nameof(request));
        }

        RequireText(request.Approval.ArtifactIdentity, "An approved artifact identity is required.", 200, nameof(request));
        ValidateSha256(request.Approval.ArtifactSha256, nameof(request));
    }

    public static void ValidateReportEvidence(
        CaseMutationRequest request,
        Guid evidenceId)
    {
        ValidateMutation(request);
        if (evidenceId == Guid.Empty)
        {
            throw new ArgumentException(
                "A stable retained approved-mailbox Sent-evidence identifier is required.",
                nameof(request));
        }
    }

    public static void ValidateClose(CloseCaseRequest request)
    {
        ValidateMutation(request);
        if (!Enum.IsDefined(request.Outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(request), "The closure outcome is invalid.");
        }

        if (request.Outcome == CaseClosureOutcome.CreatedInError)
        {
            throw new InvalidOperationException(
                "Created in error requires the atomic corrected-principal replacement action.");
        }

        if (request.Outcome == CaseClosureOutcome.SourceEmailUnlinked)
        {
            throw new InvalidOperationException(
                "Cancelling on unlink requires unlinking the email that created the case.");
        }
    }

    public static void RequireClosureIsAllowed(CaseWorkflowRecord current, CloseCaseRequest request) =>
        RequireClosureIsAllowed(current.State);

    public static void RequireClosureIsAllowed(CaseLifecycleState state)
    {
        if (state == CaseLifecycleState.ReportSent)
        {
            throw new InvalidOperationException("A case can be closed only before its report is sent.");
        }

        if (IsTerminal(state))
        {
            throw new InvalidOperationException("A closed case cannot be closed again.");
        }
    }

    public static void ValidateReopen(ReopenCaseRequest request)
    {
        ValidateMutation(request);
        if (request.Destination is not { } destination || !Enum.IsDefined(destination))
        {
            throw new ArgumentOutOfRangeException(nameof(request), "The reopen destination is invalid.");
        }
    }

    /// <summary>
    /// A Case reopened to With Engineer with no Engineer lands Unassigned, for
    /// an Engineer to take (operator, 15 October 2026, #1213 ruling 3).
    /// </summary>
    public static void RequireReopenDestinationIsAllowed(
        CaseReopenDestination? destination,
        bool hasReportSentEvidence)
    {
        if (destination == CaseReopenDestination.ReportSent && !hasReportSentEvidence)
        {
            throw new InvalidOperationException(
                "Report sent requires retained exact report-sent evidence from an approved mailbox.");
        }
    }

    private static void ValidateWorkflowConfiguration(CaseWorkflowConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        RequireText(configuration.PolicyKey, "A workflow policy key is required.", 100, nameof(configuration));
        if (configuration.PolicyVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(configuration), "The workflow policy version must be positive.");
        }
    }

    /// <summary>
    /// The one Review readiness rule, applied to the persisted
    /// completeness facts a transition store reads inside its own transaction.
    /// A caller cannot reach it with a claim of its own.
    /// </summary>
    public static void RequireReviewReadiness(
        Guid caseId,
        bool instructionComplete,
        bool imagesComplete)
    {
        if (!instructionComplete || !imagesComplete)
        {
            throw new CaseReviewReadinessException(caseId);
        }
    }

    private static void ValidateCaseAndVersion(Guid caseId, long expectedVersion)
    {
        if (caseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(caseId));
        }

        if (expectedVersion < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedVersion), "The expected version cannot be negative.");
        }
    }

    private static void ValidateActorAndOperation(ActionActor actor, string operationKey)
    {
        ValidateActor(actor);
        RequireText(operationKey, "An operation key is required.", 100, nameof(operationKey));
    }

    private static void ValidateActor(ActionActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
    }

    private static void ValidateSha256(string value, string parameterName)
    {
        RequireText(value, "A SHA-256 value is required.", 64, parameterName);
        if (value.Length != 64 || value.Any(character => !char.IsAsciiHexDigit(character)))
        {
            throw new ArgumentException("The value must be a SHA-256 hexadecimal value.", parameterName);
        }
    }

    private static void RequireText(string value, string message, int maximumLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(message, parameterName);
        }

        if (value.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(parameterName, $"The value cannot exceed {maximumLength} characters.");
        }
    }
}
