using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
using Pegasus.Core.Tasks;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The tasks Report sent creates (FRD-13, CASE-20). When a Pegasus report send is linked to its
/// Case, one open task is added per description in the after-send list the delivery froze
/// (<c>CaseReportDeliveryIntents.AfterSendTasksJson</c>), inside the transaction that records the
/// link. The edit lease is bypassed on purpose: the tasks are a consequence of a recorded Sent
/// item, not a staff edit, and the Worker's automatic link holds no lease at all.
/// </summary>
internal static class ReportSentAfterSendTasks
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// The report generation a retained Sent item proves: the generation of the Sent
    /// <c>CaseReport</c> staff-mail operation that observed this Sent item, when that generation
    /// belongs to the Case. Null when the Sent item is not Pegasus's own report send.
    /// </summary>
    public static async Task<Guid?> GenerationOfSentEvidenceAsync(
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
                select (Guid?)generation.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Adds the generation's after-send tasks that the Case does not already have and returns the
    /// ones it added. A task's id is derived from the evidence and its position in the list and
    /// its operation key from the link's, so a repeat adds nothing. Each task moves the Case's
    /// version on, after the link's own event, and the caller commits them with the link.
    /// </summary>
    public static async Task<IReadOnlyList<CaseTaskRecord>> AddAsync(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        Guid? generationId,
        Guid evidenceId,
        string linkOperationKey,
        ActionActor actor,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        if (generationId is not { } generation)
        {
            return [];
        }

        // The generation's latest delivery intent holds the list frozen when the delivery was
        // prepared. EF.Property reads the column without the entity declaring it here.
        var frozen = await (
                from intent in context.Set<CaseReportDeliveryIntentEntity>().AsNoTracking()
                join candidate in context.Set<CaseReportGenerationEntity>().AsNoTracking()
                    on intent.GenerationId equals candidate.Id
                where candidate.Id == generation && candidate.CaseId == workflow.CaseId
                orderby intent.PreparedAtUtc descending
                select EF.Property<string?>(intent, "AfterSendTasksJson"))
            .FirstOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(frozen))
        {
            return [];
        }

        var descriptions = JsonSerializer.Deserialize<string[]>(frozen, SerializerOptions) ?? [];
        var created = new List<CaseTaskRecord>();
        for (var index = 0; index < descriptions.Length; index++)
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
