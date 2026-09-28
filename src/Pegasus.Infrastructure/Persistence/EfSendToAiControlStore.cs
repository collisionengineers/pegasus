using System.Data;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.AiWork;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The Administrator-held Send to AI switch. Absent row means enabled: this
/// control is the immediate operational cut point for new AI jobs.
/// </summary>
public sealed class EfSendToAiControlStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider) : ISendToAiControl
{
    public async Task<bool> IsEnabledAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var control = await context.SendToAiControl.AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == SendToAiControlEntity.SingletonId,
                cancellationToken);
        return control?.Enabled ?? true;
    }

    public async Task<bool> SetEnabledAsync(
        bool enabled,
        ActionActor actor,
        string? reason,
        string operationKey,
        CancellationToken cancellationToken)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.ManageAutomationClients);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var control = await context.SendToAiControl
            .SingleOrDefaultAsync(
                item => item.Id == SendToAiControlEntity.SingletonId,
                cancellationToken);
        var previous = control?.Enabled ?? true;
        if (control is null)
        {
            control = new() { Id = SendToAiControlEntity.SingletonId, Enabled = enabled, Version = 0 };
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
            AggregateType = "send_to_ai",
            AggregateId = SendToAiControlEntity.SingletonId,
            EventKind = enabled ? "send_to_ai_enabled" : "send_to_ai_disabled",
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
