using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core;

namespace Pegasus.IntegrationTests;

[Collection(LocalDbFixtureDefinition.Name)]
public sealed class QdosAlphaAcceptanceGateTests
{
    private const string ManifestEnvironmentVariable = "PEGASUS_QDOS_ACCEPTANCE_MANIFEST";
    private const string RevisionEnvironmentVariable = "PEGASUS_QDOS_ACCEPTANCE_SOURCE_REVISION";
    private static readonly JsonSerializerOptions ManifestSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    [Fact]
    public void GateIsRegisteredInTheActualWebCompositionRoot()
    {
        using var factory = new IntakeWebApplicationFactory();

        var gate = factory.Services.GetRequiredService<QdosAlphaAcceptanceGate>();

        Assert.NotNull(gate);
    }

    [Fact]
    public void GateFailsClosedWhenCallerAndApprovalEvidenceIsAbsent()
    {
        var decision = new QdosAlphaAcceptanceGate().Evaluate(new(
            1,
            QdosAlphaAcceptanceGate.AcceptanceManifestKind,
            new string('a', 40),
            new string('b', 32),
            [],
            []));

        Assert.False(decision.OfflineCandidateAccepted);
        Assert.False(decision.ReleaseAccepted);
        Assert.Contains("capability:INT-01:missing", decision.Blockers);
        Assert.Contains("external-gate:approved-capacity-dataset:missing", decision.Blockers);
        Assert.Contains("external-gate:qdos-operator-acceptance:missing", decision.Blockers);
        Assert.Contains("external-gate:collision-engineers-management-approval:missing", decision.Blockers);
    }

    [Fact]
    public void GateRejectsAnUnversionedManifestBeforeEvaluatingEvidence()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new QdosAlphaAcceptanceGate().Evaluate(new(
                0,
                "unversioned",
                new string('a', 40),
                new string('b', 32),
                [],
                [])));

        Assert.Contains("schema version 1", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GateRejectsDuplicateEvidenceAndDeferralOfLocallyExercisableCapability()
    {
        var observation = new QdosAlphaCapabilityObservation(
            "INT-01",
            QdosAlphaCapabilityEvidenceOutcome.DeferredToExternalGate,
            "Pegasus.Web /Intake",
            "intake-upload.trx",
            new string('c', 64));
        var decision = new QdosAlphaAcceptanceGate().Evaluate(new(
            1,
            QdosAlphaAcceptanceGate.AcceptanceManifestKind,
            new string('a', 40),
            new string('b', 32),
            [observation, observation],
            []));

        Assert.False(decision.OfflineCandidateAccepted);
        Assert.Contains("capability:INT-01:duplicate", decision.Blockers);
        Assert.Contains("capability:INT-01:cannot-defer", decision.Blockers);
    }

    [QdosAlphaAcceptanceManifestFact]
    [Trait("Category", "QdosAlphaAcceptance")]
    public async Task RunnerManifestInvokesCoreGateThroughActualWebHost()
    {
        var manifestPath = Path.GetFullPath(
            Environment.GetEnvironmentVariable(ManifestEnvironmentVariable)!);
        var expectedRevision = Environment.GetEnvironmentVariable(RevisionEnvironmentVariable);
        Assert.NotNull(expectedRevision);
        Assert.Matches("^[a-f0-9]{40}$", expectedRevision);

        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        using (var diagnosticResponse = await client.GetAsync("/diagnostics/version"))
        {
            Assert.True(
                diagnosticResponse.IsSuccessStatusCode,
                $"Compiled Web build diagnostic failed with status {(int)diagnosticResponse.StatusCode}.");
            using var diagnostic = JsonDocument.Parse(
                await diagnosticResponse.Content.ReadAsByteArrayAsync());
            Assert.Equal(
                expectedRevision,
                diagnostic.RootElement.GetProperty("sourceSha").GetString());
        }

        await using var stream = File.OpenRead(manifestPath);
        var request = await JsonSerializer.DeserializeAsync<QdosAlphaAcceptanceRequest>(
            stream,
            ManifestSerializerOptions);
        Assert.NotNull(request);
        Assert.NotNull(request.CapabilityObservations);
        Assert.NotNull(request.ExternalGateEvidence);
        Assert.Equal(expectedRevision, request.SourceRevision);

        var manifestDirectory = Path.GetDirectoryName(manifestPath)!;
        foreach (var observation in request.CapabilityObservations)
        {
            AssertEvidenceHash(manifestDirectory, observation.EvidenceReference, observation.EvidenceSha256);
        }

        foreach (var evidence in request.ExternalGateEvidence)
        {
            AssertEvidenceHash(manifestDirectory, evidence.EvidenceReference, evidence.EvidenceSha256);
        }

        var gate = factory.Services.GetRequiredService<QdosAlphaAcceptanceGate>();
        var decision = gate.Evaluate(request);

        Assert.True(
            decision.OfflineCandidateAccepted,
            "QDOS offline acceptance is blocked: " + string.Join(", ", decision.Blockers));
    }

    private static void AssertEvidenceHash(
        string manifestDirectory,
        string evidenceReference,
        string expectedSha256)
    {
        Assert.False(string.IsNullOrWhiteSpace(evidenceReference));
        var evidencePath = Path.IsPathRooted(evidenceReference)
            ? Path.GetFullPath(evidenceReference)
            : Path.GetFullPath(Path.Combine(manifestDirectory, evidenceReference));
        Assert.True(File.Exists(evidencePath), $"Acceptance evidence does not exist: {evidencePath}");
        using var stream = File.OpenRead(evidencePath);
        var observedHash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        Assert.Equal(expectedSha256, observedHash);
    }
}

internal sealed class QdosAlphaAcceptanceManifestFactAttribute : FactAttribute
{
    public QdosAlphaAcceptanceManifestFactAttribute()
    {
        var manifestPath = Environment.GetEnvironmentVariable(
            "PEGASUS_QDOS_ACCEPTANCE_MANIFEST");
        var sourceRevision = Environment.GetEnvironmentVariable(
            "PEGASUS_QDOS_ACCEPTANCE_SOURCE_REVISION");
        if (string.IsNullOrWhiteSpace(manifestPath) || string.IsNullOrWhiteSpace(sourceRevision))
        {
            Skip = "The QDOS alpha acceptance runner manifest was not supplied.";
        }
    }
}
