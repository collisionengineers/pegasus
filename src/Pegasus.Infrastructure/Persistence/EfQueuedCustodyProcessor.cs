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
        while (true)
        {
            await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var work = await context.ExternalWorkItems
                .AsNoTracking()
                .SingleOrDefaultAsync(value => value.Id == workId, cancellationToken)
                ?? throw new InvalidOperationException("The custody work item is unavailable.");
            if (!string.Equals(work.Kind, "create_case_custody", StringComparison.Ordinal))
            {
                throw new InvalidDataException("The external work item is not a supported custody operation.");
            }

            if (work.State is "completed" or "failed")
            {
                return;
            }

            var now = timeProvider.GetUtcNow();
            if (string.Equals(work.State, "processing", StringComparison.Ordinal)
                && work.LeaseExpiresAtUtc > now)
            {
                throw new InvalidOperationException("The custody work item is already leased.");
            }

            if (work.State is not ("pending" or "dispatching" or "queued" or "processing"))
            {
                throw new InvalidDataException(
                    $"The custody work item has unknown state '{work.State}'.");
            }

            var claimed = await context.ExternalWorkItems
                .Where(value => value.Id == work.Id
                    && value.State == work.State
                    && value.LeaseToken == work.LeaseToken
                    && value.LeaseExpiresAtUtc == work.LeaseExpiresAtUtc)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(value => value.State, "processing")
                    .SetProperty(value => value.AttemptCount, value => value.AttemptCount + 1)
                    .SetProperty(value => value.LeaseToken, leaseToken)
                    .SetProperty(value => value.LeaseExpiresAtUtc, now.Add(LeaseDuration))
                    .SetProperty(value => value.FailureCode, (string?)null)
                    .SetProperty(value => value.FailureReason, (string?)null),
                    cancellationToken);
            if (claimed == 0)
            {
                continue;
            }

            try
            {
                payload = await LoadPayloadAsync(
                    context,
                    work.CaseId,
                    work.OperationKey,
                    cancellationToken);
            }
            catch (Exception exception)
            {
                await ReleaseLeaseAsync(
                    workId,
                    leaseToken,
                    GetFailureCode(exception),
                    CancellationToken.None);
                throw;
            }

            break;
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
                    payload.SourceObjectKey),
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

    private static async Task<WorkPayload> LoadPayloadAsync(
        PegasusDbContext context,
        Guid caseId,
        string operationKey,
        CancellationToken cancellationToken)
    {
        var caseEntity = await context.Cases
            .AsNoTracking()
            .SingleAsync(value => value.Id == caseId, cancellationToken);
        var receipt = await context.IntakeReceipts
            .AsNoTracking()
            .SingleAsync(value => value.Id == caseEntity.OriginIntakeReceiptId, cancellationToken);

        var source = await context.IntakeWorkItems
            .AsNoTracking()
            .Where(value => value.ProcessedReceiptId == receipt.Id)
            .Select(value => new SourcePayload(
                value.StagedReceipt.SourceFileName,
                value.StagedReceipt.MediaType,
                value.StagedReceipt.SourceLength,
                value.StagedReceipt.SourceHash,
                value.StagedReceipt.StorageKey))
            .SingleOrDefaultAsync(cancellationToken);
        if (source is null)
        {
            source = await context.IntakeAssets
                .AsNoTracking()
                .Where(value => value.IntakeReceiptId == receipt.Id
                    && value.Kind == "source"
                    && value.Disposition == "source")
                .Select(value => new SourcePayload(
                    value.FileName,
                    value.MediaType,
                    value.ContentLength,
                    value.ContentHash,
                    value.StorageKey))
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new InvalidDataException(
                    "The processed intake receipt has no retained source lineage.");
        }

        EnsureSourceMatchesReceipt(receipt, source);
        return new(
            caseEntity.Id,
            caseEntity.Reference,
            caseEntity.AuditReference,
            receipt.Id,
            receipt.SourceFileName,
            receipt.MediaType,
            receipt.SourceHash,
            source.StorageKey,
            operationKey);
    }

    private static void EnsureSourceMatchesReceipt(
        IntakeReceiptEntity receipt,
        SourcePayload source)
    {
        if (source.ContentLength != receipt.SourceLength
            || !string.Equals(source.SourceHash, receipt.SourceHash, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(source.SourceFileName, receipt.SourceFileName, StringComparison.Ordinal)
            || !string.Equals(source.MediaType, receipt.MediaType, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(source.StorageKey))
        {
            throw new InvalidDataException(
                "The retained intake source lineage does not match the processed receipt.");
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
        var work = await context.ExternalWorkItems
            .SingleOrDefaultAsync(
                value => value.Id == workId && value.LeaseToken == leaseToken,
                cancellationToken);
        if (work is null)
        {
            var state = await context.ExternalWorkItems
                .AsNoTracking()
                .Where(value => value.Id == workId)
                .Select(value => value.State)
                .SingleOrDefaultAsync(cancellationToken);
            if (string.Equals(state, "completed", StringComparison.Ordinal))
            {
                return;
            }

            throw new InvalidOperationException(
                "The custody work item lease was lost before completion could be persisted.");
        }

        var caseEntity = await context.Cases
            .SingleAsync(value => value.Id == work.CaseId, cancellationToken);

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
        work.FailureCode = null;
        work.FailureReason = null;
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

        work.State = "queued";
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
        string SourceObjectKey,
        string OperationKey);

    private sealed record SourcePayload(
        string SourceFileName,
        string MediaType,
        long ContentLength,
        string SourceHash,
        string StorageKey);
}
