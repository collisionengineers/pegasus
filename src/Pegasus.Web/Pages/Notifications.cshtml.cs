using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Notifications;

namespace Pegasus.Web.Pages;

/// <summary>
/// The bell's handlers (Work Centre D10). Opening a notification marks it read
/// and lands on its route; Mark all read returns to the page the operator was
/// on. Both are plain forms in the shell dialog, so they need no script.
/// </summary>
[Authorize]
public sealed class NotificationsModel(IMyStaffNotifications notifications) : StaffPageModel
{
    private readonly IMyStaffNotifications _notifications =
        notifications ?? throw new ArgumentNullException(nameof(notifications));

    public IActionResult OnGet() => Redirect("/?notifications=1");

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
