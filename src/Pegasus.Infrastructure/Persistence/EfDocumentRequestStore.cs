using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The request-upload link's persistence: staff issue and revoke it, and the
/// public page submits through it.
/// </summary>
/// <remarks>
/// <para>
/// The submission path does not retain anything itself. It decides whether the
/// bytes may be offered, records the occurrence that addresses this arrival,
/// and then hands the bytes to <see cref="RetainIncomingArtifact"/> — the one
/// command that talks to custody. Custody creates the document and version and
/// says what state they are in; nothing here writes a custody status.
/// </para>
/// <para>
/// <paramref name="retention"/> is optional because the Web host composes this
/// store before the custody adapter behind that command exists. When it is
/// absent the submission path refuses before writing a single row: an upload
/// that cannot reach custody must not leave an occurrence claiming it did.
/// This is the same optional-bridge shape the C01 and C08 stores use for their
/// unregistered ports.
/// </para>
/// </remarks>
internal sealed class EfDocumentRequestStore(
    IDbContextFactory<PegasusDbContext> dbContextFactory,
    RequestUploadPolicy uploadPolicy,
    RequestUploadLimits uploadLimits,
    TimeProvider timeProvider,
    RetainIncomingArtifact? retention = null) :
    ICreateRequestUploadLink,
    IRevokeRequestUploadLink,
    IUploadToRequest,
    IGetRequestUpload
{
    /// <summary>
    /// The longest sender-supplied operation key a submission may carry — the
    /// same bound this store applies to a staff operation key, and low enough
    /// that the link-scoped form still fits every column that carries it. It
    /// is checked before custody so an over-long key cannot fail the write
    /// that follows a hand-over.
    /// </summary>
    private const int MaximumOperationKeyLength = 100;

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

        // Fail closed before anything is read or written. Without the
        // retention command there is nothing to hand the bytes to, so an
        // occurrence recorded here would claim a custody that never happened.
        if (retention is null)
        {
            return Unavailable();
        }

        Guid linkId;
        try
        {
            var digest = RequestUploadToken.ComputeDigest(command.Token);
            await using var lookupContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var found = await lookupContext.Set<RequestUploadLinkEntity>()
                .AsNoTracking()
                .Where(value => value.TokenDigest == digest)
                .Select(value => (Guid?)value.Id)
                .SingleOrDefaultAsync(cancellationToken);
            if (found is not { } value)
            {
                return Unavailable();
            }

            linkId = value;
        }
        catch (ArgumentException)
        {
            return Unavailable();
        }

        var authorized = await AuthorizeAndRecordArrivalAsync(command, linkId, cancellationToken);
        if (authorized.Refusal is { } refusal)
        {
            return refusal;
        }

        var arrival = authorized.Arrival!;

        // The hand-over runs outside the transaction: custody keeps its own
        // durable state and must not be called while our locks are held.
        await using var content = OpenContent(command.File.Content);
        var retained = await retention.ExecuteAsync(
            // Stream A's rule: the authority is the persisted upload-link row,
            // never a document-request identity and never anything the sender
            // supplied.
            ActionActor.RequestLink(arrival.LinkId),
            new(
                arrival.OccurrenceId,
                // The link's own Case, read inside the transaction that
                // authorized this upload.
                arrival.CaseId,
                IntakeReceiptId: null,
                arrival.ScopedOperationKey,
                arrival.ProposedName,
                arrival.MediaType,
                arrival.ContentLength,
                arrival.Sha256),
            content,
            cancellationToken);
        if (retained.State is not (IncomingArtifactCustodyState.Confirmed
            or IncomingArtifactCustodyState.Pending))
        {
            // Refused, or neither confirmed nor refused. The occurrence
            // already records that exact state, nothing is counted, the window
            // does not open, and the same operation key may be sent again —
            // it reconciles an uncertain hand-over instead of repeating it.
            return new(RequestUploadDecision.NotRetained, null, false);
        }

        if (retained.DocumentVersionId is not { } versionId)
        {
            // Custody took the bytes without naming the version it took them
            // into. That cannot be receipted, so it is not reported as
            // accepted either.
            return new(RequestUploadDecision.NotRetained, null, false);
        }

        return await RecordAcceptedAsync(
            arrival,
            versionId,
            retained.IsConfirmed,
            cancellationToken);
    }

    /// <summary>
    /// Decides whether these bytes may be offered to custody and, if they may,
    /// commits the occurrence that addresses this arrival before any hand-over
    /// happens.
    /// </summary>
    /// <remarks>
    /// Everything that can refuse lives here, inside one transaction: the
    /// link's own policy, the Case's mutability, the fixed submission window,
    /// and the session-scoped occurrence slot. The occurrence is committed
    /// first so a crash mid-custody leaves a reconcilable arrival rather than
    /// nothing at all.
    /// </remarks>
    private async Task<ArrivalDecision> AuthorizeAndRecordArrivalAsync(
        UploadToRequestCommand command,
        Guid linkId,
        CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var link = await context.Set<RequestUploadLinkEntity>()
            .SingleAsync(value => value.Id == linkId, cancellationToken);
        var senderOperationKey = command.File.OperationKey?.Trim() ?? string.Empty;
        var priorReceipt = await context.Set<RequestUploadReceiptEntity>()
            .SingleOrDefaultAsync(
                value => value.RequestId == linkId
                    && value.OperationKey == senderOperationKey,
                cancellationToken);
        var authorization = uploadPolicy.Authorize(
            ToUploadLink(link),
            new(command.Token, command.File, command.AttemptsInCurrentRateWindow),
            priorReceipt?.ContentHash);
        if (!authorization.MayEnterCustody)
        {
            // A completed prior submission answers here without offering the
            // bytes again: Replay returns that receipt, and a different file
            // under the same key is a conflict.
            return ArrivalDecision.Refuse(
                authorization.Decision,
                priorReceipt?.Id,
                authorization.IsReplay);
        }

        // The receipt's key column is bounded, and the occurrence key carries
        // the link prefix on top of it, so an over-long key is refused before
        // custody rather than after it.
        if (senderOperationKey.Length > MaximumOperationKeyLength)
        {
            return ArrivalDecision.Refuse(RequestUploadDecision.InvalidFile);
        }

        CaseWorkflowEntity workflow;
        try
        {
            workflow = await RequireWorkflowAsync(context, link.CaseId, cancellationToken);
            ArchivedCaseGuard.RequireMutable(workflow);
        }
        catch (Exception exception)
            when (exception is CaseArchivedException or CaseTerminalMutationException)
        {
            return ArrivalDecision.Refuse(RequestUploadDecision.Unavailable);
        }

        var now = timeProvider.GetUtcNow();
        var session = await context.Set<PublicUploadSessionEntity>()
            .SingleOrDefaultAsync(
                value => value.RequestUploadLinkId == linkId,
                cancellationToken);
        if (session is null)
        {
            // One session per link, opened by the first file custody confirms.
            // Until then it exists with its window shut.
            session = new()
            {
                Id = Guid.NewGuid(),
                RequestUploadLinkId = linkId,
                LimitsVersion = link.LimitsVersion,
                Version = 1,
                ConcurrencyToken = Guid.NewGuid()
            };
            context.Add(session);
        }
        else if (!string.Equals(
            session.LimitsVersion,
            uploadLimits.Version,
            StringComparison.Ordinal))
        {
            // The session records which accepted limits the sender's earlier
            // bytes were taken under. Nothing is migrated: the Case owner
            // reissues on explicit staff action.
            return ArrivalDecision.Refuse(RequestUploadDecision.LimitsVersionMismatch);
        }

        if (!PublicUploadSessionPolicy.AcceptsBytes(ToSession(session), now))
        {
            // Finalized or expired. Unavailable is the refusal that discloses
            // nothing about the Case behind the link.
            return ArrivalDecision.Refuse(RequestUploadDecision.Unavailable);
        }

        var scopedOperationKey = EfPublicUploadRetentionStore.ScopeOperationKey(
            linkId,
            senderOperationKey);
        var occurrence = await context.Set<PublicUploadOccurrenceEntity>()
            .SingleOrDefaultAsync(
                value => value.SessionId == session.Id
                    && value.OperationKey == scopedOperationKey,
                cancellationToken);
        if (occurrence is null)
        {
            occurrence = new()
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                OperationKey = scopedOperationKey,
                ProposedName = authorization.SafeFileName!,
                MediaType = command.File.MediaType.Trim(),
                Size = command.File.Content.Length,
                Sha256 = authorization.ContentHash!,
                CustodyState = EfPublicUploadRetentionStore.ToCode(
                    IncomingArtifactCustodyState.Pending)
            };
            context.Add(occurrence);
        }
        else if (!string.Equals(
            occurrence.Sha256,
            authorization.ContentHash,
            StringComparison.Ordinal))
        {
            // The slot is addressed by its server-issued occurrence, so the
            // same key carrying different bytes is a conflict rather than a
            // silent replacement.
            return ArrivalDecision.Refuse(RequestUploadDecision.OperationConflict);
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ArrivalDecision.Accept(new(
            linkId,
            link.CaseId,
            session.Id,
            occurrence.Id,
            senderOperationKey,
            scopedOperationKey,
            occurrence.ProposedName,
            occurrence.MediaType,
            occurrence.Size,
            occurrence.Sha256));
    }

    /// <summary>
    /// Records what an accepted hand-over changed: the receipt, the link's
    /// accepted totals, and — for a confirmed file — the fixed submission
    /// window.
    /// </summary>
    /// <remarks>
    /// This runs after custody has answered because a receipt cannot name the
    /// document version before one exists. The occurrence committed before the
    /// hand-over is what makes a retry safe in the meantime, so nothing here
    /// is load-bearing for replay.
    /// </remarks>
    private async Task<UploadToRequestResult> RecordAcceptedAsync(
        AcceptedArrival arrival,
        Guid versionId,
        bool isConfirmed,
        CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var link = await context.Set<RequestUploadLinkEntity>()
            .SingleAsync(value => value.Id == arrival.LinkId, cancellationToken);
        var receipt = await context.Set<RequestUploadReceiptEntity>()
            .SingleOrDefaultAsync(
                value => value.RequestId == arrival.LinkId
                    && value.OperationKey == arrival.SenderOperationKey,
                cancellationToken);
        if (receipt is not null)
        {
            // A concurrent request completed this same operation first.
            return new(RequestUploadDecision.Replay, receipt.Id, true);
        }

        var now = timeProvider.GetUtcNow();

        // The receipt's occurrence column is a foreign key into the document
        // occurrences custody owns, so it is filled from the occurrence custody
        // created for this version. An adapter that creates none leaves nothing
        // valid to point at, and the public occurrence row stays the durable
        // record of the arrival.
        var documentOccurrenceId = await context.Set<DocumentOccurrenceEntity>()
            .Where(value => value.VersionId == versionId)
            .OrderBy(value => value.RecordedAtUtc)
            .Select(value => (Guid?)value.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var receiptId = Guid.NewGuid();
        if (documentOccurrenceId is { } documentOccurrence)
        {
            context.Add(new RequestUploadReceiptEntity
            {
                Id = receiptId,
                RequestId = arrival.LinkId,
                OccurrenceId = documentOccurrence,
                VersionId = versionId,
                OperationKey = arrival.SenderOperationKey,
                ContentHash = arrival.Sha256,
                ReceivedAtUtc = now
            });
        }

        if (isConfirmed)
        {
            var session = await context.Set<PublicUploadSessionEntity>()
                .SingleAsync(value => value.Id == arrival.SessionId, cancellationToken);
            var started = PublicUploadSessionPolicy.Start(
                ToSession(session),
                arrival.ToConfirmedOccurrence(versionId),
                now);
            session.StartedAtUtc = started.StartedAtUtc;
            session.ExpiresAtUtc = started.ExpiresAtUtc;
            session.Version = started.Version;
        }

        link.AcceptedFileCount = checked(link.AcceptedFileCount + 1);
        link.AcceptedByteCount = checked(link.AcceptedByteCount + arrival.ContentLength);
        link.Version = checked(link.Version + 1);
        if (link.AcceptedFileCount >= uploadLimits.MaximumFileCount
            || link.AcceptedByteCount >= uploadLimits.MaximumRequestBytes)
        {
            link.Status = RequestUploadStatus.Exhausted;
        }

        var workflow = await RequireWorkflowAsync(context, link.CaseId, cancellationToken);
        CaseMutationGuard.Complete(workflow);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(
            RequestUploadDecision.Accepted,
            documentOccurrenceId is null ? null : receiptId,
            false);
    }

    /// <summary>
    /// Reads the submitted bytes without copying them a second time when the
    /// caller already owns an array — a hundred-megabyte per-file limit makes
    /// that copy worth avoiding.
    /// </summary>
    private static MemoryStream OpenContent(ReadOnlyMemory<byte> content) =>
        MemoryMarshal.TryGetArray(content, out var segment) && segment.Array is not null
            ? new MemoryStream(segment.Array, segment.Offset, segment.Count, writable: false)
            : new MemoryStream(content.ToArray(), writable: false);

    private static PublicUploadSession ToSession(PublicUploadSessionEntity value) => new(
        value.Id,
        value.RequestUploadLinkId,
        value.LimitsVersion,
        value.StartedAtUtc,
        value.FinalizedAtUtc,
        value.ExpiresAtUtc,
        value.Version);

    /// <summary>One authorized arrival, committed and awaiting custody.</summary>
    private sealed record AcceptedArrival(
        Guid LinkId,
        Guid CaseId,
        Guid SessionId,
        Guid OccurrenceId,
        string SenderOperationKey,
        string ScopedOperationKey,
        string ProposedName,
        string MediaType,
        long ContentLength,
        string Sha256)
    {
        public PublicUploadOccurrence ToConfirmedOccurrence(Guid versionId) => new(
            OccurrenceId,
            SessionId,
            ScopedOperationKey,
            ProposedName,
            MediaType,
            ContentLength,
            Sha256,
            IncomingArtifactCustodyState.Confirmed,
            DocumentVersionId: versionId);
    }

    /// <summary>
    /// Either a typed refusal the sender is given, or the arrival that has
    /// been committed and may now be offered to custody.
    /// </summary>
    private sealed record ArrivalDecision(
        UploadToRequestResult? Refusal,
        AcceptedArrival? Arrival)
    {
        public static ArrivalDecision Refuse(
            RequestUploadDecision decision,
            Guid? receiptId = null,
            bool isReplay = false) => new(new(decision, receiptId, isReplay), null);

        public static ArrivalDecision Accept(AcceptedArrival arrival) => new(null, arrival);
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

        // Nothing on this path reads the Case row itself any more: custody
        // owns the document's storage identity, so the workflow's own guard
        // and version fields are the whole of what is needed.
        return await context.CaseWorkflows
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
/// The operation key this port is addressed by is scoped by the upload link
/// that issued the occurrence — <c>request:{linkId:N}:{key}</c>, minted by
/// <see cref="ScopeOperationKey"/> — because a sender's own key is only unique
/// inside their own link and this lookup is global. The link, not its one
/// session, is the scope: a key must still name the same retention if the
/// session row is ever rebuilt beneath it.
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
    public static string ScopeOperationKey(Guid requestUploadLinkId, string operationKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);
        return $"request:{requestUploadLinkId:N}:{operationKey.Trim()}";
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
