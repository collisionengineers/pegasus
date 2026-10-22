using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Pegasus.ArchitectureTests;

/// <summary>
/// The icon sprite is the static file <c>wwwroot/images/lucide-sprite.svg</c>.
/// An SVG file is read strictly where the Razor partial it replaced was
/// forgiving: a stray ampersand or a Razor comment would blank every icon in
/// the application, so the file is held to well-formed XML of unique glyphs.
/// </summary>
public sealed partial class IconSpriteContractTests
{
    private const string SpritePath = "src/Pegasus.Web/wwwroot/images/lucide-sprite.svg";
    private const string HrefCall = "IconSprite.Href(";

    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

    [GeneratedRegex("\"(icon-[a-z0-9-]+)\"")]
    private static partial Regex GlyphLiteral();

    [Fact]
    public void TheSpriteIsOneWellFormedSvgOfUniqueIconSymbols()
    {
        var sprite = XDocument.Load(Path.Combine(RepositoryRoot.Find(), SpritePath));

        Assert.Equal(Svg + "svg", sprite.Root!.Name);

        var symbols = sprite.Root.Elements(Svg + "symbol").ToList();
        Assert.NotEmpty(symbols);

        var ids = symbols.Select(symbol => (string?)symbol.Attribute("id")).ToList();
        Assert.All(ids, id => Assert.StartsWith("icon-", id, StringComparison.Ordinal));
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.All(symbols, symbol => Assert.NotNull(symbol.Attribute("viewBox")));
    }

    /// <summary>
    /// A <c>&lt;use&gt;</c> of a symbol the sprite does not hold draws nothing
    /// (v41 K1: the Tasks icon named <c>icon-check-square</c>), so every glyph a
    /// Razor call site or a presentation label names is one of the sprite's.
    /// </summary>
    [Fact]
    public void EveryGlyphAPageOrLabelNamesIsInTheSprite()
    {
        var root = RepositoryRoot.Find();
        var sprite = XDocument.Load(Path.Combine(root, SpritePath));
        var held = sprite.Root!.Elements(Svg + "symbol")
            .Select(symbol => (string?)symbol.Attribute("id"))
            .ToHashSet(StringComparer.Ordinal);

        // Glyphs are named in the pages (Razor call sites, and the page models'
        // tab icons) and in the presentation labels.
        var web = Path.Combine(root, "src/Pegasus.Web");
        var missing = new List<string>();
        foreach (var directory in new[] { "Pages", "Presentation" })
        {
            foreach (var path in Directory.EnumerateFiles(Path.Combine(web, directory), "*.*", SearchOption.AllDirectories))
            {
                IEnumerable<string> spans = [];
                if (path.EndsWith(".cs", StringComparison.Ordinal))
                {
                    spans = [File.ReadAllText(path)];
                }
                else if (path.EndsWith(".cshtml", StringComparison.Ordinal))
                {
                    spans = HrefArguments(File.ReadAllText(path));
                }

                missing.AddRange(spans
                    .SelectMany(span => GlyphLiteral().Matches(span).Select(glyph => glyph.Groups[1].Value))
                    .Where(glyph => !held.Contains(glyph))
                    .Select(glyph => $"{Path.GetRelativePath(root, path)} names {glyph}"));
            }
        }

        Assert.Empty(missing);
    }

    // The text between the parentheses of each IconSprite.Href(...) call.
    private static IEnumerable<string> HrefArguments(string source)
    {
        for (var at = source.IndexOf(HrefCall, StringComparison.Ordinal); at >= 0;
            at = source.IndexOf(HrefCall, at + HrefCall.Length, StringComparison.Ordinal))
        {
            var start = at + HrefCall.Length;
            var depth = 1;
            var end = start;
            while (end < source.Length && depth > 0)
            {
                depth += source[end] switch { '(' => 1, ')' => -1, _ => 0 };
                end++;
            }

            yield return source[start..end];
        }
    }
}
