using System.Globalization;
using System.Text;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;

namespace Pegasus.Core.Support;

/// <summary>What the browser knew when the person pressed Send: the window, the agent, whether a record was being edited, and the last script errors.</summary>
public sealed record ProblemReportClientFacts(
    string? Viewport,
    string? UserAgent,
    bool Editing,
    IReadOnlyList<string> RecentErrors);

/// <summary>One of the person's own recent acts, from the action log, as the report carries it.</summary>
public sealed record ProblemReportAction(
    DateTimeOffset OccurredAtUtc,
    string Area,
    string Operation,
    string Reference,
    string Result);

/// <summary>
/// The state captured with a report. Nothing here is document content, an
/// image, an e-mail body or a claimant's personal data: the route, the build,
/// the person, the Case reference, the trace, the person's own recent acts and
/// the browser facts are all it holds.
/// </summary>
public sealed record ProblemReportSnapshot(
    string Version,
    string SourceSha,
    DateTimeOffset OccurredAtUtc,
    string Route,
    string Method,
    string TraceId,
    string ActorName,
    string ActorRole,
    string? CaseReference,
    string? ExceptionType,
    string? ExceptionMessage,
    IReadOnlyList<ProblemReportAction> RecentActions,
    ProblemReportClientFacts Client);

public enum ProblemReportStatus
{
    Sent,
    NotSent
}

public sealed record ProblemReport(
    Guid Id,
    Guid StaffId,
    string Description,
    ProblemReportSnapshot Snapshot,
    DateTimeOffset CreatedAtUtc,
    ProblemReportStatus Status,
    int? IssueNumber,
    string? IssueUrl,
    string? Failure,
    DateTimeOffset? SentAtUtc,
    DateTimeOffset? DispatchClaimExpiresAtUtc,
    string? DispatchClaimToken);

public sealed record NewProblemReport(
    Guid StaffId,
    string Description,
    ProblemReportSnapshot Snapshot,
    DateTimeOffset CreatedAtUtc);

/// <summary>Where the report went: the issue it became.</summary>
public sealed record ProblemReportDelivery(int IssueNumber, string IssueUrl);

public interface IProblemReportStore
{
    Task<ProblemReport> AddAsync(NewProblemReport report, CancellationToken cancellationToken);

    Task<ProblemReport?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Every retained report, newest first.</summary>
    Task<IReadOnlyList<ProblemReport>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Atomically claims an unsent report for one dispatch attempt.</summary>
    Task<ProblemReport?> TryClaimAsync(
        Guid id,
        string claimToken,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken);

    Task<ProblemReport> MarkSentAsync(Guid id, string claimToken, ProblemReportDelivery delivery, DateTimeOffset atUtc, CancellationToken cancellationToken);

    Task<ProblemReport> MarkNotSentAsync(Guid id, string claimToken, string failure, CancellationToken cancellationToken);
}

/// <summary>The outward side: raises the report as an issue, or throws with the reason it could not.</summary>
public interface IProblemReportSink
{
    Task<ProblemReportDelivery> SendAsync(ProblemReport report, CancellationToken cancellationToken);
}

public static class ProblemReportPolicy
{
    public const int MaximumDescriptionLength = 4000;
    public const int RecentActionCount = 20;
    public const int RecentErrorCount = 10;
    public static readonly TimeSpan RecentActionWindow = TimeSpan.FromDays(7);
    public static readonly TimeSpan DispatchClaimLease = TimeSpan.FromMinutes(2);

