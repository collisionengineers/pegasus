using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Pegasus.Core.Tests.ReferenceData;

public sealed class PrincipalIdentificationCorpusTests
{
    private static readonly string[] ExpectedActiveCodes =
    [
        "ABRAHAMS", "ACSP", "ALISON", "ALL", "ALS", "AMS", "AS", "ASLS", "AVI", "AX",
        "BC", "BLACK", "CASTLE", "DFD", "FW", "GG", "HTU", "KBS", "KERR", "KMR",
        "MATT", "MOTORX", "MP", "OAK", "PCH", "QCL", "QDOS", "RELAY", "RJS", "RL",
        "SBL", "SS", "STALLION", "SWAN", "TA", "TEN", "TP", "WIL", "WLS", "YML"
    ];

    private static readonly string[] ExpectedDormantCodes =
        ["BAKER", "CW", "FRAZ", "LEX", "LPS", "MBH", "R1AM", "ROZZII", "ZENITH"];

    private static readonly HashSet<string> AllowedDispositions =
        ["principal", "alias", "supporting-identity", "archived-noise", "unresolved"];

    private static readonly string[] ForbiddenScoringProperties =
        ["confidence", "score", "threshold", "priority", "minimumConfidence"];

    private static readonly string[] ExpectedQdosDomains =
        ["qdosassist.co.uk", "qdosassists.co.uk", "qdoslaw.co.uk"];

    private static readonly string[] ExpectedEvidenceCohorts = ["development", "holdout"];

    private static readonly string[] DocumentProfileCandidateCodes =
        ["ACSP", "ALISON", "ALS", "AMS", "BC", "KERR", "KMR", "SBL", "SWAN", "TEN", "YML"];

    [Fact]
    public void CorpusHasCompleteFailClosedPrincipalCoverage()
    {
        using var document = LoadCorpus();
        var root = document.RootElement;

        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("principal-identification-corpus-v1", root.GetProperty("version").GetString());
        Assert.False(root.GetProperty("runtimeContract").GetProperty("loadedByRuntime").GetBoolean());

        var principals = root.GetProperty("principals").EnumerateArray().ToArray();
        Assert.Equal(49, principals.Length);
        var codes = principals.Select(item => item.GetProperty("code").GetString()!).ToArray();
        Assert.Equal(49, codes.Distinct(StringComparer.Ordinal).Count());

        var active = principals
            .Where(item => item.GetProperty("lifecycle").GetString() == "active")
            .Select(item => item.GetProperty("code").GetString()!)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var dormant = principals
            .Where(item => item.GetProperty("lifecycle").GetString() == "dormant")
            .Select(item => item.GetProperty("code").GetString()!)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(ExpectedActiveCodes.Order(StringComparer.Ordinal), active);
        Assert.Equal(ExpectedDormantCodes.Order(StringComparer.Ordinal), dormant);

        foreach (var principal in principals)
        {
            Assert.False(string.IsNullOrWhiteSpace(principal.GetProperty("canonicalName").GetString()));
            Assert.NotEmpty(principal.GetProperty("namesAndAliases").EnumerateArray());
            Assert.Equal(JsonValueKind.Array, principal.GetProperty("directSenderIdentities").ValueKind);
            Assert.Equal(JsonValueKind.Array, principal.GetProperty("intermediaryRelationships").ValueKind);
            Assert.Equal(JsonValueKind.Array, principal.GetProperty("instructionFormats").ValueKind);
            Assert.Equal(JsonValueKind.Array, principal.GetProperty("documentFingerprints").ValueKind);
            Assert.Equal(JsonValueKind.Array, principal.GetProperty("sharedTaxonomyPredicates").ValueKind);
            Assert.Equal(JsonValueKind.Array, principal.GetProperty("candidateTaxonomyPredicates").ValueKind);
            Assert.Equal(JsonValueKind.Array, principal.GetProperty("associationKeys").ValueKind);
            Assert.Equal(JsonValueKind.Array, principal.GetProperty("extractionLabels").ValueKind);
            Assert.NotEmpty(principal.GetProperty("negativeControls").EnumerateArray());
            Assert.NotEmpty(principal.GetProperty("evidenceRefs").EnumerateArray());
            Assert.True(principal.GetProperty("directionCoverage").TryGetProperty("received", out _));
            Assert.True(principal.GetProperty("directionCoverage").TryGetProperty("sent", out _));
        }

        var runtimeActive = principals
            .Where(item => item.GetProperty("policyState").GetString() == "runtime-active")
            .Select(item => item.GetProperty("code").GetString()!)
            .ToArray();
        Assert.Equal(["QDOS"], runtimeActive);
        Assert.All(
            principals.Where(item => item.GetProperty("lifecycle").GetString() == "dormant"),
            item => Assert.Equal("review-only", item.GetProperty("policyState").GetString()));

        foreach (var code in DocumentProfileCandidateCodes)
        {
            var principal = principals.Single(item => item.GetProperty("code").GetString() == code);
            Assert.Contains(
                principal.GetProperty("documentFingerprints").EnumerateArray(),
                fingerprint =>
                    fingerprint.GetProperty("requiredSignals").GetArrayLength() >= 2
                    && fingerprint.GetProperty("negativeSignals").GetArrayLength() > 0
                    && !fingerprint.GetProperty("criterionState").GetProperty("runtimeActive").GetBoolean());
        }
    }

