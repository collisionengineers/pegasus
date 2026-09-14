using Microsoft.EntityFrameworkCore;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Core.Operations;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Administration › Logs › Intake log (13 September): one row per receipt with
/// what became of it, read from the receipt and the records it produced. The
/// receipt vocabulary stays what automation reads (Received file D6); this is
/// the one place its history is shown to a person, and only to an Administrator.
/// </summary>
/// <remarks>
/// The outcome is composed from the decision and from three later facts (a
/// Triage opened, an Unidentified item closed, processing failed), so the
/// outcome filter and the paging run over the composed rows in memory. The
/// candidate set is bounded to the newest <see cref="MaximumCandidates"/>
/// receipts that match the database-side filters, which is well past a log
/// page's reach; the row count is of that bounded set.
/// </remarks>
internal sealed class EfIntakeLogQueries(
    IDbContextFactory<PegasusDbContext> contextFactory,
    IIntakeReceiptQueries receipts,
    IVrmSuggestionStore vrmSuggestions) : IIntakeLogQueries
{
    private const int MaximumCandidates = 5000;

    public async Task<IntakeLogPage> ListAsync(
        IntakeLogFilter filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = context.IntakeReceipts.AsNoTracking();
        if (filter.SourceChannel is { } channel)
        {
            var code = EfIntakeReceiptStore.ToCode(channel);
            query = query.Where(item => item.SourceChannel == code);
        }

        if (filter.FromUtc is { } from)
        {
            query = query.Where(item => item.ReceivedAtUtc >= from);
        }

        if (filter.ToUtc is { } to)
        {
            query = query.Where(item => item.ReceivedAtUtc <= to);
        }

        if (filter.PrincipalCode is { } principalCode)
        {
            query = query.Where(item =>
                (item.InstructionDraft != null && item.InstructionDraft.SuggestedPrincipalCode == principalCode)
                || context.IntakeAllocationAttempts.Any(attempt =>
                    attempt.IntakeReceiptId == item.Id && attempt.PrincipalCode == principalCode));
        }

        if (filter.Text is { } text)
        {
            query = query.Where(item =>
                item.SourceFileName.Contains(text)
                || (item.InstructionDraft != null
                    && ((item.InstructionDraft.VehicleRegistration != null && item.InstructionDraft.VehicleRegistration.Contains(text))
                        || (item.InstructionDraft.ClaimNumber != null && item.InstructionDraft.ClaimNumber.Contains(text))))
                || context.IntakeAllocationAttempts.Any(attempt =>
                    attempt.IntakeReceiptId == item.Id && attempt.CaseReference != null && attempt.CaseReference.Contains(text))
                || context.RetainedMailboxMessages.Any(message =>
                    message.ExternalReceiptToken == item.ExternalReceiptToken
                    && ((message.SenderAddress != null && message.SenderAddress.Contains(text))
                        || (message.Subject != null && message.Subject.Contains(text)))));
        }

        var candidates = await Candidates(context, Newest(query))
            .ToListAsync(cancellationToken);

        var composed = candidates
            .Select(candidate => (Candidate: candidate, Outcome: ComposeOutcome(candidate)))
            .Where(entry => filter.Outcome is null || entry.Outcome == filter.Outcome)
            .ToArray();
        var pageEntries = composed.Skip((page - 1) * pageSize).Take(pageSize).ToArray();
        var rows = await MapRowsAsync(context, pageEntries, cancellationToken);
        return new IntakeLogPage(rows, page, pageSize, composed.Length);
    }

    /// <remarks>
    /// Failed intake is the number of receipts whose composed outcome is a
    /// retryable failure (<see cref="IntakeLogPolicy.IsRetryableFailure"/>) — the
    /// rows Operations lists — judged by the same composition as the list, over
    /// the same bounded newest candidates.
    /// </remarks>
    public async Task<IntakeLogCounts> GetCountsAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var candidates = await Candidates(context, Newest(context.IntakeReceipts.AsNoTracking()))
            .ToListAsync(cancellationToken);
        var pending = await context.IntakeWorkItems.AsNoTracking()
            .GroupBy(item => item.State)
            .Select(group => new { State = group.Key, OldestDueAtUtc = group.Min(item => item.DueAtUtc) })
            .ToListAsync(cancellationToken);
        return new IntakeLogCounts(
            candidates.Count(candidate => IntakeLogPolicy.IsRetryableFailure(ComposeOutcome(candidate))),
            pending.Where(item => EfIntakeWorkStore.ParseState(item.State) is not IntakeWorkState.Failed and not IntakeWorkState.Completed)
                .Select(item => (DateTimeOffset?)item.OldestDueAtUtc)
                .Min());
    }

    private static IQueryable<IntakeReceiptEntity> Newest(IQueryable<IntakeReceiptEntity> query) =>
        query
            .OrderByDescending(item => item.ReceivedAtUtc)
            .ThenByDescending(item => item.Id)
            .Take(MaximumCandidates);

    /// <summary>The one projection every read composes its outcome from.</summary>
    private static IQueryable<Candidate> Candidates(PegasusDbContext context, IQueryable<IntakeReceiptEntity> query) =>
        query.Select(item => new Candidate(
            item.Id,
            item.ReceivedAtUtc,
            item.SourceChannel,
            item.ExternalReceiptToken,
            item.SourceFileName,
            item.Decision,
            item.FailureReason ?? item.DecisionReason,
            item.InstructionDraft == null ? null : item.InstructionDraft.SuggestedPrincipalCode,
            item.Assets.Where(asset => asset.Kind == "source" && asset.Disposition == "source").Select(asset => (Guid?)asset.Id).FirstOrDefault(),
            context.Triage.Where(triage => triage.OriginReceiptId == item.Id).Select(triage => new Produced(triage.Id, triage.Reference)).FirstOrDefault(),
            context.Set<ImageIntakeEntity>().Where(intake => intake.OriginReceiptId == item.Id).Select(intake => new Produced(intake.Id, intake.ImageIntakeReference)).FirstOrDefault(),
            context.Set<UnidentifiedItemEntity>().Where(unidentified => unidentified.OriginId == item.Id)
                .Select(unidentified => new ProducedUnidentified(unidentified.Id, unidentified.Reference, unidentified.State, unidentified.ResolutionTargetKind)).FirstOrDefault(),
            context.IntakeWorkItems.Where(work => work.ProcessedReceiptId == item.Id).Select(work => work.AttemptCount).FirstOrDefault(),
            context.IntakeWorkItems.Any(work => work.ProcessedReceiptId == item.Id && work.State == "failed"),
            context.IntakeAllocationAttempts.Count(attempt => attempt.IntakeReceiptId == item.Id),
            context.IntakeAllocationAttempts.Where(attempt => attempt.IntakeReceiptId == item.Id)
                .OrderByDescending(attempt => attempt.AttemptNumber)
                .Select(attempt => attempt.Status)
                .FirstOrDefault(),
            context.IntakeAllocationAttempts.Where(attempt => attempt.IntakeReceiptId == item.Id)
                .OrderByDescending(attempt => attempt.AttemptNumber)
                .Select(attempt => attempt.RecoveryDisposition)
                .FirstOrDefault(),
            (from asset in context.Set<IntakeAssetEntity>()
             join operation in context.Set<IntakeOcrOperationEntity>() on asset.Id equals operation.IntakeAssetId
             join work in context.ExternalWorkItems on operation.Id equals work.Id
             where asset.IntakeReceiptId == item.Id
             orderby work.DueAtUtc descending, operation.Id
             // A Failed attempt already re-queued by a person reads as Pending
             // (EfIntakeOcrOperationStore.EffectiveState).
             select operation.State == nameof(IntakeOcrState.Failed) && work.State != ExternalWorkStatePersistence.Failed
                 ? nameof(IntakeOcrState.Pending)
                 : operation.State).FirstOrDefault()));

    public async Task<IntakeLogDetail?> GetAsync(Guid receiptId, CancellationToken cancellationToken)
    {
        var receipt = await receipts.GetAsync(receiptId, cancellationToken);
        if (receipt is null)
        {
            return null;
        }

        var page = await RowForAsync(receiptId, cancellationToken);
        if (page is null)
        {
            return null;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var attempts = (await context.IntakeAllocationAttempts.AsNoTracking()
                .Where(item => item.IntakeReceiptId == receiptId)
                .OrderBy(item => item.AttemptNumber)
                .ToListAsync(cancellationToken))
            .Select(item => IntakeAllocationState.FromAttempt(EfIntakeAllocationStore.Map(item)))
            .ToArray();
        var lastOcr = await EfIntakeOcrOperationStore.FindLastForReceiptAsync(context, receiptId, cancellationToken);
        var readings = ImageIntakeLifecycleRules.IsImageOnlyMaterial(receipt)
            ? await vrmSuggestions.ListForReceiptAsync(receiptId, cancellationToken)
            : [];
        return new IntakeLogDetail(
            page,
            receipt,
            readings,
            attempts,
            new IntakeLogActions(
                CanReevaluate: true,
                CanRetryAllocation: attempts.LastOrDefault()?.CanRetry == true,
                CanRetryOcr: IntakeOcrRetryPolicy.CanRetry(
                    lastOcr is { } ocr
                        ? EfIntakeOcrOperationStore.EffectiveState(ocr.Operation.State, ocr.WorkItem.State)
                        : null)));
    }

    private async Task<IntakeLogRow?> RowForAsync(Guid receiptId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var candidate = await Candidates(context, context.IntakeReceipts.AsNoTracking().Where(item => item.Id == receiptId))
            .SingleOrDefaultAsync(cancellationToken);
        if (candidate is null)
        {
            return null;
        }

        var outcome = ComposeOutcome(candidate);
        var rows = await MapRowsAsync(context, [(candidate, outcome)], cancellationToken);
        return rows.Single();
    }

    private static async Task<IReadOnlyList<IntakeLogRow>> MapRowsAsync(
        PegasusDbContext context,
        IReadOnlyList<(Candidate Candidate, IntakeLogOutcome Outcome)> entries,
        CancellationToken cancellationToken)
    {
        if (entries.Count == 0)
        {
            return [];
        }

        var ids = entries.Select(entry => entry.Candidate.Id).ToArray();
        var tokens = entries.Select(entry => entry.Candidate.ExternalReceiptToken).Distinct().ToArray();
        var messages = await context.RetainedMailboxMessages.AsNoTracking()
            .Where(item => tokens.Contains(item.ExternalReceiptToken))
            .Select(item => new { item.Id, item.ExternalReceiptToken, item.MailboxAddress, item.SenderAddress, item.Subject })
            .ToListAsync(cancellationToken);
        var messagesByToken = messages.GroupBy(item => item.ExternalReceiptToken).ToDictionary(group => group.Key, group => group.First());
        var uploaders = await context.Set<IntakeStagedReceiptEntity>().AsNoTracking()
            .Where(item => tokens.Contains(item.ExternalReceiptToken))
            .Select(item => new { item.ExternalReceiptToken, item.Actor })
            .ToListAsync(cancellationToken);
        var uploadersByToken = uploaders.GroupBy(item => item.ExternalReceiptToken).ToDictionary(group => group.Key, group => group.First().Actor);
        var associations = await CurrentIntakeAssociations.ReadAsync(context, ids, cancellationToken);
        var allocationCases = await context.IntakeAllocationAttempts.AsNoTracking()
            .Where(item => ids.Contains(item.IntakeReceiptId) && item.CaseId != null)
            .OrderByDescending(item => item.AttemptNumber)
            .Select(item => new { item.IntakeReceiptId, item.CaseId, item.CaseReference })
            .ToListAsync(cancellationToken);
        var allocationCaseByReceipt = allocationCases.GroupBy(item => item.IntakeReceiptId).ToDictionary(group => group.Key, group => group.First());
        var allocationPrincipals = await context.IntakeAllocationAttempts.AsNoTracking()
            .Where(item => ids.Contains(item.IntakeReceiptId))
            .OrderByDescending(item => item.AttemptNumber)
            .Select(item => new { item.IntakeReceiptId, item.PrincipalCode })
            .ToListAsync(cancellationToken);
        var principalByReceipt = allocationPrincipals.GroupBy(item => item.IntakeReceiptId).ToDictionary(group => group.Key, group => group.First().PrincipalCode);

        return entries.Select(entry =>
        {
            var candidate = entry.Candidate;
            var channel = EfIntakeReceiptStore.ParseSourceChannel(candidate.SourceChannel);
            messagesByToken.TryGetValue(candidate.ExternalReceiptToken, out var message);
            var principal = candidate.SuggestedPrincipalCode ?? principalByReceipt.GetValueOrDefault(candidate.Id);
            var source = channel switch
            {
                IntakeSourceChannel.Mailbox => new IntakeLogSource(channel, message?.MailboxAddress, message?.SenderAddress),
                IntakeSourceChannel.ManualUpload => new IntakeLogSource(channel, null, uploadersByToken.GetValueOrDefault(candidate.ExternalReceiptToken)),
                _ => new IntakeLogSource(channel, null, principal)
            };
            var linkedCase = associations.Current.GetValueOrDefault(candidate.Id);
            var allocatedCase = associations.AllocationMayStandIn(candidate.Id)
                ? allocationCaseByReceipt.GetValueOrDefault(candidate.Id)
                : null;
            IntakeLogBecame? became = entry.Outcome switch
            {
                IntakeLogOutcome.CaseCreated when linkedCase is not null =>
                    new(IntakeLogBecameKind.Case, linkedCase.CaseId, linkedCase.Reference),
                IntakeLogOutcome.CaseCreated when allocatedCase?.CaseId is { } caseId =>
                    new(IntakeLogBecameKind.Case, caseId, allocatedCase.CaseReference ?? caseId.ToString("D")),
                IntakeLogOutcome.Triage when candidate.Triage is { } triage =>
                    new(IntakeLogBecameKind.Triage, triage.Id, triage.Reference),
                IntakeLogOutcome.VehicleImages when candidate.ImageIntake is { } intake =>
                    new(IntakeLogBecameKind.ImageIntake, intake.Id, intake.Reference),
                _ when candidate.Unidentified is { } unidentified =>
                    new(IntakeLogBecameKind.Unidentified, unidentified.Id, unidentified.Reference),
                _ => null
            };
            return new IntakeLogRow(
                candidate.Id,
                candidate.ReceivedAtUtc,
                source,
                channel == IntakeSourceChannel.Mailbox && !string.IsNullOrWhiteSpace(message?.Subject)
                    ? message!.Subject!
                    : candidate.SourceFileName,
                candidate.SourceAssetId,
                entry.Outcome,
                entry.Outcome is IntakeLogOutcome.CouldNotBeRead or IntakeLogOutcome.ProcessingFailed or IntakeLogOutcome.Closed
                    ? candidate.Reason
                    : null,
                became,
                Math.Max(candidate.ProcessingAttempts, 1),
                candidate.AllocationAttempts)
            {
                MessageId = message?.Id
            };
        }).ToArray();
    }

    private sealed record Produced(Guid Id, string Reference);

    private sealed record ProducedUnidentified(Guid Id, string Reference, string State, string? ResolutionTargetKind);

    private sealed record Candidate(
        Guid Id,
        DateTimeOffset ReceivedAtUtc,
        string SourceChannel,
        string ExternalReceiptToken,
        string SourceFileName,
        string Decision,
        string Reason,
        string? SuggestedPrincipalCode,
        Guid? SourceAssetId,
        Produced? Triage,
        Produced? ImageIntake,
        ProducedUnidentified? Unidentified,
        int ProcessingAttempts,
        bool ProcessingFailed,
        int AllocationAttempts,
        string? LastAllocationStatus,
        string? LastAllocationDisposition,
        string? LastOcrState);

    /// <summary>
    /// The outcome through Core's one rule, with the two actionable failures
    /// judged by their own owners: allocation retry by
    /// <see cref="IntakeAllocationState.CanRetry"/>, OCR retry by
    /// <see cref="IntakeOcrRetryPolicy"/>.
    /// </summary>
    private static IntakeLogOutcome ComposeOutcome(Candidate candidate) =>
        IntakeLogPolicy.Outcome(
            EfIntakeReceiptStore.ParseDecision(candidate.Decision),
            candidate.Triage is not null,
            candidate.Unidentified is { State: nameof(UnidentifiedState.Resolved), ResolutionTargetKind: nameof(UnidentifiedResolutionTargetKind.Closed) },
            candidate.ProcessingFailed,
            allocationFailed: candidate.LastAllocationStatus == "failed"
                && new IntakeAllocationState(
                    Guid.Empty,
                    IntakeAllocationProjectionStatus.FailedRecoverable,
                    null,
                    candidate.ReceivedAtUtc,
                    RecoveryDisposition: candidate.LastAllocationDisposition is { } disposition
                        ? EfIntakeAllocationStore.ParseRecoveryDisposition(disposition)
                        : null).CanRetry,
            ocrFailed: IntakeOcrRetryPolicy.CanRetry(
                candidate.LastOcrState is { } state ? Enum.Parse<IntakeOcrState>(state) : null));
}
