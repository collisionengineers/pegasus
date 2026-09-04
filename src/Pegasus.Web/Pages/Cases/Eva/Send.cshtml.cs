using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Eva;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;
using Pegasus.Web.Pages.Cases;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Cases.Eva;

/// <summary>
/// EXT-04: the one place a case leaves for EVA.
///
/// There are two routes — the archive an operator drags into EVA, and the API
/// submission — and putting both on the case bar would make the busiest
/// surface in the product carry a choice most operators make once per case.
/// So the workspace bar carries one control ("Send to EVA") and the choice
/// itself is the EVA handoff.
///
/// The handoff has two surfaces and one set of routes. The workspace draws it
/// as a dialog (EPIC-011 §1.8), which needs script; this page is the same
/// handoff as a route, and is the one that still works with script off. The
/// submission handler below is the POST target of both.
///
/// The page itself records nothing. Both routes are POSTs: the export to its
/// own unchanged handler, the submission to the one below.
/// </summary>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed partial class SendModel(
    ICaseDataQueries caseDataQueries,
    ICaseWorkflowQueries workflowQueries,
    IStaffAccountQueries staffAccountQueries,
    IEvaSubmissionModeStore modeStore,
    IEvaSubmissionQueries submissionQueries,
    ILogger<SendModel> logger,
    ISubmitCaseToEva? submitCaseToEva = null) : CaseMutationPageModel(logger)
{
    public Guid CaseId { get; private set; }
    public string Reference { get; private set; } = string.Empty;
    public EvaSubmissionRecord? LastSubmission { get; private set; }

    /// <summary>
    /// Whether the API button renders at all. Three things must hold: the host
    /// composed a transport, the principal enabled manual submission, and the
    /// case has not already reached EVA. Any of them false and the page offers
    /// the export alone — which is the honest shape, because an uncomposed or
    /// unenabled route is not a route.
    /// </summary>
    public EvaHandoffViewModel? Handoff { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid caseId, CancellationToken cancellationToken)
    {
        if (caseId == Guid.Empty)
        {
            return NotFound();
        }

        var caseData = await caseDataQueries.GetAsync(caseId, cancellationToken);
        if (caseData is null)
        {
            return NotFound();
        }

        if (caseData.State is not (CaseLifecycleState.Review
            or CaseLifecycleState.ReportPreparation
            or CaseLifecycleState.PostReport))
        {
            return RedirectToDetails(caseId);
        }

        CaseId = caseId;
        Reference = caseData.Identity.Reference;
        LastSubmission = await submissionQueries.GetLatestAsync(caseId, cancellationToken);

        var workflow = await workflowQueries.GetAsync(caseId, cancellationToken);
        if (workflow is null)
        {
            return NotFound();
        }
        var profiles = await staffAccountQueries.ListSignOffEngineersAsync(cancellationToken);
        var signOffEngineer = CaseSignOffEngineerResolver.Resolve(
            workflow.SignOffEngineerId,
            workflow.AssignedEngineerId,
            profiles);
        var engineer = workflow.AssignedEngineerId is { } engineerId
            ? await staffAccountQueries.GetAsync(engineerId, cancellationToken)
            : null;
        var modes = submitCaseToEva is null
            ? EvaSubmissionModes.Disabled
            : await modeStore.GetForPrincipalAsync(
                caseData.Identity.PrincipalCode,
                cancellationToken);
        Handoff = new(
            caseId,
            workflow.Version,
            workflow.State,
            EditLeaseToken: null,
            engineer?.UserName ?? OperatorLabels.CaseWorkspace.Unassigned,
            EngineerOptions: [],
            signOffEngineer?.PrintedName ?? OperatorLabels.CaseWorkspace.Unassigned,
            signOffEngineer?.StaffId,
            SignOffEngineerOptions: [],
            caseData.Completeness.Values.InstructionComplete,
            caseData.Completeness.Values.ImagesComplete,
            submitCaseToEva is not null,
            EvaSubmissionPolicy.AllowsManualSubmission(modes),
            NewOperationKey(),
            NewOperationKey());
        return Page();
    }

    public async Task<IActionResult> OnPostSubmitAsync(
        Guid caseId,
        string operationKey,
        CancellationToken cancellationToken)
    {
        if (caseId == Guid.Empty)
        {
            return NotFound();
        }
        if (submitCaseToEva is null)
        {
            return NotFound();
        }
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            var result = await submitCaseToEva.ExecuteAsync(
                new(caseId, actor, RequireOperationKey(operationKey), EvaSubmissionTrigger.Manual),
                cancellationToken);
            if (result is null)
            {
                return NotFound();
            }
            if (result.Submission is not { } submission)
            {
                TempData["CaseError"] = result.BlockingReasons.Count > 0
                    ? string.Join(" ", result.BlockingReasons)
                    : "The case could not be sent to EVA.";
                return RedirectToDetails(caseId);
            }

            // All four outcomes are reported distinctly, because FRD-07
            // requires they stay distinct and an operator's next move differs
            // for each: nothing after a success, re-send after a rejection
            // once the cause is fixed, nothing after a partial because the
            // case did reach EVA, and wait after an unknown because a retry is
            // already scheduled.
            if (submission.Outcome == EvaSubmissionOutcome.Succeeded)
            {
                TempData["CaseStatus"] = submission.FileReference is { } reference
                    ? $"Sent to EVA. File reference {reference}."
                    : "Sent to EVA.";
            }
            else
            {
                TempData["CaseError"] = Describe(submission);
            }

            return RedirectToDetails(caseId);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (EvaSubmissionNotEnabledException)
        {
            TempData["CaseError"] = OperatorLabels.CaseWorkspace.EvaApiNotEnabled;
            return RedirectToDetails(caseId);
        }
        // A submission reads every photograph out of Box before it reaches
        // EVA, so a custody transport failure is an ordinary way for it to
        // fail; without HttpRequestException here the operator would get the
        // generic error page instead of their case with a reason on it, the
        // same way PLAT-039 found for the export.
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException
            or InvalidDataException
            or IOException
            or HttpRequestException
            or UnauthorizedAccessException)
        {
            LogEvaSubmissionFailed(logger, caseId, exception);
            TempData["CaseError"] = "The case could not be sent to EVA.";
            return RedirectToDetails(caseId);
        }
    }

    /// <summary>
    /// What to tell the operator about an outcome that was not a clean
    /// success. EVA's own words are included where it gave any, because
    /// "'Agent' field value couldn't be bound" is actionable and "the
    /// submission failed" is not.
    /// </summary>
    private static string Describe(EvaSubmissionResult submission) => submission.Outcome switch
    {
        EvaSubmissionOutcome.Partial =>
            $"Sent to EVA, which returned no reference. {submission.FailureDetail}".TrimEnd(),
        EvaSubmissionOutcome.Rejected =>
            $"EVA refused the case. {submission.FailureDetail}".TrimEnd(),
        _ => $"EVA could not be reached. {submission.FailureDetail}".TrimEnd()
    };

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "EVA API submission failed for case {CaseId}.")]
    private static partial void LogEvaSubmissionFailed(
        ILogger logger,
        Guid caseId,
        Exception exception);
}
