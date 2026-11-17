using System.ComponentModel;
using System.Globalization;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Pegasus.Core;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
using Pegasus.Core.PhotoCapture;
using Pegasus.Core.Workflow;
using Labels = Pegasus.Web.Presentation.OperatorLabels.PhotoCapture;
using PhotoCaptureOptions = Pegasus.Web.PhotoCapture.PhotoCaptureOptions;
using PhotoLinkMail = Pegasus.Web.Pages.PhotoCapture.PhotoLinkMail;
using PhotoLinkPage = Pegasus.Web.Pages.PhotoCapture.DetailsModel;

namespace Pegasus.Web.Mcp;

/// <summary>One photo link as the Photo capture lists show it; its status is derived as it is read.</summary>
internal sealed record PhotoLinkSummary(
    [property: Description("The photo link (capture) identifier.")] Guid CaptureId,
    [property: Description("CaseLink for a link sent from a Case; StandingStaff, StandingForwarded or Website for a capture a standing or website link started.")] string Origin,
    Guid? CaseId,
    string? CaseReference,
    [property: Description("The registration as a plate shows it.")] string? Registration,
    string? CustomerName,
    [property: Description("A link sent from a Case: the Case's Principal; any other: its standing link's contact.")] string? From,
    [property: Description("Guided or Quick.")] string Mode,
    [property: Description("A quick link sent from a Case: AccidentManagement or Bodyshop.")] string? QuickLinkFor,
    [property: Description("The status as the link's page shows it, for example Sent, not opened, Something missing, Needs a phone call, Uploading or Received.")] string Status,
    [property: Description("The derived state: SentNotOpened, Opened, PhotosComingIn, SomethingMissing, NeedsCall, Complete, Expired or Cancelled.")] string State,
    [property: Description("The reminder due now, if any.")] string? ReminderDue,
    [property: Description("Whether the link still takes photos.")] bool IsLive,
    int Photographs,
    [property: Description("What the link is still missing.")] IReadOnlyList<string> Missing,
    int RemindersSent,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    [property: Description("The member of staff who sent the link, when one did.")] string? SentBy,
    Guid? StandingLinkId,
    [property: Description("The Vehicle images record the capture's photos became, if any.")] string? VehicleImagesReference);

internal sealed record PhotoLinkListToolResult(
    IReadOnlyList<PhotoLinkSummary> Items,
    [property: Description("How many links match, across every page.")] int TotalCount,
    [property: Description("The page returned; null for one Case's links.")] int? Page,
    int? Limit,
    bool HasNextPage,
    [property: Description("The direct tab: how many links each scope holds, before the scope and search filters.")] IReadOnlyDictionary<string, int>? CountsByScope,
    string CorrelationId);

internal sealed record PhotoLinkItemView(
    [property: Description("A photograph's number on the link's page; null for paperwork and video.")] int? Number,
    string Caption,
    [property: Description("Overview, Damage, NumberPlate, Vin, Dashboard, Scene, OtherVehicle, Paperwork or Video.")] string Kind,
    [property: Description("A damage photo's stage: Overview, Closer, Close-up or Extra.")] string? Stage,
    string FileName,
    string MediaType,
    DateTimeOffset? TakenAtUtc,
    DateTimeOffset ReceivedAtUtc,
    [property: Description("Whether intake has filed it; one filed on the Case is a Case document (pegasus_case_get, pegasus_document_download).")] bool Filed);

internal sealed record PhotoLinkHistoryLine(string Label, DateTimeOffset AtUtc, string? Detail);

internal sealed record PhotoLinkOffersView(bool Resend, bool LogCall, bool Extend, bool Cancel);

internal sealed record PhotoLinkDetailToolResult(
    PhotoLinkSummary Link,
    string? Mobile,
    string? Email,
    DateOnly? AccidentDate,
    bool LiveCameraOnly,
    bool OriginalsInBackground,
    DateTimeOffset? OpenedAtUtc,
    [property: Description("When the sender last pressed Send.")] DateTimeOffset? SentAtUtc,
    DateTimeOffset? CancelledAtUtc,
    DateTimeOffset? LastRemindedAtUtc,
    DateTimeOffset? CallLoggedAtUtc,
    [property: Description("A live link sent from a Case: the reminder due, else how many were sent.")] string? Chase,
    [property: Description("Whether a quick link's firm sent it on to their customer.")] bool SentOnToCustomer,
    [property: Description("What the sender typed on the link's form.")] PhotoCaptureDetails SenderDetails,
    [property: Description("The sender's note.")] string? Note,
    [property: Description("The photographs received, in story order.")] IReadOnlyList<PhotoLinkItemView> Photos,
    [property: Description("The paperwork and video received.")] IReadOnlyList<PhotoLinkItemView> Files,
    [property: Description("The link's key history, oldest first, naming who acted where a name is known.")] IReadOnlyList<PhotoLinkHistoryLine> History,
    [property: Description("The acts the link's page offers now: resend (pegasus_photo_link_send or resend_copy), log_call, extend and cancel.")] PhotoLinkOffersView Offers,
    string CorrelationId);

