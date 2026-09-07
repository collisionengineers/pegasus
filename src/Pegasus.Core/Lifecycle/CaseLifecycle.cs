using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Lifecycle;


public sealed class PutCaseOnHold(ICaseWorkflowStore store) : IPutCaseOnHold
{
    private readonly ICaseWorkflowStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<CaseWorkflowRecord> ExecuteAsync(PutCaseOnHoldRequest request, CancellationToken cancellationToken)
    {
        CaseLifecycleRules.ValidateMutation(request);
        var current = await CaseLifecycleRules.GetRequiredAsync(_store, request.CaseId, cancellationToken);
        if ((current.State == CaseLifecycleState.Held || CaseLifecycleRules.IsTerminal(current.State))
            && !await _store.HasOperationAsync(request.CaseId, request.OperationKey, cancellationToken))
        {
            throw new InvalidOperationException("Only an open case can be held.");
        }

        return await _store.HoldAsync(request, cancellationToken);
    }
}

public sealed class ReleaseCaseHold(ICaseWorkflowStore store) : IReleaseCaseHold
{
    private readonly ICaseWorkflowStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<CaseWorkflowRecord> ExecuteAsync(CaseMutationRequest request, CancellationToken cancellationToken)
    {
        CaseLifecycleRules.ValidateMutation(request);
        var current = await CaseLifecycleRules.GetRequiredAsync(_store, request.CaseId, cancellationToken);
        if (current.State != CaseLifecycleState.Held
            && !await _store.HasOperationAsync(request.CaseId, request.OperationKey, cancellationToken))
        {
            throw new InvalidOperationException("Only a held case can be released.");
        }

        return await _store.ReleaseHoldAsync(request, cancellationToken);
    }
}

public sealed class ReturnCaseToReview(ICaseWorkflowStore store) : IReturnCaseToReview
{
    private readonly ICaseWorkflowStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<CaseWorkflowRecord> ExecuteAsync(
        ReturnCaseToReviewRequest request,
        CancellationToken cancellationToken)
    {
        CaseLifecycleRules.ValidateReturnToReview(request);
        var current = await CaseLifecycleRules.GetRequiredAsync(_store, request.CaseId, cancellationToken);
        if (current.State != CaseLifecycleState.NotReady
            && !await _store.HasOperationAsync(request.CaseId, request.OperationKey, cancellationToken))
        {
            throw new InvalidOperationException("A case can enter Review only from Not ready.");
        }

        return await _store.ReturnToReviewAsync(request, cancellationToken);
    }
}

public sealed class AssignCaseEngineer(
    ICaseWorkflowStore store,
    ICaseWorkflowConfiguration configuration,
    ICaseEngineerEligibility eligibility,
    IStaffAccountQueries staffAccounts) : IAssignCaseEngineer
{
    private readonly ICaseWorkflowStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly ICaseWorkflowConfiguration _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    private readonly ICaseEngineerEligibility _eligibility = eligibility ?? throw new ArgumentNullException(nameof(eligibility));
    private readonly IStaffAccountQueries _staffAccounts = staffAccounts ?? throw new ArgumentNullException(nameof(staffAccounts));

    public async Task<CaseWorkflowRecord> ExecuteAsync(
        AssignCaseEngineerRequest request,
        CancellationToken cancellationToken)
    {
        var workflowConfiguration = await _configuration.GetCurrentAsync(cancellationToken);
        CaseLifecycleRules.ValidateAssignment(request, workflowConfiguration);
        var current = await CaseLifecycleRules.GetRequiredAsync(_store, request.CaseId, cancellationToken);
        var isReplay = await _store.HasOperationAsync(
            request.CaseId,
            request.OperationKey,
            cancellationToken);
        if (current.State != CaseLifecycleState.Review && !isReplay)
        {
            throw new InvalidOperationException("An Engineer can be assigned only while the case is in Review.");
        }

        Guid? signOffEngineerId = null;
        if (!isReplay)
        {
            await CaseEngineerEligibilityPolicy.RequireEligibleAsync(
                _eligibility,
                request.EngineerId,
                cancellationToken);

            var profiles = await _staffAccounts.ListSignOffEngineersAsync(cancellationToken);
            signOffEngineerId = CaseSignOffEngineerResolver.Resolve(
                current.SignOffEngineerId,
                request.EngineerId,
                profiles)?.StaffId;
        }

        return await _store.AssignEngineerAsync(request, signOffEngineerId, cancellationToken);
    }
}

