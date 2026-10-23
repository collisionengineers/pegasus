using System.Globalization;
using Microsoft.Win32.SafeHandles;

namespace Pegasus.Web.Health;

/// <summary>
/// Reads the few Linux process and container figures the runtime diagnostics
/// use: major page faults, resident memory, the container's memory cgroup and
/// the load average. Every read is best effort. A file that does not exist
/// (Windows, or a cgroup layout the container lacks) or does not parse gives
/// <c>null</c>, so a caller omits that figure and never fails. The parsers
/// take the file's text or bytes so they can be tested against samples.
/// </summary>
internal static class ProcFs
{
    private const int MajorFaultFieldAfterComm = 9;

    // One sample for the whole process: every request shares it, so the file is
    // read at most once a second however many requests there are, and through
    // one handle opened once.
    private static readonly SampledMajorFaults SelfStat = new(
        new MajorFaultReader("/proc/self/stat").Read,
        static () => Environment.TickCount64);

    /// <summary>
    /// Major page faults of this process, or null when the file is absent. The
    /// figure is the one sampled in the last second and shared across requests, so
    /// a delta over a request is what the process did between two samples, and
    /// is zero for a request that starts and ends inside one second.
    /// </summary>
    public static long? ReadMajorFaults() => SelfStat.Read();

    /// <summary>
    /// The <c>majflt</c> field of <c>/proc/self/stat</c>, from the file's bytes, which
    /// may end part way through a later field. The second field is the command name
    /// in parentheses and may itself hold spaces and parentheses, so the fields are
    /// counted from after the last closing parenthesis: state, ppid, pgrp, session,
    /// tty_nr, tpgid, flags, minflt, cminflt, majflt.
    /// </summary>
    public static long? ParseMajorFaults(ReadOnlySpan<byte> stat)
    {
        var closing = stat.LastIndexOf((byte)')');
        if (closing < 0)
        {
            return null;
        }

        var rest = stat[(closing + 1)..];
        for (var field = 0; ; field++)
        {
            while (!rest.IsEmpty && IsSeparator(rest[0]))
            {
                rest = rest[1..];
            }

            if (rest.IsEmpty)
            {
                return null;
            }

            var end = 0;
            while (end < rest.Length && !IsSeparator(rest[end]))
            {
                end++;
            }

            if (field == MajorFaultFieldAfterComm)
            {
                return long.TryParse(rest[..end], NumberStyles.None, CultureInfo.InvariantCulture, out var faults)
                    ? faults
                    : null;
            }

            rest = rest[end..];
        }
    }

    private static bool IsSeparator(byte value) => value is (byte)' ' or (byte)'\t' or (byte)'\n' or (byte)'\r';

    /// <summary>
    /// A <c>Key:   value kB</c> line of <c>/proc/self/status</c> or
    /// <c>/proc/meminfo</c>, in bytes.
    /// </summary>
    public static long? ParseKilobyteLine(string? text, string key)
    {
        foreach (var line in Lines(text))
        {
            if (!line.StartsWith(key, StringComparison.Ordinal) || line.Length <= key.Length || line[key.Length] != ':')
            {
                continue;
            }

            var value = line[(key.Length + 1)..].Trim();
            var unit = value.IndexOf(' ');
            var number = unit < 0 ? value : value[..unit];
            return long.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var kilobytes)
                ? kilobytes * 1024
                : null;
        }

        return null;
    }

    /// <summary>A <c>key value</c> line of a cgroup <c>memory.stat</c> file.</summary>
    public static long? ParseKeyedValue(string? text, string key)
    {
        foreach (var line in Lines(text))
        {
            var separator = line.IndexOf(' ');
            if (separator == key.Length
                && line.StartsWith(key, StringComparison.Ordinal)
                && long.TryParse(line[(separator + 1)..].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>
    /// The one value of a cgroup file such as <c>memory.current</c>. A limit
    /// that is not set reads <c>max</c> (v2), which is returned as that word.
    /// </summary>
    public static string? ParseSingleValue(string? text)
    {
        var value = text?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        return value == "max" || long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out _)
            ? value
            : null;
    }

    /// <summary>The one-minute load average, the first field of <c>/proc/loadavg</c>.</summary>
    public static string? ParseLoadAverage(string? text)
    {
        var first = text?.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        return first is not null && double.TryParse(first, NumberStyles.Float, CultureInfo.InvariantCulture, out _)
            ? first
            : null;
    }

    /// <summary>The whole file, or null when it does not exist or cannot be read.</summary>
    public static string? ReadText(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Reads one <c>/proc</c> stat file through a handle opened on the first read
    /// and kept for the life of the process. A file that cannot be opened is
    /// remembered as absent (Windows has none), so later reads make no call. Each
    /// read is positional at offset 0 into its own stack buffer, so concurrent
    /// requests share the handle and never a buffer.
    /// </summary>
    internal sealed class MajorFaultReader(string path)
    {
        // Far more than the fields up to majflt need.
        private const int BufferBytes = 1024;

        private readonly Lazy<SafeFileHandle?> handle = new(() => Open(path), LazyThreadSafetyMode.ExecutionAndPublication);

        public long? Read()
        {
            if (handle.Value is not { } file)
            {
                return null;
            }

            try
            {
                Span<byte> buffer = stackalloc byte[BufferBytes];
                return ParseMajorFaults(buffer[..RandomAccess.Read(file, buffer, 0)]);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                return null;
            }
        }

        private static SafeFileHandle? Open(string path)
        {
            try
            {
                return File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Holds the last figure a reader gave for one second, so every request in
    /// that second shares it. Concurrent callers at the moment it expires may each
    /// read once and none ever waits, but only one publishes, so the sample only
    /// ever moves to the figure of the refresher that got there first.
    /// </summary>
    internal sealed class SampledMajorFaults(Func<long?> read, Func<long> milliseconds)
    {
        private const long PeriodMilliseconds = 1000;

        private Sample? current;

        public long? Read()
        {
            var now = milliseconds();
            var held = Volatile.Read(ref current);
            if (held is not null && now - held.At < PeriodMilliseconds)
            {
                return held.Faults;
            }

            // Only the caller that still finds the sample it found expired publishes
            // its own. One that lost to another refresher returns what was
            // published, so the figure a caller sees never goes back to an older
            // read and a delta over a request is never negative.
            var fresh = new Sample(now, read());
            return ReferenceEquals(Interlocked.CompareExchange(ref current, fresh, held), held)
                ? fresh.Faults
                : Volatile.Read(ref current)!.Faults;
        }

        private sealed record Sample(long At, long? Faults);
    }

    private static string[] Lines(string? text) =>
        text is null
            ? []
            : text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
