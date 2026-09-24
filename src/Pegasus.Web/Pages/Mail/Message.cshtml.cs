using Microsoft.AspNetCore.Mvc;
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
/// classification, Case association, folder move, and post-report AI job.
/// </remarks>
public sealed class MessageModel(
    GetRetainedMail getRetainedMail,
    ICreateAiJob createAiJob,
    IAiJobQueries aiJobQueries,
    ISendToAiControl sendToAiControl,
    CorrectRetainedMailClassification correctClassification,
    MoveRetainedMailFolder moveRetainedMailFolder,
    ISearchCases searchCases,
    IGetCase getCase,
    Pegasus.Core.Triage.IGetTriage getTriage,
    IIntakeAssociationDestinationQueries destinations,
    IStaffMailSend staffMailSend,
    IStaffMailAttachmentResolver attachmentResolver,
    IApprovedMailboxStore approvedMailboxes,
    IGetIntake getIntake,
    IAcquireCaseEditLease acquireCaseEditLease,
    IReleaseCaseEditLease releaseCaseEditLease,
    IEditScopeLeases editScopes,
    ILinkIntake linkIntake,
    IReverseIntakeLink reverseIntakeLink,
    IDismissRetainedMail dismissRetainedMail,
    IRestoreRetainedMail restoreRetainedMail,
    IGetRetainedMailAttachmentOutcomes attachmentOutcomes) : StaffPageModel
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

    public const string LinkAssociationAction = "Link";

    public const string UnlinkAssociationAction = "Unlink";

    public static IReadOnlyList<MailClassificationSelection.SelectionOption> ClassificationOptions =>
        MailClassificationSelection.Options;

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

    private MailOperationalDestination? DestinationFilter { get; set; }

    private MailCategory? DetailedClassificationFilter { get; set; }

    [BindProperty(SupportsGet = true, Name = "section")]
    public string? Section { get; set; }

    [BindProperty(SupportsGet = true, Name = "caseQuery")]
    public string? CaseQuery { get; set; }

    [BindProperty(SupportsGet = true, Name = "targetCaseId")]
    public Guid? TargetCaseId { get; set; }

    [BindProperty]
    public int ExpectedClassificationVersion { get; set; }

    [BindProperty]
    public string? ClassificationKey { get; set; }

    [BindProperty]
    public string? OtherClassificationName { get; set; }

    [BindProperty]
    public string? OtherClassificationReasoning { get; set; }

    [BindProperty]
    public string? CorrectionReason { get; set; }

    [BindProperty]
    public int ExpectedRecommendationPolicyVersion { get; set; }

    [BindProperty]
    public string? ExpectedRecommendationPolicyKey { get; set; }

    [BindProperty]
    public int ExpectedMailboxVersion { get; set; }

    [BindProperty]
    public string? MoveOperationKey { get; set; }

    [BindProperty(SupportsGet = true, Name = "compose")]
    public string? CorrespondenceMode { get; set; }

    [BindProperty(SupportsGet = true, Name = "mailOperationId")]
    public Guid? CorrespondenceOperationId { get; set; }

    [BindProperty(SupportsGet = true, Name = "correspondenceCaseQuery")]
    public string? CorrespondenceCaseQuery { get; set; }

    [BindProperty(SupportsGet = true, Name = "correspondenceCaseReference")]
    public string? CorrespondenceCaseReference { get; set; }

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

    [BindProperty]
    public string CorrespondenceOperationKey { get; set; } = NewOperationKey();

    [TempData]
    public string? ClassificationNotice { get; set; }

    [TempData]
    public string? FolderMoveNotice { get; set; }

    [TempData]
    public string? AssociationNotice { get; set; }

    [TempData]
    public string? QueryResponseNotice { get; set; }

    [TempData]
    public string? CorrespondenceNotice { get; set; }

    public RetainedMailDetail Detail { get; private set; } = null!;

    public IReadOnlyList<AiJobRecord> AiJobs { get; private set; } = [];

    public bool IsQueryResponseSource { get; private set; }

    public string? QueryResponseCondition { get; private set; }

    public bool QueryResponseEnabled =>
        IsQueryResponseSource && QueryResponseCondition is null;

    public IntakeReceipt? AssociationReceipt { get; private set; }

    public CaseDetails? CurrentCase { get; private set; }

    /// <summary>
    /// The Case the message is linked to, of either kind, in the shape of a
    /// link destination: the version its link authority is checked against
    /// and, for a Triage Case, its Triage state. A Triage Case has no Case
    /// workflow, so <see cref="CurrentCase"/> is null for it.
    /// </summary>
    public IntakeAssociationDestination? CurrentDestination { get; private set; }

    public CaseDetails? TargetCase { get; private set; }

    /// <summary>
    /// The chosen "Link to case" target, of either kind, as the destination
    /// owner offers it. <see cref="TargetCase"/> is null for a Triage Case.
    /// </summary>
    public IntakeAssociationDestination? TargetDestination { get; private set; }

    public IReadOnlyList<UploadCaseSuggestion>? CaseResults { get; private set; }

    public string? AssociationLeaseState
    {
        get => TempData.Peek(nameof(AssociationLeaseState)) as string;
        set
        {
            if (value is null)
            {
                TempData.Remove(nameof(AssociationLeaseState));
            }
            else
            {
                TempData[nameof(AssociationLeaseState)] = value;
            }
        }
    }

    public Guid? AssociationLeaseCaseId { get; private set; }

    public Guid? AssociationLeaseMessageId { get; private set; }

    public Guid? AssociationLeaseReceiptId { get; private set; }

    public string? AssociationLeaseAction { get; private set; }

    public long? AssociationLeaseCaseVersion { get; private set; }

    public long? AssociationLeaseIntakeVersion { get; private set; }

    public string? AssociationLeaseToken { get; private set; }

    /// <summary>The prepared association holds a Triage Case's Triage edit scope, not a Case edit lease.</summary>
    public bool AssociationLeaseIsTriageCase { get; private set; }

    public string? AssociationOperationKey { get; private set; }

    public MailFolderScope ListFolder { get; private set; } = MailFolderScope.Inbox;

    /// <summary>
    /// True where the message is no longer inside the list scope it was opened
    /// from. It still renders; the screen states the mismatch and offers the way
    /// back rather than replacing the message with a not-found.
    /// </summary>
    public bool OutsideListScope { get; private set; }

    public ApprovedMailbox? CorrespondenceMailbox { get; private set; }

    public CaseDetails? CorrespondenceCase { get; private set; }

    public IReadOnlyList<StaffMailAttachmentOption> AvailableAttachments { get; private set; } = [];

    public IReadOnlyList<CaseSearchItem> CorrespondenceCaseResults { get; private set; } = [];

    public bool StaffMailAvailable => staffMailSend is not UnavailableStaffMailSend;

    public bool CorrespondenceSendBlocked { get; private set; }

    public StaffMailOperation? CorrespondenceOperation { get; private set; }

    public bool CanCorrespond => StaffMailAvailable
        && !CorrespondenceSendBlocked
        && CorrespondenceMailbox is not null;

    public bool CanReply => CanCorrespond && ReplyRecipients(Detail).To.Length > 0;

    public async Task<IActionResult> OnGetAsync(
        Guid id,
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
        return Page();
    }

    /// <summary>Dismiss on the record: always allowed; an open Unidentified item stays open (Inbox, 13 September).</summary>
    public Task<IActionResult> OnPostDismissAsync(Guid id, string? operationKey, CancellationToken cancellationToken) =>
        ChangeDismissalAsync(
            id,
            actor => dismissRetainedMail.ExecuteAsync(
                new(id, actor, string.IsNullOrWhiteSpace(operationKey) ? NewOperationKey() : operationKey),
                cancellationToken),
            OperatorLabels.Inbox.DismissedNotice);

    /// <summary>Restore on the record of a dismissed message.</summary>
    public Task<IActionResult> OnPostRestoreAsync(Guid id, string? operationKey, CancellationToken cancellationToken) =>
        ChangeDismissalAsync(
            id,
            actor => restoreRetainedMail.ExecuteAsync(
                new(id, actor, string.IsNullOrWhiteSpace(operationKey) ? NewOperationKey() : operationKey),
                cancellationToken),
            OperatorLabels.Inbox.RestoredNotice);

    private async Task<IActionResult> ChangeDismissalAsync(
        Guid id,
        Func<ActionActor, Task<RetainedMailDismissal?>> change,
        string notice)
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
        return RedirectToPage(new
        {
            id,
            mailbox = MailboxFilter,
            folder = FolderFilter,
            pageNumber = PageRouteValue,
            search = SearchTerm,
            queue = QueueFilter,
            sort = OldestFirst ? "oldest" : null
        });
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
            : await attachmentOutcomes.ExecuteAsync(actor, receipt.Id, cancellationToken);
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

    public async Task<IActionResult> OnPostSearchCorrespondenceCaseAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (!StaffMailAvailable || !TryParseListContext(out _)) return NotFound();
        if (!TryNormalizeCorrespondenceCaseQuery(out var query) || string.IsNullOrWhiteSpace(query))
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                ModelState.AddModelError(
                    nameof(CorrespondenceCaseQuery), "Enter a Case search term.");
            }
            return await ReloadAsync(actor, id, cancellationToken);
        }
        CorrespondenceCaseQuery = query;
        CorrespondenceCaseResults = await SearchCasesAsync(actor, query, cancellationToken);
        return await ReloadAsync(actor, id, cancellationToken);
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
            ModelState.AddModelError(nameof(CorrespondenceCaseReference), "Choose one Case by its Case / PO reference.");
            return result;
        }
        ModelState.Remove(nameof(CorrespondenceCaseReference));
        ModelState.Remove(nameof(ExpectedCorrespondenceCaseVersion));
        ExpectedCorrespondenceCaseVersion = CorrespondenceCase.Workflow.Version;
        return result;
    }

    public async Task<IActionResult> OnPostReconcileCorrespondenceAsync(
        Guid id,
        Guid mailOperationId,
        long expectedOperationVersion,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
            return Forbid();
        if (!StaffMailAvailable)
            return NotFound();
        if (mailOperationId == Guid.Empty || expectedOperationVersion < 0)
        {
            CorrespondenceNotice = "The send status request was incomplete. Reload the correspondence and try again.";
            return RedirectToMessage(id);
        }
        try
        {
            if (await staffMailSend.GetAsync(actor, mailOperationId, cancellationToken) is null)
            {
                CorrespondenceNotice = "That send status is no longer available. Reload the correspondence and try again.";
                return RedirectToMessage(id);
            }
            await staffMailSend.ReconcileAsync(actor, mailOperationId, expectedOperationVersion, cancellationToken);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (ArgumentException)
        {
            CorrespondenceNotice = "The send status request was invalid. Reload the correspondence and try again.";
            return RedirectToMessage(id);
        }
        catch (InvalidOperationException)
        {
            CorrespondenceNotice = "The send status could not be reconciled. Reload the correspondence and try again.";
            return RedirectToMessage(id);
        }
        return RedirectToPage(new
        {
            id,
            mailbox = MailboxFilter,
            folder = FolderRouteValue,
            pageNumber = PageRouteValue,
            search = SearchTerm,
            queue = QueueFilter,
            sort = OldestFirst ? "oldest" : null,
            compose = CorrespondenceMode,
            mailOperationId
        });
    }

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
            var detail = await getRetainedMail.ExecuteAsync(actor, id, SearchTerm, cancellationToken);
            if (detail is null)
            {
                return NotFound();
            }
            if (!IsPostReportQuery(detail) || detail.Summary.CaseId is not { } caseId)
            {
                ModelState.AddModelError(
                    string.Empty,
                    OperatorLabels.QueryResponseJobs.InvalidSource);
                return await ReloadAsync(actor, id, cancellationToken);
            }

            await createAiJob.ExecuteAsync(
                new(
                    AiJobKind.QueryResponse,
                    caseId,
                    detail.Summary.CaseReference,
                    id.ToString("D"),
                    null,
                    actor,
                    operationKey),
                cancellationToken);
            QueryResponseNotice = OperatorLabels.QueryResponseJobs.Created;
            return RedirectToMessage(id);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is
            ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return await ReloadAsync(actor, id, cancellationToken);
        }
    }

    public async Task<IActionResult> OnPostPrepareLinkCaseAsync(
        Guid id,
        Guid caseId,
        long expectedIntakeVersion,
        long expectedCaseVersion,
        string leaseOperationKey,
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

            // Staff linking reaches every Case and every Triage Case in any
            // state (operator, 24 September 2026); the destination owner
            // refuses only an archived Case.
            var destination = await destinations.GetAsync(binding, caseId, actor, cancellationToken);
            if (destination is null || destination.Version != expectedCaseVersion)
            {
                throw new IntakeVersionConflictException();
            }
            // A Triage Case destination is claimed through its Triage edit scope.
            var claim = await CaseLinkAuthority.ClaimAsync(
                destination,
                expectedCaseVersion,
                actor,
                leaseOperationKey,
                acquireCaseEditLease,
                editScopes,
                cancellationToken);
            PreserveAssociationLease(
                id,
                binding.Id,
                LinkAssociationAction,
                claim,
                expectedIntakeVersion,
                Guid.NewGuid().ToString("D"));
            return RedirectToAssociationTarget(id, caseId);
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

    public async Task<IActionResult> OnPostPrepareUnlinkCaseAsync(
        Guid id,
        Guid caseId,
        long expectedIntakeVersion,
        long expectedCaseVersion,
        string leaseOperationKey,
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

            var (current, _) = await GetCurrentDestinationAsync(caseId, actor, cancellationToken);
            if (current is null || current.Version != expectedCaseVersion)
            {
                throw new IntakeVersionConflictException();
            }
            var claim = await CaseLinkAuthority.ClaimAsync(
                current,
                expectedCaseVersion,
                actor,
                leaseOperationKey,
                acquireCaseEditLease,
                editScopes,
                cancellationToken);
            PreserveAssociationLease(
                id,
                binding.Id,
                UnlinkAssociationAction,
                claim,
                expectedIntakeVersion,
                Guid.NewGuid().ToString("D"));
            return RedirectToMessage(id);
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

    public async Task<IActionResult> OnPostLinkCaseAsync(
        Guid id,
        Guid caseId,
        long expectedIntakeVersion,
        long expectedCaseVersion,
        string editLeaseToken,
        string operationKey,
        string Reason,
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
            RestoreAssociationLease();
            RequireAssociationConfirmation(operationKey, editLeaseToken, Reason);
            var binding = await GetExactAssociationAsync(actor, id, cancellationToken);
            if (binding is null)
            {
                return NotFound();
            }
            RequirePreparedAssociation(
                id,
                binding.Id,
                LinkAssociationAction,
                caseId,
                expectedIntakeVersion,
                expectedCaseVersion,
                editLeaseToken,
                operationKey);
            await linkIntake.ExecuteAsync(
                new(
                    binding.Id,
                    caseId,
                    expectedIntakeVersion,
                    expectedCaseVersion,
                    editLeaseToken,
                    actor,
                    operationKey,
                    Reason),
                cancellationToken);
            AssociationNotice = "Message linked to the confirmed case.";
            return RedirectToMessage(id);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            var releasePending = await ResolveFailedAssociationLeaseAsync(exception, actor);
            ModelState.AddModelError(
                string.Empty,
                releasePending ? AssociationReleaseFailureMessage : AssociationFailureMessage(exception));
            return await ReloadAsync(actor, id, cancellationToken);
        }
    }

    public async Task<IActionResult> OnPostUnlinkCaseAsync(
        Guid id,
        Guid caseId,
        long expectedIntakeVersion,
        long expectedCaseVersion,
        string editLeaseToken,
        string operationKey,
        string Reason,
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
            RestoreAssociationLease();
            RequireAssociationConfirmation(operationKey, editLeaseToken, Reason);
            var binding = await GetExactAssociationAsync(actor, id, cancellationToken);
            if (binding is null)
            {
                return NotFound();
            }
            RequirePreparedAssociation(
                id,
                binding.Id,
                UnlinkAssociationAction,
                caseId,
                expectedIntakeVersion,
                expectedCaseVersion,
                editLeaseToken,
                operationKey);
            await reverseIntakeLink.ExecuteAsync(
                new(
                    binding.Id,
                    caseId,
                    expectedIntakeVersion,
                    expectedCaseVersion,
                    editLeaseToken,
                    actor,
                    operationKey,
                    Reason),
                cancellationToken);
            AssociationNotice = "Message unlinked from the confirmed case.";
            return RedirectToMessage(id);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            var releasePending = await ResolveFailedAssociationLeaseAsync(exception, actor);
            ModelState.AddModelError(
                string.Empty,
                releasePending ? AssociationReleaseFailureMessage : AssociationFailureMessage(exception));
            return await ReloadAsync(actor, id, cancellationToken);
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
        if (!TryCategory(out var category))
        {
            ModelState.AddModelError(nameof(ClassificationKey), "Choose a valid classification and complete any Other details.");
        }
        if (string.IsNullOrWhiteSpace(CorrectionReason))
        {
            ModelState.AddModelError(nameof(CorrectionReason), "Explain why this classification is being corrected.");
        }
        if (!ModelState.IsValid)
        {
            return await ReloadAsync(actor, id, cancellationToken);
        }

        try
        {
            var result = await correctClassification.ExecuteAsync(
                actor,
                new(id, ExpectedClassificationVersion, category!, CorrectionReason!),
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

        ClassificationNotice = "Classification corrected. The previous decision and evidence remain in permanent history.";
        return RedirectToPage(new
        {
            id,
            mailbox = MailboxFilter,
            folder = FolderFilter,
            pageNumber = PageNumber,
            search = SearchTerm,
            queue = QueueFilter,
            sort = OldestFirst ? "oldest" : null
        });
    }

    public async Task<IActionResult> OnPostMoveToRecommendedFolderAsync(
        Guid id,
        string? Reason,
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
            var result = await moveRetainedMailFolder.ExecuteAsync(
                actor,
                new(
                    id,
                    ExpectedClassificationVersion,
                    ExpectedRecommendationPolicyKey ?? string.Empty,
                    ExpectedRecommendationPolicyVersion,
                    ExpectedMailboxVersion,
                    MoveOperationKey ?? string.Empty,
                    Reason ?? string.Empty),
                cancellationToken);
            if (result is null)
            {
                return NotFound();
            }
            FolderMoveNotice = result.Outcome switch
            {
                RetainedMailFolderMoveOutcome.Succeeded => "Message moved to the recommended Outlook folder.",
                RetainedMailFolderMoveOutcome.Failed => "The message was not moved. You can retry with a new confirmation.",
                _ => "The move result is uncertain. Retry this same confirmation to check its current location."
            };
            return RedirectToPage(new
            {
                id,
                mailbox = MailboxFilter,
                folder = FolderFilter,
                pageNumber = PageNumber,
                search = SearchTerm,
                queue = QueueFilter,
                sort = OldestFirst ? "oldest" : null
            });
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is ArgumentException or RetainedMailFolderMoveException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
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
        await LoadRetainedOperationAsync(actor, cancellationToken);
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

        // The durable send boundary owns the atomic original-message claim.
        // A GET projection can hide a second action, but it cannot distinguish
        // a same-key replay from a forged fresh key without racing another
        // request. Always present the validated command to that boundary:
        // same-key replay returns its operation; a distinct active claim is
        // refused atomically.
        await LoadRetainedOperationAsync(actor, cancellationToken);

        if (!await LoadCorrespondenceContextAsync(actor, initializeForm: false, cancellationToken))
        {
            ModelState.AddModelError(string.Empty, "Correspondence is unavailable for this message.");
            return await ReloadAsync(actor, id, cancellationToken);
        }

        if (CorrespondenceCase is null)
        {
            ModelState.AddModelError(
                nameof(CorrespondenceCaseReference),
                "Choose one Case by its Case / PO reference.");
        }
        else if (ExpectedCorrespondenceCaseVersion < 0
            || CorrespondenceCase.Workflow.Version != ExpectedCorrespondenceCaseVersion)
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

        IReadOnlyList<StaffMailAttachment> attachments = [];
        if (CorrespondenceCase is not null)
        {
            try
            {
                attachments = await attachmentResolver.ResolveCaseAsync(
                    actor, CorrespondenceCase.Summary.CaseId, SelectedAttachments,
                    cancellationToken);
            }
            catch (StaffMailAttachmentSelectionException exception)
            {
                ModelState.AddModelError(nameof(SelectedAttachments), exception.Message);
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
                    CorrespondenceOperationKey.Trim()),
                cancellationToken);
            CorrespondenceOperation = operation;
            if (operation.State == StaffMailState.Sent)
                CorrespondenceNotice = "Correspondence sent.";
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return await ReloadAsync(actor, id, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            // An InvalidOperationException here does not by itself mean the
            // durable send boundary refused a distinct active claim — it is
            // also how an unavailable mail transport or a composition
            // invariant failure surfaces. Only translate to the existing
            // correspondence conflict message when a currently active
            // operation actually exists for this original message;
            // otherwise let the real failure propagate unmasked.
            var currentOperation = await staffMailSend.GetLatestForOriginalAsync(
                actor,
                detail.Summary.Id,
                cancellationToken);
            if (!IsActiveOperation(currentOperation))
            {
                throw;
            }
            ModelState.AddModelError(
                string.Empty,
                "The existing correspondence operation must finish or be resolved before another action.");
            return await ReloadAsync(actor, id, cancellationToken);
        }

        return RedirectToPage(new
        {
            id,
            mailbox = MailboxFilter,
            folder = FolderRouteValue,
            pageNumber = PageRouteValue,
            search = SearchTerm,
            queue = QueueFilter,
            sort = OldestFirst ? "oldest" : null,
            compose = ModeCode(mode),
            mailOperationId = CorrespondenceOperation?.Id
        });
    }

    private async Task<bool> LoadCorrespondenceContextAsync(
        ActionActor actor,
        bool initializeForm,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Detail.ImmutableMessageId)
            || Detail.Summary.Id == Guid.Empty
            || Detail.Summary.MailboxId == Guid.Empty)
        {
            return false;
        }
        var mailboxes = await approvedMailboxes.ListAsync(cancellationToken);
        CorrespondenceMailbox = mailboxes.SingleOrDefault(item =>
            item.Id == Detail.Summary.MailboxId
            && item.State == ApprovedMailboxState.Approved
            && item.RouteScopes.Contains(ApprovedMailboxRouteScope.StaffSend)
            && item.RouteScopes.Contains(ApprovedMailboxRouteScope.SentEvidence)
            && item.ActivatedAtUtc is not null
            && !string.IsNullOrWhiteSpace(item.MailboxIdentity)
            && !string.IsNullOrWhiteSpace(item.SentFolderIdentity)
            && item.Generation > 0
            && item.VerifiedEncodedMessageSizeLimit is > 0);
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

        if (string.IsNullOrWhiteSpace(CorrespondenceCaseReference))
        {
            CorrespondenceCaseReference = Detail.Summary.CaseReference;
        }
        if (TryNormalizeCorrespondenceCaseQuery(out var query) && query is not null)
        {
            CorrespondenceCaseResults = await SearchCasesAsync(
                actor, query, cancellationToken);
        }
        CorrespondenceCase = await ResolveCaseAsync(
            actor, CorrespondenceCaseReference, cancellationToken);
        if (CorrespondenceCase is not null)
        {
            CorrespondenceCaseReference = CorrespondenceCase.Summary.Reference;
            if (initializeForm)
            {
                ExpectedCorrespondenceCaseVersion = CorrespondenceCase.Workflow.Version;
            }
            AvailableAttachments = await attachmentResolver.ListCaseAsync(
                actor, CorrespondenceCase.Summary.CaseId, cancellationToken);
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
        }
        return true;
    }

    private async Task<CaseDetails?> ResolveCaseAsync(
        ActionActor actor,
        string? reference,
        CancellationToken cancellationToken)
    {
        if (!TryNormalizeCaseReference(reference, out var value))
        {
            return null;
        }

        var matches = await searchCases.ExecuteAsync(
            new(actor, new CaseSearchFilters(CaseReference: value), PageSize: 2), cancellationToken);
        var match = matches.Items.SingleOrDefault(item =>
            string.Equals(item.Reference, value, StringComparison.OrdinalIgnoreCase));
        return match is null
            ? null
            : await getCase.ExecuteAsync(new(match.CaseId, actor), cancellationToken);
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

    private bool TryNormalizeCorrespondenceCaseQuery(out string? query)
    {
        if (TryNormalizeCaseQueryValue(CorrespondenceCaseQuery, out query)) return true;
        ModelState.AddModelError(nameof(CorrespondenceCaseQuery), "Case searches must be 300 characters or fewer.");
        return false;
    }

    private static bool TryNormalizeCaseQueryValue(string? value, out string? normalized)
    {
        normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) || normalized.Length <= 300;
    }

    private static bool TryNormalizeCaseReference(string? value, out string? normalized)
    {
        normalized = value?.Trim();
        return !string.IsNullOrWhiteSpace(normalized) && normalized.Length <= 100;
    }

    private static (StaffMailRecipient[] To, StaffMailRecipient[] Cc) ReplyRecipients(
        RetainedMailDetail detail) =>
        (ParseRecipients(detail.ReplyToAddresses), []);

    private static (StaffMailRecipient[] To, StaffMailRecipient[] Cc) ReplyAllRecipients(
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
        CorrespondenceSendBlocked = IsActiveOperation(CorrespondenceOperation);
        CorrespondenceOperationId = CorrespondenceOperation?.Id;
    }

    private static bool IsActiveOperation(StaffMailOperation? operation) =>
        operation is not null
            && operation.State is not StaffMailState.Sent
                and not StaffMailState.Failed
                and not StaffMailState.Cancelled;

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
        IsQueryResponseSource = IsPostReportQuery(Detail);
        if (IsQueryResponseSource)
        {
            if (CurrentCase is null)
            {
                QueryResponseCondition = OperatorLabels.QueryResponseJobs.CaseUnavailable;
            }
            else if (!AiJobPolicy.IsEligibleQueryResponseCaseState(CurrentCase.Workflow.State))
            {
                QueryResponseCondition = OperatorLabels.QueryResponseJobs.AvailableInPostReportWork;
            }
            else if (!await sendToAiControl.IsEnabledAsync(cancellationToken))
            {
                QueryResponseCondition = OperatorLabels.QueryResponseJobs.AutomationStopped;
            }
        }

        if (ActiveSection == "case" && CurrentCase is { } currentCase)
        {
            AiJobs = await aiJobQueries.ListForSubjectAsync(
                currentCase.Summary.CaseId,
                cancellationToken);
        }
    }

    private static bool IsPostReportQuery(RetainedMailDetail detail) =>
        detail.Summary.CaseId is not null
        && detail.Classification?.Current is
        {
            Outcome: MailClassificationOutcome.Classified,
            Category:
            {
                Direction: MailDirection.Received,
                ReceivedFamily: ReceivedMailFamily.PostReportEmails
            }
        };

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
            ModelState.AddModelError(nameof(CaseQuery), exception.Message);
        }
    }

    private async Task LoadAssociationAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        RestoreAssociationLease();
        if (AssociationLeaseAction == LinkAssociationAction
            && AssociationLeaseMessageId == Detail.Summary.Id
            && AssociationLeaseCaseId is { } preparedCaseId)
        {
            TargetCaseId = preparedCaseId;
        }
        if (Detail.Summary.IntakeReceiptId is not { } receiptId)
        {
            return;
        }

        AssociationReceipt = await getIntake.ExecuteAsync(new(receiptId, actor), cancellationToken);
        if (AssociationReceipt is null)
        {
            return;
        }

        if (AssociationReceipt.CurrentCaseId is { } currentCaseId)
        {
            (CurrentDestination, CurrentCase) = await GetCurrentDestinationAsync(
                currentCaseId, actor, cancellationToken);
            return;
        }

        if (TargetCaseId is { } targetCaseId)
        {
            // The destination owner offers every Case and every Triage Case in
            // any state, and refuses only an archived Case.
            TargetDestination = await destinations.GetAsync(
                AssociationReceipt, targetCaseId, actor, cancellationToken);
            if (TargetDestination is null)
            {
                ModelState.AddModelError(string.Empty, "The selected case is not available for association.");
            }
            else if (!TargetDestination.IsTriageCase)
            {
                TargetCase = await getCase.ExecuteAsync(new(targetCaseId, actor), cancellationToken);
            }
        }
        if (!string.IsNullOrWhiteSpace(CaseQuery))
        {
            var trimmed = CaseQuery.Trim();
            if (trimmed.Length < 2)
            {
                CaseResults = [];
                return;
            }
            var results = await destinations.SearchAsync(
                AssociationReceipt, trimmed, actor, cancellationToken);
            CaseResults = results.Select(item => new UploadCaseSuggestion(
                item.CaseId,
                item.Reference,
                item.Registration,
                item.Claimant,
                OperatorLabels.AssociationDestinationState(item))).ToArray();
        }
    }

    /// <summary>
    /// The Case a receipt is linked to, in the shape the link authority takes.
    /// The destination query answers only a receipt with no Case, so the linked
    /// Case is read from its own owner: a Case through its workflow, or a
    /// Triage Case — which has none — through its Triage record.
    /// </summary>
    private async Task<(IntakeAssociationDestination? Destination, CaseDetails? Case)> GetCurrentDestinationAsync(
        Guid caseId,
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        var linkedCase = await getCase.ExecuteAsync(new(caseId, actor), cancellationToken);
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

    private async Task<IntakeReceipt?> GetExactAssociationAsync(
        ActionActor actor,
        Guid messageId,
        CancellationToken cancellationToken)
    {
        var detail = await getRetainedMail.ExecuteAsync(actor, messageId, SearchTerm, cancellationToken);
        if (detail?.Summary.IntakeReceiptId is not { } receiptId)
        {
            return null;
        }
        return await getIntake.ExecuteAsync(new(receiptId, actor), cancellationToken);
    }

    // Association handlers land back on the Case tab they act from.
    private RedirectToPageResult RedirectToMessage(Guid id) => RedirectToPage(new
    {
        id,
        mailbox = MailboxFilter,
        folder = FolderFilter,
        pageNumber = PageNumber,
        search = SearchTerm,
        queue = QueueFilter,
        sort = OldestFirst ? "oldest" : null,
        section = "case"
    });

    private RedirectToPageResult RedirectToAssociationTarget(Guid id, Guid caseId) =>
        RedirectToPage(new
        {
            id,
            mailbox = MailboxFilter,
            folder = FolderFilter,
            pageNumber = PageNumber,
            search = SearchTerm,
            queue = QueueFilter,
            sort = OldestFirst ? "oldest" : null,
            section = "case",
            caseQuery = CaseQuery,
            targetCaseId = caseId
        });

    // The authority a prepared association holds: a Case edit lease, or a
    // Triage Case's Triage edit scope.
    private const string CaseLeaseKind = "Case";

    private const string TriageCaseLeaseKind = "Triage";

    private void PreserveAssociationLease(
        Guid messageId,
        Guid receiptId,
        string action,
        CaseLinkAuthorityClaim claim,
        long intakeVersion,
        string operationKey) =>
        PreserveAssociationLease(
            messageId,
            receiptId,
            action,
            claim.CaseId,
            claim.IsTriageCase,
            intakeVersion,
            claim.Version,
            claim.Token,
            operationKey);

    private void PreserveAssociationLease(
        Guid messageId,
        Guid receiptId,
        string action,
        Guid caseId,
        bool isTriageCase,
        long intakeVersion,
        long caseVersion,
        string leaseToken,
        string operationKey)
    {
        AssociationLeaseState = string.Join(
            '|',
            messageId.ToString("D"),
            receiptId.ToString("D"),
            action,
            caseId.ToString("D"),
            intakeVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            caseVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            leaseToken,
            operationKey,
            isTriageCase ? TriageCaseLeaseKind : CaseLeaseKind);
        AssociationLeaseMessageId = messageId;
        AssociationLeaseReceiptId = receiptId;
        AssociationLeaseAction = action;
        AssociationLeaseCaseId = caseId;
        AssociationLeaseIntakeVersion = intakeVersion;
        AssociationLeaseCaseVersion = caseVersion;
        AssociationLeaseToken = leaseToken;
        AssociationLeaseIsTriageCase = isTriageCase;
        AssociationOperationKey = operationKey;
    }

    private void RestoreAssociationLease()
    {
        var parts = AssociationLeaseState?.Split('|');
        if (parts is { Length: 9 }
            && Guid.TryParse(parts[0], out var messageId)
            && Guid.TryParse(parts[1], out var receiptId)
            && parts[2] is LinkAssociationAction or UnlinkAssociationAction
            && Guid.TryParse(parts[3], out var caseId)
            && long.TryParse(parts[4], out var intakeVersion)
            && long.TryParse(parts[5], out var caseVersion)
            && parts[6] is { Length: CaseEditAuthority.LeaseTokenLength } leaseToken
            && Guid.TryParseExact(parts[7], "D", out _)
            && parts[8] is CaseLeaseKind or TriageCaseLeaseKind)
        {
            AssociationLeaseMessageId = messageId;
            AssociationLeaseReceiptId = receiptId;
            AssociationLeaseAction = parts[2];
            AssociationLeaseCaseId = caseId;
            AssociationLeaseIntakeVersion = intakeVersion;
            AssociationLeaseCaseVersion = caseVersion;
            AssociationLeaseToken = leaseToken;
            AssociationLeaseIsTriageCase = parts[8] == TriageCaseLeaseKind;
            AssociationOperationKey = parts[7];
        }
    }

    private void ClearAssociationLease()
    {
        TempData.Remove(nameof(AssociationLeaseState));
        AssociationLeaseState = null;
        AssociationLeaseMessageId = null;
        AssociationLeaseReceiptId = null;
        AssociationLeaseAction = null;
        AssociationLeaseCaseId = null;
        AssociationLeaseIntakeVersion = null;
        AssociationLeaseCaseVersion = null;
        AssociationLeaseToken = null;
        AssociationLeaseIsTriageCase = false;
        AssociationOperationKey = null;
    }

    private async Task<bool> ResolveFailedAssociationLeaseAsync(
        Exception exception,
        ActionActor actor)
    {
        if (exception is IntakeOperationConflictException)
        {
            ClearAssociationLease();
            return false;
        }
        if (!IsDefinitiveAssociationFailure(exception))
        {
            return false;
        }
        if (AssociationLeaseCaseId is not { } caseId
            || AssociationLeaseToken is not { } editLeaseToken)
        {
            ClearAssociationLease();
            return false;
        }

        try
        {
            var releaseOperationKey = $"mail-association-release:{Guid.NewGuid():N}";
            if (AssociationLeaseIsTriageCase)
            {
                await editScopes.ReleaseAsync(
                    new(
                        EditScopeKind.Triage,
                        caseId,
                        actor,
                        releaseOperationKey,
                        editLeaseToken),
                    CancellationToken.None);
            }
            else
            {
                await releaseCaseEditLease.ExecuteAsync(
                    new(
                        caseId,
                        actor,
                        releaseOperationKey,
                        editLeaseToken),
                    CancellationToken.None);
            }
        }
        catch (Exception releaseException) when (IsDefinitiveAssociationFailure(releaseException))
        {
            ClearAssociationLease();
            return false;
        }
        catch (Exception releaseException) when (IntakeExceptionPolicy.IsRecoverable(releaseException))
        {
            return true;
        }
        ClearAssociationLease();
        return false;
    }

    private void RequirePreparedAssociation(
        Guid messageId,
        Guid receiptId,
        string action,
        Guid caseId,
        long intakeVersion,
        long caseVersion,
        string leaseToken,
        string operationKey)
    {
        if (AssociationLeaseMessageId != messageId
            || AssociationLeaseReceiptId != receiptId
            || !string.Equals(AssociationLeaseAction, action, StringComparison.Ordinal)
            || AssociationLeaseCaseId != caseId
            || AssociationLeaseIntakeVersion != intakeVersion
            || AssociationLeaseCaseVersion != caseVersion
            || !string.Equals(AssociationLeaseToken, leaseToken, StringComparison.Ordinal)
            || !string.Equals(AssociationOperationKey, operationKey, StringComparison.Ordinal))
        {
            throw new ArgumentException("The association confirmation does not match this message or action.");
        }
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
        string editLeaseToken,
        string reason)
    {
        if (!Guid.TryParseExact(operationKey, "D", out _))
        {
            throw new ArgumentException("The association confirmation has expired.", nameof(operationKey));
        }
        if (editLeaseToken?.Length != CaseEditAuthority.LeaseTokenLength)
        {
            throw new ArgumentException("The case edit confirmation has expired.", nameof(editLeaseToken));
        }
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500)
        {
            throw new ArgumentException("A reason of no more than 500 characters is required.", nameof(reason));
        }
    }

    private bool IsOutsideListScope(RetainedMailDetail detail, MailFolderScope listFolder) =>
        (listFolder == MailFolderScope.Inbox
            && SearchTerm is null
            && detail.Summary.CurrentFolderType is not null)
            || ListDismissed != (detail.Summary.DismissedAtUtc is not null)
            || detail.Folder != listFolder
            || (MailboxFilter is { } mailbox
                && !string.Equals(mailbox, detail.Summary.MailboxId.ToString("D"), StringComparison.OrdinalIgnoreCase))
            || (SearchTerm is not null && detail.Summary.Matches.Count == 0)
            || !MatchesQueue(detail.Classification, detail.Summary);

    private bool TryParseListContext(out MailFolderScope listFolder)
    {
        if (!IndexModel.TryParseFolder(FolderFilter, out listFolder, out var dismissed)
            || !ParseQueueFilter(listFolder)
            || !IndexModel.TryParseSort(SortOrder, out var oldestFirst))
        {
            return false;
        }

        ListDismissed = dismissed;
        OldestFirst = oldestFirst;
        return true;
    }

    private bool ParseQueueFilter(MailFolderScope listFolder)
    {
        if (!IndexModel.TryParseQueue(
                QueueFilter,
                out var normalized,
                out var destination,
                out var detailedClassification))
        {
            return false;
        }
        if (listFolder == MailFolderScope.DeletedItems && normalized is not null)
        {
            return false;
        }
        QueueFilter = normalized;
        DestinationFilter = destination;
        DetailedClassificationFilter = detailedClassification;
        return true;
    }

    private bool MatchesQueue(
        MailClassificationDossier? dossier,
        RetainedMailSummary summary)
    {
        if (DestinationFilter is null && DetailedClassificationFilter is null)
        {
            return true;
        }
        if (dossier is null)
        {
            return false;
        }
        if (DestinationFilter is { } destination)
        {
            var matches = MailOperationalDestinationPolicy.Map(dossier.Current).Destination == destination;
            return destination == MailOperationalDestination.Unidentified
                ? matches && !summary.UnidentifiedResolved
                : matches;
        }
        var actual = dossier.Current.Category;
        var expected = DetailedClassificationFilter;
        return actual is not null
            && expected is not null
            && actual.Direction == expected.Direction
            && actual.ReceivedFamily == expected.ReceivedFamily
            && actual.SentFamily == expected.SentFamily
            && string.Equals(actual.Subtype, expected.Subtype, StringComparison.Ordinal);
    }

    private bool TryCategory(out MailCategory? category) =>
        MailClassificationSelection.TryParse(
            ClassificationKey,
            OtherClassificationName,
            OtherClassificationReasoning,
            out category);

    public string ActiveSection => Section switch
    {
        "attachments" => "attachments",
        "thread" => "thread",
        "case" => "case",
        // A case search or picked target belongs to the Case tab even when
        // the link that carried it named no section.
        _ => CaseQuery is not null || TargetCaseId is not null ? "case" : "message"
    };

    public string? FolderRouteValue => IndexModel.ListFolderCode(ListFolder, ListDismissed);

    public int? PageRouteValue => PageNumber is > 1 ? PageNumber : null;

    public static string ClassificationLabel(MailClassificationOutcome? outcome) => outcome switch
    {
        MailClassificationOutcome.Classified => "Classified",
        MailClassificationOutcome.Ambiguous => "Ambiguous",
        MailClassificationOutcome.Unclassified => "Unclassified",
        _ => "Not yet processed"
    };

    /// <summary>
    /// The operational destination for a classification decision, computed
    /// live from the Core policy rather than a second persisted value: the
    /// destination is a pure function of the already-loaded decision, so
    /// there is nothing to keep in sync.
    /// </summary>
    public static MailOperationalDestinationResult Destination(MailClassificationResult result) =>
        MailOperationalDestinationPolicy.Map(result);

    public static string DecisionLabel(MailClassificationResult result) => result.Category is { } category
        ? DecisionLabel(category)
        : ClassificationLabel(result.Outcome);

    public static string DecisionLabel(MailCategory category) =>
        OperatorLabels.MailClassification(category);

    public static string QueueLabel(MailRouteDisposition? disposition) => disposition switch
    {
        MailRouteDisposition.Accepted => "Accepted",
        MailRouteDisposition.NoMatch => "No match",
        MailRouteDisposition.NeedsSorting => "Unidentified",
        _ => "Not yet processed"
    };

    /// <summary>
    /// The one label for a message's Case association when it has no Case:
    /// the preview pane, its JSON projection and the message page must all
    /// say the same word, because two copies of this label drifted apart
    /// once before.
    /// </summary>
    public static string AssociationLabel(string? caseReference) =>
        caseReference ?? "No case";

    public static string OutcomeLabel(RetainedMailSummary summary) => summary switch
    {
        { CaseId: not null } => "Case created",
        { AllocationState.Status: IntakeAllocationProjectionStatus.Pending } => "Creating case",
        { AllocationState.Status: IntakeAllocationProjectionStatus.FailedRecoverable
            or IntakeAllocationProjectionStatus.FailedBlocked } => "Case not created",
        // A Triage request shares the NeedsSorting decision with unidentified
        // material, because both are pre-case, but they are not the same thing
        // and this column must not call one the other: the operator reported
        // this defect from this screen, which labelled a Triage request as
        // though a case were coming. The word is the destination's own
        // (OperatorLabels.MailOperationalDestination.Triage), not a new one.
        { Classification.IsTriageRequest: true } => "Triage",
        _ => OutcomeLabel(summary.ProcessingOutcome)
    };

    private static string OutcomeLabel(IntakeDecision? decision) => decision switch
    {
        IntakeDecision.CaseCreated => "Ready for case allocation",
        IntakeDecision.NeedsSorting => "Unidentified",
        IntakeDecision.OcrRequired => "Document text required",
        IntakeDecision.TechnicalFailure => "Technical failure",
        IntakeDecision.Unsupported => "Unsupported",
        IntakeDecision.ImageIntakeRegistered => "Vehicle images registered",
        _ => "Not yet processed"
    };
}
