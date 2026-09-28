namespace Pegasus.ArchitectureTests;

/// <summary>
/// Issue 923: the Web release is compiled ReadyToRun, and the FIPS BouncyCastle
/// assemblies the Box SDK carries check their own file bytes when they start.
/// A compiled copy fails "Module checksum failed" on the first Box sign-in, so
/// the Web host could store nothing in Box. Every such assembly the host ships
/// must be left as the package built it.
/// </summary>
public sealed class WebReadyToRunContractTests
{
    [Fact]
    public void EveryFipsAssemblyTheWebHostShipsIsExcludedFromReadyToRun()
    {
        var shipped = Directory
            .EnumerateFiles(AppContext.BaseDirectory, "*fips*.dll")
            .Select(Path.GetFileName)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Assert.NotEmpty(shipped);

        var project = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "Pegasus.Web", "Pegasus.Web.csproj"));
        foreach (var assembly in shipped)
        {
            Assert.Contains(
                $"<PublishReadyToRunExclude Include=\"{assembly}\" />", project, StringComparison.Ordinal);
        }
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
