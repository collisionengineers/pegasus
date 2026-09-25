using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;
using Pegasus.Infrastructure.Email;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Mail;

/// <summary>
/// The mail workspace list: retained messages, newest first, across every
/// mailbox by default.
/// </summary>
/// <remarks>
/// Reading never mutates anything: the read state it shows is the retained one,
/// and opening a message here does not mark it read in the mailbox. The one
/// write on the list is Dismiss (and Restore under the Dismissed scope), which
/// moves the retained message to the Dismissed logical folder without
/// classifying or linking it and never reaches Outlook (Inbox, 13 September).
/// </remarks>
public sealed class IndexModel(
    ListRetainedMail listRetainedMail,
    GetRetainedMail getRetainedMail,
    GetRetainedMailFreshness getFreshness,
    SearchDeletedMail searchDeletedMail,
    IDismissRetainedMail dismissRetainedMail,
    IRestoreRetainedMail restoreRetainedMail,
    IStaffMailSend? staffMailSend = null) : StaffPageModel
{
    public bool StaffMailAvailable => staffMailSend is not null
        && staffMailSend is not UnavailableStaffMailSend;
    internal const int PageSize = 25;

    /// <summary>The folder route value of the Dismissed scope.</summary>
    public const string DismissedFolderCode = "dismissed";

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

    /// <summary>The Category filter (the mail category vocabulary); the route key stays <c>queue</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "queue")]
    public string? QueueFilter { get; set; }

    /// <summary>The list's sort toggle: <c>oldest</c>, or absent for newest.</summary>
    [BindProperty(SupportsGet = true, Name = "sort")]
    public string? SortOrder { get; set; }

    /// <summary>The message whose preview the right pane renders.</summary>
    [BindProperty(SupportsGet = true, Name = "selected")]
    public string? SelectedMessage { get; set; }

    public MailOperationalDestination? DestinationFilter { get; private set; }

    public MailCategory? DetailedClassificationFilter { get; private set; }

    public MailFolderScope Folder { get; private set; } = MailFolderScope.Inbox;

    /// <summary>The Dismissed scope: dismissed messages appear there and nowhere else.</summary>
    public bool Dismissed { get; private set; }

    public bool OldestFirst { get; private set; }

    public RetainedMailPage Results { get; private set; } =
        new([], 1, PageSize, 0, false);

    public IReadOnlyList<RetainedMailMailbox> Mailboxes { get; private set; } = [];

    /// <summary>
    /// The scope rail: one row per operator scope, with the count that clicking
    /// it would page through. An invalid search term withholds the counts rather
    /// than paging an error through seven more queries.
    /// </summary>
    public IReadOnlyList<MailScopeOption> Scopes { get; private set; } = [];

    /// <summary>The message the preview pane renders, when one is selected.</summary>
    public RetainedMailDetail? SelectedDetail { get; private set; }

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

        if (!TryParseListContext())
        {
            return NotFound();
        }

        var mailbox = Guid.TryParse(MailboxFilter, out var mailboxId) && mailboxId != Guid.Empty
            ? mailboxId
            : (Guid?)null;
        MailboxFilter = mailbox?.ToString("D");
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
            Mailboxes = Folder == MailFolderScope.DeletedItems
                ? await searchDeletedMail.ListMailboxesAsync(actor, cancellationToken)
                : await listRetainedMail.ListMailboxesAsync(actor, cancellationToken);
            if (SearchValidationMessage is null
                && Folder == MailFolderScope.DeletedItems
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
                    new(
                        mailbox,
                        Folder,
                        SearchTerm,
                        DestinationFilter,
                        DetailedClassificationFilter,
                        UnreadOnly: false,
                        OldestFirst,
                        DismissedOnly: Dismissed),
                    page,
                    PageSize,
                    cancellationToken);
                await LoadSelectedDetailAsync(actor, cancellationToken);
            }
            if (SearchValidationMessage is null)
            {
                Scopes = await LoadScopeCountsAsync(actor, mailbox, cancellationToken);
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

    /// <summary>Dismiss from the row: the message leaves every incoming scope; nothing is classified, linked or deleted.</summary>
    public async Task<IActionResult> OnPostDismissAsync(
        Guid id,
        string? operationKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (!TryParseListContext())
        {
            return NotFound();
        }

        try
        {
            var result = await dismissRetainedMail.ExecuteAsync(
                new(id, actor, string.IsNullOrWhiteSpace(operationKey) ? NewOperationKey() : operationKey),
                cancellationToken);
            if (result is null)
            {
                return NotFound();
            }
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (ArgumentException)
        {
            return NotFound();
        }

        TempData["Confirmation"] = OperatorLabels.Inbox.DismissedNotice;
        return RedirectToList();
    }

    /// <summary>Restore from the Dismissed scope: the message returns to the incoming scopes it belongs to.</summary>
    public async Task<IActionResult> OnPostRestoreAsync(
        Guid id,
        string? operationKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (!TryParseListContext())
        {
            return NotFound();
        }

        try
        {
            var result = await restoreRetainedMail.ExecuteAsync(
                new(id, actor, string.IsNullOrWhiteSpace(operationKey) ? NewOperationKey() : operationKey),
                cancellationToken);
            if (result is null)
            {
                return NotFound();
            }
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (ArgumentException)
        {
            return NotFound();
        }

        TempData["Confirmation"] = OperatorLabels.Inbox.RestoredNotice;
        return RedirectToList();
    }

    private RedirectToPageResult RedirectToList() => RedirectToPage(new
    {
        mailbox = MailboxFilter,
        folder = FolderRouteValue,
        search = SearchTerm,
        queue = QueueFilter,
        sort = OldestFirst ? "oldest" : null,
        pageNumber = PageNumber is > 1 ? PageNumber : null
    });

    private bool TryParseListContext()
    {
        if (!TryParseFolder(FolderFilter, out var folder, out var dismissed)
            || !TryParseSort(SortOrder, out var oldestFirst)
            || !TryParseQueue(
                QueueFilter,
                out var normalizedQueue,
                out var destination,
                out var detailedClassification)
            || (folder == MailFolderScope.DeletedItems && normalizedQueue is not null))
        {
            return false;
        }

        Folder = folder;
        Dismissed = dismissed;
        OldestFirst = oldestFirst;
        QueueFilter = normalizedQueue;
        DestinationFilter = destination;
        DetailedClassificationFilter = detailedClassification;
        return true;
    }

    public async Task<IActionResult> OnGetPreviewAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            var detail = await getRetainedMail.ExecuteAsync(actor, id, cancellationToken);
            if (detail is null)
            {
                return NotFound();
            }

            var summary = detail.Summary;
            return new JsonResult(new
            {
                id = summary.Id,
                sender = SenderLine(summary),
                subject = SubjectLine(summary),
                receivedAtUtc = summary.ReceivedAtUtc,
                received = $"{OperatorLabels.OfficeDate(summary.ReceivedAtUtc)} {OperatorLabels.OfficeClock(summary.ReceivedAtUtc)}",
                excerpt = summary.BodyExcerpt ?? "No excerpt available",
                classification = detail.Classification is { } dossier
                    ? MessageModel.DecisionLabel(dossier.Current)
                    : MessageModel.ClassificationLabel(detail.ClassificationOutcome),
                association = MessageModel.AssociationLabel(summary.CaseReference),
                attachments = detail.Attachments.Select(attachment => attachment.FileName).ToArray()
            });
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (ArgumentException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// The preview renders the selected row of the current page; with no usable
    /// selection it renders the first row, the way the drawn list does — a
    /// selected message that is not on the page falls back to the page's first
    /// position, exactly as the drawn prototype resolves a stale selection.
    /// </summary>
    private async Task LoadSelectedDetailAsync(
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        var selectedId = Guid.TryParse(SelectedMessage, out var parsed) ? parsed : (Guid?)null;
        var row = Results.Items.FirstOrDefault(item => item.Id == selectedId)
            ?? (Results.Items.Count > 0 ? Results.Items[0] : null);
        if (row is null)
        {
            return;
        }

        SelectedDetail = await getRetainedMail.ExecuteAsync(
            actor,
            row.Id,
            SearchTerm,
            cancellationToken);
    }

    private async Task<IReadOnlyList<MailScopeOption>> LoadScopeCountsAsync(
        ActionActor actor,
        Guid? mailbox,
        CancellationToken cancellationToken)
    {
        var scopes = ScopeDefinitions.Select(definition => new MailWorkspaceScope(
            mailbox,
            definition.Folder,
            SearchTerm,
            definition.Destination,
            null,
            UnreadOnly: false,
            DismissedOnly: definition.Dismissed)).ToArray();
        var counts = await listRetainedMail.CountManyAsync(actor, scopes, cancellationToken);
        var options = new List<MailScopeOption>(ScopeDefinitions.Count);
        for (var index = 0; index < ScopeDefinitions.Count; index++)
        {
            var definition = ScopeDefinitions[index];
            options.Add(new(
                definition,
                definition.Matches(Folder, QueueFilter, Dismissed),
                counts[index],
                mailbox,
                SearchTerm));
        }

        return options;
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

    public static string SubjectLine(RetainedMailSummary item) => SubjectLine(item.Subject);

    public static string SubjectLine(string? subject) =>
        string.IsNullOrWhiteSpace(subject) ? "No subject" : subject;

    public string? FolderRouteValue => ListFolderCode(Folder, Dismissed);

    /// <summary>The folder route value for a list context: absent for Inbox, <c>dismissed</c> for the Dismissed scope.</summary>
    public static string? ListFolderCode(MailFolderScope folder, bool dismissed) =>
        dismissed ? DismissedFolderCode : folder == MailFolderScope.Inbox ? null : FolderCode(folder);

    public static string FolderCode(MailFolderScope folder) => folder switch
    {
        MailFolderScope.Inbox => "inbox",
        MailFolderScope.Sent => "sent",
        MailFolderScope.DeletedItems => "deleted",
        MailFolderScope.Upload => "upload",
        _ => throw new InvalidOperationException($"Unknown mail folder scope '{(int)folder}'.")
    };

    public static string FolderLabel(MailFolderScope folder) => folder switch
    {
        MailFolderScope.Inbox => "Inbox",
        MailFolderScope.Sent => "Sent",
        MailFolderScope.DeletedItems => "Deleted items",
        MailFolderScope.Upload => "Uploaded",
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
        ["queue"] = QueueFilter,
        ["sort"] = OldestFirst ? "oldest" : null,
        ["selected"] = SelectedDetail is { } detail ? detail.Summary.Id.ToString("D") : null,
        ["pageNumber"] = Results.Page > 1
            ? Results.Page.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : DeletedResults?.Page > 1
                ? DeletedResults.Page.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : null
    };

    /// <summary>
    /// One scope-rail row: the drawn label and icon well, the pressed state, and
    /// the count that clicking the scope would page through. The submitted form
    /// carries the mailbox and search term so a scope click stays inside the
    /// operator's current view.
    /// </summary>
    public sealed record MailScopeOption(
        MailScopeDefinition Definition,
        bool Pressed,
        int Count,
        Guid? MailboxId,
        string? SearchTerm)
    {
        /// <summary>The hidden inputs the scope's GET form submits.</summary>
        public IReadOnlyDictionary<string, string> HiddenFields
        {
            get
            {
                var fields = new Dictionary<string, string>();
                if (ListFolderCode(Definition.Folder, Definition.Dismissed) is { } folder)
                {
                    fields["folder"] = folder;
                }
                if (MailboxId is { } mailboxId)
                {
                    fields["mailbox"] = mailboxId.ToString("D");
                }
                if (SearchTerm is { } searchTerm)
                {
                    fields["search"] = searchTerm;
                }
                if (Definition.Destination is { } destination)
                {
                    fields["queue"] = DestinationKey(destination);
                }
                return fields;
            }
        }
    }

    public sealed record MailScopeDefinition(
        string Label,
        string IconId,
        MailFolderScope Folder,
        MailOperationalDestination? Destination = null,
        bool Dismissed = false)
    {
        public bool Matches(
            MailFolderScope folder,
            string? queueFilter,
            bool dismissed) =>
            folder == Folder
            && dismissed == Dismissed
            && string.Equals(
                queueFilter,
                Destination is { } destination ? DestinationKey(destination) : null,
                StringComparison.Ordinal);
    }

    /// <summary>
    /// The scope rail, in the planned order (Inbox, 13 September): no Unread scope —
    /// read state is not a queue; unread rows are bold — and a Dismissed scope last.
    /// The aggregate scopes reuse the keys the Category select already binds.
    /// </summary>
    public static readonly IReadOnlyList<MailScopeDefinition> ScopeDefinitions =
    [
        new("All incoming", "inbox", MailFolderScope.Inbox),
        new(
            "Receiving work",
            "download",
            MailFolderScope.Inbox,
            MailOperationalDestination.ReceivingWork),
        new("Case updates", "reply", MailFolderScope.Inbox, MailOperationalDestination.Queries),
        new("Pre-instructions", "clock", MailFolderScope.Inbox, MailOperationalDestination.Triage),
        new(
            "Unidentified",
            "search",
            MailFolderScope.Inbox,
            MailOperationalDestination.Unidentified),
        new("Sent Items", "send", MailFolderScope.Sent),
        new(OperatorLabels.Inbox.DismissedScope, "x", MailFolderScope.Inbox, Dismissed: true)
    ];

    public sealed record MailViewOption(
        string Value,
        string Label,
        MailOperationalDestination? Destination = null);

    public static IReadOnlyList<MailViewOption> AggregateViews { get; } =
        Enum.GetValues<MailOperationalDestination>()
            .Where(destination => destination != MailOperationalDestination.DetailedClassification)
            .Select(destination => new MailViewOption(
                DestinationKey(destination),
                OperatorLabels.MailOperationalDestinationLabel(destination),
                destination))
            .ToArray();

    public static IReadOnlyList<MailViewOption> DetailedViews { get; } =
        MailClassificationSelection.Options
            .Select(option => new
            {
                Option = option,
                Parsed = MailClassificationSelection.TryParse(
                    option.Value,
                    otherName: null,
                    otherReasoning: null,
                    out var category)
                    ? category
                    : null
            })
            .Where(item => item.Parsed is not null
                && MailOperationalDestinationPolicy.Map(item.Parsed).Destination
                    == MailOperationalDestination.DetailedClassification)
            .Select(item => new MailViewOption(
                $"classification:{item.Option.Value}",
                item.Option.Label))
            .ToArray();

    internal static bool TryParseQueue(
        string? value,
        out string? normalized,
        out MailOperationalDestination? destination,
        out MailCategory? detailedClassification)
    {
        normalized = null;
        destination = null;
        detailedClassification = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var candidate = value.Trim();
        foreach (var option in AggregateViews)
        {
            if (!string.Equals(option.Value, candidate, StringComparison.Ordinal))
            {
                continue;
            }
            normalized = option.Value;
            destination = option.Destination;
            return true;
        }

        const string prefix = "classification:";
        if (!candidate.StartsWith(prefix, StringComparison.Ordinal)
            || !MailClassificationSelection.TryParse(
                candidate[prefix.Length..],
                otherName: null,
                otherReasoning: null,
                out var category)
            || category is null
            || MailOperationalDestinationPolicy.Map(category).Destination
                != MailOperationalDestination.DetailedClassification)
        {
            return false;
        }

        normalized = $"{prefix}{candidate[prefix.Length..]}";
        detailedClassification = category;
        return true;
    }

    private static string DestinationKey(MailOperationalDestination destination) => destination switch
    {
        MailOperationalDestination.ReceivingWork => "receiving-work",
        MailOperationalDestination.Queries => "queries",
        MailOperationalDestination.Other => "other",
        MailOperationalDestination.Unidentified => "unidentified",
        MailOperationalDestination.Triage => "triage",
        MailOperationalDestination.DetailedClassification => throw new ArgumentException(
            "Detailed views use a canonical classification key.",
            nameof(destination)),
        _ => throw new ArgumentOutOfRangeException(nameof(destination), destination, null)
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

    internal static bool TryParseFolder(string? value, out MailFolderScope folder) =>
        TryParseFolder(value, out folder, out _);

    /// <summary>
    /// The list folder address: Inbox (absent or <c>inbox</c>), <c>sent</c>,
    /// <c>deleted</c>, or <c>dismissed</c> — the Dismissed scope, which reads the
    /// retained Inbox mail that has been dismissed.
    /// </summary>
    internal static bool TryParseFolder(string? value, out MailFolderScope folder, out bool dismissed)
    {
        folder = MailFolderScope.Inbox;
        dismissed = false;
        switch (value)
        {
            case null or "":
            case "inbox":
                return true;
            case DismissedFolderCode:
                dismissed = true;
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

    /// <summary>
    /// The sort toggle is a link with two states, so only the two drawn values
    /// are addresses: absent (newest) and <c>oldest</c>.
    /// </summary>
    internal static bool TryParseSort(string? value, out bool oldestFirst)
    {
        oldestFirst = false;
        switch (value)
        {
            case null or "":
                return true;
            case "oldest":
                oldestFirst = true;
                return true;
            default:
                return false;
        }
    }
}
