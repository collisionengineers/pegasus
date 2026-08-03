using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.AiWork;

/// <summary>
/// Policy for the Send to AI work-request lifecycle. Creation is a staff act
/// (the Automation actor never sends work to itself); duplicate sends replay
/// idempotently; every transition is validated against the legal state graph;
/// and closing a request never touches case content.
/// </summary>
public static class AiWorkPolicy
{
    public const string CapabilityScope = "assessment_review";
    public const int SchemaVersion = 1;
    public const int MaximumInstructionLength = 500;

    public static void ValidateCreate(CreateAiWorkRequestCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.CaseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(command));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(command.CaseReference);
        if (command.CaseVersion < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(command),
                "The captured case version cannot be negative.");
        }
        RequireStaffSender(command.Actor);
        ValidateOperationKey(command.OperationKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Instruction);
        if (command.Instruction.Trim().Length > MaximumInstructionLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(command),
                $"The instruction cannot exceed {MaximumInstructionLength} characters.");
        }
        if (command.Expiry <= TimeSpan.Zero || command.Expiry > TimeSpan.FromDays(7))
        {
            throw new ArgumentOutOfRangeException(
                nameof(command),
                "The work-request expiry must be between one second and seven days.");
        }
    }

    public static void ValidateTransition(AiWorkRequestTransition transition)
    {
        ArgumentNullException.ThrowIfNull(transition);
        if (transition.RequestId == Guid.Empty)
        {
            throw new ArgumentException("A work-request identifier is required.", nameof(transition));
        }
        if (transition.ExpectedVersion < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(transition),
                "The expected work-request version cannot be negative.");
        }
        RequireStaffSender(transition.Actor);
        ValidateOperationKey(transition.OperationKey);
        if (!Enum.IsDefined(transition.TargetState)
            || transition.TargetState == AiWorkRequestState.Created)
        {
            throw new ArgumentException(
                "The work-request transition target is invalid.",
                nameof(transition));
        }
        if (transition.TargetState == AiWorkRequestState.Cancelled
            && string.IsNullOrWhiteSpace(transition.Reason))
        {
            throw new ArgumentException(
                "Cancelling a Send to AI request requires a reason.",
                nameof(transition));
        }
        if (transition.Reason is { Length: > 500 })
        {
            throw new ArgumentOutOfRangeException(
                nameof(transition),
                "A work-request transition reason cannot exceed 500 characters.");
        }
    }

    public static bool IsLegalTransition(AiWorkRequestState from, AiWorkRequestState to) =>
        (from, to) switch
        {
            (AiWorkRequestState.Created, AiWorkRequestState.HandedOff) => true,
            (AiWorkRequestState.Created, AiWorkRequestState.Failed) => true,
            (AiWorkRequestState.Created, AiWorkRequestState.Cancelled) => true,
            (AiWorkRequestState.Created, AiWorkRequestState.Expired) => true,
            (AiWorkRequestState.HandedOff, AiWorkRequestState.Completed) => true,
            (AiWorkRequestState.HandedOff, AiWorkRequestState.Failed) => true,
            (AiWorkRequestState.HandedOff, AiWorkRequestState.Cancelled) => true,
            (AiWorkRequestState.HandedOff, AiWorkRequestState.Expired) => true,
            _ => false
        };

    public static bool IsEligibleCaseState(CaseLifecycleState state) =>
        state is CaseLifecycleState.NotReady
            or CaseLifecycleState.Review
            or CaseLifecycleState.ReportPreparation;

    private static void RequireStaffSender(ActionActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        if (actor.Kind != ActorKind.Staff)
        {
            throw new InvalidOperationException(
                "Send to AI work requests are staff actions.");
        }
    }

    private static void ValidateOperationKey(string operationKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);
        if (operationKey.Length > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(operationKey),
                "The operation key cannot exceed 100 characters.");
        }
    }
}

