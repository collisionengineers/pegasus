using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Pegasus.Core.Intake;
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
        using var factory = new IntakeWebApplicationFactory("Development", true);
        using var mcpFactory = WithAutomationMcp(factory);
        using var client = mcpFactory.CreateClient();
        var email = IntakeTestEvidence.CreateEmail(
            "triage-request.eml",
            "Triage Only Request\r\nClaimant Name: Triage Claimant\r\nClaim Number: TRIAGE-MCP\r\nVehicle Registration: AB12 CDE");
        _ = await MailboxIntakeTestData.SubmitAndProcessAsync(mcpFactory.Services, email);

        var token = await RequestTokenAsync(client, "automation.intake");
        using (var toolsResponse = await PostMcpAsync(client, token, ToolsListPayload(9)))
        {
            using var toolsDocument = await ReadJsonRpcAsync(toolsResponse);
            var tools = toolsDocument.RootElement
                .GetProperty("result").GetProperty("tools").EnumerateArray()
                .Where(tool => tool.GetProperty("name").GetString() is
                    "pegasus_triage_edit_begin" or "pegasus_triage_edit_renew" or "pegasus_triage_edit_end"
                    or "pegasus_triage_cancel")
                .ToDictionary(tool => tool.GetProperty("name").GetString()!);
            Assert.Equal(4, tools.Count);
            Assert.True(tools["pegasus_triage_edit_begin"].GetProperty("inputSchema")
                .GetProperty("properties").TryGetProperty("expectedVersion", out _));
            Assert.True(tools["pegasus_triage_edit_renew"].GetProperty("inputSchema")
                .GetProperty("properties").TryGetProperty("editLeaseToken", out _));
            Assert.True(tools["pegasus_triage_edit_end"].GetProperty("inputSchema")
                .GetProperty("properties").TryGetProperty("editLeaseToken", out _));
            Assert.True(tools["pegasus_triage_cancel"].GetProperty("inputSchema")
                .GetProperty("properties").TryGetProperty("editLeaseToken", out _));
        }
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

        using (var missingLeaseResponse = await PostMcpAsync(client, token,
            ToolCallPayload(12, "pegasus_triage_cancel", new
            {
                triageId,
                expectedVersion = version,
                editLeaseToken = "",
                reason = "No longer requires Triage.",
                operationKey = "mcp:triage-cancel-missing-lease"
            })))
        {
            using var missingLease = await ReadJsonRpcAsync(missingLeaseResponse);
            Assert.True(missingLease.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
            Assert.Contains("Triage edit lease token is required", missingLease.RootElement.ToString(), StringComparison.Ordinal);
        }

        using var beginResponse = await PostMcpAsync(client, token,
            ToolCallPayload(13, "pegasus_triage_edit_begin", new
            {
                triageId,
                expectedVersion = version,
                operationKey = "mcp:triage-edit-begin"
            }));
        var lease = await ReadStructuredContentAsync(beginResponse);
        var editLeaseToken = lease.GetProperty("editLeaseToken").GetString()!;
        Assert.Equal(version, lease.GetProperty("triageVersion").GetInt64());

        using var renewResponse = await PostMcpAsync(client, token,
            ToolCallPayload(14, "pegasus_triage_edit_renew", new
            {
                triageId,
                editLeaseToken,
                operationKey = "mcp:triage-edit-renew"
            }));
        var renewedLease = await ReadStructuredContentAsync(renewResponse);
        Assert.Equal(editLeaseToken, renewedLease.GetProperty("editLeaseToken").GetString());
        Assert.Equal(version, renewedLease.GetProperty("triageVersion").GetInt64());
        Assert.True(renewedLease.GetProperty("expiresAtUtc").GetDateTimeOffset() > DateTimeOffset.UtcNow);

        var foreignToken = new string('f', 64);
        using (var foreignLeaseResponse = await PostMcpAsync(client, token,
            ToolCallPayload(15, "pegasus_triage_cancel", new
            {
                triageId,
                expectedVersion = version,
                editLeaseToken = foreignToken,
                reason = "No longer requires Triage.",
                operationKey = "mcp:triage-cancel-foreign-lease"
            })))
        {
            using var foreignLease = await ReadJsonRpcAsync(foreignLeaseResponse);
            Assert.True(foreignLease.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
            Assert.DoesNotContain(foreignToken, foreignLease.RootElement.ToString(), StringComparison.Ordinal);
        }

        using var cancelResponse = await PostMcpAsync(client, token,
            ToolCallPayload(16, "pegasus_triage_cancel", new
            {
                triageId,
                expectedVersion = version,
                editLeaseToken,
                reason = "No longer requires Triage.",
                operationKey = "mcp:triage-cancel-test"
            }));
        var cancelled = await ReadStructuredContentAsync(cancelResponse);
        Assert.Equal("Cancelled", cancelled.GetProperty("detail")
            .GetProperty("record").GetProperty("state").GetString());
        Assert.Contains(cancelled.GetProperty("detail").GetProperty("history").EnumerateArray(),
            entry => entry.GetProperty("actor").GetString() == ClientId);

        using var replacementBeginResponse = await PostMcpAsync(client, token,
            ToolCallPayload(17, "pegasus_triage_edit_begin", new
            {
                triageId,
                expectedVersion = version + 1,
                operationKey = "mcp:triage-edit-begin-after-cancel"
            }));
        var replacementLease = await ReadStructuredContentAsync(replacementBeginResponse);

        using var endResponse = await PostMcpAsync(client, token,
            ToolCallPayload(18, "pegasus_triage_edit_end", new
            {
                triageId,
                editLeaseToken = replacementLease.GetProperty("editLeaseToken").GetString(),
                operationKey = "mcp:triage-edit-end"
            }));
        Assert.True((await ReadStructuredContentAsync(endResponse)).GetProperty("released").GetBoolean());
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

        public async Task<StagedArtifactInventoryItem> StageAsync(
            Guid stagedReceiptId, string contentHash, Stream value, long contentLength,
            DateTimeOffset firstSeenAtUtc, CancellationToken cancellationToken)
        {
            using var buffer = new MemoryStream();
            await value.CopyToAsync(buffer, cancellationToken);
            var storageKey = await StoreAsync(contentHash, buffer.ToArray(), cancellationToken);
            return new(storageKey, contentHash, contentLength, firstSeenAtUtc,
                StagedArtifactDisposition.Pending, string.Empty);
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
