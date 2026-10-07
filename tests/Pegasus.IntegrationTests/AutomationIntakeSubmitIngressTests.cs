using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using static Pegasus.IntegrationTests.AutomationMcpTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>
/// MCP-03 caller evidence for the two intake writes the tranche rule had not
/// yet seen a real caller for: a source submitted on the automation channel,
/// and an Unidentified item resolved through the same Core command as staff.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class AutomationIntakeSubmitIngressTests
{
    [Fact]
    public async Task SubmittedSourceIsQueuedProcessedListedAndResolvedThroughMcp()
    {
        using var factory = new IntakeWebApplicationFactory("Development", true);
        using var mcpFactory = WithAutomationMcp(factory);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, "automation.intake");
        var content = "not a PDF"u8.ToArray();
        const string receiptToken = "network-drive:scan-0001";

        Guid stagedReceiptId;
        using (var response = await PostMcpAsync(client, token, ToolCallPayload(1, "pegasus_intake_submit", new
        {
            fileName = "scan-0001.pdf",
            mediaType = "application/pdf",
            contentBase64 = Convert.ToBase64String(content),
            externalReceiptToken = receiptToken,
            operationKey = "mcp:intake-submit-1"
        })))
        {
            var submitted = await ReadStructuredContentAsync(response);
            Assert.False(submitted.GetProperty("isDuplicate").GetBoolean());
            Assert.Equal(receiptToken, submitted.GetProperty("externalReceiptToken").GetString());
            stagedReceiptId = submitted.GetProperty("receiptId").GetGuid();
        }

        // The same token with the same bytes is the same submission.
        using (var response = await PostMcpAsync(client, token, ToolCallPayload(2, "pegasus_intake_submit", new
        {
            fileName = "scan-0001.pdf",
            mediaType = "application/pdf",
            contentBase64 = Convert.ToBase64String(content),
            externalReceiptToken = receiptToken,
            operationKey = "mcp:intake-submit-2"
        })))
        {
            Assert.True((await ReadStructuredContentAsync(response)).GetProperty("isDuplicate").GetBoolean());
        }

        // Different bytes under the same token fail closed.
        using (var response = await PostMcpAsync(client, token, ToolCallPayload(3, "pegasus_intake_submit", new
        {
            fileName = "scan-0001.pdf",
            mediaType = "application/pdf",
            contentBase64 = Convert.ToBase64String("different bytes"u8.ToArray()),
            externalReceiptToken = receiptToken,
            operationKey = "mcp:intake-submit-3"
        })))
        {
            _ = await ReadErrorTextAsync(response);
        }

        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM ActionHistory
            WHERE ActorKind = N'Automation'
              AND EventKind = N'pegasus_intake_submit'
              AND Outcome = N'Failed'
            """));

        // The Worker's processing, run in-process as the Worker would run it.
        await using (var scope = mcpFactory.Services.CreateAsyncScope())
        {
            _ = await IntakeWebDriver.DrainStagedAsync(scope.ServiceProvider, stagedReceiptId);
        }

        using (var response = await PostMcpAsync(client, token, ToolCallPayload(4, "pegasus_intake_queue_list", new { })))
        {
            var queue = await ReadStructuredContentAsync(response);
            Assert.NotEmpty(queue.GetProperty("items").EnumerateArray());
        }

        string reference;
        long version;
        using (var response = await PostMcpAsync(client, token, ToolCallPayload(5, "pegasus_unidentified_list", new { })))
        {
            var list = await ReadStructuredContentAsync(response);
            reference = Assert.Single(list.GetProperty("items").EnumerateArray()).GetProperty("reference").GetString()!;
        }
        using (var response = await PostMcpAsync(client, token, ToolCallPayload(6, "pegasus_unidentified_get", new { reference })))
        {
            var detail = await ReadStructuredContentAsync(response);
            version = detail.GetProperty("item").GetProperty("version").GetInt64();
            Assert.Equal("Open", detail.GetProperty("item").GetProperty("state").GetString());
        }

        // A stale version is refused before anything moves.
        using (var response = await PostMcpAsync(client, token, ToolCallPayload(7, "pegasus_unidentified_resolve", new
        {
            reference,
            expectedVersion = version + 5,
            reason = "Belongs to an external file.",
            targetKind = "ExternalReference",
            targetId = "External file 42",
            targetReference = (string?)null,
            operationKey = "mcp:unidentified-resolve-stale"
        })))
        {
            _ = await ReadErrorTextAsync(response);
        }

        using (var response = await PostMcpAsync(client, token, ToolCallPayload(8, "pegasus_unidentified_resolve", new
        {
            reference,
            expectedVersion = version,
            reason = "Belongs to an external file.",
            targetKind = "ExternalReference",
            targetId = "External file 42",
            targetReference = (string?)null,
            operationKey = "mcp:unidentified-resolve"
        })))
        {
            var resolved = await ReadStructuredContentAsync(response);
            Assert.NotEqual("Open", resolved.GetProperty("state").GetString());
            Assert.True(resolved.GetProperty("version").GetInt64() > version);
        }

        using (var response = await PostMcpAsync(client, token, ToolCallPayload(9, "pegasus_unidentified_list", new { })))
        {
            var list = await ReadStructuredContentAsync(response);
            Assert.Empty(list.GetProperty("items").EnumerateArray());
        }

        // Reopen and Close with reason are the staff acts too (ADR-0064).
        using (var response = await PostMcpAsync(client, token, ToolCallPayload(10, "pegasus_unidentified_get", new { reference })))
        {
            version = (await ReadStructuredContentAsync(response)).GetProperty("item").GetProperty("version").GetInt64();
        }
        using (var response = await PostMcpAsync(client, token, ToolCallPayload(11, "pegasus_unidentified_reopen", new
        {
            reference,
            expectedVersion = version,
            reason = "The external file was the wrong one.",
            operationKey = "mcp:unidentified-reopen"
        })))
        {
            var reopened = await ReadStructuredContentAsync(response);
            Assert.Equal("Open", reopened.GetProperty("state").GetString());
            version = reopened.GetProperty("version").GetInt64();
        }

        // Closed names no destination.
        using (var response = await PostMcpAsync(client, token, ToolCallPayload(12, "pegasus_unidentified_resolve", new
        {
            reference,
            expectedVersion = version,
            reason = "Not ours.",
            targetKind = "Closed",
            targetId = "closed",
            operationKey = "mcp:unidentified-close-with-target"
        })))
        {
            Assert.Contains("no destination", await ReadErrorTextAsync(response), StringComparison.Ordinal);
        }

        using (var response = await PostMcpAsync(client, token, ToolCallPayload(13, "pegasus_unidentified_resolve", new
        {
            reference,
            expectedVersion = version,
            reason = "Not ours.",
            targetKind = "Closed",
            operationKey = "mcp:unidentified-close"
        })))
        {
            var closed = await ReadStructuredContentAsync(response);
            Assert.NotEqual("Open", closed.GetProperty("state").GetString());
        }

        Assert.Equal(2, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM ActionHistory
            WHERE ActorKind = N'Automation'
              AND EventKind = N'pegasus_unidentified_resolve'
              AND Outcome = N'Succeeded'
            """));
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM ActionHistory
            WHERE ActorKind = N'Automation'
              AND EventKind = N'pegasus_unidentified_reopen'
              AND Outcome = N'Succeeded'
            """));
    }

    [Fact]
    public async Task IntakeWritesEnforceTheIntakeScope()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        using var client = mcpFactory.CreateClient();
        var casesOnlyToken = await RequestTokenAsync(client, "automation.cases");

        var payloads = new[]
        {
            ToolCallPayload(1, "pegasus_intake_submit", new
            {
                fileName = "scan.pdf",
                mediaType = "application/pdf",
                contentBase64 = Convert.ToBase64String("x"u8.ToArray()),
                externalReceiptToken = "scope-probe",
                operationKey = "mcp:intake-submit-scope"
            }),
            ToolCallPayload(2, "pegasus_unidentified_resolve", new
            {
                reference = "U1",
                expectedVersion = 0,
                reason = "Scope probe.",
                targetKind = "ExternalReference",
                targetId = "x",
                targetReference = (string?)null,
                operationKey = "mcp:unidentified-resolve-scope"
            }),
            ToolCallPayload(3, "pegasus_unidentified_reopen", new
            {
                reference = "U1",
                expectedVersion = 0,
                reason = "Scope probe.",
                operationKey = "mcp:unidentified-reopen-scope"
            })
        };
        foreach (var payload in payloads)
        {
            using var response = await PostMcpAsync(client, casesOnlyToken, payload);
            Assert.Contains("automation.intake", await ReadErrorTextAsync(response), StringComparison.Ordinal);
        }
    }
}
