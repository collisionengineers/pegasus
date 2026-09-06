using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
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
        var item = Assert.Single(list.GetProperty("result").EnumerateArray());
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
    public async Task TriageUsesSharedCoreReadSourceAndLifecycleContractsThroughMcp()
    {
        using var factory = new IntakeWebApplicationFactory("Development", true);
        using var mcpFactory = WithAutomationMcp(factory);
        using var intakeClient = mcpFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });
        using var client = mcpFactory.CreateClient();
        var email = IntakeTestEvidence.CreateEmail(
            "triage-request.eml",
            "Triage Only Request\r\nClaimant Name: Triage Claimant\r\nClaim Number: TRIAGE-MCP\r\nVehicle Registration: AB12 CDE");
        _ = await IntakeWebDriver.UploadAndProcessAsync(
            mcpFactory, intakeClient, email.FileName, email.MediaType, email.Content);

        var token = await RequestTokenAsync(client, "automation.intake");
        using var listResponse = await PostMcpAsync(client, token,
            ToolCallPayload(10, "pegasus_triage_list", new { page = 1, pageSize = 10 }));
        var list = await ReadStructuredContentAsync(listResponse);
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
}
