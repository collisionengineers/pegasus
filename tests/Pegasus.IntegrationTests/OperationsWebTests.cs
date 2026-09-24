using System.Collections.Immutable;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Actors;
using Pegasus.Core.AiWork;
using Pegasus.Core.Cases;
using Pegasus.Core.Eva;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Core.Notifications;
using Pegasus.Core.Operations;
using Pegasus.Web.Authentication;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed partial class OperationsWebTests
{
    private static readonly DateTimeOffset FixedUtcNow =
        new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task OperationsCockpitLinksBothExactWorkspaces()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        using var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("href=\"/Operations\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OperationsPageIsStaffWorkspaceWithNoReceiptLedgerOrBoxSurface()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingOperationsStore();
        using var factory = Configure(baseFactory, store);
        using var client = CreateClient(factory);

        var html = await GetHtmlAsync(client, "/Operations");

        Assert.Contains("Operations", html, StringComparison.Ordinal);
        Assert.Contains("Attention required", html, StringComparison.Ordinal);
        // The composed list has no superseded placeholder heading or copy.
        Assert.DoesNotContain("AI operations", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Requesting an AI job and viewing live AI work are planned", html, StringComparison.Ordinal);
        // Health belongs to Administration even when its query is composed.
        Assert.DoesNotContain("Service health", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Automation MCP", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Send-to-AI transport", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Approve", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Reject", html, StringComparison.Ordinal);
        Assert.DoesNotContain("/Operations/Requests", html, StringComparison.Ordinal);
        Assert.DoesNotContain("/Operations/Email", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Received through API", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExternalWorkRetriesThroughTheCanonicalCommandWithoutReadingServiceHealth()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingOperationsStore();
        using var factory = Configure(baseFactory, store);
        using var client = CreateClient(factory);

        var html = await GetHtmlAsync(client, "/Operations");

        Assert.DoesNotContain("Service health", html, StringComparison.Ordinal);
        Assert.DoesNotContain("service-health-title", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<th scope=\"col\">Area</th>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<th scope=\"col\">Latest evidence</th>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<th scope=\"col\">Dependency</th>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Receiving dispatch", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Automation clients", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Intake dispatch", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Automation ingress", html, StringComparison.Ordinal);
        Assert.Contains("Vehicle lookup", html, StringComparison.Ordinal);
        Assert.Contains("Retry this work", html, StringComparison.Ordinal);
        Assert.DoesNotContain(" href=\"\"", html, StringComparison.Ordinal);

        using var response = await client.PostAsync(
            "/Operations?handler=RetryExternal",
            Form(
                AntiforgeryValue(html),
                ("workItemId", store.ExternalWorkId.ToString("D")),
                ("expectedAttemptCount", store.ExternalAttemptCount.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", OperationKeyValue(html))));

        AssertPrg(response, "/Operations");
        var command = Assert.IsType<RetryExternalWorkCommand>(store.ExternalRetry);
        Assert.Equal(store.ExternalWorkId, command.WorkItemId);
        Assert.Equal(store.ExternalAttemptCount, command.ExpectedAttemptCount);
        Assert.Equal(ActorKind.Staff, command.Actor.Kind);
    }

    [Theory]
    [InlineData("Engineer")]
    [InlineData("User")]
    public async Task OperationsDoesNotRenderServiceHealthForNonAdministrators(string role)
    {
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var store = new RecordingOperationsStore();
        using var factory = Configure(baseFactory, store);
        using var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);

        var html = await GetHtmlAsync(client, "/Operations");

        Assert.DoesNotContain("Service health", html, StringComparison.Ordinal);
        Assert.DoesNotContain(" href=\"\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PartialDataNoticeLinksAdministrationHealthWithoutLoadingItsSnapshot()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingOperationsStore { LimitReached = true };
        using var factory = Configure(baseFactory, store);
        using var client = CreateClient(factory);

        var html = await GetHtmlAsync(client, "/Operations");

        Assert.Equal(1, Regex.Count(html, "notice notice--warning"));
        Assert.Contains("Partial data", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/Administration/Health\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Showing recent operational results; refresh for the latest activity.",
            html,
            StringComparison.Ordinal);
        Assert.DoesNotContain(" href=\"\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EvaHandoffsShowsOnlyRecordedHealthFacts()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingOperationsStore();
        var evaSubmissions = new RecordingEvaSubmissions();
        using var factory = Configure(baseFactory, store, evaSubmissions: evaSubmissions);
        using var client = CreateClient(factory);

        var html = await GetHtmlAsync(client, "/Operations");

        Assert.Contains("EVA handoffs", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Pending work", html, StringComparison.Ordinal);
        Assert.Contains("Latest activity", html, StringComparison.Ordinal);
        Assert.Contains("Failures", html, StringComparison.Ordinal);
        Assert.Contains(
            Pegasus.Web.Presentation.OperatorLabels.OfficeTime(FixedUtcNow),
            html,
            StringComparison.Ordinal);
        Assert.Contains(
            Pegasus.Web.Presentation.OperatorLabels.OfficeTime(evaSubmissions.FailureAtUtc),
            html,
            StringComparison.Ordinal);
        Assert.DoesNotContain(evaSubmissions.FailureCaseId.ToString("D"), html, StringComparison.Ordinal);
        Assert.DoesNotContain(evaSubmissions.FailureCode, html, StringComparison.Ordinal);
        Assert.Equal(1, evaSubmissions.ActivityCalls);
        Assert.Equal(1, evaSubmissions.RecentFailuresCalls);
    }

    [Fact]
    public async Task AiJobListShowsLiveJobsAndOnlyTodaysTerminalJobs()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingOperationsStore();
        var aiWork = new RecordingAiWorkStore();
        using var factory = Configure(baseFactory, store, aiWork: aiWork);
        using var client = CreateClient(factory);

        var html = await GetHtmlAsync(client, "/Operations");

        Assert.Contains("AI Job List", html, StringComparison.Ordinal);
        // Three non-terminal jobs, the job explicitly completed today and the
        // queued job that effectively expired today; the job cancelled a week
        // ago is not on the list (FRD-27).
        Assert.Contains("6 jobs", html, StringComparison.Ordinal);
        Assert.Contains("Unidentified resolution", html, StringComparison.Ordinal);
        Assert.Contains("Unidentified-queue pass", html, StringComparison.Ordinal);
        Assert.Contains("Market research", html, StringComparison.Ordinal);
        Assert.Contains(RecordingAiWorkStore.UnidentifiedReference, html, StringComparison.Ordinal);
        Assert.Contains(RecordingAiWorkStore.CompletedInstruction, html, StringComparison.Ordinal);
        var expiredRow = RowContaining(html, RecordingAiWorkStore.ExpiredInstruction);
        Assert.Contains("Expired", expiredRow, StringComparison.Ordinal);
        Assert.DoesNotContain("<form", expiredRow, StringComparison.Ordinal);
        Assert.DoesNotContain(RecordingAiWorkStore.LastWeekInstruction, html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AiJobListIncludesOldJobClosedTodayBehindTwoHundredNewCreations()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var aiWork = new RecordingAiWorkStore { AdditionalRecentQueued = 201 };
        using var factory = Configure(baseFactory, new RecordingOperationsStore(), aiWork: aiWork);
        using var client = CreateClient(factory);

        var html = await GetHtmlAsync(client, "/Operations");

        Assert.Contains(RecordingAiWorkStore.CompletedInstruction, html, StringComparison.Ordinal);
        Assert.Contains("207 jobs", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AiJobListIncludesEveryJobClosedTodayBeyondTwoHundred()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var aiWork = new RecordingAiWorkStore { AdditionalTerminalToday = 201 };
        using var factory = Configure(baseFactory, new RecordingOperationsStore(), aiWork: aiWork);
        using var client = CreateClient(factory);

        var html = await GetHtmlAsync(client, "/Operations");

        Assert.Contains("207 jobs", html, StringComparison.Ordinal);
        Assert.Contains("Terminal result 201", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AiJobListOmitsTheEmptyStateAndTableWhenThereAreNoJobs()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingOperationsStore();
        var aiWork = new RecordingAiWorkStore { HasJobs = false };
        using var factory = Configure(baseFactory, store, aiWork: aiWork);
        using var client = CreateClient(factory);

        var html = await GetHtmlAsync(client, "/Operations");

        Assert.Contains("AI Job List", html, StringComparison.Ordinal);
        Assert.DoesNotContain("No AI jobs", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<caption class=\"sr-only\">AI Job List</caption>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AiJobListNamesTheRecordAndNeverThePersistedQueueToken()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingOperationsStore();
        var aiWork = new RecordingAiWorkStore();
        using var factory = Configure(baseFactory, store, aiWork: aiWork);
        using var client = CreateClient(factory);

        var html = await GetHtmlAsync(client, "/Operations");

        // A queue pass has no record behind it, so it prints the operator's
        // words for the queue and never the Core subject token.
        Assert.Contains("Unidentified queue", html, StringComparison.Ordinal);
        Assert.DoesNotContain("unidentified-queue", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AiJobListNamesWhoStartedEachJobAndNeverTheStoredSubjectId()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingOperationsStore();
        var aiWork = new RecordingAiWorkStore();
        using var factory = Configure(baseFactory, store, aiWork: aiWork);
        using var client = CreateClient(factory);

        var html = await GetHtmlAsync(client, "/Operations");

        // The ledger stores the acting actor's subject id, which for staff is a
        // GUID. FRD-27 gives Started by "a staff username or the connector
        // client name", so the column resolves the name and never prints the
        // recorded identifier.
        var staffRow = RowContaining(html, RecordingAiWorkStore.EstimateInstruction);
        Assert.Contains(DevelopmentOfflineIdentity.UserName, staffRow, StringComparison.Ordinal);
        Assert.DoesNotContain(RecordingAiWorkStore.StaffCreator, staffRow, StringComparison.OrdinalIgnoreCase);

        // A job the connector started keeps the client name it recorded.
        var automationRow = RowContaining(html, "Unidentified queue");
        Assert.Contains(RecordingAiWorkStore.AutomationCreator, automationRow, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DraftReadyJobsOfferOnlyTheActionsTheirKindNames()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingOperationsStore();
        var aiWork = new RecordingAiWorkStore();
        using var factory = Configure(baseFactory, store, aiWork: aiWork);
        using var client = CreateClient(factory);

        var html = await GetHtmlAsync(client, "/Operations");

        // An estimate draft is reviewed on the Case's Repair Spec section, the
        // route the Work Centre and the Case Next action open (Core's
        // AiDraftRoute), and is closed there by Use repair spec, never by hand
        // from this table. The Case reference alone is ambiguous (the
        // MarketResearch fixture shares it and renders newer, hence first), so
        // the row is found by the estimate job's own instruction text instead.
        var estimateRow = RowContaining(html, RecordingAiWorkStore.EstimateInstruction);
        Assert.Contains("Review estimate", estimateRow, StringComparison.Ordinal);
        Assert.Contains(
            $"href=\"{StaffNotificationPolicy.CaseRoute(aiWork.SubjectCaseId, "estimate")}\"",
            estimateRow,
            StringComparison.Ordinal);
        Assert.DoesNotContain("/Assessment", estimateRow, StringComparison.Ordinal);
        Assert.DoesNotContain("Complete job", estimateRow, StringComparison.Ordinal);

        // A queue pass draft is the opposite: nothing to open, closed by hand.
        var queuePassRow = RowContaining(html, "Unidentified queue");
        Assert.Contains("Complete job", queuePassRow, StringComparison.Ordinal);
        Assert.DoesNotContain("Review estimate", queuePassRow, StringComparison.Ordinal);

        var marketResearchRow = RowContaining(html, RecordingAiWorkStore.MarketResearchInstruction);
        Assert.Contains("Market research", marketResearchRow, StringComparison.Ordinal);
        Assert.Contains("Complete job", marketResearchRow, StringComparison.Ordinal);
        Assert.DoesNotContain("Review estimate", marketResearchRow, StringComparison.Ordinal);

        // Every non-terminal job may be cancelled with a reason.
        Assert.Contains("CancelAiJob", estimateRow, StringComparison.Ordinal);
        Assert.Contains("CancelAiJob", queuePassRow, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ATerminalJobRowOffersNoControlAndRendersTheDash()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingOperationsStore();
        var aiWork = new RecordingAiWorkStore();
        using var factory = Configure(baseFactory, store, aiWork: aiWork);
        using var client = CreateClient(factory);

        var html = await GetHtmlAsync(client, "/Operations");

        var completedRow = RowContaining(html, RecordingAiWorkStore.CompletedInstruction);
        Assert.Contains("—", completedRow, StringComparison.Ordinal);
        Assert.DoesNotContain("<form", completedRow, StringComparison.Ordinal);
        Assert.DoesNotContain("Review estimate", completedRow, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendUnidentifiedToAiCreatesAnUnidentifiedResolutionJobForTheChosenItem()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingOperationsStore();
        var aiWork = new RecordingAiWorkStore();
        using var factory = Configure(baseFactory, store, aiWork: aiWork);
        using var client = CreateClient(factory);
        var html = await GetHtmlAsync(client, "/Operations");

        Assert.Contains("Send Unidentified to AI", html, StringComparison.Ordinal);
        Assert.Contains("name=\"unidentifiedReference\"", html, StringComparison.Ordinal);
        // The rendered page counts open items once for the global Cases rail.
        // The action resolves one indexed reference and its redirect must not
        // enumerate the queue for an unused shell.
        Assert.Equal(1, aiWork.QueueCountCalls);
        Assert.Equal(0, aiWork.QueueListCalls);

        using var response = await client.PostAsync(
            "/Operations?handler=SendUnidentifiedToAi",
            Form(
                AntiforgeryValue(html),
                ("unidentifiedReference", RecordingAiWorkStore.UnidentifiedReference),
                ("operationKey", OperationKeyValue(html))));

        AssertPrg(response, "/Operations");
        var command = Assert.IsType<CreateAiJobCommand>(aiWork.Created);
        // Operator decision D5 and FRD-27: the button starts a resolution for one U
        // reference; the queue pass belongs to the Automation Actor.
        Assert.Equal(AiJobKind.UnidentifiedResolution, command.Kind);
        Assert.Equal(aiWork.OpenUnidentifiedId, command.SubjectId);
        Assert.Equal(ActorKind.Staff, command.Actor.Kind);
        Assert.False(string.IsNullOrWhiteSpace(command.Instruction));
        Assert.Equal(1, aiWork.ReferenceLookupCalls);
        Assert.Equal(1, aiWork.QueueCountCalls);
        Assert.Equal(0, aiWork.QueueListCalls);
    }

    [Fact]
    public async Task SendUnidentifiedToAiRefusesAReferenceThatIsNotOpen()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingOperationsStore();
        var aiWork = new RecordingAiWorkStore { HasOpenUnidentified = false };
        using var factory = Configure(baseFactory, store, aiWork: aiWork);
        using var client = CreateClient(factory);

        var html = await GetHtmlAsync(client, "/Operations");

        Assert.Contains("AI Job List", html, StringComparison.Ordinal);
        // The prior absence contract depended on enumerating the open queue on
        // GET. The bounded form is always present and refuses a closed or
        // missing reference through the indexed lookup below.
        Assert.Contains("Send Unidentified to AI", html, StringComparison.Ordinal);

        using var response = await client.PostAsync(
            "/Operations?handler=SendUnidentifiedToAi",
            Form(
                AntiforgeryValue(html),
                ("unidentifiedReference", RecordingAiWorkStore.UnidentifiedReference),
                ("operationKey", OperationKeyValue(html))));

        AssertPrg(response, "/Operations");
        Assert.Null(aiWork.Created);
        Assert.Equal(1, aiWork.ReferenceLookupCalls);
    }

    [Fact]
    public async Task SendUnidentifiedToAiSurfacesTheAdministratorRefusal()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingOperationsStore();
        var aiWork = new RecordingAiWorkStore { RefuseCreate = true };
        using var factory = Configure(baseFactory, store, aiWork: aiWork);
        using var client = CreateClient(factory);
        var html = await GetHtmlAsync(client, "/Operations");

        using var response = await client.PostAsync(
            "/Operations?handler=SendUnidentifiedToAi",
            Form(
                AntiforgeryValue(html),
                ("unidentifiedReference", RecordingAiWorkStore.UnidentifiedReference),
                ("operationKey", OperationKeyValue(html))));

        AssertPrg(response, "/Operations");
        // The refusal is recorded and read back, never swallowed.
        var after = await GetHtmlAsync(client, "/Operations");
        Assert.Contains("AI work is not accepting new jobs.", after, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompleteAiJobConfirmsThroughTheCanonicalCommand()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingOperationsStore();
        var aiWork = new RecordingAiWorkStore();
        using var factory = Configure(baseFactory, store, aiWork: aiWork);
        using var client = CreateClient(factory);
        var html = await GetHtmlAsync(client, "/Operations");

        using var response = await client.PostAsync(
            "/Operations?handler=CompleteAiJob",
            Form(
                AntiforgeryValue(html),
                ("jobId", aiWork.QueuePassDraftJobId.ToString("D")),
                ("expectedVersion", RecordingAiWorkStore.QueuePassVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", OperationKeyValue(html))));

        AssertPrg(response, "/Operations");
        var command = Assert.IsType<ConfirmAiJobCommand>(aiWork.Confirmed);
        Assert.Equal(aiWork.QueuePassDraftJobId, command.JobId);
        Assert.Equal(RecordingAiWorkStore.QueuePassVersion, command.ExpectedVersion);
        Assert.Equal(ActorKind.Staff, command.Actor.Kind);
    }

    [Fact]
    public async Task MarketResearchDraftCompletesOnlyThroughTheExistingStaffAction()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingOperationsStore();
        var aiWork = new RecordingAiWorkStore();
        using var factory = Configure(baseFactory, store, aiWork: aiWork);
        using var client = CreateClient(factory);
        var html = await GetHtmlAsync(client, "/Operations");

        using var response = await client.PostAsync(
            "/Operations?handler=CompleteAiJob",
            Form(
                AntiforgeryValue(html),
                ("jobId", aiWork.MarketResearchDraftJobId.ToString("D")),
                ("expectedVersion", RecordingAiWorkStore.MarketResearchVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", OperationKeyValue(html))));

        AssertPrg(response, "/Operations");
        var command = Assert.IsType<ConfirmAiJobCommand>(aiWork.Confirmed);
        Assert.Equal(aiWork.MarketResearchDraftJobId, command.JobId);
        Assert.Equal(RecordingAiWorkStore.MarketResearchVersion, command.ExpectedVersion);
        Assert.Equal(ActorKind.Staff, command.Actor.Kind);
    }

    [Fact]
    public async Task CompleteAiJobRefusesAJobWhoseDraftNeedsARecordAction()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingOperationsStore();
        var aiWork = new RecordingAiWorkStore();
        using var factory = Configure(baseFactory, store, aiWork: aiWork);
        using var client = CreateClient(factory);
        var html = await GetHtmlAsync(client, "/Operations");

        using var response = await client.PostAsync(
            "/Operations?handler=CompleteAiJob",
            Form(
                AntiforgeryValue(html),
                ("jobId", aiWork.EstimateDraftJobId.ToString("D")),
                ("expectedVersion", "3"),
                ("operationKey", OperationKeyValue(html))));

        AssertPrg(response, "/Operations");
        Assert.Null(aiWork.Confirmed);
    }

    [Fact]
    public async Task CancelAiJobCarriesTheOperatorReason()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingOperationsStore();
        var aiWork = new RecordingAiWorkStore();
        using var factory = Configure(baseFactory, store, aiWork: aiWork);
        using var client = CreateClient(factory);
        var html = await GetHtmlAsync(client, "/Operations");

        using var response = await client.PostAsync(
            "/Operations?handler=CancelAiJob",
            Form(
                AntiforgeryValue(html),
                ("jobId", aiWork.QueuedResolutionJobId.ToString("D")),
                ("expectedVersion", RecordingAiWorkStore.QueuedResolutionVersion.ToString(CultureInfo.InvariantCulture)),
                ("reason", "The item was resolved by hand."),
                ("operationKey", OperationKeyValue(html))));

        AssertPrg(response, "/Operations");
        var command = Assert.IsType<CancelAiJobCommand>(aiWork.Cancelled);
        Assert.Equal(aiWork.QueuedResolutionJobId, command.JobId);
        Assert.Equal("The item was resolved by hand.", command.Reason);
        Assert.Equal(ActorKind.Staff, command.Actor.Kind);
    }

    [Fact]
    public async Task CancelAiJobWithoutAReasonIsRefusedBeforeCore()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingOperationsStore();
        var aiWork = new RecordingAiWorkStore();
        using var factory = Configure(baseFactory, store, aiWork: aiWork);
        using var client = CreateClient(factory);
        var html = await GetHtmlAsync(client, "/Operations");

        using var response = await client.PostAsync(
            "/Operations?handler=CancelAiJob",
            Form(
                AntiforgeryValue(html),
                ("jobId", aiWork.QueuedResolutionJobId.ToString("D")),
                ("expectedVersion", RecordingAiWorkStore.QueuedResolutionVersion.ToString(CultureInfo.InvariantCulture)),
                ("reason", "   "),
                ("operationKey", OperationKeyValue(html))));

        AssertPrg(response, "/Operations");
        Assert.Null(aiWork.Cancelled);
    }

    [Theory]
    [InlineData("/Received")]
    [InlineData("/Received?decision=needs_sorting")]
    [InlineData("/Operations/Requests")]
    [InlineData("/Operations/Requests?handler=RetryExternal")]
    [InlineData("/Operations/Email")]
    public async Task ObsoleteListRoutesReturnNotFound(string route)
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);

        using var response = await client.GetAsync(route);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<string> GetHtmlAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// The one table row carrying <paramref name="marker"/>, so a per-row
    /// assertion cannot pass on another row's markup.
    /// </summary>
    private static string RowContaining(string html, string marker)
    {
        var anchor = html.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(anchor >= 0, $"The page must render a row containing '{marker}'.");
        var start = html.LastIndexOf("<tr>", anchor, StringComparison.Ordinal);
        Assert.True(start >= 0, $"'{marker}' must appear inside a table row.");
        var end = html.IndexOf("</tr>", anchor, StringComparison.Ordinal);
        Assert.True(end > start, $"The row containing '{marker}' must be closed.");
        return html[start..end];
    }

    private static WebApplicationFactory<Program> Configure(
        IntakeWebApplicationFactory baseFactory,
        RecordingOperationsStore store,
        RecordingAiWorkStore? aiWork = null,
        RecordingEvaSubmissions? evaSubmissions = null) => baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                if (aiWork is not null)
                {
                    services.RemoveAll<IAiJobQueries>();
                    services.RemoveAll<ICreateAiJob>();
                    services.RemoveAll<IConfirmAiJob>();
                    services.RemoveAll<ICancelAiJob>();
                    services.RemoveAll<IUnidentifiedStore>();
                    services.AddSingleton<IAiJobQueries>(aiWork);
                    services.AddSingleton<ICreateAiJob>(aiWork);
                    services.AddSingleton<IConfirmAiJob>(aiWork);
                    services.AddSingleton<ICancelAiJob>(aiWork);
                    services.AddSingleton<IUnidentifiedStore>(aiWork);
                }
                if (evaSubmissions is not null)
                {
                    services.RemoveAll<IEvaSubmissionQueries>();
                    services.AddSingleton<IEvaSubmissionQueries>(evaSubmissions);
                }
                services.RemoveAll<IEmailOperationsProjectionStore>();
                services.RemoveAll<IRequestOperationsProjectionStore>();
                services.RemoveAll<IMailboxProcessingRetryStore>();
                services.RemoveAll<IExternalWorkRetryStore>();
                services.AddSingleton<IEmailOperationsProjectionStore>(store);
                services.AddSingleton<IRequestOperationsProjectionStore>(store);
                services.AddSingleton<IMailboxProcessingRetryStore>(store);
                services.AddSingleton<IExternalWorkRetryStore>(store);
            }));

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

    private static FormUrlEncodedContent Form(
        string antiforgeryToken,
        params (string Name, string Value)[] values)
    {
        var fields = values.ToDictionary(item => item.Name, item => item.Value, StringComparer.Ordinal);
        fields["__RequestVerificationToken"] = antiforgeryToken;
        return new(fields);
    }

    private static string AntiforgeryValue(string html)
    {
        var tag = AntiforgeryTagRegex().Match(html);
        Assert.True(tag.Success, "The operations action must render an antiforgery token.");
        var value = ValueRegex().Match(tag.Value);
        Assert.True(value.Success, "The operations antiforgery token must have a value.");
        return WebUtility.HtmlDecode(value.Groups["value"].Value);
    }

    private static string OperationKeyValue(string html)
    {
        var tag = OperationKeyTagRegex().Match(html);
        Assert.True(tag.Success, "The operations lease action must render an operation key.");
        var value = ValueRegex().Match(tag.Value);
        Assert.True(value.Success, "The operations lease operation key must have a value.");
        return WebUtility.HtmlDecode(value.Groups["value"].Value);
    }

    private static void AssertPrg(HttpResponseMessage response, string expectedPath)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(expectedPath, response.Headers.Location?.OriginalString);
    }

    [GeneratedRegex("<input[^>]*name=\"__RequestVerificationToken\"[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AntiforgeryTagRegex();

    [GeneratedRegex("<input[^>]*name=\"operationKey\"[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OperationKeyTagRegex();

    [GeneratedRegex("value=\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ValueRegex();

    private sealed class RecordingOperationsStore :
        IEmailOperationsProjectionStore,
        IRequestOperationsProjectionStore,
        IMailboxProcessingRetryStore,
        IExternalWorkRetryStore
    {
        public Guid CaseId { get; } = Guid.NewGuid();
        public Guid IntakeId { get; } = Guid.NewGuid();
        public Guid TriageCaseId { get; } = Guid.NewGuid();
        public Guid ExternalWorkId { get; } = Guid.NewGuid();
        public string ReceivedMailboxId { get; } = "approved-inbox";
        public string MailboxFailureCode { get; } = "source_unavailable";
        public DateTimeOffset MailboxFailureDueAtUtc { get; } = FixedUtcNow.AddMinutes(5);
        public int ExternalAttemptCount { get; } = 5;
        public bool LimitReached { get; init; }
        public RetryMailboxProcessingCommand? MailboxRetry { get; private set; }
        public RetryExternalWorkCommand? ExternalRetry { get; private set; }

        public Task<int> CountRetryableExternalFailuresAsync(
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken) =>
            Task.FromResult(1);

        public Task<EmailOperationsProjection> GetAsync(
            int maximumItemsPerDirection,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken) => Task.FromResult(new EmailOperationsProjection(
                ImmutableArray.Create(
                    Email("received-failed", EmailOperationDirection.Received, EmailOperationState.Failed, MailboxFailureCode, ReceivedMailboxId, MailboxFailureDueAtUtc),
                    Email("received-pending", EmailOperationDirection.Received, EmailOperationState.Pending),
                    Email("received-intake", EmailOperationDirection.Received, EmailOperationState.Succeeded, intakeId: IntakeId),
                    Email("received-unknown", EmailOperationDirection.Received, EmailOperationState.Unknown)),
                ImmutableArray.Create(
                    Email("sent-failed", EmailOperationDirection.Sent, EmailOperationState.Failed, "sent_source_unavailable", "approved-sent", FixedUtcNow.AddMinutes(10)),
                    Email("sent-triage", EmailOperationDirection.Sent, EmailOperationState.Succeeded, triageCaseId: TriageCaseId),
                    Email("sent-case", EmailOperationDirection.Sent, EmailOperationState.Succeeded, caseId: CaseId, caseReference: "QD31001", principalCode: "QD")),
                ReceivedLimitReached: false,
                SentLimitReached: false));

        Task<RequestOperationsProjection> IRequestOperationsProjectionStore.GetAsync(
            int maximumItems,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken) => Task.FromResult(new RequestOperationsProjection(
                ImmutableArray.Create(
                    Request(ExternalWorkId, RequestOperationState.Failed, canRetry: true, attemptCount: ExternalAttemptCount),
                    Request(Guid.NewGuid(), RequestOperationState.Pending),
                    Request(Guid.NewGuid(), RequestOperationState.UnknownExternal)),
                LimitReached));

        public Task<OperationsRetryResult> RetryAsync(
            RetryMailboxProcessingCommand command,
            DateTimeOffset retryAtUtc,
            CancellationToken cancellationToken)
        {
            MailboxRetry = command;
            return Task.FromResult(new OperationsRetryResult(IsReplay: false));
        }

        public Task<OperationsRetryResult> RetryAsync(
            RetryExternalWorkCommand command,
            DateTimeOffset retryAtUtc,
            CancellationToken cancellationToken)
        {
            ExternalRetry = command;
            return Task.FromResult(new OperationsRetryResult(IsReplay: false));
        }

        private static EmailOperationProjection Email(
            string id,
            EmailOperationDirection direction,
            EmailOperationState state,
            string? failureCode = null,
            string? retryMailboxId = null,
            DateTimeOffset? retryDueAtUtc = null,
            Guid? intakeId = null,
            Guid? triageCaseId = null,
            Guid? caseId = null,
            string? caseReference = null,
            string? principalCode = null) => new(
                id,
                direction,
                state,
                "operations@example.invalid",
                FixedUtcNow,
                intakeId,
                triageCaseId,
                caseId,
                caseReference,
                principalCode,
                failureCode,
                retryMailboxId,
                retryDueAtUtc);

        private RequestOperationProjection Request(
            Guid id,
            RequestOperationState state,
            bool canRetry = false,
            int? attemptCount = null) => new(
                id,
                state,
                CaseId,
                "QD31001",
                "QD",
                FixedUtcNow,
                ExternalKind: "vehicle_lookup",
                attemptCount,
                FailureCode: state == RequestOperationState.Failed ? "queue_poisoned" : null,
                FailureReason: state == RequestOperationState.Failed ? "The retry policy was exhausted." : null,
                canRetry);
    }

    /// <summary>
    /// The AI job ledger and the Unidentified queue as the Operations page
    /// reads and writes them. The listing methods mirror the real store: the
    /// open query returns only persisted-open jobs, the recent query returns
    /// every job newest first, and both hand back records whose state is
    /// already the effective one.
    /// </summary>
    private sealed class RecordingAiWorkStore :
        IAiJobQueries,
        ICreateAiJob,
        IConfirmAiJob,
        ICancelAiJob,
        IUnidentifiedStore
    {
        public const string CaseReference = "QD31002";
        public const string UnidentifiedReference = "U412";
        public const string EstimateInstruction = "Draft an estimate to the recorded target.";
        public const string CompletedInstruction = "Draft the estimate that was already accepted.";
        public const string ExpiredInstruction = "A queued job expired without being claimed.";
        public const string LastWeekInstruction = "A job cancelled a week ago.";
        public const string MarketResearchInstruction = "Research comparable vehicles.";
        public const string AutomationCreator = "overnight-pass";

        /// <summary>
        /// What the ledger actually stores for a staff creator: the actor's
        /// subject id, never a username (<c>EfAiJobStore.CreateAsync</c>).
        /// </summary>
        public static readonly string StaffCreator =
            DevelopmentOfflineIdentity.AdministratorId.ToString("D");

        public const long QueuePassVersion = 2;
        public const long QueuedResolutionVersion = 1;
        public const long MarketResearchVersion = 4;

        public Guid EstimateDraftJobId { get; } = Guid.NewGuid();
        public Guid QueuedResolutionJobId { get; } = Guid.NewGuid();
        public Guid QueuePassDraftJobId { get; } = Guid.NewGuid();
        public Guid MarketResearchDraftJobId { get; } = Guid.NewGuid();
        public Guid CompletedTodayJobId { get; } = Guid.NewGuid();
        public Guid ExpiredTodayJobId { get; } = Guid.NewGuid();
        public Guid CancelledLastWeekJobId { get; } = Guid.NewGuid();
        public Guid OpenUnidentifiedId { get; } = Guid.NewGuid();
        public Guid UnidentifiedOriginId { get; } = Guid.NewGuid();
        public Guid UnidentifiedCreatorId { get; } = Guid.NewGuid();
        public Guid SubjectCaseId { get; } = Guid.NewGuid();

        public bool HasOpenUnidentified { get; init; } = true;

        public Task<int> CountOpenAsync(CancellationToken cancellationToken = default)
        {
            QueueCountCalls++;
            return Task.FromResult(HasOpenUnidentified ? 1 : 0);
        }

        public bool HasJobs { get; init; } = true;
        public int AdditionalRecentQueued { get; init; }
        public int AdditionalTerminalToday { get; init; }
        public bool RefuseCreate { get; init; }

        public CreateAiJobCommand? Created { get; private set; }
        public ConfirmAiJobCommand? Confirmed { get; private set; }
        public CancelAiJobCommand? Cancelled { get; private set; }
        public int QueueListCalls { get; private set; }
        public int QueueCountCalls { get; private set; }
        public int ReferenceLookupCalls { get; private set; }

        private IReadOnlyList<AiJobRecord> All => HasJobs
            ?
            [
            Job(
                EstimateDraftJobId,
                AiJobKind.Estimate,
                AiJobSubjectKind.Case,
                SubjectCaseId,
                CaseReference,
                EstimateInstruction,
                AiJobState.DraftReady,
                FixedUtcNow.AddHours(-3),
                version: 3),
            Job(
                QueuedResolutionJobId,
                AiJobKind.UnidentifiedResolution,
                AiJobSubjectKind.Unidentified,
                OpenUnidentifiedId,
                UnidentifiedReference,
                "Propose a destination for this Unidentified item.",
                AiJobState.Queued,
                FixedUtcNow.AddHours(-2),
                QueuedResolutionVersion),
            Job(
                QueuePassDraftJobId,
                AiJobKind.UnidentifiedQueuePass,
                AiJobSubjectKind.Queue,
                null,
                "unidentified-queue",
                "Examine the Unidentified queue.",
                AiJobState.DraftReady,
                FixedUtcNow.AddHours(-1),
                QueuePassVersion,
                createdByKind: ActorKind.Automation,
                createdBy: AutomationCreator),
            Job(
                MarketResearchDraftJobId,
                AiJobKind.MarketResearch,
                AiJobSubjectKind.Case,
                SubjectCaseId,
                CaseReference,
                MarketResearchInstruction,
                AiJobState.DraftReady,
                FixedUtcNow.AddMinutes(-45),
                MarketResearchVersion),
            Job(
                CompletedTodayJobId,
                AiJobKind.Estimate,
                AiJobSubjectKind.Case,
                SubjectCaseId,
                CaseReference,
                CompletedInstruction,
                AiJobState.Completed,
                FixedUtcNow.AddHours(-5),
                version: 6,
                closedAtUtc: FixedUtcNow.AddMinutes(-30)),
            Job(
                ExpiredTodayJobId,
                AiJobKind.UnidentifiedResolution,
                AiJobSubjectKind.Unidentified,
                OpenUnidentifiedId,
                UnidentifiedReference,
                ExpiredInstruction,
                AiJobState.Expired,
                FixedUtcNow.AddHours(-25),
                version: 1),
            Job(
                CancelledLastWeekJobId,
                AiJobKind.QueryResponse,
                AiJobSubjectKind.Case,
                SubjectCaseId,
                CaseReference,
                LastWeekInstruction,
                AiJobState.Cancelled,
                FixedUtcNow.AddDays(-7).AddHours(-1),
                version: 2,
                closedAtUtc: FixedUtcNow.AddDays(-7)),
            .. Enumerable.Range(1, AdditionalRecentQueued).Select(index => Job(
                Guid.NewGuid(), AiJobKind.Estimate, AiJobSubjectKind.Case,
                SubjectCaseId, CaseReference, $"Recent queued job {index}",
                AiJobState.Queued, FixedUtcNow.AddMinutes(-index), 1)),
            .. Enumerable.Range(1, AdditionalTerminalToday).Select(index => Job(
                Guid.NewGuid(), AiJobKind.Estimate, AiJobSubjectKind.Case,
                SubjectCaseId, CaseReference, $"Terminal result {index}",
                AiJobState.Completed, FixedUtcNow.AddDays(-2), 2,
                closedAtUtc: FixedUtcNow.AddMinutes(-1)))
            ]
            : [];

        public Task<IReadOnlyList<AiJobRecord>> ListOpenAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AiJobRecord>>(
                All.Where(job => job.ClosedAtUtc is null)
                    .OrderBy(job => job.CreatedAtUtc)
                    .ToArray());

        public Task<AiJobQueryPage> ListOpenPageAsync(AiJobKind? kind, string grantId, DateTimeOffset? afterCreatedAtUtc, Guid? afterJobId, int limit, CancellationToken cancellationToken) =>
            Task.FromResult(new AiJobQueryPage([], false));

        public Task<IReadOnlyList<AiJobRecord>> ListForSubjectAsync(
            Guid subjectId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AiJobRecord>>(
                All.Where(job => job.SubjectId == subjectId).ToArray());

        public Task<IReadOnlyList<AiJobRecord>> ListRecentAsync(
            int max,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AiJobRecord>>(
                All.OrderByDescending(job => job.CreatedAtUtc).Take(max).ToArray());

        public Task<IReadOnlyList<AiJobRecord>> ListTerminalInWindowAsync(
            DateTimeOffset startUtc,
            DateTimeOffset endUtc,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AiJobRecord>>(
                All.Where(job => AiJobStates.IsTerminal(job.State)
                    && (job.State == AiJobState.Expired ? job.ExpiresAtUtc : job.ClosedAtUtc) is { } terminal
                    && terminal >= startUtc && terminal < endUtc).ToArray());

        public Task<AiJobCounts> GetCountsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new AiJobCounts(
                All.Count(job => !AiJobStates.IsTerminal(job.State)),
                All.Count(job => job.State == AiJobState.Failed)));

        Task<AiJobRecord> ICreateAiJob.ExecuteAsync(
            CreateAiJobCommand command,
            CancellationToken cancellationToken)
        {
            if (RefuseCreate)
            {
                throw new InvalidOperationException("AI work is disabled by an Administrator.");
            }

            Created = command;
            return Task.FromResult(All[1]);
        }

        Task<AiJobRecord> IConfirmAiJob.ExecuteAsync(
            ConfirmAiJobCommand command,
            CancellationToken cancellationToken)
        {
            Confirmed = command;
            return Task.FromResult(All[2]);
        }

        Task<AiJobRecord> ICancelAiJob.ExecuteAsync(
            CancelAiJobCommand command,
            CancellationToken cancellationToken)
        {
            Cancelled = command;
            return Task.FromResult(All[1]);
        }

        public Task<IReadOnlyList<UnidentifiedQueueRow>> ListQueueAsync(
            UnidentifiedMediaKind? mediaKind,
            CancellationToken cancellationToken = default)
        {
            QueueListCalls++;
            return Task.FromResult<IReadOnlyList<UnidentifiedQueueRow>>(HasOpenUnidentified
                ?
                [
                    new(
                        OpenUnidentifiedId,
                        UnidentifiedReference,
                        UnidentifiedMediaKind.Email,
                        null,
                        "Instruction for an unknown vehicle",
                        "operations@example.invalid",
                        FixedUtcNow.AddHours(-4),
                        UnidentifiedReasonCode.NoUsableIdentification)
                ]
                : []);
        }

        // The rail lists the queue once per request; the Operations action
        // resolves one canonical reference. Every other port member throws so
        // an unexpected call fails the test that made it.
        public Task<UnidentifiedRegisterResult> RegisterAsync(
            RegisterUnidentifiedRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<UnidentifiedRegisterResult?> ProbeRegisterReplayAsync(
            RegisterUnidentifiedRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<UnidentifiedResolveResult> ResolveAsync(
            ResolveUnidentifiedRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<UnidentifiedResolveResult?> ProbeResolveReplayAsync(
            ResolveUnidentifiedRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<UnidentifiedItem?> GetAsync(
            Guid id,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<UnidentifiedItem?> GetByReferenceAsync(
            string reference,
            CancellationToken cancellationToken = default)
        {
            ReferenceLookupCalls++;
            return Task.FromResult(HasOpenUnidentified
                && string.Equals(reference, UnidentifiedReference, StringComparison.Ordinal)
                    ? new UnidentifiedItem(
                        OpenUnidentifiedId,
                        412,
                        UnidentifiedReference,
                        UnidentifiedOrigin.Receipt(UnidentifiedOriginId),
                        UnidentifiedReasonCode.NoUsableIdentification,
                        "The received instruction has no usable Case reference.",
                        UnidentifiedState.Open,
                        FixedUtcNow.AddHours(-4),
                        ResolvedAtUtc: null,
                        ActionActor.Staff(UnidentifiedCreatorId, [StaffRole.User]),
                        ResolvedBy: null,
                        ResolutionReason: null,
                        ResolutionTargetKind: null,
                        ResolutionTargetId: null,
                        ResolutionTargetReference: null,
                        Version: 1)
                    : null);
        }

        public Task<UnidentifiedItem?> GetByOriginAsync(
            UnidentifiedOrigin origin,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<UnidentifiedItem>> ListAsync(
            UnidentifiedState? state = UnidentifiedState.Open,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<UnidentifiedHistoryEntry>> HistoryAsync(
            Guid unidentifiedItemId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        private static AiJobRecord Job(
            Guid jobId,
            AiJobKind kind,
            AiJobSubjectKind subjectKind,
            Guid? subjectId,
            string subjectReference,
            string instruction,
            AiJobState state,
            DateTimeOffset createdAtUtc,
            long version,
            DateTimeOffset? closedAtUtc = null,
            ActorKind createdByKind = ActorKind.Staff,
            string? createdBy = null) => new(
                jobId,
                kind,
                subjectKind,
                subjectId,
                subjectReference,
                instruction,
                TargetPercentOfEngineerValue: null,
                EngineerValueAtSend: null,
                state,
                createdByKind,
                createdBy ?? StaffCreator,
                createdAtUtc,
                createdAtUtc.AddHours(24),
                TakenBy: null,
                TakenAtUtc: null,
                LeaseExpiresAtUtc: null,
                ProgressNote: null,
                ResultKind: null,
                ResultReference: null,
                ResultText: null,
                closedAtUtc,
                ClosureReason: null,
                version);
    }

    private sealed class RecordingEvaSubmissions : IEvaSubmissionQueries
    {
        public Guid FailureCaseId { get; } = Guid.Parse("3f6e5d14-fb09-4cda-9c04-b39b6c9d8dca");
        public string FailureCode { get; } = "eva_rejected";
        public DateTimeOffset FailureAtUtc { get; } = FixedUtcNow.AddMinutes(-10);
        public int ActivityCalls { get; private set; }
        public int RecentFailuresCalls { get; private set; }

        public Task<EvaSubmissionRecord?> GetLatestAsync(
            Guid caseId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EvaSubmissionRecord?>(null);

        public Task<IReadOnlyList<EvaSubmissionFailure>> GetRecentFailuresAsync(
            DateTimeOffset sinceUtc,
            int maximumResults,
            CancellationToken cancellationToken = default)
        {
            RecentFailuresCalls++;
            return Task.FromResult<IReadOnlyList<EvaSubmissionFailure>>(
            [
                new(FailureCaseId, EvaSubmissionOutcome.Rejected, FailureCode, FailureAtUtc)
            ]);
        }

        public Task<EvaSubmissionActivity> GetActivityAsync(
            CancellationToken cancellationToken = default)
        {
            ActivityCalls++;
            return Task.FromResult(new EvaSubmissionActivity(FixedUtcNow));
        }
    }

}
