namespace Pegasus.ArchitectureTests;

/// <summary>
/// ADR-0074: <c>PegasusTransactions</c> is the one owner of a store
/// transaction's isolation level, so no other Infrastructure source names one.
/// </summary>
public sealed class TransactionIsolationContractTests
{
    private const string Owner = "src/Pegasus.Infrastructure/Persistence/PegasusTransactions.cs";

    [Fact]
    public void OnlyTheTransactionOwnerNamesAnIsolationLevel()
    {
        var root = FindRepositoryRoot();
        var naming = Directory
            .EnumerateFiles(Path.Combine(root, "src", "Pegasus.Infrastructure"), "*.cs", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .Where(path => !path.Contains("/bin/", StringComparison.Ordinal)
                && !path.Contains("/obj/", StringComparison.Ordinal))
            .Where(path => File.ReadAllText(Path.Combine(root, path))
                .Contains("IsolationLevel.", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { Owner }, naming);
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
