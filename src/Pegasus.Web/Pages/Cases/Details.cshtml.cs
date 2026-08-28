using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Actors;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Eva;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Pages.Cases;

[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed partial class DetailsModel(
    IGetCase getCase,
    IGetAssessmentAccess getAssessmentAccess,
    IAcquireCaseEditLease acquireLease,
    IRenewCaseEditLease renewLease,
    IHeartbeatCaseEditLease heartbeatLease,
    IReleaseCaseEditLease releaseLease,
    IConfirmCompleteness confirmCompleteness,
    ISaveCase saveCase,
    IImageIntakeQueries imageIntakeQueries,
    ICaseEvidenceImageQueries caseEvidenceImageQueries,
    IDescribeCaseEditAuthorityHolder describeEditAuthorityHolder,
    IStaffAccountQueries staffAccountQueries,
    IEvaSubmissionModeStore evaModeStore,
    IEvaSubmissionQueries evaSubmissionQueries,
    TimeProvider timeProvider,
    ILogger<DetailsModel> logger,
    ISubmitCaseToEva? submitCaseToEva = null) : CaseMutationPageModel(logger)
{
    public IReadOnlyList<ImageIntakeSummary> ImageIntakes { get; private set; } = [];

    /// <summary>
    /// The instruction receipts' evidence photographs (attached image files
    /// and embedded PDF photos), selected by the one Core rule.
    /// </summary>
    public IReadOnlyList<CaseEvidenceImage> EvidenceImages { get; private set; } = [];

    /// <summary>
    /// The gallery entries for each associated Image-initiated Case, loaded
    /// only when the Evidence tab is the one being rendered.
    /// </summary>
    public IReadOnlyDictionary<Guid, IReadOnlyList<ImageIntakeImage>> ImagesByIntake { get; private set; } =
        new Dictionary<Guid, IReadOnlyList<ImageIntakeImage>>();

    /// <summary>
    /// Which section of the case workspace is open.
    /// </summary>
    /// <remarks>
    /// The sections are alternatives, not a reading order, so they are one
    /// query-selectable surface rather than panels stacked down the page. The
    /// section is in the query string and the panels are server-rendered, so
    /// the screen works with no script and every section is linkable.
    /// </remarks>
    [BindProperty(SupportsGet = true, Name = "section")]
    public string? SectionFilter { get; set; }

    public string Section => SectionFilter?.ToLowerInvariant() switch
    {
        "vehicle" => "vehicle",
        "valuations" => "valuations",
        "inspection-address" => "inspection-address",
        "case-files" => "case-files",
        "notes" => "notes",
        _ => "overview"
    };

    /// <summary>
    /// Whether the case has been exported as an EVA bundle at least once,
    /// read from the history event the export itself writes.
    /// </summary>
    public bool HasExportedBundle { get; private set; }

    /// <summary>
    /// The assigned Engineer's operator-facing name, resolved through the one
    /// staff-account query; null while no Engineer is assigned.
    /// </summary>
    public string? EngineerDisplayName { get; private set; }

    /// <summary>
    /// The named Engineer accounts offered by the EVA handoff dialog, loaded
    /// only while the case is in Review (the only state that hands off).
    /// </summary>
    public IReadOnlyList<EngineerOption> EngineerOptions { get; private set; } = [];

    /// <summary>
    /// Whether the EVA handoff dialog offers the API submission: the host
    /// composed a transport, the principal enabled manual submission, and the
    /// case has not already reached EVA.
    /// </summary>
    public bool CanSubmitToEva { get; private set; }

    public sealed record EngineerOption(Guid Id, string Name);

    /// <summary>
    /// One outstanding requirement on the case: the completeness flags the
    /// case's own projection reports as unmet, each with the missing-material
    /// reason the due-work schedule carries. The flags are Core's; this only
    /// names the ones that are false.
    /// </summary>
    public IReadOnlyList<CaseRequirement> OutstandingRequirements
    {
        get
        {
            if (Case?.Data is not { } data)
            {
                return [];
            }

            var why = Case.Workflow.DueWork is { } dueWork
                ? Pegasus.Web.Presentation.OperatorLabels.ChaseReason(dueWork.MissingMaterialReason)
                : null;
            List<CaseRequirement> requirements = [];
            AddRequirement(
                requirements,
                data.Completeness.Values.InstructionComplete,
                "Instructions incomplete",
                why);
            AddRequirement(
                requirements,
                data.Completeness.Values.ImagesComplete,
                "Images incomplete",
                why);
            AddRequirement(
                requirements,
                data.Completeness.Values.InstructionConfirmedByStaff,
                "Instructions not staff-reviewed",
                why);
            AddRequirement(
                requirements,
                data.Completeness.Values.ImagesConfirmedByStaff,
                "Images not staff-reviewed",
                why);
            return requirements;
        }
    }

    public sealed record CaseRequirement(string Title, string Source, string? Why);

    private static void AddRequirement(
        List<CaseRequirement> requirements,
        bool satisfied,
        string title,
        string? why)
    {
        if (!satisfied)
        {
            requirements.Add(new CaseRequirement(title, "Instruction completeness", why));
        }
    }


    public CaseDetails? Case { get; private set; }

    public bool CanOpenAssessment { get; private set; }

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

    public string RenewLeaseOperationKey { get; private set; } = NewOperationKey();

    public DateTimeOffset ManualChaseAttemptedAtUtc { get; private set; }

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
            CanOpenAssessment = (await getAssessmentAccess.ExecuteAsync(
                new(id, actor),
                cancellationToken))?.CanOpen == true;
            ImageIntakes = await imageIntakeQueries.ListForCaseAsync(id, cancellationToken);
            EvidenceImages = await caseEvidenceImageQueries.ListForCaseAsync(id, cancellationToken);
            if (Section == "case-files")
            {
                var imagesByIntake = new Dictionary<Guid, IReadOnlyList<ImageIntakeImage>>();
                foreach (var intake in ImageIntakes)
                {
                    imagesByIntake[intake.Id] = await imageIntakeQueries.ListImagesAsync(
                        intake.Id,
                        cancellationToken);
                }
                ImagesByIntake = imagesByIntake;
            }
            await DescribeWorkspaceExtrasAsync(cancellationToken);
            RestoreLeaseState(id, actor, Case.ActiveEditLease);
            if (LeaseToken is not null)
            {
                // Only this page renders a manual renew control, so only it needs that key.
                RenewLeaseOperationKey = GetOrCreateOperationKey(RenewLeaseOperationKeyName);
            }
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

    public Task<IActionResult> OnPostClaimLeaseAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        CancellationToken cancellationToken) =>
        ClaimLeaseAsync(
            acquireLease,
            id,
            expectedVersion,
            operationKey,
            () => RedirectToDetails(id),
            cancellationToken);

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
            TempData["CaseStatus"] = "Edit mode was renewed.";
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

    public Task<IActionResult> OnPostHeartbeatLeaseAsync(
        Guid id,
        string editLeaseToken,
        CancellationToken cancellationToken) =>
        HeartbeatLeaseAsync(heartbeatLease, id, editLeaseToken, cancellationToken);

    public Task<IActionResult> OnPostReleaseLeaseAsync(
        Guid id,
        string operationKey,
        string editLeaseToken,
        CancellationToken cancellationToken) =>
        ReleaseLeaseAsync(
            releaseLease,
            id,
            operationKey,
            editLeaseToken,
            () => RedirectToDetails(id),
            cancellationToken);

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
            "Case data was saved. The case is Not ready until completeness is confirmed again.");

    private async Task DescribeEditAuthorityHolderAsync(
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        if (Case?.ActiveEditLease is not { } activeLease)
        {
            return;
        }

        ViewerHoldsEditAuthority = CaseEditAuthority.IsHolder(
            activeLease.HolderKind,
            activeLease.Holder,
            actor);
        EditAuthorityHolder = ViewerHoldsEditAuthority
            ? CaseEditAuthorityHolder.Unnamed
            : await describeEditAuthorityHolder.ExecuteAsync(
                activeLease.HolderKind,
                activeLease.Holder,
                actor,
                cancellationToken);
    }

    /// <summary>
    /// The values the workspace frame names that the case projection does not
    /// carry directly: whether an EVA bundle export has ever happened, the
    /// assigned Engineer's account name, and — only in Review, the one state
    /// that hands off — the named Engineer accounts and whether the API
    /// submission is a route this principal allows.
    /// </summary>
    private async Task DescribeWorkspaceExtrasAsync(CancellationToken cancellationToken)
    {
        if (Case is not { Workflow: var workflow } details)
        {
            return;
        }

        HasExportedBundle = details.History.Any(entry =>
            string.Equals(
                entry.EventType,
                EvaHandoffPolicy.BundleExportedHistoryEventKind,
                StringComparison.Ordinal));

        if (workflow.AssignedEngineerId is { } engineerId)
        {
            var account = await staffAccountQueries.GetAsync(engineerId, cancellationToken);
            EngineerDisplayName = account?.UserName ?? ActorDisplayNames.UnknownStaff;
        }

        if (workflow.State != CaseLifecycleState.Review)
        {
            return;
        }

        var accounts = await staffAccountQueries.ListAsync(0, 100, cancellationToken);
        EngineerOptions = accounts.Accounts
            .Where(account => account.IsEnabled && account.Roles.Contains(StaffRole.Engineer))
            .Select(account => new EngineerOption(account.Id, account.UserName))
            .ToArray();

        var latestSubmission = await evaSubmissionQueries.GetLatestAsync(
            workflow.CaseId,
            cancellationToken);
        var modes = submitCaseToEva is null
            ? EvaSubmissionModes.Disabled
            : await evaModeStore.GetForPrincipalAsync(
                workflow.Identity.PrincipalCode,
                cancellationToken);
        // Delivered, not merely succeeded: an instruction EVA accepted without
        // returning an identifier still created a claim, and offering the
        // button again would create a second one no API call can withdraw.
        CanSubmitToEva = EvaSubmissionPolicy.AllowsManualSubmission(modes)
            && latestSubmission is not { IsDelivered: true };
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
