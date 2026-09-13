using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Lifecycle;

/// <summary>
/// "Assign to me" (Work Centre P8): an eligible Engineer takes an unassigned Case
/// for themself, from the Work Centre's Today pane or the Case's assignment
/// dialog. It is the ordinary assignment with the actor as the Engineer, so it
/// carries the same lease, version and operation key and lands in the same
/// history; only the reason is fixed, because the action is its own record.
/// </summary>
public sealed record AssignCaseToMeRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string EditLeaseToken);

public interface IAssignCaseToMe
{
    Task<CaseWorkflowRecord> ExecuteAsync(AssignCaseToMeRequest request, CancellationToken cancellationToken);
}

public sealed class AssignCaseToMe(
    ICaseWorkflowQueries queries,
    IAssignCaseEngineer assignEngineer) : IAssignCaseToMe
{
    public const string Reason = "Assigned to me.";

    private readonly ICaseWorkflowQueries _queries =
        queries ?? throw new ArgumentNullException(nameof(queries));
    private readonly IAssignCaseEngineer _assignEngineer =
        assignEngineer ?? throw new ArgumentNullException(nameof(assignEngineer));

    public async Task<CaseWorkflowRecord> ExecuteAsync(
        AssignCaseToMeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var engineerId = CaseLifecycleRules.RequireSelfAssigningEngineer(request.Actor);
        var current = await CaseLifecycleRules.GetRequiredAsync(_queries, request.CaseId, cancellationToken);
        if (!await _queries.HasOperationAsync(request.CaseId, request.OperationKey, cancellationToken))
        {
            CaseLifecycleRules.RequireSelfAssignmentAllowed(current);
        }

        return await _assignEngineer.ExecuteAsync(
            new AssignCaseEngineerRequest(
                request.CaseId,
                request.ExpectedVersion,
                request.Actor,
                request.OperationKey,
                Reason,
                request.EditLeaseToken,
                engineerId),
            cancellationToken);
    }
}
