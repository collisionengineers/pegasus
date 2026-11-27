using Pegasus.Core.Identity;

namespace Pegasus.Core.Operations;

/// <summary>
/// What the mail classifier did with one message of its batch in one run
/// (FRD-08). The six acts that change something come first; a dry run
/// records them as what it would have done.
/// </summary>
public enum ClassifierRunAction
{
    Classified,
    Linked,
    CaseCreated,
    OneOffCase,
    TriageOpened,
    QueryOpened,
    Left,
    LeftForStaff,
    Deferred,
    Failed,
    SetAside,
    NotReached
}

/// <summary>How a run ended, as the Administrator reads it; Running and Did not finish are read from a run with no close.</summary>
public enum ClassifierRunOutcome
{
    Running,
    DidNotFinish,
    Completed,
    NothingToDo,
    CandidatesHidden,
    CutShort,
    Failed
}

/// <summary>
/// Whose classifier a run belongs to. A deployment's own runs are its own
/// environment's; the hosted test instance also shows the live ones (operator,
/// 27 November 2026; R1318).
/// </summary>
public enum ClassifierRunEnvironment
{
    Live,
    Test
}

/// <summary>The settings a run started with, as the classifier reports them.</summary>
public sealed record ClassifierRunFacts(
    bool DryRun,
    string Model,
    int Batch,
    int ScanPages,
    decimal MaxBudgetUsd,
    string AgentVersion);

/// <summary>One message of the batch and what the run did with it.</summary>
public sealed record ClassifierRunItem(
    Guid MessageId,
    ClassifierRunAction Action,
    string? Category,
    Guid? CaseId,
    string? CaseReference,
    string? Reason,
    string? Refusal,
    DateTimeOffset? OccurredAtUtc);

/// <summary>How a run ended, as the classifier reports it at its close.</summary>
public sealed record ClassifierRunResult(
    string Outcome,
    int Eligible,
    int AwaitingDecision,
    bool CandidatesHidden,
    int HiddenRuns,
    int Verdicts,
    int Failed,
    int Deferred,
    int Turns,
    decimal CostUsd,
    int DurationMs,
    string? Error,
    string? Summary,
    IReadOnlyList<ClassifierRunItem> Items);

/// <param name="Result">Absent when the run opens; present when it closes.</param>
public sealed record RecordClassifierRunCommand(
    Guid RunId,
    ActionActor Actor,
    string OperationKey,
    ClassifierRunFacts Facts,
    ClassifierRunResult? Result);

public sealed record ClassifierRunRecorded(
    Guid RunId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? FinishedAtUtc,
    bool IsReplay);

/// <summary>A run as the store opens it: Pegasus's own clock is its start.</summary>
public sealed record ClassifierRunOpening(
    Guid RunId,
    ClassifierRunFacts Facts,
    string RecordedBy,
    DateTimeOffset AtUtc);

/// <summary>
/// A run as the store closes it, with what Core read from its result: how it
/// ended and how many of its messages it acted on.
/// </summary>
public sealed record ClassifierRunClosing(
    Guid RunId,
    ClassifierRunFacts Facts,
    string RecordedBy,
    DateTimeOffset AtUtc,
    ClassifierRunResult Result,
    ClassifierRunOutcome Outcome,
    int Acted);

/// <summary>
/// One run as the list draws it. <see cref="ClosedOutcome"/> is absent while
/// the run has no close; <see cref="ClassifierRunPolicy.OutcomeAt"/> reads it
/// as running or as not finished.
/// </summary>
public sealed record ClassifierRunSummary(
    Guid Id,
    ClassifierRunEnvironment Environment,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? FinishedAtUtc,
    bool DryRun,
    string? RawOutcome,
    ClassifierRunOutcome? ClosedOutcome,
    int? Eligible,
    int? AwaitingDecision,
    bool CandidatesHidden,
    int HiddenRuns,
    decimal? CostUsd,
    int? DurationMs,
    IReadOnlyDictionary<ClassifierRunAction, int> Actions);

/// <summary>One message row of a run's detail, with the message as Pegasus shows it where this deployment holds it.</summary>
public sealed record ClassifierRunItemView(
    ClassifierRunItem Item,
    string? Subject,
    string? Sender,
    bool MessageIsHere);

