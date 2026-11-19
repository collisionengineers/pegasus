using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace Pegasus.Infrastructure.Glass;

/// <summary>
/// An editor's main-frame address the Pegasus helper handed over, read
/// (<see cref="GlassEditorPages.HelpFrame"/>): the return token its
/// <c>caller</c> names, which finds the session, and the editor session the
/// address opens, with the cookies of Glass's load balancer the helper read
/// in that frame. All are secrets; <see cref="ToString"/> says none.
/// </summary>
public sealed class GlassHelpFrame
{
    /// <summary>The longest load balancer cookie kept; Glass's own are 124 characters.</summary>
    private const int MaximumBalancerCookie = 512;

    internal GlassHelpFrame(string token, GlassEditorSession session)
    {
        Token = token;
        Session = session;
    }

    /// <summary>
    /// The cookies Glass's load balancer sets, in the order a browser sends
    /// them. They name the one Glass's server that holds a window's editor
    /// session; a request without them reaches whichever server the balancer
    /// picks, and any other answers a page with none of the editor's lists
    /// (first live session, 19 November 2026). Neither is a login. The
    /// helper's own list must equal this one
    /// (<c>GlassHelperExtensionContractTests</c>).
    /// </summary>
    public static IReadOnlyList<string> BalancerCookieNames { get; } = ["AWSALB", "AWSALBCORS"];

    /// <summary>The session's one-use return token, read from the address and never sent beside it.</summary>
    public string Token { get; }

    /// <summary>
    /// The editor session the address opens: its base, <c>url0</c> and key,
    /// and the load balancer's cookies the helper handed over beside it.
    /// </summary>
    internal GlassEditorSession Session { get; }

    /// <summary>How many of the load balancer's cookies came with the key: all the host log says of them.</summary>
    public int BalancerCookies => Session.Cookies.Count;

    /// <summary>
    /// What is kept of the cookies a hand-off names: the load balancer's own
    /// (<see cref="BalancerCookieNames"/>), each a value of the base64
    /// alphabet as the balancer writes it. Any other name or value is left
    /// out, so nothing a sender chose reaches a request's header.
    /// </summary>
    internal static ReadOnlyDictionary<string, string> Balancer(IReadOnlyDictionary<string, string>? handedOver)
    {
        var kept = new Dictionary<string, string>(StringComparer.Ordinal);
        if (handedOver is not null)
        {
            foreach (var name in BalancerCookieNames)
            {
                if (handedOver.TryGetValue(name, out var value) && IsBalancerValue(value))
                {
                    kept[name] = value;
                }
            }
        }

        return kept.AsReadOnly();
    }

    private static bool IsBalancerValue([NotNullWhen(true)] string? value) =>
        value is { Length: > 0 and <= MaximumBalancerCookie }
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '+' or '/' or '=');

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
/// Beside each key it keeps the load balancer's cookies, first as the helper
/// handed them over and then as Glass's set them again on Pegasus's own
/// requests, and when Pegasus last sent one, how many times Pegasus has
/// asked the window to show its selected list again, and the last of those
/// asks the window says it has shown. One help act at a time holds a
/// session (<see cref="TryEnter"/>), so Pegasus never sends two requests on
/// one key at once.
/// </para>
/// <para>
/// Public because the Web host's Help page and Case page hold it; what it
/// holds stays inside this assembly.
/// </para>
/// </remarks>
public sealed class GlassHelpKeys(GlassRepairEstimateOptions options, TimeProvider timeProvider)
{
    /// <summary>How long a key is kept with no poll.</summary>
    public static readonly TimeSpan Silence = TimeSpan.FromSeconds(30);

    /// <summary>How often the helper polls while help is on: a position shows in the window within one poll.</summary>
    public static readonly TimeSpan HelpOnPoll = TimeSpan.FromSeconds(2);

    /// <summary>How often the helper polls while help is off.</summary>
    public static readonly TimeSpan HelpOffPoll = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How long an act waits to hear that the window has shown it: one poll
    /// at help's cadence, the helper's report a second after its reload, and
    /// a second for the way there and back.
    /// </summary>
    public static readonly TimeSpan ShowWait = HelpOnPoll + TimeSpan.FromSeconds(2);

    /// <summary>How often a waiting act looks again.</summary>
    private static readonly TimeSpan ShowLook = TimeSpan.FromMilliseconds(100);

    private readonly ConcurrentDictionary<Guid, Held> held = new();

    private readonly ConcurrentDictionary<Guid, byte> busy = new();

    /// <summary>
    /// The handed-over address read, with the load balancer's cookies the
    /// helper read beside it (<see cref="GlassHelpFrame.Balancer"/>), or null
    /// when the address is not this host's editor frame.
    /// </summary>
    public GlassHelpFrame? Read(string? frameAddress, IReadOnlyDictionary<string, string>? balancerCookies = null) =>
        GlassEditorPages.HelpFrame(frameAddress, options, balancerCookies);

