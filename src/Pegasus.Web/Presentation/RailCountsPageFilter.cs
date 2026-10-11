using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using Pegasus.Core.Actors;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Core.Notifications;
using Pegasus.Core.ReleaseNotes;
using Pegasus.Core.Operations;
using Pegasus.Core.Triage;

namespace Pegasus.Web.Presentation;

/// <summary>
/// Supplies the shell's own figures on each authenticated full page result —
/// <c>ViewData["RailCounts"]</c>, <c>ViewData["ShellRenderedAtUtc"]</c>, and the
/// bell's <c>ViewData["Notifications"]</c> — so <c>_Layout.cshtml</c> never
/// carries a shell-invented figure.
/// </summary>
/// <remarks>
/// The dictionary keys are the rail routes that can carry a count —
/// <c>Inbox</c> and <c>Cases</c>. <c>Cases</c> is the workspace
/// contract sum, not_ready + review + with_engineer + query + held + triage +
/// unidentified, read from the same queries the Cases page itself runs:
/// <see cref="IDashboardQueries.GetCaseStageCountsAsync"/> (one grouped
/// aggregate), <see cref="IListTriage"/> (the active-Triage total; the rows are
/// not projected beyond page one) and
/// <see cref="IUnidentifiedStore.ListQueueAsync"/>. Inbox has no established figure
/// to reuse without inventing one, so it is absent — the layout renders nothing
/// for a missing key, never a stale zero.
///
/// The bell is the person's own notifications (Work Centre D10), newest first,
/// read once here; the unread count is derived from the same list rather than
/// read a second time. A failed read sets <c>NotificationsUnavailable</c> so the
/// dialog states it, and the page still renders (FRD-12). The counts, the
/// notifications and the release note start together.
///
/// A global <c>IAsyncPageFilter</c> is the direct ASP.NET Core mechanism for
/// shared page <c>ViewData</c>. It waits for the selected handler's result:
/// redirects, file responses, heartbeat responses and lazy section partials do
/// not render the shell, so they do not issue its database reads. A
/// <see cref="PageResult"/> still receives the data, including an invalid form
/// that returns its page with validation errors. The one exception is the Case
/// page's own full-page GET, whose shell reads start before the handler and
/// overlap it (operator ruling, 10 Oct 2026); a POST never pre-reads, so its
/// figures are never read ahead of what it changes.
/// </remarks>
public sealed partial class RailCountsPageFilter(
    IDashboardQueries dashboardQueries,
    IListTriage listTriage,
    IUnidentifiedStore unidentifiedStore,
    IMyStaffNotifications myNotifications,
    IMyReleaseNotes myReleaseNotes,
    TimeProvider timeProvider,
    ILogger<RailCountsPageFilter> logger) : IAsyncPageFilter
{
    private static readonly object CaseCountsKey = new();

    private readonly IDashboardQueries dashboardQueries =
        dashboardQueries ?? throw new ArgumentNullException(nameof(dashboardQueries));
    private readonly IListTriage listTriage =
        listTriage ?? throw new ArgumentNullException(nameof(listTriage));
    private readonly IUnidentifiedStore unidentifiedStore =
        unidentifiedStore ?? throw new ArgumentNullException(nameof(unidentifiedStore));
    private readonly IMyStaffNotifications myNotifications =
        myNotifications ?? throw new ArgumentNullException(nameof(myNotifications));
    private readonly IMyReleaseNotes myReleaseNotes =
        myReleaseNotes ?? throw new ArgumentNullException(nameof(myReleaseNotes));
    private readonly TimeProvider timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(
        PageHandlerExecutingContext context,
        PageHandlerExecutionDelegate next)
    {
        var user = context.HttpContext.User;
        var hasActor = StaffActorFactory.TryCreate(
            user.FindFirstValue(ClaimTypes.NameIdentifier),
            user.FindAll(ClaimTypes.Role).Select(claim => claim.Value),
            out var actor);
        var authenticated = user.Identity?.IsAuthenticated == true && hasActor;
        // The Case page's own GET renders the shell on every result but a
        // refusal, and reads no figure the shell needs first, so its shell
        // reads start before the handler and run beside it.
        var early = authenticated && IsCaseFullPageGet(context)
            ? StartShellReads(context.HttpContext, actor!)
            : null;
        PageHandlerExecutedContext result;
        try
        {
            result = await next();
        }
        catch
        {
            await DrainAsync(early);
            throw;
        }

        if (result.Result is PageResult
            && authenticated
            && context.HandlerInstance is PageModel pageModel)
        {
            var shell = early ?? StartShellReads(context.HttpContext, actor!);
            await shell.WhenAllAsync();
            var countsTask = shell.Counts;
            var notificationsTask = shell.Notifications;
            var releaseNoteTask = shell.ReleaseNote;

            pageModel.ViewData["RailCounts"] = new Dictionary<string, int>
            {
                ["Cases"] = countsTask.Result.Total
            };
            pageModel.ViewData["ShellRenderedAtUtc"] = timeProvider.GetUtcNow();
            if (notificationsTask.Result is { } notifications)
            {
                pageModel.ViewData["Notifications"] = notifications;
            }
            else
            {
                pageModel.ViewData["NotificationsUnavailable"] = true;
            }

            if (releaseNoteTask.Result is { } releaseNote)
            {
                pageModel.ViewData["ReleaseNote"] = releaseNote;
            }
        }
        else
        {
            await DrainAsync(early);
        }
    }

    /// <summary>
    /// The Case workspace's unnamed GET handler, the full page. Its sections,
    /// refreshes and every POST are other handlers and keep reading the shell
    /// after their result.
    /// </summary>
    private static bool IsCaseFullPageGet(PageHandlerExecutingContext context) =>
        context.HandlerInstance is Pegasus.Web.Pages.Cases.DetailsModel
        && context.HandlerMethod is { HttpMethod: "GET", Name: null or "" };

    /// <summary>
    /// The counts, the bell and the release note share nothing and each reads
    /// on its own context, so they start together. A counts failure still
    /// propagates; the other two answer for themselves.
    /// </summary>
    private ShellReads StartShellReads(HttpContext httpContext, ActionActor actor)
    {
        var cancellationToken = httpContext.RequestAborted;
        return new(
            LoadCaseCountsAsync(httpContext, actor, cancellationToken),
            ReadNotificationsAsync(actor, cancellationToken),
            ReadReleaseNoteAsync(actor, cancellationToken));
    }

    /// <summary>Waits out reads started for a page that did not render, so none outlives the request.</summary>
    private static async Task DrainAsync(ShellReads? reads)
    {
        if (reads is null)
        {
            return;
        }

        try
        {
            await reads.WhenAllAsync();
        }
        catch (Exception)
        {
            // The page is not rendering the shell, so a failed read has no reader.
        }
    }

    private sealed record ShellReads(
        Task<CaseRailCounts> Counts,
        Task<IReadOnlyList<StaffNotification>?> Notifications,
        Task<ReleaseNote?> ReleaseNote)
    {
        public Task WhenAllAsync() => Task.WhenAll(Counts, Notifications, ReleaseNote);
    }

    /// <summary>The bell's notifications, or null when the read failed and the dialog must say so.</summary>
    private async Task<IReadOnlyList<StaffNotification>?> ReadNotificationsAsync(
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        try
        {
            using var timing = DocumentReadTelemetry.Start("web.shell.notifications");
            return await myNotifications.ListAsync(actor, cancellationToken);
        }
        catch (Exception exception) when (exception is not
            (OperationCanceledException or StaffAuthorizationException or UnauthorizedAccessException))
        {
            LogNotificationsUnavailable(logger, exception);
            return null;
        }
    }

    /// <summary>
    /// What's new (FRD-12): the newest published release note this person has
    /// not acknowledged opens once as a dialog. A failed read shows nothing
    /// rather than blocking the page.
    /// </summary>
    private async Task<ReleaseNote?> ReadReleaseNoteAsync(
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        try
        {
            return await myReleaseNotes.GetUnacknowledgedAsync(actor, cancellationToken);
        }
        catch (Exception exception) when (exception is not
            (OperationCanceledException or StaffAuthorizationException or UnauthorizedAccessException))
        {
            LogReleaseNoteUnavailable(logger, exception);
            return null;
        }
    }

    /// <summary>
    /// Carries a page's already-read totals through this one request to the
    /// shell filter. It is never a cross-request cache. A page that
    /// did not read the every-state Triage count passes null for it, and the
    /// shell reads that one alone.
    /// </summary>
    public static void SetCaseCounts(
        HttpContext context,
        CaseStageCounts stages,
        int? triageCount,
        int unidentifiedCount)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Items[CaseCountsKey] = new KnownCaseCounts(stages, triageCount, unidentifiedCount);
    }

    private async Task<CaseRailCounts> LoadCaseCountsAsync(
        HttpContext httpContext,
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        var known = httpContext.Items.TryGetValue(CaseCountsKey, out var value)
            ? value as KnownCaseCounts
            : null;
        if (known is { TriageCount: { } knownTriageCount })
        {
            return new(known.Stages, knownTriageCount, known.UnidentifiedCount);
        }

        using var timing = DocumentReadTelemetry.Start("web.shell.counts");
        var stagesTask = known is null
            ? dashboardQueries.GetCaseStageCountsAsync(cancellationToken)
            : Task.FromResult(known.Stages);
        var triageTask = listTriage.CountAsync(
            actor,
            TriageLifecycleRules.ActiveStates,
            cancellationToken);
        var unidentifiedTask = known is null
            ? unidentifiedStore.CountOpenAsync(cancellationToken)
            : Task.FromResult(known.UnidentifiedCount);
        await Task.WhenAll(stagesTask, triageTask, unidentifiedTask);
        return new(stagesTask.Result, triageTask.Result, unidentifiedTask.Result);
    }

    private sealed record KnownCaseCounts(
        CaseStageCounts Stages,
        int? TriageCount,
        int UnidentifiedCount);

    private sealed record CaseRailCounts(
        CaseStageCounts Stages,
        int TriageCount,
        int UnidentifiedCount)
    {
        public int Total => Stages.NotReady
            + Stages.Review
            + Stages.WithEngineer
            + Stages.Query
            + Stages.Held
            + TriageCount
            + UnidentifiedCount;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The notification query is unavailable.")]
    private static partial void LogNotificationsUnavailable(
        ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "The release note query is unavailable.")]
    private static partial void LogReleaseNoteUnavailable(
        ILogger logger, Exception exception);
}