    public static string ValidateDescription(string? description)
    {
        var value = (description ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        if (value.Length == 0 || value.Length > MaximumDescriptionLength
            || value.Any(character => char.IsControl(character) && character != '\n'))
        {
            throw new ArgumentException(
                $"A problem report says what happened, in 1 to {MaximumDescriptionLength} characters.", nameof(description));
        }

        return value;
    }

    /// <summary>"Problem report: /Cases/… (QDOS26001)" — the route and the Case, so the issue list reads on its own.</summary>
    public static string Title(ProblemReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var route = report.Snapshot.Route.Length > 80 ? report.Snapshot.Route[..80] + "…" : report.Snapshot.Route;
        return report.Snapshot.CaseReference is { Length: > 0 } reference
            ? $"Problem report: {route} ({reference})"
            : $"Problem report: {route}";
    }

    /// <summary>The issue body: the person's words first, then the snapshot as a fenced block.</summary>
    public static string Body(ProblemReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var snapshot = report.Snapshot;
        var builder = new StringBuilder();
        builder.Append(report.Description.Trim()).Append("\n\n");
        builder.Append("Reported by ").Append(snapshot.ActorName).Append(" (").Append(snapshot.ActorRole).Append(") at ")
            .Append(snapshot.OccurredAtUtc.ToString("O", CultureInfo.InvariantCulture)).Append(".\n\n");
        builder.Append("```\n");
        builder.Append("version    ").Append(snapshot.Version).Append('\n');
        builder.Append("source     ").Append(snapshot.SourceSha).Append('\n');
        builder.Append("route      ").Append(snapshot.Method).Append(' ').Append(snapshot.Route).Append('\n');
        builder.Append("trace      ").Append(snapshot.TraceId).Append('\n');
        builder.Append("case       ").Append(snapshot.CaseReference ?? "-").Append('\n');
        if (snapshot.ExceptionType is not null)
        {
            builder.Append("exception  ").Append(snapshot.ExceptionType).Append(": ").Append(snapshot.ExceptionMessage).Append('\n');
        }

        builder.Append("viewport   ").Append(snapshot.Client.Viewport ?? "-").Append('\n');
        builder.Append("agent      ").Append(snapshot.Client.UserAgent ?? "-").Append('\n');
        builder.Append("editing    ").Append(snapshot.Client.Editing ? "yes" : "no").Append('\n');
        builder.Append("```\n");
        if (snapshot.RecentActions.Count > 0)
        {
            builder.Append("\nRecent actions (newest first)\n\n```\n");
            foreach (var action in snapshot.RecentActions)
            {
                builder.Append(action.OccurredAtUtc.ToString("u", CultureInfo.InvariantCulture)).Append("  ")
                    .Append(action.Area).Append(" / ").Append(action.Operation).Append("  ")
                    .Append(action.Reference).Append("  ").Append(action.Result).Append('\n');
            }

            builder.Append("```\n");
        }

        if (snapshot.Client.RecentErrors.Count > 0)
        {
            builder.Append("\nBrowser errors (newest first)\n\n```\n");
            foreach (var error in snapshot.Client.RecentErrors)
            {
                builder.Append(error).Append('\n');
            }

            builder.Append("```\n");
        }

        return builder.ToString();
    }

    public static Guid RequireStaff(ActionActor actor, StaffAccessRight right)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, right);
        return actor.Kind == ActorKind.Staff && Guid.TryParse(actor.SubjectId, out var id) && id != Guid.Empty
            ? id
            : throw new StaffAuthorizationException(right);
    }
}

/// <summary>
/// What the caller knows without the log: the page, the build, the person and
/// the browser. <see cref="ReportProblem"/> adds the person's own recent acts.
/// </summary>
public sealed record ProblemReportRequest(
    ActionActor Actor,
    string? Description,
    string Version,
    string SourceSha,
    string Route,
    string Method,
    string TraceId,
    string ActorName,
    string ActorRole,
    string? CaseReference,
    string? ExceptionType,
    string? ExceptionMessage,
    ProblemReportClientFacts Client);

