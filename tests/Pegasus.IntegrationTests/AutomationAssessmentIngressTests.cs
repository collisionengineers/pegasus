using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.AiWork;
using Pegasus.Core.Identity;
using static Pegasus.IntegrationTests.AutomationMcpTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Tier-4 caller-equivalent evidence for the assessment tranche of the
/// Automation Actor toolset: real HTTP against the gated /mcp surface, real
/// LocalDB persistence, and the same attribution assertions as the original
/// nine-tool ingress tests.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class AutomationAssessmentIngressTests
{
    [Fact]
    public async Task AssessmentToolsEnforceTheAssessmentScope()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        using var client = mcpFactory.CreateClient();

        var casesOnlyToken = await RequestTokenAsync(client, "automation.cases");
        using var response = await PostMcpAsync(
            client,
            casesOnlyToken,
            ToolCallPayload(
                1,
                "pegasus_assessment_get",
                new { caseId = Guid.NewGuid() }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = await ReadJsonRpcAsync(response);
        Assert.Contains(
            "automation.assessment",
            document.RootElement.ToString(),
            StringComparison.Ordinal);

        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM SecurityEvents
            WHERE ReasonCode = N'automation_scope_denied'
              AND Outcome = N'Denied'
              AND SubjectId = N'pegasus-automation'
            """));
    }

    [Fact]
    public async Task AssessmentUpdateOverHttpMutatesUnderLeaseWithCorrelatedAttribution()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        Guid workRequestId;
        await using (var scope = mcpFactory.Services.CreateAsyncScope())
        {
            var created = await scope.ServiceProvider
                .GetRequiredService<IAiWorkRequestStore>()
                .CreateAsync(
                    new(
                        caseId,
                        "fixture-reference",
                        0,
                        ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]),
                        "ingress-send-op",
                        "Work the assessment.",
                        TimeSpan.FromHours(24)),
                    CancellationToken.None);
            workRequestId = created.RequestId;
        }

        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AllScopes);

        // The automation claims the same server-owned edit lease as staff.
        long caseVersion;
        string leaseToken;
        using (var leaseResponse = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(
                2,
                "pegasus_case_edit_begin",
                new
                {
                    caseId,
                    expectedVersion = 0,
                    operationKey = "mcp:ingress-lease-1"
                })))
        {
            Assert.Equal(HttpStatusCode.OK, leaseResponse.StatusCode);
            using var leaseDocument = await ReadJsonRpcAsync(leaseResponse);
            var lease = leaseDocument.RootElement
                .GetProperty("result")
                .GetProperty("structuredContent");
            caseVersion = lease.GetProperty("caseVersion").GetInt64();
            leaseToken = lease.GetProperty("leaseToken").GetString()!;
        }

        using (var updateResponse = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(
                3,
                "pegasus_assessment_update",
                new
                {
                    caseId,
                    expectedVersion = caseVersion,
                    editLeaseToken = leaseToken,
                    operationKey = "mcp:ingress-assessment-1",
                    reason = "Automation recorded the assessment draft.",
                    fields = new Dictionary<string, string?>
                    {
                        ["vehicle.condition"] = "good",
                        ["assessment.values.retail"] = "12000",
                        ["assessment.values.trade"] = "10500",
                        ["assessment.values.engineer"] = "12000"
                    },
                    estimateLines = new[]
                    {
                        new
                        {
                            type = "repair",
                            description = "Repair nearside door",
                            workUnits = 3.5,
                            status = "estimated",
                            evidenceLabel = "judgement",
                            justification = "Visible panel damage"
                        }
                    },
                    workRequestId = workRequestId.ToString("D")
                })))
        {
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
            using var updateDocument = await ReadJsonRpcAsync(updateResponse);
            var result = updateDocument.RootElement.GetProperty("result");
            Assert.False(result.TryGetProperty("isError", out var isError) && isError.GetBoolean());
            var structured = result.GetProperty("structuredContent");
            Assert.Equal(caseVersion + 1, structured.GetProperty("caseVersion").GetInt64());
            Assert.Equal(
                workRequestId.ToString("D"),
                structured.GetProperty("correlationId").GetString());
            var fields = structured.GetProperty("fields").EnumerateArray().ToArray();
            Assert.All(fields, field => Assert.False(field.GetProperty("isConfirmed").GetBoolean()));
        }

        // Stored values carry the unconfirmed automation provenance.
        Assert.Equal(4, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM CaseAssessmentFields
            WHERE RecordedByKind = N'Automation' AND ConfirmedBy IS NULL
            """));
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM CaseEstimateLines WHERE RecordedByKind = N'Automation'"));

        // Logging parity: the business save is recorded exactly like a staff
        // save, and the ingress attribution row correlates to the hand-off.
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM ActionHistory
            WHERE ActorKind = N'Automation'
              AND EventKind = N'case_assessment_saved'
              AND Outcome = N'Succeeded'
            """));
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM ActionHistory
            WHERE ActorKind = N'Automation'
              AND EventKind = N'pegasus_assessment_update'
              AND Outcome = N'Succeeded'
              AND CorrelationId = N'{workRequestId:D}'
            """));

        // A replayed operation key returns the original result.
        using (var replayResponse = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(
                4,
                "pegasus_assessment_update",
                new
                {
                    caseId,
                    expectedVersion = caseVersion,
                    editLeaseToken = leaseToken,
                    operationKey = "mcp:ingress-assessment-1",
                    reason = "Automation recorded the assessment draft.",
                    fields = new Dictionary<string, string?>
                    {
                        ["vehicle.condition"] = "good",
                        ["assessment.values.retail"] = "12000",
                        ["assessment.values.trade"] = "10500",
                        ["assessment.values.engineer"] = "12000"
                    },
                    estimateLines = new[]
                    {
                        new
                        {
                            type = "repair",
                            description = "Repair nearside door",
                            workUnits = 3.5,
                            status = "estimated",
                            evidenceLabel = "judgement",
                            justification = "Visible panel damage"
                        }
                    },
                    workRequestId = workRequestId.ToString("D")
                })))
        {
            Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);
            using var replayDocument = await ReadJsonRpcAsync(replayResponse);
            var structured = replayDocument.RootElement
                .GetProperty("result")
                .GetProperty("structuredContent");
            Assert.Equal(caseVersion + 1, structured.GetProperty("caseVersion").GetInt64());
        }

        // The read-back tool exposes the recorded surface with readiness.
        using (var getResponse = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(5, "pegasus_assessment_get", new { caseId })))
        {
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
            using var getDocument = await ReadJsonRpcAsync(getResponse);
            var structured = getDocument.RootElement
                .GetProperty("result")
                .GetProperty("structuredContent");
            Assert.True(structured.GetProperty("readiness").GetArrayLength() > 0);
            Assert.Equal(
                "AB12CDE",
                structured.GetProperty("caseOwned").GetProperty("registration").GetString());
        }
    }

    [Fact]
    public async Task EvaHandoffToolsRespondOverHttpAndRecordAttribution()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AllScopes);

        using (var statusResponse = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(6, "pegasus_eva_handoff_status", new { caseId })))
        {
            Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
            using var statusDocument = await ReadJsonRpcAsync(statusResponse);
            var structured = statusDocument.RootElement
                .GetProperty("result")
                .GetProperty("structuredContent");
            Assert.False(structured.GetProperty("canGenerate").GetBoolean());
            Assert.True(structured.GetProperty("blockingReasons").GetArrayLength() > 0);
        }

        long caseVersion;
        string leaseToken;
        using (var leaseResponse = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(
                7,
                "pegasus_case_edit_begin",
                new
                {
                    caseId,
                    expectedVersion = 0,
                    operationKey = "mcp:ingress-eva-lease"
                })))
        {
            using var leaseDocument = await ReadJsonRpcAsync(leaseResponse);
            var lease = leaseDocument.RootElement
                .GetProperty("result")
                .GetProperty("structuredContent");
            caseVersion = lease.GetProperty("caseVersion").GetInt64();
            leaseToken = lease.GetProperty("leaseToken").GetString()!;
        }

        using (var generateResponse = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(
                8,
                "pegasus_eva_bundle_generate",
                new
                {
                    caseId,
                    expectedVersion = caseVersion,
                    editLeaseToken = leaseToken,
                    operationKey = "mcp:ingress-eva-generate",
                    reason = "Automation requested the EVA bundle."
                })))
        {
            Assert.Equal(HttpStatusCode.OK, generateResponse.StatusCode);
            using var generateDocument = await ReadJsonRpcAsync(generateResponse);
            var result = generateDocument.RootElement.GetProperty("result");
            Assert.False(result.TryGetProperty("isError", out var isError) && isError.GetBoolean());
            var structured = result.GetProperty("structuredContent");
            // The fixture case has no custody-confirmed images, so the same
            // Core blocking rules refuse generation for the automation
            // exactly as they refuse it for staff.
            Assert.Equal("Blocked", structured.GetProperty("outcome").GetString());
            Assert.False(structured.GetProperty("firstSentToEngineerRecorded").GetBoolean());
        }

        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM ActionHistory
            WHERE ActorKind = N'Automation'
              AND EventKind = N'pegasus_eva_bundle_generate'
            """));
    }

    [Fact]
    public Task EvaToolsUseSharedCoreReviewCustodyVersionAndAttributionGuards() =>
        EvaHandoffToolsRespondOverHttpAndRecordAttribution();
}
