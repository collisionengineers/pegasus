using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
using Pegasus.Core.Reports;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The report sends <see cref="ReportSendPolicy"/> counts (R673): one row for
/// each report an observed Pegasus report e-mail carried, its generation
/// named by <c>ContextId</c> and, when the e-mail carried both works'
/// reports, by <c>SecondContextId</c> too. The Work Centre and every
/// Management Report read report sends here and nowhere else.
/// </summary>
internal static class ReportSends
{
    /// <summary>Every observed send of every report, as a query a reader composes.</summary>
    public static IQueryable<ReportSendRow> All(PegasusDbContext db) =>
        from operation in db.Set<StaffMailSendOperationEntity>().AsNoTracking()
        from generation in db.Set<CaseReportGenerationEntity>().AsNoTracking()
        where (generation.Id == operation.ContextId || generation.Id == operation.SecondContextId)
            && operation.Purpose == StaffMailPurpose.CaseReport
            && operation.State == StaffMailState.Sent
            && operation.ObservedSentAtUtc != null
        select new ReportSendRow
        {
            SendId = operation.Id,
            CaseId = generation.CaseId,
            WorkId = generation.WorkId,
            GenerationId = generation.Id,
            SentAtUtc = operation.ObservedSentAtUtc!.Value,
            ActorKind = operation.ActorKind,
            ActorSubjectId = operation.ActorSubjectId
        };

    /// <summary>
    /// Every send in the half-open period <c>[from, to)</c>, each marked first
    /// or not over its report's every send before it, whoever made them, with
    /// the Case and work facts the Management Reports split and time by. One
    /// statement reads the sends of the reports sent in the period, up to its
    /// end.
    /// </summary>
    public static async Task<IReadOnlyList<ReportSendFact>> InPeriodAsync(
        PegasusDbContext db,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        var sends = All(db);
        var reportsSentInPeriod = sends
            .Where(send => send.SentAtUtc >= fromUtc && send.SentAtUtc < toUtc)
            .Select(send => send.WorkId);
        var rows = await (
            from send in sends
            join work in db.CaseWorks.AsNoTracking() on send.WorkId equals work.Id
            join @case in db.Cases.AsNoTracking() on send.CaseId equals @case.Id
            where send.SentAtUtc < toUtc && reportsSentInPeriod.Contains(send.WorkId)
            select new ReportSendFact
            {
                SendId = send.SendId,
                CaseId = send.CaseId,
                WorkId = send.WorkId,
                GenerationId = send.GenerationId,
                SentAtUtc = send.SentAtUtc,
                ActorKind = send.ActorKind,
                ActorSubjectId = send.ActorSubjectId,
                PrincipalId = @case.PrincipalId,
                PrincipalCode = @case.Principal.Code,
                CaseType = @case.Type,
                WorkKind = work.Kind,
                WorkCreatedAtUtc = work.CreatedAtUtc,
                ReceivedAtUtc = db.IntakeReceipts
                    .Where(receipt => receipt.Id == @case.OriginIntakeReceiptId)
                    .Select(receipt => (DateTimeOffset?)receipt.ReceivedAtUtc)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        foreach (var (send, isFirst) in ReportSendPolicy.Classify(
            rows, row => row.WorkId, row => row.SentAtUtc, row => row.SendId))
        {
            send.IsFirst = isFirst;
        }

        return [.. rows.Where(row => row.SentAtUtc >= fromUtc)];
    }
}

/// <summary>One report one observed e-mail carried.</summary>
internal sealed class ReportSendRow
{
    public Guid SendId { get; init; }

    public Guid CaseId { get; init; }

    public Guid WorkId { get; init; }

    public Guid GenerationId { get; init; }

    public DateTimeOffset SentAtUtc { get; init; }

    public ActorKind ActorKind { get; init; }

    public string ActorSubjectId { get; init; } = string.Empty;
}

/// <summary>A send in a period with its report's Case and work facts, and whether it is the report's first send.</summary>
internal sealed class ReportSendFact
{
    public Guid SendId { get; init; }

    public Guid CaseId { get; init; }

    public Guid WorkId { get; init; }

    public Guid GenerationId { get; init; }

    public DateTimeOffset SentAtUtc { get; init; }

    public ActorKind ActorKind { get; init; }

    public string ActorSubjectId { get; init; } = string.Empty;

    public Guid PrincipalId { get; init; }

    public string PrincipalCode { get; init; } = string.Empty;

    public string CaseType { get; init; } = string.Empty;

    public string WorkKind { get; init; } = string.Empty;

    public DateTimeOffset WorkCreatedAtUtc { get; init; }

    public DateTimeOffset? ReceivedAtUtc { get; init; }

    /// <summary>Whether this is the report's first send; every other send of it is a re-send.</summary>
    public bool IsFirst { get; set; }

    /// <summary>MI-02's split: an Audit report (<see cref="CaseWorkKinds.IsAuditReport"/>) or an Inspection report.</summary>
    public bool IsAudit => CaseWorkKinds.IsAuditReport(CaseType, WorkKind);

    /// <summary>When the turnaround to this send starts (<see cref="ReportSendPolicy.TurnaroundStart"/>).</summary>
    public DateTimeOffset? TurnaroundStartUtc =>
        ReportSendPolicy.TurnaroundStart(CaseWorkKinds.Parse(WorkKind), ReceivedAtUtc, WorkCreatedAtUtc);
}
