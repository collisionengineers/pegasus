using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.AiWork;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;
using Pegasus.Web.Mcp;
using Pegasus.Web.Presentation;
using static Pegasus.IntegrationTests.AutomationMcpTestSupport;
using static Pegasus.IntegrationTests.CaseWebTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The Automation &amp; AI administration page (R467).
/// The Automation panel states the registered client and the kill switch; the
/// AI settings panel carries one Save; the AI jobs panel lists the ledger in
/// pages under its own Active and Failed counters (ADR-0035). Where the
/// Automation client is not composed, the page states that it is unavailable
/// above the AI jobs list.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed partial class AutomationAdministrationWebTests
{
    private const string AutomationRoute = "/Administration/Automation";
    private const string FormerAiJobsRoute = "/Administration/AiJobs";
    private static readonly ActionActor Administrator = ActionActor.Staff(
        DevelopmentOfflineIdentity.AdministratorId,
        [StaffRole.Administrator]);
    private static readonly ActionActor AutomationClient = ActionActor.Automation(ClientId);

    [Fact]
    public async Task TheAutomationPanelStatesTheRegisteredClientAndTheLedgersOwnJobCounts()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = WithAutomationMcp(baseFactory);
        // Two live jobs and one failed one, written through the ledger's own
        // store — the panel must show what the ledger counts, not a figure of
        // its own.
        await SeedJobsAsync(factory, liveJobs: 2, failedJobs: 1);
        using var client = CreateClient(factory);

        var html = await GetHtmlAsync(client, AutomationRoute);

        Assert.Equal(AutomationMcp.ClientDisplayName, FactValue(html, OperatorLabels.AutomationAdmin.RegisteredClients));
        Assert.Equal(ClientId, FactValue(html, OperatorLabels.AutomationAdmin.ClientIdentifier));
        // The counts have one home on the page, the AI jobs panel's head.
        Assert.Equal(3, FactCount(html));
        // The panel shows each scope's plain label, with the raw key kept on
        // the chip as a title so no scope key is ever silently hidden.
        foreach (var scope in AutomationMcp.Scopes)
        {
            Assert.Contains(
                $"<span title=\"{scope}\">{OperatorLabels.AutomationAdmin.AutomationScope(scope)}</span>",
                html,
                StringComparison.Ordinal);
        }
        Assert.Equal(OperatorLabels.AiJobs.Counts(new(2, 1), sendToAiEnabled: true), CountsText(html));
        Assert.Equal("2 active · 1 failed · Send to AI on", CountsText(html));
        Assert.Contains("Stop automation", html, StringComparison.Ordinal);
        Assert.Contains($"href=\"{AutomationRoute}\"", html, StringComparison.Ordinal);
        // The one consequence sentence a destructive action is allowed.
        Assert.Contains(
            "In-flight work remains visible and no result is discarded.",
            html,
            StringComparison.Ordinal);
        // Every paragraph that explained the page is gone.
        Assert.DoesNotContain("is not a staff account", html, StringComparison.Ordinal);
        Assert.DoesNotContain("is not part of this deployment", html, StringComparison.Ordinal);
        Assert.DoesNotContain("refuses new tokens immediately", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Back to Administration", html, StringComparison.Ordinal);
    }

    /// <summary>The client's name and its identifier are one fact, labelled Client identifier, when they are the same string (v41 A23).</summary>
    [Fact]
    public async Task TheRegisteredClientAndItsIdentifierAreOneFactWhenTheyAreTheSameString()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = WithAutomationMcp(baseFactory).WithWebHostBuilder(builder =>
            builder.UseSetting("AutomationMcp:ClientId", AutomationMcp.ClientDisplayName));
        using var client = CreateClient(factory);

        var html = await GetHtmlAsync(client, AutomationRoute);

        Assert.Equal(AutomationMcp.ClientDisplayName, FactValue(html, OperatorLabels.AutomationAdmin.ClientIdentifier));
        Assert.Equal(2, FactCount(html));
    }

    [Fact]
    public async Task StoppingAutomationDisablesTheClientRegistrationOnTheClick()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = WithAutomationMcp(baseFactory);
        using var client = CreateClient(factory);
        var html = await GetHtmlAsync(client, AutomationRoute);
        Assert.Equal("Enabled", ChipText(html, "automation-panel-title"));

        using (var response = await client.PostAsync(
            $"{AutomationRoute}?handler=SetEnabled",
            Form(
                AntiforgeryValue(html),
                ("TargetEnabled", InputValue(html, "TargetEnabled")),
                ("OperationKey", InputValue(html, "OperationKey")))))
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal(AutomationRoute, response.Headers.Location?.OriginalString);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var status = await scope.ServiceProvider
                .GetRequiredService<AutomationClientRegistry>()
                .GetStatusAsync(Administrator, CancellationToken.None);
            Assert.False(status.IsEnabled);
        }

        var stoppedHtml = await GetHtmlAsync(client, AutomationRoute);
        Assert.Equal("Stopped", ChipText(stoppedHtml, "automation-panel-title"));
        Assert.Contains("Start automation", stoppedHtml, StringComparison.Ordinal);
        Assert.Contains(OperatorLabels.AutomationAdmin.StoppedNotice, stoppedHtml, StringComparison.Ordinal);
    }

    /// <summary>
    /// Stop is one transaction (#1571): when its history row cannot be
    /// written, the client stays enabled, Automation keeps its scope, no
    /// history row stands, and the page says so instead of answering a 500.
    /// The retry then records the real change as Succeeded.
    /// </summary>
    [Fact]
    public async Task AStopThatCannotWriteItsHistoryChangesNothingAndTheRetrySucceeds()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = WithAutomationMcp(baseFactory);
        using var client = CreateClient(factory);
        var html = await GetHtmlAsync(client, AutomationRoute);
        var database = await baseFactory.DatabaseAsync;
        var triageId = Guid.NewGuid();
        await database.ExecuteAsync(
            $"""
            INSERT INTO EditScopes (ScopeKind, RecordId, HolderKind, Holder, TokenHash, ExpectedVersion, Generation, ExpiresAtUtc)
            VALUES (N'Triage', '{triageId:D}', N'Automation', N'switch-test-grant', REPLICATE('a', 64), 0, 1, DATEADD(minute, 5, SYSDATETIMEOFFSET()))
            """);
        await database.ExecuteAsync(
            """
            CREATE TRIGGER [RefuseAutomationSwitchHistory] ON [ActionHistory] AFTER INSERT AS
            BEGIN
                IF EXISTS (SELECT 1 FROM inserted WHERE [AggregateType] = N'automation_client')
                    THROW 50000, N'The history row is refused for this test.', 1;
            END
            """);

        using (var response = await client.PostAsync(
            $"{AutomationRoute}?handler=SetEnabled",
            Form(
                AntiforgeryValue(html),
                ("TargetEnabled", "False"),
                ("OperationKey", InputValue(html, "OperationKey")))))
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal(AutomationRoute, response.Headers.Location?.OriginalString);
        }

        var refusedHtml = await GetHtmlAsync(client, AutomationRoute);
        Assert.Equal("Enabled", ChipText(refusedHtml, "automation-panel-title"));
        Assert.Contains(
            $"<div class=\"notice notice--danger\" role=\"alert\"><span>{OperatorLabels.AutomationAdmin.NotAccepted}</span></div>",
            refusedHtml,
            StringComparison.Ordinal);
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM EditScopes WHERE HolderKind = N'Automation'"));
        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM ActionHistory WHERE AggregateType = N'automation_client'"));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            Assert.True((await scope.ServiceProvider
                .GetRequiredService<AutomationClientRegistry>()
                .GetStatusAsync(Administrator, CancellationToken.None)).IsEnabled);
        }

        await database.ExecuteAsync("DROP TRIGGER [RefuseAutomationSwitchHistory]");
        var retryKey = InputValue(refusedHtml, "OperationKey");
        using (var response = await client.PostAsync(
            $"{AutomationRoute}?handler=SetEnabled",
            Form(
                AntiforgeryValue(refusedHtml),
                ("TargetEnabled", InputValue(refusedHtml, "TargetEnabled")),
                ("OperationKey", retryKey))))
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM EditScopes WHERE HolderKind = N'Automation'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM ActionHistory
            WHERE EventKind = N'automation_client_disabled'
              AND Outcome = N'Succeeded'
              AND CorrelationId = N'{retryKey}'
              AND AfterJson LIKE N'%"EditScopes":1%'
            """));
        Assert.Equal("Stopped", ChipText(await GetHtmlAsync(client, AutomationRoute), "automation-panel-title"));
    }

    [Theory]
    [InlineData("SetEnabled")]
    [InlineData("SaveAiSettings")]
    public async Task AFailedPostKeepsTheStoredSendToAiState(string handler)
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = WithAutomationMcp(baseFactory);
        using var client = CreateClient(factory);
        var html = await GetHtmlAsync(client, AutomationRoute);

        using var response = await client.PostAsync(
            $"{AutomationRoute}?handler={handler}",
            Form(
                AntiforgeryValue(html),
                ("TargetEnabled", "false"),
                ("OperationKey", "not-a-key")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var redisplayedHtml = await response.Content.ReadAsStringAsync();
        Assert.True(CheckboxIsChecked(redisplayedHtml, "SendToAiEnabled"));
        await using var verificationScope = factory.Services.CreateAsyncScope();
        Assert.True(await verificationScope.ServiceProvider
            .GetRequiredService<ISendToAiControl>()
            .IsEnabledAsync(CancellationToken.None));
    }

    [Fact]
    public async Task WithoutTheAutomationCompositionThePageStatesThatItIsUnavailableAboveTheAiJobs()
    {
        using var factory = new IntakeWebApplicationFactory();
        await SeedJobsAsync(factory, liveJobs: 1, failedJobs: 0);
        using var client = CreateClient(factory);

        var html = await GetHtmlAsync(client, AutomationRoute);

        Assert.DoesNotContain("Registered clients", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Stop automation", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Start automation", html, StringComparison.Ordinal);
        Assert.DoesNotContain(OperatorLabels.AutomationAdmin.AiSettingsPanel, html, StringComparison.Ordinal);
        Assert.Contains(OperatorLabels.AutomationAdmin.Unavailable, html, StringComparison.Ordinal);
        Assert.Contains(
            "This deployment does not have Automation or AI configuration available.",
            html,
            StringComparison.Ordinal);
        // The AI jobs list is every deployment's, so the one page is listed
        // in the rail and its list renders where Automation is not composed.
        Assert.Contains($"href=\"{AutomationRoute}\"", html, StringComparison.Ordinal);
        Assert.Equal("1 active · 0 failed · Send to AI on", CountsText(html));
        Assert.Contains($">{OperatorLabels.AiJobs.Stop}</button>", html, StringComparison.Ordinal);
    }

    /// <summary>The former AI jobs address redirects to the one page, keeping the page asked for (R467).</summary>
    [Theory]
    [InlineData("first", "", AutomationRoute)]
    [InlineData("second", "?pageNumber=2", AutomationRoute + "?pageNumber=2")]
    public async Task TheFormerAiJobsRouteRedirectsToTheOnePage(string row, string query, string expected)
    {
        Assert.NotEmpty(row);
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);

        using var response = await client.GetAsync(FormerAiJobsRoute + query);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(expected, response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task AdministrationAiJobsLabelsQueueWorkAndKeepsThePageAfterStopping()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = WithAutomationMcp(baseFactory);
        await SeedJobsAsync(factory, liveJobs: 51, failedJobs: 0);
        using var client = CreateClient(factory);

        var first = await GetHtmlAsync(client, AutomationRoute);
        Assert.Contains("Unidentified-queue pass", first, StringComparison.Ordinal);
        Assert.Contains("Unidentified queue", first, StringComparison.Ordinal);
        Assert.Contains("Queued", first, StringComparison.Ordinal);
        // The meta states the Send to AI switch as on or off; Stop is one button posting no reason.
        Assert.Contains(OperatorLabels.AiJobs.SendToAiState(true), first, StringComparison.Ordinal);
        Assert.Contains($">{OperatorLabels.AiJobs.Stop}</button>", first, StringComparison.Ordinal);
        Assert.DoesNotContain("UnidentifiedQueuePass", first, StringComparison.Ordinal);
        Assert.DoesNotContain(AiJobPolicy.QueueSubjectReference, first, StringComparison.Ordinal);
        foreach (var header in ColumnHeaders)
        {
            Assert.Contains($">{header}</th>", first, StringComparison.Ordinal);
        }
        Assert.Contains($"aria-label=\"{OperatorLabels.AiJobs.Pages}\"", first, StringComparison.Ordinal);
        Assert.Contains($">{OperatorLabels.AiJobs.Next}</a>", first, StringComparison.Ordinal);
        Assert.Contains(OperatorLabels.AiJobs.Page(1), first, StringComparison.Ordinal);
        Assert.Contains("Page 1", first, StringComparison.Ordinal);
        Assert.Contains("pageNumber=2", first, StringComparison.OrdinalIgnoreCase);

        var second = await GetHtmlAsync(client, $"{AutomationRoute}?pageNumber=2");
        Assert.Contains(OperatorLabels.AiJobs.Page(2), second, StringComparison.Ordinal);
        Assert.Contains($">{OperatorLabels.AiJobs.Previous}</a>", second, StringComparison.Ordinal);
        Assert.Contains("pageNumber=1", second, StringComparison.OrdinalIgnoreCase);

        var stoppedJobId = Guid.Parse(InputValue(second, "jobId"));
        using var stopped = await client.PostAsync(
            $"{AutomationRoute}?handler=Stop",
            Form(
                AntiforgeryValue(second),
                ("jobId", stoppedJobId.ToString()),
                ("expectedVersion", InputValue(second, "expectedVersion")),
                ("operationKey", InputValue(second, "operationKey")),
                ("pageNumber", InputValue(second, "pageNumber"))));

        Assert.Equal(HttpStatusCode.Redirect, stopped.StatusCode);
        Assert.Equal($"{AutomationRoute}?pageNumber=2", stopped.Headers.Location?.OriginalString);

        // Stop asks no reason, and an Administrator's Stop cancels the job.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var job = await scope.ServiceProvider
                .GetRequiredService<IAiJobStore>()
                .GetAsync(stoppedJobId, CancellationToken.None);
            Assert.Equal(AiJobState.Cancelled, job?.State);
            Assert.Null(job?.ClosureReason);
        }

        var after = await GetHtmlAsync(client, stopped.Headers.Location!.OriginalString);
        Assert.Contains(OperatorLabels.AiJobs.Stopped, after, StringComparison.Ordinal);
        Assert.Contains("The AI job was stopped.", after, StringComparison.Ordinal);
        Assert.Contains("notice notice--success", after, StringComparison.Ordinal);
        Assert.DoesNotContain("notice--danger", after, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARefusedStopReadsAsAFailureNoticeAndLeavesTheJobQueued()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = WithAutomationMcp(baseFactory);
        await SeedJobsAsync(factory, liveJobs: 1, failedJobs: 0);
        using var client = CreateClient(factory);
        var html = await GetHtmlAsync(client, AutomationRoute);
        var jobId = Guid.Parse(InputValue(html, "jobId"));

        using var refused = await client.PostAsync(
            $"{AutomationRoute}?handler=Stop",
            Form(
                AntiforgeryValue(html),
                ("jobId", jobId.ToString()),
                ("expectedVersion", "999"),
                ("operationKey", InputValue(html, "operationKey")),
                ("pageNumber", InputValue(html, "pageNumber"))));

        Assert.Equal(HttpStatusCode.Redirect, refused.StatusCode);
        var after = await GetHtmlAsync(client, refused.Headers.Location!.OriginalString);
        Assert.Contains(OperatorLabels.AiJobs.ChangedBeforeStop, after, StringComparison.Ordinal);
        Assert.Contains("The AI job changed before it could be stopped.", after, StringComparison.Ordinal);
        Assert.Contains("notice notice--danger", after, StringComparison.Ordinal);
        Assert.DoesNotContain("notice--success", after, StringComparison.Ordinal);
        await using var scope = factory.Services.CreateAsyncScope();
        var job = await scope.ServiceProvider
            .GetRequiredService<IAiJobStore>()
            .GetAsync(jobId, CancellationToken.None);
        Assert.Equal(AiJobState.Queued, job?.State);
    }

    /// <summary>
    /// A Stop for a job that does not exist met no change, so it says the
    /// job could not be stopped (#1565), and the live job stays queued.
    /// </summary>
    [Fact]
    public async Task AStopForAMissingJobSaysItCouldNotBeStopped()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = WithAutomationMcp(baseFactory);
        await SeedJobsAsync(factory, liveJobs: 1, failedJobs: 0);
        using var client = CreateClient(factory);
        var html = await GetHtmlAsync(client, AutomationRoute);
        var liveJobId = Guid.Parse(InputValue(html, "jobId"));

        using var refused = await client.PostAsync(
            $"{AutomationRoute}?handler=Stop",
            Form(
                AntiforgeryValue(html),
                ("jobId", Guid.NewGuid().ToString()),
                ("expectedVersion", InputValue(html, "expectedVersion")),
                ("operationKey", Guid.NewGuid().ToString("N")),
                ("pageNumber", "1")));

        Assert.Equal(HttpStatusCode.Redirect, refused.StatusCode);
        var after = await GetHtmlAsync(client, refused.Headers.Location!.OriginalString);
        Assert.Contains(
            $"<div class=\"notice notice--danger\" role=\"alert\"><span>{OperatorLabels.AiJobs.NotStopped}</span></div>",
            after,
            StringComparison.Ordinal);
        Assert.Contains("The AI job could not be stopped.", after, StringComparison.Ordinal);
        Assert.DoesNotContain(OperatorLabels.AiJobs.ChangedBeforeStop, after, StringComparison.Ordinal);
        await using var scope = factory.Services.CreateAsyncScope();
        var job = await scope.ServiceProvider
            .GetRequiredService<IAiJobStore>()
            .GetAsync(liveJobId, CancellationToken.None);
        Assert.Equal(AiJobState.Queued, job?.State);
    }

    [Fact]
    public async Task TheOneSaveStopsSendingToAiWhenTheCheckboxIsCleared()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = WithAutomationMcp(baseFactory);
        using var client = CreateClient(factory);

        var html = await GetHtmlAsync(client, AutomationRoute);
        Assert.Contains("AI settings", html, StringComparison.Ordinal);
        Assert.Contains($"<span>{OperatorLabels.AutomationAdmin.SendToAi}</span>", html, StringComparison.Ordinal);
        Assert.True(CheckboxIsChecked(html, "SendToAiEnabled"));
        Assert.DoesNotContain("refuses new hand-offs immediately", html, StringComparison.Ordinal);
        Assert.DoesNotContain("apply from the next hand-off", html, StringComparison.Ordinal);

        using (var response = await client.PostAsync(
            $"{AutomationRoute}?handler=SaveAiSettings",
            Form(
                AntiforgeryValue(html),
                ("SendToAiEnabled", "false"),
                ("AutomaticGlassValuationEnabled", "true"),
                ("OperationKey", InputValue(html, "OperationKey")))))
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            Assert.False(await scope.ServiceProvider
                .GetRequiredService<ISendToAiControl>()
                .IsEnabledAsync(CancellationToken.None));
        }

        var savedHtml = await GetHtmlAsync(client, AutomationRoute);
        Assert.False(CheckboxIsChecked(savedHtml, "SendToAiEnabled"));
    }

    /// <summary>
    /// R546 (29 October 2026): the automatic Glass's valuation at Case creation
    /// is an Administrator switch in the AI settings panel, on until it is
    /// turned off as Send to AI is, and the same one Save turns it off.
    /// </summary>
    [Fact]
    public async Task TheOneSaveTurnsTheAutomaticGlassValuationOff()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = WithAutomationMcp(baseFactory);
        using var client = CreateClient(factory);

        var html = await GetHtmlAsync(client, AutomationRoute);
        Assert.Contains(
            $"<span>{System.Text.Encodings.Web.HtmlEncoder.Default.Encode(OperatorLabels.AutomationAdmin.AutomaticGlassValuation)}</span>",
            html,
            StringComparison.Ordinal);
        Assert.True(CheckboxIsChecked(html, "AutomaticGlassValuationEnabled"));
        var operationKey = InputValue(html, "OperationKey");

        using (var response = await client.PostAsync(
            $"{AutomationRoute}?handler=SaveAiSettings",
            Form(
                AntiforgeryValue(html),
                ("SendToAiEnabled", "true"),
                ("AutomaticGlassValuationEnabled", "false"),
                ("OperationKey", operationKey))))
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            Assert.False(await scope.ServiceProvider
                .GetRequiredService<Pegasus.Core.Assessment.IAutomaticGuideValuationControl>()
                .IsEnabledAsync(CancellationToken.None));
            Assert.True(await scope.ServiceProvider
                .GetRequiredService<ISendToAiControl>()
                .IsEnabledAsync(CancellationToken.None));
            var contextFactory = scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            Assert.Equal(
                ["automatic_guide_valuation_disabled"],
                await context.ActionHistory.AsNoTracking()
                    .Where(item => item.CorrelationId == operationKey)
                    .Select(item => item.EventKind)
                    .ToArrayAsync());
        }

        var savedHtml = await GetHtmlAsync(client, AutomationRoute);
        Assert.False(CheckboxIsChecked(savedHtml, "AutomaticGlassValuationEnabled"));
    }

    [Fact]
    public async Task ACheckboxOnlySaveWritesOnlyTheSwitchHistory()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = WithAutomationMcp(baseFactory);
        using var client = CreateClient(factory);
        var html = await GetHtmlAsync(client, AutomationRoute);
        var operationKey = InputValue(html, "OperationKey");

        using (var response = await client.PostAsync(
            $"{AutomationRoute}?handler=SaveAiSettings",
            Form(
                AntiforgeryValue(html),
                ("SendToAiEnabled", "false"),
                ("AutomaticGlassValuationEnabled", "true"),
                ("OperationKey", operationKey))))
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var eventKinds = await context.ActionHistory.AsNoTracking()
            .Where(item => item.CorrelationId == operationKey)
            .Select(item => item.EventKind)
            .ToArrayAsync();
        Assert.Equal(["send_to_ai_disabled"], eventKinds);
    }

    [Fact]
    public async Task ActionLogsRendersAutomationHistory()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = WithAutomationMcp(baseFactory);
        var caseId = await SeedAcceptedCaseAsync(factory);
        var now = factory.Services.GetRequiredService<TimeProvider>().GetUtcNow();
        string caseReference;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            caseReference = (await scope.ServiceProvider.GetRequiredService<IGetCaseHeader>()
                .ExecuteAsync(new(caseId, Administrator), CancellationToken.None))!.Summary.Reference;
            var history = scope.ServiceProvider.GetRequiredService<IActionHistoryWriter>();
            await history
                .AppendAsync(
                    new(
                        Guid.NewGuid(),
                        "automation_mcp",
                        caseId.ToString("D"),
                        "pegasus_case_get",
                        AutomationClient,
                        now,
                        "Succeeded",
                        "automation-admin-activity-reference",
                        null),
                    CancellationToken.None);
            await history.AppendAsync(
                new(
                    Guid.NewGuid(),
                    "automation_mcp",
                    caseId.ToString("D"),
                    "pegasus_document_download_batch",
                    AutomationClient,
                    now,
                    "Succeeded",
                    "automation-admin-batch-reference",
                    null),
                CancellationToken.None);
            await history.AppendAsync(
                new(
                    Guid.NewGuid(),
                    "case",
                    caseId.ToString("D"),
                    "case_reviewed",
                    Administrator,
                    now,
                    "Succeeded",
                    "staff-action-log-display",
                    null),
                CancellationToken.None);
        }
        using var client = CreateClient(factory);

        var from = Uri.EscapeDataString(now.AddDays(-1).ToString("O"));
        var to = Uri.EscapeDataString(now.AddDays(1).ToString("O"));
        var html = await GetHtmlAsync(
            client,
            $"/Administration/Logs?From={from}&To={to}");

        var automationRow = TableRow(html, OperatorLabels.ActionLogAction("pegasus_case_get"));
        Assert.Contains(caseReference, automationRow, StringComparison.Ordinal);
        Assert.Contains(AutomationMcp.ClientDisplayName, automationRow, StringComparison.Ordinal);
        Assert.DoesNotContain(caseId.ToString("D"), automationRow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ClientId, automationRow, StringComparison.Ordinal);

        // The batch download is recorded against the Case it names, too.
        var batchRow = TableRow(html, OperatorLabels.ActionLogAction("pegasus_document_download_batch"));
        Assert.Contains(caseReference, batchRow, StringComparison.Ordinal);

        var staffRow = TableRow(html, OperatorLabels.ActionLogAction("case_reviewed"));
        Assert.Contains(caseReference, staffRow, StringComparison.Ordinal);
        Assert.Contains(DevelopmentOfflineIdentity.UserName, staffRow, StringComparison.Ordinal);
        Assert.DoesNotContain(caseId.ToString("D"), staffRow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(DevelopmentOfflineIdentity.AdministratorId.ToString("D"), staffRow, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Writes <paramref name="liveJobs"/> queued jobs and
    /// <paramref name="failedJobs"/> failed ones through the ledger's own
    /// store and Automation-Actor transitions, so the counters the page reads
    /// are the counters the ledger computes.
    /// </summary>
    private static async Task SeedJobsAsync(
        WebApplicationFactory<Program> factory,
        int liveJobs,
        int failedJobs)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IAiJobStore>();
        var work = scope.ServiceProvider.GetRequiredService<IWorkAiJob>();
        for (var index = 0; index < liveJobs + failedJobs; index++)
        {
            var ordinal = index.ToString(CultureInfo.InvariantCulture);
            var created = await store.CreateAsync(
                new(
                    AiJobKind.UnidentifiedQueuePass,
                    AiJobSubjectKind.Queue,
                    null,
                    AiJobPolicy.QueueSubjectReference,
                    $"Pass the queue ({ordinal}).",
                    null,
                    null,
                    Administrator,
                    $"automation-admin-seed-{ordinal}",
                    AiJobPolicy.DefaultExpiry),
                CancellationToken.None);
            if (index < liveJobs)
            {
                continue;
            }

            var taken = await work.TakeAsync(
                new(created.JobId, created.Version, AutomationClient, $"automation-admin-take-{ordinal}"),
                CancellationToken.None);
            await work.FailAsync(
                new(
                    taken.JobId,
                    taken.Version,
                    AutomationClient,
                    $"automation-admin-fail-{ordinal}",
                    "The channel refused the request."),
                CancellationToken.None);
        }
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

    private static string TableRow(string html, string operation)
    {
        var match = Regex.Match(
            html,
            $"<tr>(?:(?!</tr>).)*{Regex.Escape(operation)}(?:(?!</tr>).)*</tr>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);
        Assert.True(match.Success, $"The action-log row for '{operation}' must render.");
        return match.Value;
    }

    /// <summary>The value of one `fact` or `definition` cell, by its label.</summary>
    private static string FactValue(string html, string label)
    {
        var match = Regex.Match(
            html,
            $"<dt>{Regex.Escape(label)}</dt>\\s*<dd[^>]*>(?<value>[^<]*)</dd>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(match.Success, $"The page must render the '{label}' cell.");
        return WebUtility.HtmlDecode(match.Groups["value"].Value).Trim();
    }

    /// <summary>The AI jobs table's column headers, from their label owner.</summary>
    private static readonly string[] ColumnHeaders =
    [
        OperatorLabels.AiJobs.JobColumn,
        OperatorLabels.AiJobs.SubjectColumn,
        OperatorLabels.AiJobs.CreatedColumn,
        OperatorLabels.AiJobs.StateColumn,
        OperatorLabels.AiJobs.ActionColumn,
    ];

    /// <summary>The AI jobs panel head's counts line, decoded.</summary>
    private static string CountsText(string html)
    {
        var match = Regex.Match(
            html,
            "<span class=\"meta\" data-ai-jobs-counts>(?<value>[^<]*)</span>",
            RegexOptions.CultureInvariant);
        Assert.True(match.Success, "The AI jobs panel must render its counts.");
        return WebUtility.HtmlDecode(match.Groups["value"].Value).Trim();
    }

    /// <summary>How many facts the Automation panel draws.</summary>
    private static int FactCount(string html) =>
        Regex.Count(html, "<dl class=\"fact\">", RegexOptions.CultureInvariant);

    /// <summary>The state chip beside one panel heading.</summary>
    private static string ChipText(string html, string headingId)
    {
        var match = Regex.Match(
            html,
            $"id=\"{Regex.Escape(headingId)}\"[^>]*>[^<]*</h2>\\s*<span class=\"status[^\"]*\">(?<value>[^<]*)</span>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(match.Success, $"The panel '{headingId}' must render a state chip.");
        return WebUtility.HtmlDecode(match.Groups["value"].Value).Trim();
    }

    /// <summary>Whether the named checkbox opens on the stored state.</summary>
    private static bool CheckboxIsChecked(string html, string name)
    {
        var tag = Regex.Matches(html, "<input[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            .Select(match => match.Value)
            .SingleOrDefault(value =>
                value.Contains($"name=\"{name}\"", StringComparison.Ordinal)
                && value.Contains("type=\"checkbox\"", StringComparison.Ordinal));
        Assert.True(tag is not null, $"The page must render the '{name}' checkbox.");
        return tag!.Contains("checked", StringComparison.OrdinalIgnoreCase);
    }

    private static string InputValue(string html, string name)
    {
        var tag = Regex.Match(
            html,
            $"<input[^>]*name=\"{Regex.Escape(name)}\"[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(tag.Success, $"The page must render '{name}'.");
        var value = ValueRegex().Match(tag.Value);
        Assert.True(value.Success, $"The field '{name}' must have a value.");
        return WebUtility.HtmlDecode(value.Groups["value"].Value);
    }

    private static string AntiforgeryValue(string html)
    {
        var tag = Regex.Match(
            html,
            "<input[^>]*name=\"__RequestVerificationToken\"[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(tag.Success, "The page must render an antiforgery token.");
        var value = ValueRegex().Match(tag.Value);
        Assert.True(value.Success, "The antiforgery token must have a value.");
        return WebUtility.HtmlDecode(value.Groups["value"].Value);
    }

    [GeneratedRegex("value=\"(?<value>[^\"]*)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ValueRegex();
}
