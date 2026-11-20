using System.Collections.ObjectModel;
using System.Globalization;
using System.Net;
using System.Text;

namespace Pegasus.Infrastructure.Glass;

/// <summary>What one piece of an editor page is: a start tag, an end tag or text.</summary>
internal enum GlassEditorTokenKind
{
    Start,
    End,
    Text,
}

/// <summary>
/// One piece of an editor page. A tag's name and its attribute names are in
/// lower case, its attribute values and any text are entity-decoded, and the
/// content of a script or style element is not read at all.
/// </summary>
internal sealed record GlassEditorToken(
    GlassEditorTokenKind Kind, string Name, IReadOnlyDictionary<string, string> Attributes, string Text)
{
    public string? Attribute(string name) => Attributes.GetValueOrDefault(name);
}

/// <summary>One option of a select, as the page serves it; its selection may be changed.</summary>
internal sealed class GlassEditorOption(string value, string text, bool selected, bool disabled)
{
    /// <summary>The value attribute, or the option's text when it has none.</summary>
    public string Value { get; } = value;

    /// <summary>The option's text with its white space collapsed, as a browser reports it.</summary>
    public string Text { get; } = text;

    public bool Selected { get; set; } = selected;

    public bool Disabled { get; } = disabled;
}

/// <summary>
/// One control of a form: an input, a select, a text area or a button. Its
/// value, tick and disabled state may be changed before the form's fields are
/// taken, as the editor's own scripts change them in a browser.
/// </summary>
internal sealed class GlassEditorControl
{
    private string current;

    public GlassEditorControl(
        string tag, string type, IReadOnlyDictionary<string, string> attributes, string value,
        IReadOnlyList<GlassEditorOption> options)
    {
        Tag = tag;
        Type = type;
        Attributes = attributes;
        current = value;
        Options = options;
        Checked = attributes.ContainsKey("checked");
        Disabled = attributes.ContainsKey("disabled");
    }

    /// <summary><c>input</c>, <c>select</c>, <c>textarea</c> or <c>button</c>.</summary>
    public string Tag { get; }

    /// <summary>
    /// The control's type as a browser reports it: an input's own type in
    /// lower case (<c>text</c> when it has none or one the browser does not
    /// know), <c>select-one</c> or <c>select-multiple</c>, <c>textarea</c>,
    /// or a button's <c>submit</c>, <c>reset</c> or <c>button</c>.
    /// </summary>
    public string Type { get; }

    public IReadOnlyDictionary<string, string> Attributes { get; }

    public string? Name => Attributes.GetValueOrDefault("name");

    public string? Id => Attributes.GetValueOrDefault("id");

    public bool Checked { get; set; }

    public bool Disabled { get; set; }

    public bool ReadOnly => Tag is "input" or "textarea" && Attributes.ContainsKey("readonly");

    /// <summary>A select's options in document order; empty for any other control.</summary>
    public IReadOnlyList<GlassEditorOption> Options { get; }

    public bool IsSelect => Tag == "select";

    /// <summary>The first selected option's position, or -1 when none is selected.</summary>
    public int SelectedIndex
    {
        get
        {
            for (var index = 0; index < Options.Count; index++)
            {
                if (Options[index].Selected)
                {
                    return index;
                }
            }

            return -1;
        }
    }

    /// <summary>
    /// The control's value. A select's is its first selected option's value
    /// (empty when none is); setting it selects the first option of that
    /// value and no other, as a browser does.
    /// </summary>
    public string Value
    {
        get
        {
            if (!IsSelect)
            {
                return current;
            }

            var selected = SelectedIndex;
            return selected < 0 ? string.Empty : Options[selected].Value;
        }
        set
        {
            if (!IsSelect)
            {
                current = value;
                return;
            }

            var found = false;
            foreach (var option in Options)
            {
                option.Selected = !found && option.Value == value;
                found |= option.Selected;
            }
        }
    }

    /// <summary>A control a browser posts only when it is the button pressed.</summary>
    public bool IsButton => Tag == "button" || Type is "submit" or "reset" or "button" or "image";
}

