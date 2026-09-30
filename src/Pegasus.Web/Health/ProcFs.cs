using System.Globalization;

namespace Pegasus.Web.Health;

/// <summary>
/// Reads the few Linux process and container figures the runtime diagnostics
/// use: major page faults, resident memory, the container's memory cgroup and
/// the load average. Every read is best effort. A file that does not exist
/// (Windows, or a cgroup layout the container lacks) or does not parse gives
/// <c>null</c>, so a caller omits that figure and never fails. The parsers
/// take the file's text so they can be tested against samples.
/// </summary>
internal static class ProcFs
{
    private const int MajorFaultFieldAfterComm = 9;

    /// <summary>Major page faults of this process so far, or null when the file is absent.</summary>
    public static long? ReadMajorFaults() => ParseMajorFaults(ReadText("/proc/self/stat"));

    /// <summary>
    /// The <c>majflt</c> field of <c>/proc/self/stat</c>. The second field is the
    /// command name in parentheses and may itself hold spaces and parentheses,
    /// so the fields are counted from after the last closing parenthesis: state,
    /// ppid, pgrp, session, tty_nr, tpgid, flags, minflt, cminflt, majflt.
    /// </summary>
    public static long? ParseMajorFaults(string? stat)
    {
        if (stat is null)
        {
            return null;
        }

        var closing = stat.LastIndexOf(')');
        if (closing < 0)
        {
            return null;
        }

        var fields = stat[(closing + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return fields.Length > MajorFaultFieldAfterComm
            && long.TryParse(fields[MajorFaultFieldAfterComm], NumberStyles.None, CultureInfo.InvariantCulture, out var faults)
                ? faults
                : null;
    }

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

    private static string[] Lines(string? text) =>
        text is null
            ? []
            : text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
