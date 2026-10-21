using System.Globalization;
using System.Text;

namespace Pegasus.Infrastructure.Cazana;

/// <summary>
/// The part of mustache.js Cazana's Companion Report templates use, so the
/// report renders here as it renders in Companion (ADR-0079): escaped
/// variables, sections, inverted sections, comments and partials. A view is
/// built from dictionaries, lists, strings, booleans, whole numbers, decimals
/// and nulls; truthiness and name lookup follow JavaScript and mustache.js.
///
/// <para>
/// A partial is inserted as it stands: Companion hands its sections over
/// already rendered. Triple mustaches, ampersand variables and delimiter
/// changes appear in none of the templates and are refused rather than
/// guessed.
/// </para>
///
/// <para>
/// A template is parsed once, names split into their segments as it is, and
/// rendered as often as it is needed.
/// </para>
/// </summary>
internal static class CazanaMustache
{
    public static string Render(Template template, object? view, IReadOnlyDictionary<string, string>? partials = null)
    {
        ArgumentNullException.ThrowIfNull(template);
        var output = new StringBuilder(template.Length);
        RenderNodes(template.Nodes, [view], partials, output);
        return output.ToString();
    }

    public static Template Parse(string template)
    {
        ArgumentNullException.ThrowIfNull(template);
        return new Template(template.Length, ParseNodes(template));
    }

    /// <summary>A name as the segments a lookup walks; the bare dot names the current frame.</summary>
    private static string[] PathOf(string name) => name == "." ? [] : name.Split('.');

    private static List<Node> ParseNodes(string template)
    {
        var root = new List<Node>();
        var open = new Stack<(string Name, bool Inverted, List<Node> Children, List<Node> Parent)>();
        var current = root;
        var position = 0;
        while (position < template.Length)
        {
            var start = template.IndexOf("{{", position, StringComparison.Ordinal);
            if (start < 0)
            {
                current.Add(new Text(template[position..]));
                break;
            }

            if (start > position)
            {
                current.Add(new Text(template[position..start]));
            }

            var end = template.IndexOf("}}", start + 2, StringComparison.Ordinal);
            if (end < 0)
            {
                throw new FormatException($"The tag at {start} is not closed.");
            }

            var tag = template[(start + 2)..end];
            position = end + 2;
            var sigil = tag.Length > 0 ? tag[0] : '\0';
            switch (sigil)
            {
                case '!':
                    break;
                case '{' or '&' or '=':
                    throw new FormatException($"The tag '{{{{{tag}}}}}' is not supported.");
                case '#' or '^':
                    var children = new List<Node>();
                    open.Push((tag[1..].Trim(), sigil == '^', children, current));
                    current = children;
                    break;
                case '/':
                    var name = tag[1..].Trim();
                    if (open.Count == 0 || open.Peek().Name != name)
                    {
                        throw new FormatException($"The section close '{name}' matches no open section.");
                    }

                    var section = open.Pop();
                    current = section.Parent;
                    current.Add(new Section(PathOf(section.Name), section.Inverted, section.Children));
                    break;
                case '>':
                    current.Add(new Partial(tag[1..].Trim()));
                    break;
                default:
                    current.Add(new Variable(PathOf(tag.Trim())));
                    break;
            }
        }

        if (open.Count > 0)
        {
            throw new FormatException($"The section '{open.Peek().Name}' is not closed.");
        }

        return root;
    }

    private static void RenderNodes(
        IReadOnlyList<Node> nodes,
        List<object?> frames,
        IReadOnlyDictionary<string, string>? partials,
        StringBuilder output)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case Text text:
                    output.Append(text.Value);
                    break;
                case Variable variable:
                    AppendEscaped(output, Print(Lookup(frames, variable.Path)));
                    break;
                case Partial partial:
                    if (partials is not null && partials.TryGetValue(partial.Name, out var html))
                    {
                        output.Append(html);
                    }