/// <summary>
/// One named form of an editor page, read by hand: no HTML library exists in
/// this repository, and the editor's pages need only what a browser's form
/// does with its controls. The reader is tolerant of what the editor serves
/// (options without closing tags, values over many lines, attributes quoted
/// either way or not at all) and keeps controls in document order.
/// </summary>
internal sealed class GlassEditorForm
{
    /// <summary>Input types a browser knows; any other reads as <c>text</c>.</summary>
    private static readonly HashSet<string> InputTypes = new(StringComparer.Ordinal)
    {
        "hidden", "text", "search", "tel", "url", "email", "password", "date", "month", "week", "time",
        "datetime-local", "number", "range", "color", "checkbox", "radio", "file", "submit", "image",
        "reset", "button",
    };

    /// <summary>Elements whose content is raw text: nothing inside them is a tag.</summary>
    private static readonly HashSet<string> RawText = new(StringComparer.Ordinal) { "script", "style" };

    /// <summary>Elements whose content is text with character references but no tags.</summary>
    private static readonly HashSet<string> EscapableText = new(StringComparer.Ordinal) { "textarea", "title" };

    /// <summary>Start tags that close an open select, as a browser's parser closes it.</summary>
    private static readonly HashSet<string> SelectClosers = new(StringComparer.Ordinal)
    {
        "select", "input", "textarea", "keygen",
    };

    private GlassEditorForm(string? action, IReadOnlyList<GlassEditorControl> controls)
    {
        Action = action;
        Controls = controls;
    }

    /// <summary>The form's action attribute as the page states it, or null when it has none.</summary>
    public string? Action { get; }

    /// <summary>The form's controls in document order, image inputs excepted.</summary>
    public IReadOnlyList<GlassEditorControl> Controls { get; }

    /// <summary>
    /// The first form whose name or id is <paramref name="name"/>, with the
    /// controls between its start tag and its end tag, or null when the page
    /// has no such form.
    /// </summary>
    public static GlassEditorForm? Read(string html, string name)
    {
        var tokens = Tokens(html);
        var start = tokens.FindIndex(token => token.Kind == GlassEditorTokenKind.Start
            && token.Name == "form"
            && (token.Attribute("name") == name || token.Attribute("id") == name));
        if (start < 0)
        {
            return null;
        }

        var controls = new List<GlassEditorControl>();
        for (var index = start + 1; index < tokens.Count; index++)
        {
            var token = tokens[index];
            if (token.Kind == GlassEditorTokenKind.End && token.Name == "form")
            {
                break;
            }
            if (token.Kind != GlassEditorTokenKind.Start)
            {
                continue;
            }

            switch (token.Name)
            {
                case "input":
                    var type = InputType(token);
                    if (type != "image")
                    {
                        controls.Add(new("input", type, token.Attributes, InputValue(token, type), []));
                    }
                    break;
                case "button":
                    var stated = token.Attribute("type")?.Trim().ToLowerInvariant();
                    controls.Add(new("button", stated is "reset" or "button" ? stated : "submit",
                        token.Attributes, token.Attribute("value") ?? string.Empty, []));
                    break;
                case "textarea":
                    var content = index + 1 < tokens.Count && tokens[index + 1].Kind == GlassEditorTokenKind.Text
                        ? tokens[index + 1].Text
                        : string.Empty;
                    controls.Add(new("textarea", "textarea", token.Attributes,
                        content.StartsWith('\n') ? content[1..] : content, []));
                    break;
                case "select":
                    controls.Add(Select(tokens, ref index));
                    break;
            }
        }

        return new(tokens[start].Attribute("action"), controls);
    }

    /// <summary>
    /// The one control named or identified <paramref name="name"/>, or null
    /// when there is none or more than one (a browser's
    /// <c>form.elements.namedItem</c> answers a list then, which the editor's
    /// builders do not use as a field).
    /// </summary>
    public GlassEditorControl? Field(string name)
    {
        var found = Named(name).Take(2).ToList();
        return found.Count == 1 ? found[0] : null;
    }

