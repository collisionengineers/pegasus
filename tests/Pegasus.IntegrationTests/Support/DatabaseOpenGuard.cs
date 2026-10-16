using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Pegasus.IntegrationTests.Support;

/// <summary>
/// Refuses every connection a database-less test host opens, and records each
/// refusal: the context and the first Pegasus frames that asked for it.
/// </summary>
/// <remarks>
/// The refusal alone proves nothing: the shell's bell and release note catch
/// every failure and render the page anyway. The record is what
/// <see cref="IntakeWebApplicationFactory"/> reports when it is disposed, so a
/// swallowed refusal still fails the test. A connection opened outside Entity
/// Framework is not seen here; the host's unresolvable address stops it.
/// </remarks>
internal sealed class DatabaseOpenGuard : DbConnectionInterceptor
{
    private const int FramesRecorded = 4;
    private readonly ConcurrentQueue<string> attempts = new();

    /// <summary>Every refused open, in the order it was refused.</summary>
    public IReadOnlyList<string> Attempts => [.. attempts];

    public override InterceptionResult ConnectionOpening(
        DbConnection connection,
        ConnectionEventData eventData,
        InterceptionResult result) =>
        throw Refuse(eventData);

    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
        DbConnection connection,
        ConnectionEventData eventData,
        InterceptionResult result,
        CancellationToken cancellationToken = default) =>
        throw Refuse(eventData);

    private InvalidOperationException Refuse(ConnectionEventData eventData)
    {
        var attempt = $"{eventData.Context?.GetType().Name ?? "A context"} opened a connection from {Callers()}";
        attempts.Enqueue(attempt);
        return new InvalidOperationException($"This test host has no database. {attempt}");
    }

    private static string Callers() => string.Join(
        " <- ",
        new StackTrace().ToString()
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(frame => frame.StartsWith("at Pegasus.", StringComparison.Ordinal)
                && !frame.Contains(nameof(DatabaseOpenGuard), StringComparison.Ordinal))
            .Take(FramesRecorded)
            .Select(frame => frame["at ".Length..]));
}