internal sealed record PhotoLinkActionToolResult(
    string Action,
    PhotoLinkSummary Link,
    [property: Description("issue and resend_copy: the link's address, returned once; null when replayed.")] string? LinkUrl,
    [property: Description("true when the operation key had already made this link or address: nothing more was made and no address is returned.")] bool Replayed,
    string OperationKey);

internal sealed record PhotoLinkSendToolResult(
    string Action,
    string Channel,
    PhotoLinkSummary Link,
    [property: Description("The link's address that was sent, returned once; null when replayed.")] string? LinkUrl,
    [property: Description("true when the operation key had already made this link: nothing more was made or sent.")] bool Replayed,
    [property: Description("email: the send operation.")] Guid? OperationId,
    [property: Description("email: Submitted once the provider accepted it, Sent once the Sent item is observed, Failed with failureCode, Unknown when the provider outcome is not known (never resend), or an in-flight state.")] string? State,
    [property: Description("Why a Failed or Unknown e-mail, or a text the provider did not accept, stopped.")] string? FailureCode,
    [property: Description("text: whether the provider accepted the text; accepted is not delivered.")] bool? TextAccepted,
    string OperationKey);

/// <summary>
/// The Automation Actor's guided photo capture links (FRD-28), with the
/// casework a member of staff does on the Photo capture pages (R1039, R1040):
/// the lists and a link's page under <c>automation.cases</c>, the link acts
/// that send nothing under <c>automation.cases</c>, and sending a link by
/// e-mail or text under <c>automation.send</c>. Each act is the Core command
/// the page calls, with no Case edit lease, as the pages take none; the
/// e-mail is <see cref="PhotoLinkMail"/>'s, the Send chaser's staff send. With
/// <c>Features:PhotoCapture</c> off the pages are absent and every tool
/// refuses before it reads anything.
/// </summary>
[McpServerToolType]
internal sealed class PhotoCaptureMcpTools(
    IPhotoCaptureQueries queries,
    IPhotoCaptureStore captures,
    IPhotoCaptureCaseReader caseReader,
    IssuePhotoLink issuePhotoLink,
    PhotoLinkActions actions,
    SendPhotoLinkText sendText,
    IStaffMailSend staffMailSend,
    IGetCaseHeader getCaseHeader,
    ResolveCaseMailSender senders,
    RenderEmailTemplate renderTemplate,
    ICaseWorkflowConfiguration workflowConfiguration,
    IConfiguration configuration,
    TimeProvider timeProvider,
    AutomationMcpAuditor auditor,
    ILogger<PhotoCaptureMcpTools> logger,
    IOptions<PhotoCaptureOptions>? options = null)
{
    private const string CaptureDescription = "The photo link (captureId from pegasus_photo_link_list).";
    private const string CustomerNameDescription = "A new link: the customer's name; omit for the Case's claimant.";
    private const string AccidentDateDescription = "A new link: the accident date, yyyy-MM-dd; omit for the Case's.";
    private const string ModeDescription = "A new link: Guided (a page per stage, for customers) or Quick (for an accident management firm's or bodyshop's staff); omit for the default Administration sets.";
    private const string QuickLinkForDescription = "A new Quick link: who it is for, AccidentManagement or Bodyshop.";
    private const string LiveCameraDescription = "A new link: true to take photos with the live camera only; omit for the default Administration sets.";
    private const string OriginalsDescription = "A new link: true to send full-size originals in the background; omit for the default Administration sets.";
    private const string DaysDescription = "A new link: its lifetime in days, one Administration offers; omit for the default.";

    /// <summary>The longest operation key a text takes: its outcome is recorded under the key and ":text-outcome".</summary>
    private const int MaximumTextOperationKeyLength = 100 - 13;

    private static readonly string[] ActionVerbs = ["issue", "resend_copy", "extend", "cancel", "log_call"];
    private static readonly string[] SendVerbs = ["send", "resend"];
    private static readonly string[] Channels = ["email", "text"];

    [McpServerTool(
        Name = "pegasus_photo_link_list",
        Title = "List photo links",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Lists photo links as the Photo capture page does. tab direct: the links sent from a Case, newest first, in one scope, with each scope's count. tab standing: the captures standing and website links started, newest first. With caseId it lists that Case's photo links instead, every one, whatever started it. Each link carries its status, any reminder due and what it is missing; read one with pegasus_photo_link_get.")]
    public async Task<PhotoLinkListToolResult> ListAsync(
        [Description("A Case: list its photo links instead of a tab; takes no other filter.")] Guid? caseId = null,
        [Description("direct (the default) or standing.")] string? tab = null,
        [Description("The direct tab's scope: awaiting (the default), due (needs chase), complete, closed or all.")] string? scope = null,
        [Description("Text the registration, customer, Case reference or contact contains.")] string? search = null,
        [Description("The standing tab: only the captures this standing link started.")] Guid? standingLinkId = null,
        [Description("The page, from 1; omit for the first.")] int? page = null,
        [Description(AutomationToolText.PageSize)] int? limit = null,
        CancellationToken cancellationToken = default)
    {
        return await auditor.CallAsync(
            "pegasus_photo_link_list",
            Resource(caseId, "photo_links"),
            async context =>
            {
                _ = RequireComposed();
                var now = timeProvider.GetUtcNow();
                if (caseId is { } wanted)
                {
                    if (tab is not null || scope is not null || search is not null
                        || standingLinkId is not null || page is not null || limit is not null)
                    {
                        throw new McpException("caseId lists one Case's photo links and takes no tab, scope, search, standingLinkId, page or limit.");
                    }

                    var id = AutomationMcpErrors.RequireId(wanted, "case identifier");
                    _ = await caseReader.GetAsync(id, cancellationToken)
                        ?? throw new McpException("The case was not found.");
                    // The Case's own read (its Files → Images), then each link
                    // as the lists read and derive it.
                    var stories = await queries.ListForCaseAsync(id, cancellationToken);
                    var links = new List<PhotoLinkSummary>(stories.Count);
                    foreach (var story in stories)
                    {
                        if (await queries.GetRowAsync(story.Capture.Id, cancellationToken) is { } found)
                        {
                            links.Add(Summary(found, now));
                        }
                    }

                    return new PhotoLinkListToolResult(
                        links, links.Count, null, null, false, null, context.TraceIdentifier);
                }

                var direct = ParseTab(tab);
                if (!direct && scope is not null)
                {
                    throw new McpException("scope applies to the direct tab.");
                }

                if (direct && standingLinkId is not null)
                {
                    throw new McpException("standingLinkId applies to the standing tab.");
                }

                var currentPage = page ?? 1;
                if (currentPage is < 1 or > 10_000)
                {
                    throw new McpException("page must be between 1 and 10000.");
                }

                var effectiveLimit = CursorPaging.NormalizeLimit(limit);
                var text = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
                if (text is { Length: > 100 })
                {
                    text = text[..100];
                }

                var listed = await queries.ListAsync(
                    new(direct, text, ParseScope(scope), direct ? null : standingLinkId, effectiveLimit, currentPage),
                    cancellationToken);
                return new PhotoLinkListToolResult(
                    listed.Rows.Select(row => Summary(row, now)).ToArray(),
                    listed.TotalCount,
                    currentPage,
                    effectiveLimit,
                    currentPage * effectiveLimit < listed.TotalCount,
                    direct
                        ? listed.CountsByScope.ToDictionary(
                            pair => Labels.ScopeKey(pair.Key), pair => pair.Value, StringComparer.Ordinal)
                        : null,
                    context.TraceIdentifier);
            },
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_photo_link_get",
        Title = "Get photo link",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("One photo link as its page shows it: its status, expiry and chase, what the sender typed and their note, the photographs, paperwork and video received (named, not their bytes), its key history and the acts the page offers now. A photo filed on the Case is downloaded as a Case document with pegasus_document_download.")]
    public async Task<PhotoLinkDetailToolResult> GetAsync(
        [Description(CaptureDescription)] Guid captureId,
        CancellationToken cancellationToken = default)
    {
        return await auditor.CallAsync(
            "pegasus_photo_link_get",
            Resource(captureId, "invalid"),
            async context =>
            {
                _ = RequireComposed();
                var id = AutomationMcpErrors.RequireId(captureId, "photo link identifier");
                var row = await queries.GetRowAsync(id, cancellationToken)
                    ?? throw new McpException("The photo link was not found.");
                var items = await captures.ListItemsAsync(id, cancellationToken);
                var acts = await queries.ListHistoryAsync(id, cancellationToken);
                var now = timeProvider.GetUtcNow();
                var derived = PhotoCaptureRowStatus.For(row, now);
                var capture = row.Capture;
                var ordered = PhotoLinkPage.ReceivedInStoryOrder(items, capture);
                var photographs = ordered.Where(item => PhotoCaptureStory.IsPhotographKind(item.Kind)).ToArray();
                var captions = Labels.Captions(photographs);
                var offers = PhotoLinkOffers.For(row, now);
                var caseLink = capture.Origin == PhotoCaptureOrigin.CaseLink;
                return new PhotoLinkDetailToolResult(
                    Summary(row, now),
                    capture.Mobile,
                    capture.Email,
                    capture.IncidentDate,
                    capture.LiveCameraOnly,
                    capture.OriginalsInBackground,
                    capture.OpenedAtUtc,
                    capture.SentAtUtc,
                    capture.RevokedAtUtc,
                    capture.LastRemindedAtUtc,
                    capture.CallLoggedAtUtc,
                    caseLink && derived.IsLive ? Labels.ChaseCue(derived.Due, capture.ReminderCount) : null,
                    row.SentOnToCustomer,
                    capture.Details,
                    capture.Note,
                    photographs.Select((item, index) => Item(item, index + 1, captions[index])).ToArray(),
                    ordered
                        .Where(item => !PhotoCaptureStory.IsPhotographKind(item.Kind))
                        .Select(item => Item(item, null, Labels.ItemLabel(item)))
                        .ToArray(),
                    PhotoLinkPage.KeyMilestones(row, acts, derived.State)
                        .Select(line => new PhotoLinkHistoryLine(line.Label, line.AtUtc, line.Detail))
                        .ToArray(),
                    new PhotoLinkOffersView(offers.Resend, offers.LogCall, offers.Extend, offers.Cancel),
                    context.TraceIdentifier);
            },
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_photo_link_action",
        Title = "Act on a photo link",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("One photo link act that sends nothing, as on the Photo capture pages, with no edit lease or expected version. issue makes a new link for a Case and returns its address to copy, as Send a photo link's Create link to copy does. resend_copy makes a fresh address for a live link sent from a Case, as Resend's Create link to copy does, counted as a reminder when one is due. extend adds days to the link; cancel stops it taking photos; log_call records a phone call to the customer. extend, cancel and log_call are taken where the link's page offers them (offers in pegasus_photo_link_get). A repeated operation key acts once; a repeated issue or resend_copy returns replayed and no address.")]
    public async Task<PhotoLinkActionToolResult> ActAsync(
        [Description("issue, resend_copy, extend, cancel or log_call.")] string action,
        [Description(AutomationToolText.OperationKey)] string operationKey,
        [Description("issue: the Case.")] Guid? caseId = null,
        [Description("resend_copy, extend, cancel and log_call: " + CaptureDescription)] Guid? captureId = null,
        [Description("extend: 3, 7 or 14. issue: the link's lifetime in days, one Administration offers; omit for the default.")] int? days = null,
        [Description(CustomerNameDescription)] string? customerName = null,
        [Description("A new link: the customer's mobile, kept on the link; omit for the Case's claimant contact number.")] string? mobile = null,
        [Description("A new link: the customer's e-mail address, kept on the link.")] string? email = null,
        [Description(AccidentDateDescription)] string? accidentDate = null,
        [Description(ModeDescription)] string? mode = null,
        [Description(QuickLinkForDescription)] string? quickLinkFor = null,
        [Description(LiveCameraDescription)] bool? liveCameraOnly = null,
        [Description(OriginalsDescription)] bool? originalsInBackground = null,
        CancellationToken cancellationToken = default)
    {
        return await auditor.CallAsync(
            "pegasus_photo_link_action",
            Resource(captureId ?? caseId, "invalid"),
            operationKey,
            async (context, key) =>
            {
                var mail = RequireComposed();
                var verb = Verb(action, ActionVerbs, "action");
                var actor = context.Actor;
                var newLink = customerName is not null || mobile is not null || email is not null || accidentDate is not null
                    || mode is not null || quickLinkFor is not null || liveCameraOnly is not null || originalsInBackground is not null;
                if (verb == "issue")
                {
                    if (captureId is not null)
                    {
                        throw new McpException("issue takes caseId, not captureId.");
                    }

                    var id = AutomationMcpErrors.RequireId(caseId ?? Guid.Empty, "case identifier");
                    var link = await NewLinkAsync(customerName, mobile, email, accidentDate, mode, quickLinkFor, liveCameraOnly, originalsInBackground, days, cancellationToken);
                    var issued = await IssueAsync(id, actor, link, PhotoLinkTokenPurpose.Copy, key, cancellationToken);
                    return await ActedAsync(verb, issued.Capture.Id, issued.Replayed ? null : mail.CaptureUrl(issued.Token), issued.Replayed, key, cancellationToken);
                }

                if (caseId is not null || newLink || (days is not null && verb != "extend"))
                {
                    throw new McpException($"{verb} takes captureId{(verb == "extend" ? " and days" : string.Empty)} and none of a new link's details.");
                }

                var captured = AutomationMcpErrors.RequireId(captureId ?? Guid.Empty, "photo link identifier");
                if (verb == "resend_copy")
                {
                    var resent = await FoundAsync(() => actions.ResendAsync(actor, captured, PhotoLinkTokenPurpose.Copy, key, cancellationToken));
                    return await ActedAsync(verb, captured, resent.Replayed ? null : mail.CaptureUrl(resent.Token), resent.Replayed, key, cancellationToken);
                }

                // Core takes each act only where the link offers it, and a
                // repeated key that already recorded it is answered as done
                // (#2130; R1092).
                switch (verb)
                {
                    case "extend":
                        var extendBy = days ?? throw new McpException("extend needs days.");
                        await OfferedAsync(verb, () => actions.ExtendAsync(actor, captured, extendBy, key, cancellationToken));
                        break;
                    case "cancel":
                        await OfferedAsync(verb, () => actions.RevokeAsync(actor, captured, key, cancellationToken));
                        break;
                    default:
                        await OfferedAsync(verb, () => actions.LogCallAsync(actor, captured, key, cancellationToken));
                        break;
                }

                return await ActedAsync(verb, captured, null, false, key, cancellationToken);
            },
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_photo_link_send",
        Title = "Send a photo link",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true,
        UseStructuredContent = true)]
    [Description("Sends a photo link to the customer, as Send a photo link and Resend do. action send makes a new link for a Case (caseId, with the new link's details) and sends it; resend sends a live link sent from a Case again (captureId) under a fresh address, counted as a reminder when one is due. channel email sends the Photo link e-mail as a new message from an Outgoing approved mailbox, to the new link's email or the link's own address; channel text texts a UK mobile, where a text provider is configured. A message given is sent as written, its placeholders such as {photo link} filled in and the link added if it was left out. A repeated operation key sends nothing more: a repeated e-mail returns replayed, a repeated text is refused. Submitted is accepted, not yet Sent; Unknown must never be resent.")]
    public async Task<PhotoLinkSendToolResult> SendAsync(
        [Description("send or resend.")] string action,
        [Description("email or text.")] string channel,
        [Description(AutomationToolText.OperationKey + " A text's key is at most 87 characters.")] string operationKey,
        [Description("send: the Case.")] Guid? caseId = null,
        [Description("resend: " + CaptureDescription)] Guid? captureId = null,
        [Description("send: the customer's e-mail address; required for email, else kept on the link.")] string? email = null,
        [Description("text: the UK mobile the text goes to; omit for the Case's claimant contact number (send) or the link's own mobile (resend). send by email: kept on the link.")] string? mobile = null,
        [Description("email: the address of the Outgoing approved mailbox to send from; omit for the Case's default sender.")] string? fromMailbox = null,
        [Description("email: the subject; omit for the standard subject naming the registration.")] string? subject = null,
        [Description("The message, plain text, sent as written; omit for the standard Photo link wording.")] string? message = null,
        [Description(CustomerNameDescription)] string? customerName = null,
        [Description(AccidentDateDescription)] string? accidentDate = null,
        [Description(ModeDescription)] string? mode = null,
        [Description(QuickLinkForDescription)] string? quickLinkFor = null,
        [Description(LiveCameraDescription)] bool? liveCameraOnly = null,
        [Description(OriginalsDescription)] bool? originalsInBackground = null,
        [Description(DaysDescription)] int? days = null,
        CancellationToken cancellationToken = default)
    {
        return await auditor.CallAsync(
            "pegasus_photo_link_send",
            Resource(captureId ?? caseId, "invalid"),
            operationKey,
            async (context, key) =>
            {
                var mail = RequireComposed();
                var verb = Verb(action, SendVerbs, "action");
                var byText = Verb(channel, Channels, "channel") == "text";
                if (byText)
                {
                    if (subject is not null || fromMailbox is not null)
                    {
                        throw new McpException("subject and fromMailbox apply to email.");
                    }

                    if (key.Length > MaximumTextOperationKeyLength)
                    {
                        throw new McpException($"A text's operation key is at most {MaximumTextOperationKeyLength} characters.");
                    }

                    if (!sendText.Available)
                    {
                        throw new McpException("Sending by text is unavailable in this runtime.");
                    }
                }
                else if (!mail.Available)
                {
                    throw new McpException("Sending by e-mail is unavailable in this runtime.");
                }

                if (message is not null && string.IsNullOrWhiteSpace(message))
                {
                    throw new McpException(byText ? SendPhotoLinkText.MessageRequired : "A message is required.");
                }

                if (subject is not null && string.IsNullOrWhiteSpace(subject))
                {
                    throw new McpException("A subject is required.");
                }

                return verb == "send"
                    ? await SendNewAsync(
                        mail, context, key, byText, caseId, captureId, email, mobile, fromMailbox, subject, message,
                        customerName, accidentDate, mode, quickLinkFor, liveCameraOnly, originalsInBackground, days,
                        cancellationToken)
                    : await ResendAsync(
                        mail, context, key, byText, caseId, captureId, email, mobile, fromMailbox, subject, message,
                        customerName is not null || accidentDate is not null || mode is not null || quickLinkFor is not null
                            || liveCameraOnly is not null || originalsInBackground is not null || days is not null,
                        cancellationToken);
            },
            cancellationToken);
    }

    /// <summary>
    /// Send a photo link's Send by e-mail or Send by text: what the page
    /// checks before it issues, then the link, then the message.
    /// </summary>
    private async Task<PhotoLinkSendToolResult> SendNewAsync(
        PhotoLinkMail mail,
        AutomationActorContext context,
        string key,
        bool byText,
        Guid? caseId,
        Guid? captureId,
        string? email,
        string? mobile,
        string? fromMailbox,
        string? subject,
        string? message,
        string? customerName,
        string? accidentDate,
        string? mode,
        string? quickLinkFor,
        bool? liveCameraOnly,
        bool? originalsInBackground,
        int? days,
        CancellationToken cancellationToken)
    {
        if (captureId is not null)
        {
            throw new McpException("send takes caseId, not captureId; resend sends a link again.");
        }

        var actor = context.Actor;
        var id = AutomationMcpErrors.RequireId(caseId ?? Guid.Empty, "case identifier");
        var facts = await caseReader.GetAsync(id, cancellationToken);
        if (facts is null || facts.IsArchived || facts.IsClosed)
        {
            throw new McpException(IssuePhotoLink.CaseCannotTakeALink);
        }

        var link = await NewLinkAsync(customerName, mobile, email, accidentDate, mode, quickLinkFor, liveCameraOnly, originalsInBackground, days, cancellationToken);
        var channel = byText ? "text" : "email";
        if (byText)
        {
            // The text goes to the mobile the form holds: the Case's claimant's
            // unless another is given.
            var number = mobile ?? facts.ClaimantContactNumber;
            if (UkMobile.ToE164(number) is null)
            {
                throw new McpException(SendPhotoLinkText.NotAUkMobile);
            }

            var texted = await IssueAsync(id, actor, link, PhotoLinkTokenPurpose.Text, key, cancellationToken);
            if (texted.Replayed)
            {
                throw new McpException(SendPhotoLinkText.AlreadySent);
            }

            return await TextAsync(mail, context, key, "send", texted, facts.PrincipalName, number, message, cancellationToken);
        }

        if (PhotoCaptureText.Email(email) is null)
        {
            throw new McpException($"email: {Labels.NeededToSendByEmail}.");
        }

        var choices = await mail.SendersAsync(id, cancellationToken);
        var fromId = FromMailboxId(choices, fromMailbox);
        if (choices.Sender(fromId) is null)
        {
            throw new McpException(new CaseMailSenderRequiredException().Message);
        }

        var subjectLine = subject ?? PhotoLinkMail.Subject(facts.NormalizedRegistration);
        var issued = await IssueAsync(id, actor, link, PhotoLinkTokenPurpose.Email, key, cancellationToken);
        if (issued.Replayed)
        {
            return await SentAsync("send", channel, issued.Capture.Id, null, true, null, null, key, cancellationToken);
        }

        return await EmailAsync(mail, context, key, "send", issued, id, facts.PrincipalName, fromId, subjectLine, message, cancellationToken);
    }

    /// <summary>
    /// Resend's composer, Send by e-mail or Send by text: only a link sent
    /// from a Case is sent again, to its own address by e-mail.
    /// </summary>
    private async Task<PhotoLinkSendToolResult> ResendAsync(
        PhotoLinkMail mail,
        AutomationActorContext context,
        string key,
        bool byText,
        Guid? caseId,
        Guid? captureId,
        string? email,
        string? mobile,
        string? fromMailbox,
        string? subject,
        string? message,
        bool newLinkDetails,
        CancellationToken cancellationToken)
    {
        if (caseId is not null || email is not null || newLinkDetails || (!byText && mobile is not null))
        {
            throw new McpException("resend takes captureId and sends the link to its own address; a new link's details are not taken.");
        }

        var actor = context.Actor;
        var id = AutomationMcpErrors.RequireId(captureId ?? Guid.Empty, "photo link identifier");
        var row = await queries.GetRowAsync(id, cancellationToken);
        var capture = row?.Capture;
        var facts = capture?.CaseId is { } linkCaseId ? await caseReader.GetAsync(linkCaseId, cancellationToken) : null;
        if (byText)
        {
            var number = mobile ?? capture?.Mobile;
            if (capture is { Origin: PhotoCaptureOrigin.CaseLink } && UkMobile.ToE164(number) is null)
            {
                throw new McpException(SendPhotoLinkText.NotAUkMobile);
            }

            var texted = await FoundAsync(() => actions.ResendAsync(actor, id, PhotoLinkTokenPurpose.Text, key, cancellationToken));
            if (texted.Replayed)
            {
                throw new McpException(SendPhotoLinkText.AlreadySent);
            }

            return await TextAsync(mail, context, key, "resend", texted, facts?.PrincipalName, number, message, cancellationToken);
        }

        Guid? fromId = null;
        if (capture is { Origin: PhotoCaptureOrigin.CaseLink } caseLink)
        {
            if (caseLink.CaseId is not { } sendFor || string.IsNullOrWhiteSpace(caseLink.Email))
            {
                throw new McpException("The link has no e-mail address to send to; send it by text, or copy it with pegasus_photo_link_action resend_copy.");
            }

            var choices = await mail.SendersAsync(sendFor, cancellationToken);
            fromId = FromMailboxId(choices, fromMailbox);
            if (choices.Sender(fromId) is null)
            {
                throw new McpException(new CaseMailSenderRequiredException().Message);
            }
        }

        var issued = await FoundAsync(() => actions.ResendAsync(actor, id, PhotoLinkTokenPurpose.Email, key, cancellationToken));
        if (issued.Replayed)
        {
            return await SentAsync("resend", "email", id, null, true, null, null, key, cancellationToken);
        }

        var subjectLine = subject ?? PhotoLinkMail.Subject(issued.Capture.NormalizedRegistration);
        return await EmailAsync(
            mail, context, key, "resend", issued, issued.Capture.CaseId!.Value, facts?.PrincipalName, fromId, subjectLine, message, cancellationToken);
    }

    /// <summary>The issued link's e-mail, as Send and Resend compose and send it.</summary>
    private async Task<PhotoLinkSendToolResult> EmailAsync(
        PhotoLinkMail mail,
        AutomationActorContext context,
        string key,
        string verb,
        IssuedPhotoLink issued,
        Guid caseId,
        string? principalName,
        Guid? fromId,
        string subject,
        string? message,
        CancellationToken cancellationToken)
    {
        var capture = issued.Capture;
        var url = mail.CaptureUrl(issued.Token);
        var values = PhotoLinkMail.Values(capture.NormalizedRegistration, capture.CustomerName, principalName, url, capture.ExpiresAtUtc);
        var body = await mail.ComposeAsync(context.Actor, message, values, url, cancellationToken);
        var operation = await mail.SendAsync(
            context.Actor, caseId, fromId, capture.Email!, subject, body, key, cancellationToken);
        if (operation.State == StaffMailState.Failed)
        {
            Pegasus.Web.Pages.Mail.StaffMailSendLog.Refused(logger, operation.Id, operation.ApprovedMailboxId, operation.FailureCode);
        }

        return await SentAsync(verb, "email", capture.Id, url, false, operation, null, key, cancellationToken);
    }

    /// <summary>The issued link's text, as Send and Resend compose and send it (R918, R919).</summary>
    private async Task<PhotoLinkSendToolResult> TextAsync(
        PhotoLinkMail mail,
        AutomationActorContext context,
        string key,
        string verb,
        IssuedPhotoLink issued,
        string? principalName,
        string? number,
        string? message,
        CancellationToken cancellationToken)
    {
        var capture = issued.Capture;
        var url = mail.CaptureUrl(issued.Token);
        var values = PhotoLinkMail.Values(capture.NormalizedRegistration, capture.CustomerName, principalName, url, capture.ExpiresAtUtc);
        var text = await mail.ComposeTextAsync(context.Actor, message, values, url, cancellationToken);
        var outcome = await sendText.ExecuteAsync(context.Actor, capture.Id, number, text, key, cancellationToken);
        return await SentAsync(verb, "text", capture.Id, url, false, null, outcome, key, cancellationToken);
    }

    private async Task<PhotoLinkSendToolResult> SentAsync(
        string verb,
        string channel,
        Guid captureId,
        string? url,
        bool replayed,
        StaffMailOperation? operation,
        PhotoLinkTextOutcome? outcome,
        string key,
        CancellationToken cancellationToken) =>
        new(
            verb,
            channel,
            await SummaryAsync(captureId, cancellationToken),
            url,
            replayed,
            operation?.Id,
            operation?.State.ToString(),
            operation?.FailureCode ?? outcome?.FailureCode,
            outcome?.Accepted,
            key);

    private async Task<PhotoLinkActionToolResult> ActedAsync(
        string verb,
        Guid captureId,
        string? url,
        bool replayed,
        string key,
        CancellationToken cancellationToken) =>
        new(
            verb,
            await SummaryAsync(captureId, cancellationToken),
            url,
            replayed,
            key);

    /// <summary>A new link's details: what the caller gave, else where Send starts from Administration's settings (R813, R817).</summary>
    private async Task<NewLink> NewLinkAsync(
        string? customerName,
        string? mobile,
        string? email,
        string? accidentDate,
        string? mode,
        string? quickLinkFor,
        bool? liveCameraOnly,
        bool? originalsInBackground,
        int? days,
        CancellationToken cancellationToken)
    {
        var settings = await workflowConfiguration.GetCurrentAsync(cancellationToken);
        var lifetime = days ?? settings.PhotoLinkDefaultLifetimeDays;
        if (!settings.PhotoLinkLifetimesOffered.Contains(lifetime))
        {
            throw new McpException(
                "days must be one of: "
                + string.Join(", ", settings.PhotoLinkLifetimesOffered.Select(offered => offered.ToString(CultureInfo.InvariantCulture)))
                + ".");
        }

        var linkMode = mode is null ? settings.PhotoLinkStartMode : ParseEnum<PhotoCaptureMode>(mode, "mode");
        PhotoCaptureQuickLinkFor? linkFor = quickLinkFor is null
            ? null
            : ParseEnum<PhotoCaptureQuickLinkFor>(quickLinkFor, "quickLinkFor");
        if (linkFor is not null && linkMode != PhotoCaptureMode.Quick)
        {
            throw new McpException("quickLinkFor applies to a Quick link.");
        }

        return new(
            customerName,
            mobile,
            email,
            AutomationMcpErrors.OptionalDate(accidentDate, "accidentDate must be a yyyy-MM-dd date."),
            linkMode,
            linkFor,
            liveCameraOnly ?? settings.PhotoLinkStartLiveCameraOnly,
            originalsInBackground ?? settings.PhotoLinkStartOriginals,
            lifetime);
    }

    private Task<IssuedPhotoLink> IssueAsync(
        Guid caseId,
        ActionActor actor,
        NewLink link,
        PhotoLinkTokenPurpose purpose,
        string key,
        CancellationToken cancellationToken) =>
        issuePhotoLink.ExecuteAsync(
            new(
                caseId,
                actor,
                link.CustomerName,
                link.Mobile,
                link.Email,
                link.AccidentDate,
                link.Mode,
                link.QuickLinkFor,
                link.LiveCameraOnly,
                link.OriginalsInBackground,
                link.Days,
                purpose,
                key),
            cancellationToken);

    private async Task<PhotoLinkSummary> SummaryAsync(Guid captureId, CancellationToken cancellationToken) =>
        Summary(
            await queries.GetRowAsync(captureId, cancellationToken)
                ?? throw new McpException("The photo link was not found."),
            timeProvider.GetUtcNow());

    /// <summary>
    /// The Photo capture pages answer 404 while <c>Features:PhotoCapture</c>
    /// is off; the tools refuse, before anything is read.
    /// </summary>
    private PhotoLinkMail RequireComposed()
    {
        if (options is null || !Pegasus.Web.Presentation.ShellRail.PhotoCaptureEnabled(configuration))
        {
            throw new McpException("Photo capture is unavailable in this runtime.");
        }

        return new PhotoLinkMail(staffMailSend, getCaseHeader, senders, renderTemplate, options);
    }

    private static PhotoLinkSummary Summary(PhotoCaptureListRow source, DateTimeOffset now)
    {
        var derived = PhotoCaptureRowStatus.For(source, now);
        var capture = source.Capture;
        return new(
            capture.Id,
            capture.Origin.ToString(),
            capture.CaseId,
            capture.CaseReference,
            capture.NormalizedRegistration is { Length: > 0 } registration
                ? PhotoCaptureText.DisplayRegistration(registration)
                : null,
            capture.CustomerName,
            source.OrganizationName,
            capture.Mode.ToString(),
            capture.QuickLinkFor?.ToString(),
            PhotoLinkPage.ChipLabelOf(capture, derived.State),
            derived.State.ToString(),
            derived.Due?.Label,
            derived.IsLive,
            source.Photographs,
            source.MissingItems,
            capture.ReminderCount,
            capture.CreatedAtUtc,
            capture.ExpiresAtUtc,
            source.IssuedByName,
            capture.StandingLinkId,
            source.ImageIntakeReference);
    }

    private static PhotoLinkItemView Item(PhotoCaptureItem item, int? number, string caption) =>
        new(
            number,
            caption,
            item.Kind.ToString(),
            item.Kind == PhotoCaptureItemKind.Damage && item.Stage is { } stage ? Labels.StageBadge(stage) : null,
            item.FileName,
            item.MediaType,
            item.TakenAtUtc,
            item.ReceivedAtUtc,
            item.IntakeAssetId is not null);

    /// <summary>A Core act on a link that is not there answers its own sentence.</summary>
    private static async Task<T> FoundAsync<T>(Func<Task<T>> act)
    {
        try
        {
            return await act();
        }
        catch (KeyNotFoundException exception)
        {
            throw new McpException(exception.Message);
        }
    }

    /// <summary>
    /// A Core act on a link, as <see cref="FoundAsync"/>, whose refusal of an
    /// act the link does not offer now names the tool that lists its offers.
    /// </summary>
    private static async Task<T> OfferedAsync<T>(string verb, Func<Task<T>> act)
    {
        try
        {
            return await FoundAsync(act);
        }
        catch (PhotoCaptureRefusalException refusal) when (refusal.Code == PhotoCaptureRefusals.NotOffered)
        {
            throw new McpException($"{verb} is not offered on this link; pegasus_photo_link_get lists the acts it offers.");
        }
    }

    /// <summary>The send-ready Outgoing mailbox the caller named; null for the Case's default sender.</summary>
    private static Guid? FromMailboxId(CaseMailSenderOptions choices, string? address)
    {
        if (address is null)
        {
            return null;
        }

        var named = address.Trim();
        return choices.Mailboxes.SingleOrDefault(mailbox =>
                string.Equals(mailbox.Address, named, StringComparison.OrdinalIgnoreCase))?.Id
            ?? throw new McpException("fromMailbox is not an Outgoing approved mailbox.");
    }

    private static bool ParseTab(string? tab) => tab?.Trim() switch
    {
        null or "" or "direct" => true,
        Labels.StandingLinksKey => false,
        _ => throw new McpException("tab must be direct or standing.")
    };

    private static PhotoCaptureScope ParseScope(string? scope)
    {
        if (scope is null)
        {
            return Labels.ScopeFromKey(null);
        }

        var key = scope.Trim();
        foreach (var candidate in Enum.GetValues<PhotoCaptureScope>())
        {
            if (string.Equals(Labels.ScopeKey(candidate), key, StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        throw new McpException(
            "scope must be one of: "
            + string.Join(", ", Enum.GetValues<PhotoCaptureScope>().Select(Labels.ScopeKey))
            + ".");
    }

    private static string Verb(string? value, string[] verbs, string name)
    {
        var verb = value?.Trim() ?? string.Empty;
        return verbs.Contains(verb, StringComparer.Ordinal)
            ? verb
            : throw new McpException($"The {name} must be one of: {string.Join(", ", verbs)}.");
    }

    private static T ParseEnum<T>(string value, string name)
        where T : struct, Enum =>
        AutomationMcpErrors.ParseName<T>(value, $"{name} must be one of: {string.Join(", ", Enum.GetNames<T>())}.");

    private static string Resource(Guid? id, string fallback) =>
        id is { } value && value != Guid.Empty ? value.ToString("D") : fallback;

    /// <summary>A new link's details, as Send a photo link's form posts them.</summary>
    private sealed record NewLink(
        string? CustomerName,
        string? Mobile,
        string? Email,
        DateOnly? AccidentDate,
        PhotoCaptureMode Mode,
        PhotoCaptureQuickLinkFor? QuickLinkFor,
        bool LiveCameraOnly,
        bool OriginalsInBackground,
        int Days);
}
