using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;
using Pegasus.Web.Presentation;

using static Pegasus.IntegrationTests.CaseWebTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The Closure page: report approval is covered beside the workspace tests; these cover the
/// terminal outcomes, reopening through the destination gates, and archiving.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class CaseClosureWebTests
{
    [Fact]
    public async Task ClosurePageBindsTerminalOutcomeReopenGatesAndArchive()
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await EnterEditModeAsync(store, services =>
        {
            Substitute<ICloseCase>(services, store);
            Substitute<IReopenCase>(services, store);
            Substitute<IArchiveCase>(services, store);
        });

        using var closed = await workspace.PostAsync(
            "Closure?handler=Close",
            workspace.MutationForm("close-case", "Principal withdrew the instruction", ("outcome", "PrincipalCancelled")));
        using var reopenedToReview = await workspace.PostAsync(
            "Closure?handler=Reopen",
            workspace.MutationForm(
                "reopen-review",
                "Principal reinstated the instruction",
                ("destination", "Review")));
        using var reopenedToNotReady = await workspace.PostAsync(
            "Closure?handler=Reopen",
            workspace.MutationForm("reopen-not-ready", "Images still outstanding", ("destination", "NotReady")));
        using var reopenedWithNoDestination = await workspace.PostAsync(
            "Closure?handler=Reopen",
            workspace.MutationForm("reopen-no-destination", "Images still outstanding"));
        using var archived = await workspace.PostAsync(
            "Closure?handler=Archive",
            workspace.MutationForm("archive-case", "Retention period reached"));

        AssertPrg(closed, store.CaseId);
        AssertPrg(reopenedToReview, store.CaseId);
        AssertPrg(reopenedToNotReady, store.CaseId);
        AssertPrg(reopenedWithNoDestination, store.CaseId);
        AssertPrg(archived, store.CaseId);

        var closure = Assert.Single(store.Closures);
        AssertLeasedMutation(workspace, closure, "close-case", "Principal withdrew the instruction");
        Assert.Equal(CaseClosureOutcome.PrincipalCancelled, closure.Outcome);

        Assert.Equal(3, store.Reopenings.Count);
        var toReview = store.Reopenings[0];
        AssertLeasedMutation(workspace, toReview, "reopen-review", "Principal reinstated the instruction");
        Assert.Equal(CaseReopenDestination.Review, toReview.Destination);
        var toNotReady = store.Reopenings[1];
        Assert.Equal("reopen-not-ready", toNotReady.OperationKey);
        Assert.Equal(CaseReopenDestination.NotReady, toNotReady.Destination);
        var withNoDestination = store.Reopenings[2];
        Assert.Equal("reopen-no-destination", withNoDestination.OperationKey);
        Assert.Null(withNoDestination.Destination);

        var archive = Assert.Single(store.Archives);
        AssertLeasedMutation(workspace, archive, "archive-case", "Retention period reached");
        var html = await workspace.GetWorkspaceAsync();
        Assert.Contains("The terminal case was archived and is now read-only.", html, StringComparison.Ordinal);

        await AssertRefusalKeepsEditModeAsync(
            workspace,
            "Closure?handler=Close",
            workspace.MutationForm("close-case-2", "Already closed", ("outcome", "CollisionEngineersRejected")));
    }

    /// <summary>
    /// Return to work (R157, v41 K10): a closed Case other than Created in
    /// error lists it in the Actions menu before Archive case. Its dialog, the
    /// Archive dialog's frame, takes the stage to return to by its stage name
    /// and a reason, and posts the Closure page's Reopen handler, which claims
    /// a lease for the action outside an edit session.
    /// </summary>
    [Theory]
    [InlineData(CaseLifecycleState.PrincipalCancelled, true)]
    [InlineData(CaseLifecycleState.CollisionEngineersRejected, true)]
    [InlineData(CaseLifecycleState.SourceEmailUnlinked, true)]
    [InlineData(CaseLifecycleState.CreatedInError, false)]
    [InlineData(CaseLifecycleState.ReportSent, false)]
    public async Task AClosedCaseOffersReturnToWorkBeforeArchive(CaseLifecycleState state, bool offered)
    {
        var store = new RecordingCaseDetailsStore { State = state };
        using var workspace = await OpenEngineerWorkspaceAsync(store, services =>
            Substitute<IReopenCase>(services, store));

        var html = WebUtility.HtmlDecode(await workspace.GetWorkspaceAsync());
        var bar = RecordBar(html);

        var item = bar.IndexOf("data-dialog-open=\"case-return-to-work-dialog\"", StringComparison.Ordinal);
        Assert.Equal(offered, item >= 0);
        Assert.Equal(offered, html.Contains("data-dialog=\"case-return-to-work-dialog\"", StringComparison.Ordinal));
        if (!offered)
        {
            return;
        }

        Assert.Contains($"<span>{CaseWorkspaceLabels.Frame.ReturnToWork}</span>", bar[item..], StringComparison.Ordinal);
        Assert.True(
            item < bar.IndexOf("data-dialog-open=\"case-archive-dialog\"", StringComparison.Ordinal),
            "Return to work is listed before Archive case.");
        var dialog = Section(html, "case-return-to-work-dialog_title");
        Assert.Contains($">{CaseWorkspaceLabels.Frame.ReturnToWork}</h2>", dialog, StringComparison.Ordinal);
        Assert.Contains($"/Cases/{store.CaseId:D}/Closure?handler=Reopen", dialog, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($">{CaseWorkspaceLabels.Frame.ReturnToWorkStage}</label>", dialog, StringComparison.Ordinal);
        Assert.Equal(
            [
                ("", ""),
                ("NotReady", OperatorLabels.CaseStage(CaseLifecycleState.NotReady)),
                ("Review", OperatorLabels.CaseStage(CaseLifecycleState.Review)),
                ("ReportPreparation", OperatorLabels.CaseStage(CaseLifecycleState.ReportPreparation)),
                ("ReportSent", OperatorLabels.CaseStage(CaseLifecycleState.ReportSent))
            ],
            System.Text.RegularExpressions.Regex.Matches(dialog, "<option value=\"(?<value>[^\"]*)\">(?<label>[^<]*)</option>")
                .Select(option => (option.Groups["value"].Value, option.Groups["label"].Value))
                .ToArray());
        Assert.Matches("<select[^>]*name=\"destination\"[^>]*required", dialog);
        Assert.Matches("<textarea[^>]*name=\"reason\"[^>]*required", dialog);
        Assert.Contains($">{OperatorLabels.CaseWorkspace.ConfirmAction}</button>", dialog, StringComparison.Ordinal);
        AssertCarriesNoLease(dialog);

        using var returned = await workspace.PostAsync(
            "Closure?handler=Reopen",
            Form(
                workspace.AntiforgeryToken,
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", "return-to-work"),
                ("reason", "Principal reinstated the instruction"),
                ("destination", "Review")));

        AssertPrg(returned, store.CaseId);
        var reopening = Assert.Single(store.Reopenings);
        Assert.Equal(
            (CaseReopenDestination.Review, "Principal reinstated the instruction", "return-to-work", store.LeaseToken),
            (reopening.Destination, reopening.Reason, reopening.OperationKey, reopening.EditLeaseToken));
        Assert.Single(store.Claims);
    }

    /// <summary>
    /// Log query (operator, 11 October 2026): its dialog takes the channel the
    /// query came by and what it is, and posts both to the one Core command
    /// under the session's lease; the details also stand as the history reason.
    /// </summary>
    [Fact]
    public async Task LogQueryPostsTheChannelAndDetailsToOpenCaseQuery()
    {
        const string details = "Handler asked when the supplementary goes out";
        var store = new RecordingCaseDetailsStore { State = CaseLifecycleState.ReportSent };
        using var workspace = await EnterEditModeAsync(store, services =>
            Substitute<IOpenCaseQuery>(services, store));

        var html = await workspace.GetWorkspaceAsync();
        Assert.Contains(
            "data-dialog-open=\"case-log-query-dialog\" data-log-query",
            RecordBar(html),
            StringComparison.Ordinal);
        var dialog = Section(html, "case-log-query-dialog-title");
        Assert.Contains($"/Cases/{store.CaseId:D}?handler=LogQuery", dialog, StringComparison.OrdinalIgnoreCase);
        Assert.Matches("<select[^>]*name=\"channel\"", dialog);
        foreach (var channel in Enum.GetValues<CaseQueryChannel>())
        {
            Assert.Contains(
                $"<option value=\"{channel}\">{CaseWorkspaceLabels.Queries.Channel(channel)}</option>",
                dialog,
                StringComparison.Ordinal);
        }
        Assert.Matches("<textarea[^>]*name=\"details\"", dialog);
        Assert.DoesNotContain("editLeaseToken", dialog, StringComparison.Ordinal);

        var operationKey = Guid.NewGuid().ToString("N");
        using var logged = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=LogQuery",
            workspace.MutationForm(operationKey, string.Empty, ("channel", "Phone"), ("details", details)));

        AssertPrg(logged, store.CaseId);
        var raised = Assert.Single(store.QueryOpens);
        Assert.Equal(store.CaseId, raised.CaseId);
        Assert.Equal(operationKey, raised.OperationKey);
        Assert.Equal(CaseQueryChannel.Phone, raised.Channel);
        Assert.Equal(details, raised.Details);
        Assert.Null(raised.RetainedMessageId);
    }

    /// <summary>
    /// The Actions menu offers Log query on a Report sent Case (operator, 27
    /// October 2026). Mark done and Reopen live on the query's own row.
    /// </summary>
    [Theory]
    [InlineData("report-sent", CaseLifecycleState.ReportSent, true)]
    [InlineData("before-report", CaseLifecycleState.ReportPreparation, false)]
    [InlineData("closed", CaseLifecycleState.PrincipalCancelled, false)]
    public async Task TheActionsMenuOffersLogQueryOnceTheReportIsSent(
        string name, CaseLifecycleState state, bool offersLog)
    {
        Assert.NotEmpty(name);
        var store = new RecordingCaseDetailsStore { State = state, HasOpenQuery = state == CaseLifecycleState.ReportSent };
        using var workspace = await EnterEditModeAsync(store, _ => { });

        var bar = RecordBar(await workspace.GetWorkspaceAsync());

        Assert.Equal(offersLog, bar.Contains("data-log-query", StringComparison.Ordinal));
        Assert.Equal(offersLog, bar.Contains(CaseWorkspaceLabels.Frame.LogQuery, StringComparison.Ordinal));
    }



    [Theory]
    [InlineData(CaseLifecycleState.NotReady, true)]
    [InlineData(CaseLifecycleState.Review, true)]
    [InlineData(CaseLifecycleState.ReportPreparation, true)]
    [InlineData(CaseLifecycleState.Held, true)]
    [InlineData(CaseLifecycleState.ReportSent, false)]
    [InlineData(CaseLifecycleState.PrincipalCancelled, false)]
    [InlineData(CaseLifecycleState.CollisionEngineersRejected, false)]
    [InlineData(CaseLifecycleState.CreatedInError, false)]
    [InlineData(CaseLifecycleState.SourceEmailUnlinked, false)]
    public async Task TheAdverseCloseActionStandsApartAndOffersOnlyThePermittedOutcomes(
        CaseLifecycleState state,
        bool offersClosure)
    {
        var store = new RecordingCaseDetailsStore { State = state };
        using var workspace = await EnterEditModeAsync(store, services =>
            Substitute<ICloseCase>(services, new CoreCheckedCloseCase(store)));

        var html = await workspace.GetWorkspaceAsync();
        var bar = RecordBar(html);

        // v26 (decision C2): Close case is the last item of the one Actions
        // menu, in red below a divider.
        Assert.Equal(offersClosure, bar.Contains("data-dialog-open=\"case-close-dialog\"", StringComparison.Ordinal));
        Assert.Equal(
            offersClosure,
            html.Contains("data-dialog=\"case-close-dialog\"", StringComparison.Ordinal));
        if (!offersClosure)
        {
            // A Case Core will not close, closed already or its report sent,
            // keeps its reference and its stage and offers no Close item. A
            // closed Case reads Closed (operator, 15 October 2026).
            Assert.DoesNotContain("data-dialog-open=\"case-close-dialog\"", html, StringComparison.Ordinal);
            Assert.Contains("QDOS3100042", html, StringComparison.Ordinal);
            var stage = CaseLifecycleRules.IsTerminal(state) ? OperatorLabels.ClosedStage : OperatorLabels.CaseStage(state);
            Assert.Contains($">{stage}</span>", RibbonStateChip(html), StringComparison.Ordinal);
            return;
        }

        // Separation: below the menu's last divider there is the one Close
        // control, in the danger style, and none of the progression actions.
        var adverse = AdverseGroup(html);
        Assert.Contains("data-dialog-open=\"case-close-dialog\"", adverse, StringComparison.Ordinal);
        Assert.Contains("btn--danger", adverse, StringComparison.Ordinal);
        Assert.Contains(OperatorLabels.CaseWorkspace.CloseCase, adverse, StringComparison.Ordinal);
        foreach (var progression in new[] { "Assign Engineer", "Log query", "Return to Engineer", "Place on Hold", "Correct principal" })
        {
            Assert.DoesNotContain(progression, adverse, StringComparison.Ordinal);
        }

        var dialog = CloseDialog(html);
        Assert.Contains(
            $"/Cases/{store.CaseId:D}/Closure?handler=Close",
            dialog,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("value=\"PrincipalCancelled\"", dialog, StringComparison.Ordinal);
        Assert.Contains("value=\"CollisionEngineersRejected\"", dialog, StringComparison.Ordinal);
        foreach (var unavailable in new[] { "CreatedInError", "SourceEmailUnlinked" })
        {
            Assert.DoesNotContain($"value=\"{unavailable}\"", dialog, StringComparison.Ordinal);
        }
        // Completed is no closure outcome (operator, 11 October 2026): every
        // offered outcome is a named terminal one.
        var offered = System.Text.RegularExpressions.Regex.Matches(dialog, "<option value=\"(?<value>[A-Za-z]+)\"")
            .Select(match => match.Groups["value"].Value)
            .ToArray();
        Assert.NotEmpty(offered);
        Assert.All(offered, value => Assert.Contains(value, Enum.GetNames<CaseClosureOutcome>()));

        Assert.Matches("<select[^>]*name=\"outcome\"[^>]*required", dialog);
        Assert.Matches("<textarea[^>]*name=\"reason\"[^>]*rows=\"3\"", dialog);
        Assert.Contains("name=\"reason\"", dialog, StringComparison.Ordinal);
        Assert.Contains("required", dialog, StringComparison.Ordinal);
        Assert.Contains($"value=\"{store.LeaseToken}\"", dialog, StringComparison.Ordinal);
        Assert.Contains(
            $"value=\"{store.CaseVersion.ToString(CultureInfo.InvariantCulture)}\"",
            dialog,
            StringComparison.Ordinal);
        // Closing is never deletion: the workspace offers no such action.
        Assert.DoesNotContain("Delete case", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("handler=DeleteCase", html, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The progression actions stay where they were: the adverse group is an
    /// addition beside them, not a replacement for them.
    /// </summary>

    [Fact]
    public async Task TheCloseActionDoesNotDisplaceTheProgressionActions()
    {
        var store = new RecordingCaseDetailsStore { State = CaseLifecycleState.ReportPreparation };
        using var workspace = await EnterEditModeAsync(store, services =>
            Substitute<ICloseCase>(services, new CoreCheckedCloseCase(store)));

        var bar = RecordBar(await workspace.GetWorkspaceAsync());
        var adverseStart = bar.IndexOf("data-dialog-open=\"case-close-dialog\"", StringComparison.Ordinal);

        Assert.Contains(CaseWorkspaceLabels.Frame.PlaceOnHold, bar, StringComparison.Ordinal);
        Assert.Contains(CaseWorkspaceLabels.Frame.CorrectPrincipal, bar, StringComparison.Ordinal);
        Assert.True(adverseStart > 0, "The Close item is not rendered.");
        // The hold group is drawn before the divider that sets Close apart at
        // the end of the menu.
        Assert.InRange(bar.IndexOf(CaseWorkspaceLabels.Frame.PlaceOnHold, StringComparison.Ordinal), 0, adverseStart);
        Assert.InRange(bar.IndexOf(CaseWorkspaceLabels.Frame.CorrectPrincipal, StringComparison.Ordinal), 0, adverseStart);
        Assert.InRange(bar.LastIndexOf("menu-sep", StringComparison.Ordinal), 0, adverseStart);
    }

    /// <summary>
    /// A complete disposition reaches Core with the workspace's own reasoned
    /// envelope — actor, version, lease, operation key and reason — carrying
    /// the chosen outcome, and the Case is still there afterwards, reading
    /// Closed. Closing never deletes a Case.
    /// </summary>

    [Fact]
    public async Task ClosingRecordsTheChosenAdverseOutcomeAndKeepsTheCase()
    {
        var store = new RecordingCaseDetailsStore { State = CaseLifecycleState.Review };
        var closeCase = new CoreCheckedCloseCase(store);
        using var workspace = await EnterEditModeAsync(store, services =>
            Substitute<ICloseCase>(services, closeCase));

        using var closed = await workspace.PostAsync(
            "Closure?handler=Close",
            workspace.MutationForm(
                "close-principal-cancelled",
                "Principal withdrew the instruction",
                ("outcome", "PrincipalCancelled")));

        AssertPrg(closed, store.CaseId);
        var closure = Assert.Single(closeCase.Closures);
        AssertLeasedMutation(workspace, closure, "close-principal-cancelled", "Principal withdrew the instruction");
        Assert.Equal(CaseClosureOutcome.PrincipalCancelled, closure.Outcome);

        // The recorded transition, as the projection then reports it.
        store.State = CaseLifecycleState.PrincipalCancelled;
        var html = await workspace.GetWorkspaceAsync();
        Assert.Contains("The selected terminal outcome was recorded.", html, StringComparison.Ordinal);
        Assert.Contains("QDOS3100042", html, StringComparison.Ordinal);
        Assert.Equal(
            $"<span class=\"status status--neutral\">{OperatorLabels.ClosedStage}</span>",
            RibbonStateChip(html));
        Assert.DoesNotContain("data-dialog-open=\"case-close-dialog\"", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// The chooser and the reason are both required, and an outcome the Case's
    /// current state does not permit is not a closure. Every refusal stops
    /// before Core's store, keeps this browser in edit mode, and hands the
    /// operator back what they submitted.
    /// </summary>

    [Theory]
    // The chooser was not answered, or was answered with something that is not
    // one of the named outcomes: neither may fall through to the enum default.
    [InlineData(CaseLifecycleState.Review, null, "Principal withdrew the instruction")]
    [InlineData(CaseLifecycleState.Review, "", "Principal withdrew the instruction")]
    [InlineData(CaseLifecycleState.Review, "99", "Principal withdrew the instruction")]
    // Each of these is reached through its own action, never through Close.
    [InlineData(CaseLifecycleState.Review, "CreatedInError", "The principal was wrong")]
    [InlineData(CaseLifecycleState.Review, "SourceEmailUnlinked", "The e-mail was unlinked")]
    // A closed Case cannot be closed again.
    [InlineData(CaseLifecycleState.PrincipalCancelled, "CollisionEngineersRejected", "Rejected after all")]
    // The reason is required.
    [InlineData(CaseLifecycleState.Review, "PrincipalCancelled", "")]
    [InlineData(CaseLifecycleState.Review, "PrincipalCancelled", "   ")]
    public async Task AnIncompleteOrUnavailableClosureNeverReachesTheCommand(
        CaseLifecycleState state,
        string? outcome,
        string reason)
    {
        var store = new RecordingCaseDetailsStore { State = state };
        var closeCase = new CoreCheckedCloseCase(store);
        using var workspace = await EnterEditModeAsync(store, services =>
            Substitute<ICloseCase>(services, closeCase));
        var fields = new List<(string Name, string Value)>
        {
            ("id", store.CaseId.ToString("D")),
            ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
            ("operationKey", "close-refused"),
            ("editLeaseToken", store.LeaseToken),
            ("reason", reason)
        };
        if (outcome is not null)
        {
            fields.Add(("outcome", outcome));
        }

        using var refused = await workspace.PostAsync(
            "Closure?handler=Close",
            Form(workspace.AntiforgeryToken, [.. fields]));

        AssertPrg(refused, store.CaseId);
        Assert.Empty(closeCase.Closures);
        var html = await workspace.GetWorkspaceAsync();
        Assert.Contains("role=\"alert\"", html, StringComparison.Ordinal);
        // Edit mode survives, and what was submitted comes back with it.
        Assert.Equal(store.LeaseToken, InputValue(html, "editLeaseToken"));
        Assert.Contains("Your change was not applied", html, StringComparison.Ordinal);
        // The Case itself is untouched and still readable.
        Assert.Contains("QDOS3100042", html, StringComparison.Ordinal);
    }

    /// <summary>The Actions menu's tail below its last divider (v26): the Close item alone.</summary>

    private static string AdverseGroup(string html)
    {
        var bar = RecordBar(html);
        var menuEnd = bar.IndexOf("</details>", StringComparison.Ordinal);
        Assert.True(menuEnd > 0, "The Actions menu is not rendered.");
        var start = bar.LastIndexOf("class=\"menu-sep\"", menuEnd, StringComparison.Ordinal);
        Assert.True(start >= 0, "The adverse divider is not rendered.");
        return bar[start..menuEnd];
    }


    private static string CloseDialog(string html)
    {
        var start = html.IndexOf("data-dialog=\"case-close-dialog\"", StringComparison.Ordinal);
        Assert.True(start >= 0, "The close dialog is not rendered.");
        var end = html.IndexOf("</section>", start, StringComparison.Ordinal);
        Assert.True(end > start, "The close dialog is not closed.");
        return html[start..end];
    }

    /// <summary>
    /// Stands in for Core's <c>CloseCase</c> over the recording projection: it
    /// applies exactly the rules that use case applies, in its order, and
    /// replaces only the persistence. A closure the real command would refuse
    /// is refused here too, so the page's own refusal path is exercised
    /// against the real policy rather than against a permissive double.
    /// </summary>

    private sealed class CoreCheckedCloseCase(RecordingCaseDetailsStore store) : ICloseCase
    {
        public List<CloseCaseRequest> Closures { get; } = [];

        public async Task<CaseWorkflowRecord> ExecuteAsync(
            CloseCaseRequest request,
            CancellationToken cancellationToken)
        {
            CaseLifecycleRules.ValidateClose(request);
            var current = await ((ICaseWorkflowQueries)store).GetAsync(request.CaseId, cancellationToken)
                ?? throw new KeyNotFoundException($"Case '{request.CaseId}' was not found.");
            CaseLifecycleRules.RequireClosureIsAllowed(current, request);
            Closures.Add(request);
            return current with
            {
                State = Enum.Parse<CaseLifecycleState>(request.Outcome.ToString()),
                ClosureOutcome = request.Outcome
            };
        }
    }

}
