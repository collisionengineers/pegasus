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

using static Pegasus.IntegrationTests.CaseWebTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The v26 Case record frame and the 13 September settlements: the one
/// Actions menu per state, Place on Hold with an optional review date, Assign
/// to me, Create audit, the Notes band, and a Save that asks for no reason.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class CaseRecordFrameV26WebTests
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
    /// needs no lease. A colleague's live lease offers Take over.
    /// </summary>
    [Fact]
    public async Task OutsideAnEditSessionTheMenuIsAbsentAndAColleaguesLeaseOffersTakeOver()
    {
        var store = new RecordingCaseDetailsStore();
        var reading = RecordBar(await ReadCaseAsync(store));
        Assert.DoesNotContain("data-case-actions", reading, StringComparison.Ordinal);
        Assert.Contains(CaseWorkspaceLabels.Frame.EditCase, reading, StringComparison.Ordinal);

        var held = new RecordingCaseDetailsStore { LeaseHolder = "colleague-staff-id" };
        var html = await ReadCaseAsync(held);
        Assert.Contains("data-edit-authority", html, StringComparison.Ordinal);
        Assert.Contains(CaseWorkspaceLabels.Frame.TakeOver, html, StringComparison.Ordinal);
        Assert.Contains("handler=ClaimLease", html, StringComparison.Ordinal);
        Assert.Contains("name=\"takeOver\" value=\"true\"", html, StringComparison.Ordinal);
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
    /// P8: a User on a Review Case with no assignee gets Assign to me in the
    /// assignment dialog, as its own form, and the post reaches the
    /// self-assignment command with the session's envelope.
    /// </summary>
    [Fact]
    public async Task AssignToMeIsOfferedToAUserAndPostsTheSelfAssignment()
    {
        var store = new RecordingCaseDetailsStore { State = CaseLifecycleState.Review };
        using var workspace = await EnterEngineerEditModeAsync(store, services =>
        {
            Substitute<IStaffAccountQueries>(services, new StubStaffAccounts(Guid.NewGuid(), "User", StaffRole.User));
            Substitute<IAssignCaseToMe>(services, store);
        }, StaffRole.User);
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
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", operationKey),
                ("editLeaseToken", store.LeaseToken)));
        AssertPrg(response, store.CaseId);
        var assignment = Assert.Single(store.SelfAssignments);
        AssertClaimant(workspace, assignment.Actor);
        Assert.Equal(store.CaseId, assignment.CaseId);
        Assert.Equal(store.CaseVersion, assignment.ExpectedVersion);
        Assert.Equal(store.LeaseToken, assignment.EditLeaseToken);
        Assert.Equal(operationKey, assignment.OperationKey);
        Assert.Contains("The case was assigned to you.", await workspace.GetWorkspaceAsync(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Create audit (v29 P5) is offered, inside the edit session and after
    /// Correct principal, exactly where Core's Audit policy finds no refusal:
    /// an Inspection + Audit Case whose report is sent (Post-report, Complete
    /// or Query, never Held), with its Engineer and no Audit yet. The dialog
    /// and the post are covered by <see cref="CaseViewsWebTests"/>.
    /// </summary>
    [Theory]
    [InlineData(CaseType.InspectionAndAudit, CaseLifecycleState.PostReport, false, true)]
    [InlineData(CaseType.InspectionAndAudit, CaseLifecycleState.PostReportComplete, false, true)]
    [InlineData(CaseType.InspectionAndAudit, CaseLifecycleState.Query, false, true)]
    [InlineData(CaseType.InspectionAndAudit, CaseLifecycleState.ReportPreparation, false, false)]
    [InlineData(CaseType.InspectionAndAudit, CaseLifecycleState.Held, false, false)]
    [InlineData(CaseType.Inspection, CaseLifecycleState.PostReport, false, false)]
    [InlineData(CaseType.InspectionAndAudit, CaseLifecycleState.PostReport, true, false)]
    public async Task CreateAuditIsOfferedOnlyWhereTheAuditPolicyAccepts(
        CaseType caseType, CaseLifecycleState state, bool auditExists, bool offered)
    {
        var store = new RecordingCaseDetailsStore
        {
            State = state,
            SummaryCaseType = caseType,
            AssignedEngineerId = Guid.NewGuid(),
            ReportSentEvidence = SentEvidence(new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero))
        };
        if (auditExists)
        {
            store.GiveAudit(new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero));
        }
        using var workspace = await EnterEditModeAsync(store, _ => { });

        var html = await workspace.GetWorkspaceAsync();
        var bar = RecordBar(html);

        Assert.Equal(offered, bar.Contains("data-create-audit", StringComparison.Ordinal));
        Assert.Equal(offered, html.Contains("data-dialog=\"case-create-audit-dialog\"", StringComparison.Ordinal));
        if (offered && bar.Contains("data-dialog-open=\"case-correct-principal-dialog\"", StringComparison.Ordinal))
        {
            Assert.True(
                bar.IndexOf("data-dialog-open=\"case-correct-principal-dialog\"", StringComparison.Ordinal)
                    < bar.IndexOf("data-create-audit", StringComparison.Ordinal),
                "Create audit stays after Correct principal.");
        }
        Assert.Equal(
            caseType == CaseType.InspectionAndAudit,
            WebUtility.HtmlDecode(html).Contains("data-case-type-chip>Inspection + Audit<", StringComparison.Ordinal));
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
        // The record's notes stay a read-only cell inside the session.
        Assert.Contains("<div class=\"fc ro\" data-record-notes=\"principal\">", editing, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OverviewKeepsItsContactNotesAndAccidentBandsInsideBalancedMarkup()
    {
        var reading = OverviewPanel(await ReadCaseAsync(new RecordingCaseDetailsStore()));
        AssertOverviewBands(reading);

        using var workspace = await EnterEditModeAsync(new RecordingCaseDetailsStore(), _ => { });
        var editing = OverviewPanel(await workspace.GetWorkspaceAsync());
        AssertOverviewBands(editing);
    }

    private static void AssertOverviewBands(string overview)
    {
        AssertBalancedMarkup(overview);
        Assert.DoesNotContain("}", overview, StringComparison.Ordinal);
        Assert.Contains("data-collapse=\"case.overview.contact\"", overview, StringComparison.Ordinal);
        Assert.Contains("data-notes-band", overview, StringComparison.Ordinal);
        Assert.Contains("data-accident-band", overview, StringComparison.Ordinal);
        var sectionEnd = overview.IndexOf("</section>", StringComparison.Ordinal);
        Assert.True(sectionEnd > overview.IndexOf("data-collapse=\"case.overview.contact\"", StringComparison.Ordinal));
        Assert.True(sectionEnd > overview.IndexOf("data-notes-band", StringComparison.Ordinal));
        Assert.True(sectionEnd > overview.IndexOf("data-accident-band", StringComparison.Ordinal));
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
    }}