    /// <summary>
    /// Keeps the frame's key for the session, replacing any older one. The
    /// refresh count and what the window has shown carry over to the newer
    /// key, so the helper's next poll reads the same count.
    /// </summary>
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
            _ => new Held(frame.Session, now, 0, 0, null),
            (_, older) => new Held(frame.Session, now, older.Refresh, older.Seen, null));
    }

    /// <summary>
    /// A poll from the session's window: it keeps a held key alive, records
    /// the last ask to show the selected list again that the window says it
    /// has shown (<paramref name="seen"/>, never past the count), and says
    /// whether a key is held. A key past <see cref="Silence"/> is dropped first.
    /// </summary>
    public GlassHelpPoll Poll(Guid sessionId, long seen)
    {
        var now = timeProvider.GetUtcNow();
        while (Current(sessionId, now) is { } entry)
        {
            var shown = Math.Max(entry.Seen, Math.Min(seen, entry.Refresh));
            // A lost race with a newer hand-off or an act is tried again.
            if (held.TryUpdate(sessionId, entry with { SeenAtUtc = now, Seen = shown }, entry))
            {
                return new(true, entry.Refresh);
            }
        }

        return new(false, 0);
    }

    /// <summary>Whether a live key is held for the session: the Case page's "connected", a memory read.</summary>
    public bool IsHeld(Guid sessionId) => Current(sessionId, timeProvider.GetUtcNow()) is not null;

    /// <summary>Forgets the session's key: the session is no longer <c>Active</c>.</summary>
    public void Drop(Guid sessionId) => held.TryRemove(sessionId, out _);

    /// <summary>The held editor session, or null when no live key is held.</summary>
    internal GlassEditorSession? Session(Guid sessionId) =>
        Current(sessionId, timeProvider.GetUtcNow())?.Session;

    /// <summary>When Pegasus last sent a request on the session's key, or null before its first.</summary>
    internal DateTimeOffset? LastRequestAtUtc(Guid sessionId) =>
        held.TryGetValue(sessionId, out var entry) ? entry.LastRequestAtUtc : null;

    /// <summary>
    /// Keeps the cookies Glass's set on Pegasus's requests and when the last
    /// was sent, while the key they were sent on is still the one held; a
    /// newer key handed over meanwhile is left as it is.
    /// </summary>
    internal void Keep(Guid sessionId, GlassEditorSession session, DateTimeOffset? lastRequestAtUtc)
    {
        ArgumentNullException.ThrowIfNull(session);
        while (held.TryGetValue(sessionId, out var entry)
            && string.Equals(entry.Session.Key, session.Key, StringComparison.Ordinal)
            && string.Equals(entry.Session.Url0, session.Url0, StringComparison.Ordinal))
        {
            if (held.TryUpdate(
                sessionId,
                entry with { Session = entry.Session with { Cookies = session.Cookies }, LastRequestAtUtc = lastRequestAtUtc },
                entry))
            {
                return;
            }
        }
    }

    /// <summary>
    /// Asks the window to show its selected list again: the session's
    /// refresh count rises by one and the new count is answered; zero when no
    /// key is held, as no window would hear it.
    /// </summary>
    internal long Refresh(Guid sessionId)
    {
        while (held.TryGetValue(sessionId, out var entry))
        {
            var raised = entry with { Refresh = entry.Refresh + 1 };
            if (held.TryUpdate(sessionId, raised, entry))
            {
                return raised.Refresh;
            }
        }

        return 0;
    }

    /// <summary>
    /// Whether the window says it has shown the ask numbered
    /// <paramref name="refresh"/>, waiting for at most <see cref="ShowWait"/>.
    /// </summary>
    internal async Task<bool> ShownAsync(Guid sessionId, long refresh, CancellationToken cancellationToken)
    {
        if (refresh <= 0)
        {
            return false;
        }

        // Counted, not timed by the clock, so a clock that stands still cannot hold an act.
        var looks = (int)(ShowWait / ShowLook);
        for (var look = 0; look <= looks; look++)
        {
            if (held.TryGetValue(sessionId, out var entry) && entry.Seen >= refresh)
            {
                return true;
            }
            if (look < looks)
            {
                await Task.Delay(ShowLook, timeProvider, cancellationToken);
            }
        }

        return false;
    }

    /// <summary>
    /// The one-at-a-time gate: a holder while no other help act holds the
    /// session, or null when one does. Disposing the holder frees it.
    /// </summary>
    internal IDisposable? TryEnter(Guid sessionId) =>
        busy.TryAdd(sessionId, 0) ? new Gate(busy, sessionId) : null;

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
    /// One held key: the editor session with the cookies of Pegasus's own
    /// requests, when the window was last heard from, the count of asks to
    /// show the selected list again, the last of them the window has shown,
    /// and when Pegasus last sent a request on it.
    /// </summary>
    private sealed record Held(
        GlassEditorSession Session, DateTimeOffset SeenAtUtc, long Refresh, long Seen, DateTimeOffset? LastRequestAtUtc);

    private sealed class Gate(ConcurrentDictionary<Guid, byte> busy, Guid sessionId) : IDisposable
    {
        public void Dispose() => busy.TryRemove(sessionId, out _);
    }
}
