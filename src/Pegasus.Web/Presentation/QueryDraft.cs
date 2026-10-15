using Pegasus.Core.AiWork;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Presentation;

/// <summary>
/// The live Query response job for one retained query as a page draws it
/// (FRD-27): the job and the state a reader sees now, so a lapsed lease reads
/// Queued. Draft ready offers **Reply with AI draft**; Queued and Taken read
/// as pending; none offers **Draft reply with AI**.
/// </summary>
public sealed record QueryDraft(AiJobRecord Job, AiJobState State)
{
    public bool IsReady => State == AiJobState.DraftReady;

    public static QueryDraft? For(IEnumerable<AiJobRecord> jobs, Guid messageId, DateTimeOffset now) =>
        AiJobPolicy.QueryResponseJobFor(jobs, messageId, now) is { } job
            ? new(job, AiJobPolicy.EffectiveState(job, now))
            : null;
}

/// <summary>
/// Why **Draft reply with AI** is withheld for a Case, or null when it is
/// offered: one answer for the message record and the Case's Queries row.
/// The Send to AI switch is read only once the Case qualifies.
/// </summary>
public static class QueryResponseAvailability
{
    public static async Task<string?> ConditionAsync(
        CaseWorkflowRecord? workflow,
        ISendToAiControl sendToAi,
        CancellationToken cancellationToken)
    {
        if (workflow is null)
        {
            return OperatorLabels.QueryResponseJobs.CaseUnavailable;
        }
        if (!AiJobPolicy.IsEligibleQueryResponse(workflow))
        {
            return OperatorLabels.QueryResponseJobs.AvailableInPostReportWork;
        }
        return await sendToAi.IsEnabledAsync(cancellationToken)
            ? null
            : OperatorLabels.QueryResponseJobs.AutomationStopped;
    }
}
