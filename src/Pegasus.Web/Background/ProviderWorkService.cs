namespace Pegasus.Web.Background;

/// <summary>
/// Runs the Web host's queued provider work: a few pieces at once, each in its
/// own dependency scope and under an overall time cap.
/// </summary>
/// <remarks>
/// The work runs here, not in the Worker, because it reads provider session
/// state and per-staff credentials protected by the Web host's own key ring,
/// which also protects staff cookies. Work that passes the cap or is stopped by shutdown is
/// cancelled, and the work itself records what that leaves.
/// </remarks>
public sealed partial class ProviderWorkService(
    ProviderWorkQueue queue,
    IServiceScopeFactory scopes,
    TimeProvider timeProvider,
    ILogger<ProviderWorkService> logger) : BackgroundService
{
    /// <summary>How many pieces of work run at once.</summary>
    public const int Concurrency = 4;

    /// <summary>The longest one piece of work may run before it is cancelled.</summary>
    public static readonly TimeSpan TimeCap = TimeSpan.FromMinutes(3);

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(Enumerable.Range(0, Concurrency).Select(_ => ConsumeAsync(stoppingToken)));

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var work in queue.Reader.ReadAllAsync(stoppingToken))
            {
                await RunAsync(work, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping; queued work is not started.
        }
    }

    private async Task RunAsync(ProviderWork work, CancellationToken stoppingToken)
    {
        using var cap = new CancellationTokenSource(TimeCap, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, cap.Token);
        try
        {
            // Work for one record runs one piece at a time; the time cap counts
            // the wait behind earlier work too.
            using var entered = await queue.EnterAsync(work.Key, linked.Token);
            await using var scope = scopes.CreateAsyncScope();
            await work.RunAsync(scope.ServiceProvider, linked.Token);
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested)
        {
            LogCancelled(logger, work.Kind, work.Key, cap.IsCancellationRequested ? "time cap" : "shutdown");
        }
        catch (Exception failure)
        {
            LogFailed(logger, work.Kind, work.Key, failure);
        }
        finally
        {
            // Queued work holds the reservation it was admitted under.
            queue.Release(work.Key);
        }
    }

    [LoggerMessage(
        EventId = 1240,
        Level = LogLevel.Warning,
        Message = "Provider work {Kind} for {Key} was cancelled by the {Reason}")]
    private static partial void LogCancelled(ILogger logger, string kind, Guid key, string reason);

    [LoggerMessage(
        EventId = 1241,
        Level = LogLevel.Error,
        Message = "Provider work {Kind} for {Key} failed")]
    private static partial void LogFailed(ILogger logger, string kind, Guid key, Exception exception);
}
