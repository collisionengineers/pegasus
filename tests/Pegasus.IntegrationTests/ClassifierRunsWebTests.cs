using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Operations;
using Pegasus.Web.Presentation;
using static Pegasus.IntegrationTests.IntakeWebDriver;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Administration › Mail classifier (FRD-17; R1318 to R1320) over the real
/// page, Core and the EF store: Administrators only; its health, its runs
/// newest first with their filters and a run's dialog; an empty state on a
/// fresh install; and on the test instance, which these tests run as, the
/// one sentence that says why the live runs are missing, or the live runs
/// beside its own, each with its environment.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed partial class ClassifierRunsWebTests
{
    private const string Page = "/Administration/Classifier";
    private static readonly DateTimeOffset Now = IntakeWebApplicationFactory.FixedUtcNow;
    private static readonly ClassifierRunFacts Facts = new(false, "claude-sonnet-5-5", 20, 10, 2m, "1.0.0");

    [Theory]
    [InlineData("Engineer")]
    [InlineData("User")]
    public async Task ThePageIsForbiddenToAnyoneButAnAdministrator(string role)
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);

        using var response = await client.GetAsync(Page);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AFreshInstallSaysNoRunIsRecordedAndWhyTheLiveRunsAreMissing()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);

        var html = Decoded(await GetHtmlAsync(client, Page));

        Assert.Contains("data-classifier-empty", html, StringComparison.Ordinal);
        Assert.Contains(OperatorLabels.ClassifierRuns.None, html, StringComparison.Ordinal);
        Assert.Contains(OperatorLabels.ClassifierRuns.LiveOff, html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-classifier-health", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-classifier-runs", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheNavEntryAndHubCardStandBeforeLogs()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);

        var hub = await GetHtmlAsync(client, "/Administration");
        var page = await GetHtmlAsync(client, Page);

        var nav = page[page.IndexOf("class=\"admin-nav panel\"", StringComparison.Ordinal)..];
        var entry = nav.IndexOf($"href=\"{Page}\"", StringComparison.Ordinal);
        Assert.True(entry > nav.IndexOf("href=\"/Administration/Health\"", StringComparison.Ordinal), "The entry follows Service health.");
        Assert.True(entry < nav.IndexOf("href=\"/Administration/Logs\"", StringComparison.Ordinal), "The entry comes before Logs.");
        Assert.Contains("aria-current=\"page\"", nav[(entry - 80)..(entry + 120)], StringComparison.Ordinal);
        Assert.Contains("#icon-mail-search", nav[entry..], StringComparison.Ordinal);
        Assert.Contains($"class=\"admin-card\" href=\"{Page}\"", hub, StringComparison.Ordinal);
        Assert.Contains(OperatorLabels.Admin.Classifier, hub, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheRunsAreListedNewestFirstWithTheirHealthFiltersAndDialog()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);
        var linkedCase = Guid.NewGuid();
        var acted = await SeedAsync(factory, Now.AddMinutes(-10), "success", 0.25m,
        [
            Item(ClassifierRunAction.Classified, "cancellation", reason: "The sender cancels the inspection."),
            Item(ClassifierRunAction.Linked, caseId: linkedCase, caseReference: "QDOS26002", reason: "The registration matches."),
            Item(ClassifierRunAction.LeftForStaff, "new-instruction", refusal: "Pegasus knows no Principal for this e-mail.")
        ]);
        var idle = await SeedAsync(factory, Now.AddMinutes(-40), "nothing eligible", 0m, []);
        var failed = await SeedAsync(factory, Now.AddMinutes(-70), "failed: no verdicts", 0.5m,
            [Item(ClassifierRunAction.Failed, refusal: "no verdict in a completed run")]);
        var running = Guid.NewGuid();
        await WithStoreAsync(factory, store => store.OpenAsync(new(running, Facts, "pegasus-automation", Now.AddMinutes(-2)), CancellationToken.None));

        var html = Decoded(await GetHtmlAsync(client, Page));

        Assert.Equal(
            $"{running} {acted} {idle} {failed}",
            string.Join(' ', RunRow().Matches(html).Select(match => match.Groups["id"].Value)));
        Assert.Contains("1 classified · 1 linked · 1 left for staff", Row(html, acted), StringComparison.Ordinal);
        Assert.Contains(OperatorLabels.ClassifierRuns.Nothing, Row(html, idle), StringComparison.Ordinal);
        Assert.Contains("Failed", Row(html, failed), StringComparison.Ordinal);
        Assert.Contains("Running", Row(html, running), StringComparison.Ordinal);
        Assert.Contains("$0.25", Row(html, acted), StringComparison.Ordinal);
        Assert.Contains(OperatorLabels.ClassifierRuns.Write, Row(html, acted), StringComparison.Ordinal);
        // The test instance names each row's environment; the live ones are missing, and it says why.
        Assert.Contains("data-classifier-run-environment=\"Test\"", html, StringComparison.Ordinal);
        Assert.Contains(OperatorLabels.ClassifierRuns.LiveOff, html, StringComparison.Ordinal);

        var health = Section(html, "data-classifier-health");
        Assert.Contains(OperatorLabels.OfficeTime(Now.AddMinutes(-2)), health, StringComparison.Ordinal);
        Assert.Matches("<td>4</td>\\s*<td>0</td>\\s*<td>\\$0\\.75</td>\\s*<td>\\$0\\.75</td>", health);

        var onlyFailed = Decoded(await GetHtmlAsync(client, $"{Page}?Outcome=Failed"));
        Assert.Equal(failed.ToString(), Assert.Single(RunRow().Matches(onlyFailed)).Groups["id"].Value);
        var onlyActed = Decoded(await GetHtmlAsync(client, $"{Page}?Acted=acted"));
        Assert.Equal(acted.ToString(), Assert.Single(RunRow().Matches(onlyActed)).Groups["id"].Value);
        var none = Decoded(await GetHtmlAsync(client, $"{Page}?Outcome=CutShort"));
        Assert.Contains(OperatorLabels.ClassifierRuns.NoneMatch, none, StringComparison.Ordinal);
        var tooLong = Decoded(await GetHtmlAsync(client, $"{Page}?From=2031-03-01&To=2031-05-06"));
        Assert.Contains(OperatorLabels.ClassifierRuns.PeriodInvalid, tooLong, StringComparison.Ordinal);

        var dialog = Decoded(await GetHtmlAsync(client, $"{Page}?Run={acted}&RunEnvironment=Test"));
        var detail = Section(dialog, "data-classifier-run-detail");
        Assert.Contains(OperatorLabels.ClassifierRuns.Action(ClassifierRunAction.Classified, dryRun: false), detail, StringComparison.Ordinal);
        Assert.Contains("Cancellation", detail, StringComparison.Ordinal);
        Assert.Contains("The sender cancels the inspection.", detail, StringComparison.Ordinal);
        Assert.Contains($"href=\"/Cases/{linkedCase}\"", detail, StringComparison.Ordinal);
        Assert.Contains("Pegasus knows no Principal for this e-mail.", detail, StringComparison.Ordinal);
        Assert.Contains(OperatorLabels.ClassifierRuns.NoLongerHeld, detail, StringComparison.Ordinal);
        Assert.Contains("The run's summary.", detail, StringComparison.Ordinal);
        Assert.Matches("href=\"/Administration/Logs\\?Area=automation_mcp&Actor=pegasus-automation&From=[^\"]+&To=[^\"]+&Sort=oldest\"", detail);

        var missing = Decoded(await GetHtmlAsync(client, $"{Page}?Run={Guid.NewGuid()}&RunEnvironment=Test"));
        Assert.Contains(OperatorLabels.ClassifierRuns.RunNotFound, missing, StringComparison.Ordinal);
        Assert.DoesNotContain("data-classifier-run-detail", missing, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheInstanceShowsTheLiveRunsBesideItsOwnEachWithItsEnvironment()
    {
        var liveRun = Guid.NewGuid();
        var live = new LiveRuns(liveRun);
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory, live);
        var own = await SeedAsync(factory, Now.AddMinutes(-30), "success", 0.1m, [Item(ClassifierRunAction.Left)]);

        var html = Decoded(await GetHtmlAsync(client, Page));

        Assert.Equal(
            $"{liveRun} {own}",
            string.Join(' ', RunRow().Matches(html).Select(match => match.Groups["id"].Value)));
        Assert.Contains("data-classifier-run-environment=\"Live\"", Row(html, liveRun), StringComparison.Ordinal);
        Assert.Contains("data-classifier-run-environment=\"Test\"", Row(html, own), StringComparison.Ordinal);
        Assert.Contains("data-classifier-health-row=\"Test\"", html, StringComparison.Ordinal);
        Assert.Contains("data-classifier-health-row=\"Live\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain(OperatorLabels.ClassifierRuns.LiveOff, html, StringComparison.Ordinal);

        var onlyLive = Decoded(await GetHtmlAsync(client, $"{Page}?Environment=Live"));
        Assert.Equal(liveRun.ToString(), Assert.Single(RunRow().Matches(onlyLive)).Groups["id"].Value);

        var dialog = Decoded(await GetHtmlAsync(
            client, $"{Page}?Run={liveRun}&RunEnvironment=Live&Day={Now.UtcDateTime:yyyy-MM-dd}"));
        var detail = Section(dialog, "data-classifier-run-detail");
        Assert.Contains("A live subject", detail, StringComparison.Ordinal);
        Assert.Contains("sender@principal.test", detail, StringComparison.Ordinal);
        // Production's mail and Cases are not on the instance, so nothing links to them.
        Assert.DoesNotContain("href=\"/Inbox?open=", detail, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/Cases/", detail, StringComparison.Ordinal);
        Assert.Contains("QDOS26999", detail, StringComparison.Ordinal);
        Assert.DoesNotContain("data-classifier-action-logs", detail, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LiveRunsThatCannotBeReadSayWhyInOneSentence(bool refused)
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory, new LiveRuns(Guid.NewGuid(), refused));
        await SeedAsync(factory, Now.AddMinutes(-30), "success", 0.1m, []);

        var html = Decoded(await GetHtmlAsync(client, Page));

        Assert.Contains(
            refused ? OperatorLabels.ClassifierRuns.LiveRefused : OperatorLabels.ClassifierRuns.LiveUnavailable,
            html,
            StringComparison.Ordinal);
        Assert.DoesNotContain("data-classifier-health-row=\"Live\"", html, StringComparison.Ordinal);
        Assert.Single(RunRow().Matches(html));
    }

    private static HttpClient CreateClient(IntakeWebApplicationFactory factory, IProductionClassifierRuns? live = null) =>
        live is null
            ? IntakeWebDriver.CreateClient(factory)
            : IntakeWebDriver.CreateClient(factory.WithWebHostBuilder(builder =>
                builder.ConfigureTestServices(services => services.AddSingleton(live))));

    private static async Task<Guid> SeedAsync(
        IntakeWebApplicationFactory factory,
        DateTimeOffset startedAtUtc,
        string outcome,
        decimal cost,
        IReadOnlyList<ClassifierRunItem> items)
    {
        var runId = Guid.NewGuid();
        await WithStoreAsync(factory, async store =>
        {
            await store.OpenAsync(new(runId, Facts, "pegasus-automation", startedAtUtc), CancellationToken.None);
            await store.CloseAsync(
                new(
                    runId,
                    Facts,
                    "pegasus-automation",
                    startedAtUtc.AddMinutes(1),
                    new(outcome, items.Count, 0, false, 0, items.Count, 0, 0, 6, cost, 60_000, null, "The run's summary.", items),
                    ClassifierRunPolicy.OutcomeOf(outcome),
                    items.Count(item => ClassifierRunPolicy.IsAct(item.Action))),
                CancellationToken.None);
        });
        return runId;
    }

    private static async Task WithStoreAsync(IntakeWebApplicationFactory factory, Func<IClassifierRunStore, Task> act)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await act(scope.ServiceProvider.GetRequiredService<IClassifierRunStore>());
    }

    private static ClassifierRunItem Item(
        ClassifierRunAction action,
        string? category = null,
        Guid? caseId = null,
        string? caseReference = null,
        string? reason = null,
        string? refusal = null) =>
        new(Guid.NewGuid(), action, category, caseId, caseReference, reason, refusal, Now.AddMinutes(-9));

    private static string Decoded(string html) => WebUtility.HtmlDecode(html);

    /// <summary>One run's row of the list.</summary>
    private static string Row(string html, Guid runId)
    {
        var start = html.IndexOf($"data-classifier-run=\"{runId}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, $"No row for {runId}.");
        return html[start..html.IndexOf("</tr>", start, StringComparison.Ordinal)];
    }

    /// <summary>The section a marker is on, to its own close: the run's dialog holds sections of its own.</summary>
    private static string Section(string html, string marker)
    {
        var start = html.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"No {marker}.");
        var depth = 1;
        var at = start;
        while (depth > 0)
        {
            var open = html.IndexOf("<section", at, StringComparison.Ordinal);
            var close = html.IndexOf("</section>", at, StringComparison.Ordinal);
            Assert.True(close >= 0, $"The section of {marker} does not close.");
            if (open >= 0 && open < close)
            {
                depth++;
                at = open + 1;
            }
            else
            {
                depth--;
                at = close + 1;
            }
        }

        return html[start..at];
    }

    [GeneratedRegex("data-classifier-run=\"(?<id>[0-9a-f-]{36})\"")]
    private static partial Regex RunRow();

    /// <summary>The live runs as the instance's read of production's storage account would give them.</summary>
    private sealed class LiveRuns(Guid runId, bool? refused = null) : IProductionClassifierRuns
    {
        private ClassifierRunSummary Summary => new(
            runId,
            ClassifierRunEnvironment.Live,
            Now.AddMinutes(-5),
            Now.AddMinutes(-4),
            false,
            "success",
            ClassifierRunOutcome.Completed,
            1,
            0,
            false,
            0,
            0.2m,
            60_000,
            new Dictionary<ClassifierRunAction, int> { [ClassifierRunAction.Linked] = 1 });

        public Task<IReadOnlyList<ClassifierRunSummary>> ListAsync(
            DateTimeOffset fromUtc,
            DateTimeOffset toUtc,
            CancellationToken cancellationToken)
        {
            if (refused is { } isRefused)
            {
                throw new ProductionClassifierRunsUnavailableException(isRefused, "The live runs could not be read.");
            }

            IReadOnlyList<ClassifierRunSummary> runs = [Summary];
            return Task.FromResult(runs);
        }

        public Task<ClassifierRunDetail?> GetAsync(Guid id, DateOnly startedOnUtc, CancellationToken cancellationToken) =>
            Task.FromResult<ClassifierRunDetail?>(id != runId
                ? null
                : new(
                    Summary,
                    Facts,
                    1,
                    0,
                    0,
                    5,
                    null,
                    "One link.",
                    null,
                    [
                        new(
                            new(Guid.NewGuid(), ClassifierRunAction.Linked, null, Guid.NewGuid(), "QDOS26999", "The claim number matches.", null, Now.AddMinutes(-4)),
                            "A live subject",
                            "sender@principal.test",
                            MessageIsHere: false)
                    ],
                    ItemsCut: false));
    }
}
