using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Pegasus.Infrastructure.Custody;

/// <summary>
/// Renews the Box access token in the background, so the request that would
/// have minted it finds a live token and does not wait on Box's token endpoint.
/// </summary>
/// <remarks>
/// It waits for <see cref="IHostApplicationLifetime.ApplicationStarted"/>, so
/// nothing here holds the port or the host's start. It then asks the provider
/// to renew if due at once and every <see cref="CheckInterval"/>. A check that
/// finds the token live does nothing.
///
/// The provider is resolved inside each check, not at construction. Its Box
/// options are built at first use on purpose: an unresolved Box secret must
/// fail a Box work item, never the host. A failed resolve or mint is logged and
/// the loop carries on. The request path mints on demand as it always did, so a
/// renewal that never works costs a request the mint it paid before, never a
/// failed read.
/// </remarks>
internal sealed partial class BoxTokenRenewalService(
    IServiceProvider services,
    IHostApplicationLifetime lifetime,
    TimeProvider timeProvider,
    ILogger<BoxTokenRenewalService> logger) : BackgroundService
{
    /// <summary>How often the token is checked. A check that finds it live is a memory read.</summary>
    internal static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(60);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Every hosted service finishes starting before the port binds.
        await Task.Yield();
        try
        {
            await ApplicationStartedAsync(stoppingToken);
            var failing = false;
            while (true)
            {
                failing = await RenewIfDueAsync(failing, stoppingToken);
                await Task.Delay(CheckInterval, timeProvider, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping.
        }
    }

    private async Task ApplicationStartedAsync(CancellationToken stoppingToken)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = lifetime.ApplicationStarted.Register(() => started.TrySetResult());
        await started.Task.WaitAsync(stoppingToken);
    }

    /// <summary>
    /// One check. Answers whether it failed, so the log says a run of failures
    /// once at Warning and then quietly, and says so again after a success.
    /// </summary>
    private async Task<bool> RenewIfDueAsync(bool failedBefore, CancellationToken stoppingToken)
    {
        try
        {
            await services.GetRequiredService<IBoxAuthorizationHeaderProvider>()
                .RenewIfDueAsync(stoppingToken);
            return false;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            if (failedBefore)
            {
                LogRenewalStillFailing(logger, exception);
            }
            else
            {
                LogRenewalFailed(logger, exception);
            }
            return true;
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "The background Box token renewal failed. Requests mint a token on demand.")]
    private static partial void LogRenewalFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "The background Box token renewal failed again.")]
    private static partial void LogRenewalStillFailing(ILogger logger, Exception exception);
}
