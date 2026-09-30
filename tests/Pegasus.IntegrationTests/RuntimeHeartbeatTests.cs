using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pegasus.Web.Health;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The runtime heartbeat's readers work from a file's text, so they are tested
/// against samples of what Linux and the container's cgroup write. A file that
/// does not exist gives no value, and the heartbeat leaves that figure out.
/// </summary>
public sealed class RuntimeHeartbeatTests
{
    [Fact]
    public void MajorFaultsAreTheTenthFieldAfterTheCommandName()
    {
        const string stat =
            "4242 (dotnet) S 1 4242 4242 0 -1 4194560 2000 0 17 0 300 40 0 0 20 0 30 0 100 1000000 5000";

        Assert.Equal(17, ProcFs.ParseMajorFaults(stat));
    }

    [Fact]
    public void ACommandNameWithSpacesAndParenthesesDoesNotShiftTheFields()
    {
        // The name is the text between the first "(" and the LAST ")".
        const string stat =
            "77 (my (odd) app) R 1 77 77 0 -1 0 5 0 9 0 1 1 0 0 20 0 1 0 5 100 10";

        Assert.Equal(9, ProcFs.ParseMajorFaults(stat));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no closing bracket here")]
    [InlineData("1 (short) S 1 1")]
    [InlineData("1 (x) S 1 1 1 0 -1 0 5 0 notanumber 0")]
    public void MajorFaultsAreOmittedWhenTheTextIsMissingOrShort(string? stat)
    {
        Assert.Null(ProcFs.ParseMajorFaults(stat));
    }

    [Fact]
    public void KilobyteLinesAreReadInBytesAndTheKeyMustMatchWhole()
    {
        const string status = "Name:\tdotnet\nVmRSS:\t  345678 kB\nRssAnon:\t  300000 kB\nRssFile:\t   45000 kB\nRssShmem:\t     678 kB\n";

        Assert.Equal(345678L * 1024, ProcFs.ParseKilobyteLine(status, "VmRSS"));
        Assert.Equal(300000L * 1024, ProcFs.ParseKilobyteLine(status, "RssAnon"));
        Assert.Equal(678L * 1024, ProcFs.ParseKilobyteLine(status, "RssShmem"));
        Assert.Null(ProcFs.ParseKilobyteLine(status, "Rss"));
        Assert.Null(ProcFs.ParseKilobyteLine(status, "VmSwap"));
        Assert.Null(ProcFs.ParseKilobyteLine(null, "VmRSS"));
    }

    [Fact]
    public void MemInfoLinesAreReadTheSameWay()
    {
        const string meminfo = "MemTotal:        1789012 kB\nMemFree:          100000 kB\nMemAvailable:     512000 kB\n";

        Assert.Equal(1789012L * 1024, ProcFs.ParseKilobyteLine(meminfo, "MemTotal"));
        Assert.Equal(512000L * 1024, ProcFs.ParseKilobyteLine(meminfo, "MemAvailable"));
    }

    [Fact]
    public void CgroupStatValuesMatchTheWholeKey()
    {
        const string stat = "anon 100\nfile 200\nanon_thp 5\nfile_mapped 7\npgmajfault 11\n";

        Assert.Equal(100, ProcFs.ParseKeyedValue(stat, "anon"));
        Assert.Equal(200, ProcFs.ParseKeyedValue(stat, "file"));
        Assert.Equal(11, ProcFs.ParseKeyedValue(stat, "pgmajfault"));
        Assert.Null(ProcFs.ParseKeyedValue(stat, "shmem"));
        Assert.Null(ProcFs.ParseKeyedValue(null, "anon"));
    }

    [Theory]
    [InlineData("1073741824\n", "1073741824")]
    [InlineData("max\n", "max")]
    [InlineData("", null)]
    [InlineData("not a number", null)]
    [InlineData(null, null)]
    public void ASingleValueFileGivesItsNumberOrTheWordMax(string? text, string? expected)
    {
        Assert.Equal(expected, ProcFs.ParseSingleValue(text));
    }

    [Theory]
    [InlineData("0.52 0.48 0.40 1/234 5678\n", "0.52")]
    [InlineData("", null)]
    [InlineData("garbage", null)]
    [InlineData(null, null)]
    public void TheLoadAverageIsTheFirstField(string? text, string? expected)
    {
        Assert.Equal(expected, ProcFs.ParseLoadAverage(text));
    }

    [Fact]
    public void AFileThatDoesNotExistReadsAsNull()
    {
        Assert.Null(ProcFs.ReadText(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing")));
    }

    [Fact]
    public void TheHeartbeatNamesEveryFigureItsSourcesProvide()
    {
        var files = new Dictionary<string, string>
        {
            ["/proc/self/status"] = "VmRSS:\t 524288 kB\nRssAnon:\t 400000 kB\nRssFile:\t 120000 kB\nRssShmem:\t 4288 kB\n",
            ["/proc/self/stat"] = "1 (dotnet) S 1 1 1 0 -1 0 5 0 12 0",
            ["/proc/meminfo"] = "MemTotal: 1789012 kB\nMemAvailable: 204800 kB\n",
            ["/sys/fs/cgroup/memory.current"] = "1073741824\n",
            ["/sys/fs/cgroup/memory.max"] = "1610612736\n",
            ["/sys/fs/cgroup/memory.stat"] = "anon 700000000\nfile 300000000\npgmajfault 33\n",
            ["/proc/loadavg"] = "0.75 0.60 0.55 2/300 999\n"
        };

        var line = RuntimeHeartbeat.Describe(path => files.GetValueOrDefault(path));

        Assert.StartsWith("vmrss_mb=512 rssanon_mb=390 rssfile_mb=117 rssshmem_mb=4 majflt=12 ", line, StringComparison.Ordinal);
        Assert.Contains(" mem_total_mb=1747 mem_available_mb=200 ", line, StringComparison.Ordinal);
        Assert.Contains(" cg_current_mb=1024 cg_max_mb=1536 cg_anon_mb=667 cg_file_mb=286 cg_pgmajfault=33 ", line, StringComparison.Ordinal);
        Assert.Contains(" load1=0.75 ", line, StringComparison.Ordinal);
        Assert.Contains(" gc_heap_mb=", line, StringComparison.Ordinal);
        Assert.Contains(" gc_total_mb=", line, StringComparison.Ordinal);
        Assert.Contains(" threads=", line, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnsetCgroupLimitReadsAsMax()
    {
        var files = new Dictionary<string, string>
        {
            ["/sys/fs/cgroup/memory.current"] = "1048576\n",
            ["/sys/fs/cgroup/memory.max"] = "max\n"
        };

        var line = RuntimeHeartbeat.Describe(path => files.GetValueOrDefault(path));

        Assert.Contains("cg_current_mb=1 cg_max_mb=max", line, StringComparison.Ordinal);
    }

    [Fact]
    public void CgroupVersionOneIsReadOnlyWhenVersionTwoIsAbsent()
    {
        var files = new Dictionary<string, string>
        {
            ["/sys/fs/cgroup/memory/memory.usage_in_bytes"] = "2097152\n",
            ["/sys/fs/cgroup/memory/memory.limit_in_bytes"] = "4194304\n",
            ["/sys/fs/cgroup/memory/memory.stat"] = "cache 1048576\nrss 524288\npgmajfault 4\n"
        };

        var line = RuntimeHeartbeat.Describe(path => files.GetValueOrDefault(path));

        Assert.Contains("cg_current_mb=2 cg_max_mb=4 cg_anon_mb=0 cg_file_mb=1 cg_pgmajfault=4", line, StringComparison.Ordinal);
    }

    [Fact]
    public void FiguresWhoseSourcesAreMissingAreLeftOutAndTheRuntimeOnesRemain()
    {
        var line = RuntimeHeartbeat.Describe(_ => null);

        foreach (var absent in new[] { "vmrss", "rssanon", "majflt", "mem_total", "mem_available", "cg_", "load1" })
        {
            Assert.DoesNotContain(absent, line, StringComparison.Ordinal);
        }

        Assert.StartsWith("gc_heap_mb=", line, StringComparison.Ordinal);
        Assert.Contains(" gc_total_mb=", line, StringComparison.Ordinal);
        Assert.Contains(" threads=", line, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnIntervalOfZeroTurnsTheHeartbeatOff()
    {
        var logger = new RecordingLogger();
        using var lifetime = new FakeLifetime();
        using var heartbeat = new RuntimeHeartbeat(lifetime, Configuration("00:00:00"), logger);

        await heartbeat.StartAsync(CancellationToken.None);
        lifetime.Start();
        await Assert.IsAssignableFrom<Task>(heartbeat.ExecuteTask).WaitAsync(TimeSpan.FromSeconds(30));
        await heartbeat.StopAsync(CancellationToken.None);

        Assert.Empty(logger.Messages);
    }

    [Fact]
    public async Task TheHeartbeatWaitsForTheHostToStartThenLogsOneTracePerBeat()
    {
        var logger = new RecordingLogger();
        using var lifetime = new FakeLifetime();
        using var heartbeat = new RuntimeHeartbeat(lifetime, Configuration("01:00:00"), logger);

        await heartbeat.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(300));
        Assert.Empty(logger.Messages);
        Assert.False(Assert.IsAssignableFrom<Task>(heartbeat.ExecuteTask).IsCompleted);

        lifetime.Start();
        var message = await logger.FirstMessage.WaitAsync(TimeSpan.FromSeconds(30));
        await heartbeat.StopAsync(CancellationToken.None);

        Assert.StartsWith("Runtime heartbeat: ", message, StringComparison.Ordinal);
        Assert.Contains("threads=", message, StringComparison.Ordinal);
        Assert.Single(logger.Messages);
        Assert.Equal(LogLevel.Information, logger.Levels[0]);
    }

    private static IConfiguration Configuration(string interval) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Diagnostics:HeartbeatInterval"] = interval })
            .Build();

    private sealed class FakeLifetime : IHostApplicationLifetime, IDisposable
    {
        private readonly CancellationTokenSource started = new();

        public CancellationToken ApplicationStarted => started.Token;

        public CancellationToken ApplicationStopping => CancellationToken.None;

        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void Start() => started.Cancel();

        public void Dispose() => started.Dispose();

        public void StopApplication()
        {
        }
    }

    private sealed class RecordingLogger : ILogger<RuntimeHeartbeat>
    {
        private readonly object gate = new();
        private readonly TaskCompletionSource<string> first = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<string> messages = [];
        private readonly List<LogLevel> levels = [];

        public Task<string> FirstMessage => first.Task;

        public string[] Messages
        {
            get
            {
                lock (gate)
                {
                    return messages.ToArray();
                }
            }
        }

        public LogLevel[] Levels
        {
            get
            {
                lock (gate)
                {
                    return levels.ToArray();
                }
            }
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            lock (gate)
            {
                messages.Add(message);
                levels.Add(logLevel);
            }

            first.TrySetResult(message);
        }
    }
}
