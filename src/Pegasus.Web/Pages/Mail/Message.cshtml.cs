using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Actors;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Mail;

/// <summary>
/// One retained message.
/// </summary>
/// <remarks>
/// Reads one retained message and exposes only the Core-owned correction command;
/// Case linking and mailbox mutation remain separate capabilities.
/// </remarks>
public sealed class MessageModel(
    GetRetainedMail getRetainedMail,
    CorrectRetainedMailClassification correctClassification,
    MoveRetainedMailFolder moveRetainedMailFolder,
    IUploadCaseDecision caseDecision,
    IGetCase getCase,
    IGetIntake getIntake,
    IAcquireCaseEditLease acquireCaseEditLease,
    ILinkIntake linkIntake,
    IReverseIntakeLink reverseIntakeLink) : StaffPageModel
{
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

    [TempData]
    public string? ClassificationNotice { get; set; }

    [TempData]
    public string? FolderMoveNotice { get; set; }

    [TempData]
    public string? AssociationNotice { get; set; }

    public RetainedMailDetail Detail { get; private set; } = null!;

    public IntakeReceipt? AssociationReceipt { get; private set; }

    public CaseDetails? CurrentCase { get; private set; }

    public CaseDetails? TargetCase { get; private set; }

    public IReadOnlyList<UploadCaseSuggestion>? CaseResults { get; private set; }

    public MailFolderScope ListFolder { get; private set; } = MailFolderScope.Inbox;

    /// <summary>
    /// True where the message is no longer inside the list scope it was opened
    /// from. It still renders; the screen states the mismatch and offers the way
    /// back rather than replacing the message with a not-found.
    /// </summary>
    public bool OutsideListScope { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        if (!IndexModel.TryParseFolder(FolderFilter, out var listFolder))
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
        return Page();
    }

    public async Task<IActionResult> OnPostLinkCaseAsync(
        Guid id,
        Guid caseId,
        long expectedIntakeVersion,
        long expectedCaseVersion,
        string operationKey,
        string Reason,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            RequireAssociationReason(Reason);
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
                new(caseId, expectedCaseVersion, actor, operationKey),
                cancellationToken);
            await linkIntake.ExecuteAsync(
                new(
                    binding.Id,
                    caseId,
                    expectedIntakeVersion,
                    lease.Version,
                    lease.Token,
                    actor,
                    operationKey,
                    Reason),
                cancellationToken);
            AssociationNotice = $"Message linked to {selectedCase.Summary.Reference}.";
            return RedirectToMessage(id);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            ModelState.AddModelError(string.Empty, AssociationFailureMessage(exception));
            return await ReloadAsync(actor, id, cancellationToken);
        }
    }

    public async Task<IActionResult> OnPostUnlinkCaseAsync(
        Guid id,
        Guid caseId,
        long expectedIntakeVersion,
        long expectedCaseVersion,
        string operationKey,
        string Reason,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            RequireAssociationReason(Reason);
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
                new(caseId, expectedCaseVersion, actor, operationKey),
                cancellationToken);
            await reverseIntakeLink.ExecuteAsync(
                new(
                    binding.Id,
                    caseId,
                    expectedIntakeVersion,
                    lease.Version,
                    lease.Token,
                    actor,
                    operationKey,
                    Reason),
                cancellationToken);
            AssociationNotice = $"Message unlinked from {currentCase.Summary.Reference}.";
            return RedirectToMessage(id);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            ModelState.AddModelError(string.Empty, AssociationFailureMessage(exception));
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
            search = SearchTerm
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
                search = SearchTerm
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
        if (!IndexModel.TryParseFolder(FolderFilter, out var listFolder))
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
        return Page();
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
            ModelState.AddModelError(nameof(CaseQuery), exception.Message);
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

    private RedirectToPageResult RedirectToMessage(Guid id) => RedirectToPage(new
    {
        id,
        mailbox = MailboxFilter,
        folder = FolderFilter,
        pageNumber = PageNumber,
        search = SearchTerm
    });

    private static string AssociationFailureMessage(Exception exception) => exception switch
    {
        ArgumentException => "Enter a reason and reload the message before trying again.",
        _ => "The message or case changed. Reload it, review the current target, and try again."
    };

    private static void RequireAssociationReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
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
                && !string.Equals(mailbox, detail.Summary.MailboxId, StringComparison.Ordinal))
            || (SearchTerm is not null && detail.Summary.Matches.Count == 0);

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
        _ => "message"
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
        ? $"{(category.Direction == MailDirection.Sent ? "Sent: " : string.Empty)}{category.Name}{(category.Subtype is null ? string.Empty : "/" + category.Subtype)}"
        : ClassificationLabel(result.Outcome);

    public static string QueueLabel(MailRouteDisposition? disposition) => disposition switch
    {
        MailRouteDisposition.Accepted => "Accepted",
        MailRouteDisposition.NoMatch => "No match",
        MailRouteDisposition.NeedsSorting => "Unidentified",
        _ => "Not yet processed"
    };

    public static string OutcomeLabel(RetainedMailSummary summary) => summary switch
    {
        { CaseId: not null } => "Case created",
        { AllocationState.Status: IntakeAllocationProjectionStatus.Pending } => "Creating case",
        { AllocationState.Status: IntakeAllocationProjectionStatus.FailedRecoverable
            or IntakeAllocationProjectionStatus.FailedBlocked } => "Case not created",
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
