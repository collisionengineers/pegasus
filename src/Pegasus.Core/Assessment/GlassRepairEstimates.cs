using Pegasus.Core.Identity;

namespace Pegasus.Core.Assessment;

public enum GlassRepairEstimateSessionState
{
    Prepared, Launching, Active, Importing, AwaitingImport, Completed, Failed, Unknown, Expired, Cancelled
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
}
