using System.Globalization;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Cases;
using Pegasus.Core.Custody;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Core.Triage;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;

namespace Pegasus.IntegrationTests;

/// <summary>
/// A Triage is a Case (decision R): it answers on <c>/Cases/{id}</c> and on
/// none of the Case workflow's sub-routes, takes the shared Case sequence, gets
/// standard Case custody and is a staff link destination in any state. Proved
/// against the real database and the real Web host.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class TriageCaseWebTests
{
    private static readonly string[] CaseWorkflowSubRoutes =
    [
        "Custody",
        "Tasks",
        "Vehicle",
        "Workflow",
        "Closure",
        "Assessment",
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
        // answers on its own sub-routes.
        using var caseTasks = await client.GetAsync($"/Cases/{inspection.CaseId:D}/Tasks");
        Assert.NotEqual(HttpStatusCode.NotFound, caseTasks.StatusCode);
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
            $"/Cases/{inspection.CaseId:D}?handler=TriageEdit",
            ExpectedVersionForm(antiforgeryToken, 0));
        Assert.Equal(HttpStatusCode.NotFound, triageHandlerOnCase.StatusCode);

        using var triageHandlerOnNothing = await client.PostAsync(
            $"/Cases/{Guid.NewGuid():D}?handler=TriageEdit",
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
                "link-cancelled-case",
                "Staff confirmed this material belongs with the Case."),
            CancellationToken.None);

        Assert.Equal(inspection.CaseId, (await GetReceiptAsync(services, caseReceiptId)).CurrentCaseId);
        Assert.Equal(
            CaseLifecycleState.ProviderCancelled,
            Assert.IsType<CaseWorkflowRecord>(await services.GetRequiredService<ICaseWorkflowQueries>()
                .GetAsync(inspection.CaseId, CancellationToken.None)).State);
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
    /// Completion needs a finding and a replied Sent email; the state is what
    /// the page and the link rule read, so the fixture sets it directly.
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

    private static async Task<IntakeReceipt> GetReceiptAsync(IServiceProvider services, Guid receiptId)
    {
        await using var scope = services.CreateAsyncScope();
        return Assert.IsType<IntakeReceipt>(await scope.ServiceProvider
            .GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(receiptId, CancellationToken.None));
    }

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
