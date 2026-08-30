using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Persists security events and business action history in their separate append-only streams.
/// </summary>
public sealed class EfIdentityAuditStore(IDbContextFactory<PegasusDbContext> contextFactory)
    : ISecurityEventWriter, IActionHistoryWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task AppendAsync(
        SecurityEvent securityEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(securityEvent);
        ValidateId(securityEvent.Id, nameof(securityEvent));
        ValidateEnum(securityEvent.Type, nameof(securityEvent));
        ValidateEnum(securityEvent.Outcome, nameof(securityEvent));
        ValidateRequired(securityEvent.SubjectId, 200, nameof(securityEvent.SubjectId));
        ValidateUtc(securityEvent.OccurredAtUtc, nameof(securityEvent.OccurredAtUtc));
        ValidateRequired(securityEvent.CorrelationId, 100, nameof(securityEvent.CorrelationId));
        ValidateOptional(securityEvent.ReasonCode, 100, nameof(securityEvent.ReasonCode));

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.SecurityEvents.Add(new SecurityEventEntity
        {
            Id = securityEvent.Id,
            Type = securityEvent.Type.ToString(),
            Outcome = securityEvent.Outcome.ToString(),
            SubjectId = securityEvent.SubjectId,
            OccurredAtUtc = securityEvent.OccurredAtUtc,
            CorrelationId = securityEvent.CorrelationId,
            ReasonCode = securityEvent.ReasonCode
        });
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task AppendAsync(
        ActionHistoryEntry entry,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.ActionHistory.Add(ToEntity(entry));
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> TryAppendAsync(
        ActionHistoryEntry entry,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.ActionHistory.Add(ToEntity(entry));
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception)
            when (exception.GetBaseException() is SqlException { Number: 2601 or 2627 })
        {
            // The derived identity is already in permanent history: another
            // writer recorded this same operation first, and its row stands.
            return false;
        }
    }

    private static ActionHistoryEntity ToEntity(ActionHistoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ValidateId(entry.Id, nameof(entry));
        ValidateRequired(entry.AggregateType, 100, nameof(entry.AggregateType));
        ValidateRequired(entry.AggregateId, 200, nameof(entry.AggregateId));
        ValidateRequired(entry.EventKind, 100, nameof(entry.EventKind));
        ArgumentNullException.ThrowIfNull(entry.Actor);
        ValidateRequired(entry.Actor.SubjectId, 200, nameof(entry.Actor.SubjectId));
        ValidateUtc(entry.OccurredAtUtc, nameof(entry.OccurredAtUtc));
        ValidateRequired(entry.Outcome, 40, nameof(entry.Outcome));
        ValidateRequired(entry.CorrelationId, 100, nameof(entry.CorrelationId));
        ValidateOptional(entry.Reason, 1000, nameof(entry.Reason));
        ValidateOptional(entry.PolicyVersion, 100, nameof(entry.PolicyVersion));

        var roles = entry.Actor.Roles
            .OrderBy(role => role)
            .Select(role => role.ToString())
            .ToArray();

        return new ActionHistoryEntity
        {
            Id = entry.Id,
            AggregateType = entry.AggregateType,
            AggregateId = entry.AggregateId,
            EventKind = entry.EventKind,
            ActorKind = entry.Actor.Kind.ToString(),
            ActorSubjectId = entry.Actor.SubjectId,
            ActorRolesJson = JsonSerializer.Serialize(roles, SerializerOptions),
            OccurredAtUtc = entry.OccurredAtUtc,
            Outcome = entry.Outcome,
            CorrelationId = entry.CorrelationId,
            Reason = entry.Reason,
            BeforeJson = entry.BeforeJson,
            AfterJson = entry.AfterJson,
            PolicyVersion = entry.PolicyVersion
        };
    }

    private static void ValidateId(Guid id, string parameterName)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("An audit record requires a non-empty identifier.", parameterName);
        }
    }

    private static void ValidateEnum<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, "The audit record value is invalid.");
        }
    }

    private static void ValidateRequired(string value, int maximumLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length > maximumLength)
        {
            throw new ArgumentException(
                $"The audit value cannot exceed {maximumLength} characters.",
                parameterName);
        }
    }

    private static void ValidateOptional(string? value, int maximumLength, string parameterName)
    {
        if (value is not null && value.Length > maximumLength)
        {
            throw new ArgumentException(
                $"The audit value cannot exceed {maximumLength} characters.",
                parameterName);
        }
    }

    private static void ValidateUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Audit instants must be expressed in UTC.", parameterName);
        }
    }
}
