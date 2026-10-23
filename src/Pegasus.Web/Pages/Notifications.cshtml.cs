using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Documents;
using Pegasus.Core.Notifications;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages;

/// <summary>
/// The bell's handlers (Work Centre D10). Opening a notification marks it read
/// and lands on its route; Mark all read returns to the page the operator was
/// on. Both are plain forms in the shell dialog, so they need no script. The
/// dialog's list is the List handler's partial. The same page answers the
/// shell's other dialogs when they first open (the ShellDialog handler), so
/// the shell has one place that serves its dialog fragments.
/// </summary>
[Authorize]
public sealed class NotificationsModel(
    IMyStaffNotifications notifications,
    TimeProvider timeProvider,
    ILogger<NotificationsModel> logger) : StaffPageModel, Pegasus.Web.Presentation.IShellFreeGet
{
    private readonly IMyStaffNotifications _notifications =
        notifications ?? throw new ArgumentNullException(nameof(notifications));

    public IActionResult OnGet() => Redirect("/?notifications=1");

    /// <summary>
    /// The bell's list, fetched when the dialog opens (the page carries only
    /// the unread count). The rows' forms return to the page the dialog is on.
    /// A failed read is logged, timed as <c>web.shell.notifications</c> and
    /// answers the dialog's unavailable state, as the page's own read does.
    /// </summary>
    public async Task<IActionResult> OnGetListAsync(string? returnUrl, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        var safeReturnUrl = SafeReturnUrl(returnUrl);
        try
        {
            using var timing = DocumentReadTelemetry.Start("web.shell.notifications");
            var rows = await _notifications.ListAsync(actor, cancellationToken);
            return Partial("_NotificationList", new NotificationListView(rows, safeReturnUrl, timeProvider.GetUtcNow()));
        }
        catch (Exception exception) when (ShellPageFilter.IsUnavailable(exception, cancellationToken))
        {
            ShellPageFilter.LogNotificationsUnavailable(logger, exception);
            return Partial("_NotificationList", new NotificationListView([], safeReturnUrl, timeProvider.GetUtcNow(), Unavailable: true));
        }
    }

    /// <summary>
    /// A shell dialog's content, fetched the first time it opens so a page does
    /// not carry the markup of the Account, command palette and Report a problem
    /// dialogs. The Report a problem dialog names the page it was opened on in
    /// the request (<paramref name="traceId"/> and <paramref name="method"/>),
    /// which is what its report carries.
    /// </summary>
    public IActionResult OnGetShellDialog(string? name, string? traceId, string? method) =>
        name switch
        {
            "account" => Partial("_ShellAccountDialog"),
            "command" => Partial("_ShellCommandDialog"),
            "problem" => Partial(
                "_ShellProblemDialog",
                new ShellProblemView(
                    string.IsNullOrWhiteSpace(traceId) ? System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier : traceId,
                    string.IsNullOrWhiteSpace(method) ? HttpMethods.Get : method)),
            _ => NotFound()
        };

    public async Task<IActionResult> OnPostOpenAsync(Guid id, string? returnUrl, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        if (id == Guid.Empty)
        {
            return NotFound();
        }

        var opened = await _notifications.OpenAsync(actor, id, cancellationToken);
        if (opened is null)
        {
            // Not this person's, or already past retention: nothing to open,
            // and nothing to say beyond the page they were on.
            return LocalRedirect(SafeReturnUrl(returnUrl));
        }

        return LocalRedirect(Url.IsLocalUrl(opened.Route) ? opened.Route : "/");
    }

    public async Task<IActionResult> OnPostMarkAllReadAsync(string? returnUrl, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        await _notifications.MarkAllReadAsync(actor, cancellationToken);
        return LocalRedirect(SafeReturnUrl(returnUrl));
    }

    private string SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/";
}
