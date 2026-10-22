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
/// <c>ViewData["ShellRenderedAtUtc"]</c>, the bell's unread figure
/// <c>ViewData["NotificationsUnread"]</c> and the What's new
/// <c>ViewData["ReleaseNote"]</c> — so <c>_Layout.cshtml</c> never carries a
/// shell-invented figure.
/// </summary>
/// <remarks>
/// The bell is the person's own notifications (Work Centre D10). The page
/// carries the exact unread count, counted without reading a row; the list
/// loads when the bell opens (<c>/Notifications?handler=List</c>). Only a page
/// requested with <c>?notifications=1</c>, which opens the dialog on load,
/// reads the list here (<c>ViewData["Notifications"]</c>) and counts its unread
/// rows. A failed read sets <c>NotificationsUnavailable</c> so the dialog states
/// it, and the page still renders (FRD-12). The bell and the release note start
/// together.
///
/// A global <c>IAsyncPageFilter</c> is the direct ASP.NET Core mechanism for
/// shared page <c>ViewData</c>. It gives a <see cref="PageResult"/> the data,
/// including an invalid form that returns its page with validation errors.
/// Every unnamed GET handler starts the reads before the handler and awaits
/// them when the page renders (operator rulings, 10 and 22 Oct 2026), except a
/// page marked <see cref="IShellFreeGet"/>, whose GET is a redirect, a file or a
/// page with no layout. Other results drain the reads unused. Named handlers
/// (heartbeats, polls, lazy section partials) and every POST never pre-read
/// and read after their result only if it is a page, so a POST's figures are
/// never read ahead of what it changes.
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
        // A full-page GET renders the shell on every result but a refusal, and
        // reads no figure the shell needs first (no GET writes the bell or the
        // release note), so its shell reads start before the handler and run
        // beside it.
        var early = authenticated && IsFullPageGet(context)
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
            var bellTask = shell.Bell;
            var releaseNoteTask = shell.ReleaseNote;

            pageModel.ViewData["ShellRenderedAtUtc"] = timeProvider.GetUtcNow();
            if (bellTask.Result is { } bell)
            {
                pageModel.ViewData["NotificationsUnread"] = bell.Unread;
                if (bell.Rows is { } rows)
                {
                    pageModel.ViewData["Notifications"] = rows;
                }
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
    /// A page's unnamed GET handler, the full page, or a GET to a page with no
    /// handler at all, which renders. Its sections, refreshes, polls and every
    /// POST are other handlers and keep reading the shell after their result,
    /// as does a page whose GET never renders the shell
    /// (<see cref="IShellFreeGet"/>).
    /// </summary>
    private static bool IsFullPageGet(PageHandlerExecutingContext context) =>
        context.HandlerInstance is not IShellFreeGet
        && (context.HandlerMethod is null
            ? HttpMethods.IsGet(context.HttpContext.Request.Method)
            : context.HandlerMethod is { Name: null or "" } handler && HttpMethods.IsGet(handler.HttpMethod));

    /// <summary>
    /// The bell and the release note share nothing and each reads on its own
    /// context, so they start together. Each answers for itself.
    /// </summary>
    private ShellReads StartShellReads(HttpContext httpContext, ActionActor actor)
    {
        var cancellationToken = httpContext.RequestAborted;
        // ?notifications=1 opens the bell on load, so that one page reads the list.
        var listRows = string.Equals(httpContext.Request.Query["notifications"], "1", StringComparison.Ordinal);
        return new(
            ReadBellAsync(actor, listRows, cancellationToken),
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
        Task<BellRead?> Bell,
        Task<ReleaseNote?> ReleaseNote)
    {
        public Task WhenAllAsync() => Task.WhenAll(Bell, ReleaseNote);
    }

    /// <summary>The bell's unread figure and, only when the dialog opens on load, its rows.</summary>
    private sealed record BellRead(int Unread, IReadOnlyList<StaffNotification>? Rows);

    /// <summary>The bell, or null when the read failed and the dialog must say so.</summary>
    private async Task<BellRead?> ReadBellAsync(
        ActionActor actor,
        bool listRows,
        CancellationToken cancellationToken)
    {
        try
        {
            using var timing = DocumentReadTelemetry.Start("web.shell.notifications");
            if (!listRows)
            {
                return new(await myNotifications.UnreadCountAsync(actor, cancellationToken), null);
            }

            var rows = await myNotifications.ListAsync(actor, cancellationToken);
            return new(rows.Count(notification => notification.IsUnread), rows);
        }
        catch (Exception exception) when (IsUnavailable(exception, cancellationToken))
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
        catch (Exception exception) when (IsUnavailable(exception, cancellationToken))
        {
            LogReleaseNoteUnavailable(logger, exception);
            return null;
        }
    }

    /// <summary>
    /// Whether a failed shell read is logged as unavailable. A cancellation and
    /// a failed authorisation propagate, and so does the cancelled command
    /// SqlClient reports in its own wording once the request has been aborted
    /// (<see cref="ClientAbort"/>): that is the caller leaving, not a fault.
    /// </summary>
    private static bool IsUnavailable(Exception exception, CancellationToken cancellationToken) =>
        exception is not (OperationCanceledException or StaffAuthorizationException or UnauthorizedAccessException)
        && !(cancellationToken.IsCancellationRequested && ClientAbort.IsCancellation(exception));

    [LoggerMessage(Level = LogLevel.Error, Message = "The notification query is unavailable.")]
    private static partial void LogNotificationsUnavailable(
        ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "The release note query is unavailable.")]
    private static partial void LogReleaseNoteUnavailable(
        ILogger logger, Exception exception);
}
