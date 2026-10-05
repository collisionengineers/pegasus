using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Operations;
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
    /// Outside an edit session the menu offers the state's items too
    /// (operator, 29 September 2026), their dialogs posting no lease so the
    /// handler claims one for the action. A colleague's live lease offers
    /// only Take over: nothing that needs the lease is on the menu.
    /// </summary>
    [Fact]
    public async Task OutsideAnEditSessionTheMenuOffersTheStatesItemsAndAColleaguesLeaseOffersOnlyTakeOver()
    {
        var store = new RecordingCaseDetailsStore();
        var page = await ReadCaseAsync(store);
        var reading = RecordBar(page);
        Assert.Equal(1, Occurrences(reading, "data-case-actions"));
        Assert.Contains("data-dialog-open=\"case-hold-dialog\"", reading, StringComparison.Ordinal);
        Assert.Contains("data-dialog-open=\"case-correct-principal-dialog\"", reading, StringComparison.Ordinal);
        Assert.Contains(CaseWorkspaceLabels.Frame.EditCase, reading, StringComparison.Ordinal);
        var hold = Section(page, "case-hold-dialog-title");
        Assert.Contains("handler=Hold", hold, StringComparison.Ordinal);
        // No lease to post: a page in read mode carries no lease field at all.
        Assert.DoesNotContain("name=\"editLeaseToken\"", hold, StringComparison.Ordinal);

        var held = new RecordingCaseDetailsStore { LeaseHolder = "colleague-staff-id" };
        var html = await ReadCaseAsync(held);
        Assert.DoesNotContain("data-case-actions", RecordBar(html), StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"case-hold-dialog\"", html, StringComparison.Ordinal);
        Assert.Contains("data-edit-authority", html, StringComparison.Ordinal);
        Assert.Contains(CaseWorkspaceLabels.Frame.TakeOver, html, StringComparison.Ordinal);
        Assert.Contains("handler=ClaimLease", html, StringComparison.Ordinal);
        Assert.Contains("name=\"takeOver\" value=\"true\"", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// A Completed or Query Case offers no Edit, but a colleague's lease on
    /// it is still offered Take over (operator, 29 September 2026): the
    /// Actions menu is withheld while the colleague holds the Case, so Take
    /// over is the way in before the lease lapses.
    /// </summary>
    [Theory]
    [InlineData(CaseLifecycleState.PostReportComplete)]
    [InlineData(CaseLifecycleState.Query)]
    public async Task ACompletedOrQueryCaseOffersTakeOverOfAColleaguesLeaseButNoEdit(CaseLifecycleState state)
    {
        var reading = RecordBar(await ReadCaseAsync(new RecordingCaseDetailsStore { State = state }));
        Assert.DoesNotContain(CaseWorkspaceLabels.Frame.EditCase, reading, StringComparison.Ordinal);
        Assert.DoesNotContain("handler=ClaimLease", reading, StringComparison.Ordinal);

        var held = RecordBar(await ReadCaseAsync(new RecordingCaseDetailsStore { State = state, LeaseHolder = "colleague-staff-id" }));
        Assert.DoesNotContain("data-case-actions", held, StringComparison.Ordinal);
        Assert.Contains(CaseWorkspaceLabels.Frame.TakeOver, held, StringComparison.Ordinal);
        Assert.Contains("handler=ClaimLease", held, StringComparison.Ordinal);
        Assert.Contains("name=\"takeOver\" value=\"true\"", held, StringComparison.Ordinal);
    }

    /// <summary>
    /// An Actions-menu item taken outside an edit session runs under a lease
    /// the handler claims for it, the way Generate report does: one claim on
    /// the Case's version, the command under that token, and nothing to
    /// release afterwards because the command consumed the lease. The page
    /// reads on; no session was opened.
    /// </summary>
    [Fact]
    public async Task PlaceOnHoldOutsideAnEditSessionRunsUnderALeaseClaimedForIt()
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await OpenEngineerWorkspaceAsync(store, services =>
        {
            Substitute<IHoldCase>(services, store);
            Substitute<IReleaseCaseEditLease>(services, store);
        });
        Assert.DoesNotContain("data-case-editing=\"true\"", await workspace.GetWorkspaceAsync(), StringComparison.Ordinal);

        using var response = await workspace.PostAsync(
            "Workflow?handler=Hold",
            Form(
                workspace.AntiforgeryToken,
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", "hold-outside-a-session"),
                ("editLeaseToken", string.Empty),
                ("reason", "Awaiting the claimant's photographs")));

        AssertPrg(response, store.CaseId);
        var claim = Assert.Single(store.Claims);
        Assert.Equal(store.CaseVersion, claim.ExpectedVersion);
        var hold = Assert.Single(store.Holds);
        Assert.Equal(store.LeaseToken, hold.EditLeaseToken);
        Assert.Equal(store.CaseVersion, hold.ExpectedVersion);
        Assert.Equal(claim.Actor.SubjectId, hold.Actor.SubjectId);
        Assert.Empty(store.LeaseReleases);
        Assert.Null(store.LeaseHolder);
        var after = await workspace.GetWorkspaceAsync();
        Assert.DoesNotContain("data-case-editing=\"true\"", after, StringComparison.Ordinal);
        Assert.Contains("The case was put on hold.", after, StringComparison.Ordinal);
    }

    /// <summary>
    /// A command the Case refuses frees the lease that was claimed for it, so
    /// nothing is left holding the Case, and states the refusal.
    /// </summary>
    [Fact]
    public async Task ARefusedActionOutsideAnEditSessionFreesTheLeaseItClaimed()
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await OpenEngineerWorkspaceAsync(store, services =>
        {
            Substitute<IHoldCase>(services, new RefusingHold());
            Substitute<IReleaseCaseEditLease>(services, store);
        });

        using var response = await workspace.PostAsync(
            "Workflow?handler=Hold",
            Form(
                workspace.AntiforgeryToken,
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", "hold-refused"),
                ("editLeaseToken", string.Empty),
                ("reason", "Awaiting the claimant's photographs")));

        AssertPrg(response, store.CaseId);
        Assert.Single(store.Claims);
        Assert.Empty(store.Holds);
        var release = Assert.Single(store.LeaseReleases);
        Assert.Equal(store.LeaseToken, release.LeaseToken);
        Assert.Null(store.LeaseHolder);
        var after = await workspace.GetWorkspaceAsync();
        Assert.DoesNotContain("data-case-editing=\"true\"", after, StringComparison.Ordinal);
        Assert.Contains("The case action was not applied", after, StringComparison.Ordinal);
    }

    /// <summary>
    /// A colleague's live lease refuses the claim with the claim's own
    /// wording: the command never runs and nothing is released.
    /// </summary>
    [Fact]
    public async Task AColleaguesLeaseRefusesAnActionOutsideAnEditSessionWithTheEditingWording()
    {
        var store = new RecordingCaseDetailsStore { LeaseHolder = Guid.NewGuid().ToString("D") };
        using var workspace = await OpenEngineerWorkspaceAsync(store, services =>
        {
            Substitute<IHoldCase>(services, store);
            Substitute<IReleaseCaseEditLease>(services, store);
        });

        store.NextFailure = new CaseEditLeaseConflictException(store.CaseId, store.CaseVersion);
        using var response = await workspace.PostAsync(
            "Workflow?handler=Hold",
            Form(
                workspace.AntiforgeryToken,
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", "hold-while-a-colleague-edits"),
                ("editLeaseToken", string.Empty),
                ("reason", "Awaiting the claimant's photographs")));

        AssertPrg(response, store.CaseId);
        Assert.Empty(store.Holds);
        Assert.Empty(store.LeaseReleases);
        Assert.Contains(
            "Someone else is editing this case.",
            await workspace.GetWorkspaceAsync(),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// A request abandoned mid-command (the browser went away) frees the
    /// lease that was claimed for it, so the Case is not held until the
    /// lease lapses.
    /// </summary>
    [Fact]
    public async Task AnAbandonedActionOutsideAnEditSessionFreesTheLeaseItClaimed()
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await OpenEngineerWorkspaceAsync(store, services =>
        {
            Substitute<IHoldCase>(services, new AbandonedHold());
            Substitute<IReleaseCaseEditLease>(services, store);
        });

        try
        {
            using var response = await workspace.PostAsync(
                "Workflow?handler=Hold",
                Form(
                    workspace.AntiforgeryToken,
                    ("id", store.CaseId.ToString("D")),
                    ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                    ("operationKey", "hold-abandoned"),
                    ("reason", "Awaiting the claimant's photographs")));
        }
        catch (OperationCanceledException)
        {
            // The host may surface the abandoned request to the caller.
        }

        Assert.Single(store.Claims);
        Assert.Empty(store.Holds);
        var release = Assert.Single(store.LeaseReleases);
        Assert.Equal(store.LeaseToken, release.LeaseToken);
        Assert.Null(store.LeaseHolder);
    }

    private sealed class RefusingHold : IHoldCase
    {
        public Task<CaseWorkflowRecord> ExecuteAsync(PutCaseOnHoldRequest request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The hold was refused.");
    }

    private sealed class AbandonedHold : IHoldCase
    {
        public Task<CaseWorkflowRecord> ExecuteAsync(PutCaseOnHoldRequest request, CancellationToken cancellationToken) =>
            throw new OperationCanceledException("The request was abandoned.");
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
    /// Hand to Engineer on a Review Case is the select. Assign to me is not a
    /// second way to take the Case.
    /// </summary>
    [Fact]
    public async Task HandToEngineerDoesNotOfferAssignToMe()
    {
        var store = new RecordingCaseDetailsStore { State = CaseLifecycleState.Review };
        using var workspace = await EnterEngineerEditModeAsync(store, services =>
        {
            Substitute<IStaffAccountQueries>(services, new StubStaffAccounts(Guid.NewGuid(), "User", StaffRole.User));
        }, StaffRole.User);
        var leased = await workspace.GetWorkspaceAsync();
        var dialog = Section(leased, "case-handoff-dialog-title");
        Assert.Contains("handler=AssignEngineer", dialog, StringComparison.Ordinal);
        Assert.DoesNotContain("handler=AssignToMe", dialog, StringComparison.Ordinal);
        Assert.DoesNotContain("Assign to me", dialog, StringComparison.Ordinal);
    }

    /// <summary>
    /// Create audit is listed, after Correct principal, on every Inspection +
    /// Audit Case and never on another type (operator, 1 October 2026). It is
    /// live exactly where Core's Audit policy finds no refusal, whether or not
    /// the Inspection report is sent; otherwise it is greyed out and its hover
    /// title states the refusal, and no dialog is rendered. The dialog and the
    /// post are covered by <see cref="CaseViewsWebTests"/>.
    /// </summary>
    [Theory]
    [InlineData(CaseType.InspectionAndAudit, CaseLifecycleState.PostReport, true, false, null)]
    [InlineData(CaseType.InspectionAndAudit, CaseLifecycleState.PostReportComplete, true, false, null)]
    [InlineData(CaseType.InspectionAndAudit, CaseLifecycleState.Query, true, false, null)]
    [InlineData(CaseType.InspectionAndAudit, CaseLifecycleState.ReportPreparation, false, false, null)]
    [InlineData(CaseType.InspectionAndAudit, CaseLifecycleState.Review, false, false, null)]
    [InlineData(CaseType.InspectionAndAudit, CaseLifecycleState.Held, true, false, "A held case cannot have an audit created from it.")]
    [InlineData(CaseType.InspectionAndAudit, CaseLifecycleState.PostReport, true, true, "This case already has its audit.")]
    [InlineData(CaseType.Inspection, CaseLifecycleState.PostReport, true, false, null)]
    public async Task CreateAuditIsListedOnEveryInspectionAndAuditCaseAndLiveWhereTheAuditPolicyAccepts(
        CaseType caseType, CaseLifecycleState state, bool sent, bool auditExists, string? condition)
    {
        var store = new RecordingCaseDetailsStore
        {
            State = state,
            SummaryCaseType = caseType,
            AssignedEngineerId = Guid.NewGuid(),
            ReportSentEvidence = sent ? SentEvidence(new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero)) : null
        };
        if (auditExists)
        {
            store.GiveAudit(new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero));
        }
        using var workspace = await EnterEditModeAsync(store, _ => { });

        var html = await workspace.GetWorkspaceAsync();
        var bar = WebUtility.HtmlDecode(RecordBar(html));
        var listed = caseType == CaseType.InspectionAndAudit;
        var live = listed && condition is null;

        Assert.Equal(listed, bar.Contains("data-create-audit", StringComparison.Ordinal));
        Assert.Equal(live, bar.Contains("data-dialog-open=\"case-create-audit-dialog\"", StringComparison.Ordinal));
        Assert.Equal(live, html.Contains("data-dialog=\"case-create-audit-dialog\"", StringComparison.Ordinal));
        if (condition is not null)
        {
            Assert.Contains($"<span class=\"menu-gated\" title=\"{condition}\" data-create-audit-condition=\"{condition}\">", bar, StringComparison.Ordinal);
            Assert.Contains("class=\"btn\" disabled aria-disabled=\"true\" data-create-audit>", bar, StringComparison.Ordinal);
        }
        else
        {
            Assert.DoesNotContain("data-create-audit-condition", bar, StringComparison.Ordinal);
            Assert.DoesNotContain("menu-gated", bar, StringComparison.Ordinal);
        }
        if (listed && bar.Contains("data-dialog-open=\"case-correct-principal-dialog\"", StringComparison.Ordinal))
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
    /// While a colleague holds the lease, Create audit stays listed but greyed
    /// out, its hover title naming them with the sections' own editing
    /// wording, and no dialog is rendered.
    /// </summary>
    [Fact]
    public async Task CreateAuditIsGreyedOutWithTheEditingWordingWhileAColleagueHoldsTheLease()
    {
        var store = new RecordingCaseDetailsStore
        {
            State = CaseLifecycleState.ReportPreparation,
            SummaryCaseType = CaseType.InspectionAndAudit,
            AssignedEngineerId = Guid.NewGuid(),
            LeaseHolder = Guid.NewGuid().ToString("D")
        };
        using var workspace = await OpenEngineerWorkspaceAsync(store, _ => { });

        var html = await workspace.GetWorkspaceAsync();
        var bar = WebUtility.HtmlDecode(RecordBar(html));

        Assert.Matches("<span class=\"menu-gated\" title=\"[^\"]+ is editing\" data-create-audit-condition=\"[^\"]+ is editing\">", bar);
        Assert.Contains("class=\"btn\" disabled aria-disabled=\"true\" data-create-audit>", bar, StringComparison.Ordinal);
        Assert.DoesNotContain("data-dialog-open=\"case-create-audit-dialog\"", bar, StringComparison.Ordinal);
        Assert.DoesNotContain("data-dialog=\"case-create-audit-dialog\"", html, StringComparison.Ordinal);
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

    /// <summary>
    /// Send chaser is offered on any open Case while staff mail is available
    /// (operator, 5 October 2026): a link to the composer opened as this
    /// Case's chaser, with no dialog and no lease. A closed Case, archived or
    /// not, and a host without staff mail offer none.
    /// </summary>
    [Fact]
    public async Task SendChaserIsOfferedOnAnOpenCaseWhileStaffMailIsAvailable()
    {
        static void Mail(IServiceCollection services) =>
            Substitute<IStaffMailSend>(services, new OfferedStaffMailSend());

        var open = RecordBar(await ReadCaseAsync(new RecordingCaseDetailsStore(), Mail));
        var link = Regex.Match(open, "<a[^>]*data-send-chaser[^>]*>");
        Assert.True(link.Success, "Send chaser is not offered on an open Case.");
        Assert.Contains(
            "href=\"/Inbox/Compose?caseReference=QDOS3100042&amp;purpose=chaser\"",
            link.Value,
            StringComparison.Ordinal);
        Assert.DoesNotContain("data-dialog-open", link.Value, StringComparison.Ordinal);
        Assert.Contains(CaseWorkspaceLabels.Frame.SendChaser, open, StringComparison.Ordinal);

        var closed = RecordBar(await ReadCaseAsync(
            new RecordingCaseDetailsStore { State = CaseLifecycleState.PrincipalCancelled }, Mail));
        Assert.DoesNotContain("data-send-chaser", closed, StringComparison.Ordinal);

        var offline = RecordBar(await ReadCaseAsync(new RecordingCaseDetailsStore()));
        Assert.DoesNotContain("data-send-chaser", offline, StringComparison.Ordinal);
    }

    /// <summary>A staff mail sender the page reads as available; the frame never sends.</summary>
    private sealed class OfferedStaffMailSend : IStaffMailSend
    {
        public Task<StaffMailOperation> SendAsync(StaffMailSendCommand command, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<StaffMailOperation?> GetAsync(ActionActor actor, Guid operationId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<StaffMailOperation?> GetLatestForOriginalAsync(
            ActionActor actor, Guid retainedMessageId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<StaffMailOperation> ReconcileAsync(
            ActionActor actor, Guid operationId, long expectedVersion, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<StaffMailOperation> CancelAsync(
            ActionActor actor, Guid operationId, long expectedVersion, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    /// <summary>The store's own Case data with this Case's notes recorded on it.</summary>
    private static async Task<CaseDataProjection> WithCaseNotesAsync(
        RecordingCaseDetailsStore store, string? principalNotes, string? claimSourceNotes)
    {
        var data = await store.GetAsync(store.CaseId, CaseWorkSelector.Current, CancellationToken.None);
        return data! with
        {
            Workspace = new CaseWorkspaceData(
                null, null, null, null, null, null, null, null, null, null, null,
                PrincipalNotes: principalNotes,
                ClaimSourceNotes: claimSourceNotes)
        };
    }}
