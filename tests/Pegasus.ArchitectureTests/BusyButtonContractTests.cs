using System.Text.RegularExpressions;

namespace Pegasus.ArchitectureTests;

/// <summary>
/// Every action shows it is working from the press until the result arrives
/// (FRD-12, site.js <c>pegasusBusy</c>). The pressed button says what it is
/// doing in its <c>data-busy-label</c>, a word from
/// <c>OperatorLabels.Busy</c>, beside the one turning loader glyph site.js
/// draws; after five seconds the words say "Still …", and an action answered
/// in place holds a tick and its <c>data-busy-done</c> word for a moment.
/// These checks keep a new POST button from going silent, keep the words
/// with their owner, and keep pages from drawing a second spinner.
/// </summary>
public sealed partial class BusyButtonContractTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    // A tag and its attributes; a quoted value may hold '>' (a Razor lambda).
    [GeneratedRegex("""<form\b((?:[^>"]|"[^"]*")*)>""", RegexOptions.Singleline)]
    private static partial Regex FormTag();

    [GeneratedRegex("""<button\b((?:[^>"]|"[^"]*")*)>""", RegexOptions.Singleline)]
    private static partial Regex ButtonTag();

    // A plain value, or a Razor expression @( … ) that may quote a string.
    [GeneratedRegex("""data-busy-label="(@\(.*?\)|[^"]*)"(?=[\s/>])""")]
    private static partial Regex BusyLabel();

    [GeneratedRegex("""data-busy-done="(@\(.*?\)|[^"]*)"(?=[\s/>])""")]
    private static partial Regex BusyDone();

    // A capitalised verb phrase ending in an ellipsis, so "Still " + a
    // lowercased first letter reads as a sentence ("Still placing on hold…").
    [GeneratedRegex("""^\p{Lu}\p{Ll}+( \p{Ll}+)*…$""")]
    private static partial Regex BusyPhrase();

    [Fact]
    public void EveryButtonThatPostsNamesItsBusyWords()
    {
        var silent = new List<string>();
        foreach (var path in RazorFiles())
        {
            var source = File.ReadAllText(path);
            var postSpans = new List<(int Start, int End)>();
            var postIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match form in FormTag().Matches(source))
            {
                var attributes = form.Groups[1].Value;
                if (!string.Equals(Attribute(attributes, "method"), "post", StringComparison.OrdinalIgnoreCase)
                    || Attribute(attributes, "target") is not null)
                {
                    continue;
                }
                var end = source.IndexOf("</form>", form.Index + form.Length, StringComparison.Ordinal);
                postSpans.Add((form.Index + form.Length, end < 0 ? source.Length : end));
                if (Attribute(attributes, "id") is { } id)
                {
                    postIds.Add(id);
                }
            }

            foreach (Match button in ButtonTag().Matches(source))
            {
                var attributes = button.Groups[1].Value;
                var type = Attribute(attributes, "type") ?? "submit";
                if (!type.Equals("submit", StringComparison.OrdinalIgnoreCase)
                    || HasAttribute(attributes, "data-dialog-close")
                    || string.Equals(Attribute(attributes, "formmethod"), "get", StringComparison.OrdinalIgnoreCase)
                    // With script this button only opens the confirm dialog,
                    // whose own submit carries the busy words.
                    || HasAttribute(attributes, "data-upload-review-button"))
                {
                    continue;
                }
                var owner = Attribute(attributes, "form");
                var posts = owner is not null
                    ? postIds.Contains(owner)
                    : postSpans.Any(span => button.Index >= span.Start && button.Index < span.End);
                if (posts && !HasAttribute(attributes, "data-busy-label"))
                {
                    var line = source.AsSpan(0, button.Index).Count('\n') + 1;
                    silent.Add($"{Relative(path)}:{line}");
                }
            }
        }

        Assert.True(
            silent.Count == 0,
            "These buttons post without busy words; add data-busy-label=\"@OperatorLabels.Busy.…\": "
                + string.Join(", ", silent));
    }

    [Fact]
    public void BusyWordsComeFromTheLabelOwner()
    {
        foreach (var path in RazorFiles())
        {
            foreach (Match label in BusyLabel().Matches(File.ReadAllText(path)))
            {
                var value = label.Groups[1].Value;
                Assert.True(
                    value.Contains("OperatorLabels.Busy.", StringComparison.Ordinal)
                        && !value.Contains("OperatorLabels.Busy.Done.", StringComparison.Ordinal)
                        && !value.Contains("OperatorLabels.Busy.Still", StringComparison.Ordinal),
                    $"{Relative(path)} writes its own busy words ({value}); use an OperatorLabels.Busy \"-ing\" word.");
            }
        }
    }

    [Fact]
    public void DoneWordsComeFromTheLabelOwner()
    {
        foreach (var path in RazorFiles())
        {
            foreach (Match label in BusyDone().Matches(File.ReadAllText(path)))
            {
                Assert.True(
                    label.Groups[1].Value.Contains("OperatorLabels.Busy.Done.", StringComparison.Ordinal),
                    $"{Relative(path)} writes its own done words ({label.Groups[1].Value}); use OperatorLabels.Busy.Done.");
            }
        }
    }

    [Fact]
    public void EveryBusyWordReadsAfterStill()
    {
        // site.js builds "Still saving…" from the layout's Still word and the
        // button's own; a word that does not open with its verb would misread.
        var words = typeof(Pegasus.Web.Presentation.OperatorLabels.Busy)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string)
                && field.Name is not (nameof(Pegasus.Web.Presentation.OperatorLabels.Busy.Still)
                    or nameof(Pegasus.Web.Presentation.OperatorLabels.Busy.DownloadFailed)))
            .Select(field => (field.Name, Value: (string)field.GetRawConstantValue()!))
            .ToList();

        Assert.NotEmpty(words);
        Assert.Equal("Still", Pegasus.Web.Presentation.OperatorLabels.Busy.Still);
        foreach (var (name, value) in words)
        {
            Assert.True(BusyPhrase().IsMatch(value), $"OperatorLabels.Busy.{name} = \"{value}\" would not read after \"Still\".");
        }
    }

    [Fact]
    public void TheLayoutsCarryTheBusyDefaults()
    {
        foreach (var layout in new[] { "src/Pegasus.Web/Pages/Shared/_Layout.cshtml", "src/Pegasus.Web/Pages/Shared/_LayoutAuth.cshtml" })
        {
            var source = Read(layout);
            Assert.Contains("data-busy-label=\"@OperatorLabels.Busy.Working\"", source, StringComparison.Ordinal);
            Assert.Contains("data-busy-still=\"@OperatorLabels.Busy.Still\"", source, StringComparison.Ordinal);
        }
    }

    // The glyph classes as whole tokens: data-busy-done is a button's words,
    // not a drawn glyph.
    [GeneratedRegex("""(?<![\w-])busy-(spin|done)(?![\w-])""")]
    private static partial Regex BusyGlyphClass();

    [Fact]
    public void OnlySiteScriptDrawsTheBusySpin()
    {
        foreach (var path in RazorFiles())
        {
            var source = File.ReadAllText(path);
            Assert.False(
                BusyGlyphClass().IsMatch(source)
                    || source.Contains("href=\"#icon-loader\"", StringComparison.Ordinal),
                $"{Relative(path)} draws its own busy glyph; site.js pegasusBusy draws the one loader and tick.");
        }
    }

    [Fact]
    public void BusyIsNotTheDisabledLook()
    {
        // The pressed button keeps its colours; only its neighbours stand
        // aside. The retired rule greyed every submit in a busy form, and the
        // retired ring was a border circle rather than the Refresh glyph's spin.
        var siteCss = Read("src/Pegasus.Web/wwwroot/css/site.css");

        Assert.DoesNotContain("form[aria-busy=\"true\"] button", siteCss, StringComparison.Ordinal);
        Assert.DoesNotContain(".busy-ring", siteCss, StringComparison.Ordinal);
        Assert.Contains("[data-busy-aside]", siteCss, StringComparison.Ordinal);
        Assert.Contains(".busy-spin{animation:pegasus-spin 1s linear infinite}", siteCss, StringComparison.Ordinal);
        Assert.Contains("[data-busy-complete]", siteCss, StringComparison.Ordinal);
    }

    [Fact]
    public void ADownloadFormIsNotAnsweredInPlace()
    {
        // The Case page's in-place post would read the file as a page and
        // discard it.
        foreach (var path in RazorFiles())
        {
            foreach (Match form in FormTag().Matches(File.ReadAllText(path)))
            {
                var attributes = form.Groups[1].Value;
                if (HasAttribute(attributes, "data-busy-download"))
                {
                    Assert.True(
                        HasAttribute(attributes, "data-no-inplace"),
                        $"{Relative(path)} has a download form without data-no-inplace.");
                }
            }
        }
    }

    private static string? Attribute(string attributes, string name)
    {
        var match = Regex.Match(attributes, $"(?<![\\w-]){Regex.Escape(name)}\\s*=\\s*\"([^\"]*)\"");
        return match.Success ? match.Groups[1].Value : null;
    }

    private static bool HasAttribute(string attributes, string name) =>
        Regex.IsMatch(attributes, $@"(?<![\w-]){Regex.Escape(name)}(?![\w-])");

    private static IEnumerable<string> RazorFiles() => Directory
        .EnumerateFiles(Path.Combine(RepositoryRoot, "src/Pegasus.Web/Pages"), "*.cshtml", SearchOption.AllDirectories);

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
