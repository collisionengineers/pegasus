using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
using Pegasus.Core.Reports;
using Pegasus.Core.Tasks;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The Pegasus report send a recorded Sent item belongs to: the operation, who
/// sent it and what the send recorded (<see cref="ReportDispatchRecord"/>).
/// </summary>
internal sealed record ReportSendOfEvidence(Guid OperationId, string ActorSubjectId, string? ReportDispatchJson);

/// <summary>
/// The tasks Report sent creates (FRD-13, CASE-20). When a Pegasus report send is linked to its
/// Case, one open task is added per description in the after-send list that send recorded
/// (<c>StaffMailSendOperations.ReportDispatchJson</c>), inside the transaction that records the
/// link, in the name of the staff member who sent the report. They are system work: the Case's
/// version moves on and an editor keeps their session.
/// </summary>
internal static class ReportSentAfterSendTasks
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// The report send the Worker has just matched to its Sent item, by its operation. Null when
    /// the operation is not a report send of this Case.
    /// </summary>
    public static async Task<ReportSendOfEvidence?> SendAsync(
        PegasusDbContext context,
        Guid caseId,
        Guid operationId,
        CancellationToken cancellationToken) =>
        await (
                from operation in context.Set<StaffMailSendOperationEntity>().AsNoTracking()
                join generation in context.Set<CaseReportGenerationEntity>().AsNoTracking()
                    on operation.ContextId equals generation.Id
                where operation.Id == operationId
                    && operation.Purpose == StaffMailPurpose.CaseReport
                    && generation.CaseId == caseId
                select new ReportSendOfEvidence(
                    operation.Id, operation.ActorSubjectId, operation.ReportDispatchJson))
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// The report send a retained Sent item proves: the Sent <c>CaseReport</c> staff-mail
    /// operation that observed this Sent item, when its generation belongs to the Case. Null
    /// when the Sent item is not Pegasus's own report send.
    /// </summary>
    public static async Task<ReportSendOfEvidence?> SendOfSentEvidenceAsync(
        PegasusDbContext context,
        Guid caseId,
        Guid evidenceId,
        CancellationToken cancellationToken)
    {
        var immutableItemIdentity = await context.CaseReportSentEvidence
            .AsNoTracking()
            .Where(item => item.Id == evidenceId)
            .Select(item => item.ImmutableItemIdentity)
            .SingleOrDefaultAsync(cancellationToken);
        if (immutableItemIdentity is null)
        {
            return null;
        }

        return await (
                from operation in context.Set<StaffMailSendOperationEntity>().AsNoTracking()
                join generation in context.Set<CaseReportGenerationEntity>().AsNoTracking()
                    on operation.ContextId equals generation.Id
                where operation.Purpose == StaffMailPurpose.CaseReport
                    && operation.State == StaffMailState.Sent
                    && operation.ObservedSentImmutableMessageId == immutableItemIdentity
                    && generation.CaseId == caseId
                select new ReportSendOfEvidence(
                    operation.Id, operation.ActorSubjectId, operation.ReportDispatchJson))
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Adds the send's after-send tasks that the Case does not already have and returns the ones
    /// it added. A task's id is derived from the evidence and its position in the list and its
    /// operation key from the link's, so a repeat adds nothing. Each task moves the Case's
    /// version on, after the link's own event, and the caller commits them with the link.
    /// </summary>
    /// <param name="linkActor">Who recorded the link; the tasks name the sender instead when the send records one.</param>
    public static async Task<IReadOnlyList<CaseTaskRecord>> AddAsync(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        ReportSendOfEvidence? send,
        Guid evidenceId,
        string linkOperationKey,
        ActionActor linkActor,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        if (send is null || string.IsNullOrWhiteSpace(send.ReportDispatchJson))
        {
            return [];
        }

        var descriptions = JsonSerializer
            .Deserialize<ReportDispatchRecord>(send.ReportDispatchJson, SerializerOptions)?.AfterSendTasks ?? [];
        if (descriptions.Count == 0)
        {
            return [];
        }

        var actor = await SenderAsync(context, send, linkActor, cancellationToken);
        var created = new List<CaseTaskRecord>();
        for (var index = 0; index < descriptions.Count; index++)
        {
            var description = descriptions[index]?.Trim();
            if (string.IsNullOrEmpty(description))
            {
                continue;
            }

            var taskId = TaskId(evidenceId, index);
            var operationKey = $"{linkOperationKey.Trim()}:task:{index}";
            if (await context.CaseTasks.AnyAsync(item => item.Id == taskId, cancellationToken)
                || await context.CaseWorkflowEvents.AnyAsync(
                    item => item.CaseId == workflow.CaseId && item.OperationKey == operationKey,
                    cancellationToken))
            {
                continue;
            }

            created.Add(EfCaseTaskStore.AddConsequenceTask(
                context,
                workflow,
                actor,
                taskId,
                description,
                operationKey,
                CaseTaskReasons.ReportSent,
                occurredAtUtc));
        }

        return created;
    }

    /// <summary>
    /// The staff member who sent the report, with the role their prepared send recorded (the
    /// Worker does not read the staff role tables). When the send does not record exactly one
    /// staff role, the tasks carry whoever recorded the link.
    /// </summary>
    private static async Task<ActionActor> SenderAsync(
        PegasusDbContext context,
        ReportSendOfEvidence send,
        ActionActor linkActor,
        CancellationToken cancellationToken)
    {
        var roles = await EfStaffMailSendStore.PreparedRolesAsync(context, send.OperationId, cancellationToken);
        return roles.Count == 1 && Guid.TryParse(send.ActorSubjectId, out var staffId) && staffId != Guid.Empty
            ? ActionActor.Staff(staffId, roles)
            : linkActor;
    }

    /// <summary>A stable id for the task at <paramref name="index"/> of the evidence's list.</summary>
    internal static Guid TaskId(Guid evidenceId, int index)
    {
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(
            Encoding.UTF8.GetBytes($"report-sent-task:{evidenceId:D}:{index}"),
            hash);
        return new Guid(hash[..16]);
    }
}
