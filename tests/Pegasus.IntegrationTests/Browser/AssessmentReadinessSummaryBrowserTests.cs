using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Actors;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;

namespace Pegasus.IntegrationTests.Browser;

/// <summary>
/// ENG-003: proves the assessment page's two duplicate warning surfaces
/// (the readiness aside and the "Report draft &#8594; Not ready" card, both
/// rendering <see cref="AssessmentPolicy.EvaluateReadiness"/> output as a
/// per-issue list) collapsed into one combined indicator with the itemised
/// list reached through a single accessible disclosure. The fixture mirrors
/// QDOS26002 from prod-diagnostics.md &#167;4: a near-empty assessment, so
/// most of the readiness rail fires.
/// </summary>
[Trait("Category", "SqlServer")]
[Trait("Category", "Browser")]
public sealed class AssessmentReadinessSummaryBrowserTests
{
    [Fact]
    public async Task ReadinessSummaryOwnsTheOneItemisedListRevealedByHoverAndFocus()
    {
        var caseId = Guid.NewGuid();
        await using var support = await BrowserTestSupport.StartAsync(
            width: 1920,
            height: 1080,
            configureWebHost: builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetCase>();
                services.RemoveAll<IGetCaseAssessment>();
                services.RemoveAll<IGetAssessmentWorkspace>();
                services.RemoveAll<IAssessmentReportProjectionSource>();
                services.AddSingleton<IGetCase>(new FakeGetCase(caseId));
                services.AddSingleton<IGetCaseAssessment>(new FakeGetCaseAssessment(NearEmptyProjection(caseId)));
                services.AddSingleton<IGetAssessmentWorkspace>(new FakeGetAssessmentWorkspace(
                    AssessmentWorkspaceTestData.Create(NearEmptyProjection(caseId))));
                services.AddSingleton<IAssessmentReportProjectionSource>(
                    new FakeProjectionSource(NearEmptyInput(caseId)));
            }));

        var response = await support.GoToAsync($"/Cases/{caseId:D}/Assessment");
        Assert.Equal(200, response.Status);

        // Exactly one itemised list on the whole page: the readiness panel's
        // disclosure owns it, the report-draft card only references the count.
        var itemisedLists = support.Page.Locator(".blocker-list");
        Assert.Equal(1, await itemisedLists.CountAsync());
        var items = itemisedLists.Locator(".blocker");
        var itemCount = await items.CountAsync();
        Assert.True(itemCount > 1, "The near-empty fixture should fail most readiness checks.");

        // The combined chip names the same count as the disclosed list.
        var chipText = await support.Page.Locator(".readiness-summary summary .status-chip").InnerTextAsync();
        var chipCount = int.Parse(Regex.Match(chipText, @"\d+").Value, CultureInfo.InvariantCulture);
        Assert.Equal(itemCount, chipCount);
        Assert.Matches(@"^\d+ issues detected$", chipText.Trim());

        // The report-draft "Not ready" card names the same count and points
        // back to the readiness panel instead of repeating the list.
        var notReadyText = await support.Page.Locator(".status-card--attention", new()
        {
            HasText = "Not ready"
        }).InnerTextAsync();
        Assert.Contains($"{chipCount} issues detected", notReadyText, StringComparison.Ordinal);
        Assert.Contains("see Readiness above", notReadyText, StringComparison.Ordinal);

        // Collapsed by default: the disclosure content is not visible.
        Assert.False(await items.First.IsVisibleAsync());

        // Hover reveals it.
        await support.Page.HoverAsync(".readiness-summary summary");
        Assert.True(await items.First.IsVisibleAsync());
        var revealedText = await items.First.InnerTextAsync();
        Assert.NotEmpty(revealedText);

        // Moving away closes it again.
        await support.Page.Locator("h1").First.HoverAsync();
        Assert.False(await items.First.IsVisibleAsync());

        // Keyboard focus alone (no activation) also reveals it.
        await support.Page.Locator(".readiness-summary summary").FocusAsync();
        Assert.True(await items.First.IsVisibleAsync());

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
        Costs: null);

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
