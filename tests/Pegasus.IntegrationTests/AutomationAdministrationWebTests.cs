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
using static Pegasus.IntegrationTests.AutomationMcpTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>
/// AUTO-006 / EPIC-011 §1.12: the Automation &amp; AI administration area.
/// The Automation panel states the registered client, the AI job ledger's own
/// Active and Failed counters (ADR-0035) and the kill switch; the AI settings
/// panel carries one Save. Both are absent where the deployment does not
/// compose them, and neither explains itself.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed partial class AutomationAdministrationWebTests
{
    private const string AutomationRoute = "/Administration/Automation";
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

        Assert.Equal(AutomationMcp.ClientDisplayName, FactValue(html, "Registered clients"));
        Assert.Equal(ClientId, FactValue(html, "Client identifier"));
        Assert.Equal(
            string.Join(", ", AutomationMcp.Scopes.Order(StringComparer.Ordinal)),
            FactValue(html, "Granted scopes"));
        Assert.Equal("2", FactValue(html, "Active jobs"));
        Assert.Equal("1", FactValue(html, "Failed jobs"));
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
        // The superseded Activity page (§1.14 → Action Logs) is no longer
        // linked from the area that replaced it.
        Assert.DoesNotContain("/Administration/Automation/Activity", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StoppingAutomationDisablesTheClientRegistrationThroughTheReasonDialog()
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
                ("OperationKey", InputValue(html, "OperationKey")),
                ("Reason", "Stopped while the estimate connector is replaced."))))
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
    }

    [Theory]
    [InlineData("SetEnabled")]
    [InlineData("ClearChannelToken")]
    public async Task FailedReasonDialogPostKeepsTheStoredSendToAiState(string handler)
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        using var automationFactory = WithAutomationMcp(baseFactory);
        using var factory = automationFactory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Features:SendToAi", "true");
            builder.UseSetting("SendToAi:ChannelBaseUrl", "http://127.0.0.1:8629");
            builder.UseSetting(
                "SendToAi:ChannelToken",
                "auto-006-redisplay-channel-token-0123456789");
            builder.UseSetting("SendToAi:TimeoutSeconds", "5");
        });
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IAiChannelConnectorStore>()
                .RotateTokenAsync(
                    new(
                        Administrator,
                        "Exercise both reason-dialog redisplay paths.",
                        "auto-006-redisplay-token",
                        "auto-006-administration-channel-token-0123456789"),
                    CancellationToken.None);
        }
        using var client = CreateClient(factory);
        var html = await GetHtmlAsync(client, AutomationRoute);

        using var response = await client.PostAsync(
            $"{AutomationRoute}?handler={handler}",
            Form(
                AntiforgeryValue(html),
                ("TargetEnabled", "false"),
                ("OperationKey", InputValue(html, "OperationKey")),
                ("Reason", " ")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var redisplayedHtml = await response.Content.ReadAsStringAsync();
        Assert.Equal("Enabled", ChipText(redisplayedHtml, "ai-settings-panel-title"));
        Assert.True(CheckboxIsChecked(redisplayedHtml, "SendToAiEnabled"));
        await using var verificationScope = factory.Services.CreateAsyncScope();
        Assert.True(await verificationScope.ServiceProvider
            .GetRequiredService<ISendToAiControl>()
            .IsEnabledAsync(CancellationToken.None));
        Assert.True((await verificationScope.ServiceProvider
            .GetRequiredService<IAiChannelConnectorStore>()
            .GetAsync(CancellationToken.None)).TokenHeld);
    }

    [Fact]
    public async Task WithoutTheAutomationCompositionThePanelAndItsRailRowAreAbsent()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);

        var html = await GetHtmlAsync(client, AutomationRoute);

        Assert.DoesNotContain("Registered clients", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Stop automation", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Start automation", html, StringComparison.Ordinal);
        // The shared rail lists Automation &amp; AI only where it is composed.
        Assert.DoesNotContain($"href=\"{AutomationRoute}\"", html, StringComparison.Ordinal);
        // An absent capability is absent, not narrated.
        Assert.DoesNotContain("is not part of this deployment", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheOneSaveStoresTheTimeoutAndStopsSendingToAiWhenTheCheckboxIsCleared()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = SendToAiIntegrationTests.WithSendToAi(baseFactory, "http://127.0.0.1:8629");
        using var client = CreateClient(factory);

        var html = await GetHtmlAsync(client, AutomationRoute);
        Assert.Contains("AI settings", html, StringComparison.Ordinal);
        Assert.Contains("Reviewed AI proposals enabled", html, StringComparison.Ordinal);
        Assert.Equal("Enabled", ChipText(html, "ai-settings-panel-title"));
        Assert.True(CheckboxIsChecked(html, "SendToAiEnabled"));
        Assert.DoesNotContain("refuses new hand-offs immediately", html, StringComparison.Ordinal);
        Assert.DoesNotContain("apply from the next hand-off", html, StringComparison.Ordinal);

        using (var response = await client.PostAsync(
            $"{AutomationRoute}?handler=SaveAiSettings",
            Form(
                AntiforgeryValue(html),
                ("ChannelTimeoutSeconds", "7"),
                ("SendToAiEnabled", "false"),
                ("OperationKey", InputValue(html, "OperationKey")),
                ("Reason", "Paused while the channel host is rebuilt."))))
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            Assert.False(await scope.ServiceProvider
                .GetRequiredService<ISendToAiControl>()
                .IsEnabledAsync(CancellationToken.None));
            var settings = await scope.ServiceProvider
                .GetRequiredService<IAiChannelConnectorStore>()
                .GetAsync(CancellationToken.None);
            Assert.Equal(7, settings.TimeoutSeconds);
        }

        var savedHtml = await GetHtmlAsync(client, AutomationRoute);
        Assert.Equal("Stopped", ChipText(savedHtml, "ai-settings-panel-title"));
        Assert.False(CheckboxIsChecked(savedHtml, "SendToAiEnabled"));
    }

    [Fact]
    public async Task ACheckboxOnlySaveWritesNoUnchangedConnectorHistory()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = SendToAiIntegrationTests.WithSendToAi(
            baseFactory,
            "http://127.0.0.1:8629");
        using var client = CreateClient(factory);
        var html = await GetHtmlAsync(client, AutomationRoute);
        var operationKey = InputValue(html, "OperationKey");

        using (var response = await client.PostAsync(
            $"{AutomationRoute}?handler=SaveAiSettings",
            Form(
                AntiforgeryValue(html),
                ("SendToAiEnabled", "false"),
                ("OperationKey", operationKey),
                ("Reason", "Pause hand-offs without changing the connector."))))
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
    public async Task ActivityRendersCaseReferencesAndNoFilterNarration()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = WithAutomationMcp(baseFactory);
        var caseId = await SeedAcceptedCaseAsync(factory);
        string caseReference;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var details = await scope.ServiceProvider.GetRequiredService<IGetCase>()
                .ExecuteAsync(new(caseId, Administrator), CancellationToken.None)
                ?? throw new InvalidOperationException("The seeded case is unavailable.");
            caseReference = details.Summary.Reference;
            await scope.ServiceProvider.GetRequiredService<IActionHistoryWriter>()
                .AppendAsync(
                    new(
                        Guid.NewGuid(),
                        "automation_mcp",
                        caseId.ToString("D"),
                        "pegasus_case_get",
                        AutomationClient,
                        DateTimeOffset.UtcNow,
                        "Succeeded",
                        "auto-006-activity-reference",
                        null),
                    CancellationToken.None);
        }
        using var client = CreateClient(factory);

        var html = await GetHtmlAsync(client, $"{AutomationRoute}/Activity");

        Assert.Contains(caseReference, html, StringComparison.Ordinal);
        Assert.DoesNotContain(caseId.ToString("D"), html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("you can filter by", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ActivityOmitsTheEmptyStatePanel()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);

        var html = await GetHtmlAsync(client, $"{AutomationRoute}/Activity");

        Assert.DoesNotContain("automation-activity-heading", html, StringComparison.Ordinal);
        Assert.DoesNotContain("No Automation activity is recorded", html, StringComparison.Ordinal);
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
                    $"auto-006-seed-{ordinal}",
                    AiJobPolicy.DefaultExpiry),
                CancellationToken.None);
            if (index < liveJobs)
            {
                continue;
            }

            var taken = await work.TakeAsync(
                new(created.JobId, created.Version, AutomationClient, $"auto-006-take-{ordinal}"),
                CancellationToken.None);
            await work.FailAsync(
                new(
                    taken.JobId,
                    taken.Version,
                    AutomationClient,
                    $"auto-006-fail-{ordinal}",
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

    private static async Task<string> GetHtmlAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static FormUrlEncodedContent Form(
        string antiforgeryToken,
        params (string Name, string Value)[] values)
    {
        var fields = values.ToDictionary(item => item.Name, item => item.Value, StringComparer.Ordinal);
        fields["__RequestVerificationToken"] = antiforgeryToken;
        return new(fields);
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
