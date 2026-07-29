using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Documents;
using Pegasus.Infrastructure.Custody;

namespace Pegasus.Infrastructure.Persistence;

internal sealed class EfDocumentRequestStore(
    IDbContextFactory<PegasusDbContext> dbContextFactory,
    LocalDocumentContentStore contentStore,
    RequestUploadPolicy uploadPolicy,
    RequestUploadLimits uploadLimits,
    TimeProvider timeProvider) :
    ICreateRequestUploadLink,
    IRevokeRequestUploadLink,
    IUploadToRequest,
    IGetRequestUpload
{

    async Task<CreateRequestUploadLinkResult> ICreateRequestUploadLink.ExecuteAsync(
        CreateRequestUploadLinkCommand command,
        CancellationToken cancellationToken)
    {
        ValidateActorAndOperation(command.Actor, command.OperationKey);
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        _ = await RequireCaseAsync(context, command.CaseId, cancellationToken);
        var replay = await context.Set<RequestUploadLinkEntity>()
            .SingleOrDefaultAsync(
                value => value.CaseId == command.CaseId
                    && value.CreateOperationKey == command.OperationKey,
                cancellationToken);
        if (replay is not null)
        {
            return new(ToUploadLink(replay), null, true);
        }

        var issue = RequestUploadPolicy.CreateToken();
        var now = timeProvider.GetUtcNow();
        var entity = new RequestUploadLinkEntity
        {
            Id = Guid.NewGuid(),
            CaseId = command.CaseId,
            TokenDigest = issue.TokenDigest,
            Status = RequestUploadStatus.Active,
            CreatedAtUtc = now,
            ExpiresAtUtc = uploadPolicy.CalculateExpiry(now),
            LimitsVersion = uploadLimits.Version,
            Version = 1,
            CreateOperationKey = command.OperationKey
        };
        context.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return new(ToUploadLink(entity), issue.Secret, false);
    }

    async Task IRevokeRequestUploadLink.ExecuteAsync(
        RevokeRequestUploadLinkCommand command,
        CancellationToken cancellationToken)
    {
        ValidateActorAndOperation(command.Actor, command.OperationKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Reason);
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.Set<RequestUploadLinkEntity>()
            .SingleOrDefaultAsync(value => value.Id == command.RequestId, cancellationToken)
            ?? throw new InvalidOperationException("The upload request is unavailable.");
        if (entity.RevokeOperationKey is not null)
        {
            if (!string.Equals(entity.RevokeOperationKey, command.OperationKey, StringComparison.Ordinal))
            {
                throw new DbUpdateConcurrencyException("The upload request has already changed.");
            }

            return;
        }

        EnsureExpectedVersion(entity.Version, command.ExpectedVersion, "upload request");
        entity.Status = RequestUploadStatus.Revoked;
        entity.RevokedAtUtc = timeProvider.GetUtcNow();
        entity.RevokeOperationKey = command.OperationKey;
        entity.Version = checked(entity.Version + 1);
        await context.SaveChangesAsync(cancellationToken);
    }

    async Task<UploadToRequestResult> IUploadToRequest.ExecuteAsync(
        UploadToRequestCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.File);
        RequestUploadLinkEntity? entity;
        try
        {
            var digest = RequestUploadToken.ComputeDigest(command.Token);
            await using var lookupContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            entity = await lookupContext.Set<RequestUploadLinkEntity>()
                .AsNoTracking()
                .SingleOrDefaultAsync(value => value.TokenDigest == digest, cancellationToken);
        }
        catch (ArgumentException)
        {
            return Unavailable();
        }

        if (entity is null)
        {
            return Unavailable();
        }

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        entity = await context.Set<RequestUploadLinkEntity>()
            .SingleAsync(value => value.Id == entity.Id, cancellationToken);
        var priorReceipt = await context.Set<RequestUploadReceiptEntity>()
            .SingleOrDefaultAsync(
                value => value.RequestId == entity.Id
                    && value.OperationKey == command.File.OperationKey,
                cancellationToken);
        var authorization = uploadPolicy.Authorize(
            ToUploadLink(entity),
            new(command.Token, command.File, command.AttemptsInCurrentRateWindow),
            priorReceipt?.ContentHash);
        if (!authorization.MayEnterCustody)
        {
            return new(
                authorization.Decision,
                priorReceipt?.Id,
                authorization.IsReplay);
        }

        var caseEntity = await RequireCaseAsync(context, entity.CaseId, cancellationToken);
        var receiptId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var occurrenceId = Guid.NewGuid();
        var now = timeProvider.GetUtcNow();
        var sourceIdentity = $"request:{entity.Id:N}:{receiptId:N}";
        var document = new CaseDocumentEntity
        {
            Id = documentId,
            CaseId = entity.CaseId,
            SourceOccurrenceIdentity = sourceIdentity
        };
        var version = new DocumentVersionEntity
        {
            Id = versionId,
            DocumentId = documentId,
            Version = 1,
            FileName = authorization.SafeFileName!,
            MediaType = command.File.MediaType.Trim(),
            ContentLength = command.File.Content.Length,
            Sha256 = authorization.ContentHash!,
            CustodyStatus = DocumentCustodyStatus.Confirmed,
            CreatedAtUtc = now,
            CreatedBy = "request-upload",
            IsCurrent = true
        };
        var occurrence = new DocumentOccurrenceEntity
        {
            Id = occurrenceId,
            CaseId = entity.CaseId,
            DocumentId = documentId,
            VersionId = versionId,
            SemanticRole = DocumentSemanticRole.Other,
            Source = DocumentSource.RequestUpload,
            SourceOccurrenceIdentity = sourceIdentity,
            RecordedAtUtc = now,
            OperationKey = $"request:{entity.Id:N}:{command.File.OperationKey}"
        };
        var receipt = new RequestUploadReceiptEntity
        {
            Id = receiptId,
            RequestId = entity.Id,
            OccurrenceId = occurrenceId,
            VersionId = versionId,
            OperationKey = command.File.OperationKey,
            ContentHash = authorization.ContentHash!,
            ReceivedAtUtc = now
        };

        await contentStore.StoreAsync(
            entity.CaseId,
            versionId,
            command.File.Content,
            authorization.ContentHash!,
            cancellationToken);
        context.AddRange(document, version, occurrence, receipt);
        entity.AcceptedFileCount = checked(entity.AcceptedFileCount + 1);
        entity.AcceptedByteCount = checked(entity.AcceptedByteCount + command.File.Content.Length);
        entity.Version = checked(entity.Version + 1);
        if (entity.AcceptedFileCount >= uploadLimits.MaximumFileCount
            || entity.AcceptedByteCount >= uploadLimits.MaximumRequestBytes)
        {
            entity.Status = RequestUploadStatus.Exhausted;
        }

        caseEntity.Version = checked(caseEntity.Version + 1);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(RequestUploadDecision.Accepted, receiptId, false);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            await using var replayContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var concurrentReceipt = await replayContext.Set<RequestUploadReceiptEntity>()
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    value => value.RequestId == entity.Id
                        && value.OperationKey == command.File.OperationKey,
                    cancellationToken);
            if (concurrentReceipt is null)
            {
                throw;
            }

            return string.Equals(
                concurrentReceipt.ContentHash,
                authorization.ContentHash,
                StringComparison.Ordinal)
                ? new(RequestUploadDecision.Replay, concurrentReceipt.Id, true)
                : new(RequestUploadDecision.OperationConflict, null, false);
        }
    }

    async Task<RequestUploadPublicView?> IGetRequestUpload.ExecuteAsync(
        string token,
        CancellationToken cancellationToken)
    {
        string digest;
        try
        {
            digest = RequestUploadToken.ComputeDigest(token);
        }
        catch (ArgumentException)
        {
            return null;
        }

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.Set<RequestUploadLinkEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.TokenDigest == digest, cancellationToken);
        if (entity is null
            || !RequestUploadToken.Matches(token, entity.TokenDigest)
            || entity.Status != RequestUploadStatus.Active
            || entity.RevokedAtUtc is not null
            || entity.ExpiresAtUtc <= timeProvider.GetUtcNow()
            || !string.Equals(entity.LimitsVersion, uploadLimits.Version, StringComparison.Ordinal))
        {
            return null;
        }

        return new(uploadLimits.AllowedMediaTypes, uploadLimits.MaximumFileBytes);
    }

    private static async Task<CaseEntity> RequireCaseAsync(
        PegasusDbContext context,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        if (caseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(caseId));
        }

        return await context.Set<CaseEntity>()
            .SingleOrDefaultAsync(value => value.Id == caseId, cancellationToken)
            ?? throw new InvalidOperationException("The case is unavailable.");
    }

    private static void ValidateActorAndOperation(string actor, string operationKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);
    }

    private static void EnsureExpectedVersion(long actual, long expected, string aggregate)
    {
        if (actual != expected)
        {
            throw new DbUpdateConcurrencyException($"The {aggregate} version is stale.");
        }
    }


    private static RequestUploadLink ToUploadLink(RequestUploadLinkEntity value) => new(
        value.Id,
        value.CaseId,
        value.TokenDigest,
        value.Status,
        value.CreatedAtUtc,
        value.ExpiresAtUtc,
        value.RevokedAtUtc,
        value.AcceptedFileCount,
        value.AcceptedByteCount,
        value.LimitsVersion,
        value.Version);

    private static UploadToRequestResult Unavailable() =>
        new(RequestUploadDecision.Unavailable, null, false);
}