    [Fact]
    public void HistoricalRowsHaveExactlyOneDispositionAndSupportingIdentitiesStaySeparate()
    {
        using var document = LoadCorpus();
        var root = document.RootElement;
        var crosswalks = root.GetProperty("historicalCrosswalks");

        AssertCrosswalk(crosswalks.GetProperty("pegasusProviderRows"), 88);
        AssertCrosswalk(crosswalks.GetProperty("pegasusOperatorJobSheetRows"), 58);
        AssertCrosswalk(crosswalks.GetProperty("collisionSpikeProviderRows"), 440);

        var principalCodes = root.GetProperty("principals")
            .EnumerateArray()
            .Select(item => item.GetProperty("code").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        var supporting = root.GetProperty("supportingIdentities").EnumerateArray().ToArray();
        Assert.NotEmpty(supporting);
        Assert.All(supporting, item =>
        {
            Assert.DoesNotContain(item.GetProperty("id").GetString()!, principalCodes);
            Assert.NotEmpty(item.GetProperty("roles").EnumerateArray());
            foreach (var relationship in item.GetProperty("relationships").EnumerateArray())
            {
                Assert.Contains(relationship.GetProperty("principalCode").GetString()!, principalCodes);
            }
        });

        Assert.DoesNotContain("CNX", principalCodes);
        Assert.DoesNotContain("EVA", principalCodes);
        Assert.DoesNotContain("CDQ", principalCodes);
        Assert.DoesNotContain("TRACTABLE", principalCodes);
    }

    [Fact]
    public void CriteriaUseExplicitMonotonicReviewStatesWithoutScores()
    {
        using var document = LoadCorpus();
        var stateCount = 0;
        Visit(document.RootElement, (propertyName, value) =>
        {
            Assert.DoesNotContain(
                propertyName,
                ForbiddenScoringProperties,
                StringComparer.OrdinalIgnoreCase);
            if (!propertyName.Equals("criterionState", StringComparison.Ordinal))
            {
                return;
            }

            stateCount++;
            Assert.Equal(JsonValueKind.Object, value.ValueKind);
            var observed = value.GetProperty("observed").GetBoolean();
            var accepted = value.GetProperty("operatorAccepted").GetBoolean();
            var active = value.GetProperty("runtimeActive").GetBoolean();
            Assert.False(accepted && !observed);
            Assert.False(active && !accepted);
        });
        Assert.True(stateCount > 100, $"Expected a substantial criterion corpus, found {stateCount} states.");
    }

    [Fact]
    public void EveryCriterionHasEvidenceAndEveryReferenceResolves()
    {
        using var document = LoadCorpus();
        var root = document.RootElement;
        var resolvableIds = root.GetProperty("sourceSnapshots").EnumerateArray()
            .Concat(root.GetProperty("evaluationSummaries").EnumerateArray())
            .Concat(root.GetProperty("evidenceItems").EnumerateArray())
            .Select(item => item.GetProperty("id").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        var criterionCount = 0;

        VisitObjects(root, item =>
        {
            if (!item.TryGetProperty("criterionState", out _))
            {
                return;
            }

            criterionCount++;
            var evidenceRefs = item.GetProperty("evidenceRefs").EnumerateArray()
                .Select(reference => reference.GetString()!)
                .ToArray();
            Assert.NotEmpty(evidenceRefs);
            Assert.All(evidenceRefs, reference =>
            {
                if (reference.StartsWith("sha256:", StringComparison.Ordinal))
                {
                    Assert.Matches("^sha256:[0-9a-f]{64}$", reference);
                    return;
                }

                var sourceId = reference.Split('#', 2)[0];
                Assert.Contains(sourceId, resolvableIds);
            });
        });

        Assert.True(criterionCount > 100);
    }

    [Fact]
    public void QdosPreservesVersionFiveAndAddsOnlyObservedReviewCandidates()
    {
        using var document = LoadCorpus();
        var qdos = document.RootElement.GetProperty("principals")
            .EnumerateArray()
            .Single(item => item.GetProperty("code").GetString() == "QDOS");

        var accepted = qdos.GetProperty("sharedTaxonomyPredicates").EnumerateArray().ToArray();
        Assert.Equal(6, accepted.Length);
        Assert.All(accepted, item =>
        {
            var criterionState = item.GetProperty("criterionState");
            Assert.True(criterionState.GetProperty("observed").GetBoolean());
            Assert.True(criterionState.GetProperty("operatorAccepted").GetBoolean());
            Assert.True(criterionState.GetProperty("runtimeActive").GetBoolean());
        });

        var candidates = qdos.GetProperty("candidateTaxonomyPredicates").EnumerateArray().ToArray();
        Assert.Equal(8, candidates.Length);
        Assert.All(candidates, item =>
        {
            Assert.Equal(JsonValueKind.Null, item.GetProperty("taxonomyTarget").ValueKind);
            var criterionState = item.GetProperty("criterionState");
            Assert.True(criterionState.GetProperty("observed").GetBoolean());
            Assert.False(criterionState.GetProperty("operatorAccepted").GetBoolean());
            Assert.False(criterionState.GetProperty("runtimeActive").GetBoolean());
        });

        var domains = qdos.GetProperty("directSenderIdentities")
            .EnumerateArray()
            .Select(item => item.GetProperty("domain").GetString()!)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(ExpectedQdosDomains, domains);
        Assert.All(qdos.GetProperty("directSenderIdentities").EnumerateArray(), identity =>
            Assert.Contains(
                "qdos-route-policy-v4",
                identity.GetProperty("evidenceRefs").EnumerateArray()
                    .Select(reference => reference.GetString())));
        Assert.All(qdos.GetProperty("extractionLabels").EnumerateArray(), label =>
            Assert.Contains(
                "qdos-extraction-policy-v7",
                label.GetProperty("evidenceRefs").EnumerateArray()
                    .Select(reference => reference.GetString())));

        var evaluation = document.RootElement.GetProperty("evaluationSummaries")
            .EnumerateArray()
            .Single(item => item.GetProperty("id").GetString() == "qdos-policy-v5-volume-evaluation");
        Assert.Equal(5, evaluation.GetProperty("classificationPolicy").GetProperty("version").GetInt32());
        var results = evaluation.GetProperty("results");
        Assert.Equal(138, results.GetProperty("processed").GetInt32());
        Assert.Equal(10, results.GetProperty("unreadable").GetInt32());
        Assert.Equal(47, results.GetProperty("routes").GetProperty("Accepted").GetInt32());
        Assert.Equal(3, results.GetProperty("matchedPredicates")
            .GetProperty("body.triage-only-request").GetInt32());
        Assert.False(evaluation.GetProperty("criterionState").GetProperty("operatorAccepted").GetBoolean());
    }

    [Fact]
    public void EvidenceIsHashDeduplicatedAndCohortsAreDeterministic()
    {
        using var document = LoadCorpus();
        var evidence = document.RootElement.GetProperty("evidenceItems").EnumerateArray().ToArray();
        Assert.NotEmpty(evidence);
        var hashes = evidence.Select(item => item.GetProperty("sha256").GetString()!).ToArray();
        Assert.Equal(hashes.Length, hashes.Distinct(StringComparer.Ordinal).Count());
        Assert.All(hashes, hash => Assert.Matches("^[0-9a-f]{64}$", hash));

        var cohorts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in evidence)
        {
            Assert.NotEmpty(item.GetProperty("sourceLocations").EnumerateArray());
            var grouping = item.GetProperty("grouping");
            var keyHash = grouping.GetProperty("keySha256").GetString()!;
            var expectedBucket = (int)(BigInteger.Parse(
                "0" + keyHash,
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture) % 10);
            var bucket = grouping.GetProperty("bucket").GetInt32();
            Assert.Equal(expectedBucket, bucket);
            var cohort = grouping.GetProperty("cohort").GetString()!;
            Assert.Equal(bucket is 0 or 1 ? "holdout" : "development", cohort);
            cohorts.Add(cohort);
        }
        Assert.Equal(ExpectedEvidenceCohorts, cohorts.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void TrackedPegasusSourceHashesHaveNotDrifted()
    {
        using var document = LoadCorpus();
        var repositoryRoot = FindRepositoryRoot();
        var snapshots = document.RootElement.GetProperty("sourceSnapshots").EnumerateArray()
            .Where(item => item.GetProperty("repository").GetString() == "pegasus")
            .Where(item => item.TryGetProperty("sha256", out _))
            .ToArray();

        Assert.NotEmpty(snapshots);
        foreach (var source in snapshots)
        {
            var relativePath = source.GetProperty("relativePath").GetString()!;
            var bytes = File.ReadAllBytes(Path.Combine(repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            var hashMode = source.GetProperty("hashMode").GetString();
            if (hashMode == "normalized-lf")
            {
                bytes = Encoding.UTF8.GetBytes(
                    Encoding.UTF8.GetString(bytes)
                        .Replace("\r\n", "\n", StringComparison.Ordinal)
                        .Replace("\r", "\n", StringComparison.Ordinal));
            }
            else
            {
                Assert.Equal("raw-bytes", hashMode);
            }
            Assert.Equal(
                source.GetProperty("sha256").GetString(),
                Convert.ToHexStringLower(SHA256.HashData(bytes)));
        }
    }

    private static void AssertCrosswalk(JsonElement rows, int expectedCount)
    {
        Assert.Equal(expectedCount, rows.GetArrayLength());
        foreach (var row in rows.EnumerateArray())
        {
            Assert.Contains(row.GetProperty("disposition").GetString()!, AllowedDispositions);
            Assert.Equal(JsonValueKind.Object, row.GetProperty("raw").ValueKind);
            Assert.Equal(JsonValueKind.Array, row.GetProperty("principalCodes").ValueKind);
        }
    }

    private static void Visit(JsonElement element, Action<string, JsonElement> visitor)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                visitor(property.Name, property.Value);
                Visit(property.Value, visitor);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                Visit(item, visitor);
            }
        }
    }

    private static void VisitObjects(JsonElement element, Action<JsonElement> visitor)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            visitor(element);
            foreach (var property in element.EnumerateObject())
            {
                VisitObjects(property.Value, visitor);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                VisitObjects(item, visitor);
            }
        }
    }

    private static JsonDocument LoadCorpus() => JsonDocument.Parse(
        File.ReadAllBytes(Path.Combine(
            FindRepositoryRoot(),
            "reference",
            "workproviders-and-repairers",
            "principal-identification-corpus.v1.json")));

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
