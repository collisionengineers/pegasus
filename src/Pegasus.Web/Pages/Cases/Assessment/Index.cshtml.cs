using System.Globalization;
using System.IO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.AiWork;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Reports;
using Pegasus.Core.Vehicle;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Pages.Cases.Assessment;

/// <summary>
/// The Send to AI wiring for the assessment surface (AI-09; see ADR-0031 /
/// FRD-11: docs/adr/0031-automation-actor-contract-without-eva-export-tools.md,
/// docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md), plus
/// the DELIV-012 report-draft entry point. This model binds the case
/// identity header, the Send to Claude panel, the report-draft panel, and
/// the PAV slider's recorded-evidence data; the section forms themselves
/// stay unbound design markup until the UI-15 activation task wires the
/// staff save paths. The report draft reads already-saved assessment values
/// through the same store as the rest of this page — it does not depend on
/// those unbound forms.
/// </summary>
/// <remarks>
/// CASE-024: the save paths this page does have now run under edit mode the
/// operator enters, the same one server-owned lease the case workspace claims,
/// instead of each handler claiming a lease of its own for the length of one
/// save. An engineer working an assessment is therefore visible to other staff
/// as the case's editor, which is what the lease is for.
/// </remarks>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class IndexModel(
    IGetCase getCase,
    IGetAssessmentAccess getAssessmentAccess,
    IGetAssessmentWorkspace getAssessmentWorkspace,
    ISendToAiControl sendToAiControl,
    GenerateCaseAssessmentReportDraft generateReportDraft,
    IRepairSpecificationStore repairSpecifications,
    IEstimateDocumentParser estimateParser,
    IAddCaseDocument addCaseDocument,
    IAcquireCaseEditLease acquireLease,
    IHeartbeatCaseEditLease heartbeatLease,
    IReleaseCaseEditLease releaseLease,
    IDescribeCaseEditAuthorityHolder describeEditAuthorityHolder,
    ISaveAssessment saveAssessment,
    TimeProvider timeProvider,
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

    public AiWorkRequestRecord? LatestRequest { get; private set; }

    /// <summary>Panel state: available, in-flight, sent, completed, failed, unavailable.</summary>
    public string PanelState { get; private set; } = "unavailable";

    public IReadOnlyList<string> UnavailableReasons { get; private set; } = [];

    public string? FailureReason { get; private set; }

    public string SendOperationKey { get; private set; } = NewOperationKey();

    public string ReconcileOperationKey { get; private set; } = NewOperationKey();

    /// <summary>
    /// The DELIV-012 report-draft entry point's readiness: ready to render,
    /// or every named reason it is not (case unrecognized when null).
    /// </summary>
    public AssessmentReportDraftPreparation? ReportDraftPreparation { get; private set; }

    public string ReportDraftOperationKey { get; private set; } = NewOperationKey();

    /// <summary>
    /// ENG-003: the one readiness list the page renders. <see cref="ReportDraftPreparation"/>'s
    /// <c>Reasons</c> already reuses <see cref="AssessmentPolicy.EvaluatePostReviewReadiness"/> as its
    /// base and only appends report-specific requirements on top
    /// (<see cref="Pegasus.Core.Reports.AssessmentReportProjection.Project"/>). The access gate
    /// guarantees that Review-entry requirements excluded by this list already passed.
    /// </summary>
    public IReadOnlyList<AssessmentReadinessItem> CombinedReadiness { get; private set; } = [];

    /// <summary>ENG-002: the case's current repair specification, draft first.</summary>
    public RepairSpecificationVersion? DraftSpecification { get; private set; }

    public RepairSpecificationVersion? AcceptedSpecification { get; private set; }

    public bool ActorIsEngineer { get; private set; }

    public string ImportOperationKey { get; private set; } = NewOperationKey();

    public string AcceptOperationKey { get; private set; } = NewOperationKey();

    /// <summary>A saved assessment value for one vocabulary path, or null.</summary>
    public string? SavedValue(string path) => Assessment?.Field(path)?.Value;

    /// <summary>
    /// The Mileage prefill: the saved assessment value, else confirmed vehicle
    /// evidence, else the DVSA estimate (miles only) — CASE-008.
    /// </summary>
    public string? MileagePrefill
    {
        get
        {
            if (SavedValue("vehicle.odometer_miles") is { Length: > 0 } saved)
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

    /// <summary>The Source prefill: saved, else online data when the mileage came from evidence.</summary>
    public string? MileageSourcePrefill =>
        SavedValue("vehicle.odometer_miles") is { Length: > 0 }
            ? SavedValue("vehicle.mileage_source")
            : CaseMileageSourcePrefill();

    /// <summary>A vehicle-detail prefill: the saved assessment value, else lookup evidence.</summary>
    public string? VehiclePrefill(string path)
    {
        if (SavedValue(path) is { Length: > 0 } saved)
        {
            return saved;
        }

        var vehicle = Case?.Data?.Vehicle;
        var details = Case?.LatestVehicleObservation?.Vehicle;
        return path switch
        {
            "vehicle.make" => vehicle?.Make.Confirmed?.Value ?? vehicle?.Make.Fact?.Value ?? details?.Make,
            "vehicle.model" => vehicle?.Model.Confirmed?.Value ?? vehicle?.Model.Fact?.Value ?? details?.Model,
            "vehicle.year" => details?.ManufactureYear?.ToString(CultureInfo.InvariantCulture),
            "vehicle.engine_cc" => details?.EngineCapacityCc?.ToString(CultureInfo.InvariantCulture),
            "vehicle.fuel" => details?.FuelType,
            _ => null
        };
    }

    private string? CaseMileageSourcePrefill()
    {
        var mileage = Case?.Data?.Vehicle.Mileage;
        var selected = mileage?.Confirmed ?? mileage?.Fact;
        if (selected is not null)
        {
            return selected.Source.Kind == CaseDataSourceKind.VehicleLookup ? "online_data" : null;
        }

        return Case?.LatestVehicleObservation?.Mileage is { Unit: VehicleMileageUnit.Miles }
            ? "online_data"
            : null;
    }

    private static bool IsMiles(string? unit) =>
        unit is null || string.Equals(unit, "miles", StringComparison.OrdinalIgnoreCase);

    public string DamageOperationKey { get; private set; } = NewOperationKey();

    /// <summary>The saved damage location, highlighted on the diagram (ENG-006).</summary>
    public string? SavedImpactLocation =>
        Assessment?.Field(AssessmentVocabulary.ImpactLocation)?.Value;

    /// <summary>The case's recorded inspection mode, preselecting the method radios.</summary>
    public CaseInspectionMode? RecordedInspectionMode =>
        Case?.Data?.Inspection.Mode.Current?.Value;

    /// <summary>
    /// ENG-006: one click on a damage region saves it as the case's impact
    /// location through the assessment save seam, under the edit mode the
    /// operator entered — the same value the report prints and the Impact
    /// location dropdown edits.
    /// </summary>
    public async Task<IActionResult> OnPostSaveDamageAsync(
        Guid id,
        string operationKey,
        string? editLeaseToken,
        string? impactLocation,
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
            return RedirectToPage(new { id, section = "report" });
        }
        if (string.IsNullOrWhiteSpace(impactLocation))
        {
            TempData["AssessmentError"] = "Choose where the damage is.";
            return RedirectToPage(new { id, section = "report" });
        }
        if (string.IsNullOrWhiteSpace(editLeaseToken))
        {
            TempData["AssessmentError"] = NotInEditMode;
            return RedirectToPage(new { id, section = "report" });
        }

        var details = await getCase.ExecuteAsync(new(id, actor), cancellationToken);
        if (details is null)
        {
            return NotFound();
        }

        try
        {
            await saveAssessment.ExecuteAsync(
                new(
                    id,
                    details.Workflow.Version,
                    actor,
                    operationKey,
                    "Damage location marked on the assessment diagram.",
                    editLeaseToken,
                    new Dictionary<string, string?>
                    {
                        [AssessmentVocabulary.ImpactLocation] = impactLocation
                    }),
                cancellationToken);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            HandleLeaseFailure(id, editLeaseToken, exception);
            TempData["AssessmentError"] = exception.Message;
            return RedirectToPage(new { id, section = "report" });
        }

        // The save ended edit mode inside its own transaction, as every case mutation does, so
        // this browser forgets a token the server has already cleared.
        ClearLeaseState();
        TempData["AssessmentStatus"] = "Damage location saved.";
        return RedirectToPage(new { id, section = "report" });
    }

    public bool SendComposed => HttpContext.RequestServices.GetService<ISendCaseToAi>() is not null;

    /// <summary>
    /// The holder disclosure other staff see. The workspace projection carries the lease and the
    /// assessment projection does not, so this page reads it from the same
    /// <see cref="IGetCase"/> it already uses rather than widening
    /// <see cref="AssessmentWorkspace"/> with a second copy of it. The case version is not read
    /// that way — the assessment header already carries it.
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
        string? section,
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
            () => RedirectToPage(new { id, section }),
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
        string? section,
        CancellationToken cancellationToken) =>
        ReleaseLeaseAsync(
            releaseLease,
            id,
            operationKey,
            editLeaseToken,
            () => RedirectToPage(new { id, section }),
            cancellationToken);

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        Case = await getAssessmentWorkspace.ExecuteAsync(new(id, actor), cancellationToken);
        if (Case is null)
        {
            return NotFound();
        }

        await RestoreEditModeAsync(id, actor, cancellationToken);
        Assessment = Case.Assessment;
        DraftSpecification = Case.DraftSpecification;
        AcceptedSpecification = Case.AcceptedSpecification;
        ActorIsEngineer = actor.IsInRole(StaffRole.Engineer);
        LatestRequest = Case.LatestRequest;
        ReportDraftPreparation = AssessmentReportProjection.Prepare(Assessment, costs: null);
        CombinedReadiness = ReportDraftPreparation.Reasons;
        await EvaluatePanelStateAsync(cancellationToken);
        return Page();
    }

    /// <summary>
    /// Renders and returns the report draft PDF (DELIV-012). Readiness is
    /// decided by <see cref="AssessmentReportProjection"/>, the same
    /// readiness rail rendered on this page; a case that is not ready
    /// returns to the page with every outstanding reason named rather than
    /// throwing.
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
    /// ENG-002: the drag-and-drop estimate import. The file is parsed first
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
            return RedirectToEstimate(id);
        }
        if (!IsOperationKeyValid(operationKey))
        {
            TempData["AssessmentError"] = "The form has expired. Retry the operation.";
            return RedirectToEstimate(id);
        }
        if (estimateFile is null || estimateFile.Length is <= 0 or > MaximumEstimateUploadBytes)
        {
            TempData["AssessmentError"] = "Choose a non-empty estimate file of 10 MB or less.";
            return RedirectToEstimate(id);
        }
        if (!estimateParser.CanParse(estimateFile.FileName, estimateFile.ContentType))
        {
            TempData["AssessmentError"] = "Only a PDF estimate can be imported at present.";
            return RedirectToEstimate(id);
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
            return RedirectToEstimate(id);
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
            return RedirectToEstimate(id);
        }
        var accepted = await repairSpecifications.GetCurrentAcceptedAsync(id, cancellationToken);
        var trimmedReason = reason?.Trim();
        if (accepted is not null && string.IsNullOrEmpty(trimmedReason))
        {
            TempData["AssessmentError"] = "This case already has an accepted repair specification. "
                + "Give the reason this import corrects it.";
            return RedirectToEstimate(id);
        }

        if (string.IsNullOrWhiteSpace(editLeaseToken))
        {
            TempData["AssessmentError"] = NotInEditMode;
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
            return RedirectToEstimate(id);
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

        return RedirectToEstimate(id);
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
            return RedirectToEstimate(id);
        }
        if (!IsOperationKeyValid(operationKey))
        {
            TempData["AssessmentError"] = "The form has expired. Retry the operation.";
            return RedirectToEstimate(id);
        }
        if (repairerVatRegistered is not ("true" or "false"))
        {
            TempData["AssessmentError"] = "Answer whether the repairer is VAT registered.";
            return RedirectToEstimate(id);
        }
        if (string.IsNullOrWhiteSpace(editLeaseToken))
        {
            TempData["AssessmentError"] = NotInEditMode;
            return RedirectToEstimate(id);
        }

        var draft = await repairSpecifications.GetCurrentDraftAsync(id, cancellationToken);
        if (draft is null || draft.SpecificationId != specificationId)
        {
            TempData["AssessmentError"] = "The draft repair specification changed. Review it again.";
            return RedirectToEstimate(id);
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

        return RedirectToEstimate(id);
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

    private RedirectToPageResult RedirectToEstimate(Guid id) =>
        RedirectToPage(new { id, section = "estimate" });


    /// <summary>
    /// Loads the case's edit authority for this render. The assessment projection does not carry
    /// the lease, so it comes from the workspace query this page already depends on.
    /// </summary>
    private async Task RestoreEditModeAsync(
        Guid id,
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        var details = await getCase.ExecuteAsync(new(id, actor), cancellationToken);
        if (details is null)
        {
            return;
        }

        CaseIsArchived = details.Workflow.Archive is not null;
        RestoreLeaseState(id, actor, details.ActiveEditLease);
        if (details.ActiveEditLease is not { } activeLease)
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

    public async Task<IActionResult> OnPostSendAsync(
        Guid id,
        string operationKey,
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

        var sendCaseToAi = HttpContext.RequestServices.GetService<ISendCaseToAi>();
        if (sendCaseToAi is null)
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

        var result = await sendCaseToAi.ExecuteAsync(
            new(
                id,
                actor,
                operationKey,
                $"Work the assessment for case {details.Summary.Reference} in Pegasus: "
                + "read the case, record your working values through the automation tools, "
                + "and reply done when finished."),
            cancellationToken);
        TempData["AssessmentStatus"] = result.Outcome switch
        {
            SendCaseToAiOutcome.HandedOff => "Sent. Changes will appear on this case for your review.",
            SendCaseToAiOutcome.Failed => "Nothing was sent. " + string.Join(" ", result.Reasons),
            _ => "Sending is not available. " + string.Join(" ", result.Reasons)
        };
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostReconcileAsync(
        Guid id,
        Guid requestId,
        string operationKey,
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

        var reconcile = HttpContext.RequestServices.GetService<IReconcileAiWorkRequest>();
        if (reconcile is null)
        {
            return NotFound();
        }
        if (requestId == Guid.Empty || !IsOperationKeyValid(operationKey))
        {
            TempData["AssessmentError"] = "The form has expired. Retry the operation.";
            return RedirectToPage(new { id });
        }

        try
        {
            var record = await reconcile.ExecuteAsync(
                new(id, requestId, actor, operationKey),
                cancellationToken);
            TempData["AssessmentStatus"] = record.State switch
            {
                AiWorkRequestState.Completed =>
                    "Claude has finished. Review the changes on this case.",
                AiWorkRequestState.Failed =>
                    "The hand-off failed. " + (record.ReplyMessage ?? record.ClosureReason ?? string.Empty),
                AiWorkRequestState.Expired =>
                    "The request expired before a reply was recorded.",
                _ => "No reply has been recorded yet."
            };
        }
        catch (KeyNotFoundException)
        {
            TempData["AssessmentError"] = "The Send to AI request was not found.";
        }

        return RedirectToPage(new { id });
    }

    /// <summary>
    /// ENG-003: the one place the "N issues detected" pluralisation rule
    /// lives, so the readiness panel's summary chip and the report-draft
    /// "Not ready" card's reference back to it never drift apart.
    /// </summary>
    public static string IssueSummaryText(int count) =>
        $"{count} {(count == 1 ? "issue" : "issues")} detected";

    public string FieldValue(string path) => Assessment?.Field(path)?.Value ?? string.Empty;

    public decimal? MoneyField(string path) =>
        Assessment?.Field(path)?.Value is { } value
            && decimal.TryParse(
                value,
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var parsed)
            ? parsed
            : null;

    private async Task EvaluatePanelStateAsync(CancellationToken cancellationToken)
    {
        // The composition gate decides first: an uncomposed capability is
        // absent from the page entirely (docs/design/README.md), and with
        // Features:SendToAi off there is no reconcile handler to post to.
        if (!SendComposed)
        {
            return;
        }

        if (LatestRequest is { } request)
        {
            var expired = request.ExpiresAtUtc <= timeProvider.GetUtcNow();
            switch (request.State)
            {
                case AiWorkRequestState.Created or AiWorkRequestState.HandedOff when !expired:
                    PanelState = "sent";
                    return;
                case AiWorkRequestState.Completed:
                    PanelState = "completed";
                    return;
                case AiWorkRequestState.Failed:
                    PanelState = "failed";
                    FailureReason = request.ReplyMessage ?? request.ClosureReason;
                    return;
            }
        }

        var reasons = new List<string>();
        if (!await sendToAiControl.IsEnabledAsync(cancellationToken))
        {
            reasons.Add("Sending to AI is disabled by an Administrator.");
        }

        if (Case is { } details
            && !AiWorkPolicy.IsEligibleCaseState(details.Header.State))
        {
            reasons.Add("The case is not in a state that accepts assessment work.");
        }

        PanelState = reasons.Count == 0 ? "available" : "unavailable";
        UnavailableReasons = reasons;
    }

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