/// <summary>
/// Creates the tracking record, hands the pointer to the channel, and
/// records the outcome honestly: HandedOff means the connector accepted and
/// forwarded the pointer — never that Claude read it. A refused or
/// unreachable channel is a visible Failed request with the case unchanged.
/// </summary>
public sealed class SendCaseToAi(
    ICaseDataQueries caseData,
    IAiWorkRequestStore store,
    IAiHandOffTransport transport,
    ISendToAiControl control,
    TimeProvider timeProvider) : ISendCaseToAi
{
    private readonly ICaseDataQueries _caseData =
        caseData ?? throw new ArgumentNullException(nameof(caseData));
    private readonly IAiWorkRequestStore _store =
        store ?? throw new ArgumentNullException(nameof(store));
    private readonly IAiHandOffTransport _transport =
        transport ?? throw new ArgumentNullException(nameof(transport));
    private readonly ISendToAiControl _control =
        control ?? throw new ArgumentNullException(nameof(control));
    private readonly TimeProvider _timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public static readonly TimeSpan DefaultExpiry = TimeSpan.FromHours(24);

    public async Task<SendCaseToAiResult> ExecuteAsync(
        SendCaseToAiRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reasons = new List<string>();
        if (!await _control.IsEnabledAsync(cancellationToken))
        {
            reasons.Add("Sending to AI is disabled by an Administrator.");
            return new(SendCaseToAiOutcome.NotEligible, null, reasons);
        }

        var projection = await _caseData.GetAsync(request.CaseId, cancellationToken);
        if (projection is null)
        {
            reasons.Add("The case was not found.");
            return new(SendCaseToAiOutcome.NotEligible, null, reasons);
        }
        if (!AiWorkPolicy.IsEligibleCaseState(projection.State))
        {
            reasons.Add("The case is not in a state that accepts assessment work.");
            return new(SendCaseToAiOutcome.NotEligible, null, reasons);
        }

        var latest = await _store.GetLatestForCaseAsync(request.CaseId, cancellationToken);
        if (latest is not null
            && !AiWorkRequestStates.IsTerminal(latest.State)
            && latest.ExpiresAtUtc > _timeProvider.GetUtcNow())
        {
            reasons.Add("A Send to AI request is already in flight for this case.");
            return new(SendCaseToAiOutcome.NotEligible, latest, reasons);
        }

        var created = await _store.CreateAsync(
            new(
                request.CaseId,
                projection.Identity.Reference,
                projection.Version,
                request.Actor,
                request.OperationKey,
                request.Instruction,
                DefaultExpiry),
            cancellationToken);
        if (created.State != AiWorkRequestState.Created)
        {
            // Idempotent replay of an operation key that already progressed.
            IReadOnlyList<string> replayReasons = created.ClosureReason is { } closureReason
                ? [closureReason]
                : [];
            return new(
                created.State == AiWorkRequestState.Failed
                    ? SendCaseToAiOutcome.Failed
                    : SendCaseToAiOutcome.HandedOff,
                created,
                replayReasons);
        }

        var handOff = await _transport.HandOffAsync(
            new(
                created.RequestId.ToString("D"),
                created.CaseReference,
                created.Instruction,
                AiWorkPolicy.SchemaVersion),
            cancellationToken);
        if (handOff.Kind == AiHandOffOutcomeKind.Accepted)
        {
            var handedOff = await _store.TransitionAsync(
                new(
                    created.RequestId,
                    created.Version,
                    AiWorkRequestState.HandedOff,
                    request.Actor,
                    request.OperationKey + ":handoff"),
                cancellationToken);
            return new(SendCaseToAiOutcome.HandedOff, handedOff, []);
        }

        var failed = await _store.TransitionAsync(
            new(
                created.RequestId,
                created.Version,
                AiWorkRequestState.Failed,
                request.Actor,
                request.OperationKey + ":handoff",
                Reason: handOff.Detail ?? (handOff.Kind == AiHandOffOutcomeKind.Refused
                    ? "The channel refused the hand-off."
                    : "The channel was unreachable.")),
            cancellationToken);
        return new(
            SendCaseToAiOutcome.Failed,
            failed,
            [failed.ClosureReason ?? "Nothing was sent."]);
    }
}

/// <summary>
/// The operator-triggered reconcile: reads the connector's delivery record
/// for the request and flips the tracking state. Completed and Failed are
/// claims about the hand-off, not about case content — the writes themselves
/// are already independently visible in permanent action history.
/// </summary>
public sealed class ReconcileAiWorkRequest(
    IAiWorkRequestStore store,
    IAiHandOffTransport transport,
    TimeProvider timeProvider) : IReconcileAiWorkRequest
{
    private readonly IAiWorkRequestStore _store =
        store ?? throw new ArgumentNullException(nameof(store));
    private readonly IAiHandOffTransport _transport =
        transport ?? throw new ArgumentNullException(nameof(transport));
    private readonly TimeProvider _timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public async Task<AiWorkRequestRecord> ExecuteAsync(
        ReconcileAiWorkRequestCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var record = await _store.GetAsync(command.RequestId, cancellationToken)
            ?? throw new KeyNotFoundException("The Send to AI request was not found.");
        if (AiWorkRequestStates.IsTerminal(record.State))
        {
            return record;
        }

        if (record.ExpiresAtUtc <= _timeProvider.GetUtcNow())
        {
            return await _store.TransitionAsync(
                new(
                    record.RequestId,
                    record.Version,
                    AiWorkRequestState.Expired,
                    command.Actor,
                    command.OperationKey,
                    Reason: "The request expired before a reply was recorded."),
                cancellationToken);
        }

        var reply = await _transport.TryReadReplyAsync(
            record.RequestId.ToString("D"),
            cancellationToken);
        return reply?.Status switch
        {
            "done" => await _store.TransitionAsync(
                new(
                    record.RequestId,
                    record.Version,
                    AiWorkRequestState.Completed,
                    command.Actor,
                    command.OperationKey,
                    ReplyStatus: reply.Status,
                    ReplyMessage: reply.Message),
                cancellationToken),
            "failed" => await _store.TransitionAsync(
                new(
                    record.RequestId,
                    record.Version,
                    AiWorkRequestState.Failed,
                    command.Actor,
                    command.OperationKey,
                    Reason: "Claude reported the hand-off as failed.",
                    ReplyStatus: reply.Status,
                    ReplyMessage: reply.Message),
                cancellationToken),
            _ => record
        };
    }
}

public sealed class CancelAiWorkRequest(IAiWorkRequestStore store) : ICancelAiWorkRequest
{
    private readonly IAiWorkRequestStore _store =
        store ?? throw new ArgumentNullException(nameof(store));

    public async Task<AiWorkRequestRecord> ExecuteAsync(
        CancelAiWorkRequestCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var record = await _store.GetAsync(command.RequestId, cancellationToken)
            ?? throw new KeyNotFoundException("The Send to AI request was not found.");
        if (record.State == AiWorkRequestState.Cancelled)
        {
            return record;
        }

        return await _store.TransitionAsync(
            new(
                record.RequestId,
                record.Version,
                AiWorkRequestState.Cancelled,
                command.Actor,
                command.OperationKey,
                command.Reason),
            cancellationToken);
    }
}
