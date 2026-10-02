using System.Text.RegularExpressions;

namespace Pegasus.ArchitectureTests;

/// <summary>
/// Every action shows it is working from the press until the result arrives
/// (FRD-12, site.js <c>pegasusBusy</c>). The pressed button says what it is
/// doing in its <c>data-busy-label</c>, a word from
/// <c>OperatorLabels.Busy</c>, beside the one busy ring site.js draws. These
/// checks keep a new POST button from going silent, keep the words with
/// their owner, and keep pages from drawing a second spinner.
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
                Assert.True(
                    label.Groups[1].Value.Contains("OperatorLabels.Busy.", StringComparison.Ordinal),
                    $"{Relative(path)} writes its own busy words ({label.Groups[1].Value}); use OperatorLabels.Busy.");
            }
        }
    }

    [Fact]
    public void OnlySiteScriptDrawsTheBusyRing()
    {
        foreach (var path in RazorFiles())
        {
            Assert.False(
                File.ReadAllText(path).Contains("busy-ring", StringComparison.Ordinal),
                $"{Relative(path)} draws a busy ring; site.js pegasusBusy draws the one ring.");
        }
    }

    [Fact]
    public void BusyIsNotTheDisabledLook()
    {
        // The pressed button keeps its colours; only its neighbours stand
        // aside. The retired rule greyed every submit in a busy form.
        var siteCss = Read("src/Pegasus.Web/wwwroot/css/site.css");

        Assert.DoesNotContain("form[aria-busy=\"true\"] button", siteCss, StringComparison.Ordinal);
        Assert.Contains("[data-busy-aside]", siteCss, StringComparison.Ordinal);
        Assert.Contains(".busy-ring", siteCss, StringComparison.Ordinal);
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
