using System.Globalization;
using System.IO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.AiWork;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Reports;
using Pegasus.Core.Vehicle;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Assessment;

namespace Pegasus.Web.Pages.Cases.Assessment;

/// <summary>
/// One editable estimate-line row as posted by the form and rendered by the
/// editor. Core owns the conversion from its operation word to a persisted
/// line type through <see cref="EstimateOperations"/>.
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

/// <summary>
/// The Assessment workspace (context.md §1.9, ENG-025). Access is owned by
/// <see cref="AssessmentAccessPolicy"/> in Core (D11: With Engineer or
/// onwards plus a current-cycle export; read-only once Complete). The page
/// draws the identity ribbon, the record bar (estimate import, the Glass's
/// and Audatex disabled seams, Send to Claude through the AI job ledger,
/// report-draft generation and preview) and the assessment-v3 evidence and
/// estimates panes over the repair-specification seam (ENG-002/EXT-09).
/// </summary>
/// <remarks>
/// CASE-024: the save paths this page offers run under edit mode the
/// operator enters, the same one server-owned lease the case workspace
/// claims, so an engineer working an assessment is visible to other staff
/// as the case's editor.
/// </remarks>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class IndexModel(
    IGetCase getCase,
    IGetAssessmentAccess getAssessmentAccess,
    IGetAssessmentWorkspace getAssessmentWorkspace,
    ICaseEvidenceImageQueries evidenceImageQueries,
    ICreateAiJob createAiJob,
    ISendToAiControl sendToAiControl,
    GenerateCaseAssessmentReportDraft generateReportDraft,
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
    IHeartbeatCaseEditLease heartbeatLease,
    IReleaseCaseEditLease releaseLease,
    IDescribeCaseEditAuthorityHolder describeEditAuthorityHolder,
    ILogger<IndexModel> logger) : CaseMutationPageModel(logger)
{
    /// <summary>The staff custody upload's own ceiling (Cases/Custody), reused unchanged.</summary>
    private const long MaximumEstimateUploadBytes = 10 * 1024 * 1024;

    /// <summary>
    /// Refused before the save reaches Core, so the operator is told what to do rather than shown
    /// a refusal about a lease token they never had.
    /// </summary>
    private const string NotInEditMode = "Enter edit mode to change the assessment.";

    public AssessmentWorkspace? Case { get; private set; }

    public CaseAssessmentProjection? Assessment { get; private set; }

    /// <summary>
    /// The unavailable surface (contract §1.9 gate): access refused names
    /// the case and states the export condition rather than 404-ing a URL
    /// the Case workspace had been offering.
    /// </summary>
    public string? UnavailableReference { get; private set; }

    public string? UnavailableRegistration { get; private set; }

    /// <summary>D11: Post-report complete renders every pane read-only.</summary>
    public bool IsReadOnly { get; private set; }

    public RepairSpecificationVersion? AcceptedSpecification { get; private set; }

    /// <summary>Every named estimate available to the case, oldest first.</summary>
    public IReadOnlyList<RepairSpecificationVersion> Estimates { get; private set; } = [];

    /// <summary>The estimate selected by the query-string tab, if any.</summary>
    public RepairSpecificationVersion? SelectedEstimate { get; private set; }

    /// <summary>Whether the editor is composing a new estimate.</summary>
    public bool EditingNewEstimate { get; private set; }

    public EstimateDetails? EditorDetails { get; private set; }

    public IReadOnlyList<EstimateEditorLine> EditorLines { get; private set; } = [];

    /// <summary>Only a Draft can be changed in place.</summary>
    public bool SelectedEstimateIsEditable =>
        !IsReadOnly
        && ActorIsEngineer
        && (EditingNewEstimate || SelectedEstimate?.State == RepairSpecificationState.Draft);

    /// <summary>
    /// An Engineer can duplicate a settled estimate to revise it; Core turns
    /// that copy into a Draft and rejects discarded estimates.
    /// </summary>
    public bool SelectedEstimateCanBeDuplicated =>
        !IsReadOnly
        && ActorIsEngineer
        && SelectedEstimate is { State: not RepairSpecificationState.Discarded };

    /// <summary>Draft and accepted estimates can become the Current estimate.</summary>
    public bool SelectedEstimateCanBeCurrent =>
        !IsReadOnly
        && ActorIsEngineer
        && SelectedEstimate is { IsCurrent: false }
        && (SelectedEstimate.State == RepairSpecificationState.Draft
            || SelectedEstimate.State == RepairSpecificationState.Accepted);

    /// <summary>
    /// The display totals reuse Core's sole totals owner over the editor's
    /// transient values; browser code never calculates money.
    /// </summary>
    public EstimateTotals EditorTotals
    {
        get
        {
            var details = EditorDetails ?? SelectedEstimate?.Details
                ?? new EstimateDetails(
                    Name: "Estimate",
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

    private static decimal? ParseNumber(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : decimal.TryParse(value.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;

    public bool ActorIsEngineer { get; private set; }

    /// <summary>Instruction-role live files for the evidence rail.</summary>
    public IReadOnlyList<CaseFile> InstructionFiles { get; private set; } = [];

    /// <summary>Custody-confirmed case images for the evidence rail.</summary>
    public IReadOnlyList<CaseEvidenceImage> EvidenceImages { get; private set; } = [];

    public string ImportOperationKey { get; private set; } = NewOperationKey();

    public string SaveEstimateOperationKey { get; private set; } = NewOperationKey();

    public string DuplicateOperationKey { get; private set; } = NewOperationKey();

    public string DiscardOperationKey { get; private set; } = NewOperationKey();

    public string UseEstimateOperationKey { get; private set; } = NewOperationKey();

    public string SendOperationKey { get; private set; } = NewOperationKey();

    public string ReportDraftOperationKey { get; private set; } = NewOperationKey();

    /// <summary>
    /// The DELIV-012 report-draft entry point's readiness: ready to render,
    /// or every named reason it is not (case unrecognized when null).
    /// </summary>
    public AssessmentReportDraftPreparation? ReportDraftPreparation { get; private set; }

    /// <summary>The case's claimed identity line, rendered in the header eyebrow.</summary>
    public string HeaderEyebrow =>
        string.IsNullOrWhiteSpace(Case?.Header.Registration)
            ? Case?.Header.Reference ?? string.Empty
            : $"{Case.Header.Reference} · {Case.Header.Registration}";

    /// <summary>
    /// The claimant the ribbon shows; the workspace projection carries no
    /// party list, so this reads the same <see cref="IGetCase"/> summary
    /// the page already loads for edit mode.
    /// </summary>
    public string? Claimant { get; private set; }

    /// <summary>
    /// The Mileage figure the ribbon shows: the saved assessment value,
    /// else confirmed vehicle evidence, else the DVSA estimate (miles
    /// only) — the CASE-008 cascade, unchanged.
    /// </summary>
    public string? MileageDisplay
    {
        get
        {
            if (Assessment?.Field("vehicle.odometer_miles")?.Value is { Length: > 0 } saved)
            {
                return saved;
            }
            if (Case?.Data?.Vehicle.Mileage.Confirmed is { Value: var confirmed }
                && IsMiles(Case.Data.Vehicle.MileageUnit.Confirmed?.Value))
            {
                return confirmed.ToString(CultureInfo.InvariantCulture);
            }
            if (Case?.Data?.Vehicle.Mileage.Fact is { Value: var fact }
                && IsMiles(Case.Data.Vehicle.MileageUnit.Fact?.Value))
            {
                return fact.ToString(CultureInfo.InvariantCulture);
            }
            return Case?.LatestVehicleObservation?.Mileage is { Unit: VehicleMileageUnit.Miles } estimate
                ? estimate.Value.ToString(CultureInfo.InvariantCulture)
                : null;
        }
    }

    /// <summary>The Vehicle figure the ribbon shows, from vehicle evidence.</summary>
    public string VehicleDisplay
    {
        get
        {
            var vehicle = Case?.Data?.Vehicle;
            var details = Case?.LatestVehicleObservation?.Vehicle;
            var make = vehicle?.Make.Confirmed?.Value ?? vehicle?.Make.Fact?.Value ?? details?.Make;
            var model = vehicle?.Model.Confirmed?.Value ?? vehicle?.Model.Fact?.Value ?? details?.Model;
            return string.Join(" ", new[] { make, model }.Where(part => !string.IsNullOrWhiteSpace(part)));
        }
    }

    /// <summary>
    /// The confirmed Engineer's Value that anchors the Send to Claude
    /// target (FRD-11 § AI Job List); Core refuses the job without it, so
    /// the dialog disables and names the condition here first.
    /// </summary>
    public decimal? EngineerValue =>
        Assessment?.Field(AssessmentVocabulary.ValueEngineer) is { IsConfirmed: true } engineerValue
            && decimal.TryParse(
                engineerValue.Value,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var parsed)
            ? parsed
            : null;

    /// <summary>The condition naming why Send to Claude is not offered, or null when offered.</summary>
    public string? SendToClaudeCondition { get; private set; }

    /// <summary>
    /// The dialog the query string deliberately exposes for script-off
    /// operation. With JavaScript, the shell enhances its static link.
    /// </summary>
    public string? OpenDialog { get; private set; }

    /// <summary>The condition naming why an estimate import is not offered, or null when offered.</summary>
    public string? ImportCondition =>
        IsReadOnly
            ? "Read-only once Complete"
            : !ActorIsEngineer
                ? "Only an Engineer can import an estimate"
                : null;

    /// <summary>
    /// The single condition the D7 estimating-service seams (Glass's,
    /// Audatex, EXT-09) state. One list per concept: the record bar draws
    /// two controls from it, so the sentence is written once here rather
    /// than typed into the view twice.
    /// </summary>
    public string EstimatingServiceCondition =>
        "Available once the estimating-service link is agreed";

    /// <summary>The condition naming why the report-draft controls are not offered, or null.</summary>
    public string? ReportDraftCondition { get; private set; }

    /// <summary>Whether the report-draft readiness names outstanding reasons this render.</summary>
    public bool ReportDraftNotReady =>
        ReportDraftPreparation is { CanGenerate: false }
        && ReportDraftReasons.Count > 0;

    /// <summary>
    /// The report-draft readiness reasons, named once beside the disabled
    /// control (FRD-11: a missing requirement leaves the control disabled
    /// and states that outstanding reason by name).
    /// </summary>
    public IReadOnlyList<AssessmentReadinessItem> ReportDraftReasons
        => ReportDraftPreparation?.Reasons ?? [];

    /// <summary>
    /// The holder disclosure other staff see. The workspace projection carries the lease and the
    /// assessment projection does not, so this page reads it from the same
    /// <see cref="IGetCase"/> it already uses rather than widening
    /// <see cref="AssessmentWorkspace"/> with a second copy of it.
    /// </summary>
    public CaseEditAuthorityHolder? EditAuthorityHolder { get; private set; }

    public bool ViewerHoldsEditAuthority { get; private set; }

    public bool CaseIsArchived { get; private set; }

    /// <summary>This page's messages are rendered by its own panels, not the workspace's.</summary>
    protected override string StatusTempDataKey => "AssessmentStatus";

    protected override string ErrorTempDataKey => "AssessmentError";

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
        if (!await CanAccessAsync(id, actor, cancellationToken))
        {
            return NotFound();
        }

        return await ClaimLeaseAsync(
            acquireLease,
            id,
            expectedVersion,
            operationKey,
            () => RedirectToPage(new { id }),
            cancellationToken);
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
            () => RedirectToPage(new { id }),
            cancellationToken);

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

        var access = await getAssessmentAccess.ExecuteAsync(new(id, actor), cancellationToken);
        if (access is null)
        {
            return NotFound();
        }
        if (!access.CanOpen)
        {
            // The gate the contract draws: name the case, state the export
            // condition, offer the way back (§1.9 "Assessment unavailable").
            var gateDetails = await getCase.ExecuteAsync(new(id, actor), cancellationToken);
            if (gateDetails is null)
            {
                return NotFound();
            }
            UnavailableReference = gateDetails.Summary.Reference;
            UnavailableRegistration = gateDetails.Summary.Registration;
            return Page();
        }

        Case = await getAssessmentWorkspace.ExecuteAsync(new(id, actor), cancellationToken);
        if (Case is null)
        {
            return NotFound();
        }

        var details = await LoadCaseContextAsync(id, actor, cancellationToken);
        Assessment = Case.Assessment;
        AcceptedSpecification = Case.AcceptedSpecification;
        ActorIsEngineer = actor.IsInRole(StaffRole.Engineer);
        IsReadOnly = access.IsReadOnly;
        Claimant = details?.Summary.Claimant;
        InstructionFiles = CaseFiles.Live(details?.Documents ?? [])
            .Where(file => file.Occurrence.SemanticRole == DocumentSemanticRole.Instruction)
            .ToList();
        EvidenceImages = await evidenceImageQueries.ListForCaseAsync(id, cancellationToken);
        Estimates = await listEstimates.ExecuteAsync(id, cancellationToken);
        ApplyEstimateSelection(estimate);
        // The same inputs the projection source hands Project (Costs null,
        // the Current estimate as the cost block, ENG-026), so the control's
        // condition cannot disagree with what generating would decide.
        ReportDraftPreparation = AssessmentReportProjection.Prepare(
            Assessment,
            costs: null,
            currentEstimate: AcceptedSpecification);
        await EvaluateRecordBarConditionsAsync(cancellationToken);
        OpenDialog = dialog switch
        {
            "import-estimate" when ImportCondition is null => "import-estimate",
            "send-to-claude" when SendToClaudeCondition is null => "send-to-claude",
            "delete-estimate" when SelectedEstimateIsEditable
                && SelectedEstimate is { IsCurrent: false } => "delete-estimate",
            _ => null
        };
        return Page();
    }

    /// <summary>
    /// Resolves the tab query to a named estimate, the new-estimate editor,
    /// or the Current estimate (falling back to the newest record).
    /// </summary>
    private void ApplyEstimateSelection(string? estimate)
    {
        if (string.Equals(estimate, "new", StringComparison.OrdinalIgnoreCase))
        {
            EditingNewEstimate = true;
            EditorDetails = new EstimateDetails(
                Name: "New estimate",
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

    /// <summary>
    /// Renders and returns the report draft PDF (DELIV-012). Readiness is
    /// decided by <see cref="AssessmentReportProjection"/>, the same
    /// readiness that conditions the record-bar controls; a case that is
    /// not ready returns to the page with every outstanding reason named
    /// rather than throwing.
    /// </summary>
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
            TempData["AssessmentError"] = "The form has expired. Retry the operation.";
            return RedirectToPage(new { id });
        }

        GenerateCaseAssessmentReportDraftResult result;
        try
        {
            result = await generateReportDraft.ExecuteAsync(id, actor, cancellationToken);
        }
        catch (Exception exception) when (exception is ReportRenderRejectedException
            or InvalidOperationException
            or IOException
            or TimeoutException)
        {
            TempData["AssessmentError"] = "The report draft could not be generated. Retry the operation.";
            return RedirectToPage(new { id });
        }

        switch (result.Outcome)
        {
            case GenerateCaseAssessmentReportDraftOutcome.NotFound:
                return NotFound();
            case GenerateCaseAssessmentReportDraftOutcome.NotReady:
                TempData["AssessmentError"] =
                    "The report draft is not ready. " + string.Join(
                        " ",
                        result.Reasons.Select(reason => $"{reason.Requirement}: {reason.WhyOutstanding}"));
                return RedirectToPage(new { id });
            default:
                var assessmentPdf = result.Draft!.Assessment;
                return File(assessmentPdf.Pdf, "application/pdf", assessmentPdf.SuggestedFileName);
        }
    }

    /// <summary>
    /// The record bar's preview: the same read-only rendering seam as the
    /// generate control, delivered inline so the browser's own viewer is
    /// the preview surface (nothing is saved by either).
    /// </summary>
    public async Task<IActionResult> OnGetPreviewReportDraftAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        var result = await generateReportDraft.ExecuteAsync(id, actor, cancellationToken);
        return result.Outcome switch
        {
            GenerateCaseAssessmentReportDraftOutcome.NotFound => NotFound(),
            GenerateCaseAssessmentReportDraftOutcome.NotReady => RedirectToPage(new { id }),
            _ => File(result.Draft!.Assessment.Pdf, "application/pdf"),
        };
    }

    /// <summary>
    /// FRD-11 § AI Job List: Send to Claude queues an Estimate-kind AI job
    /// carrying the operator's direction and a target percentage of the
    /// confirmed Engineer's Value. Core owns every refusal — the eligible
    /// state, the missing Engineer's Value and the Administrator switch —
    /// and this handler surfaces the refusal sentence it is given.
    /// </summary>
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
        if (!await CanAccessAsync(id, actor, cancellationToken))
        {
            return NotFound();
        }
        if (!IsOperationKeyValid(operationKey))
        {
            TempData["AssessmentError"] = "The form has expired. Retry the operation.";
            return RedirectToPage(new { id });
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
            TempData["AssessmentError"] = "Choose a target between 1 and 100 percent of the Engineer's Value.";
            return RedirectToPage(new { id });
        }
        catch (Exception exception) when (exception is InvalidOperationException or KeyNotFoundException)
        {
            TempData["AssessmentError"] = exception.Message;
            return RedirectToPage(new { id });
        }

        TempData["AssessmentStatus"] =
            "Sent to Claude. The job is queued; its estimate opens from Operations when ready.";
        return RedirectToPage(new { id });
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
            TempData["AssessmentError"] =
                "Check the estimate's lines: an operation, a quantity, hours or an amount does not read as a number.";
            return RedirectToPage(new { id, estimate = estimateId?.ToString("D") });
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
            TempData["AssessmentStatus"] = "The estimate was saved.";
            return RedirectToPage(new { id, estimate = saved.SpecificationId.ToString("D") });
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            TempData["AssessmentError"] = MutationRefusalMessage(
                exception, "The estimate was not saved because the case changed or another editor holds it. Retry the operation.");
            return RedirectToPage(new { id, estimate = estimateId?.ToString("D") });
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
            TempData["AssessmentStatus"] = "The estimate was duplicated.";
            return RedirectToPage(new { id, estimate = copy.SpecificationId.ToString("D") });
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            TempData["AssessmentError"] = MutationRefusalMessage(
                exception, "The estimate was not duplicated because the case changed or another editor holds it. Retry the operation.");
            return RedirectToPage(new { id, estimate = estimateId.ToString("D") });
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
            TempData["AssessmentError"] = "Give the reason this estimate is deleted.";
            return RedirectToPage(new { id, estimate = estimateId.ToString("D") });
        }

        try
        {
            await discardEstimate.ExecuteAsync(
                new(id, currentCaseVersion, actor, operationKey, reason.Trim(), editLeaseToken!, estimateId),
                cancellationToken);
            ClearLeaseState();
            TempData["AssessmentStatus"] = "The estimate was deleted.";
            return RedirectToPage(new { id });
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            TempData["AssessmentError"] = MutationRefusalMessage(
                exception, "The estimate was not deleted because the case changed or another editor holds it. Retry the operation.");
            return RedirectToPage(new { id, estimate = estimateId.ToString("D") });
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
            TempData["AssessmentStatus"] = "The estimate is now the case's current estimate.";
            return RedirectToPage(new { id, estimate = estimateId.ToString("D") });
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            TempData["AssessmentError"] = MutationRefusalMessage(
                exception, "The estimate was not made current because the case changed or another editor holds it. Retry the operation.");
            return RedirectToPage(new { id, estimate = estimateId.ToString("D") });
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
            TempData["AssessmentError"] = "Only an Engineer can change an estimate.";
            return RedirectToPage(new { id });
        }
        if (access.IsReadOnly)
        {
            TempData["AssessmentError"] = "The case is read-only once Complete.";
            return RedirectToPage(new { id });
        }
        if (!IsOperationKeyValid(operationKey))
        {
            TempData["AssessmentError"] = "The form has expired. Retry the operation.";
            return RedirectToPage(new { id });
        }
        if (string.IsNullOrWhiteSpace(editLeaseToken))
        {
            TempData["AssessmentError"] = NotInEditMode;
            return RedirectToPage(new { id });
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
        var rows = new List<EstimateEditorLine>(operations.Length);
        var lines = new List<EstimateLineInput>(operations.Length);
        var existingLineIds = new List<Guid?>(operations.Length);
        var linesAreValid = true;
        static string Field(string?[] values, int index) =>
            index >= 0 && index < values.Length && values[index] is not null ? values[index]! : string.Empty;
        for (var index = 0; index < operations.Length; index++)
        {
            var operation = operations[index] ?? string.Empty;
            var description = Field(form["lineDescription"].ToArray(), index);
            var partNumber = Field(form["linePartNumber"].ToArray(), index);
            var quantity = Field(form["lineQuantity"].ToArray(), index);
            var labourHours = Field(form["lineLabourHours"].ToArray(), index);
            var paintHours = Field(form["linePaintHours"].ToArray(), index);
            var partPounds = Field(form["linePartPounds"].ToArray(), index);
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
            TempData["AssessmentError"] = "Only an Engineer can import an estimate.";
            return RedirectToPage(new { id });
        }
        if (importAccess.IsReadOnly)
        {
            TempData["AssessmentError"] = "The case is read-only once Complete.";
            return RedirectToPage(new { id });
        }
        if (!IsOperationKeyValid(operationKey))
        {
            TempData["AssessmentError"] = "The form has expired. Retry the operation.";
            return RedirectToPage(new { id });
        }
        var trimmedName = name?.Trim();
        if (string.IsNullOrEmpty(trimmedName))
        {
            TempData["AssessmentError"] = "Name the imported estimate.";
            return RedirectToPage(new { id });
        }
        var parser = string.Equals(source, "json", StringComparison.OrdinalIgnoreCase)
            ? jsonEstimateParser
            : estimateParser;
        var isJson = ReferenceEquals(parser, jsonEstimateParser);
        if (estimateFile is null || estimateFile.Length is <= 0 or > MaximumEstimateUploadBytes)
        {
            TempData["AssessmentError"] = "Choose a non-empty estimate file of 10 MB or less.";
            return RedirectToPage(new { id });
        }
        if (!parser.CanParse(estimateFile.FileName, estimateFile.ContentType))
        {
            TempData["AssessmentError"] = isJson
                ? "Only a JSON estimate can be imported from this source."
                : "Only a PDF estimate can be imported from this source.";
            return RedirectToPage(new { id });
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
            TempData["AssessmentError"] = exception.Message;
            return RedirectToPage(new { id });
        }

        var details = await getCase.ExecuteAsync(new(id, actor), cancellationToken);
        if (details is null)
        {
            return NotFound();
        }
        if (string.IsNullOrWhiteSpace(editLeaseToken))
        {
            TempData["AssessmentError"] = NotInEditMode;
            return RedirectToPage(new { id });
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
            TempData["AssessmentError"] = MutationRefusalMessage(
                exception,
                "The estimate was not imported because the case changed or another editor holds it. "
                + "Nothing was recorded; retry the import.");
            return RedirectToPage(new { id });
        }

        try
        {
            // Retaining the document was itself a case mutation, so it ended edit mode and moved
            // the version. The estimate is the second half of one operator action, so this
            // re-enters edit mode on the operator's behalf.
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
            TempData["AssessmentStatus"] =
                $"{trimmedName} was imported as a draft with {parsed.Lines.Count} lines for your review. "
                + "The original document is kept on the case.";
            return RedirectToPage(new { id, estimate = imported.SpecificationId.ToString("D") });
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Whatever this leaves behind, the retained-document half already committed and the
            // lease state is whatever the failure made it — never a token this page invented.
            HandleLeaseFailure(id, PeekLeaseToken(), exception);
            TempData["AssessmentError"] = MutationRefusalMessage(
                exception,
                "The original document was kept on the case, but the estimate lines were not "
                + "recorded because the case changed. Retry the import.");
        }

        return RedirectToPage(new { id });
    }

    /// <summary>
    /// The repair-specification policy refuses with complete operator-safe
    /// sentences; version and lease conflicts carry internals, so they get
    /// the fallback instead.
    /// </summary>
    private static string MutationRefusalMessage(Exception exception, string fallback) =>
        exception is InvalidOperationException
            and not CaseVersionConflictException
            and not CaseEditLeaseConflictException
            and not CaseEditLeaseExpiredException
            and not CaseOperationConflictException
            ? exception.Message
            : fallback;

    /// <summary>
    /// Loads the case's edit authority and live documents for this render.
    /// The assessment projection does not carry the lease, so it comes from
    /// the workspace query this page already depends on.
    /// </summary>
    private async Task<CaseDetails?> LoadCaseContextAsync(
        Guid id,
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        var details = await getCase.ExecuteAsync(new(id, actor), cancellationToken);
        if (details is null)
        {
            return null;
        }

        CaseIsArchived = details.Workflow.Archive is not null;
        RestoreLeaseState(id, actor, details.ActiveEditLease);
        if (details.ActiveEditLease is not { } activeLease)
        {
            return details;
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
        return details;
    }

    /// <summary>
    /// The record-bar conditions, computed once per render so the controls
    /// and their gating spans cannot disagree: Send to Claude needs an
    /// editable case, the switch on and a confirmed Engineer's Value; the
    /// report draft needs a projectable assessment.
    /// </summary>
    private async Task EvaluateRecordBarConditionsAsync(CancellationToken cancellationToken)
    {
        var sendConditions = new List<string>();
        if (IsReadOnly)
        {
            sendConditions.Add("Read-only once Complete");
        }
        else if (!await sendToAiControl.IsEnabledAsync(cancellationToken))
        {
            sendConditions.Add("Sending to AI is disabled by an Administrator");
        }
        else if (EngineerValue is null)
        {
            sendConditions.Add("A confirmed Engineer's Value is required");
        }
        SendToClaudeCondition = sendConditions.FirstOrDefault();

        ReportDraftCondition = ReportDraftPreparation is null
            ? "Not available for this case"
            : ReportDraftPreparation.CanGenerate
                ? null
                : "Not ready";
    }

    private static bool IsMiles(string? unit) =>
        unit is null || string.Equals(unit, "miles", StringComparison.OrdinalIgnoreCase);

    private async Task<bool> CanAccessAsync(
        Guid caseId,
        ActionActor actor,
        CancellationToken cancellationToken) =>
        (await getAssessmentAccess.ExecuteAsync(
            new(caseId, actor),
            cancellationToken))?.CanOpen == true;

    private static bool IsOperationKeyValid(string value) =>
        Guid.TryParseExact(value, "N", out var operationId) && operationId != Guid.Empty;
}
