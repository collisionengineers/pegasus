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
    /// <remarks>
    /// The server-issued key a second, different file is given is longer than
    /// what a sender may send - a root and a digest,
    /// <see cref="RequestUploadOperationKey.MaximumLength"/> - and is bounded
    /// by its own shape rather than by this. Both fit the receipt's 256 and the
    /// occurrence's 450 with the link scope on top.
    /// </remarks>
    private const int MaximumOperationKeyLength = 100;

    async Task<CreateRequestUploadLinkResult> ICreateRequestUploadLink.ExecuteAsync(
        CreateRequestUploadLinkCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var operationKey = ValidateActorAndOperation(command.Actor, command.OperationKey);
        var (recipient, reason) = NormalizeCreate(command.Recipient, command.Reason);
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
            if (!string.Equals(replayLink.Recipient, recipient, StringComparison.Ordinal)
                || !string.Equals(replayLink.Reason, reason, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The upload-request creation operation key was reused with different values.");
            }
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
            Recipient = recipient,
            Reason = reason,
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
        var occurrence = await FindOccurrenceAsync(
            context,
            session.Id,
            scopedOperationKey,
            cancellationToken);
        if (occurrence is not null
            && !string.Equals(
                occurrence.Sha256,
                authorization.ContentHash,
                StringComparison.Ordinal))
        {
            // The same key, carrying a different file. While the arrival it
            // names is still unresolved that key belongs to the first file and
            // has to keep belonging to it, so this is neither a replacement
            // nor a conflict: it is the second deliberate submission plan
            // item 6 allows until finalization, and it gets its own
            // server-issued key derived from this one and these bytes. The
            // derivation is what keeps its own retry a retry. A key whose
            // arrival custody has already answered is closed, and a different
            // file sent under it stays the conflict it was.
            if (!EfPublicUploadRetentionStore.UnresolvedCodes.Contains(occurrence.CustodyState))
            {
                return ArrivalDecision.Refuse(RequestUploadDecision.OperationConflict);
            }

            senderOperationKey = RequestUploadOperationKey.ForContent(
                senderOperationKey,
                authorization.ContentHash!);
            scopedOperationKey = EfPublicUploadRetentionStore.ScopeOperationKey(
                linkId,
                senderOperationKey);
            occurrence = await FindOccurrenceAsync(
                context,
                session.Id,
                scopedOperationKey,
                cancellationToken);
        }
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
                // Not a custody state: the bytes have not been offered yet.
                // A distinct code is what lets the accepted totals count an
                // occurrence exactly once, when custody first accepts it.
                CustodyState = EfPublicUploadRetentionStore.ArrivedCode
            };
            context.Add(occurrence);
        }
        else if (!string.Equals(
            occurrence.Sha256,
            authorization.ContentHash,
            StringComparison.Ordinal))
        {
            // A derived key names these exact bytes, so a row under it that
            // holds other bytes cannot arise from this path. Refused rather
            // than trusted: whatever wrote it, the slot is not this file's.
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
    /// The arrival one session-scoped operation key already has, if any. The
    /// pair is uniquely indexed, so this is the slot itself and not a guess at
    /// it.
    /// </summary>
    private static Task<PublicUploadOccurrenceEntity?> FindOccurrenceAsync(
        PegasusDbContext context,
        Guid sessionId,
        string scopedOperationKey,
        CancellationToken cancellationToken) =>
        context.Set<PublicUploadOccurrenceEntity>()
            .SingleOrDefaultAsync(
                value => value.SessionId == sessionId
                    && value.OperationKey == scopedOperationKey,
                cancellationToken);

    /// <summary>
    /// Records what an accepted hand-over changed: the link's accepted totals,
    /// and — for a confirmed file — its receipt and the fixed submission
    /// window.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This runs after custody has answered because a receipt cannot name the
    /// document version before one exists. The occurrence committed before the
    /// hand-over is what makes a retry safe in the meantime, so nothing here
    /// is load-bearing for replay.
    /// </para>
    /// <para>
    /// Only a confirmed hand-over earns a receipt. A receipt refuses every
    /// later submission of its operation key as a replay, which is right for a
    /// file custody holds and wrong for one it has not finished: a receipted
    /// Pending could never be asked about again, and would render as being
    /// stored for ever.
    /// </para>
    /// </remarks>
    private async Task<UploadToRequestResult> RecordAcceptedAsync(
        AcceptedArrival arrival,
        Guid versionId,
        bool isConfirmed,
        CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var link = await LockLinkAsync(context, arrival.LinkId, cancellationToken);
        var receipt = await context.Set<RequestUploadReceiptEntity>()
            .SingleOrDefaultAsync(
                value => value.RequestId == arrival.LinkId
                    && value.OperationKey == arrival.SenderOperationKey,
                cancellationToken);
        if (receipt is not null)
        {
            // A concurrent request confirmed this same operation first.
            return new(RequestUploadDecision.Replay, receipt.Id, true);
        }

        var now = timeProvider.GetUtcNow();
        Guid? receiptId = null;
        if (isConfirmed)
        {
            // The receipt's occurrence column is a foreign key into the
            // document occurrences custody owns, so it is filled from the
            // occurrence custody created for this version. An adapter that
            // creates none leaves nothing valid to point at, and the public
            // occurrence row stays the durable record of the arrival.
            var documentOccurrenceId = await context.Set<DocumentOccurrenceEntity>()
                .Where(value => value.VersionId == versionId)
                .OrderBy(value => value.RecordedAtUtc)
                .Select(value => (Guid?)value.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (documentOccurrenceId is { } documentOccurrence)
            {
                receiptId = Guid.NewGuid();
                context.Add(new RequestUploadReceiptEntity
                {
                    Id = receiptId.Value,
                    RequestId = arrival.LinkId,
                    OccurrenceId = documentOccurrence,
                    VersionId = versionId,
                    OperationKey = arrival.SenderOperationKey,
                    ContentHash = arrival.Sha256,
                    ReceivedAtUtc = now
                });
            }

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

        await ApplyAcceptedTotalsAsync(context, link, arrival.SessionId, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(
            isConfirmed
                ? RequestUploadDecision.Accepted
                : RequestUploadDecision.AcceptedPending,
            receiptId,
            false);
    }

    /// <summary>
    /// Sets the link's accepted totals to what its session's occurrences hold,
    /// and exhausts the link when they reach a limit.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The totals are derived rather than incremented, because the committed
    /// occurrence — not the receipt — is what says a file was accepted, and it
    /// says it exactly once however many times the same operation key is sent.
    /// An arrival still being offered carries
    /// <see cref="EfPublicUploadRetentionStore.ArrivedCode"/> and is not one of
    /// them, so nothing is counted before custody answers.
    /// </para>
    /// <para>
    /// A replay changes nothing and therefore bumps nothing. When the totals do
    /// change, the Case workflow is completed only if the Case would still
    /// accept an edit: custody already holds the bytes, so the arrival must be
    /// recorded either way, but a Case archived or moved terminal during the
    /// hand-over does not have its version bumped and its edit lease cleared on
    /// the strength of one.
    /// </para>
    /// </remarks>
    private async Task ApplyAcceptedTotalsAsync(
        PegasusDbContext context,
        RequestUploadLinkEntity link,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var totals = await context.Set<PublicUploadOccurrenceEntity>()
            .Where(value => value.SessionId == sessionId
                && (value.CustodyState == EfPublicUploadRetentionStore.ConfirmedCode
                    || value.CustodyState == EfPublicUploadRetentionStore.PendingCode))
            .GroupBy(value => value.SessionId)
            .Select(group => new
            {
                FileCount = group.Count(),
                ByteCount = group.Sum(value => value.Size)
            })
            .SingleOrDefaultAsync(cancellationToken);
        var fileCount = totals?.FileCount ?? 0;
        var byteCount = totals?.ByteCount ?? 0;
        var changed = fileCount != link.AcceptedFileCount
            || byteCount != link.AcceptedByteCount;
        link.AcceptedFileCount = fileCount;
        link.AcceptedByteCount = byteCount;
        if (fileCount >= uploadLimits.MaximumFileCount
            || byteCount >= uploadLimits.MaximumRequestBytes)
        {
            changed = changed || link.Status != RequestUploadStatus.Exhausted;
            link.Status = RequestUploadStatus.Exhausted;
        }

        if (!changed)
        {
            return;
        }

        link.Version = checked(link.Version + 1);
        var workflow = await RequireWorkflowAsync(context, link.CaseId, cancellationToken);
        if (IsMutable(workflow))
        {
            CaseMutationGuard.Complete(workflow);
        }
    }

    /// <summary>
    /// Reads the link for update. Its accepted totals are derived from the
    /// session's occurrences, so two accepted arrivals must not compute them
    /// against different committed sets. The update lock is the same idiom the
    /// Triage sequence allocation uses, and it is the first lock this
    /// transaction takes, so concurrent submissions queue on the link rather
    /// than deadlocking against each other.
    /// </summary>
    private static async Task<RequestUploadLinkEntity> LockLinkAsync(
        PegasusDbContext context,
        Guid linkId,
        CancellationToken cancellationToken)
    {
        var links = context.Set<RequestUploadLinkEntity>();
        return context.Database.IsSqlServer()
            ? await links
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM [RequestUploadLinks] WITH (UPDLOCK, HOLDLOCK)
                    WHERE [Id] = {linkId}
                """)
                .SingleAsync(cancellationToken)
            : await links.SingleAsync(value => value.Id == linkId, cancellationToken);
    }

    /// <summary>
    /// Whether the Case would still accept an edit. Asked, rather than
    /// asserted, because the caller has to record an arrival custody already
    /// holds even when the answer is no.
    /// </summary>
    private static bool IsMutable(CaseWorkflowEntity workflow)
    {
        try
        {
            ArchivedCaseGuard.RequireMutable(workflow);
            return true;
        }
        catch (Exception exception)
            when (exception is CaseArchivedException or CaseTerminalMutationException)
        {
            return false;
        }
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

        // A submission that has not resolved keeps its own operation key on the
        // page. Minting a fresh one while an arrival is claimed, uncertain, or
        // accepted-and-unconfirmed is what turns a sender's retry into a second
        // deliberate submission of bytes custody may already hold.
        var linkId = entity.Id;
        var unresolved = await context.Set<PublicUploadOccurrenceEntity>()
            .AsNoTracking()
            .Where(item =>
                EfPublicUploadRetentionStore.UnresolvedCodes.Contains(item.CustodyState)
                && context.Set<PublicUploadSessionEntity>().Any(session =>
                    session.Id == item.SessionId
                    && session.RequestUploadLinkId == linkId))
            // One link has one session and one unresolved arrival at a time in
            // practice; the ordering is only so that two never race to be the
            // one presented.
            .OrderBy(item => item.OperationKey)
            .Select(item => item.OperationKey)
            .FirstOrDefaultAsync(cancellationToken);
        return new(
            uploadLimits.AllowedMediaTypes,
            uploadLimits.MaximumFileBytes,
            unresolved is null
                ? null
                : EfPublicUploadRetentionStore.UnscopeOperationKey(linkId, unresolved));
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

    private static (string Recipient, string? Reason) NormalizeCreate(
        string recipient,
        string? reason)
    {
        var normalizedRecipient = RequireText(recipient, 500, nameof(recipient));
        var normalizedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (normalizedReason?.Length > 1000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(reason),
                "The value cannot exceed 1000 characters.");
        }

        return (normalizedRecipient, normalizedReason);
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
        ValidateStoredRecipient(entity.Recipient),
        ValidateStoredReason(entity.Reason),
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
        string Recipient,
        string? Reason,
        long Version);

    private static string ValidateStoredRecipient(string? recipient)
    {
        if (string.IsNullOrWhiteSpace(recipient)
            || recipient.Length > 500
            || !string.Equals(recipient, recipient.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The upload-request link has an invalid recipient.");
        }

        return recipient;
    }

    private static string? ValidateStoredReason(string? reason)
    {
        if (reason is not null
            && (string.IsNullOrWhiteSpace(reason)
                || reason.Length > 1000
                || !string.Equals(reason, reason.Trim(), StringComparison.Ordinal)))
        {
            throw new InvalidDataException(
                "The upload-request link has an invalid reason.");
        }

        return reason;
    }

    private static RequestUploadLink ToCreatedUploadLink(
        RequestUploadLinkEntity current,
        ActionHistoryEntity history)
    {
        var snapshot =
            DocumentActionHistory.Deserialize<RequestUploadHistoryValue>(history.AfterJson);
        var snapshotRecipient = ValidateStoredRecipient(snapshot.Recipient);
        var snapshotReason = ValidateStoredReason(snapshot.Reason);
        var currentRecipient = ValidateStoredRecipient(current.Recipient);
        var currentReason = ValidateStoredReason(current.Reason);
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
            || !string.Equals(snapshotRecipient, currentRecipient, StringComparison.Ordinal)
            || !string.Equals(snapshotReason, currentReason, StringComparison.Ordinal)
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
            snapshot.Version,
            snapshotRecipient,
            snapshotReason);
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
        value.Version,
        ValidateStoredRecipient(value.Recipient),
        ValidateStoredReason(value.Reason));

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
    /// <summary>
    /// The state of an arrival that has been committed but not yet offered to
    /// custody. It is deliberately not one of the four custody states: custody
    /// has said nothing about it, so the accepted totals do not count it, and
    /// it is what separates "we have not asked yet" from "custody answered
    /// Pending", which the same code for both could not.
    /// </summary>
    /// <remarks>
    /// It is also the only state <see cref="TryClaimHandOverAsync"/> will move
    /// out of, which is what makes exactly one caller the one that offers the
    /// bytes. <see cref="FindAsync"/> still reports the row - as Unknown,
    /// because that is what is known about it - so a caller that loses the
    /// claim can see the arrival it must reconcile instead of a null it would
    /// hand over against.
    /// </remarks>
    internal const string ArrivedCode = "arrived";

    internal const string PendingCode = "pending";
    internal const string ConfirmedCode = "confirmed";
    internal const string FailedCode = "failed";
    internal const string UnknownCode = "unknown";

    public static string ScopeOperationKey(Guid requestUploadLinkId, string operationKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);
        return $"{ScopePrefix(requestUploadLinkId)}{operationKey.Trim()}";
    }

    /// <summary>
    /// The sender's own operation key back out of a stored one. The page
    /// re-presents that key, not the scoped form it is stored under, because
    /// the sender is what has to send it again.
    /// </summary>
    public static string? UnscopeOperationKey(Guid requestUploadLinkId, string scopedOperationKey)
    {
        var prefix = ScopePrefix(requestUploadLinkId);
        return scopedOperationKey.StartsWith(prefix, StringComparison.Ordinal)
            ? scopedOperationKey[prefix.Length..]
            : null;
    }

    private static string ScopePrefix(Guid requestUploadLinkId) =>
        $"request:{requestUploadLinkId:N}:";

    /// <summary>
    /// The states an arrival can still resolve into something else from. While
    /// one of these stands for a link, the sender's original operation key is
    /// re-presented rather than replaced, so a retry reconciles that arrival
    /// instead of becoming a second one.
    /// </summary>
    internal static readonly string[] UnresolvedCodes = [ArrivedCode, UnknownCode, PendingCode];

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
                // What this arrival was committed with. A retry under the same
                // key re-offers these exact bytes or is refused.
                occurrence.Sha256,
                occurrence.Size,
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
        if (row is null)
        {
            return null;
        }

        // Every committed row is reported, the not-yet-offered arrival
        // included: it reads as Unknown because that is the whole of what is
        // known about it. Reporting it as no row at all is what let two
        // callers of one operation key both reach custody with the bytes.
        return new(
            row.Id,
            row.OperationKey,
            ParseCustodyState(row.CustodyState),
            row.CaseId,
            row.DocumentId,
            row.DocumentVersionId,
            row.Remote?.BoxFileId,
            row.Remote?.BoxVersionId,
            Sha256: row.Sha256,
            ContentLength: row.Size);
    }

    /// <summary>
    /// Moves one committed arrival out of its pre-custody state, and only out
    /// of that state, in a single conditional update. The row count is the
    /// whole of the decision: exactly one of any number of simultaneous
    /// callers sees 1, and that caller alone offers the bytes.
    /// </summary>
    /// <remarks>
    /// The claim lands as Unknown because that is precisely what becomes true
    /// the moment it is taken: custody may hold this. No new state word, table
    /// or worker is needed to say it. The update commits on its own, before
    /// the possibly accepting call, so a crash or a failure to record the
    /// result leaves a claimed arrival to reconcile rather than an arrival
    /// anyone may offer again.
    /// </remarks>
    public async Task<bool> TryClaimHandOverAsync(
        Guid occurrenceId,
        CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var claimed = await context.Set<PublicUploadOccurrenceEntity>()
            .Where(item => item.Id == occurrenceId && item.CustodyState == ArrivedCode)
            .ExecuteUpdateAsync(
                update => update.SetProperty(item => item.CustodyState, UnknownCode),
                cancellationToken);
        return claimed == 1;
    }

    /// <summary>
    /// The states each custody answer may be recorded <em>from</em>: the
    /// forward-only rule of
    /// <see cref="IncomingArtifactCustodyProgress.MovesForward"/>, expressed
    /// as the set a conditional update names in its WHERE clause.
    /// </summary>
    /// <remarks>
    /// Derived from that rule rather than restated beside it, so there is
    /// still one place the rule lives. <see cref="ArrivedCode"/> is in every
    /// set because custody has said nothing about an arrival, so any answer it
    /// gives is the first one. Two things follow from what is <em>not</em>
    /// here: nothing transitions to arrived - it is the state a row is created
    /// in and only the claim leaves it - and nothing transitions out of
    /// confirmed or failed, because both are answers custody has given.
    /// </remarks>
    private static readonly Dictionary<IncomingArtifactCustodyState, string[]> ForwardSourceCodes =
        Enum.GetValues<IncomingArtifactCustodyState>()
            .ToDictionary(target => target, ForwardSourceCodesOf);

    private static string[] ForwardSourceCodesOf(IncomingArtifactCustodyState target) =>
        [ArrivedCode, .. Enum.GetValues<IncomingArtifactCustodyState>()
            .Where(source => IncomingArtifactCustodyProgress.MovesForward(source, target))
            .Select(ToCode)];

    public async Task RecordAsync(
        RetainedIncomingArtifact artifact,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var occurrences = context.Set<PublicUploadOccurrenceEntity>();
        var target = ToCode(artifact.State);
        var sources = ForwardSourceCodes[artifact.State];
        var documentId = artifact.DocumentId;
        var documentVersionId = artifact.DocumentVersionId;

        // Forward only, and decided by the database rather than by a state
        // this process read a moment ago. One conditional statement names the
        // states this answer may be recorded from, so a recorder that knows
        // less than the row does cannot pass a test in memory and then win the
        // write: a late Pending or Unknown never pulls a Confirmed back, a
        // Failed never displaces a Confirmed, and a claim loser reconciling
        // while the winner is still inside custody cannot undo what the winner
        // committed. Rows affected is the whole of the answer - one is the
        // transition, none is a row already at or past this answer, which is a
        // no-op and not a failure. Identities move in the same statement,
        // because they are only ever filled where they are missing.
        var moved = await occurrences
            .Where(item => item.Id == artifact.OccurrenceId
                && sources.Contains(item.CustodyState))
            .ExecuteUpdateAsync(
                update => update
                    .SetProperty(item => item.CustodyState, target)
                    .SetProperty(item => item.DocumentId, item => item.DocumentId ?? documentId)
                    .SetProperty(
                        item => item.DocumentVersionId,
                        item => item.DocumentVersionId ?? documentVersionId),
                cancellationToken);
        if (moved == 0 && (documentId is not null || documentVersionId is not null))
        {
            // The answer stands, but identities this recorder recovered are
            // still true of it - the lost response asked custody by its
            // operation key and learned which document it holds. Filling a
            // null column is monotonic whatever the state, and this statement
            // cannot touch the state, so learning them is not a reason to
            // reopen a transition the row has refused.
            await occurrences
                .Where(item => item.Id == artifact.OccurrenceId
                    && (item.DocumentId == null || item.DocumentVersionId == null))
                .ExecuteUpdateAsync(
                    update => update
                        .SetProperty(item => item.DocumentId, item => item.DocumentId ?? documentId)
                        .SetProperty(
                            item => item.DocumentVersionId,
                            item => item.DocumentVersionId ?? documentVersionId),
                    cancellationToken);
        }

        // What the row says now, which is what decides the rest. A conditional
        // write reports how many rows it changed and not why, so the state a
        // remote identity may be written against is read back rather than
        // assumed, and a missing occurrence - a caller recording against
        // something that was never committed - is still the error it was.
        var recorded = await occurrences
            .AsNoTracking()
            .Where(item => item.Id == artifact.OccurrenceId)
            .Select(item => new { item.CustodyState, item.DocumentVersionId })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Public upload occurrence '{artifact.OccurrenceId}' was not found.");

        // Only a confirmed retention says anything about where custody holds
        // the bytes, so only a confirmed retention writes the remote
        // identities — and it never writes null. A version can back more than
        // one occurrence (two arrivals of the same file are two occurrences),
        // so clearing on a later Pending or Failed record would erase an
        // earlier confirmed identity that is still true. The row's own state,
        // not this recorder's, is what permits the write: a Pending that was
        // refused the transition must not land a Box identity either.
        var boxFileId = artifact.BoxFileId;
        var boxVersionId = artifact.BoxVersionId;
        if (artifact.State == IncomingArtifactCustodyState.Confirmed
            && string.Equals(recorded.CustodyState, ConfirmedCode, StringComparison.Ordinal)
            && recorded.DocumentVersionId is { } versionId
            && (boxFileId is not null || boxVersionId is not null))
        {
            await context.Set<DocumentVersionEntity>()
                .Where(item => item.Id == versionId)
                .ExecuteUpdateAsync(
                    update => update
                        .SetProperty(item => item.BoxFileId, item => boxFileId ?? item.BoxFileId)
                        .SetProperty(
                            item => item.BoxVersionId,
                            item => boxVersionId ?? item.BoxVersionId),
                    cancellationToken);
        }
    }

    internal static string ToCode(IncomingArtifactCustodyState state) => state switch
    {
        IncomingArtifactCustodyState.Pending => PendingCode,
        IncomingArtifactCustodyState.Confirmed => ConfirmedCode,
        IncomingArtifactCustodyState.Failed => FailedCode,
        IncomingArtifactCustodyState.Unknown => UnknownCode,
        _ => throw new ArgumentOutOfRangeException(nameof(state))
    };

    internal static IncomingArtifactCustodyState ParseCustodyState(string value) => value switch
    {
        PendingCode => IncomingArtifactCustodyState.Pending,
        ConfirmedCode => IncomingArtifactCustodyState.Confirmed,
        FailedCode => IncomingArtifactCustodyState.Failed,
        UnknownCode => IncomingArtifactCustodyState.Unknown,
        // A committed arrival is an answer custody has not given, which is
        // what Unknown says. Reading it as anything more would claim custody
        // spoke; reading it as nothing at all would hide the row a losing
        // caller has to reconcile against.
        ArrivedCode => IncomingArtifactCustodyState.Unknown,
        // An unrecognised stored state is never read as success.
        _ => throw new InvalidOperationException(
            $"The retained occurrence custody state '{value}' is not recognized.")
    };
}