public sealed class SetCaseSignOffEngineer(
    ICaseWorkflowStore store,
    IStaffAccountQueries staffAccounts) : ISetCaseSignOffEngineer
{
    private readonly ICaseWorkflowStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly IStaffAccountQueries _staffAccounts = staffAccounts ?? throw new ArgumentNullException(nameof(staffAccounts));

    public async Task<CaseWorkflowRecord> ExecuteAsync(
        SetCaseSignOffEngineerRequest request,
        CancellationToken cancellationToken)
    {
        CaseLifecycleRules.ValidateMutation(request);
        if (request.SignOffEngineerId == Guid.Empty)
        {
            throw new ArgumentException("A Sign-off Engineer identifier is required.", nameof(request));
        }

        var current = await CaseLifecycleRules.GetRequiredAsync(_store, request.CaseId, cancellationToken);
        var isReplay = await _store.HasOperationAsync(request.CaseId, request.OperationKey, cancellationToken);
        if (current.State is not (CaseLifecycleState.Review
                or CaseLifecycleState.ReportPreparation
                or CaseLifecycleState.PostReport)
            && !isReplay)
        {
            throw new InvalidOperationException(
                "A Sign-off Engineer can be selected only while the case is in Review or With Engineer.");
        }

        if (!isReplay)
        {
            CaseSignOffEngineerResolver.RequireEligible(
                await _staffAccounts.ListSignOffEngineersAsync(cancellationToken),
                request.SignOffEngineerId);
        }

        return await _store.SetSignOffEngineerAsync(request, cancellationToken);
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
    ICaseEngineerEligibility eligibility) : IStartCaseWork
{
    private readonly ICaseWorkflowStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly ICaseEngineerEligibility _eligibility = eligibility ?? throw new ArgumentNullException(nameof(eligibility));

    public async Task<CaseWorkflowRecord> ExecuteAsync(CaseMutationRequest request, CancellationToken cancellationToken)
    {
        CaseLifecycleRules.ValidateMutation(request);
        var current = await CaseLifecycleRules.GetRequiredAsync(_store, request.CaseId, cancellationToken);
        var isReplay = await _store.HasOperationAsync(
            request.CaseId,
            request.OperationKey,
            cancellationToken);
        if (!isReplay)
        {
            await CaseEngineerEligibilityPolicy.RequireStartCaseWorkAsync(
                _eligibility,
                current.State,
                current.AssignedEngineerId,
                cancellationToken);
        }

        return await _store.ChangeStateAsync(request, CaseLifecycleState.ReportPreparation, cancellationToken);
    }
}

public static class CaseEngineerEligibilityPolicy
{
    public static async Task RequireStartCaseWorkAsync(
        ICaseEngineerEligibility source,
        CaseLifecycleState state,
        Guid? assignedEngineerId,
        CancellationToken cancellationToken)
    {
        if (state != CaseLifecycleState.Review || assignedEngineerId is null)
        {
            throw new InvalidOperationException(
                "Case work can start only from Review after an Engineer is assigned.");
        }

        await RequireEligibleAsync(source, assignedEngineerId.Value, cancellationToken);
    }

    public static async Task RequireEligibleAsync(
        ICaseEngineerEligibility source,
        Guid engineerId,
        CancellationToken cancellationToken)
    {
        var eligibility = await source.GetAsync(engineerId, cancellationToken);
        if (!eligibility.AccountExists)
        {
            throw new InvalidOperationException("The assigned Engineer account does not exist.");
        }

        if (!eligibility.IsEnabled)
        {
            throw new InvalidOperationException("The assigned Engineer account is disabled.");
        }

        if (!eligibility.HasEngineerRole)
        {
            throw new InvalidOperationException(
                "The assigned staff account does not hold the Engineer role.");
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
        var current = await CaseLifecycleRules.GetRequiredAsync(_store, request.CaseId, cancellationToken);
        if (current.State != CaseLifecycleState.ReportPreparation
            && !await _store.HasOperationAsync(request.CaseId, request.OperationKey, cancellationToken))
        {
            throw new InvalidOperationException("A report can be approved only while report preparation is active.");
        }

        return await _store.RecordReportApprovalAsync(request, cancellationToken);
    }
}

public sealed class LinkReportEvidence(ICaseWorkflowStore store) : ILinkReportEvidence
{
    private readonly ICaseWorkflowStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<CaseWorkflowRecord> ExecuteAsync(
        LinkReportEvidenceRequest request,
        CancellationToken cancellationToken)
    {
        CaseLifecycleRules.ValidateReportEvidence(request, request.EvidenceId);
        var current = await CaseLifecycleRules.GetRequiredAsync(
            _store,
            request.CaseId,
            cancellationToken);
        if (current.State != CaseLifecycleState.ReportPreparation
            && !await _store.HasOperationAsync(
                request.CaseId,
                request.OperationKey,
                cancellationToken))
        {
            throw new InvalidOperationException(
                "Exact report-Sent evidence can enter post-report work only from Report preparation.");
        }

        return await _store.LinkReportEvidenceAsync(request, cancellationToken);
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
                || link.State != CaseLifecycleState.PostReport
                || link.Version < 0)
            {
                throw new InvalidDataException(
                    "The automatic report-evidence store returned an invalid committed link.");
            }

            return;
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
        var current = await CaseLifecycleRules.GetRequiredAsync(
            _store,
            request.CaseId,
            cancellationToken);
        var isReplay = await _store.HasOperationAsync(
            request.CaseId,
            request.OperationKey,
            cancellationToken);
        if (!isReplay && current.Archive is not null)
        {
            throw new CaseArchivedException(request.CaseId);
        }
        if (!isReplay && CaseLifecycleRules.IsTerminal(current.State))
        {
            throw new InvalidOperationException(
                "A closed case must be reasonedly reopened before report-Sent evidence can be unlinked.");
        }
        if (!isReplay && current.State == CaseLifecycleState.Held)
        {
            throw new InvalidOperationException(
                "A held case must be released before report-Sent evidence can be unlinked.");
        }
        if (!isReplay && current.State != CaseLifecycleState.ReportPreparation)
        {
            throw new InvalidOperationException(
                "Report-Sent evidence can be unlinked only while report preparation is active.");
        }
        if (!isReplay && current.ReportSentEvidence?.EvidenceId != request.EvidenceId)
        {
            throw new InvalidOperationException(
                "The selected report-Sent evidence is not the case's current association.");
        }

        return await _store.UnlinkReportEvidenceAsync(request, cancellationToken);
    }
}

public sealed class CloseCase(ICaseWorkflowStore store) : ICloseCase
{
    private readonly ICaseWorkflowStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<CaseWorkflowRecord> ExecuteAsync(CloseCaseRequest request, CancellationToken cancellationToken)
    {
        CaseLifecycleRules.ValidateClose(request);
        var current = await CaseLifecycleRules.GetRequiredAsync(_store, request.CaseId, cancellationToken);
        if (CaseLifecycleRules.IsTerminal(current.State)
            && await _store.HasOperationAsync(request.CaseId, request.OperationKey, cancellationToken))
        {
            return await _store.CloseAsync(request, cancellationToken);
        }
        CaseLifecycleRules.RequireClosureIsAllowed(current, request);
        return await _store.CloseAsync(request, cancellationToken);
    }
}

public sealed class ReopenCase(ICaseWorkflowStore store) : IReopenCase
{
    private readonly ICaseWorkflowStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<CaseWorkflowRecord> ExecuteAsync(ReopenCaseRequest request, CancellationToken cancellationToken)
    {
        CaseLifecycleRules.ValidateReopen(request);
        var current = await CaseLifecycleRules.GetRequiredAsync(_store, request.CaseId, cancellationToken);
        if ((!CaseLifecycleRules.IsTerminal(current.State) || current.State == CaseLifecycleState.CreatedInError)
            && !await _store.HasOperationAsync(request.CaseId, request.OperationKey, cancellationToken))
        {
            throw new InvalidOperationException("Only a closed case other than Created in error can be reopened.");
        }

        CaseLifecycleRules.RequireReopenDestinationIsAllowed(current, request);

        return await _store.ReopenAsync(request, cancellationToken);
    }
}

public static class CaseLifecycleRules
{
    public static async Task<CaseWorkflowRecord> GetRequiredAsync(
        ICaseWorkflowQueries queries,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        if (caseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(caseId));
        }

        return await queries.GetAsync(caseId, cancellationToken)
            ?? throw new KeyNotFoundException($"Case '{caseId}' was not found.");
    }

    public static bool IsTerminal(CaseLifecycleState state) => state is
        CaseLifecycleState.PostReportComplete or
        CaseLifecycleState.ProviderCancelled or
        CaseLifecycleState.CollisionEngineersRejected or
        CaseLifecycleState.CreatedInError or
        CaseLifecycleState.SourceEmailUnlinked;

    /// <summary>
    /// The terminal states as the names they are persisted under, for store
    /// queries that cannot call <see cref="IsTerminal"/> across a database
    /// boundary. Derived from <see cref="IsTerminal"/> rather than restated, so
    /// the two cannot drift: a state that is terminal here but missing from a
    /// hand-written copy elsewhere is silently non-terminal for whatever that
    /// copy guards (INTK-029).
    /// </summary>
    public static string[] TerminalStateNames() =>
    [
        .. Enum.GetValues<CaseLifecycleState>()
            .Where(IsTerminal)
            .Select(state => state.ToString())
    ];

    public static void ValidateMutation(CaseMutationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateCaseAndVersion(request.CaseId, request.ExpectedVersion);
        ValidateActorAndOperation(request.Actor, request.OperationKey);
        RequireText(request.Reason, "A reason is required.", 500, nameof(request));
        RequireText(
            request.EditLeaseToken,
            "An active edit lease token is required.",
            CaseEditAuthority.LeaseTokenLength,
            nameof(request));
    }


    /// <summary>
    /// CASE-046: entry to Review is gated on the persisted completeness facts,
    /// re-read by the transition store inside its own transaction. Nothing a
    /// client posts is evidence of readiness, so there is nothing to validate
    /// here beyond the ordinary mutation envelope.
    /// </summary>
    public static void ValidateReturnToReview(ReturnCaseToReviewRequest request) =>
        ValidateMutation(request);

    public static void ValidateAssignment(
        AssignCaseEngineerRequest request,
        CaseWorkflowConfiguration configuration)
    {
        ValidateMutation(request);
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

    public static void RequireClosureIsAllowed(CaseWorkflowRecord current, CloseCaseRequest request)
    {
        if (IsTerminal(current.State))
        {
            throw new InvalidOperationException("A closed case cannot be closed again.");
        }

        if (request.Outcome == CaseClosureOutcome.PostReportComplete && current.State != CaseLifecycleState.PostReport)
        {
            throw new InvalidOperationException("Post-report completion is available only after exact report-sent evidence enters post-report work.");
        }
    }

    public static void ValidateReopen(ReopenCaseRequest request)
    {
        ValidateMutation(request);
        if (!Enum.IsDefined(request.Destination))
        {
            throw new ArgumentOutOfRangeException(nameof(request), "The reopen destination is invalid.");
        }

    }

    public static void RequireReopenDestinationIsAllowed(
        CaseWorkflowRecord current,
        ReopenCaseRequest request)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(request);
        if (request.Destination == CaseReopenDestination.ReportPreparation
            && current.AssignedEngineerId is null)
        {
            throw new InvalidOperationException("Report preparation requires an assigned Engineer.");
        }

        if (request.Destination == CaseReopenDestination.PostReport
            && current.ReportSentEvidence is null)
        {
            throw new InvalidOperationException(
                "Post report requires retained exact report-sent evidence from an approved mailbox.");
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
    /// CASE-046: the one Review readiness rule, applied to the persisted
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
