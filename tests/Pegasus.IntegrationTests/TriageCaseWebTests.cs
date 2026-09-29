using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Cases;
using Pegasus.Core.Custody;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Core.Operations;
using Pegasus.Core.Triage;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;

using static Pegasus.IntegrationTests.CaseWebTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>
/// A Triage is a Case (decision R): it answers on <c>/Cases/{id}</c> and on
/// none of the Case workflow's sub-routes, takes the shared Case sequence, gets
/// standard Case custody (retried like any Case's) and is a staff link
/// destination in any state. Proved against the real database and the real
/// Web host.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed partial class TriageCaseWebTests
{
    private static readonly string[] CaseWorkflowSubRoutes =
    [
        "Tasks",
        "Vehicle",
        "Workflow",
        "Closure",
        "Documents/Export",
        "Eva/Send"
    ];

    [Theory]
    [InlineData("/Triage")]
    [InlineData("/Triage/78da3cb3-01fb-4d8c-801c-85f93855b44f")]
    public async Task TheRetiredTriagePagesAreNotFound(string path)
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AManualTriageCaseTakesTheCaseSequenceAndReplays()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var request = ManualTriageRequest("manual-triage-replay", "AB12CDE");
        var create = services.GetRequiredService<ICreateManualCase>();

        var created = await create.ExecuteAsync(request, CancellationToken.None);
        var replayed = await create.ExecuteAsync(request, CancellationToken.None);

        // The factory clock is in 2031: the first QDOS Case/PO of the year.
        Assert.Equal("t.QDOS31001", created.Reference);
        Assert.Equal(created, replayed);
        await Assert.ThrowsAsync<InvalidOperationException>(() => create.ExecuteAsync(
            request with { Data = new(VehicleRegistration: "XY12ZZZ") },
            CancellationToken.None));

        var detail = Assert.IsType<TriageDetail>(await services.GetRequiredService<ITriageQueries>()
            .GetAsync(created.CaseId, CancellationToken.None));
        Assert.Null(detail.Record.Origin);
        Assert.Equal("AB12CDE", detail.Record.NormalizedVehicleRegistration);
        Assert.Equal(TriageState.Open, detail.Record.State);
        Assert.Equal(created.Reference, detail.Record.Reference);
        Assert.Equal("QDOS", detail.PrincipalCode);

        await using (var context = await services.GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync())
        {
            // A Triage Case has no Case workflow, data snapshot or match index
            // entry, and exactly one standard custody work item.
            Assert.False(await context.CaseWorkflows.AnyAsync(item => item.CaseId == created.CaseId));
            Assert.False(await context.CaseMatchIndex.AnyAsync(item => item.CaseId == created.CaseId));
            Assert.Equal(1, await context.Cases.CountAsync());
            Assert.Equal(1, await context.Triage.CountAsync());
            Assert.Equal(1, await context.ExternalWorkItems.CountAsync(item =>
                item.CaseId == created.CaseId && item.Kind == ExternalWorkKinds.CreateCaseCustody));
        }

        using var response = await client.GetAsync($"/Cases/{created.CaseId:D}");
        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("t.QDOS31001", html, StringComparison.Ordinal);
        Assert.Contains("class=\"record triage-record\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"section-files\"", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(TriageState.Completed)]
    [InlineData(TriageState.Cancelled)]
    public async Task ATriageCaseInAClosedStateStillShowsItsRecordAndFiles(TriageState state)
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var triage = await CreateManualTriageAsync(factory.Services, $"closed-triage-{state}");
        await SetTriageStateAsync(factory.Services, triage.CaseId, state);

        using var response = await client.GetAsync($"/Cases/{triage.CaseId:D}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("class=\"record triage-record\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"section-files\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ATriageCaseIsNotFoundOnEveryCaseWorkflowSubRoute()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var triage = await CreateManualTriageAsync(factory.Services, "triage-sub-routes");
        var inspection = await CreateManualInspectionAsync(factory.Services, "inspection-sub-routes");

        foreach (var subRoute in CaseWorkflowSubRoutes)
        {
            using var response = await client.GetAsync($"/Cases/{triage.CaseId:D}/{subRoute}");
            Assert.True(
                response.StatusCode == HttpStatusCode.NotFound,
                $"/Cases/{{id}}/{subRoute} answered {(int)response.StatusCode} for a Triage Case.");
        }

        // The guard is by kind, not a blanket refusal: an instructed Case still
        // answers on its own sub-routes. The others are post-only, so
        // Documents/Export's GET (a redirect to the Case) is the control.
        using var caseExport = await client.GetAsync($"/Cases/{inspection.CaseId:D}/Documents/Export");
        Assert.NotEqual(HttpStatusCode.NotFound, caseExport.StatusCode);
    }

    [Fact]
    public async Task EachKindOfCaseRefusesTheOtherKindsHandlers()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var triage = await CreateManualTriageAsync(factory.Services, "wrong-kind-triage");
        var inspection = await CreateManualInspectionAsync(factory.Services, "wrong-kind-inspection");
        var antiforgeryToken = await IntakeWebDriver.GetAntiforgeryTokenAsync(client);

        using var caseHandlerOnTriage = await client.PostAsync(
            $"/Cases/{triage.CaseId:D}?handler=ClaimLease",
            ExpectedVersionForm(antiforgeryToken, 0));
        Assert.Equal(HttpStatusCode.NotFound, caseHandlerOnTriage.StatusCode);

        using var sectionOnTriage = await client.GetAsync($"/Cases/{triage.CaseId:D}/Section");
        Assert.Equal(HttpStatusCode.NotFound, sectionOnTriage.StatusCode);

        using var triageHandlerOnCase = await client.PostAsync(
            $"/Cases/{inspection.CaseId:D}?handler=TriageAction",
            ExpectedVersionForm(antiforgeryToken, 0));
        Assert.Equal(HttpStatusCode.NotFound, triageHandlerOnCase.StatusCode);

        using var triageHandlerOnNothing = await client.PostAsync(
            $"/Cases/{Guid.NewGuid():D}?handler=TriageAction",
            ExpectedVersionForm(antiforgeryToken, 0));
        Assert.Equal(HttpStatusCode.NotFound, triageHandlerOnNothing.StatusCode);
    }

    [Fact]
    public async Task AManualTriageCaseGetsStandardCaseCustodyWithoutAWorkflow()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var triage = await CreateManualTriageAsync(services, "manual-triage-custody");

        await services.GetRequiredService<IProcessQueuedCustody>()
            .ExecuteAsync(await CustodyWorkIdAsync(services, triage.CaseId), CancellationToken.None);

        var detail = Assert.IsType<TriageDetail>(await services.GetRequiredService<ITriageQueries>()
            .GetAsync(triage.CaseId, CancellationToken.None));
        Assert.Equal(CaseCustodyState.Confirmed, detail.CustodyState);
        // Custody changes no Triage state or version.
        Assert.Equal(TriageState.Open, detail.Record.State);
        Assert.Equal(0, detail.Record.Version);
        await using var context = await services.GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        Assert.False(await context.CaseWorkflows.AnyAsync(item => item.CaseId == triage.CaseId));
    }

    /// <summary>
    /// A Triage Case has no workflow row, yet a failure of its custody is still
    /// recorded on the Case, whether the adapter fails or the queue gives the
    /// work up.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AFailedTriageCaseCustodyReadsFailed(bool poisoned)
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var triage = await CreateManualTriageAsync(services, $"failed-triage-custody-{poisoned}");
        var workId = await CustodyWorkIdAsync(services, triage.CaseId);
        var workStore = services.GetRequiredService<IExternalWorkStore>();
        var clock = services.GetRequiredService<TimeProvider>();

        if (poisoned)
        {
            await workStore.MarkPoisonedAsync(workId, clock.GetUtcNow(), CancellationToken.None);
        }
        else
        {
            await Assert.ThrowsAsync<HttpRequestException>(() => new EfQueuedCustodyProcessor(
                services.GetRequiredService<IDbContextFactory<PegasusDbContext>>(),
                workStore,
                new FailingCaseCustody(),
                clock).ExecuteAsync(workId, CancellationToken.None));
        }

        var detail = Assert.IsType<TriageDetail>(await services.GetRequiredService<ITriageQueries>()
            .GetAsync(triage.CaseId, CancellationToken.None));
        Assert.Equal(CaseCustodyState.Failed, detail.CustodyState);
        Assert.Equal(TriageState.Open, detail.Record.State);
    }

    /// <summary>
    /// A Triage Case keeps standard Case custody, so its failed custody is
    /// retried on the Custody page like any Case's. The Triage version the page
    /// renders is posted and the retry claims the Triage hold for its one
    /// save; it re-arms the job, and the Triage page shows the outcome.
    /// </summary>
    [Fact]
    public async Task AFailedTriageCaseCustodyIsRetriedOnTheCustodyPage()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var triage = await CreateManualTriageAsync(factory.Services, "custody-page-triage");
        var workId = await PoisonCustodyAsync(factory.Services, triage.CaseId);

        var record = await GetHtmlAsync(client, $"/Cases/{triage.CaseId:D}");

        using var retried = await client.PostAsync(
            $"/Cases/{triage.CaseId:D}/Custody?handler=RetryCustody",
            Form(
                AntiforgeryValue(record),
                ("expectedVersion", InputValue(record, "expectedVersion")),
                ("operationKey", "custody-page-triage-retry"),
                ("reason", "Box is available again"),
                ("targetKind", nameof(CustodyTargetKind.CaseSource))));

        AssertPrg(retried, triage.CaseId, "?section=files");
        var files = await GetHtmlAsync(client, $"/Cases/{triage.CaseId:D}?section=files");
        Assert.Contains("Custody retry is pending.", files, StringComparison.Ordinal);

        await using var context = await factory.Services
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>().CreateDbContextAsync();
        Assert.Equal(
            ExternalWorkStatePersistence.Pending,
            await context.ExternalWorkItems
                .Where(item => item.Id == workId)
                .Select(item => item.State)
                .SingleAsync());
        // A staff write through the Triage authority: the Triage version moves
        // and the hold it claimed ends, as every Triage mutation's does.
        Assert.Equal(
            1,
            await context.Triage
                .Where(item => item.CaseId == triage.CaseId)
                .Select(item => item.Version)
                .SingleAsync());
        Assert.False(await context.CaseWorkflows.AnyAsync(item => item.CaseId == triage.CaseId));
        await AssertNoLiveScopeAsync(factory.Services, triage.CaseId);
    }

    [Fact]
    [Trait("Category", "QdosAlphaAcceptance")]
    public async Task AnIntakeTriageCaseRetainsItsSourceInStandardCaseCustody()
    {
        using var factory = new IntakeWebApplicationFactory();
        var email = IntakeTestEvidence.CreateEngineerTriageRequest("triage-custody.eml");
        await MailboxIntakeTestData.SubmitAndProcessAsync(factory.Services, email);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var queries = services.GetRequiredService<ITriageQueries>();
        var summary = Assert.Single(await queries.ListAsync(null, CancellationToken.None));

        await services.GetRequiredService<IProcessQueuedCustody>()
            .ExecuteAsync(await CustodyWorkIdAsync(services, summary.CaseId), CancellationToken.None);

        var detail = Assert.IsType<TriageDetail>(await queries.GetAsync(summary.CaseId, CancellationToken.None));
        Assert.Equal(CaseCustodyState.Confirmed, detail.CustodyState);
        Assert.NotEmpty(detail.Documents);
        Assert.Equal(TriageState.Open, detail.Record.State);
        Assert.Equal(summary.Version, detail.Record.Version);
    }

    [Fact]
    [Trait("Category", "QdosAlphaAcceptance")]
    public async Task ATriageRequestWhosePrincipalIsNotEstablishedWaitsInUnidentified()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ITriagePrincipalGate>();
                services.AddSingleton<ITriagePrincipalGate>(new NoEstablishedPrincipal());
            }));
        var email = IntakeTestEvidence.CreateEngineerTriageRequest("triage-without-principal.eml");

        var receiptId = await MailboxIntakeTestData.SubmitAndProcessAsync(factory.Services, email);

        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<ITriageQueries>()
            .ListAsync(null, CancellationToken.None));
        Assert.NotNull(await scope.ServiceProvider.GetRequiredService<IUnidentifiedStore>()
            .GetByOriginAsync(UnidentifiedOrigin.Receipt(receiptId), CancellationToken.None));
    }

    /// <summary>
    /// Staff "Link to case" reaches every Triage Case and every Case in any
    /// state (operator, 24 September 2026). A Triage Case answers through its
    /// Triage version and edit scope, and the link reopens nothing.
    /// </summary>
    [Fact]
    public async Task StaffLinkReachesACompletedTriageCaseAndACaseInAnyState()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var actor = ActionActor.Staff(DevelopmentOfflineIdentity.AdministratorId, [StaffRole.Administrator]);
        var link = services.GetRequiredService<ILinkIntake>();

        var triage = await CreateManualTriageAsync(services, "link-completed-triage");
        await SetTriageStateAsync(services, triage.CaseId, TriageState.Completed);
        var triageReceiptId = await TriageQueuesWebTests.StoreMinimalReceiptAsync(services, "link-to-triage.pdf");
        var triageScope = await services.GetRequiredService<IEditScopeLeases>().ClaimAsync(
            new(EditScopeKind.Triage, triage.CaseId, 0, actor, "link-completed-triage-edit"),
            CancellationToken.None);
        await link.ExecuteAsync(
            new LinkIntakeRequest(
                triageReceiptId,
                triage.CaseId,
                (await GetReceiptAsync(services, triageReceiptId)).Version,
                0,
                triageScope.Token,
                actor,
                "link-completed-triage",
                "Staff confirmed this material belongs with the Triage."),
            CancellationToken.None);

        Assert.Equal(triage.CaseId, (await GetReceiptAsync(services, triageReceiptId)).CurrentCaseId);
        var linkedTriage = Assert.IsType<TriageDetail>(await services.GetRequiredService<ITriageQueries>()
            .GetAsync(triage.CaseId, CancellationToken.None));
        Assert.Equal(TriageState.Completed, linkedTriage.Record.State);
        Assert.Equal(1, linkedTriage.Record.Version);

        var inspection = await CreateManualInspectionAsync(services, "link-cancelled-case");
        await using (var context = await services.GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync())
        {
            await context.CaseWorkflows.Where(item => item.CaseId == inspection.CaseId)
                .ExecuteUpdateAsync(update => update.SetProperty(
                    item => item.State, nameof(CaseLifecycleState.ProviderCancelled)));
        }
        var workflow = Assert.IsType<CaseWorkflowRecord>(await services.GetRequiredService<ICaseWorkflowQueries>()
            .GetAsync(inspection.CaseId, CancellationToken.None));
        var caseLease = await services.GetRequiredService<ILeaseCaseForEdit>().ClaimAsync(
            new ClaimCaseEditLeaseRequest(inspection.CaseId, workflow.Version, actor, "link-cancelled-case-lease"),
            CancellationToken.None);
        var caseReceiptId = await TriageQueuesWebTests.StoreMinimalReceiptAsync(services, "link-to-cancelled.pdf");
        await link.ExecuteAsync(
            new LinkIntakeRequest(
                caseReceiptId,
                inspection.CaseId,
                (await GetReceiptAsync(services, caseReceiptId)).Version,
                caseLease.Version,
                caseLease.Token,
                actor,
                "link-to-cancelled-case",
                "Staff confirmed this material belongs with the Case."),
            CancellationToken.None);

        Assert.Equal(inspection.CaseId, (await GetReceiptAsync(services, caseReceiptId)).CurrentCaseId);
        Assert.Equal(
            CaseLifecycleState.ProviderCancelled,
            Assert.IsType<CaseWorkflowRecord>(await services.GetRequiredService<ICaseWorkflowQueries>()
                .GetAsync(inspection.CaseId, CancellationToken.None)).State);
    }

    /// <summary>
    /// A Triage Case has no Edit step (plan 01): no Edit Triage, Take over or
    /// edit-session Cancel in any state, no heartbeat and no leaving beacon.
    /// The ribbon carries the Principal and the Case, and there is no Source
    /// panel.
    /// </summary>
    [Theory]
    [InlineData(TriageState.Open)]
    [InlineData(TriageState.AwaitingInformation)]
    [InlineData(TriageState.FindingRecorded)]
    [InlineData(TriageState.Completed)]
    [InlineData(TriageState.Cancelled)]
    public async Task TheTriagePageHasNoEditStepInAnyState(TriageState state)
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var triage = await CreateManualTriageAsync(factory.Services, $"no-edit-step-{state}");
        await SetTriageStateAsync(factory.Services, triage.CaseId, state);

        var html = await GetHtmlAsync(client, $"/Cases/{triage.CaseId:D}");

        Assert.DoesNotContain("Edit Triage", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Take over", html, StringComparison.Ordinal);
        Assert.DoesNotContain("handler=TriageEdit", html, StringComparison.Ordinal);
        Assert.DoesNotContain("handler=TriageCancelEdit", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-edit-heartbeat", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-edit-scope-release", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"editLeaseToken\"", html, StringComparison.Ordinal);

        var ribbon = TriageRibbon(html);
        Assert.Contains(
            $"<span class=\"ribbon-label\">{Pegasus.Web.Presentation.OperatorLabels.Principal}</span><span class=\"ribbon-value\">QDOS</span>",
            ribbon,
            StringComparison.Ordinal);
        Assert.Contains("<span class=\"ribbon-label\">Case</span>", ribbon, StringComparison.Ordinal);
        Assert.Contains(Pegasus.Web.Presentation.OperatorLabels.Triage.NoCase, ribbon, StringComparison.Ordinal);
        Assert.DoesNotContain("triage-source-title", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">Source</h2>", html, StringComparison.Ordinal);
        Assert.Contains($">{Pegasus.Web.Presentation.OperatorLabels.Triage.Determinations}</h2>", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Complete and Await information post once each, with no reason and no
    /// prior claim; Complete needs no response evidence. Each writes its fixed
    /// history text and shows its own notice.
    /// </summary>
    [Fact]
    public async Task CompleteAndAwaitInformationEachPostOnceWithNoReason()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var completing = await CreateManualTriageAsync(factory.Services, "one-post-complete");
        var awaiting = await CreateManualTriageAsync(factory.Services, "one-post-await");
        var antiforgery = AntiforgeryValue(await GetHtmlAsync(client, $"/Cases/{completing.CaseId:D}"));

        var recorded = await PostTriageActionAsync(
            client,
            completing.CaseId,
            antiforgery,
            0,
            "record_finding",
            ("reason", "Reviewed the request images."),
            ("roadworthiness", nameof(RoadworthinessFinding.Roadworthy)));
        Assert.Contains(Pegasus.Web.Presentation.OperatorLabels.Triage.FindingRecorded, recorded, StringComparison.Ordinal);

        var completed = await PostTriageActionAsync(client, completing.CaseId, antiforgery, 1, "complete");
        Assert.Contains(Pegasus.Web.Presentation.OperatorLabels.Triage.Completed, completed, StringComparison.Ordinal);
        // A Triage staff created directly came by no e-mail: no reply to offer.
        Assert.DoesNotContain("data-triage-reply-link", completed, StringComparison.Ordinal);
        var completedDetail = await GetTriageAsync(factory.Services, completing.CaseId);
        Assert.Equal(TriageState.Completed, completedDetail.Record.State);
        Assert.Empty(completedDetail.ResponseEvidence);
        Assert.Equal("triage_state_completed", completedDetail.History[^1].EventType);
        Assert.Equal(CompleteTriage.Reason, completedDetail.History[^1].Reason);

        var awaited = await PostTriageActionAsync(client, awaiting.CaseId, antiforgery, 0, "await_information");
        Assert.Contains(Pegasus.Web.Presentation.OperatorLabels.Triage.AwaitingInformation, awaited, StringComparison.Ordinal);
        var awaitingDetail = await GetTriageAsync(factory.Services, awaiting.CaseId);
        Assert.Equal(TriageState.AwaitingInformation, awaitingDetail.Record.State);
        Assert.Equal(AwaitTriageInformation.Reason, awaitingDetail.History[^1].Reason);

        await AssertNoLiveScopeAsync(factory.Services, completing.CaseId);
        await AssertNoLiveScopeAsync(factory.Services, awaiting.CaseId);
    }

    /// <summary>
    /// One Assign control with no prior claim: the roster lists the signed-in
    /// account first as "(you)" with nothing preselected, and Unassign posts
    /// from the same dialog with no reason.
    /// </summary>
    [Fact]
    public async Task AssignNeedsNoClaimAndListsTheSignedInAccountFirst()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var triage = await CreateManualTriageAsync(factory.Services, "assign-roster");
        // Sorts before the signed-in account by user name, so first place is
        // the page's ordering, not the roster's.
        var colleague = await CreateStaffAccountAsync(factory.Services, "aaron-engineer");

        var page = await GetHtmlAsync(client, $"/Cases/{triage.CaseId:D}");
        var ribbon = TriageRibbon(page);
        Assert.Contains("data-dialog-open=\"triage-assign-dialog\"", ribbon, StringComparison.Ordinal);
        Assert.Contains(">Assign</button>", ribbon, StringComparison.Ordinal);
        var options = AssigneeOptions(page);
        Assert.Equal(string.Empty, options[0].Value);
        Assert.Equal(DevelopmentOfflineIdentity.AdministratorId.ToString("D"), options[1].Value);
        Assert.Equal(
            Pegasus.Web.Presentation.OperatorLabels.Triage.You(DevelopmentOfflineIdentity.UserName),
            options[1].Text);
        Assert.Contains(options, option => option.Value == colleague.ToString("D") && option.Text == "aaron-engineer");
        Assert.DoesNotContain("selected", AssignDialog(page), StringComparison.Ordinal);
        // Nobody is assigned yet, so there is nothing to unassign.
        Assert.DoesNotContain("data-triage-unassign", page, StringComparison.Ordinal);

        var antiforgery = AntiforgeryValue(page);
        var assigned = await PostTriageActionAsync(
            client, triage.CaseId, antiforgery, 0, "assign", ("assigneeId", colleague.ToString("D")));
        Assert.Contains(
            Pegasus.Web.Presentation.OperatorLabels.Triage.AssignedTo("aaron-engineer"),
            assigned,
            StringComparison.Ordinal);
        Assert.Contains(">Reassign</button>", TriageRibbon(assigned), StringComparison.Ordinal);
        Assert.Contains("data-triage-unassign", assigned, StringComparison.Ordinal);
        Assert.Equal(colleague, (await GetTriageAsync(factory.Services, triage.CaseId)).Record.AssigneeId);

        var unassigned = await PostTriageActionAsync(client, triage.CaseId, antiforgery, 1, "unassign");
        Assert.Contains(Pegasus.Web.Presentation.OperatorLabels.Triage.Unassigned, unassigned, StringComparison.Ordinal);
        var detail = await GetTriageAsync(factory.Services, triage.CaseId);
        Assert.Null(detail.Record.AssigneeId);
        Assert.Equal("triage_unassigned", detail.History[^1].EventType);
        Assert.Equal(UnassignTriage.Reason, detail.History[^1].Reason);
        await AssertNoLiveScopeAsync(factory.Services, triage.CaseId);
    }

    /// <summary>
    /// Work Centre Assign to me on a Triage claims the Triage hold for its one
    /// save, so it assigns (F10).
    /// </summary>
    [Fact]
    public async Task WorkCentreAssignToMeAssignsATriage()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var triage = await CreateManualTriageAsync(factory.Services, "work-centre-assign-to-me");
        var antiforgery = await IntakeWebDriver.GetAntiforgeryTokenAsync(client);

        using var response = await client.PostAsync(
            "/?handler=AssignTriageToMe",
            Form(
                antiforgery,
                ("triageId", triage.CaseId.ToString("D")),
                ("operationKey", Guid.NewGuid().ToString("N")),
                ("returnUrl", "/")));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var detail = await GetTriageAsync(factory.Services, triage.CaseId);
        Assert.Equal(DevelopmentOfflineIdentity.AdministratorId, detail.Record.AssigneeId);
        Assert.Equal(1, detail.Record.Version);
        Assert.Equal("triage_assigned", detail.History[^1].EventType);
        await AssertNoLiveScopeAsync(factory.Services, triage.CaseId);
    }

    /// <summary>
    /// An Automation session holding the Triage record refuses a staff save
    /// with the "is editing" wording; the page still renders and nothing
    /// changes.
    /// </summary>
    [Fact]
    public async Task AnAutomationSessionHoldingTheRecordRefusesAStaffAssign()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var triage = await CreateManualTriageAsync(factory.Services, "automation-holds-triage");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IEditScopeLeases>().ClaimAsync(
                new(EditScopeKind.Triage, triage.CaseId, 0, ActionActor.Automation("triage-test-client"), "automation-holds-triage-edit"),
                CancellationToken.None);
        }

        var antiforgery = AntiforgeryValue(await GetHtmlAsync(client, $"/Cases/{triage.CaseId:D}"));
        var refused = await PostTriageActionAsync(
            client,
            triage.CaseId,
            antiforgery,
            0,
            "assign",
            ("assigneeId", DevelopmentOfflineIdentity.AdministratorId.ToString("D")));

        Assert.Contains("AI is editing this Triage record.", refused, StringComparison.Ordinal);
        Assert.Contains("class=\"record triage-record\"", refused, StringComparison.Ordinal);
        var detail = await GetTriageAsync(factory.Services, triage.CaseId);
        Assert.Null(detail.Record.AssigneeId);
        Assert.Equal(0, detail.Record.Version);
    }

    /// <summary>
    /// A repeated post — a double click, or a reload that re-posts — is a
    /// replay of its operation key, not a refusal: the second post gets the
    /// completed record and its notice, and nothing is written twice. A
    /// genuinely stale post is still refused as changed.
    /// </summary>
    [Fact]
    public async Task ARepeatedCompleteIsReplayedNotRefused()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var triage = await CreateManualTriageAsync(factory.Services, "repeat-complete");
        var antiforgery = AntiforgeryValue(await GetHtmlAsync(client, $"/Cases/{triage.CaseId:D}"));
        _ = await PostTriageActionAsync(
            client,
            triage.CaseId,
            antiforgery,
            0,
            "record_finding",
            ("reason", "Reviewed the request images."),
            ("roadworthiness", nameof(RoadworthinessFinding.Roadworthy)));
        var operationKey = Guid.NewGuid().ToString("N");

        var first = await PostTriageActionAsync(client, triage.CaseId, antiforgery, 1, "complete", operationKey);
        var second = await PostTriageActionAsync(client, triage.CaseId, antiforgery, 1, "complete", operationKey);

        Assert.Contains(Pegasus.Web.Presentation.OperatorLabels.Triage.Completed, first, StringComparison.Ordinal);
        Assert.Contains(Pegasus.Web.Presentation.OperatorLabels.Triage.Completed, second, StringComparison.Ordinal);
        Assert.DoesNotContain(Pegasus.Web.Presentation.OperatorLabels.Triage.Changed, second, StringComparison.Ordinal);
        var detail = await GetTriageAsync(factory.Services, triage.CaseId);
        Assert.Equal(TriageState.Completed, detail.Record.State);
        Assert.Equal(2, detail.Record.Version);
        Assert.Single(detail.History, entry => entry.EventType == "triage_state_completed");
        await AssertNoLiveScopeAsync(factory.Services, triage.CaseId);

        // A different post at the old version is not a repeat: the store's
        // version check refuses it as changed, and nothing is written.
        var stale = await PostTriageActionAsync(client, triage.CaseId, antiforgery, 1, "reopen", ("reason", "Stale reopen"));
        Assert.Contains(Pegasus.Web.Presentation.OperatorLabels.Triage.Changed, stale, StringComparison.Ordinal);
        Assert.Equal(2, (await GetTriageAsync(factory.Services, triage.CaseId)).Record.Version);
        await AssertNoLiveScopeAsync(factory.Services, triage.CaseId);

        // A colleague's Complete from a page opened before the first one
        // landed: Core would say the state does not permit it, a sentence
        // about a state this operator never saw. They are told it changed.
        var late = await PostTriageActionAsync(
            client, triage.CaseId, antiforgery, 1, "complete", Guid.NewGuid().ToString("N"));
        Assert.Contains(Pegasus.Web.Presentation.OperatorLabels.Triage.Changed, late, StringComparison.Ordinal);
        Assert.DoesNotContain("only after a finding is recorded", late, StringComparison.Ordinal);
        Assert.Equal(2, (await GetTriageAsync(factory.Services, triage.CaseId)).Record.Version);
        await AssertNoLiveScopeAsync(factory.Services, triage.CaseId);
    }

    /// <summary>
    /// A custody retry the store refuses — here, custody work that has not
    /// failed — answers with a result rather than an exception. The hold the
    /// retry claimed is released all the same, so the record is free at once.
    /// </summary>
    [Fact]
    public async Task ARefusedTriageCustodyRetryLeavesNoHold()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var triage = await CreateManualTriageAsync(factory.Services, "custody-refused-triage");
        var record = await GetHtmlAsync(client, $"/Cases/{triage.CaseId:D}");

        using var retried = await client.PostAsync(
            $"/Cases/{triage.CaseId:D}/Custody?handler=RetryCustody",
            Form(
                AntiforgeryValue(record),
                ("expectedVersion", InputValue(record, "expectedVersion")),
                ("operationKey", "custody-refused-triage-retry"),
                ("reason", "Retry pressed on custody that has not failed"),
                ("targetKind", nameof(CustodyTargetKind.CaseSource))));

        AssertPrg(retried, triage.CaseId, "?section=files");
        var files = await GetHtmlAsync(client, $"/Cases/{triage.CaseId:D}?section=files");
        Assert.Contains("Only failed custody work can be retried.", files, StringComparison.Ordinal);
        Assert.Equal(0, (await GetTriageAsync(factory.Services, triage.CaseId)).Record.Version);
        await AssertNoLiveScopeAsync(factory.Services, triage.CaseId);
    }

    /// <summary>
    /// Work Centre Assign to me on a Triage an Automation session holds names
    /// the holder, as the Triage page does, instead of the catch-all refusal.
    /// </summary>
    [Fact]
    public async Task WorkCentreAssignToMeNamesWhoHoldsTheTriage()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var triage = await CreateManualTriageAsync(factory.Services, "work-centre-automation-holds-triage");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IEditScopeLeases>().ClaimAsync(
                new(EditScopeKind.Triage, triage.CaseId, 0, ActionActor.Automation("triage-test-client"), "work-centre-automation-holds-triage-edit"),
                CancellationToken.None);
        }
        var antiforgery = await IntakeWebDriver.GetAntiforgeryTokenAsync(client);

        using var response = await client.PostAsync(
            "/?handler=AssignTriageToMe",
            Form(
                antiforgery,
                ("triageId", triage.CaseId.ToString("D")),
                ("operationKey", Guid.NewGuid().ToString("N")),
                ("returnUrl", "/")));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var workCentre = await GetHtmlAsync(client, "/");
        Assert.Contains("AI is editing this Triage record.", workCentre, StringComparison.Ordinal);
        Assert.DoesNotContain(Pegasus.Web.Presentation.OperatorLabels.WorkCentre.TriageAssignRefused, workCentre, StringComparison.Ordinal);
        var detail = await GetTriageAsync(factory.Services, triage.CaseId);
        Assert.Null(detail.Record.AssigneeId);
        Assert.Equal(0, detail.Record.Version);
    }

    /// <summary>
    /// On a Completed Triage the determinations are greyed boxes with the
    /// recorded values, and Record correction opens the same form in a
    /// dialog; the correction supersedes the finding and returns the Triage
    /// to Finding recorded.
    /// </summary>
    [Fact]
    public async Task ACompletedTriageShowsGreyedDeterminationsAndOffersRecordCorrection()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var triage = await CreateManualTriageAsync(factory.Services, "completed-correction");
        var antiforgery = AntiforgeryValue(await GetHtmlAsync(client, $"/Cases/{triage.CaseId:D}"));
        _ = await PostTriageActionAsync(
            client,
            triage.CaseId,
            antiforgery,
            0,
            "record_finding",
            ("reason", "Reviewed the request images."),
            ("roadworthiness", nameof(RoadworthinessFinding.Roadworthy)),
            ("assessment", nameof(AssessmentFinding.Repairable)));
        var finding = Assert.Single((await GetTriageAsync(factory.Services, triage.CaseId)).Findings);
        var completed = await PostTriageActionAsync(client, triage.CaseId, antiforgery, 1, "complete");

        var panel = DeterminationsPanel(completed);
        Assert.DoesNotContain("data-triage-determinations", panel, StringComparison.Ordinal);
        Assert.Contains("<div class=\"fc ro\">", panel, StringComparison.Ordinal);
        Assert.Contains(">Roadworthy</div>", panel, StringComparison.Ordinal);
        Assert.Contains(">Repairable</div>", panel, StringComparison.Ordinal);
        Assert.Contains("data-dialog-open=\"triage-correction-dialog\"", panel, StringComparison.Ordinal);
        var dialog = CorrectionDialog(completed);
        Assert.Contains("data-triage-determinations", dialog, StringComparison.Ordinal);
        Assert.Contains("value=\"supersede_finding\"", dialog, StringComparison.Ordinal);
        Assert.Contains($"name=\"supersedesFindingId\" value=\"{finding.Id:D}\"", dialog, StringComparison.Ordinal);
        Assert.Contains("value=\"Roadworthy\" selected=\"selected\"", dialog, StringComparison.Ordinal);
        Assert.Contains("value=\"Repairable\" selected=\"selected\"", dialog, StringComparison.Ordinal);

        var corrected = await PostTriageActionAsync(
            client,
            triage.CaseId,
            antiforgery,
            2,
            "supersede_finding",
            ("reason", "Further images show the damage."),
            ("roadworthiness", nameof(RoadworthinessFinding.Unroadworthy)),
            ("assessment", nameof(AssessmentFinding.TotalLoss)),
            ("supersedesFindingId", finding.Id.ToString("D")));
        Assert.Contains(Pegasus.Web.Presentation.OperatorLabels.Triage.FindingRecorded, corrected, StringComparison.Ordinal);
        var detail = await GetTriageAsync(factory.Services, triage.CaseId);
        Assert.Equal(TriageState.FindingRecorded, detail.Record.State);
        Assert.Equal(finding.Id, detail.Findings.Single(item => item.SupersedesFindingId is not null).SupersedesFindingId);
        await AssertNoLiveScopeAsync(factory.Services, triage.CaseId);
    }

    private static CreateManualCaseRequest ManualTriageRequest(string operationKey, string registration) => new(
        ActionActor.Staff(DevelopmentOfflineIdentity.AdministratorId, [StaffRole.Administrator]),
        operationKey,
        "QDOS",
        CaseType.Triage,
        new(VehicleRegistration: registration));

    private static async Task<CaseIdentity> CreateManualTriageAsync(IServiceProvider services, string operationKey)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ICreateManualCase>().ExecuteAsync(
            ManualTriageRequest(operationKey, "AB12CDE"),
            CancellationToken.None);
    }

    private static async Task<CaseIdentity> CreateManualInspectionAsync(IServiceProvider services, string operationKey)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ICreateManualCase>().ExecuteAsync(
            new(
                ActionActor.Staff(DevelopmentOfflineIdentity.AdministratorId, [StaffRole.Administrator]),
                operationKey,
                "QDOS",
                CaseType.Inspection,
                new(ClaimantName: "Jane Doe", ClaimNumber: "C-1", VehicleRegistration: "XY12ZZZ")),
            CancellationToken.None);
    }

    /// <summary>
    /// Completion needs a recorded finding; the state is what the page and
    /// the link rule read, so the fixture sets it directly.
    /// </summary>
    private static async Task SetTriageStateAsync(IServiceProvider services, Guid caseId, TriageState state)
    {
        await using var scope = services.CreateAsyncScope();
        await using var context = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>().CreateDbContextAsync();
        var code = EfTriageStore.ToCode(state);
        await context.Triage.Where(item => item.CaseId == caseId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.State, code));
    }

    private static async Task<Guid> CustodyWorkIdAsync(IServiceProvider services, Guid caseId)
    {
        await using var scope = services.CreateAsyncScope();
        await using var context = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>().CreateDbContextAsync();
        return await context.ExternalWorkItems
            .Where(item => item.CaseId == caseId && item.Kind == ExternalWorkKinds.CreateCaseCustody)
            .Select(item => item.Id)
            .SingleAsync();
    }

    /// <summary>The queue gives the Case's custody job up: the job and the Case's custody read failed.</summary>
    private static async Task<Guid> PoisonCustodyAsync(IServiceProvider services, Guid caseId)
    {
        var workId = await CustodyWorkIdAsync(services, caseId);
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IExternalWorkStore>().MarkPoisonedAsync(
            workId,
            scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow(),
            CancellationToken.None);
        return workId;
    }

    /// <summary>The Triage Case page's Files section, from its head to its end.</summary>
    private static string FilesSection(string html)
    {
        var start = html.IndexOf("id=\"section-files\"", StringComparison.Ordinal);
        Assert.True(start >= 0, "The Files section is not rendered.");
        var end = html.IndexOf("</section>", start, StringComparison.Ordinal);
        Assert.True(end > start, "The Files section is not closed.");
        return html[start..end];
    }

    private static async Task<IntakeReceipt> GetReceiptAsync(IServiceProvider services, Guid receiptId)
    {
        await using var scope = services.CreateAsyncScope();
        return Assert.IsType<IntakeReceipt>(await scope.ServiceProvider
            .GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(receiptId, CancellationToken.None));
    }

    /// <summary>One Triage page action, posted once as the page posts it.</summary>
    private static Task<string> PostTriageActionAsync(
        HttpClient client,
        Guid triageCaseId,
        string antiforgery,
        long expectedVersion,
        string actionName,
        params (string Name, string Value)[] fields) =>
        PostTriageActionAsync(client, triageCaseId, antiforgery, expectedVersion, actionName, Guid.NewGuid().ToString("N"), fields);

    /// <summary>The same action under a chosen operation key, so a post can be repeated.</summary>
    private static async Task<string> PostTriageActionAsync(
        HttpClient client,
        Guid triageCaseId,
        string antiforgery,
        long expectedVersion,
        string actionName,
        string operationKey,
        params (string Name, string Value)[] fields)
    {
        using var response = await client.PostAsync(
            $"/Cases/{triageCaseId:D}?handler=TriageAction",
            Form(
                antiforgery,
                [
                    ("expectedVersion", expectedVersion.ToString(CultureInfo.InvariantCulture)),
                    ("operationKey", operationKey),
                    ("actionName", actionName),
                    .. fields
                ]));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static async Task<TriageDetail> GetTriageAsync(IServiceProvider services, Guid caseId)
    {
        await using var scope = services.CreateAsyncScope();
        return Assert.IsType<TriageDetail>(await scope.ServiceProvider.GetRequiredService<ITriageQueries>()
            .GetAsync(caseId, CancellationToken.None));
    }

    /// <summary>Every save ends its own hold, so nothing is left holding the record.</summary>
    private static async Task AssertNoLiveScopeAsync(IServiceProvider services, Guid caseId)
    {
        await using var scope = services.CreateAsyncScope();
        Assert.Null(await scope.ServiceProvider.GetRequiredService<IEditScopeLeases>().GetActiveAsync(
            EditScopeKind.Triage,
            caseId,
            ActionActor.Staff(DevelopmentOfflineIdentity.AdministratorId, [StaffRole.Administrator]),
            CancellationToken.None));
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

    /// <summary>The Triage ribbon, from its facts to its state chip.</summary>
    private static string TriageRibbon(string html)
    {
        var start = html.IndexOf("class=\"ribbon triage-ribbon\"", StringComparison.Ordinal);
        Assert.True(start >= 0, "The Triage ribbon is not rendered.");
        var end = html.IndexOf("class=\"ribbon-chips\"", start, StringComparison.Ordinal);
        Assert.True(end > start, "The Triage ribbon has no state chip.");
        return html[start..end];
    }

    /// <summary>The Determinations panel, from its head to its end.</summary>
    private static string DeterminationsPanel(string html)
    {
        var start = html.IndexOf("id=\"triage-determinations-title\"", StringComparison.Ordinal);
        Assert.True(start >= 0, "The Determinations panel is not rendered.");
        var end = html.IndexOf("</section>", start, StringComparison.Ordinal);
        Assert.True(end > start, "The Determinations panel is not closed.");
        return html[start..end];
    }

    private static string CorrectionDialog(string html)
    {
        var start = html.IndexOf("data-dialog=\"triage-correction-dialog\"", StringComparison.Ordinal);
        Assert.True(start >= 0, "The Record correction dialog is not rendered.");
        var end = html.IndexOf("</section>", start, StringComparison.Ordinal);
        return html[start..end];
    }

    private static string AssignDialog(string html)
    {
        var start = html.IndexOf("data-dialog=\"triage-assign-dialog\"", StringComparison.Ordinal);
        Assert.True(start >= 0, "The Assign dialog is not rendered.");
        var end = html.IndexOf("</section>", start, StringComparison.Ordinal);
        return html[start..end];
    }

    private static (string Value, string Text)[] AssigneeOptions(string html) =>
    [
        .. AssigneeOptionRegex().Matches(AssignDialog(html))
            .Select(match => (
                match.Groups["value"].Value,
                System.Net.WebUtility.HtmlDecode(match.Groups["text"].Value).Trim()))
    ];

    [System.Text.RegularExpressions.GeneratedRegex(
        "<option value=\"(?<value>[^\"]*)\"[^>]*>(?<text>[^<]*)</option>",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex AssigneeOptionRegex();

    private static FormUrlEncodedContent ExpectedVersionForm(string antiforgeryToken, long expectedVersion) => new(
    [
        KeyValuePair.Create("__RequestVerificationToken", antiforgeryToken),
        KeyValuePair.Create("expectedVersion", expectedVersion.ToString(CultureInfo.InvariantCulture))
    ]);

    private sealed class NoEstablishedPrincipal : ITriagePrincipalGate
    {
        public Task<Guid?> GetEstablishedPrincipalIdAsync(Guid receiptId, CancellationToken cancellationToken) =>
            Task.FromResult<Guid?>(null);
    }

    private sealed class FailingCaseCustody : ICaseCustody
    {
        private static HttpRequestException Failure() => new("Fixture adapter failure.");

        public Task<CaseCustodyRoot> CreateCaseRootAsync(
            Guid caseId, string caseReference, string creationOwnerToken, string operationKey,
            CancellationToken cancellationToken) => throw Failure();

        public Task<CaseCustodyRoot> GetExistingCaseRootAsync(
            Guid caseId,
            string caseReference,
            CancellationToken cancellationToken) => throw Failure();

        public Task<CustodyDocumentVersion> RetainAcceptedIntakeSourceAsync(
            CaseCustodyRoot root, IntakeSourceCustodyReference source, string operationKey,
            CancellationToken cancellationToken) => throw Failure();

        public Task<string> CreateAuditReferenceFolderAsync(
            CaseCustodyRoot root, string auditReference, string creationOwnerToken, string operationKey,
            CancellationToken cancellationToken) => throw Failure();
    }
}
