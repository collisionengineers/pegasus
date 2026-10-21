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

    public static IEnumerable<JsonElement> Items(JsonElement? element, params ReadOnlySpan<string> path) =>
        Get(element, path) is { ValueKind: JsonValueKind.Array } items ? items.EnumerateArray() : [];
}
