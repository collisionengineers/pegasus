using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Verifies that every immutable source the principal-identification work cites
/// is actually present on this machine and hashes to what the pack records.
///
/// The pack is a local, git-ignored reference collection: this test therefore
/// resolves its root from <c>PEGASUS_REFERENCE_PACK_ROOT</c> and skips clearly
/// when that is unset, rather than hard-coding one workstation's path. It reads
/// paths, sizes and hashes only — no corpus bytes are copied, printed, or
/// written into the report.
///
/// The counts (81 instructions, 29 third-party reports, 14 EVA workbooks) are
/// the whole point: a citation that cannot be resolved, or that resolves to
/// different bytes, is a claim about evidence that no longer holds.
/// </summary>
[Trait("Category", "Corpus")]
public sealed partial class PrincipalSourceManifestTests
{
    public const string PackRootVariable = "PEGASUS_REFERENCE_PACK_ROOT";

    private const int ExpectedInstructionCount = 81;
    private const int ExpectedReportCount = 29;
    private const int ExpectedEvaCount = 14;

    /// <summary>
    /// The EVA report names one workbook by a filename the pack does not carry:
    /// the manifest-listed file is the same bytes under the current name. It is
    /// resolved by that alias and then confirmed by hash, never by name alone.
    /// </summary>
    private const string JobSheetReportName = "backup_of_ce_job_sheet_260429.xlsm";
    private const string JobSheetPackPath = "ce-docs/job-sheet-current.xlsm";

    /// <summary>
    /// A filename that exists twice in the pack with DIFFERENT bytes: the EVA
    /// report's copy, which the manifest pins, and a pinned snapshot under
    /// astra_output that it does not. Resolving this by name alone picks the
    /// wrong workbook, so the test proves both are present and distinguishes
    /// them by hash.
    /// </summary>
    private const string AmbiguousWorkbookName = "providers-worked-on.xlsx";

    /// <summary>
    /// The Box-hosted originals. No local filename, size or hash is recorded
    /// for any of them anywhere in the pack, so they can never be verified
    /// here. They are reported as unavailable — never as passed, which would be
    /// a false claim about evidence nobody has checked.
    /// </summary>
    private static readonly string[] UnavailableIdentifiers =
        [.. Enumerable.Range(1, 28).Select(index => $"E{index:D2}")];

