using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using Pegasus.Core.Actors;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Notifications;
using Pegasus.Core.ReleaseNotes;

namespace Pegasus.Web.Presentation;

/// <summary>
/// Supplies the shell's own data on each authenticated full page result —
/// <c>ViewData["ShellRenderedAtUtc"]</c>, the bell's
/// <c>ViewData["Notifications"]</c> and the What's new
/// <c>ViewData["ReleaseNote"]</c> — so <c>_Layout.cshtml</c> never carries a
/// shell-invented figure.
/// </summary>
/// <remarks>
/// The bell is the person's own notifications (Work Centre D10), newest first,
/// read once here; the unread count is derived from the same list rather than
/// read a second time. A failed read sets <c>NotificationsUnavailable</c> so the
/// dialog states it, and the page still renders (FRD-12). The notifications and
/// the release note start together.
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
public sealed partial class ShellPageFilter(
    IMyStaffNotifications myNotifications,
    IMyReleaseNotes myReleaseNotes,
    TimeProvider timeProvider,
    ILogger<ShellPageFilter> logger) : IAsyncPageFilter
{
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
            var notificationsTask = shell.Notifications;
            var releaseNoteTask = shell.ReleaseNote;

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
    // Razor Pages names the handler's method from the method name ("Get" for
    // OnGetAsync), so the method is compared without regard to case.
    private static bool IsCaseFullPageGet(PageHandlerExecutingContext context) =>
        context.HandlerInstance is Pegasus.Web.Pages.Cases.DetailsModel
        && context.HandlerMethod is { Name: null or "" } handler
        && HttpMethods.IsGet(handler.HttpMethod);

    /// <summary>
    /// The bell and the release note share nothing and each reads on its own
    /// context, so they start together. Each answers for itself.
    /// </summary>
    private ShellReads StartShellReads(HttpContext httpContext, ActionActor actor)
    {
        var cancellationToken = httpContext.RequestAborted;
        return new(
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
        Task<IReadOnlyList<StaffNotification>?> Notifications,
        Task<ReleaseNote?> ReleaseNote)
    {
        public Task WhenAllAsync() => Task.WhenAll(Notifications, ReleaseNote);
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

    [LoggerMessage(Level = LogLevel.Error, Message = "The notification query is unavailable.")]
    private static partial void LogNotificationsUnavailable(
        ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "The release note query is unavailable.")]
    private static partial void LogReleaseNoteUnavailable(
        ILogger logger, Exception exception);
}
