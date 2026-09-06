using Pegasus.Core.Actors;
using Pegasus.Core.Address;
using Pegasus.Core.Identity;
using Pegasus.Core.Cases;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Triage;
using Pegasus.Core.Workflow;
using Microsoft.AspNetCore.Mvc;
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
    ICreateTriageFromIntake createTriage,
    ITriageQueries triageQueries,
    ReconcileUnidentifiedDestinations unidentifiedDestinations,
    ILogger<DetailsModel> logger,
    IInspectionAddressResolutionStore addressResolutionStore,
    IProviderInspectionModeStore providerInspectionModeStore) : StaffPageModel
{
    public ImageIntakeDetail? ImageIntake { get; private set; }

    public IReadOnlyList<ImageVrmSuggestion> VrmSuggestions { get; private set; } = [];

    public IReadOnlyList<ImageIntakeCaseCandidate> AssociationCandidates { get; private set; } = [];

    public bool CanRegisterImageIntake { get; private set; }

    public string RegistrationPrefill { get; private set; } = string.Empty;

    /// <summary>
    /// The Triage this receipt has already opened, if any — the destination
    /// panel, and the reason the action below is not offered twice.
    /// </summary>
    public TriageSummary? Triage { get; private set; }

    /// <summary>
    /// Whether staff can supply the registration this Triage request never
    /// carried and open its Triage — the staff half of the operator's Stage 0
    /// rule, "until a vehicle registration is known, then open the Triage".
    /// The automatic half runs in intake processing and is unchanged.
    /// </summary>
    public bool CanOpenTriage { get; private set; }

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
        if (!TryGetActor(out var actor))
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
            "The received item was blocked with the recorded reason.",
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
        if (!TryGetActor(out var actor))
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
            "The received item was linked to the selected case.",
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


    /// <summary>
    /// The receipt's outcome as the operator reads it. A Triage request is
    /// <see cref="IntakeDecision.NeedsSorting"/> because it is pre-case work,
    /// and unidentified material is too — but naming a Triage request
    /// "Unidentified" is the same label/reality gap INTK-033 exists to close.
    /// </summary>
    public static string DecisionLabel(IntakeReceipt receipt) =>
        receipt.MailClassificationDecision is { IsTriageRequest: true }
            ? "Triage"
            : DecisionLabel(receipt.Decision);

    public static string DecisionLabel(IntakeDecision decision) => decision switch
    {
        IntakeDecision.CaseCreated => "Ready for case allocation",
        IntakeDecision.NeedsSorting => "Unidentified",
        // Kept identical to the list label: one decision, one name.
        IntakeDecision.BlockedIntake => "Blocked",
        IntakeDecision.Unsupported => "Unsupported",
        IntakeDecision.OcrRequired => "Document text required",
        IntakeDecision.TechnicalFailure => "Technical failure",
        IntakeDecision.ImageIntakeRegistered => "Vehicle images registered",
        _ => throw new InvalidOperationException($"Unknown intake decision value '{(int)decision}'.")
    };

    public static string SourceChannelLabel(IntakeSourceChannel channel) =>
        OperatorLabels.SourceChannel(channel);

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
        if (!TryGetActor(out var actor))
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
        if (!TryGetActor(out var actor))
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
        await LoadTriageAsync(cancellationToken);
        return null;
    }

    /// <summary>
    /// A Triage only ever opens from a receipt the accepted route classified as
    /// a Triage request, so that one recorded reading answers both questions
    /// and no destination lookup is issued for the receipts — nearly all of
    /// them — that could never have one.
    /// </summary>
    private async Task LoadTriageAsync(CancellationToken cancellationToken)
    {
        if (!ProcessIntake.IsTriageRequest(Receipt))
        {
            return;
        }

        Triage = await triageQueries.GetByOriginReceiptAsync(Receipt.Id, cancellationToken);
        // The same accepted-match condition the POST handler enforces. Without
        // it the action is offered to receipts whose submission cannot succeed:
        // a reply on a Triage thread is a Triage request but deliberately
        // carries no accepted match, so it would show "Open the Triage" and
        // then refuse it (INTK-035 review).
        CanOpenTriage = Triage is null
            && Receipt.Decision == IntakeDecision.NeedsSorting
            && Receipt.Evidence.Count(
                evidence => evidence.Finding == IntakeEvidenceFinding.AcceptedTriageMatch) == 1;
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
                var normalized = ImageIntakeLifecycleRules.NormalizeRegistrationInput(vehicleRegistration);
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

    /// <summary>
    /// The operator's Stage 0 rule has a staff half: a Triage request held in
    /// Unidentified because no registration could be read is promoted by
    /// somebody supplying one. Everything else the Triage needs — the accepted
    /// route classification, its evidence, the origin identity — was already
    /// recorded when the receipt was processed.
    /// </summary>
    /// <remarks>
    /// Correcting the instruction draft is NOT the way to do this. That path
    /// rewrites the decision to CaseCreated or BlockedIntake, which sends a
    /// Triage request back into case allocation — the fault INTK-033 fixed —
    /// and breaks the deferral rule, which keys off NeedsSorting.
    ///
    /// The accepted-match evidence is passed back as the receipt's own record
    /// rather than rebuilt: the store re-checks that it is retained uniquely
    /// on the receipt by full record equality, so a reconstructed one fails
    /// closed.
    /// </remarks>
    public async Task<IActionResult> OnPostOpenTriageAsync(
        Guid id,
        string? vehicleRegistration,
        string operationKey,
        CancellationToken cancellationToken = default) =>
        await ExecuteCommandAsync(
            id,
            async actor =>
            {
                // The Triage creation request carries the typed actor but checks
                // only its kind, not this right, so the caller authorises.
                StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
                var receipt = await getIntake.ExecuteAsync(new GetIntakeQuery(id, actor), cancellationToken)
                    ?? throw new KeyNotFoundException($"Intake receipt '{id}' was not found.");
                var acceptedMatch = receipt.Evidence.SingleOrDefault(
                    evidence => evidence.Finding == IntakeEvidenceFinding.AcceptedTriageMatch)
                    ?? throw new InvalidOperationException(
                        "The receipt does not carry exactly one accepted Triage-match record.");
                var origin = await imageIntakeOriginResolver.ResolveOriginAsync(id, cancellationToken)
                    ?? throw new InvalidOperationException(
                        "The intake receipt has no completed evaluation to open a Triage from.");
                await createTriage.ExecuteAsync(
                    new(
                        new TriageOrigin(
                            origin.ReceiptId,
                            origin.SourceIdentity,
                            origin.SourceHash,
                            origin.EvaluationRevisionId),
                        ImageIntakeLifecycleRules.NormalizeRegistrationInput(vehicleRegistration),
                        acceptedMatch,
                        actor,
                        $"triage-from-staff:{operationKey}"),
                    cancellationToken);
                await CloseUnidentifiedForTriageAsync(receipt, cancellationToken);
            },
            "The Triage was opened.",
            cancellationToken);

    /// <summary>
    /// Closes the receipt's open Unidentified item against the Triage that now
    /// exists, through the one owner of that supersession rule. The receipt's
    /// own processing pass registers the item and returns without reconciling,
    /// so nothing else closes it until the periodic sweep runs — which is the
    /// backstop if this advisory write fails, exactly as the suggestion
    /// bookkeeping below treats a failure after a committed write.
    /// </summary>
    private async Task CloseUnidentifiedForTriageAsync(
        IntakeReceipt receipt,
        CancellationToken cancellationToken)
    {
        try
        {
            await unidentifiedDestinations.ResolveForReceiptAsync(receipt, cancellationToken);
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            LogIntakeCommandFailed(logger, receipt.Id, exception);
        }
    }

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
