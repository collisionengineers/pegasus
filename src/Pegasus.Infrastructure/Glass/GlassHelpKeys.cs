using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Pegasus.Infrastructure.Glass;

/// <summary>
/// An editor's main-frame address the Pegasus helper handed over, read
/// (<see cref="GlassEditorPages.HelpFrame"/>): the return token its
/// <c>caller</c> names, which finds the session, and the editor session the
/// address opens. Both are secrets; <see cref="ToString"/> says neither.
/// </summary>
public sealed class GlassHelpFrame
{
    internal GlassHelpFrame(string token, GlassEditorSession session)
    {
        Token = token;
        Session = session;
    }

    /// <summary>The session's one-use return token, read from the address and never sent beside it.</summary>
    public string Token { get; }

    /// <summary>The editor session the address opens: its base, <c>url0</c> and key, and no cookie.</summary>
    internal GlassEditorSession Session { get; }

    /// <summary>
    /// Whether the text is a return token as a launch mints it: 64
    /// lower-case hex characters (<c>GlassRepairEstimateGateway</c>).
    /// </summary>
    public static bool IsToken([NotNullWhen(true)] string? text) =>
        text is { Length: 64 } && text.All(char.IsAsciiHexDigitLower);

    public override string ToString() => nameof(GlassHelpFrame);
}

/// <summary>
/// What a poll learns: whether a key is held for the session, and how many
/// times Pegasus has asked the window to show its selected list again.
/// </summary>
public readonly record struct GlassHelpPoll(bool Held, long Refresh);

/// <summary>
/// The editor keys the Pegasus helper has handed over, one per Glass's
/// session, in this host's memory only (ADR-0091, R1170).
/// </summary>
/// <remarks>
/// <para>
/// A key is written to no store, no log and no error. A newer key for the
/// same session replaces the older one: a resume or the stall retry opens a
/// new editor under the same return token. A key is dropped when its
/// session is found not <c>Active</c>, when no poll has come for
/// <see cref="Silence"/> (the window closed or the helper went), and when the
/// host restarts; the helper's next poll then hears that a key is needed.
/// </para>
/// <para>
/// Public because the Web host's Help page holds it; what it holds stays
/// inside this assembly.
/// </para>
/// </remarks>
public sealed class GlassHelpKeys(GlassRepairEstimateOptions options, TimeProvider timeProvider)
{
    /// <summary>How long a key is kept with no poll.</summary>
    public static readonly TimeSpan Silence = TimeSpan.FromSeconds(30);

    private readonly ConcurrentDictionary<Guid, Held> held = new();

    /// <summary>The handed-over address read, or null when it is not this host's editor frame.</summary>
    public GlassHelpFrame? Read(string? frameAddress) => GlassEditorPages.HelpFrame(frameAddress, options);

    /// <summary>Keeps the frame's key for the session, replacing any older one.</summary>
    public void Hold(Guid sessionId, GlassHelpFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var now = timeProvider.GetUtcNow();
        foreach (var entry in held)
        {
            if (!Fresh(entry.Value, now))
            {
                held.TryRemove(entry);
            }
        }

        held.AddOrUpdate(
            sessionId,
            _ => new Held(frame.Session, now, 0),
            (_, older) => new Held(frame.Session, now, older.Refresh));
    }

    /// <summary>
    /// A poll from the session's window: it keeps a held key alive and says
    /// whether one is held. A key past <see cref="Silence"/> is dropped first.
    /// </summary>
    public GlassHelpPoll Poll(Guid sessionId)
    {
        var now = timeProvider.GetUtcNow();
        if (Current(sessionId, now) is not { } entry)
        {
            return new(false, 0);
        }

        // A lost race with a newer hand-off leaves the newer one, which is as fresh.
        held.TryUpdate(sessionId, entry with { SeenAtUtc = now }, entry);
        return new(true, entry.Refresh);
    }

    /// <summary>Forgets the session's key: the session is no longer <c>Active</c>.</summary>
    public void Drop(Guid sessionId) => held.TryRemove(sessionId, out _);

    /// <summary>The held editor session, or null when no live key is held.</summary>
    internal GlassEditorSession? Session(Guid sessionId) =>
        Current(sessionId, timeProvider.GetUtcNow())?.Session;

    private Held? Current(Guid sessionId, DateTimeOffset now)
    {
        if (!held.TryGetValue(sessionId, out var entry))
        {
            return null;
        }
        if (Fresh(entry, now))
        {
            return entry;
        }

        held.TryRemove(new KeyValuePair<Guid, Held>(sessionId, entry));
        return null;
    }

    private static bool Fresh(Held entry, DateTimeOffset now) => now - entry.SeenAtUtc <= Silence;

    /// <summary>
    /// One held key: the editor session, when the window was last heard from,
    /// and the count of asks to show the selected list again, which nothing
    /// raises yet.
    /// </summary>
    private sealed record Held(GlassEditorSession Session, DateTimeOffset SeenAtUtc, long Refresh);
}
