using Pegasus.Core.Identity;

namespace Pegasus.Core.Assessment;

public enum GlassRepairEstimateSessionState
{
    Prepared, Launching, Active, Importing, Completed, Failed, Unknown, Expired, Cancelled
}
public sealed record GlassRepairEstimateSession(
    Guid Id, Guid CaseId, Guid PegasusUserId, long CredentialGeneration,
    string NormalizedExternalAccountKey, GlassRepairEstimateSessionState State,
    long Version, string OperationKey, DateTimeOffset CreatedAtUtc, DateTimeOffset ExpiresAtUtc,
    string? ProviderVehicleId, string? ProviderEstimateId, string? FailureCode);
public sealed record GlassRepairEstimateLaunchRequest(
    ActionActor Actor, Guid CaseId, long ExpectedCaseVersion, string LeaseToken,
    string OperationKey);
public sealed record GlassRepairEstimateCallback(
    ActionActor Actor, Guid SessionId, long ExpectedVersion, string Correlation,
    string OperationKey);
public interface IGlassRepairEstimateGateway
{
    Task<GlassRepairEstimateSession> LaunchAsync(
        GlassRepairEstimateLaunchRequest request, CancellationToken cancellationToken);
    Task<GlassRepairEstimateSession> ResumeAsync(
        ActionActor actor, Guid sessionId, long expectedVersion, CancellationToken cancellationToken);
    Task<GlassRepairEstimateSession> CompleteAsync(
        GlassRepairEstimateCallback callback, CancellationToken cancellationToken);
}
/// <summary>Durable provider session material stays server-side and protected at rest.</summary>
public sealed class GlassRepairEstimateSessionMaterial(
    GlassRepairEstimateSession session, string protectedProviderState, string callbackDigest)
{
    public GlassRepairEstimateSession Session { get; } = session;
    public string ProtectedProviderState { get; } = protectedProviderState;
    public string CallbackDigest { get; } = callbackDigest;
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
