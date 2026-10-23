using Pegasus.Core.Notifications;

namespace Pegasus.Web.Presentation;

/// <summary>
/// The bell's list as <c>Shared/_NotificationList</c> draws it: the rows, the
/// page the forms return to, and the instant the relative times count from.
/// <paramref name="Unavailable"/> is a failed read, which the dialog states.
/// </summary>
public sealed record NotificationListView(
    IReadOnlyList<StaffNotification> Rows,
    string ReturnUrl,
    DateTimeOffset Now,
    bool Unavailable = false);
