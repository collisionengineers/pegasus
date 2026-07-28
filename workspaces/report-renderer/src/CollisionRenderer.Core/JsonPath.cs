using System.Text.Json;
using System.Text.Json.Nodes;

namespace CollisionRenderer.Core;

/// <summary>
/// Shared engine for the dotted/indexed JSON paths the document forms use — for example
/// <c>sections[1].blocks[0].media[0].imagePath</c>. Parses a path into segments, reads a
/// value (<see cref="Navigate"/>), or assigns one (<see cref="Set"/>), creating intermediate
/// objects and arrays to fit the path as it goes. Mutation is deliberately lenient: a path
/// that does not fit the current shape is a silent no-op rather than an exception, because
/// callers feed partial drafts and form-defined paths that can outrun the data present.
/// </summary>
public static class JsonPath
{
    /// <summary>One step of a path: either a <paramref name="Property"/> name or an array <paramref name="Index"/>.</summary>
    public readonly record struct Segment(string? Property, int? Index);

    /// <summary>
    /// Split a dotted/indexed path into segments. The <c>$</c> token (the form builder uses it
    /// to mean "this whole repeater item") is skipped; a malformed bracket ends parsing there
    /// and returns what was parsed so far.
    /// </summary>
    public static IReadOnlyList<Segment> Parse(string path)
    {
        var result = new List<Segment>();
        foreach (var part in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part == "$")
            {
                continue;
            }

            var bracket = part.IndexOf('[');
            if (bracket < 0)
            {
                result.Add(new Segment(part, null));
                continue;
            }

            if (bracket > 0)
            {
                result.Add(new Segment(part[..bracket], null));
            }

            var rest = part[bracket..];
            while (rest.StartsWith('['))
            {
                var close = rest.IndexOf(']');
                if (close < 0 || !int.TryParse(rest[1..close], out var index))
                {
                    return result;
                }

                result.Add(new Segment(null, index));
                rest = rest[(close + 1)..];
            }
        }

        return result;
    }

    /// <summary>
    /// Assign <paramref name="value"/> at <paramref name="path"/> under <paramref name="root"/>,
    /// creating intermediate objects/arrays to fit the next segment. A path that can't be
    /// followed (e.g. a property step where the current node is not an object) is a silent
    /// no-op. The value is coerced through <see cref="ToNode"/>.
    /// </summary>
    public static void Set(JsonNode root, string path, object? value)
    {
        var segments = Parse(path);
        if (segments.Count == 0)
        {
            return;
        }

        JsonNode current = root;
        for (var i = 0; i < segments.Count - 1; i++)
        {
            var segment = segments[i];
            var next = segments[i + 1];

            if (segment.Property is not null)
            {
                if (current is not JsonObject obj)
                {
                    return;
                }

                var child = obj[segment.Property];
                if (child is null
                    || (next.Index is not null && child is not JsonArray)
                    || (next.Property is not null && child is not JsonObject))
                {
                    child = next.Index is not null ? new JsonArray() : new JsonObject();
                    obj[segment.Property] = child;
                }

                current = child;
            }
            else
            {
                if (current is not JsonArray arr || segment.Index is not { } idx)
                {
                    return;
                }

                EnsureSize(arr, idx + 1);
                var child = arr[idx];
                if (child is null
                    || (next.Index is not null && child is not JsonArray)
                    || (next.Property is not null && child is not JsonObject))
                {
                    child = next.Index is not null ? new JsonArray() : new JsonObject();
                    arr[idx] = child;
                }

                current = child;
            }
        }

        var last = segments[^1];
        var node = ToNode(value);
        if (last.Property is not null && current is JsonObject parentObj)
        {
            parentObj[last.Property] = node;
        }
        else if (last.Index is { } lastIdx && current is JsonArray parentArr)
        {
            EnsureSize(parentArr, lastIdx + 1);
            parentArr[lastIdx] = node;
        }
    }

    /// <summary>Read the node at <paramref name="path"/>, or null if any step is missing or out of range.</summary>
    public static JsonNode? Navigate(JsonNode? root, string path)
    {
        var current = root;
        foreach (var segment in Parse(path))
        {
            if (current is null)
            {
                return null;
            }

            current = segment.Property is not null
                ? (current as JsonObject)?[segment.Property]
                : current is JsonArray arr && segment.Index is { } i && i >= 0 && i < arr.Count
                    ? arr[i]
                    : null;
        }

        return current;
    }

    /// <summary>Join a parent path with a child path; the child token <c>$</c> means "the parent itself".</summary>
    public static string Combine(string prefix, string childPath) =>
        childPath == "$" ? prefix : $"{prefix}.{childPath}";

    /// <summary>
    /// Convert a CLR value into the node stored at a leaf. An existing <see cref="JsonNode"/> is
    /// deep-cloned so a caller can't alias the same node into two trees; everything else maps to
    /// a <see cref="JsonValue"/> (with a serializer fallback for complex objects).
    /// </summary>
    public static JsonNode? ToNode(object? value) => value switch
    {
        null => null,
        JsonNode node => node.DeepClone(),
        bool b => JsonValue.Create(b),
        decimal d => JsonValue.Create(d),
        double d => JsonValue.Create(d),
        int i => JsonValue.Create(i),
        string s => JsonValue.Create(s),
        _ => JsonSerializer.SerializeToNode(value),
    };

    private static void EnsureSize(JsonArray array, int size)
    {
        while (array.Count < size)
        {
            array.Add(null);
        }
    }
}
