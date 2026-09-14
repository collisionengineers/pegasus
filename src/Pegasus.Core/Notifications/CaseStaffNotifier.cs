using Pegasus.Core.AiWork;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Notifications;

/// <summary>
/// The Case-side entry to the notification policy: a use case that knows only the
/// Case and what happened calls here, and this reads the Case's engineer and
/// reference before asking the policy who is told. A notification is a side
/// record of an act that already committed, so a caller never fails its act on
/// one, and a replayed act raises none.
/// </summary>
public interface ICaseStaffNotifier
{
    Task<StaffNotification?> NotifyAsync(
        StaffNotificationCause cause,
        Guid caseId,
        ActionActor? actor,
        string? section,
        string? registration,
        CancellationToken cancellationToken);

    /// <summary>
    /// An e-mail linked to a Case: a query when the link put the Case into Query,
    /// otherwise an ordinary arrival. Nothing is raised for a Case with no engineer.
    /// </summary>
    Task<StaffNotification?> NotifyMailArrivalAsync(
        Guid caseId,
        ActionActor? actor,
        CancellationToken cancellationToken);

    /// <summary>An AI draft ready on the job's subject; Market research and the queue pass raise nothing.</summary>
    Task<StaffNotification?> NotifyAiDraftReadyAsync(AiJobRecord job, CancellationToken cancellationToken);
}

public sealed class CaseStaffNotifier(
    IRaiseStaffNotification raise,
    ICaseWorkflowQueries workflows) : ICaseStaffNotifier
{
    private readonly IRaiseStaffNotification _raise = raise ?? throw new ArgumentNullException(nameof(raise));
    private readonly ICaseWorkflowQueries _workflows = workflows ?? throw new ArgumentNullException(nameof(workflows));

    public async Task<StaffNotification?> NotifyAsync(
        StaffNotificationCause cause,
        Guid caseId,
        ActionActor? actor,
        string? section,
        string? registration,
        CancellationToken cancellationToken)
    {
        var workflow = await _workflows.GetAsync(caseId, cancellationToken);
        if (workflow is null)
        {
            return null;
        }

        return await _raise.ExecuteAsync(
            new StaffNotificationEvent(
                cause,
                caseId,
                workflow.Identity.Reference,
                registration,
                StaffNotificationPolicy.CaseRoute(caseId, section),
                workflow.AssignedEngineerId,
                actor),
            cancellationToken);
    }

    public async Task<StaffNotification?> NotifyMailArrivalAsync(
        Guid caseId,
        ActionActor? actor,
        CancellationToken cancellationToken)
    {
        var workflow = await _workflows.GetAsync(caseId, cancellationToken);
        if (workflow?.AssignedEngineerId is null)
        {
            return null;
        }

        var isQuery = workflow.State == CaseLifecycleState.Query;
        return await _raise.ExecuteAsync(
            new StaffNotificationEvent(
                isQuery ? StaffNotificationCause.QueryReceived : StaffNotificationCause.EmailReceived,
                caseId,
                workflow.Identity.Reference,
                null,
                StaffNotificationPolicy.CaseRoute(caseId, isQuery ? "correspondence" : "files"),
                workflow.AssignedEngineerId,
                actor),
            cancellationToken);
    }

    public async Task<StaffNotification?> NotifyAiDraftReadyAsync(AiJobRecord job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        if (job.State != AiJobState.DraftReady
            || StaffNotificationPolicy.AiDraftRoute(job) is not { } route)
        {
            return null;
        }

        Guid? caseId = null;
        Guid? engineerId = null;
        var reference = job.SubjectReference;
        if (job.SubjectKind == AiJobSubjectKind.Case && job.SubjectId is { } subjectCaseId)
        {
            var workflow = await _workflows.GetAsync(subjectCaseId, cancellationToken);
            caseId = subjectCaseId;
            engineerId = workflow?.AssignedEngineerId;
            reference = workflow?.Identity.Reference ?? reference;
        }

        var starter = job.CreatedByKind == ActorKind.Staff && Guid.TryParse(job.CreatedBy, out var starterId)
            ? starterId
            : (Guid?)null;
        return await _raise.ExecuteAsync(
            new StaffNotificationEvent(
                StaffNotificationCause.AiDraftReady,
                caseId,
                reference,
                null,
                route,
                engineerId,
                Actor: null,
                JobStarterStaffId: starter),
            cancellationToken);
    }
}
