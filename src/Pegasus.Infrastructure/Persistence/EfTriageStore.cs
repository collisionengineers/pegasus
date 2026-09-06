using System.Data;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Triage;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

public sealed class EfTriageStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider? timeProvider = null) : ITriageStore
{
    /// <summary>
    /// The single seeded <c>TriageSequences</c> row. The Triage reference
    /// sequence is global, so there is exactly one counter and it is never
    /// partitioned by principal, vehicle or year.
    /// </summary>
    private const int TriageSequenceRowId = 1;


    public async Task<TriageRecord> CreateAsync(
        CreateTriageFromIntakeRequest request,
        CancellationToken cancellationToken)
    {
        ValidateCreate(request);
        var operationKey = request.OperationKey.Trim();
        var actor = request.Actor;
        var vrm = request.NormalizedVehicleRegistration.Trim().ToUpperInvariant();
        var sourceChannel = ToCode(request.Origin.SourceIdentity.Channel);
        var sourceToken = request.Origin.SourceIdentity.ExternalReceiptToken.Trim();
        var sourceHash = request.Origin.SourceHash.ToLowerInvariant();
        var acceptedMatch = request.AcceptedMatchEvidence;
        var matcherKey = acceptedMatch.MatcherKey!.Trim();
        var matchSignal = acceptedMatch.Signal.Trim();
        var requestHash = Hash(
            $"create|{request.Origin.ReceiptId:N}|{sourceChannel}|{sourceToken}|{sourceHash}|{request.Origin.EvaluationRevisionId:N}|{vrm}|{acceptedMatch.Source}|{acceptedMatch.Strength}|{acceptedMatch.Finding}|{matcherKey}|{acceptedMatch.MatcherVersion}|{matchSignal}|{acceptedMatch.Detail.Trim()}|{actor.Kind}|{actor.SubjectId}");

        // The replay probe runs before the transaction, holding nothing: a
        // retry of a committed creation returns its original reference without
        // ever reaching the counter, so a replay can never consume a number.
        await using (var probeContext = await contextFactory.CreateDbContextAsync(cancellationToken))
        {
            var committed = await FindReplayAsync(probeContext, operationKey, cancellationToken);
            if (committed is not null)
            {
                EnsureReplay(committed, "triage_created", requestHash);
                return await MapReplayAsync(probeContext, committed, cancellationToken);
            }
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        // The counter is the FIRST lock this transaction takes, before any
        // read or write of a Triage row. Every creator therefore queues on the
        // one counter row while holding nothing else, so two creators can
        // never each hold Triage locks while waiting for the counter — which
        // is the cycle that deadlocked when the counter was taken last.
        var allocatedSequence = await AllocateSequenceAsync(context, cancellationToken);

        // Re-probed under the counter, because a creation with this operation
        // key may have committed between the probe above and this lock. The
        // number just taken is discarded with the transaction, so this costs
        // nothing.
        var replay = await FindReplayAsync(context, operationKey, cancellationToken);
        if (replay is not null)
        {
            EnsureReplay(replay, "triage_created", requestHash);
            return await MapReplayAsync(context, replay, cancellationToken);
        }

        var existing = await context.Triage.AsNoTracking().SingleOrDefaultAsync(
            item => item.OriginReceiptId == request.Origin.ReceiptId
                || (item.SourceChannel == sourceChannel && item.ExternalReceiptToken == sourceToken),
            cancellationToken);
        if (existing is not null)
        {
            if (existing.OriginReceiptId != request.Origin.ReceiptId
                || existing.SourceChannel != sourceChannel
                || existing.ExternalReceiptToken != sourceToken
                || !string.Equals(existing.SourceHash, sourceHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new IntakeSourceIdentityConflictException();
            }

            return Map(existing);
        }

        var receipt = await context.IntakeReceipts.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == request.Origin.ReceiptId,
            cancellationToken) ?? throw new InvalidOperationException("The originating intake receipt does not exist.");
        if (receipt.SourceChannel != sourceChannel
            || receipt.ExternalReceiptToken != sourceToken
            || !string.Equals(receipt.SourceHash, sourceHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new IntakeSourceIdentityConflictException();
        }

        var retainedAcceptedMatches = EfIntakeReceiptStore.DeserializeEvidence(receipt.EvidenceJson)
            .Where(evidence => evidence.Finding == IntakeEvidenceFinding.AcceptedTriageMatch)
            .ToArray();
        if (retainedAcceptedMatches.Length != 1
            || retainedAcceptedMatches[0] != acceptedMatch)
        {
            throw new InvalidOperationException(
                "The accepted Triage-match evidence is not retained uniquely on the originating intake receipt.");
        }

        var evaluationExists = await context.IntakeEvaluations.AsNoTracking().AnyAsync(
            item => item.Id == request.Origin.EvaluationRevisionId
                && item.ProcessedReceiptId == request.Origin.ReceiptId,
            cancellationToken);
        if (!evaluationExists)
        {
            throw new InvalidOperationException("The creating intake evaluation revision does not exist for the receipt.");
        }

        var now = UtcNow();
        var principalId = await ResolveEstablishedPrincipalAsync(
            context,
            request.Origin.ReceiptId,
            cancellationToken);
        var entity = new TriageEntity
        {
            Id = Guid.NewGuid(),
            Sequence = allocatedSequence,
            Reference = TriageReferenceFormat.Format(allocatedSequence),
            PrincipalId = principalId,
            OriginReceiptId = request.Origin.ReceiptId,
            SourceChannel = sourceChannel,
            ExternalReceiptToken = sourceToken,
            SourceHash = sourceHash,
            EvaluationRevisionId = request.Origin.EvaluationRevisionId,
            NormalizedVehicleRegistration = vrm,
            State = ToCode(TriageState.Open),
            CreatedAtUtc = now,
            CreationOperationKey = operationKey,
            Version = 0
        };
        context.Triage.Add(entity);
        AppendHistory(
            context,
            entity,
            "triage_created",
            actor,
            operationKey,
            $"Created from accepted Triage matcher {matcherKey} v{acceptedMatch.MatcherVersion} ({matchSignal})",
            requestHash,
            -1);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(entity);
    }

    /// <summary>
    /// Takes the next global Triage sequence from the one <c>TriageSequences</c>
    /// row.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This must be the first statement of the enclosing transaction. The row
    /// is read under an update lock held to commit, so it is the single point
    /// every creator serializes on; taking it while already holding Triage
    /// locks is what produced a deadlock cycle, and taking it first is what
    /// removes the cycle rather than merely making it rarer.
    /// </para>
    /// <para>
    /// The increment is pending until the caller saves, so a transaction that
    /// returns early or fails releases the number rather than burning it. A
    /// number lost to a committed-then-failed sequence of events simply leaves
    /// a gap: the counter only moves forward and a reference is never reused.
    /// The unique indexes on <c>Triage.Sequence</c> and <c>Triage.Reference</c>
    /// remain the backstop — a duplicate would surface as a violation, never
    /// as a silently reused reference.
    /// </para>
    /// </remarks>
    private static async Task<long> AllocateSequenceAsync(
        PegasusDbContext context,
        CancellationToken cancellationToken)
    {
        var sequences = context.Set<TriageSequenceEntity>();
        var sequence = context.Database.IsSqlServer()
            ? await sequences
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM [TriageSequences] WITH (UPDLOCK, HOLDLOCK)
                    WHERE [Id] = {TriageSequenceRowId}
                """)
                .SingleAsync(cancellationToken)
            : await sequences.SingleAsync(
                item => item.Id == TriageSequenceRowId,
                cancellationToken);
        return checked(++sequence.LastAllocatedSequence);
    }

    /// <summary>
    /// The principal the receipt already established, taken from the
    /// originating instruction draft's suggested principal code and accepted
    /// only when it resolves to exactly one active principal. Anything else —
    /// no draft, no code, an unknown code, a deactivated principal — leaves the
    /// Triage without one, which the operator sees as `Not known`. Nothing is
    /// inferred from the vehicle registration or from a later linked Case.
    /// </summary>
    private static async Task<Guid?> ResolveEstablishedPrincipalAsync(
        PegasusDbContext context,
        Guid originReceiptId,
        CancellationToken cancellationToken)
    {
        var suggestedCode = await context.InstructionDrafts.AsNoTracking()
            .Where(draft => draft.IntakeReceiptId == originReceiptId)
            .Select(draft => draft.SuggestedPrincipalCode)
            .SingleOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(suggestedCode))
        {
            return null;
        }

        var code = suggestedCode.Trim();
        var candidates = await context.Principals.AsNoTracking()
            .Where(principal => principal.Code == code && principal.IsActive)
            .Select(principal => principal.Id)
            .Take(2)
            .ToArrayAsync(cancellationToken);
        return candidates.Length == 1 ? candidates[0] : null;
    }

    public async Task<TriageRecord> AssignAsync(AssignTriageRequest request, CancellationToken cancellationToken)
    {
        ValidateMutation(request.TriageId, request.ExpectedVersion, request.Actor, request.OperationKey, request.Reason);
        if (request.AssigneeId == Guid.Empty)
        {
            throw new ArgumentException("A valid assignee is required.", nameof(request));
        }

        return await MutateAsync(
            request.TriageId,
            request.ExpectedVersion,
            request.Actor,
            request.OperationKey,
            request.Reason,
            "triage_assigned",
            Hash($"assign|{request.TriageId:N}|{request.ExpectedVersion}|{request.AssigneeId:N}|{request.Actor.Kind}|{request.Actor.SubjectId}|{request.Reason.Trim()}"),
            item =>
            {
                if (item.AssigneeId == request.AssigneeId)
                {
                    throw new InvalidOperationException("The Triage record is already assigned to that staff member.");
                }
                item.AssigneeId = request.AssigneeId;
            },
            cancellationToken);
    }

    public Task<TriageRecord> UnassignAsync(TriageMutationRequest request, CancellationToken cancellationToken) =>
        MutateStateNeutralAsync(request, "triage_unassigned", item => item.AssigneeId = null, cancellationToken);

    public Task<TriageRecord> RecordFindingAsync(
        RecordTriageFindingRequest request,
        CancellationToken cancellationToken) => RecordFindingAsync(request, false, cancellationToken);

    public Task<TriageRecord> SupersedeFindingAsync(
        RecordTriageFindingRequest request,
        CancellationToken cancellationToken) => RecordFindingAsync(request, true, cancellationToken);

    public Task<TriageOperationReplay?> ProbeRecordFindingReplayAsync(
        RecordTriageFindingRequest request,
        CancellationToken cancellationToken) =>
        ProbeFindingReplayAsync(request, superseding: false, cancellationToken);

    public Task<TriageOperationReplay?> ProbeSupersedeFindingReplayAsync(
        RecordTriageFindingRequest request,
        CancellationToken cancellationToken) =>
        ProbeFindingReplayAsync(request, superseding: true, cancellationToken);

    public async Task<TriageOperationReplay?> ProbeStateChangeReplayAsync(
        TriageMutationRequest request,
        TriageState targetState,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateMutation(
            request.TriageId,
            request.ExpectedVersion,
            request.Actor,
            request.OperationKey,
            request.Reason);
        ValidateState(targetState);
        return await ProbeReplayAsync(
            request.TriageId,
            request.OperationKey,
            StateEventType(targetState),
            StateRequestHash(request, targetState),
            cancellationToken);
    }

    public Task<TriageOperationReplay?> ProbeLinkResponseEvidenceReplayAsync(
        TriageResponseEvidenceLinkRequest request,
        CancellationToken cancellationToken)
    {
        ValidateResponseEvidenceMutation(request);
        return ProbeReplayAsync(
            request.TriageId,
            request.OperationKey,
            "triage_response_linked",
            LinkResponseRequestHash(request),
            cancellationToken);
    }

    public Task<TriageOperationReplay?> ProbeUnlinkResponseEvidenceReplayAsync(
        TriageResponseEvidenceUnlinkRequest request,
        CancellationToken cancellationToken)
    {
        ValidateResponseEvidenceMutation(request);
        return ProbeReplayAsync(
            request.TriageId,
            request.OperationKey,
            "triage_response_unlinked",
            UnlinkResponseRequestHash(request),
            cancellationToken);
    }


    public Task<TriageOperationReplay?> ProbeAddNoteReplayAsync(
        AddTriageNoteRequest request,
        CancellationToken cancellationToken)
    {
        TriageLifecycleRules.ValidateNote(request);
        return ProbeReplayAsync(
            request.TriageId,
            request.OperationKey,
            TriageNotes.EventType,
            NoteRequestHash(request),
            cancellationToken);
    }

    /// <summary>
    /// Appends one operator note as an entry in the same history every state
    /// change writes.
    /// </summary>
    /// <remarks>
    /// The note changes no state, so the entry carries the current state,
    /// assignee and case link forward unchanged; it still takes the next
    /// version, because the history's before/after versions are what make a
    /// retry recognisable and an entry's place in the sequence unambiguous.
    /// The note text is the entry's reason: there is no second note store and
    /// no editable note record.
    /// </remarks>
    public Task<TriageRecord> AddNoteAsync(
        AddTriageNoteRequest request,
        CancellationToken cancellationToken)
    {
        TriageLifecycleRules.ValidateNote(request);
        return MutateAsync(
            request.TriageId,
            request.ExpectedVersion,
            request.Actor,
            request.OperationKey,
            request.Note,
            TriageNotes.EventType,
            NoteRequestHash(request),
            static _ => { },
            cancellationToken);
    }

    private static string NoteRequestHash(AddTriageNoteRequest request) =>
        Hash($"note|{request.TriageId:N}|{request.ExpectedVersion}|{request.Actor.Kind}|{request.Actor.SubjectId}|{request.Note.Trim()}");

    public async Task LinkResponseEvidenceAsync(
        TriageResponseEvidenceLinkRequest request,
        CancellationToken cancellationToken)
    {
        ValidateResponseEvidenceMutation(request);
        var requestHash = LinkResponseRequestHash(request);
        var operationKey = request.OperationKey.Trim();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var replay = await FindReplayAsync(context, operationKey, cancellationToken);
        if (replay is not null)
        {
            EnsureReplay(replay, "triage_response_linked", requestHash, request.TriageId);
            return;
        }

        var triage = await LoadForMutationAsync(
            context,
            request.TriageId,
            request.ExpectedVersion,
            cancellationToken);
        var sent = await context.SentEmailEvidence
            .Include(item => item.Response)
            .SingleOrDefaultAsync(item => item.Id == request.SentEvidenceId, cancellationToken)
            ?? throw new InvalidOperationException("The Sent evidence does not exist.");
        if (sent.TriageId != triage.Id)
        {
            throw new InvalidOperationException(
                "The selected Sent evidence does not belong to this Triage.");
        }
        if (await context.TriageResponseEvidenceLinks.AnyAsync(
                item => item.TriageId == triage.Id,
                cancellationToken))
        {
            throw new TriageResponseEvidenceAlreadyLinkedException(triage.Id);
        }

        var outcome = await context.ApprovedSentPollOutcomes.SingleOrDefaultAsync(
            item => item.Id == request.PollOutcomeId,
            cancellationToken)
            ?? throw new InvalidOperationException("The approved Sent poll outcome does not exist.");
        var inReplyToIdentities = DeserializeInReplyToIdentities(outcome);
        if (!inReplyToIdentities.Contains(sent.MessageIdentity, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "The selected poll outcome is not an exact reply to the selected Triage Sent evidence.");
        }
        if (outcome.SentAtUtc < sent.SentAtUtc)
        {
            throw new InvalidOperationException(
                "Response evidence cannot predate the selected Sent evidence.");
        }

        if (sent.Response is { } retainedResponse)
        {
            EnsureRetainedResponseOutcome(
                outcome,
                retainedResponse,
                sent,
                inReplyToIdentities);
        }
        else
        {
            EnsureSelectableResponseOutcome(outcome, sent);
            if (await context.EmailResponseEvidence.AnyAsync(
                    item => item.MessageIdentity == outcome.InternetMessageIdentity,
                    cancellationToken))
            {
                throw new InvalidOperationException(
                    "The response message identity is already recorded by another operation.");
            }

            context.EmailResponseEvidence.Add(new()
            {
                Id = outcome.Id,
                SentEvidenceId = sent.Id,
                SentEvidence = sent,
                PollOutcomeId = outcome.Id,
                MailboxId = outcome.MailboxId,
                MailboxAddress = outcome.MailboxAddress,
                SentFolderIdentity = outcome.SentFolderIdentity!,
                ImmutableItemIdentity = outcome.ImmutableItemIdentity!,
                MessageIdentity = outcome.InternetMessageIdentity!,
                ConversationIdentity = outcome.ConversationIdentity!,
                ReplyChainIdentity = outcome.ReplyChainIdentity!,
                InReplyToIdentitiesJson = JsonSerializer.Serialize(inReplyToIdentities),
                SourceOccurrenceIdentity = outcome.SourceOccurrenceIdentity,
                SourceSha256 = outcome.SourceSha256,
                MimeSha256 = outcome.MimeSha256!,
                SentAtUtc = outcome.SentAtUtc!.Value,
                DiscoveredAtUtc = outcome.RecordedAtUtc,
                Actor = request.Actor.SubjectId,
                OperationKey = operationKey,
                RequestHash = requestHash
            });
            sent.Version++;
            outcome.RelatedEvidenceId = sent.Id;
            outcome.OutcomeKind = nameof(SentEvidencePollOutcomeKind.TriageResponseRecorded);
        }

        context.TriageResponseEvidenceLinks.Add(new()
        {
            TriageId = triage.Id,
            Triage = triage,
            SentEvidenceId = sent.Id,
            SentEvidence = sent,
            Actor = request.Actor.SubjectId,
            OperationKey = operationKey,
            Reason = request.Reason.Trim(),
            LinkedAtUtc = UtcNow()
        });
        AppendHistory(
            context,
            triage,
            "triage_response_linked",
            request.Actor,
            operationKey,
            request.Reason.Trim(),
            requestHash);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            await using var verification =
                await contextFactory.CreateDbContextAsync(CancellationToken.None);
            var committedReplay = await FindReplayAsync(
                verification,
                operationKey,
                CancellationToken.None);
            if (committedReplay is not null)
            {
                EnsureReplay(
                    committedReplay,
                    "triage_response_linked",
                    requestHash,
                    request.TriageId);
                return;
            }

            if (await verification.TriageResponseEvidenceLinks.AsNoTracking().AnyAsync(
                    item => item.TriageId == request.TriageId,
                    CancellationToken.None))
            {
                throw new TriageResponseEvidenceAlreadyLinkedException(
                    request.TriageId,
                    exception);
            }

            throw;
        }
    }

    public async Task UnlinkResponseEvidenceAsync(
        TriageResponseEvidenceUnlinkRequest request,
        CancellationToken cancellationToken)
    {
        ValidateResponseEvidenceMutation(request);
        var requestHash = UnlinkResponseRequestHash(request);
        var operationKey = request.OperationKey.Trim();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var replay = await FindReplayAsync(context, operationKey, cancellationToken);
        if (replay is not null)
        {
            EnsureReplay(replay, "triage_response_unlinked", requestHash, request.TriageId);
            return;
        }

        var triage = await LoadForMutationAsync(
            context,
            request.TriageId,
            request.ExpectedVersion,
            cancellationToken);
        var link = await context.TriageResponseEvidenceLinks.SingleOrDefaultAsync(
            item => item.TriageId == triage.Id && item.SentEvidenceId == request.SentEvidenceId,
            cancellationToken) ?? throw new InvalidOperationException("The response evidence is not linked.");
        context.TriageResponseEvidenceLinks.Remove(link);
        AppendHistory(
            context,
            triage,
            "triage_response_unlinked",
            request.Actor,
            operationKey,
            request.Reason.Trim(),
            requestHash);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public Task<TriageRecord> ChangeStateAsync(
        TriageMutationRequest request,
        TriageState targetState,
        CancellationToken cancellationToken)
    {
        ValidateState(targetState);
        return MutateAsync(
            request.TriageId,
            request.ExpectedVersion,
            request.Actor,
            request.OperationKey,
            request.Reason,
            StateEventType(targetState),
            StateRequestHash(request, targetState),
            item => item.State = ToCode(targetState),
            cancellationToken);
    }

    public Task LinkCaseAsync(TriageCaseLinkRequest request, CancellationToken cancellationToken) =>
        ChangeCaseLinkAsync(request, true, cancellationToken);

    public Task UnlinkCaseAsync(TriageCaseLinkRequest request, CancellationToken cancellationToken) =>
        ChangeCaseLinkAsync(request, false, cancellationToken);


    public async Task<TriageSummary?> GetByOriginReceiptAsync(
        Guid originReceiptId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var row = await TriageWithDraftQuery(context, item => item.OriginReceiptId == originReceiptId)
            .SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : ToSummary(row);
    }

    public async Task<IReadOnlyList<TriageSummary>> ListAsync(TriageState? state, CancellationToken cancellationToken)
    {
        if (state is not null && !Enum.IsDefined(state.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var stateCode = state is null ? null : ToCode(state.Value);
        var rows = await TriageWithDraftQuery(
            context,
            stateCode is null ? null : item => item.State == stateCode).ToListAsync(cancellationToken);
        // The same newest-first order the keyset page uses, so the two read
        // paths cannot disagree about what "the next row" is.
        return rows.OrderByDescending(row => row.Item.CreatedAtUtc)
            .ThenByDescending(row => row.Item.Sequence)
            .Select(ToSummary)
            .ToArray();
    }

    /// <summary>
    /// The keyset page. Both the order and the continuation bound are expressed
    /// in SQL over the <c>(State, CreatedAtUtc)</c> index rather than in memory,
    /// so a later page never materialises the rows before it and a Triage
    /// created between two requests never shifts a page boundary. One extra row
    /// is read to learn whether a next page exists without a second count.
    /// </summary>
    /// <remarks>
    /// The position the caller carries is the pair the order is defined by —
    /// the instant and the Triage identity — but the tie-break is applied on
    /// the row's allocation <c>Sequence</c>, which is unique, ordered and
    /// unambiguous in SQL, where <c>uniqueidentifier</c> ordering is not the
    /// ordering <see cref="Guid.CompareTo(Guid)"/> defines. Resolving the
    /// cursor's identity to its sequence costs one primary-key read per
    /// continuation page and rejects a cursor naming a Triage that is not
    /// there.
    /// </remarks>
    public async Task<TriageListSlice> ListPageAsync(
        TriageState? state,
        TriageListPosition? after,
        int limit,
        CancellationToken cancellationToken)
    {
        if (state is not null && !Enum.IsDefined(state.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        if (after is { } position && position.Id == Guid.Empty)
        {
            throw new ArgumentException(
                "A keyset position requires a Triage identity.",
                nameof(after));
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var stateCode = state is null ? null : ToCode(state.Value);
        // The filter, the keyset bound, the order and the limit are all
        // expressed on the Triage entity itself, before the instruction-draft
        // join and the projection: EF cannot order by a member of a
        // constructed row type, and the database must do the paging anyway.
        var triage = context.Triage.AsNoTracking();
        if (stateCode is not null)
        {
            triage = triage.Where(item => item.State == stateCode);
        }
        if (after is { } cursor)
        {
            var afterSequence = await context.Triage.AsNoTracking()
                .Where(item => item.Id == cursor.Id)
                .Select(item => (long?)item.Sequence)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new CursorRejectedException(
                    "The cursor names a Triage that is no longer listed.");
            var afterCreatedAtUtc = cursor.CreatedAtUtc;
            // Strictly after the position in the newest-first order: an older
            // row, or the same instant with an earlier allocation.
            triage = triage.Where(item =>
                item.CreatedAtUtc < afterCreatedAtUtc
                || (item.CreatedAtUtc == afterCreatedAtUtc && item.Sequence < afterSequence));
        }

        var bounded = triage
            .OrderByDescending(item => item.CreatedAtUtc)
            .ThenByDescending(item => item.Sequence)
            .Take(limit + 1);
        var rows = await ProjectWithDraft(context, bounded).ToListAsync(cancellationToken);
        var hasMore = rows.Count > limit;
        // The join above may not preserve the bounded query's order, so the
        // page is put back in order here. It is at most `limit` rows and the
        // database has already chosen which ones they are.
        var page = rows
            .OrderByDescending(row => row.Item.CreatedAtUtc)
            .ThenByDescending(row => row.Item.Sequence)
            .Take(limit)
            .Select(ToSummary)
            .ToArray();
        var next = hasMore && page.Length > 0
            ? new TriageListPosition(page[^1].CreatedAtUtc, page[^1].Id)
            : null;
        return new(page, next);
    }

    /// <summary>
    /// The one query behind both Triage read paths: Triage left-joined with
    /// its originating <c>InstructionDraft</c> (a Triage need not carry one)
    /// so the row already carries the reference and provider the queue rows
    /// need — no per-row lookup. <paramref name="triagePredicate"/> filters
    /// the Triage side before the join/projection: EF Core cannot translate
    /// a filter applied after a <c>Select</c> into a named record type (only
    /// into an anonymous type), so filtering happens here, not by the caller
    /// composing <c>.Where</c> on the returned query.
    /// </summary>
    private static IQueryable<TriageWithDraftRow> TriageWithDraftQuery(
        PegasusDbContext context,
        Expression<Func<TriageEntity, bool>>? triagePredicate = null)
    {
        var triage = context.Triage.AsNoTracking();
        if (triagePredicate is not null)
        {
            triage = triage.Where(triagePredicate);
        }

        return ProjectWithDraft(context, triage);
    }

    /// <summary>
    /// The LEFT JOIN and projection alone, over whatever Triage query the
    /// caller has already filtered, ordered and bounded. Split out so the
    /// keyset page can do all of that on entity columns — EF cannot translate
    /// an order by a member of the constructed row.
    /// </summary>
    private static IQueryable<TriageWithDraftRow> ProjectWithDraft(
        PegasusDbContext context,
        IQueryable<TriageEntity> triage) =>
        from item in triage
        join draft in context.InstructionDrafts.AsNoTracking()
            on item.OriginReceiptId equals draft.IntakeReceiptId into drafts
        from draft in drafts.DefaultIfEmpty()
        select new TriageWithDraftRow(item, draft == null ? null : draft.ClaimNumber, draft == null ? null : draft.SuggestedPrincipalCode);

    private static TriageSummary ToSummary(TriageWithDraftRow row) => new(
        row.Item.Id,
        row.Item.NormalizedVehicleRegistration,
        ParseState(row.Item.State),
        row.Item.AssigneeId,
        row.Item.LinkedCaseId,
        row.Item.CreatedAtUtc,
        row.Item.Version,
        row.Item.Reference,
        row.Provider,
        row.ClaimNumber,
        row.Item.PrincipalId);

    private sealed record TriageWithDraftRow(TriageEntity Item, string? ClaimNumber, string? Provider);

    public async Task<TriageDetail?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A Triage identity is required.", nameof(id));
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        // The principal code comes out of this one read as a LEFT JOIN through
        // the foreign key rather than a follow-up lookup, so the detail read
        // stays a single round trip whether or not a principal is recorded.
        var row = await context.Triage.AsNoTracking()
            .Include(item => item.Findings)
            .Include(item => item.ResponseEvidenceLinks)
            .Include(item => item.History)
            .Where(item => item.Id == id)
            .Select(item => new
            {
                Item = item,
                PrincipalCode = context.Principals
                    .Where(principal => principal.Id == item.PrincipalId)
                    .Select(principal => principal.Code)
                    .FirstOrDefault()
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        var entity = row.Item;
        return new(
            Map(entity),
            entity.CreatedAtUtc,
            entity.Findings.OrderBy(item => item.RecordedAtUtc).ThenBy(item => item.Id).Select(Map).ToArray(),
            entity.ResponseEvidenceLinks.OrderBy(item => item.LinkedAtUtc).ThenBy(item => item.SentEvidenceId).Select(Map).ToArray(),
            entity.History.OrderBy(item => item.AfterVersion).ThenBy(item => item.Id).Select(Map).ToArray(),
            Array.Empty<TriageResponseEvidenceCandidate>(),
            row.PrincipalCode);
    }

    public async Task<IReadOnlyList<TriageSentEvidenceReference>> ListSentEvidenceReferencesAsync(
        Guid triageId,
        int maximumResults,
        CancellationToken cancellationToken)
    {
        if (triageId == Guid.Empty)
        {
            throw new ArgumentException("A Triage identity is required.", nameof(triageId));
        }
        if (maximumResults is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumResults),
                "Between one and 100 Sent-evidence references can be requested.");
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.SentEmailEvidence
            .AsNoTracking()
            .Where(item => item.TriageId == triageId)
            .OrderByDescending(item => item.SentAtUtc)
            .ThenBy(item => item.Id)
            .Take(maximumResults)
            .Select(item => new TriageSentEvidenceReference(item.Id, item.MessageIdentity))
            .ToArrayAsync(cancellationToken);
    }

    private async Task<TriageOperationReplay?> ProbeFindingReplayAsync(
        RecordTriageFindingRequest request,
        bool superseding,
        CancellationToken cancellationToken)
    {
        ValidateFindingMutation(request, superseding);
        var eventType = FindingEventType(superseding);
        return await ProbeReplayAsync(
            request.TriageId,
            request.OperationKey,
            eventType,
            FindingRequestHash(request, eventType),
            cancellationToken);
    }

    private async Task<TriageRecord> RecordFindingAsync(
        RecordTriageFindingRequest request,
        bool superseding,
        CancellationToken cancellationToken)
    {
        ValidateFindingMutation(request, superseding);
        var eventType = FindingEventType(superseding);
        var requestHash = FindingRequestHash(request, eventType);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var replay = await FindReplayAsync(context, request.OperationKey.Trim(), cancellationToken);
        if (replay is not null)
        {
            EnsureReplay(replay, eventType, requestHash, request.TriageId);
            return await MapReplayAsync(context, replay, cancellationToken);
        }

        var triage = await LoadForMutationAsync(
            context,
            request.TriageId,
            request.ExpectedVersion,
            cancellationToken);
        if (superseding)
        {
            var priorExists = await context.TriageFindings.AnyAsync(
                item => item.Id == request.SupersedesFindingId && item.TriageId == triage.Id,
                cancellationToken);
            var alreadySuperseded = await context.TriageFindings.AnyAsync(
                item => item.SupersedesFindingId == request.SupersedesFindingId,
                cancellationToken);
            if (!priorExists || alreadySuperseded)
            {
                throw new InvalidOperationException("The prior finding does not exist or has already been superseded.");
            }
        }
        if (superseding)
        {
            var responseLinks = await context.TriageResponseEvidenceLinks
                .Where(item => item.TriageId == triage.Id)
                .ToListAsync(cancellationToken);
            context.TriageResponseEvidenceLinks.RemoveRange(responseLinks);
        }

        context.TriageFindings.Add(new()
        {
            Id = Guid.NewGuid(),
            TriageId = triage.Id,
            Triage = triage,
            Roadworthiness = request.Roadworthiness is null ? null : ToCode(request.Roadworthiness.Value),
            Assessment = request.Assessment is null ? null : ToCode(request.Assessment.Value),
            SupersedesFindingId = request.SupersedesFindingId,
            Actor = request.Actor.SubjectId,
            OperationKey = request.OperationKey.Trim(),
            Reason = request.Reason.Trim(),
            RecordedAtUtc = UtcNow()
        });
        triage.State = ToCode(TriageState.FindingRecorded);
        AppendHistory(context, triage, eventType, request.Actor, request.OperationKey.Trim(), request.Reason.Trim(), requestHash);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(triage);
    }

    private async Task<TriageRecord> MutateStateNeutralAsync(
        TriageMutationRequest request,
        string eventType,
        Action<TriageEntity> mutation,
        CancellationToken cancellationToken)
    {
        ValidateMutation(request.TriageId, request.ExpectedVersion, request.Actor, request.OperationKey, request.Reason);
        return await MutateAsync(
            request.TriageId,
            request.ExpectedVersion,
            request.Actor,
            request.OperationKey,
            request.Reason,
            eventType,
            Hash($"{eventType}|{request.TriageId:N}|{request.ExpectedVersion}|{request.Actor.Kind}|{request.Actor.SubjectId}|{request.Reason.Trim()}"),
            mutation,
            cancellationToken);
    }

    private async Task<TriageRecord> MutateAsync(
        Guid triageId,
        long expectedVersion,
        ActionActor actor,
        string operationKey,
        string reason,
        string eventType,
        string requestHash,
        Action<TriageEntity> mutation,
        CancellationToken cancellationToken)
    {
        ValidateMutation(triageId, expectedVersion, actor, operationKey, reason);
        operationKey = operationKey.Trim();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var replay = await FindReplayAsync(context, operationKey, cancellationToken);
        if (replay is not null)
        {
            EnsureReplay(replay, eventType, requestHash, triageId);
            return await MapReplayAsync(context, replay, cancellationToken);
        }

        var triage = await LoadForMutationAsync(
            context,
            triageId,
            expectedVersion,
            cancellationToken);
        if (eventType == "triage_unassigned" && triage.AssigneeId is null)
        {
            throw new InvalidOperationException("The Triage record is not assigned.");
        }
        if (eventType == "triage_state_completed"
            && await context.TriageResponseEvidenceLinks.CountAsync(
                item => item.TriageId == triage.Id,
                cancellationToken) != 1)
        {
            throw new InvalidOperationException(
                "Triage completion requires exactly one replied Sent email evidence link.");
        }
        mutation(triage);
        AppendHistory(context, triage, eventType, actor, operationKey, reason.Trim(), requestHash);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(triage);
    }

    private async Task ChangeCaseLinkAsync(
        TriageCaseLinkRequest request,
        bool linking,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateMutation(
            request.TriageId,
            request.ExpectedTriageVersion,
            request.Actor,
            request.OperationKey,
            request.Reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.CaseEditLeaseToken);
        if (request.CaseId == Guid.Empty || request.ExpectedCaseVersion < 0)
        {
            throw new ArgumentException(
                "A valid case and expected case workflow version are required.",
                nameof(request));
        }

        var eventType = linking ? "triage_case_linked" : "triage_case_unlinked";
        var actorRolesJson = JsonSerializer.Serialize(request.Actor.Roles.OrderBy(role => role));
        var requestHash = Hash(
            $"{eventType}|{request.TriageId:N}|{request.ExpectedTriageVersion}|{request.CaseId:N}|{request.ExpectedCaseVersion}|{request.Actor.Kind}|{request.Actor.SubjectId}|{actorRolesJson}|{request.Reason.Trim()}");
        var operationKey = request.OperationKey.Trim();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var replay = await FindReplayAsync(context, operationKey, cancellationToken);
        if (replay is not null)
        {
            EnsureReplay(replay, eventType, requestHash, request.TriageId);
            return;
        }

        if (await context.CaseWorkflowEvents.AsNoTracking().AnyAsync(
                item => item.OperationKey == operationKey,
                cancellationToken)
            || await context.CaseHistory.AsNoTracking().AnyAsync(
                item => item.OperationKey == operationKey,
                cancellationToken))
        {
            throw new TriageOperationConflictException(request.TriageId, operationKey);
        }

        var triage = await LoadForMutationAsync(
            context,
            request.TriageId,
            request.ExpectedTriageVersion,
            cancellationToken);
        var workflow = await context.CaseWorkflows.SingleOrDefaultAsync(
            item => item.CaseId == request.CaseId,
            cancellationToken)
            ?? throw new InvalidOperationException("The case workflow does not exist.");
        var now = UtcNow();
        CaseMutationGuard.Require(
            workflow,
            request.Actor,
            request.ExpectedCaseVersion,
            request.CaseEditLeaseToken,
            now);

        if (linking)
        {
            if (triage.LinkedCaseId is not null)
            {
                throw new InvalidOperationException("The Triage record is already linked to a case.");
            }

            triage.LinkedCaseId = workflow.CaseId;
        }
        else
        {
            if (triage.LinkedCaseId != workflow.CaseId)
            {
                throw new InvalidOperationException(
                    "The Triage record is not linked to the specified case.");
            }

            triage.LinkedCaseId = null;
        }

        var beforeCaseVersion = workflow.Version;
        CaseMutationGuard.Complete(workflow);
        context.CaseWorkflowEvents.Add(new()
        {
            Id = Guid.NewGuid(),
            CaseId = workflow.CaseId,
            Workflow = workflow,
            EventType = eventType,
            OperationKey = operationKey,
            RequestHash = requestHash,
            ActorKind = request.Actor.Kind.ToString(),
            ActorSubjectId = request.Actor.SubjectId,
            ActorRolesJson = actorRolesJson,
            Reason = request.Reason.Trim(),
            OccurredAtUtc = now,
            BeforeVersion = beforeCaseVersion,
            AfterVersion = workflow.Version
        });
        AppendHistory(
            context,
            triage,
            eventType,
            request.Actor,
            operationKey,
            request.Reason.Trim(),
            requestHash);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<TriageEntity> LoadForMutationAsync(
        PegasusDbContext context,
        Guid id,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        var triage = await context.Triage.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Triage '{id}' does not exist.");
        EnsureVersion(triage, expectedVersion);
        return triage;
    }

    private async Task<TriageOperationReplay?> ProbeReplayAsync(
        Guid triageId,
        string operationKey,
        string eventType,
        string requestHash,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var replay = await FindReplayAsync(context, operationKey.Trim(), cancellationToken);
        if (replay is null)
        {
            return null;
        }

        EnsureReplay(replay, eventType, requestHash, triageId);
        return new(await MapReplayAsync(context, replay, cancellationToken));
    }

    private static string FindingEventType(bool superseding) =>
        superseding ? "triage_finding_superseded" : "triage_finding_recorded";

    private static string FindingRequestHash(
        RecordTriageFindingRequest request,
        string eventType) =>
        Hash($"{eventType}|{request.TriageId:N}|{request.ExpectedVersion}|{request.Roadworthiness}|{request.Assessment}|{request.SupersedesFindingId:N}|{request.Actor.Kind}|{request.Actor.SubjectId}|{request.Reason.Trim()}");

    private static string StateEventType(TriageState targetState) =>
        $"triage_state_{ToCode(targetState)}";

    private static string StateRequestHash(
        TriageMutationRequest request,
        TriageState targetState) =>
        Hash($"state|{request.TriageId:N}|{request.ExpectedVersion}|{ToCode(targetState)}|{request.Actor.Kind}|{request.Actor.SubjectId}|{request.Reason.Trim()}");

    private static Task<TriageHistoryEntity?> FindReplayAsync(
        PegasusDbContext context,
        string operationKey,
        CancellationToken cancellationToken) => context.TriageHistory.AsNoTracking().SingleOrDefaultAsync(
            item => item.OperationKey == operationKey,
            cancellationToken);

    private static void EnsureReplay(
        TriageHistoryEntity replay,
        string eventType,
        string requestHash,
        Guid? requestedTriageId = null)
    {
        if (replay.EventType != eventType
            || replay.RequestHash != requestHash
            || (requestedTriageId is not null && replay.TriageId != requestedTriageId))
        {
            throw new TriageOperationConflictException(replay.TriageId, replay.OperationKey);
        }
    }

    private static async Task<TriageRecord> MapReplayAsync(
        PegasusDbContext context,
        TriageHistoryEntity replay,
        CancellationToken cancellationToken)
    {
        var entity = await context.Triage.AsNoTracking().SingleAsync(item => item.Id == replay.TriageId, cancellationToken);
        return Map(entity) with
        {
            State = ParseState(replay.AfterState),
            AssigneeId = replay.AfterAssigneeId,
            LinkedCaseId = replay.AfterLinkedCaseId,
            Version = replay.AfterVersion
        };
    }

    private void AppendHistory(
        PegasusDbContext context,
        TriageEntity triage,
        string eventType,
        ActionActor actor,
        string operationKey,
        string reason,
        string requestHash,
        long? beforeVersion = null)
    {
        var before = beforeVersion ?? triage.Version;
        if (beforeVersion is null)
        {
            triage.Version++;
        }
        context.TriageHistory.Add(new()
        {
            Id = Guid.NewGuid(),
            TriageId = triage.Id,
            Triage = triage,
            EventType = eventType,
            Actor = actor.SubjectId,
            ActorKind = actor.Kind.ToString(),
            Reason = reason,
            OperationKey = operationKey,
            RequestHash = requestHash,
            OccurredAtUtc = UtcNow(),
            BeforeVersion = before,
            AfterVersion = triage.Version,
            AfterState = triage.State,
            AfterAssigneeId = triage.AssigneeId,
            AfterLinkedCaseId = triage.LinkedCaseId
        });
    }


    private static void EnsureVersion(TriageEntity triage, long expectedVersion)
    {
        if (triage.Version != expectedVersion)
        {
            throw new TriageVersionConflictException(triage.Id, expectedVersion, triage.Version);
        }
    }

    private static void ValidateCreate(CreateTriageFromIntakeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        TriageLifecycleRules.ValidateCreate(request);
        ArgumentNullException.ThrowIfNull(request.Origin);
        ArgumentNullException.ThrowIfNull(request.Origin.SourceIdentity);
        ValidateIdentityAndOperation(request.Origin.ReceiptId, request.Actor, request.OperationKey);
        if (request.Origin.EvaluationRevisionId == Guid.Empty)
        {
            throw new ArgumentException("A creating evaluation revision is required.", nameof(request));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Origin.SourceIdentity.ExternalReceiptToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.NormalizedVehicleRegistration);
        ValidateSha256(request.Origin.SourceHash, nameof(request));
        if (request.Origin.SourceIdentity.ExternalReceiptToken.Trim().Length > 200
            || request.NormalizedVehicleRegistration.Trim().Length > 20)
        {
            throw new ArgumentException("Triage origin or vehicle registration exceeds its storage limit.", nameof(request));
        }
    }

    private static void ValidateFindingMutation(
        RecordTriageFindingRequest request,
        bool superseding)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateMutation(
            request.TriageId,
            request.ExpectedVersion,
            request.Actor,
            request.OperationKey,
            request.Reason);
        if (request.Roadworthiness is null && request.Assessment is null)
        {
            throw new ArgumentException(
                "A Triage finding must record Roadworthiness, Assessment, or both.",
                nameof(request));
        }

        if ((request.Roadworthiness is { } roadworthiness && !Enum.IsDefined(roadworthiness))
            || (request.Assessment is { } assessment && !Enum.IsDefined(assessment)))
        {
            throw new ArgumentOutOfRangeException(nameof(request));
        }

        if (superseding != (request.SupersedesFindingId is not null))
        {
            throw new ArgumentException(
                "Finding supersession must identify exactly one prior finding.",
                nameof(request));
        }
    }

    private static void ValidateState(TriageState targetState)
    {
        if (!Enum.IsDefined(targetState))
        {
            throw new ArgumentOutOfRangeException(nameof(targetState));
        }
    }

    private static void ValidateResponseEvidenceMutation(
        TriageResponseEvidenceLinkRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateMutation(
            request.TriageId,
            request.ExpectedVersion,
            request.Actor,
            request.OperationKey,
            request.Reason);
        if (request.PollOutcomeId == Guid.Empty || request.SentEvidenceId == Guid.Empty)
        {
            throw new ArgumentException(
                "A valid poll outcome and Sent-evidence identity are required.",
                nameof(request));
        }
    }

    private static void ValidateResponseEvidenceMutation(
        TriageResponseEvidenceUnlinkRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateMutation(
            request.TriageId,
            request.ExpectedVersion,
            request.Actor,
            request.OperationKey,
            request.Reason);
        if (request.SentEvidenceId == Guid.Empty)
        {
            throw new ArgumentException(
                "A valid Sent-evidence identity is required.",
                nameof(request));
        }
    }

    private static string LinkResponseRequestHash(
        TriageResponseEvidenceLinkRequest request) =>
        Hash(
            $"link_response|{request.TriageId:N}|{request.ExpectedVersion}|{request.PollOutcomeId:N}|{request.SentEvidenceId:N}|{request.Actor.Kind}|{request.Actor.SubjectId}|{request.Reason.Trim()}");

    private static string UnlinkResponseRequestHash(
        TriageResponseEvidenceUnlinkRequest request) =>
        Hash(
            $"unlink_response|{request.TriageId:N}|{request.ExpectedVersion}|{request.SentEvidenceId:N}|{request.Actor.Kind}|{request.Actor.SubjectId}|{request.Reason.Trim()}");

    private static string[] DeserializeInReplyToIdentities(
        ApprovedSentPollOutcomeEntity outcome)
    {
        try
        {
            var identities = JsonSerializer.Deserialize<string[]>(
                outcome.InReplyToIdentitiesJson!);
            if (identities is null
                || identities.Length is < 1 or > 100
                || identities.Any(identity =>
                    string.IsNullOrWhiteSpace(identity)
                    || identity.Length > 500
                    || identity.Any(char.IsControl)))
            {
                throw new InvalidDataException(
                    "The selected poll outcome has invalid exact reply-chain identities.");
            }

            return identities;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "The selected poll outcome has invalid exact reply-chain identities.",
                exception);
        }
    }

    private static void EnsureSelectableResponseOutcome(
        ApprovedSentPollOutcomeEntity outcome,
        SentEmailEvidenceEntity sentEvidence)
    {
        if (outcome.RelatedEvidenceId is not null
            || outcome.ObservationKind != nameof(ApprovedSentItemObservationKind.Discovered)
            || outcome.OutcomeKind is not (
                nameof(SentEvidencePollOutcomeKind.Unmatched)
                or nameof(SentEvidencePollOutcomeKind.Ambiguous))
            || string.IsNullOrWhiteSpace(outcome.MailboxId)
            || string.IsNullOrWhiteSpace(outcome.MailboxAddress)
            || string.IsNullOrWhiteSpace(outcome.SentFolderIdentity)
            || string.IsNullOrWhiteSpace(outcome.ImmutableItemIdentity)
            || string.IsNullOrWhiteSpace(outcome.InternetMessageIdentity)
            || string.IsNullOrWhiteSpace(outcome.ConversationIdentity)
            || string.IsNullOrWhiteSpace(outcome.ReplyChainIdentity)
            || string.IsNullOrWhiteSpace(outcome.InReplyToIdentitiesJson)
            || string.IsNullOrWhiteSpace(outcome.SourceOccurrenceIdentity)
            || string.IsNullOrWhiteSpace(outcome.SourceSha256)
            || string.IsNullOrWhiteSpace(outcome.MimeSha256)
            || outcome.SentAtUtc is null)
        {
            throw new InvalidOperationException(
                "The selected poll outcome is not an unlinked exact-reply candidate.");
        }

        if (outcome.SentAtUtc < sentEvidence.SentAtUtc)
        {
            throw new InvalidOperationException(
                "The selected poll outcome predates the selected Sent evidence.");
        }
    }

    private static void EnsureRetainedResponseOutcome(
        ApprovedSentPollOutcomeEntity outcome,
        EmailResponseEvidenceEntity retainedResponse,
        SentEmailEvidenceEntity sentEvidence,
        IReadOnlyList<string> inReplyToIdentities)
    {
        string[] retainedInReplyToIdentities;
        try
        {
            retainedInReplyToIdentities =
                JsonSerializer.Deserialize<string[]>(retainedResponse.InReplyToIdentitiesJson)
                ?? throw new InvalidDataException(
                    "The retained response evidence has no exact reply-chain identities.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "The retained response evidence has invalid exact reply-chain identities.",
                exception);
        }

        if (outcome.Id != retainedResponse.PollOutcomeId
            || retainedResponse.Id != outcome.Id
            || retainedResponse.SentEvidenceId != sentEvidence.Id
            || outcome.RelatedEvidenceId != sentEvidence.Id
            || outcome.ObservationKind != nameof(ApprovedSentItemObservationKind.Discovered)
            || outcome.OutcomeKind != nameof(SentEvidencePollOutcomeKind.TriageResponseRecorded)
            || !string.Equals(
                outcome.MailboxId,
                retainedResponse.MailboxId,
                StringComparison.Ordinal)
            || !string.Equals(
                outcome.MailboxAddress,
                retainedResponse.MailboxAddress,
                StringComparison.OrdinalIgnoreCase)
            || !string.Equals(
                outcome.SentFolderIdentity,
                retainedResponse.SentFolderIdentity,
                StringComparison.Ordinal)
            || !string.Equals(
                outcome.ImmutableItemIdentity,
                retainedResponse.ImmutableItemIdentity,
                StringComparison.Ordinal)
            || !string.Equals(
                outcome.InternetMessageIdentity,
                retainedResponse.MessageIdentity,
                StringComparison.Ordinal)
            || !string.Equals(
                outcome.ConversationIdentity,
                retainedResponse.ConversationIdentity,
                StringComparison.Ordinal)
            || !string.Equals(
                outcome.ReplyChainIdentity,
                retainedResponse.ReplyChainIdentity,
                StringComparison.Ordinal)
            || !inReplyToIdentities.SequenceEqual(
                retainedInReplyToIdentities,
                StringComparer.Ordinal)
            || !string.Equals(
                outcome.SourceOccurrenceIdentity,
                retainedResponse.SourceOccurrenceIdentity,
                StringComparison.Ordinal)
            || !string.Equals(
                outcome.SourceSha256,
                retainedResponse.SourceSha256,
                StringComparison.OrdinalIgnoreCase)
            || !string.Equals(
                outcome.MimeSha256,
                retainedResponse.MimeSha256,
                StringComparison.OrdinalIgnoreCase)
            || outcome.SentAtUtc != retainedResponse.SentAtUtc
            || outcome.RecordedAtUtc != retainedResponse.DiscoveredAtUtc)
        {
            throw new InvalidOperationException(
                "The selected poll outcome does not identify the retained exact response evidence.");
        }
    }

    private static void ValidateMutation(
        Guid triageId,
        long expectedVersion,
        ActionActor actor,
        string operationKey,
        string reason)
    {
        ValidateIdentityAndOperation(triageId, actor, operationKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (expectedVersion < 0 || reason.Trim().Length > 500)
        {
            throw new ArgumentException("A valid expected version and bounded reason are required.");
        }
    }


    private static void ValidateIdentityAndOperation(Guid id, ActionActor actor, string operationKey)
    {
        ValidateIdentity(id, actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);
        if (operationKey.Trim().Length > 100)
        {
            throw new ArgumentException("The operation key cannot exceed 100 characters.", nameof(operationKey));
        }
    }

    private static void ValidateIdentity(Guid id, ActionActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (id == Guid.Empty || actor.SubjectId.Length > 200)
        {
            throw new ArgumentException("A valid identity and bounded actor are required.");
        }
    }


    private static void ValidateSha256(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length != 64 || value.Any(item => !char.IsAsciiHexDigit(item)))
        {
            throw new ArgumentException("A SHA-256 value must contain 64 hexadecimal characters.", parameterName);
        }
    }

    private DateTimeOffset UtcNow() => timeProvider?.GetUtcNow() ?? TimeProvider.System.GetUtcNow();

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static TriageRecord Map(TriageEntity entity) => new(
        entity.Id,
        new(
            entity.OriginReceiptId,
            new(ParseSourceChannel(entity.SourceChannel), entity.ExternalReceiptToken),
            entity.SourceHash,
            entity.EvaluationRevisionId),
        entity.NormalizedVehicleRegistration,
        ParseState(entity.State),
        entity.AssigneeId,
        entity.LinkedCaseId,
        entity.Version,
        entity.Reference,
        entity.PrincipalId);

    private static TriageFinding Map(TriageFindingEntity entity) => new(
        entity.Id,
        entity.TriageId,
        entity.Roadworthiness is null ? null : ParseRoadworthiness(entity.Roadworthiness),
        entity.Assessment is null ? null : ParseAssessment(entity.Assessment),
        entity.SupersedesFindingId,
        entity.Actor,
        entity.OperationKey,
        entity.Reason,
        entity.RecordedAtUtc);

    private static TriageResponseEvidenceLink Map(TriageResponseEvidenceLinkEntity entity) => new(
        entity.TriageId,
        entity.SentEvidenceId,
        entity.Actor,
        entity.OperationKey,
        entity.Reason,
        entity.LinkedAtUtc);

    private static TriageHistoryEntry Map(TriageHistoryEntity entity) => new(
        entity.Id,
        entity.TriageId,
        entity.EventType,
        entity.Actor,
        entity.ActorKind,
        entity.Reason,
        entity.OperationKey,
        entity.OccurredAtUtc,
        entity.BeforeVersion,
        entity.AfterVersion,
        ParseState(entity.AfterState),
        entity.AfterAssigneeId,
        entity.AfterLinkedCaseId);

    private static string ToCode(IntakeSourceChannel value) => value switch
    {
        IntakeSourceChannel.ManualUpload => "manual_upload",
        IntakeSourceChannel.Mailbox => "mailbox",
        IntakeSourceChannel.Automation => "automation",
        IntakeSourceChannel.ProviderApi => "provider_api",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static IntakeSourceChannel ParseSourceChannel(string value) => value switch
    {
        "manual_upload" => IntakeSourceChannel.ManualUpload,
        "mailbox" => IntakeSourceChannel.Mailbox,
        "automation" => IntakeSourceChannel.Automation,
        "provider_api" => IntakeSourceChannel.ProviderApi,
        _ => throw new InvalidDataException($"Unknown persisted intake source channel '{value}'.")
    };

    private static string ToCode(TriageState value) => value switch
    {
        TriageState.Open => "open",
        TriageState.AwaitingInformation => "awaiting_information",
        TriageState.FindingRecorded => "finding_recorded",
        TriageState.Completed => "completed",
        TriageState.Cancelled => "cancelled",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static TriageState ParseState(string value) => value switch
    {
        "open" => TriageState.Open,
        "awaiting_information" => TriageState.AwaitingInformation,
        "finding_recorded" => TriageState.FindingRecorded,
        "completed" => TriageState.Completed,
        "cancelled" => TriageState.Cancelled,
        _ => throw new InvalidDataException($"Unknown persisted Triage state '{value}'.")
    };

    private static string ToCode(RoadworthinessFinding value) => value switch
    {
        RoadworthinessFinding.Roadworthy => "roadworthy",
        RoadworthinessFinding.Unroadworthy => "unroadworthy",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static RoadworthinessFinding ParseRoadworthiness(string value) => value switch
    {
        "roadworthy" => RoadworthinessFinding.Roadworthy,
        "unroadworthy" => RoadworthinessFinding.Unroadworthy,
        _ => throw new InvalidDataException($"Unknown persisted Roadworthiness finding '{value}'.")
    };

    private static string ToCode(AssessmentFinding value) => value switch
    {
        AssessmentFinding.Repairable => "repairable",
        AssessmentFinding.TotalLoss => "total_loss",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static AssessmentFinding ParseAssessment(string value) => value switch
    {
        "repairable" => AssessmentFinding.Repairable,
        "total_loss" => AssessmentFinding.TotalLoss,
        _ => throw new InvalidDataException($"Unknown persisted Assessment finding '{value}'.")
    };
}
