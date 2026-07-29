using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Custody;

namespace Pegasus.Infrastructure.Persistence;

internal sealed class EfQueuedCustodyProcessor(
    IDbContextFactory<PegasusDbContext> dbContextFactory,
    ICaseCustody caseCustody,
    TimeProvider timeProvider) : IProcessQueuedCustody
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);

    public async Task ExecuteAsync(Guid workId, CancellationToken cancellationToken)
    {
        if (workId == Guid.Empty)
        {
            throw new ArgumentException("A custody work identifier is required.", nameof(workId));
        }

        var leaseToken = Guid.NewGuid().ToString("N");
        WorkPayload payload;
        await using (var context = await dbContextFactory.CreateDbContextAsync(cancellationToken))
        await using (var transaction = await context.Database.BeginTransactionAsync(cancellationToken))
        {
            var work = await context.Set<ExternalWorkItemEntity>()
                .SingleOrDefaultAsync(value => value.Id == workId, cancellationToken)
                ?? throw new InvalidOperationException("The custody work item is unavailable.");
            if (!string.Equals(work.Kind, "create_case_custody", StringComparison.Ordinal))
            {
                throw new InvalidDataException("The external work item is not a supported custody operation.");
            }

            if (string.Equals(work.State, "completed", StringComparison.Ordinal))
            {
                return;
            }

            var now = timeProvider.GetUtcNow();
            if (string.Equals(work.State, "processing", StringComparison.Ordinal)
                && work.LeaseExpiresAtUtc > now)
            {
                throw new InvalidOperationException("The custody work item is already leased.");
            }

            var caseEntity = await context.Set<CaseEntity>()
                .AsNoTracking()
                .SingleAsync(value => value.Id == work.CaseId, cancellationToken);
            var source = await context.Set<IntakeStagedReceiptEntity>()
                .AsNoTracking()
                .SingleAsync(value => value.Id == caseEntity.OriginIntakeReceiptId, cancellationToken);

            work.State = "processing";
            work.AttemptCount = checked(work.AttemptCount + 1);
            work.LeaseToken = leaseToken;
            work.LeaseExpiresAtUtc = now.Add(LeaseDuration);
            work.FailureCode = null;
            work.FailureReason = null;
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            payload = new(
                work.CaseId,
                caseEntity.Reference,
                caseEntity.AuditReference,
                caseEntity.OriginIntakeReceiptId,
                source.SourceFileName,
                source.MediaType,
                source.SourceHash,
                source.StorageKey,
                work.OperationKey);
        }

        try
        {
            var root = await caseCustody.CreateCaseRootAsync(
                payload.CaseId,
                payload.CaseReference,
                $"{payload.OperationKey}:root",
                cancellationToken);
            var version = await caseCustody.RetainAcceptedIntakeSourceAsync(
                root,
                new(
                    payload.IntakeReceiptId,
                    payload.SourceFileName,
                    payload.MediaType,
                    payload.SourceHash,
                    payload.StagedObjectKey),
                $"{payload.OperationKey}:source",
                cancellationToken);
            if (!string.IsNullOrWhiteSpace(payload.AuditReference))
            {
                _ = await caseCustody.CreateAuditReferenceFolderAsync(
                    root,
                    payload.AuditReference,
                    $"{payload.OperationKey}:audit",
                    cancellationToken);
            }

            await CompleteAsync(workId, leaseToken, root, version, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await ReleaseLeaseAsync(workId, leaseToken, "cancelled", CancellationToken.None);
            throw;
        }
        catch (Exception exception)
        {
            await ReleaseLeaseAsync(workId, leaseToken, GetFailureCode(exception), CancellationToken.None);
            throw;
        }
    }

    private async Task CompleteAsync(
        Guid workId,
        string leaseToken,
        CaseCustodyRoot root,
        CustodyDocumentVersion version,
        CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var work = await context.Set<ExternalWorkItemEntity>()
            .SingleAsync(value => value.Id == workId && value.LeaseToken == leaseToken, cancellationToken);
        var caseEntity = await context.Set<CaseEntity>()
            .SingleAsync(value => value.Id == work.CaseId, cancellationToken);
        if (string.Equals(work.State, "completed", StringComparison.Ordinal))
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var beforeVersion = caseEntity.Version;
        caseEntity.CustodyRootRemoteId = root.RemoteId;
        caseEntity.CustodySourceRemoteId = version.RemoteId;
        caseEntity.CustodySourceContentHash = version.ContentHash;
        caseEntity.CustodySourceETag = version.ETag;
        caseEntity.CustodyConfirmedAtUtc = now;
        caseEntity.CustodyState = "confirmed";
        caseEntity.Version = checked(caseEntity.Version + 1);
        work.State = "completed";
        work.CompletedAtUtc = now;
        work.ExternalReceipt = version.RemoteId;
        work.LeaseToken = null;
        work.LeaseExpiresAtUtc = null;
        context.Set<CaseHistoryEntity>().Add(new()
        {
            Id = Guid.NewGuid(),
            CaseId = caseEntity.Id,
            EventType = "custody_confirmed",
            Actor = "system",
            Reason = "Accepted source custody confirmed.",
            OccurredAtUtc = now,
            OperationKey = $"{work.OperationKey}:confirmed",
            BeforeVersion = beforeVersion,
            AfterVersion = caseEntity.Version
        });
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task ReleaseLeaseAsync(
        Guid workId,
        string leaseToken,
        string failureCode,
        CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var work = await context.Set<ExternalWorkItemEntity>()
            .SingleOrDefaultAsync(
                value => value.Id == workId && value.LeaseToken == leaseToken,
                cancellationToken);
        if (work is null || string.Equals(work.State, "completed", StringComparison.Ordinal))
        {
            return;
        }

        work.State = "pending";
        work.FailureCode = failureCode;
        work.FailureReason = "Custody dependency did not confirm the operation.";
        work.DueAtUtc = timeProvider.GetUtcNow();
        work.LeaseToken = null;
        work.LeaseExpiresAtUtc = null;
        await context.SaveChangesAsync(cancellationToken);
    }

    private static string GetFailureCode(Exception exception) => exception switch
    {
        FileNotFoundException => "source_unavailable",
        InvalidDataException => "integrity_failure",
        UnauthorizedAccessException => "scope_denied",
        IOException => "custody_io_failure",
        _ => "custody_dependency_failure"
    };

    private sealed record WorkPayload(
        Guid CaseId,
        string CaseReference,
        string? AuditReference,
        Guid IntakeReceiptId,
        string SourceFileName,
        string MediaType,
        string SourceHash,
        string StagedObjectKey,
        string OperationKey);
}
