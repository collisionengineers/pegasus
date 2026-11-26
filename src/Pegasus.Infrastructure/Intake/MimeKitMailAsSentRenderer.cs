using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using MimeKit;
using MimeKit.Text;
using Pegasus.Core.Intake;

namespace Pegasus.Infrastructure.Intake;

/// <summary>
/// One e-mail's HTML as it was sent, as a sanitised, self-contained document:
/// the message <see cref="LocalEmailDisplayReader"/> shows, filtered through an
/// allow-list of structure and text-formatting elements, its own style sheets
/// kept, pictures carried in the message inlined and the messages quoted beneath
/// it cut away (they are drawn elsewhere as folded rows).
/// </summary>
/// <remarks>
/// Every element and attribute not on the allow-list is dropped; text and
/// attribute values are written encoded, never copied as markup. Links keep only
/// http, https and mailto targets; pictures keep only http and https sources or
/// a cid reference to an image part of the message.
/// </remarks>
public sealed partial class MimeKitMailAsSentRenderer : IRenderMailAsSent
{
    /// <summary>The longest HTML body, in characters, that is rendered.</summary>
    private const int MaximumHtmlLength = 5 * 1024 * 1024;

    /// <summary>The largest decoded picture inlined from the message.</summary>
    private const long MaximumInlineImageBytes = 8 * 1024 * 1024;

    /// <summary>How many blocks after a quoted header's start are searched for its Subject line.</summary>
    private const int HeaderSearchBlocks = 10;

    /// <summary>How many times a style sheet is cleaned before one that keeps changing is dropped.</summary>
    private const int MaximumStyleSheetPasses = 16;

    /// <summary>The deepest element nesting written; deeper start tags are dropped, their content kept.</summary>
    private const int MaximumNesting = 256;

    private const string StyleClose = "</style";

    private const string DocumentHead =
        "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><base target=\"_blank\"><style>"
        + "body{margin:0;padding:16px 20px;overflow-wrap:anywhere;"
        + "font-family:system-ui,-apple-system,\"Segoe UI\",Roboto,\"Helvetica Neue\",Arial,sans-serif}"
        + "img{max-width:100%;height:auto}"
        + "table{max-width:100%}"
        + "</style>";

    private static readonly HashSet<string> AllowedElements = new(StringComparer.Ordinal)
    {
        "a", "abbr", "address", "article", "aside", "b", "bdi", "bdo", "big", "blockquote", "br",
        "caption", "center", "cite", "code", "col", "colgroup", "dd", "del", "details", "dfn", "div",
        "dl", "dt", "em", "figcaption", "figure", "font", "footer", "h1", "h2", "h3", "h4", "h5", "h6",
        "header", "hr", "i", "img", "ins", "kbd", "li", "main", "mark", "nav", "ol", "p", "pre", "q",
        "s", "samp", "section", "small", "span", "strike", "strong", "sub", "summary", "sup", "table",
        "tbody", "td", "tfoot", "th", "thead", "tr", "tt", "u", "ul", "var", "wbr"
    };

    /// <summary>Allowed elements that never have content or an end tag.</summary>
    private static readonly HashSet<string> VoidElements = new(StringComparer.Ordinal)
    {
        "br", "col", "hr", "img", "wbr"
    };

    /// <summary>
    /// Elements dropped together with everything inside them. The void elements
    /// on the same rule (input, embed, source, track, frame) have no content and
    /// are dropped as any other element off the allow-list.
    /// </summary>
    private static readonly HashSet<string> DroppedWithContent = new(StringComparer.Ordinal)
    {
        "script", "noscript", "iframe", "frameset", "object", "applet", "form", "button", "select",
        "textarea", "option", "svg", "math", "audio", "video", "canvas", "template", "title"
    };

    /// <summary>Elements whose tags separate one block of text from the next.</summary>
    private static readonly HashSet<string> BlockElements = new(StringComparer.Ordinal)
    {
        "address", "article", "aside", "blockquote", "body", "br", "caption", "center", "dd", "details",
        "div", "dl", "dt", "figcaption", "figure", "footer", "h1", "h2", "h3", "h4", "h5", "h6", "header",
        "hr", "li", "main", "nav", "ol", "p", "pre", "section", "summary", "table", "tbody", "td", "tfoot",
        "th", "thead", "tr", "ul"
    };

