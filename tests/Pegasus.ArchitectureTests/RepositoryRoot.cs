namespace Pegasus.ArchitectureTests;

/// <summary>The repository root every source-reading contract test starts from.</summary>
internal static class RepositoryRoot
{
    /// <summary>The nearest parent of the test binaries that holds <c>Pegasus.slnx</c>.</summary>
    internal static string Find()
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
