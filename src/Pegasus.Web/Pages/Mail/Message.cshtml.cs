using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Pegasus.Core.Actors;
using Pegasus.Core.AiWork;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Operations;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Email;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Mail;

/// <summary>
/// One retained message.
/// </summary>
/// <remarks>
/// Reads one retained message and invokes the existing Core commands for its
/// classification, Case association and post-report AI job. The Inbox opens it
/// in a dialog over the list, fetched from the <c>Dialog</c> handler; this page
/// is the same record for a new tab and for no script (R159 c).
/// </remarks>
/// <summary>The next action a message's classification offers (FRD-20).</summary>
public enum MessageOffer
{
    CreateCase,
    OpenTriage,
    RegisterImages
}

public sealed class MessageModel(
    GetRetainedMail getRetainedMail,
    StartQueryResponse startQueryResponse,
    IAiJobQueries aiJobQueries,
    IAiJobStore aiJobStore,
    ISendToAiControl sendToAiControl,
    TimeProvider clock,
    CorrectRetainedMailClassification correctClassification,
    ISearchCases searchCases,
    IGetCaseHeader getCaseHeader,
    Pegasus.Core.Triage.IGetTriage getTriage,
    IIntakeAssociationDestinationQueries destinations,
    IStaffMailSend staffMailSend,
    IStaffMailAttachmentResolver attachmentResolver,
    IApprovedMailboxStore approvedMailboxes,
    IGetIntake getIntake,
    OneCommandEditAuthority editAuthority,
    ILinkIntake linkIntake,
    IReverseIntakeLink reverseIntakeLink,
    IOpenCaseQuery openCaseQuery,
    IMarkCaseQueryDone markCaseQueryDone,
    DismissRetainedMail dismissRetainedMail,
    RestoreRetainedMail restoreRetainedMail,
    IGetRetainedMailAttachmentOutcomes attachmentOutcomes,
    GetIntakeOfferedActions offeredActions,
    Pegasus.Web.Intake.StaffIntakeActions intakeActions,
    ILogger<MessageModel> logger) : StaffPageModel
{
    /// <summary>
    /// One attachment's outcome in operator words (message planning, 13 September):
    /// Case created, Unidentified, Vehicle images, Could not be read (with the reason
    /// and its Unidentified item) or Processing failed. Nothing links to a receipt.
    /// </summary>
    public sealed record AttachmentOutcome(
        string Label,
        string Tone,
        string? Href = null,
        string? LinkText = null,
        string? Reason = null);

    /// <summary>An attachment row: the retained attachment, its outcome and the viewer address of its retained file.</summary>
    public sealed record AttachmentRow(
        RetainedMailAttachment Attachment,
        AttachmentOutcome Outcome,
        string? ViewerHref);

    public IReadOnlyList<AttachmentRow> AttachmentRows { get; private set; } = [];

    /// <summary>The list this message was opened from was the Dismissed scope.</summary>
    public bool ListDismissed { get; private set; }

    public bool IsDismissed => Detail.Summary.DismissedAtUtc is not null;

    /// <summary>
    /// The record is drawn for the Inbox dialog: its forms carry <c>dialog</c>,
    /// so a write lands back on the Inbox with this message open (v41 I2).
    /// </summary>
    [BindProperty(SupportsGet = true, Name = "dialog")]
    public bool InDialog { get; set; }

    /// <summary>
    /// The keys a link post that could not settle its edit authority keeps on
    /// the redrawn form, so the retry is the same confirmation.
    /// </summary>
    public string? RetainedLeaseOperationKey { get; private set; }

    public string? RetainedOperationKey { get; private set; }

    public IReadOnlyList<MailClassificationSelection.SelectionOption> ClassificationOptions =>
        MailClassificationSelection.TypeSelectOptions;

    /// <summary>
    /// The list scope this message was opened from, carried through untouched so
    /// Back reconstructs the exact position the operator left.
    /// </summary>
    [BindProperty(SupportsGet = true, Name = "mailbox")]
    public string? MailboxFilter { get; set; }

    [BindProperty(SupportsGet = true, Name = "folder")]
    public string? FolderFilter { get; set; }

    [BindProperty(SupportsGet = true, Name = "pageNumber")]
    public int? PageNumber { get; set; }

    [BindProperty(SupportsGet = true, Name = "search")]
    public string? SearchTerm { get; set; }

    [BindProperty(SupportsGet = true, Name = "queue")]
    public string? QueueFilter { get; set; }

    /// <summary>The list's sort toggle state this message was opened from.</summary>
    [BindProperty(SupportsGet = true, Name = "sort")]
    public string? SortOrder { get; set; }

    public bool OldestFirst { get; private set; }

    private bool NoCaseFoundFilter { get; set; }

    [BindProperty(SupportsGet = true, Name = "section")]
    public string? Section { get; set; }

    [BindProperty]
    public int ExpectedClassificationVersion { get; set; }

    [BindProperty]
    public string? ClassificationKey { get; set; }

    [BindProperty]
    public string? CorrectionReason { get; set; }

    [BindProperty]
    public string? WorkType { get; set; }

    [BindProperty(SupportsGet = true, Name = "compose")]
    public string? CorrespondenceMode { get; set; }

    [BindProperty(SupportsGet = true, Name = "mailOperationId")]
    public Guid? CorrespondenceOperationId { get; set; }

    /// <summary>
    /// The reply composer's Mark query done tick: the replied message's own
    /// open query is marked done when the Sent item is observed. Pre-ticked
    /// when the composer was reached from Answer query or an AI draft on that
    /// message.
    /// </summary>
    [BindProperty(SupportsGet = true, Name = "markQueryDone")]
    public bool MarkQueryDone { get; set; }

    /// <summary>Whether this message holds an open query on its linked Case.</summary>
    public bool MessageHoldsOpenQuery { get; private set; }

    /// <summary>
    /// The Draft ready Query response job the composer was opened on (FRD-27):
    /// its text prefills Message, and the send carries the job so an accepted
    /// send completes it. Cleared when the job is no longer that draft.
    /// </summary>
    [BindProperty(SupportsGet = true, Name = "aiJob")]
    public Guid? AiJobId { get; set; }

    /// <summary>The draft the composer holds, for its one AI line; null when none applied.</summary>
    public AiJobRecord? AiDraft { get; private set; }

    /// <summary>
    /// The live Query response job for this message, which the record bar
    /// draws in place of **Draft reply with AI**: pending while Queued or
    /// Taken, **Reply with AI draft** once Draft ready.
    /// </summary>
    public QueryDraft? QueryResponseJob { get; private set; }

    /// <summary>
    /// The Case page the Correspondence tab's Tag as query returns to after
    /// its one-click correction; absent on the Inbox record's own dialog.
    /// </summary>
    [BindProperty]
    public Guid? ReturnCaseId { get; set; }

    /// <summary>Whether the reply form offers the tick: a reply or reply-all to a message that holds an open query.</summary>
    public bool OffersMarkQueryDone =>
        CorrespondenceMode is "reply" or "reply-all" && MessageHoldsOpenQuery;

    /// <summary>
    /// The one Case / PO field of the Reply/Forward composer: it searches as
    /// staff type and takes a typed reference as it is. Choosing a Case from
    /// its list fixes that Case and its version.
    /// </summary>
    [BindProperty(SupportsGet = true, Name = "correspondenceCaseReference")]
    public string? CorrespondenceCaseReference { get; set; }

    /// <summary>The reference whose version <see cref="ExpectedCorrespondenceCaseVersion"/> fixed; null when none was chosen.</summary>
    [BindProperty]
    public string? FixedCorrespondenceCaseReference { get; set; }

    [BindProperty]
    public string? SelectedCorrespondenceCaseReference { get; set; }

    [BindProperty]
    public long ExpectedCorrespondenceCaseVersion { get; set; }

    [BindProperty]
    public string? CorrespondenceTo { get; set; }

    [BindProperty]
    public string? CorrespondenceCc { get; set; }

    [BindProperty]
    public string? CorrespondenceSubject { get; set; }

    [BindProperty]
    public string? CorrespondenceBody { get; set; }

    [BindProperty]
    public List<string> SelectedAttachments { get; set; } = [];

    // Nullable on purpose: a non-nullable bound string is implicitly required,
    // and every post from this page that is not the compose form (a correction,
    // a dismissal) omits it. Send validates it itself below.
    [BindProperty]
    public string? CorrespondenceOperationKey { get; set; } = NewOperationKey();

    [TempData]
    public string? ClassificationNotice { get; set; }

    [TempData]
    public string? AssociationNotice { get; set; }

    [TempData]
    public string? QueryResponseNotice { get; set; }

    [TempData]
    public string? CorrespondenceNotice { get; set; }

    /// <summary>
    /// The line a reply composed on an AI draft returns when its send left the
    /// job as it was, kept across the redirect to its send status.
    /// </summary>
    [TempData]
    public string? AiJobNotCompletedNotice { get; set; }

    public RetainedMailDetail Detail { get; private set; } = null!;

    public IReadOnlyList<AiJobRecord> AiJobs { get; private set; } = [];

    public bool IsQueryResponseSource { get; private set; }

    public string? QueryResponseCondition { get; private set; }

    public bool QueryResponseEnabled =>
        IsQueryResponseSource && QueryResponseCondition is null;

    public IntakeReceipt? AssociationReceipt { get; private set; }

    /// <summary>What the message's receipt offers right now; none once a Case holds it.</summary>
    public IntakeOfferedActions Offers { get; private set; } = IntakeOfferedActions.None;

    /// <summary>
    /// The one next action the current classification calls for (operator,
    /// 1 October 2026): staff press it; nothing runs on its own. Absent when
    /// the classification names none, the receipt no longer qualifies, or a
    /// Case already holds the message.
    /// </summary>
    public MessageOffer? Offer =>
        AssociationReceipt is not { CurrentCaseId: null }
            ? null
            : Detail.Classification?.Current is { IsClassified: true, Category: { } category }
                ? category switch
                {
                    MailCategory.NewInstruction when Offers.CanCreateCase => MessageOffer.CreateCase,
                    MailCategory.TriageRequest when Offers.CanOpenTriage => MessageOffer.OpenTriage,
                    _ => null
                }
                : Offers.CanRegisterImages ? MessageOffer.RegisterImages : null;

    public CaseHeader? CurrentCase { get; private set; }

    /// <summary>
    /// The Case the message is linked to, of either kind, in the shape of a
    /// link destination: the version its link authority is checked against
    /// and, for a Triage Case, its Triage state. A Triage Case has no Case
    /// workflow, so <see cref="CurrentCase"/> is null for it.
    /// </summary>
    public IntakeAssociationDestination? CurrentDestination { get; private set; }

    /// <summary>
    /// Whether Link to Case is drawn, with its reason dialog and the Case / PO
    /// picker: the message is not linked and its decision leaves a Case to link
    /// it to (Core's <see cref="IntakeAssociationDestinationPolicy.CanOffer"/>).
    /// </summary>
    public bool OffersLinkToCase =>
        AssociationReceipt is { } receipt
        && IntakeAssociationDestinationPolicy.CanOffer(receipt);

    /// <summary>
    /// Whether Change linked Case is drawn: the message is linked and its
    /// decision leaves a Case to link it to (Core's
    /// <see cref="IntakeAssociationDestinationPolicy.CanOfferToChange"/>).
    /// </summary>
    public bool OffersChangeLinkedCase =>
        CurrentDestination is not null
        && AssociationReceipt is { } receipt
        && IntakeAssociationDestinationPolicy.CanOfferToChange(receipt);

    public MailFolderScope ListFolder { get; private set; } = MailFolderScope.Inbox;

    /// <summary>
    /// True where the message is no longer inside the list scope it was opened
    /// from. It still renders; the screen states the mismatch and offers the way
    /// back rather than replacing the message with a not-found.
    /// </summary>
    public bool OutsideListScope { get; private set; }

    public ApprovedMailbox? CorrespondenceMailbox { get; private set; }

    public CaseHeader? CorrespondenceCase { get; private set; }

    private bool correspondenceMailboxResolved;

    private bool correspondenceCaseResolved;

    private bool attachmentOptionsRead;

    private bool retainedOperationRead;

    private bool aiDraftApplied;

    public IReadOnlyList<StaffMailAttachmentOption> AvailableAttachments { get; private set; } = [];

    public bool StaffMailAvailable => staffMailSend is not UnavailableStaffMailSend;

    public StaffMailOperation? CorrespondenceOperation { get; private set; }

    public bool CanCorrespond => StaffMailAvailable
        && CorrespondenceMailbox is not null;

    public bool CanReply => CanCorrespond && ReplyRecipients(Detail).To.Length > 0;

    public Task<IActionResult> OnGetAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        ShowAsync(id, inDialog: false, cancellationToken);

    /// <summary>
    /// The record as the Inbox's message dialog, fetched on open: the same read,
    /// actor and refusals as the page, and like it a read that changes nothing.
    /// One reader serves the page and the dialog.
    /// </summary>
    public Task<IActionResult> OnGetDialogAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        ShowAsync(id, inDialog: true, cancellationToken);

    private async Task<IActionResult> ShowAsync(
        Guid id,
        bool inDialog,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        if (!TryParseListContext(out var listFolder))
        {
            return NotFound();
        }
        if (CorrespondenceMode is not null
            && !TryComposeMode(CorrespondenceMode, out _))
        {
            return NotFound();
        }

        InDialog = inDialog;
        ListFolder = listFolder;
        MailboxFilter = string.IsNullOrWhiteSpace(MailboxFilter) ? null : MailboxFilter.Trim();

        RetainedMailDetail? detail;
        try
        {
            detail = await getRetainedMail.ExecuteAsync(actor, id, SearchTerm, cancellationToken);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (ArgumentException)
        {
            return NotFound();
        }

        if (detail is null)
        {
            return NotFound();
        }

        Detail = detail;
        OutsideListScope = IsOutsideListScope(detail, listFolder);
        await LoadAssociationSafelyAsync(actor, cancellationToken);
        await LoadAttachmentOutcomesAsync(actor, cancellationToken);
        await LoadAiJobContextAsync(cancellationToken);
        if (StaffMailAvailable)
        {
            await LoadRetainedOperationAsync(actor, cancellationToken);
            if (!await LoadCorrespondenceContextAsync(actor, initializeForm: true, cancellationToken))
            {
                CorrespondenceMode = null;
            }
        }
        else
        {
            CorrespondenceMode = null;
        }
        if (inDialog)
        {
            return Partial("Shared/_MessageDialog", this);
        }
        return Page();
    }

    /// <summary>
    /// The message's head, text and attachment names as a fragment, for the
    /// Case's Correspondence dialog, with the record's Reply, Reply all and
    /// Forward where the record offers them. Each opens the record's composer
    /// with the dialog's Case (<see cref="CorrespondenceCaseReference"/>)
    /// chosen. The same actor and read as the record, and like it a read that
    /// changes nothing.
    /// </summary>
    public async Task<IActionResult> OnGetContentAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        RetainedMailDetail? detail;
        try
        {
            detail = await getRetainedMail.ExecuteAsync(actor, id, cancellationToken);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (ArgumentException)
        {
            return NotFound();
        }

        if (detail is null)
        {
            return NotFound();
        }

        Detail = detail;
        CorrespondenceCaseReference = TryNormalizeCaseReference(CorrespondenceCaseReference, out var reference)
            ? reference
            : null;
        if (StaffMailAvailable)
        {
            await LoadRetainedOperationAsync(actor, cancellationToken);
            CorrespondenceMailbox = await ResolveCorrespondenceMailboxAsync(cancellationToken);
        }
        return Partial("Shared/_MessageContent", this);
    }

    /// <summary>The route line under a message's subject: its forwarder and its To and Cc recipients.</summary>
    public static IReadOnlyList<string> RouteParts(RetainedMailDetail detail)
    {
        var parts = new List<string>();
        if (IndexModel.ForwarderLine(detail.Summary) is { } forwarder)
        {
            parts.Add($"Forwarded by {forwarder}");
        }
        if (detail.ToAddresses.Count > 0)
        {
            parts.Add($"To {string.Join(", ", detail.ToAddresses)}");
        }
        if (detail.CcAddresses.Count > 0)
        {
            parts.Add($"Cc {string.Join(", ", detail.CcAddresses)}");
        }
        return parts;
    }

    /// <summary>
    /// Dismiss on the record: always allowed; an open Unidentified item stays open
    /// (Inbox, 13 September). From the dialog, <paramref name="next"/> is the
    /// message the list reads after this one, and the Inbox opens it (v41 I10).
    /// </summary>
    public Task<IActionResult> OnPostDismissAsync(
        Guid id, string? operationKey, Guid? next, CancellationToken cancellationToken) =>
        ChangeDismissalAsync(
            id,
            actor => dismissRetainedMail.ExecuteAsync(
                new(id, actor, string.IsNullOrWhiteSpace(operationKey) ? NewOperationKey() : operationKey),
                cancellationToken),
            OperatorLabels.Inbox.DismissedNotice,
            next);

    /// <summary>Restore on the record of a dismissed message.</summary>
    public Task<IActionResult> OnPostRestoreAsync(
        Guid id, string? operationKey, Guid? next, CancellationToken cancellationToken) =>
        ChangeDismissalAsync(
            id,
            actor => restoreRetainedMail.ExecuteAsync(
                new(id, actor, string.IsNullOrWhiteSpace(operationKey) ? NewOperationKey() : operationKey),
                cancellationToken),
            OperatorLabels.Inbox.RestoredNotice,
            next);

    private async Task<IActionResult> ChangeDismissalAsync(
        Guid id,
        Func<ActionActor, Task<RetainedMailDismissal?>> change,
        string notice,
        Guid? next)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (!TryParseListContext(out _))
        {
            return NotFound();
        }

        try
        {
            if (await change(actor) is null)
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

        TempData["Confirmation"] = notice;
        // From the dialog the message has left its list: the Inbox opens the one
        // that followed it, else the one before it, else shows the list (v41 I10).
        return InDialog ? LandOnInbox(next, null, null) : LandOnRecord(id);
    }

    private async Task LoadAttachmentOutcomesAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        if (ActiveSection != "attachments" || Detail.Attachments.Count == 0)
        {
            return;
        }

        var receipt = AssociationReceipt;
        var assets = receipt?.AssetRecords ?? [];
        // Core decides each attachment's own outcome (Case, Unidentified, Vehicle
        // images, Could not be read, Processing failed) from its receipt and assets.
        var outcomes = receipt is null
            ? []
            : await attachmentOutcomes.ExecuteAsync(actor, receipt.Id, receipt, cancellationToken);
        AttachmentRows = Detail.Attachments
            .Select(attachment =>
            {
                var asset = attachment.IntakeAssetId is { } assetId
                    ? assets.SingleOrDefault(item => item.Id == assetId)
                    : null;
                var outcome = asset is null ? null : outcomes.FirstOrDefault(item => item.AssetId == asset.Id);
                return new AttachmentRow(
                    attachment,
                    Describe(outcome, receipt),
                    receipt is not null && asset is not null
                        ? $"/Received/{receipt.Id:D}/Asset/{asset.Id:D}"
                        : null);
            })
            .ToArray();
    }

    /// <summary>One attachment's outcome in operator words, with the record it opens.</summary>
    private AttachmentOutcome Describe(RetainedMailAttachmentOutcome? outcome, IntakeReceipt? receipt)
    {
        if (outcome is null)
        {
            return new("Not yet processed", "neutral");
        }

        var href = outcome.Record is { } record
            ? record.Kind switch
            {
                AttachmentOutcomeRecordKind.Case => $"/Cases/{record.Id:D}",
                AttachmentOutcomeRecordKind.ImageIntake => $"/VehicleImages/{record.Id:D}",
                _ => $"/Unidentified/{record.Id:D}"
            }
            : null;
        var linkText = outcome.Record?.Reference;
        return outcome.Kind switch
        {
            AttachmentOutcomeKind.CaseCreated => new("Case created", "green", href, linkText),
            AttachmentOutcomeKind.LinkedToCase => new("Linked to Case", "green", href, linkText),
            AttachmentOutcomeKind.VehicleImages => new("Vehicle images", "green", href, linkText),
            AttachmentOutcomeKind.CouldNotBeRead => new(
                "Could not be read",
                "amber",
                href,
                linkText,
                outcome.Reason ?? (receipt is null ? null : OperatorLabels.IntakeCannotBecomeCaseReason(receipt.Decision))),
            AttachmentOutcomeKind.ProcessingFailed => new(
                "Processing failed",
                "red",
                href,
                linkText,
                outcome.Reason ?? OperatorLabels.IntakeFailure(receipt?.FailureCode)),
            AttachmentOutcomeKind.Triage => new("Triage", "navy"),
            AttachmentOutcomeKind.Unidentified => new("Unidentified", "amber", href, linkText),
            AttachmentOutcomeKind.NotYetProcessed => new("Not yet processed", "neutral"),
            _ => new(OutcomeLabel(Detail.Summary), "neutral")
        };
    }

    public Task<IActionResult> OnPostReplyAsync(Guid id, CancellationToken cancellationToken) =>
        SendCorrespondenceAsync(id, StaffMailComposeMode.Reply, cancellationToken);

    public Task<IActionResult> OnPostReplyAllAsync(Guid id, CancellationToken cancellationToken) =>
        SendCorrespondenceAsync(id, StaffMailComposeMode.ReplyAll, cancellationToken);

    public Task<IActionResult> OnPostForwardAsync(Guid id, CancellationToken cancellationToken) =>
        SendCorrespondenceAsync(id, StaffMailComposeMode.Forward, cancellationToken);

    private const string CaseOptionsView = "/Pages/Shared/_CaseOptions.cshtml";

    /// <summary>
    /// The Case / PO field's option list for Reply/Forward, fetched as staff
    /// type. On the record page choosing an option posts
    /// <c>SelectCorrespondenceCase</c>; the dialog's picker only fills the
    /// field, so a reply never leaves the dialog for the record page.
    /// </summary>
    public async Task<IActionResult> OnGetCorrespondenceCaseOptionsAsync(
        Guid id,
        string? q,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (!StaffMailAvailable) return NotFound();
        return Partial(
            CaseOptionsView,
            new Pegasus.Web.Presentation.CaseOptions(
                (await SearchCasesAsync(actor, q, cancellationToken))
                    .Select(item => new CaseOption(item.Reference, item.Registration, item.Claimant))
                    .ToList(),
                "message-case-option",
                Url.Page("/Mail/Message", "SelectCorrespondenceCase", new { id })!,
                nameof(SelectedCorrespondenceCaseReference)));
    }

    public async Task<IActionResult> OnPostSelectCorrespondenceCaseAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (!StaffMailAvailable || !TryParseListContext(out _)) return NotFound();
        CorrespondenceCaseReference = SelectedCorrespondenceCaseReference;
        var result = await ReloadAsync(actor, id, cancellationToken);
        if (CorrespondenceCase is null)
        {
            ModelState.AddModelError(nameof(CorrespondenceCaseReference), ChooseOneCaseMessage);
            return result;
        }
        ModelState.Remove(nameof(CorrespondenceCaseReference));
        ModelState.Remove(nameof(FixedCorrespondenceCaseReference));
        ModelState.Remove(nameof(ExpectedCorrespondenceCaseVersion));
        FixedCorrespondenceCaseReference = CorrespondenceCase.Summary.Reference;
        ExpectedCorrespondenceCaseVersion = CorrespondenceCase.Workflow.Version;
        return result;
    }

    /// <summary>
    /// Whether the Case the composer resolved is the one a selection fixed,
    /// so its fixed version applies. A reference typed in full was never
    /// fixed and sends against the Case as it is now.
    /// </summary>
    private bool IsFixedCorrespondenceCase(CaseHeader details) =>
        !string.IsNullOrWhiteSpace(FixedCorrespondenceCaseReference)
        && string.Equals(
            details.Summary.Reference,
            FixedCorrespondenceCaseReference.Trim(),
            StringComparison.OrdinalIgnoreCase);

    public async Task<IActionResult> OnPostCreateQueryResponseAsync(
        Guid id,
        string operationKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (!TryParseListContext(out _))
        {
            return NotFound();
        }

        try
        {
            // One owner with the Case's Queries row and pegasus_ai_job_create (FRD-27).
            await startQueryResponse.ExecuteAsync(new(actor, id, operationKey), cancellationToken);
            QueryResponseNotice = OperatorLabels.QueryResponseJobs.Created;
            return LandOn(id);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return await ReloadAsync(actor, id, cancellationToken);
        }
    }

    /// <summary>
    /// Link to Case: one step (v41 I13). The Case / PO the operator named and
    /// the reason arrive together; this claims the Case's edit authority through
    /// <see cref="OneCommandEditAuthority"/>, runs the link under it and releases or
    /// compensates on every way out. Staff linking reaches every Case and every
    /// Triage Case in any state (operator, 24 September 2026); the destination
    /// owner refuses only an archived Case.
    /// </summary>
    public Task<IActionResult> OnPostLinkCaseAsync(
        Guid id,
        string? caseReference,
        long expectedIntakeVersion,
        string leaseOperationKey,
        string operationKey,
        string Reason,
        CancellationToken cancellationToken) =>
        AssociationWriteAsync(id, async actor =>
        {
            RequireAssociationConfirmation(operationKey, leaseOperationKey, Reason);
            var binding = await GetExactAssociationAsync(actor, id, cancellationToken);
            if (binding is null)
            {
                return NotFound();
            }
            if (binding.Version != expectedIntakeVersion
                || binding.CurrentCaseId is not null)
            {
                throw new IntakeVersionConflictException();
            }

            // A message the policy does not let be linked is refused before any
            // Case is searched or claimed.
            if (!IntakeAssociationDestinationPolicy.CanOffer(binding))
            {
                ModelState.AddModelError(
                    string.Empty, OperatorLabels.IntakeCannotBecomeCaseReason(binding.Decision));
                return await ReloadAsync(actor, id, cancellationToken);
            }

            // Only the destination's authority row is read: the claim needs its
            // version and the post shows none of its facts.
            var targetCaseId = await ResolveCaseIdAsync(actor, caseReference, cancellationToken);
            var destination = targetCaseId is { } caseId
                ? await destinations.GetAuthorityAsync(caseId, actor, cancellationToken)
                : null;
            if (destination is null)
            {
                ModelState.AddModelError(string.Empty, ChooseOneCaseMessage);
                return await ReloadAsync(actor, id, cancellationToken);
            }

            var failure = await LinkStepAsync(
                actor, binding, destination, leaseOperationKey, operationKey, Reason, cancellationToken);
            if (failure is not null)
            {
                return await RefuseAssociationAsync(
                    actor, id, failure, leaseOperationKey, operationKey, cancellationToken);
            }

            AssociationNotice = LinkedNotice;
            return LandOn(id);
        }, cancellationToken);

    /// <summary>
    /// Unlink: one step (v41 I13), through the reason dialog. Where the message's
    /// own acceptance created the Case, the dialog said unlinking cancels it.
    /// </summary>
    public Task<IActionResult> OnPostUnlinkCaseAsync(
        Guid id,
        Guid caseId,
        long expectedIntakeVersion,
        string leaseOperationKey,
        string operationKey,
        string Reason,
        CancellationToken cancellationToken) =>
        AssociationWriteAsync(id, async actor =>
        {
            RequireAssociationConfirmation(operationKey, leaseOperationKey, Reason);
            var binding = await GetExactAssociationAsync(actor, id, cancellationToken);
            if (binding is null)
            {
                return NotFound();
            }
            if (binding.Version != expectedIntakeVersion
                || binding.CurrentCaseId != caseId)
            {
                throw new IntakeVersionConflictException();
            }

            // Only the linked Case's authority row is read: the claim needs its
            // version and the post shows none of its facts.
            var current = await destinations.GetAuthorityAsync(caseId, actor, cancellationToken);
            if (current is null)
            {
                throw new IntakeVersionConflictException();
            }

            var failure = await UnlinkStepAsync(
                actor, binding, caseId, current, leaseOperationKey, operationKey, Reason, cancellationToken);
            if (failure is not null)
            {
                return await RefuseAssociationAsync(
                    actor, id, failure, leaseOperationKey, operationKey, cancellationToken);
            }

            AssociationNotice = "Message unlinked from the confirmed case.";
            return LandOn(id);
        }, cancellationToken);

    /// <summary>
    /// Change linked Case (v41 I9, I13): the existing Unlink and the existing
    /// Link to Case in one post, with the one reason for both, each under its
    /// own edit authority and each keeping its own history entry (FRD-21: Unlinked,
    /// Relinked with a reason). The new Case is found and its authority claimed
    /// before anything is unlinked, so a refused target leaves the message linked.
    /// </summary>
    public Task<IActionResult> OnPostChangeLinkedCaseAsync(
        Guid id,
        string? caseReference,
        long expectedIntakeVersion,
        string leaseOperationKey,
        string operationKey,
        string linkLeaseOperationKey,
        string linkOperationKey,
        string Reason,
        CancellationToken cancellationToken) =>
        AssociationWriteAsync(id, async actor =>
        {
            RequireAssociationConfirmation(operationKey, leaseOperationKey, Reason);
            RequireAssociationConfirmation(linkOperationKey, linkLeaseOperationKey, Reason);
            var binding = await GetExactAssociationAsync(actor, id, cancellationToken);
            if (binding is null)
            {
                return NotFound();
            }
            if (binding.Version != expectedIntakeVersion
                || binding.CurrentCaseId is not { } currentCaseId)
            {
                throw new IntakeVersionConflictException();
            }

            // The unlink is made first and the link then needs a message the
            // policy lets be linked, so a message it does not is refused before
            // anything is claimed or unlinked.
            if (!IntakeAssociationDestinationPolicy.CanOfferToChange(binding))
            {
                ModelState.AddModelError(
                    string.Empty, OperatorLabels.IntakeCannotBecomeCaseReason(binding.Decision));
                return await ReloadAsync(actor, id, cancellationToken);
            }

            var targetCaseId = await ResolveCaseIdAsync(actor, caseReference, cancellationToken);
            var target = targetCaseId is { } newCaseId && newCaseId != currentCaseId
                ? await destinations.GetAuthorityAsync(newCaseId, actor, cancellationToken)
                : null;
            if (target is null)
            {
                ModelState.AddModelError(string.Empty, ChooseOneCaseMessage);
                return await ReloadAsync(actor, id, cancellationToken);
            }

            var current = await destinations.GetAuthorityAsync(currentCaseId, actor, cancellationToken);
            if (current is null)
            {
                throw new IntakeVersionConflictException();
            }

            // The new Case's authority is claimed before anything is unlinked: a
            // Case a colleague is editing refuses here, with nothing written.
            var (claim, claimFailure) = await ClaimAuthorityAsync(
                actor, target, linkLeaseOperationKey, cancellationToken);
            if (claim is null)
            {
                return await RefuseAssociationAsync(
                    actor, id, claimFailure!, linkLeaseOperationKey, linkOperationKey, cancellationToken);
            }

            // The link command consumes the claim when it runs; until then every
            // way out gives it back.
            var claimHandedOn = false;
            try
            {
                var unlinkFailure = await UnlinkStepAsync(
                    actor, binding, currentCaseId, current, leaseOperationKey, operationKey, Reason, cancellationToken);
                if (unlinkFailure is not null)
                {
                    return await RefuseAssociationAsync(
                        actor, id, unlinkFailure, leaseOperationKey, operationKey, cancellationToken);
                }

                // The unlink advanced the receipt: the link is made against it as it now stands.
                var unlinked = await getIntake.ExecuteAsync(new(binding.Id, actor), cancellationToken)
                    ?? throw new IntakeVersionConflictException();

                claimHandedOn = true;
                var linkFailure = await RunClaimedAsync(
                    actor,
                    claim,
                    LinkCommand(actor, unlinked, target.CaseId, linkOperationKey, Reason, cancellationToken),
                    cancellationToken);
                if (linkFailure is not null)
                {
                    return await RefuseAssociationAsync(
                        actor, id, linkFailure, linkLeaseOperationKey, linkOperationKey, cancellationToken);
                }
            }
            finally
            {
                if (!claimHandedOn)
                {
                    await ReleaseClaimAsync(actor, claim);
                }
            }

            AssociationNotice = LinkedNotice;
            return LandOn(id);
        }, cancellationToken);

    private static readonly string LinkedNotice =
        $"Message linked to the confirmed case. {OperatorLabels.Upload.AppearUnderFiles}";

    /// <summary>
    /// What the three association writes share: staff and list context are
    /// checked, <paramref name="write"/> runs, a staff refusal is Forbid and any
    /// other recoverable failure redraws the record with its sentence.
    /// </summary>
    private async Task<IActionResult> AssociationWriteAsync(
        Guid id,
        Func<ActionActor, Task<IActionResult>> write,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (!TryParseListContext(out _))
        {
            return NotFound();
        }

        try
        {
            return await write(actor);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            ModelState.AddModelError(string.Empty, AssociationPreparationFailureMessage(exception));
            return await ReloadAsync(actor, id, cancellationToken);
        }
    }

    /// <summary>The link command, run under the destination's claimed authority, against the receipt as it stands.</summary>
    private Task<AssociationFailure?> LinkStepAsync(
        ActionActor actor,
        IntakeReceipt binding,
        IntakeAssociationDestination destination,
        string leaseOperationKey,
        string operationKey,
        string reason,
        CancellationToken cancellationToken) =>
        TryAssociateAsync(
            actor,
            destination,
            leaseOperationKey,
            LinkCommand(actor, binding, destination.CaseId, operationKey, reason, cancellationToken),
            cancellationToken);

    private Func<OneCommandEditClaim, Task> LinkCommand(
        ActionActor actor,
        IntakeReceipt binding,
        Guid caseId,
        string operationKey,
        string reason,
        CancellationToken cancellationToken) =>
        claim => linkIntake.ExecuteAsync(
            new(
                binding.Id,
                caseId,
                binding.Version,
                claim.Version,
                claim.Token,
                actor,
                operationKey,
                reason),
            cancellationToken);

    /// <summary>The unlink command, run under the linked Case's claimed authority, against the receipt as it stands.</summary>
    private Task<AssociationFailure?> UnlinkStepAsync(
        ActionActor actor,
        IntakeReceipt binding,
        Guid caseId,
        IntakeAssociationDestination current,
        string leaseOperationKey,
        string operationKey,
        string reason,
        CancellationToken cancellationToken) =>
        TryAssociateAsync(
            actor,
            current,
            leaseOperationKey,
            claim => reverseIntakeLink.ExecuteAsync(
                new(
                    binding.Id,
                    caseId,
                    binding.Version,
                    claim.Version,
                    claim.Token,
                    actor,
                    operationKey,
                    reason),
                cancellationToken),
            cancellationToken);

    /// <summary>
    /// The Case / PO options as staff type: the Cases and Triage Cases the
    /// message's receipt may be linked to, or, for a linked message, changed to.
    /// Choosing one fills the Case / PO field; nothing is written until the
    /// reason dialog is confirmed.
    /// </summary>
    public async Task<IActionResult> OnGetLinkCaseOptionsAsync(
        Guid id,
        string? q,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            var binding = await GetExactAssociationAsync(actor, id, cancellationToken);
            if (binding is null || !TryNormalizeCaseQueryValue(q, out var term))
            {
                return NotFound();
            }

            // Choosing an option only fills the Case / PO field.
            var found = binding.CurrentCaseId is null
                ? await destinations.SearchAsync(binding, term!, actor, cancellationToken)
                : await destinations.SearchToChangeAsync(binding, term!, actor, cancellationToken);
            return Partial(
                CaseOptionsView,
                new Pegasus.Web.Presentation.CaseOptions(
                    found.Select(item => new CaseOption(item.Reference, item.Registration, item.Claimant)).ToList(),
                    "mail-link-option"));
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

    public async Task<IActionResult> OnPostCorrectClassificationAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (!TryParseListContext(out _))
        {
            return NotFound();
        }
        RefuseInvalidCorrection(
            ModelState, ClassificationKey, WorkType, CorrectionReason, out var category, out var workType);
        if (!ModelState.IsValid)
        {
            return await ReloadAsync(actor, id, cancellationToken);
        }

        try
        {
            var result = await correctClassification.ExecuteAsync(
                actor,
                new(id, ExpectedClassificationVersion, category, CorrectionReason!, workType),
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
        catch (MailClassificationConcurrencyException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return await ReloadAsync(actor, id, cancellationToken);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return await ReloadAsync(actor, id, cancellationToken);
        }

        ClassificationNotice = "Type corrected. The previous decision and evidence remain in permanent history.";
        if (ReturnCaseId is { } returnCaseId && returnCaseId != Guid.Empty)
        {
            // Tag as query from the Case's Correspondence tab lands back on
            // that tab (operator, 11 October 2026).
            return RedirectToPage("/Cases/Details", new { id = returnCaseId, section = "files" });
        }
        return LandOn(id);
    }

    public async Task<IActionResult> OnPostOpenQueryAsync(
        Guid id,
        string? operationKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (!TryParseListContext(out _))
        {
            return NotFound();
        }

        try
        {
            var detail = await getRetainedMail.ExecuteAsync(actor, id, cancellationToken);
            if (detail?.Summary.CaseId is not { } caseId || detail.Summary.IsTriageCase)
            {
                return NotFound();
            }

            await openCaseQuery.ExecuteAsync(
                new(caseId, actor, string.IsNullOrWhiteSpace(operationKey) ? NewOperationKey() : operationKey, id, null, null),
                cancellationToken);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return await ReloadAsync(actor, id, cancellationToken);
        }

        return LandOn(id);
    }

    public async Task<IActionResult> OnPostMarkQueryDoneAsync(
        Guid id,
        Guid queryId,
        string? operationKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (!TryParseListContext(out _) || queryId == Guid.Empty)
        {
            return NotFound();
        }

        try
        {
            await markCaseQueryDone.ExecuteAsync(
                new(queryId, actor, string.IsNullOrWhiteSpace(operationKey) ? NewOperationKey() : operationKey),
                cancellationToken);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return await ReloadAsync(actor, id, cancellationToken);
        }

        return LandOn(id);
    }

    /// <summary>
    /// Place on Hold, as the Case page offers it: Core allows the hold and no
    /// colleague holds the edit lease.
    /// </summary>
    public bool OffersPlaceOnHold =>
        CurrentCase is { } header
        && CaseLifecycleRules.CanBeHeld(header.Workflow.State)
        && !ColleagueHoldsLease(header);

    /// <summary>
    /// Close case, as the Case page offers an adverse close: Core allows closure
    /// and no colleague holds the edit lease.
    /// </summary>
    public bool OffersCloseCase =>
        CurrentCase is { } header
        && ClosureIsAllowed(header.Workflow)
        && !ColleagueHoldsLease(header);

    private bool ColleagueHoldsLease(CaseHeader header) =>
        header.ActiveEditLease is { Holder: { } holder }
        && !string.Equals(holder, ActorSubjectId, StringComparison.OrdinalIgnoreCase);

    private string? ActorSubjectId =>
        TryGetActor(out var actor) ? actor.SubjectId : null;

    private static bool ClosureIsAllowed(CaseWorkflowRecord workflow)
    {
        try
        {
            CaseLifecycleRules.RequireClosureIsAllowed(workflow.State);
            return workflow.Archive is null;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public Task<IActionResult> OnPostOpenTriageAsync(
        Guid id,
        string? vehicleRegistration,
        string operationKey,
        CancellationToken cancellationToken) =>
        RunOfferedActionAsync(
            id,
            async (actor, receipt) =>
            {
                await intakeActions.OpenTriageAsync(actor, receipt, vehicleRegistration, operationKey, cancellationToken);
                return "The Triage was opened.";
            },
            cancellationToken);

    public Task<IActionResult> OnPostRegisterImagesAsync(
        Guid id,
        string? vehicleRegistration,
        string reason,
        string operationKey,
        CancellationToken cancellationToken) =>
        RunOfferedActionAsync(
            id,
            async (actor, receipt) =>
            {
                var record = await intakeActions.RegisterImagesAsync(
                    actor, receipt, vehicleRegistration, reason, operationKey, cancellationToken);
                return OperatorLabels.RegisterImages.Registered(record.ImageIntakeReference);
            },
            cancellationToken);

    // The offered next action runs against the message's own receipt. A
    // refusal reloads the record with the reason, as a correction does.
    private async Task<IActionResult> RunOfferedActionAsync(
        Guid id,
        Func<ActionActor, IntakeReceipt, Task<string>> run,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (!TryParseListContext(out _))
        {
            return NotFound();
        }

        try
        {
            var receipt = await GetExactAssociationAsync(actor, id, cancellationToken);
            if (receipt is null)
            {
                return NotFound();
            }
            ClassificationNotice = await run(actor, receipt);
            return LandOn(id);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException
            || IntakeExceptionPolicy.IsRecoverable(exception))
        {
            ModelState.AddModelError(
                string.Empty,
                exception is ArgumentException argument
                    ? argument.Message
                    : OperatorLabels.ActionNotApplied);
            return await ReloadAsync(actor, id, cancellationToken);
        }
    }

    private async Task<IActionResult> ReloadAsync(
        ActionActor actor,
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryParseListContext(out var listFolder))
        {
            return NotFound();
        }
        // A refused write redraws the record page, wherever it was posted from:
        // the refusal shows on the record, where the operator reads it.
        InDialog = false;
        ListFolder = listFolder;
        RetainedMailDetail? detail;
        try
        {
            detail = await getRetainedMail.ExecuteAsync(actor, id, SearchTerm, cancellationToken);
        }
        catch (ArgumentException)
        {
            return NotFound();
        }
        if (detail is null)
        {
            return NotFound();
        }
        Detail = detail;
        OutsideListScope = IsOutsideListScope(detail, listFolder);
        await LoadAssociationSafelyAsync(actor, cancellationToken);
        await LoadAttachmentOutcomesAsync(actor, cancellationToken);
        await LoadAiJobContextAsync(cancellationToken);
        // A refused send read the message's latest operation earlier in this
        // request, and it has written none since.
        if (!retainedOperationRead)
        {
            await LoadRetainedOperationAsync(actor, cancellationToken);
        }
        await LoadCorrespondenceContextAsync(actor, initializeForm: false, cancellationToken);
        return Page();
    }

    private async Task<IActionResult> SendCorrespondenceAsync(
        Guid id,
        StaffMailComposeMode mode,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
            return Forbid();
        if (!TryParseListContext(out _))
            return NotFound();
        if (!StaffMailAvailable)
            return NotFound();

        CorrespondenceMode = ModeCode(mode);
        try
        {
            StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }

        RetainedMailDetail? detail;
        try
        {
            detail = await getRetainedMail.ExecuteAsync(actor, id, SearchTerm, cancellationToken);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (ArgumentException)
        {
            return NotFound();
        }
        if (detail is null)
            return NotFound();
        Detail = detail;

        // The durable send boundary owns idempotency: a same-key replay
        // returns its operation, and a fresh key is a further reply.
        await LoadRetainedOperationAsync(actor, cancellationToken);
        retainedOperationRead = true;

        // Every refusal reloads the context, which lists the options; the
        // send itself never renders them.
        if (!await LoadCorrespondenceContextAsync(
                actor, initializeForm: false, cancellationToken, listAttachments: false))
        {
            ModelState.AddModelError(string.Empty, "Correspondence is unavailable for this message.");
            return await ReloadAsync(actor, id, cancellationToken);
        }

        if (CorrespondenceCase is null)
        {
            ModelState.AddModelError(
                nameof(CorrespondenceCaseReference),
                ChooseOneCaseMessage);
        }
        else if (IsFixedCorrespondenceCase(CorrespondenceCase)
            && (ExpectedCorrespondenceCaseVersion < 0
                || CorrespondenceCase.Workflow.Version != ExpectedCorrespondenceCaseVersion))
        {
            ModelState.AddModelError(
                string.Empty,
                "The Case changed after this correspondence was selected. Select it again before sending.");
        }
        if (string.IsNullOrWhiteSpace(CorrespondenceSubject))
            ModelState.AddModelError(nameof(CorrespondenceSubject), "A subject is required.");
        if (string.IsNullOrWhiteSpace(CorrespondenceBody))
            ModelState.AddModelError(nameof(CorrespondenceBody), "A message is required.");
        if (!IsRetainedOperationKey(CorrespondenceOperationKey, detail.Summary.Id))
        {
            ModelState.AddModelError(
                nameof(CorrespondenceOperationKey),
                "The send operation has expired. Reload the message and try again.");
        }

        var (to, cc) = mode switch
        {
            StaffMailComposeMode.Reply => ReplyRecipients(detail),
            StaffMailComposeMode.ReplyAll => ReplyAllRecipients(detail, CorrespondenceMailbox!.Address),
            StaffMailComposeMode.Forward => (
                ParseRecipients(CorrespondenceTo),
                ParseRecipients(CorrespondenceCc)),
            _ => ([], [])
        };
        if (to.Length == 0)
            ModelState.AddModelError(nameof(CorrespondenceTo), "At least one recipient is required.");
        if (to.Concat(cc).Any(recipient => !IsMailboxAddress(recipient.Address)))
            ModelState.AddModelError(nameof(CorrespondenceTo), "Enter valid recipient addresses.");

        // The Case's files are read only when some are selected, and that one
        // read also serves the options a refusal redraws.
        IReadOnlyList<StaffMailAttachment> attachments = [];
        if (CorrespondenceCase is not null && SelectedAttachments.Count > 0)
        {
            var selection = await attachmentResolver.SelectCaseAsync(
                actor, CorrespondenceCase.Summary.CaseId, SelectedAttachments,
                cancellationToken);
            AvailableAttachments = selection.Options;
            attachmentOptionsRead = true;
            attachments = selection.Attachments;
            if (selection.SelectionError is { } selectionError)
            {
                ModelState.AddModelError(nameof(SelectedAttachments), selectionError);
            }
        }

        if (!ModelState.IsValid)
            return await ReloadAsync(actor, id, cancellationToken);

        var original = new StaffMailOriginalMessage(
            detail.Summary.Id,
            detail.Summary.MailboxId,
            detail.ImmutableMessageId,
            detail.InternetMessageId,
            detail.ConversationId);
        try
        {
            var operation = await staffMailSend.SendAsync(
                new(
                    actor,
                    CorrespondenceMailbox!.Id,
                    CorrespondenceMailbox.Generation,
                    StaffMailPurpose.GeneralCorrespondence,
                    CorrespondenceCase!.Summary.CaseId,
                    CorrespondenceCase.Workflow.Version,
                    mode,
                    original,
                    to,
                    cc,
                    CorrespondenceSubject!.Trim(),
                    CorrespondenceBody!.Trim(),
                    attachments,
                    CorrespondenceOperationKey!.Trim(),
                    MarksQueryDone: MarkQueryDone && OffersMarkQueryDone,
                    AiJobId: AiJobId is { } aiJobId && aiJobId != Guid.Empty ? aiJobId : null),
                cancellationToken);
            CorrespondenceOperation = operation;
            if (operation.State == StaffMailState.Sent)
                CorrespondenceNotice = "Correspondence sent.";
            else if (operation.State == StaffMailState.Failed)
                StaffMailSendLog.Refused(logger, operation.Id, CorrespondenceMailbox!.Id, operation.FailureCode);
            if (operation.AiJobRefusal is { } refusal)
                AiJobNotCompletedNotice = OperatorLabels.QueryResponseJobs.NotCompleted(refusal);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (ArgumentException exception)
        {
            // The send may have written its operation before it refused.
            retainedOperationRead = false;
            ModelState.AddModelError(string.Empty, exception.Message);
            return await ReloadAsync(actor, id, cancellationToken);
        }

        return LandOn(id, ModeCode(mode), CorrespondenceOperation?.Id);
    }

    /// <summary>
    /// The send-ready approved mailbox that holds this message, which Reply
    /// and Forward send as; none where the message or its mailbox cannot send.
    /// </summary>
    private Task<ApprovedMailbox?> ResolveCorrespondenceMailboxAsync(CancellationToken cancellationToken) =>
        CorrespondenceSenderAsync(Detail, approvedMailboxes, cancellationToken);

    /// <summary>
    /// The send-ready approved mailbox that holds <paramref name="detail"/>,
    /// which Reply and Forward send as, for staff and for the Automation
    /// Actor's <c>pegasus_mail_send</c> alike; none where it cannot send.
    /// Only that one mailbox is read.
    /// </summary>
    internal static async Task<ApprovedMailbox?> CorrespondenceSenderAsync(
        RetainedMailDetail detail,
        IApprovedMailboxStore mailboxes,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(detail.ImmutableMessageId)
            || detail.Summary.Id == Guid.Empty
            || detail.Summary.MailboxId == Guid.Empty)
        {
            return null;
        }
        return await mailboxes.GetSenderAsync(detail.Summary.MailboxId, cancellationToken);
    }

    private async Task<bool> LoadCorrespondenceContextAsync(
        ActionActor actor,
        bool initializeForm,
        CancellationToken cancellationToken,
        bool listAttachments = true)
    {
        // A refused send reloads this context in the same request, and the
        // mailbox it resolved stands.
        if (!correspondenceMailboxResolved)
        {
            CorrespondenceMailbox = await ResolveCorrespondenceMailboxAsync(cancellationToken);
            correspondenceMailboxResolved = true;
        }
        if (CorrespondenceMailbox is null)
            return false;

        var mode = StaffMailComposeMode.New;
        if (CorrespondenceMode is not null
            && !TryComposeMode(CorrespondenceMode, out mode))
        {
            return false;
        }
        if (mode is StaffMailComposeMode.Reply or StaffMailComposeMode.ReplyAll
            && ReplyRecipients(Detail).To.Length == 0)
        {
            return false;
        }

        // The message's Case is the default, unless it is a Triage Case: the
        // composer resolves an instruction Case by its Case/PO, and a Triage's
        // t. reference never resolves, so staff choose the Case themselves.
        if (string.IsNullOrWhiteSpace(CorrespondenceCaseReference)
            && !Detail.Summary.IsTriageCase)
        {
            CorrespondenceCaseReference = Detail.Summary.CaseReference;
            // The message's own Case is not a reference staff typed, so a
            // send against it holds to the version the page showed.
            if (!initializeForm)
            {
                FixedCorrespondenceCaseReference = Detail.Summary.CaseReference;
            }
        }
        // Only the composer and the posts it makes read the Case and its
        // attachments; a plain view of the message renders neither. A refused
        // send reloads this context in the same request, and the Case it
        // resolved and the options it read stand.
        if (CorrespondenceMode is not null && !correspondenceCaseResolved)
        {
            CorrespondenceCase = await ResolveCaseAsync(
                actor, CorrespondenceCaseReference, cancellationToken);
            correspondenceCaseResolved = true;
            MessageHoldsOpenQuery = await MessageHoldsOpenQueryAsync(cancellationToken);
        }
        if (CorrespondenceCase is not null)
        {
            CorrespondenceCaseReference = CorrespondenceCase.Summary.Reference;
            if (initializeForm)
            {
                FixedCorrespondenceCaseReference = CorrespondenceCase.Summary.Reference;
                ExpectedCorrespondenceCaseVersion = CorrespondenceCase.Workflow.Version;
            }
            if (listAttachments && !attachmentOptionsRead)
            {
                AvailableAttachments = await attachmentResolver.ListCaseAsync(
                    actor, CorrespondenceCase.Summary.CaseId, cancellationToken);
            }
        }

        if (CorrespondenceMode is not null)
        {
            var recipients = mode switch
            {
                StaffMailComposeMode.Reply => ReplyRecipients(Detail),
                StaffMailComposeMode.ReplyAll => ReplyAllRecipients(Detail, CorrespondenceMailbox.Address),
                _ => ([], [])
            };
            if (mode is StaffMailComposeMode.Reply or StaffMailComposeMode.ReplyAll)
            {
                CorrespondenceTo = string.Join("; ", recipients.Item1.Select(item => item.Address));
                CorrespondenceCc = string.Join("; ", recipients.Item2.Select(item => item.Address));
            }
            if (initializeForm)
            {
                CorrespondenceOperationKey = NewRetainedOperationKey(Detail.Summary.Id);
                CorrespondenceSubject = SubjectFor(mode, Detail.Summary.Subject);
            }
            await ApplyAiDraftAsync(mode, prefill: initializeForm, cancellationToken);
        }
        return true;
    }

    /// <summary>
    /// A composer opened on an AI draft (FRD-27, FRD-21): the named job must
    /// still be the Draft ready reply to this message on the composer's Case.
    /// When the form is first drawn its text becomes Message and the tick
    /// starts ticked, as from the Case's Resolve query Next action; a reload
    /// after a refused post keeps what staff typed and only redraws the AI
    /// line. Any other job prefills nothing and is forgotten, so the send
    /// completes nothing.
    /// </summary>
    private async Task ApplyAiDraftAsync(
        StaffMailComposeMode mode, bool prefill, CancellationToken cancellationToken)
    {
        // A refused send redraws the same context in this request, and the
        // draft it settled on stands.
        if (aiDraftApplied)
        {
            return;
        }
        aiDraftApplied = true;
        if (AiJobId is not { } jobId || jobId == Guid.Empty)
        {
            AiJobId = null;
            return;
        }
        var job = mode is StaffMailComposeMode.Reply or StaffMailComposeMode.ReplyAll
            && CorrespondenceCase is { } correspondenceCase
                ? await aiJobStore.GetAsync(jobId, cancellationToken)
                : null;
        if (job is null
            || !AiJobPolicy.IsDraftReplyFor(job, Detail.Summary.Id, CorrespondenceCase!.Summary.CaseId, clock.GetUtcNow()))
        {
            AiJobId = null;
            return;
        }
        AiDraft = job;
        if (prefill)
        {
            CorrespondenceBody = job.ResultText;
            MarkQueryDone = OffersMarkQueryDone;
        }
    }

    private async Task<bool> MessageHoldsOpenQueryAsync(CancellationToken cancellationToken)
    {
        if (CorrespondenceCase is not { Workflow.HasOpenQuery: true }
            || CorrespondenceMode is not ("reply" or "reply-all"))
        {
            return false;
        }

        var queries = HttpContext.RequestServices.GetRequiredService<ICaseQueryStore>();
        var listed = await queries.ListCaseQueriesAsync(CorrespondenceCase.Summary.CaseId, cancellationToken);
        return listed.Any(item => item.IsOpen && item.Message?.RetainedMessageId == Detail.Summary.Id);
    }

    private Task<CaseHeader?> ResolveCaseAsync(
        ActionActor actor,
        string? reference,
        CancellationToken cancellationToken)
    {
        // The Case this message is linked to was read for CurrentCase moments
        // ago; naming it again needs no second search and header read.
        if (CurrentCase is { } linked
            && TryNormalizeCaseReference(reference, out var value)
            && string.Equals(linked.Summary.Reference, value, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<CaseHeader?>(linked);
        }

        return ResolveCaseByReferenceAsync(actor, reference, cancellationToken);
    }

    /// <summary>The Case a typed Case / PO reference names exactly; null when it names none.</summary>
    private async Task<CaseHeader?> ResolveCaseByReferenceAsync(
        ActionActor actor,
        string? reference,
        CancellationToken cancellationToken)
    {
        var match = await FindCaseByReferenceAsync(actor, reference, includeTriage: false, cancellationToken);
        return match is null
            ? null
            : await getCaseHeader.ExecuteAsync(new(match.CaseId, actor), cancellationToken);
    }

    /// <summary>
    /// The Case, and with <paramref name="includeTriage"/> the Triage Case, a typed
    /// Case / PO reference names exactly; null when it names none. Nothing is
    /// claimed or written.
    /// </summary>
    private async Task<CaseSearchItem?> FindCaseByReferenceAsync(
        ActionActor actor,
        string? reference,
        bool includeTriage,
        CancellationToken cancellationToken)
    {
        if (!TryNormalizeCaseReference(reference, out var value))
        {
            return null;
        }

        var matches = await searchCases.ExecuteAsync(
            new(actor, new CaseSearchFilters(CaseReference: value, IncludeTriage: includeTriage), PageSize: 10),
            cancellationToken);
        return matches.Items.SingleOrDefault(item =>
            string.Equals(item.Reference, value, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryNormalizeCaseReference(string? value, out string? normalized)
    {
        normalized = value?.Trim();
        return !string.IsNullOrWhiteSpace(normalized) && normalized.Length <= 100;
    }

    private async Task<IReadOnlyList<CaseSearchItem>> SearchCasesAsync(
        ActionActor actor,
        string? query,
        CancellationToken cancellationToken)
    {
        if (!TryNormalizeCaseQueryValue(query, out var value))
        {
            return [];
        }

        return (await searchCases.ExecuteAsync(
            new(actor, new CaseSearchFilters(Query: value), PageSize: 10), cancellationToken)).Items;
    }

    private static bool TryNormalizeCaseQueryValue(string? value, out string? normalized)
    {
        normalized = value?.Trim();
        return !string.IsNullOrWhiteSpace(normalized) && normalized.Length <= 300;
    }

    internal static (StaffMailRecipient[] To, StaffMailRecipient[] Cc) ReplyRecipients(
        RetainedMailDetail detail) =>
        (ParseRecipients(detail.ReplyToAddresses), []);

    internal static (StaffMailRecipient[] To, StaffMailRecipient[] Cc) ReplyAllRecipients(
        RetainedMailDetail detail,
        string ownAddress)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ownAddress };
        var to = new List<StaffMailRecipient>();
        var cc = new List<StaffMailRecipient>();
        AddUnique(to, ParseRecipients(detail.ReplyToAddresses), seen);
        AddUnique(to, detail.ToAddresses.Select(AddressRecipient), seen);
        AddUnique(cc, detail.CcAddresses.Select(AddressRecipient), seen);
        return ([.. to], [.. cc]);
    }

    private static void AddUnique(
        List<StaffMailRecipient> target,
        IEnumerable<StaffMailRecipient> candidates,
        HashSet<string> seen)
    {
        foreach (var candidate in candidates)
        {
            if (seen.Add(candidate.Address))
                target.Add(candidate);
        }
    }

    private static StaffMailRecipient AddressRecipient(string address) =>
        new(address.Trim(), DisplayName: null);

    private static StaffMailRecipient[] ParseRecipients(string? value) =>
        (value ?? string.Empty)
            .Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(AddressRecipient)
            .DistinctBy(item => item.Address, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static StaffMailRecipient[] ParseRecipients(IReadOnlyList<string>? values) =>
        (values ?? [])
            .SelectMany(ParseRecipients)
            .Where(item => IsMailboxAddress(item.Address))
            .DistinctBy(item => item.Address, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static bool IsMailboxAddress(string value) =>
        System.Net.Mail.MailAddress.TryCreate(value, out var parsed)
        && string.Equals(parsed.Address, value, StringComparison.OrdinalIgnoreCase);

    private static string SubjectFor(StaffMailComposeMode mode, string? subject)
    {
        var value = subject?.Trim() ?? string.Empty;
        return mode switch
        {
            StaffMailComposeMode.Reply or StaffMailComposeMode.ReplyAll
                when value.StartsWith("Re:", StringComparison.OrdinalIgnoreCase) => value,
            StaffMailComposeMode.Reply or StaffMailComposeMode.ReplyAll => $"Re: {value}".TrimEnd(),
            StaffMailComposeMode.Forward when value.StartsWith("Fwd:", StringComparison.OrdinalIgnoreCase) => value,
            StaffMailComposeMode.Forward => $"Fwd: {value}".TrimEnd(),
            _ => value
        };
    }

    private static bool TryComposeMode(string value, out StaffMailComposeMode mode)
    {
        mode = value switch
        {
            "reply" => StaffMailComposeMode.Reply,
            "reply-all" => StaffMailComposeMode.ReplyAll,
            "forward" => StaffMailComposeMode.Forward,
            _ => StaffMailComposeMode.New
        };
        return mode != StaffMailComposeMode.New;
    }

    private static string ModeCode(StaffMailComposeMode mode) => mode switch
    {
        StaffMailComposeMode.Reply => "reply",
        StaffMailComposeMode.ReplyAll => "reply-all",
        StaffMailComposeMode.Forward => "forward",
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };

    private async Task LoadRetainedOperationAsync(
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        CorrespondenceOperation = await staffMailSend.GetLatestForOriginalAsync(
            actor,
            Detail.Summary.Id,
            cancellationToken);
        CorrespondenceOperationId = CorrespondenceOperation?.Id;
    }

    private static string NewRetainedOperationKey(Guid retainedMessageId) =>
        $"retained:{retainedMessageId:N}:{Guid.NewGuid():N}";

    private static bool IsRetainedOperationKey(string? value, Guid retainedMessageId)
    {
        var prefix = $"retained:{retainedMessageId:N}:";
        return value is not null
            && value.StartsWith(prefix, StringComparison.Ordinal)
            && Guid.TryParseExact(value[prefix.Length..], "N", out _);
    }

    private async Task LoadAiJobContextAsync(CancellationToken cancellationToken)
    {
        IsQueryResponseSource = AiJobPolicy.IsQueryResponseSource(Detail);
        if (IsQueryResponseSource)
        {
            QueryResponseCondition = await QueryResponseAvailability.ConditionAsync(
                CurrentCase?.Workflow, sendToAiControl, cancellationToken);
        }

        // The foot needs the Case's jobs for the live Query response job
        // whenever the message is a query source.
        if (IsQueryResponseSource && CurrentCase is { } currentCase)
        {
            AiJobs = await aiJobQueries.ListOpenForSubjectAsync(
                currentCase.Summary.CaseId,
                cancellationToken);
            if (IsQueryResponseSource)
            {
                QueryResponseJob = QueryDraft.For(AiJobs, Detail.Summary.Id, clock.GetUtcNow());
            }
        }
    }

    private async Task LoadAssociationSafelyAsync(
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        try
        {
            await LoadAssociationAsync(actor, cancellationToken);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
        }
    }

    private async Task LoadAssociationAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        if (Detail.Summary.IntakeReceiptId is not { } receiptId)
        {
            return;
        }

        AssociationReceipt = await getIntake.ExecuteAsync(new(receiptId, actor), cancellationToken);
        if (AssociationReceipt is null)
        {
            return;
        }
        Offers = AssociationReceipt.CurrentCaseId is null
            ? await offeredActions.ExecuteAsync(actor, AssociationReceipt, cancellationToken)
            : IntakeOfferedActions.None;

        if (AssociationReceipt.CurrentCaseId is { } currentCaseId)
        {
            (CurrentDestination, CurrentCase) = await GetCurrentDestinationAsync(
                currentCaseId, actor, cancellationToken);
        }
    }

    /// <summary>
    /// The Case a receipt is linked to, in the shape the link authority takes.
    /// The destination query answers only a receipt with no Case, so the linked
    /// Case is read from its own owner: a Case through its workflow, or a
    /// Triage Case — which has none — through its Triage record.
    /// </summary>
    private async Task<(IntakeAssociationDestination? Destination, CaseHeader? Case)> GetCurrentDestinationAsync(
        Guid caseId,
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        var linkedCase = await getCaseHeader.ExecuteAsync(new(caseId, actor), cancellationToken);
        if (linkedCase is not null)
        {
            return (
                new IntakeAssociationDestination(
                    linkedCase.Summary.CaseId,
                    linkedCase.Summary.Reference,
                    linkedCase.Summary.Registration,
                    linkedCase.Summary.Claimant,
                    linkedCase.Workflow.State,
                    linkedCase.Workflow.Version),
                linkedCase);
        }

        var triage = await getTriage.ExecuteAsync(new(caseId, actor), cancellationToken);
        if (triage is null)
        {
            return (null, null);
        }

        return (
            new IntakeAssociationDestination(
                triage.Record.CaseId,
                triage.Record.Reference,
                triage.Record.NormalizedVehicleRegistration,
                null,
                null,
                triage.Record.Version)
            {
                TriageState = triage.Record.State
            },
            null);
    }

    /// <summary>
    /// The receipt the message became, as it stands: only the message's receipt
    /// identifier is read, then the receipt itself.
    /// </summary>
    private async Task<IntakeReceipt?> GetExactAssociationAsync(
        ActionActor actor,
        Guid messageId,
        CancellationToken cancellationToken) =>
        await getRetainedMail.FindReceiptIdAsync(actor, messageId, cancellationToken) is { } receiptId
            ? await getIntake.ExecuteAsync(new(receiptId, actor), cancellationToken)
            : null;

    /// <summary>
    /// Where a write lands. The dialog's writes return to the Inbox with this
    /// message open, so the list behind it shows the change; the page's return to
    /// the page.
    /// </summary>
    private RedirectToPageResult LandOn(Guid id, string? compose = null, Guid? mailOperationId = null) =>
        InDialog ? LandOnInbox(id, compose, mailOperationId) : LandOnRecord(id, compose, mailOperationId);

    private RedirectToPageResult LandOnRecord(Guid id, string? compose = null, Guid? mailOperationId = null) =>
        RedirectToPage(new
        {
            id,
            mailbox = MailboxFilter,
            folder = FolderFilter,
            pageNumber = PageRouteValue,
            search = SearchTerm,
            queue = QueueFilter,
            sort = OldestFirst ? "oldest" : null,
            compose,
            mailOperationId
        });

    /// <summary>
    /// The Inbox as the operator left it, with <paramref name="open"/> marked
    /// and opened by script; with none, the list alone.
    /// </summary>
    private RedirectToPageResult LandOnInbox(Guid? open, string? compose, Guid? mailOperationId) =>
        RedirectToPage("/Mail/Index", new
        {
            mailbox = MailboxFilter,
            folder = FolderFilter,
            search = SearchTerm,
            queue = QueueFilter,
            sort = OldestFirst ? "oldest" : null,
            pageNumber = PageRouteValue,
            selected = open,
            open,
            compose,
            mailOperationId
        });

    /// <summary>The Case or Triage Case a typed Case / PO reference names exactly; null when it names none.</summary>
    private async Task<Guid?> ResolveCaseIdAsync(
        ActionActor actor,
        string? reference,
        CancellationToken cancellationToken) =>
        (await FindCaseByReferenceAsync(actor, reference, includeTriage: true, cancellationToken))?.CaseId;

    /// <summary>One link step that did not complete: what the operator reads, and whether its confirmation is kept.</summary>
    private sealed record AssociationFailure(string Message, bool KeepConfirmation);

    /// <summary>
    /// One association step (v41 I13): claims the destination's edit authority
    /// through <see cref="OneCommandEditAuthority.ClaimAsync"/> (a Triage Case's Triage
    /// edit scope, a Case's edit lease), runs <paramref name="command"/> under
    /// it, and gives the authority back when the command did not complete. The
    /// command consumes the authority itself when it commits. Null on success.
    /// </summary>
    private async Task<AssociationFailure?> TryAssociateAsync(
        ActionActor actor,
        IntakeAssociationDestination destination,
        string leaseOperationKey,
        Func<OneCommandEditClaim, Task> command,
        CancellationToken cancellationToken)
    {
        var (claim, failure) = await ClaimAuthorityAsync(actor, destination, leaseOperationKey, cancellationToken);
        return claim is null
            ? failure
            : await RunClaimedAsync(actor, claim, command, cancellationToken);
    }

    /// <summary>The destination's edit authority, or why it was refused (nothing is written either way).</summary>
    private async Task<(OneCommandEditClaim? Claim, AssociationFailure? Failure)> ClaimAuthorityAsync(
        ActionActor actor,
        IntakeAssociationDestination destination,
        string leaseOperationKey,
        CancellationToken cancellationToken)
    {
        try
        {
            var claim = await editAuthority.ClaimAsync(
                destination.CaseId,
                destination.IsTriageCase,
                destination.Version,
                actor,
                leaseOperationKey,
                cancellationToken);
            return (claim, null);
        }
        catch (StaffAuthorizationException)
        {
            throw;
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            return (null, new(AssociationPreparationFailureMessage(exception), false));
        }
    }

    /// <summary>Runs <paramref name="command"/> under a claimed authority and gives it back when the command did not complete.</summary>
    private async Task<AssociationFailure?> RunClaimedAsync(
        ActionActor actor,
        OneCommandEditClaim claim,
        Func<OneCommandEditClaim, Task> command,
        CancellationToken cancellationToken)
    {
        try
        {
            await command(claim);
            return null;
        }
        catch (StaffAuthorizationException)
        {
            throw;
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            // A failure that leaves the result unknown releases nothing; one that
            // failed for good gives the authority back, and when that cannot be
            // done the form keeps its confirmation so the same confirmation retries.
            var releasePending = exception is not IntakeOperationConflictException
                && IsDefinitiveAssociationFailure(exception)
                && await ReleaseClaimAsync(actor, claim);
            return new(
                releasePending ? AssociationReleaseFailureMessage : AssociationFailureMessage(exception),
                releasePending || !IsDefinitiveAssociationFailure(exception));
        }
    }

    /// <summary>Gives a claimed authority back; true when it could not be (it then lapses on its own).</summary>
    private async Task<bool> ReleaseClaimAsync(ActionActor actor, OneCommandEditClaim claim)
    {
        try
        {
            await editAuthority.ReleaseAsync(
                claim,
                actor,
                $"mail-association-release:{Guid.NewGuid():N}",
                CancellationToken.None);
        }
        catch (Exception releaseException) when (IsDefinitiveAssociationFailure(releaseException))
        {
            return false;
        }
        catch (Exception releaseException) when (IntakeExceptionPolicy.IsRecoverable(releaseException))
        {
            return true;
        }

        return false;
    }

    /// <summary>A step that did not complete: its refusal on the record, and the confirmation kept where it must be retried.</summary>
    private Task<IActionResult> RefuseAssociationAsync(
        ActionActor actor,
        Guid id,
        AssociationFailure failure,
        string leaseOperationKey,
        string operationKey,
        CancellationToken cancellationToken)
    {
        ModelState.AddModelError(string.Empty, failure.Message);
        if (failure.KeepConfirmation)
        {
            RetainedLeaseOperationKey = leaseOperationKey;
            RetainedOperationKey = operationKey;
        }
        return ReloadAsync(actor, id, cancellationToken);
    }

    private static bool IsDefinitiveAssociationFailure(Exception exception) => exception is
        ArgumentException
        or InvalidOperationException
        or InvalidDataException
        or KeyNotFoundException
        or IntakeOperationConflictException
        or IntakeVersionConflictException
        or IntakeAssociationConflictException;

    private static string AssociationFailureMessage(Exception exception) => exception switch
    {
        IntakeOperationConflictException =>
            "This confirmation identity was already used with different details. Reload and review the action again.",
        _ when IsDefinitiveAssociationFailure(exception) =>
            "The message or case changed. Reload it, review the current target, and try again.",
        _ => "The association result could not be confirmed. Retry this same confirmation."
    };

    /// <summary>The refusal for a Case / PO that names no Case: the composer's existing sentence.</summary>
    private const string ChooseOneCaseMessage = "Choose one Case by its Case / PO reference.";

    private const string AssociationReleaseFailureMessage =
        "The association was not applied, but edit authority could not be released. Retry this same confirmation.";

    private static string AssociationPreparationFailureMessage(Exception exception) => exception switch
    {
        CaseEditLeaseConflictException or EditScopeConflictException =>
            "This case is currently being edited. Reload and try again later.",
        _ => "The message or case changed. Reload it, review the current target, and try again."
    };

    private static void RequireAssociationConfirmation(
        string operationKey,
        string leaseOperationKey,
        string reason)
    {
        if (!Guid.TryParseExact(operationKey, "D", out _))
        {
            throw new ArgumentException("The association confirmation has expired.", nameof(operationKey));
        }
        if (!Guid.TryParseExact(leaseOperationKey, "N", out _))
        {
            throw new ArgumentException("The case edit confirmation has expired.", nameof(leaseOperationKey));
        }
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500)
        {
            throw new ArgumentException("A reason of no more than 500 characters is required.", nameof(reason));
        }
    }

    private bool IsOutsideListScope(RetainedMailDetail detail, MailFolderScope listFolder) =>
        ListDismissed != (detail.Summary.DismissedAtUtc is not null)
            || detail.Folder != listFolder
            || (MailboxFilter is { } mailbox
                && !string.Equals(mailbox, detail.Summary.MailboxId.ToString("D"), StringComparison.OrdinalIgnoreCase))
            || (SearchTerm is not null && detail.Summary.Matches.Count == 0)
            || !MatchesQueue(detail.Summary);

    private bool TryParseListContext(out MailFolderScope listFolder)
    {
        if (!IndexModel.TryParseFolder(FolderFilter, out listFolder, out var dismissed)
            || !ParseQueueFilter()
            || !IndexModel.TryParseSort(SortOrder, out var oldestFirst))
        {
            return false;
        }

        ListDismissed = dismissed;
        OldestFirst = oldestFirst;
        return true;
    }

    private bool ParseQueueFilter()
    {
        if (!IndexModel.TryParseQueue(QueueFilter, out var normalized, out var noCaseFound))
        {
            return false;
        }
        QueueFilter = normalized;
        NoCaseFoundFilter = noCaseFound;
        return true;
    }

    private bool MatchesQueue(RetainedMailSummary summary)
    {
        if (NoCaseFoundFilter)
        {
            // The same membership the store lists (ApplyWorkspaceFilter): no
            // Case, and not decided to be a Pre-Pegasus case, which leaves the
            // scope the moment it is set.
            return summary.IntakeReceiptId is not null
                && summary.CaseId is null
                && summary.Classification?.Is(MailCategory.PrePegasusCase) != true;
        }
        return true;
    }

    /// <summary>
    /// What a classification correction refuses before any message is touched:
    /// a category the picker does not offer, a New instruction with no case
    /// type, and no reason. The message record and the list's ticked rows
    /// refuse with the same words, under the same field names.
    /// </summary>
    internal static void RefuseInvalidCorrection(
        ModelStateDictionary modelState,
        string? classificationKey,
        string? workTypeValue,
        string? correctionReason,
        out MailCategory? category,
        out CaseType? workType)
    {
        if (!MailClassificationSelection.TryParseSelection(
                classificationKey, workTypeValue, out category, out workType))
        {
            modelState.AddModelError(nameof(ClassificationKey), MailClassificationSelection.UnknownSelectionMessage);
        }
        if (string.IsNullOrWhiteSpace(correctionReason))
        {
            modelState.AddModelError(nameof(CorrectionReason), OperatorLabels.Inbox.CorrectionReasonRequired);
        }
    }

    /// <summary>The record's tabs: Message, Attachments and Thread. The Case folded into Decision on Message (v41 I9).</summary>
    public string ActiveSection => Section switch
    {
        "attachments" => "attachments",
        "thread" => "thread",
        _ => "message"
    };

    public string? FolderRouteValue => IndexModel.ListFolderCode(ListFolder, ListDismissed);

    public int? PageRouteValue => PageNumber is > 1 ? PageNumber : null;

    public static string ClassificationLabel(MailClassificationOutcome? outcome) => outcome switch
    {
        MailClassificationOutcome.Classified => "Classified",
        MailClassificationOutcome.Unclassified => OperatorLabels.Inbox.Correspondence,
        _ => "Not yet processed"
    };

    public static string DecisionLabel(MailClassificationResult result) =>
        result is { IsClassified: true, Category: { } category }
            ? category == MailCategory.NewInstruction && result.CaseType is { } caseType
                ? MailClassificationSelection.TypeSelectOptions
                    .First(option => option.Value == MailClassificationSelection.Combined(category, caseType)).Label
                : DecisionLabel(category)
            : OperatorLabels.Inbox.Correspondence;

    public static string DecisionLabel(MailCategory category) =>
        OperatorLabels.MailClassification(category);

    /// <summary>
    /// The one label for a message's Case cell when it has no Case, so every
    /// place that draws the cell says the same word.
    /// </summary>
    public static string AssociationLabel(string? caseReference) =>
        caseReference ?? OperatorLabels.Inbox.NoCase;

    public static string OutcomeLabel(RetainedMailSummary summary) => summary switch
    {
        // A Triage Case is the message's Case, but the chip names what it
        // is: a Triage, not an instruction Case created.
        { IsTriageCase: true } => OperatorLabels.Inbox.Triage,
        { CaseId: not null } => "Case created",
        { AllocationState.Status: IntakeAllocationProjectionStatus.Pending } => "Creating case",
        { AllocationState.Status: IntakeAllocationProjectionStatus.FailedRecoverable
            or IntakeAllocationProjectionStatus.FailedBlocked } => "Case not created",
        // A Triage request shares the NeedsSorting decision with mail that
        // found no Case, because both are pre-case, but they are not the same thing
        // and this column must not call one the other: the operator reported
        // this defect from this screen, which labelled a Triage request as
        // though a case were coming. The word is the Triage destination's
        // own (OperatorLabels.Inbox.Triage), not a new one.
        { Classification.IsTriageRequest: true } => OperatorLabels.Inbox.Triage,
        // A Pre-Pegasus case has left No Case Found by decision, so the chip
        // names the category rather than the scope it is no longer in
        // (operator, 30 October 2026, R561).
        { Classification: { } decision } when decision.Is(MailCategory.PrePegasusCase)
            => OperatorLabels.MailClassification(MailCategory.PrePegasusCase),
        _ => OutcomeLabel(summary.ProcessingOutcome)
    };

    private static string OutcomeLabel(IntakeDecision? decision) => decision switch
    {
        IntakeDecision.CaseCreated => "Ready for case allocation",
        IntakeDecision.NeedsSorting => OperatorLabels.Inbox.NoCaseFound,
        IntakeDecision.OcrRequired => "Document text required",
        IntakeDecision.TechnicalFailure => "Technical failure",
        IntakeDecision.Unsupported => "Unsupported",
        IntakeDecision.ImageIntakeRegistered => "Vehicle images registered",
        _ => "Not yet processed"
    };
}
