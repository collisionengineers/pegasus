using System.Collections.Frozen;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Operations;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The classifier's runs in this deployment's database (FRD-08; R1318). A run
/// is opened once and closed once: a second open or close under the same id
/// is answered as a replay and writes nothing. A run's messages are written
/// with its close, in the same commit.
/// </summary>
internal sealed class EfClassifierRunStore(IDbContextFactory<PegasusDbContext> contextFactory) : IClassifierRunStore
{
    public async Task<ClassifierRunRecorded> OpenAsync(ClassifierRunOpening opening, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(opening);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await context.Set<ClassifierRunEntity>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == opening.RunId, cancellationToken);
        if (existing is not null)
        {
            return Recorded(existing, isReplay: true);
        }

        var entity = NewRun(opening.RunId, opening.Facts, opening.RecordedBy, opening.AtUtc);
        context.Set<ClassifierRunEntity>().Add(entity);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return Recorded(entity, isReplay: false);
        }
        catch (DbUpdateException exception) when (IsDuplicate(exception))
        {
            return Recorded(await ReadAsync(opening.RunId, cancellationToken), isReplay: true);
        }
    }

    public async Task<ClassifierRunRecorded> CloseAsync(ClassifierRunClosing closing, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(closing);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.Set<ClassifierRunEntity>()
            .SingleOrDefaultAsync(item => item.Id == closing.RunId, cancellationToken);
        if (entity is { FinishedAtUtc: not null })
        {
            return Recorded(entity, isReplay: true);
        }

        if (entity is null)
        {
            // The open never arrived: the run started when its close says it did.
            entity = NewRun(
                closing.RunId,
                closing.Facts,
                closing.RecordedBy,
                closing.AtUtc.AddMilliseconds(-closing.Result.DurationMs));
            context.Set<ClassifierRunEntity>().Add(entity);
        }

        var result = closing.Result;
        entity.FinishedAtUtc = closing.AtUtc;
        entity.Outcome = result.Outcome;
        entity.OutcomeKind = closing.Outcome.ToString();
        entity.Eligible = result.Eligible;
        entity.AwaitingDecision = result.AwaitingDecision;
        entity.CandidatesHidden = result.CandidatesHidden;
        entity.HiddenRuns = result.HiddenRuns;
        entity.Verdicts = result.Verdicts;
        entity.Failed = result.Failed;
        entity.Deferred = result.Deferred;
        entity.Acted = closing.Acted;
        entity.Turns = result.Turns;
        entity.CostUsd = result.CostUsd;
        entity.DurationMs = result.DurationMs;
        entity.Error = result.Error;
        entity.Summary = result.Summary;
        context.Set<ClassifierRunItemEntity>().AddRange(result.Items.Select((item, ordinal) => new ClassifierRunItemEntity
        {
            RunId = closing.RunId,
            Ordinal = ordinal,
            MessageId = item.MessageId,
            Action = item.Action.ToString(),
            Category = item.Category,
            CaseId = item.CaseId,
            CaseReference = item.CaseReference,
            Reason = item.Reason,
            Refusal = item.Refusal,
            OccurredAtUtc = item.OccurredAtUtc
        }));
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return Recorded(entity, isReplay: false);
        }
        catch (DbUpdateException exception) when (IsDuplicate(exception))
        {
            return Recorded(await ReadAsync(closing.RunId, cancellationToken), isReplay: true);
        }
    }

    public async Task<IReadOnlyList<ClassifierRunSummary>> ListAsync(
        ClassifierRunFilter filter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = context.Set<ClassifierRunEntity>().AsNoTracking()
            .Where(item => item.StartedAtUtc >= filter.FromUtc && item.StartedAtUtc < filter.ToUtc);
        var runningSince = filter.NowUtc - ClassifierRunPolicy.RunningWindow;
        query = filter.Outcome switch
        {
            null => query,
            ClassifierRunOutcome.Running => query.Where(item => item.FinishedAtUtc == null && item.StartedAtUtc >= runningSince),
            ClassifierRunOutcome.DidNotFinish => query.Where(item => item.FinishedAtUtc == null && item.StartedAtUtc < runningSince),
            { } outcome => query.Where(item => item.OutcomeKind == outcome.ToString())
        };
        if (filter.OnlyActed)
        {
            query = query.Where(item => item.Acted > 0);
        }

        var ordered = filter.OldestFirst
            ? query.OrderBy(item => item.StartedAtUtc).ThenBy(item => item.Id)
            : query.OrderByDescending(item => item.StartedAtUtc).ThenByDescending(item => item.Id);
        var runs = await ordered.Skip(filter.Skip).Take(filter.Take).ToArrayAsync(cancellationToken);
        if (runs.Length == 0)
        {
            return [];
        }

        var ids = runs.Select(run => run.Id).ToArray();
        var counts = (await context.Set<ClassifierRunItemEntity>().AsNoTracking()
                .Where(item => ids.Contains(item.RunId))
                .GroupBy(item => new { item.RunId, item.Action })
                .Select(group => new { group.Key.RunId, group.Key.Action, Count = group.Count() })
                .ToArrayAsync(cancellationToken))
            .GroupBy(row => row.RunId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyDictionary<ClassifierRunAction, int>)group
                    .Where(row => Enum.TryParse<ClassifierRunAction>(row.Action, out _))
                    .ToDictionary(row => Enum.Parse<ClassifierRunAction>(row.Action), row => row.Count));
        return [.. runs.Select(run => Summary(run, counts.GetValueOrDefault(run.Id) ?? NoActions))];
    }

    public async Task<ClassifierRunDetail?> GetAsync(Guid runId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var run = await context.Set<ClassifierRunEntity>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == runId, cancellationToken);
        if (run is null)
        {
            return null;
        }

        var items = await context.Set<ClassifierRunItemEntity>().AsNoTracking()
            .Where(item => item.RunId == runId)
            .OrderBy(item => item.Ordinal)
            .ToArrayAsync(cancellationToken);
        var messageIds = items.Select(item => item.MessageId).Distinct().ToArray();
        var messages = await context.RetainedMailboxMessages.AsNoTracking()
            .Where(message => messageIds.Contains(message.Id))
            .Select(message => new { message.Id, message.Subject, message.SenderDisplayName, message.SenderAddress })
            .ToDictionaryAsync(message => message.Id, cancellationToken);
        var views = items
            .Where(item => Enum.TryParse<ClassifierRunAction>(item.Action, out _))
            .Select(item =>
            {
                var message = messages.GetValueOrDefault(item.MessageId);
                return new ClassifierRunItemView(
                    new(
                        item.MessageId,
                        Enum.Parse<ClassifierRunAction>(item.Action),
                        item.Category,
                        item.CaseId,
                        item.CaseReference,
                        item.Reason,
                        item.Refusal,
                        item.OccurredAtUtc),
                    message?.Subject,
                    message is null ? null : message.SenderDisplayName ?? message.SenderAddress,
                    message is not null);
            })
            .ToArray();
        var actions = views
            .GroupBy(view => view.Item.Action)
            .ToDictionary(group => group.Key, group => group.Count());
        return new(
            Summary(run, actions),
            new(run.DryRun, run.Model, run.Batch, run.ScanPages, run.MaxBudgetUsd, run.AgentVersion),
            run.Verdicts,
            run.Failed,
            run.Deferred,
            run.Turns,
            run.Error,
            run.Summary,
            run.RecordedBy,
            views,
            ItemsCut: false);
    }

    public async Task<ClassifierRunHealthFacts> HealthAsync(
        DateTimeOffset nowUtc,
        DateTimeOffset dayStartUtc,
        DateTimeOffset monthStartUtc,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var runs = context.Set<ClassifierRunEntity>().AsNoTracking();
        var recent = await runs
            .OrderByDescending(item => item.StartedAtUtc)
            .ThenByDescending(item => item.Id)
            .Take(ClassifierRunPolicy.RecentRuns)
            .ToArrayAsync(cancellationToken);
        var dayAgo = nowUtc.AddHours(-24);
        var last24Hours = await runs.CountAsync(
            item => item.StartedAtUtc >= dayAgo && item.StartedAtUtc <= nowUtc,
            cancellationToken);
        var today = await runs.Where(item => item.StartedAtUtc >= dayStartUtc)
            .SumAsync(item => item.CostUsd, cancellationToken);
        var month = await runs.Where(item => item.StartedAtUtc >= monthStartUtc)
            .SumAsync(item => item.CostUsd, cancellationToken);
        return new(
            [.. recent.Select(run => Summary(run, NoActions))],
            last24Hours,
            today ?? 0m,
            month ?? 0m);
    }

    private static readonly FrozenDictionary<ClassifierRunAction, int> NoActions =
        FrozenDictionary<ClassifierRunAction, int>.Empty;

    private static ClassifierRunEntity NewRun(Guid runId, ClassifierRunFacts facts, string recordedBy, DateTimeOffset startedAtUtc) =>
        new()
        {
            Id = runId,
            StartedAtUtc = startedAtUtc,
            DryRun = facts.DryRun,
            Model = facts.Model,
            Batch = facts.Batch,
            ScanPages = facts.ScanPages,
            MaxBudgetUsd = facts.MaxBudgetUsd,
            AgentVersion = facts.AgentVersion,
            RecordedBy = recordedBy
        };

    private async Task<ClassifierRunEntity> ReadAsync(Guid runId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Set<ClassifierRunEntity>().AsNoTracking()
            .SingleAsync(item => item.Id == runId, cancellationToken);
    }

    private static ClassifierRunRecorded Recorded(ClassifierRunEntity entity, bool isReplay) =>
        new(entity.Id, entity.StartedAtUtc, entity.FinishedAtUtc, isReplay);

    // The summary's environment is the deployment's own; the caller names it.
    private static ClassifierRunSummary Summary(
        ClassifierRunEntity run,
        IReadOnlyDictionary<ClassifierRunAction, int> actions) =>
        new(
            run.Id,
            ClassifierRunEnvironment.Live,
            run.StartedAtUtc,
            run.FinishedAtUtc,
            run.DryRun,
            run.Outcome,
            run.FinishedAtUtc is not null && Enum.TryParse<ClassifierRunOutcome>(run.OutcomeKind, out var kind) ? kind : null,
            run.Eligible,
            run.AwaitingDecision,
            run.CandidatesHidden,
            run.HiddenRuns,
            run.CostUsd,
            run.DurationMs,
            actions);

    private static bool IsDuplicate(DbUpdateException exception) =>
        exception.GetBaseException() is SqlException { Number: 2601 or 2627 };
}