    /// <summary>Every control whose name or id is <paramref name="name"/>.</summary>
    public IEnumerable<GlassEditorControl> Named(string name) =>
        Controls.Where(control => control.Name == name || control.Id == name);

    /// <summary>
    /// The fields a browser submits when <paramref name="submitter"/> is
    /// pressed, in document order: no disabled control, no button but the one
    /// pressed, a tick box or radio button only when ticked (as <c>on</c> when
    /// it has no value), a select's selected options, and nothing without a
    /// name.
    /// </summary>
    public GlassEditorFields Submit(GlassEditorControl? submitter)
    {
        var fields = new List<KeyValuePair<string, string>>();
        foreach (var control in Controls)
        {
            var name = control.Name;
            if (control.Disabled
                || (control.IsButton && !ReferenceEquals(control, submitter))
                || (control.Type is "checkbox" or "radio" && !control.Checked)
                || control.Type == "file"
                || string.IsNullOrEmpty(name))
            {
                continue;
            }

            if (control.IsSelect)
            {
                foreach (var option in control.Options)
                {
                    if (option.Selected && !option.Disabled)
                    {
                        fields.Add(KeyValuePair.Create(name, option.Value));
                    }
                }
                continue;
            }

            fields.Add(KeyValuePair.Create(name, control.Value));
        }

        return new(fields);
    }

    /// <summary>
    /// The page as a browser's tokeniser reads it, simplified to what the
    /// editor's pages use: line ends normalised to a line feed, comments,
    /// doctypes and processing instructions dropped, script and style content
    /// skipped, and text and attribute values entity-decoded.
    /// </summary>
    public static List<GlassEditorToken> Tokens(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        var source = html.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var tokens = new List<GlassEditorToken>();
        var text = new StringBuilder();
        var index = 0;
        while (index < source.Length)
        {
            var character = source[index];
            var next = index + 1 < source.Length ? source[index + 1] : '\0';
            if (character != '<' || !(char.IsAsciiLetter(next) || next is '/' or '!' or '?'))
            {
                text.Append(character);
                index++;
                continue;
            }

            if (next == '/' && !(index + 2 < source.Length && char.IsAsciiLetter(source[index + 2])))
            {
                // "</" not followed by a letter is a bogus comment to the next ">".
                Flush(tokens, text);
                index = After(source, '>', index + 2);
                continue;
            }

            Flush(tokens, text);
            if (next == '!' && string.CompareOrdinal(source, index, "<!--", 0, 4) == 0)
            {
                var close = source.IndexOf("-->", index + 4, StringComparison.Ordinal);
                index = close < 0 ? source.Length : close + 3;
                continue;
            }
            if (next is '!' or '?')
            {
                index = After(source, '>', index + 2);
                continue;
            }

            var end = next == '/';
            var (name, attributes, position) = Tag(source, index + (end ? 2 : 1));
            tokens.Add(new(end ? GlassEditorTokenKind.End : GlassEditorTokenKind.Start, name, attributes, string.Empty));
            index = position;
            if (end || !(RawText.Contains(name) || EscapableText.Contains(name)))
            {
                continue;
            }

            var closing = ClosingTag(source, name, index);
            if (EscapableText.Contains(name))
            {
                tokens.Add(new(GlassEditorTokenKind.Text, string.Empty, Empty, WebUtility.HtmlDecode(source[index..closing])));
            }

            index = closing;
        }

        Flush(tokens, text);
        return tokens;
    }

    private static readonly ReadOnlyDictionary<string, string> Empty = new Dictionary<string, string>(StringComparer.Ordinal).AsReadOnly();

    private static void Flush(List<GlassEditorToken> tokens, StringBuilder text)
    {
        if (text.Length == 0)
        {
            return;
        }

        tokens.Add(new(GlassEditorTokenKind.Text, string.Empty, Empty, WebUtility.HtmlDecode(text.ToString())));
        text.Clear();
    }

