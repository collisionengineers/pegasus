using System.Text.Json;

namespace Pegasus.Infrastructure.Brego;

/// <summary>
/// Reading Brego's answers without trusting their shape: a path that meets
/// anything but an object at any step answers nothing rather than throwing.
/// </summary>
internal static class BregoJson
{
    public static JsonElement? Get(JsonElement? element, params ReadOnlySpan<string> path)
    {
        var current = element;
        foreach (var name in path)
        {
            if (current is not { ValueKind: JsonValueKind.Object } value || !value.TryGetProperty(name, out var next))
            {
                return null;
            }

            current = next;
        }

        return current;
    }

    public static string? String(JsonElement? element, params ReadOnlySpan<string> path) =>
        Get(element, path) is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;

    public static int? Int32(JsonElement? element, params ReadOnlySpan<string> path) =>
        Get(element, path) is { ValueKind: JsonValueKind.Number } value && value.TryGetInt32(out var number)
            ? number
            : null;

    public static decimal? Decimal(JsonElement? element, params ReadOnlySpan<string> path) =>
        Get(element, path) is { ValueKind: JsonValueKind.Number } value && value.TryGetDecimal(out var number)
            ? number
            : null;

    public static bool? Boolean(JsonElement? element, params ReadOnlySpan<string> path) =>
        Get(element, path) switch
        {
            { ValueKind: JsonValueKind.True } => true,
            { ValueKind: JsonValueKind.False } => false,
            _ => null,
        };

    /// <summary>
    /// The array at the path, walked by the struct enumerator itself; a path
    /// that meets no array answers a default enumerator, which yields nothing.
    /// </summary>
    public static JsonElement.ArrayEnumerator Items(JsonElement? element, params ReadOnlySpan<string> path) =>
        Get(element, path) is { ValueKind: JsonValueKind.Array } items ? items.EnumerateArray() : default;

    /// <summary>The first item of the array at the path, or none.</summary>
    public static JsonElement? First(JsonElement? element, params ReadOnlySpan<string> path)
    {
        var items = Items(element, path);
        return items.MoveNext() ? items.Current : null;
    }
}
