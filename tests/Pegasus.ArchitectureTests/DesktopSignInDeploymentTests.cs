namespace Pegasus.ArchitectureTests;

/// <summary>
/// Sign-in from Pegasus Desktop is offered only where
/// <c>AutomationMcp:DesktopRedirectUris</c> is configured, and live has no
/// such configuration (R1297, ADR-0092). The deployment definitions are how
/// production gets its settings, so none of them may name the key.
/// </summary>
public sealed class DesktopSignInDeploymentTests
{
    [Fact]
    public void NoDeploymentDefinitionConfiguresPegasusDesktopSignIn()
    {
        var infrastructure = Path.Combine(RepositoryRoot.Find(), "infra");
        Assert.True(Directory.Exists(infrastructure), "The deployment definitions were not found.");
        var files = Directory
            .EnumerateFiles(infrastructure, "*", SearchOption.AllDirectories)
            .ToArray();
        Assert.NotEmpty(files);

        var offenders = files
            .Where(path => File.ReadAllText(path).Contains(
                "DesktopRedirectUris",
                StringComparison.OrdinalIgnoreCase))
            .Select(path => Path.GetRelativePath(infrastructure, path).Replace('\\', '/'))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "These deployment definitions configure sign-in from Pegasus Desktop: "
            + string.Join(", ", offenders));
    }
}
