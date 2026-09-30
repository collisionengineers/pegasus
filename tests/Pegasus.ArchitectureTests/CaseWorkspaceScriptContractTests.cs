using System.Text.RegularExpressions;

namespace Pegasus.ArchitectureTests;

/// <summary>
/// Issue 916: after a save, the retained sections' forms take the Case's new
/// version and edit lease together. The Glass's and report forms name the version
/// <c>expectedCaseVersion</c>, so the update must look for it as well as for
/// <c>expectedVersion</c>, or a retained form keeps a stale version beside a
/// fresh lease.
/// </summary>
public sealed class CaseWorkspaceScriptContractTests
{
    [Fact]
    public void ARetainedSectionsFormsAdvanceEitherNameOfTheCaseVersion()
    {
        var script = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "Pegasus.Web", "wwwroot", "js", "case-workspace.js"));

        Assert.Contains(
            "var version = form.querySelector('[name=\"expectedVersion\"], [name=\"expectedCaseVersion\"]');",
            script,
            StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Pegasus.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Pegasus repository root.");
    }

    /// <summary>
    /// Roadmap Lane H (FRD-16): the server answers a commit with the parts the script
    /// redraws after one, so a root the script gains and the answer never draws would
    /// silently stop updating. The script keeps the sections and the viewer, and every
    /// other root it names must be drawn by the answer's partials. Every input it
    /// carries forward must be drawn there too, or the next commit sends a stale key,
    /// version, lease or valuation calculation.
    /// </summary>
    [Fact]
    public void TheCommitAnswerDrawsEveryPartTheScriptRedrawsAfterACommit()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(
            root, "src", "Pegasus.Web", "wwwroot", "js", "case-workspace.js"));
        var cases = Path.Combine(root, "src", "Pegasus.Web", "Pages", "Cases");
        var shared = Path.Combine(cases, "Shared");

        Assert.Contains(
            "selector === '#case-main' || selector === '[data-case-viewer-host]'",
            script,
            StringComparison.Ordinal);
        var roots = Regex.Match(script, @"var swapRoots = \[(?<list>.*?)\];").Groups["list"].Value;
        var redrawn = Regex.Matches(roots, @"'\[(?<attribute>[a-z-]+)\]'")
            .Select(match => match.Groups["attribute"].Value)
            .Where(attribute => attribute != "data-case-viewer-host")
            .ToArray();
        Assert.Equal(5, redrawn.Length);

        var parts = new Dictionary<string, string>(StringComparer.Ordinal);
        var pending = new Queue<string>(["_CaseCommitResult"]);
        while (pending.TryDequeue(out var name))
        {
            if (parts.ContainsKey(name) || !File.Exists(Path.Combine(shared, name + ".cshtml")))
            {
                continue;
            }

            var markup = File.ReadAllText(Path.Combine(shared, name + ".cshtml"));
            parts[name] = markup;
            foreach (Match part in Regex.Matches(markup, "<partial name=\"Cases/Shared/(?<part>_[A-Za-z]+)\""))
            {
                pending.Enqueue(part.Groups["part"].Value);
            }
        }

        var drawn = string.Concat(parts.Values);
        foreach (var attribute in redrawn)
        {
            Assert.Contains(attribute, drawn, StringComparison.Ordinal);
        }

        var carriers = Directory.GetFiles(cases, "*.cshtml", SearchOption.AllDirectories)
            .Where(file => File.ReadAllText(file).Contains("data-carry-forward", StringComparison.Ordinal))
            .Select(file => Path.GetFileNameWithoutExtension(file)!)
            .ToArray();
        Assert.NotEmpty(carriers);
        Assert.All(carriers, carrier => Assert.Contains(carrier, parts.Keys));
    }
}
