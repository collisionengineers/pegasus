using System.Xml.Linq;

namespace Pegasus.ArchitectureTests;

/// <summary>
/// The icon sprite is the static file <c>wwwroot/images/lucide-sprite.svg</c>.
/// An SVG file is read strictly where the Razor partial it replaced was
/// forgiving: a stray ampersand or a Razor comment would blank every icon in
/// the application, so the file is held to well-formed XML of unique glyphs.
/// </summary>
public sealed class IconSpriteContractTests
{
    private const string SpritePath = "src/Pegasus.Web/wwwroot/images/lucide-sprite.svg";

    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

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
}