    /// <summary>Presentational attributes kept as written (href and src are checked separately).</summary>
    private static readonly HashSet<string> KeptAttributes = new(StringComparer.Ordinal)
    {
        "style", "class", "id", "width", "height", "align", "valign", "bgcolor", "color", "face", "size",
        "border", "cellpadding", "cellspacing", "colspan", "rowspan", "dir", "lang", "title", "alt",
        "start", "type", "nowrap"
    };

    /// <summary>The elements whose type attribute is kept: the list marker style.</summary>
    private static readonly HashSet<string> ListElements = new(StringComparer.Ordinal)
    {
        "ol", "ul", "li"
    };

    private static readonly HashSet<string> InlineImageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/gif", "image/webp"
    };

    private static readonly char[] ClassSeparators = [' ', '\t', '\n', '\r', '\f'];

    public string Version => "s1";

    public MailAsSentRendering? Render(ReadOnlyMemory<byte> original, bool isStaffForward)
    {
        try
        {
            using var stream = OpenRead(original);
            using var message = MimeMessage.Load(stream);
            var (shown, isAttachedOriginal) = LocalEmailDisplayReader.ShownMessage(message);
            var html = shown.HtmlBody;
            if (string.IsNullOrWhiteSpace(html) || html.Length > MaximumHtmlLength)
            {
                return null;
            }

            var outline = new Outline(Tokenize(html));
            var (start, end) = isStaffForward && !isAttachedOriginal
                ? outline.ForwardedRange()
                : outline.OwnRange();
            return new MailAsSentRendering(Write(outline, start, end, new InlineImages(shown)));
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            return null;
        }
    }

    private static MemoryStream OpenRead(ReadOnlyMemory<byte> bytes) =>
        MemoryMarshal.TryGetArray(bytes, out var segment) && segment.Array is not null
            ? new MemoryStream(segment.Array, segment.Offset, segment.Count, writable: false)
            : new MemoryStream(bytes.ToArray(), writable: false);

    private static List<HtmlToken> Tokenize(string html)
    {
        var tokens = new List<HtmlToken>();
        using var reader = new StringReader(html);
        var tokenizer = new HtmlTokenizer(reader)
        {
            DecodeCharacterReferences = true,
            IgnoreTruncatedTags = true
        };
        while (tokenizer.ReadNextToken(out var token))
        {
            tokens.Add(token);
        }

        return tokens;
    }

    /// <summary>
    /// Writes the document: the fixed head, the e-mail's own style sheets, and
    /// the visible tokens in [<paramref name="start"/>, <paramref name="end"/>)
    /// through the allow-list, closing every element still open at the end.
    /// </summary>
    private static string Write(Outline outline, int start, int end, InlineImages images)
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        output.Write(DocumentHead);
        foreach (var sheet in outline.Styles)
        {
            var safe = SafeStyleSheet(sheet);
            if (!string.IsNullOrWhiteSpace(safe))
            {
                output.Write("<style>");
                output.Write(safe);
                output.Write("</style>");
            }
        }

        output.Write("</head><body>");
        using var writer = new HtmlWriter(output, leaveOpen: true);
        var open = new List<string>();
        for (var index = start; index < end; index++)
        {
            if (!outline.Visible[index])
            {
                continue;
            }

            if (outline.Tokens[index] is HtmlDataToken data)
            {
                writer.WriteText(data.Data);
            }
            else if (outline.Tokens[index] is HtmlTagToken tag && AllowedElements.Contains(TagName(tag)))
            {
                if (tag.IsEndTag)
                {
                    WriteEndTag(writer, open, TagName(tag));
                }
                else
                {
                    WriteStartTag(writer, open, tag, TagName(tag), images);
                }
            }
        }

        for (var depth = open.Count - 1; depth >= 0; depth--)
        {
            writer.WriteEndTag(open[depth]);
        }

        writer.Flush();
        output.Write("</body></html>");
        return output.ToString();
    }

    private static void WriteStartTag(
        HtmlWriter writer,
        List<string> open,
        HtmlTagToken tag,
        string name,
        InlineImages images)
    {
        string? source = null;
        if (name == "img")
        {
            source = tag.Attributes.TryGetValue("src", out var src) ? images.Source(src.Value) : null;
            if (source is null)
            {
                return;
            }
        }

        while (open.Count > 0 && IsClosedBy(open[^1], name))
        {
            writer.WriteEndTag(open[^1]);
            open.RemoveAt(open.Count - 1);
        }

        var isVoid = VoidElements.Contains(name);
        if (!isVoid && open.Count >= MaximumNesting)
        {
            // Deeper nesting than any real e-mail: the tag is dropped and its
            // content kept, which bounds the work an end tag costs.
            return;
        }

        writer.WriteStartTag(name);
        foreach (var attribute in tag.Attributes)
        {
            var attributeName = attribute.Name.ToLowerInvariant();
            if (!KeptAttributes.Contains(attributeName)
                || (attributeName == "type" && !ListElements.Contains(name)))
            {
                continue;
            }

            var value = attribute.Value ?? string.Empty;
            if (attributeName == "style")
            {
                value = WithoutUnsafeCss(value);
            }

            writer.WriteAttribute(attributeName, value);
        }

        if (source is not null)
        {
            writer.WriteAttribute("src", source);
        }
        else if (name == "a"
            && tag.Attributes.TryGetValue("href", out var href)
            && LinkTarget(href.Value) is { } target)
        {
            writer.WriteAttribute("href", target);
            writer.WriteAttribute("target", "_blank");
            writer.WriteAttribute("rel", "noopener noreferrer");
        }

        if (!isVoid)
        {
            open.Add(name);
        }
    }

    /// <summary>
    /// Closes <paramref name="name"/> and anything opened inside it; an end tag
    /// for an element that is not open is dropped.
    /// </summary>
    private static void WriteEndTag(HtmlWriter writer, List<string> open, string name)
    {
        var at = open.LastIndexOf(name);
        if (at < 0)
        {
            return;
        }

        for (var depth = open.Count - 1; depth >= at; depth--)
        {
            writer.WriteEndTag(open[depth]);
        }

        open.RemoveRange(at, open.Count - at);
    }

    /// <summary>The end tags HTML leaves out: a new paragraph, item or cell closes the open one.</summary>
    private static bool IsClosedBy(string open, string starting) => starting switch
    {
        "p" => open == "p",
        "li" => open == "li",
        "dt" or "dd" => open is "dt" or "dd",
        "td" or "th" => open is "td" or "th",
        "tr" => open is "td" or "th" or "tr",
        _ => false,
    };

    /// <summary>A link target kept only when it is an http, https or mailto URL.</summary>
    private static string? LinkTarget(string? value) =>
        CleanUrl(value) is { } url
            && (HasScheme(url, "http:") || HasScheme(url, "https:") || HasScheme(url, "mailto:"))
            ? url
            : null;

    /// <summary>
    /// The URL with its control characters removed, trimmed, and whitespace
    /// removed from its scheme; null when it has no scheme (a relative URL).
    /// </summary>
    private static string? CleanUrl(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var url = ControlCharacterRegex().Replace(value, string.Empty).Trim();
        var colon = url.IndexOf(':');
        if (colon <= 0)
        {
            return null;
        }

        return string.Concat(WhitespaceRegex().Replace(url[..colon], string.Empty), url.AsSpan(colon));
    }

    private static bool HasScheme(string url, string scheme) =>
        url.StartsWith(scheme, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A style sheet that cannot import another, run script in an old browser,
    /// or close the style element it is written into. Each cleaning can join the
    /// text around what it removes, so the sheet is cleaned until it stops
    /// changing; one that keeps changing is dropped.
    /// </summary>
    private static string SafeStyleSheet(string css)
    {
        for (var pass = 0; pass < MaximumStyleSheetPasses; pass++)
        {
            var cleaned = WithoutStyleClose(WithoutUnsafeCss(ImportRuleRegex().Replace(css, string.Empty)));
            if (cleaned.Length == css.Length)
            {
                return cleaned;
            }

            css = cleaned;
        }

        return string.Empty;
    }

    /// <summary>
    /// The CSS without any declaration (the text between two of ';', '{' and
    /// '}') that names a script or binding construct. The delimiters stay, so
    /// rule braces stay balanced.
    /// </summary>
    private static string WithoutUnsafeCss(string css)
    {
        if (!UnsafeCssRegex().IsMatch(css))
        {
            return css;
        }

        var kept = new StringBuilder(css.Length);
        var segmentStart = 0;
        for (var index = 0; index <= css.Length; index++)
        {
            if (index < css.Length && css[index] is not (';' or '{' or '}'))
            {
                continue;
            }

            var segment = css.AsSpan(segmentStart, index - segmentStart);
            if (!UnsafeCssRegex().IsMatch(segment))
            {
                kept.Append(segment);
            }

            if (index < css.Length)
            {
                kept.Append(css[index]);
            }

            segmentStart = index + 1;
        }

        return kept.ToString();
    }

    /// <summary>
    /// The text without any "&lt;/style" (in any case), including one formed
    /// by removing another.
    /// </summary>
    private static string WithoutStyleClose(string css)
    {
        if (!css.Contains(StyleClose, StringComparison.OrdinalIgnoreCase))
        {
            return css;
        }

        var kept = new StringBuilder(css.Length);
        foreach (var character in css)
        {
            kept.Append(character);
            if (EndsWithStyleClose(kept))
            {
                kept.Length -= StyleClose.Length;
            }
        }

        return kept.ToString();
    }

    private static bool EndsWithStyleClose(StringBuilder text)
    {
        if (text.Length < StyleClose.Length)
        {
            return false;
        }

        var offset = text.Length - StyleClose.Length;
        for (var index = 0; index < StyleClose.Length; index++)
        {
            if (char.ToLowerInvariant(text[offset + index]) != StyleClose[index])
            {
                return false;
            }
        }

        return true;
    }

    private static string TagName(HtmlTagToken tag) => tag.Name.ToLowerInvariant();

    private static string? AttributeValue(HtmlTagToken tag, string name) =>
        tag.Attributes.TryGetValue(name, out var attribute) ? attribute.Value : null;

    private static bool HasClass(string? classes, string name) =>
        classes is not null
        && Array.Exists(
            classes.Split(ClassSeparators, StringSplitOptions.RemoveEmptyEntries),
            item => string.Equals(item, name, StringComparison.OrdinalIgnoreCase));

    private static string Collapse(string text) => WhitespaceRegex().Replace(text, " ").Trim();

    /// <summary>One run of visible text between block tags, with the tokens it lies between.</summary>
    /// <param name="Text">The text, whitespace collapsed and trimmed; never empty.</param>
    /// <param name="StartIndex">The first token after the tag that started the block.</param>
    /// <param name="EndIndex">The tag that ended the block, or the token count at the end.</param>
    private readonly record struct Block(string Text, int StartIndex, int EndIndex);

    private enum BoundaryKind
    {
        /// <summary>An element that holds only the quoted message's header (Outlook's divRplyFwdMsg).</summary>
        HeaderContainer,

        /// <summary>An element that starts the quoted part and holds its header and text.</summary>
        Wrapper,

        /// <summary>A block whose text is a quoted header, found when no element marks one.</summary>
        Text,
    }

    /// <summary>The start of a quoted message: the token index it begins at.</summary>
    private readonly record struct Boundary(int Index, BoundaryKind Kind);

    /// <summary>
    /// The tokenised body read once: which tokens can be shown, the style
    /// sheets, the blocks of text and the quoted-message boundaries in
    /// document order.
    /// </summary>
    private sealed class Outline
    {
        private readonly List<Block> blocks = [];
        private readonly List<Boundary> boundaries;

        public Outline(List<HtmlToken> tokens)
        {
            Tokens = tokens;
            Visible = new bool[tokens.Count];
            var markers = new List<Boundary>();
            var borderDivs = new List<int>();
            var text = new StringBuilder();
            var blockStart = 0;
            string? skipped = null;
            var skipDepth = 0;
            StringBuilder? style = null;

            for (var index = 0; index < tokens.Count; index++)
            {
                var token = tokens[index];
                if (skipped is not null)
                {
                    if (token is HtmlTagToken inner && !inner.IsEmptyElement && TagName(inner) == skipped)
                    {
                        skipDepth += inner.IsEndTag ? -1 : 1;
                        if (skipDepth == 0)
                        {
                            skipped = null;
                        }
                    }

                    continue;
                }

                if (style is not null)
                {
                    if (token is HtmlTagToken { IsEndTag: true } close && TagName(close) == "style")
                    {
                        Styles.Add(style.ToString());
                        style = null;
                    }
                    else if (token is HtmlDataToken data)
                    {
                        style.Append(data.Data);
                    }

                    continue;
                }

                if (token is HtmlDataToken { Kind: HtmlTokenKind.Data } content)
                {
                    Visible[index] = true;
                    text.Append(content.Data);
                    continue;
                }

                // Comments (conditional ones included), doctypes, CDATA and
                // script data are never shown.
                if (token is not HtmlTagToken tag)
                {
                    continue;
                }

                var name = TagName(tag);
                if (!tag.IsEndTag && !tag.IsEmptyElement && DroppedWithContent.Contains(name))
                {
                    skipped = name;
                    skipDepth = 1;
                    continue;
                }

                if (name == "style")
                {
                    if (!tag.IsEndTag && !tag.IsEmptyElement)
                    {
                        style = new StringBuilder();
                    }

                    continue;
                }

                Visible[index] = true;
                if (BlockElements.Contains(name))
                {
                    AddBlock(text, blockStart, index);
                    blockStart = index + 1;
                }

                if (!tag.IsEndTag)
                {
                    if (MarkerKind(name, tag) is { } kind)
                    {
                        markers.Add(new Boundary(index, kind));
                    }
                    else if (name == "div" && IsBorderTopSeparator(tag))
                    {
                        borderDivs.Add(index);
                    }
                }
            }

            AddBlock(text, blockStart, tokens.Count);
            if (style is not null)
            {
                Styles.Add(style.ToString());
            }

            // Outlook desktop's separator is a top-bordered div whose text
            // starts with its From line: the first block of text after it.
            // The candidates are in document order, so one pass over the
            // blocks finds each one's first block.
            var first = 0;
            foreach (var candidate in borderDivs)
            {
                while (first < blocks.Count && blocks[first].StartIndex <= candidate)
                {
                    first++;
                }

                if (first < blocks.Count
                    && blocks[first].Text.StartsWith("From:", StringComparison.OrdinalIgnoreCase))
                {
                    markers.Add(new Boundary(candidate, BoundaryKind.Wrapper));
                }
            }

            markers.Sort((left, right) => left.Index.CompareTo(right.Index));
            if (markers.Count > 0)
            {
                boundaries = markers;
            }
            else
            {
                boundaries = [];
                for (var index = 0; index < blocks.Count; index++)
                {
                    if (IsQuotedHeaderText(index))
                    {
                        boundaries.Add(new Boundary(blocks[index].StartIndex, BoundaryKind.Text));
                    }
                }
            }
        }

        public List<HtmlToken> Tokens { get; }

        /// <summary>Tokens outside dropped elements and style sheets: text and tags.</summary>
        public bool[] Visible { get; }

        public List<string> Styles { get; } = [];

        /// <summary>The sender's own words: everything before the first quoted message.</summary>
        public (int Start, int End) OwnRange() =>
            boundaries.Count == 0
                ? (0, Tokens.Count)
                : (0, Backtrack(boundaries[0].Index, 0));

        /// <summary>
        /// An inline forward's forwarded message: from the end of the first
        /// quoted header to the next quoted message; the whole body when that
        /// header's end cannot be found.
        /// </summary>
        public (int Start, int End) ForwardedRange()
        {
            if (boundaries.Count == 0)
            {
                return (0, Tokens.Count);
            }

            var start = HeaderEnd(boundaries[0]);
            if (start < 0)
            {
                return (0, Tokens.Count);
            }

            var next = boundaries.FindIndex(boundary => boundary.Index >= start);
            return (start, next < 0 ? Tokens.Count : Backtrack(boundaries[next].Index, start));
        }

        private void AddBlock(StringBuilder text, int start, int end)
        {
            var collapsed = Collapse(text.ToString());
            text.Clear();
            if (collapsed.Length > 0)
            {
                blocks.Add(new Block(collapsed, start, end));
            }
        }

        /// <summary>The token the quoted message's own text starts at, or -1.</summary>
        private int HeaderEnd(Boundary boundary)
        {
            if (boundary.Kind == BoundaryKind.HeaderContainer)
            {
                var end = ElementEnd(boundary.Index);
                return end < 0 ? -1 : end + 1;
            }

            var first = blocks.FindIndex(block => block.StartIndex >= boundary.Index);
            if (first < 0)
            {
                return -1;
            }

            for (var index = first; index < blocks.Count && index < first + HeaderSearchBlocks; index++)
            {
                if (!blocks[index].Text.Contains("Subject:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Start at the tag that ended the Subject block when it opens
                // an element, so that element's content keeps its wrapper.
                var after = blocks[index].EndIndex;
                if (after >= Tokens.Count)
                {
                    return Tokens.Count;
                }

                return Tokens[after] is HtmlTagToken { IsEndTag: false } opening
                    && !VoidElements.Contains(TagName(opening))
                    ? after
                    : after + 1;
            }

            return -1;
        }

        /// <summary>
        /// Moves a cut back over what would be left empty immediately before
        /// it: whitespace, hidden tokens, rules, line breaks and wrappers opened
        /// but not yet holding anything.
        /// </summary>
        private int Backtrack(int cut, int floor)
        {
            while (cut > floor && IsEmptyLead(cut - 1))
            {
                cut--;
            }

            return cut;
        }

        private bool IsEmptyLead(int index) =>
            !Visible[index]
            || (Tokens[index] switch
            {
                HtmlDataToken data => string.IsNullOrWhiteSpace(data.Data),
                HtmlTagToken tag => !tag.IsEndTag && TagName(tag) != "img",
                _ => true,
            });

        /// <summary>The index of the end tag that closes the element started at <paramref name="start"/>, or -1.</summary>
        private int ElementEnd(int start)
        {
            if (Tokens[start] is not HtmlTagToken { IsEmptyElement: false } opening)
            {
                return -1;
            }

            var name = TagName(opening);
            var depth = 0;
            for (var index = start; index < Tokens.Count; index++)
            {
                if (!Visible[index]
                    || Tokens[index] is not HtmlTagToken tag
                    || tag.IsEmptyElement
                    || TagName(tag) != name)
                {
                    continue;
                }

                depth += tag.IsEndTag ? -1 : 1;
                if (depth == 0)
                {
                    return index;
                }
            }

            return -1;
        }

        /// <summary>
        /// Whether the block is one of the quoted-header shapes the plain-text
        /// splitter reads: a separator line, an "On … wrote:" line, or a From
        /// line with a Sent or Date line in the same or one of the next two blocks.
        /// </summary>
        private bool IsQuotedHeaderText(int index)
        {
            var text = blocks[index].Text;
            if (SeparatorRegex().IsMatch(text) || AttributionRegex().IsMatch(text))
            {
                return true;
            }

            if (!text.StartsWith("From:", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (text.IndexOf("Sent:", 5, StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("Date:", 5, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            for (var next = index + 1; next < blocks.Count && next <= index + 2; next++)
            {
                if (blocks[next].Text.StartsWith("Sent:", StringComparison.OrdinalIgnoreCase)
                    || blocks[next].Text.StartsWith("Date:", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static BoundaryKind? MarkerKind(string name, HtmlTagToken tag)
        {
            var id = AttributeValue(tag, "id");
            var classes = AttributeValue(tag, "class");
            if (string.Equals(id, "divRplyFwdMsg", StringComparison.OrdinalIgnoreCase)
                || HasClass(classes, "OutlookMessageHeader")
                || HasClass(classes, "moz-cite-prefix"))
            {
                return BoundaryKind.HeaderContainer;
            }

            if (string.Equals(id, "appendonsend", StringComparison.OrdinalIgnoreCase)
                || HasClass(classes, "gmail_quote")
                || HasClass(classes, "gmail_quote_container")
                || (name == "blockquote"
                    && string.Equals(AttributeValue(tag, "type"), "cite", StringComparison.OrdinalIgnoreCase)))
            {
                return BoundaryKind.Wrapper;
            }

            return null;
        }

        private static bool IsBorderTopSeparator(HtmlTagToken tag) =>
            AttributeValue(tag, "style") is { } style
            && WhitespaceRegex().Replace(style, string.Empty)
                .Contains("border-top:solid", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The pictures the message carries, inlined as data URIs when a cid
    /// reference names one; each reference is resolved once.
    /// </summary>
    private sealed class InlineImages(MimeMessage message)
    {
        private readonly Dictionary<string, string?> resolved = new(StringComparer.Ordinal);

        /// <summary>The source to write for an img, or null when the img is dropped.</summary>
        public string? Source(string? value)
        {
            var url = CleanUrl(value);
            if (url is null)
            {
                return null;
            }

            if (HasScheme(url, "http:") || HasScheme(url, "https:"))
            {
                return url;
            }

            if (!HasScheme(url, "cid:"))
            {
                return null;
            }

            if (!resolved.TryGetValue(url, out var inlined))
            {
                inlined = Inline(url);
                resolved[url] = inlined;
            }

            return inlined;
        }

        private string? Inline(string url)
        {
            var contentId = Uri.UnescapeDataString(url[4..]).Trim().Trim('<', '>');
            var part = FindPart(message.Body, contentId, url);
            if (part?.Content is null || !InlineImageTypes.Contains(part.ContentType.MimeType))
            {
                return null;
            }

            using var decoded = new MemoryStream();
            part.Content.DecodeTo(decoded);
            if (decoded.Length > MaximumInlineImageBytes)
            {
                return null;
            }

            return string.Concat(
                "data:",
                part.ContentType.MimeType.ToLowerInvariant(),
                ";base64,",
                Convert.ToBase64String(decoded.GetBuffer(), 0, (int)decoded.Length));
        }

        /// <summary>The part of the message's own body with the Content-ID, or the Content-Location, named.</summary>
        private static MimePart? FindPart(MimeEntity? entity, string contentId, string url)
        {
            if (entity is Multipart multipart)
            {
                foreach (var child in multipart)
                {
                    if (FindPart(child, contentId, url) is { } found)
                    {
                        return found;
                    }
                }

                return null;
            }

            return entity is MimePart part
                && (string.Equals(part.ContentId, contentId, StringComparison.Ordinal)
                    || (part.ContentLocation is { } location
                        && (string.Equals(location.OriginalString, url, StringComparison.Ordinal)
                            || string.Equals(location.OriginalString, contentId, StringComparison.Ordinal))))
                ? part
                : null;
        }
    }

    // Outlook's "-----Original Message-----" and Gmail's
    // "---------- Forwarded message ---------", as the plain-text splitter reads them.
    [GeneratedRegex(
        "^-{2,}\\s*(?:Original Message\\s*-{2,}|Forwarded message)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SeparatorRegex();

    [GeneratedRegex("^On .{6,120} wrote:$", RegexOptions.CultureInvariant)]
    private static partial Regex AttributionRegex();

    [GeneratedRegex("@import\\b[^;]*;?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ImportRuleRegex();

    [GeneratedRegex(
        "expression\\s*\\(|behavior\\s*:|-moz-binding|javascript\\s*:|vbscript\\s*:",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UnsafeCssRegex();

    [GeneratedRegex("\\p{Cc}", RegexOptions.CultureInvariant)]
    private static partial Regex ControlCharacterRegex();

    [GeneratedRegex("\\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}
