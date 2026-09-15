using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;
using Pegasus.Web.Presentation;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The v26 Case record frame and the 13 September settlements: the one
/// Actions menu per state, Place on Hold with an optional review date, Assign
/// to me, Create audit, the Notes band, and a Save that asks for no reason.
/// </summary>
public sealed partial class CaseDetailsWebTests
{
    /// <summary>
    /// The Actions menu offers exactly the items the state permits inside an
    /// edit session (v25 § Coverage): the hold pair swaps with the state, the
    /// post-report items appear only after the report, and Return to Engineer
    /// only on a completed Case.
    /// </summary>
    [Theory]
    [InlineData(CaseLifecycleState.NotReady,
        "case-hold-dialog,case-correct-principal-dialog",
        "case-release-hold-dialog,case-complete-dialog,case-return-review-dialog,case-return-engineer-dialog")]
    [InlineData(CaseLifecycleState.Held,
        "case-release-hold-dialog,case-correct-principal-dialog",
        "case-hold-dialog,case-complete-dialog,case-return-engineer-dialog")]
    [InlineData(CaseLifecycleState.PostReport,
        "case-complete-dialog,case-return-review-dialog,case-hold-dialog",
        "case-release-hold-dialog,case-return-engineer-dialog")]
    [InlineData(CaseLifecycleState.PostReportComplete,
        "case-return-engineer-dialog",
        "case-hold-dialog,case-release-hold-dialog,case-complete-dialog,case-correct-principal-dialog")]
    public async Task TheActionsMenuOffersTheItemsTheStatePermits(
        CaseLifecycleState state, string offered, string absent)
    {
        var store = new RecordingCaseDetailsStore { State = state };
        using var workspace = await EnterEditModeAsync(store, _ => { });

        var bar = RecordBar(await workspace.GetWorkspaceAsync());

        Assert.Equal(1, Occurrences(bar, "data-case-actions"));
        foreach (var dialog in offered.Split(','))
        {
            Assert.Contains($"data-dialog-open=\"{dialog}\"", bar, StringComparison.Ordinal);
        }
        foreach (var dialog in absent.Split(','))
        {
            Assert.DoesNotContain($"data-dialog-open=\"{dialog}\"", bar, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Outside an edit session the menu is absent when none of its items
    /// needs no lease, and a colleague's live lease offers no Take over
    /// (13 September) — only the colleague chip.
    /// </summary>
    [Fact]
    public async Task OutsideAnEditSessionTheMenuIsAbsentAndAColleaguesLeaseOffersNoTakeOver()
    {
        var store = new RecordingCaseDetailsStore();
        var reading = RecordBar(await ReadCaseAsync(store));
        Assert.DoesNotContain("data-case-actions", reading, StringComparison.Ordinal);
        Assert.Contains(CaseWorkspaceLabels.Frame.EditCase, reading, StringComparison.Ordinal);

        var held = new RecordingCaseDetailsStore { LeaseHolder = "colleague-staff-id" };
        var html = await ReadCaseAsync(held);
        Assert.Contains("data-edit-authority", html, StringComparison.Ordinal);
        Assert.DoesNotContain(CaseWorkspaceLabels.Frame.TakeOver, html, StringComparison.Ordinal);
        Assert.DoesNotContain("handler=ClaimLease", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Place on Hold carries a reason and an optional Review on date no
    /// earlier than today; the posted date reaches the hold command, and a
    /// held Case with a review date says so on the ribbon's state chip.
    /// </summary>
    [Fact]
    public async Task PlaceOnHoldPostsTheReviewDateAndTheRibbonReadsIt()
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await EnterEditModeAsync(store, services =>
            Substitute<IHoldCase>(services, store));

        var html = await workspace.GetWorkspaceAsync();
        var dialog = Section(html, "case-hold-dialog-title");
        Assert.Contains("handler=Hold", dialog, StringComparison.Ordinal);
        Assert.Matches("<textarea[^>]*name=\"reason\"[^>]*required", dialog);
        var today = Pegasus.Core.LondonCalendar.DateAt(DateTimeOffset.UtcNow)
            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        Assert.Contains(
            $"name=\"reviewOn\" type=\"date\" min=\"{today}\"",
            dialog,
            StringComparison.Ordinal);
        Assert.DoesNotMatch("<input[^>]*name=\"reviewOn\"[^>]*required", dialog);

        using var response = await workspace.PostAsync(
            "Workflow?handler=Hold",
            workspace.MutationForm("hold-with-review", "Awaiting the claimant's photographs", ("reviewOn", "2031-09-24")));
        AssertPrg(response, store.CaseId);
        var hold = Assert.Single(store.Holds);
        AssertLeasedMutation(workspace, hold, "hold-with-review", "Awaiting the claimant's photographs");
        Assert.Equal(new DateOnly(2031, 9, 24), hold.ReviewOn);

        using var withoutDate = await workspace.PostAsync(
            "Workflow?handler=Hold",
            workspace.MutationForm("hold-without-review", "No date yet"));
        Assert.Null(store.Holds[^1].ReviewOn);

        store.State = CaseLifecycleState.Held;
        store.HoldReviewOn = new DateOnly(2031, 9, 24);
        var heldHtml = WebUtility.HtmlDecode(await workspace.GetWorkspaceAsync());
        Assert.Contains(
            $"{OperatorLabels.CaseStage(CaseLifecycleState.Held)} · review on 24 Sep",
            heldHtml,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// P8: an Engineer on a Review Case with no Engineer gets Assign to me in
    /// the assignment dialog, as its own form, and the post reaches the
    /// self-assignment command with the session's envelope. A User, who holds
    /// no Engineer authority, is not offered it.
    /// </summary>
    [Fact]
    public async Task AssignToMeIsOfferedToAnEngineerAndPostsTheSelfAssignment()
    {
        var engineerId = Guid.NewGuid();
        var store = new RecordingCaseDetailsStore { State = CaseLifecycleState.Review };
        using (var user = await EnterEditModeAsRoleAsync(store, "User", services =>
            Substitute<IStaffAccountQueries>(services, new StubStaffAccounts(engineerId, "Engineer", StaffRole.Engineer))))
        {
            var html = await user.GetWorkspaceAsync();
            Assert.Contains("data-dialog-open=\"case-handoff-dialog\"", RecordBar(html), StringComparison.Ordinal);
            Assert.DoesNotContain("handler=AssignToMe", html, StringComparison.Ordinal);
        }

        var engineerStore = new RecordingCaseDetailsStore { State = CaseLifecycleState.Review };
        using var workspace = await EnterEngineerEditModeAsync(engineerStore, services =>
        {
            Substitute<IStaffAccountQueries>(services, new StubStaffAccounts(engineerId, "Engineer", StaffRole.Engineer));
            Substitute<IAssignCaseToMe>(services, engineerStore);
        });
        var leased = await workspace.GetWorkspaceAsync();
        var dialog = Section(leased, "case-handoff-dialog-title");
        Assert.Contains("handler=AssignToMe", dialog, StringComparison.Ordinal);
        Assert.Contains("data-assign-to-me", dialog, StringComparison.Ordinal);
        Assert.Contains(CaseWorkspaceLabels.Frame.AssignToMe, dialog, StringComparison.Ordinal);

        const string operationKey = "5a5b5c5d5e5f50515253545556575859";
        using var response = await workspace.PostAsync(
            "Workflow?handler=AssignToMe",
            Form(
                workspace.AntiforgeryToken,
                ("id", engineerStore.CaseId.ToString("D")),
                ("expectedVersion", engineerStore.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", operationKey),
                ("editLeaseToken", engineerStore.LeaseToken)));
        AssertPrg(response, engineerStore.CaseId);
        var assignment = Assert.Single(engineerStore.SelfAssignments);
        AssertClaimant(workspace, assignment.Actor);
        Assert.Equal(engineerStore.CaseId, assignment.CaseId);
        Assert.Equal(engineerStore.CaseVersion, assignment.ExpectedVersion);
        Assert.Equal(engineerStore.LeaseToken, assignment.EditLeaseToken);
        Assert.Equal(operationKey, assignment.OperationKey);
        Assert.Contains("The case was assigned to you.", await workspace.GetWorkspaceAsync(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Create audit (13 September) is offered only on an Inspection + Audit
    /// Case with a generated report and no Audit yet; an existing Audit is a
    /// link on the ribbon instead.
    /// </summary>
    [Theory]
    [InlineData(CaseType.InspectionAndAudit, true, false, true)]
    [InlineData(CaseType.InspectionAndAudit, false, false, false)]
    [InlineData(CaseType.Inspection, true, false, false)]
    [InlineData(CaseType.InspectionAndAudit, true, true, false)]
    public async Task CreateAuditIsOfferedOnlyWhereTheUseCaseWouldAccept(
        CaseType caseType, bool reportGenerated, bool auditExists, bool offered)
    {
        var store = new RecordingCaseDetailsStore { State = CaseLifecycleState.PostReport, SummaryCaseType = caseType };
        var audit = new RecordingAuditPorts(reportGenerated, auditExists ? new CaseAuditLink(Guid.NewGuid(), "ap.QDOS3100042") : null);
        using var workspace = await EnterEditModeAsync(store, audit.Register);

        var html = await workspace.GetWorkspaceAsync();
        var bar = RecordBar(html);

        Assert.Equal(offered, bar.Contains("data-create-audit", StringComparison.Ordinal));
        Assert.Equal(offered, html.Contains("data-dialog=\"case-create-audit-dialog\"", StringComparison.Ordinal));
        Assert.Equal(auditExists, bar.Contains("data-audit-link", StringComparison.Ordinal));
        if (auditExists)
        {
            Assert.Contains("ap.QDOS3100042", bar, StringComparison.Ordinal);
        }
        Assert.Equal(
            caseType == CaseType.InspectionAndAudit,
            WebUtility.HtmlDecode(html).Contains("data-case-type-chip>Inspection + Audit<", StringComparison.Ordinal));
    }

    /// <summary>
    /// The dialog's one primary posts the session's envelope to the use case;
    /// success lands on the new Audit Case, and a refusal stays on this Case
    /// with Core's reason.
    /// </summary>
    [Fact]
    public async Task CreateAuditLandsOnTheNewCaseOrStatesTheRefusal()
    {
        var store = new RecordingCaseDetailsStore { State = CaseLifecycleState.PostReport, SummaryCaseType = CaseType.InspectionAndAudit };
        var audit = new RecordingAuditPorts(reportGenerated: true, existing: null);
        using var workspace = await EnterEditModeAsync(store, audit.Register);

        var html = await workspace.GetWorkspaceAsync();
        var dialog = Section(html, "case-create-audit-dialog-title");
        Assert.Contains("handler=CreateAudit", dialog, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"reason\"", dialog, StringComparison.Ordinal);
        Assert.Contains(CaseWorkspaceLabels.Frame.OriginalCase, dialog, StringComparison.Ordinal);

        const string operationKey = "6a6b6c6d6e6f60616263646566676869";
        using var created = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=CreateAudit",
            Form(
                workspace.AntiforgeryToken,
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", operationKey),
                ("editLeaseToken", store.LeaseToken)));

        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        Assert.Equal($"/Cases/{audit.AuditCaseId:D}", created.Headers.Location?.OriginalString);
        var request = Assert.Single(audit.Requests);
        AssertClaimant(workspace, request.Actor);
        Assert.Equal(store.CaseId, request.CaseId);
        Assert.Equal(store.CaseVersion, request.ExpectedVersion);
        Assert.Equal(store.LeaseToken, request.EditLeaseToken);
        Assert.Equal(operationKey, request.OperationKey);

        audit.Refusal = AuditCaseRefusal.NoRecordedOutcome;
        using var refused = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=CreateAudit",
            Form(
                workspace.AntiforgeryToken,
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", "7a7b7c7d7e7f70717273747576777879"),
                ("editLeaseToken", store.LeaseToken)));
        AssertPrg(refused, store.CaseId);
        var after = WebUtility.HtmlDecode(await workspace.GetWorkspaceAsync());
        Assert.Contains(new AuditCaseCreationException(AuditCaseRefusal.NoRecordedOutcome).Message, after, StringComparison.Ordinal);
    }

    /// <summary>
    /// The Notes band (13 September): the Principal record's notes read live
    /// and locked, absent when the record has none; this Case's own notes read
    /// in read mode and become the Save form's textareas in the edit session.
    /// </summary>
    [Fact]
    public async Task TheNotesBandReadsTheRecordNotesAndEditsTheCasesOwn()
    {
        var store = new RecordingCaseDetailsStore { RecordNotes = new("Always telephone the handler first.", null) };
        store.DataOverride = await WithCaseNotesAsync(store, "Handler prefers e-mail.", null);

        var reading = OverviewPanel(await ReadCaseAsync(store));
        Assert.Contains("data-notes-band", reading, StringComparison.Ordinal);
        Assert.Contains("data-record-notes=\"principal\"", reading, StringComparison.Ordinal);
        Assert.Contains("Always telephone the handler first.", reading, StringComparison.Ordinal);
        Assert.DoesNotContain("data-record-notes=\"claim-source\"", reading, StringComparison.Ordinal);
        Assert.Contains("Handler prefers e-mail.", reading, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"principalNotes\"", reading, StringComparison.Ordinal);

        using var workspace = await EnterEditModeAsync(store, services =>
            Substitute<ISaveCaseWorkspace>(services, store));
        var editing = OverviewPanel(await workspace.GetWorkspaceAsync());
        Assert.Contains(
            "name=\"principalNotes\" form=\"case-edit-form\" maxlength=\"4000\">Handler prefers e-mail.</textarea>",
            editing,
            StringComparison.Ordinal);
        Assert.Contains("name=\"claimSourceNotes\" form=\"case-edit-form\"", editing, StringComparison.Ordinal);
        // The record's notes stay a locked identity cell inside the session.
        Assert.Matches("<div class=\"fc ro idn rec-notes\" data-record-notes=\"principal\">", editing);
    }

    /// <summary>
    /// v25 decision A: Save asks for no reason. The Save form renders no
    /// reason field, a Save posted without one reaches the workspace command
    /// (Core composes the history line), and this Case's notes travel with it.
    /// </summary>
    [Fact]
    public async Task SaveWithoutAReasonCarriesTheCaseNotesToTheWorkspaceCommand()
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await EnterEditModeAsync(store, services =>
            Substitute<ISaveCaseWorkspace>(services, store));

        var html = await workspace.GetWorkspaceAsync();
        var saveForm = Regex.Match(
            html,
            "<form[^>]*id=\"case-edit-form\"[^>]*>(?<body>(?:(?!</form>).)*)</form>",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);
        Assert.True(saveForm.Success, "The record's Save form must render in the edit session.");
        Assert.DoesNotContain("name=\"reason\"", saveForm.Value, StringComparison.Ordinal);

        using var response = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=Save",
            Form(
                workspace.AntiforgeryToken,
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", DetailsModelOperationKey),
                ("editLeaseToken", store.LeaseToken),
                ("principalNotes", "Handler prefers e-mail."),
                ("claimSourceNotes", "Quote the claim source's reference.")));

        AssertPrg(response, store.CaseId);
        var saved = Assert.Single(store.Saves);
        Assert.True(string.IsNullOrEmpty(saved.Reason), "No reason is posted, so none reaches Core.");
        Assert.Equal(store.LeaseToken, saved.EditLeaseToken);
        Assert.Equal("Handler prefers e-mail.", saved.Overview!.PrincipalNotes);
        Assert.Equal("Quote the claim source's reference.", saved.Overview.ClaimSourceNotes);
    }

    /// <summary>
    /// Enters edit mode as a staff member holding only <paramref name="role"/>,
    /// through the test authentication scheme, the way the operator does.
    /// </summary>
    private static async Task<LeasedWorkspace> EnterEditModeAsRoleAsync(
        RecordingCaseDetailsStore store,
        string role,
        Action<IServiceCollection> substitutePorts)
    {
        var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                Substitute<IGetCase>(services, store);
                Substitute<IGetCasePageFrame>(services, store);
                Substitute<IGetCaseVehicleSection>(services, store);
                Substitute<IGetCaseValuationSection>(services, store);
                Substitute<IGetCaseNotesSection>(services, store);
                Substitute<IGetCaseFilesSection>(services, store);
                Substitute<IAcquireCaseEditLease>(services, store);
                Substitute<IGetAssessmentWorkspace>(services, store);
                substitutePorts(services);
            }));
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);
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

    /// <summary>The store's own Case data with this Case's notes recorded on it.</summary>
    private static async Task<CaseDataProjection> WithCaseNotesAsync(
        RecordingCaseDetailsStore store, string? principalNotes, string? claimSourceNotes)
    {
        var details = await store.ExecuteAsync(
            new GetCaseQuery(
                store.CaseId,
                ActionActor.Staff(Pegasus.Web.Authentication.DevelopmentOfflineIdentity.AdministratorId, [StaffRole.Administrator])),
            CancellationToken.None);
        return details!.Data! with
        {
            Workspace = new CaseWorkspaceData(
                null, null, null, null, null, null, null, null, null, null, null,
                PrincipalNotes: principalNotes,
                ClaimSourceNotes: claimSourceNotes)
        };
    }

    private sealed partial class RecordingCaseDetailsStore : IAssignCaseToMe
    {
        /// <summary>The Case type the summary reports; a plain Inspection unless a test says otherwise.</summary>
        public CaseType SummaryCaseType { get; init; } = CaseType.Inspection;

        /// <summary>The Principal and Claim source records' notes the Case reads live.</summary>
        public CaseRecordNotes RecordNotes { get; init; } = CaseRecordNotes.None;

        /// <summary>The hold's review date the workflow reports.</summary>
        public DateOnly? HoldReviewOn { get; set; }

        public List<AssignCaseToMeRequest> SelfAssignments { get; } = [];

        Task<CaseWorkflowRecord> IAssignCaseToMe.ExecuteAsync(
            AssignCaseToMeRequest request,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            SelfAssignments.Add(request);
            return Task.FromResult(CreateWorkflow() with { AssignedEngineerId = Guid.NewGuid() });
        }
    }

    /// <summary>
    /// The frame's Create audit reads and the use case itself, substituted
    /// together so availability follows the seeded facts and the command is
    /// recorded rather than persisted.
    /// </summary>
    private sealed class RecordingAuditPorts(bool reportGenerated, CaseAuditLink? existing) :
        ICaseReportGeneratedQueries,
        ICaseAuditLinkQueries,
        ICreateAuditCase
    {
        public Guid AuditCaseId { get; } = Guid.NewGuid();

        public List<CreateAuditCaseRequest> Requests { get; } = [];

        public AuditCaseRefusal? Refusal { get; set; }

        public void Register(IServiceCollection services)
        {
            Substitute<ICaseReportGeneratedQueries>(services, this);
            Substitute<ICaseAuditLinkQueries>(services, this);
            Substitute<ICreateAuditCase>(services, this);
        }

        public Task<bool> HasGeneratedReportAsync(Guid caseId, CancellationToken cancellationToken) =>
            Task.FromResult(reportGenerated);

        public Task<CaseAuditLink?> GetAuditCaseAsync(Guid sourceCaseId, CancellationToken cancellationToken) =>
            Task.FromResult(existing);

        public Task<CaseAuditLink?> GetOriginalCaseAsync(Guid auditCaseId, CancellationToken cancellationToken) =>
            Task.FromResult<CaseAuditLink?>(null);

        public Task<CreateAuditCaseResult> ExecuteAsync(CreateAuditCaseRequest request, CancellationToken cancellationToken)
        {
            if (Refusal is { } refusal)
            {
                throw new AuditCaseCreationException(refusal);
            }
            Requests.Add(request);
            return Task.FromResult(new CreateAuditCaseResult(
                new CaseIdentity(request.CaseId, "QDOS", 2031, 42, "QDOS3100042"),
                new CaseIdentity(AuditCaseId, "QDOS", 2031, 42, "ap.QDOS3100042"),
                AuditAssessment.TotalLoss,
                false));
        }
    }
}