public sealed record ClassifierRunDetail(
    ClassifierRunSummary Summary,
    ClassifierRunFacts Facts,
    int? Verdicts,
    int? Failed,
    int? Deferred,
    int? Turns,
    string? Error,
    string? ModelSummary,
    string? RecordedBy,
    IReadOnlyList<ClassifierRunItemView> Items,
    bool ItemsCut);

/// <param name="Take">Rows to return; the caller asks for one more than it shows to know whether more follow.</param>
public sealed record ClassifierRunFilter(
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    ClassifierRunOutcome? Outcome,
    bool OnlyActed,
    bool OldestFirst,
    int Skip,
    int Take,
    DateTimeOffset NowUtc);

/// <summary>
/// The figures a deployment's health row is read from: the newest runs
/// (newest first, at most <see cref="ClassifierRunPolicy.RecentRuns"/>), the
/// runs started in the last 24 hours, and the sum of the recorded costs of
/// the runs started today and this month in London.
/// </summary>
public sealed record ClassifierRunHealthFacts(
    IReadOnlyList<ClassifierRunSummary> Recent,
    int RunsLast24Hours,
    decimal SpendToday,
    decimal SpendThisMonth);

/// <summary>A deployment's classifier at a glance.</summary>
public sealed record ClassifierRunHealth(
    ClassifierRunEnvironment Environment,
    ClassifierRunSummary? LastRun,
    ClassifierRunOutcome? LastOutcome,
    int RunsLast24Hours,
    int FailuresInARow,
    bool FailuresAtLeast,
    decimal SpendToday,
    decimal SpendThisMonth,
    int? HiddenAwaiting,
    int HiddenRuns);

/// <summary>
/// This deployment's runs. A summary or detail it returns names no
/// environment of its own: <see cref="ListClassifierRuns"/> gives it this
/// deployment's.
/// </summary>
public interface IClassifierRunStore
{
    /// <summary>Opens the run, or answers the run already opened under its id as a replay.</summary>
    Task<ClassifierRunRecorded> OpenAsync(ClassifierRunOpening opening, CancellationToken cancellationToken);

    /// <summary>
    /// Closes the run with its messages in one commit, opening it first when
    /// its open never arrived; a run already closed is answered as a replay
    /// and nothing is written.
    /// </summary>
    Task<ClassifierRunRecorded> CloseAsync(ClassifierRunClosing closing, CancellationToken cancellationToken);

    Task<IReadOnlyList<ClassifierRunSummary>> ListAsync(ClassifierRunFilter filter, CancellationToken cancellationToken);

    Task<ClassifierRunDetail?> GetAsync(Guid runId, CancellationToken cancellationToken);

    Task<ClassifierRunHealthFacts> HealthAsync(
        DateTimeOffset nowUtc,
        DateTimeOffset dayStartUtc,
        DateTimeOffset monthStartUtc,
        CancellationToken cancellationToken);
}

/// <summary>
/// The live classifier's runs as its own storage account keeps them, read by
/// the hosted test instance alone (ADR-0088 as amended; R1318, R1319). Read
/// when the page opens and never kept (R696).
/// </summary>
public interface IProductionClassifierRuns
{
    /// <summary>Every live run started in the period, without its messages.</summary>
    Task<IReadOnlyList<ClassifierRunSummary>> ListAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken);

    /// <summary>One live run with its messages, by its id and the UTC date it started on.</summary>
    Task<ClassifierRunDetail?> GetAsync(Guid runId, DateOnly startedOnUtc, CancellationToken cancellationToken);
}

/// <summary>Why the live runs could not be read: a refused access, or anything else.</summary>
public sealed class ProductionClassifierRunsUnavailableException(bool refused, string message, Exception? inner = null)
    : Exception(message, inner)
{
    public bool Refused { get; } = refused;
}

/// <summary>The rules a run's record is read by (FRD-08; FRD-17 Classifier).</summary>
public static class ClassifierRunPolicy
{
    public const int PageSize = 50;
    public const int MaximumPage = 200;
    public const int MaximumItems = 500;
    public const int RecentRuns = 100;
    public const int ModelLength = 100;
    public const int AgentVersionLength = 40;
    public const int OutcomeLength = 100;
    public const int ErrorLength = 1000;
    public const int SummaryLength = 4000;
    public const int CategoryLength = 60;
    public const int CaseReferenceLength = 40;
    public const int ReasonLength = 500;
    public const int RefusalLength = 1000;

    /// <summary>The longest period one page reads, beside the month its health row sums.</summary>
    public static readonly TimeSpan MaximumPeriod = TimeSpan.FromDays(31);

