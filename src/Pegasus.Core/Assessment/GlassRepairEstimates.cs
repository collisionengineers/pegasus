using Pegasus.Core.Identity;

namespace Pegasus.Core.Assessment;

public enum GlassRepairEstimateSessionState
{
    Prepared, Launching, Active, Importing, AwaitingImport, Completed, Failed, Unknown, Expired, Cancelled
}

public static class GlassRepairEstimateSessionPolicy
{
    public static bool OccupiesAccount(GlassRepairEstimateSessionState state) => state is
        GlassRepairEstimateSessionState.Prepared or GlassRepairEstimateSessionState.Launching
        or GlassRepairEstimateSessionState.Active or GlassRepairEstimateSessionState.Importing
        or GlassRepairEstimateSessionState.AwaitingImport or GlassRepairEstimateSessionState.Unknown;

    /// <summary>
    /// Which sessions the owning Engineer may close: every one that still
    /// holds the account except one mid-import, whose claim is acting on the
    /// provider's return and must be allowed to settle.
    /// </summary>
    public static bool CanClose(GlassRepairEstimateSessionState state) =>
        OccupiesAccount(state) && state != GlassRepairEstimateSessionState.Importing;

    public static void ValidateClosure(
        GlassRepairEstimateCloseRequest request, GlassRepairEstimateSession session)
    {
        RepairSpecificationPolicy.RequireEngineer(request.Actor);
        if (request.Actor.Kind != ActorKind.Staff
            || !Guid.TryParse(request.Actor.SubjectId, out var staffId)
            || staffId != session.PegasusUserId)
        {
            throw new GlassRepairEstimateRefusalException("This Glass's session belongs to another Engineer.");
        }
        if (!CanClose(session.State)
            || !request.ExternalSessionClosed || string.IsNullOrWhiteSpace(request.Reason)
            || request.Reason.Trim().Length > 2000)
        {
            throw new GlassRepairEstimateRefusalException(
                "Closing a Glass's session that holds the account requires confirmation of external closure and a reason.");
        }
    }
}

/// <summary>Which invariant a Glass's session write ran into.</summary>
public enum GlassRepairEstimateSessionConflict
{
    ActiveAccount,
    Version,
    Callback,
    OperationKey
}

/// <summary>A Glass's session write refused because it would break an invariant.</summary>
public sealed class GlassRepairEstimateSessionConflictException(
    GlassRepairEstimateSessionConflict conflict, Guid sessionId, string message)
    : InvalidOperationException(message)
{
    public GlassRepairEstimateSessionConflict Conflict { get; } = conflict;
    public Guid SessionId { get; } = sessionId;
}

/// <summary>An expected Glass's action refusal whose reason may be shown to staff.</summary>
public sealed class GlassRepairEstimateRefusalException(string message)
    : InvalidOperationException(message);

public sealed record GlassRepairEstimateSession(
    Guid Id, Guid CaseId, Guid PegasusUserId, long CredentialGeneration,
    string NormalizedExternalAccountKey, GlassRepairEstimateSessionState State,
    long Version, string OperationKey, DateTimeOffset CreatedAtUtc, DateTimeOffset ExpiresAtUtc,
    string? ProviderVehicleId, string? ProviderEstimateId, string? FailureCode,
    DateTimeOffset? CallbackConsumedAtUtc = null);
public sealed record GlassRepairEstimateLaunchRequest(
    ActionActor Actor, Guid CaseId, long ExpectedCaseVersion, string LeaseToken,
    string OperationKey);
public sealed record GlassRepairEstimateResumeRequest(
    ActionActor Actor, Guid SessionId, long ExpectedVersion,
    long? ExpectedCaseVersion = null, string? LeaseToken = null);
public sealed record GlassRepairEstimateCloseRequest(
    ActionActor Actor, Guid SessionId, long ExpectedVersion, bool ExternalSessionClosed,
    string Reason);
public sealed record GlassRepairEstimateCallback(
    ActionActor Actor, Guid SessionId, long ExpectedVersion, string Correlation,
    string RawQuery)
{
    /// <summary>The provider query exactly as received, without normalization or re-encoding.</summary>
    public override string ToString() => nameof(GlassRepairEstimateCallback);
}
public interface IGlassRepairEstimateGateway
{
    Task<GlassRepairEstimateSession> LaunchAsync(
        GlassRepairEstimateLaunchRequest request, CancellationToken cancellationToken);
    Task<GlassRepairEstimateSession> ResumeAsync(
        GlassRepairEstimateResumeRequest request, CancellationToken cancellationToken);
    Task<GlassRepairEstimateSession> CloseAsync(
        GlassRepairEstimateCloseRequest request, CancellationToken cancellationToken);
    Task<GlassRepairEstimateSession> CompleteAsync(
        GlassRepairEstimateCallback callback, CancellationToken cancellationToken);
    Task<Uri?> GetEstimatorUrlAsync(
        ActionActor actor, Guid sessionId, CancellationToken cancellationToken);
}
/// <summary>Durable provider session material stays server-side and protected at rest.</summary>
public sealed class GlassRepairEstimateSessionMaterial(
    GlassRepairEstimateSession session, string protectedProviderState, string callbackDigest,
    string? resultArtifactsJson = null)
{
    public GlassRepairEstimateSession Session { get; } = session;
    public string ProtectedProviderState { get; } = protectedProviderState;
    public string CallbackDigest { get; } = callbackDigest;
    public string? ResultArtifactsJson { get; } = resultArtifactsJson;
    public override string ToString() => nameof(GlassRepairEstimateSessionMaterial);
}
public interface IGlassRepairEstimateSessionStore
{
    Task<GlassRepairEstimateSessionMaterial?> GetAsync(Guid sessionId, CancellationToken cancellationToken);
    Task<GlassRepairEstimateSessionMaterial> CreateAsync(
        GlassRepairEstimateSessionMaterial material, CancellationToken cancellationToken);
    Task SaveAsync(GlassRepairEstimateSessionMaterial material, long expectedVersion,
        CancellationToken cancellationToken);
    Task<GlassRepairEstimateSession> CloseAsync(
        GlassRepairEstimateCloseRequest request, CancellationToken cancellationToken);
    /// <summary>The session that holds the external account now, or null when none does.</summary>
    Task<GlassRepairEstimateSession?> FindLiveForAccountAsync(
        string normalizedExternalAccountKey, CancellationToken cancellationToken);
}
