using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;
using Pegasus.Core.Custody;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// A Case's automatic requests (R545, 29 October 2026; R1286, 26 November
/// 2026): one external work item per kind whose Administrator switch is on
/// (R546, R1287), recorded by Case creation in the creation's own transaction
/// and beside its automatic vehicle lookup. Each operation key names the kind
/// and the Case, and the key is unique, so a replayed creation or any later
/// path cannot record a second. The Web host runs them
/// (<see cref="RunAutomaticRequest"/>); the Worker's dispatch leaves them alone.
/// </summary>
internal sealed class EfAutomaticRequestStore(
    IDbContextFactory<PegasusDbContext> contextFactory) : IAutomaticRequestStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Every automatic request's external work kind: the Web host's own work,
    /// which the Worker never dispatches and which never holds up an archive.
    /// </summary>
    internal static readonly string[] WorkKinds =
        [.. Enum.GetValues<AutomaticRequestKind>().Select(WorkKind)];

    /// <summary>The external work kind of each automatic request; Glass's keeps the one its waiting rows carry.</summary>
    internal static string WorkKind(AutomaticRequestKind kind) => kind switch
    {
        AutomaticRequestKind.GlassesValuation => ExternalWorkKinds.GuideValuation,
        AutomaticRequestKind.BregoValuation => ExternalWorkKinds.BregoGuideValuation,
        AutomaticRequestKind.CazanaValuation => ExternalWorkKinds.CazanaGuideValuation,
        AutomaticRequestKind.CapValuation => ExternalWorkKinds.CapGuideValuation,
        AutomaticRequestKind.VehicleHistoryCheck => ExternalWorkKinds.VehicleHistoryCheck,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "The automatic request kind is unknown."),
    };

    /// <summary>The automatic request kind an external work kind records.</summary>
    internal static AutomaticRequestKind RequestKind(string workKind) => workKind switch
    {
        ExternalWorkKinds.GuideValuation => AutomaticRequestKind.GlassesValuation,
        ExternalWorkKinds.BregoGuideValuation => AutomaticRequestKind.BregoValuation,
        ExternalWorkKinds.CazanaGuideValuation => AutomaticRequestKind.CazanaValuation,
        ExternalWorkKinds.CapGuideValuation => AutomaticRequestKind.CapValuation,
        ExternalWorkKinds.VehicleHistoryCheck => AutomaticRequestKind.VehicleHistoryCheck,
        _ => throw new ArgumentOutOfRangeException(nameof(workKind), workKind, "The external work kind is not an automatic request."),
    };

    /// <summary>
    /// The one operation key a Case's automatic request of a kind carries.
    /// Glass's keeps the key its existing rows carry.
    /// </summary>
    internal static string OperationKey(AutomaticRequestKind kind, Guid caseId) => kind switch
    {
        AutomaticRequestKind.GlassesValuation => $"guide-valuation:auto:{caseId:N}",
        AutomaticRequestKind.BregoValuation => $"guide-valuation:auto:brego:{caseId:N}",
        AutomaticRequestKind.CazanaValuation => $"guide-valuation:auto:cazana:{caseId:N}",
        AutomaticRequestKind.CapValuation => $"guide-valuation:auto:cap:{caseId:N}",
        AutomaticRequestKind.VehicleHistoryCheck => $"vehicle-history-check:auto:{caseId:N}",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "The automatic request kind is unknown."),
    };

    /// <summary>
    /// Adds the Case's automatic requests to <paramref name="context"/>, one for
    /// each switch that is on, inside the creation's transaction; nothing is
    /// saved here. Every switch is read in one statement, so creation reads no
    /// more than it did with one switch. A Case created while a switch is off
    /// has no request of that kind.
    /// </summary>
    internal static async Task EnqueueForCaseAsync(
        PegasusDbContext context,
        Guid caseId,
        CaseEntity? caseEntity,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var switches = await EfSendToAiControlStore.ReadAutomaticAsync(context, cancellationToken);
        foreach (var kind in Enum.GetValues<AutomaticRequestKind>())
        {
            if (!switches[kind])
            {
                continue;
            }

            context.ExternalWorkItems.Add(new ExternalWorkItemEntity
            {
                Id = Guid.NewGuid(),
                CaseId = caseId,
                Case = caseEntity,
                Kind = WorkKind(kind),
                OperationKey = OperationKey(kind, caseId),
                State = ExternalWorkStatePersistence.Pending,
                AttemptCount = 0,
                DueAtUtc = nowUtc,
            });
        }
    }

    public async Task<IReadOnlyList<AutomaticRequest>> ListDueAutomaticAsync(
        DateTimeOffset nowUtc,
        int maximumItems,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var due = await context.ExternalWorkItems.AsNoTracking()
            .Where(item => WorkKinds.Contains(item.Kind)
                && item.State == ExternalWorkStatePersistence.Pending
                && item.CaseId != null
                && item.DueAtUtc <= nowUtc)
            .OrderBy(item => item.DueAtUtc)
            .ThenBy(item => item.Id)
            .Take(maximumItems)
            .Select(item => new { item.Id, CaseId = item.CaseId!.Value, item.Kind })
            .ToListAsync(cancellationToken);
        return [.. due.Select(item => new AutomaticRequest(item.Id, item.CaseId, RequestKind(item.Kind)))];
    }

    public async Task<AutomaticRequestTarget?> ReadAutomaticTargetAsync(
        Guid caseId,
        AutomaticRequestKind kind,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var workflow = await context.CaseWorkflows.AsNoTracking()
            .SingleOrDefaultAsync(item => item.CaseId == caseId, cancellationToken);
        return workflow is null
            ? null
            : await TargetAsync(context, workflow, kind, cancellationToken);
    }

    public async Task DeferAutomaticAsync(Guid workItemId, DateTimeOffset dueAtUtc, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await context.ExternalWorkItems
            .Where(item => item.Id == workItemId
                && WorkKinds.Contains(item.Kind)
                && item.State == ExternalWorkStatePersistence.Pending)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.DueAtUtc, dueAtUtc), cancellationToken);
    }

    public async Task<bool> BeginAutomaticAsync(Guid workItemId, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        // Processing is never picked again: if the host stops while the
        // provider answers, the request is not asked a second time.
        return await context.ExternalWorkItems
            .Where(item => item.Id == workItemId
                && WorkKinds.Contains(item.Kind)
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
        AutomaticRequestOutcome outcome,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        string? code = outcome == AutomaticRequestOutcome.Recorded ? null : outcome.ToString();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await context.ExternalWorkItems
            .Where(item => item.Id == workItemId
                && WorkKinds.Contains(item.Kind)
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
    /// A valuation's card, recorded as system work under the Case's workflow
    /// lock (FRD-14): the Case must still be open and its current work hold no
    /// card of the source, so a card staff recorded while the provider answered
    /// stands; the card is recorded as a Save records it, the version advances,
    /// and whoever holds the lease keeps it.
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
            || !AutomaticRequestPolicy.IsNeeded(
                await SourceTargetAsync(context, workflow, details.Source, cancellationToken)))
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

    private static Task<AutomaticRequestTarget> TargetAsync(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        AutomaticRequestKind kind,
        CancellationToken cancellationToken) =>
        AutomaticRequestPolicy.Source(kind) is { } source
            ? SourceTargetAsync(context, workflow, source, cancellationToken)
            : HistoryCheckTargetAsync(context, workflow, cancellationToken);

    /// <summary>A valuation's answer is a card of its source on the current work.</summary>
    private static async Task<AutomaticRequestTarget> SourceTargetAsync(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        ValuationSource source,
        CancellationToken cancellationToken)
    {
        var workId = await CaseWorkScope.CurrentIdAsync(context, workflow.CaseId, cancellationToken);
        var sourceName = source.ToString();
        return new(
            IsOpen(workflow),
            await context.CaseValuations.AnyAsync(
                item => item.WorkId == workId && item.Source == sourceName,
                cancellationToken),
            workflow.EditLeaseExpiresAtUtc,
            AcceptsAssessment(workflow));
    }

    /// <summary>
    /// The history check's answer is Vehicle history text on the current work:
    /// text that already holds words is never replaced by the automatic check
    /// (R1291).
    /// </summary>
    private static async Task<AutomaticRequestTarget> HistoryCheckTargetAsync(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        CancellationToken cancellationToken)
    {
        var workId = await CaseWorkScope.CurrentIdAsync(context, workflow.CaseId, cancellationToken);
        var historyValues = await context.CaseAssessmentFields.AsNoTracking()
            .Where(item => item.WorkId == workId && item.FieldPath == AssessmentVocabulary.HistoryCheck)
            .Select(item => item.Value)
            .ToListAsync(cancellationToken);
        return new(
            IsOpen(workflow),
            historyValues.Exists(value => !string.IsNullOrWhiteSpace(value)),
            workflow.EditLeaseExpiresAtUtc,
            AcceptsAssessment(workflow));
    }

    /// <summary>The state the assessment save accepts, read from the workflow row already loaded.</summary>
    private static bool AcceptsAssessment(CaseWorkflowEntity workflow) =>
        AssessmentPolicy.IsWritableState(Enum.Parse<CaseLifecycleState>(workflow.State));

    private static bool IsOpen(CaseWorkflowEntity workflow) =>
        workflow.ArchivedAtUtc is null
        && !CaseLifecycleRules.IsTerminal(Enum.Parse<CaseLifecycleState>(workflow.State));
}
