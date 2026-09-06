using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Pegasus.Core.Intake;
using Pegasus.Core.Identity;
using Pegasus.Core.Triage;
using static Pegasus.IntegrationTests.AutomationMcpTestSupport;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class AutomationIntakeParityIngressTests
{
    [Fact]
    public async Task UnidentifiedReceiptCanBeListedInspectedAndDownloadedThroughMcp()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var mcpFactory = WithAutomationMcp(factory);
        using var intakeClient = mcpFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });
        using var client = mcpFactory.CreateClient();
        _ = await IntakeWebDriver.UploadAndProcessAsync(
            mcpFactory,
            intakeClient,
            "corrupt.pdf",
            "application/pdf",
            "not a PDF"u8.ToArray());

        var token = await RequestTokenAsync(client, "automation.intake");
        using var listResponse = await PostMcpAsync(client, token,
            ToolCallPayload(1, "pegasus_unidentified_list", new { }));
        var list = await ReadStructuredContentAsync(listResponse);
        Assert.Equal(50, list.GetProperty("limit").GetInt32());
        Assert.True(
            !list.TryGetProperty("nextCursor", out var nextCursor)
            || nextCursor.ValueKind == JsonValueKind.Null);
        var item = Assert.Single(list.GetProperty("items").EnumerateArray());
        var reference = item.GetProperty("reference").GetString();

        using var getResponse = await PostMcpAsync(client, token,
            ToolCallPayload(2, "pegasus_unidentified_get", new { reference }));
        var detail = await ReadStructuredContentAsync(getResponse);
        var source = Assert.Single(detail.GetProperty("sources").EnumerateArray());
        var receiptId = source.GetProperty("receiptId").GetGuid();

        using var downloadResponse = await PostMcpAsync(client, token,
            ToolCallPayload(3, "pegasus_unidentified_source_download",
                new { reference, memberReceiptId = receiptId, maxInlineBytes = 1024 }));
        var download = await ReadStructuredContentAsync(downloadResponse);
        Assert.True(download.GetProperty("contentIncluded").GetBoolean());
        Assert.Equal("not a PDF", Encoding.UTF8.GetString(
            Convert.FromBase64String(download.GetProperty("contentBase64").GetString()!)));
    }

    [Fact]
    public async Task OversizedUnidentifiedSourceReturnsAuthorizedMetadataWithoutReadingContent()
    {
        var artifacts = new CountingArtifactStore();
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            artifactStore: artifacts);
        using var mcpFactory = WithAutomationMcp(factory);
        using var intakeClient = mcpFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });
        using var client = mcpFactory.CreateClient();
        var content = Encoding.UTF8.GetBytes(new string('x', 2 * 1024));
        _ = await IntakeWebDriver.UploadAndProcessAsync(
            mcpFactory,
            intakeClient,
            "oversized-corrupt.pdf",
            "application/pdf",
            content);

        var token = await RequestTokenAsync(client, "automation.intake");
        using var listResponse = await PostMcpAsync(client, token,
            ToolCallPayload(21, "pegasus_unidentified_list", new { }));
        var list = await ReadStructuredContentAsync(listResponse);
        var reference = Assert.Single(list.GetProperty("items").EnumerateArray())
            .GetProperty("reference")
            .GetString();
        using var detailResponse = await PostMcpAsync(client, token,
            ToolCallPayload(22, "pegasus_unidentified_get", new { reference }));
        var detail = await ReadStructuredContentAsync(detailResponse);
        var receiptId = Assert.Single(detail.GetProperty("sources").EnumerateArray())
            .GetProperty("receiptId")
            .GetGuid();
        var readsBeforeDownload = artifacts.ReadCount;

        using var downloadResponse = await PostMcpAsync(client, token,
            ToolCallPayload(23, "pegasus_unidentified_source_download",
                new { reference, memberReceiptId = receiptId, maxInlineBytes = 1024 }));
        var download = await ReadStructuredContentAsync(downloadResponse);

        Assert.False(download.GetProperty("contentIncluded").GetBoolean());
        Assert.Equal(content.Length, download.GetProperty("contentLength").GetInt64());
        Assert.True(
            !download.TryGetProperty("contentBase64", out var omittedContent)
            || omittedContent.ValueKind == JsonValueKind.Null);
        Assert.Equal(readsBeforeDownload, artifacts.ReadCount);
    }

    [Fact]
    public async Task TriageUsesSharedCoreReadSourceAndLifecycleContractsThroughMcp()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            extractionPolicy: new AcceptedTriageMatchPolicy());
        using var mcpFactory = WithAutomationMcp(factory);
        using var intakeClient = mcpFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });
        using var client = mcpFactory.CreateClient();
        var email = IntakeTestEvidence.CreateEmail(
            "triage-request.eml",
            "QDOS instruction\r\nClaimant Name: Triage Claimant\r\nClaim Number: TRIAGE-MCP\r\nVehicle Registration: AB12 CDE");
        _ = await IntakeWebDriver.UploadAndProcessAsync(
            mcpFactory, intakeClient, email.FileName, email.MediaType, email.Content);

        var token = await RequestTokenAsync(client, "automation.intake");
        using var listResponse = await PostMcpAsync(client, token,
            ToolCallPayload(10, "pegasus_triage_list", new { limit = 10 }));
        var list = await ReadStructuredContentAsync(listResponse);
        Assert.Equal(10, list.GetProperty("limit").GetInt32());
        Assert.True(
            !list.TryGetProperty("nextCursor", out var nextCursor)
            || nextCursor.ValueKind == JsonValueKind.Null);
        var item = Assert.Single(list.GetProperty("items").EnumerateArray());
        var triageId = item.GetProperty("id").GetGuid();
        var version = item.GetProperty("version").GetInt64();

        using var sourceResponse = await PostMcpAsync(client, token,
            ToolCallPayload(11, "pegasus_triage_source_download", new { triageId }));
        var source = await ReadStructuredContentAsync(sourceResponse);
        Assert.True(source.GetProperty("contentIncluded").GetBoolean());

        using var cancelResponse = await PostMcpAsync(client, token,
            ToolCallPayload(12, "pegasus_triage_cancel", new
            {
                triageId,
                expectedVersion = version,
                reason = "No longer requires Triage.",
                operationKey = "mcp:triage-cancel-test"
            }));
        var cancelled = await ReadStructuredContentAsync(cancelResponse);
        Assert.Equal("Cancelled", cancelled.GetProperty("detail")
            .GetProperty("record").GetProperty("state").GetString());
        Assert.Contains(cancelled.GetProperty("detail").GetProperty("history").EnumerateArray(),
            entry => entry.GetProperty("actor").GetString() == ClientId);
    }

    private sealed class AcceptedTriageMatchPolicy : IInstructionExtractionPolicy
    {
        private readonly QdosInstructionExtractionPolicy inner = new();

        public string PrincipalCode => inner.PrincipalCode;

        public InstructionExtractionResult Extract(
            IntakeSourceReadResult readResult,
            DateTimeOffset processedAtUtc,
            EstablishedPrincipalContext principalContext)
        {
            var result = inner.Extract(readResult, processedAtUtc, principalContext);
            return result.Applicability != InstructionPolicyApplicability.Applicable
                ? result
                : result with
                {
                    Evidence =
                    [
                        .. result.Evidence,
                        new IntakeEvidence(
                            IntakeEvidenceSource.EmailBody,
                            IntakeEvidenceStrength.Strong,
                            IntakeEvidenceFinding.AcceptedTriageMatch,
                            "accepted-triage-request",
                            "The existing fixture represents an accepted Triage match.",
                            "automation-mcp-triage-test",
                            1)
                    ]
                };
        }
    }

    private sealed class CountingArtifactStore : IIntakeArtifactStore
    {
        private readonly Dictionary<string, ReadOnlyMemory<byte>> content = [];

        public int ReadCount { get; private set; }

        public Task<string> StoreAsync(
            string contentHash,
            ReadOnlyMemory<byte> value,
            CancellationToken cancellationToken)
        {
            var storageKey = $"sha256/{contentHash[..2]}/{contentHash}";
            content[storageKey] = value.ToArray();
            return Task.FromResult(storageKey);
        }

        public Task<ReadOnlyMemory<byte>?> ReadAsync(
            string storageKey,
            CancellationToken cancellationToken)
        {
            ReadCount++;
            return Task.FromResult(
                content.TryGetValue(storageKey, out var value)
                    ? (ReadOnlyMemory<byte>?)value
                    : null);
        }
    }
}
