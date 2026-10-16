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
    /// edit session (v25 § Coverage): the hold pair swaps with the state, and
    /// Log query, Return to Review and Return to Engineer appear once the
    /// report is sent (operator, 11 October 2026).
    /// </summary>
    [Theory]
    [InlineData(CaseLifecycleState.NotReady,
        "case-hold-dialog,case-correct-principal-dialog",
        "case-release-hold-dialog,case-log-query-dialog,case-return-review-dialog,case-return-engineer-dialog")]
    [InlineData(CaseLifecycleState.Held,
        "case-release-hold-dialog,case-correct-principal-dialog",
        "case-hold-dialog,case-log-query-dialog,case-return-engineer-dialog")]
    [InlineData(CaseLifecycleState.ReportPreparation,
        "case-hold-dialog,case-correct-principal-dialog",
        "case-release-hold-dialog")]
    [InlineData(CaseLifecycleState.ReportSent,
        "case-log-query-dialog,case-return-review-dialog,case-return-engineer-dialog,case-correct-principal-dialog",
        "case-hold-dialog,case-release-hold-dialog,case-complete-dialog,case-close-dialog")]
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
    /// Take over, and the menu keeps only Export case and Tasks, which need no lease.
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
        var heldBar = RecordBar(html);
        Assert.Contains("data-case-export", heldBar, StringComparison.Ordinal);
        // Tasks, which reads without a lease, is the one dialog the menu still opens.
        Assert.Equal(1, Occurrences(heldBar, "data-dialog-open"));
        Assert.Contains("data-dialog-open=\"case-tasks-dialog\"", heldBar, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"case-hold-dialog\"", html, StringComparison.Ordinal);
        Assert.Contains("data-edit-authority", html, StringComparison.Ordinal);
        Assert.Contains(CaseWorkspaceLabels.Frame.TakeOver, html, StringComparison.Ordinal);
        Assert.Contains("handler=ClaimLease", html, StringComparison.Ordinal);
        Assert.Contains("name=\"takeOver\" value=\"true\"", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// A Report sent Case, with or without an open query, is editable like a
    /// With Engineer Case (operator, 11 October 2026): it offers Edit Case,
    /// and a colleague's lease on it is offered Take over (operator, 29
    /// September 2026) while the Actions menu keeps only Export case and Tasks.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AReportSentCaseOffersEditAndTakeOverOfAColleaguesLease(bool hasOpenQuery)
    {
        const CaseLifecycleState state = CaseLifecycleState.ReportSent;
        var reading = RecordBar(await ReadCaseAsync(new RecordingCaseDetailsStore { State = state, HasOpenQuery = hasOpenQuery }));
        Assert.Contains(CaseWorkspaceLabels.Frame.EditCase, reading, StringComparison.Ordinal);
        Assert.Contains("handler=ClaimLease", reading, StringComparison.Ordinal);
        Assert.Contains("data-case-edit-form", reading, StringComparison.Ordinal);

        var held = RecordBar(await ReadCaseAsync(new RecordingCaseDetailsStore { State = state, HasOpenQuery = hasOpenQuery, LeaseHolder = "colleague-staff-id" }));
        Assert.Contains("data-case-export", held, StringComparison.Ordinal);
        Assert.Equal(1, Occurrences(held, "data-dialog-open"));
        Assert.Contains("data-dialog-open=\"case-tasks-dialog\"", held, StringComparison.Ordinal);
        Assert.Contains(CaseWorkspaceLabels.Frame.TakeOver, held, StringComparison.Ordinal);
        Assert.Contains("handler=ClaimLease", held, StringComparison.Ordinal);
        Assert.Contains("name=\"takeOver\" value=\"true\"", held, StringComparison.Ordinal);
    }

    /// <summary>
    /// Core holds a Case only from Not ready, Review or With Engineer, so the
    /// page of a Report sent Case draws neither the Place on Hold item nor its
    /// dialog; Correct principal stays.
    /// </summary>
    [Fact]
    public async Task AReportSentCaseOffersNoPlaceOnHoldAndNoHoldDialog()
    {
        var page = await ReadCaseAsync(new RecordingCaseDetailsStore { State = CaseLifecycleState.ReportSent });

        Assert.DoesNotContain("data-dialog-open=\"case-hold-dialog\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"case-hold-dialog\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain("handler=Hold", page, StringComparison.Ordinal);
        Assert.Contains("data-dialog-open=\"case-correct-principal-dialog\"", page, StringComparison.Ordinal);
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
    /// Assign Engineer on a Review Case is the select. Assign to me is not a
    /// second way to take the Case. The Next action opens the Actions menu's
    /// own dialog rather than jumping to a section (issue 1025), and the step
    /// is that one control, not its words beside it (operator, 8 October 2026).
    /// </summary>
    [Fact]
    public async Task AssignEngineerDoesNotOfferAssignToMe()
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
        Assert.Contains(
            "<button type=\"button\" class=\"btn\" data-dialog-open=\"case-handoff-dialog\">",
            RecordBar(leased),
            StringComparison.Ordinal);
        Assert.Matches(
            "<div class=\"next-step\" data-next-step>\\s*"
                + "<button type=\"button\" class=\"btn next-step-go\" data-dialog-open=\"case-handoff-dialog\" data-next-assign-engineer>Assign Engineer</button>\\s*</div>",
            leased);
        Assert.DoesNotContain("<strong data-next-label>Assign Engineer</strong>", leased, StringComparison.Ordinal);
    }

    /// <summary>
    /// While a colleague holds the lease the Actions menu does not offer
    /// Assign Engineer, so the Next action names the step without a control
    /// and no dialog is rendered.
    /// </summary>
    [Fact]
    public async Task AssignEngineerIsNamedWithoutAControlWhileAColleagueHoldsTheLease()
    {
        var store = new RecordingCaseDetailsStore
        {
            State = CaseLifecycleState.Review,
            LeaseHolder = Guid.NewGuid().ToString("D")
        };
        using var workspace = await OpenEngineerWorkspaceAsync(store, services =>
        {
            Substitute<IStaffAccountQueries>(services, new StubStaffAccounts(Guid.NewGuid(), "User", StaffRole.User));
        });

        var html = await workspace.GetWorkspaceAsync();

        Assert.Matches("<div class=\"next-step\" data-next-step>\\s*<strong data-next-label>Assign Engineer</strong>\\s*</div>", html);
        Assert.DoesNotContain("data-next-assign-engineer", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-dialog-open=\"case-handoff-dialog\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-dialog=\"case-handoff-dialog\"", html, StringComparison.Ordinal);
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
    [InlineData(CaseType.InspectionAndAudit, CaseLifecycleState.ReportSent, true, false, null)]
    [InlineData(CaseType.InspectionAndAudit, CaseLifecycleState.ReportPreparation, false, false, null)]
    [InlineData(CaseType.InspectionAndAudit, CaseLifecycleState.Review, false, false, null)]
    [InlineData(CaseType.InspectionAndAudit, CaseLifecycleState.Held, true, false, "A held case cannot have an audit created from it.")]
    [InlineData(CaseType.InspectionAndAudit, CaseLifecycleState.ReportSent, true, true, "This case already has its audit.")]
    [InlineData(CaseType.Inspection, CaseLifecycleState.ReportSent, true, false, null)]
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
    /// (operator, 5 October 2026): a link to this Case's chaser page
    /// (operator, 12 October 2026), with no dialog and no lease. A closed
    /// Case, archived or not, and a host without staff mail offer none.
    /// </summary>
    [Fact]
    public async Task SendChaserIsOfferedOnAnOpenCaseWhileStaffMailIsAvailable()
    {
        static void Mail(IServiceCollection services) =>
            Substitute<IStaffMailSend>(services, new OfferedStaffMailSend());

        var store = new RecordingCaseDetailsStore();
        var open = RecordBar(await ReadCaseAsync(store, Mail));
        var link = Regex.Match(open, "<a[^>]*data-send-chaser[^>]*>");
        Assert.True(link.Success, "Send chaser is not offered on an open Case.");
        Assert.Contains(
            $"href=\"/Cases/{store.CaseId:D}/Chaser\"",
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