    private static int After(string source, char character, int from)
    {
        var at = source.IndexOf(character, Math.Min(from, source.Length));
        return at < 0 ? source.Length : at + 1;
    }

    /// <summary>Where the end tag of a raw or escapable text element starts, or the page's end.</summary>
    private static int ClosingTag(string source, string name, int from)
    {
        var marker = "</" + name;
        for (var at = source.IndexOf(marker, from, StringComparison.OrdinalIgnoreCase);
             at >= 0;
             at = source.IndexOf(marker, at + 1, StringComparison.OrdinalIgnoreCase))
        {
            var following = at + marker.Length < source.Length ? source[at + marker.Length] : '>';
            if (char.IsWhiteSpace(following) || following is '/' or '>')
            {
                return at;
            }
        }

        return source.Length;
    }

    /// <summary>
    /// One tag from its name to its closing angle bracket. Attribute names
    /// are case-insensitive and may stand alone; values may be quoted either
    /// way, over many lines, or not quoted at all. A repeated attribute keeps
    /// its first value, as a browser keeps it.
    /// </summary>
    private static (string Name, IReadOnlyDictionary<string, string> Attributes, int Position) Tag(
        string source, int position)
    {
        var start = position;
        while (position < source.Length && !char.IsWhiteSpace(source[position]) && source[position] is not '/' and not '>')
        {
            position++;
        }

        var name = source[start..position].ToLowerInvariant();
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
        while (position < source.Length)
        {
            var character = source[position];
            if (character == '>')
            {
                return (name, attributes, position + 1);
            }
            if (char.IsWhiteSpace(character) || character == '/')
            {
                position++;
                continue;
            }

            var nameStart = position++;
            while (position < source.Length && !char.IsWhiteSpace(source[position]) && source[position] is not '/' and not '>' and not '=')
            {
                position++;
            }

            var attribute = source[nameStart..position].ToLowerInvariant();
            while (position < source.Length && char.IsWhiteSpace(source[position]))
            {
                position++;
            }

            var value = string.Empty;
            if (position < source.Length && source[position] == '=')
            {
                position++;
                while (position < source.Length && char.IsWhiteSpace(source[position]))
                {
                    position++;
                }

                if (position < source.Length && source[position] is '"' or '\'')
                {
                    var close = source.IndexOf(source[position], position + 1);
                    close = close < 0 ? source.Length : close;
                    value = source[(position + 1)..close];
                    position = Math.Min(close + 1, source.Length);
                }
                else
                {
                    var valueStart = position;
                    while (position < source.Length && !char.IsWhiteSpace(source[position]) && source[position] != '>')
                    {
                        position++;
                    }

                    value = source[valueStart..position];
                }
            }

            _ = attributes.TryAdd(attribute, WebUtility.HtmlDecode(value));
        }

        return (name, attributes, position);
    }

    private static string InputType(GlassEditorToken token)
    {
        var type = token.Attribute("type")?.Trim().ToLowerInvariant();
        return type is not null && InputTypes.Contains(type) ? type : "text";
    }

    /// <summary>
    /// An input's value as a browser holds it: a tick box or radio button
    /// without a value is <c>on</c>, a one-line text box drops line breaks,
    /// and a hidden field keeps its value exactly, over as many lines as it has.
    /// </summary>
    private static string InputValue(GlassEditorToken token, string type)
    {
        var value = token.Attribute("value");
        if (type is "checkbox" or "radio")
        {
            return value ?? "on";
        }
        if (value is null)
        {
            return string.Empty;
        }
        if (type is "text" or "search" or "tel" or "password" or "url" or "email")
        {
            value = value.Replace("\n", string.Empty, StringComparison.Ordinal);
        }

        return type is "url" or "email" ? value.Trim(' ', '\t', '\n', '\f') : value;
    }

