using Pegasus.Core.Identity;

namespace Pegasus.Core.Tasks;

/// <summary>
/// One immutable due-work occurrence eligible for a generated, copyable chaser.
/// The optional request reference identifies an existing request-scoped upload link;
/// it never carries the protected token or URL.
/// </summary>
public sealed record DueCaseChaser(
    Guid CaseId,
    long DueWorkVersion,
    string CaseReference,
    string MissingMaterialReason,
    DateTimeOffset ScheduledAtUtc,
    Guid? RequestLinkReference);

/// <summary>
/// A locally persisted chaser draft. It is not evidence that correspondence was sent,
/// delivered, received, read, or answered.
/// </summary>
public sealed record GeneratedCaseChaser(
    Guid Id,
    Guid CaseId,
    DateTimeOffset ScheduledAtUtc,
    DateTimeOffset GeneratedAtUtc,
    DateTimeOffset NextChaseAtUtc,
    string CopyableText,
    Guid? RequestLinkReference,
    string? RequestLinkPurpose,
    long DueWorkVersion);

public sealed record DueChaserTransition(
    Guid Id,
    Guid CaseId,
    long ExpectedDueWorkVersion,
    DateTimeOffset ScheduledAtUtc,
    DateTimeOffset GeneratedAtUtc,
    DateTimeOffset NextChaseAtUtc,
    string CopyableText,
    Guid? RequestLinkReference,
    string? RequestLinkPurpose,
    string OperationKey,
    ActionActor Actor);

public enum DueChaserClaimOutcome
{
    Recorded,
    Replay,
    Superseded
}

public sealed record DueChaserClaimResult(
    DueChaserClaimOutcome Outcome,
    GeneratedCaseChaser? Chaser);

public sealed record RunDueChasersResult(
    int ExaminedCount,
    int GeneratedCount,
    int ReplayCount,
    int SupersededCount);

/// <summary>
/// Supplies bounded due-work snapshots and locally generated drafts. Implementations must
/// return only open Not-ready work and active request references owned by the same case.
/// </summary>
public interface ICaseDueChaserQueries
{
    Task<IReadOnlyList<DueCaseChaser>> GetDueAsync(
        DateTimeOffset asOfUtc,
        int maximumResults,
        CancellationToken cancellationToken);

    Task<GeneratedCaseChaser?> GetLatestAsync(
        Guid caseId,
        CancellationToken cancellationToken);
}

/// <summary>
/// Atomically claims one exact scheduled occurrence, advances its seven-calendar-day
/// cadence, persists the copyable draft, and appends permanent action history.
/// </summary>
public interface ICaseDueChaserStore
{
    Task<DueChaserClaimResult> TryClaimAndRecordAsync(
        DueChaserTransition transition,
        CancellationToken cancellationToken);
}

/// <summary>
/// Bounded Core background use case for generating due chaser drafts. It has no outbound
/// communication adapter and cannot assert sending or delivery.
/// </summary>
public sealed class RunDueChasers(
    ICaseDueChaserQueries queries,
    ICaseDueChaserStore store,
    TimeProvider timeProvider)
{
    public const int MaximumBatchSize = 500;
    public const string WorkerSubjectId = "due-work-sweep";
    public const string MissingMaterialRequestLinkPurpose = "missing-material-upload";

    private readonly ICaseDueChaserQueries _queries =
        queries ?? throw new ArgumentNullException(nameof(queries));
    private readonly ICaseDueChaserStore _store =
        store ?? throw new ArgumentNullException(nameof(store));
    private readonly TimeProvider _timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public async Task<RunDueChasersResult> ExecuteAsync(
        int maximumItems,
        CancellationToken cancellationToken = default)
    {
        if (maximumItems is < 1 or > MaximumBatchSize)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumItems));
        }

        var asOfUtc = _timeProvider.GetUtcNow().ToUniversalTime();
        var candidates = await _queries.GetDueAsync(
            asOfUtc,
            maximumItems,
            cancellationToken);
        if (candidates.Count > maximumItems)
        {
            throw new InvalidOperationException(
                "The due-chaser query returned more rows than the requested bound.");
        }

        var actor = ActionActor.SystemWorker(WorkerSubjectId);
        StaffAuthorization.Require(actor, StaffAccessRight.ExecuteSystemWork);
        var generated = 0;
        var replayed = 0;
        var superseded = 0;

        foreach (var candidate in candidates)
        {
            ValidateCandidate(candidate, asOfUtc);
            var nextChaseAtUtc = CaseChaseSchedule.NextChaseAt(candidate.ScheduledAtUtc);
            var transition = new DueChaserTransition(
                Guid.NewGuid(),
                candidate.CaseId,
                candidate.DueWorkVersion,
                candidate.ScheduledAtUtc.ToUniversalTime(),
                asOfUtc,
                nextChaseAtUtc,
                CreateCopyableText(candidate),
                candidate.RequestLinkReference,
                candidate.RequestLinkReference is null
                    ? null
                    : MissingMaterialRequestLinkPurpose,
                CreateOperationKey(candidate),
                actor);

            var result = await _store.TryClaimAndRecordAsync(
                transition,
                cancellationToken);
            switch (result.Outcome)
            {
                case DueChaserClaimOutcome.Recorded:
                    RequirePersistedChaser(result);
                    generated++;
                    break;
                case DueChaserClaimOutcome.Replay:
                    RequirePersistedChaser(result);
                    replayed++;
                    break;
                case DueChaserClaimOutcome.Superseded:
                    if (result.Chaser is not null)
                    {
                        throw new InvalidOperationException(
                            "A superseded due-chaser claim cannot return a persisted draft.");
                    }
                    superseded++;
                    break;
                default:
                    throw new InvalidOperationException("The due-chaser store returned an unknown outcome.");
            }
        }

        return new(candidates.Count, generated, replayed, superseded);
    }

    private static void ValidateCandidate(DueCaseChaser candidate, DateTimeOffset asOfUtc)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (candidate.CaseId == Guid.Empty)
        {
            throw new InvalidOperationException("A due-chaser candidate must identify its case.");
        }
        if (candidate.DueWorkVersion < 0)
        {
            throw new InvalidOperationException("A due-chaser candidate has an invalid version.");
        }
        if (string.IsNullOrWhiteSpace(candidate.CaseReference))
        {
            throw new InvalidOperationException("A due-chaser candidate must include its case reference.");
        }
        if (string.IsNullOrWhiteSpace(candidate.MissingMaterialReason))
        {
            throw new InvalidOperationException(
                "A due-chaser candidate must identify the outstanding material.");
        }
        if (candidate.ScheduledAtUtc > asOfUtc)
        {
            throw new InvalidOperationException("A due-chaser candidate cannot be future-dated.");
        }
        if (candidate.RequestLinkReference == Guid.Empty)
        {
            throw new InvalidOperationException("A request-link reference cannot be an empty identifier.");
        }
    }

    private static string CreateCopyableText(DueCaseChaser candidate) =>
        $"Please provide the outstanding material for case {candidate.CaseReference.Trim()}: " +
        $"{candidate.MissingMaterialReason.Trim()}.";

    private static string CreateOperationKey(DueCaseChaser candidate) =>
        $"due-chaser:{candidate.CaseId:N}:{candidate.ScheduledAtUtc.UtcDateTime.Ticks}";

    private static void RequirePersistedChaser(DueChaserClaimResult result)
    {
        if (result.Chaser is null)
        {
            throw new InvalidOperationException(
                "A recorded or replayed due-chaser claim must return its persisted draft.");
        }
    }
}
