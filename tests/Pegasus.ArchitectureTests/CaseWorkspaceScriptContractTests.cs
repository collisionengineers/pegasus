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
}
