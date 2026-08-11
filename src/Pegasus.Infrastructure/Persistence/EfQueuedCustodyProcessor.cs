using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Custody;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Custody;

namespace Pegasus.Infrastructure.Persistence;

internal sealed class EfQueuedCustodyProcessor(
    IDbContextFactory<PegasusDbContext> dbContextFactory,
    IExternalWorkStore workStore,
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
            if (work.Kind is not ("create_case_custody" or "create_audit_reference_custody"))
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
                    work.Kind,
                    work.CaseId,
                    work.OperationKey,
                    work.CaseRootCreationToken,
                    work.AuditFolderCreationToken,
                    cancellationToken);
            }
            catch (Exception exception)
            {
                await workStore.FailProcessingAsync(
                    workId,
                    leaseToken,
                    timeProvider.GetUtcNow(),
                    GetFailureCode(exception),
                    GetFailureReason(exception),
                    CancellationToken.None);
                throw;
            }

            break;
        }

        try
        {
            var leaseGuard = new CustodyEffectLeaseGuard(
                token => workStore.HoldsProcessingLeaseAsync(workId, leaseToken, token));
            await leaseGuard.RequireCurrentAsync(cancellationToken);
            var isAuditCustody = string.Equals(
                payload.WorkKind,
                "create_audit_reference_custody",
                StringComparison.Ordinal);
            var root = isAuditCustody
                ? await caseCustody.GetExistingCaseRootAsync(
                    payload.CaseId,
                    payload.CaseReference,
                    cancellationToken)
                : await caseCustody.CreateCaseRootAsync(
                    payload.CaseId,
                    payload.CaseReference,
                    RequireCreationOwner(payload.CaseRootCreationToken),
                    $"{payload.OperationKey}:root",
                    leaseGuard,
                    cancellationToken);
            await leaseGuard.RequireCurrentAsync(cancellationToken);
            if (isAuditCustody)
            {
                if (string.IsNullOrWhiteSpace(payload.AuditReference))
                {
                    throw new InvalidDataException(
                        "The later Audit custody operation has no allocated Audit identity.");
                }
                var auditFolderRemoteId = await caseCustody.CreateAuditReferenceFolderAsync(
                    root,
                    payload.AuditReference,
                    RequireCreationOwner(payload.AuditFolderCreationToken),
                    $"{payload.OperationKey}:audit",
                    leaseGuard,
                    cancellationToken);
                await leaseGuard.RequireCurrentAsync(cancellationToken);
                await CompleteAuditCustodyAsync(
                    workId,
                    leaseToken,
                    root,
                    auditFolderRemoteId,
                    cancellationToken);
            }
            else
            {
                var version = await caseCustody.RetainAcceptedIntakeSourceAsync(
                    root,
                    new(
                        payload.IntakeReceiptId,
                        payload.SourceFileName,
                        payload.MediaType,
                        payload.SourceHash,
                        payload.SourceObjectKey,
                        payload.SourceLength),
                    $"{payload.OperationKey}:source",
                    leaseGuard,
                    cancellationToken);
                await leaseGuard.RequireCurrentAsync(cancellationToken);
                var auditFolderRemoteId = string.IsNullOrWhiteSpace(payload.AuditReference)
                    ? null
                    : await caseCustody.CreateAuditReferenceFolderAsync(
                        root,
                        payload.AuditReference,
                        RequireCreationOwner(payload.AuditFolderCreationToken),
                        $"{payload.OperationKey}:audit",
                        leaseGuard,
                        cancellationToken);
                await leaseGuard.RequireCurrentAsync(cancellationToken);
                await CompleteCaseCustodyAsync(
                    workId,
                    leaseToken,
                    root,
                    version,
                    auditFolderRemoteId,
                    cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await workStore.FailProcessingAsync(
                workId,
                leaseToken,
                timeProvider.GetUtcNow(),
                "custody_cancelled",
                "Case evidence storage was interrupted before completion.",
                CancellationToken.None);
            throw;
        }
        catch (Exception exception)
        {
            await workStore.FailProcessingAsync(
                workId,
                leaseToken,
                timeProvider.GetUtcNow(),
                GetFailureCode(exception),
                GetFailureReason(exception),
                CancellationToken.None);
            throw;
        }
    }

    private static async Task<WorkPayload> LoadPayloadAsync(
        PegasusDbContext context,
        string workKind,
        Guid caseId,
        string operationKey,
        string? caseRootCreationToken,
        string? auditFolderCreationToken,
        CancellationToken cancellationToken)
    {
        var caseEntity = await context.Cases
            .AsNoTracking()
            .SingleAsync(value => value.Id == caseId, cancellationToken);
        var receipt = await context.IntakeReceipts
            .AsNoTracking()
            .SingleAsync(value => value.Id == caseEntity.OriginIntakeReceiptId, cancellationToken);

        var source = await context.IntakeAssets
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
        EnsureSourceMatchesReceipt(receipt, source);

        var stagedSource = await context.IntakeWorkItems
            .AsNoTracking()
            .Where(value => value.ProcessedReceiptId == receipt.Id)
            .Select(value => new StagedSourcePayload(
                value.StagedReceipt.SourceFileName,
                value.StagedReceipt.MediaType,
                value.StagedReceipt.SourceLength,
                value.StagedReceipt.SourceHash,
                value.StagedReceipt.SourceChannel,
                value.StagedReceipt.ExternalReceiptToken))
            .SingleOrDefaultAsync(cancellationToken);
        if (stagedSource is not null)
        {
            EnsureStagedSourceMatchesReceipt(receipt, stagedSource);
        }
        return new(
            workKind,
            caseEntity.Id,
            caseEntity.Reference,
            caseEntity.AuditReference,
            receipt.Id,
            receipt.SourceFileName,
            receipt.MediaType,
            receipt.SourceHash,
            source.StorageKey,
            source.ContentLength,
            operationKey,
            caseRootCreationToken,
            auditFolderCreationToken);
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

    private static void EnsureStagedSourceMatchesReceipt(
        IntakeReceiptEntity receipt,
        StagedSourcePayload source)
    {
        if (source.ContentLength != receipt.SourceLength
            || !string.Equals(source.SourceHash, receipt.SourceHash, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(source.SourceFileName, receipt.SourceFileName, StringComparison.Ordinal)
            || !string.Equals(source.MediaType, receipt.MediaType, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(source.SourceChannel, receipt.SourceChannel, StringComparison.Ordinal)
            || !string.Equals(
                source.ExternalReceiptToken,
                receipt.ExternalReceiptToken,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The staged intake lineage does not match the processed receipt.");
        }
    }

    private async Task CompleteCaseCustodyAsync(
        Guid workId,
        string leaseToken,
        CaseCustodyRoot root,
        CustodyDocumentVersion version,
        string? auditFolderRemoteId,
        CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var work = await context.ExternalWorkItems
            .SingleOrDefaultAsync(
                value => value.Id == workId
                    && value.State == "processing"
                    && value.LeaseToken == leaseToken
                    && value.LeaseExpiresAtUtc > now,
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
        var workflow = await context.CaseWorkflows
            .SingleAsync(value => value.CaseId == work.CaseId, cancellationToken);
        ArchivedCaseGuard.RequireMutable(workflow);

        var beforeVersion = workflow.Version;
        caseEntity.CustodyRootRemoteId = root.RemoteId;
        caseEntity.CustodySourceRemoteId = version.RemoteId;
        caseEntity.CustodySourceContentHash = version.ContentHash;
        caseEntity.CustodySourceETag = version.ETag;
        caseEntity.CustodyConfirmedAtUtc = now;
        caseEntity.CustodyState = "confirmed";
        if (auditFolderRemoteId is not null)
        {
            caseEntity.AuditCustodyRemoteId = auditFolderRemoteId;
            caseEntity.AuditCustodyConfirmedAtUtc = now;
        }
        if (workflow.State == CaseLifecycleState.NotReady.ToString()
            && caseEntity.InstructionComplete
            && caseEntity.ImagesComplete
            && caseEntity.InstructionConfirmedByStaff
            && caseEntity.ImagesConfirmedByStaff)
        {
            workflow.State = CaseLifecycleState.Review.ToString();
        }
        CaseMutationGuard.Complete(workflow);
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
            AfterVersion = workflow.Version
        });
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task CompleteAuditCustodyAsync(
        Guid workId,
        string leaseToken,
        CaseCustodyRoot root,
        string auditFolderRemoteId,
        CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var work = await context.ExternalWorkItems
            .SingleOrDefaultAsync(
                value => value.Id == workId
                    && value.State == "processing"
                    && value.LeaseToken == leaseToken
                    && value.LeaseExpiresAtUtc > now,
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
                "The Audit custody work item lease was lost before completion could be persisted.");
        }
        if (!string.Equals(
                work.Kind,
                "create_audit_reference_custody",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The work item is not a later Audit custody operation.");
        }

        var caseEntity = await context.Cases
            .SingleAsync(value => value.Id == work.CaseId, cancellationToken);
        var workflow = await context.CaseWorkflows
            .SingleAsync(value => value.CaseId == work.CaseId, cancellationToken);
        ArchivedCaseGuard.RequireMutable(workflow);
        if (string.IsNullOrWhiteSpace(caseEntity.AuditReference))
        {
            throw new InvalidDataException(
                "The later Audit custody operation has no immutable Audit identity.");
        }

        var beforeVersion = workflow.Version;
        caseEntity.CustodyRootRemoteId = root.RemoteId;
        caseEntity.AuditCustodyRemoteId = auditFolderRemoteId;
        caseEntity.AuditCustodyConfirmedAtUtc = now;
        CaseMutationGuard.Complete(workflow);
        work.State = "completed";
        work.CompletedAtUtc = now;
        work.ExternalReceipt = auditFolderRemoteId;
        work.LeaseToken = null;
        work.LeaseExpiresAtUtc = null;
        work.FailureCode = null;
        work.FailureReason = null;
        context.CaseHistory.Add(new()
        {
            Id = Guid.NewGuid(),
            CaseId = caseEntity.Id,
            EventType = "audit_custody_confirmed",
            Actor = "system",
            Reason = "Later Audit reference custody confirmed.",
            OccurredAtUtc = now,
            OperationKey = $"{work.OperationKey}:confirmed",
            BeforeVersion = beforeVersion,
            AfterVersion = workflow.Version
        });
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static string GetFailureCode(Exception exception) => exception switch
    {
        FileNotFoundException => "source_unavailable",
        InvalidDataException => "source_integrity_conflict",
        UnauthorizedAccessException => "custody_scope_denied",
        CustodyProcessingLeaseLostException => "custody_lease_lost",
        OperationCanceledException => "custody_cancelled",
        HttpRequestException or IOException => "custody_dependency_failure",
        _ => "custody_unexpected_failure"
    };

    private static string GetFailureReason(Exception exception) => GetFailureCode(exception) switch
    {
        "source_unavailable" => "The original evidence is unavailable from retained storage.",
        "source_integrity_conflict" => "The retained evidence no longer matches the accepted source.",
        "custody_scope_denied" => "The approved Case storage location could not be verified.",
        "custody_lease_lost" => "Case evidence storage stopped because this processing attempt no longer owns the work.",
        "custody_dependency_failure" =>
            "Case evidence could not be stored because the storage service was unavailable.",
        "custody_cancelled" => "Case evidence storage was interrupted before completion.",
        _ => "Case evidence could not be stored."
    };

    private static string RequireCreationOwner(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException(
                "The custody operation has no predeclared remote creation owner.");
        }
        BoxCaseCustody.ValidateCreationOwnerToken(value);
        return value;
    }

    private sealed record WorkPayload(
        string WorkKind,
        Guid CaseId,
        string CaseReference,
        string? AuditReference,
        Guid IntakeReceiptId,
        string SourceFileName,
        string MediaType,
        string SourceHash,
        string SourceObjectKey,
        long SourceLength,
        string OperationKey,
        string? CaseRootCreationToken,
        string? AuditFolderCreationToken);

    private sealed record SourcePayload(
        string SourceFileName,
        string MediaType,
        long ContentLength,
        string SourceHash,
        string StorageKey);

    private sealed record StagedSourcePayload(
        string SourceFileName,
        string MediaType,
        long ContentLength,
        string SourceHash,
        string SourceChannel,
        string ExternalReceiptToken);

}
