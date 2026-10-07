using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Persistence;
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
            "unplaced.pdf",
            "application/pdf",
            IntakeTestEvidence.CreatePdf("Unplaced source text for the Unidentified queue."));

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

        // A PDF comes back as its page text, the way Claude's hosted connector
        // can take it; the structured half keeps identity, hash and URL.
        using var downloadResponse = await PostMcpAsync(client, token,
            ToolCallPayload(3, "pegasus_unidentified_source_download",
                new { reference, memberReceiptId = receiptId }));
        var (blocks, download) = await ReadToolResultAsync(downloadResponse);
        Assert.True(download.GetProperty("contentIncluded").GetBoolean());
        Assert.Equal("application/pdf", download.GetProperty("mediaType").GetString());
        Assert.Contains(
            blocks.EnumerateArray(),
            block => block.GetProperty("type").GetString() == "text"
                && block.GetProperty("text").GetString()!.Contains("Unplaced source text", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UnreadablePdfSourceReturnsAuthorizedMetadataAndTheOriginalBytesByContentUrl()
    {
        using var factory = new IntakeWebApplicationFactory("Development", true);
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

        using var downloadResponse = await PostMcpAsync(client, token,
            ToolCallPayload(23, "pegasus_unidentified_source_download",
                new { reference, memberReceiptId = receiptId, maxInlineBytes = 1024 }));
        var (blocks, download) = await ReadToolResultAsync(downloadResponse);

        // Bytes that are not a PDF cannot become text, so the client gets the
        // metadata, the reason, and the authenticated route to the original.
        Assert.False(download.GetProperty("contentIncluded").GetBoolean());
        Assert.Equal(content.Length, download.GetProperty("contentLength").GetInt64());
        Assert.Contains("could not be read", download.GetProperty("notice").GetString(), StringComparison.Ordinal);
        Assert.All(blocks.EnumerateArray(), block => Assert.Equal("text", block.GetProperty("type").GetString()));
        var contentUrl = download.GetProperty("contentUrl").GetString();
        Assert.Equal($"/automation/intake-sources/{receiptId:D}", contentUrl);

        using var streamRequest = new HttpRequestMessage(HttpMethod.Get, contentUrl);
        streamRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var streamed = await client.SendAsync(streamRequest);
        Assert.Equal(HttpStatusCode.OK, streamed.StatusCode);
        Assert.Equal(content, await streamed.Content.ReadAsByteArrayAsync());

        using var unauthenticated = await client.GetAsync(contentUrl);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        var wrongScopeToken = await RequestTokenAsync(client, "automation.cases");
        using var wrongScopeRequest = new HttpRequestMessage(HttpMethod.Get, contentUrl);
        wrongScopeRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", wrongScopeToken);
        using var wrongScope = await client.SendAsync(wrongScopeRequest);
        Assert.Equal(HttpStatusCode.Forbidden, wrongScope.StatusCode);
    }

    /// <summary>
    /// The Triage contract over real HTTP: the shared edit lease for a
    /// multi-step change, then every mutation with no lease token, each holding
    /// the Triage for its one command as staff changes do (FRD-14), including
    /// the merged finding, response-evidence and Case-link tools.
    /// </summary>
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

        var token = await RequestTokenAsync(client, AllScopes);
        using (var toolsResponse = await PostMcpAsync(client, token, ToolsListPayload(9)))
        {
            using var toolsDocument = await ReadJsonRpcAsync(toolsResponse);
            var tools = toolsDocument.RootElement
                .GetProperty("result").GetProperty("tools").EnumerateArray()
                .Where(tool => tool.GetProperty("name").GetString() is
                    "pegasus_edit_begin" or "pegasus_edit_renew" or "pegasus_edit_end"
                    or "pegasus_triage_cancel")
                .ToDictionary(tool => tool.GetProperty("name").GetString()!);
            Assert.Equal(4, tools.Count);
            Assert.True(tools["pegasus_edit_begin"].GetProperty("inputSchema")
                .GetProperty("properties").TryGetProperty("recordKind", out _));
            Assert.True(tools["pegasus_edit_begin"].GetProperty("inputSchema")
                .GetProperty("properties").TryGetProperty("expectedVersion", out _));
            Assert.True(tools["pegasus_edit_renew"].GetProperty("inputSchema")
                .GetProperty("properties").TryGetProperty("editLeaseToken", out _));
            Assert.True(tools["pegasus_edit_end"].GetProperty("inputSchema")
                .GetProperty("properties").TryGetProperty("editLeaseToken", out _));
            Assert.True(tools["pegasus_triage_cancel"].GetProperty("inputSchema")
                .GetProperty("properties").TryGetProperty("editLeaseToken", out _));
            Assert.False(string.IsNullOrWhiteSpace(tools["pegasus_triage_cancel"].GetProperty("description").GetString()));
        }
        using var listResponse = await PostMcpAsync(client, token,
            ToolCallPayload(10, "pegasus_triage_list", new { limit = 10 }));
        var list = await ReadStructuredContentAsync(listResponse);
        Assert.Equal(10, list.GetProperty("limit").GetInt32());
        Assert.True(
            !list.TryGetProperty("nextCursor", out var nextCursor)
            || nextCursor.ValueKind == JsonValueKind.Null);
        var item = Assert.Single(list.GetProperty("items").EnumerateArray());
        var triageId = item.GetProperty("caseId").GetGuid();
        var version = item.GetProperty("version").GetInt64();

        using (var sourceResponse = await PostMcpAsync(client, token,
            ToolCallPayload(11, "pegasus_triage_source_download", new { caseId = triageId })))
        {
            // The origin is the email itself, which is neither image, PDF nor
            // plain text: metadata and the authenticated route to its bytes.
            var (blocks, source) = await ReadToolResultAsync(sourceResponse);
            Assert.False(string.IsNullOrWhiteSpace(source.GetProperty("sha256").GetString()));
            var contentUrl = source.GetProperty("contentUrl").GetString();
            Assert.StartsWith("/automation/intake-sources/", contentUrl, StringComparison.Ordinal);
            Assert.NotEmpty(blocks.EnumerateArray());
            using var streamRequest = new HttpRequestMessage(HttpMethod.Get, contentUrl);
            streamRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var streamed = await client.SendAsync(streamRequest);
            Assert.Equal(HttpStatusCode.OK, streamed.StatusCode);
        }

        using var beginResponse = await PostMcpAsync(client, token,
            ToolCallPayload(13, "pegasus_edit_begin", new
            {
                recordKind = "Triage",
                recordId = triageId,
                expectedVersion = version,
                operationKey = "mcp:triage-edit-begin"
            }));
        var lease = await ReadStructuredContentAsync(beginResponse);
        var editLeaseToken = lease.GetProperty("editLeaseToken").GetString()!;
        Assert.Equal("Triage", lease.GetProperty("recordKind").GetString());
        Assert.Equal(version, lease.GetProperty("version").GetInt64());
        Assert.StartsWith("t.", lease.GetProperty("reference").GetString(), StringComparison.Ordinal);

        using var renewResponse = await PostMcpAsync(client, token,
            ToolCallPayload(14, "pegasus_edit_renew", new
            {
                recordKind = "Triage",
                recordId = triageId,
                editLeaseToken,
                operationKey = "mcp:triage-edit-renew"
            }));
        var renewedLease = await ReadStructuredContentAsync(renewResponse);
        Assert.Equal(editLeaseToken, renewedLease.GetProperty("editLeaseToken").GetString());
        Assert.Equal(version, renewedLease.GetProperty("version").GetInt64());
        Assert.True(renewedLease.GetProperty("expiresAtUtc").GetDateTimeOffset() > DateTimeOffset.UtcNow);

        var foreignToken = new string('f', 64);
        using (var foreignLeaseResponse = await PostMcpAsync(client, token,
            ToolCallPayload(15, "pegasus_triage_cancel", new
            {
                caseId = triageId,
                expectedVersion = version,
                editLeaseToken = foreignToken,
                reason = "No longer requires Triage.",
                operationKey = "mcp:triage-cancel-foreign-lease"
            })))
        {
            var foreignLease = await ReadErrorTextAsync(foreignLeaseResponse);
            Assert.DoesNotContain(foreignToken, foreignLease, StringComparison.Ordinal);
        }

        using var cancelResponse = await PostMcpAsync(client, token,
            ToolCallPayload(16, "pegasus_triage_cancel", new
            {
                caseId = triageId,
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
        version = cancelled.GetProperty("detail").GetProperty("record").GetProperty("version").GetInt64();

        using var replacementBeginResponse = await PostMcpAsync(client, token,
            ToolCallPayload(17, "pegasus_edit_begin", new
            {
                recordKind = "Triage",
                recordId = triageId,
                expectedVersion = version,
                operationKey = "mcp:triage-edit-begin-after-cancel"
            }));
        var replacementLease = await ReadStructuredContentAsync(replacementBeginResponse);

        using var endResponse = await PostMcpAsync(client, token,
            ToolCallPayload(18, "pegasus_edit_end", new
            {
                recordKind = "Triage",
                recordId = triageId,
                editLeaseToken = replacementLease.GetProperty("editLeaseToken").GetString(),
                operationKey = "mcp:triage-edit-end"
            }));
        Assert.True((await ReadStructuredContentAsync(endResponse)).GetProperty("released").GetBoolean());

        // From here no lease token is presented: each write holds the Triage
        // for its one command.
        var detail = await MutateAsync(client, token, 19, "pegasus_triage_reopen", new
        {
            caseId = triageId,
            expectedVersion = version,
            reason = "Reopened for the parity walk.",
            operationKey = "mcp:triage-reopen"
        });
        Assert.Equal("Open", State(detail));
        version = Version(detail);

        detail = await MutateAsync(client, token, 20, "pegasus_triage_await_information", new
        {
            caseId = triageId,
            expectedVersion = version,
            operationKey = "mcp:triage-await"
        });
        Assert.Equal("AwaitingInformation", State(detail));
        version = Version(detail);

        detail = await MutateAsync(client, token, 21, "pegasus_triage_record_finding", new
        {
            caseId = triageId,
            expectedVersion = version,
            reason = "Vehicle inspected.",
            roadworthiness = "Roadworthy",
            operationKey = "mcp:triage-finding"
        });
        Assert.Equal("FindingRecorded", State(detail));
        var firstFinding = Assert.Single(detail.GetProperty("findings").EnumerateArray()).GetProperty("id").GetGuid();
        version = Version(detail);

        detail = await MutateAsync(client, token, 22, "pegasus_triage_record_finding", new
        {
            caseId = triageId,
            expectedVersion = version,
            reason = "Corrected after a second look.",
            roadworthiness = "Unroadworthy",
            assessment = "Repairable",
            supersedesFindingId = firstFinding,
            operationKey = "mcp:triage-supersede"
        });
        Assert.Equal(2, detail.GetProperty("findings").GetArrayLength());
        Assert.Contains(detail.GetProperty("findings").EnumerateArray(),
            finding => finding.TryGetProperty("supersedesFindingId", out var supersedes)
                && supersedes.ValueKind == JsonValueKind.String
                && supersedes.GetGuid() == firstFinding);
        version = Version(detail);

        // Response evidence: Link needs the poll outcome it answers; Unlink of
        // evidence never linked is the Core refusal, not a crash.
        using (var linkWithoutOutcome = await PostMcpAsync(client, token,
            ToolCallPayload(23, "pegasus_triage_response_evidence", new
            {
                caseId = triageId,
                expectedVersion = version,
                action = "Link",
                sentEvidenceId = Guid.NewGuid(),
                reason = "Response received.",
                operationKey = "mcp:triage-response-link-no-outcome"
            })))
        {
            Assert.Contains("pollOutcomeId", await ReadErrorTextAsync(linkWithoutOutcome), StringComparison.Ordinal);
        }
        using (var badAction = await PostMcpAsync(client, token,
            ToolCallPayload(24, "pegasus_triage_response_evidence", new
            {
                caseId = triageId,
                expectedVersion = version,
                action = "Attach",
                sentEvidenceId = Guid.NewGuid(),
                reason = "Response received.",
                operationKey = "mcp:triage-response-bad-action"
            })))
        {
            Assert.Contains("Link or Unlink", await ReadErrorTextAsync(badAction), StringComparison.Ordinal);
        }
        using (var unlinkUnknown = await PostMcpAsync(client, token,
            ToolCallPayload(25, "pegasus_triage_response_evidence", new
            {
                caseId = triageId,
                expectedVersion = version,
                action = "Unlink",
                sentEvidenceId = Guid.NewGuid(),
                reason = "Wrong evidence.",
                operationKey = "mcp:triage-response-unlink-unknown"
            })))
        {
            _ = await ReadErrorTextAsync(unlinkUnknown);
        }

        // Assignment and notes are the staff acts too (ADR-0064). The assignee
        // is chosen from the enabled staff, never the acting client.
        using (var unassigned = await PostMcpAsync(client, token,
            ToolCallPayload(40, "pegasus_triage_assign", new
            {
                caseId = triageId,
                expectedVersion = version,
                action = "Unassign",
                operationKey = "mcp:triage-unassign-nobody"
            })))
        {
            Assert.Contains("not assigned", await ReadErrorTextAsync(unassigned), StringComparison.Ordinal);
        }
        using (var stranger = await PostMcpAsync(client, token,
            ToolCallPayload(41, "pegasus_triage_assign", new
            {
                caseId = triageId,
                expectedVersion = version,
                action = "Assign",
                assigneeId = Guid.NewGuid(),
                operationKey = "mcp:triage-assign-stranger"
            })))
        {
            Assert.Contains("enabled member of staff", await ReadErrorTextAsync(stranger), StringComparison.Ordinal);
        }

        var assigneeId = await CreateStaffAccountAsync(mcpFactory.Services, "triage.assignee");
        detail = await MutateAsync(client, token, 42, "pegasus_triage_assign", new
        {
            caseId = triageId,
            expectedVersion = version,
            action = "Assign",
            assigneeId,
            operationKey = "mcp:triage-assign"
        });
        Assert.Equal(assigneeId, detail.GetProperty("record").GetProperty("assigneeId").GetGuid());
        version = Version(detail);

        detail = await MutateAsync(client, token, 43, "pegasus_triage_assign", new
        {
            caseId = triageId,
            expectedVersion = version,
            action = "Unassign",
            operationKey = "mcp:triage-unassign"
        });
        Assert.True(
            !detail.GetProperty("record").TryGetProperty("assigneeId", out var cleared)
            || cleared.ValueKind == JsonValueKind.Null);
        version = Version(detail);

        detail = await MutateAsync(client, token, 44, "pegasus_triage_note_add", new
        {
            caseId = triageId,
            expectedVersion = version,
            note = "Repairer says the vehicle is off the road.",
            operationKey = "mcp:triage-note"
        });
        Assert.Contains(detail.GetProperty("history").EnumerateArray(),
            entry => entry.GetProperty("reason").GetString() == "Repairer says the vehicle is off the road."
                && entry.GetProperty("actor").GetString() == ClientId);
        version = Version(detail);

        // Linking the Triage to an instruction Case changes both records, so
        // both leases are held for the one command and both versions checked.
        // Accepting the instruction may itself touch the Triage (the system
        // matches arriving instructions to open Triage), so the versions are
        // re-read as any caller would, and the walk flips whatever link exists.
        var instructionCaseId = await SeedAcceptedCaseAsync(mcpFactory);
        using (var current = await PostMcpAsync(client, token,
            ToolCallPayload(26, "pegasus_triage_get", new { caseId = triageId })))
        {
            detail = (await ReadStructuredContentAsync(current)).GetProperty("detail");
        }
        version = Version(detail);
        var alreadyLinked = LinkedCase(detail) == instructionCaseId;

        detail = await MutateAsync(client, token, 27, "pegasus_triage_case_link", new
        {
            caseId = triageId,
            instructionCaseId,
            action = alreadyLinked ? "Unlink" : "Link",
            expectedTriageVersion = version,
            expectedCaseVersion = await GetWorkflowVersionAsync(mcpFactory, instructionCaseId),
            reason = "Same vehicle as the instruction.",
            operationKey = "mcp:triage-case-link-first"
        });
        Assert.Equal(alreadyLinked ? null : instructionCaseId, LinkedCase(detail));
        version = Version(detail);
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseWorkflows WHERE CaseId = '{instructionCaseId:D}' AND EditLeaseHolderKind IS NULL"));

        detail = await MutateAsync(client, token, 28, "pegasus_triage_case_link", new
        {
            caseId = triageId,
            instructionCaseId,
            action = alreadyLinked ? "Link" : "Unlink",
            expectedTriageVersion = version,
            expectedCaseVersion = await GetWorkflowVersionAsync(mcpFactory, instructionCaseId),
            reason = "Put back as it was.",
            operationKey = "mcp:triage-case-link-second"
        });
        Assert.Equal(alreadyLinked ? instructionCaseId : null, LinkedCase(detail));
        version = Version(detail);

        detail = await MutateAsync(client, token, 29, "pegasus_triage_complete", new
        {
            caseId = triageId,
            expectedVersion = version,
            operationKey = "mcp:triage-complete"
        });
        Assert.Equal("Completed", State(detail));

        using var getResponse = await PostMcpAsync(client, token,
            ToolCallPayload(30, "pegasus_triage_get", new { caseId = triageId }));
        var fetched = await ReadStructuredContentAsync(getResponse);
        Assert.Equal("Completed", State(fetched.GetProperty("detail")));
        Assert.Contains(fetched.GetProperty("detail").GetProperty("history").EnumerateArray(),
            entry => entry.GetProperty("actor").GetString() == ClientId);

        // A stale version is refused before anything moves.
        using (var stale = await PostMcpAsync(client, token,
            ToolCallPayload(31, "pegasus_triage_reopen", new
            {
                caseId = triageId,
                expectedVersion = version,
                reason = "Stale.",
                operationKey = "mcp:triage-reopen-stale"
            })))
        {
            _ = await ReadErrorTextAsync(stale);
        }
    }

    private static async Task<JsonElement> MutateAsync(
        HttpClient client,
        string token,
        int rpcId,
        string tool,
        object arguments)
    {
        using var response = await PostMcpAsync(client, token, ToolCallPayload(rpcId, tool, arguments));
        return (await ReadStructuredContentAsync(response)).GetProperty("detail");
    }

    private static async Task<Guid> CreateStaffAccountAsync(IServiceProvider services, string userName)
    {
        await using var scope = services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<PegasusIdentityUser>>();
        var user = new PegasusIdentityUser
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            IsEnabled = true,
            MustChangePassword = false
        };
        Assert.True((await users.CreateAsync(user)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, StaffRole.Engineer.ToString())).Succeeded);
        return user.Id;
    }

    private static string State(JsonElement detail) =>
        detail.GetProperty("record").GetProperty("state").GetString()!;

    private static long Version(JsonElement detail) =>
        detail.GetProperty("record").GetProperty("version").GetInt64();

    private static Guid? LinkedCase(JsonElement detail) =>
        detail.GetProperty("record").TryGetProperty("linkedInstructionCaseId", out var linked)
            && linked.ValueKind == JsonValueKind.String
            ? linked.GetGuid()
            : null;
}
