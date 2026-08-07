using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Core.Actors;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Web.Pages.Mail;

/// <summary>
/// The mail workspace list: retained messages, newest first, across every
/// mailbox by default.
/// </summary>
/// <remarks>
/// A viewer and nothing else. It renders no form that mutates anything, and the
/// read state it shows is the retained one — opening a message here does not
/// mark it read in the mailbox.
/// </remarks>
public sealed class IndexModel(
    ListRetainedMail listRetainedMail,
    GetRetainedMailFreshness getFreshness) : PageModel
{
    internal const int PageSize = 25;

    /// <summary>
    /// The active scope lives in the query string and nowhere else. Requirements
    /// say a fresh visit resets to the default all-mailboxes view, so a TempData
    /// or cookie memory of the last filter would be a defect, not a convenience.
    /// </summary>
    [BindProperty(SupportsGet = true, Name = "mailbox")]
    public string? MailboxFilter { get; set; }

    [BindProperty(SupportsGet = true, Name = "folder")]
    public string? FolderFilter { get; set; }

    /// <summary>
    /// The page, bound as <c>pageNumber</c> because <c>page</c> is the reserved
    /// Razor Pages route key: an <c>asp-route-page</c> is overwritten by
    /// <c>asp-page</c>, so a pager built on it silently emits links with no page
    /// on them at all.
    /// </summary>
    [BindProperty(SupportsGet = true, Name = "pageNumber")]
    public int? PageNumber { get; set; }

    public MailFolderScope Folder { get; private set; } = MailFolderScope.Inbox;

    public RetainedMailPage Results { get; private set; } =
        new([], 1, PageSize, 0, false);

    public IReadOnlyList<RetainedMailMailbox> Mailboxes { get; private set; } = [];

    public MailFreshness Freshness { get; private set; } =
        new(MailFreshnessState.Unavailable, null);

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!StaffActorFactory.TryCreate(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                User.FindAll(ClaimTypes.Role).Select(claim => claim.Value),
                out var actor))
        {
            return Forbid();
        }

        if (!TryParseFolder(FolderFilter, out var folder))
        {
            return NotFound();
        }

        Folder = folder;
        var mailbox = string.IsNullOrWhiteSpace(MailboxFilter) ? null : MailboxFilter.Trim();
        MailboxFilter = mailbox;
        var page = Math.Clamp(PageNumber ?? 1, 1, 10_000);
        PageNumber = page;

        try
        {
            Mailboxes = await listRetainedMail.ListMailboxesAsync(actor, cancellationToken);
            Results = await listRetainedMail.ExecuteAsync(
                actor,
                new(mailbox, folder),
                page,
                PageSize,
                cancellationToken);
            Freshness = await getFreshness.ExecuteAsync(actor, cancellationToken);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (ArgumentException)
        {
            return NotFound();
        }

        return Page();
    }

    /// <summary>
    /// What the operator sees where a message has no display name: the address,
    /// or a plain statement rather than an invented name.
    /// </summary>
    public static string SenderLine(RetainedMailSummary item) =>
        item.SenderDisplayName
        ?? item.SenderAddress
        ?? "Sender not recorded";

    public static string SubjectLine(RetainedMailSummary item) =>
        string.IsNullOrWhiteSpace(item.Subject) ? "No subject" : item.Subject;

    public string? FolderRouteValue => Folder == MailFolderScope.Inbox ? null : FolderCode(Folder);

    public static string FolderCode(MailFolderScope folder) => folder switch
    {
        MailFolderScope.Inbox => "inbox",
        MailFolderScope.Sent => "sent",
        MailFolderScope.DeletedItems => "deleted",
        _ => throw new InvalidOperationException($"Unknown mail folder scope '{(int)folder}'.")
    };

    public static string FolderLabel(MailFolderScope folder) => folder switch
    {
        MailFolderScope.Inbox => "Inbox",
        MailFolderScope.Sent => "Sent",
        MailFolderScope.DeletedItems => "Deleted items",
        _ => throw new InvalidOperationException($"Unknown mail folder scope '{(int)folder}'.")
    };

    public static string FreshnessStatus(MailFreshnessState state) => state switch
    {
        MailFreshnessState.Current => "current",
        MailFreshnessState.Stale => "stale",
        MailFreshnessState.Unavailable => "unavailable",
        _ => "unavailable"
    };

    /// <summary>
    /// The scope the refresh button has to carry back, so refreshing reloads what
    /// the operator is looking at.
    /// </summary>
    public IReadOnlyDictionary<string, string?> RefreshFields => new Dictionary<string, string?>
    {
        ["mailbox"] = MailboxFilter,
        ["folder"] = FolderRouteValue,
        ["pageNumber"] = Results.Page > 1
            ? Results.Page.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : null
    };

    internal static bool TryParseFolder(string? value, out MailFolderScope folder)
    {
        folder = MailFolderScope.Inbox;
        switch (value)
        {
            case null or "":
            case "inbox":
                return true;
            case "sent":
                folder = MailFolderScope.Sent;
                return true;
            case "deleted":
                folder = MailFolderScope.DeletedItems;
                return true;
            default:
                return false;
        }
    }
}
