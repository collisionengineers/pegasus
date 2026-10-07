using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Glass;

/// <summary>
/// The gateway a host composes when it does not reach Glass's. Every act is
/// refused the same way, so a command that slipped past the Case record's
/// own availability check still fails closed with one named reason rather
/// than on a missing <c>Glass:*</c> setting.
/// </summary>
public sealed class UnavailableGlassRepairEstimateGateway : IGlassRepairEstimateGateway
{
    private const string UnavailableMessage = "Glass's is not composed on this host.";

    public Task<GlassRepairEstimateStep> PrepareLaunchAsync(
        GlassRepairEstimateLaunchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromException<GlassRepairEstimateStep>(Unavailable());
    }

    public Task<GlassRepairEstimateSession> ContinueLaunchAsync(
        GlassRepairEstimateContinueRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromException<GlassRepairEstimateSession>(Unavailable());
    }

    public Task<GlassRepairEstimateStep> PrepareResumeAsync(
        GlassRepairEstimateResumeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromException<GlassRepairEstimateStep>(Unavailable());
    }

    public Task<GlassRepairEstimateStep> AcceptCallbackAsync(
        GlassRepairEstimateCallback callback, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(callback);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromException<GlassRepairEstimateStep>(Unavailable());
    }

    public Task<GlassRepairEstimateSession> ContinueImportAsync(
        GlassRepairEstimateContinueRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromException<GlassRepairEstimateSession>(Unavailable());
    }

    public Task<GlassRepairEstimateSession> SettleInterruptedAsync(
        ActionActor actor, Guid sessionId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromException<GlassRepairEstimateSession>(Unavailable());
    }

    public Task<GlassRepairEstimateSession> CloseAsync(
        GlassRepairEstimateCloseRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromException<GlassRepairEstimateSession>(Unavailable());
    }

    public Task<Uri?> GetEstimatorUrlAsync(
        ActionActor actor, Guid sessionId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromException<Uri?>(Unavailable());
    }

    private static InvalidOperationException Unavailable() => new(UnavailableMessage);
}
