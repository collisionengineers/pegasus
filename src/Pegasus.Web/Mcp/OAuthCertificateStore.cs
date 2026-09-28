using Microsoft.Extensions.Options;
using OpenIddict.Server;

namespace Pegasus.Web.Mcp;

/// <summary>
/// The Automation OAuth certificates once they are loaded from Key Vault. The
/// load is a remote read behind a managed-identity token, so it runs after the
/// port binds (<see cref="OAuthCertificateLoadService"/>) and the token
/// server's options are built only after it has finished, never before.
/// </summary>
internal sealed class OAuthCertificateStore
{
    private readonly TaskCompletionSource<OAuthCertificateSet> loaded =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public bool IsReady => loaded.Task.IsCompletedSuccessfully;

    /// <summary>Completes when the certificates are loaded. It never faults.</summary>
    public Task Loaded => loaded.Task;

    public OAuthCertificateSet Current => IsReady
        ? loaded.Task.Result
        : throw new InvalidOperationException("The Automation OAuth certificates are not loaded yet.");

    public void Complete(OAuthCertificateSet certificates) => loaded.TrySetResult(certificates);

    /// <summary>
    /// Adds the certificates to the token server's options through the
    /// server's own builder, so the algorithms and checks stay the server's.
    /// </summary>
    public void AddTo(OpenIddictServerOptions target)
    {
        var certificates = Current;
        var scratch = new ServiceCollection();
        var builder = new OpenIddictServerBuilder(scratch);
        foreach (var certificate in certificates.Encryption)
        {
            builder.AddEncryptionCertificate(certificate);
        }

        foreach (var certificate in certificates.Signing)
        {
            builder.AddSigningCertificate(certificate);
        }

        using var provider = scratch.BuildServiceProvider();
        var built = provider.GetRequiredService<IOptions<OpenIddictServerOptions>>().Value;
        foreach (var credentials in built.EncryptionCredentials)
        {
            target.EncryptionCredentials.Add(credentials);
        }

        foreach (var credentials in built.SigningCredentials)
        {
            target.SigningCredentials.Add(credentials);
        }
    }

    /// <summary>
    /// Answers 503 to every request but the health and version probes until the
    /// certificates are loaded. Any other request builds the token server's
    /// options, and building them without certificates would fail for good.
    /// </summary>
    public Task Gate(HttpContext context, RequestDelegate next)
    {
        if (IsReady
            || context.Request.Path.StartsWithSegments("/health")
            || context.Request.Path.StartsWithSegments("/diagnostics"))
        {
            return next(context);
        }

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.Headers.RetryAfter = "5";
        return Task.CompletedTask;
    }
}

/// <summary>
/// Loads the Automation OAuth certificates after start. A failed attempt is
/// logged and retried with a growing pause until it succeeds or the host
/// stops, so a slow managed-identity sidecar delays the token server and
/// never stops the process.
/// </summary>
internal sealed partial class OAuthCertificateLoadService(
    OAuthCertificateStore store,
    Func<OAuthCertificateSet> load,
    TimeSpan firstPause,
    TimeSpan longestPause,
    ILogger<OAuthCertificateLoadService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Returns the caller at once: a hosted service that has not yielded
        // holds the port until it does.
        await Task.Yield();
        var pause = firstPause;
        for (var attempt = 1; !stoppingToken.IsCancellationRequested; attempt++)
        {
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                var certificates = await Task.Run(load, stoppingToken);
                store.Complete(certificates);
                var elapsedMilliseconds = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                LogLoaded(logger, attempt, elapsedMilliseconds);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                LogFailed(logger, attempt, pause.TotalSeconds, exception);
            }

            try
            {
                await Task.Delay(pause, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            pause = TimeSpan.FromTicks(Math.Min(pause.Ticks * 2, longestPause.Ticks));
        }
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Automation OAuth certificates loaded on attempt {Attempt} in {ElapsedMs} ms")]
    private static partial void LogLoaded(ILogger logger, int attempt, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Automation OAuth certificates not loaded on attempt {Attempt}; retrying in {PauseSeconds} s")]
    private static partial void LogFailed(ILogger logger, int attempt, double pauseSeconds, Exception exception);
}
