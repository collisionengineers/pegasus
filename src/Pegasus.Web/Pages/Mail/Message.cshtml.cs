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
        if (!ParseQueueFilter(listFolder))
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
            queue = QueueFilter
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
                search = SearchTerm,
                queue = QueueFilter
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
        if (!ParseQueueFilter(listFolder))
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

    private RedirectToPageResult RedirectToMessage(Guid id) => RedirectToPage(new
    {
        id,
        mailbox = MailboxFilter,
        folder = FolderFilter,
        pageNumber = PageNumber,
        search = SearchTerm,
        queue = QueueFilter
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
                && !string.Equals(mailbox, detail.Summary.MailboxId, StringComparison.Ordinal))
            || (SearchTerm is not null && detail.Summary.Matches.Count == 0)
            || !MatchesQueue(detail.Classification);

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
        ? DecisionLabel(category)
        : ClassificationLabel(result.Outcome);

    public static string DecisionLabel(MailCategory category) =>
        $"{(category.Direction == MailDirection.Sent ? "Sent: " : string.Empty)}{category.Name}{(category.Subtype is null ? string.Empty : "/" + category.Subtype)}";

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
