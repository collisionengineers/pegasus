using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.AiWork;
using Pegasus.Core.Actors;
using Pegasus.Core.Address;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Eva;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Operations;
using Pegasus.Core.Reports;
using Pegasus.Core.Vehicle;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Assessment;
using Labels = Pegasus.Web.Presentation.OperatorLabels;

namespace Pegasus.Web.Pages.Cases;

[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed partial class DetailsModel(
    IGetCase getCase,
    IGetAssessmentAccess getAssessmentAccess,
    IGetAssessmentWorkspace getAssessmentWorkspace,
    ICreateAiJob createAiJob,
    ISendToAiControl sendToAiControl,
    GenerateCaseAssessmentReportDraft generateReportDraft,
    IGenerateCaseReport generateReport,
    IGeneratedCaseArtifactStore generatedArtifacts,
    ICaseReportGenerationStore reportGenerations,
    IPrepareCaseReportDelivery prepareReportDelivery,
    ISendPreparedCaseReport sendPreparedReport,
    ICaseReportDeliveryPreparationStore deliveryPreparations,
    IListCaseEstimates listEstimates,
    ISaveEstimate saveEstimate,
    IDuplicateEstimate duplicateEstimate,
    IDiscardEstimate discardEstimate,
    ISetCurrentEstimate setCurrentEstimate,
    IRepairSpecificationStore repairSpecifications,
    IEstimateDocumentParser estimateParser,
    JsonEstimateParser jsonEstimateParser,
    IAddCaseDocument addCaseDocument,
    IAcquireCaseEditLease acquireLease,
    IRenewCaseEditLease renewLease,
    IHeartbeatCaseEditLease heartbeatLease,
    IReleaseCaseEditLease releaseLease,
    IConfirmCompleteness confirmCompleteness,
    ISaveCase saveCase,
    IInspectionAddressChoicesQueries inspectionAddressChoicesQueries,
    IImageIntakeQueries imageIntakeQueries,
    ICaseEvidenceImageQueries caseEvidenceImageQueries,
    IListCaseValuations listCaseValuations,
    ISaveValuation saveValuation,
    IEngineerNoteQueries engineerNoteQueries,
    IAddEngineerNote addEngineerNote,
    IDescribeCaseEditAuthorityHolder describeEditAuthorityHolder,
    IStaffAccountQueries staffAccountQueries,
    IEvaSubmissionModeStore evaModeStore,
    TimeProvider timeProvider,
    ILogger<DetailsModel> logger,
    ISubmitCaseToEva? submitCaseToEva = null) : CaseMutationPageModel(logger)
{
    /// <summary>
    /// The Case's recorded valuation source cards (B01 port/B03): one card
    /// per source with its figures, loaded with the valuation section.
    /// </summary>
    public IReadOnlyList<CaseValuation> Valuations { get; private set; } = [];

    public IReadOnlyList<InspectionAddressChoice> InspectionAddressChoices { get; private set; } = [];

    public IReadOnlyList<ImageIntakeSummary> ImageIntakes { get; private set; } = [];

    /// <summary>
    /// The instruction receipts' evidence photographs (attached image files
    /// and embedded PDF photos), selected by the one Core rule.
    /// </summary>
    public IReadOnlyList<CaseEvidenceImage> EvidenceImages { get; private set; } = [];

    /// <summary>
    /// The gallery entries for each associated Image-initiated Case, loaded
    /// only when the Files section's body is being rendered.
    /// </summary>
    public IReadOnlyDictionary<Guid, IReadOnlyList<ImageIntakeImage>> ImagesByIntake { get; private set; } =
        new Dictionary<Guid, IReadOnlyList<ImageIntakeImage>>();

    public IReadOnlyList<EngineerNoteDisplay> EngineerNotes { get; private set; } = [];

    public sealed record EngineerNoteDisplay(
        string RecordedBy,
        string Note,
        DateTimeOffset RecordedAtUtc);

    /// <summary>
    /// Which section of the Case record the request addresses.
    /// </summary>
    /// <remarks>
    /// The record is one scrolling page (D29), so the section is a jump
    /// target rather than an alternative: it is rendered server-side on the
    /// first response and the jump-nav scrolls to it. The vocabulary is the
    /// eleven keys of <see cref="Labels.CaseWorkspace.Sections"/>; a value
    /// the record does not own selects Overview.
    /// </remarks>
    [BindProperty(SupportsGet = true, Name = "section")]
    public string? SectionFilter { get; set; }

    public string Section => NormalizeSection(SectionFilter);

    private static string NormalizeSection(string? value)
    {
        var key = value?.Trim().ToLowerInvariant();
        return Labels.CaseWorkspace.Sections.Any(section =>
            string.Equals(section.Key, key, StringComparison.Ordinal))
            ? key!
            : Labels.CaseWorkspace.DefaultSectionKey;
    }

    /// <summary>
    /// The sections whose body is fetched as a fragment when it approaches the
    /// viewport, and the view that renders each. Only sections that have a
    /// body below the fold are here: the first three are always rendered, and
    /// a section whose body its owning lane has not built yet is a heading the
    /// frame renders itself.
    /// </summary>
    private static readonly Dictionary<string, string> LazySectionViews =
        new(StringComparer.Ordinal)
        {
            ["engineer-notes"] = "/Pages/Cases/Shared/_CaseEngineerNotes.cshtml",
            ["vehicle"] = "/Pages/Cases/Shared/_CaseVehicle.cshtml",
            ["valuation"] = "/Pages/Cases/Shared/_CaseValuation.cshtml",
            ["files"] = "/Pages/Cases/Shared/_CaseFiles.cshtml",
            ["notes"] = "/Pages/Cases/Shared/_CaseHistory.cshtml"
        };

    /// <summary>
    /// Whether <paramref name="key"/> is fetched rather than rendered with the
    /// first response. The addressed section is always rendered, so
    /// <c>?section=</c> works over plain HTTP; while the viewer holds the edit
    /// lease nothing is deferred at all, so unsaved input can never be
    /// replaced by a mounting body.
    /// </summary>
    public bool SectionIsDeferred(string key) =>
        LeaseToken is null
        && !string.Equals(key, Section, StringComparison.Ordinal)
        && LazySectionViews.ContainsKey(key);

    /// <summary>
    /// The assigned Engineer's operator-facing name, resolved through the one
    /// staff-account query; null while no Engineer is assigned.
    /// </summary>
    public string? EngineerDisplayName { get; private set; }

    public string SignOffEngineerDisplayName { get; private set; } = Labels.CaseWorkspace.Unassigned;

    public EvaHandoffViewModel? EvaHandoff { get; private set; }

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

    /// <summary>
    /// Whether the Engineer sections are read-only: the one Core access rule
    /// (Complete only), read by ENG-034's Engineer forms. The record has no
    /// Open Assessment action and no section visibility gate (D30). An
    /// unresolved access answer reads as read-only.
    /// </summary>
    public bool AssessmentIsReadOnly { get; private set; }

    /// <summary>
    /// D11: whether GuardEstimateEditAsync/OnPostImportEstimateAsync will
    /// accept a mutation right now. Unresolved access fails closed to false,
    /// the same direction as AssessmentIsReadOnly.
    /// </summary>
    public bool AssessmentCanOpen { get; private set; }

    private const long MaximumEstimateUploadBytes = 10 * 1024 * 1024;
    private const string NotInEditMode = "Enter edit mode to change the assessment.";

    public CaseAssessmentProjection? Assessment { get; private set; }

    public RepairSpecificationVersion? AcceptedSpecification { get; private set; }

    public IReadOnlyList<RepairSpecificationVersion> Estimates { get; private set; } = [];

    public RepairSpecificationVersion? SelectedEstimate { get; private set; }

    public bool EditingNewEstimate { get; private set; }

    public EstimateDetails? EditorDetails { get; private set; }

    public IReadOnlyList<EstimateEditorLine> EditorLines { get; private set; } = [];

    public bool ActorIsEngineer { get; private set; }

    public bool CaseIsArchived => Case?.Workflow.Archive is not null;

    public bool SelectedEstimateIsEditable =>
        !AssessmentIsReadOnly
        && AssessmentCanOpen
        && ActorIsEngineer
        && (EditingNewEstimate || SelectedEstimate?.State == RepairSpecificationState.Draft);

    public bool SelectedEstimateCanBeDuplicated =>
        !AssessmentIsReadOnly
        && AssessmentCanOpen
        && ActorIsEngineer
        && SelectedEstimate is { State: not RepairSpecificationState.Discarded };

    public bool SelectedEstimateCanBeCurrent =>
        !AssessmentIsReadOnly
        && AssessmentCanOpen
        && ActorIsEngineer
        && SelectedEstimate is { IsCurrent: false }
        && (SelectedEstimate.State == RepairSpecificationState.Draft
            || SelectedEstimate.State == RepairSpecificationState.Accepted);

    public EstimateTotals EditorTotals
    {
        get
        {
            var details = EditorDetails ?? SelectedEstimate?.Details
                ?? new EstimateDetails(
                    Name: Labels.CaseWorkspace.EngineerSections.Estimate,
                    RepairDays: null,
                    LabourRate: null,
                    PaintLabourRate: null,
                    PaintMaterials: null,
                    OtherCosts: null,
                    VatPercent: EstimatePolicy.DefaultVatPercent,
                    Notes: null);
            return EstimateTotals.Compute(new(
                SelectedEstimate?.SpecificationId ?? Guid.Empty,
                SelectedEstimate?.CaseId ?? Guid.Empty,
                SelectedEstimate?.Version ?? 1,
                RepairSpecificationState.Draft,
                SelectedEstimate?.Source ?? new(RepairSpecificationSourceRoute.Manual, null, null, null),
                [.. EditorLines.Select((line, index) => new CaseEstimateLineRecord(
                    Guid.Empty,
                    index + 1,
                    EstimateOperations.TryParse(line.Operation, out var operation)
                        ? EstimateOperations.ToLineType(operation)
                        : "specialist_fixed",
                    null,
                    line.Description,
                    ParseNumber(line.LabourHours),
                    ParseNumber(line.PartPounds),
                    false,
                    line.PartNumber,
                    null,
                    null,
                    null,
                    null,
                    ActorKind.Staff,
                    SelectedEstimate?.CreatedBy ?? string.Empty,
                    DateTimeOffset.UtcNow,
                    null,
                    null,
                    ParseNumber(line.PaintHours),
                    string.IsNullOrWhiteSpace(line.Quantity)
                        ? null
                        : (int?)ParseNumber(line.Quantity)))],
                null,
                SelectedEstimate?.CreatedBy ?? string.Empty,
                SelectedEstimate?.CreatedAtUtc ?? DateTimeOffset.UtcNow,
                null,
                null,
                null,
                null,
                details,
                SelectedEstimate?.IsCurrent ?? false,
                SelectedEstimate?.AiJobId,
                SelectedEstimate?.DiscardReason));
        }
    }

    public decimal? EngineerValue =>
        Assessment?.Field(AssessmentVocabulary.ValueEngineer) is { IsConfirmed: true } engineerValue
            && decimal.TryParse(
                engineerValue.Value,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var parsed)
            ? parsed
            : null;

    public string AssessmentValue(string path) =>
        Assessment?.Field(path)?.Value is { } value && !string.IsNullOrWhiteSpace(value)
            ? value
            : Labels.CaseWorkspace.AbsentValue;

    public string? ImportCondition =>
        AssessmentIsReadOnly
            ? Labels.CaseWorkspace.EngineerSections.ReadOnlyOnceComplete
            : !ActorIsEngineer
                ? Labels.CaseWorkspace.EngineerSections.EngineerOnlyImport
                : null;

    public string? SendToClaudeCondition { get; private set; }

    public AssessmentReportDraftPreparation? ReportDraftPreparation { get; private set; }

    public string? ReportDraftCondition { get; private set; }

    public bool ReportDraftNotReady =>
        ReportDraftPreparation is { CanGenerate: false }
        && ReportDraftReasons.Count > 0;

    public IReadOnlyList<AssessmentReadinessItem> ReportDraftReasons =>
        ReportDraftPreparation?.Reasons ?? [];

    /// <summary>
    /// The Case's current generated report snapshot (B05): the newest
    /// generation that no later material change has superseded, with every
    /// artifact it was asked for. Null until the first generation.
    /// </summary>
    public CaseReportGenerationRecord? CurrentReportGeneration { get; private set; }

    /// <summary>
    /// The current generation's latest delivery preparation (B07), if one
    /// exists. Recipient facts are read from the structured contacts; the
    /// operator never types an address into the Report section.
    /// </summary>
    public CaseReportDeliveryPreparationRecord? CurrentDeliveryPreparation { get; private set; }

    public string? OpenDialog { get; private set; }

    public string ImportOperationKey { get; private set; } = NewOperationKey();

    public string SaveEstimateOperationKey { get; private set; } = NewOperationKey();

    public string DuplicateOperationKey { get; private set; } = NewOperationKey();

    public string DiscardOperationKey { get; private set; } = NewOperationKey();

    public string UseEstimateOperationKey { get; private set; } = NewOperationKey();

    public string SendOperationKey { get; private set; } = NewOperationKey();

    public string ReportDraftOperationKey { get; private set; } = NewOperationKey();

    public string GenerateReportOperationKey { get; private set; } = NewOperationKey();

    public string PrepareDeliveryOperationKey { get; private set; } = NewOperationKey();

    private static decimal? ParseNumber(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : decimal.TryParse(value.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;

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

    public async Task<IActionResult> OnGetAsync(
        Guid id,
        string? estimate,
        string? dialog,
        CancellationToken cancellationToken)
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
            // No access answer is not an editable record: an unresolved
            // result fails closed to read-only, the same direction the
            // pre-case gates fail.
            var assessmentAccess = await getAssessmentAccess.ExecuteAsync(
                new(id, actor),
                cancellationToken);
            AssessmentIsReadOnly = assessmentAccess?.IsReadOnly ?? true;
            AssessmentCanOpen = assessmentAccess?.CanOpen ?? false;
            await LoadEngineerSectionsAsync(id, actor, estimate, dialog, cancellationToken);
            // The lease decides how much of the record is rendered now, so it is
            // restored before the section-specific loads are chosen.
            RestoreLeaseState(id, actor, Case.ActiveEditLease);
            if (LeaseToken is not null)
            {
                // Only this page renders a manual renew control, so only it needs that key.
                RenewLeaseOperationKey = GetOrCreateOperationKey(RenewLeaseOperationKeyName);
            }
            if (!SectionIsDeferred("inspection"))
            {
                var choices = await inspectionAddressChoicesQueries.GetAsync(id, cancellationToken);
                InspectionAddressChoices = choices is null
                    ? []
                    : Pegasus.Core.Address.InspectionAddressChoices.Resolve(choices);
            }
            ImageIntakes = await imageIntakeQueries.ListForCaseAsync(id, cancellationToken);
            EvidenceImages = await caseEvidenceImageQueries.ListForCaseAsync(id, cancellationToken);
            if (!SectionIsDeferred("files"))
            {
                await LoadIntakeGalleriesAsync(cancellationToken);
            }
            if (!SectionIsDeferred("valuation"))
            {
                Valuations = await listCaseValuations.ExecuteAsync(id, cancellationToken);
            }
            if (!SectionIsDeferred("engineer-notes"))
            {
                await LoadEngineerNotesAsync(id, cancellationToken);
            }
            await DescribeWorkspaceExtrasAsync(cancellationToken);
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

    private async Task LoadEngineerSectionsAsync(
        Guid id,
        ActionActor actor,
        string? estimate,
        string? dialog,
        CancellationToken cancellationToken)
    {
        ActorIsEngineer = actor.IsInRole(StaffRole.Engineer);
        var workspace = await getAssessmentWorkspace.ExecuteAsync(new(id, actor), cancellationToken);
        if (workspace is null)
        {
            await EvaluateEngineerSectionConditionsAsync(cancellationToken);
            return;
        }

        Assessment = workspace.Assessment;
        AcceptedSpecification = workspace.AcceptedSpecification;
        Estimates = await listEstimates.ExecuteAsync(id, cancellationToken);
        ApplyEstimateSelection(estimate);
        ReportDraftPreparation = AssessmentReportProjection.Prepare(
            Assessment,
            currentEstimate: AcceptedSpecification);
        CurrentReportGeneration = await reportGenerations.GetCurrentAsync(actor, id, cancellationToken);
        CurrentDeliveryPreparation = CurrentReportGeneration is null
            ? null
            : await deliveryPreparations.GetCurrentAsync(actor, id, cancellationToken);
        await EvaluateEngineerSectionConditionsAsync(cancellationToken);
        OpenDialog = dialog switch
        {
            "import-estimate" when ImportCondition is null => "import-estimate",
            "send-to-claude" when SendToClaudeCondition is null => "send-to-claude",
            "delete-estimate" when SelectedEstimateIsEditable
                && SelectedEstimate is { IsCurrent: false } => "delete-estimate",
            _ => null
        };
    }

    private void ApplyEstimateSelection(string? estimate)
    {
        if (string.Equals(estimate, "new", StringComparison.OrdinalIgnoreCase))
        {
            EditingNewEstimate = true;
            EditorDetails = new EstimateDetails(
                Name: Labels.CaseWorkspace.EngineerSections.NewEstimate,
                RepairDays: null,
                LabourRate: null,
                PaintLabourRate: null,
                PaintMaterials: null,
                OtherCosts: null,
                VatPercent: EstimatePolicy.DefaultVatPercent,
                Notes: null);
            EditorLines = [new EstimateEditorLine("", null, null, null, null, null, null)];
            return;
        }

        SelectedEstimate = Guid.TryParse(estimate, out var estimateId)
            ? Estimates.FirstOrDefault(item => item.SpecificationId == estimateId)
            : null;
        SelectedEstimate ??= Estimates.FirstOrDefault(item => item.IsCurrent)
            ?? Estimates.MaxBy(item => item.Version);
        if (SelectedEstimate is null)
        {
            return;
        }

        EditorDetails = SelectedEstimate.Details;
        EditorLines = SelectedEstimate.Lines
            .OrderBy(line => line.Position)
            .Select(line => new EstimateEditorLine(
                EstimateOperations.FromLineType(line.Type).ToString(),
                line.Description,
                line.PartNumber,
                line.Quantity?.ToString(CultureInfo.InvariantCulture),
                line.WorkUnits?.ToString(CultureInfo.InvariantCulture),
                line.PaintWorkUnits?.ToString(CultureInfo.InvariantCulture),
                line.Price?.ToString("0.##", CultureInfo.InvariantCulture),
                line.Id))
            .ToList();
    }

    private async Task EvaluateEngineerSectionConditionsAsync(CancellationToken cancellationToken)
    {
        if (AssessmentIsReadOnly)
        {
            SendToClaudeCondition = Labels.CaseWorkspace.EngineerSections.ReadOnlyOnceComplete;
        }
        else if (!AssessmentCanOpen)
        {
            // D11: the assessment workspace has not opened yet, so
            // HasAssessmentAccessAsync will refuse the mutation the same as
            // GuardEstimateEditAsync does for the other Estimate handlers.
            SendToClaudeCondition = Labels.CaseWorkspace.EngineerSections.NotAvailableForCase;
        }
        else if (!await sendToAiControl.IsEnabledAsync(cancellationToken))
        {
            SendToClaudeCondition = Labels.CaseWorkspace.EngineerSections.SendingToAiDisabled;
        }
        else if (EngineerValue is null)
        {
            SendToClaudeCondition = Labels.CaseWorkspace.EngineerSections.ConfirmedEngineerValueRequired;
        }

        ReportDraftCondition = !AssessmentCanOpen || ReportDraftPreparation is null
            ? Labels.CaseWorkspace.EngineerSections.NotAvailableForCase
            : ReportDraftPreparation.CanGenerate
                ? null
                : Labels.CaseWorkspace.EngineerSections.NotReady;
    }

    /// <summary>
    /// One Case section's body, for the frame's lazy mount, on the record's
    /// own fragment path <c>/Cases/{id}/Section?section=&lt;key&gt;</c>. It runs the same
    /// authorized load, lease restoration and section-specific supplemental
    /// query as the full GET and returns only the named body, so a mounted
    /// section carries the same lease token and version the page holds.
    /// </summary>
    public async Task<IActionResult> OnGetSectionAsync(
        Guid id,
        string? section,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (id == Guid.Empty)
        {
            return NotFound();
        }

        var key = NormalizeSection(section);
        if (!LazySectionViews.TryGetValue(key, out var view))
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
            SectionFilter = key;
            RestoreLeaseState(id, actor, Case.ActiveEditLease);
            ImageIntakes = await imageIntakeQueries.ListForCaseAsync(id, cancellationToken);
            EvidenceImages = await caseEvidenceImageQueries.ListForCaseAsync(id, cancellationToken);
            if (key == "files")
            {
                await LoadIntakeGalleriesAsync(cancellationToken);
            }
            if (key == "valuation")
            {
                Valuations = await listCaseValuations.ExecuteAsync(id, cancellationToken);
            }
            if (key == "engineer-notes")
            {
                await LoadEngineerNotesAsync(id, cancellationToken);
            }
            await DescribeWorkspaceExtrasAsync(cancellationToken);
            return Partial(view, this);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCaseDetailsQueryFailed(logger, id, exception);
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    private async Task LoadIntakeGalleriesAsync(CancellationToken cancellationToken)
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

    private async Task LoadEngineerNotesAsync(Guid caseId, CancellationToken cancellationToken)
    {
        var notes = await engineerNoteQueries.ListNewestFirstAsync(caseId, cancellationToken);
        var names = await ActorDisplayNames.ResolveStaffNamesAsync(
            staffAccountQueries,
            notes.Select(note => note.RecordedByStaffId),
            cancellationToken);
        EngineerNotes = notes.Select(note => new EngineerNoteDisplay(
            ActorDisplayNames.Resolve(
                ActorKind.Staff,
                note.RecordedByStaffId.ToString("D"),
                names),
            note.Note,
            note.RecordedAtUtc)).ToArray();
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

    public Task<IActionResult> OnPostAddEngineerNoteAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string note,
        string editLeaseToken,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandAsync(
            id,
            editLeaseToken,
            "add_engineer_note",
            actor => addEngineerNote.ExecuteAsync(
                new(id, actor, expectedVersion, operationKey, note, editLeaseToken),
                cancellationToken),
            Labels.CaseWorkspace.EngineerNoteAdded);

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
        // CASE-027: SaveCase writes every one of CaseEditableData's members, so
        // a value this handler does not bind is written as null and clears the
        // confirmed field. These two were omitted, and every Overview save
        // silently discarded the claimant's contact number and address.
        string? claimantContactNumber,
        string? claimantAddress,
        string? storageLocation,
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
                        CaseDataPolicy.InferInspectionMode(inspectionAddress),
                        claimantContactNumber,
                        claimantAddress,
                        storageLocation)),
                cancellationToken),
            "Case data was saved. The case is Not ready until completeness is confirmed again.");

    public async Task<IActionResult> OnPostGenerateReportDraftAsync(
        Guid id,
        string operationKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (!IsOperationKeyValid(operationKey))
        {
            TempData["CaseError"] = "The form has expired. Retry the operation.";
            return RedirectToEstimate(id);
        }

        GenerateCaseAssessmentReportDraftResult result;
        try
        {
            result = await generateReportDraft.ExecuteAsync(
                id, actor, CaseReportArtifactKind.AssessmentReport, cancellationToken);
        }
        catch (Exception exception) when (exception is ReportRenderRejectedException
            or InvalidOperationException
            or IOException
            or TimeoutException)
        {
            TempData["CaseError"] = "The report draft could not be generated. Retry the operation.";
            return RedirectToEstimate(id);
        }

        switch (result.Outcome)
        {
            case GenerateCaseAssessmentReportDraftOutcome.NotFound:
                return NotFound();
            case GenerateCaseAssessmentReportDraftOutcome.NotReady:
                TempData["CaseError"] =
                    "The report draft is not ready. " + string.Join(
                        " ",
                        result.Reasons.Select(reason => $"{reason.Requirement}: {reason.WhyOutstanding}"));
                return RedirectToEstimate(id);
            default:
                var assessmentPdf = result.Draft!;
                return File(assessmentPdf.Pdf, "application/pdf", assessmentPdf.SuggestedFileName);
        }
    }

    public async Task<IActionResult> OnGetPreviewReportDraftAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        var result = await generateReportDraft.ExecuteAsync(
            id, actor, CaseReportArtifactKind.AssessmentReport, cancellationToken);
        return result.Outcome switch
        {
            GenerateCaseAssessmentReportDraftOutcome.NotFound => NotFound(),
            GenerateCaseAssessmentReportDraftOutcome.NotReady => RedirectToEstimate(id),
            _ => File(result.Draft!.Pdf, "application/pdf"),
        };
    }

    /// <summary>
    /// B05's immutable generation: freezes the accepted snapshot inside the
    /// store's short transaction and renders through the registered
    /// renderer, one artifact per request. The draft handlers above stay for
    /// the labelled ungenerated working preview; this is the real report.
    /// </summary>
    public Task<IActionResult> OnPostGenerateReportAsync(
        Guid id,
        string operationKey,
        string? editLeaseToken,
        CancellationToken cancellationToken) =>
        GenerateArtifactAsync(
            id, operationKey, editLeaseToken, CaseReportArtifactKind.AssessmentReport, cancellationToken);

    public Task<IActionResult> OnPostGenerateFeeNoteAsync(
        Guid id,
        string operationKey,
        string? editLeaseToken,
        CancellationToken cancellationToken) =>
        GenerateArtifactAsync(
            id, operationKey, editLeaseToken, CaseReportArtifactKind.FeeNote, cancellationToken);

    private async Task<IActionResult> GenerateArtifactAsync(
        Guid id,
        string operationKey,
        string? editLeaseToken,
        CaseReportArtifactKind kind,
        CancellationToken cancellationToken)
    {
        var guard = await GuardReportCommandAsync(id, operationKey, editLeaseToken, cancellationToken);
        if (guard is not null)
        {
            return guard;
        }
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        CaseReportGenerationResult result;
        try
        {
            result = await generateReport.ExecuteAsync(
                new(
                    actor,
                    id,
                    currentCaseVersion,
                    editLeaseToken!,
                    operationKey,
                    kind,
                    kind == CaseReportArtifactKind.AssessmentReport
                        ? "Generate the immutable case report"
                        : "Generate the immutable fee note"),
                cancellationToken);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException
            or IOException
            or TimeoutException
            or ReportRenderRejectedException)
        {
            TempData["CaseError"] = MutationRefusalMessage(
                exception,
                "The artifact could not be generated. Retry the operation.");
            return RedirectToReport(id);
        }

        switch (result.Outcome)
        {
            case CaseReportGenerationOutcome.NotFound:
                return NotFound();
            case CaseReportGenerationOutcome.NotReady:
                TempData["CaseError"] = string.Join(
                    " ",
                    Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.GenerationNotReady + ":",
                    string.Join("; ", result.Reasons.Select(reason =>
                        $"{reason.Requirement}: {reason.WhyOutstanding}")));
                return RedirectToReport(id);
            case CaseReportGenerationOutcome.Pending:
                TempData["CaseStatus"] = Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.GenerationPending;
                return RedirectToReport(id);
            case CaseReportGenerationOutcome.Failed:
                TempData["CaseError"] =
                    "The artifact could not be generated. Retry the operation.";
                return RedirectToReport(id);
            default:
                ClearLeaseState();
                TempData["CaseStatus"] = kind == CaseReportArtifactKind.AssessmentReport
                    ? Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.ReportGenerated
                    : Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.FeeNoteGenerated;
                return RedirectToReport(id);
        }
    }

    /// <summary>
    /// Reopens a confirmed artifact's immutable bytes — never a regeneration
    /// and never a Pending, Failed or Unknown artifact (the store refuses
    /// those; the page does not decide).
    /// </summary>
    public async Task<IActionResult> OnGetGeneratedArtifactAsync(
        Guid id,
        Guid generationId,
        Guid artifactId,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (!await HasReportJourneyAccessAsync(id, actor, cancellationToken))
        {
            return NotFound();
        }

        var content = await generatedArtifacts.OpenAsync(
            actor, id, generationId, artifactId, cancellationToken);
        // The file result owns and disposes the stream once the body is
        // written; disposing here would close it before MVC reads it.
        return File(content.Content, content.MediaType, content.FileName);
    }

    /// <summary>
    /// B07 delivery preparation: pins the current generation's confirmed
    /// artifacts and the addressing resolved from the Case's structured
    /// contacts. Nothing is sent and no Sent state is claimed here.
    /// </summary>
    public async Task<IActionResult> OnPostPrepareReportDeliveryAsync(
        Guid id,
        string operationKey,
        string? editLeaseToken,
        Guid generationId,
        long expectedGenerationVersion,
        CancellationToken cancellationToken)
    {
        var guard = await GuardReportCommandAsync(id, operationKey, editLeaseToken, cancellationToken);
        if (guard is not null)
        {
            return guard;
        }
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            await prepareReportDelivery.ExecuteAsync(
                new(
                    actor,
                    id,
                    currentCaseVersion,
                    editLeaseToken!,
                    generationId,
                    expectedGenerationVersion,
                    operationKey),
                cancellationToken);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException
            or KeyNotFoundException)
        {
            TempData["CaseError"] = MutationRefusalMessage(
                exception,
                "The report delivery could not be prepared. Retry the operation.");
            return RedirectToReport(id);
        }

        ClearLeaseState();
        TempData["CaseStatus"] =
            Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.DeliveryPrepared + ".";
        return RedirectToReport(id);
    }

    /// <summary>
    /// The one page caller of A's staff send transport. The operation key is
    /// derived from the immutable preparation identity server-side — a
    /// reload can never mint a second send operation for one preparation —
    /// and the send boundary re-checks recipients, freshness and attachment
    /// hashes. A's returned state is mapped truthfully: only observation says
    /// sent, and an Unknown outcome never claims one.
    /// </summary>
    public async Task<IActionResult> OnPostSendPreparedReportAsync(
        Guid id,
        Guid preparationId,
        long expectedPreparationVersion,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        var operationKey = preparationId.ToString("N");

        StaffMailOperation operation;
        try
        {
            operation = await sendPreparedReport.ExecuteAsync(
                new(actor, id, preparationId, expectedPreparationVersion, operationKey),
                cancellationToken);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException
            or KeyNotFoundException)
        {
            TempData["CaseError"] = MutationRefusalMessage(
                exception,
                "The report was not sent because the case changed or the preparation is no longer current. Prepare it again.");
            return RedirectToReport(id);
        }

        switch (operation.State)
        {
            case StaffMailState.Sent:
                TempData["CaseStatus"] =
                    Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.SendObservedSent;
                break;
            case StaffMailState.Submitted:
                TempData["CaseStatus"] =
                    Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.SendAccepted;
                break;
            case StaffMailState.Failed:
                TempData["CaseError"] =
                    Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.SendFailed;
                break;
            case StaffMailState.Unknown:
                TempData["CaseError"] =
                    Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.SendUnknown;
                break;
            case StaffMailState.Cancelled:
                TempData["CaseStatus"] =
                    Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.SendCancelled;
                break;
            default:
                // Prepared/DraftCreating/DraftReady/Sending: the transport
                // accepted work that is still in flight — neither success
                // nor failure is claimed.
                TempData["CaseStatus"] =
                    Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.SendInProgress;
                break;
        }

        return RedirectToReport(id);
    }

    /// <summary>
    /// The Report-section mutation guard: the estimate guard's rules
    /// (Engineer, writable case, valid form, live lease, current version)
    /// with the Report section's redirect target.
    /// </summary>
    private async Task<IActionResult?> GuardReportCommandAsync(
        Guid id,
        string operationKey,
        string? editLeaseToken,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            ClearLeaseState();
            return Forbid();
        }
        var access = await getAssessmentAccess.ExecuteAsync(new(id, actor), cancellationToken);
        if (access is null || !AssessmentAccessPolicy.CanOpenReports(access))
        {
            return NotFound();
        }
        if (!actor.IsInRole(StaffRole.Engineer))
        {
            TempData["CaseError"] = "Only an Engineer can generate or deliver reports.";
            return RedirectToReport(id);
        }
        if (access.IsReadOnly)
        {
            TempData["CaseError"] = "The case is read-only once Complete.";
            return RedirectToReport(id);
        }
        if (!IsOperationKeyValid(operationKey))
        {
            TempData["CaseError"] = "The form has expired. Retry the operation.";
            return RedirectToReport(id);
        }
        if (string.IsNullOrWhiteSpace(editLeaseToken))
        {
            TempData["CaseError"] = NotInEditMode;
            return RedirectToReport(id);
        }

        var details = await getCase.ExecuteAsync(new(id, actor), cancellationToken);
        if (details is null)
        {
            return NotFound();
        }
        currentCaseVersion = details.Workflow.Version;
        return null;
    }

    private RedirectToPageResult RedirectToReport(Guid id) =>
        RedirectToPage("/Cases/Details", new { id, section = "report" });

    /// <summary>
    /// B01 port re-homed from PR 670's rejected standalone Valuation page:
    /// recording a guide valuation is a section command on the one Case
    /// workspace. The Add-valuation dialog posts here.
    /// </summary>
    public async Task<IActionResult> OnPostAddValuationAsync(
        Guid id,
        string operationKey,
        string? editLeaseToken,
        long expectedVersion,
        ValuationSource source,
        DateOnly date,
        TimeOnly time,
        string? guideMonth,
        long mileage,
        decimal retailValue,
        decimal tradeValue,
        CancellationToken cancellationToken)
    {
        var guard = await GuardValuationCommandAsync(id, operationKey, editLeaseToken, cancellationToken);
        if (guard is not null)
        {
            return guard;
        }
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            await saveValuation.ExecuteAsync(
                new(
                    id,
                    // The submitted version travels unchanged: the store
                    // enforces it against the live case, and a network replay
                    // must keep the request's original fingerprint rather
                    // than being rewritten with a newer version.
                    expectedVersion,
                    actor,
                    operationKey,
                    "Valuation recorded.",
                    editLeaseToken!,
                    new(source, date, time, mileage, retailValue, tradeValue, ParseGuideMonth(guideMonth))),
                cancellationToken);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException)
        {
            TempData["CaseError"] = MutationRefusalMessage(
                exception, "The valuation was not recorded. Retry the operation.");
            return RedirectToValuation(id);
        }

        ClearLeaseState();
        TempData["CaseStatus"] = "The valuation was recorded.";
        return RedirectToValuation(id);
    }

    private static DateOnly? ParseGuideMonth(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!DateOnly.TryParseExact(
                value,
                "yyyy-MM",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var month))
        {
            throw new ArgumentException("The guide month is invalid.", nameof(value));
        }
        return new DateOnly(month.Year, month.Month, 1);
    }

    /// <summary>
    /// The valuation-section mutation guard: the report guard's rules with
    /// the valuation redirect target.
    /// </summary>
    private async Task<IActionResult?> GuardValuationCommandAsync(
        Guid id,
        string operationKey,
        string? editLeaseToken,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            ClearLeaseState();
            return Forbid();
        }
        var access = await getAssessmentAccess.ExecuteAsync(new(id, actor), cancellationToken);
        if (access?.CanOpen != true)
        {
            return NotFound();
        }
        if (!actor.IsInRole(StaffRole.Engineer))
        {
            TempData["CaseError"] = "Only an Engineer can record a valuation.";
            return RedirectToValuation(id);
        }
        if (access.IsReadOnly)
        {
            TempData["CaseError"] = "The case is read-only once Complete.";
            return RedirectToValuation(id);
        }
        if (!IsOperationKeyValid(operationKey))
        {
            TempData["CaseError"] = "The form has expired. Retry the operation.";
            return RedirectToValuation(id);
        }
        if (string.IsNullOrWhiteSpace(editLeaseToken))
        {
            TempData["CaseError"] = NotInEditMode;
            return RedirectToValuation(id);
        }

        return null;
    }

    private RedirectToPageResult RedirectToValuation(Guid id) =>
        RedirectToPage("/Cases/Details", new { id, section = "valuation" });

    public async Task<IActionResult> OnPostSendToClaudeAsync(
        Guid id,
        string operationKey,
        string? direction,
        int? targetPercent,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (!await HasAssessmentAccessAsync(id, actor, cancellationToken))
        {
            return NotFound();
        }
        if (!IsOperationKeyValid(operationKey))
        {
            TempData["CaseError"] = "The form has expired. Retry the operation.";
            return RedirectToEstimate(id);
        }

        var details = await getCase.ExecuteAsync(new(id, actor), cancellationToken);
        if (details is null)
        {
            return NotFound();
        }

        var trimmedDirection = direction?.Trim();
        var instruction = string.IsNullOrWhiteSpace(trimmedDirection)
            ? $"Draft an estimate for case {details.Summary.Reference}."
            : trimmedDirection;
        try
        {
            await createAiJob.ExecuteAsync(
                new(
                    AiJobKind.Estimate,
                    id,
                    details.Summary.Reference,
                    instruction,
                    targetPercent,
                    actor,
                    operationKey),
                cancellationToken);
        }
        catch (ArgumentException)
        {
            TempData["CaseError"] = "Choose a target between 1 and 100 percent of the Engineer's Value.";
            return RedirectToEstimate(id);
        }
        catch (Exception exception) when (exception is InvalidOperationException or KeyNotFoundException)
        {
            TempData["CaseError"] = exception.Message;
            return RedirectToEstimate(id);
        }

        TempData["CaseStatus"] =
            "Sent to Claude. The job is queued; its estimate opens from Operations when ready.";
        return RedirectToEstimate(id);
    }

    /// <summary>
    /// Creates a named estimate or replaces the whole content of an existing
    /// Draft through ENG-026's Core-owned save use case.
    /// </summary>
    public async Task<IActionResult> OnPostSaveEstimateAsync(
        Guid id,
        string operationKey,
        string? editLeaseToken,
        Guid? estimateId,
        CancellationToken cancellationToken)
    {
        var editor = ReadEditorPost();
        var guard = await GuardEstimateEditAsync(id, operationKey, editLeaseToken, cancellationToken);
        if (guard is not null)
        {
            return guard;
        }
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (editor.Lines is null)
        {
            TempData["CaseError"] =
                "Check the estimate's lines: an operation, a quantity, hours or an amount does not read as a number.";
            return RedirectToEstimate(id, estimateId?.ToString("D"));
        }

        var existing = await ResolveEstimateAsync(id, estimateId, cancellationToken);
        var existingLines = existing?.Lines.ToDictionary(line => line.Id)
            ?? new Dictionary<Guid, CaseEstimateLineRecord>();
        var lines = editor.Lines.Select((line, index) =>
            editor.ExistingLineIds[index] is { } lineId
            && existingLines.TryGetValue(lineId, out var previous)
                ? line with
                {
                    GuideCode = previous.GuideCode,
                    // Carried forward only while the line still has no price.
                    // AssessmentPolicy refuses a line that is both marked To be
                    // confirmed and priced, so preserving it unconditionally would
                    // make pricing an imported unpriced line impossible.
                    Unpriced = previous.Unpriced && line.Price is null,
                    Betterment = previous.Betterment,
                    Status = previous.Status,
                    EvidenceLabel = previous.EvidenceLabel,
                    Justification = previous.Justification,
                }
                : line).ToArray();
        var details = new EstimateDetails(
            editor.Name ?? string.Empty,
            editor.RepairDays,
            editor.LabourRate,
            editor.PaintLabourRate,
            editor.PaintMaterials,
            editor.OtherCosts,
            editor.VatPercent ?? EstimatePolicy.DefaultVatPercent,
            editor.Notes);
        try
        {
            var saved = await saveEstimate.ExecuteAsync(
                new(
                    id,
                    currentCaseVersion,
                    actor,
                    operationKey,
                    estimateId is null ? "Estimate created" : "Estimate saved",
                    editLeaseToken!,
                    estimateId,
                    details,
                    lines,
                    existing?.Source ?? new(RepairSpecificationSourceRoute.Manual, null, null, null),
                    existing?.AiJobId),
                cancellationToken);
            ClearLeaseState();
            TempData["CaseStatus"] = "The estimate was saved.";
            return RedirectToEstimate(id, saved.SpecificationId.ToString("D"));
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            TempData["CaseError"] = MutationRefusalMessage(
                exception, "The estimate was not saved because the case changed or another editor holds it. Retry the operation.");
            return RedirectToEstimate(id, estimateId?.ToString("D"));
        }
    }

    /// <summary>
    /// Re-renders an estimate form with one row added or removed. The posted
    /// values are not persisted until Save estimate runs.
    /// </summary>
    public async Task<IActionResult> OnPostEditLineAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var editor = ReadEditorPost();
        IReadOnlyList<EstimateEditorLine> rows = editor.Rows;
        if (Request.Form.TryGetValue("removeLine", out var removed)
            && int.TryParse(removed.ToString(), out var removeAt)
            && removeAt >= 0 && removeAt < rows.Count)
        {
            rows = rows.Where((_, index) => index != removeAt).ToArray();
        }
        else
        {
            rows = [.. rows, new EstimateEditorLine("", null, null, null, null, null, null)];
        }

        return await RedrawEditorAsync(id, editor.EstimateId, editor, rows, cancellationToken);
    }

    /// <summary>Creates an Engineer's working copy of the selected estimate.</summary>
    public async Task<IActionResult> OnPostDuplicateEstimateAsync(
        Guid id,
        string operationKey,
        string? editLeaseToken,
        Guid estimateId,
        CancellationToken cancellationToken)
    {
        var guard = await GuardEstimateEditAsync(id, operationKey, editLeaseToken, cancellationToken);
        if (guard is not null)
        {
            return guard;
        }
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            var copy = await duplicateEstimate.ExecuteAsync(
                new(id, currentCaseVersion, actor, operationKey, "Estimate duplicated", editLeaseToken!, estimateId),
                cancellationToken);
            ClearLeaseState();
            TempData["CaseStatus"] = "The estimate was duplicated.";
            return RedirectToEstimate(id, copy.SpecificationId.ToString("D"));
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            TempData["CaseError"] = MutationRefusalMessage(
                exception, "The estimate was not duplicated because the case changed or another editor holds it. Retry the operation.");
            return RedirectToEstimate(id, estimateId.ToString("D"));
        }
    }

    /// <summary>Discards a non-current draft estimate with the supplied reason.</summary>
    public async Task<IActionResult> OnPostDiscardEstimateAsync(
        Guid id,
        string operationKey,
        string? editLeaseToken,
        Guid estimateId,
        string? reason,
        CancellationToken cancellationToken)
    {
        var guard = await GuardEstimateEditAsync(id, operationKey, editLeaseToken, cancellationToken);
        if (guard is not null)
        {
            return guard;
        }
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["CaseError"] = "Give the reason this estimate is deleted.";
            return RedirectToEstimate(id, estimateId.ToString("D"));
        }

        try
        {
            await discardEstimate.ExecuteAsync(
                new(id, currentCaseVersion, actor, operationKey, reason.Trim(), editLeaseToken!, estimateId),
                cancellationToken);
            ClearLeaseState();
            TempData["CaseStatus"] = "The estimate was deleted.";
            return RedirectToEstimate(id);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            TempData["CaseError"] = MutationRefusalMessage(
                exception, "The estimate was not deleted because the case changed or another editor holds it. Retry the operation.");
            return RedirectToEstimate(id, estimateId.ToString("D"));
        }
    }

    /// <summary>
    /// Makes an estimate Current. Core derives its calculation basis and
    /// completes a cited Draft-ready Estimate job when applicable.
    /// </summary>
    public async Task<IActionResult> OnPostSetCurrentEstimateAsync(
        Guid id,
        string operationKey,
        string? editLeaseToken,
        Guid estimateId,
        CancellationToken cancellationToken)
    {
        var guard = await GuardEstimateEditAsync(id, operationKey, editLeaseToken, cancellationToken);
        if (guard is not null)
        {
            return guard;
        }
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            await setCurrentEstimate.ExecuteAsync(
                new(id, currentCaseVersion, actor, operationKey, "Estimate made current", editLeaseToken!, estimateId),
                cancellationToken);
            ClearLeaseState();
            TempData["CaseStatus"] = "The estimate is now the case's current estimate.";
            return RedirectToEstimate(id, estimateId.ToString("D"));
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            TempData["CaseError"] = MutationRefusalMessage(
                exception, "The estimate was not made current because the case changed or another editor holds it. Retry the operation.");
            return RedirectToEstimate(id, estimateId.ToString("D"));
        }
    }

    private long currentCaseVersion;

    private async Task<IActionResult?> GuardEstimateEditAsync(
        Guid id,
        string operationKey,
        string? editLeaseToken,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            ClearLeaseState();
            return Forbid();
        }
        var access = await getAssessmentAccess.ExecuteAsync(new(id, actor), cancellationToken);
        if (access?.CanOpen != true)
        {
            return NotFound();
        }
        if (!actor.IsInRole(StaffRole.Engineer))
        {
            TempData["CaseError"] = "Only an Engineer can change an estimate.";
            return RedirectToEstimate(id);
        }
        if (access.IsReadOnly)
        {
            TempData["CaseError"] = "The case is read-only once Complete.";
            return RedirectToEstimate(id);
        }
        if (!IsOperationKeyValid(operationKey))
        {
            TempData["CaseError"] = "The form has expired. Retry the operation.";
            return RedirectToEstimate(id);
        }
        if (string.IsNullOrWhiteSpace(editLeaseToken))
        {
            TempData["CaseError"] = NotInEditMode;
            return RedirectToEstimate(id);
        }

        var details = await getCase.ExecuteAsync(new(id, actor), cancellationToken);
        if (details is null)
        {
            return NotFound();
        }
        currentCaseVersion = details.Workflow.Version;
        return null;
    }

    private async Task<RepairSpecificationVersion?> ResolveEstimateAsync(
        Guid caseId,
        Guid? estimateId,
        CancellationToken cancellationToken) =>
        estimateId is { } selected
            ? await repairSpecifications.GetVersionAsync(caseId, selected, cancellationToken)
            : null;

    private async Task<IActionResult> RedrawEditorAsync(
        Guid id,
        Guid? estimateId,
        EstimateEditorPost editor,
        IReadOnlyList<EstimateEditorLine> rows,
        CancellationToken cancellationToken)
    {
        var result = await OnGetAsync(id, estimateId?.ToString("D"), null, cancellationToken);
        if (Case is null)
        {
            return result;
        }

        SelectedEstimate = estimateId is { } selected
            ? Estimates.FirstOrDefault(item => item.SpecificationId == selected)
            : null;
        EditingNewEstimate = estimateId is null;
        EditorDetails = new EstimateDetails(
            editor.Name ?? string.Empty,
            editor.RepairDays,
            editor.LabourRate,
            editor.PaintLabourRate,
            editor.PaintMaterials,
            editor.OtherCosts,
            editor.VatPercent ?? EstimatePolicy.DefaultVatPercent,
            editor.Notes);
        EditorLines = rows.Count > 0 ? rows : [new EstimateEditorLine("", null, null, null, null, null, null)];
        return Page();
    }

    private sealed record EstimateEditorPost(
        string? Name,
        int? RepairDays,
        decimal? LabourRate,
        decimal? PaintLabourRate,
        decimal? PaintMaterials,
        decimal? OtherCosts,
        decimal? VatPercent,
        string? Notes,
        Guid? EstimateId,
        IReadOnlyList<EstimateEditorLine> Rows,
        IReadOnlyList<EstimateLineInput>? Lines,
        IReadOnlyList<Guid?> ExistingLineIds);

    private EstimateEditorPost ReadEditorPost()
    {
        var form = Request.Form;
        static decimal? Money(string? value) =>
            string.IsNullOrWhiteSpace(value)
                ? null
                : decimal.TryParse(value.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
                    ? parsed
                    : decimal.MinusOne;
        static int? Days(string? value) =>
            string.IsNullOrWhiteSpace(value)
                ? null
                : int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                    ? parsed
                    : -1;

        var operations = form["lineOperation"].ToArray();
        var postedLineIds = form["lineId"].ToArray();
        var descriptions = form["lineDescription"].ToArray();
        var partNumbers = form["linePartNumber"].ToArray();
        var quantities = form["lineQuantity"].ToArray();
        var labourHoursValues = form["lineLabourHours"].ToArray();
        var paintHoursValues = form["linePaintHours"].ToArray();
        var partPoundsValues = form["linePartPounds"].ToArray();
        var rows = new List<EstimateEditorLine>(operations.Length);
        var lines = new List<EstimateLineInput>(operations.Length);
        var existingLineIds = new List<Guid?>(operations.Length);
        var linesAreValid = true;
        static string Field(string?[] values, int index) =>
            index >= 0 && index < values.Length && values[index] is not null ? values[index]! : string.Empty;
        for (var index = 0; index < operations.Length; index++)
        {
            var operation = operations[index] ?? string.Empty;
            var description = Field(descriptions, index);
            var partNumber = Field(partNumbers, index);
            var quantity = Field(quantities, index);
            var labourHours = Field(labourHoursValues, index);
            var paintHours = Field(paintHoursValues, index);
            var partPounds = Field(partPoundsValues, index);
            var existingLineId = Guid.TryParse(Field(postedLineIds, index), out var parsedLineId)
                ? parsedLineId
                : (Guid?)null;
            rows.Add(new EstimateEditorLine(
                operation, description, partNumber, quantity, labourHours, paintHours, partPounds, existingLineId));

            var isEmpty = string.IsNullOrWhiteSpace(description)
                && string.IsNullOrWhiteSpace(partNumber)
                && string.IsNullOrWhiteSpace(quantity)
                && string.IsNullOrWhiteSpace(labourHours)
                && string.IsNullOrWhiteSpace(paintHours)
                && string.IsNullOrWhiteSpace(partPounds);
            if (isEmpty)
            {
                continue;
            }
            existingLineIds.Add(existingLineId);

            var typed = EstimateOperations.TryParse(operation, out var parsedOperation);
            var workUnits = Money(labourHours);
            var paintWorkUnits = Money(paintHours);
            var price = Money(partPounds);
            int? parsedQuantity = null;
            if (!string.IsNullOrWhiteSpace(quantity))
            {
                parsedQuantity = int.TryParse(
                    quantity.Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var quantityValue) && quantityValue >= 0
                    ? quantityValue
                    : -1;
            }
            if (!typed
                || workUnits == decimal.MinusOne
                || paintWorkUnits == decimal.MinusOne
                || price == decimal.MinusOne
                || parsedQuantity == -1
                || workUnits is < 0
                || paintWorkUnits is < 0
                || price is < 0)
            {
                linesAreValid = false;
                continue;
            }

            lines.Add(new(
                EstimateOperations.ToLineType(parsedOperation),
                null,
                string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                workUnits,
                price,
                false,
                string.IsNullOrWhiteSpace(partNumber) ? null : partNumber.Trim(),
                null,
                null,
                null,
                null,
                paintWorkUnits,
                parsedQuantity));
        }

        Guid? estimateId = Guid.TryParse(form["estimateId"].ToString(), out var parsedId)
            && parsedId != Guid.Empty
            ? parsedId
            : null;
        return new(
            form["estimateName"].ToString(),
            Days(form["estimateRepairDays"].ToString()),
            Money(form["estimateLabourRate"].ToString()),
            Money(form["estimatePaintLabourRate"].ToString()),
            Money(form["estimatePaintMaterials"].ToString()),
            Money(form["estimateOtherCosts"].ToString()),
            Money(form["estimateVatPercent"].ToString()),
            form["estimateNotes"].ToString(),
            estimateId,
            rows,
            linesAreValid ? lines : null,
            existingLineIds);
    }

    /// <summary>
    /// ENG-026: the estimate import. The file is parsed first
    /// (no side effects — a rejected parse retains nothing), then retained
    /// through the existing case-document custody path, then landed as a
    /// named Draft estimate carrying the route, source version and hash of
    /// the retained document. Nothing feeds a report until an Engineer makes
    /// an estimate Current.
    /// </summary>
    public async Task<IActionResult> OnPostImportEstimateAsync(
        Guid id,
        string operationKey,
        string? editLeaseToken,
        string? name,
        string? source,
        string? reason,
        IFormFile? estimateFile,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        var importAccess = await getAssessmentAccess.ExecuteAsync(new(id, actor), cancellationToken);
        if (importAccess?.CanOpen != true)
        {
            return NotFound();
        }
        if (!actor.IsInRole(StaffRole.Engineer))
        {
            TempData["CaseError"] = "Only an Engineer can import an estimate.";
            return RedirectToEstimate(id);
        }
        if (importAccess.IsReadOnly)
        {
            TempData["CaseError"] = "The case is read-only once Complete.";
            return RedirectToEstimate(id);
        }
        if (!IsOperationKeyValid(operationKey))
        {
            TempData["CaseError"] = "The form has expired. Retry the operation.";
            return RedirectToEstimate(id);
        }
        var trimmedName = name?.Trim();
        if (string.IsNullOrEmpty(trimmedName))
        {
            TempData["CaseError"] = "Name the imported estimate.";
            return RedirectToEstimate(id);
        }
        var isJson = string.Equals(source, "json", StringComparison.OrdinalIgnoreCase);
        if (!isJson && !string.Equals(source, "audatex-pdf", StringComparison.OrdinalIgnoreCase))
        {
            // Only the sources the form offers; anything else is not this form's post.
            TempData["CaseError"] = "The form has expired. Retry the operation.";
            return RedirectToEstimate(id);
        }
        var parser = isJson ? jsonEstimateParser : estimateParser;
        if (estimateFile is null || estimateFile.Length is <= 0 or > MaximumEstimateUploadBytes)
        {
            TempData["CaseError"] = "Choose a non-empty estimate file of 10 MB or less.";
            return RedirectToEstimate(id);
        }
        if (!parser.CanParse(estimateFile.FileName, estimateFile.ContentType))
        {
            TempData["CaseError"] = isJson
                ? "Only a JSON estimate can be imported from this source."
                : "Only a PDF estimate can be imported from this source.";
            return RedirectToEstimate(id);
        }

        await using var buffer = new MemoryStream((int)estimateFile.Length);
        await estimateFile.CopyToAsync(buffer, cancellationToken);
        var content = buffer.GetBuffer().AsMemory(0, checked((int)buffer.Length));

        ParsedEstimate parsed;
        try
        {
            parsed = parser.Parse(content);
        }
        catch (EstimateParseRejectedException exception)
        {
            TempData["CaseError"] = exception.Message;
            return RedirectToEstimate(id);
        }

        var details = await getCase.ExecuteAsync(new(id, actor), cancellationToken);
        if (details is null)
        {
            return NotFound();
        }
        if (string.IsNullOrWhiteSpace(editLeaseToken))
        {
            TempData["CaseError"] = NotInEditMode;
            return RedirectToEstimate(id);
        }

        var caseVersion = details.Workflow.Version;
        var artifactIdentity = $"estimate-import:{operationKey}";
        AddCaseDocumentResult retained;
        try
        {
            retained = await addCaseDocument.ExecuteAsync(
                new(
                    id,
                    Path.GetFileName(estimateFile.FileName),
                    isJson ? "application/json" : "application/pdf",
                    content,
                    DocumentSemanticRole.Other,
                    DocumentSource.StaffUpload,
                    artifactIdentity,
                    actor,
                    $"{operationKey}-document",
                    caseVersion,
                    editLeaseToken),
                cancellationToken);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            HandleLeaseFailure(id, editLeaseToken, exception);
            TempData["CaseError"] = MutationRefusalMessage(
                exception,
                "The estimate was not imported because the case changed or another editor holds it. "
                + "Nothing was recorded; retry the import.");
            return RedirectToEstimate(id);
        }

        try
        {
            var draftLease = await acquireLease.ExecuteAsync(
                new(id, caseVersion + 1, actor, NewOperationKey()),
                cancellationToken);
            StoreLeaseAuthority(id, draftLease.Token);
            var imported = await saveEstimate.ExecuteAsync(
                new(
                    id,
                    caseVersion + 1,
                    actor,
                    operationKey,
                    string.IsNullOrWhiteSpace(reason) ? "Estimate imported from a document" : reason.Trim(),
                    draftLease.Token,
                    null,
                    new(trimmedName, null, null, null, null, null, EstimatePolicy.DefaultVatPercent, null),
                    parsed.Lines,
                    new(parser.Route, artifactIdentity, parsed.SourceVersion, retained.Version.Sha256)),
                cancellationToken);
            ClearLeaseState();
            TempData["CaseStatus"] =
                $"{trimmedName} was imported as a draft with {parsed.Lines.Count} lines for your review. "
                + "The original document is kept on the case.";
            return RedirectToEstimate(id, imported.SpecificationId.ToString("D"));
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            HandleLeaseFailure(id, PeekLeaseToken(), exception);
            TempData["CaseError"] = MutationRefusalMessage(
                exception,
                "The original document was kept on the case, but the estimate lines were not "
                + "recorded because the case changed. Retry the import.");
        }

        return RedirectToEstimate(id);
    }

    private static string MutationRefusalMessage(Exception exception, string fallback) =>
        exception is InvalidOperationException
            and not CaseVersionConflictException
            and not CaseEditLeaseConflictException
            and not CaseEditLeaseExpiredException
            and not CaseOperationConflictException
            ? exception.Message
            : fallback;

    private async Task<bool> HasAssessmentAccessAsync(
        Guid caseId,
        ActionActor actor,
        CancellationToken cancellationToken) =>
        (await getAssessmentAccess.ExecuteAsync(
            new(caseId, actor),
            cancellationToken))?.CanOpen == true;

    /// <summary>
    /// H3: the report generation/preview/delivery journey uses the workspace
    /// state set without D11's EVA-export clause.
    /// </summary>
    private async Task<bool> HasReportJourneyAccessAsync(
        Guid caseId,
        ActionActor actor,
        CancellationToken cancellationToken) =>
        (await getAssessmentAccess.ExecuteAsync(
            new(caseId, actor),
            cancellationToken)) is { } access
        && AssessmentAccessPolicy.CanOpenReports(access);

    private static bool IsOperationKeyValid(string value) =>
        Guid.TryParseExact(value, "N", out var operationId) && operationId != Guid.Empty;

    private RedirectToPageResult RedirectToEstimate(
        Guid id,
        string? estimate = null,
        string? dialog = null) =>
        RedirectToPage(
            "/Cases/Details",
            new { id, section = "estimate", estimate, dialog });

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
    /// carry directly: the assigned and Sign-off Engineer names, the Engineer
    /// choices available in Review, and whether API submission is a composed
    /// route this principal allows.
    /// </summary>
    private async Task DescribeWorkspaceExtrasAsync(CancellationToken cancellationToken)
    {
        if (Case is not { Workflow: var workflow } details)
        {
            return;
        }

        if (workflow.AssignedEngineerId is { } engineerId)
        {
            var account = await staffAccountQueries.GetAsync(engineerId, cancellationToken);
            EngineerDisplayName = account?.UserName ?? ActorDisplayNames.UnknownStaff;
        }

        var profiles = await staffAccountQueries.ListSignOffEngineersAsync(cancellationToken);
        var signOffEngineer = CaseSignOffEngineerResolver.Resolve(
            workflow.SignOffEngineerId,
            workflow.AssignedEngineerId,
            profiles);
        SignOffEngineerDisplayName = signOffEngineer?.PrintedName
            ?? Labels.CaseWorkspace.Unassigned;

        IReadOnlyList<EvaHandoffEngineerOption> engineerOptions = [];
        if (workflow.State == CaseLifecycleState.Review)
        {
            var accounts = await staffAccountQueries.ListAsync(0, 100, cancellationToken);
            engineerOptions = accounts.Accounts
                .Where(account => account.IsEnabled && account.Roles.Contains(StaffRole.Engineer))
                .Select(account => new EvaHandoffEngineerOption(account.Id, account.UserName))
                .ToArray();
        }

        var modes = submitCaseToEva is null
            ? EvaSubmissionModes.Disabled
            : await evaModeStore.GetForPrincipalAsync(
                workflow.Identity.PrincipalCode,
                cancellationToken);
        EvaHandoff = new(
            workflow.CaseId,
            workflow.Version,
            workflow.State,
            LeaseToken,
            EngineerDisplayName ?? Labels.CaseWorkspace.Unassigned,
            engineerOptions,
            SignOffEngineerDisplayName,
            signOffEngineer?.StaffId,
            profiles.Select(profile => new EvaHandoffEngineerOption(
                profile.StaffId,
                profile.PrintedName)).ToArray(),
            details.Data?.Completeness.Values.InstructionComplete ?? false,
            details.Data?.Completeness.Values.ImagesComplete ?? false,
            submitCaseToEva is not null,
            EvaSubmissionPolicy.AllowsManualSubmission(modes),
            NewOperationKey(),
            NewOperationKey());
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
            "storageLocation" => data.Inspection.StorageLocation?.Confirmed?.Value,

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
        "storageLocation" => Labels.CaseWorkspace.StorageLocation,
        "reason" => "Reason",

        // The completeness flags are labelled as the form the editor was looking at labelled them.
        "instructionComplete" or "instructionsComplete" => "Instructions complete",
        "imagesComplete" => "Images complete",
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

/// <summary>
/// One editable estimate-line row as posted by the Case Estimate section.
/// Core owns the conversion from its operation word to a persisted line type.
/// </summary>
public sealed record EstimateEditorLine(
    string Operation,
    string? Description,
    string? PartNumber,
    string? Quantity,
    string? LabourHours,
    string? PaintHours,
    string? PartPounds,
    Guid? ExistingLineId = null);
