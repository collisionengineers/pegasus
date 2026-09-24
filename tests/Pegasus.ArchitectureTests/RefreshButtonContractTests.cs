using System.Text.RegularExpressions;

namespace Pegasus.ArchitectureTests;

/// <summary>
/// Issue 831: the refresh button is one component. Every refresh surface
/// renders <c>Pages/Shared/_RefreshButton.cshtml</c>, which owns the
/// <c>data-refresh-form</c>, its <c>data-refresh-region</c> live region, the
/// <c>icon--spin</c> decoration, the <c>data-refresh-label</c> the label
/// rewrite targets and the refresh glyph itself. These checks keep pages from
/// hand-rolling a second refresh rendering or a second spinner.
/// </summary>
public sealed class RefreshButtonContractTests
{
    private const string PartialPath = "src/Pegasus.Web/Pages/Shared/_RefreshButton.cshtml";

    // The sprite declares the glyph once as a symbol; the partial is the one
    // page-level drawing of it.
    private const string SpritePath = "src/Pegasus.Web/Pages/Shared/_LucideSprite.cshtml";

    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void TheRefreshButtonPartialIsTheOnlyOwnerOfARefreshForm()
    {
        foreach (var path in RazorFiles())
        {
            Assert.False(
                Read(path).Contains("data-refresh-form", StringComparison.Ordinal),
                $"{Relative(path)} renders a data-refresh-form; refresh controls must compose {PartialPath}.");
        }
    }

    [Fact]
    public void TheRefreshGlyphAndSpinDecorationBelongToTheRefreshButtonAlone()
    {
        foreach (var path in RazorFiles())
        {
            var source = Read(path);
            Assert.False(
                source.Contains("icon-refresh-cw", StringComparison.Ordinal),
                $"{Relative(path)} draws the refresh glyph; it is reserved for the refresh button.");
            Assert.False(
                source.Contains("icon--spin", StringComparison.Ordinal),
                $"{Relative(path)} adds icon--spin; the spin decorates a refresh only.");
        }
    }

    [Fact]
    public void TheRefreshButtonPartialCarriesTheWholeFeedbackContract()
    {
        var source = Read(PartialPath);

        Assert.Contains("data-refresh-region", source, StringComparison.Ordinal);
        Assert.Contains("role=\"status\"", source, StringComparison.Ordinal);
        Assert.Contains("aria-live=\"polite\"", source, StringComparison.Ordinal);
        Assert.Contains("<form method=\"get\" data-refresh-form>", source, StringComparison.Ordinal);
        Assert.Contains("class=\"icon icon--spin\"", source, StringComparison.Ordinal);
        Assert.Contains("data-refresh-label", source, StringComparison.Ordinal);
        Assert.Contains("title=\"Refresh\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSpinnerIsDeclaredExactlyOnce()
    {
        var siteCss = Read("src/Pegasus.Web/wwwroot/css/site.css");

        Assert.Equal(
            1,
            Regex.Count(siteCss, @"@keyframes\s+pegasus-spin\b"));
        Assert.Equal(
            1,
            Regex.Count(siteCss, @"animation:\s*pegasus-spin\b"));
    }

    [Fact]
    public void NoStylesheetKeepsRetiredRefreshRules()
    {
        foreach (var path in Directory.GetFiles(
                     Path.Combine(RepositoryRoot, "src/Pegasus.Web/wwwroot/css"),
                     "*.css",
                     SearchOption.AllDirectories))
        {
            var source = Read(path);

            Assert.DoesNotContain("freshness-banner", source, StringComparison.Ordinal);
            Assert.DoesNotContain("wc-refresh-form", source, StringComparison.Ordinal);
        }
    }

    private static IEnumerable<string> RazorFiles() => Directory
        .EnumerateFiles(Path.Combine(RepositoryRoot, "src/Pegasus.Web/Pages"), "*.cshtml", SearchOption.AllDirectories)
        .Where(path =>
        {
            var relative = Relative(path).Replace('\\', '/');
            return !relative.Equals(PartialPath, StringComparison.Ordinal)
                && !relative.Equals(SpritePath, StringComparison.Ordinal);
        });

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(RepositoryRoot, relativePath));

    private static string Relative(string path) =>
        Path.GetRelativePath(RepositoryRoot, path);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Pegasus.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not locate Pegasus.slnx.");
    }
}
