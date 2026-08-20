using System.Globalization;
using System.IO;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Core.Actors;
using Pegasus.Core.AiWork;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Pages.Cases.Assessment;

/// <summary>
/// The Send to AI wiring for the assessment surface (AI-09; see ADR-0021 /
/// FRD-11: docs/adr/0021-automation-actor-direct-write-assessment-contract.md,
/// docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md), plus
/// the DELIV-012 report-draft entry point. This model binds the case
/// identity header, the Send to Claude panel, the report-draft panel, and
/// the PAV slider's recorded-evidence data; the section forms themselves
/// stay unbound design markup until the UI-15 activation task wires the
/// staff save paths. The report draft reads already-saved assessment values
/// through the same store as the rest of this page — it does not depend on
/// those unbound forms.
/// </summary>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class IndexModel(
    IGetCase getCase,
    IGetCaseAssessment getAssessment,
    IAiWorkRequestStore workRequests,
    ISendToAiControl sendToAiControl,
    GenerateCaseAssessmentReportDraft generateReportDraft,
    IRepairSpecificationStore repairSpecifications,
    IEstimateDocumentParser estimateParser,
    IAddCaseDocument addCaseDocument,
    IAcquireCaseEditLease acquireLease,
    TimeProvider timeProvider) : PageModel
{
    /// <summary>The staff custody upload's own ceiling (Cases/Custody), reused unchanged.</summary>
    private const long MaximumEstimateUploadBytes = 10 * 1024 * 1024;

    public CaseDetails? Case { get; private set; }

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
    /// <c>Reasons</c> already reuses <see cref="AssessmentPolicy.EvaluateReadiness"/> as its
    /// base and only appends report-specific requirements on top
    /// (<see cref="Pegasus.Core.Reports.AssessmentReportProjection.Project"/>), so it is always a
    /// superset of <see cref="Assessment"/>'s own <c>Readiness</c> for the same case — using it
    /// here loses nothing the readiness rail previously showed on its own.
    /// </summary>
    public IReadOnlyList<AssessmentReadinessItem> CombinedReadiness { get; private set; } = [];

    /// <summary>ENG-002: the case's current repair specification, draft first.</summary>
    public RepairSpecificationVersion? DraftSpecification { get; private set; }

    public RepairSpecificationVersion? AcceptedSpecification { get; private set; }

    public bool ActorIsEngineer { get; private set; }

    public string ImportOperationKey { get; private set; } = NewOperationKey();

    public string AcceptOperationKey { get; private set; } = NewOperationKey();

    public bool SendComposed => HttpContext.RequestServices.GetService<ISendCaseToAi>() is not null;

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        Case = await getCase.ExecuteAsync(new(id, actor), cancellationToken);
        if (Case is null)
        {
            return NotFound();
        }

        Assessment = await getAssessment.ExecuteAsync(id, cancellationToken);
        DraftSpecification = await repairSpecifications.GetCurrentDraftAsync(id, cancellationToken);
        AcceptedSpecification = await repairSpecifications.GetCurrentAcceptedAsync(id, cancellationToken);
        ActorIsEngineer = actor.IsInRole(StaffRole.Engineer);
        LatestRequest = await workRequests.GetLatestForCaseAsync(id, cancellationToken);
        ReportDraftPreparation = await generateReportDraft.PrepareAsync(id, actor, cancellationToken);
        CombinedReadiness = ReportDraftPreparation?.Reasons ?? Assessment?.Readiness ?? [];
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
        string? reason,
        IFormFile? estimateFile,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
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

        var caseVersion = details.Workflow.Version;
        var artifactIdentity = $"estimate-import:{operationKey}";
        AddCaseDocumentResult retained;
        try
        {
            var documentLease = await acquireLease.ExecuteAsync(
                new(id, caseVersion, actor, NewOperationKey()),
                cancellationToken);
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
                    documentLease.Token),
                cancellationToken);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            TempData["AssessmentError"] = MutationRefusalMessage(
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
            var lease = await acquireLease.ExecuteAsync(
                new(id, details.Workflow.Version, actor, NewOperationKey()),
                cancellationToken);
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
                    lease.Token),
                cancellationToken);
            TempData["AssessmentStatus"] = "The repair specification was accepted.";
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
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
            ? exception.Message
            : fallback;

    private RedirectToPageResult RedirectToEstimate(Guid id) =>
        RedirectToPage(new { id, section = "estimate" });

    /// <summary>Operator wording for the specification's provenance route.</summary>
    public static string RouteLabel(RepairSpecificationSourceRoute route) => route switch
    {
        RepairSpecificationSourceRoute.Manual => "entered by hand",
        RepairSpecificationSourceRoute.Glasses => "imported from Glass's",
        RepairSpecificationSourceRoute.AudatexPdf => "imported from Audatex",
        RepairSpecificationSourceRoute.ApprovedAiProposal => "from an approved AI proposal",
        _ => "recorded before source tracking",
    };

    /// <summary>The same names the estimate line-type choices show.</summary>
    public static string LineTypeLabel(string type) => type switch
    {
        "rnr" => "Remove and refit",
        "repair" => "Repair",
        "new_part" => "New part",
        "check_labour" => "Check",
        "paint_new" => "Paint — new part",
        "paint_repair" => "Paint — repair",
        "paint_blend" => "Paint — blend",
        "paint_prep" => "Paint — preparation",
        "specialist_fixed" => "Specialist, fixed price",
        "specialist_wu" => "Specialist, by work units",
        _ => type,
    };

    public async Task<IActionResult> OnPostSendAsync(
        Guid id,
        string operationKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
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
        // The composition gate decides first. With Features:SendToAi off
        // there is no reconcile handler to post to, so a persisted request
        // must not render as an actionable Sent/Completed/Failed state.
        if (SendComposed && LatestRequest is { } request)
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
        if (!SendComposed)
        {
            reasons.Add("Sending to AI is not part of this deployment.");
        }
        else if (!await sendToAiControl.IsEnabledAsync(cancellationToken))
        {
            reasons.Add("Sending to AI is disabled by an Administrator.");
        }

        if (Case is { } details
            && !AiWorkPolicy.IsEligibleCaseState(details.Summary.State))
        {
            reasons.Add("The case is not in a state that accepts assessment work.");
        }

        PanelState = reasons.Count == 0 ? "available" : "unavailable";
        UnavailableReasons = reasons;
    }

    private bool TryGetActor(out ActionActor actor)
    {
        var created = StaffActorFactory.TryCreate(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            User.FindAll(ClaimTypes.Role).Select(claim => claim.Value),
            out var resolved);
        actor = resolved!;
        return created;
    }

    private static string NewOperationKey() => Guid.NewGuid().ToString("N");

    private static bool IsOperationKeyValid(string value) =>
        Guid.TryParseExact(value, "N", out var operationId) && operationId != Guid.Empty;
}
