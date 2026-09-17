using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Eva;
using Pegasus.Core.Identity;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;

using static Pegasus.IntegrationTests.CaseWebTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The Workflow page: hold and release are covered beside the workspace tests; these
/// cover return-to-Review, Engineer assignment and finding, and the linked replacement.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class CaseWorkflowWebTests
{
    [Fact]
    public Task WorkflowPageReturnsNotFoundOnGet() => AssertPostOnlyPageAsync("Workflow");

    [Fact]
    public Task VehiclePageReturnsNotFoundOnGet() => AssertPostOnlyPageAsync("Vehicle");

    [Fact]
    public Task CustodyPageReturnsNotFoundOnGet() => AssertPostOnlyPageAsync("Custody");

    [Fact]
    public Task TasksPageReturnsNotFoundOnGet() => AssertPostOnlyPageAsync("Tasks");

    [Fact]
    public Task ClosurePageReturnsNotFoundOnGet() => AssertPostOnlyPageAsync("Closure");

    private static async Task AssertPostOnlyPageAsync(string page)
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        using var response = await client.GetAsync($"/Cases/{Guid.NewGuid():D}/{page}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task NativeHandoffDialogPostsWithoutEvaOrASeparateReviewAction()
    {
        var engineerId = Guid.NewGuid();
        var store = new RecordingCaseDetailsStore { State = CaseLifecycleState.Review };
        using var workspace = await EnterEditModeAsync(store, services =>
        {
            Substitute<IAssignCaseEngineer>(services, store);
            Substitute<IStaffAccountQueries>(services,
                new StubStaffAccounts(engineerId, "Engineer", StaffRole.Engineer));
            services.RemoveAll<ISubmitCaseToEva>();
        });
        var html = await workspace.GetWorkspaceAsync();
        Assert.Contains("Hand to Engineer", RecordBar(html), StringComparison.Ordinal);
        var dialog = Section(html, "case-handoff-dialog-title");
        Assert.Contains("handler=AssignEngineer", dialog, StringComparison.Ordinal);
        Assert.Contains(engineerId.ToString("D"), dialog, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"reason\"", dialog, StringComparison.Ordinal);
        Assert.DoesNotContain("reviewed", dialog, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("handler=StartWork", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Start report preparation", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"eva-handoff-dialog-title\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Send via API", html, StringComparison.Ordinal);

        using var denied = await workspace.PostAsync("Workflow?handler=AssignEngineer",
            new FormUrlEncodedContent([]));
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        using var response = await workspace.PostAsync("Workflow?handler=AssignEngineer",
            Form(AntiforgeryValue(dialog),
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", InputValue(dialog, "expectedVersion")),
                ("operationKey", InputValue(dialog, "operationKey")),
                ("editLeaseToken", InputValue(dialog, "editLeaseToken")),
                ("engineerId", engineerId.ToString("D")),
                ("instructionsComplete", InputValue(dialog, "instructionsComplete")),
                ("imagesComplete", InputValue(dialog, "imagesComplete")),
                ("evidenceReference", InputValue(dialog, "evidenceReference"))));
        AssertPrg(response, store.CaseId);
        var handoff = Assert.Single(store.EngineerAssignments);
        AssertLeasedMutation(workspace, handoff, InputValue(dialog, "operationKey"), "Hand to Engineer");
        Assert.Equal(engineerId, handoff.EngineerId);
        Assert.Empty(store.Transitions);
        Assert.Contains("The case was handed to the Engineer.",
            await workspace.GetWorkspaceAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task WorkflowPageBindsReviewReturnEngineerAssignmentFindingAndLinkedReplacement()
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await EnterEditModeAsync(store, services =>
        {
            Substitute<ITransitionCase>(services, store);
            Substitute<IAssignCaseEngineer>(services, store);
            Substitute<ISetCaseSignOffEngineer>(services, store);
            Substitute<IRecordEngineerFinding>(services, store);
            Substitute<ICreateLinkedReplacement>(services, store);
        });
        var engineerId = Guid.NewGuid();
        var signOffEngineerId = Guid.NewGuid();
        (string Name, string Value)[] readiness =
        [
            ("instructionsComplete", "true"),
            ("imagesComplete", "true"),
            ("evidenceReference", "review-evidence-1")
        ];

        using var returned = await workspace.PostAsync(
            "Workflow?handler=ReturnToReview",
            workspace.MutationForm("return-to-review", "Images arrived", readiness));
        using var assigned = await workspace.PostAsync(
            "Workflow?handler=AssignEngineer",
            workspace.MutationForm("assign-engineer", "Engineer available", [("engineerId", engineerId.ToString("D")), .. readiness]));
        using var signOffSet = await workspace.PostAsync(
            "Workflow?handler=SetSignOffEngineer",
            workspace.MutationForm(
                "set-sign-off-engineer",
                "Signatory selected",
                ("signOffEngineerId", signOffEngineerId.ToString("D"))));
        using var found = await workspace.PostAsync(
            "Workflow?handler=RecordEngineerFinding",
            workspace.MutationForm("record-finding", "Inspection complete", ("assessment", "TotalLoss")));
        using var replaced = await workspace.PostAsync(
            "Workflow?handler=CreateLinkedReplacement",
            workspace.MutationForm("create-replacement", "Wrong principal", ("replacementPrincipalCode", "ACME")));

        AssertPrg(returned, store.CaseId);
        AssertPrg(assigned, store.CaseId);
        AssertPrg(signOffSet, store.CaseId);
        AssertPrg(found, store.CaseId);
        AssertPrg(replaced, store.CaseId);
        var expectedReadiness = new CaseReadinessEvidence(true, true, "review-evidence-1");

        var transition = Assert.Single(store.Transitions);
        AssertLeasedMutation(workspace, transition, "return-to-review", "Images arrived");
        Assert.Equal(CaseTransitionDestination.Review, transition.Destination);
        Assert.Equal(expectedReadiness, transition.Readiness);

        var assignment = Assert.Single(store.EngineerAssignments);
        AssertLeasedMutation(workspace, assignment, "assign-engineer", "Hand to Engineer");
        Assert.Equal(engineerId, assignment.EngineerId);
        Assert.Equal(expectedReadiness, assignment.Readiness);

        var signOffSelection = Assert.Single(store.SignOffSelections);
        AssertLeasedMutation(
            workspace,
            signOffSelection,
            "set-sign-off-engineer",
            "Signatory selected");
        Assert.Equal(signOffEngineerId, signOffSelection.SignOffEngineerId);

        var finding = Assert.Single(store.EngineerFindings);
        AssertLeasedMutation(workspace, finding, "record-finding", "Inspection complete");
        Assert.Equal(AuditAssessment.TotalLoss, finding.Assessment);

        var replacement = Assert.Single(store.LinkedReplacements);
        AssertLeasedMutation(workspace, replacement, "create-replacement", "Wrong principal");
        Assert.Equal("ACME", replacement.ReplacementPrincipalCode);
        var html = await workspace.GetWorkspaceAsync();
        Assert.Contains("Replacement case ACME3100001 was allocated and linked.", html, StringComparison.Ordinal);

        await AssertRefusalKeepsEditModeAsync(
            workspace,
            "Workflow?handler=RecordEngineerFinding",
            workspace.MutationForm("record-finding-2", "Second look", ("assessment", "Repairable")));
        await AssertLostLeaseClearsEditModeAsync(
            workspace,
            "Workflow?handler=ReturnToReview",
            workspace.MutationForm("return-to-review-2", "Lease gone", readiness));
    }

    /// <summary>
    /// FRD-07: the EVA handoff is available in Review,
    /// Report Preparation and Post Report. Not Ready offers no EVA control and
    /// draws no disabled handoff.
    /// </summary>
    [Theory]
    [InlineData(CaseLifecycleState.NotReady, false)]
    [InlineData(CaseLifecycleState.ReportPreparation, true)]
    [InlineData(CaseLifecycleState.PostReport, true)]
    [InlineData(CaseLifecycleState.Review, true)]
    public async Task SendToEvaRendersInReviewAndWithEngineer(CaseLifecycleState state, bool offersHandoff)
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingCaseDetailsStore { State = state };
        var evaStores = new StubEvaSubmissionStores(
            new EvaSubmissionModes(PrincipalReportGenerationPolicy.EvaZip));
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
                Substitute<IEvaSubmissionQueries>(services, evaStores);
                Substitute<IEvaSubmissionModeStore>(services, evaStores);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var html = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");

        Assert.Equal(offersHandoff, RecordBar(html).Contains("Send to EVA", StringComparison.Ordinal));
        Assert.Equal(
            offersHandoff,
            html.Contains("data-dialog=\"eva-handoff-dialog\"", StringComparison.Ordinal));
        // The trigger is a real link to the fallback page the
        // dialog's own form posts to, so the handoff stays reachable without
        // JavaScript rather than being a dead button with no static target.
        Assert.Equal(
            offersHandoff,
            html.Contains(
                $"href=\"/Cases/{store.CaseId:D}/Eva/Send\"",
                StringComparison.OrdinalIgnoreCase));
        // The handoff's own routes come with it: the export posts from the
        // dialog, so the route is present exactly when the control is.
        Assert.Equal(
            offersHandoff,
            html.Contains(
                $"/Cases/{store.CaseId:D}/Documents/Export",
                StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(CaseLifecycleState.Review, PrincipalReportGenerationPolicy.EvaManualApi, "Send via API")]
    [InlineData(CaseLifecycleState.ReportPreparation, PrincipalReportGenerationPolicy.EvaManualApi, "Send via API")]
    [InlineData(CaseLifecycleState.Review, PrincipalReportGenerationPolicy.EvaZip, "Export EVA ZIP")]
    [InlineData(CaseLifecycleState.ReportPreparation, PrincipalReportGenerationPolicy.EvaZip, "Export EVA ZIP")]
    public async Task SendPageRendersItsChoiceInReviewAndWithEngineer(
        CaseLifecycleState state,
        PrincipalReportGenerationPolicy policy,
        string expectedAction)
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingCaseDetailsStore { CaseState = state, State = state };
        var evaStores = new StubEvaSubmissionStores(
            new EvaSubmissionModes(policy));
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                Substitute<ICaseDataQueries>(services, store);
                Substitute<ICaseWorkflowQueries>(services, store);
                Substitute<IEvaSubmissionQueries>(services, evaStores);
                Substitute<IEvaSubmissionModeStore>(services, evaStores);
                // A composed transport is required for the manual API policy.
                Substitute<ISubmitCaseToEva>(services, new StubSubmitCaseToEva());
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        // EXT-04: the send page for a case still in Review — the one place the
        // operator gets the principal's configured EVA route.
        var html = await IntakeWebDriver.GetHtmlAsync(client, $"/Cases/{store.CaseId:D}/Eva/Send");

        // The page's own copy, as the workspace redesign restyled it: the handoff heading,
        // the case it is for, and its configured route out.
        Assert.Contains("<h1>EVA handoff</h1>", html, StringComparison.Ordinal);
        Assert.Contains(
            "<h2 id=\"eva-handoff-title\">QDOS3100042</h2>",
            html,
            StringComparison.Ordinal);
        Assert.Contains($"<span>{expectedAction}</span>", html, StringComparison.Ordinal);
        if (policy == PrincipalReportGenerationPolicy.EvaManualApi)
        {
            Assert.Contains($"/Cases/{store.CaseId:D}/Eva/Send", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Export EVA ZIP", html, StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains($"/Cases/{store.CaseId:D}/Documents/Export", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Send via API", html, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// D10: "report sent" is confirmed from detected Sent evidence and is never
    /// asserted by hand, so the action renders only while the case is With
    /// Engineer, this browser holds the edit authority, and retained evidence
    /// exists. The evidence is named by mailbox and time; the transport handles
    /// and hashes it also carries stay internal.
    /// </summary>

    [Fact]
    public async Task LifecyclePostsBindHoldReleaseAndNativeHandoffToAuthenticatedLease()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingCaseDetailsStore();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetCase>();
                services.RemoveAll<IAcquireCaseEditLease>();
                services.RemoveAll<IHoldCase>();
                services.RemoveAll<IReleaseCase>();
                services.RemoveAll<ITransitionCase>();
                services.RemoveAll<IAssignCaseEngineer>();
                services.AddSingleton<IGetCase>(store);
                SubstituteDetailsPageReaders(services, store);
                services.AddSingleton<IAcquireCaseEditLease>(store);
                services.AddSingleton<IHoldCase>(store);
                services.AddSingleton<IReleaseCase>(store);
                services.AddSingleton<ITransitionCase>(store);
                services.AddSingleton<IAssignCaseEngineer>(store);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var initialHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        using var claimResponse = await client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=ClaimLease",
            Form(
                AntiforgeryValue(initialHtml),
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", InputValue(initialHtml, "operationKey"))));
        AssertPrg(claimResponse, store.CaseId);

        var leasedHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        // The hold control is state-gated on the bar and its reason dialog
        // carries the lease envelope; the posts below exercise every route.
        Assert.Contains("Place on Hold", leasedHtml, StringComparison.Ordinal);
        Assert.Contains("data-dialog=\"case-hold-dialog\"", leasedHtml, StringComparison.Ordinal);
        Assert.Contains("name=\"reason\"", leasedHtml, StringComparison.Ordinal);
        var antiforgeryToken = AntiforgeryValue(leasedHtml);
        using var holdResponse = await client.PostAsync(
            $"/Cases/{store.CaseId:D}/Workflow?handler=Hold",
            LifecycleForm(antiforgeryToken, store, "hold-case", "Awaiting provider"));
        using var releaseResponse = await client.PostAsync(
            $"/Cases/{store.CaseId:D}/Workflow?handler=ReleaseHold",
            LifecycleForm(antiforgeryToken, store, "release-case", "Provider replied"));
        var engineerId = Guid.NewGuid();
        using var handoffResponse = await client.PostAsync(
            $"/Cases/{store.CaseId:D}/Workflow?handler=AssignEngineer",
            Form(antiforgeryToken,
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", "native-handoff"),
                ("editLeaseToken", store.LeaseToken),
                ("engineerId", engineerId.ToString("D")),
                ("instructionsComplete", "true"),
                ("imagesComplete", "true"),
                ("evidenceReference", "case-completeness-projection")));

        AssertPrg(holdResponse, store.CaseId);
        AssertPrg(releaseResponse, store.CaseId);
        AssertPrg(handoffResponse, store.CaseId);
        var actorSubjectId = Assert.Single(store.Claims).Actor.SubjectId;
        var hold = Assert.Single(store.Holds);
        var release = Assert.Single(store.Releases);
        var handoff = Assert.Single(store.EngineerAssignments);
        Assert.Equal(actorSubjectId, hold.Actor.SubjectId);
        Assert.Equal(actorSubjectId, release.Actor.SubjectId);
        Assert.Equal(actorSubjectId, handoff.Actor.SubjectId);
        Assert.Equal(store.CaseVersion, hold.ExpectedVersion);
        Assert.Equal(store.CaseVersion, release.ExpectedVersion);
        Assert.Equal(store.CaseVersion, handoff.ExpectedVersion);
        Assert.Equal(store.LeaseToken, hold.EditLeaseToken);
        Assert.Equal(store.LeaseToken, release.EditLeaseToken);
        Assert.Equal(store.LeaseToken, handoff.EditLeaseToken);
        Assert.Equal("hold-case", hold.OperationKey);
        Assert.Equal("release-case", release.OperationKey);
        Assert.Equal("native-handoff", handoff.OperationKey);
        Assert.Equal("Hand to Engineer", handoff.Reason);
        Assert.Equal(engineerId, handoff.EngineerId);
        Assert.Empty(store.Transitions);
    }
}
