using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Web.Presentation;

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
    GetRetainedMailFreshness getFreshness,
    SearchDeletedMail searchDeletedMail) : StaffPageModel
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

    [BindProperty(SupportsGet = true, Name = "search")]
    public string? SearchTerm { get; set; }

    public MailFolderScope Folder { get; private set; } = MailFolderScope.Inbox;

    public RetainedMailPage Results { get; private set; } =
        new([], 1, PageSize, 0, false);

    public IReadOnlyList<RetainedMailMailbox> Mailboxes { get; private set; } = [];

    public MailFreshness Freshness { get; private set; } =
        new(MailFreshnessState.Unavailable, null);

    public DeletedMailSearchPage? DeletedResults { get; private set; }

    public string? SearchValidationMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
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
        if (SearchTerm is not null)
        {
            SearchTerm = SearchTerm.Trim();
            SearchValidationMessage = SearchTerm.Length switch
            {
                0 => "Enter a search term.",
                > 200 => "Search terms must be 200 characters or fewer.",
                _ => null
            };
            if (SearchTerm.Length == 0)
            {
                SearchTerm = null;
            }
        }

        try
        {
            Mailboxes = folder == MailFolderScope.DeletedItems
                ? await searchDeletedMail.ListMailboxesAsync(actor, cancellationToken)
                : await listRetainedMail.ListMailboxesAsync(actor, cancellationToken);
            if (SearchValidationMessage is null
                && folder == MailFolderScope.DeletedItems
                && SearchTerm is not null)
            {
                DeletedResults = await searchDeletedMail.ExecuteAsync(
                    actor,
                    mailbox,
                    SearchTerm,
                    page,
                    PageSize,
                    cancellationToken);
            }
            else if (SearchValidationMessage is null)
            {
                Results = await listRetainedMail.ExecuteAsync(
                    actor,
                    new(mailbox, folder, SearchTerm),
                    page,
                    PageSize,
                    cancellationToken);
            }
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
        item.EffectiveSenderAddress
        ?? item.SenderDisplayName
        ?? item.SenderAddress
        ?? "Sender not recorded";

    /// <summary>
    /// The Graph envelope remains the provenance for an inline forward even
    /// where intake has proved an original sender from its forwarded header.
    /// </summary>
    public static string? ForwarderLine(RetainedMailSummary item)
    {
        if (string.IsNullOrWhiteSpace(item.EffectiveSenderAddress)
            || string.IsNullOrWhiteSpace(item.SenderAddress)
            || string.Equals(
                item.EffectiveSenderAddress,
                item.SenderAddress,
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return item.SenderDisplayName is { Length: > 0 } displayName
            ? $"{displayName} <{item.SenderAddress}>"
            : item.SenderAddress;
    }

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
        ["search"] = SearchTerm,
        ["pageNumber"] = Results.Page > 1
            ? Results.Page.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : DeletedResults?.Page > 1
                ? DeletedResults.Page.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : null
    };

    public static string MatchLabel(RetainedMailSearchMatch match) => match.Kind switch
    {
        MailSearchMatchKind.MessageBody => "Message body",
        MailSearchMatchKind.AttachmentFileName =>
            $"Attachment name: {AttachmentLabel(match)}",
        MailSearchMatchKind.AttachmentContent =>
            $"Attachment content: {AttachmentLabel(match)}",
        _ => "Message"
    };

    private static string AttachmentLabel(RetainedMailSearchMatch match) =>
        match.AttachmentOrdinal is { } ordinal
            ? $"{match.AttachmentFileName} (attachment {ordinal + 1})"
            : match.AttachmentFileName ?? "Attachment";

    public static string MailFailureSentence(string? failureCode, long? sourceLength = null)
    {
        var size = sourceLength is { } bytes ? $" It is {OperatorLabels.FileSize(bytes)}." : string.Empty;
        return failureCode switch
        {
            null or "" => "This message could not be processed.",
            "source_unavailable" => "The message could not be read from the mailbox.",
            "sent_mailbox_not_approved" => "The mailbox it was sent from is not an approved mailbox.",
            "sent_source_throttled" => "The mailbox refused further reads for a while.",
            "sent_evidence_poll_failure" => "The sent folder could not be read.",
            "message_too_large" => $"This message is larger than the {OperatorLabels.FileSize(IntakeEnvelopeLimits.MaximumMailboxContentLength)} limit, so it was kept but not read.{size}",
            "empty_message" => "This message arrived with no content, so there was nothing to read.",
            "missing_message_identity" or "message_identity_too_long" => "This message did not carry a usable identity, so it could not be tracked.",
            "missing_message_file_name" or "invalid_message_file_name" or "message_file_name_too_long" => "This message did not carry a usable file name, so it could not be retained.",
            "immutable_source_changed" => "This message changed in the mailbox after it was first seen, so it was kept unread for review.",
            "immutable_source_missing" => "This message was no longer in the mailbox when it came to be read.",
            "source_identity_conflict" => "A different message is already recorded under this message's identity.",
            "artifact_retention_failure" => "This message could not be kept safely, so it was not processed.",
            "invalid_mailbox_source" => "The mailbox returned something that could not be read.",
            "mailbox_poll_failure" => "The last message from this mailbox could not be processed.",
            _ => "The last message from this mailbox could not be processed."
        };
    }

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
