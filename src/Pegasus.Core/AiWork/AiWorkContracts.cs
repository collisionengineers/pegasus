using Pegasus.Core.Identity;

namespace Pegasus.Core.AiWork;

/// <summary>
/// The Send to AI work request (AI-09): a Core-owned tracking record for one
/// pointer hand-off to the external AI channel. The channel carries operator
/// chat only — a case reference and a short instruction out, a short
/// confirmation back. The work itself returns as ordinary attributed
/// Automation Actor writes through the assessment toolset, so completing,
/// cancelling, or expiring a request never applies or undoes case content:
/// it closes the tracking record only.
/// </summary>
public enum AiWorkRequestState
{
    Created,
    HandedOff,
    Completed,
    Failed,
    Cancelled,
    Expired
}

public static class AiWorkRequestStates
{
    public static bool IsTerminal(AiWorkRequestState state) =>
        state is AiWorkRequestState.Completed
            or AiWorkRequestState.Failed
            or AiWorkRequestState.Cancelled
            or AiWorkRequestState.Expired;
}

public sealed record AiWorkRequestRecord(
    Guid RequestId,
    Guid CaseId,
    string CaseReference,
    long CaseVersionAtSend,
    string CapabilityScope,
    string Instruction,
    AiWorkRequestState State,
    DateTimeOffset CreatedAtUtc,
    string CreatedBy,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset? HandedOffAtUtc,
    DateTimeOffset? ClosedAtUtc,
    string? ClosureReason,
    string? ReplyStatus,
    string? ReplyMessage,
    long Version);

/// <summary>
/// The pointer handed to the channel: never case content. The request
/// identifier doubles as the round trip's correlation identifier in action
/// history, the channel's delivery events, and Claude's reply.
/// </summary>
public sealed record AiHandOffPointer(
    string RequestId,
    string CaseReference,
    string Instruction,
    int SchemaVersion);

public enum AiHandOffOutcomeKind
{
    /// <summary>The channel accepted and forwarded the pointer.</summary>
    Accepted,

    /// <summary>Terminal configuration refusal (auth, media type, size).</summary>
    Refused,

    /// <summary>Transient transport failure (connection refused, 5xx).</summary>
    Unreachable
}

public sealed record AiHandOffResult(AiHandOffOutcomeKind Kind, string? Detail);

public sealed record AiChannelReply(
    string Status,
    string? Message,
    DateTimeOffset? RepliedAtUtc);

/// <summary>
/// The one outbound seam to the channel connector. Reading the reply is a
/// diagnostic read of the connector's delivery record — never a business
/// ingress; business content only ever arrives through the Automation Actor.
/// </summary>
public interface IAiHandOffTransport
{
    Task<AiHandOffResult> HandOffAsync(
        AiHandOffPointer handOff,
        CancellationToken cancellationToken);

    Task<AiChannelReply?> TryReadReplyAsync(
        string requestId,
        CancellationToken cancellationToken);
}

public sealed record CreateAiWorkRequestCommand(
    Guid CaseId,
    string CaseReference,
    long CaseVersion,
    ActionActor Actor,
    string OperationKey,
    string Instruction,
    TimeSpan Expiry);

public sealed record AiWorkRequestTransition(
    Guid RequestId,
    long ExpectedVersion,
    AiWorkRequestState TargetState,
    ActionActor Actor,
    string OperationKey,
    string? Reason = null,
    string? ReplyStatus = null,
    string? ReplyMessage = null);

public interface IAiWorkRequestStore
{
    Task<AiWorkRequestRecord> CreateAsync(
        CreateAiWorkRequestCommand command,
        CancellationToken cancellationToken);

    Task<AiWorkRequestRecord?> GetAsync(Guid requestId, CancellationToken cancellationToken);

    Task<AiWorkRequestRecord?> GetLatestForCaseAsync(
        Guid caseId,
        CancellationToken cancellationToken);

    Task<AiWorkRequestRecord> TransitionAsync(
        AiWorkRequestTransition transition,
        CancellationToken cancellationToken);
}

/// <summary>
/// The Administrator-held Send to AI outbound switch, mirroring the
/// Automation client kill switch: disabling refuses new hand-offs
/// immediately with attributable permanent history.
/// </summary>
public interface ISendToAiControl
{
    Task<bool> IsEnabledAsync(CancellationToken cancellationToken);

    Task<bool> SetEnabledAsync(
        bool enabled,
        ActionActor actor,
        string reason,
        string operationKey,
        CancellationToken cancellationToken);
}

public enum SendCaseToAiOutcome
{
    HandedOff,
    Failed,
    NotEligible
}

public sealed record SendCaseToAiRequest(
    Guid CaseId,
    ActionActor Actor,
    string OperationKey,
    string Instruction);

public sealed record SendCaseToAiResult(
    SendCaseToAiOutcome Outcome,
    AiWorkRequestRecord? Request,
    IReadOnlyList<string> Reasons);

public interface ISendCaseToAi
{
    Task<SendCaseToAiResult> ExecuteAsync(
        SendCaseToAiRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// Reconciles one work request. <paramref name="CaseId"/> is the case the
/// caller is acting on; a request belonging to another case fails closed
/// rather than being read and transitioned from the wrong case's screen.
/// </summary>
public sealed record ReconcileAiWorkRequestCommand(
    Guid CaseId,
    Guid RequestId,
    ActionActor Actor,
    string OperationKey);

public interface IReconcileAiWorkRequest
{
    Task<AiWorkRequestRecord> ExecuteAsync(
        ReconcileAiWorkRequestCommand command,
        CancellationToken cancellationToken);
}

public sealed record CancelAiWorkRequestCommand(
    Guid RequestId,
    ActionActor Actor,
    string OperationKey,
    string Reason);

public interface ICancelAiWorkRequest
{
    Task<AiWorkRequestRecord> ExecuteAsync(
        CancelAiWorkRequestCommand command,
        CancellationToken cancellationToken);
}
