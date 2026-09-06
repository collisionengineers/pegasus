using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Actors;
using Pegasus.Core.AiWork;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Operations;
using Pegasus.Core.Workflow;
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
    IUploadCaseDecision caseDecision,
    IGetCase getCase,
    IStaffMailSend staffMailSend,
    IApprovedMailboxStore approvedMailboxes,
    IGetIntake getIntake,
    IAcquireCaseEditLease acquireCaseEditLease,
    IReleaseCaseEditLease releaseCaseEditLease,
    ILinkIntake linkIntake,
    IReverseIntakeLink reverseIntakeLink) : StaffPageModel
{
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

    /// <summary>The Unread scope of the list this message was opened from.</summary>
    [BindProperty(SupportsGet = true, Name = "unread")]
    public string? UnreadFilter { get; set; }

    /// <summary>The list's sort toggle state this message was opened from.</summary>
    [BindProperty(SupportsGet = true, Name = "sort")]
    public string? SortOrder { get; set; }

    public bool UnreadOnly { get; private set; }

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

    public CaseDetails? TargetCase { get; private set; }

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

    public bool CorrespondenceSendBlocked { get; private set; }

    public StaffMailOperation? CorrespondenceOperation { get; private set; }

    public bool CanCorrespond => !CorrespondenceSendBlocked
        && CorrespondenceMailbox is not null
        && CorrespondenceCase is not null;

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
        await LoadAiJobContextAsync(cancellationToken);
        if (CorrespondenceOperationId is { } operationId)
        {
            CorrespondenceOperation = await staffMailSend.GetAsync(actor, operationId, cancellationToken);
            CorrespondenceSendBlocked = CorrespondenceOperation is not null
                && CorrespondenceOperation.State is not StaffMailState.Sent
                    and not StaffMailState.Failed
                    and not StaffMailState.Cancelled;
        }
        if (!await LoadCorrespondenceContextAsync(actor, initializeForm: true, cancellationToken))
        {
            CorrespondenceMode = null;
        }
        return Page();
    }

    public Task<IActionResult> OnPostReplyAsync(Guid id, CancellationToken cancellationToken) =>
        SendCorrespondenceAsync(id, StaffMailComposeMode.Reply, cancellationToken);

    public Task<IActionResult> OnPostReplyAllAsync(Guid id, CancellationToken cancellationToken) =>
        SendCorrespondenceAsync(id, StaffMailComposeMode.ReplyAll, cancellationToken);

    public Task<IActionResult> OnPostForwardAsync(Guid id, CancellationToken cancellationToken) =>
        SendCorrespondenceAsync(id, StaffMailComposeMode.Forward, cancellationToken);

    public async Task<IActionResult> OnPostReconcileCorrespondenceAsync(
        Guid id,
        Guid mailOperationId,
        long expectedOperationVersion,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
            return Forbid();
        try
        {
            await staffMailSend.ReconcileAsync(
                actor, mailOperationId, expectedOperationVersion, cancellationToken);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        return RedirectToPage(new
        {
            id,
            mailbox = MailboxFilter,
            folder = FolderRouteValue,
            pageNumber = PageRouteValue,
            search = SearchTerm,
            queue = QueueFilter,
            unread = UnreadOnly ? "true" : null,
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

            var selectedCase = await getCase.ExecuteAsync(new(caseId, actor), cancellationToken);
            if (selectedCase is null
                || selectedCase.Workflow.Version != expectedCaseVersion
                || selectedCase.Workflow.Archive is not null
                || CaseLifecycleRules.IsTerminal(selectedCase.Workflow.State))
            {
                throw new IntakeVersionConflictException();
            }
            var lease = await acquireCaseEditLease.ExecuteAsync(
                new(caseId, expectedCaseVersion, actor, leaseOperationKey),
                cancellationToken);
            PreserveAssociationLease(
                id,
                binding.Id,
                LinkAssociationAction,
                lease,
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

            var currentCase = await getCase.ExecuteAsync(new(caseId, actor), cancellationToken);
            if (currentCase is null || currentCase.Workflow.Version != expectedCaseVersion)
            {
                throw new IntakeVersionConflictException();
            }
            var lease = await acquireCaseEditLease.ExecuteAsync(
                new(caseId, expectedCaseVersion, actor, leaseOperationKey),
                cancellationToken);
            PreserveAssociationLease(
                id,
                binding.Id,
                UnlinkAssociationAction,
                lease,
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
            unread = UnreadOnly ? "true" : null,
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
                unread = UnreadOnly ? "true" : null,
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
        await LoadAiJobContextAsync(cancellationToken);
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

        if (!await LoadCorrespondenceContextAsync(actor, initializeForm: false, cancellationToken))
        {
            ModelState.AddModelError(string.Empty, "Correspondence is unavailable for this message.");
            return await ReloadAsync(actor, id, cancellationToken);
        }

        if (CorrespondenceCase!.Workflow.Version != ExpectedCorrespondenceCaseVersion)
        {
            ModelState.AddModelError(
                string.Empty,
                "The Case changed after this message was opened. Review it and try again.");
        }
        if (string.IsNullOrWhiteSpace(CorrespondenceSubject))
            ModelState.AddModelError(nameof(CorrespondenceSubject), "A subject is required.");
        if (string.IsNullOrWhiteSpace(CorrespondenceBody))
            ModelState.AddModelError(nameof(CorrespondenceBody), "A message is required.");
        if (string.IsNullOrWhiteSpace(CorrespondenceOperationKey)
            || CorrespondenceOperationKey.Trim().Length > 100)
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
                    CorrespondenceCase.Summary.CaseId,
                    CorrespondenceCase.Workflow.Version,
                    mode,
                    original,
                    to,
                    cc,
                    CorrespondenceSubject!.Trim(),
                    CorrespondenceBody!.Trim(),
                    Attachments: [],
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

        return RedirectToPage(new
        {
            id,
            mailbox = MailboxFilter,
            folder = FolderRouteValue,
            pageNumber = PageRouteValue,
            search = SearchTerm,
            queue = QueueFilter,
            unread = UnreadOnly ? "true" : null,
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
            || Detail.Summary.MailboxId == Guid.Empty
            || Detail.Summary.CaseId is not { } caseId
            || caseId == Guid.Empty)
        {
            return false;
        }

        CorrespondenceCase = await getCase.ExecuteAsync(new(caseId, actor), cancellationToken);
        if (CorrespondenceCase is null || CorrespondenceCase.Workflow.Version <= 0)
            return false;

        var mailboxes = await approvedMailboxes.ListAsync(cancellationToken);
        CorrespondenceMailbox = mailboxes.SingleOrDefault(item =>
            item.Id == Detail.Summary.MailboxId
            && item.State == ApprovedMailboxState.Approved
            && item.RouteScopes.Contains(ApprovedMailboxRouteScope.StaffSend)
            && item.Generation > 0);
        if (CorrespondenceMailbox is null)
            return false;

        var mode = StaffMailComposeMode.New;
        if (CorrespondenceMode is not null
            && !TryComposeMode(CorrespondenceMode, out mode))
        {
            return false;
        }
        if (initializeForm && CorrespondenceMode is not null)
        {
            ExpectedCorrespondenceCaseVersion = CorrespondenceCase.Workflow.Version;
            CorrespondenceOperationKey = NewOperationKey();
            CorrespondenceSubject = SubjectFor(mode, Detail.Summary.Subject);
            var recipients = mode switch
            {
                StaffMailComposeMode.Reply => ReplyRecipients(Detail),
                StaffMailComposeMode.ReplyAll => ReplyAllRecipients(Detail, CorrespondenceMailbox.Address),
                _ => ([], [])
            };
            CorrespondenceTo = string.Join("; ", recipients.Item1.Select(item => item.Address));
            CorrespondenceCc = string.Join("; ", recipients.Item2.Select(item => item.Address));
        }
        return true;
    }

    private static (StaffMailRecipient[] To, StaffMailRecipient[] Cc) ReplyRecipients(
        RetainedMailDetail detail) =>
        (ParseRecipients(detail.Summary.SenderAddress), []);

    private static (StaffMailRecipient[] To, StaffMailRecipient[] Cc) ReplyAllRecipients(
        RetainedMailDetail detail,
        string ownAddress)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ownAddress };
        var to = new List<StaffMailRecipient>();
        var cc = new List<StaffMailRecipient>();
        AddUnique(to, ParseRecipients(detail.Summary.SenderAddress), seen);
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
            CurrentCase = await getCase.ExecuteAsync(new(currentCaseId, actor), cancellationToken);
            return;
        }

        if (TargetCaseId is { } targetCaseId)
        {
            var target = await getCase.ExecuteAsync(new(targetCaseId, actor), cancellationToken);
            if (target is not null
                && target.Workflow.Archive is null
                && !CaseLifecycleRules.IsTerminal(target.Workflow.State))
            {
                TargetCase = target;
            }
            else
            {
                ModelState.AddModelError(string.Empty, "The selected case is not available for association.");
            }
        }
        if (!string.IsNullOrWhiteSpace(CaseQuery))
        {
            CaseResults = await caseDecision.SearchAsync(CaseQuery, actor, cancellationToken);
        }
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
        unread = UnreadOnly ? "true" : null,
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
            unread = UnreadOnly ? "true" : null,
            sort = OldestFirst ? "oldest" : null,
            section = "case",
            caseQuery = CaseQuery,
            targetCaseId = caseId
        });

    private void PreserveAssociationLease(
        Guid messageId,
        Guid receiptId,
        string action,
        CaseEditLease lease,
        long intakeVersion,
        string operationKey) =>
        PreserveAssociationLease(
            messageId,
            receiptId,
            action,
            lease.CaseId,
            intakeVersion,
            lease.Version,
            lease.Token,
            operationKey);

    private void PreserveAssociationLease(
        Guid messageId,
        Guid receiptId,
        string action,
        Guid caseId,
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
            operationKey);
        AssociationLeaseMessageId = messageId;
        AssociationLeaseReceiptId = receiptId;
        AssociationLeaseAction = action;
        AssociationLeaseCaseId = caseId;
        AssociationLeaseIntakeVersion = intakeVersion;
        AssociationLeaseCaseVersion = caseVersion;
        AssociationLeaseToken = leaseToken;
        AssociationOperationKey = operationKey;
    }

    private void RestoreAssociationLease()
    {
        var parts = AssociationLeaseState?.Split('|');
        if (parts is { Length: 8 }
            && Guid.TryParse(parts[0], out var messageId)
            && Guid.TryParse(parts[1], out var receiptId)
            && parts[2] is LinkAssociationAction or UnlinkAssociationAction
            && Guid.TryParse(parts[3], out var caseId)
            && long.TryParse(parts[4], out var intakeVersion)
            && long.TryParse(parts[5], out var caseVersion)
            && parts[6] is { Length: CaseEditAuthority.LeaseTokenLength } leaseToken
            && Guid.TryParseExact(parts[7], "D", out _))
        {
            AssociationLeaseMessageId = messageId;
            AssociationLeaseReceiptId = receiptId;
            AssociationLeaseAction = parts[2];
            AssociationLeaseCaseId = caseId;
            AssociationLeaseIntakeVersion = intakeVersion;
            AssociationLeaseCaseVersion = caseVersion;
            AssociationLeaseToken = leaseToken;
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
            await releaseCaseEditLease.ExecuteAsync(
                new(
                    caseId,
                    actor,
                    $"mail-association-release:{Guid.NewGuid():N}",
                    editLeaseToken),
                CancellationToken.None);
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
        CaseEditLeaseConflictException => "This case is currently being edited. Reload and try again later.",
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
            || detail.Folder != listFolder
            || (MailboxFilter is { } mailbox
                && !string.Equals(mailbox, detail.Summary.MailboxId.ToString("D"), StringComparison.OrdinalIgnoreCase))
            || (SearchTerm is not null && detail.Summary.Matches.Count == 0)
            || !MatchesQueue(detail.Classification);

    private bool TryParseListContext(out MailFolderScope listFolder)
    {
        if (!IndexModel.TryParseFolder(FolderFilter, out listFolder)
            || !ParseQueueFilter(listFolder)
            || !IndexModel.TryParseUnread(UnreadFilter, listFolder, out var unreadOnly)
            || !IndexModel.TryParseSort(SortOrder, out var oldestFirst))
        {
            return false;
        }

        UnreadOnly = unreadOnly;
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

    private bool MatchesQueue(MailClassificationDossier? dossier)
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
            return MailOperationalDestinationPolicy.Map(dossier.Current).Destination == destination;
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

    public string? FolderRouteValue =>
        ListFolder == MailFolderScope.Inbox ? null : IndexModel.FolderCode(ListFolder);

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
        IntakeDecision.BlockedIntake => "Blocked",
        IntakeDecision.OcrRequired => "Document text required",
        IntakeDecision.TechnicalFailure => "Technical failure",
        IntakeDecision.Unsupported => "Unsupported",
        IntakeDecision.ImageIntakeRegistered => "Vehicle images registered",
        _ => "Not yet processed"
    };
}