    /// <summary>
    /// A run with no close is running for this long after its start, and has
    /// not finished after it: the classifier ends a run at 12 minutes and its
    /// host stops it at 14.
    /// </summary>
    public static readonly TimeSpan RunningWindow = TimeSpan.FromMinutes(15);

    private static readonly IReadOnlyDictionary<string, ClassifierRunAction> WireActions =
        Enum.GetValues<ClassifierRunAction>().ToDictionary(WireName, action => action, StringComparer.Ordinal);

    /// <summary>
    /// The act's name as the classifier sends it and its storage account keeps
    /// it: the enum name in snake case (<c>case_created</c>).
    /// </summary>
    public static string WireName(ClassifierRunAction action)
    {
        var name = action.ToString();
        var wire = new System.Text.StringBuilder(name.Length + 4);
        for (var index = 0; index < name.Length; index++)
        {
            if (char.IsUpper(name[index]) && index > 0)
            {
                wire.Append('_');
            }

            wire.Append(char.ToLowerInvariant(name[index]));
        }

        return wire.ToString();
    }

    /// <summary>The act a wire name names, or none for a name the classifier does not send.</summary>
    public static ClassifierRunAction? ActionFromWire(string? name) =>
        name is not null && WireActions.TryGetValue(name.Trim(), out var action) ? action : null;

    /// <summary>The acts that change something in Pegasus, or would in a dry run.</summary>
    public static bool IsAct(ClassifierRunAction action) => action is ClassifierRunAction.Classified
        or ClassifierRunAction.Linked
        or ClassifierRunAction.CaseCreated
        or ClassifierRunAction.OneOffCase
        or ClassifierRunAction.TriageOpened
        or ClassifierRunAction.QueryOpened;

    /// <summary>
    /// How a closed run ended, from the outcome the classifier reports:
    /// <c>success</c> completed; <c>nothing eligible</c> had nothing to do;
    /// a list hiding mail is its own outcome; the turn and spending caps cut a
    /// run short; every other outcome, an unknown one included, is a failure.
    /// </summary>
    public static ClassifierRunOutcome OutcomeOf(string outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        return outcome.Trim() switch
        {
            "success" => ClassifierRunOutcome.Completed,
            "nothing eligible" => ClassifierRunOutcome.NothingToDo,
            "nothing eligible: candidates hidden" => ClassifierRunOutcome.CandidatesHidden,
            "error_max_turns" or "error_max_budget_usd" => ClassifierRunOutcome.CutShort,
            _ => ClassifierRunOutcome.Failed
        };
    }

    /// <summary>Whether a cut-short run met its spending cap rather than its turn cap.</summary>
    public static bool MetSpendingCap(string? outcome) =>
        string.Equals(outcome?.Trim(), "error_max_budget_usd", StringComparison.Ordinal);

    /// <summary>How a run reads at <paramref name="nowUtc"/>.</summary>
    public static ClassifierRunOutcome OutcomeAt(ClassifierRunSummary run, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (run.ClosedOutcome is { } closed)
        {
            return closed;
        }

        return nowUtc - run.StartedAtUtc < RunningWindow
            ? ClassifierRunOutcome.Running
            : ClassifierRunOutcome.DidNotFinish;
    }

    /// <summary>Whether a run matches the list's outcome and acted filters at <paramref name="nowUtc"/>.</summary>
    public static bool Matches(ClassifierRunSummary run, ClassifierRunOutcome? outcome, bool onlyActed, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(run);
        return (outcome is null || OutcomeAt(run, nowUtc) == outcome)
            && (!onlyActed || Acted(run) > 0);
    }

    /// <summary>The messages a run acted on.</summary>
    public static int Acted(ClassifierRunSummary run)
    {
        ArgumentNullException.ThrowIfNull(run);
        return run.Actions.Where(pair => IsAct(pair.Key)).Sum(pair => pair.Value);
    }

