using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Where the two sent-mail tidies record what they did so that staff see it
/// (operator, 11 November 2026; R959): on the Case's Notes tab, which reads
/// <c>CaseWorkflowEvents</c>, or on a Triage Case's, which reads
/// <c>TriageHistory</c>. Neither is a Case mutation, so neither carries a
/// fresh version, as a correspondence send the Sent poll observed does not.
/// </summary>
internal static class SentMailTidyHistory
{
    internal const string InstructionMoved = "instruction_moved_to_deleted_items";
    internal const string InstructionMoveFailed = "instruction_move_failed";
    internal const string CorrespondenceMoved = "correspondence_moved_to_deleted_items";
    internal const string CorrespondenceMoveFailed = "correspondence_move_failed";

    /// <summary>
    /// Adds the entry to the unit of work the caller saves with its send row.
    /// A Case with neither a workflow nor a Triage has no Notes tab to show it.
    /// </summary>
    internal static async Task AddAsync(
        PegasusDbContext db,
        Guid caseId,
        string eventType,
        string actor,
        string reason,
        string operationKey,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        var requestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(operationKey)));
        var workflow = await db.CaseWorkflows.SingleOrDefaultAsync(
            item => item.CaseId == caseId, cancellationToken);
        if (workflow is not null)
        {
            db.CaseWorkflowEvents.Add(new()
            {
                Id = Guid.NewGuid(),
                CaseId = workflow.CaseId,
                Workflow = workflow,
                EventType = eventType,
                OperationKey = operationKey,
                RequestHash = requestHash,
                ActorKind = nameof(ActorKind.SystemWorker),
                ActorSubjectId = actor,
                ActorRolesJson = "[]",
                Reason = reason,
                OccurredAtUtc = occurredAtUtc,
                BeforeVersion = workflow.Version,
                AfterVersion = workflow.Version
            });
            return;
        }

        var triage = await db.Triage.SingleOrDefaultAsync(
            item => item.CaseId == caseId, cancellationToken);
        if (triage is null)
        {
            return;
        }

        db.TriageHistory.Add(new()
        {
            Id = Guid.NewGuid(),
            TriageCaseId = triage.CaseId,
            Triage = triage,
            EventType = eventType,
            Actor = actor,
            ActorKind = nameof(ActorKind.SystemWorker),
            Reason = reason,
            OperationKey = operationKey,
            RequestHash = requestHash,
            OccurredAtUtc = occurredAtUtc,
            BeforeVersion = triage.Version,
            AfterVersion = triage.Version,
            AfterState = triage.State,
            AfterAssigneeId = triage.AssigneeId,
            AfterLinkedInstructionCaseId = triage.LinkedInstructionCaseId
        });
    }
}
