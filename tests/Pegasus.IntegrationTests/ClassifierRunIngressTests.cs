using Pegasus.Core.Identity;
using Pegasus.Web.Mcp;
using Pegasus.Web.Presentation;
using static Pegasus.IntegrationTests.AutomationMcpTestSupport;
using static Pegasus.IntegrationTests.DesktopSignInTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The classifier's record of its own runs (FRD-08, FRD-10; R1318) over real
/// HTTP: the Automation Actor opens a run and closes it once with its
/// messages, a repeat of either stage changes nothing, the tool sits in the
/// mail scope, and a member of staff signed in from Pegasus Desktop is
/// refused it.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class ClassifierRunIngressTests
{
    private const string Tool = "pegasus_classifier_run_record";

    [Fact]
    public async Task TheAutomationActorOpensARunAndClosesItOnceWithItsMessages()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var host = WithAutomationMcp(factory);
        using var client = host.CreateClient();
        var database = await factory.DatabaseAsync;
        var runId = Guid.NewGuid();
        var key = runId.ToString("N");

        var casesOnly = await RequestTokenAsync(client, AutomationMcp.CasesScope);
        using (var denied = await PostMcpAsync(client, casesOnly, ToolCallPayload(1, Tool, Open(runId, key))))
        {
            await AssertScopeDeniedAsync(denied, AutomationMcp.MailScope);
        }

        var token = await RequestTokenAsync(client, AllScopes);
        using (var opened = await PostMcpAsync(client, token, ToolCallPayload(2, Tool, Open(runId, key))))
        {
            var result = await ReadStructuredContentAsync(opened);
            Assert.Equal(runId, result.GetProperty("runId").GetGuid());
            Assert.Equal("open", result.GetProperty("stage").GetString());
            Assert.False(result.GetProperty("isReplay").GetBoolean());
            Assert.Equal(System.Text.Json.JsonValueKind.Null, result.GetProperty("finishedAtUtc").ValueKind);
        }

        using (var again = await PostMcpAsync(client, token, ToolCallPayload(3, Tool, Open(runId, key))))
        {
            Assert.True((await ReadStructuredContentAsync(again)).GetProperty("isReplay").GetBoolean());
        }

        var linked = Guid.NewGuid();
        using (var closed = await PostMcpAsync(client, token, ToolCallPayload(4, Tool, Close(runId, key, linked))))
        {
            var result = await ReadStructuredContentAsync(closed);
            Assert.False(result.GetProperty("isReplay").GetBoolean());
            Assert.Equal($"mcp:run-close:{key}", result.GetProperty("correlationId").GetString());
            Assert.True(result.GetProperty("finishedAtUtc").GetDateTimeOffset() >= result.GetProperty("startedAtUtc").GetDateTimeOffset());
        }

        using (var again = await PostMcpAsync(client, token, ToolCallPayload(5, Tool, Close(runId, key, linked))))
        {
            Assert.True((await ReadStructuredContentAsync(again)).GetProperty("isReplay").GetBoolean());
        }

        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM ClassifierRuns WHERE Id = '{runId}' AND FinishedAtUtc IS NOT NULL "
            + "AND OutcomeKind = 'Completed' AND Acted = 2 AND RecordedBy = 'pegasus-automation'"));
        Assert.Equal(3, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM ClassifierRunItems WHERE RunId = '{runId}'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM ClassifierRunItems WHERE RunId = '{runId}' AND Action = 'Linked' "
            + $"AND CaseId = '{linked}' AND CaseReference = 'QDOS26002' AND Reason = 'The registration matches.'"));
        // Each call is recorded in Action logs under its own key.
        Assert.Equal(4, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM ActionHistory WHERE AggregateType = '{AutomationMcpAuditor.AutomationAggregateType}' "
            + $"AND EventKind = '{Tool}' AND AggregateId = '{runId}' AND Outcome = 'Succeeded'"));
    }

    [Fact]
    public async Task ACloseWhoseOpenNeverArrivedRecordsTheRunAndAnUnknownActIsRefused()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var host = WithAutomationMcp(factory);
        using var client = host.CreateClient();
        var database = await factory.DatabaseAsync;
        var token = await RequestTokenAsync(client, AllScopes);
        var runId = Guid.NewGuid();
        var key = runId.ToString("N");

        using (var unknown = await PostMcpAsync(client, token, ToolCallPayload(1, Tool, new
        {
            runId,
            stage = "close",
            operationKey = $"mcp:run-close:{key}",
            facts = Facts(),
            result = Result("success", [new { messageId = Guid.NewGuid(), action = "archived" }])
        })))
        {
            Assert.Contains("'archived' is not an act the classifier records.", await ReadErrorTextAsync(unknown), StringComparison.Ordinal);
        }

        Assert.Equal(0, await database.ScalarAsync<int>("SELECT COUNT(*) FROM ClassifierRuns"));

        using (var closed = await PostMcpAsync(client, token, ToolCallPayload(2, Tool, Close(runId, key, Guid.NewGuid()))))
        {
            Assert.False((await ReadStructuredContentAsync(closed)).GetProperty("isReplay").GetBoolean());
        }

        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM ClassifierRuns WHERE Id = '{runId}' AND StartedAtUtc < FinishedAtUtc"));
    }

    [Fact]
    public async Task AMemberOfStaffSignedInFromPegasusDesktopIsRefusedTheClassifiersRecord()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var host = WithDesktopSignIn(factory);
        using var desktop = CreateConnector(host);
        var (_, token) = await SignedInStaffAsync(host, desktop, "desktop-classifier", StaffRole.Administrator);
        var database = await factory.DatabaseAsync;
        var runId = Guid.NewGuid();

        using (var refused = await PostMcpAsync(desktop, token, ToolCallPayload(1, Tool, Open(runId, runId.ToString("N")))))
        {
            Assert.Contains(
                OperatorLabels.DesktopSignIn.ActionNotAllowed,
                await ReadErrorTextAsync(refused),
                StringComparison.Ordinal);
        }

        Assert.Equal(0, await database.ScalarAsync<int>("SELECT COUNT(*) FROM ClassifierRuns"));
    }

    private static object Facts() => new
    {
        dryRun = false,
        model = "claude-sonnet-5-5",
        batch = 20,
        scanPages = 10,
        maxBudgetUsd = 2,
        agentVersion = "1.0.0"
    };

    private static object Open(Guid runId, string key) => new
    {
        runId,
        stage = "open",
        operationKey = $"mcp:run-open:{key}",
        facts = Facts()
    };

    private static object Close(Guid runId, string key, Guid linkedCaseId) => new
    {
        runId,
        stage = "close",
        operationKey = $"mcp:run-close:{key}",
        facts = Facts(),
        result = Result("success",
        [
            new { messageId = Guid.NewGuid(), action = "classified", category = "cancellation", reason = "The sender cancels." },
            new
            {
                messageId = Guid.NewGuid(),
                action = "linked",
                caseId = (Guid?)linkedCaseId,
                caseReference = "QDOS26002",
                reason = "The registration matches."
            },
            new { messageId = Guid.NewGuid(), action = "left", reason = "Already a cancellation." }
        ])
    };

    private static object Result(string outcome, object[] items) => new
    {
        outcome,
        eligible = items.Length,
        awaitingDecision = 0,
        candidatesHidden = false,
        hiddenRuns = 0,
        verdicts = items.Length,
        failed = 0,
        deferred = 0,
        turns = 9,
        costUsd = 0.1234,
        durationMs = 61_000,
        summary = "Three e-mails: one cancellation, one link, one left.",
        items
    };
}
