using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;
using Pegasus.IntegrationTests.Reports;

using static Pegasus.IntegrationTests.CaseWebTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>
/// v28 P30: the report's narrative is the Engineer's wording blocks in their
/// order. The Report section offers every block the report would print, each
/// control joining the record's one Save form, and states them as derived
/// cells without the lease. A Case whose report cannot yet be projected has
/// no panel at all.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class CaseReportWordingWebTests
{
    private const string NatureSentence =
        "The vehicle has suffered Moderate collision/impact damage to the Right Rear.";

    [Fact]
    public async Task TheReportSectionOffersEveryWordingBlockThroughTheOneSaveForm()
    {
        var store = ReadyStore();
        using var workspace = await EnterEditModeAsync(store, services => Register(services, store));

        var report = SectionHtml(await workspace.GetWorkspaceAsync(), "report");

        Assert.Contains("data-report-wording", report, StringComparison.Ordinal);
        foreach (var key in new[] { "nature", "comments", "history", "condition", "settlement" })
        {
            Assert.Contains($"data-wording-block=\"{key}\"", report, StringComparison.Ordinal);
        }
        // A repairable Case has no salvage paragraph to print.
        Assert.DoesNotContain("data-wording-block=\"salvage\"", report, StringComparison.Ordinal);
        Assert.Contains(NatureSentence, report, StringComparison.Ordinal);
        Assert.Contains("wordingEdits[0].Key", report, StringComparison.Ordinal);

        // Every control the panel posts belongs to the Case's one Save form.
        var controls = Regex.Matches(report, "<(?:input|textarea)[^>]*name=\"wordingEdits\\[[^\"]*\"[^>]*>");
        Assert.NotEmpty(controls);
        Assert.All(controls, control =>
            Assert.Contains("form=\"case-edit-form\"", control.Value, StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnExcludedEmptyComposedBlockStillHasCaseSaveControls()
    {
        var store = ReadyStore();
        using var workspace = await EnterEditModeAsync(
            store,
            services => Register(
                services,
                store,
                [new CaseReportWording(
                    ReportWordingComposition.ValuationCommentary,
                    null,
                    null,
                    null,
                    Included: false)]));

        var report = SectionHtml(await workspace.GetWorkspaceAsync(), "report");

        Assert.Contains("data-wording-block=\"commentary\"", report, StringComparison.Ordinal);
        Assert.Matches(
            "name=\"wordingEdits\\[[0-9]+\\]\\.Key\" value=\"commentary\"[^>]*form=\"case-edit-form\"",
            report);
    }

    /// <summary>
    /// Wording the Engineer writes travels with the Case save; wording that
    /// reads the same as the composed sentence is no change, so the block
    /// keeps tracking its fields.
    /// </summary>
    [Fact]
    public async Task TheWordingTheEngineerWroteTravelsWithTheCaseSave()
    {
        var store = ReadyStore();
        using var workspace = await EnterEditModeAsync(store, services => Register(services, store));
        await workspace.GetWorkspaceAsync();

        using var response = await SaveAsync(
            workspace,
            ("wordingEdits[0].Key", "nature"),
            ("wordingEdits[0].Title", "Nature of Incident"),
            ("wordingEdits[0].Text", "The vehicle was struck from behind while stationary."),
            ("wordingEdits[0].Order", "0"),
            ("wordingEdits[0].Included", "true"),
            ("wordingEdits[0].Manual", "false"),
            ("wordingEdits[1].Key", "condition"),
            ("wordingEdits[1].Title", "Pre-Incident Condition"),
            ("wordingEdits[1].Text", "The vehicle is considered to be in Good condition for its age and type."),
            ("wordingEdits[1].Order", "6"),
            ("wordingEdits[1].Included", "false"),
            ("wordingEdits[1].Manual", "false"));

        AssertPrg(response, store.CaseId);
        var blocks = Assert.Single(store.Saves).ReportWording!.Blocks!;
        var nature = blocks.Single(block => block.Key == "nature");
        Assert.Equal("The vehicle was struck from behind while stationary.", nature.Text);
        Assert.Null(nature.Title);
        Assert.Null(nature.Order);
        Assert.True(nature.Included);
        var condition = blocks.Single(block => block.Key == "condition");
        Assert.Null(condition.Text);
        Assert.Null(condition.Title);
        Assert.False(condition.Included);
    }

    [Fact]
    public async Task TheWordingReadsAsDerivedCellsWithoutTheLease()
    {
        var store = ReadyStore();
        var html = await ReadCaseAsync(store, services => Register(services, store));

        var report = SectionHtml(html, "report");
        Assert.Contains("data-report-wording", report, StringComparison.Ordinal);
        Assert.Contains(NatureSentence, report, StringComparison.Ordinal);
        Assert.DoesNotContain("wordingEdits[", report, StringComparison.Ordinal);
        Assert.DoesNotContain("data-wording-new", report, StringComparison.Ordinal);
    }

    /// <summary>
    /// A Case whose report cannot yet be projected carries no wording panel:
    /// the readiness rail already states what is outstanding, and an empty
    /// panel would state nothing.
    /// </summary>
    [Fact]
    public async Task ACaseWhoseReportCannotBeProjectedCarriesNoWordingPanel()
    {
        var store = ReadyStore();
        var html = await ReadCaseAsync(
            store,
            services => Substitute<ICaseReportSnapshotSource>(
                services, new ReadyReportSnapshots(store, currentEstimate: false)));

        Assert.DoesNotContain("data-report-wording", SectionHtml(html, "report"), StringComparison.Ordinal);
    }

    private static RecordingCaseDetailsStore ReadyStore() => new()
    {
        State = CaseLifecycleState.ReportPreparation,
        CaseState = CaseLifecycleState.ReportPreparation
    };

    private static void Register(
        IServiceCollection services,
        RecordingCaseDetailsStore store,
        IReadOnlyList<CaseReportWording>? wording = null)
    {
        Substitute<ISaveCaseWorkspace>(services, store);
        Substitute<IGetAssessmentAccess>(services, new FakeGetAssessmentAccess(canOpen: true));
        Substitute<ICaseReportSnapshotSource>(
            services,
            new ReadyReportSnapshots(store, currentEstimate: true, wording));
    }

    private static async Task<string> ReadCaseAsync(
        RecordingCaseDetailsStore store, Action<IServiceCollection> substitutePorts)
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                Substitute<IGetCase>(services, store);
                Substitute<IGetCasePageFrame>(services, store);
                Substitute<IGetCaseVehicleSection>(services, store);
                Substitute<IGetCaseValuationSection>(services, store);
                Substitute<IGetCaseNotesSection>(services, store);
                Substitute<IGetCaseFilesSection>(services, store);
                Substitute<IGetAssessmentWorkspace>(services, store);
                Substitute<IGetAssessmentAccess>(services, new FakeGetAssessmentAccess(canOpen: true));
                substitutePorts(services);
            }));
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        return await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}?section=report");
    }

    private static Task<HttpResponseMessage> SaveAsync(
        LeasedWorkspace workspace, params (string Name, string Value)[] fields) =>
        workspace.Client.PostAsync(
            $"/Cases/{workspace.Store.CaseId:D}?handler=Save",
            Form(
                workspace.AntiforgeryToken,
                [
                    ("id", workspace.Store.CaseId.ToString("D")),
                    ("expectedVersion", workspace.Store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                    ("operationKey", Guid.NewGuid().ToString("N")),
                    ("editLeaseToken", workspace.Store.LeaseToken),
                    .. fields
                ]));

    /// <summary>One section element's markup, from its opening tag to the next section's.</summary>
    private static string SectionHtml(string html, string key)
    {
        var start = html.IndexOf($"id=\"section-{key}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, $"The {key} section must render.");
        var next = html.IndexOf("<section class=\"record-section", start, StringComparison.Ordinal);
        return next < 0 ? html[start..] : html[start..next];
    }

    /// <summary>
    /// A Case whose report projects: the ready report fixture's own facts,
    /// with the Current estimate withheld when the report is not to project.
    /// </summary>
    private sealed class ReadyReportSnapshots(
        RecordingCaseDetailsStore store,
        bool currentEstimate,
        IReadOnlyList<CaseReportWording>? wording = null)
        : ICaseReportSnapshotSource
    {
        public Task<CaseReportFreezeInputs?> GetAsync(
            Guid caseId, ActionActor actor, CancellationToken cancellationToken)
        {
            var projection = AssessmentReportDraftWebTests.ReadyInput(caseId);
            if (!currentEstimate)
            {
                projection = projection with { CurrentEstimate = null };
            }
            projection = projection with { Wording = wording };
            return Task.FromResult<CaseReportFreezeInputs?>(new(
                projection,
                new(
                    projection.Assessment,
                    null,
                    null,
                    [],
                    projection.CurrentEstimate,
                    null,
                    [],
                    new Dictionary<Guid, DocumentVersion>()),
                projection.OurReference,
                store.CaseVersion));
        }
    }
}
