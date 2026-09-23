using System.Security.Cryptography;
using System.Globalization;
using System.Text;
using System.Text.Json;
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
    ProblemReportClientFacts Client,
    string? ExceptionDetails = null);

public enum ProblemReportStatus
{
    Sent,
    NotSent,
    Unknown
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
    string? DispatchClaimToken,
    string OperationKey = "",
    string RequestHash = "");

public sealed record NewProblemReport(
    Guid StaffId,
    string Description,
    ProblemReportSnapshot Snapshot,
    DateTimeOffset CreatedAtUtc,
    string OperationKey,
    string RequestHash);

public sealed record ProblemReportAddResult(ProblemReport Report, bool IsReplay);

public sealed class ProblemReportOperationConflictException : InvalidOperationException
{
    public ProblemReportOperationConflictException() : base("This problem report form was already used with different details.") { }
}

public sealed class ProblemReportClaimConflictException : InvalidOperationException
{
    public ProblemReportClaimConflictException() : base("The problem report dispatch claim changed; reconcile the report before retrying.") { }
}

public sealed class ProblemReportDeliveryUnknownException(string message, Exception? inner = null)
    : Exception(message, inner);

/// <summary>Where the report went: the issue it became.</summary>
public sealed record ProblemReportDelivery(int IssueNumber, string IssueUrl);

public interface IProblemReportStore
{
    Task<ProblemReportAddResult> AddAsync(NewProblemReport report, CancellationToken cancellationToken);

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

    Task<ProblemReport> MarkUnknownAsync(Guid id, string claimToken, string reason, CancellationToken cancellationToken);

    Task<ProblemReport> ConfirmIssueAsync(Guid id, ProblemReportDelivery delivery, DateTimeOffset atUtc, CancellationToken cancellationToken);

    Task<ProblemReport> ConfirmNoIssueAsync(Guid id, CancellationToken cancellationToken);
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

    public static string NormalizeOperationKey(string? operationKey) =>
        Guid.TryParse(operationKey, out var value) && value != Guid.Empty
            ? value.ToString("N")
            : throw new ArgumentException("The problem report form has expired. Open it again.", nameof(operationKey));

    public static string RequestHash(Guid staffId, string description, ProblemReportRequest request) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            staffId,
            description,
            request.Route,
            request.Method,
            request.TraceId,
            request.CaseReference,
            request.Client.Viewport,
            request.Client.Editing,
            Errors = request.Client.RecentErrors.Take(RecentErrorCount).ToArray()
        }))));

    /// <summary>The issue identifies the full report kept in Pegasus.</summary>
    public static string Title(ProblemReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var firstLine = report.Description.Split('\n', 2)[0].Trim();
        var summary = firstLine.Length > 100 ? firstLine[..100].TrimEnd() + "…" : firstLine;
        return $"Pegasus: {summary} [{report.Id:D}]";
    }

    /// <summary>The issue carries the reporter's words and the captured diagnostic context.</summary>
    public static string Body(ProblemReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var snapshot = report.Snapshot;
        var body = new StringBuilder();
        body.AppendLine("## What happened").AppendLine().AppendLine(report.Description).AppendLine();
        body.AppendLine("## Pegasus context").AppendLine();
        body.AppendLine(CultureInfo.InvariantCulture, $"- Report ID: `{report.Id:D}`");
        body.AppendLine(CultureInfo.InvariantCulture, $"- Occurred (UTC): `{snapshot.OccurredAtUtc:O}`");
        body.AppendLine(CultureInfo.InvariantCulture, $"- Page: `{snapshot.Route}`");
        body.AppendLine(CultureInfo.InvariantCulture, $"- Method: `{snapshot.Method}`");
        body.AppendLine(CultureInfo.InvariantCulture, $"- Trace ID: `{snapshot.TraceId}`");
        body.AppendLine(CultureInfo.InvariantCulture, $"- Version: `{snapshot.Version}`");
        body.AppendLine(CultureInfo.InvariantCulture, $"- Source SHA: `{snapshot.SourceSha}`");
        body.AppendLine(CultureInfo.InvariantCulture, $"- Reporter: {snapshot.ActorName} ({snapshot.ActorRole})");
        if (!string.IsNullOrWhiteSpace(snapshot.CaseReference))
            body.AppendLine(CultureInfo.InvariantCulture, $"- Case: `{snapshot.CaseReference}`");
        body.AppendLine(CultureInfo.InvariantCulture, $"- Viewport: {snapshot.Client.Viewport ?? "Unknown"}");
        body.AppendLine(CultureInfo.InvariantCulture, $"- Browser: {snapshot.Client.UserAgent ?? "Unknown"}");
        body.AppendLine(CultureInfo.InvariantCulture, $"- Editing: {(snapshot.Client.Editing ? "Yes" : "No")}");

        body.AppendLine().AppendLine("## Server exception").AppendLine();
        if (!string.IsNullOrWhiteSpace(snapshot.ExceptionDetails))
            AppendIndented(body, snapshot.ExceptionDetails);
        else if (!string.IsNullOrWhiteSpace(snapshot.ExceptionType))
            AppendIndented(body, $"{snapshot.ExceptionType}: {snapshot.ExceptionMessage}");
        else
            body.AppendLine("No server exception was captured for this report.");

        body.AppendLine().AppendLine("## Recent actions").AppendLine();
        if (snapshot.RecentActions.Count == 0)
            body.AppendLine("None captured.");
        else
            foreach (var action in snapshot.RecentActions)
                body.AppendLine(CultureInfo.InvariantCulture, $"- `{action.OccurredAtUtc:O}` {action.Area} · {action.Operation} · {action.Reference} · {action.Result}");

        body.AppendLine().AppendLine("## Browser errors").AppendLine();
        if (snapshot.Client.RecentErrors.Count == 0)
            body.AppendLine("None captured.");
        else
            foreach (var error in snapshot.Client.RecentErrors)
                body.AppendLine(CultureInfo.InvariantCulture, $"- {error}");
        return body.ToString().TrimEnd();
    }

    private static void AppendIndented(StringBuilder body, string value)
    {
        foreach (var line in value.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
            body.Append("    ").AppendLine(line);
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
    ProblemReportClientFacts Client,
    string OperationKey = "",
    string? ExceptionDetails = null);

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
        var operationKey = ProblemReportPolicy.NormalizeOperationKey(request.OperationKey);
        var requestHash = ProblemReportPolicy.RequestHash(staffId, description, request);
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
            },
            request.ExceptionDetails);
        var added = await _store.AddAsync(new NewProblemReport(
            staffId, description, snapshot, now, operationKey, requestHash), cancellationToken);
        if (added.IsReplay)
        {
            return added.Report;
        }

        return await ProblemReportDispatch.SendAsync(_store, _sink, added.Report.Id, _timeProvider, cancellationToken)
            ?? throw new InvalidOperationException($"Problem report {added.Report.Id:D} was not found after it was stored.");
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

