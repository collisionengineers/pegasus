using CollisionDocNet.Core;
using CollisionDocNet.Model;

namespace CollisionDocNet.Email.Tests;

[TestClass]
public sealed class EmlLocalCohortSmokeTests
{
    private const string CohortPathEnvironmentVariable = "COLLISIONDOCNET_EML_COHORT_PATH";
    private readonly TestContext _testContext;

    public EmlLocalCohortSmokeTests(TestContext testContext) => _testContext = testContext;

    [TestMethod]
    [TestCategory("LocalCohort")]
    [Timeout(120_000, CooperativeCancellation = true)]
    public void Extract_OpaqueLocalCohort_ReportsAggregateOutcomesOnly()
    {
        string? configuredCohortPath = Environment.GetEnvironmentVariable(CohortPathEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(configuredCohortPath))
        {
            Assert.Inconclusive($"Set {CohortPathEnvironmentVariable} to an exact cohort directory to opt in.");
        }

        string cohortPath = Path.GetFullPath(configuredCohortPath);
        if (!Directory.Exists(cohortPath))
        {
            Assert.Fail("The explicitly configured opaque EML cohort directory is unavailable.");
        }

        ResourceLimits limits = ResourceLimits.CreateCollisionSpikeDefault();
        var outcomes = new Dictionary<ExtractionOutcome, int>();
        var issueCodes = new Dictionary<string, int>(StringComparer.Ordinal);
        var nestedOutcomes = new Dictionary<ExtractionOutcome, int>();
        var nestedIssueCodes = new Dictionary<string, int>(StringComparer.Ordinal);
        var relationshipKinds = new Dictionary<string, int>(StringComparer.Ordinal);
        int processed = 0;
        foreach (string path in Directory.EnumerateFiles(cohortPath, "*.eml", SearchOption.TopDirectoryOnly))
        {
            _testContext.CancellationToken.ThrowIfCancellationRequested();
            byte[] bytes = File.ReadAllBytes(path);
            ExtractionResult result = EmlExtractor.Extract(bytes, limits, cancellationToken: _testContext.CancellationToken);
            ExtractionResult retry = EmlExtractor.Extract(bytes, limits, cancellationToken: _testContext.CancellationToken);
            Assert.AreEqual(result.Outcome, retry.Outcome);
            CollectionAssert.AreEqual(ExtractionResultJson.SerializeToUtf8Bytes(result), ExtractionResultJson.SerializeToUtf8Bytes(retry));
            Assert.AreEqual(ExtractionOutcome.Complete, result.Outcome);
            Assert.IsGreaterThan(0, result.Content.Length + result.Metadata.Length + result.Participants.Length + result.Assets.Length + result.NestedResults.Length);
            outcomes[result.Outcome] = outcomes.GetValueOrDefault(result.Outcome) + 1;
            CountIssues(result.Issues, issueCodes);
            CountRelationships(result.Relationships, relationshipKinds);
            foreach (ExtractionResult nested in result.NestedResults)
            {
                nestedOutcomes[nested.Outcome] = nestedOutcomes.GetValueOrDefault(nested.Outcome) + 1;
                CountIssues(nested.Issues, nestedIssueCodes);
                CountRelationships(nested.Relationships, relationshipKinds);
            }
            processed++;
        }

        string aggregate = string.Join(
            ",",
            outcomes.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}"));
        _testContext.WriteLine($"EML cohort aggregate: processed={processed}; outcomes={aggregate}");
        _testContext.WriteLine($"EML cohort issue codes: {FormatCounts(issueCodes)}");
        _testContext.WriteLine($"EML cohort nested outcomes: {FormatCounts(nestedOutcomes)}");
        _testContext.WriteLine($"EML cohort nested issue codes: {FormatCounts(nestedIssueCodes)}");
        _testContext.WriteLine($"EML cohort relationship kinds: {FormatCounts(relationshipKinds)}");
        Assert.IsGreaterThan(0, processed);
        Assert.AreEqual(processed, outcomes.Values.Sum());
    }

    private static void CountIssues(IEnumerable<ExtractionIssue> issues, Dictionary<string, int> counts)
    {
        foreach (ExtractionIssue issue in issues)
        {
            counts[issue.Code] = counts.GetValueOrDefault(issue.Code) + 1;
        }
    }

    private static void CountRelationships(IEnumerable<EvidenceRelationship> relationships, Dictionary<string, int> counts)
    {
        foreach (EvidenceRelationship relationship in relationships)
        {
            counts[relationship.Kind] = counts.GetValueOrDefault(relationship.Kind) + 1;
        }
    }

    private static string FormatCounts<TKey>(IReadOnlyDictionary<TKey, int> counts) where TKey : notnull =>
        string.Join(",", counts.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}"));
}
