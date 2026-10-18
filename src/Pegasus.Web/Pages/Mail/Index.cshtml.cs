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
/// and opening a message here does not mark it read in the mailbox. The writes
/// on the list are Dismiss (and Restore under the Dismissed scope), from a row
/// or for ticked rows, which moves the retained message to the Dismissed
/// logical folder without classifying or linking it (Inbox, 13 September), and
/// a classification correction for ticked rows (operator, 15 October 2026).
/// Each applies the single-message command to one message at a time; none
/// changes read state or reaches Outlook.
/// </remarks>
public sealed class IndexModel(
    ListRetainedMail listRetainedMail,
    GetRetainedMailPreview getRetainedMailPreview,
    GetRetainedMailFreshness getFreshness,
    SearchDeletedMail searchDeletedMail,
    IDismissRetainedMail dismissRetainedMail,
    IRestoreRetainedMail restoreRetainedMail,
    CorrectRetainedMailClassification correctClassification,
    IApprovedMailboxStore approvedMailboxes,
    IStaffMailSend? staffMailSend = null) : StaffPageModel
{
    public bool StaffMailAvailable => staffMailSend is not null
        && staffMailSend is not UnavailableStaffMailSend;

    /// <summary>The approved mailboxes a reply can leave from; a received message held by one offers Reply.</summary>
    public IReadOnlySet<Guid> ReplyMailboxIds { get; private set; } = new HashSet<Guid>();
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

    // The ticked rows' correction: one category, case type and reason for
    // every ticked message, under the message record's own field names.
    [BindProperty]
    public string? ClassificationKey { get; set; }

    [BindProperty]
    public string? WorkType { get; set; }

    [BindProperty]
    public string? CorrectionReason { get; set; }

    /// <summary>The ticked messages the last bulk post applied to, carried to the list it redirects to.</summary>
    [TempData]
    public string[]? BulkSucceeded { get; set; }

    /// <summary>The ticked messages the last bulk post could not apply to; the others still applied.</summary>
    [TempData]
    public string[]? BulkFailed { get; set; }

    /// <summary>The one mail category the list narrows to, from <c>queue</c>.</summary>
    public MailCategory? CategoryFilter { get; private set; }

    /// <summary>The No Case Found scope (<c>queue=no_case_found</c>): received mail with no linked Case.</summary>
    public bool NoCaseFoundFilter { get; private set; }

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
    public RetainedMailPreview? SelectedDetail { get; private set; }

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

        return await LoadAsync(actor, cancellationToken);
    }

    /// <summary>The list as its query string scopes it: the GET, and a refused ticked-row correction.</summary>
    private async Task<IActionResult> LoadAsync(ActionActor actor, CancellationToken cancellationToken)
    {
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
            // The mailboxes, the list, the scope counts and the freshness share
            // nothing and each reads on its own database context, so they start
            // together. Each starts inside its own async method, so a reader that
            // throws before its first await still fails through the same catches.
            var mailboxesRead = ReadAsync(() => Folder == MailFolderScope.DeletedItems
                ? searchDeletedMail.ListMailboxesAsync(actor, cancellationToken)
                : listRetainedMail.ListMailboxesAsync(actor, cancellationToken));
            var listRead = ReadListAsync(actor, mailbox, page, cancellationToken);
            var scopesRead = ReadAsync(() => SearchValidationMessage is null
                ? LoadScopeCountsAsync(actor, mailbox, cancellationToken)
                : Task.FromResult<IReadOnlyList<MailScopeOption>>([]));
            var freshnessRead = ReadAsync(() => getFreshness.ExecuteAsync(actor, cancellationToken));
            var replyRead = ReadAsync(() => StaffMailAvailable
                ? approvedMailboxes.ListAsync(cancellationToken)
                : Task.FromResult<IReadOnlyList<ApprovedMailbox>>([]));
            await Task.WhenAll(mailboxesRead, listRead, scopesRead, freshnessRead, replyRead);
            Mailboxes = await mailboxesRead;
            Scopes = await scopesRead;
            Freshness = await freshnessRead;
            ReplyMailboxIds = (await replyRead)
                .Where(item => item.CanSend)
                .Select(item => item.Id)
                .ToHashSet();
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

    private static async Task<T> ReadAsync<T>(Func<Task<T>> read) => await read();

    /// <summary>
    /// Whether a received message offers Reply from the list: its mailbox can
    /// send and it has a sender to answer. Reply opens the message record's own
    /// reply, from the mailbox the message arrived in.
    /// </summary>
    public bool OffersReply(RetainedMailSummary item) =>
        Folder != MailFolderScope.Sent
        && ReplyMailboxIds.Contains(item.MailboxId)
        && !string.IsNullOrWhiteSpace(item.SenderAddress);

    /// <summary>
    /// The list itself and, with it, the message the preview pane renders. The
    /// pane is drawn from the list's own row, so the preview read adds only what
    /// the row does not carry.
    /// </summary>
    private async Task ReadListAsync(
        ActionActor actor,
        Guid? mailbox,
        int page,
        CancellationToken cancellationToken)
    {
        if (SearchValidationMessage is not null)
        {
            return;
        }

        if (Folder == MailFolderScope.DeletedItems && SearchTerm is not null)
        {
            DeletedResults = await searchDeletedMail.ExecuteAsync(
                actor,
                mailbox,
                SearchTerm,
                page,
                PageSize,
                cancellationToken);
            return;
        }

        Results = await listRetainedMail.ExecuteAsync(
            actor,
            new(
                mailbox,
                Folder,
                SearchTerm,
                CategoryFilter,
                NoCaseFoundFilter,
                UnreadOnly: false,
                OldestFirst,
                DismissedOnly: Dismissed),
            page,
            PageSize,
            cancellationToken);
        await LoadSelectedDetailAsync(actor, cancellationToken);
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

    /// <summary>Dismiss for the ticked rows of an incoming scope: the row's Dismiss, once per message.</summary>
    public Task<IActionResult> OnPostBulkDismissAsync(
        Guid[]? ids,
        string? operationKey,
        CancellationToken cancellationToken) =>
        BulkDismissalAsync(
            ids,
            operationKey,
            dismissedScope: false,
            (actor, id, key) => dismissRetainedMail.ExecuteAsync(new(id, actor, key), cancellationToken),
            OperatorLabels.Inbox.DismissedNotice);

    /// <summary>Restore for the ticked rows of the Dismissed scope: the row's Restore, once per message.</summary>
    public Task<IActionResult> OnPostBulkRestoreAsync(
        Guid[]? ids,
        string? operationKey,
        CancellationToken cancellationToken) =>
        BulkDismissalAsync(
            ids,
            operationKey,
            dismissedScope: true,
            (actor, id, key) => restoreRetainedMail.ExecuteAsync(new(id, actor, key), cancellationToken),
            OperatorLabels.Inbox.RestoredNotice);

    private async Task<IActionResult> BulkDismissalAsync(
        Guid[]? ids,
        string? operationKey,
        bool dismissedScope,
        Func<ActionActor, Guid, string, Task<RetainedMailDismissal?>> act,
        string notice)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (!TryParseListContext() || !OffersBulk(dismissedScope))
        {
            return NotFound();
        }

        // One key renders with the bulk form; each message's act is keyed by
        // it and the message, so a repeated post replays rather than repeats.
        var formKey = string.IsNullOrWhiteSpace(operationKey) ? NewOperationKey() : operationKey;
        return await ApplyToEachAsync(
            ids,
            async id => await act(actor, id, $"{formKey}:{id:N}") is not null,
            notice);
    }

    /// <summary>
    /// The ticked rows' classification correction: the message record's
    /// command once per message, each against the classification version its
    /// row showed, with the one category, case type and reason entered for all.
    /// </summary>
    public async Task<IActionResult> OnPostBulkClassifyAsync(
        Guid[]? ids,
        Dictionary<Guid, int>? versions,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (!TryParseListContext() || !OffersBulk(dismissedScope: false))
        {
            return NotFound();
        }
        if (ids is not { Length: > 0 })
        {
            return RedirectToList();
        }

        MessageModel.RefuseInvalidCorrection(
            ModelState, ClassificationKey, WorkType, CorrectionReason, out var category, out var workType);
        if (!ModelState.IsValid)
        {
            return await LoadAsync(actor, cancellationToken);
        }

        // A message posted without its row's classification version has no
        // decision to correct, and is named among the failures.
        return await ApplyToEachAsync(
            ids,
            async id => versions is not null
                && versions.TryGetValue(id, out var version)
                && await correctClassification.ExecuteAsync(
                    actor,
                    new(id, version, category, CorrectionReason!, workType),
                    cancellationToken) is not null,
            confirmation: null);
    }

    /// <summary>
    /// Where the ticked-row acts are offered, as the row's own Dismiss and
    /// Restore are: the incoming scopes, or the Dismissed scope; never Sent
    /// Items or Deleted Items.
    /// </summary>
    private bool OffersBulk(bool dismissedScope) =>
        Folder == MailFolderScope.Inbox && Dismissed == dismissedScope;

    /// <summary>
    /// Applies a single-message command to each ticked message in turn, each
    /// in its own transaction. A message the command refuses, cannot find or
    /// finds changed is named among the failures and the others still apply;
    /// an actor without casework is refused the whole post. An empty selection
    /// writes nothing.
    /// </summary>
    private async Task<IActionResult> ApplyToEachAsync(
        Guid[]? ids,
        Func<Guid, Task<bool>> apply,
        string? confirmation)
    {
        var succeeded = new List<string>();
        var failed = new List<string>();
        try
        {
            foreach (var id in (ids ?? []).Distinct())
            {
                bool applied;
                try
                {
                    applied = await apply(id);
                }
                catch (Exception exception)
                    when (exception is ArgumentException or MailClassificationConcurrencyException)
                {
                    applied = false;
                }
                (applied ? succeeded : failed).Add(id.ToString("D"));
            }
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }

        BulkSucceeded = succeeded.Count == 0 ? null : [.. succeeded];
        BulkFailed = failed.Count == 0 ? null : [.. failed];
        if (confirmation is not null && succeeded.Count > 0 && failed.Count == 0)
        {
            TempData["Confirmation"] = confirmation;
        }
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
                out var category,
                out var noCaseFound)
            || (folder is MailFolderScope.DeletedItems or MailFolderScope.Sent && normalizedQueue is not null))
        {
            return false;
        }

        Folder = folder;
        Dismissed = dismissed;
        OldestFirst = oldestFirst;
        QueueFilter = normalizedQueue;
        CategoryFilter = category;
        NoCaseFoundFilter = noCaseFound;
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
            var detail = await getRetainedMailPreview.ExecuteAsync(actor, id, cancellationToken: cancellationToken);
            if (detail is null)
            {
                return NotFound();
            }

            var summary = detail.Summary;
            var state = StateValue(detail);
            return new JsonResult(new
            {
                id = summary.Id,
                sender = SenderLine(summary),
                subject = SubjectLine(summary),
                receivedAtUtc = summary.ReceivedAtUtc,
                received = $"{OperatorLabels.OfficeDate(summary.ReceivedAtUtc)} {OperatorLabels.OfficeClock(summary.ReceivedAtUtc)}",
                mailbox = summary.MailboxAddress,
                state,
                stateTone = state is null ? null : OperatorLabels.StatusTone(state),
                excerpt = summary.BodyExcerpt ?? "No excerpt available",
                attachments = OperatorLabels.Inbox.Attachments(detail.Attachments),
                classification = ClassificationValue(detail),
                association = MessageModel.AssociationLabel(summary.CaseReference),
                folder = FolderValue(detail),
                // A pinned row's pane offers its own Open Case or Open Triage.
                caseUrl = summary.CaseId is { } caseId ? Url.Page("/Cases/Details", new { id = caseId }) : null,
                caseAction = summary.CaseId is null
                    ? null
                    : summary.IsTriageCase ? OperatorLabels.Inbox.OpenTriage : OperatorLabels.Inbox.OpenCase
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

        // The list row is the summary the pane draws, search matches included.
        SelectedDetail = await getRetainedMailPreview.ExecuteAsync(
            actor,
            row.Id,
            row,
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
            NoCaseFound: definition.NoCaseFound,
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

    /// <summary>The preview's Folder cell: the scope the message was read from.</summary>
    public static string FolderValue(RetainedMailPreview preview) => FolderLabel(preview.Folder);

    /// <summary>
    /// The preview's Classification cell: the current decision's label, else
    /// "Not yet processed" for received mail. A Sent item is never processed;
    /// with no decision its cell says the value is not recorded.
    /// </summary>
    public static string ClassificationValue(RetainedMailPreview preview) =>
        preview.Classification is { } current
            ? MessageModel.DecisionLabel(current)
            : preview.Folder == MailFolderScope.Sent
                ? OperatorLabels.CaseWorkspace.AbsentValue
                : MessageModel.ClassificationLabel(null);

    /// <summary>The preview's processing chip: received mail's outcome; a Sent item carries none.</summary>
    public static string? StateValue(RetainedMailPreview preview) =>
        preview.Folder == MailFolderScope.Sent ? null : MessageModel.OutcomeLabel(preview.Summary);

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
                if (Definition.NoCaseFound)
                {
                    fields["queue"] = NoCaseFoundQueueKey;
                }
                return fields;
            }
        }
    }

    public sealed record MailScopeDefinition(
        string Label,
        string IconId,
        MailFolderScope Folder,
        bool NoCaseFound = false,
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
                NoCaseFound ? NoCaseFoundQueueKey : null,
                StringComparison.Ordinal);
    }

    /// <summary>
    /// The scope rail, in the planned order (Inbox, 13 September): no Unread scope —
    /// read state is not a queue; unread rows are bold — and a Dismissed scope last.
    /// The No Case Found scope reuses the key the Category select already binds.
    /// </summary>
    public static readonly IReadOnlyList<MailScopeDefinition> ScopeDefinitions =
    [
        new("All incoming", "inbox", MailFolderScope.Inbox),
        new(OperatorLabels.Inbox.NoCaseFound, "search", MailFolderScope.Inbox, NoCaseFound: true),
        new("Sent Items", "send", MailFolderScope.Sent),
        new(OperatorLabels.Inbox.DismissedScope, "x", MailFolderScope.Inbox, Dismissed: true)
    ];

    /// <summary>The <c>queue</c> value of the No Case Found scope.</summary>
    public const string NoCaseFoundQueueKey = "no_case_found";

    public sealed record MailViewOption(string Value, string Label);

    /// <summary>
    /// The Category select: one flat list, every mail category in order and
    /// then No Case Found. Each value is the category's code.
    /// </summary>
    public static IReadOnlyList<MailViewOption> CategoryViews { get; } =
    [
        .. MailCategories.All.Select(category => new MailViewOption(
            MailCategories.Code(category),
            OperatorLabels.MailClassification(category))),
        new(NoCaseFoundQueueKey, OperatorLabels.Inbox.NoCaseFound)
    ];

    /// <summary>
    /// A <c>queue</c> value: a mail category code or <c>no_case_found</c>;
    /// anything else is not an address.
    /// </summary>
    internal static bool TryParseQueue(
        string? value,
        out string? normalized,
        out MailCategory? category,
        out bool noCaseFound)
    {
        normalized = null;
        category = null;
        noCaseFound = false;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var candidate = value.Trim();
        if (string.Equals(candidate, NoCaseFoundQueueKey, StringComparison.Ordinal))
        {
            normalized = NoCaseFoundQueueKey;
            noCaseFound = true;
            return true;
        }
        if (!MailCategories.TryParse(candidate, out var parsed))
        {
            return false;
        }

        normalized = candidate;
        category = parsed;
        return true;
    }

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
