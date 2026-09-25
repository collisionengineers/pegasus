using System.Net;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Actors;
using Pegasus.Core.AiWork;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;
using Pegasus.Web.Pages;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The Work Centre as the v30 B ledger over Work Centre D1–D10: the compact
/// five-count strip, the paged Needs attention table grouped by due day with
/// empty groups omitted, Office/Mine, the kind filter and Find, the row that
/// opens its facts and actions in place, the section tabs, New cases with
/// arrival chips and the since-you-last-looked line, and AI jobs with per-kind
/// actions. The Core reads are replaced by recording fakes so the page's
/// rendering and the query it sends are what is asserted.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class WorkCentreWebTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public async Task NeedsAttentionGroupsByDueDayAndOmitsEmptyGroups()
    {
        var snapshot = new FakeSnapshot
        {
            Items =
            [
                Item(NeedsAttentionKind.ReviewCase, "QDOS26001", NeedsAttentionPriority.Overdue, Now.AddDays(-2)),
                Item(NeedsAttentionKind.HeldDecision, "QDOS26002", NeedsAttentionPriority.Normal, Now.AddDays(9))
            ],
            OverdueCount = 1,
            TodayCount = 0,
            LaterCount = 1
        };
        using var host = Host(snapshot);
        using var client = Client(host);

        var html = await GetOkAsync(client, "/");

        Assert.Contains("data-wc-group=\"overdue\"><td colspan=\"5\"><h3>Overdue (1)</h3>", html, StringComparison.Ordinal);
        Assert.Contains("data-wc-group=\"later\"><td colspan=\"5\"><h3>Later (1)</h3>", html, StringComparison.Ordinal);
        // An empty due group is not drawn (WG, operator 25 September 2026).
        Assert.DoesNotContain("Due today (0)", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Nothing due today", html, StringComparison.Ordinal);
        Assert.Contains("days overdue", html, StringComparison.Ordinal);
        Assert.Contains("wc-due--overdue", html, StringComparison.Ordinal);
        // Five metrics in their own refresh section, no Blocked (D7); the fifth
        // counts the active Triage Cases and opens the Cases Triage tab.
        Assert.Contains("data-wc-refresh-section=\"metrics\" data-wc-refresh-state=\"current\"", html, StringComparison.Ordinal);
        Assert.Equal(5, Regex.Count(html, "class=\"metric\" data-value="));
        Assert.Matches(
            "data-value=\"triage\" href=\"/Cases\\?tab=triage\">\\s*<span class=\"metric-label\"><span>Triages</span></span>\\s*<span class=\"metric-value\">5</span>",
            html);
        // The head reads Updated HH:MM; nothing opens by itself.
        Assert.Contains("data-wc-refresh-outcome-label>Updated ", html, StringComparison.Ordinal);
        Assert.DoesNotContain("wc-inline-detail", html, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"status status--red\"", html, StringComparison.Ordinal);
        // The ledger's columns.
        Assert.Contains(">Next action</th>", html, StringComparison.Ordinal);
        Assert.Contains(">Record / detail</th>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnEmptyOfficeSaysNoWorkToShowAndAnEmptyMineKeepsTheSwitch()
    {
        using var host = Host(new FakeSnapshot());
        using var client = Client(host);

        var office = await GetOkAsync(client, "/");
        Assert.Contains("No work to show.", office, StringComparison.Ordinal);
        Assert.Contains("class=\"panel wc-ledger\" hidden=\"hidden\"", office, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"wc-tab-attention\"", office, StringComparison.Ordinal);

        // Mine keeps its section, so Office stays one click away.
        var mine = await GetOkAsync(client, "/?scope=mine");
        Assert.DoesNotContain("No work to show.", mine, StringComparison.Ordinal);
        Assert.Contains("Nothing needs attention", mine, StringComparison.Ordinal);
        Assert.Contains("data-wc-scope-link=\"office\"", mine, StringComparison.Ordinal);
        Assert.Contains("Page 1 of 1 &#xB7; earliest due first", mine, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OfficeIsTheDefaultForAUserAndMineIsSentWhenChosen()
    {
        var snapshot = new FakeSnapshot
        {
            Items = [Item(NeedsAttentionKind.ReviewCase, "QDOS26003", NeedsAttentionPriority.Normal, Now.AddDays(3))],
            LaterCount = 1
        };
        using var host = Host(snapshot);
        using var client = Client(host, StaffRoleNames.User);

        var office = await GetOkAsync(client, "/");
        Assert.Equal(NeedsAttentionScope.Office, Assert.Single(snapshot.Queries).Scope);
        Assert.Contains("data-wc-scope=\"office\"", office, StringComparison.Ordinal);
        Assert.Contains("data-wc-scope-named=\"false\"", office, StringComparison.Ordinal);
        Assert.Contains("data-wc-scope-link=\"mine\"", office, StringComparison.Ordinal);

        snapshot.Queries.Clear();
        var mine = await GetOkAsync(client, "/?scope=mine");
        Assert.Equal(NeedsAttentionScope.Mine, Assert.Single(snapshot.Queries).Scope);
        Assert.Contains("data-wc-scope=\"mine\"", mine, StringComparison.Ordinal);
        Assert.Contains("data-wc-scope-named=\"true\"", mine, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheKindFilterAndFindAreSentAndChipsCountTheWholeScope()
    {
        var snapshot = new FakeSnapshot
        {
            UnfilteredKindCounts = new Dictionary<NeedsAttentionKind, int>
            {
                [NeedsAttentionKind.HeldDecision] = 3,
                [NeedsAttentionKind.Triage] = 5
            }
        };
        using var host = Host(snapshot);
        using var client = Client(host);

        var html = await GetOkAsync(client, "/?kind=held&kind=triage&q=+QDOS26+");

        // One read: Core counts the chips over the scope before the kind filter
        // and applies the term before paging.
        var filtered = Assert.Single(snapshot.Queries);
        Assert.Equal([NeedsAttentionKind.HeldDecision, NeedsAttentionKind.Triage], filtered.Kinds!.ToArray());
        Assert.Equal("QDOS26", filtered.Search);
        Assert.Matches("data-wc-kind=\"held\">Held<span class=\"n\">3</span>", html);
        Assert.Matches("data-wc-kind=\"triage\">Triage<span class=\"n\">5</span>", html);
        Assert.Contains("class=\"chip on\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"q\" value=\"QDOS26\"", html, StringComparison.Ordinal);
        // Clear filters drops both the kinds and the term; the chip links keep the term.
        Assert.Contains("href=\"/?scope=office\" data-wc-clear>Clear filters</a>", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/?scope=office&amp;kind=triage&amp;q=QDOS26\"", html, StringComparison.Ordinal);
        Assert.Contains("No work matches these filters.", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PagingReadsPageOfNAndLinksTheNeighbours()
    {
        var snapshot = new FakeSnapshot
        {
            Items = Enumerable.Range(0, 50)
                .Select(index => Item(NeedsAttentionKind.ReviewCase, $"QDOS2{index:D4}", NeedsAttentionPriority.Normal, Now.AddDays(20)))
                .ToArray(),
            TotalCountOverride = 120,
            LaterCount = 120
        };
        using var host = Host(snapshot);
        using var client = Client(host);

        var html = await GetOkAsync(client, "/?page=2");

        Assert.Equal(2, snapshot.Queries[0].Page);
        Assert.Contains("Page 2 of 3 &#xB7; earliest due first", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/?scope=office\">Previous</a>", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/?scope=office&amp;page=3\">Next</a>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheOpenRowShowsItsFactsAndOffersAssignToMeWithoutScript()
    {
        var unassigned = Item(NeedsAttentionKind.UnassignedEngineer, "QDOS26005", NeedsAttentionPriority.Today, Now.AddHours(3)) with
        {
            Route = "/Cases/00000000-0000-0000-0000-000000000005?section=assign"
        };
        using var host = Host(new FakeSnapshot { Items = [unassigned], TodayCount = 1 });
        using var client = Client(host);

        var closed = await GetOkAsync(client, "/");
        Assert.Contains($"href=\"/?scope=office&amp;selected={unassigned.Id:D}\"", closed, StringComparison.Ordinal);
        Assert.DoesNotContain("wc-inline-detail", closed, StringComparison.Ordinal);
        Assert.DoesNotContain("data-wc-take", closed, StringComparison.Ordinal);

        var html = await GetOkAsync(client, $"/?selected={unassigned.Id:D}");

        // The open row: its chip reads amber Due today, a second click closes it.
        Assert.Contains($"data-wc-row=\"{unassigned.Id}\" data-wc-row-kind=\"unassigned\" aria-selected=\"true\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/?scope=office\"\n", html.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Contains($"aria-expanded=\"true\" aria-controls=\"wc-detail-{unassigned.Id:D}\"", html, StringComparison.Ordinal);
        Assert.Contains($"id=\"wc-detail-{unassigned.Id}\"", html, StringComparison.Ordinal);
        // The chip's tone follows the fixture's due group; its words follow the host clock.
        Assert.Contains("class=\"status status--amber\">", html, StringComparison.Ordinal);
        Assert.Contains("<dt>Vehicle</dt>", html, StringComparison.Ordinal);
        // An enabled User may take an unowned row in place (P8).
        Assert.Contains("data-wc-take", html, StringComparison.Ordinal);
        Assert.Contains("handler=AssignToMe", html, StringComparison.Ordinal);
        Assert.Contains($"name=\"caseId\" value=\"{unassigned.Id}\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NewCasesShowArrivalChipsTheSinceYouLastLookedLineAndAutomationChanges()
    {
        var lastSeen = Now.AddHours(-5);
        var feed = new FakeRecentCases
        {
            LastSeen = lastSeen,
            Rows =
            [
                new(RecentCaseRowKind.NewCase, Guid.NewGuid(), "QDOS26100", "AB12CDE", "Mr A Claimant", "QDOS", Now.AddHours(-1), CaseArrival.ProviderApi),
                new(RecentCaseRowKind.ChangedByAutomation, Guid.NewGuid(), "QDOS26101", "CD34EFG", "Ms B Claimant", "QDOS", Now.AddHours(-2), CaseArrival.Automation, "case_workspace_saved"),
                new(RecentCaseRowKind.NewCase, Guid.NewGuid(), "PCH26102", "EF56GHJ", "Mr C Claimant", "PCH", Now.AddDays(-2), CaseArrival.Email)
            ]
        };
        using var host = Host(new FakeSnapshot(), feed);
        using var client = Client(host);

        var html = await GetOkAsync(client, "/?tab=new-cases");

        Assert.True(feed.Calls.Single().MarkSeen, "The first page of an open marks the look.");
        // The section has its tab, with its count, and is the one shown.
        Assert.Contains("data-wc-tab-link=\"new-cases\"\n               aria-selected=\"true\"", html.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Contains("New cases<span class=\"tab-count\">3</span>", html, StringComparison.Ordinal);
        Assert.Contains("data-wc-refresh-outcome=\"current\" data-wc-tab=\"new-cases\"", html, StringComparison.Ordinal);
        Assert.Contains("data-wc-arrival>Provider API</span>", html, StringComparison.Ordinal);
        Assert.Contains("data-wc-arrival>Automation</span>", html, StringComparison.Ordinal);
        Assert.Contains("data-wc-arrival>E-mail</span>", html, StringComparison.Ordinal);
        Assert.Contains("QDOS26101 &#xB7; Changed by automation", html, StringComparison.Ordinal);
        Assert.Equal(1, Regex.Count(html, "data-wc-divider"));
        Assert.True(
            html.IndexOf("QDOS26101", StringComparison.Ordinal) < html.IndexOf("data-wc-divider", StringComparison.Ordinal)
                && html.IndexOf("data-wc-divider", StringComparison.Ordinal) < html.IndexOf("PCH26102", StringComparison.Ordinal),
            "The line falls between what is new since the last look and what is not.");
        Assert.Contains("Last 7 days &#xB7; 3 rows", html, StringComparison.Ordinal);

        // A refresh does not move the person's last look.
        feed.Calls.Clear();
        _ = await GetOkAsync(client, $"/?refresh=true&since={Uri.EscapeDataString(lastSeen.ToString("O"))}");
        Assert.False(feed.Calls.Single().MarkSeen);
    }

    [Fact]
    public async Task EmptySectionsHaveNoTabAndAnUnavailableOneKeepsItsTabWithADash()
    {
        var snapshot = new FakeSnapshot
        {
            Items = [Item(NeedsAttentionKind.ReviewCase, "QDOS26150", NeedsAttentionPriority.Today, Now.AddHours(2))],
            TodayCount = 1
        };
        using var host = Host(snapshot, jobs: new FakeAiJobs { Throw = true });
        using var client = Client(host);

        var html = await GetOkAsync(client, "/?tab=new-cases");

        // No New cases: no tab, and the named tab falls back to the first shown.
        Assert.DoesNotContain("data-wc-tab-link=\"new-cases\"", html, StringComparison.Ordinal);
        Assert.Contains("data-wc-refresh-outcome=\"partial\" data-wc-tab=\"attention\"", html, StringComparison.Ordinal);
        // AI jobs could not be read: the tab stays with a dash and the section says so.
        Assert.Contains("AI jobs<span class=\"tab-count\">&#x2014;</span>", html, StringComparison.Ordinal);
        Assert.Contains("AI jobs are unavailable.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("No work to show.", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RefreshHandlerReturnsOnlyTheWorkCentreBodyAndRetainsRefreshState()
    {
        var lastSeen = Now.AddHours(-2);
        var selected = Item(NeedsAttentionKind.ReviewCase, "QDOS26150", NeedsAttentionPriority.Today, Now.AddHours(2));
        var snapshot = new FakeSnapshot { Items = [selected], TodayCount = 1, TotalCountOverride = 100 };
        var feed = new FakeRecentCases
        {
            LastSeen = lastSeen,
            Rows = [new(RecentCaseRowKind.NewCase, Guid.NewGuid(), "QDOS26100", "AB12CDE", "Mr A Claimant", "QDOS", Now.AddHours(-1), CaseArrival.Manual)]
        };
        using var host = Host(snapshot, feed);
        using var client = Client(host);

        using var response = await client.GetAsync(
            $"/?handler=Refresh&scope=mine&kind=review&q=QDOS&tab=new-cases&selected={selected.Id:D}&page=2&newPage=3&refresh=true&since={Uri.EscapeDataString(lastSeen.ToString("O"))}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Contains("data-work-centre", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<html", html, StringComparison.OrdinalIgnoreCase);
        var query = Assert.Single(snapshot.Queries);
        Assert.Equal(NeedsAttentionScope.Mine, query.Scope);
        Assert.Equal("QDOS", query.Search);
        Assert.Equal((3, false), Assert.Single(feed.Calls));
        Assert.Contains("name=\"refresh\" value=\"true\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"q\" value=\"QDOS\" data-refresh-field", html, StringComparison.Ordinal);
        Assert.Contains("name=\"tab\" value=\"new-cases\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"newPage\" value=\"3\"", html, StringComparison.Ordinal);
        Assert.Contains($"name=\"since\" value=\"{HtmlEncoder.Default.Encode($"{lastSeen:O}")}\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RefreshHandlerMarksOnlyTheFailedSectionsUnavailable()
    {
        using var host = Host(new FakeSnapshot { Throw = true });
        using var client = Client(host);

        var html = await GetOkAsync(client, "/?handler=Refresh&refresh=true");

        Assert.Contains("data-wc-refresh-outcome=\"partial\"", html, StringComparison.Ordinal);
        Assert.Contains("data-wc-refresh-section=\"metrics\" data-wc-refresh-state=\"unavailable\"", html, StringComparison.Ordinal);
        Assert.Contains("data-wc-refresh-section=\"attention\" data-wc-refresh-state=\"unavailable\"", html, StringComparison.Ordinal);
        Assert.Contains("data-wc-refresh-section=\"new-cases\" data-wc-refresh-state=\"current\"", html, StringComparison.Ordinal);
        Assert.Contains("data-wc-refresh-section=\"ai-jobs\" data-wc-refresh-state=\"current\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RefreshHandlerMarksTheWholeFragmentFailedWhenEverySectionReadFails()
    {
        using var host = Host(
            new FakeSnapshot { Throw = true },
            new FakeRecentCases { Throw = true },
            new FakeAiJobs { Throw = true });
        using var client = Client(host);

        var html = await GetOkAsync(client, "/?handler=Refresh&refresh=true");

        Assert.Contains("data-wc-refresh-outcome=\"failed\"", html, StringComparison.Ordinal);
        Assert.Contains(">Refresh unavailable</span>", html, StringComparison.Ordinal);
        Assert.Contains("data-wc-refresh-section=\"metrics\" data-wc-refresh-state=\"unavailable\"", html, StringComparison.Ordinal);
        Assert.Contains("data-wc-refresh-section=\"attention\" data-wc-refresh-state=\"unavailable\"", html, StringComparison.Ordinal);
        Assert.Contains("data-wc-refresh-section=\"new-cases\" data-wc-refresh-state=\"unavailable\"", html, StringComparison.Ordinal);
        Assert.Contains("data-wc-refresh-section=\"ai-jobs\" data-wc-refresh-state=\"unavailable\"", html, StringComparison.Ordinal);
        // Every failed section keeps its tab, with a dash.
        Assert.Equal(3, Regex.Count(html, "<span class=\"tab-count\">&#x2014;</span>"));
    }

    [Fact]
    public async Task AiJobsListDraftReadyTakenQueuedAndFailedWithPerKindActions()
    {
        var caseId = Guid.NewGuid();
        var estimate = Job(AiJobKind.Estimate, AiJobState.DraftReady, "QDOS26200", caseId);
        var query = Job(AiJobKind.QueryResponse, AiJobState.DraftReady, "QDOS26201", Guid.NewGuid());
        var taken = Job(AiJobKind.Estimate, AiJobState.Taken, "QDOS26202", Guid.NewGuid()) with { LeaseExpiresAtUtc = Now.AddMinutes(30) };
        var failed = Job(AiJobKind.Estimate, AiJobState.Failed, "QDOS26203", caseId) with
        {
            ClosedAtUtc = Now.AddHours(-3),
            ClosureReason = "The client refused the job: the estimate lines could not be read"
        };
        var research = Job(AiJobKind.MarketResearch, AiJobState.DraftReady, "QDOS26204", Guid.NewGuid());
        var jobs = new FakeAiJobs { Open = [estimate, query, taken, research] };
        var drafts = new FakeAiDrafts
        {
            Drafts =
            [
                new(estimate, AiDraftAction.ReviewEstimate, $"/Cases/{caseId:D}?section=estimate", Now.AddHours(-4), Now.AddDays(1)),
                new(query, AiDraftAction.OpenQuery, $"/Cases/{query.SubjectId:D}?section=correspondence", Now.AddHours(-4), Now.AddDays(1))
            ]
        };
        using var host = Host(new FakeSnapshot(), jobs: jobs, drafts: drafts);
        using var client = Client(host);
        // The window is the host clock's, not the test's: the failed job closed
        // three hours before the host's now.
        var hostNow = host.Services.GetRequiredService<TimeProvider>().GetUtcNow();
        failed = failed with { ClosedAtUtc = hostNow.AddHours(-3) };
        jobs.Recent.Add(failed);

        var html = await GetOkAsync(client, "/?tab=ai-jobs");

        Assert.Contains("AI jobs<span class=\"tab-count\">4</span>", html, StringComparison.Ordinal);
        Assert.Contains("2 draft ready &#xB7; 1 failed", html, StringComparison.Ordinal);
        Assert.Contains($"href=\"/Cases/{caseId:D}?section=estimate\">Review estimate</a>", html, StringComparison.Ordinal);
        Assert.Contains(">Open query</a>", html, StringComparison.Ordinal);
        // Complete job belongs to a Query response, not an Estimate; it returns to this tab.
        Assert.Equal(1, Regex.Count(html, "handler=CompleteAiJob"));
        Assert.Contains("name=\"returnUrl\" value=\"/?scope=office&amp;tab=ai-jobs\"", html, StringComparison.Ordinal);
        Assert.Contains("Started by", html, StringComparison.Ordinal);
        Assert.Contains("Lease expires", html, StringComparison.Ordinal);
        Assert.Contains("The client refused the job: the estimate lines could not be read", html, StringComparison.Ordinal);
        Assert.Contains($"data-wc-job=\"{failed.JobId}\"", html, StringComparison.Ordinal);
        // Market research never waits for a person.
        Assert.DoesNotContain($"data-wc-job=\"{research.JobId}\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFailedLiveReadIsStatedAndRendersNoZeroMetrics()
    {
        using var host = Host(new FakeSnapshot { Throw = true });
        using var client = Client(host);

        var html = await GetOkAsync(client, "/");

        Assert.Contains("Work Centre is unavailable. Refresh to run the live queues again.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"metric\" data-value=", html, StringComparison.Ordinal);
        Assert.Contains("Needs attention<span class=\"tab-count\">&#x2014;</span>", html, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(InvalidOperationException), html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheUtilityBarOmitsNewCaseOnTheWorkCentre()
    {
        using var host = Host(new FakeSnapshot());
        using var client = Client(host);

        var html = await GetOkAsync(client, "/");

        // Create Case has one home here: the page header (v30 WE).
        Assert.DoesNotContain(">New case</span>", html, StringComparison.Ordinal);
        Assert.Equal(1, Regex.Count(html, ">Create Case</span>"));
    }

    private static NeedsAttentionItem Item(
        NeedsAttentionKind kind,
        string reference,
        NeedsAttentionPriority priority,
        DateTimeOffset due) =>
        new(
            kind,
            Guid.NewGuid(),
            reference,
            "Ford Focus AB12CDE",
            "QDOS",
            kind.ToString(),
            priority,
            Owner: null,
            due,
            LastOutcome: null,
            Source: null,
            Attempts: null,
            Received: Now.AddDays(-3))
        {
            Route = "/Cases"
        };

    private static AiJobRecord Job(AiJobKind kind, AiJobState state, string reference, Guid subjectId) =>
        new(
            Guid.NewGuid(),
            kind,
            AiJobSubjectKind.Case,
            subjectId,
            reference,
            "front bumper replacement",
            null,
            null,
            state,
            ActorKind.Staff,
            Guid.NewGuid().ToString("D"),
            Now.AddHours(-6),
            Now.AddDays(3),
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            1);

    private static WebApplicationFactory<Program> Host(
        FakeSnapshot snapshot,
        FakeRecentCases? feed = null,
        FakeAiJobs? jobs = null,
        FakeAiDrafts? drafts = null) =>
        new IntakeWebApplicationFactory().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetOperationsSnapshot>();
                services.AddSingleton<IGetOperationsSnapshot>(snapshot);
                services.RemoveAll<IListRecentCases>();
                services.AddSingleton<IListRecentCases>(feed ?? new FakeRecentCases());
                services.RemoveAll<IAiJobQueries>();
                services.AddSingleton<IAiJobQueries>(jobs ?? new FakeAiJobs());
                services.RemoveAll<IAiDraftQueries>();
                services.AddSingleton<IAiDraftQueries>(drafts ?? new FakeAiDrafts());
            }));

    private static HttpClient Client(WebApplicationFactory<Program> host, string? role = null)
    {
        var client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });
        if (role is not null)
        {
            client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        }

        return client;
    }

    private static async Task<string> GetOkAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return html;
    }

    private sealed class FakeSnapshot : IGetOperationsSnapshot
    {
        public List<NeedsAttentionQuery> Queries { get; } = [];

        public NeedsAttentionItem[] Items { get; init; } = [];

        public int OverdueCount { get; init; }

        public int TodayCount { get; init; }

        public int LaterCount { get; init; }

        public int? TotalCountOverride { get; init; }

        public IReadOnlyDictionary<NeedsAttentionKind, int>? UnfilteredKindCounts { get; init; }

        public bool Throw { get; init; }

        public Task<OperationsSnapshot> ExecuteAsync(ActionActor actor, CancellationToken cancellationToken = default) =>
            ExecuteAsync(new NeedsAttentionQuery(actor), cancellationToken);

        public Task<OperationsSnapshot> ExecuteAsync(NeedsAttentionQuery query, CancellationToken cancellationToken = default)
        {
            Queries.Add(query);
            if (Throw)
            {
                throw new InvalidOperationException("sensitive store failure");
            }

            var total = TotalCountOverride ?? Items.Length;
            var counts = UnfilteredKindCounts
                ?? Enum.GetValues<NeedsAttentionKind>().ToDictionary(kind => kind, kind => Items.Count(item => item.Kind == kind));
            var page = new NeedsAttentionPage(Items, query.Page, GetOperationsSnapshot.PageSize, total, counts, OverdueCount, TodayCount, LaterCount);
            return Task.FromResult(new OperationsSnapshot(Now, new IntakeQueueCounts(0), 0, 0, [], new CaseStageCounts(1, 2, 3, 0), Items)
            {
                Attention = page,
                Scope = query.Scope,
                Metrics = new WorkCentreMetrics(1, 2, 3, 4, 5)
            });
        }
    }

    private sealed class FakeRecentCases : IListRecentCases
    {
        public List<(int Page, bool MarkSeen)> Calls { get; } = [];

        public IReadOnlyList<RecentCaseRow> Rows { get; init; } = [];

        public DateTimeOffset? LastSeen { get; init; }

        public bool Throw { get; init; }

        public Task<RecentCasesFeed> ExecuteAsync(ActionActor actor, int page, bool markSeen,
            CancellationToken cancellationToken, DateTimeOffset? asOfUtc = null)
        {
            Calls.Add((page, markSeen));
            if (Throw)
            {
                throw new InvalidOperationException("recent cases failed");
            }
            return Task.FromResult(new RecentCasesFeed(
                new RecentCasesPage(Rows, page, RecentCasesPolicy.PageSize, Rows.Count),
                RecentCasesPolicy.WindowStart(Now),
                LastSeen));
        }
    }

    private sealed class FakeAiJobs : IAiJobQueries
    {
        public IReadOnlyList<AiJobRecord> Open { get; init; } = [];

        public List<AiJobRecord> Recent { get; } = [];

        public bool Throw { get; init; }

        public Task<IReadOnlyList<AiJobRecord>> ListOpenAsync(CancellationToken cancellationToken) =>
            Throw
                ? Task.FromException<IReadOnlyList<AiJobRecord>>(new InvalidOperationException("AI jobs failed"))
                : Task.FromResult(Open);

        public Task<IReadOnlyList<AiJobRecord>> ListRecentAsync(int max, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AiJobRecord>>(Recent);

        public Task<IReadOnlyList<AiJobRecord>> ListTerminalInWindowAsync(DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AiJobRecord>>(Recent.Where(job =>
                (job.State == AiJobState.Expired ? job.ExpiresAtUtc : job.ClosedAtUtc) is { } terminal
                && terminal >= startUtc && terminal < endUtc).ToArray());

        public Task<AiJobQueryPage> ListOpenPageAsync(AiJobKind? kind, string grantId, DateTimeOffset? afterCreatedAtUtc, Guid? afterJobId, int limit, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<AiJobRecord>> ListForSubjectAsync(Guid subjectId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AiJobRecord>>([]);

        public Task<AiJobCounts> GetCountsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new AiJobCounts(Open.Count, Recent.Count));
    }

    private sealed class FakeAiDrafts : IAiDraftQueries
    {
        public IReadOnlyList<AiDraft> Drafts { get; init; } = [];

        public Task<IReadOnlyList<AiDraft>> ListForCaseAsync(Guid caseId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AiDraft>>(Drafts.Where(draft => draft.Job.SubjectId == caseId).ToArray());

        public Task<IReadOnlyList<AiDraft>> ListOpenAsync(CancellationToken cancellationToken) => Task.FromResult(Drafts);
    }
}
