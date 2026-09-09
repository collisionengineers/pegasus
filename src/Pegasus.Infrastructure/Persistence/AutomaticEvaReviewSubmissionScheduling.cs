using Pegasus.Core.Reports;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>Called inside the same transaction that moves a Case into Review.</summary>
internal static class AutomaticEvaReviewSubmissionScheduling
{
    public static void AddForReviewTransition(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        long resultingWorkflowVersion,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(workflow);
        if (workflow.Case.Principal.ReportGenerationPolicy
            != nameof(PrincipalReportGenerationPolicy.EvaAutomaticApiOnReview))
        {
            return;
        }

        // Re-entry and request replay must not cause a second provider
        // submission for the same Case. This query and the unique Case index
        // live in the transition transaction, so policy changes never create
        // an intent for a case that did not actually enter Review.
        if (context.Set<AutomaticEvaReviewSubmissionEntity>()
            .Any(item => item.CaseId == workflow.CaseId))
        {
            return;
        }

        var id = Guid.CreateVersion7();
        context.Set<AutomaticEvaReviewSubmissionEntity>().Add(new()
        {
            Id = id,
            CaseId = workflow.CaseId,
            WorkflowVersion = resultingWorkflowVersion,
            OperationKey = id.ToString("N"),
            State = "Pending",
            CreatedAtUtc = nowUtc,
            DueAtUtc = nowUtc
        });
    }
}
