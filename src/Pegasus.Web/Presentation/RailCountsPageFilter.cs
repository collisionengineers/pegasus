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
/// <c>Inbox</c>, <c>Cases</c>, <c>Operations</c>. <c>Cases</c> is the workspace
/// contract sum, not_ready + review + with_engineer + query + held + triage +
/// unidentified, read from the same queries the Cases page itself runs:
/// <see cref="IDashboardQueries.GetCaseStageCountsAsync"/> (one grouped
/// aggregate), <see cref="IListTriage"/> (the open-Triage total; the rows are
/// not projected beyond page one) and
/// <see cref="IUnidentifiedStore.ListQueueAsync"/>. <c>Operations</c> is the
/// retryable-failure badge (<see cref="IGetOperationsBadge"/>, 13 September) and
/// is present only when it is above zero: a badge is an attention signal, and
/// nothing needing attention renders nothing. Inbox has no established figure
/// to reuse without inventing one, so it is absent — the layout renders nothing
/// for a missing key, never a stale zero.
///
/// The bell is the person's own notifications (Work Centre D10), newest first,
/// read once here; the unread count is derived from the same list rather than
/// read a second time. A failed read sets <c>NotificationsUnavailable</c> so the
/// dialog states it, and the page still renders (FRD-12).
///
/// A global <c>IAsyncPageFilter</c> is the direct ASP.NET Core mechanism for
/// shared page <c>ViewData</c>. It waits for the selected handler's result:
/// redirects, file responses, heartbeat responses and lazy section partials do
/// not render the shell, so they do not issue its database reads. A
/// <see cref="PageResult"/> still receives the data, including an invalid form
/// that returns its page with validation errors.
/// </remarks>
public sealed partial class RailCountsPageFilter(
    IDashboardQueries dashboardQueries,
    IListTriage listTriage,
    IUnidentifiedStore unidentifiedStore,
    IGetOperationsBadge getOperationsBadge,
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
    private readonly IGetOperationsBadge getOperationsBadge =
        getOperationsBadge ?? throw new ArgumentNullException(nameof(getOperationsBadge));
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
        var result = await next();
        var user = context.HttpContext.User;
        if (result.Result is PageResult
            && user.Identity?.IsAuthenticated == true
            && context.HandlerInstance is PageModel pageModel
            && StaffActorFactory.TryCreate(
                user.FindFirstValue(ClaimTypes.NameIdentifier),
                user.FindAll(ClaimTypes.Role).Select(claim => claim.Value),
                out var actor))
        {
            var cancellationToken = context.HttpContext.RequestAborted;
            var caseCounts = TryGetCaseCounts(context.HttpContext, out var loadedCounts)
                ? loadedCounts
                : await LoadCaseCountsAsync(actor, cancellationToken);
            var railCounts = new Dictionary<string, int>
            {
                ["Cases"] = caseCounts.Total
            };
            if (await OperationsBadgeAsync(actor, cancellationToken) is > 0 and var badge)
            {
                railCounts["Operations"] = badge;
            }

            pageModel.ViewData["RailCounts"] = railCounts;
            pageModel.ViewData["ShellRenderedAtUtc"] = timeProvider.GetUtcNow();

            try
            {
                using var timing = DocumentReadTelemetry.Start("web.shell.notifications");
                pageModel.ViewData["Notifications"] = await myNotifications.ListAsync(actor, cancellationToken);
            }
            catch (Exception exception) when (exception is not
                (OperationCanceledException or StaffAuthorizationException or UnauthorizedAccessException))
            {
                LogNotificationsUnavailable(logger, exception);
                pageModel.ViewData["NotificationsUnavailable"] = true;
            }

            // What's new (FRD-12): the newest published release note this person
            // has not acknowledged opens once as a dialog. A failed read shows
            // nothing rather than blocking the page.
            try
            {
                if (await myReleaseNotes.GetUnacknowledgedAsync(actor, cancellationToken) is { } releaseNote)
                {
                    pageModel.ViewData["ReleaseNote"] = releaseNote;
                }
            }
            catch (Exception exception) when (exception is not
                (OperationCanceledException or StaffAuthorizationException or UnauthorizedAccessException))
            {
                LogReleaseNoteUnavailable(logger, exception);
            }
        }
    }

    /// <summary>
    /// Carries the Cases page's already-read totals through this one request to
    /// the post-handler shell filter. It is never a cross-request cache.
    /// </summary>
    public static void SetCaseCounts(
        HttpContext context,
        CaseStageCounts stages,
        int triageCount,
        int unidentifiedCount)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Items[CaseCountsKey] = new CaseRailCounts(stages, triageCount, unidentifiedCount);
    }

    private static bool TryGetCaseCounts(HttpContext context, out CaseRailCounts counts)
    {
        if (context.Items.TryGetValue(CaseCountsKey, out var value)
            && value is CaseRailCounts result)
        {
            counts = result;
            return true;
        }

        counts = default!;
        return false;
    }

    private async Task<CaseRailCounts> LoadCaseCountsAsync(
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        using var timing = DocumentReadTelemetry.Start("web.shell.counts");
        var stagesTask = dashboardQueries.GetCaseStageCountsAsync(cancellationToken);
        var triageTask = listTriage.CountAsync(
            actor,
            state: null,
            cancellationToken: cancellationToken);
        var unidentifiedTask = unidentifiedStore.CountOpenAsync(cancellationToken);
        await Task.WhenAll(stagesTask, triageTask, unidentifiedTask);
        return new(stagesTask.Result, triageTask.Result, unidentifiedTask.Result);
    }

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

    /// <summary>
    /// The Operations badge, or nothing. The badge needs casework rights, so a
    /// User has none; a failed read is logged and renders no figure rather than
    /// a page failure.
    /// </summary>
    private async Task<int> OperationsBadgeAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        if (!StaffAuthorization.IsAuthorized(actor, StaffAccessRight.PerformCasework))
        {
            return 0;
        }

        try
        {
            using var timing = DocumentReadTelemetry.Start("web.shell.operations");
            return await getOperationsBadge.ExecuteAsync(actor, cancellationToken);
        }
        catch (Exception exception) when (exception is not
            (OperationCanceledException or StaffAuthorizationException or UnauthorizedAccessException))
        {
            LogOperationsBadgeUnavailable(logger, exception);
            return 0;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The notification query is unavailable.")]
    private static partial void LogNotificationsUnavailable(
        ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "The release note query is unavailable.")]
    private static partial void LogReleaseNoteUnavailable(
        ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "The Operations badge query is unavailable.")]
    private static partial void LogOperationsBadgeUnavailable(
        ILogger logger, Exception exception);
}
