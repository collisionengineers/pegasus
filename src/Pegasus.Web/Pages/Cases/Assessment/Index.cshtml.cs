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
using Pegasus.Core.Identity;
using Pegasus.Core.Reports;

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
    TimeProvider timeProvider) : PageModel
{
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
        LatestRequest = await workRequests.GetLatestForCaseAsync(id, cancellationToken);
        ReportDraftPreparation = await generateReportDraft.PrepareAsync(id, actor, cancellationToken);
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
