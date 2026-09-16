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
            workspace.MutationForm("close-case", "Provider withdrew the instruction", ("outcome", "ProviderCancelled")));
        using var reopenedToReview = await workspace.PostAsync(
            "Closure?handler=Reopen",
            workspace.MutationForm(
                "reopen-review",
                "Provider reinstated the instruction",
                ("destination", "Review"),
                ("instructionsComplete", "true"),
                ("imagesComplete", "false"),
                ("evidenceReference", "reopen-evidence-1")));
        using var reopenedToNotReady = await workspace.PostAsync(
            "Closure?handler=Reopen",
            workspace.MutationForm("reopen-not-ready", "Images still outstanding", ("destination", "NotReady")));
        using var archived = await workspace.PostAsync(
            "Closure?handler=Archive",
            workspace.MutationForm("archive-case", "Retention period reached"));

        AssertPrg(closed, store.CaseId);
        AssertPrg(reopenedToReview, store.CaseId);
        AssertPrg(reopenedToNotReady, store.CaseId);
        AssertPrg(archived, store.CaseId);

        var closure = Assert.Single(store.Closures);
        AssertLeasedMutation(workspace, closure, "close-case", "Provider withdrew the instruction");
        Assert.Equal(CaseClosureOutcome.ProviderCancelled, closure.Outcome);

        Assert.Equal(2, store.Reopenings.Count);
        var toReview = store.Reopenings[0];
        AssertLeasedMutation(workspace, toReview, "reopen-review", "Provider reinstated the instruction");
        Assert.Equal(CaseReopenDestination.Review, toReview.Destination);
        Assert.Equal(new CaseReadinessEvidence(true, false, "reopen-evidence-1"), toReview.Readiness);
        var toNotReady = store.Reopenings[1];
        Assert.Equal("reopen-not-ready", toNotReady.OperationKey);
        Assert.Equal(CaseReopenDestination.NotReady, toNotReady.Destination);
        Assert.Null(toNotReady.Readiness);

        var archive = Assert.Single(store.Archives);
        AssertLeasedMutation(workspace, archive, "archive-case", "Retention period reached");
        var html = await workspace.GetWorkspaceAsync();
        Assert.Contains("The terminal case was archived and is now read-only.", html, StringComparison.Ordinal);

        await AssertRefusalKeepsEditModeAsync(
            workspace,
            "Closure?handler=Close",
            workspace.MutationForm("close-case-2", "Already closed", ("outcome", "PostReportComplete")));
    }



    [Theory]
    [InlineData(CaseLifecycleState.NotReady, true)]
    [InlineData(CaseLifecycleState.Review, true)]
    [InlineData(CaseLifecycleState.ReportPreparation, true)]
    [InlineData(CaseLifecycleState.PostReport, true)]
    [InlineData(CaseLifecycleState.PostReportComplete, true)]
    [InlineData(CaseLifecycleState.Query, true)]
    [InlineData(CaseLifecycleState.ProviderCancelled, false)]
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
            // A closed Case is read: it keeps its reference and reads its
            // recorded outcome, and offers no way to close it a second time.
            Assert.DoesNotContain("data-dialog-open=\"case-close-dialog\"", html, StringComparison.Ordinal);
            Assert.Contains("QDOS3100042", html, StringComparison.Ordinal);
            Assert.Contains(EncodedStage(state), html, StringComparison.Ordinal);
            return;
        }

        // Separation: below the menu's last divider there is the one Close
        // control, in the danger style, and none of the progression actions.
        var adverse = AdverseGroup(html);
        Assert.Contains("data-dialog-open=\"case-close-dialog\"", adverse, StringComparison.Ordinal);
        Assert.Contains("btn--danger", adverse, StringComparison.Ordinal);
        Assert.Contains(OperatorLabels.CaseWorkspace.CloseCase, adverse, StringComparison.Ordinal);
        foreach (var progression in new[] { "Hand to Engineer", "Mark completed", "Return to Engineer", "Place on Hold", "Correct principal" })
        {
            Assert.DoesNotContain(progression, adverse, StringComparison.Ordinal);
        }

        var dialog = CloseDialog(html);
        Assert.Contains(
            $"/Cases/{store.CaseId:D}/Closure?handler=Close",
            dialog,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("value=\"ProviderCancelled\"", dialog, StringComparison.Ordinal);
        Assert.Contains("value=\"CollisionEngineersRejected\"", dialog, StringComparison.Ordinal);
        foreach (var unavailable in new[] { "CreatedInError", "SourceEmailUnlinked", "PostReportComplete" })
        {
            Assert.DoesNotContain($"value=\"{unavailable}\"", dialog, StringComparison.Ordinal);
        }

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
        var store = new RecordingCaseDetailsStore { State = CaseLifecycleState.PostReport };
        using var workspace = await EnterEditModeAsync(store, services =>
            Substitute<ICloseCase>(services, new CoreCheckedCloseCase(store)));

        var bar = RecordBar(await workspace.GetWorkspaceAsync());
        var adverseStart = bar.IndexOf("data-dialog-open=\"case-close-dialog\"", StringComparison.Ordinal);

        Assert.Contains("Mark completed", bar, StringComparison.Ordinal);
        Assert.Contains("Place on Hold", bar, StringComparison.Ordinal);
        Assert.True(adverseStart > 0, "The Close item is not rendered.");
        // The progression items and the hold group are drawn before the
        // divider that sets Close apart at the end of the menu.
        Assert.InRange(bar.IndexOf("Mark completed", StringComparison.Ordinal), 0, adverseStart);
        Assert.InRange(bar.IndexOf("Place on Hold", StringComparison.Ordinal), 0, adverseStart);
        Assert.InRange(bar.LastIndexOf("menu-sep", StringComparison.Ordinal), 0, adverseStart);
    }

    /// <summary>
    /// A complete disposition reaches Core with the workspace's own reasoned
    /// envelope — actor, version, lease, operation key and reason — carrying
    /// the chosen outcome, and the Case is still there afterwards, reading the
    /// outcome it was closed with. Closing never deletes a Case.
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
                "close-provider-cancelled",
                "Provider withdrew the instruction",
                ("outcome", "ProviderCancelled")));

        AssertPrg(closed, store.CaseId);
        var closure = Assert.Single(closeCase.Closures);
        AssertLeasedMutation(workspace, closure, "close-provider-cancelled", "Provider withdrew the instruction");
        Assert.Equal(CaseClosureOutcome.ProviderCancelled, closure.Outcome);

        // The recorded transition, as the projection then reports it.
        store.State = CaseLifecycleState.ProviderCancelled;
        var html = await workspace.GetWorkspaceAsync();
        Assert.Contains("The selected terminal outcome was recorded.", html, StringComparison.Ordinal);
        Assert.Contains("QDOS3100042", html, StringComparison.Ordinal);
        Assert.Contains(
            EncodedStage(CaseLifecycleState.ProviderCancelled),
            html,
            StringComparison.Ordinal);
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
    [InlineData(CaseLifecycleState.Review, null, "Provider withdrew the instruction")]
    [InlineData(CaseLifecycleState.Review, "", "Provider withdrew the instruction")]
    [InlineData(CaseLifecycleState.Review, "99", "Provider withdrew the instruction")]
    // Each of these is reached through its own action, never through Close.
    [InlineData(CaseLifecycleState.Review, "CreatedInError", "The principal was wrong")]
    [InlineData(CaseLifecycleState.Review, "SourceEmailUnlinked", "The e-mail was unlinked")]
    // Progression, and not from this state.
    [InlineData(CaseLifecycleState.Review, "PostReportComplete", "Post-report work is done")]
    // A closed Case cannot be closed again.
    [InlineData(CaseLifecycleState.ProviderCancelled, "CollisionEngineersRejected", "Rejected after all")]
    // The reason is required.
    [InlineData(CaseLifecycleState.Review, "ProviderCancelled", "")]
    [InlineData(CaseLifecycleState.Review, "ProviderCancelled", "   ")]
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

    /// <summary>
    /// A stage name as the response actually carries it: the framework's HTML
    /// encoder writes the chip's separator as a numeric reference.
    /// </summary>

    private static string EncodedStage(CaseLifecycleState state) =>
        System.Text.Encodings.Web.HtmlEncoder.Default.Encode(OperatorLabels.CaseStage(state));

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
            var current = await CaseLifecycleRules.GetRequiredAsync(store, request.CaseId, cancellationToken);
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
