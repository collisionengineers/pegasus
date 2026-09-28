using System.Text.RegularExpressions;

namespace Pegasus.ArchitectureTests;

/// <summary>
/// Issues 845 and 846: site.js's arrow-key roving selector names the markup
/// the pages still emit (Cases rows and scope buttons), the scope lists opt
/// in with <c>data-row-list</c>, and the gallery retry binder sits in its own
/// block so the Case page, which has no evidence viewer, still gets it.
/// </summary>
public sealed class RowListAndGalleryScriptContractTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void TheRowSelectorCoversCasesRowsAndScopeButtons()
    {
        var site = Read("src/Pegasus.Web/wwwroot/js/site.js");
        var selector = Regex.Match(site, @"var ROW = '([^']+)';");

        Assert.True(selector.Success, "site.js has no ROW selector.");
        foreach (var token in new[] { ".row-button", ".scope-button", "tr[data-select-href]", "tr[data-cases-row]" })
        {
            Assert.Contains(token, selector.Groups[1].Value, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("src/Pegasus.Web/Pages/Cases/Index.cshtml", "class=\"scope-list\" data-row-list")]
    [InlineData("src/Pegasus.Web/Pages/Cases/Index.cshtml", "<div class=\"cases-table\" data-row-list>")]
    [InlineData("src/Pegasus.Web/Pages/Mail/Index.cshtml", "class=\"scope-list\" data-mail-scopes data-row-list")]
    public void EveryRovingListOptsIn(string page, string markup) =>
        Assert.Contains(markup, Read(page), StringComparison.Ordinal);

    [Fact]
    public void TheGalleryRetryBinderIsNotInsideTheEvidenceViewerBlock()
    {
        var site = Read("src/Pegasus.Web/wwwroot/js/site.js").Replace("
", "
", StringComparison.Ordinal);
        var viewerStart = site.IndexOf("var viewer = document.querySelector('[data-evidence-viewer]');", StringComparison.Ordinal);
        var binder = site.IndexOf("function bindGalleryImages(root)", StringComparison.Ordinal);

        Assert.True(viewerStart >= 0 && binder > viewerStart, "Expected the viewer block and then the gallery binder.");
        // The viewer block closes with its own IIFE terminator before the binder starts.
        Assert.Contains("
    })();
", site[viewerStart..binder], StringComparison.Ordinal);
        Assert.Contains(
            "(window.pegasusMountBinders = window.pegasusMountBinders || []).push(bindGalleryImages);",
            site,
            StringComparison.Ordinal);
    }

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(RepositoryRoot, relativePath));

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