                    break;
                case Section { Inverted: true } inverted:
                    if (!Truthy(Lookup(frames, inverted.Path)))
                    {
                        RenderNodes(inverted.Children, frames, partials, output);
                    }

                    break;
                case Section section:
                    var value = Lookup(frames, section.Path);
                    if (!Truthy(value))
                    {
                        break;
                    }

                    if (value is IReadOnlyList<object?> items)
                    {
                        foreach (var item in items)
                        {
                            RenderPushed(section.Children, frames, item, partials, output);
                        }
                    }
                    else if (value is bool)
                    {
                        // mustache.js renders a true boolean in the context it found it.
                        RenderNodes(section.Children, frames, partials, output);
                    }
                    else
                    {
                        RenderPushed(section.Children, frames, value, partials, output);
                    }

                    break;
            }
        }
    }

    private static void RenderPushed(
        IReadOnlyList<Node> nodes,
        List<object?> frames,
        object? frame,
        IReadOnlyDictionary<string, string>? partials,
        StringBuilder output)
    {
        frames.Add(frame);
        RenderNodes(nodes, frames, partials, output);
        frames.RemoveAt(frames.Count - 1);
    }

    /// <summary>
    /// The nearest frame whose first name segment resolves answers the whole
    /// name; later segments that do not resolve answer nothing.
    /// </summary>
    private static object? Lookup(List<object?> frames, string[] path)
    {
        if (path.Length == 0)
        {
            return frames[^1];
        }

        for (var index = frames.Count - 1; index >= 0; index--)
        {
            if (!TryResolve(frames[index], path[0], out var value))
            {
                continue;
            }

            for (var segment = 1; segment < path.Length; segment++)
            {
                if (!TryResolve(value, path[segment], out value))
                {
                    return null;
                }
            }

            return value;
        }

        return null;
    }

    private static bool TryResolve(object? frame, string key, out object? value)
    {
        switch (frame)
        {
            case IReadOnlyDictionary<string, object?> map:
                return map.TryGetValue(key, out value);
            case string text when key == "length":
                value = text.Length;
                return true;
            case IReadOnlyList<object?> list when key == "length":
                value = list.Count;
                return true;
            default:
                value = null;
                return false;
        }
    }

    private static bool Truthy(object? value) => value switch
    {
        null => false,
        bool flag => flag,
        string text => text.Length > 0,
        int number => number != 0,
        long number => number != 0,
        decimal number => number != 0,
        IReadOnlyList<object?> list => list.Count > 0,
        _ => true,
    };

    private static string Print(object? value) => value switch
    {
        null => "",
        string text => text,
        bool flag => flag ? "true" : "false",
        int number => number.ToString(CultureInfo.InvariantCulture),
        long number => number.ToString(CultureInfo.InvariantCulture),
        decimal number => number.ToString(CultureInfo.InvariantCulture),
        _ => throw new FormatException($"A {value.GetType().Name} cannot be printed by a template variable."),
    };

    /// <summary>mustache.js's own escape table.</summary>
    private static void AppendEscaped(StringBuilder output, string text)
    {
        foreach (var character in text)
        {
            var entity = character switch
            {
                '&' => "&amp;",
                '<' => "&lt;",
                '>' => "&gt;",
                '"' => "&quot;",
                '\'' => "&#39;",
                '/' => "&#x2F;",
                '`' => "&#x60;",
                '=' => "&#x3D;",
                _ => null,
            };
            if (entity is null)
            {
                output.Append(character);
            }
            else
            {
                output.Append(entity);
            }
        }
    }

    /// <summary>A template parsed once, to be rendered with any view.</summary>
    internal sealed record Template(int Length, IReadOnlyList<Node> Nodes);

    internal abstract record Node;

    private sealed record Text(string Value) : Node;

    private sealed record Variable(string[] Path) : Node;

    private sealed record Partial(string Name) : Node;

    private sealed record Section(string[] Path, bool Inverted, IReadOnlyList<Node> Children) : Node;
}
