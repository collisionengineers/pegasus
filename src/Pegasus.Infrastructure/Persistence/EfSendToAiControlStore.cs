using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Pegasus.Core.AiWork;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The Administrator-held switches, one row each: Send to AI, the immediate
/// operational cut point for new AI jobs, and the automatic Glass's valuation
/// at Case creation (R546), both on the Automation &amp; AI settings panel; the
/// automatic Brego, Cazana and CAP valuations and the automatic Experian
/// vehicle history check (R1286); and the temporary Case/PO suffix on
/// Configuration (R656). An absent row means the switch's
/// <see cref="SendToAiControlEntity.DefaultEnabled"/> state: on, except for the
/// four automatic switches added on 26 November 2026, which are off until
/// turned on (R1287). Send to AI alone is kept in <see cref="IMemoryCache"/>
/// for <see cref="SendToAiLifetime"/> (R766): the Case page reads it on every
/// open, the Administration save evicts it so this process sees the cut at
/// once, and any other copy of it expires within the lifetime. The other
/// switches, and every read inside a store's own transaction, read the row.
/// </summary>
public sealed class EfSendToAiControlStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider,
    IMemoryCache cache) : ISendToAiControl, IAutomaticRequestControl, ICaseReferenceSuffixControl
{
    // R342's lifetime for a switch a Worker copy could hold: the cut reaches it
    // within this long.
    internal static readonly TimeSpan SendToAiLifetime = TimeSpan.FromSeconds(30);

    // A private key object, so no other IMemoryCache user can collide with the entry.
    private static readonly object SendToAiKey = new();

    // Every automatic request switch's row, read together where Case creation
    // or the settings read them all.
    private static readonly string[] AutomaticSwitchIds =
        [.. Enum.GetValues<AutomaticRequestKind>().Select(AutomaticSwitchId)];

    public Task<bool> IsEnabledAsync(CancellationToken cancellationToken) =>
        EvictableCacheEntry.GetOrReadAsync(
            cache, SendToAiKey, SendToAiLifetime, slidingWindow: null, ReadSendToAiAsync, cancellationToken);

    private async Task<bool> ReadSendToAiAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await IsEnabledAsync(context, SendToAiControlEntity.SingletonId, cancellationToken);
    }

    public Task<bool> SetEnabledAsync(
        bool enabled,
        ActionActor actor,
        string? reason,
        string operationKey,
        CancellationToken cancellationToken) =>
        SetEnabledAsync(
            SendToAiControlEntity.SingletonId,
            "send_to_ai",
            StaffAccessRight.ManageAutomationClients,
            enabled,
            actor,
            reason,
            operationKey,
            cancellationToken);

    /// <summary>The switch row each automatic request kind is held in (R546, R1286).</summary>
    internal static string AutomaticSwitchId(AutomaticRequestKind kind) => kind switch
    {
        AutomaticRequestKind.GlassesValuation => SendToAiControlEntity.AutomaticGuideValuationId,
        AutomaticRequestKind.BregoValuation => SendToAiControlEntity.AutomaticBregoValuationId,
        AutomaticRequestKind.CazanaValuation => SendToAiControlEntity.AutomaticCazanaValuationId,
        AutomaticRequestKind.CapValuation => SendToAiControlEntity.AutomaticCapValuationId,
        AutomaticRequestKind.VehicleHistoryCheck => SendToAiControlEntity.AutomaticVehicleHistoryCheckId,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "The automatic request kind is unknown."),
    };

    /// <summary>
    /// The history aggregate and event stem each switch's change is recorded
    /// under; Glass's keeps the one its existing history carries.
    /// </summary>
    private static string AutomaticHistoryName(AutomaticRequestKind kind) => kind switch
    {
        AutomaticRequestKind.GlassesValuation => "automatic_guide_valuation",
        AutomaticRequestKind.BregoValuation => "automatic_brego_valuation",
        AutomaticRequestKind.CazanaValuation => "automatic_cazana_valuation",
        AutomaticRequestKind.CapValuation => "automatic_cap_valuation",
        AutomaticRequestKind.VehicleHistoryCheck => "automatic_vehicle_history_check",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "The automatic request kind is unknown."),
    };

    async Task<bool> IAutomaticRequestControl.IsEnabledAsync(
        AutomaticRequestKind kind,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await IsEnabledAsync(context, AutomaticSwitchId(kind), cancellationToken);
    }

    async Task<IReadOnlyDictionary<AutomaticRequestKind, bool>> IAutomaticRequestControl.ReadAsync(
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await ReadAutomaticAsync(context, cancellationToken);
    }

    Task<bool> IAutomaticRequestControl.SetEnabledAsync(
        AutomaticRequestKind kind,
        bool enabled,
        ActionActor actor,
        string? reason,
        string operationKey,
        CancellationToken cancellationToken) =>
        SetEnabledAsync(
            AutomaticSwitchId(kind),
            AutomaticHistoryName(kind),
            StaffAccessRight.ManageAutomationClients,
            enabled,
            actor,
            reason,
            operationKey,
            cancellationToken);

    /// <summary>
    /// Every automatic request switch, read in the caller's context in one
    /// statement; an absent row is its <see cref="SendToAiControlEntity.DefaultEnabled"/> state.
    /// </summary>
    internal static async Task<Dictionary<AutomaticRequestKind, bool>> ReadAutomaticAsync(
        PegasusDbContext context,
        CancellationToken cancellationToken)
    {
        var rows = await context.SendToAiControl.AsNoTracking()
            .Where(item => AutomaticSwitchIds.Contains(item.Id))
            .Select(item => new { item.Id, item.Enabled })
            .ToDictionaryAsync(item => item.Id, item => item.Enabled, StringComparer.Ordinal, cancellationToken);
        return Enum.GetValues<AutomaticRequestKind>().ToDictionary(
            kind => kind,
            kind =>
            {
                var id = AutomaticSwitchId(kind);
                return rows.TryGetValue(id, out var enabled) ? enabled : SendToAiControlEntity.DefaultEnabled(id);
            });
    }

    async Task<bool> ICaseReferenceSuffixControl.IsEnabledAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await IsEnabledAsync(context, SendToAiControlEntity.CaseReferenceSuffixId, cancellationToken);
    }

    Task<bool> ICaseReferenceSuffixControl.SetEnabledAsync(
        bool enabled,
        ActionActor actor,
        string operationKey,
        CancellationToken cancellationToken) =>
        SetEnabledAsync(
            SendToAiControlEntity.CaseReferenceSuffixId,
            "case_reference_suffix",
            StaffAccessRight.ManageWorkflowConfiguration,
            enabled,
            actor,
            reason: null,
            operationKey,
            cancellationToken);

    /// <summary>
    /// One switch's state, read in the caller's context; an absent row is the
    /// switch's <see cref="SendToAiControlEntity.DefaultEnabled"/> state.
    /// </summary>
    internal static async Task<bool> IsEnabledAsync(
        PegasusDbContext context,
        string switchId,
        CancellationToken cancellationToken)
    {
        var control = await context.SendToAiControl.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == switchId, cancellationToken);
        return control?.Enabled ?? SendToAiControlEntity.DefaultEnabled(switchId);
    }

    private async Task<bool> SetEnabledAsync(
        string switchId,
        string aggregateType,
        StaffAccessRight requiredRight,
        bool enabled,
        ActionActor actor,
        string? reason,
        string operationKey,
        CancellationToken cancellationToken)
    {
        StaffAuthorization.Require(actor, requiredRight);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginReadCommittedAsync(cancellationToken);
        // The switch's row, or the gap its first set would fill, is the first
        // lock, so a concurrent first set waits instead of inserting it twice.
        await context.Database.ExecuteSqlAsync(
            $"SELECT 1 FROM [SendToAiControl] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {switchId}",
            cancellationToken);
        var control = await context.SendToAiControl
            .SingleOrDefaultAsync(
                item => item.Id == switchId,
                cancellationToken);
        var previous = control?.Enabled ?? SendToAiControlEntity.DefaultEnabled(switchId);
        if (control is null)
        {
            control = new() { Id = switchId, Enabled = enabled, Version = 0 };
            context.SendToAiControl.Add(control);
        }
        else
        {
            control.Enabled = enabled;
            control.Version++;
        }

        context.ActionHistory.Add(new()
        {
            Id = Guid.NewGuid(),
            AggregateType = aggregateType,
            AggregateId = switchId,
            EventKind = enabled ? $"{aggregateType}_enabled" : $"{aggregateType}_disabled",
            ActorKind = actor.Kind.ToString(),
            ActorSubjectId = actor.SubjectId,
            ActorRolesJson = System.Text.Json.JsonSerializer.Serialize(
                actor.Roles.OrderBy(role => role)),
            OccurredAtUtc = timeProvider.GetUtcNow(),
            Outcome = previous == enabled ? "Unchanged" : "Succeeded",
            CorrelationId = operationKey.Trim(),
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()
        });
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        if (switchId == SendToAiControlEntity.SingletonId)
        {
            EvictableCacheEntry.Evict(cache, SendToAiKey);
        }

        return enabled;
    }
}
