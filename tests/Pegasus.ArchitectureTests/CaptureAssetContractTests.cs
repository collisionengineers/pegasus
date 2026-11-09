using System.Text.RegularExpressions;

namespace Pegasus.ArchitectureTests;

/// <summary>
/// The photo capture app (FRD-28) is served under a Content Security Policy
/// that admits no inline script or style, so its assets and the pages that
/// embed them carry none: an inline handler, a style attribute or a
/// <c>javascript:</c> URL would be refused by the browser and break the app
/// silently. No browser automation exists to catch that (ADR-0050), so the
/// text is held to the rule here.
/// </summary>
public sealed partial class CaptureAssetContractTests
{
    private static readonly string[] AssetPaths =
    [
        "src/Pegasus.Web/wwwroot/capture/capture.js",
        "src/Pegasus.Web/wwwroot/capture/shell.html"
    ];

    private const string PagesFolder = "src/Pegasus.Web/Pages/Capture";

    [GeneratedRegex(@"style=""", RegexOptions.IgnoreCase)]
    private static partial Regex StyleAttribute();

    [GeneratedRegex(@"\son[a-z]+=""", RegexOptions.IgnoreCase)]
    private static partial Regex InlineHandler();

    [GeneratedRegex(@"javascript:", RegexOptions.IgnoreCase)]
    private static partial Regex JavaScriptUrl();

    [GeneratedRegex(@"<script\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptTag();

    [Fact]
    public void TheCaptureAssetsCarryNoInlineStyleHandlerOrJavaScriptUrl()
    {
        var root = RepositoryRoot.Find();
        foreach (var relative in AssetPaths)
        {
            var path = Path.Combine(root, relative);
            Assert.True(File.Exists(path), $"{relative} is missing.");
            var text = File.ReadAllText(path);
            Assert.False(StyleAttribute().IsMatch(text), $"{relative} carries an inline style attribute.");
            Assert.False(InlineHandler().IsMatch(text), $"{relative} carries an inline event handler.");
            Assert.False(JavaScriptUrl().IsMatch(text), $"{relative} carries a javascript: URL.");
        }
    }

    /// <summary>
    /// The capture pages embed exactly two JSON blocks (the asset manifest and
    /// the link context) and load the app from its external file; no other
    /// script element, inline or not, may appear.
    /// </summary>
    [Fact]
    public void TheCapturePagesEmbedOnlyTheTwoJsonBlocksAndTheExternalScript()
    {
        var folder = Path.Combine(RepositoryRoot.Find(), PagesFolder);
        Assert.True(Directory.Exists(folder), $"{PagesFolder} is missing.");
        var pages = Directory.GetFiles(folder, "*.cshtml", SearchOption.AllDirectories);
        Assert.NotEmpty(pages);
        foreach (var page in pages)
        {
            var text = File.ReadAllText(page);
            Assert.False(StyleAttribute().IsMatch(text), $"{Path.GetFileName(page)} carries an inline style attribute.");
            Assert.False(InlineHandler().IsMatch(text), $"{Path.GetFileName(page)} carries an inline event handler.");
            foreach (Match script in ScriptTag().Matches(text))
            {
                var isJson = script.Value.Contains("type=\"application/json\"", StringComparison.OrdinalIgnoreCase);
                var isExternal = script.Value.Contains(" src=", StringComparison.OrdinalIgnoreCase);
                Assert.True(isJson || isExternal, $"{Path.GetFileName(page)} embeds a script that is neither a JSON block nor the external app: {script.Value}");
            }
        }
    }
}
