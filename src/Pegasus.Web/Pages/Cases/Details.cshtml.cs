using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Workflow;

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
    IImageIntakeQueries imageIntakeQueries,
    IDescribeCaseEditAuthorityHolder describeEditAuthorityHolder,
    TimeProvider timeProvider,
    ILogger<DetailsModel> logger) : CaseMutationPageModel(logger)
{
    public IReadOnlyList<ImageIntakeSummary> ImageIntakes { get; private set; } = [];

    /// <summary>
    /// Which section of the case container is open.
    /// </summary>
    /// <remarks>
    /// Overview, Evidence and History are alternatives, not a reading order,
    /// so they are tabs rather than panels stacked down the page. The tab is
    /// in the query string and the panels are server-rendered, so the screen
    /// works with no script and every section is linkable.
    /// </remarks>
    [BindProperty(SupportsGet = true, Name = "tab")]
    public string? TabFilter { get; set; }

    public string Tab => TabFilter?.ToLowerInvariant() switch
    {
        "evidence" => "evidence",
        "history" => "history",
        _ => "overview"
    };

    /// <summary>
    /// Everything the case carries: files, vehicle images and linked e-mail.
    /// </summary>
    public int EvidenceCount =>
        (Case?.Documents.Count ?? 0) + ImageIntakes.Count;

    public CaseDetails? Case { get; private set; }

    /// <summary>
    /// The values a refused editor submitted, held for comparison against the values the case now
    /// holds. There is no control that applies, merges, or forces them: the only way forward is to
    /// enter edit mode again and retype.
    /// </summary>
    public IReadOnlyList<ProposedCaseValue> ProposedValues { get; private set; } = [];

    public bool ProposedValuesWereDropped { get; private set; }

    public bool ProposedValuesWereShortened { get; private set; }

    /// <summary>
    /// Who holds edit authority, as an operator may see them. Null when nobody is editing; a
    /// holder whose account cannot be resolved is still disclosed, without an identifier.
    /// </summary>
    public CaseEditAuthorityHolder? EditAuthorityHolder { get; private set; }

    public bool ViewerHoldsEditAuthority { get; private set; }

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
            RestoreLeaseState(id, actor);
            RestoreProposedValues(id);
            await DescribeEditAuthorityHolderAsync(actor, cancellationToken);
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

    private async Task DescribeEditAuthorityHolderAsync(
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        if (Case?.ActiveEditLease is not { } activeLease)
        {
            return;
        }

        ViewerHoldsEditAuthority = string.Equals(
            activeLease.Holder,
            actor.SubjectId,
            StringComparison.Ordinal);
        EditAuthorityHolder = ViewerHoldsEditAuthority
            ? CaseEditAuthorityHolder.Unnamed
            : await describeEditAuthorityHolder.ExecuteAsync(
                activeLease.Holder,
                actor,
                cancellationToken);
    }

    /// <summary>
    /// Reads the retained values only for the case they were submitted against. A refusal on one
    /// case survives a visit to another, so nothing is consumed until it belongs to this page.
    /// </summary>
    private void RestoreProposedValues(Guid caseId)
    {
        if (PeekGuid(ProposedValuesCaseIdKey) != caseId)
        {
            TempData.Keep(ProposedValuesCaseIdKey);
            TempData.Keep(ProposedValuesKey);
            TempData.Keep(ProposedValuesDroppedKey);
            TempData.Keep(ProposedValuesShortenedKey);
            return;
        }

        TempData.Remove(ProposedValuesCaseIdKey);
        var payload = TempData[ProposedValuesKey] as string;
        ProposedValuesWereDropped = TempData[ProposedValuesDroppedKey] is true;
        ProposedValuesWereShortened = TempData[ProposedValuesShortenedKey] is true;
        if (string.IsNullOrWhiteSpace(payload))
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
                    DisplayValue(value.Field, value.Value),
                    CurrentValue(value.Field)))
                .ToArray();
    }

    /// <summary>
    /// Renders a proposed checkbox value in the same words as the current one, so the two columns
    /// compare rather than reading "true" beside "Yes".
    /// </summary>
    private static string DisplayValue(string field, string value) =>
        BooleanFormFields.Contains(field)
            ? YesOrNo(string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
            : value;

    private static string YesOrNo(bool value) => value ? "Yes" : "No";

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
            "inspectionMode" => data.Inspection.Mode.Confirmed?.Value.ToString(),

            // The corrected-vehicle-suggestion form posts unprefixed names against the same case
            // fields, so the case's confirmed vehicle values are what it is compared with.
            "registration" => data.Vehicle.Registration.Confirmed?.Value,
            "make" => data.Vehicle.Make.Confirmed?.Value,
            "model" => data.Vehicle.Model.Confirmed?.Value,
            "mileage" => data.Vehicle.Mileage.Confirmed?.Value.ToString(
                CultureInfo.InvariantCulture),
            "mileageUnit" => data.Vehicle.MileageUnit.Confirmed?.Value,

            // Two handlers name the same completeness flags differently; both compare against the
            // one projected value.
            "instructionComplete" or "instructionsComplete" =>
                YesOrNo(data.Completeness.Values.InstructionComplete),
            "imagesComplete" => YesOrNo(data.Completeness.Values.ImagesComplete),
            "instructionConfirmedByStaff" or "instructionsReviewedByStaff" =>
                YesOrNo(data.Completeness.Values.InstructionConfirmedByStaff),
            "imagesConfirmedByStaff" or "imagesReviewedByStaff" =>
                YesOrNo(data.Completeness.Values.ImagesConfirmedByStaff),
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

        // The completeness flags are labelled as the form the editor was looking at labelled them.
        "instructionComplete" or "instructionsComplete" => "Instructions complete",
        "imagesComplete" => "Images complete",
        "instructionConfirmedByStaff" or "instructionsReviewedByStaff" =>
            "Instructions staff-reviewed",
        "imagesConfirmedByStaff" or "imagesReviewedByStaff" => "Images staff-reviewed",
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

    private static string RequireOperationKey(string value) =>
        Guid.TryParseExact(value, "N", out var operationId)
            ? operationId.ToString("N")
            : throw new ArgumentException("The operation key is invalid.", nameof(value));

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "The authorized case detail query failed for case {CaseId}.")]
    private static partial void LogCaseDetailsQueryFailed(
        ILogger logger,
        Guid caseId,
        Exception exception);
}

/// <summary>
/// One field of a refused submission beside the value the case now holds, for comparison only.
/// </summary>
public sealed record ProposedCaseValue(string Label, string Proposed, string? Current);
