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
    public async Task CaseEstimateHeaderOffersPrintRepairSpecUnderMoreInReadAndEditMode(bool editing)
    {
        var store = new RecordingCaseDetailsStore();
        var estimates = new SingleEstimateList(store.CaseId);
        if (editing)
        {
            using var workspace = await EnterEngineerEditModeAsync(store, services =>
                Substitute<IListCaseEstimates>(services, estimates), StaffRole.User);
            var html = await GetHtmlAsync(
                workspace.Client,
                $"/Cases/{store.CaseId:D}?section=estimate&estimate={estimates.Estimate.SpecificationId:D}");
            Assert.Contains("data-document-preview", html, StringComparison.Ordinal);
            Assert.Contains(">Print Repair Spec<", html, StringComparison.Ordinal);
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
        client.DefaultRequestHeaders.Add("X-Test-Roles", "User");

        var readHtml = await GetHtmlAsync(
            client,
            $"/Cases/{store.CaseId:D}?section=estimate&estimate={estimates.Estimate.SpecificationId:D}");

        Assert.Contains("data-document-preview", readHtml, StringComparison.Ordinal);
        Assert.Contains(">Print Repair Spec<", readHtml, StringComparison.Ordinal);
    }

    /// <summary>
    /// Reading and editing are one layout (operator, 23 September 2026): the
    /// read view draws the editor's header cells, its ten grid columns and its
    /// contract, discount and VAT bars, each value greyed in its control's
    /// place and no control among them.
    /// </summary>
    [Fact]
    public async Task ReadModeDrawsTheEditorsHeaderCellsGridColumnsAndBarsWithoutControls()
    {
        var store = new RecordingCaseDetailsStore();
        var estimates = new SingleEstimateList(store.CaseId);
        var path = $"/Cases/{store.CaseId:D}?section=estimate&estimate={estimates.Estimate.SpecificationId:D}";

        string editing;
        using (var workspace = await EnterEngineerEditModeAsync(store, services =>
            Substitute<IListCaseEstimates>(services, estimates), StaffRole.User))
        {
            editing = WebUtility.HtmlDecode(EstimateSection(await GetHtmlAsync(workspace.Client, path)));
        }
        // The editor has no form of its own: its controls belong to the Case form (one Save).
        Assert.Contains("name=\"estimateName\" form=\"case-edit-form\"", editing, StringComparison.Ordinal);

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
        client.DefaultRequestHeaders.Add("X-Test-Roles", "User");
        var reading = WebUtility.HtmlDecode(EstimateSection(await GetHtmlAsync(client, path)));
        var readStart = reading.IndexOf("data-estimate-read", StringComparison.Ordinal);
        Assert.True(readStart >= 0, "Read mode must draw the Repair Spec body.");
        var body = reading[readStart..];

        foreach (var label in new[]
        {
            OperatorLabels.CaseWorkspace.EngineerSections.EstimateName,
            OperatorLabels.CaseWorkspace.EngineerSections.LabourRatePerHour,
            OperatorLabels.CaseWorkspace.EngineerSections.RegionalUplift,
            OperatorLabels.CaseWorkspace.EngineerSections.OtherCostsPounds,
            OperatorLabels.CaseWorkspace.EngineerSections.VatPercent,
            CaseWorkspaceLabels.EstimateVat.RepairerStatus
        })
        {
            Assert.Contains(label, editing, StringComparison.Ordinal);
            Assert.Contains(label, body, StringComparison.Ordinal);
        }
        Assert.Equal(10, Occurrences(editing, "<th scope=\"col\""));
        Assert.Equal(10, Occurrences(body, "<th scope=\"col\""));
        foreach (var bar in new[] { "data-estimate-contract", "data-estimate-discounts", "data-estimate-vat" })
        {
            Assert.Contains(bar, editing, StringComparison.Ordinal);
            Assert.Contains(bar, body, StringComparison.Ordinal);
        }
        Assert.DoesNotContain("<input", body, StringComparison.Ordinal);
        Assert.DoesNotContain("<select", body, StringComparison.Ordinal);
        Assert.DoesNotContain("<textarea", body, StringComparison.Ordinal);
        Assert.Contains("<div class=\"fc ro\" data-estimate-name-cell>", body, StringComparison.Ordinal);
        // The line's Type reads in the words the editor's control offers.
        Assert.Contains(
            $"<span class=\"gv\">{OperatorLabels.CaseWorkspace.EngineerSections.Repair}</span>",
            body,
            StringComparison.Ordinal);
    }

    /// <summary>The Repair Spec section, up to its import drop overlay.</summary>
    private static string EstimateSection(string html)
    {
        var start = html.IndexOf("id=\"section-estimate\"", StringComparison.Ordinal);
        Assert.True(start >= 0, "The Repair Spec section must render.");
        var end = html.IndexOf("data-estimate-import-overlay", start, StringComparison.Ordinal);
        return end < 0 ? html[start..] : html[start..end];
    }

    [Fact]
    public async Task CaseEstimateHeaderOmitsPrintRepairSpecForAnEmptyDraft()
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
        client.DefaultRequestHeaders.Add("X-Test-Roles", "User");

        var html = await GetHtmlAsync(
            client,
            $"/Cases/{store.CaseId:D}?section=estimate&estimate={estimates.Estimate.SpecificationId:D}");

        Assert.DoesNotContain(">Print Repair Spec<", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithoutAConfirmedEngineersValueTheEstimateHeaderShowsNeitherSendToAiNorALockedPill()
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await EnterEngineerEditModeAsync(store, _ => { }, StaffRole.User);

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
                "Estimate 1", 50m, null, 20m,
                EstimateDiscounts.None, EstimateVatPolicy.For(RepairerVatStatus.Registered));
            Estimate = new(
                Guid.NewGuid(), caseId, 1, RepairSpecificationState.Draft,
                new(RepairSpecificationSourceRoute.Manual, null, null, null),
                includeLine
                    ? [new(Guid.NewGuid(), 1, "repair", null, "Repair door", 1m, null, false,
                        null, null, null, null, null, ActorKind.Staff, "engineer", now)]
                    : [],
                null, "engineer", now, null, null, null, null, details);
        }

        public RepairSpecificationVersion Estimate { get; }

        public Task<IReadOnlyList<RepairSpecificationVersion>> ExecuteAsync(
            Guid caseId,
            CaseWorkSelector work, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RepairSpecificationVersion>>(
                caseId == Estimate.CaseId ? [Estimate] : []);
    }
}
