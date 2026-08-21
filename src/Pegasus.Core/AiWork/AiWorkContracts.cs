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

/// <summary>
/// The one owner of the Send to AI channel connector bounds. Composition
/// options and Administration entry both validate against these, so the two
/// entry routes cannot drift: a loopback http origin without path or query,
/// a bearer token of at least 32 characters, and a 1-60 second timeout.
/// The loopback restriction is ADR-0021's research-preview transport
/// decision, not a connector-administration choice.
/// </summary>
public static class AiChannelConnectorRules
{
    public const int MinimumTokenLength = 32;
    public const double MinimumTimeoutSeconds = 1;
    public const double MaximumTimeoutSeconds = 60;

    public static bool TryParseBaseUrl(string? candidate, out Uri? baseUrl)
    {
        baseUrl = null;
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var parsed)
            || parsed.Scheme != Uri.UriSchemeHttp
            || !parsed.IsLoopback
            || !string.IsNullOrEmpty(parsed.Query)
            || parsed.AbsolutePath != "/")
        {
            return false;
        }

        baseUrl = parsed;
        return true;
    }

    public static bool IsValidTimeoutSeconds(double seconds) =>
        seconds is >= MinimumTimeoutSeconds and <= MaximumTimeoutSeconds;

    public static bool IsValidToken(string? token) =>
        !string.IsNullOrWhiteSpace(token) && token.Length >= MinimumTokenLength;
}

/// <summary>
/// What Administration may see of the connector: whether an
/// administration-entered token is held and when it last changed — never the
/// token itself, which is write-only from entry onward.
/// </summary>
public sealed record AiChannelConnectorSettings(
    string? ChannelBaseUrl,
    double? TimeoutSeconds,
    bool TokenHeld,
    DateTimeOffset? TokenRotatedAtUtc,
    int Version);

/// <summary>
/// What the outbound transport reads at each hand-off. A null member means
/// Administration has not set it and the composed configuration value
/// applies.
/// </summary>
public sealed record AiChannelConnectorRuntime(
    Uri? ChannelBaseUrl,
    TimeSpan? Timeout,
    string? ChannelToken);

public sealed record UpdateAiChannelConnectorCommand(
    ActionActor Actor,
    string Reason,
    string OperationKey,
    string? ChannelBaseUrl,
    double? TimeoutSeconds);

public sealed record RotateAiChannelTokenCommand(
    ActionActor Actor,
    string Reason,
    string OperationKey,
    string? NewToken);

/// <summary>
/// Administration-held connector settings stored beside the Send to AI
/// switch. Updates and rotations are attributed permanent history; the token
/// is stored protected and surfaces only through the runtime view consumed
/// by the transport.
/// </summary>
public interface IAiChannelConnectorStore
{
    Task<AiChannelConnectorSettings> GetAsync(CancellationToken cancellationToken);

    Task<AiChannelConnectorRuntime> GetRuntimeAsync(CancellationToken cancellationToken);

    Task<AiChannelConnectorSettings> UpdateAsync(
        UpdateAiChannelConnectorCommand command,
        CancellationToken cancellationToken);

    Task<AiChannelConnectorSettings> RotateTokenAsync(
        RotateAiChannelTokenCommand command,
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
