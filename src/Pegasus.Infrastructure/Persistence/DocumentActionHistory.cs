using System.Text.Json;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Persistence;

internal static class DocumentActionHistory
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    public static ActionHistoryEntity Succeeded(
        string aggregateType,
        string aggregateId,
        string eventKind,
        ActionActor actor,
        DateTimeOffset occurredAtUtc,
        string operationKey,
        string? reason = null,
        string? beforeJson = null,
        string? afterJson = null)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return new()
        {
            Id = Guid.NewGuid(),
            AggregateType = aggregateType,
            AggregateId = aggregateId,
            EventKind = eventKind,
            ActorKind = actor.Kind.ToString(),
            ActorSubjectId = actor.SubjectId,
            ActorRolesJson = SerializeRoles(actor),
            OccurredAtUtc = occurredAtUtc,
            Outcome = "Succeeded",
            CorrelationId = operationKey,
            Reason = reason,
            BeforeJson = beforeJson,
            AfterJson = afterJson
        };
    }

    public static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, SerializerOptions);

    public static T Deserialize<T>(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException("The persisted document action snapshot is missing.");
        }

        try
        {
            return JsonSerializer.Deserialize<T>(value, SerializerOptions)
                ?? throw new InvalidDataException(
                    "The persisted document action snapshot is invalid.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "The persisted document action snapshot is invalid.",
                exception);
        }
    }

    public static void RequireExactReplay(
        ActionHistoryEntity existing,
        string aggregateType,
        string aggregateId,
        string eventKind,
        ActionActor actor,
        string? reason,
        string? afterJson)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var actorRolesJson = SerializeRoles(actor);
        if (!string.Equals(existing.AggregateType, aggregateType, StringComparison.Ordinal)
            || !string.Equals(existing.AggregateId, aggregateId, StringComparison.Ordinal)
            || !string.Equals(existing.EventKind, eventKind, StringComparison.Ordinal)
            || !string.Equals(existing.ActorKind, actor.Kind.ToString(), StringComparison.Ordinal)
            || !string.Equals(existing.ActorSubjectId, actor.SubjectId, StringComparison.Ordinal)
            || !string.Equals(existing.ActorRolesJson, actorRolesJson, StringComparison.Ordinal)
            || !string.Equals(existing.Outcome, "Succeeded", StringComparison.Ordinal)
            || !string.Equals(existing.Reason, reason, StringComparison.Ordinal)
            || !string.Equals(existing.AfterJson, afterJson, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The document operation key was already used for a different audited action.");
        }
    }

    private static string SerializeRoles(ActionActor actor) =>
        JsonSerializer.Serialize(
            actor.Roles.OrderBy(role => role).Select(role => role.ToString()),
            SerializerOptions);
}
