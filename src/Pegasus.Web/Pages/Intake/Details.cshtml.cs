using System.Security.Claims;
using Pegasus.Core.Actors;
using Pegasus.Core.Address;
using Pegasus.Core.Identity;
using Pegasus.Core.Cases;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Pegasus.Web.Pages.Intake;

public sealed partial class DetailsModel(
    IGetIntake getIntake,
    IResolveIntake resolveIntake,
    IReevaluateIntake reevaluateIntake,
    ILinkIntake linkIntake,
    IReverseIntakeLink reverseIntakeLink,
    IAcquireCaseEditLease acquireCaseEditLease,
    IAcceptIntake acceptIntake,
    IConfirmStandaloneAuditEvidence confirmStandaloneAuditEvidence,
    IStandaloneAuditEvidenceQueries standaloneAuditEvidenceQueries,
    IImageIntakeQueries imageIntakeQueries,
    IImageIntakeOriginResolver imageIntakeOriginResolver,
    IRegisterImageIntake registerImageIntake,
    IVrmSuggestionStore vrmSuggestionStore,
    IImageIntakeCaseCandidates imageIntakeCaseCandidates,
    ILogger<DetailsModel> logger,
    IInspectionAddressResolutionStore addressResolutionStore,
    IProviderInspectionModeStore providerInspectionModeStore) : PageModel
{
    public ImageIntakeDetail? ImageIntake { get; private set; }

    public IReadOnlyList<ImageVrmSuggestion> VrmSuggestions { get; private set; } = [];

    public IReadOnlyList<ImageIntakeCaseCandidate> AssociationCandidates { get; private set; } = [];

    public bool CanRegisterImageIntake { get; private set; }

    public string RegistrationPrefill { get; private set; } = string.Empty;


    public IntakeReceipt Receipt { get; private set; } = null!;

    public bool IsDuplicate { get; private set; }
    public InspectionAddressResolutionSnapshot AddressResolution { get; private set; } = null!;

    public bool ProviderIsImageBased { get; private set; }

    public string PrincipalCode { get; set; } = string.Empty;

    public CaseType CaseType { get; set; } = Pegasus.Core.Cases.CaseType.Inspection;

    public AuditAssessment? StandaloneAuditAssessment { get; set; }
    public Guid? StandaloneAuditOriginalReportAssetId { get; set; }

    public string StandaloneAuditEvidenceReason { get; set; } = string.Empty;

    public StandaloneAuditEvidence? ConfirmedStandaloneAuditEvidence { get; private set; }


    public bool InstructionComplete { get; set; }

    public bool ImagesComplete { get; set; }

    public bool InstructionConfirmedByStaff { get; set; }

    public bool ImagesConfirmedByStaff { get; set; }

    public string AcceptanceOperationKey { get; set; } = string.Empty;
    public string AcceptanceReason { get; set; } = string.Empty;


    public long? ReviewedReceiptVersion { get; set; }

    public long ExpectedAddressReceiptVersion { get; set; }

    public string AddressSuggestionFingerprint { get; set; } = string.Empty;

    public string AddressOperationId { get; set; } = string.Empty;

    public string CorrectedInspectionAddress { get; set; } = string.Empty;
    public Guid? LeasedCaseId { get; private set; }

    public long? LeasedCaseVersion { get; private set; }

    public string? CaseEditLeaseToken { get; private set; }



    public async Task<IActionResult> OnGetAsync(
        Guid id,
        bool duplicate = false,
        CancellationToken cancellationToken = default)
    {
        var result = await LoadReceiptAsync(id, cancellationToken);
        if (result is not null)
        {
            return result;
        }

        IsDuplicate = duplicate;
        PrincipalCode = Receipt.InstructionDraft?.SuggestedPrincipalCode ?? string.Empty;
        InstructionComplete = Receipt.InstructionDraft is not null
            && Receipt.MissingFields.Count == 0
            && (ProviderIsImageBased
                || AddressResolution.State is (
                    InspectionAddressResolutionState.Accepted
                    or InspectionAddressResolutionState.Corrected));
        InstructionConfirmedByStaff = InstructionComplete;
        AcceptanceOperationKey = Guid.NewGuid().ToString("N");
        if (ConfirmedStandaloneAuditEvidence is { } evidence)
        {
            StandaloneAuditAssessment = evidence.Assessment;
            StandaloneAuditOriginalReportAssetId = evidence.OriginalReportAssetId;
            StandaloneAuditEvidenceReason = evidence.Reason;
        }
        ReviewedReceiptVersion =
            ConfirmedStandaloneAuditEvidence?.ReceiptVersion
            ?? AddressResolution.ReceiptVersion;
        RestoreCaseLease();
        return Page();
    }

    public async Task<IActionResult> OnPostBlockAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string reason,
        CancellationToken cancellationToken = default) =>
        await ExecuteCommandAsync(
            id,
            actor => resolveIntake.ExecuteAsync(
                new(
                    id,
                    expectedVersion,
                    actor,
                    operationKey,
                    reason,
                    IntakeResolutionKind.Block,
                    CorrectedDraft: null),
                cancellationToken),
            "The intake receipt was blocked with the recorded reason.",
            cancellationToken);

    public async Task<IActionResult> OnPostReevaluateAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string reason,
        CancellationToken cancellationToken = default) =>
        await ExecuteCommandAsync(
            id,
            actor => reevaluateIntake.ExecuteAsync(
                new(id, expectedVersion, actor, operationKey, reason),
                cancellationToken),
            "Policy re-evaluation was queued.",
            cancellationToken);

    public async Task<IActionResult> OnPostCorrectDraftAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string reason,
        string? suggestedPrincipalCode,
        string? claimantName,
        string? claimNumber,
        string? vehicleRegistration,
        string? vehicleMake,
        string? vehicleModel,
        long? vehicleMileage,
        string? accidentCircumstances,
        DateOnly? dateOfIncident,
        DateOnly? instructionDate,
        string? inspectionAddress,
        DateOnly? inspectionDate,
        CancellationToken cancellationToken = default)
    {
        var correctedDraft = new InstructionDraft(
            Optional(suggestedPrincipalCode),
            Optional(claimantName),
            Optional(claimNumber),
            Optional(vehicleRegistration),
            Optional(vehicleMake),
            Optional(vehicleModel),
            vehicleMileage,
            Optional(accidentCircumstances),
            dateOfIncident,
            instructionDate,
            Optional(inspectionAddress),
            inspectionDate);
        return await ExecuteCommandAsync(
            id,
            actor => resolveIntake.ExecuteAsync(
                new(
                    id,
                    expectedVersion,
                    actor,
                    operationKey,
                    reason,
                    IntakeResolutionKind.CorrectDraft,
                    correctedDraft),
                cancellationToken),
            "The corrected instruction draft was recorded.",
            cancellationToken);
    }

    public async Task<IActionResult> OnPostClaimCaseLeaseAsync(
        Guid id,
        Guid caseId,
        long expectedCaseVersion,
        string operationKey,
        CancellationToken cancellationToken = default)
    {
        if (!StaffActorFactory.TryCreate(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                User.FindAll(ClaimTypes.Role).Select(claim => claim.Value),
                out var actor))
        {
            return Forbid();
        }

        try
        {
            var lease = await acquireCaseEditLease.ExecuteAsync(
                new(caseId, expectedCaseVersion, actor, operationKey),
                cancellationToken);
            PreserveCaseLease(lease.CaseId, lease.Version, lease.Token);
            TempData["IntakeDetailsStatus"] =
                $"Case edit mode is active until {lease.ExpiresAtUtc:u}.";
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            TempData["IntakeDetailsError"] =
                "Case edit mode could not be entered. Check the case version and try again.";
        }

        return RedirectToPage("/Intake/Details", new { id });
    }

    public async Task<IActionResult> OnPostLinkCaseAsync(
        Guid id,
        Guid caseId,
        long expectedIntakeVersion,
        long expectedCaseVersion,
        string editLeaseToken,
        string operationKey,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var result = await ExecuteCommandAsync(
            id,
            actor => linkIntake.ExecuteAsync(
                new(
                    id,
                    caseId,
                    expectedIntakeVersion,
                    expectedCaseVersion,
                    editLeaseToken,
                    actor,
                    operationKey,
                    reason),
                cancellationToken),
            "The intake receipt was linked to the selected case.",
            cancellationToken);
        if (TempData.Peek("IntakeDetailsError") is null)
        {
            ClearCaseLease();
        }
        else
        {
            PreserveCaseLease(caseId, expectedCaseVersion, editLeaseToken);
        }
        return result;
    }

    public async Task<IActionResult> OnPostReverseCaseLinkAsync(
        Guid id,
        Guid caseId,
        long expectedIntakeVersion,
        long expectedCaseVersion,
        string editLeaseToken,
        string operationKey,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var result = await ExecuteCommandAsync(
            id,
            actor => reverseIntakeLink.ExecuteAsync(
                new(
                    id,
                    caseId,
                    expectedIntakeVersion,
                    expectedCaseVersion,
                    editLeaseToken,
                    actor,
                    operationKey,
                    reason),
                cancellationToken),
            "The current intake-to-case association was reversed.",
            cancellationToken);
        if (TempData.Peek("IntakeDetailsError") is null)
        {
            ClearCaseLease();
        }
        else
        {
            PreserveCaseLease(caseId, expectedCaseVersion, editLeaseToken);
        }
        return result;
    }

    public async Task<IActionResult> OnPostAcceptAsync(
        Guid id,
        string? principalCode,
        CaseType caseType,
        AuditAssessment? standaloneAuditAssessment,
        Guid? standaloneAuditOriginalReportAssetId,
        string? standaloneAuditEvidenceReason,
        bool instructionComplete,
        bool imagesComplete,
        bool instructionConfirmedByStaff,
        bool imagesConfirmedByStaff,
        string? acceptanceOperationKey,
        string? acceptanceReason,
        long? reviewedReceiptVersion,
        CancellationToken cancellationToken = default)
    {
        PrincipalCode = principalCode ?? string.Empty;
        CaseType = caseType;
        StandaloneAuditAssessment = standaloneAuditAssessment;
        StandaloneAuditOriginalReportAssetId = standaloneAuditOriginalReportAssetId;
        StandaloneAuditEvidenceReason = standaloneAuditEvidenceReason ?? string.Empty;
        InstructionComplete = instructionComplete;
        ImagesComplete = imagesComplete;
        InstructionConfirmedByStaff = instructionConfirmedByStaff;
        ImagesConfirmedByStaff = imagesConfirmedByStaff;
        AcceptanceOperationKey = acceptanceOperationKey ?? string.Empty;
        AcceptanceReason = acceptanceReason ?? string.Empty;
        ReviewedReceiptVersion = reviewedReceiptVersion;
        var result = await LoadReceiptAsync(id, cancellationToken);
        if (result is not null)
        {
            return result;
        }

        if (Receipt.Decision != IntakeDecision.NeedsSorting)
        {
            ModelState.AddModelError(
                string.Empty,
                "Only an item that needs sorting can be turned into a case here.");
        }
        if (string.IsNullOrWhiteSpace(AcceptanceReason))
        {
            ModelState.AddModelError(
                nameof(AcceptanceReason),
                "Record the staff reason for accepting this intake.");
        }
        else if (AcceptanceReason.Trim().Length > 500)
        {
            ModelState.AddModelError(
                nameof(AcceptanceReason),
                "The acceptance reason must be 500 characters or fewer.");
        }
        PrincipalCode = PrincipalCode.Trim().ToUpperInvariant();
        if (PrincipalCode.Length == 0)
        {
            ModelState.AddModelError(
                nameof(PrincipalCode),
                "Enter the confirmed principal code.");
        }
        else if (PrincipalCode.Length > CasePrincipalCode.MaximumLength)
        {
            ModelState.AddModelError(
                nameof(PrincipalCode),
                $"The principal code must be {CasePrincipalCode.MaximumLength} characters or fewer.");
        }

        var postedProviderMode = PrincipalCode.Length == 0
            ? null
            : await providerInspectionModeStore.GetForPrincipalAsync(
                PrincipalCode,
                cancellationToken);
        if (postedProviderMode != CaseInspectionMode.ImageBasedAssessment
            && AddressResolution.State is not InspectionAddressResolutionState.Accepted
                and not InspectionAddressResolutionState.Corrected)
        {
            ModelState.AddModelError(
                string.Empty,
                "Accept or correct the inspection-address suggestion before allocating a case reference.");
        }

        if (!Enum.IsDefined(CaseType))
        {
            ModelState.AddModelError(nameof(CaseType), "Choose a valid case type.");
        }

        if (StandaloneAuditAssessment is { } assessment && !Enum.IsDefined(assessment))
        {
            ModelState.AddModelError(
                nameof(StandaloneAuditAssessment),
                "Choose a valid Audit assessment.");
        }
        else if (CaseType == Pegasus.Core.Cases.CaseType.Audit && StandaloneAuditAssessment is null)
        {
            ModelState.AddModelError(
                nameof(StandaloneAuditAssessment),
                "Confirm the standalone Audit assessment before accepting the case.");
        }
        else if (CaseType != Pegasus.Core.Cases.CaseType.Audit && StandaloneAuditAssessment is not null)
        {
            ModelState.AddModelError(
                nameof(StandaloneAuditAssessment),
                "An assessment can be recorded here only for a standalone Audit.");
        }
        StandaloneAuditEvidenceReason = StandaloneAuditEvidenceReason.Trim();
        if (CaseType == Pegasus.Core.Cases.CaseType.Audit)
        {
            if (StandaloneAuditOriginalReportAssetId is not { } selectedAssetId
                || selectedAssetId == Guid.Empty)
            {
                ModelState.AddModelError(
                    nameof(StandaloneAuditOriginalReportAssetId),
                    "Select the retained original Engineer report.");
            }
            if (StandaloneAuditEvidenceReason.Length == 0
                || StandaloneAuditEvidenceReason.Length > 500)
            {
                ModelState.AddModelError(
                    nameof(StandaloneAuditEvidenceReason),
                    "Record why this retained report supports the Audit assessment (500 characters maximum).");
            }
            if (ConfirmedStandaloneAuditEvidence is { } confirmed
                && (StandaloneAuditAssessment != confirmed.Assessment
                    || StandaloneAuditOriginalReportAssetId != confirmed.OriginalReportAssetId
                    || !string.Equals(
                        StandaloneAuditEvidenceReason,
                        confirmed.Reason,
                        StringComparison.Ordinal)))
            {
                ModelState.AddModelError(
                    string.Empty,
                    "The standalone Audit evidence was already confirmed with different immutable details.");
            }
        }
        else
        {
            if (StandaloneAuditOriginalReportAssetId is not null
                || StandaloneAuditEvidenceReason.Length > 0
                || ConfirmedStandaloneAuditEvidence is not null)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Retained original-report evidence can be linked only to a standalone Audit.");
            }
        }

        if (!Guid.TryParseExact(AcceptanceOperationKey, "N", out var operationId))
        {
            ModelState.AddModelError(
                string.Empty,
                "The acceptance request is invalid. Reload the page and try again.");
        }

        var reviewedVersion = ReviewedReceiptVersion.GetValueOrDefault(-1);
        if (ReviewedReceiptVersion is null || reviewedVersion < 0)
        {
            ModelState.AddModelError(
                string.Empty,
                "The reviewed intake version is missing. Reload the receipt before accepting it.");
        }

        var subjectId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!StaffActorFactory.TryCreate(
            subjectId,
            User.FindAll(ClaimTypes.Role).Select(claim => claim.Value),
            out var actionActor))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            var acceptanceVersion = reviewedVersion;
            Guid? standaloneAuditEvidenceId = null;
            if (CaseType == Pegasus.Core.Cases.CaseType.Audit)
            {
                var evidence = ConfirmedStandaloneAuditEvidence;
                if (evidence is null)
                {
                    evidence = await confirmStandaloneAuditEvidence.ExecuteAsync(
                        new(
                            operationId,
                            Receipt.Id,
                            reviewedVersion,
                            StandaloneAuditOriginalReportAssetId.GetValueOrDefault(),
                            StandaloneAuditAssessment.GetValueOrDefault(),
                            actionActor,
                            $"standalone-audit-evidence:{operationId:N}",
                            StandaloneAuditEvidenceReason),
                        cancellationToken);
                    ConfirmedStandaloneAuditEvidence = evidence;
                }
                else if (evidence.ReceiptVersion != reviewedVersion)
                {
                    throw new InvalidOperationException(
                        "The intake evidence changed after the original report was confirmed.");
                }

                standaloneAuditEvidenceId = evidence.Id;
                acceptanceVersion = evidence.ReceiptVersion;
                ReviewedReceiptVersion = acceptanceVersion;
            }

            var outcome = await acceptIntake.ExecuteAsync(
                new(
                    Receipt.Id,
                    acceptanceVersion,
                    actionActor,
                    $"intake-accept:{operationId:N}",
                    AcceptanceReason,
                    CaseType,
                    PrincipalCode,
                    new(
                        InstructionComplete,
                        ImagesComplete,
                        InstructionConfirmedByStaff,
                        ImagesConfirmedByStaff),
                    standaloneAuditEvidenceId,
                    Receipt.InstructionDraft?.InspectionDate),
                cancellationToken);

            TempData["IntakeQueueStatus"] = outcome.IsDuplicate
                ? "acceptance_duplicate"
                : "accepted";
            TempData["AcceptedCaseReference"] = outcome.Identity.AuditReference
                ?? outcome.Identity.Reference;
            return RedirectToPage("/Intake/Index");
        }
        catch (CaseAcceptanceOperationConflictException)
        {
            ModelState.AddModelError(
                string.Empty,
                "This intake receipt was already accepted using different review details. Reload the receipt.");
        }
        catch (CaseIdentitySequenceExhaustedException exception)
        {
            LogIdentitySequenceExhausted(logger, Receipt.Id, exception);
            ModelState.AddModelError(
                string.Empty,
                "The case reference sequence is exhausted. The intake receipt was not accepted.");
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            LogCaseAcceptanceFailed(logger, Receipt.Id, exception);
            ModelState.AddModelError(
                string.Empty,
                "The case could not be accepted. No reference was allocated; reload the receipt and try again.");
        }

        return Page();
    }
    public Task<IActionResult> OnPostAcceptAddressAsync(
        Guid id,
        long expectedAddressReceiptVersion,
        string? addressSuggestionFingerprint,
        string? addressOperationId,
        CancellationToken cancellationToken = default)
    {
        ExpectedAddressReceiptVersion = expectedAddressReceiptVersion;
        AddressSuggestionFingerprint = addressSuggestionFingerprint ?? string.Empty;
        AddressOperationId = addressOperationId ?? string.Empty;
        CorrectedInspectionAddress = string.Empty;
        return ResolveAddressAsync(
            id,
            InspectionAddressStaffDecision.AcceptSuggestion,
            cancellationToken);
    }

    public Task<IActionResult> OnPostCorrectAddressAsync(
        Guid id,
        long expectedAddressReceiptVersion,
        string? addressSuggestionFingerprint,
        string? addressOperationId,
        string? correctedInspectionAddress,
        CancellationToken cancellationToken = default)
    {
        ExpectedAddressReceiptVersion = expectedAddressReceiptVersion;
        AddressSuggestionFingerprint = addressSuggestionFingerprint ?? string.Empty;
        AddressOperationId = addressOperationId ?? string.Empty;
        CorrectedInspectionAddress = correctedInspectionAddress ?? string.Empty;
        return ResolveAddressAsync(
            id,
            InspectionAddressStaffDecision.CorrectSuggestion,
            cancellationToken);
    }


    public int DuplicateOccurrenceCount(IntakeAssetRecord asset) =>
        Receipt.AssetRecords.Count(candidate => candidate.ContentHash == asset.ContentHash);


    public static string DecisionLabel(IntakeDecision decision) => decision switch
    {
        IntakeDecision.CaseCreated => "Case created",
        IntakeDecision.NeedsSorting => "Needs sorting",
        IntakeDecision.BlockedIntake => "Blocked intake",
        IntakeDecision.Unsupported => "Unsupported",
        IntakeDecision.OcrRequired => "Document text required",
        IntakeDecision.TechnicalFailure => "Technical failure",
        IntakeDecision.ImageIntakeRegistered => "Image intake registered",
        _ => throw new InvalidOperationException($"Unknown intake decision value '{(int)decision}'.")
    };

    public static string SourceChannelLabel(IntakeSourceChannel channel) => channel switch
    {
        IntakeSourceChannel.ManualUpload => "Manual upload",
        IntakeSourceChannel.Mailbox => "Approved inbox",
        IntakeSourceChannel.Automation => "Automation",
        _ => throw new InvalidOperationException($"Unknown intake source channel value '{(int)channel}'.")
    };

    private async Task<IActionResult> ExecuteCommandAsync(
        Guid id,
        Func<ActionActor, Task> execute,
        string successMessage,
        CancellationToken cancellationToken)
    {
        if (!StaffActorFactory.TryCreate(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                User.FindAll(ClaimTypes.Role).Select(claim => claim.Value),
                out var actor))
        {
            return Forbid();
        }

        try
        {
            await execute(actor);
            TempData["IntakeDetailsStatus"] = successMessage;
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or InvalidOperationException
                or KeyNotFoundException
            || IntakeExceptionPolicy.IsRecoverable(exception))
        {
            LogIntakeCommandFailed(logger, id, exception);
            TempData["IntakeDetailsError"] =
                "The intake command could not be applied. Reload the receipt and try again.";
        }

        return RedirectToPage("/Intake/Details", new { id });
    }

    private static string? Optional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    private void PreserveCaseLease(Guid caseId, long caseVersion, string editLeaseToken)
    {
        TempData["IntakeCaseLeaseId"] = caseId.ToString("D");
        TempData["IntakeCaseLeaseVersion"] =
            caseVersion.ToString(System.Globalization.CultureInfo.InvariantCulture);
        TempData["IntakeCaseLeaseToken"] = editLeaseToken;
    }

    private void RestoreCaseLease()
    {
        if (Guid.TryParse(TempData.Peek("IntakeCaseLeaseId") as string, out var caseId)
            && long.TryParse(
                TempData.Peek("IntakeCaseLeaseVersion") as string,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out var caseVersion)
            && TempData.Peek("IntakeCaseLeaseToken") is string editLeaseToken
            && !string.IsNullOrWhiteSpace(editLeaseToken))
        {
            LeasedCaseId = caseId;
            LeasedCaseVersion = caseVersion;
            CaseEditLeaseToken = editLeaseToken;
        }
    }

    private void ClearCaseLease()
    {
        TempData.Remove("IntakeCaseLeaseId");
        TempData.Remove("IntakeCaseLeaseVersion");
        TempData.Remove("IntakeCaseLeaseToken");
        LeasedCaseId = null;
        LeasedCaseVersion = null;
        CaseEditLeaseToken = null;
    }

    private async Task<IActionResult> ResolveAddressAsync(
        Guid id,
        InspectionAddressStaffDecision decision,
        CancellationToken cancellationToken)
    {
        var loadResult = await LoadReceiptAsync(id, cancellationToken);
        if (loadResult is not null)
        {
            return loadResult;
        }

        if (!Guid.TryParseExact(AddressOperationId, "N", out var operationId))
        {
            ModelState.AddModelError(
                string.Empty,
                "The inspection-address request is invalid. Reload the receipt and try again.");
            PrepareAddressCommand();
            return Page();
        }

        var subject = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(subject, out var staffId) || staffId == Guid.Empty)
        {
            return Forbid();
        }

        var roles = new List<StaffRole>(3);
        if (User.IsInRole(StaffRoleNames.Administrator))
        {
            roles.Add(StaffRole.Administrator);
        }
        if (User.IsInRole(StaffRoleNames.Engineer))
        {
            roles.Add(StaffRole.Engineer);
        }
        if (User.IsInRole(StaffRoleNames.User))
        {
            roles.Add(StaffRole.User);
        }
        if (roles.Count == 0)
        {
            return Forbid();
        }

        try
        {
            var resolution = await addressResolutionStore.ResolveAsync(
                new(
                    id,
                    ExpectedAddressReceiptVersion,
                    AddressSuggestionFingerprint,
                    decision,
                    decision == InspectionAddressStaffDecision.CorrectSuggestion
                        ? CorrectedInspectionAddress
                        : null,
                    ActionActor.Staff(staffId, roles),
                    operationId,
                    HttpContext.TraceIdentifier),
                cancellationToken);
            TempData["InspectionAddressStatus"] =
                resolution.State == InspectionAddressResolutionState.Accepted
                    ? "Inspection address suggestion accepted."
                    : "Inspection address correction recorded.";
            return RedirectToPage("/Intake/Details", new { id });
        }
        catch (InspectionAddressResolutionConcurrencyException)
        {
            ModelState.AddModelError(
                string.Empty,
                "The intake evidence changed. Review the current inspection-address suggestion before confirming it.");
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(nameof(CorrectedInspectionAddress), exception.Message);
        }
        catch (InvalidOperationException)
        {
            ModelState.AddModelError(
                string.Empty,
                "The inspection address remains unresolved. Review the current source evidence.");
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            LogAddressResolutionFailed(logger, id, exception);
            ModelState.AddModelError(
                string.Empty,
                "The inspection address could not be recorded. Reload the receipt and try again.");
        }

        await LoadReceiptAsync(id, cancellationToken);
        PrepareAddressCommand();
        return Page();
    }

    private void PrepareAddressCommand()
    {
        ExpectedAddressReceiptVersion = AddressResolution.ReceiptVersion;
        AddressSuggestionFingerprint =
            AddressResolution.Evaluation.Suggestion?.Fingerprint ?? string.Empty;
        AddressOperationId = Guid.NewGuid().ToString("N");
        CorrectedInspectionAddress =
            AddressResolution.Evaluation.Suggestion?.Value ?? string.Empty;
    }

    private async Task<IActionResult?> LoadReceiptAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!StaffActorFactory.TryCreate(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                User.FindAll(ClaimTypes.Role).Select(claim => claim.Value),
                out var actor))
        {
            return Forbid();
        }

        var receipt = await getIntake.ExecuteAsync(
            new GetIntakeQuery(id, actor),
            cancellationToken);
        if (receipt is null)
        {
            return NotFound();
        }

        var addressResolution = await addressResolutionStore.GetAsync(id, cancellationToken);
        ConfirmedStandaloneAuditEvidence =
            await standaloneAuditEvidenceQueries.GetForReceiptAsync(id, cancellationToken);
        Receipt = receipt;
        var suggestedPrincipalCode = receipt.InstructionDraft?.SuggestedPrincipalCode;
        ProviderIsImageBased = !string.IsNullOrWhiteSpace(suggestedPrincipalCode)
            && await providerInspectionModeStore.GetForPrincipalAsync(
                suggestedPrincipalCode,
                cancellationToken) == CaseInspectionMode.ImageBasedAssessment;
        AddressResolution = addressResolution ?? new(
            id,
            0,
            InspectionAddressResolutionState.Unresolved,
            Ext18InspectionAddressPolicy.Evaluate(receipt),
            null,
            null,
            null);
        PrepareAddressCommand();
        await LoadImageIntakeAsync(cancellationToken);
        return null;
    }

    private async Task LoadImageIntakeAsync(CancellationToken cancellationToken)
    {
        ImageIntake = await imageIntakeQueries.GetByOriginReceiptAsync(Receipt.Id, cancellationToken);
        var isImageOnly = ImageIntakeLifecycleRules.IsImageOnlyMaterial(Receipt);
        if (isImageOnly)
        {
            VrmSuggestions = await vrmSuggestionStore.ListForReceiptAsync(Receipt.Id, cancellationToken);
        }

        CanRegisterImageIntake = isImageOnly
            && ImageIntake is null
            && Receipt.Decision == IntakeDecision.NeedsSorting;
        RegistrationPrefill = VrmSuggestions
            .Where(suggestion => suggestion.Outcome == VrmRecognitionOutcomeKind.Suggested
                && suggestion.Disposition == ImageVrmSuggestionDisposition.Pending
                && suggestion.SuggestedRegistration is not null)
            .OrderByDescending(suggestion => suggestion.Confidence ?? 0)
            .Select(suggestion => suggestion.SuggestedRegistration!)
            .FirstOrDefault() ?? string.Empty;
        AssociationCandidates = ImageIntake is { AssociatedCaseId: null } detail
            ? await imageIntakeCaseCandidates.FindEligibleByRegistrationAsync(
                detail.Record.NormalizedVehicleRegistration,
                cancellationToken)
            : [];
    }

    public async Task<IActionResult> OnPostRegisterImageIntakeAsync(
        Guid id,
        string? vehicleRegistration,
        string operationKey,
        string reason,
        CancellationToken cancellationToken = default) =>
        await ExecuteCommandAsync(
            id,
            async actor =>
            {
                var normalized = new string((vehicleRegistration ?? string.Empty)
                    .ToUpperInvariant()
                    .Where(character => char.IsAsciiLetterUpper(character) || char.IsAsciiDigit(character))
                    .ToArray());
                var origin = await imageIntakeOriginResolver.ResolveOriginAsync(id, cancellationToken)
                    ?? throw new InvalidOperationException(
                        "The intake receipt has no completed evaluation to register from.");
                var record = await registerImageIntake.ExecuteAsync(
                    new(origin, normalized, actor, operationKey, reason),
                    cancellationToken);
                await ConfirmMatchingSuggestionsAsync(record, actor, cancellationToken);
            },
            "The Image intake was registered with its permanent reference.",
            cancellationToken);

    public async Task<IActionResult> OnPostDismissSuggestionAsync(
        Guid id,
        Guid suggestionId,
        string operationKey,
        string reason,
        CancellationToken cancellationToken = default) =>
        await ExecuteCommandAsync(
            id,
            actor => vrmSuggestionStore.SetDispositionAsync(
                new(
                    suggestionId,
                    ImageVrmSuggestionDisposition.Dismissed,
                    actor,
                    reason,
                    operationKey),
                cancellationToken),
            "The registration suggestion was dismissed with the recorded reason.",
            cancellationToken);

    private async Task ConfirmMatchingSuggestionsAsync(
        ImageIntakeRecord record,
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        var suggestions = await vrmSuggestionStore.ListForReceiptAsync(
            record.Origin.ReceiptId,
            cancellationToken);
        foreach (var suggestion in suggestions)
        {
            if (suggestion.Disposition != ImageVrmSuggestionDisposition.Pending
                || suggestion.Outcome != VrmRecognitionOutcomeKind.Suggested
                || !string.Equals(
                    suggestion.SuggestedRegistration,
                    record.NormalizedVehicleRegistration,
                    StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                await vrmSuggestionStore.SetDispositionAsync(
                    new(
                        suggestion.Id,
                        ImageVrmSuggestionDisposition.Confirmed,
                        actor,
                        "The staff registration used this suggested registration.",
                        $"vrm-confirm:{suggestion.Id:N}"),
                    cancellationToken);
            }
            catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
            {
                // Confirmation is bookkeeping over an already-recorded
                // suggestion; the registration itself has already committed.
            }
        }
    }

    public static string SuggestionOutcomeLabel(ImageVrmSuggestion suggestion) => suggestion.Outcome switch
    {
        VrmRecognitionOutcomeKind.Suggested =>
            $"Suggested {suggestion.SuggestedRegistration} ({suggestion.Confidence:P0} confidence)",
        VrmRecognitionOutcomeKind.NoReadableResult => "No readable registration",
        VrmRecognitionOutcomeKind.TechnicalFailure => "Technical failure",
        VrmRecognitionOutcomeKind.Unavailable => "Recognition unavailable",
        _ => throw new InvalidOperationException(
            $"Unknown recognition outcome value '{(int)suggestion.Outcome}'.")
    };

    [LoggerMessage(
        EventId = 1200,
        Level = LogLevel.Warning,
        Message = "Case acceptance exhausted the identity sequence for intake receipt {ReceiptId}.")]
    private static partial void LogIdentitySequenceExhausted(
        ILogger logger,
        Guid receiptId,
        Exception exception);

    [LoggerMessage(
        EventId = 1201,
        Level = LogLevel.Warning,
        Message = "Case acceptance failed closed for intake receipt {ReceiptId}.")]
    private static partial void LogCaseAcceptanceFailed(
        ILogger logger,
        Guid receiptId,
        Exception exception);

    [LoggerMessage(
        EventId = 1202,
        Level = LogLevel.Warning,
        Message = "Inspection-address resolution failed closed for intake receipt {ReceiptId}.")]
    private static partial void LogAddressResolutionFailed(
        ILogger logger,
        Guid receiptId,
        Exception exception);

    [LoggerMessage(
        EventId = 1203,
        Level = LogLevel.Warning,
        Message = "Intake command failed closed for intake receipt {ReceiptId}.")]
    private static partial void LogIntakeCommandFailed(
        ILogger logger,
        Guid receiptId,
        Exception exception);

}
