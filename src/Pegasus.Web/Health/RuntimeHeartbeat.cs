using System.Globalization;
using System.Text;

namespace Pegasus.Web.Health;

/// <summary>
/// Every <c>Diagnostics:HeartbeatInterval</c> (five minutes unless set;
/// <c>00:00:00</c> turns it off) logs one trace with the container's memory
/// picture: the process's resident memory split into anonymous, file and
/// shared, the host's free memory, the container's cgroup usage against its
/// limit, page faults, load and the runtime's own heap and thread figures.
/// Plan memory sits near its limit, and the platform's own figures do not say
/// what holds it. Every figure is best effort and left out when its source is
/// absent. The service starts after the host has started, so it never delays
/// the port, and it never throws out of its loop.
/// </summary>
internal sealed partial class RuntimeHeartbeat(
    IHostApplicationLifetime lifetime,
    IConfiguration configuration,
    ILogger<RuntimeHeartbeat> logger) : BackgroundService
{
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromMinutes(5);

    private const string IntervalKey = "Diagnostics:HeartbeatInterval";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        var interval = configuration.GetValue(IntervalKey, DefaultInterval);
        if (interval <= TimeSpan.Zero)
        {
            return;
        }

        try
        {
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await using var registration = lifetime.ApplicationStarted.Register(() => started.TrySetResult());
            await started.Task.WaitAsync(stoppingToken);

            using var timer = new PeriodicTimer(interval);
            do
            {
                Beat();
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping.
        }
    }

    private void Beat()
    {
        try
        {
            var values = Describe(ProcFs.ReadText);
            LogHeartbeat(logger, values);
        }
        catch (Exception)
        {
            // A diagnostic must never stop the host or end its own loop.
        }
    }

    /// <summary>
    /// The figures as <c>name=value</c> pairs, memory in whole MiB. <paramref name="read"/>
    /// returns a file's text, or null when it does not exist.
    /// </summary>
    internal static string Describe(Func<string, string?> read)
    {
        var values = new StringBuilder();

        var status = read("/proc/self/status");
        AppendMebibytes(values, "vmrss_mb", ProcFs.ParseKilobyteLine(status, "VmRSS"));
        AppendMebibytes(values, "rssanon_mb", ProcFs.ParseKilobyteLine(status, "RssAnon"));
        AppendMebibytes(values, "rssfile_mb", ProcFs.ParseKilobyteLine(status, "RssFile"));
        AppendMebibytes(values, "rssshmem_mb", ProcFs.ParseKilobyteLine(status, "RssShmem"));
        Append(values, "majflt", ProcFs.ParseMajorFaults(read("/proc/self/stat")));

        var meminfo = read("/proc/meminfo");
        AppendMebibytes(values, "mem_total_mb", ProcFs.ParseKilobyteLine(meminfo, "MemTotal"));
        AppendMebibytes(values, "mem_available_mb", ProcFs.ParseKilobyteLine(meminfo, "MemAvailable"));

        AppendCgroup(values, read);

        Append(values, "load1", ProcFs.ParseLoadAverage(read("/proc/loadavg")));

        AppendMebibytes(values, "gc_heap_mb", GC.GetGCMemoryInfo().HeapSizeBytes);
        AppendMebibytes(values, "gc_total_mb", GC.GetTotalMemory(false));
        Append(values, "threads", ThreadPool.ThreadCount);
        return values.ToString();
    }

    /// <summary>
    /// The container's memory cgroup. Version 2 keeps <c>memory.current</c>,
    /// <c>memory.max</c> and <c>memory.stat</c> (<c>anon</c>, <c>file</c>,
    /// <c>pgmajfault</c>) under <c>/sys/fs/cgroup</c>; version 1 keeps the
    /// same facts under <c>/sys/fs/cgroup/memory</c> as <c>usage_in_bytes</c>,
    /// <c>limit_in_bytes</c> and <c>memory.stat</c> (<c>rss</c>, <c>cache</c>,
    /// <c>pgmajfault</c>). Version 1 is read only when version 2 is absent.
    /// </summary>
    private static void AppendCgroup(StringBuilder values, Func<string, string?> read)
    {
        const string V2 = "/sys/fs/cgroup/";
        const string V1 = "/sys/fs/cgroup/memory/";
        var current = ProcFs.ParseSingleValue(read(V2 + "memory.current"));
        var (root, currentFile, maxFile, anonKey, fileKey) = current is not null
            ? (V2, "memory.current", "memory.max", "anon", "file")
            : (V1, "memory.usage_in_bytes", "memory.limit_in_bytes", "rss", "cache");
        current ??= ProcFs.ParseSingleValue(read(root + currentFile));

        AppendMebibytes(values, "cg_current_mb", ToBytes(current));
        var max = ProcFs.ParseSingleValue(read(root + maxFile));
        Append(values, "cg_max_mb", max is null || max == "max" ? max : Mebibytes(ToBytes(max)!.Value));

        var stat = read(root + "memory.stat");
        AppendMebibytes(values, "cg_anon_mb", ProcFs.ParseKeyedValue(stat, anonKey));
        AppendMebibytes(values, "cg_file_mb", ProcFs.ParseKeyedValue(stat, fileKey));
        Append(values, "cg_pgmajfault", ProcFs.ParseKeyedValue(stat, "pgmajfault"));
    }

    private static long? ToBytes(string? value) =>
        long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var bytes) ? bytes : null;

    private static string Mebibytes(long bytes) =>
        (bytes / (1024 * 1024)).ToString(CultureInfo.InvariantCulture);

    private static void AppendMebibytes(StringBuilder values, string name, long? bytes)
    {
        if (bytes is { } value)
        {
            Append(values, name, Mebibytes(value));
        }
    }

    private static void Append(StringBuilder values, string name, object? value)
    {
        if (value is null)
        {
            return;
        }

        if (values.Length > 0)
        {
            values.Append(' ');
        }

        values.Append(name).Append('=').Append(Convert.ToString(value, CultureInfo.InvariantCulture));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Runtime heartbeat: {Values}")]
    private static partial void LogHeartbeat(ILogger logger, string values);
}
