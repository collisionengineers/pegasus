using Microsoft.EntityFrameworkCore;
using Pegasus.Core.AiWork;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The Administrator-held switches of the Automation &amp; AI settings panel,
/// one row each: Send to AI, the immediate operational cut point for new AI
/// jobs, and the automatic Glass's valuation at Case creation (R546). An
/// absent row means enabled.
/// </summary>
public sealed class EfSendToAiControlStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider) : ISendToAiControl, IAutomaticGuideValuationControl
{
    public async Task<bool> IsEnabledAsync(CancellationToken cancellationToken)
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
        SetEnabledAsync(SendToAiControlEntity.SingletonId, "send_to_ai", enabled, actor, reason, operationKey, cancellationToken);

    async Task<bool> IAutomaticGuideValuationControl.IsEnabledAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await IsEnabledAsync(context, SendToAiControlEntity.AutomaticGuideValuationId, cancellationToken);
    }

    Task<bool> IAutomaticGuideValuationControl.SetEnabledAsync(
        bool enabled,
        ActionActor actor,
        string? reason,
        string operationKey,
        CancellationToken cancellationToken) =>
        SetEnabledAsync(
            SendToAiControlEntity.AutomaticGuideValuationId,
            "automatic_guide_valuation",
            enabled,
            actor,
            reason,
            operationKey,
            cancellationToken);

    /// <summary>One switch's state, read in the caller's context; an absent row means enabled.</summary>
    internal static async Task<bool> IsEnabledAsync(
        PegasusDbContext context,
        string switchId,
        CancellationToken cancellationToken)
    {
        var control = await context.SendToAiControl.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == switchId, cancellationToken);
        return control?.Enabled ?? true;
    }

    private async Task<bool> SetEnabledAsync(
        string switchId,
        string aggregateType,
        bool enabled,
        ActionActor actor,
        string? reason,
        string operationKey,
        CancellationToken cancellationToken)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.ManageAutomationClients);
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
        var previous = control?.Enabled ?? true;
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
        return enabled;
    }
}
