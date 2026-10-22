using System.Text.Json;

namespace Pegasus.Infrastructure.Cazana;

/// <summary>
/// Reading Cazana's answers without trusting their shape: a path that meets
/// anything but an object at any step answers nothing rather than throwing.
/// </summary>
internal static class CazanaJson
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

    /// <summary>
    /// The array at the path, walked by the struct enumerator itself (boxed by
    /// no interface, no iterator on top); a path that meets no array answers a
    /// default enumerator, which yields nothing.
    /// </summary>
    public static JsonElement.ArrayEnumerator Items(JsonElement? element, params ReadOnlySpan<string> path) =>
        Get(element, path) is { ValueKind: JsonValueKind.Array } items ? items.EnumerateArray() : default;

    /// <summary>How many items the array at the path holds; none when the path meets no array.</summary>
    public static int Length(JsonElement? element, params ReadOnlySpan<string> path) =>
        Get(element, path) is { ValueKind: JsonValueKind.Array } items ? items.GetArrayLength() : 0;

    /// <summary>The first item of the array at the path, or none.</summary>
    public static JsonElement? First(JsonElement? element, params ReadOnlySpan<string> path)
    {
        var items = Items(element, path);
        return items.MoveNext() ? items.Current : null;
    }
}
