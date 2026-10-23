namespace Pegasus.Core.Intake;

public sealed record ReplayCaseFilingsResult(
    int Candidates,
    int Filed,
    int Deferred,
    int Failures,
    int Exhausted);

/// <summary>
/// One receipt actively associated with a Case whose filing has not completed
/// there, as the store's page returns it.
/// </summary>
/// <param name="LinkedAtUtc">When the active association was made. With <paramref name="ReceiptId"/> it orders the pages.</param>
/// <param name="AssociationVersion">The association's version, which every unlink and relink advances; it keys this association's replay history.</param>
/// <param name="IntakeVersion">The receipt's version, recorded on its replay history.</param>
/// <param name="FailedAttempts">The <c>replay_failed</c> rows recorded for this receipt and Case since <paramref name="LinkedAtUtc"/>.</param>
/// <param name="LastFailedAtUtc">When the latest of those was recorded.</param>
/// <param name="PlannedOccurrences">How many files this receipt's filing plan for this Case names; zero without one.</param>
/// <param name="RecordedOccurrences">How many of those are already recorded on the Case.</param>
/// <param name="PendingOccurrences">How many of those still wait for custody.</param>
public sealed record CaseFilingReplayCandidate(
    Guid ReceiptId,
    Guid CaseId,
    DateTimeOffset LinkedAtUtc,
    long AssociationVersion,
    long IntakeVersion,
    int FailedAttempts,
    DateTimeOffset? LastFailedAtUtc,
    int PlannedOccurrences,
    int RecordedOccurrences,
    int PendingOccurrences);

/// <summary>The last candidate of the previous page; the next page starts strictly after it.</summary>
public sealed record CaseFilingReplayCursor(DateTimeOffset LinkedAtUtc, Guid ReceiptId);

public enum CaseFilingReplayEvent
{
    /// <summary>An attempt failed with a fault worth retrying; it is tried again after its delay.</summary>
    Failed,

    /// <summary>No further attempt is made for this association.</summary>
    Exhausted
}

/// <param name="Attempt">The attempt that failed, counted from one for this association.</param>
/// <param name="FailureCode">The failure code, recorded as the row's reason.</param>
public sealed record CaseFilingReplayRecord(
    CaseFilingReplayCandidate Candidate,
    CaseFilingReplayEvent Kind,
    int Attempt,
    string FailureCode,
    DateTimeOffset OccurredAtUtc)
{
    public string OperationKey => Kind == CaseFilingReplayEvent.Exhausted
        ? $"intake-replay-exhausted:{Candidate.ReceiptId:N}:{Candidate.CaseId:N}:{Candidate.AssociationVersion}"
        : $"intake-replay-failed:{Candidate.ReceiptId:N}:{Candidate.CaseId:N}:{Candidate.AssociationVersion}:{Attempt}";
}

/// <summary>The store <see cref="ReplayCaseFilings"/> pages and records through.</summary>
public interface ICaseFilingReplayStore
{
    /// <summary>
    /// One page, in one statement, of up to <paramref name="maximumItems"/>
    /// receipts strictly after <paramref name="after"/> in
    /// (<c>LinkedAtUtc</c>, receipt) order: each actively associated with a
    /// Case <see cref="CaseFilingTargetRule"/> answers Current for at
    /// <paramref name="nowUtc"/>, with a completed evaluation and a retained
    /// source the filer can file, not a discarded group's member, and with
    /// neither a completed filing on that Case nor a <c>replay_exhausted</c>
    /// row since the association was made. A pipeline association made before
    /// <paramref name="systemAssociationCutoff"/> is not listed (FRD-22); a
    /// member of staff's is listed until it files. Each candidate carries its
    /// plan's counts, read for the whole page at once. The store reads no
    /// clock of its own.
    /// </summary>
    /// <param name="systemAssociationCutoff">The start of the pipeline associations' replay window, from Core's clock.</param>
    /// <param name="nowUtc">The time the edit lease is judged at, the clock the filer's own check reads.</param>
    Task<IReadOnlyList<CaseFilingReplayCandidate>> ListDueAsync(
        CaseFilingReplayCursor? after,
        DateTimeOffset systemAssociationCutoff,
        DateTimeOffset nowUtc,
        int maximumItems,
        CancellationToken cancellationToken);

    /// <summary>Records one replay history row; a row already recorded under its operation key counts as recorded.</summary>
    Task RecordAsync(
        CaseFilingReplayRecord entry,
        CancellationToken cancellationToken);
}

