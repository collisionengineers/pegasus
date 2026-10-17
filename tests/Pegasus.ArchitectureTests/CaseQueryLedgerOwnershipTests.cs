using System.Text.RegularExpressions;

namespace Pegasus.ArchitectureTests;

/// <summary>
/// CaseQueryLedger is the one writer of a Case's queries and of the
/// workflow's open-query mark (operator, 11 October 2026): no other
/// persistence source sets <c>HasOpenQuery</c> on an entity or adds a
/// <c>CaseQueries</c> row. Projections that read the mark into a record
/// (<c>HasOpenQuery = row.HasOpenQuery</c> in an initializer) are reads and
/// are not matched. Migrations and the compiled model are generated and
/// excluded.
/// </summary>
public sealed class CaseQueryLedgerOwnershipTests
{
    private const string LedgerFile = "CaseQueryLedger.cs";

    private static readonly Regex[] Writes =
    [
        // workflow.HasOpenQuery = ..., not ==, => or an initializer's bare name.
        new(@"\.\s*HasOpenQuery\s*=(?![=>])", RegexOptions.CultureInvariant),
        // ExecuteUpdate's SetProperty(item => item.HasOpenQuery, ...).
        new(@"SetProperty\(\s*\w+\s*=>\s*\w+\s*\.\s*HasOpenQuery\b", RegexOptions.CultureInvariant),
        new(@"\bCaseQueries\s*\.\s*Add(Range)?(Async)?\s*\(", RegexOptions.CultureInvariant),
        new(@"\bSet<\s*CaseQueryEntity\s*>\s*\(\s*\)\s*\.\s*Add", RegexOptions.CultureInvariant),
    ];

    [Fact]
    public void OnlyTheLedgerWritesTheOpenQueryMarkOrAddsACaseQuery()
    {
        var persistence = Path.Combine(RepositoryRoot.Find(), "src", "Pegasus.Infrastructure", "Persistence");
        var offenders = Directory
            .EnumerateFiles(persistence, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsGenerated(persistence, path))
            .Where(path => !string.Equals(Path.GetFileName(path), LedgerFile, StringComparison.Ordinal))
            .SelectMany(path => File.ReadAllLines(path).Select((line, index) => (path, line, number: index + 1)))
            .Where(entry => Writes.Any(pattern => pattern.IsMatch(entry.line)))
            .Select(entry => $"{Path.GetRelativePath(persistence, entry.path)}:{entry.number}: {entry.line.Trim()}")
            .ToArray();

        Assert.True(offenders.Length == 0, "Only CaseQueryLedger may write queries:" + Environment.NewLine + string.Join(Environment.NewLine, offenders));

        // The ledger itself is where the writes live, so the scan is not vacuous.
        var ledger = File.ReadAllText(Path.Combine(persistence, LedgerFile));
        Assert.Matches(Writes[0], ledger);
        Assert.Matches(Writes[2], ledger);
    }

    private static bool IsGenerated(string persistence, string path)
    {
        var relative = Path.GetRelativePath(persistence, path);
        var first = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
        return first is "Migrations" or "CompiledModel";
    }
}