/// <summary>
/// Stores the report, then raises it. A failed raise leaves the row Not sent
/// with the reason, so nothing a person wrote is lost; an Administrator
/// retries from the list.
/// </summary>
public sealed class ReportProblem(
    IProblemReportStore store,
    IProblemReportSink sink,
    IActionLogQueries actionLogs,
    TimeProvider timeProvider)
{
    private readonly IProblemReportStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly IProblemReportSink _sink = sink ?? throw new ArgumentNullException(nameof(sink));
    private readonly IActionLogQueries _actionLogs = actionLogs ?? throw new ArgumentNullException(nameof(actionLogs));
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public async Task<ProblemReport> ExecuteAsync(ProblemReportRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var staffId = ProblemReportPolicy.RequireStaff(request.Actor, StaffAccessRight.AccessStaffApplication);
        var description = ProblemReportPolicy.ValidateDescription(request.Description);
        var now = _timeProvider.GetUtcNow();
        var actions = await RecentActionsAsync(request.Actor, now, cancellationToken);
        var snapshot = new ProblemReportSnapshot(
            request.Version,
            request.SourceSha,
            now,
            request.Route,
            request.Method,
            request.TraceId,
            request.ActorName,
            request.ActorRole,
            request.CaseReference,
            request.ExceptionType,
            request.ExceptionMessage,
            actions,
            request.Client with
            {
                RecentErrors = request.Client.RecentErrors.Take(ProblemReportPolicy.RecentErrorCount).ToArray()
            });
        var report = await _store.AddAsync(new NewProblemReport(staffId, description, snapshot, now), cancellationToken);
        return await ProblemReportDispatch.SendAsync(_store, _sink, report.Id, _timeProvider, cancellationToken)
            ?? throw new InvalidOperationException($"Problem report {report.Id:D} was not found after it was stored.");
    }

    private async Task<IReadOnlyList<ProblemReportAction>> RecentActionsAsync(
        ActionActor actor,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        try
        {
            var page = await _actionLogs.ListAsync(
                new ActionLogFilter(
                    now - ProblemReportPolicy.RecentActionWindow,
                    now,
                    SearchText: null,
                    Area: null,
                    Actor: null,
                    Result: null,
                    Operation: null,
                    Record: null,
                    CorrelationId: null,
                    PageSize: ProblemReportPolicy.RecentActionCount,
                    ActingActor: actor.SubjectId),
                cancellationToken);
            return page.Rows
                .Select(row => new ProblemReportAction(row.OccurredAtUtc, row.Area, row.Operation, row.Reference, row.Result))
                .ToArray();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The log is context, not the report; a report without it still goes.
            return [];
        }
    }
}

/// <summary>An Administrator's retry of a report that was not sent.</summary>
public sealed class RetryProblemReport(
    IProblemReportStore store,
    IProblemReportSink sink,
    TimeProvider timeProvider)
{
    private readonly IProblemReportStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly IProblemReportSink _sink = sink ?? throw new ArgumentNullException(nameof(sink));
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public async Task<ProblemReport?> ExecuteAsync(ActionActor actor, Guid id, CancellationToken cancellationToken)
    {
        ProblemReportPolicy.RequireStaff(actor, StaffAccessRight.ViewOperationalReports);
        return await ProblemReportDispatch.SendAsync(_store, _sink, id, _timeProvider, cancellationToken);
    }
}

public sealed class ListProblemReports(IProblemReportStore store)
{
    private readonly IProblemReportStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public Task<IReadOnlyList<ProblemReport>> ExecuteAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        ProblemReportPolicy.RequireStaff(actor, StaffAccessRight.ViewOperationalReports);
        return _store.ListAsync(cancellationToken);
    }
}

internal static class ProblemReportDispatch
{
    public static async Task<ProblemReport?> SendAsync(
        IProblemReportStore store,
        IProblemReportSink sink,
        Guid id,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var claimToken = Guid.NewGuid().ToString("N");
        var nowUtc = timeProvider.GetUtcNow();
        var report = await store.TryClaimAsync(
            id,
            claimToken,
            nowUtc,
            ProblemReportPolicy.DispatchClaimLease,
            cancellationToken);
        if (report is null)
        {
            return await store.GetAsync(id, cancellationToken);
        }

        ProblemReportDelivery delivery;
        try
        {
            delivery = await sink.SendAsync(report, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var failure = exception.Message.Length > 400 ? exception.Message[..400] : exception.Message;
            return await store.MarkNotSentAsync(report.Id, claimToken, failure, cancellationToken);
        }

        return await store.MarkSentAsync(report.Id, claimToken, delivery, timeProvider.GetUtcNow(), cancellationToken);
    }
}
