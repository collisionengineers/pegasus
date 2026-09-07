using System.Globalization;
using System.Text.Json;
using Pegasus.Core.Identity;

namespace Pegasus.Core;

public sealed record CursorPage<T>(IReadOnlyList<T> Items, string? NextCursor);

public static class CursorPaging
{
    public const int DefaultLimit = 50;
    public const int MaximumLimit = 100;

    public static int NormalizeLimit(int? limit)
    {
        var value = limit ?? DefaultLimit;
        if (value is < 1 or > MaximumLimit)
        {
            throw new ArgumentOutOfRangeException(nameof(limit),
                $"The page limit must be between 1 and {MaximumLimit}.");
        }
        return value;
    }

    public static string CreateScope(
        string query, ActionActor actor, params string?[] filtersAndOrder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(filtersAndOrder);
        return JsonSerializer.Serialize(new Scope(
            query, actor.Kind.ToString(), actor.SubjectId, filtersAndOrder));
    }

    public static string EncodeUtcTimestamp(DateTimeOffset value) =>
        value.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture);

    public static DateTimeOffset DecodeUtcTimestamp(string value)
    {
        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var ticks))
            throw new CursorRejectedException();
        try
        {
            return new DateTimeOffset(ticks, TimeSpan.Zero);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new CursorRejectedException(exception);
        }
    }

    private sealed record Scope(
        string Query, string ActorKind, string ActorSubjectId,
        IReadOnlyList<string?> FiltersAndOrder);
}

public sealed class CursorRejectedException : InvalidOperationException
{
    public CursorRejectedException() : base("The cursor is invalid or no longer applies to this query.") { }

    public CursorRejectedException(string reason) : base(reason) { }

    public CursorRejectedException(Exception innerException)
        : base("The cursor is invalid or no longer applies to this query.", innerException) { }
}

public interface ICursorProtector
{
    string Protect(string scope, string sortKey, Guid id);
    (string SortKey, Guid Id) Unprotect(string cursor, string scope);
}