/// <summary>
/// Files the retained evidence of a receipt that is associated with a Case
/// but whose filing did not complete in the receipt's own processing pass: a
/// filing that waited for a Case editor, or one that failed. It calls the
/// filer (<see cref="PromoteAssociatedIntakeCaseEvidence"/>) directly; the
/// receipt's other replay steps already ran (operator, 22 October 2026;
/// R367).
/// </summary>
/// <remarks>
/// A pipeline association is filed only inside the replay window the
/// grouped-image sweep already has, two hours from the association; after it
/// the mail stays No Case Found for a person, and a filing still waiting for
/// an editor then is not filed and leaves no row. A member of staff's
/// association keeps FRD-22's rule: it waits for a live edit lease and
/// retries (R189, R377).
/// <para>
/// Each attempt that fails with a fault worth retrying records a
/// <c>replay_failed</c> row and is tried again after the live filing path's
/// delay for that attempt (<see cref="ProcessQueuedIntake.RetryDelays"/>); the
/// fifth attempt, or any other fault at once, records <c>replay_exhausted</c>
/// and no further attempt is made for that association. The receipt's work
/// item is never written, so it stays completed and a later replay of the
/// receipt, a staff link's included, still runs (R190, R378). A set whose
/// planned files are all recorded with one still waiting for custody is
/// <c>ReconcilePendingArtifactCustody</c>'s.
/// </para>
/// </remarks>
public sealed class ReplayCaseFilings(
    ICaseFilingReplayStore store,
    IIntakeReceiptQueries receiptQueries,
    PromoteAssociatedIntakeCaseEvidence filer,
    TimeProvider timeProvider)
{
    /// <summary>The attempts one association gets: the live filing path's own bound (R190).</summary>
    public static int MaximumAttempts => ProcessQueuedIntake.RetryDelays.Length;

    /// <summary>How long after a pipeline association its filing is still replayed (R377).</summary>
    public static TimeSpan SystemAssociationWindow => ReconcileGroupedImageIntake.EscapeAfter;

    public async Task<ReplayCaseFilingsResult> ExecuteAsync(
        int maximumItems,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        var nowUtc = timeProvider.GetUtcNow();
        var due = await ListDueAsync(maximumItems, nowUtc, cancellationToken);

        var filed = 0;
        var deferred = 0;
        var failures = 0;
        var exhausted = 0;
        foreach (var candidate in due)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var attempt = candidate.FailedAttempts + 1;
            CaseFilingReplayRecord? failure = null;
            try
            {
                var receipt = await receiptQueries.GetAsync(candidate.ReceiptId, cancellationToken);
                if (receipt is null || receipt.CurrentCaseId != candidate.CaseId)
                {
                    // Unlinked or relinked since the page was read.
                    continue;
                }

                var outcome = await filer.ExecuteAsync(receipt, cancellationToken);
                if (outcome == AutomaticCaseEvidencePromotionOutcome.Deferred)
                {
                    deferred++;
                }
                else if (outcome == AutomaticCaseEvidencePromotionOutcome.Failed)
                {
                    // A stored terminal custody state: retrying cannot change it.
                    failure = new(candidate, CaseFilingReplayEvent.Exhausted, attempt, "case_custody_failed", nowUtc);
                }
                else if (outcome is AutomaticCaseEvidencePromotionOutcome.Pending
                    or AutomaticCaseEvidencePromotionOutcome.Confirmed)
                {
                    filed++;
                }

                // NotApplicable filed nothing, so it is not counted as filed.
            }
            catch (Exception exception) when (IntakeExceptionPolicy.IsTransientFailure(exception))
            {
                failure = new(
                    candidate,
                    attempt >= MaximumAttempts ? CaseFilingReplayEvent.Exhausted : CaseFilingReplayEvent.Failed,
                    attempt,
                    ProcessQueuedIntake.TransientFailureCode(exception),
                    nowUtc);
            }
            catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
            {
                failure = new(
                    candidate,
                    CaseFilingReplayEvent.Exhausted,
                    attempt,
                    TerminalReplayFailureCode(exception),
                    nowUtc);
            }

            if (failure is not null)
            {
                failures++;
                if (failure.Kind == CaseFilingReplayEvent.Exhausted)
                {
                    exhausted++;
                }

                await store.RecordAsync(failure, cancellationToken);
            }
        }

        return new(due.Count, filed, deferred, failures, exhausted);
    }

    /// <summary>
    /// The failure code a replay records for a fault that will not clear on
    /// retry. A known bad input keeps the live path's own code; anything else
    /// is the unexpected processing failure.
    /// </summary>
    internal static string TerminalReplayFailureCode(Exception exception) =>
        ProcessQueuedIntake.TerminalInputFailureCode(exception) ?? "unexpected_intake_processing_failure";

    /// <summary>
    /// Whether a listed candidate is attempted now: not a set custody
    /// reconciliation owns, and past the live path's delay after its last
    /// failed attempt.
    /// </summary>
    internal static bool IsDue(CaseFilingReplayCandidate candidate, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (candidate.PlannedOccurrences > 0
            && candidate.RecordedOccurrences == candidate.PlannedOccurrences
            && candidate.PendingOccurrences > 0)
        {
            return false;
        }

        if (candidate.FailedAttempts == 0 || candidate.LastFailedAtUtc is not { } lastFailedAtUtc)
        {
            return true;
        }

        var delays = ProcessQueuedIntake.RetryDelays;
        return nowUtc >= lastFailedAtUtc + delays[Math.Min(candidate.FailedAttempts, delays.Length) - 1];
    }

    /// <summary>
    /// Pages from the oldest association until <paramref name="maximumItems"/>
    /// candidates are due or the listing ends; one page is two statements at
    /// most whatever the backlog.
    /// </summary>
    private async Task<IReadOnlyList<CaseFilingReplayCandidate>> ListDueAsync(
        int maximumItems,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var due = new List<CaseFilingReplayCandidate>();
        var systemAssociationCutoff = nowUtc - SystemAssociationWindow;
        CaseFilingReplayCursor? cursor = null;
        while (true)
        {
            var page = await store.ListDueAsync(
                cursor, systemAssociationCutoff, nowUtc, maximumItems, cancellationToken);
            foreach (var candidate in page)
            {
                if (IsDue(candidate, nowUtc))
                {
                    due.Add(candidate);
                    if (due.Count == maximumItems)
                    {
                        return due;
                    }
                }
            }

            if (page.Count < maximumItems)
            {
                return due;
            }

            cursor = new(page[^1].LinkedAtUtc, page[^1].ReceiptId);
        }
    }
}
