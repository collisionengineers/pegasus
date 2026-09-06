using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Custody;

namespace Pegasus.Infrastructure.Persistence;

internal sealed class EfDocumentRequestStore(
    IDbContextFactory<PegasusDbContext> dbContextFactory,
    IDocumentContentStore contentStore,
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
        ArgumentNullException.ThrowIfNull(command);
        var operationKey = ValidateActorAndOperation(command.Actor, command.OperationKey);
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var history = await FindHistoryAsync(context, operationKey, cancellationToken);
        var replay = await context.Set<RequestUploadLinkEntity>()
            .SingleOrDefaultAsync(
                value => value.CaseId == command.CaseId
                    && value.CreateOperationKey == operationKey,
                cancellationToken);
        if (replay is not null)
        {
            if (history is null)
            {
                throw new InvalidDataException(
                    "The replayed upload-request creation is missing its action history.");
            }

            var replayLink = ToCreatedUploadLink(replay, history);
            DocumentActionHistory.RequireExactReplay(
                history,
                "request_upload_link",
                replay.Id.ToString("D"),
                "request_upload_created",
                command.Actor,
                reason: null,
                afterJson: history.AfterJson);
            return new(replayLink, null, true);
        }
        if (history is not null)
        {
            throw new InvalidOperationException(
                "The document operation key was already used for another audited action.");
        }
        var workflow = await RequireWorkflowAsync(context, command.CaseId, cancellationToken);
        CaseMutationGuard.Require(
            workflow,
            command.Actor,
            command.ExpectedCaseVersion,
            command.EditLeaseToken,
            timeProvider.GetUtcNow());

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
            CreateOperationKey = operationKey
        };
        context.Add(entity);
        context.ActionHistory.Add(DocumentActionHistory.Succeeded(
            "request_upload_link",
            entity.Id.ToString("D"),
            "request_upload_created",
            command.Actor,
            now,
            operationKey,
            afterJson: DocumentActionHistory.Serialize(HistoryValue(entity))));
        CaseMutationGuard.Complete(workflow);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(ToUploadLink(entity), issue.Secret, false);
    }

    async Task IRevokeRequestUploadLink.ExecuteAsync(
        RevokeRequestUploadLinkCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.CaseId == Guid.Empty || command.RequestId == Guid.Empty)
        {
            throw new ArgumentException(
                "Case and upload request identifiers are required.",
                nameof(command));
        }

        var operationKey = ValidateActorAndOperation(command.Actor, command.OperationKey);
        var reason = RequireText(command.Reason, 1000, nameof(command.Reason));
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var entity = await context.Set<RequestUploadLinkEntity>()
            .SingleOrDefaultAsync(
                value => value.Id == command.RequestId && value.CaseId == command.CaseId,
                cancellationToken)
            ?? throw new InvalidOperationException("The upload request is unavailable.");
        var history = await FindHistoryAsync(context, operationKey, cancellationToken);
        if (entity.RevokeOperationKey is not null)
        {
            if (!string.Equals(entity.RevokeOperationKey, operationKey, StringComparison.Ordinal))
            {
                throw new DbUpdateConcurrencyException("The upload request has already changed.");
            }
            if (history is null)
            {
                throw new InvalidDataException(
                    "The replayed upload-request revocation is missing its action history.");
            }

            DocumentActionHistory.RequireExactReplay(
                history,
                "request_upload_link",
                entity.Id.ToString("D"),
                "request_upload_revoked",
                command.Actor,
                reason,
                DocumentActionHistory.Serialize(HistoryValue(entity)));
            return;
        }
        if (history is not null)
        {
            throw new InvalidOperationException(
                "The document operation key was already used for another audited action.");
        }

        var workflow = await RequireWorkflowAsync(context, command.CaseId, cancellationToken);
        CaseMutationGuard.Require(
            workflow,
            command.Actor,
            command.ExpectedCaseVersion,
            command.EditLeaseToken,
            timeProvider.GetUtcNow());
        EnsureExpectedVersion(entity.Version, command.ExpectedRequestVersion, "upload request");
        var beforeJson = DocumentActionHistory.Serialize(HistoryValue(entity));
        entity.Status = RequestUploadStatus.Revoked;
        entity.RevokedAtUtc = timeProvider.GetUtcNow();
        entity.RevokeOperationKey = operationKey;
        entity.Version = checked(entity.Version + 1);
        context.ActionHistory.Add(DocumentActionHistory.Succeeded(
            "request_upload_link",
            entity.Id.ToString("D"),
            "request_upload_revoked",
            command.Actor,
            entity.RevokedAtUtc.Value,
            operationKey,
            reason,
            beforeJson,
            DocumentActionHistory.Serialize(HistoryValue(entity))));
        CaseMutationGuard.Complete(workflow);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
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
        CaseWorkflowEntity workflow;
        try
        {
            workflow = await RequireWorkflowAsync(context, entity.CaseId, cancellationToken);
            ArchivedCaseGuard.RequireMutable(workflow);
        }
        catch (Exception exception)
            when (exception is CaseArchivedException or CaseTerminalMutationException)
        {
            return Unavailable();
        }
        var caseReference = workflow.Case.Reference;
        var caseRootRemoteId = workflow.Case.CustodyRootRemoteId;

        var lastOrdinal = await context.Set<CaseDocumentEntity>()
            .Where(value => value.CaseId == entity.CaseId)
            .Select(value => (int?)value.Ordinal)
            .MaxAsync(cancellationToken) ?? 1;
        var ordinal = checked(lastOrdinal + 1);

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
            Ordinal = ordinal,
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
            Ordinal = ordinal,
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

        var contentAddress = new ManagedDocumentContentAddress(
            entity.CaseId,
            caseReference,
            caseRootRemoteId,
            occurrence.Id,
            occurrence.Ordinal,
            occurrence.DocumentId,
            version.Id,
            version.Version,
            occurrence.SemanticRole,
            version.FileName,
            version.MediaType);
        var contentWrite = await contentStore.StoreVersionAsync(
            contentAddress,
            command.File.Content,
            authorization.ContentHash!,
            cancellationToken);
        try
        {
            context.AddRange(document, version, occurrence, receipt);
            entity.AcceptedFileCount = checked(entity.AcceptedFileCount + 1);
            entity.AcceptedByteCount = checked(entity.AcceptedByteCount + command.File.Content.Length);
            entity.Version = checked(entity.Version + 1);
            if (entity.AcceptedFileCount >= uploadLimits.MaximumFileCount
                || entity.AcceptedByteCount >= uploadLimits.MaximumRequestBytes)
            {
                entity.Status = RequestUploadStatus.Exhausted;
            }

            CaseMutationGuard.Complete(workflow);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(RequestUploadDecision.Accepted, receiptId, false);
        }
        catch (Exception exception)
        {
            Exception? rollbackFailure = null;
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch (Exception caught)
            {
                rollbackFailure = caught;
            }

            try
            {
                if (contentWrite.Disposition == DocumentContentWriteDisposition.Created)
                {
                    await DocumentContentRollback.RemoveOrphanAsync(
                        dbContextFactory,
                        contentStore,
                        entity.CaseId,
                        caseReference,
                        versionId,
                        exception);
                }
            }
            catch (Exception cleanupFailure) when (rollbackFailure is not null)
            {
                throw new AggregateException(
                    "The request-upload database write failed, its rollback could not be confirmed, and custody cleanup did not complete.",
                    exception,
                    rollbackFailure,
                    cleanupFailure);
            }

            if (rollbackFailure is not null)
            {
                throw new AggregateException(
                    "The request-upload database transaction failed and its rollback could not be confirmed.",
                    exception,
                    rollbackFailure);
            }

            if (exception is not DbUpdateException)
            {
                throw;
            }

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

    private static async Task<CaseWorkflowEntity> RequireWorkflowAsync(
        PegasusDbContext context,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        if (caseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(caseId));
        }

        return await context.CaseWorkflows
            .Include(value => value.Case)
            .SingleOrDefaultAsync(value => value.CaseId == caseId, cancellationToken)
            ?? throw new InvalidOperationException("The case is unavailable.");
    }

    private static string ValidateActorAndOperation(
        ActionActor actor,
        string operationKey)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        return RequireText(operationKey, 100, nameof(operationKey));
    }

    private static string RequireText(string value, int maximumLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"The value cannot exceed {maximumLength} characters.");
        }

        return normalized;
    }

    private static Task<ActionHistoryEntity?> FindHistoryAsync(
        PegasusDbContext context,
        string operationKey,
        CancellationToken cancellationToken) =>
        context.ActionHistory.SingleOrDefaultAsync(
            value => value.AggregateType == "request_upload_link"
                && value.CorrelationId == operationKey,
            cancellationToken);

    private static RequestUploadHistoryValue HistoryValue(RequestUploadLinkEntity entity) => new(
        entity.Id,
        entity.CaseId,
        entity.Status.ToString(),
        entity.CreatedAtUtc,
        entity.ExpiresAtUtc,
        entity.RevokedAtUtc,
        entity.AcceptedFileCount,
        entity.AcceptedByteCount,
        entity.LimitsVersion,
        entity.Version);

    private sealed record RequestUploadHistoryValue(
        Guid RequestId,
        Guid CaseId,
        string Status,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset ExpiresAtUtc,
        DateTimeOffset? RevokedAtUtc,
        int AcceptedFileCount,
        long AcceptedByteCount,
        string LimitsVersion,
        long Version);

    private static RequestUploadLink ToCreatedUploadLink(
        RequestUploadLinkEntity current,
        ActionHistoryEntity history)
    {
        var snapshot =
            DocumentActionHistory.Deserialize<RequestUploadHistoryValue>(history.AfterJson);
        if (snapshot.RequestId != current.Id
            || snapshot.CaseId != current.CaseId
            || !string.Equals(
                snapshot.Status,
                RequestUploadStatus.Active.ToString(),
                StringComparison.Ordinal)
            || snapshot.CreatedAtUtc != current.CreatedAtUtc
            || snapshot.ExpiresAtUtc != current.ExpiresAtUtc
            || snapshot.RevokedAtUtc is not null
            || snapshot.AcceptedFileCount != 0
            || snapshot.AcceptedByteCount != 0
            || !string.Equals(
                snapshot.LimitsVersion,
                current.LimitsVersion,
                StringComparison.Ordinal)
            || snapshot.Version != 1)
        {
            throw new InvalidDataException(
                "The replayed upload-request creation snapshot is invalid.");
        }

        return new(
            snapshot.RequestId,
            snapshot.CaseId,
            current.TokenDigest,
            RequestUploadStatus.Active,
            snapshot.CreatedAtUtc,
            snapshot.ExpiresAtUtc,
            RevokedAtUtc: null,
            AcceptedFileCount: 0,
            AcceptedByteCount: 0,
            snapshot.LimitsVersion,
            snapshot.Version);
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

/// <summary>
/// Where a public submission's occurrences keep their custody state.
/// </summary>
/// <remarks>
/// <para>
/// The operation key this port is addressed by is scoped by the session that
/// issued the occurrence — <c>public-upload:{sessionId:N}:{key}</c>, minted by
/// <see cref="ScopeOperationKey"/> — because a sender's own key is only unique
/// inside their session and this lookup is global.
/// </para>
/// <para>
/// Custody's remote identities are written onto the document version the
/// occurrence points at, and only for a confirmed disposition: recording a Box
/// file against a pending or failed retention would assert that custody holds
/// bytes it has not said it holds.
/// </para>
/// </remarks>
internal sealed class EfPublicUploadRetentionStore(
    IDbContextFactory<PegasusDbContext> dbContextFactory) : IIncomingArtifactRetentionStore
{
    public static string ScopeOperationKey(Guid sessionId, string operationKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);
        return $"public-upload:{sessionId:N}:{operationKey.Trim()}";
    }

    public async Task<RetainedIncomingArtifact?> FindAsync(
        string operationKey,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var row = await context.Set<PublicUploadOccurrenceEntity>()
            .AsNoTracking()
            .Where(occurrence => occurrence.OperationKey == operationKey)
            .Select(occurrence => new
            {
                occurrence.Id,
                occurrence.OperationKey,
                occurrence.CustodyState,
                occurrence.DocumentId,
                occurrence.DocumentVersionId,
                CaseId = context.Set<PublicUploadSessionEntity>()
                    .Where(session => session.Id == occurrence.SessionId)
                    .Join(
                        context.Set<RequestUploadLinkEntity>(),
                        session => session.RequestUploadLinkId,
                        link => link.Id,
                        (_, link) => (Guid?)link.CaseId)
                    .FirstOrDefault(),
                // One subquery for both identities: they live on the same row,
                // so asking for them separately would read it twice.
                Remote = context.Set<DocumentVersionEntity>()
                    .Where(version => version.Id == occurrence.DocumentVersionId)
                    .Select(version => new { version.BoxFileId, version.BoxVersionId })
                    .FirstOrDefault()
            })
            .SingleOrDefaultAsync(cancellationToken);
        return row is null
            ? null
            : new(
                row.Id,
                row.OperationKey,
                ParseCustodyState(row.CustodyState),
                row.CaseId,
                row.DocumentId,
                row.DocumentVersionId,
                row.Remote?.BoxFileId,
                row.Remote?.BoxVersionId);
    }

    public async Task RecordAsync(
        RetainedIncomingArtifact artifact,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var occurrence = await context.Set<PublicUploadOccurrenceEntity>()
            .SingleOrDefaultAsync(item => item.Id == artifact.OccurrenceId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Public upload occurrence '{artifact.OccurrenceId}' was not found.");
        occurrence.CustodyState = ToCode(artifact.State);
        occurrence.DocumentId = artifact.DocumentId;
        occurrence.DocumentVersionId = artifact.DocumentVersionId;

        // Only a confirmed retention says anything about where custody holds
        // the bytes, so only a confirmed retention writes the remote
        // identities — and it never writes null. A version can back more than
        // one occurrence (two arrivals of the same file are two occurrences),
        // so clearing on a later Pending or Failed record would erase an
        // earlier confirmed identity that is still true.
        if (artifact.State == IncomingArtifactCustodyState.Confirmed
            && artifact.DocumentVersionId is { } versionId)
        {
            var version = await context.Set<DocumentVersionEntity>()
                .SingleOrDefaultAsync(item => item.Id == versionId, cancellationToken);
            if (version is not null)
            {
                version.BoxFileId = artifact.BoxFileId ?? version.BoxFileId;
                version.BoxVersionId = artifact.BoxVersionId ?? version.BoxVersionId;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    internal static string ToCode(IncomingArtifactCustodyState state) => state switch
    {
        IncomingArtifactCustodyState.Pending => "pending",
        IncomingArtifactCustodyState.Confirmed => "confirmed",
        IncomingArtifactCustodyState.Failed => "failed",
        IncomingArtifactCustodyState.Unknown => "unknown",
        _ => throw new ArgumentOutOfRangeException(nameof(state))
    };

    internal static IncomingArtifactCustodyState ParseCustodyState(string value) => value switch
    {
        "pending" => IncomingArtifactCustodyState.Pending,
        "confirmed" => IncomingArtifactCustodyState.Confirmed,
        "failed" => IncomingArtifactCustodyState.Failed,
        "unknown" => IncomingArtifactCustodyState.Unknown,
        // An unrecognised stored state is never read as success.
        _ => throw new InvalidOperationException(
            $"The retained occurrence custody state '{value}' is not recognized.")
    };
}
