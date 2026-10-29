using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;
using Pegasus.Core.Custody;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// A Case's one automatic Glass's valuation request (R545, 29 October 2026):
/// an external work item recorded by Case creation, in the creation's own
/// transaction and beside its automatic vehicle lookup, while the
/// Administrator's switch is on (R546). Its operation key names the Case, and
/// the key is unique, so a replayed creation or any later path cannot record a
/// second. The Web host runs it (<see cref="RunAutomaticGuideValuation"/>); the
/// Worker's dispatch leaves it alone.
/// </summary>
internal sealed class EfAutomaticGuideValuationStore(
    IDbContextFactory<PegasusDbContext> contextFactory) : IAutomaticGuideValuationStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>The one operation key a Case's automatic request carries.</summary>
    internal static string OperationKey(Guid caseId) => $"guide-valuation:auto:{caseId:N}";

    /// <summary>
    /// Adds the Case's automatic request to <paramref name="context"/> when the
    /// Administrator's switch is on, inside the creation's transaction; nothing
    /// is saved here. A Case created while the switch is off has no request.
    /// </summary>
    internal static async Task EnqueueForCaseAsync(
        PegasusDbContext context,
        Guid caseId,
        CaseEntity? caseEntity,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        if (!await EfSendToAiControlStore.IsEnabledAsync(
                context, SendToAiControlEntity.AutomaticGuideValuationId, cancellationToken))
        {
            return;
        }

        context.ExternalWorkItems.Add(new ExternalWorkItemEntity
        {
            Id = Guid.NewGuid(),
            CaseId = caseId,
            Case = caseEntity,
            Kind = ExternalWorkKinds.GuideValuation,
            OperationKey = OperationKey(caseId),
            State = ExternalWorkStatePersistence.Pending,
            AttemptCount = 0,
            DueAtUtc = nowUtc,
        });
    }

    public async Task<IReadOnlyList<AutomaticGuideValuationRequest>> ListDueAutomaticAsync(
        DateTimeOffset nowUtc,
        int maximumItems,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.ExternalWorkItems.AsNoTracking()
            .Where(item => item.Kind == ExternalWorkKinds.GuideValuation
                && item.State == ExternalWorkStatePersistence.Pending
                && item.CaseId != null
                && item.DueAtUtc <= nowUtc)
            .OrderBy(item => item.DueAtUtc)
            .ThenBy(item => item.Id)
            .Take(maximumItems)
            .Select(item => new AutomaticGuideValuationRequest(item.Id, item.CaseId!.Value))
            .ToListAsync(cancellationToken);
    }

    public async Task<AutomaticGuideValuationTarget?> ReadAutomaticTargetAsync(
        Guid caseId,
        ValuationSource source,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var workflow = await context.CaseWorkflows.AsNoTracking()
            .SingleOrDefaultAsync(item => item.CaseId == caseId, cancellationToken);
        return workflow is null
            ? null
            : await TargetAsync(context, workflow, source, cancellationToken);
    }

    public async Task DeferAutomaticAsync(Guid workItemId, DateTimeOffset dueAtUtc, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await context.ExternalWorkItems
            .Where(item => item.Id == workItemId
                && item.Kind == ExternalWorkKinds.GuideValuation
                && item.State == ExternalWorkStatePersistence.Pending)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.DueAtUtc, dueAtUtc), cancellationToken);
    }

    public async Task<bool> BeginAutomaticAsync(Guid workItemId, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        // Processing is never picked again: if the host stops while Glass's
        // answers, the request is not asked a second time.
        return await context.ExternalWorkItems
            .Where(item => item.Id == workItemId
                && item.Kind == ExternalWorkKinds.GuideValuation
                && item.State == ExternalWorkStatePersistence.Pending)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.State, ExternalWorkStatePersistence.Processing)
                    .SetProperty(item => item.AttemptCount, item => item.AttemptCount + 1)
                    .SetProperty(item => item.DueAtUtc, nowUtc),
                cancellationToken) == 1;
    }

    public async Task CompleteAutomaticAsync(
        Guid workItemId,
        AutomaticGuideValuationOutcome outcome,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        string? code = outcome == AutomaticGuideValuationOutcome.Recorded ? null : outcome.ToString();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await context.ExternalWorkItems
            .Where(item => item.Id == workItemId
                && item.Kind == ExternalWorkKinds.GuideValuation
                && (item.State == ExternalWorkStatePersistence.Pending
                    || item.State == ExternalWorkStatePersistence.Processing))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.State, ExternalWorkStatePersistence.Completed)
                    .SetProperty(item => item.FailureCode, code)
                    .SetProperty(item => item.CompletedAtUtc, nowUtc),
                cancellationToken);
    }

    /// <summary>
    /// The card, recorded as system work under the Case's workflow lock
    /// (FRD-14): the Case must still be open and its current work hold no
    /// Glass's card, so a card staff recorded while Glass's answered stands;
    /// the card is recorded as a Save records it, the version advances, and
    /// whoever holds the lease keeps it.
    /// </summary>
    public async Task<bool> RecordAutomaticAsync(
        Guid caseId,
        ActionActor actor,
        string operationKey,
        ValuationDetails card,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);
        var details = ValuationPolicy.ValidateGuideEntry(actor, card);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginReadCommittedAsync(cancellationToken);
        await EfCaseWorkflowStore.AcquireWorkflowMutationLockAsync(context, caseId, cancellationToken);
        var workflow = await context.CaseWorkflows
            .Include(item => item.Case)
            .SingleOrDefaultAsync(item => item.CaseId == caseId, cancellationToken);
        if (workflow is null
            || !AutomaticGuideValuationPolicy.IsNeeded(
                await TargetAsync(context, workflow, details.Source, cancellationToken)))
        {
            return false;
        }

        var workId = await CaseWorkScope.CurrentIdAsync(context, caseId, cancellationToken);
        var recorded = await EfValuationStore.RecordGuideEntriesAsync(
            context, workflow, workId, actor, operationKey, [details], nowUtc, cancellationToken);
        await EfValuationStore.MarkStaleIfNeededAsync(
            context, caseId, workId, recorded.Before, recorded.After, nowUtc, cancellationToken);
        var beforeVersion = workflow.Version;
        CaseMutationGuard.Advance(workflow);
        var valuation = EfValuationStore.Map(recorded.Written.Single());
        CaseMutationHistory.Add(
            context,
            workflow,
            actor,
            operationKey,
            "Valuation recorded.",
            "valuation_created",
            CaseOperationReplay.Hash(JsonSerializer.Serialize(details, SerializerOptions)),
            beforeVersion,
            workflow.Version,
            "{}",
            JsonSerializer.Serialize(new { Valuation = valuation }, SerializerOptions),
            $"{ValuationPolicy.PolicyKey}/v{ValuationPolicy.PolicyVersion}",
            nowUtc);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static async Task<AutomaticGuideValuationTarget> TargetAsync(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        ValuationSource source,
        CancellationToken cancellationToken)
    {
        var workId = await CaseWorkScope.CurrentIdAsync(context, workflow.CaseId, cancellationToken);
        var sourceName = source.ToString();
        return new(
            workflow.ArchivedAtUtc is null
                && !CaseLifecycleRules.IsTerminal(Enum.Parse<CaseLifecycleState>(workflow.State)),
            await context.CaseValuations.AnyAsync(
                item => item.WorkId == workId && item.Source == sourceName,
                cancellationToken),
            workflow.EditLeaseExpiresAtUtc);
    }
}