/// <summary>An Administrator resolves an unknown GitHub outcome after checking the report ID.</summary>
public sealed class ReconcileProblemReport(IProblemReportStore store, TimeProvider timeProvider)
{
    public Task<ProblemReport> ConfirmIssueAsync(
        ActionActor actor, Guid id, ProblemReportDelivery delivery, CancellationToken cancellationToken)
    {
        ProblemReportPolicy.RequireStaff(actor, StaffAccessRight.ViewOperationalReports);
        if (id == Guid.Empty || delivery.IssueNumber <= 0
            || string.IsNullOrWhiteSpace(delivery.IssueUrl))
        {
            throw new ArgumentException("A matching issue is required to reconcile this report.");
        }

        return store.ConfirmIssueAsync(id, delivery, timeProvider.GetUtcNow(), cancellationToken);
    }

    public Task<ProblemReport> ConfirmNoIssueAsync(
        ActionActor actor, Guid id, CancellationToken cancellationToken)
    {
        ProblemReportPolicy.RequireStaff(actor, StaffAccessRight.ViewOperationalReports);
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A report ID is required.");
        }

        return store.ConfirmNoIssueAsync(id, cancellationToken);
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
        catch (ProblemReportDeliveryUnknownException exception)
        {
            var reason = exception.Message.Length > 400 ? exception.Message[..400] : exception.Message;
            return await store.MarkUnknownAsync(report.Id, claimToken, reason, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var failure = exception.Message.Length > 400 ? exception.Message[..400] : exception.Message;
            return await store.MarkNotSentAsync(report.Id, claimToken, failure, cancellationToken);
        }

        try
        {
            return await store.MarkSentAsync(report.Id, claimToken, delivery, timeProvider.GetUtcNow(), cancellationToken);
        }
        catch (ProblemReportClaimConflictException)
        {
            try
            {
                return await store.MarkUnknownAsync(report.Id, claimToken,
                    "GitHub accepted the issue, but the local dispatch claim changed. Reconcile by report ID.",
                    cancellationToken);
            }
            catch (ProblemReportClaimConflictException)
            {
                return await store.GetAsync(report.Id, cancellationToken);
            }
        }
    }
}
