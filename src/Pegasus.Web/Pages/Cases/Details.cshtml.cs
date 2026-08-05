using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Core.Actors;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Eva;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Tasks;
using Pegasus.Core.Workflow;
using Pegasus.Core.Vehicle;

namespace Pegasus.Web.Pages.Cases;

[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed partial class DetailsModel(
    IGetCase getCase,
    IAcquireCaseEditLease acquireLease,
    IRenewCaseEditLease renewLease,
    IReleaseCaseEditLease releaseLease,
    IConfirmCompleteness confirmCompleteness,
    ISaveCase saveCase,
    IHoldCase holdCase,
    IReleaseCase releaseCase,
    ITransitionCase transitionCase,
    IAssignCaseEngineer assignEngineer,
    IRecordEngineerFinding recordEngineerFinding,
    ICreateLinkedReplacement createLinkedReplacement,
    ICloseCase closeCase,
    IReopenCase reopenCase,
    IArchiveCase archiveCase,
    ICreateCaseTask createCaseTask,
    IAssignCaseTask assignCaseTask,
    ICompleteCaseTask completeCaseTask,
    ICancelCaseTask cancelCaseTask,
    IRecordCaseReportApproval recordCaseReportApproval,
    ILinkReportEvidence linkReportEvidence,
    IUnlinkReportEvidence unlinkReportEvidence,
    IRecordManualCaseChase recordManualCaseChase,
    IRequestVehicleLookup requestVehicleLookup,
    IAcceptVehicleSuggestion acceptVehicleSuggestion,
    IEvaHandoffQueries evaHandoffQueries,
    IGenerateEvaHandoff generateEvaHandoff,
    IAddCaseDocument addCaseDocument,
    ILogicallyRemoveDocument logicallyRemoveDocument,
    IConfirmThirdPartyVehicleEvidence confirmThirdPartyVehicleEvidence,
    ICreateBoxFileRequest createBoxFileRequest,
    IRevokeBoxFileRequest revokeBoxFileRequest,
    ICreateRequestUploadLink createRequestUploadLink,
    IRevokeRequestUploadLink revokeRequestUploadLink,
    IImageIntakeQueries imageIntakeQueries,
    TimeProvider timeProvider,
    ILogger<DetailsModel> logger) : PageModel
{
    public IReadOnlyList<ImageIntakeSummary> ImageIntakes { get; private set; } = [];

    private const long MaximumStaffUploadBytes = 10 * 1024 * 1024;
    private const string LeaseTokenKey = "CaseLeaseToken";
    private const string LeaseCaseIdKey = "CaseLeaseCaseId";
    private const string ClaimLeaseOperationKeyName = "CaseClaimLeaseOperationKey";
    private const string ClaimLeaseCaseIdKey = "CaseClaimLeaseCaseId";
    private const string RenewLeaseOperationKeyName = "CaseRenewLeaseOperationKey";
    private const string ReleaseLeaseOperationKeyName = "CaseReleaseLeaseOperationKey";
    private const string ProposedValuesKey = "CaseProposedValues";
    private const string ProposedValuesCaseIdKey = "CaseProposedValuesCaseId";
    private const string ProposedValuesDroppedKey = "CaseProposedValuesDropped";

    /// <summary>
    /// Cookie TempData carries roughly four kilobytes for the whole response, so the retained
    /// refusal payload is capped well inside it. A payload that does not fit is not silently
    /// dropped: the editor is told the proposed values could not be kept.
    /// </summary>
    private const int MaximumRetainedProposedCharacters = 2000;
    private const int MaximumRetainedProposedValueCharacters = 300;

    private static readonly string[] UnretainedFormFields =
    [
        "id",
        "caseId",
        "expectedVersion",
        "expectedCaseVersion",
        "expectedTaskVersion",
        "expectedRequestVersion",
        "operationKey",
        "editLeaseToken",
        "__RequestVerificationToken"
    ];

    public CaseDetails? Case { get; private set; }

    /// <summary>
    /// The values a refused editor submitted, held for comparison against the values the case now
    /// holds. There is no control that applies, merges, or forces them: the only way forward is to
    /// enter edit mode again and retype.
    /// </summary>
    public IReadOnlyList<ProposedCaseValue> ProposedValues { get; private set; } = [];

    public bool ProposedValuesWereDropped { get; private set; }

    public string? ViewerSubjectId { get; private set; }

    public bool QueryFailed { get; private set; }

    public string? LeaseToken { get; private set; }

    public string ClaimLeaseOperationKey { get; private set; } = NewOperationKey();

    public bool CanRecoverLease { get; private set; }

    public string RenewLeaseOperationKey { get; private set; } = NewOperationKey();

    public Guid ReportApprovalId { get; } = Guid.NewGuid();

    public DateTimeOffset ManualChaseAttemptedAtUtc { get; private set; }
    public string ReleaseLeaseOperationKey { get; private set; } = NewOperationKey();

    public IReadOnlyList<DocumentSemanticRole> DocumentSemanticRoles { get; } =
        Enum.GetValues<DocumentSemanticRole>();

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (id == Guid.Empty)
        {
            return NotFound();
        }

        try
        {
            Case = await getCase.ExecuteAsync(new(id, actor), cancellationToken);
            if (Case is null)
            {
                return NotFound();
            }
            ImageIntakes = await imageIntakeQueries.ListForCaseAsync(id, cancellationToken);
            ViewerSubjectId = actor.SubjectId;
            RestoreLeaseState(id, actor);
            RestoreProposedValues(id);
            ManualChaseAttemptedAtUtc = timeProvider.GetUtcNow();
            return Page();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCaseDetailsQueryFailed(logger, id, exception);
            QueryFailed = true;
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return Page();
        }
    }

    public async Task<IActionResult> OnGetEvaDownloadAsync(
        Guid id,
        int revision,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (id == Guid.Empty || revision <= 0)
        {
            return NotFound();
        }

        try
        {
            var artifact = await evaHandoffQueries.GetRevisionAsync(
                id,
                revision,
                actor,
                cancellationToken);
            if (artifact is null
                || SafeEvaFileName(artifact.FileName) is not { } fileName)
            {
                return NotFound();
            }

            Response.Headers.XContentTypeOptions = "nosniff";
            Response.Headers.CacheControl = "private, no-store";
            Response.Headers["X-Content-SHA256"] = artifact.BundleSha256;
            Response.ContentLength = artifact.ContentLength;
            return File(artifact.Content, EvaHandoffRevisionArtifact.MediaType, fileName);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCaseDetailsQueryFailed(logger, id, exception);
            return NotFound();
        }
    }

    public async Task<IActionResult> OnPostClaimLeaseAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            ClearLeaseState();
            return Forbid();
        }

        try
        {
            var normalizedOperationKey = RequireOperationKey(operationKey);
            var lease = await acquireLease.ExecuteAsync(
                new(id, expectedVersion, actor, normalizedOperationKey),
                cancellationToken);
            StoreClaimLeaseOperation(id, normalizedOperationKey);
            StoreLeaseAuthority(id, lease.Token);
            TempData.Remove(RenewLeaseOperationKeyName);
            TempData.Remove(ReleaseLeaseOperationKeyName);
            TempData["CaseStatus"] = $"Edit mode is active until {lease.ExpiresAtUtc:u}.";
        }
        catch (StaffAuthorizationException)
        {
            ClearLeaseState();
            return Forbid();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCaseCommandFailed(logger, id, "claim_lease", exception);
            if (IsLeaseLoss(exception))
            {
                ClearLeaseState();
            }
            else if (Guid.TryParseExact(operationKey, "N", out var operationId))
            {
                StoreClaimLeaseOperation(id, operationId.ToString("N"));
            }
            TempData["CaseError"] =
                "Edit mode could not be entered because the case changed or is being edited by another member of staff.";
        }

        return RedirectToDetails(id);
    }

    public async Task<IActionResult> OnPostRenewLeaseAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string editLeaseToken,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            ClearLeaseState();
            return Forbid();
        }

        try
        {
            var normalizedOperationKey = RequireOperationKey(operationKey);
            var lease = await renewLease.ExecuteAsync(
                new(id, expectedVersion, actor, normalizedOperationKey, editLeaseToken),
                cancellationToken);
            StoreLeaseAuthority(id, lease.Token);
            TempData.Remove(RenewLeaseOperationKeyName);
            TempData["CaseStatus"] = $"Edit mode was renewed until {lease.ExpiresAtUtc:u}.";
        }
        catch (StaffAuthorizationException)
        {
            ClearLeaseState();
            return Forbid();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCaseCommandFailed(logger, id, "renew_lease", exception);
            if (IsLeaseLoss(exception))
            {
                ClearLeaseState();
            }
            else
            {
                StoreLeaseAuthority(id, editLeaseToken);
                TempData[RenewLeaseOperationKeyName] = operationKey;
            }
            TempData["CaseError"] =
                "Edit mode could not be renewed. Reload the case and enter edit mode again.";
        }

        return RedirectToDetails(id);
    }

    public async Task<IActionResult> OnPostReleaseLeaseAsync(
        Guid id,
        string operationKey,
        string editLeaseToken,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            ClearLeaseState();
            return Forbid();
        }

        try
        {
            await releaseLease.ExecuteAsync(
                new(id, actor, RequireOperationKey(operationKey), editLeaseToken),
                cancellationToken);
            ClearLeaseState();
            TempData["CaseStatus"] = "Edit mode was left safely.";
        }
        catch (StaffAuthorizationException)
        {
            ClearLeaseState();
            return Forbid();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCaseCommandFailed(logger, id, "release_lease", exception);
            if (IsLeaseLoss(exception))
            {
                ClearLeaseState();
            }
            else
            {
                StoreLeaseAuthority(id, editLeaseToken);
                TempData[ReleaseLeaseOperationKeyName] = operationKey;
            }
            TempData["CaseError"] = "Edit mode could not be released. Reload the case to confirm its current state.";
        }

        return RedirectToDetails(id);
    }

    public Task<IActionResult> OnPostConfirmCompletenessAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        bool instructionComplete,
        bool imagesComplete,
        bool instructionConfirmedByStaff,
        bool imagesConfirmedByStaff,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandAsync(
            id,
            editLeaseToken,
            "confirm_completeness",
            actor => confirmCompleteness.ExecuteAsync(
                new(
                    id,
                    expectedVersion,
                    actor,
                    operationKey,
                    reason,
                    editLeaseToken,
                    new(
                        instructionComplete,
                        imagesComplete,
                        instructionConfirmedByStaff,
                        imagesConfirmedByStaff)),
                cancellationToken),
            "Case completeness was confirmed against the current policy.");

    public Task<IActionResult> OnPostSaveAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        string? claimantName,
        string? claimNumber,
        string? vehicleRegistration,
        string? vehicleMake,
        string? vehicleModel,
        long? vehicleMileage,
        string? vehicleMileageUnit,
        string? accidentCircumstances,
        DateOnly? incidentDate,
        string? contactName,
        string? contactEmailAddress,
        string? contactPhoneNumber,
        DateOnly? instructionDate,
        string? vatStatus,
        DateOnly? inspectionDate,
        DateOnly? inspectionDeadline,
        string? inspectionAddress,
        CaseInspectionMode? inspectionMode,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandAsync(
            id,
            editLeaseToken,
            "save_case",
            actor => saveCase.ExecuteAsync(
                new(
                    id,
                    expectedVersion,
                    actor,
                    operationKey,
                    reason,
                    editLeaseToken,
                    new(
                        claimantName,
                        claimNumber,
                        vehicleRegistration,
                        vehicleMake,
                        vehicleModel,
                        vehicleMileage,
                        vehicleMileageUnit,
                        accidentCircumstances,
                        incidentDate,
                        contactName,
                        contactEmailAddress,
                        contactPhoneNumber,
                        instructionDate,
                        vatStatus,
                        inspectionDate,
                        inspectionDeadline,
                        inspectionAddress,
                        inspectionMode)),
                cancellationToken),
            "Case data was saved with attributable field provenance.");

    public Task<IActionResult> OnPostCreateTaskAsync(
        Guid id,
        Guid taskId,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        string description,
        Guid? assigneeId,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandAsync(
            id,
            editLeaseToken,
            "create_case_task",
            actor => createCaseTask.ExecuteAsync(
                new(
                    id,
                    taskId,
                    expectedVersion,
                    actor,
                    operationKey,
                    reason,
                    editLeaseToken,
                    description,
                    assigneeId),
                cancellationToken),
            "The case task was created.");

    public Task<IActionResult> OnPostAssignTaskAsync(
        Guid id,
        Guid taskId,
        long expectedVersion,
        long expectedTaskVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        Guid? assigneeId,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandAsync(
            id,
            editLeaseToken,
            "assign_case_task",
            actor => assignCaseTask.ExecuteAsync(
                new(
                    id,
                    taskId,
                    expectedVersion,
                    expectedTaskVersion,
                    actor,
                    operationKey,
                    reason,
                    editLeaseToken,
                    assigneeId),
                cancellationToken),
            "The case task assignment was updated.");

    public Task<IActionResult> OnPostCompleteTaskAsync(
        Guid id,
        Guid taskId,
        long expectedVersion,
        long expectedTaskVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandAsync(
            id,
            editLeaseToken,
            "complete_case_task",
            actor => completeCaseTask.ExecuteAsync(
                new(
                    id,
                    taskId,
                    expectedVersion,
                    expectedTaskVersion,
                    actor,
                    operationKey,
                    reason,
                    editLeaseToken),
                cancellationToken),
            "The case task was completed.");

    public Task<IActionResult> OnPostCancelTaskAsync(
        Guid id,
        Guid taskId,
        long expectedVersion,
        long expectedTaskVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandAsync(
            id,
            editLeaseToken,
            "cancel_case_task",
            actor => cancelCaseTask.ExecuteAsync(
                new(
                    id,
                    taskId,
                    expectedVersion,
                    expectedTaskVersion,
                    actor,
                    operationKey,
                    reason,
                    editLeaseToken),
                cancellationToken),
            "The case task was cancelled.");

    public Task<IActionResult> OnPostRecordReportApprovalAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        Guid approvalId,
        string artifactIdentity,
        string artifactSha256,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandAsync(
            id,
            editLeaseToken,
            "record_report_approval",
            actor => recordCaseReportApproval.ExecuteAsync(
                new(
                    id,
                    expectedVersion,
                    actor,
                    operationKey,
                    reason,
                    editLeaseToken,
                    new(
                        approvalId,
                        artifactIdentity,
                        artifactSha256)),
                cancellationToken),
            "The immutable report artifact was approved; this does not claim it was sent.");

    public Task<IActionResult> OnPostLinkReportEvidenceAsync(
        Guid id,
        Guid evidenceId,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandAsync(
            id,
            editLeaseToken,
            "link_report_evidence",
            actor => linkReportEvidence.ExecuteAsync(
                new(
                    id,
                    expectedVersion,
                    actor,
                    operationKey,
                    reason,
                    editLeaseToken,
                    evidenceId),
                cancellationToken),
            "The exact retained report-Sent evidence was linked.");

    public Task<IActionResult> OnPostUnlinkReportEvidenceAsync(
        Guid id,
        Guid evidenceId,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandAsync(
            id,
            editLeaseToken,
            "unlink_report_evidence",
            actor => unlinkReportEvidence.ExecuteAsync(
                new(
                    id,
                    expectedVersion,
                    actor,
                    operationKey,
                    reason,
                    editLeaseToken,
                    evidenceId),
                cancellationToken),
            "The report-Sent evidence was unlinked; retained evidence and history were preserved.");

    public Task<IActionResult> OnPostRecordManualChaseAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        DateTimeOffset attemptedAtUtc,
        string channel,
        string targetPartyOrAddress,
        string outcome,
        string? note,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandAsync(
            id,
            editLeaseToken,
            "record_manual_chase",
            actor => recordManualCaseChase.ExecuteAsync(
                new(
                    id,
                    expectedVersion,
                    editLeaseToken,
                    actor,
                    operationKey,
                    reason,
                    channel,
                    targetPartyOrAddress,
                    attemptedAtUtc,
                    outcome,
                    note),
                cancellationToken),
            "The manual chase was recorded and the next chase date was scheduled.");

    public Task<IActionResult> OnPostArchiveAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandAsync(
            id,
            editLeaseToken,
            "archive_case",
            actor => archiveCase.ExecuteAsync(
                new(id, expectedVersion, actor, operationKey, reason, editLeaseToken),
                cancellationToken),
            "The terminal case was archived and is now read-only.");

    public Task<IActionResult> OnPostHoldAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandAsync(
            id,
            editLeaseToken,
            "hold",
            actor => holdCase.ExecuteAsync(
                new(id, expectedVersion, actor, operationKey, reason, editLeaseToken),
                cancellationToken),
            "The case was put on hold.");

    public Task<IActionResult> OnPostReleaseHoldAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandAsync(
            id,
            editLeaseToken,
            "release_hold",
            actor => releaseCase.ExecuteAsync(
                new ChangeCaseStateRequest(
                    id,
                    expectedVersion,
                    actor,
                    operationKey,
                    reason,
                    editLeaseToken),
                cancellationToken),
            "The case hold was released.");

    public Task<IActionResult> OnPostReturnToReviewAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        bool instructionsComplete,
        bool imagesComplete,
        bool instructionsReviewedByStaff,
        bool imagesReviewedByStaff,
        string evidenceReference,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandAsync(
            id,
            editLeaseToken,
            "return_to_review",
            actor => transitionCase.ExecuteAsync(
                new(
                    id,
                    expectedVersion,
                    actor,
                    operationKey,
                    reason,
                    editLeaseToken,
                    CaseTransitionDestination.Review,
                    Readiness(
                        instructionsComplete,
                        imagesComplete,
                        instructionsReviewedByStaff,
                        imagesReviewedByStaff,
                        evidenceReference)),
                cancellationToken),
            "The case returned to Review.");

    public Task<IActionResult> OnPostAssignEngineerAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        Guid engineerId,
        bool instructionsComplete,
        bool imagesComplete,
        bool instructionsReviewedByStaff,
        bool imagesReviewedByStaff,
        string evidenceReference,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandAsync(
            id,
            editLeaseToken,
            "assign_engineer",
            actor => assignEngineer.ExecuteAsync(
                new(
                    id,
                    expectedVersion,
                    actor,
                    operationKey,
                    reason,
                    editLeaseToken,
                    engineerId,
                    Readiness(
                        instructionsComplete,
                        imagesComplete,
                        instructionsReviewedByStaff,
                        imagesReviewedByStaff,
                        evidenceReference)),
                cancellationToken),
            "The Engineer was assigned.");

    public Task<IActionResult> OnPostStartWorkAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandAsync(
            id,
            editLeaseToken,
            "start_work",
            actor => transitionCase.ExecuteAsync(
                new(
                    id,
                    expectedVersion,
                    actor,
                    operationKey,
                    reason,
                    editLeaseToken,
                    CaseTransitionDestination.ReportPreparation),
                cancellationToken),
            "Report preparation was started.");

    public Task<IActionResult> OnPostRecordEngineerFindingAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        AuditAssessment assessment,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandAsync(
            id,
            editLeaseToken,
            "record_engineer_finding",
            actor => recordEngineerFinding.ExecuteAsync(
                new(
                    id,
                    expectedVersion,
                    actor,
                    operationKey,
                    reason,
                    editLeaseToken,
                    assessment),
                cancellationToken),
            "The Engineer finding was recorded.");



    public async Task<IActionResult> OnPostCreateLinkedReplacementAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        string replacementPrincipalCode,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            var outcome = await createLinkedReplacement.ExecuteAsync(
                new(
                    id,
                    expectedVersion,
                    actor,
                    operationKey,
                    reason,
                    editLeaseToken,
                    replacementPrincipalCode),
                cancellationToken);
            ClearLeaseState();
            TempData["CaseStatus"] = outcome.IsDuplicate
                ? $"Replacement case {outcome.Identity.Reference} was already allocated."
                : $"Replacement case {outcome.Identity.Reference} was allocated and linked.";
        }
        catch (StaffAuthorizationException)
        {
            ClearLeaseState();
            return Forbid();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCaseCommandFailed(logger, id, "create_linked_replacement", exception);
            HandleLeaseFailure(id, editLeaseToken, exception);
            TempData["CaseError"] =
                "The corrected replacement could not be created because the case changed or the request is not permitted.";
        }

        return RedirectToDetails(id);
    }

    public Task<IActionResult> OnPostCloseAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        CaseClosureOutcome outcome,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandAsync(
            id,
            editLeaseToken,
            "close",
            actor => closeCase.ExecuteAsync(
                new(id, expectedVersion, actor, operationKey, reason, editLeaseToken, outcome),
                cancellationToken),
            "The selected terminal outcome was recorded.");

    public Task<IActionResult> OnPostReopenAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        CaseReopenDestination destination,
        bool instructionsComplete,
        bool imagesComplete,
        bool instructionsReviewedByStaff,
        bool imagesReviewedByStaff,
        string? evidenceReference,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandAsync(
            id,
            editLeaseToken,
            "reopen",
            actor => reopenCase.ExecuteAsync(
                new(
                    id,
                    expectedVersion,
                    actor,
                    operationKey,
                    reason,
                    editLeaseToken,
                    destination,
                    destination == CaseReopenDestination.Review
                        ? Readiness(
                            instructionsComplete,
                            imagesComplete,
                            instructionsReviewedByStaff,
                            imagesReviewedByStaff,
                            evidenceReference ?? string.Empty)
                        : null),
                cancellationToken),
            "The case was reopened through the selected destination gates.");

    public Task<IActionResult> OnPostRequestVehicleLookupAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string editLeaseToken,
        string registration,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandAsync(
            id,
            editLeaseToken,
            "request_vehicle_lookup",
            actor => requestVehicleLookup.ExecuteAsync(
                new(
                    id,
                    expectedVersion,
                    registration,
                    actor,
                    operationKey,
                    editLeaseToken),
                cancellationToken),
            "The vehicle lookup was queued. Refresh later for current, stale, partial, no-result, unavailable, or failed evidence.");

    public Task<IActionResult> OnPostAcceptVehicleSuggestionAsync(
        Guid id,
        long expectedVersion,
        Guid lookupObservationId,
        VehicleSuggestionDecision decision,
        string? registration,
        string? make,
        string? model,
        long? mileage,
        VehicleMileageUnit? mileageUnit,
        string operationKey,
        string reason,
        string editLeaseToken,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandAsync(
            id,
            editLeaseToken,
            "accept_vehicle_suggestion",
            actor => acceptVehicleSuggestion.ExecuteAsync(
                new(
                    id,
                    expectedVersion,
                    lookupObservationId,
                    decision,
                    decision == VehicleSuggestionDecision.Correct
                        ? new(
                            registration ?? string.Empty,
                            make,
                            model,
                            mileage,
                            mileageUnit)
                        : null,
                    actor,
                    operationKey,
                    reason,
                    editLeaseToken),
                cancellationToken),
            decision == VehicleSuggestionDecision.Accept
                ? "The vehicle suggestion was accepted with its external provenance."
                : "The corrected vehicle values were confirmed with attributable provenance.");

    public async Task<IActionResult> OnPostGenerateEvaHandoffAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            var preparation = await evaHandoffQueries.GetPreparationAsync(id, cancellationToken);
            if (preparation is null || preparation.Images.Count == 0)
            {
                PreserveLeaseState(id, editLeaseToken);
                TempData["CaseError"] = "The EVA handoff was not generated because no eligible images are available.";
                return RedirectToDetails(id);
            }

            var result = await generateEvaHandoff.ExecuteAsync(
                new(
                    id,
                    expectedVersion,
                    actor,
                    operationKey,
                    reason,
                    editLeaseToken),
                cancellationToken);
            if (result.Outcome == GenerateEvaHandoffOutcome.Generated)
            {
                ClearLeaseState();
                TempData["CaseStatus"] =
                    $"EVA handoff revision {result.Revision} was generated deterministically.";
            }
            else
            {
                PreserveLeaseState(id, editLeaseToken);
                TempData["CaseError"] = result.Reasons.Count == 0
                    ? "The EVA handoff was not generated because the case evidence changed."
                    : string.Join(" ", result.Reasons);
            }
        }
        catch (StaffAuthorizationException)
        {
            ClearLeaseState();
            return Forbid();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCaseCommandFailed(logger, id, "generate_eva_handoff", exception);
            HandleLeaseFailure(id, editLeaseToken, exception);
            TempData["CaseError"] =
                "The EVA handoff was not generated because the case changed, edit mode was lost, or bundle generation is unavailable.";
        }

        return RedirectToDetails(id);
    }

    public async Task<IActionResult> OnPostUploadDocumentAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string editLeaseToken,
        DocumentSemanticRole semanticRole,
        IFormFile? upload,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (!Guid.TryParseExact(operationKey, "N", out var operationId)
            || expectedVersion < 0
            || upload is null
            || upload.Length is <= 0 or > MaximumStaffUploadBytes)
        {
            PreserveLeaseState(id, editLeaseToken);
            TempData["CaseError"] =
                "Choose a non-empty document of 10 MB or less and reload the case before retrying.";
            return RedirectToDetails(id);
        }

        await using var content = new MemoryStream((int)upload.Length);
        await upload.CopyToAsync(content, cancellationToken);
        try
        {
            var result = await addCaseDocument.ExecuteAsync(
                new(
                    id,
                    Path.GetFileName(upload.FileName),
                    SafeMediaType(upload.ContentType),
                    content.GetBuffer().AsMemory(0, checked((int)content.Length)),
                    semanticRole,
                    DocumentSource.StaffUpload,
                    $"staff-upload:{operationId:N}",
                    actor,
                    operationId.ToString("N"),
                    expectedVersion,
                    editLeaseToken),
                cancellationToken);
            ClearLeaseState();
            TempData["CaseStatus"] = result.IsReplay
                ? "This document upload was already completed."
                : "The document was retained in case custody.";
        }
        catch (StaffAuthorizationException)
        {
            ClearLeaseState();
            return Forbid();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCaseCommandFailed(logger, id, "add_case_document", exception);
            HandleLeaseFailure(id, editLeaseToken, exception);
            TempData["CaseError"] =
                "The document could not be retained because the case changed, edit mode was lost, or custody is unavailable.";
        }

        return RedirectToDetails(id);
    }

    public Task<IActionResult> OnPostRemoveDocumentAsync(
        Guid id,
        Guid occurrenceId,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        CancellationToken cancellationToken) =>
        ExecuteTransportCommandAsync(
            id,
            editLeaseToken,
            "remove_document",
            actor => logicallyRemoveDocument.ExecuteAsync(
                new(
                    id,
                    occurrenceId,
                    actor,
                    reason,
                    operationKey,
                    expectedVersion,
                    editLeaseToken),
                cancellationToken),
            "The document occurrence was logically removed; custody content and history were retained.");

    public Task<IActionResult> OnPostConfirmThirdPartyVehicleEvidenceAsync(
        Guid id,
        Guid occurrenceId,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        CancellationToken cancellationToken) =>
        ExecuteTransportCommandAsync(
            id,
            editLeaseToken,
            "confirm_third_party_vehicle_evidence",
            actor => confirmThirdPartyVehicleEvidence.ExecuteAsync(
                new(
                    id,
                    occurrenceId,
                    actor,
                    reason,
                    operationKey,
                    expectedVersion,
                    editLeaseToken),
                cancellationToken),
            "The custody-confirmed image was recorded as third-party vehicle evidence and is excluded from EVA export.");

    public async Task<IActionResult> OnPostCreateBoxFileRequestAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string editLeaseToken,
        DateTimeOffset? expiresAtUtc,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            var result = await createBoxFileRequest.ExecuteAsync(
                new(id, actor, operationKey, expiresAtUtc, expectedVersion, editLeaseToken),
                cancellationToken);
            ClearLeaseState();
            TempData["CaseStatus"] = result.IsReplay
                ? "This Box file request was already created. Its secret cannot be displayed again."
                : "The Box file request was created. Copy its secret now; it will not be shown again.";
            if (result.Secret is not null)
            {
                TempData["CaseRequestSecret"] = result.Secret.Url;
            }
        }
        catch (StaffAuthorizationException)
        {
            ClearLeaseState();
            return Forbid();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCaseCommandFailed(logger, id, "create_box_file_request", exception);
            HandleLeaseFailure(id, editLeaseToken, exception);
            TempData["CaseError"] =
                "The Box file request could not be created because the case changed, edit mode was lost, or the service is unavailable.";
        }

        return RedirectToDetails(id);
    }

    public Task<IActionResult> OnPostRevokeBoxFileRequestAsync(
        Guid id,
        Guid fileRequestId,
        long expectedFileRequestVersion,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        CancellationToken cancellationToken) =>
        ExecuteTransportCommandAsync(
            id,
            editLeaseToken,
            "revoke_box_file_request",
            actor => revokeBoxFileRequest.ExecuteAsync(
                new(
                    id,
                    fileRequestId,
                    actor,
                    reason,
                    operationKey,
                    expectedFileRequestVersion,
                    expectedVersion,
                    editLeaseToken),
                cancellationToken),
            "The Box file request was revoked.");

    public async Task<IActionResult> OnPostCreateRequestUploadLinkAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string editLeaseToken,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            var result = await createRequestUploadLink.ExecuteAsync(
                new(id, actor, operationKey, expectedVersion, editLeaseToken),
                cancellationToken);
            ClearLeaseState();
            if (result.Secret is null)
            {
                TempData["CaseStatus"] =
                    "This upload request was already created. Its secret cannot be displayed again.";
            }
            else
            {
                TempData["CaseRequestSecret"] = Url.Page(
                    "/Uploads/Request",
                    pageHandler: null,
                    values: new { token = result.Secret.Token },
                    protocol: Request.Scheme);
                TempData["CaseStatus"] =
                    $"The upload request expires at {result.Link.ExpiresAtUtc:u}. Copy its secret link now.";
            }
        }
        catch (StaffAuthorizationException)
        {
            ClearLeaseState();
            return Forbid();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCaseCommandFailed(logger, id, "create_request_upload_link", exception);
            HandleLeaseFailure(id, editLeaseToken, exception);
            TempData["CaseError"] =
                "The upload request could not be created because the case changed, edit mode was lost, or requests are unavailable.";
        }

        return RedirectToDetails(id);
    }

    public Task<IActionResult> OnPostRevokeRequestUploadLinkAsync(
        Guid id,
        Guid requestId,
        long expectedRequestVersion,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        CancellationToken cancellationToken) =>
        ExecuteTransportCommandAsync(
            id,
            editLeaseToken,
            "revoke_request_upload_link",
            actor => revokeRequestUploadLink.ExecuteAsync(
                new(
                    id,
                    requestId,
                    actor,
                    reason,
                    operationKey,
                    expectedRequestVersion,
                    expectedVersion,
                    editLeaseToken),
                cancellationToken),
            "The upload request was revoked.");

    public static string NewOperationKey() => Guid.NewGuid().ToString("N");

    private async Task<IActionResult> ExecuteCaseCommandAsync<T>(
        Guid id,
        string editLeaseToken,
        string commandName,
        Func<ActionActor, Task<T>> execute,
        string successMessage)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            await execute(actor);
            ClearLeaseState();
            TempData["CaseStatus"] = successMessage;
        }
        catch (StaffAuthorizationException)
        {
            ClearLeaseState();
            return Forbid();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCaseCommandFailed(logger, id, commandName, exception);
            HandleLeaseFailure(id, editLeaseToken, exception);
            RetainProposedValues(id);
            TempData["CaseError"] =
                "The case action was not applied because the case changed, edit mode was lost, or the action is not permitted.";
        }

        return RedirectToDetails(id);
    }

    private async Task<IActionResult> ExecuteTransportCommandAsync(
        Guid id,
        string editLeaseToken,
        string commandName,
        Func<ActionActor, Task> execute,
        string successMessage)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            await execute(actor);
            ClearLeaseState();
            TempData["CaseStatus"] = successMessage;
        }
        catch (StaffAuthorizationException)
        {
            ClearLeaseState();
            return Forbid();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCaseCommandFailed(logger, id, commandName, exception);
            HandleLeaseFailure(id, editLeaseToken, exception);
            RetainProposedValues(id);
            TempData["CaseError"] =
                "The case action was not applied because the item is unavailable, changed, or not part of this case.";
        }

        return RedirectToDetails(id);
    }

    private RedirectToPageResult RedirectToDetails(Guid id) =>
        RedirectToPage("/Cases/Details", new { id });

    private void RestoreLeaseState(Guid caseId, ActionActor actor)
    {
        // An expired lease is already absent from the projection, so this page keeps no second rule.
        var activeLease = Case!.ActiveEditLease;
        if (activeLease is null)
        {
            if (!string.IsNullOrWhiteSpace(PeekLeaseToken())
                || PeekGuid(LeaseCaseIdKey) is not null)
            {
                ClearLeaseState();
            }

            ClaimLeaseOperationKey = GetOrCreateClaimLeaseOperation(caseId);
            return;
        }

        if (!string.Equals(activeLease.Holder, actor.SubjectId, StringComparison.Ordinal))
        {
            ClearLeaseState();
            return;
        }

        if (!Guid.TryParseExact(activeLease.OperationKey, "N", out var claimOperationId))
        {
            ClearLeaseState();
            return;
        }

        ClaimLeaseOperationKey = claimOperationId.ToString("N");
        StoreClaimLeaseOperation(caseId, ClaimLeaseOperationKey);
        var storedToken = PeekLeaseToken();
        if (PeekGuid(LeaseCaseIdKey) == caseId && !string.IsNullOrWhiteSpace(storedToken))
        {
            LeaseToken = storedToken;
            RenewLeaseOperationKey = GetOrCreateOperationKey(RenewLeaseOperationKeyName);
            ReleaseLeaseOperationKey = GetOrCreateOperationKey(ReleaseLeaseOperationKeyName);
            return;
        }

        ClearLeaseAuthority();
        CanRecoverLease = true;
    }

    private string GetOrCreateClaimLeaseOperation(Guid caseId)
    {
        var storedOperationId = PeekGuid(ClaimLeaseOperationKeyName);
        if (PeekGuid(ClaimLeaseCaseIdKey) == caseId
            && storedOperationId is { } operationId
            && operationId != Guid.Empty)
        {
            return operationId.ToString("N");
        }

        ClearLeaseState();
        var operationKey = NewOperationKey();
        StoreClaimLeaseOperation(caseId, operationKey);
        return operationKey;
    }

    private string GetOrCreateOperationKey(string key)
    {
        if (PeekGuid(key) is { } operationId && operationId != Guid.Empty)
        {
            return operationId.ToString("N");
        }

        var operationKey = NewOperationKey();
        TempData[key] = operationKey;
        return operationKey;
    }

    private void StoreClaimLeaseOperation(Guid caseId, string operationKey)
    {
        TempData[ClaimLeaseCaseIdKey] = caseId;
        TempData[ClaimLeaseOperationKeyName] = Guid.ParseExact(operationKey, "N");
    }

    private void StoreLeaseAuthority(Guid caseId, string leaseToken)
    {
        if (string.IsNullOrWhiteSpace(leaseToken))
        {
            return;
        }

        TempData[LeaseCaseIdKey] = caseId;
        TempData[LeaseTokenKey] = new[] { leaseToken };
    }

    /// <summary>
    /// Carries the refused form's own submitted values through the post-redirect-get so the editor
    /// can compare them with the reloaded case. No lease token, version, or case identifier beyond
    /// the route value is retained, and an oversized payload is reported rather than discarded.
    /// </summary>
    private void RetainProposedValues(Guid caseId)
    {
        if (!Request.HasFormContentType)
        {
            return;
        }

        var submitted = Request.Form
            .Where(field => !UnretainedFormFields.Contains(field.Key, StringComparer.Ordinal))
            .Select(field => new
            {
                field.Key,
                Value = string.Join(", ", field.Value.Where(value => !string.IsNullOrWhiteSpace(value)))
            })
            .Where(field => !string.IsNullOrWhiteSpace(field.Value))
            .Select(field => new RetainedProposedValue(
                field.Key,
                Truncate(field.Value, MaximumRetainedProposedValueCharacters)))
            .ToArray();
        if (submitted.Length == 0)
        {
            return;
        }

        TempData[ProposedValuesCaseIdKey] = caseId;
        var payload = JsonSerializer.Serialize(submitted);
        if (payload.Length > MaximumRetainedProposedCharacters)
        {
            TempData.Remove(ProposedValuesKey);
            TempData[ProposedValuesDroppedKey] = true;
            return;
        }

        TempData.Remove(ProposedValuesDroppedKey);
        TempData[ProposedValuesKey] = payload;
    }

    private void RestoreProposedValues(Guid caseId)
    {
        var retainedCaseId = PeekGuid(ProposedValuesCaseIdKey);
        TempData.Remove(ProposedValuesCaseIdKey);
        var payload = TempData[ProposedValuesKey] as string;
        ProposedValuesWereDropped = TempData[ProposedValuesDroppedKey] is true
            && retainedCaseId == caseId;
        if (retainedCaseId != caseId || string.IsNullOrWhiteSpace(payload))
        {
            return;
        }

        RetainedProposedValue[]? retained;
        try
        {
            retained = JsonSerializer.Deserialize<RetainedProposedValue[]>(payload);
        }
        catch (JsonException)
        {
            ProposedValuesWereDropped = true;
            return;
        }

        ProposedValues = retained is null
            ? []
            : retained
                .Select(value => new ProposedCaseValue(
                    FieldLabel(value.Field),
                    value.Value,
                    CurrentValue(value.Field)))
                .ToArray();
    }

    private string? CurrentValue(string field)
    {
        if (Case?.Data is not { } data)
        {
            return null;
        }

        return field switch
        {
            "claimantName" => data.Claimant.Name.Confirmed?.Value,
            "claimNumber" => data.Claim.Number.Confirmed?.Value,
            "vehicleRegistration" => data.Vehicle.Registration.Confirmed?.Value,
            "vehicleMake" => data.Vehicle.Make.Confirmed?.Value,
            "vehicleModel" => data.Vehicle.Model.Confirmed?.Value,
            "vehicleMileage" => data.Vehicle.Mileage.Confirmed?.Value.ToString(
                CultureInfo.InvariantCulture),
            "vehicleMileageUnit" => data.Vehicle.MileageUnit.Confirmed?.Value,
            "accidentCircumstances" => data.Accident.Circumstances.Confirmed?.Value,
            "incidentDate" => data.Accident.IncidentDate.Confirmed?.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            "contactName" => data.Contact.Name.Confirmed?.Value,
            "contactEmailAddress" => data.Contact.EmailAddress.Confirmed?.Value,
            "contactPhoneNumber" => data.Contact.PhoneNumber.Confirmed?.Value,
            "instructionDate" => data.Instruction.InstructionDate.Confirmed?.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            "vatStatus" => data.Instruction.VatStatus.Confirmed?.Value,
            "inspectionDate" => data.Inspection.InspectionDate.Confirmed?.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            "inspectionDeadline" => data.Inspection.Deadline.Confirmed?.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            "inspectionAddress" => data.Inspection.Address.Confirmed?.Value,
            _ => null
        };
    }

    private static string FieldLabel(string field) => field switch
    {
        "claimantName" => "Claimant",
        "claimNumber" => "Claim number",
        "vehicleRegistration" => "Registration",
        "vehicleMake" => "Vehicle make",
        "vehicleModel" => "Vehicle model",
        "vehicleMileage" => "Mileage",
        "vehicleMileageUnit" => "Mileage unit",
        "accidentCircumstances" => "Accident circumstances",
        "incidentDate" => "Incident date",
        "contactName" => "Contact name",
        "contactEmailAddress" => "Contact email",
        "contactPhoneNumber" => "Contact phone",
        "instructionDate" => "Instruction date",
        "vatStatus" => "VAT status",
        "inspectionDate" => "Inspection date",
        "inspectionDeadline" => "Inspection deadline",
        "inspectionAddress" => "Inspection address",
        "inspectionMode" => "Inspection mode",
        "reason" => "Reason",
        _ => Humanize(field)
    };

    private static string Humanize(string field)
    {
        var text = new StringBuilder(field.Length + 8);
        foreach (var character in field)
        {
            if (char.IsUpper(character) && text.Length > 0)
            {
                text.Append(' ');
                text.Append(char.ToLowerInvariant(character));
                continue;
            }

            text.Append(text.Length == 0 ? char.ToUpperInvariant(character) : character);
        }

        return text.ToString();
    }

    private static string Truncate(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : value[..maximumLength];

    private void HandleLeaseFailure(Guid caseId, string? editLeaseToken, Exception exception)
    {
        if (IsLeaseLoss(exception))
        {
            ClearLeaseState();
        }
        else
        {
            PreserveLeaseState(caseId, editLeaseToken);
        }
    }

    private void PreserveLeaseState(Guid caseId, string? editLeaseToken)
    {
        if (!string.IsNullOrWhiteSpace(editLeaseToken))
        {
            StoreLeaseAuthority(caseId, editLeaseToken);
        }
    }

    // TempData materializes Guid-shaped strings as Guid values; the token array keeps opaque tokens textual.
    private string? PeekLeaseToken() =>
        TempData.Peek(LeaseTokenKey) switch
        {
            string token => token,
            string[] { Length: 1 } tokens => tokens[0],
            _ => null
        };

    private Guid? PeekGuid(string key) =>
        TempData.Peek(key) switch
        {
            Guid value => value,
            string text when Guid.TryParse(text, out var value) => value,
            _ => null
        };

    private void ClearLeaseAuthority()
    {
        TempData.Remove(LeaseTokenKey);
        TempData.Remove(LeaseCaseIdKey);
        TempData.Remove(RenewLeaseOperationKeyName);
        TempData.Remove(ReleaseLeaseOperationKeyName);
        LeaseToken = null;
        CanRecoverLease = false;
    }

    private void ClearLeaseState()
    {
        ClearLeaseAuthority();
        TempData.Remove(ClaimLeaseOperationKeyName);
        TempData.Remove(ClaimLeaseCaseIdKey);
    }

    private static bool IsLeaseLoss(Exception exception) =>
        exception is CaseEditLeaseExpiredException or CaseEditLeaseConflictException;

    private bool TryGetActor(out ActionActor actor)
    {
        if (StaffActorFactory.TryCreate(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                User.FindAll(ClaimTypes.Role).Select(claim => claim.Value),
                out var resolved))
        {
            actor = resolved;
            return true;
        }

        actor = null!;
        return false;
    }

    private static CaseReadinessEvidence Readiness(
        bool instructionsComplete,
        bool imagesComplete,
        bool instructionsReviewedByStaff,
        bool imagesReviewedByStaff,
        string evidenceReference) =>
        new(
            instructionsComplete,
            imagesComplete,
            instructionsReviewedByStaff,
            imagesReviewedByStaff,
            evidenceReference);

    private static string RequireOperationKey(string value) =>
        Guid.TryParseExact(value, "N", out var operationId)
            ? operationId.ToString("N")
            : throw new ArgumentException("The operation key is invalid.", nameof(value));


    private static string? SafeEvaFileName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > 255
            || value is "." or ".."
            || value.Contains('/', StringComparison.Ordinal)
            || value.Contains('\\', StringComparison.Ordinal)
            || value.Any(char.IsControl)
            || !value.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return value;
    }

    private static string SafeMediaType(string? value) =>
        string.IsNullOrWhiteSpace(value)
            || value.Length > 200
            || value.Contains('\r', StringComparison.Ordinal)
            || value.Contains('\n', StringComparison.Ordinal)
                ? "application/octet-stream"
                : value.Trim();

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "The authorized case detail query failed for case {CaseId}.")]
    private static partial void LogCaseDetailsQueryFailed(
        ILogger logger,
        Guid caseId,
        Exception exception);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Case command {CommandName} failed for case {CaseId}.")]
    private static partial void LogCaseCommandFailed(
        ILogger logger,
        Guid caseId,
        string commandName,
        Exception exception);

    private sealed record RetainedProposedValue(string Field, string Value);
}

/// <summary>
/// One field of a refused submission beside the value the case now holds, for comparison only.
/// </summary>
public sealed record ProposedCaseValue(string Label, string Proposed, string? Current);