    /// <summary>
    /// A deployment's health row: its newest run and how it ended, its runs in
    /// the last 24 hours, its failures in a row (the newest finished runs that
    /// failed or did not finish, counted until one that did not; a run still
    /// running is passed over), its spend, and a list hiding mail when the
    /// newest finished run found one.
    /// </summary>
    public static ClassifierRunHealth Health(
        ClassifierRunEnvironment environment,
        ClassifierRunHealthFacts facts,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var last = facts.Recent.Count > 0 ? facts.Recent[0] : null;
        var failures = 0;
        var counted = 0;
        ClassifierRunSummary? lastFinished = null;
        foreach (var run in facts.Recent)
        {
            var outcome = OutcomeAt(run, nowUtc);
            if (outcome == ClassifierRunOutcome.Running)
            {
                continue;
            }

            lastFinished ??= run;
            counted++;
            if (outcome is not (ClassifierRunOutcome.Failed or ClassifierRunOutcome.DidNotFinish))
            {
                break;
            }

            failures++;
        }

        var hidden = lastFinished is { CandidatesHidden: true };
        return new(
            environment,
            last,
            last is null ? null : OutcomeAt(last, nowUtc),
            facts.RunsLast24Hours,
            failures,
            failures > 0 && failures == counted && facts.Recent.Count >= RecentRuns,
            facts.SpendToday,
            facts.SpendThisMonth,
            hidden ? lastFinished!.AwaitingDecision : null,
            hidden ? lastFinished!.HiddenRuns : 0);
    }

    /// <summary>The figures of a health row read from a list of runs, newest first: the live runs the instance reads.</summary>
    public static ClassifierRunHealthFacts FactsFrom(
        IReadOnlyList<ClassifierRunSummary> newestFirst,
        DateTimeOffset nowUtc,
        DateTimeOffset dayStartUtc,
        DateTimeOffset monthStartUtc)
    {
        ArgumentNullException.ThrowIfNull(newestFirst);
        return new(
            [.. newestFirst.Take(RecentRuns)],
            newestFirst.Count(run => run.StartedAtUtc >= nowUtc.AddHours(-24) && run.StartedAtUtc <= nowUtc),
            newestFirst.Where(run => run.StartedAtUtc >= dayStartUtc).Sum(run => run.CostUsd ?? 0m),
            newestFirst.Where(run => run.StartedAtUtc >= monthStartUtc).Sum(run => run.CostUsd ?? 0m));
    }

    /// <summary>The start of the London month <paramref name="instant"/> falls in.</summary>
    public static DateTimeOffset MonthStartAt(DateTimeOffset instant)
    {
        var date = LondonCalendar.DateAt(instant);
        return LondonCalendar.StartOfDay(new DateOnly(date.Year, date.Month, 1));
    }

    public static string? Cut(string? value, int length)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length <= length ? trimmed : trimmed[..length];
    }
}

/// <summary>
/// The classifier's record of its own runs (FRD-08; operator, 27 November
/// 2026; R1318): it opens a run as a sweep starts and closes it with how it
/// ended and one row per message of its batch. It is the classifier's own
/// record, so only the Automation Actor writes one; a member of staff signed
/// in from Pegasus Desktop is refused. Text past its column's length is cut,
/// not refused, so a long summary never loses the run.
/// </summary>
public sealed class RecordClassifierRun(IClassifierRunStore store, TimeProvider timeProvider)
{
    public Task<ClassifierRunRecorded> ExecuteAsync(
        RecordClassifierRunCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Actor);
        StaffAuthorization.Require(command.Actor, StaffAccessRight.PerformCasework);
        if (command.Actor.Kind != ActorKind.Automation)
        {
            throw new StaffAuthorizationException(StaffAccessRight.PerformCasework);
        }

        if (command.RunId == Guid.Empty)
        {
            throw new ArgumentException("A run identifier is required.", nameof(command));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(command.OperationKey);
        var facts = Facts(command.Facts);
        var now = timeProvider.GetUtcNow();
        if (command.Result is null)
        {
            return store.OpenAsync(new(command.RunId, facts, command.Actor.SubjectId, now), cancellationToken);
        }

        var result = Result(command.Result);
        return store.CloseAsync(
            new(
                command.RunId,
                facts,
                command.Actor.SubjectId,
                now,
                result,
                ClassifierRunPolicy.OutcomeOf(result.Outcome),
                result.Items.Count(item => ClassifierRunPolicy.IsAct(item.Action))),
            cancellationToken);
    }

    private static ClassifierRunFacts Facts(ClassifierRunFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (facts.Batch < 0 || facts.ScanPages < 0 || facts.MaxBudgetUsd < 0)
        {
            throw new ArgumentException("A run's batch, scan pages and budget are zero or more.", nameof(facts));
        }

        return facts with
        {
            Model = ClassifierRunPolicy.Cut(facts.Model, ClassifierRunPolicy.ModelLength)
                ?? throw new ArgumentException("A run's model is required.", nameof(facts)),
            AgentVersion = ClassifierRunPolicy.Cut(facts.AgentVersion, ClassifierRunPolicy.AgentVersionLength) ?? "unknown"
        };
    }

