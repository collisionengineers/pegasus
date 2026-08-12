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
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Intake;

public sealed partial class DetailsModel(
    IGetIntake getIntake,
    IResolveIntake resolveIntake,
    IAllocateIntake allocateIntake,
    IReevaluateIntake reevaluateIntake,
    ILinkIntake linkIntake,
    IReverseIntakeLink reverseIntakeLink,
    IAcquireCaseEditLease acquireCaseEditLease,
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

    public StandaloneAuditEvidence? ConfirmedStandaloneAuditEvidence { get; private set; }

    /// <summary>
    /// The eleven instruction values, for the correction form this screen still
    /// offers a blocked item.
    /// </summary>
    public InstructionDraftFieldsView DraftFields => new(
        Receipt.InstructionDraft,
        Receipt.Fields,
        IncludePrincipalCode: true,
        IncludeInspectionAddress: true);

    /// <summary>
    /// Whether this item can still be turned into a case, and so whether the
    /// screen offers the link to where that happens.
    /// </summary>
    public bool CanCreateCase =>
        Receipt.AcceptedCaseId is null
        && Receipt.AllocationState is null
        && IntakeDecisionPolicy.CanBecomeCase(Receipt.Decision);

    public bool CanRetryAllocation => Receipt.AllocationState?.CanRetry == true;

    /// <summary>
    /// The inspection-address state in words. The enum name was printed here
    /// verbatim, which the operator notes forbid.
    /// </summary>
    public static string AddressStateLabel(InspectionAddressResolutionState state) => state switch
    {
        InspectionAddressResolutionState.Unresolved => "Not found",
        InspectionAddressResolutionState.Suggested => "Found in the document",
        InspectionAddressResolutionState.Accepted => "Confirmed",
        InspectionAddressResolutionState.Corrected => "Corrected by staff",
        InspectionAddressResolutionState.Supplied => "Entered by staff",
        _ => throw new InvalidOperationException(
            $"Unknown inspection-address resolution state '{(int)state}'.")
    };

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
        RestoreCaseLease();
        return Page();
    }

    public async Task<IActionResult> OnPostRetryAllocationAsync(
        Guid id,
        long expectedVersion,
        Guid expectedAttemptId,
        string operationKey,
        string reason,
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
            var result = await allocateIntake.RetryAsync(
                new(id, expectedVersion, expectedAttemptId, actor, operationKey, reason),
                cancellationToken);
            if (result.State.Status == IntakeAllocationProjectionStatus.Succeeded
                && result.State.CaseId is { } caseId)
            {
                TempData["CaseDetailsStatus"] =
                    $"Case {result.State.AuditReference ?? result.State.CaseReference} was created.";
                return RedirectToPage("/Cases/Details", new { id = caseId });
            }

            TempData["IntakeDetailsError"] = result.State.SafeReason
                ?? "The case could not be created. No reference was allocated.";
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or IntakeAllocationConcurrencyException
                or IntakeAllocationOperationConflictException)
        {
            LogIntakeCommandFailed(logger, id, exception);
            TempData["IntakeDetailsError"] =
                "The receipt or allocation state changed. Reload it before retrying.";
        }

        return RedirectToPage("/Intake/Details", new { id });
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

    public int DuplicateOccurrenceCount(IntakeAssetRecord asset) =>
        Receipt.AssetRecords.Count(candidate => candidate.ContentHash == asset.ContentHash);


    public static string DecisionLabel(IntakeDecision decision) => decision switch
    {
        IntakeDecision.CaseCreated => "Ready for case allocation",
        IntakeDecision.NeedsSorting => "Needs sorting",
        // Kept identical to the list label: one decision, one name.
        IntakeDecision.BlockedIntake => "Blocked",
        IntakeDecision.Unsupported => "Unsupported",
        IntakeDecision.OcrRequired => "Document text required",
        IntakeDecision.TechnicalFailure => "Technical failure",
        IntakeDecision.ImageIntakeRegistered => "Vehicle images registered",
        _ => throw new InvalidOperationException($"Unknown intake decision value '{(int)decision}'.")
    };

    public static string SourceChannelLabel(IntakeSourceChannel channel) => channel switch
    {
        IntakeSourceChannel.ManualUpload => "Manual upload",
        IntakeSourceChannel.Mailbox => "Approved inbox",
        IntakeSourceChannel.Automation => "Automation",
        _ => throw new InvalidOperationException($"Unknown intake source channel value '{(int)channel}'.")
    };

    public static string CaseTypeLabel(CaseType? caseType) => caseType switch
    {
        CaseType.Inspection => "Inspection",
        CaseType.Audit => "Audit",
        CaseType.InspectionAndAudit => "Inspection and Audit",
        _ => "Not available"
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
        EventId = 1203,
        Level = LogLevel.Warning,
        Message = "Intake command failed closed for intake receipt {ReceiptId}.")]
    private static partial void LogIntakeCommandFailed(
        ILogger logger,
        Guid receiptId,
        Exception exception);

}
