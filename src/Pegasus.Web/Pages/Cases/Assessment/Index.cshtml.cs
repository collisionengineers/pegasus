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

namespace Pegasus.Web.Pages.Cases.Assessment;

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
    IRepairSpecificationStore repairSpecifications,
    IEstimateDocumentParser estimateParser,
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

    /// <summary>ENG-002: the case's current repair specification, draft first.</summary>
    public RepairSpecificationVersion? DraftSpecification { get; private set; }

    public RepairSpecificationVersion? AcceptedSpecification { get; private set; }

    public bool ActorIsEngineer { get; private set; }

    /// <summary>Instruction-role live files for the evidence rail.</summary>
    public IReadOnlyList<CaseFile> InstructionFiles { get; private set; } = [];

    /// <summary>Custody-confirmed case images for the evidence rail.</summary>
    public IReadOnlyList<CaseEvidenceImage> EvidenceImages { get; private set; } = [];

    public string ImportOperationKey { get; private set; } = NewOperationKey();

    public string AcceptOperationKey { get; private set; } = NewOperationKey();

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

    /// <summary>The condition naming why an estimate import is not offered, or null when offered.</summary>
    public string? ImportCondition =>
        IsReadOnly
            ? "Read-only once Complete"
            : !ActorIsEngineer
                ? "Only an Engineer can import an estimate"
                : DraftSpecification is not null
                    ? "A draft estimate is awaiting acceptance"
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

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
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
        DraftSpecification = Case.DraftSpecification;
        AcceptedSpecification = Case.AcceptedSpecification;
        ActorIsEngineer = actor.IsInRole(StaffRole.Engineer);
        IsReadOnly = access.IsReadOnly;
        Claimant = details?.Summary.Claimant;
        InstructionFiles = CaseFiles.Live(details?.Documents ?? [])
            .Where(file => file.Occurrence.SemanticRole == DocumentSemanticRole.Instruction)
            .ToList();
        EvidenceImages = await evidenceImageQueries.ListForCaseAsync(id, cancellationToken);
        // The same inputs the projection source hands Project (Costs null,
        // the Current estimate as the cost block, ENG-026), so the control's
        // condition cannot disagree with what generating would decide.
        ReportDraftPreparation = AssessmentReportProjection.Prepare(
            Assessment,
            costs: null,
            currentEstimate: AcceptedSpecification);
        await EvaluateRecordBarConditionsAsync(cancellationToken);
        return Page();
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
    /// ENG-002: the estimate import. The file is parsed first
    /// (no side effects — a rejected parse retains nothing), then retained
    /// through the existing case-document custody path, then landed as a
    /// draft repair specification with the route, source version and hash of
    /// the retained document. The specification stays a draft until an
    /// Engineer accepts it; nothing feeds a report from a draft.
    /// </summary>
    public async Task<IActionResult> OnPostImportEstimateAsync(
        Guid id,
        string operationKey,
        string? editLeaseToken,
        string? reason,
        IFormFile? estimateFile,
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
        if (!actor.IsInRole(StaffRole.Engineer))
        {
            TempData["AssessmentError"] = "Only an Engineer can import an estimate.";
            return RedirectToPage(new { id });
        }
        if (!IsOperationKeyValid(operationKey))
        {
            TempData["AssessmentError"] = "The form has expired. Retry the operation.";
            return RedirectToPage(new { id });
        }
        if (estimateFile is null || estimateFile.Length is <= 0 or > MaximumEstimateUploadBytes)
        {
            TempData["AssessmentError"] = "Choose a non-empty estimate file of 10 MB or less.";
            return RedirectToPage(new { id });
        }
        if (!estimateParser.CanParse(estimateFile.FileName, estimateFile.ContentType))
        {
            TempData["AssessmentError"] = "Only a PDF estimate can be imported at present.";
            return RedirectToPage(new { id });
        }

        await using var buffer = new MemoryStream((int)estimateFile.Length);
        await estimateFile.CopyToAsync(buffer, cancellationToken);
        var content = buffer.GetBuffer().AsMemory(0, checked((int)buffer.Length));

        ParsedEstimate parsed;
        try
        {
            parsed = estimateParser.Parse(content);
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
        if (await repairSpecifications.GetCurrentDraftAsync(id, cancellationToken) is not null)
        {
            TempData["AssessmentError"] = "A draft repair specification already exists for this case. "
                + "Accept it or replace its lines before importing another estimate.";
            return RedirectToPage(new { id });
        }
        var accepted = await repairSpecifications.GetCurrentAcceptedAsync(id, cancellationToken);
        var trimmedReason = reason?.Trim();
        if (accepted is not null && string.IsNullOrEmpty(trimmedReason))
        {
            TempData["AssessmentError"] = "This case already has an accepted repair specification. "
                + "Give the reason this import corrects it.";
            return RedirectToPage(new { id });
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
                    "application/pdf",
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
            // the version. The draft is the second half of one operator action, so this re-enters
            // edit mode on their behalf rather than making them do it between two halves.
            var draftLease = await acquireLease.ExecuteAsync(
                new(id, caseVersion + 1, actor, NewOperationKey()),
                cancellationToken);
            StoreLeaseAuthority(id, draftLease.Token);
            await repairSpecifications.StartDraftAsync(
                new(
                    id,
                    caseVersion + 1,
                    new(estimateParser.Route, artifactIdentity, parsed.SourceVersion, retained.Version.Sha256),
                    actor,
                    operationKey,
                    string.IsNullOrEmpty(trimmedReason) ? "Estimate imported from a document" : trimmedReason,
                    draftLease.Token,
                    accepted?.SpecificationId,
                    parsed.Lines),
                cancellationToken);
            ClearLeaseState();
            TempData["AssessmentStatus"] =
                $"The estimate was imported as a draft with {parsed.Lines.Count} lines for your review. "
                + "The original document is kept on the case.";
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
    /// ENG-002: Engineer acceptance of the current draft specification. The
    /// money figures are typed from the retained original document — no
    /// derivation from lines exists until EXT-09's formula authority is
    /// accepted — and the Core policy enforces that the total equals the
    /// typed figures plus VAT.
    /// </summary>
    public async Task<IActionResult> OnPostAcceptSpecificationAsync(
        Guid id,
        string operationKey,
        string? editLeaseToken,
        Guid specificationId,
        int specificationVersion,
        decimal labour,
        decimal parts,
        decimal paintMaterials,
        decimal specialistOther,
        decimal vat,
        string? repairerVatRegistered,
        string? reason,
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
        if (!actor.IsInRole(StaffRole.Engineer))
        {
            TempData["AssessmentError"] = "Only an Engineer can accept a repair specification.";
            return RedirectToPage(new { id });
        }
        if (!IsOperationKeyValid(operationKey))
        {
            TempData["AssessmentError"] = "The form has expired. Retry the operation.";
            return RedirectToPage(new { id });
        }
        if (repairerVatRegistered is not ("true" or "false"))
        {
            TempData["AssessmentError"] = "Answer whether the repairer is VAT registered.";
            return RedirectToPage(new { id });
        }
        if (string.IsNullOrWhiteSpace(editLeaseToken))
        {
            TempData["AssessmentError"] = NotInEditMode;
            return RedirectToPage(new { id });
        }

        var draft = await repairSpecifications.GetCurrentDraftAsync(id, cancellationToken);
        if (draft is null || draft.SpecificationId != specificationId)
        {
            TempData["AssessmentError"] = "The draft repair specification changed. Review it again.";
            return RedirectToPage(new { id });
        }
        var details = await getCase.ExecuteAsync(new(id, actor), cancellationToken);
        if (details is null)
        {
            return NotFound();
        }

        try
        {
            var basis = new RepairCalculationBasis(
                labour,
                parts,
                paintMaterials,
                specialistOther,
                repairerVatRegistered == "true",
                vat,
                labour + parts + paintMaterials + specialistOther + vat,
                $"{RepairSpecificationPolicy.PolicyKey}/v{RepairSpecificationPolicy.PolicyVersion}");
            await repairSpecifications.AcceptAsync(
                new(
                    id,
                    details.Workflow.Version,
                    specificationId,
                    specificationVersion,
                    draft.Source,
                    basis,
                    actor,
                    operationKey,
                    string.IsNullOrWhiteSpace(reason) ? "Repair specification accepted" : reason.Trim(),
                    editLeaseToken),
                cancellationToken);
            ClearLeaseState();
            TempData["AssessmentStatus"] = "The repair specification was accepted.";
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
                "The repair specification was not accepted because the case changed or another "
                + "editor holds it. Review it again.");
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