    private static ClassifierRunResult Result(ClassifierRunResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Eligible < 0 || result.AwaitingDecision < 0 || result.HiddenRuns < 0
            || result.Verdicts < 0 || result.Failed < 0 || result.Deferred < 0
            || result.Turns < 0 || result.CostUsd < 0 || result.DurationMs < 0)
        {
            throw new ArgumentException("A run's counts, cost and duration are zero or more.", nameof(result));
        }

        var items = result.Items ?? [];
        if (items.Count > ClassifierRunPolicy.MaximumItems)
        {
            throw new ArgumentException(
                $"A run records at most {ClassifierRunPolicy.MaximumItems} messages.", nameof(result));
        }

        return result with
        {
            Outcome = ClassifierRunPolicy.Cut(result.Outcome, ClassifierRunPolicy.OutcomeLength)
                ?? throw new ArgumentException("A run's outcome is required.", nameof(result)),
            Error = ClassifierRunPolicy.Cut(result.Error, ClassifierRunPolicy.ErrorLength),
            Summary = ClassifierRunPolicy.Cut(result.Summary, ClassifierRunPolicy.SummaryLength),
            Items = [.. items.Select(Item)]
        };
    }

    private static ClassifierRunItem Item(ClassifierRunItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.MessageId == Guid.Empty || !Enum.IsDefined(item.Action))
        {
            throw new ArgumentException("Each message names its identifier and a known act.", nameof(item));
        }

        return item with
        {
            Category = ClassifierRunPolicy.Cut(item.Category, ClassifierRunPolicy.CategoryLength),
            CaseId = item.CaseId == Guid.Empty ? null : item.CaseId,
            CaseReference = ClassifierRunPolicy.Cut(item.CaseReference, ClassifierRunPolicy.CaseReferenceLength),
            Reason = ClassifierRunPolicy.Cut(item.Reason, ClassifierRunPolicy.ReasonLength),
            Refusal = ClassifierRunPolicy.Cut(item.Refusal, ClassifierRunPolicy.RefusalLength)
        };
    }
}

/// <summary>Which of the two sources the page shows the live runs from on the test instance, and why it shows none.</summary>
public enum ClassifierLiveRunsState
{
    /// <summary>This deployment is the live one: its own runs are the live runs.</summary>
    ThisDeployment,
    Shown,
    Off,
    Refused,
    Unavailable
}

public sealed record ClassifierRunsQuery(
    ActionActor Actor,
    ClassifierRunEnvironment LocalEnvironment,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    ClassifierRunOutcome? Outcome,
    bool OnlyActed,
    ClassifierRunEnvironment? Environment,
    bool OldestFirst,
    int Page);

public sealed record ClassifierRunsView(
    IReadOnlyList<ClassifierRunHealth> Health,
    IReadOnlyList<ClassifierRunSummary> Runs,
    bool HasMore,
    ClassifierLiveRunsState LiveRuns);