    /// <summary>
    /// A select and its options, which end at the next option, at the
    /// select's end tag, or where a browser's parser would close the select.
    /// A single-choice select keeps only its last option marked selected, or
    /// selects its first enabled option when none is marked, as a browser does.
    /// </summary>
    private static GlassEditorControl Select(List<GlassEditorToken> tokens, ref int index)
    {
        var select = tokens[index];
        var options = new List<(IReadOnlyDictionary<string, string> Attributes, StringBuilder Text)>();
        var open = false;
        for (index++; index < tokens.Count; index++)
        {
            var token = tokens[index];
            if (token.Kind == GlassEditorTokenKind.Text)
            {
                if (open)
                {
                    options[^1].Text.Append(token.Text);
                }
                continue;
            }
            if (token.Kind == GlassEditorTokenKind.End && token.Name == "select")
            {
                break;
            }
            if ((token.Kind == GlassEditorTokenKind.End && token.Name == "form")
                || (token.Kind == GlassEditorTokenKind.Start && SelectClosers.Contains(token.Name)))
            {
                // Left for the form's own reading: the form's end, or a control
                // that closes the select before it is read.
                index--;
                break;
            }
            if (token.Kind == GlassEditorTokenKind.Start && token.Name == "option")
            {
                options.Add((token.Attributes, new StringBuilder()));
                open = true;
                continue;
            }
            if (token.Name is "option" or "optgroup")
            {
                // An option's text ends at its own end tag or at an optgroup.
                open = false;
            }
        }

        var built = options.Select(option =>
        {
            var text = Collapse(option.Text.ToString());
            return new GlassEditorOption(
                option.Attributes.GetValueOrDefault("value") ?? text,
                text,
                option.Attributes.ContainsKey("selected"),
                option.Attributes.ContainsKey("disabled"));
        }).ToList();

        var multiple = select.Attributes.ContainsKey("multiple");
        if (!multiple)
        {
            var last = built.FindLastIndex(option => option.Selected);
            for (var position = 0; position < built.Count; position++)
            {
                built[position].Selected = position == last;
            }

            var size = int.TryParse(select.Attribute("size"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var stated)
                && stated > 0 ? stated : 1;
            if (last < 0 && size == 1 && built.FirstOrDefault(option => !option.Disabled) is { } first)
            {
                first.Selected = true;
            }
        }

        return new("select", multiple ? "select-multiple" : "select-one", select.Attributes, string.Empty, built);
    }

    /// <summary>Text with ASCII white space stripped from both ends and collapsed to single spaces.</summary>
    internal static string Collapse(string text) =>
        string.Join(' ', text.Split(AsciiWhiteSpace, StringSplitOptions.RemoveEmptyEntries));

    private static readonly char[] AsciiWhiteSpace = [' ', '\t', '\n', '\f', '\r'];
}

/// <summary>
/// The fields a form submits, in order, with the operations a browser's
/// <c>FormData</c> offers: read the first value of a name, set a name (the
/// first entry takes the value and later ones of the same name go; a new
/// name goes at the end) and delete a name.
/// </summary>
internal sealed class GlassEditorFields
{
    private readonly List<KeyValuePair<string, string>> entries;

    public GlassEditorFields(List<KeyValuePair<string, string>> entries) => this.entries = [.. entries];

    public IReadOnlyList<KeyValuePair<string, string>> Entries => entries;

    public bool Has(string name) => entries.Exists(entry => entry.Key == name);

    public string? Get(string name)
    {
        var at = entries.FindIndex(entry => entry.Key == name);
        return at < 0 ? null : entries[at].Value;
    }

    public void Set(string name, string value)
    {
        var at = entries.FindIndex(entry => entry.Key == name);
        if (at < 0)
        {
            entries.Add(KeyValuePair.Create(name, value));
            return;
        }

        entries[at] = KeyValuePair.Create(name, value);
        for (var index = entries.Count - 1; index > at; index--)
        {
            if (entries[index].Key == name)
            {
                entries.RemoveAt(index);
            }
        }
    }

    public void Delete(string name) => entries.RemoveAll(entry => entry.Key == name);
}
