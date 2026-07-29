using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Lifecycle;


public sealed class PutCaseOnHold(ICaseWorkflowStore store) : IPutCaseOnHold
{
    private readonly ICaseWorkflowStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<CaseWorkflowRecord> ExecuteAsync(PutCaseOnHoldRequest request, CancellationToken cancellationToken)
    {
        CaseLifecycleRules.ValidateHold(request);
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
            throw new InvalidOperationException("Only a held case can be released to Not ready.");
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
    ICaseWorkflowConfiguration configuration) : IAssignCaseEngineer
{
    private readonly ICaseWorkflowStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly ICaseWorkflowConfiguration _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

    public async Task<CaseWorkflowRecord> ExecuteAsync(
        AssignCaseEngineerRequest request,
        CancellationToken cancellationToken)
    {
        CaseLifecycleRules.ValidateAssignment(request, _configuration.GetCurrent());
        var current = await CaseLifecycleRules.GetRequiredAsync(_store, request.CaseId, cancellationToken);
        if (current.State != CaseLifecycleState.Review
            && !await _store.HasOperationAsync(request.CaseId, request.OperationKey, cancellationToken))
        {
            throw new InvalidOperationException("An Engineer can be assigned only while the case is in Review.");
        }

        return await _store.AssignEngineerAsync(request, cancellationToken);
    }
}

public sealed class StartCaseWork(ICaseWorkflowStore store) : IStartCaseWork
{
    private readonly ICaseWorkflowStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<CaseWorkflowRecord> ExecuteAsync(CaseMutationRequest request, CancellationToken cancellationToken)
    {
        CaseLifecycleRules.ValidateMutation(request);
        var current = await CaseLifecycleRules.GetRequiredAsync(_store, request.CaseId, cancellationToken);
        if ((current.State != CaseLifecycleState.Review || current.AssignedEngineerId is null)
            && !await _store.HasOperationAsync(request.CaseId, request.OperationKey, cancellationToken))
        {
            throw new InvalidOperationException("Case work can start only from Review after an Engineer is assigned.");
        }

        return await _store.ChangeStateAsync(request, CaseLifecycleState.Active, cancellationToken);
    }
}

public sealed class BeginCaseReportPreparation(ICaseWorkflowStore store) : IBeginCaseReportPreparation
{
    private readonly ICaseWorkflowStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<CaseWorkflowRecord> ExecuteAsync(CaseMutationRequest request, CancellationToken cancellationToken)
    {
        CaseLifecycleRules.ValidateMutation(request);
        var current = await CaseLifecycleRules.GetRequiredAsync(_store, request.CaseId, cancellationToken);
        if (current.State != CaseLifecycleState.Active
            && !await _store.HasOperationAsync(request.CaseId, request.OperationKey, cancellationToken))
        {
            throw new InvalidOperationException("Report preparation can begin only during active case work.");
        }

        return await _store.ChangeStateAsync(request, CaseLifecycleState.ReportPreparation, cancellationToken);
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

public sealed class RecordCaseReportSent(ICaseWorkflowStore store) : IRecordCaseReportSent
{
    private readonly ICaseWorkflowStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<CaseWorkflowRecord> ExecuteAsync(
        RecordCaseReportSentRequest request,
        CancellationToken cancellationToken)
    {
        CaseLifecycleRules.ValidateReportSent(request);
        var current = await CaseLifecycleRules.GetRequiredAsync(_store, request.CaseId, cancellationToken);
        if ((current.State != CaseLifecycleState.ReportPreparation || current.ReportApproval is null)
            && !await _store.HasOperationAsync(request.CaseId, request.OperationKey, cancellationToken))
        {
            throw new InvalidOperationException(
                "Exact report-sent evidence can enter post-report work only after report approval.");
        }

        return await _store.RecordReportSentAsync(request, cancellationToken);
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

public sealed class ReopenCase(
    ICaseWorkflowStore store,
    ICaseWorkflowConfiguration configuration) : IReopenCase
{
    private readonly ICaseWorkflowStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly ICaseWorkflowConfiguration _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

    public async Task<CaseWorkflowRecord> ExecuteAsync(ReopenCaseRequest request, CancellationToken cancellationToken)
    {
        CaseLifecycleRules.ValidateReopen(request, _configuration.GetCurrent());
        var current = await CaseLifecycleRules.GetRequiredAsync(_store, request.CaseId, cancellationToken);
        if ((!CaseLifecycleRules.IsTerminal(current.State) || current.State == CaseLifecycleState.CreatedInError)
            && !await _store.HasOperationAsync(request.CaseId, request.OperationKey, cancellationToken))
        {
            throw new InvalidOperationException("Only a closed case other than Created in error can be reopened.");
        }

        if (request.Destination is CaseReopenDestination.Active or CaseReopenDestination.ReportPreparation
            && current.AssignedEngineerId is null)
        {
            throw new InvalidOperationException("The selected reopen destination requires an assigned Engineer.");
        }

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
        CaseLifecycleState.CreatedInError;

    public static void ValidateMutation(CaseMutationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateCaseAndVersion(request.CaseId, request.ExpectedVersion);
        ValidateActorAndOperation(request.Actor, request.OperationKey);
        RequireText(request.Reason, "A reason is required.", 500, nameof(request));
        RequireText(request.EditLeaseToken, "An active edit lease token is required.", 128, nameof(request));
    }

    public static void ValidateHold(PutCaseOnHoldRequest request)
    {
        ValidateMutation(request);
        if (request.HeldAtUtc == default)
        {
            throw new ArgumentException("The hold time is required.", nameof(request));
        }
    }

    public static void ValidateReturnToReview(ReturnCaseToReviewRequest request)
    {
        ValidateMutation(request);
        ValidateReviewReadiness(request.Readiness);
    }

    public static void ValidateAssignment(
        AssignCaseEngineerRequest request,
        CaseWorkflowConfiguration configuration)
    {
        ValidateMutation(request);
        if (request.EngineerId == Guid.Empty)
        {
            throw new ArgumentException("An Engineer identifier is required.", nameof(request));
        }

        ValidateReadiness(request.Readiness, configuration);
    }

    public static void ValidateReportApproval(RecordCaseReportApprovalRequest request)
    {
        ValidateMutation(request);
        ArgumentNullException.ThrowIfNull(request.Approval);
        if (request.Approval.ApprovalId == Guid.Empty)
        {
            throw new ArgumentException("A report approval identity is required.", nameof(request));
        }

        ValidateActor(request.Approval.ApprovedBy);
        if (!string.Equals(request.Actor.SubjectId, request.Approval.ApprovedBy.SubjectId, StringComparison.Ordinal))
        {
            throw new ArgumentException("The report approval must be recorded by its approving actor.", nameof(request));
        }

        RequireText(request.Approval.ArtifactIdentity, "An approved artifact identity is required.", 200, nameof(request));
        ValidateSha256(request.Approval.ArtifactSha256, nameof(request));
        if (request.Approval.ApprovedAtUtc == default)
        {
            throw new ArgumentException("The approval time is required.", nameof(request));
        }
    }

    public static void ValidateReportSent(RecordCaseReportSentRequest request)
    {
        ValidateMutation(request);
        ArgumentNullException.ThrowIfNull(request.Evidence);
        if (request.Evidence.EvidenceId == Guid.Empty)
        {
            throw new ArgumentException("Exact report-sent evidence is required.", nameof(request));
        }

        ValidateActor(request.Evidence.LinkedBy);
        if (!string.Equals(request.Actor.SubjectId, request.Evidence.LinkedBy.SubjectId, StringComparison.Ordinal))
        {
            throw new ArgumentException("The report-sent evidence must be linked by the acting staff member.", nameof(request));
        }

        RequireText(request.Evidence.MailboxIdentity, "An approved mailbox identity is required.", 200, nameof(request));
        RequireText(request.Evidence.SentFolderIdentity, "A Sent-folder identity is required.", 200, nameof(request));
        RequireText(request.Evidence.ImmutableItemIdentity, "An immutable Sent-item identity is required.", 500, nameof(request));
        RequireText(request.Evidence.ConversationIdentity, "A conversation identity is required.", 500, nameof(request));
        RequireText(request.Evidence.ReplyChainIdentity, "A reply-chain identity is required.", 500, nameof(request));
        if (request.Evidence.SentAtUtc == default || request.Evidence.LinkedAtUtc == default)
        {
            throw new ArgumentException("Sent and link times are required.", nameof(request));
        }

        if (request.Evidence.LinkedAtUtc < request.Evidence.SentAtUtc)
        {
            throw new ArgumentException("Report-sent evidence cannot be linked before it was sent.", nameof(request));
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
            if (request.ReplacementCaseId is not { } replacementCaseId || replacementCaseId == Guid.Empty)
            {
                throw new ArgumentException("Created in error requires a linked replacement case.", nameof(request));
            }

            if (replacementCaseId == request.CaseId)
            {
                throw new ArgumentException("The linked replacement must be a different case.", nameof(request));
            }
        }
        else if (request.ReplacementCaseId is not null)
        {
            throw new ArgumentException("Only Created in error may link a replacement case.", nameof(request));
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

    public static void ValidateReopen(ReopenCaseRequest request, CaseWorkflowConfiguration configuration)
    {
        ValidateMutation(request);
        if (!Enum.IsDefined(request.Destination))
        {
            throw new ArgumentOutOfRangeException(nameof(request), "The reopen destination is invalid.");
        }

        if (request.Destination == CaseReopenDestination.Review)
        {
            if (request.Readiness is null)
            {
                throw new ArgumentException("The selected reopen destination requires readiness evidence.", nameof(request));
            }

            ValidateReviewReadiness(request.Readiness);
        }
        else if (request.Destination == CaseReopenDestination.Active)
        {
            if (request.Readiness is null)
            {
                throw new ArgumentException("The selected reopen destination requires readiness evidence.", nameof(request));
            }

            ValidateReadiness(request.Readiness, configuration);
        }
        else if (request.Readiness is not null)
        {
            throw new ArgumentException("Readiness evidence is accepted only for a Review or Active reopen destination.", nameof(request));
        }
    }

    private static void ValidateReviewReadiness(CaseReadinessEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        RequireText(evidence.EvidenceReference, "Readiness evidence is required.", 200, nameof(evidence));
        if ((!evidence.InstructionsComplete || !evidence.ImagesComplete)
            && (!evidence.InstructionsReviewedByStaff || !evidence.ImagesReviewedByStaff))
        {
            throw new InvalidOperationException(
                "Review requires complete instructions and images or explicit staff confirmation of both.");
        }
    }

    private static void ValidateReadiness(CaseReadinessEvidence evidence, CaseWorkflowConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(configuration);
        RequireText(configuration.PolicyKey, "A workflow policy key is required.", 100, nameof(configuration));
        if (configuration.PolicyVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(configuration), "The workflow policy version must be positive.");
        }

        RequireText(evidence.EvidenceReference, "Readiness evidence is required.", 200, nameof(evidence));
        if (configuration.RequireCompleteInstructionsBeforeEngineerAssignment && !evidence.InstructionsComplete
            || configuration.RequireCompleteImagesBeforeEngineerAssignment && !evidence.ImagesComplete
            || configuration.RequireStaffInstructionReviewBeforeEngineerAssignment && !evidence.InstructionsReviewedByStaff
            || configuration.RequireStaffImageReviewBeforeEngineerAssignment && !evidence.ImagesReviewedByStaff)
        {
            throw new InvalidOperationException("The configured instruction/image readiness gates are not satisfied.");
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
