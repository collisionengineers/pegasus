using System.Diagnostics;
using System.Globalization;
using Microsoft.AspNetCore.DataProtection;

namespace Pegasus.Web.Health;

/// <summary>
/// Where the time goes between the process starting and the port listening.
/// Each mark prints one stdout line with the milliseconds since the process
/// began (so runtime start-up counts) and since the previous mark. Stdout is
/// used, not the logger, because the first marks come before the host, and a
/// start that dies before it listens still shows how far it got.
/// </summary>
internal sealed class StartupTimeline
{
    /// <summary>The logger category the summary and warm-up timings use.</summary>
    public const string Category = "Pegasus.Web.Startup";

    private readonly object gate = new();
    private readonly Action<string> write;
    private readonly long startedAt = Stopwatch.GetTimestamp();
    private readonly TimeSpan beforeFirstMark;
    private readonly List<(string Phase, long AtMs, long StepMs)> marks = [];
    private long previousMs;

    public StartupTimeline(Action<string> write, TimeSpan beforeFirstMark)
    {
        this.write = write;
        this.beforeFirstMark = beforeFirstMark;
    }

    /// <summary>The timeline the running process writes to stdout.</summary>
    public static StartupTimeline Current { get; } = new(Console.Out.WriteLine, ProcessAge());

    /// <summary>Records that a phase has just finished.</summary>
    public void Mark(string phase)
    {
        string line;
        lock (gate)
        {
            var atMs = (long)(beforeFirstMark + Stopwatch.GetElapsedTime(startedAt)).TotalMilliseconds;
            var stepMs = atMs - previousMs;
            previousMs = atMs;
            marks.Add((phase, atMs, stepMs));
            line = string.Create(
                CultureInfo.InvariantCulture,
                $"[startup] +{atMs} ms (+{stepMs} ms) {phase}");
        }

        write(line);
    }

    /// <summary>One line naming every phase so far, for the log the platform keeps.</summary>
    public string Summary()
    {
        lock (gate)
        {
            return string.Join(
                "; ",
                marks.Select(mark => string.Create(
                    CultureInfo.InvariantCulture,
                    $"{mark.Phase} +{mark.AtMs} ms (+{mark.StepMs} ms)")));
        }
    }

    private static TimeSpan ProcessAge()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            var age = DateTime.UtcNow - process.StartTime.ToUniversalTime();
            return age < TimeSpan.Zero ? TimeSpan.Zero : age;
        }
        catch (Exception)
        {
            // The age only labels the output; never let it stop the host.
            return TimeSpan.Zero;
        }
    }
}

internal static class StartupServiceCollectionExtensions
{
    private const string DataProtectionHostedServiceName =
        "Microsoft.AspNetCore.DataProtection.Internal.DataProtectionHostedService";

    /// <summary>
    /// Stops the framework loading the data-protection key ring before the port
    /// binds. Its hosted service reads the ring synchronously in
    /// <c>StartAsync</c>, and every hosted service finishes starting before
    /// Kestrel listens, so a ring kept in Blob Storage holds the port behind a
    /// managed-identity token. The ring then loads in the start-up warm-up, and
    /// otherwise on first use. Call it last, after every <c>AddDataProtection</c>
    /// (each one adds the service back).
    /// </summary>
    public static IServiceCollection DeferDataProtectionKeyRingLoad(this IServiceCollection services)
    {
        for (var index = services.Count - 1; index >= 0; index--)
        {
            var descriptor = services[index];
            if (descriptor.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService)
                && descriptor.ImplementationType?.FullName == DataProtectionHostedServiceName)
            {
                services.RemoveAt(index);
            }
        }

        return services;
    }

    /// <summary>
    /// Loads the key ring now, so the first sign-in does not pay for it. As the
    /// framework's own start-up load did, it creates the first key when the
    /// ring is empty.
    /// </summary>
    public static void LoadKeyRing(this IDataProtectionProvider provider) =>
        provider.CreateProtector("Pegasus.StartupWarmup").Protect([0]);
}