    [ReferencePackFact]
    public void EveryCitedImmutableSourceResolvesAndHashesAsRecorded()
    {
        var root = PackRoot();
        var manifest = ReadManifest(root);

        var instructions = ResolveInstructions(root);
        var reports = ResolveReports(root);
        var eva = ResolveEvaWorkbooks(root, manifest);

        var report = new StringBuilder()
            .AppendLine("# Principal source manifest verification")
            .AppendLine()
            .AppendLine(CultureInfo.InvariantCulture, $"Pack root read from `{PackRootVariable}`.")
            .AppendLine(CultureInfo.InvariantCulture, $"Manifest data rows: {manifest.Count}.")
            .AppendLine()
            .AppendLine("| Group | Referenced | Resolved | Hash verified |")
            .AppendLine("| --- | ---: | ---: | ---: |");
        AppendGroup(report, "instruction", instructions);
        AppendGroup(report, "report", reports);
        AppendGroup(report, "eva", eva);

        report.AppendLine()
            .AppendLine("## Unavailable")
            .AppendLine()
            .AppendLine(
                "Box-hosted originals with no local filename, size or hash recorded in the "
                + "pack. Reported as unavailable, never as passed.")
            .AppendLine();
        foreach (var identifier in UnavailableIdentifiers)
        {
            report.AppendLine(CultureInfo.InvariantCulture, $"- {identifier}: unavailable");
        }

        var ambiguous = ResolveAmbiguousWorkbook(root, manifest, report);
        WriteReport(report.ToString());

        Assert.Equal(
            (ExpectedInstructionCount, ExpectedInstructionCount, ExpectedInstructionCount),
            Counts(instructions));
        Assert.Equal(
            (ExpectedReportCount, ExpectedReportCount, ExpectedReportCount),
            Counts(reports));
        Assert.Equal(
            (ExpectedEvaCount, ExpectedEvaCount, ExpectedEvaCount),
            Counts(eva));

        // Two distinct workbooks share the ambiguous filename, and exactly one
        // of them is the copy the EVA report and the manifest pin.
        Assert.Equal(2, ambiguous.Select(item => item.Sha256).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Single(ambiguous, item => item.InManifest);

        Assert.Equal(28, UnavailableIdentifiers.Length);
    }

    private static (int Referenced, int Resolved, int Verified) Counts(
        IReadOnlyList<ResolvedSource> items) =>
        (items.Count,
            items.Count(item => item.ResolvedPath is not null),
            items.Count(item => item.HashVerified));

    private static void AppendGroup(
        StringBuilder report,
        string group,
        IReadOnlyList<ResolvedSource> items)
    {
        var (referenced, resolved, verified) = Counts(items);
        report.AppendLine(
            CultureInfo.InvariantCulture,
            $"| {group} | {referenced} | {resolved} | {verified} |");
    }

    /// <summary>
    /// The 81 instruction samples, from the "Immutable sample references"
    /// section of each principal's own method.md. The bullet carries the
    /// pack-relative path and the recorded SHA-256; both are read, and the file
    /// is hashed and compared.
    /// </summary>
    private static List<ResolvedSource> ResolveInstructions(string root)
    {
        var principalsRoot = Path.Combine(root, "astra_output", "reports", "principals");
        var resolved = new List<ResolvedSource>();
        foreach (var methodFile in Directory
            .EnumerateFiles(principalsRoot, "method.md", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal))
        {
            var lines = File.ReadAllLines(methodFile);
            var inSection = false;
            foreach (var line in lines)
            {
                if (line.StartsWith("## ", StringComparison.Ordinal))
                {
                    inSection = line.Contains("Immutable sample references", StringComparison.Ordinal);
                    continue;
                }

                if (!inSection)
                {
                    continue;
                }

                var match = SampleReferenceRegex().Match(line);
                if (match.Success)
                {
                    resolved.Add(Verify(
                        root,
                        match.Groups["path"].Value,
                        match.Groups["hash"].Value));
                }
            }
        }

        return resolved;
    }

    /// <summary>
    /// The 29 third-party engineer reports, each named with its own recorded
    /// hash in the inventory the reports themselves were built from.
    /// </summary>
    private static ResolvedSource[] ResolveReports(string root)
    {
        var inventoryPath = Path.Combine(
            root, "astra_output", "reports", "third-party-source-inventory.json");
        using var document = JsonDocument.Parse(File.ReadAllBytes(inventoryPath));
        return document.RootElement.EnumerateArray()
            .Select(entry => Verify(
                root,
                entry.GetProperty("source").GetString()!,
                entry.GetProperty("sha256").GetString()!))
            .ToArray();
    }

    /// <summary>
    /// The 14 EVA workbooks named by the export's own source inventory. Each is
    /// resolved to a unique manifest entry by its leaf name — except the job
    /// sheet, whose report name is an earlier one and which is resolved by the
    /// recorded alias and then confirmed by hash.
    /// </summary>
    private static List<ResolvedSource> ResolveEvaWorkbooks(
        string root,
        Dictionary<string, ManifestEntry> manifest)
    {
        var inventoryPath = Path.Combine(
            root, "more_docs", "eva_data_export", "Source_inventory.csv");
        var names = File.ReadLines(inventoryPath)
            .Skip(1)
            .Select(line => line.Split(',')[0].Trim().TrimStart('﻿'))
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var resolved = new List<ResolvedSource>();
        foreach (var name in names)
        {
            if (string.Equals(name, JobSheetReportName, StringComparison.OrdinalIgnoreCase))
            {
                resolved.Add(Verify(root, JobSheetPackPath, manifest[JobSheetPackPath].Sha256));
                continue;
            }

            var candidates = manifest.Values
                .Where(entry => string.Equals(
                    Path.GetFileName(entry.Path), name, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (candidates.Length != 1)
            {
                // Not resolvable without guessing between two workbooks, which
                // is exactly the failure this manifest exists to catch.
                resolved.Add(new(name, null, null, null, false));
                continue;
            }

            resolved.Add(Verify(root, candidates[0].Path, candidates[0].Sha256));
        }

        return resolved;
    }

    private static AmbiguousCandidate[] ResolveAmbiguousWorkbook(
        string root,
        Dictionary<string, ManifestEntry> manifest,
        StringBuilder report)
    {
        var candidates = Directory
            .EnumerateFiles(root, AmbiguousWorkbookName, SearchOption.AllDirectories)
            .Select(path => new
            {
                Relative = Path.GetRelativePath(root, path).Replace('\\', '/'),
                Sha256 = Sha256Of(path)
            })
            .Select(item => new AmbiguousCandidate(
                item.Relative,
                item.Sha256,
                manifest.ContainsKey(item.Relative)))
            .OrderBy(item => item.Path, StringComparer.Ordinal)
            .ToArray();

        report.AppendLine()
            .AppendLine(CultureInfo.InvariantCulture, $"## {AmbiguousWorkbookName}")
            .AppendLine()
            .AppendLine("| Path | SHA-256 | In manifest |")
            .AppendLine("| --- | --- | --- |");
        foreach (var candidate in candidates)
        {
            report.AppendLine(
                CultureInfo.InvariantCulture,
                $"| {candidate.Path} | {candidate.Sha256} | {candidate.InManifest} |");
        }

        return candidates;
    }

    private static ResolvedSource Verify(string root, string packRelativePath, string recordedSha256)
    {
        var normalized = packRelativePath.Replace('\\', '/').Trim();
        var absolute = Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(absolute))
        {
            return new(normalized, null, recordedSha256, null, false);
        }

        var actual = Sha256Of(absolute);
        return new(
            normalized,
            normalized,
            recordedSha256,
            actual,
            string.Equals(actual, recordedSha256, StringComparison.OrdinalIgnoreCase));
    }

    private static string Sha256Of(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    /// <summary>
    /// The manifest is comma-delimited with unquoted paths, and some paths
    /// contain commas. The last two fields are therefore split from the RIGHT;
    /// a plain split on "," silently mangles those rows.
    /// </summary>
    private static Dictionary<string, ManifestEntry> ReadManifest(string root)
    {
        var entries = new Dictionary<string, ManifestEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadLines(Path.Combine(root, "MANIFEST.sha256")).Skip(1))
        {
            if (line.Length == 0)
            {
                continue;
            }

            var lastComma = line.LastIndexOf(',');
            if (lastComma < 0)
            {
                continue;
            }

            var sizeComma = line.LastIndexOf(',', lastComma - 1);
            if (sizeComma < 0)
            {
                continue;
            }

            var path = line[..sizeComma].Replace('\\', '/');
            var size = long.Parse(
                line[(sizeComma + 1)..lastComma], CultureInfo.InvariantCulture);
            entries[path] = new(path, size, line[(lastComma + 1)..].Trim());
        }

        return entries;
    }

    internal static string? ConfiguredPackRoot()
    {
        var root = Environment.GetEnvironmentVariable(PackRootVariable);
        return string.IsNullOrWhiteSpace(root) ? null : root;
    }

    private static string PackRoot() =>
        ConfiguredPackRoot()
        ?? throw new InvalidOperationException(
            $"{PackRootVariable} is not set; this test should have been skipped.");

    private static void WriteReport(string content)
    {
        var directory = Path.Combine(
            FindRepositoryRoot(), "artifacts", "evaluation", "v1-intake");
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            Path.Combine(directory, "principal-source-manifest.md"),
            content,
            new UTF8Encoding(false));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Repository root not found.");
    }

    [GeneratedRegex(
        @"^-\s+`(?<path>[^`]+)`\s+—\s+SHA-256\s+`(?<hash>[0-9a-fA-F]{64})`",
        RegexOptions.CultureInvariant,
        100)]
    private static partial Regex SampleReferenceRegex();

    private sealed record ManifestEntry(string Path, long Size, string Sha256);

    private sealed record ResolvedSource(
        string Reference,
        string? ResolvedPath,
        string? RecordedSha256,
        string? ComputedSha256,
        bool HashVerified);

    private sealed record AmbiguousCandidate(string Path, string Sha256, bool InManifest);
}

/// <summary>
/// The reference pack is a local, git-ignored collection that differs per
/// machine, so the test that verifies it skips with a clear reason rather than
/// failing where the pack is absent — the same idiom the other corpus-gated
/// suites use.
/// </summary>
internal sealed class ReferencePackFactAttribute : FactAttribute
{
    public ReferencePackFactAttribute()
    {
        var root = PrincipalSourceManifestTests.ConfiguredPackRoot();
        if (root is null)
        {
            Skip = $"{PrincipalSourceManifestTests.PackRootVariable} is not set; the reference "
                + "pack is a local, git-ignored collection that differs per machine.";
        }
        else if (!Directory.Exists(root))
        {
            Skip = $"{PrincipalSourceManifestTests.PackRootVariable} names a directory that does "
                + "not exist on this machine.";
        }
    }
}
