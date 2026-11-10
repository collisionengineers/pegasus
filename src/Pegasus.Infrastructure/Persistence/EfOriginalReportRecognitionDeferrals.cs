using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Original report recordings that waited for a Case editor, kept as the
/// receipt's own append-only intake history (R728, #1981). The n-th wait of a
/// receipt and Case pair and its settlement share n in their operation keys,
/// n being how many waits were settled before it, so recording a wait that is
/// already open, or settling one twice, writes nothing. A wait is open while
/// no settlement for its pair follows it.
/// </summary>
internal sealed class EfOriginalReportRecognitionDeferrals(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider) : IOriginalReportRecognitionDeferrals
{
    internal const string DeferredEventType = "original_report_recognition_deferred";
    internal const string SettledEventType = "original_report_recognition_settled";
    private const string SystemWorkerKind = nameof(ActorKind.SystemWorker);
    private const string DeferredKeyPrefix = "original-report-recognition-deferred:";
    private const string SettledKeyPrefix = "original-report-recognition-settled:";

    public async Task RecordAsync(
        Guid caseId,
        Guid receiptId,
        long intakeVersion,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var settled = await CountAsync(context, receiptId, caseId, SettledEventType, cancellationToken);
        await AppendAsync(
            context,
            receiptId,
            caseId,
            DeferredEventType,
            $"{DeferredKeyPrefix}{receiptId:N}:{caseId:N}:{settled}",
            "The Case was being edited; recording the recognised original report waits for the editor.",
            intakeVersion,
            cancellationToken);
    }

    public async Task<IReadOnlyList<OriginalReportRecognitionDeferral>> ListAsync(
        int maximumItems,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var history = context.IntakeMutationHistory.AsNoTracking();
        // A wait is open while no settlement for its pair was recorded at or
        // after it: a pair has at most one open wait, and its settlement
        // follows it. The waits are found through the unique operation key
        // index, their settlements through the receipt's history index.
        var waiting = await history
            .Where(row => row.OperationKey.StartsWith(DeferredKeyPrefix)
                && row.EventType == DeferredEventType
                && row.CaseId != null
                && !history.Any(settled => settled.IntakeReceiptId == row.IntakeReceiptId
                    && settled.CaseId == row.CaseId
                    && settled.EventType == SettledEventType
                    && settled.OccurredAtUtc >= row.OccurredAtUtc))
            .OrderBy(row => row.OccurredAtUtc)
            .ThenBy(row => row.IntakeReceiptId)
            .Take(maximumItems)
            .Select(row => new { row.IntakeReceiptId, CaseId = row.CaseId!.Value })
            .ToArrayAsync(cancellationToken);
        return [.. waiting.Select(row => new OriginalReportRecognitionDeferral(row.CaseId, row.IntakeReceiptId))];
    }

    public async Task SettleAsync(
        OriginalReportRecognitionDeferral deferral,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(deferral);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var settled = await CountAsync(
            context, deferral.ReceiptId, deferral.CaseId, SettledEventType, cancellationToken);
        var deferred = await CountAsync(
            context, deferral.ReceiptId, deferral.CaseId, DeferredEventType, cancellationToken);
        if (deferred <= settled)
        {
            return;
        }

        var intakeVersion = await context.IntakeReceipts
            .Where(receipt => receipt.Id == deferral.ReceiptId)
            .Select(receipt => (long?)receipt.Version)
            .SingleOrDefaultAsync(cancellationToken) ?? 0;
        await AppendAsync(
            context,
            deferral.ReceiptId,
            deferral.CaseId,
            SettledEventType,
            $"{SettledKeyPrefix}{deferral.ReceiptId:N}:{deferral.CaseId:N}:{settled}",
            "A later attempt decided the recognised original report.",
            intakeVersion,
            cancellationToken);
    }

    private static Task<int> CountAsync(
        PegasusDbContext context,
        Guid receiptId,
        Guid caseId,
        string eventType,
        CancellationToken cancellationToken) =>
        context.IntakeMutationHistory.CountAsync(
            row => row.IntakeReceiptId == receiptId && row.CaseId == caseId && row.EventType == eventType,
            cancellationToken);

    private async Task AppendAsync(
        PegasusDbContext context,
        Guid receiptId,
        Guid caseId,
        string eventType,
        string operationKey,
        string reason,
        long intakeVersion,
        CancellationToken cancellationToken)
    {
        context.IntakeMutationHistory.Add(new IntakeMutationHistoryEntity
        {
            Id = Guid.NewGuid(),
            IntakeReceiptId = receiptId,
            CaseId = caseId,
            EventType = eventType,
            ActorKind = SystemWorkerKind,
            ActorSubjectId = "intake-processing",
            ActorRolesJson = "[]",
            Reason = reason,
            OperationKey = operationKey,
            RequestFingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(operationKey))),
            OccurredAtUtc = timeProvider.GetUtcNow(),
            ExpectedIntakeVersion = intakeVersion,
            BeforeIntakeVersion = intakeVersion,
            AfterIntakeVersion = intakeVersion
        });
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.GetBaseException() is SqlException { Number: 2601 or 2627 })
        {
            // Already recorded under this operation key.
        }
    }
}
