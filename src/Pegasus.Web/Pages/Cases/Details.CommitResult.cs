using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Actors;
using Pegasus.Core.AiWork;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// The answer to a save-as-you-go commit (FRD-16). Every field commit of the
/// page script posts the Save handler. The script keeps every section as the
/// operator has it and swaps only the notices, the ribbon, the aside and the
/// dialogs, copies the next authority out of the Save form, and, when staged
/// crops or rotations were recorded, replaces Files. It used to follow the
/// redirect to the whole Case page and throw the rest away. A commit posted
/// by that script is now answered with those parts alone; every other request
/// keeps its redirect.
/// </summary>
public sealed partial class DetailsModel
{
    private const string CommitResultView = "/Pages/Cases/Shared/_CaseCommitResult.cshtml";

    /// <summary>
    /// Answers a commit that ran, landed or refused, with the parts the script
    /// swaps. A commit that landed but ended the edit session (its lease could
    /// not be claimed again) leaves nothing to update in place, and the script
    /// draws the whole record afresh, so that commit takes the redirect and
    /// hands it the notices and the commit it confirms.
    /// </summary>
    private async Task<IActionResult> AnswerCommitAsync(
        Guid id,
        string? section,
        bool carriesPreparation,
        IActionResult redirect,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        // The section the operator was looking at, as the redirect would have carried it.
        SectionFilter = section;
        try
        {
            if (!await LoadCommitResultAsync(id, actor, carriesPreparation, cancellationToken))
            {
                return NotFound();
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCaseDetailsQueryFailed(logger, id, exception);
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        if (heldEditorCommit is not null && !IsEditing)
        {
            TempData["CaseEditorCommit"] = heldEditorCommit;
            QueueHeldNotices();
            return redirect;
        }

        return Partial(CommitResultView, this);
    }

    /// <summary>
    /// Loads only what <c>_CaseCommitResult</c> draws: the frame, with the
    /// access answer its workflow gives, and the workspace; the lease; the
    /// viewed work's report readiness and current generation, which the aside's
    /// Next action reads; the assigned Engineer, the EVA handoff and the lease holder,
    /// which the ribbon and the dialogs read; the AI drafts; the calculation
    /// the valuation calculator now opens on; and, when the commit recorded
    /// staged crops or rotations, Files.
    /// Nothing a section draws is read. The phases keep the page's names.
    /// </summary>
    private async Task<bool> LoadCommitResultAsync(
        Guid id,
        ActionActor actor,
        bool carriesPreparation,
        CancellationToken cancellationToken)
    {
        using var activity = DocumentReadTelemetry.Start("web.case.commit");
        var work = WorkSelector;
        (Case, var workspace) = await ReadFrameAndWorkspaceAsync(id, actor, work, cancellationToken);
        if (Case is null)
        {
            return false;
        }

        ApplyAssessmentAccess(actor, Case.Workflow);
        await RestoreLeaseStateAsync(id, actor, Case.ActiveEditLease, resumeLease, cancellationToken);
        if (LeaseToken is not null)
        {
            RenewLeaseOperationKey = GetOrCreateOperationKey(RenewLeaseOperationKeyName);
        }
        Assessment = workspace?.Assessment;
        CurrentSpecification = workspace?.CurrentSpecification;

        var activeLease = Case.ActiveEditLease;
        var viewerHoldsLease = activeLease is not null
            && CaseEditAuthority.IsHolder(activeLease.HolderKind, activeLease.Holder, actor);
        var recordsPreparation = carriesPreparation && heldEditorCommit is not null;
        var canEditCaseData = CanEditCaseData;
        var sectionQuery = new GetCaseSectionQuery(
            id,
            actor,
            workspace,
            HasAssessmentWorkspace: true,
            Data: Case.Data,
            Documents: Case.Documents,
            Frame: Case.Frame,
            Work: work);
        var reuse = new ReportProjectionReuse(work, workspace, Frame: Case.Frame);

        using var reads = new BoundedReads(cancellationToken);
        using (DocumentReadTelemetry.Start("web.case.engineer-sections"))
        {
            var readiness = AssessmentCanOpen
                ? reads.Start(token => reportSnapshotSource.GetAsync(id, actor, work, reuse, token))
                : null;
            var generation = reads.Start(token =>
                reportGenerations.GetCurrentAsync(actor, id, work, token));
            var caseAiJobs = reads.Start(token => aiJobs.ListForSubjectAsync(id, token));
            var configuration = reads.Start(token => workflowConfiguration.GetCurrentAsync(token));
            var holder = activeLease is { } lease && !viewerHoldsLease
                ? reads.Start(token => describeEditAuthorityHolder.ExecuteAsync(
                    lease.HolderKind,
                    lease.Holder,
                    actor,
                    token))
                : null;
            // The calculator's opening calculation is the one the script carries
            // forward, so it is read wherever the calculator is editable.
            var valuation = SectionIsEditable("valuation")
                ? reads.Start(token => ReadValuationSectionAsync(id, actor, work, token, openingOnly: true))
                : null;
            var files = recordsPreparation
                ? reads.Start(token => getCaseFilesSection.ExecuteAsync(sectionQuery, token))
                : null;
            var fileLists = recordsPreparation
                ? reads.Start(token => ReadFilesAsync(id, canEditCaseData, token))
                : null;
            var preparations = recordsPreparation
                ? reads.Start(token => caseAssetPreparationQueries.ListForCaseAsync(id, token))
                : null;
            await reads.WhenAllAsync();

            var inputs = readiness is null ? null : await readiness;
            if (inputs is not null)
            {
                var readinessResult = CaseReportReadiness.Evaluate(inputs.Readiness);
                ReportDraftPreparation = new(readinessResult.Reasons);
                EligibleSignOffEngineers = inputs.Readiness.EligibleSignOffEngineers;
                SelectedSignOffEngineerId = readinessResult.Signatory?.StaffId;
            }
            CurrentReportGeneration = await generation;
            AiDrafts = AiDraftPolicy.Drafts(await caseAiJobs, (await configuration).AiDraftTargetDays);
            if (activeLease is not null)
            {
                ViewerHoldsEditAuthority = viewerHoldsLease;
                EditAuthorityHolder = holder is null ? CaseEditAuthorityHolder.Unnamed : await holder;
            }
            if (valuation is not null)
            {
                ApplyValuationSection(await valuation);
            }
            if (files is not null && fileLists is not null && preparations is not null)
            {
                FilesSection = await files
                    ?? throw new InvalidOperationException("The Case files section is unavailable.");
                ApplyFiles(await fileLists);
                AssetPreparations = await preparations;
            }
        }

        // The extras start from the Engineers the readiness read listed, and the
        // delivery preparation from the generation, so each waits for its input.
        using (DocumentReadTelemetry.Start("web.case.extras"))
        {
            var extrasInputs = new WorkspaceExtrasInputs(
                Case,
                AssessmentCanOpen,
                EligibleSignOffEngineers,
                LeaseToken);
            var extras = reads.Start(token => ReadWorkspaceExtrasAsync(extrasInputs, token));
            var delivery = CurrentReportGeneration is not null
                ? reads.Start(token => deliveryPreparations.GetCurrentAsync(actor, id, work, token))
                : null;
            await reads.WhenAllAsync();

            var workspaceExtras = await extras;
            EngineerDisplayName = workspaceExtras.EngineerDisplayName;
            SignOffEngineerDisplayName = workspaceExtras.SignOffEngineerDisplayName;
            EvaHandoff = workspaceExtras.EvaHandoff;
            CurrentDeliveryPreparation = delivery is null ? null : await delivery;
            AvailableClosureOutcomes = DescribeClosureOutcomes(Case.Workflow, actor);
            CanAssignToMe = CaseLifecycleRules.CanAssignToSelf(Case.Workflow);
        }

        return true;
    }
}
