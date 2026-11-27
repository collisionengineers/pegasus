using System.Text.RegularExpressions;

namespace Pegasus.ArchitectureTests;

/// <summary>
/// A document is filed in its Case's folder, in the Audit's <c>a.</c> folder
/// or in the Case folder's one subfolder (R1238), and
/// <c>CaseCustodyFolders</c> is the one place that tells which. A read, a
/// write and the Box parent check that each chose the folder for themselves
/// would disagree the day a folder is added, so no other source names the
/// Audit folder's code or picks between the Case's folder identities.
/// </summary>
public sealed partial class CaseCustodyFolderChoiceTests
{
    private const string Owner = "CaseCustodyFolders.cs";

    [Fact]
    public void OnlyCaseCustodyFoldersChoosesADocumentsFolder()
    {
        var source = Path.Combine(RepositoryRoot.Find(), "src");
        var offenders = Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutputOrGenerated(source, path) && Path.GetFileName(path) != Owner)
            .SelectMany(path =>
            {
                var text = File.ReadAllText(path);
                var file = Path.GetRelativePath(source, path).Replace(Path.DirectorySeparatorChar, '/');
                return AuditFolderCode().Matches(text).Select(match => $"{file}: names {match.Value}")
                    .Concat(InlineFolderChoice().Matches(text).Select(_ => $"{file}: picks between the Case's folders itself"));
            })
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "Take a document's folder from CaseCustodyFolders (RootOf, RootIs, IsAudit):" + Environment.NewLine
                + string.Join(Environment.NewLine, offenders));

        // The owner is the one file the scan leaves out, and it holds the code.
        var owner = Directory.EnumerateFiles(source, Owner, SearchOption.AllDirectories)
            .Single(path => !IsBuildOutputOrGenerated(source, path));
        Assert.Contains("public const string Audit = \"audit\";", File.ReadAllText(owner), StringComparison.Ordinal);
    }

    private static bool IsBuildOutputOrGenerated(string source, string path)
    {
        var segments = Path.GetRelativePath(source, path)
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Any(segment => segment is "bin" or "obj" or "Migrations");
    }

    [GeneratedRegex(@"CaseCustodyFolders\.Audit\b", RegexOptions.CultureInvariant)]
    private static partial Regex AuditFolderCode();

    // "? x.AuditCustodyRemoteId ... : y.CustodyRootRemoteId": the Audit's folder or the Case's, chosen in place.
    [GeneratedRegex(
        @"\?\s*(?:\w+\.)+AuditCustodyRemoteId\b[^;]{0,120}?:\s*(?:\w+\.)+CustodyRootRemoteId\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex InlineFolderChoice();
}
