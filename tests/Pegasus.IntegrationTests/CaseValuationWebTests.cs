using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Workflow;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The Valuation section's commands on the one Case workspace (B01 port of
/// PR 670's standalone Valuation page, re-homed as section handlers).
/// </summary>
public sealed partial class CaseDetailsWebTests
{
    /// <summary>
    /// The submitted case version reaches the valuation command unchanged:
    /// the store enforces it against the live case, and the page never
    /// rewrites it with a fresher version it read for itself.
    /// </summary>
    [Fact]
    public async Task SaveValuationForwardsTheSubmittedVersionAndDetailsUnchanged()
    {
        var store = new RecordingCaseDetailsStore();
        var valuations = new RecordingValuationSaver();
        using var workspace = await EnterEngineerEditModeAsync(
            store,
            services => Substitute<ISaveValuation>(services, valuations));
        const string operationKey = "0f0e0d0c0b0a09080706050403020100";

        using var response = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=SaveValuation",
            workspace.MutationForm(
                operationKey,
                "ignored: the handler names its own reason",
                ("source", nameof(ValuationSource.Glasses)),
                ("guideMonth", "2031-05"),
                ("mileage", "42000"),
                ("retailValue", "12500.00"),
                ("tradeValue", "10250.00")));

        AssertValuationPrg(response, store.CaseId);
        var save = Assert.Single(valuations.Saves);
        AssertLeasedMutation(workspace, save, operationKey, "Valuation recorded.");
        Assert.Equal(store.CaseVersion, save.ExpectedVersion);
        // v25 decision F: the valuation is an immediate post inside the
        // session, so the record re-acquires the lease the store's mutation
        // cleared and the editor stays editing.
        Assert.Equal(2, store.Claims.Count);
        Assert.All(store.Claims, claim => Assert.Equal(store.Claims[0].Actor.SubjectId, claim.Actor.SubjectId));
        Assert.Equal(store.LeaseToken, InputValue(await workspace.GetWorkspaceAsync(), "editLeaseToken"));
        // The card records the moment it was saved; the boxes carry the rest.
        Assert.Equal(ValuationSource.Glasses, save.Details.Source);
        Assert.Equal(42_000, save.Details.Mileage);
        Assert.Equal(12_500m, save.Details.RetailValue);
        Assert.Equal(10_250m, save.Details.TradeValue);
        Assert.Equal(new DateOnly(2031, 5, 1), save.Details.GuideMonth);
        var today = Pegasus.Core.LondonCalendar.DateAt(DateTimeOffset.UtcNow);
        Assert.InRange(save.Details.Date, today.AddDays(-1), today.AddDays(1));
    }

    /// <summary>
    /// A stale POST — a form rendered against an older case version — is
    /// forwarded with exactly the version it submitted, so the store's
    /// concurrency check sees the editor's real premise; it is refused once
    /// and never retried with the live version. The refusal is reported on
    /// the section the editor came from.
    /// </summary>
    [Fact]
    public async Task AStaleValuationPostForwardsTheStaleVersionAndReportsTheRefusal()
    {
        var store = new RecordingCaseDetailsStore();
        var valuations = new RecordingValuationSaver();
        using var workspace = await EnterEngineerEditModeAsync(
            store,
            services => Substitute<ISaveValuation>(services, valuations));
        var staleVersion = store.CaseVersion - 1;
        valuations.NextFailure = new CaseVersionConflictException(store.CaseId, staleVersion, store.CaseVersion);

        using var response = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=SaveValuation",
            Form(
                workspace.AntiforgeryToken,
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", staleVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", "1f1e1d1c1b1a19181716151413121110"),
                ("editLeaseToken", store.LeaseToken),
                ("source", nameof(ValuationSource.Glasses)),
                ("mileage", "42000"),
                ("retailValue", "12500.00"),
                ("tradeValue", "10250.00")));

        AssertValuationPrg(response, store.CaseId);
        var save = Assert.Single(valuations.Saves);
        Assert.Equal(staleVersion, save.ExpectedVersion);
        Assert.NotEqual(store.CaseVersion, save.ExpectedVersion);
        var html = await GetHtmlAsync(workspace.Client, $"/Cases/{store.CaseId:D}?section=valuation");
        Assert.Contains("role=\"alert\"", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// The valuation redirect lands on the Valuation section, not the record's
    /// top, so the editor reads the outcome where they acted.
    /// </summary>
    private static void AssertValuationPrg(HttpResponseMessage response, Guid caseId)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(
            $"/Cases/{caseId:D}?section=valuation",
            response.Headers.Location?.OriginalString);
    }

    /// <summary>
    /// The shared harness enters edit mode as the offline Administrator; a
    /// valuation is an Engineer's act, so this workspace authenticates the
    /// same staff member with the Engineer role and claims the lease the same
    /// way.
    /// </summary>
    private static async Task<LeasedWorkspace> EnterEngineerEditModeAsync(
        RecordingCaseDetailsStore store,
        Action<IServiceCollection> substitutePorts)
    {
        var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                Substitute<IGetCase>(services, store);
                Substitute<IAcquireCaseEditLease>(services, store);
                Substitute<IGetAssessmentAccess>(services, new FakeGetAssessmentAccess(canOpen: true));
                substitutePorts(services);
            }));
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Engineer");
        var initial = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        using var claim = await client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=ClaimLease",
            Form(
                AntiforgeryValue(initial),
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", InputValue(initial, "operationKey"))));
        AssertPrg(claim, store.CaseId);
        var leased = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        Assert.Equal(store.LeaseToken, InputValue(leased, "editLeaseToken"));
        return new(baseFactory, factory, client, store, AntiforgeryValue(leased));
    }

    private sealed class RecordingValuationSaver : ISaveValuation
    {
        public List<SaveValuationRequest> Saves { get; } = [];

        /// <summary>Armed by a test so the next save is refused once, after it is recorded.</summary>
        public Exception? NextFailure { get; set; }

        public Task<CaseValuation> ExecuteAsync(
            SaveValuationRequest request,
            CancellationToken cancellationToken)
        {
            Saves.Add(request);
            if (NextFailure is { } failure)
            {
                NextFailure = null;
                throw failure;
            }

            return Task.FromResult(new CaseValuation(
                Guid.NewGuid(),
                request.CaseId,
                request.Details,
                request.Actor.SubjectId,
                new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero)));
        }
    }
}
