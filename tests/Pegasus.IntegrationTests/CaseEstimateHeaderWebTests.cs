using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Web.Presentation;

using static Pegasus.IntegrationTests.CaseWebTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The Estimate header offers Send to AI only once the Engineer's Value is
/// confirmed; before that the control is absent, not drawn locked with an
/// explanation (design authority, no explanatory copy; 15 September walk).
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class CaseEstimateHeaderWebTests
{
    /// <summary>
    /// One Production host for both preview routes: a host costs ~95 s to
    /// build, and a theory row per path also listed identically once the
    /// shard partition truncated the long path argument.
    /// </summary>
    [Fact]
    public async Task CaseEstimateHeaderDocumentPreviewsUseTheDeployedBlobFramePolicy()
    {
        using var factory = new ConfiguredWebApplicationFactory(
            "Production",
            new Dictionary<string, string?>());
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });

        foreach (var previewPath in new[]
                 {
                     "/Cases/00000000-0000-0000-0000-000000000001?handler=PreviewReportDraft&section=report",
                     "/Cases/00000000-0000-0000-0000-000000000001?handler=EstimateDocument&estimateId=00000000-0000-0000-0000-000000000002"
                 })
        {
            using var response = await client.GetAsync(previewPath);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal(
                "default-src 'self'; object-src 'none'; base-uri 'self'; " +
                "frame-src 'self' blob:; frame-ancestors 'self'",
                Assert.Single(response.Headers.GetValues("Content-Security-Policy")));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CaseEstimateHeaderOffersPdfForTheSavedSelectedEstimateInReadAndEditMode(bool editing)
    {
        var store = new RecordingCaseDetailsStore();
        var estimates = new SingleEstimateList(store.CaseId);
        if (editing)
        {
            using var workspace = await EnterEngineerEditModeAsync(store, services =>
                Substitute<IListCaseEstimates>(services, estimates));
            var html = await GetHtmlAsync(
                workspace.Client,
                $"/Cases/{store.CaseId:D}?section=estimate&estimate={estimates.Estimate.SpecificationId:D}");
            Assert.Contains("data-document-preview", html, StringComparison.Ordinal);
            Assert.Contains(">Estimate PDF<", html, StringComparison.Ordinal);
            return;
        }

        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            Substitute<IGetCase>(services, store);
            SubstituteDetailsPageReaders(services, store);
            // A lifecycle state outside the former Assessment CanOpen gate
            // remains a readable Case and still offers this read action.
            Substitute<IGetAssessmentAccess>(services, new FakeGetAssessmentAccess(canOpen: false));
            Substitute<IListCaseEstimates>(services, estimates);
        }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Engineer");

        var readHtml = await GetHtmlAsync(
            client,
            $"/Cases/{store.CaseId:D}?section=estimate&estimate={estimates.Estimate.SpecificationId:D}");

        Assert.Contains("data-document-preview", readHtml, StringComparison.Ordinal);
        Assert.Contains(">Estimate PDF<", readHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CaseEstimateHeaderOmitsPdfForAnEmptyDraft()
    {
        var store = new RecordingCaseDetailsStore();
        var estimates = new SingleEstimateList(store.CaseId, includeLine: false);
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            Substitute<IGetCase>(services, store);
            SubstituteDetailsPageReaders(services, store);
            Substitute<IGetAssessmentAccess>(services, new FakeGetAssessmentAccess(canOpen: true));
            Substitute<IListCaseEstimates>(services, estimates);
        }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Engineer");

        var html = await GetHtmlAsync(
            client,
            $"/Cases/{store.CaseId:D}?section=estimate&estimate={estimates.Estimate.SpecificationId:D}");

        Assert.DoesNotContain(">Estimate PDF<", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithoutAConfirmedEngineersValueTheEstimateHeaderShowsNeitherSendToAiNorALockedPill()
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await EnterEngineerEditModeAsync(store, _ => { });

        var html = await GetHtmlAsync(workspace.Client, $"/Cases/{store.CaseId:D}?section=estimate");

        Assert.Contains("data-case-editing=\"true\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-estimate-send-to-ai", html, StringComparison.Ordinal);
        Assert.DoesNotContain(
            OperatorLabels.CaseWorkspace.EngineerSections.ConfirmedEngineerValueRequired,
            html,
            StringComparison.Ordinal);
    }

    private sealed class SingleEstimateList : IListCaseEstimates
    {
        public SingleEstimateList(Guid caseId, bool includeLine = true)
        {
            var now = new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);
            var details = new EstimateDetails(
                "Estimate 1", 2, 50m, null, null, 20m, null,
                EstimateDiscounts.None, EstimateVatPolicy.For(RepairerVatStatus.Registered));
            Estimate = new(
                Guid.NewGuid(), caseId, 1, RepairSpecificationState.Draft,
                new(RepairSpecificationSourceRoute.Manual, null, null, null),
                includeLine
                    ? [new(Guid.NewGuid(), 1, "repair", null, "Repair door", 1m, null, false,
                        null, null, null, null, null, ActorKind.Staff, "engineer", now, "engineer", now)]
                    : [],
                null, "engineer", now, null, null, null, null, details);
        }

        public RepairSpecificationVersion Estimate { get; }

        public Task<IReadOnlyList<RepairSpecificationVersion>> ExecuteAsync(
            Guid caseId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RepairSpecificationVersion>>(
                caseId == Estimate.CaseId ? [Estimate] : []);
    }
}
