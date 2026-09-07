using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;

namespace Pegasus.IntegrationTests.Browser;

/// <summary>
/// ENG-034: report-draft readiness is rendered in the Case Report section.
/// The readiness verdict conditions the draft controls and names every
/// outstanding requirement (design README §Absent versus disabled).
/// The fixture mirrors QDOS26002 from prod-diagnostics.md §4: a near-empty
/// assessment, so most readiness checks fire.
/// </summary>
[Trait("Category", "SqlServer")]
[Trait("Category", "Browser")]
public sealed class AssessmentReadinessSummaryBrowserTests
{
    [Fact]
    public async Task NotReadyReportDraftControlsStateTheConditionAndTheShellRenders()
    {
        var caseId = Guid.NewGuid();
        await using var support = await BrowserTestSupport.StartAsync(
            width: 1920,
            height: 1080,
            configureWebHost: builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetCase>();
                services.RemoveAll<IGetCaseAssessment>();
                services.RemoveAll<IGetAssessmentAccess>();
                services.RemoveAll<IGetAssessmentWorkspace>();
                services.RemoveAll<IAssessmentReportProjectionSource>();
                services.AddSingleton<IGetCase>(new FakeGetCase(caseId));
                services.AddSingleton<IGetCaseAssessment>(new FakeGetCaseAssessment(NearEmptyProjection(caseId)));
                services.AddSingleton<IGetAssessmentAccess>(new FakeGetAssessmentAccess());
                services.AddSingleton<IGetAssessmentWorkspace>(new FakeGetAssessmentWorkspace(
                    AssessmentWorkspaceTestData.Create(NearEmptyProjection(caseId))));
                services.AddSingleton<IAssessmentReportProjectionSource>(
                    new FakeProjectionSource(NearEmptyInput(caseId)));
            }));

        var response = await support.GoToAsync($"/Cases/{caseId:D}?section=report");
        Assert.Equal(200, response.Status);

        // The Case frame remains the one page and the addressed Report body
        // renders server-side on the initial response.
        var heading = support.Page.Locator("#section-report-title");
        Assert.Equal("Report", (await heading.InnerTextAsync()).Trim());
        Assert.Equal(7, await support.Page.Locator(".record-ribbon .ribbon-item").CountAsync());
        Assert.Equal("Estimate", (await support.Page.Locator("#section-estimate-title").InnerTextAsync()).Trim());

        // The readiness verdict the old disclosure itemised now leaves the
        // report-draft controls disabled with their condition stated, and
        // the reasons are named once, beside the bar (FRD-11 fail-closed).
        var generate = support.Page.Locator("#section-report .gated");
        var condition = await generate.GetAttributeAsync("data-condition");
        Assert.Equal("Not ready", condition?.Trim());
        Assert.True(await generate.Locator("button[disabled][aria-disabled=\"true\"]").CountAsync() == 1);
        Assert.Equal(0, await support.Page.Locator("#section-report a:has-text(\"Preview report draft\")").CountAsync());
        var warning = support.Page.Locator("#section-report .notice--warning");
        var warningText = (await warning.First.InnerTextAsync()).Trim();
        Assert.StartsWith("Report draft not ready:", warningText, StringComparison.Ordinal);
        Assert.Contains(
            AssessmentReportProjection.RepairCostRequirement,
            warningText,
            StringComparison.Ordinal);
        Assert.Equal(0, await support.Page.Locator(".readiness-summary").CountAsync());

        // No estimate surfaces are drawn for a case with no specification:
        // the pane states the empty fact, and no inert tab strip renders.
        Assert.Equal("No estimates recorded",
            (await support.Page.Locator(".estimate-empty").InnerTextAsync()).Trim());
        Assert.Equal(0, await support.Page.Locator(".estimate-tabs").CountAsync());

        Assert.Empty(await support.FindAccessibilityViolationIdsAsync());
    }

    private static CaseAssessmentProjection NearEmptyProjection(Guid caseId) => new(
        caseId,
        "QDOS-2026-00042",
        CaseVersion: 0,
        State: CaseLifecycleState.NotReady,
        AssignedEngineerId: null,
        Fields: [],
        EstimateLines: [],
        CaseOwned: new AssessmentCaseOwnedData(
            Registration: null,
            Make: null,
            Model: null,
            Mileage: null,
            MileageUnit: null,
            IncidentDate: null,
            InstructionDate: null,
            InspectionMode: null,
            InspectionAddress: null));

    private static AssessmentReportProjectionInput NearEmptyInput(Guid caseId) => new(
        NearEmptyProjection(caseId),
        ClaimantName: null,
        OurReference: "QDOS-2026-00042",
        YourReference: null,
        ReportFor: [],
        ReportDate: new DateOnly(2026, 8, 20),
        Photos: [],
        Sources: [],
        CurrentEstimate: null,
        Signatory: null);

    private sealed class FakeGetCase(Guid caseId) : IGetCase
    {
        public Task<CaseDetails?> ExecuteAsync(GetCaseQuery query, CancellationToken cancellationToken)
        {
            if (query.CaseId != caseId)
            {
                return Task.FromResult<CaseDetails?>(null);
            }

            var identity = new CaseIdentity(caseId, "QDOS", 2026, 42, "QDOS-2026-00042");
            var workflow = new CaseWorkflowRecord(
                caseId, identity, CaseLifecycleState.NotReady, null, null,
                null, null, null, null, null, 0);
            var summary = new CaseSearchItem(
                caseId, identity.Reference, null, CaseType.Inspection, "Approved Principal",
                workflow.State, null, null, "Alex Example", "P-100",
                DateTimeOffset.UtcNow, null, "Email", DateTimeOffset.UtcNow);
            CaseDetails details = new(
                summary, workflow, null, [], null, CaseCustodyState.Pending, [], [], []);
            return Task.FromResult<CaseDetails?>(details);
        }
    }

    private sealed class FakeGetCaseAssessment(CaseAssessmentProjection projection) : IGetCaseAssessment
    {
        public Task<CaseAssessmentProjection?> ExecuteAsync(Guid caseId, CancellationToken cancellationToken) =>
            Task.FromResult<CaseAssessmentProjection?>(projection);
    }

    private sealed class FakeProjectionSource(AssessmentReportProjectionInput input)
        : IAssessmentReportProjectionSource
    {
        public Task<AssessmentReportProjectionInput?> GetAsync(
            Guid caseId, ActionActor actor, CancellationToken cancellationToken = default) =>
            Task.FromResult<AssessmentReportProjectionInput?>(input);
    }
}
