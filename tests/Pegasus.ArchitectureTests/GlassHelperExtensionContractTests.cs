using System.Text.Json;
using System.Text.RegularExpressions;
using Pegasus.Web.Pages.Integrations.Glass;

namespace Pegasus.ArchitectureTests;

/// <summary>
/// The Pegasus helper extension (<c>src/pegasus-glass-helper/</c>, R1170)
/// asks for as little as it can: no extension permission, host access to the
/// two Pegasus origins only, its scripts in Glass's editor frame only, and
/// only the start-up fix in the page's own world. Its protocol is the Help
/// page's. The extension has no CI of its own; these pins and the manual
/// walks in its README are its evidence.
/// </summary>
public sealed partial class GlassHelperExtensionContractTests
{
    private const string EditorFrame = "https://repairestimate.autovistagroup.com/ere/aco/acofr.php*";

    /// <summary>The name Chrome shows for the helper (operator, 18 November 2026; R1183).</summary>
    private const string Name = "Pegasus helper for Glass's";

    private static readonly string[] PegasusOrigins =
    [
        "https://pegasus-prod-web-252ow37gij.azurewebsites.net/*",
        "https://cemattypc-1.tail432b53.ts.net/*",
    ];

    private static readonly string[] TopLevelKeys =
        ["background", "content_scripts", "description", "host_permissions", "manifest_version", "name", "version"];

    private static readonly string[] Scripts = ["helper.js", "patch-dialog.js"];

    private static readonly string[] MainWorldScripts = ["patch-dialog.js"];

    [GeneratedRegex(@"^const PROTOCOL = (\d+);\r?$", RegexOptions.Multiline)]
    private static partial Regex ProtocolLine();

    [GeneratedRegex(@"^\d+\.\d+\.\d+$")]
    private static partial Regex ManifestVersion();

    [Fact]
    public void TheManifestAsksForNoPermissionAndOnlyThePegasusOrigins()
    {
        using var manifest = Manifest();
        var root = manifest.RootElement;

        Assert.Equal(TopLevelKeys, root.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.False(root.TryGetProperty("permissions", out _));
        Assert.Equal(3, root.GetProperty("manifest_version").GetInt32());
        Assert.Equal(Name, root.GetProperty("name").GetString());
        // Chrome shows at most 132 characters of a description.
        Assert.InRange(root.GetProperty("description").GetString()?.Length ?? 0, 1, 132);
        Assert.Matches(ManifestVersion(), root.GetProperty("version").GetString());
        Assert.Equal(PegasusOrigins, Strings(root.GetProperty("host_permissions")));
        var background = root.GetProperty("background");
        Assert.Equal("background.js", Assert.Single(background.EnumerateObject(), property => property.Name == "service_worker").Value.GetString());
        Assert.Single(background.EnumerateObject());
    }

    [Fact]
    public void ItsScriptsRunInTheEditorFrameAndOnlyTheStartUpFixInThePagesWorld()
    {
        using var manifest = Manifest();
        var scripts = manifest.RootElement.GetProperty("content_scripts").EnumerateArray().ToList();

        Assert.Equal(Scripts, scripts.SelectMany(script => Strings(script.GetProperty("js"))).Order(StringComparer.Ordinal));
        foreach (var script in scripts)
        {
            Assert.Equal(EditorFrame, Assert.Single(Strings(script.GetProperty("matches"))));
            var main = script.TryGetProperty("world", out var world) && world.GetString() == "MAIN";
            Assert.Equal(main, Strings(script.GetProperty("js")).SequenceEqual(MainWorldScripts));
        }
    }

    [Fact]
    public void TheHelpersProtocolAndAddressAreTheHelpPages()
    {
        var background = File.ReadAllText(Path.Combine(Folder(), "background.js"));

        var protocol = ProtocolLine().Match(background);
        Assert.True(protocol.Success, "background.js must declare its PROTOCOL.");
        Assert.Equal(HelpModel.Protocol, int.Parse(protocol.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Contains("'" + HelpModel.BasePath.TrimStart('/') + "/'", background, StringComparison.Ordinal);
    }

    /// <summary>
    /// The helper reports, on each poll, the last ask to show the selected
    /// list again that it has acted on, and Pegasus's poll body reads it, so
    /// a help act can say whether the Engineer's window shows the change.
    /// </summary>
    [Fact]
    public void ThePollSaysWhatTheWindowHasShown()
    {
        var helper = File.ReadAllText(Path.Combine(Folder(), "helper.js"));
        var background = File.ReadAllText(Path.Combine(Folder(), "background.js"));

        Assert.Contains("kind: 'poll', seen:", helper, StringComparison.Ordinal);
        Assert.Contains("if (shown) { showSelectedAgain(); }", helper, StringComparison.Ordinal);
        Assert.Contains("seen: Number.isSafeInteger(message.seen) ? message.seen : 0", background, StringComparison.Ordinal);
    }

    private static string Folder() => Path.Combine(RepositoryRoot.Find(), "src", "pegasus-glass-helper");

    private static JsonDocument Manifest() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(Folder(), "manifest.json")));

    private static List<string> Strings(JsonElement array) =>
        array.EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToList();
}