/// <summary>
/// The Administrator's Classifier page (FRD-17; operator, 27 November 2026;
/// R1318, R1319): each deployment reads its own runs; the hosted test instance
/// also reads the live classifier's, from the live storage account, when it
/// has been given that read, and shows both with their environment. Live
/// runs are read when the page opens and never kept (R696). Production never
/// reads the instance (ADR-0014 OPS-11).
/// </summary>
public sealed class ListClassifierRuns(
    IClassifierRunStore store,
    TimeProvider timeProvider,
    IProductionClassifierRuns? liveRuns = null)
{
    public async Task<ClassifierRunsView> ExecuteAsync(ClassifierRunsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        StaffAuthorization.Require(query.Actor, StaffAccessRight.ManageStaffAccounts);
        if (query.FromUtc >= query.ToUtc || query.ToUtc - query.FromUtc > ClassifierRunPolicy.MaximumPeriod
            || query.Page is < 1 or > ClassifierRunPolicy.MaximumPage)
        {
            throw new ArgumentOutOfRangeException(nameof(query));
        }

        var now = timeProvider.GetUtcNow();
        var (dayStart, _, _) = LondonCalendar.DayAndWeekBoundariesAt(now);
        var monthStart = ClassifierRunPolicy.MonthStartAt(now);
        var local = query.LocalEnvironment;
        var isInstance = local == ClassifierRunEnvironment.Test;
        var showLocal = query.Environment is null || query.Environment == local;
        var showLive = isInstance && (query.Environment is null || query.Environment == ClassifierRunEnvironment.Live);

        var health = new List<ClassifierRunHealth>
        {
            ClassifierRunPolicy.Health(local, await store.HealthAsync(now, dayStart, monthStart, cancellationToken), now)
        };
        var live = isInstance ? ClassifierLiveRunsState.Off : ClassifierLiveRunsState.ThisDeployment;
        IReadOnlyList<ClassifierRunSummary> liveRows = [];
        if (isInstance && liveRuns is not null)
        {
            // One read serves the list's period and the health row's month and day.
            var from = Min(Min(query.FromUtc, monthStart), now.AddHours(-24));
            var to = query.ToUtc > now ? query.ToUtc : now;
            try
            {
                var read = (await liveRuns.ListAsync(from, to, cancellationToken))
                    .OrderByDescending(run => run.StartedAtUtc)
                    .ToArray();
                health.Add(ClassifierRunPolicy.Health(
                    ClassifierRunEnvironment.Live,
                    ClassifierRunPolicy.FactsFrom(read, now, dayStart, monthStart),
                    now));
                liveRows = read;
                live = ClassifierLiveRunsState.Shown;
            }
            catch (ProductionClassifierRunsUnavailableException exception)
            {
                live = exception.Refused ? ClassifierLiveRunsState.Refused : ClassifierLiveRunsState.Unavailable;
            }
        }

        var merging = showLive && live == ClassifierLiveRunsState.Shown;
        var skip = (query.Page - 1) * ClassifierRunPolicy.PageSize;
        IReadOnlyList<ClassifierRunSummary> localRows = [];
        if (showLocal)
        {
            // Alone, the store pages itself; beside the live runs it reads up
            // to the end of this page, and the two are paged together.
            var read = await store.ListAsync(
                new(
                    query.FromUtc,
                    query.ToUtc,
                    query.Outcome,
                    query.OnlyActed,
                    query.OldestFirst,
                    merging ? 0 : skip,
                    merging ? skip + ClassifierRunPolicy.PageSize + 1 : ClassifierRunPolicy.PageSize + 1,
                    now),
                cancellationToken);
            localRows = [.. read.Select(run => run with { Environment = local })];
            if (!merging)
            {
                return View(health, localRows, live);
            }
        }

        var liveMatches = merging
            ? liveRows.Where(run => run.StartedAtUtc >= query.FromUtc && run.StartedAtUtc < query.ToUtc
                && ClassifierRunPolicy.Matches(run, query.Outcome, query.OnlyActed, now))
            : Enumerable.Empty<ClassifierRunSummary>();
        var merged = localRows.Concat(liveMatches);
        var ordered = query.OldestFirst
            ? merged.OrderBy(run => run.StartedAtUtc).ThenBy(run => run.Id)
            : merged.OrderByDescending(run => run.StartedAtUtc).ThenByDescending(run => run.Id);
        return View(health, [.. ordered.Skip(skip).Take(ClassifierRunPolicy.PageSize + 1)], live);
    }

    /// <summary>One run with its messages; a live run is read only on the instance, by the date it started on.</summary>
    public async Task<ClassifierRunDetail?> GetAsync(
        ActionActor actor,
        ClassifierRunEnvironment localEnvironment,
        ClassifierRunEnvironment environment,
        Guid runId,
        DateOnly? startedOnUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.ManageStaffAccounts);
        if (runId == Guid.Empty)
        {
            return null;
        }

        if (environment == localEnvironment)
        {
            return await store.GetAsync(runId, cancellationToken) is { } detail
                ? detail with { Summary = detail.Summary with { Environment = localEnvironment } }
                : null;
        }

        return localEnvironment == ClassifierRunEnvironment.Test && liveRuns is not null && startedOnUtc is { } day
            ? await liveRuns.GetAsync(runId, day, cancellationToken)
            : null;
    }

    private static ClassifierRunsView View(
        IReadOnlyList<ClassifierRunHealth> health,
        IReadOnlyList<ClassifierRunSummary> rows,
        ClassifierLiveRunsState live) =>
        new(health, [.. rows.Take(ClassifierRunPolicy.PageSize)], rows.Count > ClassifierRunPolicy.PageSize, live);

    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right) => left < right ? left : right;
}
