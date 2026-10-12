using System.Globalization;
using System.Text;
using Pegasus.Core.Cases;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Intake;
using Xunit.Abstractions;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Acceptance cohorts over the local genuine corpus (git-ignored, immutable,
/// per-machine). Labelled `extraction-corpus/QDOS/{audits,inspections,...}`
/// folders are human-filed ground truth by work type when that tree exists.
/// Volume comes from `emailevals/{general,received,sent,to-sort}` when present,
/// otherwise from a flat `corpus/*.eml` dump. Counts are reported exactly — the
/// ambiguous and unclassified totals are stated, never rounded away — and
/// outputs land under artifacts/evaluation/qdos-classification/. The corpus is
/// read-only. A machine without labelled folders still runs the volume cohort.
/// </summary>
[Trait("Category", "Corpus")]
public sealed class QdosEmailCohortTests(ITestOutputHelper output)
{
    private static readonly DateTimeOffset ReceivedAtUtc =
        new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    // The folder records what the case became, not what every message in it announced: a
    // genuine Triage request precedes the formal instruction and is filed under the case's
    // eventual work-type folder, so a pre-instruction category is a correct message-level
    // classification anywhere. The hard guard is the cross-work-type row: an audit letter
    // must never classify as inspection work and vice versa, and a triage-labelled email
    // must never classify as a new instruction.
    private static readonly (string Folder, MailCategory Category, CaseType[] CaseTypes)[] LabelledFolders =
    [
        ("audits", MailCategory.NewInstruction, [CaseType.Audit]),
        ("inspections", MailCategory.NewInstruction, [CaseType.Inspection, CaseType.InspectionAndAudit]),
        ("inspection-and-audit", MailCategory.NewInstruction, [CaseType.Inspection, CaseType.InspectionAndAudit]),
        ("triage", MailCategory.TriageRequest, [])
    ];

    private static readonly MailCategory[] PreInstructionCategories =
        [MailCategory.TriageRequest, MailCategory.PreInstruction, MailCategory.ImagesReceived];

    private static string? Code(MailClassificationResult result) =>
        result.Category is { } category ? MailCategories.Code(category) : null;

    [QdosLabelledCorpusFact]
    public async Task LabelledWorkTypeEmailsNeverMisclassifyAcrossFamilies()
    {
        var reader = new MimeKitPdfPigOpenXmlIntakeSourceReader(TimeProvider.System);
        var policy = new PrincipalMailClassificationPolicy("QDOS");
        var rows = new StringBuilder("label,file,outcome,category,case_type\n");
        var processed = 0;
        var classified = 0;
        var honestlyUndecided = 0;

        foreach (var (folder, expectedCategory, expectedCaseTypes) in LabelledFolders)
        {
            var root = Path.Combine(QdosCorpus.ExtractionRoot, folder);
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var path in EnumerateEmails(root))
            {
                var readResult = await reader.ReadAsync(
                    Source(path, processed),
                    CancellationToken.None);
                if (readResult.Status != IntakeSourceReadStatus.Readable)
                {
                    rows.AppendLine(CultureInfo.InvariantCulture, $"{folder},{CsvName(path)},unreadable,,");
                    processed++;
                    continue;
                }

                var result = policy.Classify(readResult);
                rows.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"{folder},{CsvName(path)},{result.Outcome},{Code(result)},{result.CaseType}");
                processed++;
                if (result.Outcome == MailClassificationOutcome.Classified)
                {
                    classified++;
                    var category = result.Category!.Value;
                    var matchesFolder =
                        category == expectedCategory
                        && (expectedCaseTypes.Length == 0
                            || (result.CaseType is { } caseType && expectedCaseTypes.Contains(caseType)));
                    var isMessageLevelPreInstruction = PreInstructionCategories.Contains(category);
                    Assert.True(
                        matchesFolder || isMessageLevelPreInstruction,
                        $"'{Path.GetFileName(path)}' in '{folder}' classified as "
                            + $"{MailCategories.Code(category)}/{result.CaseType} against the folder's ground truth.");
                }
                else
                {
                    honestlyUndecided++;
                }
            }
        }

        QdosCorpus.WriteArtifact("labelled-cohort.csv", rows.ToString());
        output.WriteLine(
            $"labelled cohort: processed={processed}; classified={classified}; "
            + $"unclassified={honestlyUndecided}");
        Assert.True(processed > 0, "The labelled QDOS extraction corpus yielded no emails.");
    }

    [QdosCorpusFact]
    public async Task VolumeCohortRecordsExactOutcomeCounts()
    {
        var reader = new MimeKitPdfPigOpenXmlIntakeSourceReader(TimeProvider.System);
        var routePolicy = new PrincipalMailRoutePolicy();
        var classificationPolicy = new PrincipalMailClassificationPolicy("QDOS");
        var matchPolicy = new PrincipalCaseMatchPolicy(new QdosInstructionExtractionPolicy());
        var routeCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var familyCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var matchedPredicateCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var claimTokens = 0;
        var acceptedRoute = 0;
        var unreadable = 0;
        var processed = 0;
        var rows = new StringBuilder(
            "file,route,classification,category,case_type,claim_token,vrm,surname,incident_date\n");

        foreach (var path in QdosCorpus.VolumeRoots
                     .SelectMany(EnumerateEmails)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var readResult = await reader.ReadAsync(Source(path, processed), CancellationToken.None);
            processed++;
            if (readResult.Status != IntakeSourceReadStatus.Readable || readResult.IsIncomplete)
            {
                unreadable++;
                rows.AppendLine(CultureInfo.InvariantCulture, $"{CsvName(path)},unreadable,,,,,,,");
                continue;
            }

            var route = routePolicy.Evaluate(readResult);
            Count(routeCounts, route.Disposition.ToString());
            if (route.Disposition != MailRouteDisposition.Accepted)
            {
                rows.AppendLine(CultureInfo.InvariantCulture, $"{CsvName(path)},{route.Disposition},,,,,,,");
                continue;
            }

            acceptedRoute++;
            var classification = classificationPolicy.Classify(readResult);
            foreach (var predicate in classification.Predicates.Where(item => item.Matched))
            {
                Count(matchedPredicateCounts, predicate.Key);
            }
            Count(
                familyCounts,
                classification.Outcome == MailClassificationOutcome.Classified
                    ? Code(classification)!
                    : classification.Outcome.ToString());
            var keys = matchPolicy.ExtractMatchKeys(readResult);
            if (keys.DurableClaimToken is not null)
            {
                claimTokens++;
            }

            rows.AppendLine(
                CultureInfo.InvariantCulture,
                $"{CsvName(path)},{route.Disposition},{classification.Outcome},"
                + $"{Code(classification)},{classification.CaseType},"
                + $"{keys.DurableClaimToken},{keys.NormalizedVrm},{keys.NormalizedSurname},"
                + $"{keys.IncidentDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        }

        QdosCorpus.WriteArtifact("cohort-results.csv", rows.ToString());
        output.WriteLine($"volume cohort: processed={processed}; unreadable={unreadable}");
        output.WriteLine(
            $"routes: {string.Join("; ", routeCounts.OrderBy(item => item.Key).Select(item => $"{item.Key}={item.Value}"))}");
        output.WriteLine(
            $"accepted-route categories: {string.Join("; ", familyCounts.OrderBy(item => item.Key).Select(item => $"{item.Key}={item.Value}"))}");
        output.WriteLine(
            $"matched classification predicates: {string.Join("; ", matchedPredicateCounts.OrderBy(item => item.Key).Select(item => $"{item.Key}={item.Value}"))}");
        output.WriteLine($"claim-token coverage: {claimTokens}/{acceptedRoute} accepted-route emails");
        Assert.True(processed > 0, "The volume corpus yielded no emails.");
    }

    [QdosLabelledCorpusFact]
    public async Task LabelledClaimTokensNeverCollideAcrossCaseFolders()
    {
        var reader = new MimeKitPdfPigOpenXmlIntakeSourceReader(TimeProvider.System);
        var matchPolicy = new PrincipalCaseMatchPolicy(new QdosInstructionExtractionPolicy());
        var tokensByCaseFolder = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var processed = 0;
        var withToken = 0;

        foreach (var (folder, _, _) in LabelledFolders)
        {
            var root = Path.Combine(QdosCorpus.ExtractionRoot, folder);
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var path in EnumerateEmails(root))
            {
                var readResult = await reader.ReadAsync(Source(path, processed), CancellationToken.None);
                processed++;
                if (readResult.Status != IntakeSourceReadStatus.Readable)
                {
                    continue;
                }

                var keys = matchPolicy.ExtractMatchKeys(readResult);
                if (keys.DurableClaimToken is null)
                {
                    continue;
                }

                withToken++;
                var caseFolder = Path.GetRelativePath(QdosCorpus.ExtractionRoot, Path.GetDirectoryName(path)!);
                tokensByCaseFolder.TryAdd(caseFolder, new(StringComparer.OrdinalIgnoreCase));
                tokensByCaseFolder[caseFolder].Add(keys.DurableClaimToken);
            }
        }

        output.WriteLine($"claim tokens: {withToken}/{processed} labelled emails");
        var collisions = tokensByCaseFolder
            .SelectMany(entry => entry.Value.Select(token => (entry.Key, token)))
            .GroupBy(item => item.token, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Select(item => item.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        Assert.True(
            collisions.Length == 0,
            $"Claim tokens collide across distinct case folders: {string.Join(", ", collisions)}.");
        Assert.True(processed > 0, "The labelled QDOS extraction corpus yielded no emails.");
    }

    private static IEnumerable<string> EnumerateEmails(string root) =>
        Directory.EnumerateFiles(root, "*.eml", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal);

    private static IntakeSource Source(string path, int index) =>
        new(
            Path.GetFileName(path),
            "message/rfc822",
            File.ReadAllBytes(path),
            ReceivedAtUtc,
            "cohort-evaluation",
            new(IntakeSourceChannel.Mailbox, $"cohort-{index:00000}"));

    private static string CsvName(string path) =>
        $"\"{Path.GetFileName(path).Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static void Count(Dictionary<string, int> counts, string key) =>
        counts[key] = counts.GetValueOrDefault(key) + 1;
}

internal static class QdosCorpus
{
    private static readonly string[] LabelledFolderNames =
        ["audits", "inspections", "inspection-and-audit", "triage"];

    public static string Root =>
        Environment.GetEnvironmentVariable("PEGASUS_CORPUS_ROOT")
        ?? DiscoverCorpusRoot();

    public static string ExtractionRoot => Path.Combine(Root, "extraction-corpus", "QDOS");

    public static string[] VolumeRoots
    {
        get
        {
            string[] specific =
            [
                Path.Combine(Root, "emailevals", "general"),
                Path.Combine(Root, "emailevals", "received"),
                Path.Combine(Root, "emailevals", "sent"),
                Path.Combine(Root, "emailevals", "to-sort"),
                ExtractionRoot
            ];
            var present = specific.Where(Directory.Exists).ToArray();
            if (present.Length > 0)
            {
                return present;
            }

            return ContainsEml(Root) ? [Root] : [];
        }
    }

    public static bool HasLabelledFolders =>
        Directory.Exists(ExtractionRoot)
        && LabelledFolderNames.Any(folder =>
            Directory.Exists(Path.Combine(ExtractionRoot, folder)));

    public static bool IsPresent => VolumeRoots.Any(ContainsEml) || HasLabelledFolders;

    public static void WriteArtifact(string fileName, string content)
    {
        var directory = Path.Combine(
            FindRepositoryRoot(),
            "artifacts",
            "evaluation",
            "qdos-classification");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, fileName), content);
    }

    private static bool ContainsEml(string directory) =>
        Directory.Exists(directory)
        && Directory.EnumerateFiles(directory, "*.eml", SearchOption.AllDirectories).Any();

    /// <summary>
    /// Worktrees do not receive the ignored <c>corpus/</c> tree. Prefer the
    /// worktree's own corpus when it has mail, otherwise the primary checkout
    /// reached through the common git directory. The corpus is never written.
    /// </summary>
    private static string DiscoverCorpusRoot()
    {
        var repoRoot = FindRepositoryRoot();
        var local = Path.Combine(repoRoot, "corpus");
        if (ContainsEml(local))
        {
            return local;
        }

        var gitFile = Path.Combine(repoRoot, ".git");
        if (File.Exists(gitFile))
        {
            foreach (var line in File.ReadLines(gitFile))
            {
                const string prefix = "gitdir:";
                if (!line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var gitDir = line[prefix.Length..].Trim();
                var commonGit = Path.GetFullPath(Path.Combine(gitDir, "..", ".."));
                if (!Path.GetFileName(commonGit)
                        .Equals(".git", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var primary = Directory.GetParent(commonGit)?.FullName;
                if (primary is null)
                {
                    continue;
                }

                var primaryCorpus = Path.Combine(primary, "corpus");
                if (ContainsEml(primaryCorpus))
                {
                    return primaryCorpus;
                }
            }
        }

        return local;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}

internal sealed class QdosCorpusFactAttribute : FactAttribute
{
    public QdosCorpusFactAttribute()
    {
        if (!QdosCorpus.IsPresent)
        {
            Skip = "This machine's ignored local corpus has no QDOS email trees; corpora differ per system.";
        }
    }
}

internal sealed class QdosLabelledCorpusFactAttribute : FactAttribute
{
    public QdosLabelledCorpusFactAttribute()
    {
        if (!QdosCorpus.HasLabelledFolders)
        {
            Skip = "This machine's ignored local corpus has no labelled QDOS extraction-corpus folders; corpora differ per system.";
        }
    }
}
