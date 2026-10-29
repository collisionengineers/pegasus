using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Tasks;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure;
using Pegasus.Infrastructure.Email;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The class shares one database (<see cref="WorkflowDatabase"/>) and every test
/// makes Cases of its own in it. A test that reads or writes across the whole
/// database makes a database of its own (<c>CreateIsolatedAsync</c>): the
/// retained Sent evidence and the approved mailbox's poll, a count of every
/// Case, a search over all of them, the QDOS Principal and its sequence, the
/// action history of every cancelled task, and an instruction draft.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class CaseWorkflowPersistenceTests(WorkflowDatabase database)
    : IClassFixture<WorkflowDatabase>
{
    [Fact]
    public async Task HasOperationFindsAKeyInTheHistoryOrTheLeaseOperationsAndNothingElse()
    {
        await using var harness = await WorkflowHarness.CreateAsync(database);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var lease = await harness.Store.ClaimAsync(
            new(harness.CaseId, 0, actor, "claim-probe"),
            default);
        await new StartCaseWork(harness.Store).ExecuteAsync(
            new ChangeCaseStateRequest(harness.CaseId, 0, actor, "start-probe", "Inspection work started", lease.Token),
            default);

        Assert.True(await harness.Store.HasOperationAsync(harness.CaseId, "claim-probe", default));
        Assert.True(await harness.Store.HasOperationAsync(harness.CaseId, " start-probe ", default));
        Assert.False(await harness.Store.HasOperationAsync(harness.CaseId, "never-used", default));
        Assert.False(await harness.Store.HasOperationAsync(Guid.NewGuid(), "claim-probe", default));
    }

    [Fact]
    public async Task StartMovesDirectlyToReportPreparationAndRetainedSentEvidenceNeedsNoApproval()
    {
        await using var harness = await WorkflowHarness.CreateIsolatedAsync();
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var startLease = await harness.Store.ClaimAsync(
            new(harness.CaseId, 0, actor, "claim-start"),
            default);
        var startRequest = new ChangeCaseStateRequest(
            harness.CaseId,
            0,
            actor,
            "start-1",
            "Inspection work started",
            startLease.Token);
        var start = new StartCaseWork(harness.Store);

        var started = await start.ExecuteAsync(startRequest, default);
        await harness.SetStaffEnabledAsync(started.AssignedEngineerId!.Value, false);
        var replay = await start.ExecuteAsync(startRequest, default);

        Assert.Equal(CaseLifecycleState.ReportPreparation, started.State);
        Assert.Equal(started, replay);
        Assert.Null(started.ReportApproval);
        harness.TimeProvider.Advance(TimeSpan.FromMinutes(3));

        var evidenceId = Guid.NewGuid();
        var discoveredAtUtc = harness.TimeProvider.GetUtcNow().AddMinutes(-1);
        var retained = await new RetainApprovedMailboxReportSentEvidence(
            harness.ReportSentEvidenceStore).ExecuteAsync(
            new(
                evidenceId,
                "instructions@collisionengineers.co.uk",
                "sent-folder-identity-1",
                "immutable-item-1",
                "internet-message-1",
                "conversation-1",
                "reply-chain-1",
                "source-occurrence-1",
                new string('a', 64),
                new string('b', 64),
                discoveredAtUtc.AddMinutes(-1),
                discoveredAtUtc,
                ActionActor.SystemWorker("approved-mailbox-evidence-ingestion"),
                "retain-evidence-1"),
            default);
        var sentLease = await harness.Store.ClaimAsync(
            new(harness.CaseId, started.Version, actor, "claim-sent"),
            default);
        var sentResult = await new LinkReportEvidence(harness.Store).ExecuteAsync(
            new(
                harness.CaseId,
                started.Version,
                actor,
                "sent-1",
                "Exact approved-mailbox Sent item linked",
                sentLease.Token,
                retained.EvidenceId),
            default);
        var sent = sentResult.Workflow;

        Assert.Equal(CaseLifecycleState.ReportSent, sent.State);
        Assert.Null(sent.ReportApproval);
        Assert.Equal(evidenceId, sent.ReportSentEvidence?.EvidenceId);

        var closed = await ArrangeClosedFromReportSentAsync(harness, harness.CaseId);
        var reopenLease = await harness.Store.ClaimAsync(
            new(harness.CaseId, closed.Version, actor, "claim-post-report-reopen"),
            default);
        var reopened = await new ReopenCase(harness.Store).ExecuteAsync(
            new(
                harness.CaseId,
                closed.Version,
                actor,
                "reopen-post-report",
                "Provider requested resumed post-report work",
                reopenLease.Token,
                CaseReopenDestination.ReportSent),
            default);

        Assert.Equal(CaseLifecycleState.ReportSent, reopened.State);
        Assert.Equal(evidenceId, reopened.ReportSentEvidence?.EvidenceId);
    }

    [Fact]
    public async Task ReturnToEngineerAllowsReportSentWithTheNormalLeaseAndAssignedEngineerGates()
    {
        await using var harness = await WorkflowHarness.CreateAsync(database);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            var workflow = await context.CaseWorkflows.SingleAsync(item => item.CaseId == harness.CaseId);
            workflow.State = nameof(CaseLifecycleState.ReportSent);
            workflow.ClosureOutcome = null;
            await context.SaveChangesAsync();
        }
        var lease = await harness.Store.ClaimAsync(
            new(harness.CaseId, 0, actor, "claim-return-query"),
            CancellationToken.None);

        var returned = await new ReturnCaseToEngineer(harness.Store).ExecuteAsync(
            new(
                harness.CaseId,
                0,
                actor,
                "return-query-to-engineer",
                "Query requires report preparation work.",
                lease.Token),
            CancellationToken.None);

        Assert.Equal(CaseLifecycleState.ReportPreparation, returned.State);
        Assert.Equal(1L, returned.Version);
    }

    [Fact]
    public async Task ReturnToEngineerRefusesOtherStates()
    {
        await using var stateHarness = await WorkflowHarness.CreateAsync(database);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var reviewLease = await stateHarness.Store.ClaimAsync(
            new(stateHarness.CaseId, 0, actor, "claim-return-review"),
            CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ReturnCaseToEngineer(stateHarness.Store).ExecuteAsync(
                new(
                    stateHarness.CaseId,
                    0,
                    actor,
                    "return-review-to-engineer",
                    "This state is not post-report.",
                    reviewLease.Token),
                CancellationToken.None));
    }

    /// <summary>
    /// #1213 ruling 3 (operator, 15 October 2026): a Case with no Engineer
    /// returns to Engineer, or reopens to With Engineer, Unassigned, for an
    /// Engineer to take.
    /// </summary>
    [Fact]
    public async Task ReturnToEngineerAndReopenLandAnUnassignedCaseUnassigned()
    {
        await using var harness = await WorkflowHarness.CreateAsync(database);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            var returning = await context.CaseWorkflows.SingleAsync(item => item.CaseId == harness.NotReadyCaseId);
            returning.State = nameof(CaseLifecycleState.ReportSent);
            returning.AssignedEngineerId = null;
            var reopening = await context.CaseWorkflows.SingleAsync(item => item.CaseId == harness.SecondCaseId);
            reopening.State = nameof(CaseLifecycleState.PrincipalCancelled);
            reopening.ClosureOutcome = nameof(CaseClosureOutcome.PrincipalCancelled);
            reopening.AssignedEngineerId = null;
            await context.SaveChangesAsync();
        }

        var returnLease = await harness.Store.ClaimAsync(
            new(harness.NotReadyCaseId, 0, actor, "claim-return-unassigned"),
            CancellationToken.None);
        var returned = await new ReturnCaseToEngineer(harness.Store).ExecuteAsync(
            new(
                harness.NotReadyCaseId,
                0,
                actor,
                "return-unassigned-to-engineer",
                "A query needs the report again.",
                returnLease.Token),
            CancellationToken.None);
        var reopenLease = await harness.Store.ClaimAsync(
            new(harness.SecondCaseId, 0, actor, "claim-reopen-unassigned"),
            CancellationToken.None);
        var reopened = await new ReopenCase(harness.Store).ExecuteAsync(
            new(
                harness.SecondCaseId,
                0,
                actor,
                "reopen-unassigned-to-engineer",
                "The Principal withdrew the cancellation.",
                reopenLease.Token,
                CaseReopenDestination.ReportPreparation),
            CancellationToken.None);

        Assert.Equal(CaseLifecycleState.ReportPreparation, returned.State);
        Assert.Null(returned.AssignedEngineerId);
        Assert.Equal(CaseLifecycleState.ReportPreparation, reopened.State);
        Assert.Null(reopened.AssignedEngineerId);
    }

    /// <summary>
    /// #1213 (operator, 15 October 2026): a With Engineer Case is handed to
    /// Unassigned, taken, and handed to a named Engineer with no typed reason.
    /// Each hand-off records Core's words, stays in With Engineer without
    /// meeting the Review gate again (its facts already passed it on entry),
    /// and leaves the time the Case entered With Engineer as it was.
    /// </summary>
    [Fact]
    public async Task AWithEngineerCaseIsHandedOnWithoutTheReviewGateAndKeepsItsEntryTime()
    {
        await using var harness = await WorkflowHarness.CreateAsync(database);
        var actorId = Guid.NewGuid();
        var engineerId = Guid.NewGuid();
        await harness.SeedStaffAccountAsync(actorId, true, StaffRole.Engineer);
        await harness.SeedStaffAccountAsync(engineerId, true, StaffRole.Engineer);
        var actor = ActionActor.Staff(actorId, [StaffRole.Engineer]);
        var sut = new AssignCaseEngineer(
            harness.Store,
            new CaseDataCompletenessPersistenceTests.FixedConfiguration(),
            new EfStaffAccountQueries(harness.Factory, new StaffAccountReads(TimeProvider.System)));

        async Task<CaseWorkflowRecord> HandAsync(Guid? to, string operationKey)
        {
            var current = Assert.IsType<CaseWorkflowRecord>(await harness.Store.GetAsync(harness.CaseId, default));
            var lease = await harness.Store.ClaimAsync(
                new(harness.CaseId, current.Version, actor, $"claim-{operationKey}"),
                default);
            return await sut.ExecuteAsync(
                new(harness.CaseId, current.Version, actor, operationKey, string.Empty, lease.Token, to),
                default);
        }

        await HandAsync(engineerId, "hand-from-review");
        var entered = await harness.StateEnteredAtAsync(harness.CaseId);
        // The persisted facts now fail the Review gate, which a With Engineer
        // hand-off does not ask again.
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Cases SET InstructionComplete = {false} WHERE Id = {harness.CaseId}");
        }
        harness.TimeProvider.Advance(TimeSpan.FromHours(1));

        var unassigned = await HandAsync(null, "hand-to-unassigned");
        var taken = await HandAsync(actorId, "take");
        var assigned = await HandAsync(engineerId, "hand-to-engineer");

        Assert.Null(unassigned.AssignedEngineerId);
        Assert.Equal(actorId, taken.AssignedEngineerId);
        Assert.Equal(engineerId, assigned.AssignedEngineerId);
        Assert.All(
            new[] { unassigned, taken, assigned },
            workflow => Assert.Equal(CaseLifecycleState.ReportPreparation, workflow.State));
        Assert.Equal(entered, await harness.StateEnteredAtAsync(harness.CaseId));
        Assert.Equal(
            "Set to Unassigned",
            Assert.Single(await harness.WorkflowEventsAsync("hand-to-unassigned")).Reason);
        Assert.Equal(
            $"Taken by workflow-test-{actorId:N}",
            Assert.Single(await harness.WorkflowEventsAsync("take")).Reason);
        Assert.Equal(
            $"Assigned to workflow-test-{engineerId:N}",
            Assert.Single(await harness.WorkflowEventsAsync("hand-to-engineer")).Reason);
        // Every hand-off is the With Engineer state event the Sent to Engineer
        // figure counts once, at the Case's first entry.
        Assert.Equal(4L, await harness.WorkflowEventTypeCountAsync(harness.CaseId, "state_ReportPreparation"));
    }

    /// <summary>
    /// FRD-13 "Report sent and queries" (operator, 11 October 2026): a staff
    /// member logs a query on a Report sent Case under the lease; it is an open
    /// Staff row with its channel and details, and the Case carries the
    /// open-query mark. The state stays Report sent.
    /// </summary>
    [Fact]
    public async Task ALoggedQueryIsAnOpenStaffRowThatMarksTheReportSentCase()
    {
        await using var harness = await WorkflowHarness.CreateAsync(database);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        await SetStateAsync(harness, harness.CaseId, CaseLifecycleState.ReportSent);

        var queryId = await harness.Store.OpenQueryAsync(
            new(harness.CaseId, actor, "log-phone-query", null, CaseQueryChannel.Phone, "  Asked about the labour rate.  "),
            CancellationToken.None);
        var raised = await harness.Store.GetAsync(harness.CaseId, CancellationToken.None)
            ?? throw new InvalidOperationException("The case was not found.");
        Assert.NotEqual(Guid.Empty, queryId);

        Assert.Equal(CaseLifecycleState.ReportSent, raised.State);
        Assert.True(raised.HasOpenQuery);
        Assert.Equal(1L, raised.Version);
        await using var context = await harness.Factory.CreateDbContextAsync();
        var query = Assert.Single(await context.CaseQueries
            .Where(item => item.CaseId == harness.CaseId)
            .ToListAsync());
        Assert.Equal(nameof(CaseQuerySource.Staff), query.Source);
        Assert.Equal(nameof(CaseQueryChannel.Phone), query.Channel);
        Assert.Equal("Asked about the labour rate.", query.Details);
        Assert.Null(query.IntakeReceiptId);
        Assert.Null(query.ClosedAtUtc);
        Assert.Null(query.ClosedAs);
        Assert.Equal(nameof(ActorKind.Staff), query.RaisedByKind);
        Assert.Equal(actor.SubjectId, query.RaisedBySubjectId);
        Assert.Equal("log-phone-query", query.RaisedOperationKey);
        Assert.Equal(1L, await harness.WorkflowEventTypeCountAsync(harness.CaseId, "case_query_logged"));
        await AssertOpenQueryMarkMatchesRowsAsync(harness, harness.CaseId);
    }

    [Fact]
    public async Task LoggingAQueryBeforeTheReportIsSentIsRefused()
    {
        await using var harness = await WorkflowHarness.CreateAsync(database);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Store.OpenQueryAsync(
            new(harness.CaseId, actor, "log-review-query", null, CaseQueryChannel.Email, null),
            CancellationToken.None));

        var persisted = await harness.Store.GetAsync(harness.CaseId, CancellationToken.None);
        Assert.Equal(CaseLifecycleState.Review, persisted?.State);
        Assert.False(persisted?.HasOpenQuery);
        Assert.Equal(0L, persisted?.Version);
        await AssertOpenQueryMarkMatchesRowsAsync(harness, harness.CaseId);
    }

    /// <summary>
    /// The Case commands read no Case of their own (eff-07-l, register A3-06): the
    /// transition store asks each state rule of the workflow it reads under its lock,
    /// before the archive, lease and version checks, and says it in the words the
    /// command always used. A refusal changes nothing.
    /// </summary>
    [Fact]
    public async Task EachLifecycleCommandIsRefusedFromTheLockedStateInItsOwnWords()
    {
        await using var harness = await WorkflowHarness.CreateAsync(database);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var caseId = harness.CaseId;
        var evidenceId = Guid.NewGuid();

        async Task<string> LeaseAsync(string key) => (await harness.Store.ClaimAsync(
            new ClaimCaseEditLeaseRequest(caseId, 0, actor, $"claim-{key}") { TakeOver = true },
            default)).Token;

        async Task RefusedAsync(string words, Func<Task> command)
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(command);
            Assert.Equal(words, exception.Message);
        }

        // Review: nothing to release, reopen, unlink, link, approve or resolve, and no report sent to query.
        var token = await LeaseAsync("in-review");
        await RefusedAsync(
            "Only a held case can be released.",
            () => new ReleaseCaseHold(harness.Store).ExecuteAsync(
                new ChangeCaseStateRequest(caseId, 0, actor, "release-in-review", "Back to work", token), default));
        await RefusedAsync(
            "A case can enter Review only from With Engineer or Report sent.",
            () => new ReturnCaseToReview(harness.Store).ExecuteAsync(
                new ReturnCaseToReviewRequest(caseId, 0, actor, "return-in-review", "Back to Review", token), default));
        await RefusedAsync(
            "Only a closed case other than Created in error can be reopened.",
            () => new ReopenCase(harness.Store).ExecuteAsync(
                new ReopenCaseRequest(
                    caseId, 0, actor, "reopen-in-review", "Resume", token, CaseReopenDestination.Review),
                default));
        await RefusedAsync(
            "Report-Sent evidence can be unlinked only while report preparation is active.",
            () => new UnlinkReportEvidence(harness.Store).ExecuteAsync(
                new UnlinkReportEvidenceRequest(caseId, 0, actor, "unlink-in-review", "Wrong item", token, evidenceId),
                default));
        await RefusedAsync(
            "Exact report-Sent evidence can enter post-report work only from Report preparation.",
            () => new LinkReportEvidence(harness.Store).ExecuteAsync(
                new LinkReportEvidenceRequest(caseId, 0, actor, "link-in-review", "Sent item", token, evidenceId),
                default));
        await RefusedAsync(
            "A report can be approved only while report preparation is active.",
            () => new RecordCaseReportApproval(harness.Store).ExecuteAsync(
                new RecordCaseReportApprovalRequest(
                    caseId, 0, actor, "approve-in-review", "Approved", token,
                    new(Guid.NewGuid(), "case-report-v1.pdf", new string('A', 64))),
                default));
        await RefusedAsync(
            "A query can be opened only once the report is sent.",
            () => new OpenCaseQuery(harness.Store).ExecuteAsync(
                new(caseId, actor, "open-in-review", null, CaseQueryChannel.Phone, null), default));
        await RefusedAsync(
            "Only a terminal case can be archived.",
            () => new ArchiveCase(harness.Store).ExecuteAsync(
                new ArchiveCaseRequest(caseId, 0, actor, "archive-in-review", "Retain", token), default));
        await RefusedAsync(
            "A case can return to Engineer only from Report sent.",
            () => new ReturnCaseToEngineer(harness.Store).ExecuteAsync(
                new ReturnCaseToEngineerRequest(
                    caseId, 0, actor, "return-to-engineer-in-review", "Query needs the report", token),
                default));

        // Report sent: no new hold, and a closure is for a Case whose report is not yet sent.
        await SetStateAsync(harness, caseId, CaseLifecycleState.ReportSent);
        token = await LeaseAsync("report-sent");
        await RefusedAsync(
            "A case can be held only from Not ready, Review or With Engineer.",
            () => new PutCaseOnHold(harness.Store, harness.TimeProvider).ExecuteAsync(
                new PutCaseOnHoldRequest(caseId, 0, actor, "hold-report-sent", "Waiting", token), default));
        await RefusedAsync(
            "A case can be closed only before its report is sent.",
            () => new CloseCase(harness.Store).ExecuteAsync(
                new CloseCaseRequest(
                    caseId, 0, actor, "close-report-sent", "Cancelled", token, CaseClosureOutcome.PrincipalCancelled),
                default));
        await RefusedAsync(
            "Case work can start only from Review after a staff member is assigned.",
            () => new StartCaseWork(harness.Store).ExecuteAsync(
                new ChangeCaseStateRequest(caseId, 0, actor, "start-report-sent", "Start work", token), default));
        // The state is refused before the Engineer is looked up: this Engineer has no account.
        await RefusedAsync(
            "A staff member can be assigned only while the case is in Review or With Engineer.",
            () => harness.Store.AssignEngineerAsync(
                new AssignCaseEngineerRequest(
                    caseId, 0, actor, "assign-report-sent", "Assign Engineer", token, Guid.NewGuid()),
                CaseLifecycleState.ReportPreparation,
                default));

        // A closed Case: no second closure, no hold, and no unlink before a reasoned reopening.
        await SetStateAsync(harness, caseId, CaseLifecycleState.PrincipalCancelled);
        token = await LeaseAsync("closed");
        await RefusedAsync(
            "A closed case cannot be closed again.",
            () => new CloseCase(harness.Store).ExecuteAsync(
                new CloseCaseRequest(
                    caseId, 0, actor, "close-closed", "Cancelled", token, CaseClosureOutcome.PrincipalCancelled),
                default));
        await RefusedAsync(
            "A case can be held only from Not ready, Review or With Engineer.",
            () => new PutCaseOnHold(harness.Store, harness.TimeProvider).ExecuteAsync(
                new PutCaseOnHoldRequest(caseId, 0, actor, "hold-closed", "Waiting", token), default));
        await RefusedAsync(
            "A closed case must be reasonedly reopened before report-Sent evidence can be unlinked.",
            () => new UnlinkReportEvidence(harness.Store).ExecuteAsync(
                new UnlinkReportEvidenceRequest(caseId, 0, actor, "unlink-closed", "Wrong item", token, evidenceId),
                default));

        var persisted = Assert.IsType<CaseWorkflowRecord>(await harness.Store.GetAsync(caseId, default));
        Assert.Equal(CaseLifecycleState.PrincipalCancelled, persisted.State);
        Assert.Equal(0L, persisted.Version);
    }

    /// <summary>
    /// An operation the store already committed is answered, not refused again, though the
    /// Case has moved on: a Hold replayed from Held is the committed Hold, and a new
    /// operation key on the same Held Case is refused.
    /// </summary>
    [Fact]
    public async Task AReplayedHoldIsAnsweredFromHeldAndANewKeyIsRefused()
    {
        await using var harness = await WorkflowHarness.CreateAsync(database);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var lease = await harness.Store.ClaimAsync(new(harness.CaseId, 0, actor, "claim-hold-replay"), default);
        var hold = new PutCaseOnHold(harness.Store, harness.TimeProvider);
        var request = new PutCaseOnHoldRequest(harness.CaseId, 0, actor, "hold-once", "Waiting", lease.Token);

        var held = await hold.ExecuteAsync(request, default);
        var replay = await hold.ExecuteAsync(request, default);

        Assert.Equal(CaseLifecycleState.Held, held.State);
        Assert.Equal(held.Version, replay.Version);
        Assert.Equal(CaseLifecycleState.Held, replay.State);
        Assert.Equal(1L, await harness.WorkflowEventCountAsync("hold-once"));

        var again = await harness.Store.ClaimAsync(new(harness.CaseId, held.Version, actor, "claim-hold-again"), default);
        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => hold.ExecuteAsync(
            request with { ExpectedVersion = held.Version, OperationKey = "hold-twice", EditLeaseToken = again.Token },
            default));
        Assert.Equal("A case can be held only from Not ready, Review or With Engineer.", refused.Message);
    }

    /// <summary>
    /// A Close is answered from the committed record while the Case is still closed, and is
    /// refused in the Close words once the Case has moved to Report sent, as Core always did.
    /// </summary>
    [Fact]
    public async Task AReplayedCloseIsAnsweredWhileClosedAndRefusedOnceTheCaseIsReportSent()
    {
        await using var harness = await WorkflowHarness.CreateAsync(database);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var lease = await harness.Store.ClaimAsync(new(harness.CaseId, 0, actor, "claim-close-replay"), default);
        var close = new CloseCase(harness.Store);
        var request = new CloseCaseRequest(
            harness.CaseId, 0, actor, "close-once", "Cancelled", lease.Token, CaseClosureOutcome.PrincipalCancelled);

        var closed = await close.ExecuteAsync(request, default);
        var replay = await close.ExecuteAsync(request, default);

        Assert.Equal(CaseLifecycleState.PrincipalCancelled, closed.State);
        Assert.Equal(closed.Version, replay.Version);

        await SetStateAsync(harness, harness.CaseId, CaseLifecycleState.ReportSent);
        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => close.ExecuteAsync(request, default));
        Assert.Equal("A case can be closed only before its report is sent.", refused.Message);
    }

    /// <summary>
    /// Mark done closes one open query and recomputes the open-query mark.
    /// Marking a query that is already Done is refused and changes nothing.
    /// </summary>
    [Fact]
    public async Task MarkDoneClosesOneOpenQueryAndRefusesASecondMark()
    {
        await using var harness = await WorkflowHarness.CreateAsync(database);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        await SetStateAsync(harness, harness.CaseId, CaseLifecycleState.ReportSent);
        var version = 0L;
        foreach (var (key, channel) in new[] { ("log-email-query", CaseQueryChannel.Email), ("log-whatsapp-query", CaseQueryChannel.WhatsApp) })
        {
            await harness.Store.OpenQueryAsync(
                new(harness.CaseId, actor, key, null, channel, null),
                CancellationToken.None);
            version = (await harness.Store.GetAsync(harness.CaseId, CancellationToken.None))!.Version;
            await AssertOpenQueryMarkMatchesRowsAsync(harness, harness.CaseId);
        }
        harness.TimeProvider.Advance(TimeSpan.FromMinutes(5));

        Guid[] openIds;
        await using (var listed = await harness.Factory.CreateDbContextAsync())
        {
            openIds = await listed.CaseQueries.Where(item => item.CaseId == harness.CaseId).Select(item => item.Id).ToArrayAsync();
        }
        CaseWorkflowRecord resolved = null!;
        foreach (var (id, index) in openIds.Select((id, index) => (id, index)))
        {
            resolved = await harness.Store.MarkQueryDoneAsync(
                new(id, actor, $"mark-done-{index}"),
                CancellationToken.None);
        }

        Assert.Equal(CaseLifecycleState.ReportSent, resolved.State);
        Assert.False(resolved.HasOpenQuery);
        Assert.Equal(version + openIds.Length, resolved.Version);
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            var queries = await context.CaseQueries
                .Where(item => item.CaseId == harness.CaseId)
                .ToListAsync();
            Assert.Equal(2, queries.Count);
            Assert.All(queries, query =>
            {
                Assert.Equal(nameof(CaseQueryClosure.Resolved), query.ClosedAs);
                Assert.Equal(harness.TimeProvider.GetUtcNow(), query.ClosedAtUtc);
                Assert.Equal(nameof(ActorKind.Staff), query.ClosedByKind);
                Assert.Equal(actor.SubjectId, query.ClosedBySubjectId);
                Assert.Null(query.ResolvedByMailOperationId);
            });
        }
        Assert.Equal(2L, await harness.WorkflowEventTypeCountAsync(harness.CaseId, "case_query_resolved"));
        await AssertOpenQueryMarkMatchesRowsAsync(harness, harness.CaseId);

        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Store.MarkQueryDoneAsync(
            new(openIds[0], actor, "mark-done-again"),
            CancellationToken.None));

        var persisted = await harness.Store.GetAsync(harness.CaseId, CancellationToken.None);
        Assert.Equal(resolved.Version, persisted?.Version);
        Assert.Equal(2L, await harness.WorkflowEventTypeCountAsync(harness.CaseId, "case_query_resolved"));
        await AssertOpenQueryMarkMatchesRowsAsync(harness, harness.CaseId);
    }

    /// <summary>A hold the migration left on a Report sent Case still releases back to Report sent, its open query untouched.</summary>
    [Fact]
    public async Task AHoldMigratedFromReportSentStillReleases()
    {
        await using var harness = await WorkflowHarness.CreateAsync(database);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        await SetStateAsync(harness, harness.CaseId, CaseLifecycleState.ReportSent);
        await harness.Store.OpenQueryAsync(
            new(harness.CaseId, actor, "log-before-hold", null, CaseQueryChannel.Other, "By letter."),
            CancellationToken.None);
        var logged = await harness.Store.GetAsync(harness.CaseId, CancellationToken.None)
            ?? throw new InvalidOperationException("The case was not found.");
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            var workflow = await context.CaseWorkflows.SingleAsync(item => item.CaseId == harness.CaseId);
            workflow.State = nameof(CaseLifecycleState.Held);
            workflow.PreHoldState = nameof(CaseLifecycleState.ReportSent);
            workflow.HeldAtUtc = harness.TimeProvider.GetUtcNow();
            await context.SaveChangesAsync();
        }

        var releaseLease = await harness.Store.ClaimAsync(
            new(harness.CaseId, logged.Version, actor, "claim-release-report-sent"),
            CancellationToken.None);
        var released = await new ReleaseCaseHold(harness.Store).ExecuteAsync(
            new ChangeCaseStateRequest(
                harness.CaseId, logged.Version, actor, "release-report-sent", "Principal replied.", releaseLease.Token),
            CancellationToken.None);

        Assert.Equal(CaseLifecycleState.ReportSent, released.State);
        Assert.True(released.HasOpenQuery);
        await AssertOpenQueryMarkMatchesRowsAsync(harness, harness.CaseId);
    }

    /// <summary>
    /// Core refuses to close a Report sent Case and the store asks the same rule
    /// of the workflow it reads under its lock, so a Case closed from Report sent,
    /// as Cases were before that rule, is arranged in the database.
    /// </summary>
    private static async Task<CaseWorkflowRecord> ArrangeClosedFromReportSentAsync(
        WorkflowHarness harness,
        Guid caseId)
    {
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            var workflow = await context.CaseWorkflows.SingleAsync(item => item.CaseId == caseId);
            workflow.State = nameof(CaseLifecycleState.PrincipalCancelled);
            workflow.ClosureOutcome = nameof(CaseClosureOutcome.PrincipalCancelled);
            await context.SaveChangesAsync();
        }

        return Assert.IsType<CaseWorkflowRecord>(await harness.Store.GetAsync(caseId, default));
    }

    private static async Task SetStateAsync(WorkflowHarness harness, Guid caseId, CaseLifecycleState state)
    {
        await using var context = await harness.Factory.CreateDbContextAsync();
        var workflow = await context.CaseWorkflows.SingleAsync(item => item.CaseId == caseId);
        workflow.State = state.ToString();
        await context.SaveChangesAsync();
    }

    // The open-query mark is exactly "an open CaseQueries row exists".
    private static async Task AssertOpenQueryMarkMatchesRowsAsync(WorkflowHarness harness, Guid caseId)
    {
        await using var context = await harness.Factory.CreateDbContextAsync();
        var marked = await context.CaseWorkflows
            .Where(item => item.CaseId == caseId)
            .Select(item => item.HasOpenQuery)
            .SingleAsync();
        Assert.Equal(
            await context.CaseQueries.AnyAsync(item => item.CaseId == caseId && item.ClosedAtUtc == null),
            marked);
    }

    [Fact]
    public async Task ReviewGatedTransitionsRefuseOnIncompletePersistedFacts()
    {
        // The gate reads the case's own Instruction complete and
        // Images complete columns inside the transition's transaction.
        await using var harness = await WorkflowHarness.CreateAsync(database);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Cases SET InstructionComplete = {false} WHERE Id = {harness.CaseId}");
        }

        var lease = await harness.Store.ClaimAsync(
            new(harness.CaseId, 0, actor, "claim-return-refused"),
            default);
        // Return to Review asks its state rule (With Engineer or Report sent)
        // before the readiness gate, so the Case is put in a state that passes it.
        await SetStateAsync(harness, harness.CaseId, CaseLifecycleState.ReportPreparation);
        await Assert.ThrowsAsync<CaseReviewReadinessException>(() =>
            harness.Store.ReturnToReviewAsync(
                new(
                    harness.CaseId,
                    0,
                    actor,
                    "return-to-review-refused",
                    "Return to Review while the record is incomplete",
                    lease.Token),
                default));
        await SetStateAsync(harness, harness.CaseId, CaseLifecycleState.Review);
        // The store refuses an unknown Engineer before the readiness gate, so the
        // hand-off names an enabled one.
        var engineerId = Guid.NewGuid();
        await harness.SeedStaffAccountAsync(engineerId, true, StaffRole.Engineer);

        await Assert.ThrowsAsync<CaseReviewReadinessException>(() =>
            harness.Store.AssignEngineerAsync(
                new(
                    harness.CaseId,
                    0,
                    actor,
                    "assign-engineer-refused",
                    "Assign Engineer while the record is incomplete",
                    lease.Token,
                    engineerId),
                CaseLifecycleState.ReportPreparation,
                default));

        var workflow = Assert.IsType<CaseWorkflowRecord>(
            await harness.Store.GetAsync(harness.CaseId, default));
        Assert.Equal(0, workflow.Version);
    }

    [Fact]
    public async Task AssignedEngineersAreReadForManyCasesAtOnceAndAnUnknownCaseIsAbsent()
    {
        await using var harness = await WorkflowHarness.CreateAsync(database);
        var unknownCaseId = Guid.NewGuid();

        var engineers = await harness.Store.GetAssignedEngineersAsync(
            [harness.CaseId, harness.NotReadyCaseId, unknownCaseId, harness.CaseId],
            default);

        Assert.Equal(2, engineers.Count);
        Assert.NotNull(engineers[harness.CaseId]);
        foreach (var caseId in new[] { harness.CaseId, harness.NotReadyCaseId })
        {
            var workflow = Assert.IsType<CaseWorkflowRecord>(await harness.Store.GetAsync(caseId, default));
            Assert.Equal(workflow.AssignedEngineerId, engineers[caseId]);
        }
        Assert.False(engineers.ContainsKey(unknownCaseId));
        Assert.Empty(await harness.Store.GetAssignedEngineersAsync([], default));
    }

    [Fact]
    public async Task StartRejectsEngineerDisabledAfterAssignment()
    {
        await using var harness = await WorkflowHarness.CreateAsync(database);
        var workflow = Assert.IsType<CaseWorkflowRecord>(
            await harness.Store.GetAsync(harness.CaseId, default));
        var engineerId = Assert.IsType<Guid>(workflow.AssignedEngineerId);
        await harness.SetStaffEnabledAsync(engineerId, false);
        var actor = ActionActor.Staff(engineerId, [StaffRole.Engineer]);
        var lease = await harness.Store.ClaimAsync(
            new(harness.CaseId, workflow.Version, actor, "claim-start-disabled-engineer"),
            default);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new StartCaseWork(harness.Store).ExecuteAsync(
                new ChangeCaseStateRequest(
                    harness.CaseId,
                    workflow.Version,
                    actor,
                    "start-disabled-engineer",
                    "Attempted start after Engineer account was disabled",
                    lease.Token),
                default));

        Assert.Contains("assigned staff account is disabled", exception.Message, StringComparison.Ordinal);
        var unchanged = Assert.IsType<CaseWorkflowRecord>(
            await harness.Store.GetAsync(harness.CaseId, default));
        Assert.Equal(CaseLifecycleState.Review, unchanged.State);
        Assert.Equal(workflow.Version, unchanged.Version);
    }

    [Fact]
    public async Task ApprovedSentPollRetainsAndAutoLinksOneAuthoritativeCaseIdempotently()
    {
        await using var harness = await WorkflowHarness.CreateIsolatedAsync();
        var staff = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var started = await new StartCaseWork(harness.Store).ExecuteAsync(
            new ChangeCaseStateRequest(
                harness.CaseId,
                0,
                staff,
                "start-for-sent-poll-auto-link",
                "Inspection work started before the retained report was sent",
                (await harness.Store.ClaimAsync(
                    new(harness.CaseId, 0, staff, "claim-for-sent-poll-auto-link"),
                    default)).Token),
            default);
        Assert.Equal(CaseLifecycleState.ReportPreparation, started.State);
        harness.TimeProvider.Advance(TimeSpan.FromMinutes(3));

        const string mailboxId = WorkflowHarness.ApprovedMailboxIdentity;
        const string mailboxAddress = WorkflowHarness.ApprovedMailboxAddress;
        const string sentFolderId = WorkflowHarness.ApprovedSentFolderIdentity;
        const string immutableItemId = "report-auto-link-item";
        var item = new ApprovedSentItem(
            "report-auto-link-occurrence",
            new string('a', 64),
            sentFolderId,
            ApprovedSentItemObservationKind.Discovered,
            new(
                mailboxId,
                mailboxAddress,
                sentFolderId,
                immutableItemId,
                "<report-auto-link@example.test>",
                "report-auto-link-conversation",
                "report-auto-link-reply-chain",
                [],
                [harness.CaseId],
                harness.TimeProvider.GetUtcNow().AddMinutes(-1),
                new string('b', 64)),
            MalformedReasonCode: null,
            "report-auto-link-cursor");
        var options = new LocalApprovedSentOptions(
            LocalApprovedSentOptions.RequiredRuntimeProfile,
            mailboxId,
            mailboxAddress,
            sentFolderId,
            Path.GetTempPath());
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPegasusInfrastructure(
            (_, builder) => builder.UseSqlServer(harness.ConnectionString));
        services.AddSingleton<TimeProvider>(harness.TimeProvider);
        services.AddLocalApprovedSent(_ => options);
        services.AddSingleton<IApprovedSentSource>(new OneItemApprovedSentSource(item));
        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();
        var poll = scope.ServiceProvider.GetRequiredService<PollSentEvidence>();
        var worker = ActionActor.SystemWorker("sent-evidence-poll");

        var first = await poll.ExecuteAsync(1, 10, worker, default);
        // The completing poll schedules the next claim a minute out; the
        // replay below must be a real due claim, so the fixture clock moves
        // past that delay first.
        harness.TimeProvider.Advance(TimeSpan.FromMinutes(2));
        var replay = await poll.ExecuteAsync(1, 10, worker, default);

        Assert.Equal(first, replay);
        Assert.Equal(1, first.ReportEvidenceRetained);
        Assert.Equal(0, first.UnlinkedItems);
        var workflow = Assert.IsType<CaseWorkflowRecord>(
            await harness.Store.GetAsync(harness.CaseId, default));
        var linked = Assert.IsType<ApprovedMailboxReportSentEvidence>(
            workflow.ReportSentEvidence);
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            var evidence = await context.CaseReportSentEvidence
                .AsNoTracking()
                .SingleAsync(e => e.Id == linked.EvidenceId);
            Assert.Equal(immutableItemId, evidence.ImmutableItemIdentity);
        }
        Assert.Equal(CaseLifecycleState.ReportSent, workflow.State);
        Assert.Equal(harness.CaseId, await harness.ReadReportEvidenceCaseIdAsync(linked.EvidenceId));
        Assert.Equal(
            1L,
            await harness.PollOutcomeCountAsync(
                immutableItemId,
                nameof(SentEvidencePollOutcomeKind.ReportEvidenceAutoLinked),
                linked.EvidenceId));
        Assert.Equal(
            1L,
            await harness.WorkflowEventTypeCountAsync(
                harness.CaseId,
                "report_evidence_auto_linked"));
        Assert.Equal(
            1L,
            await harness.ActionHistoryAggregateCountAsync(
                "report_sent_evidence",
                linked.EvidenceId.ToString("D"),
                "report_sent_evidence_retained"));
    }

    [Fact]
    public async Task ReportApprovalIsServerStampedAndSentEvidenceFollowsPreparationAndApproval()
    {
        await using var harness = await WorkflowHarness.CreateIsolatedAsync();
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var started = await new StartCaseWork(harness.Store).ExecuteAsync(
            new ChangeCaseStateRequest(
                harness.CaseId,
                0,
                actor,
                "start-report-chronology",
                "Inspection work started",
                (await harness.Store.ClaimAsync(
                    new(harness.CaseId, 0, actor, "claim-report-chronology"),
                    default)).Token),
            default);
        var linkEvidence = new LinkReportEvidence(harness.Store);
        var editLease = await harness.Store.ClaimAsync(
            new(harness.CaseId, started.Version, actor, "claim-stale-report-evidence"),
            default);
        var preparationTime = harness.TimeProvider.GetUtcNow();
        var staleEvidence = await new RetainApprovedMailboxReportSentEvidence(
            harness.ReportSentEvidenceStore).ExecuteAsync(
            new(
                Guid.NewGuid(),
                "instructions@collisionengineers.co.uk",
                "sent-folder-chronology",
                "immutable-item-before-preparation",
                "internet-message-before-preparation",
                "conversation-before-preparation",
                "reply-chain-before-preparation",
                "source-occurrence-before-preparation",
                new string('1', 64),
                new string('2', 64),
                preparationTime.AddMinutes(-2),
                preparationTime.AddMinutes(-1),
                ActionActor.SystemWorker("approved-mailbox-evidence-ingestion"),
                "retain-before-preparation"),
            default);

        await Assert.ThrowsAsync<InvalidOperationException>(() => linkEvidence.ExecuteAsync(
            new(
                harness.CaseId,
                started.Version,
                actor,
                "link-before-preparation",
                "Attempt to link an older Sent item",
                editLease.Token,
                staleEvidence.EvidenceId),
            default));
        Assert.Equal(0L, await harness.WorkflowEventCountAsync("link-before-preparation"));

        harness.TimeProvider.Advance(TimeSpan.FromMinutes(2));
        var approvalTime = harness.TimeProvider.GetUtcNow();
        var approvalRequest = new RecordCaseReportApprovalRequest(
            harness.CaseId,
            started.Version,
            actor,
            "approve-report-artifact",
            "Engineer approved the immutable report artifact",
            editLease.Token,
            new(
                Guid.NewGuid(),
                "case-report-v1.pdf",
                new string('A', 64)));
        var approve = new RecordCaseReportApproval(harness.Store);
        var approved = await approve.ExecuteAsync(approvalRequest, default);
        harness.TimeProvider.Advance(TimeSpan.FromMinutes(1));
        var approvalReplay = await approve.ExecuteAsync(approvalRequest, default);

        Assert.Equal(approved.Version, approvalReplay.Version);
        Assert.Equal(approvalRequest.Approval.ApprovalId, approved.ReportApproval?.ApprovalId);
        Assert.Equal(approvalTime, approved.ReportApproval?.ApprovedAtUtc);
        Assert.Equal(approvalTime, approvalReplay.ReportApproval?.ApprovedAtUtc);
        Assert.Equal(actor.SubjectId, approved.ReportApproval?.ApprovedBy.SubjectId);
        Assert.Equal(1L, await harness.WorkflowEventCountAsync(approvalRequest.OperationKey));

        var sentLease = await harness.Store.ClaimAsync(
            new(harness.CaseId, approved.Version, actor, "claim-approved-report-evidence"),
            default);
        var beforeApproval = await new RetainApprovedMailboxReportSentEvidence(
            harness.ReportSentEvidenceStore).ExecuteAsync(
            new(
                Guid.NewGuid(),
                "instructions@collisionengineers.co.uk",
                "sent-folder-chronology",
                "immutable-item-before-approval",
                "internet-message-before-approval",
                "conversation-before-approval",
                "reply-chain-before-approval",
                "source-occurrence-before-approval",
                new string('3', 64),
                new string('4', 64),
                approvalTime.AddMinutes(-1),
                harness.TimeProvider.GetUtcNow(),
                ActionActor.SystemWorker("approved-mailbox-evidence-ingestion"),
                "retain-before-approval"),
            default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => linkEvidence.ExecuteAsync(
            new(
                harness.CaseId,
                approved.Version,
                actor,
                "link-before-approval",
                "Attempt to link evidence predating approval",
                sentLease.Token,
                beforeApproval.EvidenceId),
            default));
        Assert.Equal(0L, await harness.WorkflowEventCountAsync("link-before-approval"));

        var qualifyingEvidence = await new RetainApprovedMailboxReportSentEvidence(
            harness.ReportSentEvidenceStore).ExecuteAsync(
            new(
                Guid.NewGuid(),
                "instructions@collisionengineers.co.uk",
                "sent-folder-chronology",
                "immutable-item-after-approval",
                "internet-message-after-approval",
                "conversation-after-approval",
                "reply-chain-after-approval",
                "source-occurrence-after-approval",
                new string('5', 64),
                new string('6', 64),
                approvalTime,
                harness.TimeProvider.GetUtcNow(),
                ActionActor.SystemWorker("approved-mailbox-evidence-ingestion"),
                "retain-after-approval"),
            default);
        var linkedResult = await linkEvidence.ExecuteAsync(
            new(
                harness.CaseId,
                approved.Version,
                actor,
                "link-after-approval",
                "Link exact approved-mailbox evidence",
                sentLease.Token,
                qualifyingEvidence.EvidenceId),
            default);
        var linked = linkedResult.Workflow;

        Assert.Equal(CaseLifecycleState.ReportSent, linked.State);
        Assert.Equal(qualifyingEvidence.EvidenceId, linked.ReportSentEvidence?.EvidenceId);
        Assert.Equal(1L, await harness.WorkflowEventCountAsync("link-after-approval"));
    }

    [Fact]
    public async Task AutoLinkUsesCanonicalVersionAndHistoryKeepsTheLeaseAndReplays()
    {
        await using var harness = await WorkflowHarness.CreateIsolatedAsync();
        var staff = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var started = await new StartCaseWork(harness.Store).ExecuteAsync(
            new ChangeCaseStateRequest(
                harness.CaseId,
                0,
                staff,
                "start-auto-link",
                "Inspection work started",
                (await harness.Store.ClaimAsync(
                    new(harness.CaseId, 0, staff, "claim-start-auto-link"),
                    default)).Token),
            default);
        _ = await harness.Store.ClaimAsync(
            new(harness.CaseId, started.Version, staff, "claim-before-auto-link"),
            default);
        harness.TimeProvider.Advance(TimeSpan.FromMinutes(3));
        var retained = await RetainReportEvidenceAsync(
            harness,
            "auto-link-success",
            harness.TimeProvider.GetUtcNow().AddMinutes(-2),
            harness.TimeProvider.GetUtcNow().AddMinutes(-1));
        var worker = ActionActor.SystemWorker("approved-mailbox-sent-poll");
        var request = new AutoLinkReportEvidenceRequest(
            harness.CaseId,
            retained.EvidenceId,
            worker,
            "auto-link-report-evidence",
            "Exact-one approved-mailbox Case match");
        var sut = new AutoLinkReportEvidence(harness.Store);

        var first = await sut.ExecuteAsync(request, default);
        harness.TimeProvider.Advance(TimeSpan.FromHours(1));
        var replay = await sut.ExecuteAsync(request, default);

        Assert.Equal(AutoLinkReportEvidenceDisposition.Linked, first.Disposition);
        Assert.Equal(AutoLinkReportEvidenceDisposition.Linked, replay.Disposition);
        var linked = Assert.IsType<AutoLinkedReportEvidence>(first.Link);
        var replayed = Assert.IsType<AutoLinkedReportEvidence>(replay.Link);
        Assert.Equal(harness.CaseId, linked.CaseId);
        Assert.Equal(retained.EvidenceId, linked.EvidenceId);
        Assert.Equal(CaseLifecycleState.ReportSent, linked.State);
        Assert.Equal(started.Version + 1, linked.Version);
        Assert.Equal(linked, replayed);
        var details = Assert.IsType<CaseHeader>(
            await harness.QueryStore.GetHeaderAsync(new(harness.CaseId, staff), default));
        Assert.Equal(
            worker.Kind,
            details.Workflow.ReportSentEvidence?.LinkedBy.Kind);
        Assert.Equal(
            worker.SubjectId,
            details.Workflow.ReportSentEvidence?.LinkedBy.SubjectId);
        Assert.Null(first.NotLinkedReasonCode);
        Assert.Null(replay.NotLinkedReasonCode);
        // System work never ends a member of staff's edit session.
        Assert.True(await harness.HasLeaseReplayMaterialAsync(harness.CaseId));
        Assert.Equal(1L, await harness.WorkflowEventCountAsync(request.OperationKey));
        Assert.Equal(
            1L,
            await harness.ActionHistoryCountAsync(
                "report_evidence_auto_linked",
                request.OperationKey));
    }

    [Fact]
    public async Task AutoLinkPolicyDenialsLeaveEvidenceUnlinkedAndWorkflowUnchanged()
    {
        await using var harness = await WorkflowHarness.CreateIsolatedAsync();
        var unreadyEvidence = await RetainReportEvidenceAsync(
            harness,
            "auto-link-unready",
            harness.TimeProvider.GetUtcNow().AddMinutes(-2),
            harness.TimeProvider.GetUtcNow().AddMinutes(-1));
        var worker = ActionActor.SystemWorker("approved-mailbox-sent-poll");
        var sut = new AutoLinkReportEvidence(harness.Store);
        var unreadyRequest = new AutoLinkReportEvidenceRequest(
            harness.NotReadyCaseId,
            unreadyEvidence.EvidenceId,
            worker,
            "auto-link-unready-case",
            "Exact-one approved-mailbox Case match");

        var unready = await sut.ExecuteAsync(unreadyRequest, default);

        Assert.Equal(AutoLinkReportEvidenceDisposition.NotLinked, unready.Disposition);
        Assert.Equal("case_not_report_preparation", unready.NotLinkedReasonCode);
        Assert.Null(unready.Link);
        Assert.Null(await harness.ReadReportEvidenceCaseIdAsync(unreadyEvidence.EvidenceId));
        var unreadyWorkflow = Assert.IsType<CaseWorkflowRecord>(
            await harness.Store.GetAsync(harness.NotReadyCaseId, default));
        Assert.Equal(CaseLifecycleState.NotReady, unreadyWorkflow.State);
        Assert.Equal(0L, unreadyWorkflow.Version);
        Assert.Equal(0L, await harness.WorkflowEventCountAsync(unreadyRequest.OperationKey));

        var staleEvidence = await RetainReportEvidenceAsync(
            harness,
            "auto-link-before-preparation",
            harness.TimeProvider.GetUtcNow().AddMinutes(-2),
            harness.TimeProvider.GetUtcNow().AddMinutes(-1));
        var staff = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var started = await new StartCaseWork(harness.Store).ExecuteAsync(
            new ChangeCaseStateRequest(
                harness.SecondCaseId,
                0,
                staff,
                "start-after-sent-evidence",
                "Inspection work started after the retained Sent item",
                (await harness.Store.ClaimAsync(
                    new(harness.SecondCaseId, 0, staff, "claim-start-after-sent"),
                    default)).Token),
            default);
        var chronologyRequest = new AutoLinkReportEvidenceRequest(
            harness.SecondCaseId,
            staleEvidence.EvidenceId,
            worker,
            "auto-link-before-preparation",
            "Exact-one approved-mailbox Case match");

        var chronology = await sut.ExecuteAsync(chronologyRequest, default);

        Assert.Equal(AutoLinkReportEvidenceDisposition.NotLinked, chronology.Disposition);
        Assert.Equal(
            "evidence_predates_report_preparation",
            chronology.NotLinkedReasonCode);
        Assert.Null(chronology.Link);
        Assert.Null(await harness.ReadReportEvidenceCaseIdAsync(staleEvidence.EvidenceId));
        var unchanged = Assert.IsType<CaseWorkflowRecord>(
            await harness.Store.GetAsync(harness.SecondCaseId, default));
        Assert.Equal(CaseLifecycleState.ReportPreparation, unchanged.State);
        Assert.Equal(started.Version, unchanged.Version);
        Assert.Equal(0L, await harness.WorkflowEventCountAsync(chronologyRequest.OperationKey));
    }

    [Fact]
    public async Task AutoLinkReplayAfterStaffRelinkDoesNotOverwriteCurrentAssociation()
    {
        await using var harness = await WorkflowHarness.CreateIsolatedAsync();
        var staff = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var firstStarted = await new StartCaseWork(harness.Store).ExecuteAsync(
            new ChangeCaseStateRequest(
                harness.CaseId,
                0,
                staff,
                "start-auto-link-origin",
                "First inspection work started",
                (await harness.Store.ClaimAsync(
                    new(harness.CaseId, 0, staff, "claim-auto-link-origin"),
                    default)).Token),
            default);
        var secondStarted = await new StartCaseWork(harness.Store).ExecuteAsync(
            new ChangeCaseStateRequest(
                harness.SecondCaseId,
                0,
                staff,
                "start-auto-link-target",
                "Second inspection work started",
                (await harness.Store.ClaimAsync(
                    new(harness.SecondCaseId, 0, staff, "claim-auto-link-target"),
                    default)).Token),
            default);
        harness.TimeProvider.Advance(TimeSpan.FromMinutes(3));
        var retained = await RetainReportEvidenceAsync(
            harness,
            "auto-link-staff-relink",
            harness.TimeProvider.GetUtcNow().AddMinutes(-2),
            harness.TimeProvider.GetUtcNow().AddMinutes(-1));
        var autoRequest = new AutoLinkReportEvidenceRequest(
            harness.CaseId,
            retained.EvidenceId,
            ActionActor.SystemWorker("approved-mailbox-sent-poll"),
            "auto-link-before-staff-relink",
            "Exact-one approved-mailbox Case match");
        var autoLink = new AutoLinkReportEvidence(harness.Store);
        var autoLinked = await autoLink.ExecuteAsync(autoRequest, default);
        var linkedAssociation = Assert.IsType<AutoLinkedReportEvidence>(autoLinked.Link);
        Assert.Equal(firstStarted.Version + 1, linkedAssociation.Version);

        var closed = await ArrangeClosedFromReportSentAsync(harness, harness.CaseId);
        var reopened = await new ReopenCase(harness.Store).ExecuteAsync(
            new(
                harness.CaseId,
                closed.Version,
                staff,
                "reopen-before-auto-unlink",
                "Return to report preparation to correct the evidence",
                (await harness.Store.ClaimAsync(
                    new(
                        harness.CaseId,
                        closed.Version,
                        staff,
                        "claim-reopen-before-auto-unlink"),
                    default)).Token,
                CaseReopenDestination.ReportPreparation),
            default);
        var unlinked = await new UnlinkReportEvidence(harness.Store).ExecuteAsync(
            new(
                harness.CaseId,
                reopened.Version,
                staff,
                "unlink-auto-linked-evidence",
                "The exact retained item belongs to the second Case",
                (await harness.Store.ClaimAsync(
                    new(
                        harness.CaseId,
                        reopened.Version,
                        staff,
                        "claim-unlink-auto-evidence"),
                    default)).Token,
                retained.EvidenceId),
            default);
        Assert.Null(unlinked.ReportSentEvidence);

        var staffLinkedResult = await new LinkReportEvidence(harness.Store).ExecuteAsync(
            new(
                harness.SecondCaseId,
                secondStarted.Version,
                staff,
                "staff-link-after-auto-unlink",
                "Associate the retained Sent item with its accepted Case",
                (await harness.Store.ClaimAsync(
                    new(
                        harness.SecondCaseId,
                        secondStarted.Version,
                        staff,
                        "claim-staff-link-after-auto"),
                    default)).Token,
                retained.EvidenceId),
            default);
        var staffLinked = staffLinkedResult.Workflow;

        var staleReplay = await autoLink.ExecuteAsync(autoRequest, default);

        Assert.Equal(AutoLinkReportEvidenceDisposition.NotLinked, staleReplay.Disposition);
        Assert.Equal("concurrency_conflict", staleReplay.NotLinkedReasonCode);
        Assert.Null(staleReplay.Link);
        Assert.Equal(
            harness.SecondCaseId,
            await harness.ReadReportEvidenceCaseIdAsync(retained.EvidenceId));
        Assert.Equal(retained.EvidenceId, staffLinked.ReportSentEvidence?.EvidenceId);
        var original = Assert.IsType<CaseWorkflowRecord>(
            await harness.Store.GetAsync(harness.CaseId, default));
        Assert.Equal(CaseLifecycleState.ReportPreparation, original.State);
        Assert.Null(original.ReportSentEvidence);
        Assert.Equal(1L, await harness.WorkflowEventCountAsync(autoRequest.OperationKey));
    }

    [Fact]
    public async Task ConcurrentStaffAndWorkerLinkExactlyOneCaseAssociation()
    {
        await using var harness = await WorkflowHarness.CreateIsolatedAsync();
        var staff = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var started = await new StartCaseWork(harness.Store).ExecuteAsync(
            new ChangeCaseStateRequest(
                harness.CaseId,
                0,
                staff,
                "start-concurrent-evidence-link",
                "Inspection work started",
                (await harness.Store.ClaimAsync(
                    new(harness.CaseId, 0, staff, "claim-start-concurrent-link"),
                    default)).Token),
            default);
        var staffLease = await harness.Store.ClaimAsync(
            new(harness.CaseId, started.Version, staff, "claim-concurrent-staff-link"),
            default);
        harness.TimeProvider.Advance(TimeSpan.FromMinutes(3));
        var retained = await RetainReportEvidenceAsync(
            harness,
            "concurrent-staff-worker-link",
            harness.TimeProvider.GetUtcNow().AddMinutes(-2),
            harness.TimeProvider.GetUtcNow().AddMinutes(-1));
        const string staffOperationKey = "concurrent-staff-evidence-link";
        const string autoOperationKey = "concurrent-worker-evidence-link";

        var staffTask = new LinkReportEvidence(harness.Store).ExecuteAsync(
            new(
                harness.CaseId,
                started.Version,
                staff,
                staffOperationKey,
                "Staff selected the exact retained Sent item",
                staffLease.Token,
                retained.EvidenceId),
            default);
        var autoTask = new AutoLinkReportEvidence(harness.Store).ExecuteAsync(
            new(
                harness.CaseId,
                retained.EvidenceId,
                ActionActor.SystemWorker("approved-mailbox-sent-poll"),
                autoOperationKey,
                "Exact-one approved-mailbox Case match"),
            default);
        var combined = Task.WhenAll(staffTask, autoTask);
        var completed = await Task.WhenAny(
            combined,
            Task.Delay(TimeSpan.FromSeconds(30)));
        Assert.Same(combined, completed);
        _ = combined.Exception;
        Assert.True(
            autoTask.IsCompletedSuccessfully,
            autoTask.Exception?.GetBaseException().Message);
        var autoResult = await autoTask;
        var workerLinked =
            autoResult.Disposition == AutoLinkReportEvidenceDisposition.Linked;

        Assert.NotEqual(staffTask.IsCompletedSuccessfully, workerLinked);
        var persisted = Assert.IsType<CaseWorkflowRecord>(
            await harness.Store.GetAsync(harness.CaseId, default));
        Assert.Equal(CaseLifecycleState.ReportSent, persisted.State);
        Assert.Equal(retained.EvidenceId, persisted.ReportSentEvidence?.EvidenceId);
        Assert.Equal(
            1L,
            await harness.WorkflowEventCountAsync(staffOperationKey)
                + await harness.WorkflowEventCountAsync(autoOperationKey));
        // The staff link ends its lease; the Worker's link leaves it, and the
        // refused staff link then writes nothing.
        Assert.Equal(workerLinked, await harness.HasLeaseReplayMaterialAsync(harness.CaseId));
    }

    [Theory]
    [InlineData(false, true, StaffRole.User, "does not exist")]
    [InlineData(true, false, StaffRole.User, "is disabled")]
    public async Task MissingOrDisabledStaffCannotBeAssigned(
        bool createAccount,
        bool isEnabled,
        StaffRole role,
        string expectedMessage)
    {
        await using var harness = await WorkflowHarness.CreateAsync(database);
        var engineerId = Guid.NewGuid();
        if (createAccount)
        {
            await harness.SeedStaffAccountAsync(engineerId, isEnabled, role);
        }

        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        var before = Assert.IsType<CaseWorkflowRecord>(
            await harness.Store.GetAsync(harness.CaseId, default));
        var lease = await harness.Store.ClaimAsync(
            new(harness.CaseId, before.Version, actor, "claim-ineligible-assignment"),
            default);
        var operationKey = $"assign-ineligible-{role}-{isEnabled}-{createAccount}";
        var sut = new AssignCaseEngineer(
            harness.Store,
            new CaseDataCompletenessPersistenceTests.FixedConfiguration(),
            new EfStaffAccountQueries(harness.Factory, new StaffAccountReads(TimeProvider.System)));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ExecuteAsync(
                new(
                    harness.CaseId,
                    before.Version,
                    actor,
                    operationKey,
                    "Attempt to assign ineligible staff",
                    lease.Token,
                    engineerId),
                default));

        Assert.Contains(expectedMessage, exception.Message, StringComparison.Ordinal);
        var persisted = await harness.Store.GetAsync(harness.CaseId, default);
        Assert.Equal(before.AssignedEngineerId, persisted?.AssignedEngineerId);
        Assert.Equal(before.Version, persisted?.Version);
        Assert.Equal(0L, await harness.WorkflowEventCountAsync(operationKey));
    }

    [Fact]
    public async Task NativeHandoffToAnEnabledUserIsAtomicGuardedAndReplaySurvivesLaterDisablement()
    {
        await using var harness = await WorkflowHarness.CreateIsolatedAsync();
        var engineerId = Guid.NewGuid();
        await harness.SeedStaffAccountAsync(
            engineerId,
            true,
            StaffRole.User,
            isSignOffEngineer: true);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        var before = Assert.IsType<CaseWorkflowRecord>(
            await harness.Store.GetAsync(harness.CaseId, default));
        var lease = await harness.Store.ClaimAsync(
            new(harness.CaseId, before.Version, actor, "claim-eligible-assignment"),
            default);
        var request = new AssignCaseEngineerRequest(
            harness.CaseId,
            before.Version,
            actor,
            "assign-eligible-user",
            "Assign enabled User",
            lease.Token,
            engineerId);
        var sut = new AssignCaseEngineer(
            harness.Store,
            new CaseDataCompletenessPersistenceTests.FixedConfiguration(),
            new EfStaffAccountQueries(harness.Factory, new StaffAccountReads(TimeProvider.System)));

        await Assert.ThrowsAsync<CaseVersionConflictException>(() =>
            sut.ExecuteAsync(request with { ExpectedVersion = before.Version + 1 }, default));
        await Assert.ThrowsAsync<CaseEditLeaseConflictException>(() =>
            sut.ExecuteAsync(request with { EditLeaseToken = new string('x', lease.Token.Length) }, default));
        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            sut.ExecuteAsync(request with { Actor = ActionActor.SystemWorker("handoff-refused") }, default));
        Assert.Equal(before, await harness.Store.GetAsync(harness.CaseId, default));
        Assert.Equal(0L, await harness.WorkflowEventCountAsync(request.OperationKey));

        var assigned = await sut.ExecuteAsync(request, default);
        await harness.SetStaffEnabledAsync(engineerId, false);
        await using var eligibilityContext = await harness.Factory.CreateDbContextAsync();
        var disabled = await EfCaseEngineerEligibility.ReadAsync(eligibilityContext, engineerId, default);
        var replay = await sut.ExecuteAsync(request, default);

        Assert.False(disabled.IsEnabled);
        Assert.Equal(engineerId, assigned.AssignedEngineerId);
        Assert.Equal(engineerId, assigned.SignOffEngineerId);
        Assert.Equal(CaseLifecycleState.ReportPreparation, assigned.State);
        Assert.Equal(before.Version + 1, assigned.Version);
        Assert.Equal(assigned, replay);
        Assert.Equal(1L, await harness.WorkflowEventCountAsync(request.OperationKey));
        Assert.Equal(1L, await harness.WorkflowEventTypeCountAsync(
            harness.CaseId, "state_ReportPreparation"));
        var nativeAccess = await new EfAssessmentAccessSource(harness.Factory).GetAsync(harness.CaseId);
        Assert.NotNull(nativeAccess);
        Assert.True(nativeAccess.CanOpen);
        Assert.False(nativeAccess.IsReadOnly);
        Assert.Null((await harness.QueryStore.GetHeaderAsync(new(harness.CaseId, actor), default))?.ActiveEditLease);

        // Core composes the reason, so a caller's changed words replay; a
        // changed hand-off is different input under the same key.
        Assert.Equal(assigned, await sut.ExecuteAsync(request with { Reason = "Changed replay input" }, default));
        await Assert.ThrowsAsync<CaseOperationConflictException>(() =>
            sut.ExecuteAsync(request with { EngineerId = null }, default));
        Assert.Equal(
            $"Assigned to workflow-test-{engineerId:N}",
            Assert.Single(await harness.WorkflowEventsAsync(request.OperationKey)).Reason);

        // The handoff writes the existing state event used by Sent chronology,
        // not a new assignment-only event or invented external delivery.
        var handoffAt = harness.TimeProvider.GetUtcNow();
        var earlier = await RetainReportEvidenceAsync(
            harness, "before-native-handoff", handoffAt.AddMinutes(-2), handoffAt.AddMinutes(-1));
        var sentLease = await harness.Store.ClaimAsync(
            new(harness.CaseId, assigned.Version, actor, "claim-native-sent"), default);
        var link = new LinkReportEvidence(harness.Store);
        await Assert.ThrowsAsync<InvalidOperationException>(() => link.ExecuteAsync(
            new(harness.CaseId, assigned.Version, actor, "native-before-sent",
                "Older Sent item", sentLease.Token, earlier.EvidenceId), default));
        harness.TimeProvider.Advance(TimeSpan.FromMinutes(3));
        var later = await RetainReportEvidenceAsync(
            harness, "after-native-handoff", handoffAt.AddMinutes(1), handoffAt.AddMinutes(2));
        var linked = (await link.ExecuteAsync(
            new(harness.CaseId, assigned.Version, actor, "native-after-sent",
                "Retained Sent item after handoff", sentLease.Token, later.EvidenceId), default)).Workflow;
        Assert.Equal(CaseLifecycleState.ReportSent, linked.State);
        Assert.Equal(later.EvidenceId, linked.ReportSentEvidence?.EvidenceId);
    }

    [Fact]
    public async Task ArchivedCaseProjectionRetainsPersistedSignOffEngineer()
    {
        await using var harness = await WorkflowHarness.CreateAsync(database);
        var signOffEngineerId = Guid.NewGuid();
        await harness.SeedStaffAccountAsync(
            signOffEngineerId,
            true,
            StaffRole.Engineer,
            isSignOffEngineer: true);
        var archivedBy = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            var workflow = await context.CaseWorkflows.SingleAsync(
                item => item.CaseId == harness.CaseId);
            workflow.SignOffEngineerId = signOffEngineerId;
            workflow.ArchivedAtUtc = harness.TimeProvider.GetUtcNow();
            workflow.ArchivedByKind = archivedBy.Kind.ToString();
            workflow.ArchivedBySubjectId = archivedBy.SubjectId;
            workflow.ArchivedByRolesJson = System.Text.Json.JsonSerializer.Serialize(
                archivedBy.Roles.OrderBy(role => role));
            workflow.ArchiveReason = "Projection test archive";
            await context.SaveChangesAsync();
        }

        var details = Assert.IsType<CaseHeader>(
            await harness.QueryStore.GetHeaderAsync(new(harness.CaseId, archivedBy), default));

        Assert.NotNull(details.Workflow.Archive);
        Assert.Equal(signOffEngineerId, details.Workflow.SignOffEngineerId);
        Assert.NotEqual(details.Workflow.AssignedEngineerId, details.Workflow.SignOffEngineerId);
    }

    [Fact]
    public async Task FabricatedOrAlreadyLinkedSentEvidenceCannotTransitionAnotherCase()
    {
        await using var harness = await WorkflowHarness.CreateIsolatedAsync();
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var started = await new StartCaseWork(harness.Store).ExecuteAsync(
            new ChangeCaseStateRequest(
                harness.CaseId,
                0,
                actor,
                "start-evidence-case",
                "Inspection work started",
                (await harness.Store.ClaimAsync(
                    new(harness.CaseId, 0, actor, "claim-evidence-case"),
                    default)).Token),
            default);
        var sentLease = await harness.Store.ClaimAsync(
            new(harness.CaseId, started.Version, actor, "claim-evidence-link"),
            default);
        var linkEvidence = new LinkReportEvidence(harness.Store);

        await Assert.ThrowsAsync<InvalidOperationException>(() => linkEvidence.ExecuteAsync(
            new(
                harness.CaseId,
                started.Version,
                actor,
                "fabricated-evidence",
                "Caller supplied an unknown identifier",
                sentLease.Token,
                Guid.NewGuid()),
            default));

        var afterFabricated = await harness.Store.GetAsync(harness.CaseId, default);
        Assert.Equal(CaseLifecycleState.ReportPreparation, afterFabricated?.State);
        Assert.Equal(started.Version, afterFabricated?.Version);
        Assert.Null(afterFabricated?.ReportSentEvidence);
        Assert.Equal(0L, await harness.WorkflowEventCountAsync("fabricated-evidence"));

        harness.TimeProvider.Advance(TimeSpan.FromMinutes(3));
        var unverifiedEvidenceId = await harness.SeedUnverifiedReportEvidenceAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => linkEvidence.ExecuteAsync(
            new(
                harness.CaseId,
                started.Version,
                actor,
                "unverified-evidence",
                "Caller selected an unverified legacy row",
                sentLease.Token,
                unverifiedEvidenceId),
            default));
        Assert.Equal(0L, await harness.WorkflowEventCountAsync("unverified-evidence"));

        var discoveredAtUtc = harness.TimeProvider.GetUtcNow().AddMinutes(-1);
        var retained = await new RetainApprovedMailboxReportSentEvidence(
            harness.ReportSentEvidenceStore).ExecuteAsync(
            new(
                Guid.NewGuid(),
                "instructions@collisionengineers.co.uk",
                "sent-folder-identity-1",
                "immutable-item-exclusive",
                "internet-message-exclusive",
                "conversation-exclusive",
                "reply-chain-exclusive",
                "source-occurrence-exclusive",
                new string('c', 64),
                new string('d', 64),
                discoveredAtUtc.AddMinutes(-1),
                discoveredAtUtc,
                ActionActor.SystemWorker("approved-mailbox-evidence-ingestion"),
                "retain-exclusive-evidence"),
            default);
        var linkRequest = new LinkReportEvidenceRequest(
            harness.CaseId,
            started.Version,
            actor,
            "link-exclusive-evidence",
            "Exact retained Sent evidence linked",
            sentLease.Token,
            retained.EvidenceId);
        var linked = (await linkEvidence.ExecuteAsync(linkRequest, default)).Workflow;
        var replay = (await linkEvidence.ExecuteAsync(linkRequest, default)).Workflow;

        Assert.Equal(linked.CaseId, replay.CaseId);
        Assert.Equal(linked.State, replay.State);
        Assert.Equal(linked.Version, replay.Version);
        Assert.Equal(
            linked.ReportSentEvidence?.EvidenceId,
            replay.ReportSentEvidence?.EvidenceId);
        Assert.Equal(retained.EvidenceId, linked.ReportSentEvidence?.EvidenceId);
        Assert.Equal(1L, await harness.WorkflowEventCountAsync("link-exclusive-evidence"));

        var secondStarted = await new StartCaseWork(harness.Store).ExecuteAsync(
            new ChangeCaseStateRequest(
                harness.SecondCaseId,
                0,
                actor,
                "start-second-evidence-case",
                "Second inspection work started",
                (await harness.Store.ClaimAsync(
                    new(harness.SecondCaseId, 0, actor, "claim-second-evidence-case"),
                    default)).Token),
            default);
        var secondLease = await harness.Store.ClaimAsync(
            new(harness.SecondCaseId, secondStarted.Version, actor, "claim-second-evidence-link"),
            default);

        await Assert.ThrowsAsync<InvalidOperationException>(() => linkEvidence.ExecuteAsync(
            new(
                harness.SecondCaseId,
                secondStarted.Version,
                actor,
                "reuse-exclusive-evidence",
                "Attempt to reuse another case's evidence",
                secondLease.Token,
                retained.EvidenceId),
            default));

        var secondPersisted = await harness.Store.GetAsync(harness.SecondCaseId, default);
        Assert.Equal(CaseLifecycleState.ReportPreparation, secondPersisted?.State);
        Assert.Null(secondPersisted?.ReportSentEvidence);
        Assert.Equal(secondStarted.Version, secondPersisted?.Version);
        Assert.Equal(0L, await harness.WorkflowEventCountAsync("reuse-exclusive-evidence"));
        var firstPersisted = await harness.Store.GetAsync(harness.CaseId, default);
        Assert.Equal(CaseLifecycleState.ReportSent, firstPersisted?.State);
        Assert.Equal(linked.Version, firstPersisted?.Version);
        Assert.Equal(retained.EvidenceId, firstPersisted?.ReportSentEvidence?.EvidenceId);
    }

    [Fact]
    public async Task ReportEvidenceUnlinkRequiresReportPreparationAndPreservesRetainedEvidence()
    {
        await using var harness = await WorkflowHarness.CreateIsolatedAsync();
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var started = await new StartCaseWork(harness.Store).ExecuteAsync(
            new ChangeCaseStateRequest(
                harness.CaseId,
                0,
                actor,
                "start-unlink-evidence",
                "Inspection work started",
                (await harness.Store.ClaimAsync(
                    new(harness.CaseId, 0, actor, "claim-start-unlink"),
                    default)).Token),
            default);
        harness.TimeProvider.Advance(TimeSpan.FromMinutes(3));
        var discoveredAtUtc = harness.TimeProvider.GetUtcNow().AddMinutes(-1);
        var retained = await new RetainApprovedMailboxReportSentEvidence(
            harness.ReportSentEvidenceStore).ExecuteAsync(
            new(
                Guid.NewGuid(),
                "instructions@collisionengineers.co.uk",
                "sent-folder-unlink",
                "immutable-item-unlink",
                "internet-message-unlink",
                "conversation-unlink",
                "reply-chain-unlink",
                "source-occurrence-unlink",
                new string('e', 64),
                new string('f', 64),
                discoveredAtUtc.AddMinutes(-1),
                discoveredAtUtc,
                ActionActor.SystemWorker("approved-mailbox-evidence-ingestion"),
                "retain-unlink-evidence"),
            default);
        var linkedResult = await new LinkReportEvidence(harness.Store).ExecuteAsync(
            new(
                harness.CaseId,
                started.Version,
                actor,
                "link-unlink-evidence",
                "Link exact Sent item",
                (await harness.Store.ClaimAsync(
                    new(harness.CaseId, started.Version, actor, "claim-link-unlink"),
                    default)).Token,
                retained.EvidenceId),
            default);
        var linked = linkedResult.Workflow;
        var unlinkEvidence = new UnlinkReportEvidence(harness.Store);
        var postReportLease = await harness.Store.ClaimAsync(
            new(harness.CaseId, linked.Version, actor, "claim-post-report-correction"),
            default);

        await Assert.ThrowsAsync<InvalidOperationException>(() => unlinkEvidence.ExecuteAsync(
            new(
                harness.CaseId,
                linked.Version,
                actor,
                "unlink-while-post-report",
                "Attempt unlink before reasoned reopen",
                postReportLease.Token,
                retained.EvidenceId),
            default));

        await harness.Store.ReleaseAsync(
            new(harness.CaseId, actor, "release-post-report-correction", postReportLease.Token),
            default);
        var closed = await ArrangeClosedFromReportSentAsync(harness, harness.CaseId);
        var reopened = await new ReopenCase(harness.Store).ExecuteAsync(
            new(
                harness.CaseId,
                closed.Version,
                actor,
                "reopen-before-unlink",
                "Report evidence must be corrected",
                (await harness.Store.ClaimAsync(
                    new(harness.CaseId, closed.Version, actor, "claim-reopen-before-unlink"),
                    default)).Token,
                CaseReopenDestination.ReportPreparation),
            default);
        var unlinkRequest = new UnlinkReportEvidenceRequest(
            harness.CaseId,
            reopened.Version,
            actor,
            "unlink-report-evidence",
            "Incorrect retained Sent item was associated",
            (await harness.Store.ClaimAsync(
                new(harness.CaseId, reopened.Version, actor, "claim-unlink-evidence"),
                default)).Token,
            retained.EvidenceId);

        var unlinked = await unlinkEvidence.ExecuteAsync(unlinkRequest, default);
        var replay = await unlinkEvidence.ExecuteAsync(unlinkRequest, default);

        Assert.Equal(CaseLifecycleState.ReportPreparation, unlinked.State);
        Assert.Null(unlinked.ReportSentEvidence);
        Assert.Equal(unlinked, replay);
        Assert.Null(await harness.ReadReportEvidenceCaseIdAsync(retained.EvidenceId));
        Assert.Equal(1L, await harness.WorkflowEventCountAsync("unlink-report-evidence"));
        Assert.Equal(0L, await harness.WorkflowEventCountAsync("unlink-while-post-report"));
    }

    [Fact]
    public async Task HoldReleaseRestoresReportPreparationAndNotReadyChaseInterval()
    {
        await using var harness = await WorkflowHarness.CreateAsync(database);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var startLease = await harness.Store.ClaimAsync(
            new(harness.CaseId, 0, actor, "claim-start"),
            default);
        var reportPreparation = await new StartCaseWork(harness.Store).ExecuteAsync(
            new ChangeCaseStateRequest(
                harness.CaseId,
                0,
                actor,
                "start-1",
                "Inspection work started",
                startLease.Token),
            default);
        var reportHoldLease = await harness.Store.ClaimAsync(
            new(harness.CaseId, reportPreparation.Version, actor, "claim-report-hold"),
            default);
        var reportHeld = await new PutCaseOnHold(harness.Store, harness.TimeProvider).ExecuteAsync(
            new(
                harness.CaseId,
                reportPreparation.Version,
                actor,
                "hold-report",
                "Awaiting provider clarification",
                reportHoldLease.Token),
            default);
        var reportReleaseLease = await harness.Store.ClaimAsync(
            new(harness.CaseId, reportHeld.Version, actor, "claim-report-release"),
            default);
        var reportReleased = await new ReleaseCaseHold(harness.Store).ExecuteAsync(
            new ChangeCaseStateRequest(
                harness.CaseId,
                reportHeld.Version,
                actor,
                "release-report",
                "Clarification received",
                reportReleaseLease.Token),
            default);

        Assert.Equal(CaseLifecycleState.ReportPreparation, reportReleased.State);
        Assert.Null(reportReleased.DueWork);

        var dueBeforeHold = (await harness.Store.GetAsync(harness.NotReadyCaseId, default))!.DueWork!;
        var expectedRemaining = dueBeforeHold.NextChaseAtUtc!.Value - harness.TimeProvider.GetUtcNow();
        var chaseHoldLease = await harness.Store.ClaimAsync(
            new(harness.NotReadyCaseId, 0, actor, "claim-chase-hold"),
            default);
        var chaseHeld = await new PutCaseOnHold(harness.Store, harness.TimeProvider).ExecuteAsync(
            new(
                harness.NotReadyCaseId,
                0,
                actor,
                "hold-chase",
                "Missing evidence is temporarily unavailable",
                chaseHoldLease.Token),
            default);

        Assert.Equal(CaseLifecycleState.Held, chaseHeld.State);
        Assert.Equal(CaseDueWorkState.Held, chaseHeld.DueWork?.State);
        Assert.Equal(expectedRemaining, chaseHeld.DueWork?.RemainingChaseInterval);
        Assert.Equal(harness.TimeProvider.GetUtcNow(), chaseHeld.DueWork?.HeldAtUtc);

        harness.TimeProvider.Advance(TimeSpan.FromDays(2));
        var chaseReleaseLease = await harness.Store.ClaimAsync(
            new(harness.NotReadyCaseId, chaseHeld.Version, actor, "claim-chase-release"),
            default);
        var chaseReleased = await new ReleaseCaseHold(harness.Store).ExecuteAsync(
            new ChangeCaseStateRequest(
                harness.NotReadyCaseId,
                chaseHeld.Version,
                actor,
                "release-chase",
                "Missing evidence can be chased again",
                chaseReleaseLease.Token),
            default);

        Assert.Equal(CaseLifecycleState.NotReady, chaseReleased.State);
        Assert.Equal(CaseDueWorkState.Scheduled, chaseReleased.DueWork?.State);
        Assert.Equal(
            harness.TimeProvider.GetUtcNow() + expectedRemaining,
            chaseReleased.DueWork?.NextChaseAtUtc);
        Assert.Null(chaseReleased.DueWork?.HeldAtUtc);
        Assert.Null(chaseReleased.DueWork?.RemainingChaseInterval);
    }

    [Fact]
    public async Task ManualChaseUsesTrustedUtcAndExactReplayDoesNotDuplicateWork()
    {
        await using var harness = await WorkflowHarness.CreateAsync(database);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var lease = await harness.Store.ClaimAsync(
            new(harness.NotReadyCaseId, 0, actor, "claim-manual-chase"),
            default);
        var record = new RecordManualCaseChase(
            harness.Store,
            harness.Store,
            harness.TimeProvider);
        var attemptedAtUtc = harness.TimeProvider.GetUtcNow();
        var request = new ManualChaseRecord(
            harness.NotReadyCaseId,
            lease.Version,
            lease.Token,
            actor,
            "record-manual-chase",
            "email",
            "claims@qdosassist.co.uk",
            attemptedAtUtc,
            "Provider confirmed the images will follow",
            "Awaiting the promised upload.");

        await Assert.ThrowsAsync<ArgumentException>(() => record.ExecuteAsync(
            request with { AttemptedAtUtc = attemptedAtUtc.AddMinutes(1) },
            default));

        var recorded = await record.ExecuteAsync(request, default);
        var replay = await record.ExecuteAsync(request, default);

        Assert.Equal(recorded, replay);
        Assert.Equal(CaseDueWorkState.Scheduled, recorded.State);
        Assert.Equal("email", recorded.MostRecentChannel);
        Assert.Equal("Provider confirmed the images will follow", recorded.MostRecentOutcome);
        Assert.Equal("Awaiting the promised upload.", recorded.MostRecentNote);
        Assert.Equal(CaseChaseSchedule.NextChaseAt(attemptedAtUtc), recorded.NextChaseAtUtc);
        Assert.Equal(1L, recorded.Version);
        Assert.Equal(1L, await harness.WorkflowEventCountAsync(request.OperationKey));
        var history = await harness.QueryStore.ListHistoryByCursorAsync(
            harness.NotReadyCaseId, null, null, 20, default);
        var chase = Assert.Single(history, entry => entry.EventType == "manual_chase_recorded");
        Assert.Equal(
            "email to claims@qdosassist.co.uk: Provider confirmed the images will follow — Awaiting the promised upload.",
            chase.Reason);
    }

    [Fact]
    public async Task ReopenToReportSentWithoutRetainedSentEvidenceFailsClosed()
    {
        await using var harness = await WorkflowHarness.CreateAsync(database);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        var closeLease = await harness.Store.ClaimAsync(
            new(harness.SecondCaseId, 0, actor, "claim-close"),
            default);
        var closed = await new CloseCase(harness.Store).ExecuteAsync(
            new(
                harness.SecondCaseId,
                0,
                actor,
                "close-principal",
                "Principal cancelled before report delivery",
                closeLease.Token,
                CaseClosureOutcome.PrincipalCancelled),
            default);
        var reopenLease = await harness.Store.ClaimAsync(
            new(harness.SecondCaseId, closed.Version, actor, "claim-reopen"),
            default);
        var reopen = new ReopenCase(harness.Store);

        await Assert.ThrowsAsync<InvalidOperationException>(() => reopen.ExecuteAsync(
            new(
                harness.SecondCaseId,
                closed.Version,
                actor,
                "reopen-post-report",
                "Provider asks to resume after cancellation",
                reopenLease.Token,
                CaseReopenDestination.ReportSent),
            default));

        var persisted = await harness.Store.GetAsync(harness.SecondCaseId, default);
        Assert.Equal(CaseLifecycleState.PrincipalCancelled, persisted?.State);
        Assert.Equal(closed.Version, persisted?.Version);
        Assert.Null(persisted?.ReportSentEvidence);
    }

    [Fact]
    public async Task EditLeaseClaimAndRenewExpireFiveMinutesFromServerTime()
    {
        await using var harness = await WorkflowHarness.CreateAsync(database);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var claimedAtUtc = harness.TimeProvider.GetUtcNow();

        var lease = await harness.Store.ClaimAsync(
            new(harness.CaseId, 0, actor, "claim-lease"),
            default);

        Assert.Equal(claimedAtUtc.AddMinutes(5), lease.ExpiresAtUtc);

        harness.TimeProvider.Advance(TimeSpan.FromMinutes(1));
        var renewRequest = new RenewCaseEditLeaseRequest(
            harness.CaseId,
            0,
            actor,
            "renew-lease",
            lease.Token);
        var renewed = await harness.Store.RenewAsync(renewRequest, default);
        harness.TimeProvider.Advance(TimeSpan.FromMinutes(1));
        var replay = await harness.Store.RenewAsync(renewRequest, default);

        Assert.Equal(renewed, replay);
        Assert.Equal(claimedAtUtc.AddMinutes(6), renewed.ExpiresAtUtc);
        Assert.Equal(
            1,
            await harness.LeaseOperationCountAsync(
                harness.CaseId,
                renewRequest.OperationKey));
    }

    [Fact]
    public async Task ExactLeaseClaimReplayRecoversOpaqueTokenWithoutExtendingExpiry()
    {
        await using var harness = await WorkflowHarness.CreateAsync(database);
        var staffId = Guid.NewGuid();
        var actor = ActionActor.Staff(
            staffId,
            [StaffRole.Administrator]);
        var request = new ClaimCaseEditLeaseRequest(
            harness.CaseId,
            0,
            actor,
            "claim-replay");

        var claimed = await harness.Store.ClaimAsync(request, default);
        harness.TimeProvider.Advance(TimeSpan.FromMinutes(1));
        var replay = await harness.Store.ClaimAsync(
            request with
            {
                Actor = ActionActor.Staff(
                    staffId,
                    [StaffRole.Administrator])
            },
            default);

        Assert.Equal(claimed, replay);
        Assert.Equal(claimed.Token, replay.Token);
        Assert.Equal(claimed.ExpiresAtUtc, replay.ExpiresAtUtc);
        Assert.True(await harness.HasLeaseReplayMaterialAsync(harness.CaseId));
        Assert.Equal(0, await harness.WorkflowEventCountAsync("claim-replay"));
        Assert.Equal(
            1,
            await harness.LeaseOperationCountAsync(
                harness.CaseId,
                request.OperationKey));
    }

    [Fact]
    public async Task LeaseClaimOperationKeyRejectsChangedFingerprintAndCompetingClaim()
    {
        await using var harness = await WorkflowHarness.CreateAsync(database);
        var staffId = Guid.NewGuid();
        var actor = ActionActor.Staff(
            staffId,
            [StaffRole.Administrator]);
        var request = new ClaimCaseEditLeaseRequest(
            harness.CaseId,
            0,
            actor,
            "claim-fingerprint");
        _ = await harness.Store.ClaimAsync(request, default);

        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            harness.Store.ClaimAsync(
                request with
                {
                    Actor = ActionActor.SystemWorker(actor.SubjectId)
                },
                default));

        await Assert.ThrowsAsync<CaseOperationConflictException>(() =>
            harness.Store.ClaimAsync(
                request with
                {
                    Actor = ActionActor.Staff(staffId, [StaffRole.Engineer])
                },
                default));
        await Assert.ThrowsAsync<CaseOperationConflictException>(() =>
            harness.Store.ClaimAsync(
                request with { ExpectedVersion = 1 },
                default));
        await Assert.ThrowsAsync<CaseOperationConflictException>(() =>
            harness.Store.ClaimAsync(
                request with
                {
                    Actor = ActionActor.Staff(
                        Guid.NewGuid(),
                        [StaffRole.Administrator])
                },
                default));
        await Assert.ThrowsAsync<CaseEditLeaseConflictException>(() =>
            harness.Store.ClaimAsync(
                request with { OperationKey = "claim-competing" },
                default));
        await Assert.ThrowsAsync<CaseOperationConflictException>(() =>
            harness.Store.RenewAsync(
                new(
                    harness.CaseId,
                    0,
                    actor,
                    request.OperationKey,
                    request.OperationKey),
                default));

        var unrelatedCaseLease = await harness.Store.ClaimAsync(
            request with { CaseId = harness.SecondCaseId },
            default);
        Assert.Equal(harness.SecondCaseId, unrelatedCaseLease.CaseId);
    }

    [Fact]
    public async Task AuthorisedColleagueTakeoverRotatesTheCaseTokenAndRecordsThePriorHolder()
    {
        await using var harness = await WorkflowHarness.CreateAsync(database);
        var first = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var second = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var held = await harness.Store.ClaimAsync(
            new ClaimCaseEditLeaseRequest(harness.CaseId, 0, first, "first-claim"), default);

        var taken = await harness.Store.ClaimAsync(
            new ClaimCaseEditLeaseRequest(harness.CaseId, 0, second, "colleague-takeover")
            {
                TakeOver = true
            }, default);

        Assert.NotEqual(held.Token, taken.Token);
        Assert.Equal(second.SubjectId, taken.Holder);
        await Assert.ThrowsAsync<CaseEditLeaseConflictException>(() =>
            harness.Store.HeartbeatAsync(
                new HeartbeatCaseEditLeaseRequest(harness.CaseId, first, held.Token), default));
        // The first holder has no staff account, so the history line cannot name them: they are
        // unnamed, never their subject identifier (R520, 29 October 2026).
        var history = Assert.Single(await harness.WorkflowEventsAsync("colleague-takeover"));
        Assert.Equal("Edit lease taken over from another member of staff.", history.Reason);
        var replay = await harness.Store.ClaimAsync(
            new ClaimCaseEditLeaseRequest(harness.CaseId, 0, second, "colleague-takeover")
            {
                TakeOver = true
            }, default);
        Assert.Equal(taken.Token, replay.Token);
        Assert.Equal(1, await harness.WorkflowEventCountAsync("colleague-takeover"));
    }

    /// <summary>
    /// The QDOS26019 failure: every Case past its first change already has an event at its current
    /// version, and the takeover's history line is written at that same version. The takeover is
    /// history, not a Case change, so it neither collides with that event nor with an earlier
    /// takeover at the same version.
    /// </summary>
    [Fact]
    public async Task AColleagueTakesOverACaseWhoseCurrentVersionAlreadyHasItsOwnEvent()
    {
        await using var harness = await WorkflowHarness.CreateAsync(database);
        var first = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var second = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var third = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var started = await new StartCaseWork(harness.Store).ExecuteAsync(
            new ChangeCaseStateRequest(
                harness.CaseId,
                0,
                first,
                "start-before-takeover",
                "Inspection work started",
                (await harness.Store.ClaimAsync(
                    new(harness.CaseId, 0, first, "claim-start-before-takeover"),
                    default)).Token),
            default);
        Assert.Equal(1, await harness.WorkflowEventCountAsync("start-before-takeover"));
        var held = await harness.Store.ClaimAsync(
            new(harness.CaseId, started.Version, first, "claim-after-start"),
            default);

        var taken = await harness.Store.ClaimAsync(
            new(harness.CaseId, started.Version, second, "takeover-at-current-version")
            {
                TakeOver = true
            },
            default);
        var takenAgain = await harness.Store.ClaimAsync(
            new(harness.CaseId, started.Version, third, "second-takeover-at-same-version")
            {
                TakeOver = true
            },
            default);

        Assert.NotEqual(held.Token, taken.Token);
        Assert.NotEqual(taken.Token, takenAgain.Token);
        Assert.Equal(third.SubjectId, takenAgain.Holder);
        Assert.Equal(started.Version, takenAgain.Version);
        Assert.Equal(1, await harness.WorkflowEventCountAsync("takeover-at-current-version"));
        Assert.Equal(1, await harness.WorkflowEventCountAsync("second-takeover-at-same-version"));
    }

    /// <summary>
    /// A one-off command the holder makes elsewhere claims, and is refused while their lease is
    /// live, so it can never take or end their open edit session. Taking their own lease back
    /// explicitly rotates it but is not a takeover: nothing is recorded as history.
    /// </summary>
    [Fact]
    public async Task TheHoldersOwnClaimIsRefusedUnlessTakenBackAndIsNeverATakeover()
    {
        await using var harness = await WorkflowHarness.CreateAsync(database);
        var holder = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var held = await harness.Store.ClaimAsync(
            new(harness.CaseId, 0, holder, "own-first-claim"),
            default);

        await Assert.ThrowsAsync<CaseEditLeaseConflictException>(() =>
            harness.Store.ClaimAsync(
                new(harness.CaseId, 0, holder, "own-one-off-claim"),
                default));
        var takenBack = await harness.Store.ClaimAsync(
            new(harness.CaseId, 0, holder, "own-take-back") { TakeOver = true },
            default);

        Assert.NotEqual(held.Token, takenBack.Token);
        Assert.Equal(holder.SubjectId, takenBack.Holder);
        Assert.Equal(0, await harness.WorkflowEventCountAsync("own-take-back"));
        var resumed = await harness.Store.ResumeAsync(new(harness.CaseId, holder), default);
        Assert.Equal(takenBack.Token, resumed?.Token);
    }

    /// <summary>
    /// Returning to a Case puts its holder straight back into edit mode: the holder is handed
    /// their live lease, renewed for a full lease as a heartbeat renews it, with no operation row.
    /// Anyone else, and the holder once the lease has lapsed, gets nothing.
    /// </summary>
    [Fact]
    public async Task ResumeHandsTheHolderTheirLiveLeaseAndNobodyElse()
    {
        await using var harness = await WorkflowHarness.CreateAsync(database);
        var holder = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var colleague = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var held = await harness.Store.ClaimAsync(
            new(harness.CaseId, 0, holder, "claim-before-resume"),
            default);
        harness.TimeProvider.Advance(TimeSpan.FromMinutes(4));

        var resumed = await harness.Store.ResumeAsync(new(harness.CaseId, holder), default);

        Assert.NotNull(resumed);
        Assert.Equal(held.Token, resumed.Token);
        Assert.Equal(harness.TimeProvider.GetUtcNow().AddMinutes(5), resumed.ExpiresAtUtc);
        Assert.Equal(1, await harness.LeaseOperationCountAsync(harness.CaseId));
        Assert.Null(await harness.Store.ResumeAsync(new(harness.CaseId, colleague), default));
        Assert.Null(await harness.Store.ResumeAsync(
            new(harness.CaseId, ActionActor.Automation(holder.SubjectId)),
            default));

        harness.TimeProvider.Advance(TimeSpan.FromMinutes(5));
        Assert.Null(await harness.Store.ResumeAsync(new(harness.CaseId, holder), default));
    }

    /// <summary>
    /// A member of staff may take over a lease the Automation Actor holds (operator, 15 October
    /// 2026). A plain claim is still refused; Take over hands the lease to them under a new token
    /// and generation, and the Actor's next heartbeat, renew, write and release are refused. The
    /// history line reads "Edit lease taken over from AI." (operator, 16 October 2026), never the
    /// Actor's grant id, and replaying the takeover writes no second one.
    /// </summary>
    [Fact]
    public async Task StaffTakeOverOfAnAutomationHeldCaseRecordsTheAutomationActor()
    {
        await using var harness = await WorkflowHarness.CreateAsync(database);
        var automation = ActionActor.Automation($"grant:{Guid.NewGuid():N}");
        var staff = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var held = await harness.Store.ClaimAsync(
            new ClaimCaseEditLeaseRequest(harness.CaseId, 0, automation, "automation-claim"), default);

        await Assert.ThrowsAsync<CaseEditLeaseConflictException>(() =>
            harness.Store.ClaimAsync(
                new ClaimCaseEditLeaseRequest(harness.CaseId, 0, staff, "staff-plain-claim"), default));
        var takeover = new ClaimCaseEditLeaseRequest(harness.CaseId, 0, staff, "staff-takeover")
        {
            TakeOver = true
        };
        var taken = await harness.Store.ClaimAsync(takeover, default);

        Assert.NotEqual(held.Token, taken.Token);
        Assert.Equal(held.Generation + 1, taken.Generation);
        Assert.Equal(staff.SubjectId, taken.Holder);
        var row = await harness.ReadLeaseRowAsync(harness.CaseId);
        Assert.Equal(staff.SubjectId, row.Holder);
        Assert.Equal(nameof(ActorKind.Staff), row.HolderKind);
        await Assert.ThrowsAsync<CaseEditLeaseConflictException>(() =>
            harness.Store.HeartbeatAsync(
                new HeartbeatCaseEditLeaseRequest(harness.CaseId, automation, held.Token), default));
        await Assert.ThrowsAsync<CaseEditLeaseConflictException>(() =>
            harness.Store.RenewAsync(
                new RenewCaseEditLeaseRequest(harness.CaseId, 0, automation, "automation-renew", held.Token),
                default));
        await Assert.ThrowsAsync<CaseEditLeaseConflictException>(() =>
            harness.Store.CloseAsync(
                new CloseCaseRequest(
                    harness.CaseId, 0, automation, "automation-write", "Automation wrote after the takeover", held.Token,
                    CaseClosureOutcome.PrincipalCancelled),
                default));
        await Assert.ThrowsAsync<CaseEditLeaseConflictException>(() =>
            harness.Store.ReleaseAsync(
                new ReleaseCaseEditLeaseRequest(harness.CaseId, automation, "automation-release", held.Token),
                default));

        var history = Assert.Single(await harness.WorkflowEventsAsync("staff-takeover"));
        Assert.Equal("edit_lease_taken_over", history.EventType);
        Assert.Equal(nameof(ActorKind.Staff), history.ActorKind);
        Assert.Equal(staff.SubjectId, history.ActorSubjectId);
        Assert.Equal("Edit lease taken over from AI.", history.Reason);
        Assert.Equal(taken.Version, history.BeforeVersion);
        Assert.Equal(taken.Version, history.AfterVersion);

        var replay = await harness.Store.ClaimAsync(takeover, default);
        Assert.Equal(taken.Token, replay.Token);
        Assert.Equal(1, await harness.WorkflowEventCountAsync("staff-takeover"));
    }

    /// <summary>
    /// Only staff take over a lease the Automation Actor holds (operator, 16 October 2026): another
    /// Automation session asking to take it over is refused, the lease stands and nothing is
    /// recorded.
    /// </summary>
    [Fact]
    public async Task AnAutomationClaimCannotTakeOverAnAutomationHeldCase()
    {
        await using var harness = await WorkflowHarness.CreateAsync(database);
        var automation = ActionActor.Automation($"grant:{Guid.NewGuid():N}");
        var otherGrant = ActionActor.Automation($"grant:{Guid.NewGuid():N}");
        var held = await harness.Store.ClaimAsync(
            new ClaimCaseEditLeaseRequest(harness.CaseId, 0, automation, "automation-claim"), default);

        await Assert.ThrowsAsync<CaseEditLeaseConflictException>(() =>
            harness.Store.ClaimAsync(
                new ClaimCaseEditLeaseRequest(harness.CaseId, 0, otherGrant, "automation-takeover")
                {
                    TakeOver = true
                }, default));

        var row = await harness.ReadLeaseRowAsync(harness.CaseId);
        Assert.Equal(automation.SubjectId, row.Holder);
        Assert.Equal(nameof(ActorKind.Automation), row.HolderKind);
        Assert.Equal(held.Token, (await harness.Store.ClaimAsync(
            new ClaimCaseEditLeaseRequest(harness.CaseId, 0, automation, "automation-claim"), default)).Token);
        Assert.Equal(0, await harness.WorkflowEventCountAsync("automation-takeover"));
    }

    [Fact]
    public async Task LeaseReleaseAndExpiryDiscardReplayCredentialBeforeReplacement()
    {
        await using var harness = await WorkflowHarness.CreateAsync(database);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var request = new ClaimCaseEditLeaseRequest(
            harness.CaseId,
            0,
            actor,
            "claim-release");
        var released = await harness.Store.ClaimAsync(request, default);
        var releaseRequest = new ReleaseCaseEditLeaseRequest(
            harness.CaseId,
            actor,
            "release-lease",
            released.Token);
        var changedReleaseToken =
            $"{(released.Token[0] == '0' ? '1' : '0')}{released.Token[1..]}";

        await harness.Store.ReleaseAsync(releaseRequest, default);
        await harness.Store.ReleaseAsync(releaseRequest, default);

        Assert.False(await harness.HasLeaseReplayMaterialAsync(harness.CaseId));
        Assert.Equal(
            1,
            await harness.LeaseOperationCountAsync(
                harness.CaseId,
                releaseRequest.OperationKey));
        await Assert.ThrowsAsync<CaseOperationConflictException>(() =>
            harness.Store.ReleaseAsync(
                releaseRequest with { LeaseToken = changedReleaseToken },
                default));
        await Assert.ThrowsAsync<CaseEditLeaseExpiredException>(() =>
            harness.Store.RenewAsync(
                new(
                    harness.CaseId,
                    0,
                    actor,
                    "renew-released-lease",
                    released.Token),
                default));

        var replacement = await harness.Store.ClaimAsync(
            request with { OperationKey = "claim-after-release" },
            default);
        Assert.NotEqual(released.Token, replacement.Token);
        harness.TimeProvider.Advance(TimeSpan.FromMinutes(5));
        await Assert.ThrowsAsync<CaseEditLeaseExpiredException>(() =>
            harness.Store.ClaimAsync(
                request with { OperationKey = "claim-after-release" },
                default));
        var afterExpiry = await harness.Store.ClaimAsync(
            request with { OperationKey = "claim-after-expiry" },
            default);

        Assert.NotEqual(replacement.Token, afterExpiry.Token);
        Assert.Equal(
            harness.TimeProvider.GetUtcNow().AddMinutes(5),
            afterExpiry.ExpiresAtUtc);
        await Assert.ThrowsAsync<CaseEditLeaseConflictException>(() =>
            harness.Store.RenewAsync(
                new(
                    harness.CaseId,
                    0,
                    actor,
                    "renew-replaced-lease",
                    replacement.Token),
                default));
        Assert.True(await harness.HasLeaseReplayMaterialAsync(harness.CaseId));
    }

    [Fact]
    public async Task EachClaimAdvancesLeaseGeneration()
    {
        await using var harness = await WorkflowHarness.CreateAsync(database);
        var holderId = Guid.NewGuid();
        var holder = ActionActor.Staff(holderId, [StaffRole.User]);
        var first = await harness.Store.ClaimAsync(
            new(harness.CaseId, 0, holder, "first-generation"),
            default);
        await harness.Store.ReleaseAsync(
            new(harness.CaseId, holder, "release-first-generation", first.Token),
            default);
        var replacement = await harness.Store.ClaimAsync(
            new(harness.CaseId, 0, holder, "replacement-generation"),
            default);

        Assert.Equal(first.Generation + 1, replacement.Generation);

        var stillHeld = await harness.Store.HeartbeatAsync(
            new(harness.CaseId, holder, replacement.Token),
            default);
        Assert.Equal(replacement.Generation, stillHeld.Generation);
    }

    [Fact]
    public async Task ALiveLeaseProjectsItsHolderAndExpiryAndAnExpiredOneProjectsNoActiveEditor()
    {
        await using var harness = await WorkflowHarness.CreateAsync(database);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var claimedAtUtc = harness.TimeProvider.GetUtcNow();
        var lease = await harness.Store.ClaimAsync(
            new(harness.CaseId, 0, actor, "claim-projection"),
            default);

        var whileHeld = await harness.QueryStore.GetHeaderAsync(new(harness.CaseId, actor), default);
        var activeLease = whileHeld?.ActiveEditLease;
        Assert.NotNull(activeLease);
        Assert.Equal(actor.SubjectId, activeLease.Holder);
        Assert.Equal(claimedAtUtc.AddMinutes(5), activeLease.ExpiresAtUtc);
        Assert.Equal(lease.ExpiresAtUtc, activeLease.ExpiresAtUtc);
        Assert.Equal("claim-projection", activeLease.OperationKey);

        // One second before expiry the case still reads as held; at expiry it reads as free,
        // with no sweeper having run and the retained columns untouched.
        harness.TimeProvider.Advance(TimeSpan.FromMinutes(5) - TimeSpan.FromSeconds(1));
        var justBeforeExpiry = await harness.QueryStore.GetHeaderAsync(new(harness.CaseId, actor), default);
        Assert.NotNull(justBeforeExpiry?.ActiveEditLease);

        harness.TimeProvider.Advance(TimeSpan.FromSeconds(1));
        var afterExpiry = await harness.QueryStore.GetHeaderAsync(new(harness.CaseId, actor), default);
        Assert.Null(afterExpiry?.ActiveEditLease);
    }

    /// <summary>
    /// A heartbeat extends the lease and records nothing. It is issued once a minute for
    /// as long as an editor is open, so a replay row per beat would grow a table nothing prunes,
    /// and rewriting the operation key would destroy the claim key the workspace reads back to
    /// recover edit mode.
    /// </summary>
    [Fact]
    public async Task HeartbeatExtendsTheLeaseWithoutWritingAReplayRowOrTouchingTheClaimKey()
    {
        await using var harness = await WorkflowHarness.CreateAsync(database);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var lease = await harness.Store.ClaimAsync(
            new(harness.CaseId, 0, actor, "claim-heartbeat"),
            default);

        harness.TimeProvider.Advance(TimeSpan.FromMinutes(4));
        var beaten = await harness.Store.HeartbeatAsync(
            new(harness.CaseId, actor, lease.Token),
            default);

        Assert.Equal(harness.TimeProvider.GetUtcNow().AddMinutes(5), beaten.ExpiresAtUtc);
        Assert.Equal(lease.Token, beaten.Token);
        Assert.Equal(lease.Holder, beaten.Holder);
        Assert.Equal(lease.Version, beaten.Version);
        // One row for the claim, and none for the beat.
        Assert.Equal(
            1,
            await harness.LeaseOperationCountAsync(harness.CaseId, "claim-heartbeat"));
        Assert.Equal(1, await harness.LeaseOperationCountAsync(harness.CaseId));
        Assert.Equal("claim-heartbeat", await harness.LeaseOperationKeyAsync(harness.CaseId));
    }

    /// <summary>
    /// A beat is refused for a non-holder and after another claim, but the staff holder's own beat
    /// with its own token picks up a lease that lapsed unbeaten (operator, 6 October 2026): nobody
    /// else could have taken it without rewriting the retained hash.
    /// </summary>
    [Fact]
    public async Task HeartbeatIsRefusedForANonHolderAfterExpiryAndAfterAnotherClaim()
    {
        await using var harness = await WorkflowHarness.CreateAsync(database);
        var holder = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var other = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var lease = await harness.Store.ClaimAsync(
            new(harness.CaseId, 0, holder, "claim-refusals"),
            default);

        await Assert.ThrowsAsync<CaseEditLeaseConflictException>(() =>
            harness.Store.HeartbeatAsync(new(harness.CaseId, other, lease.Token), default));

        harness.TimeProvider.Advance(TimeSpan.FromMinutes(5));
        var revived = await harness.Store.HeartbeatAsync(new(harness.CaseId, holder, lease.Token), default);
        Assert.Equal(lease.Token, revived.Token);
        Assert.Equal(harness.TimeProvider.GetUtcNow().AddMinutes(5), revived.ExpiresAtUtc);

        harness.TimeProvider.Advance(TimeSpan.FromMinutes(5));
        var taken = await harness.Store.ClaimAsync(
            new(harness.CaseId, 0, other, "claim-after-lapse"),
            default);
        Assert.Equal(other.SubjectId, taken.Holder);
        await Assert.ThrowsAsync<CaseEditLeaseConflictException>(() =>
            harness.Store.HeartbeatAsync(new(harness.CaseId, holder, lease.Token), default));
    }

    /// <summary>
    /// The first requirement, end to end: an editor that keeps beating is never timed out, however
    /// long the session runs; one that stops frees the case for someone else.
    /// </summary>
    [Fact]
    public async Task RepeatedHeartbeatsHoldTheLeaseIndefinitelyAndStoppingThemLetsItLapse()
    {
        await using var harness = await WorkflowHarness.CreateAsync(database);
        var editor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var waiting = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var lease = await harness.Store.ClaimAsync(
            new(harness.CaseId, 0, editor, "claim-long-session"),
            default);

        // Ten beats at the rendered interval - well past the point the lease would have lapsed
        // unattended, which is the whole complaint this fixes.
        for (var beat = 0; beat < 10; beat++)
        {
            harness.TimeProvider.Advance(CaseEditAuthority.HeartbeatInterval);
            await harness.Store.HeartbeatAsync(new(harness.CaseId, editor, lease.Token), default);
        }

        await Assert.ThrowsAsync<CaseEditLeaseConflictException>(() =>
            harness.Store.ClaimAsync(
                new(harness.CaseId, 0, waiting, "claim-while-beating"),
                default));
        Assert.Equal(1, await harness.LeaseOperationCountAsync(harness.CaseId));

        harness.TimeProvider.Advance(TimeSpan.FromMinutes(5));
        var reacquired = await harness.Store.ClaimAsync(
            new(harness.CaseId, 0, waiting, "claim-after-beats-stopped"),
            default);

        Assert.Equal(waiting.SubjectId, reacquired.Holder);
    }

    /// <summary>
    /// The second requirement: a save ends edit mode as it commits, and a beat still in flight
    /// cannot bring back the lease that save just cleared.
    /// </summary>
    [Fact]
    public async Task ASaveEndsEditModeImmediatelyAndALaterHeartbeatCannotResurrectIt()
    {
        await using var harness = await WorkflowHarness.CreateAsync(database);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var lease = await harness.Store.ClaimAsync(
            new(harness.CaseId, 0, actor, "claim-before-save"),
            default);
        await harness.Store.HeartbeatAsync(new(harness.CaseId, actor, lease.Token), default);

        await new PutCaseOnHold(harness.Store, harness.TimeProvider).ExecuteAsync(
            new(harness.CaseId, 0, actor, "hold-ends-edit-mode", "Waiting", lease.Token),
            default);

        var afterSave = await harness.QueryStore.GetHeaderAsync(new(harness.CaseId, actor), default);
        Assert.Null(afterSave?.ActiveEditLease);
        await Assert.ThrowsAsync<CaseEditLeaseExpiredException>(() =>
            harness.Store.HeartbeatAsync(new(harness.CaseId, actor, lease.Token), default));
    }

    /// <summary>
    /// Automation holds and staff competes: the staff claim, a write presenting the
    /// holder's own token, renew, heartbeat, and release are all refused; the retained lease is
    /// untouched by every refusal; and the holder then heartbeats, saves, keeps its lease
    /// through that save (operator, 8 October 2026) and ends it.
    /// </summary>
    [Fact]
    public async Task AnAutomationHeldLeaseRefusesEveryStaffOperationAndTheHolderStillSaves()
    {
        await using var harness = await WorkflowHarness.CreateAsync(database);
        var automation = ActionActor.Automation("pegasus-automation");
        var staff = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var lease = await harness.Store.ClaimAsync(
            new(harness.CaseId, 0, automation, "mcp:automation-holds"),
            default);
        var held = await harness.ReadLeaseRowAsync(harness.CaseId);
        Assert.Equal(nameof(ActorKind.Automation), held.HolderKind);
        Assert.Equal(automation.SubjectId, held.Holder);

        await AssertCompetitorIsRefusedAsync(harness, staff, lease.Token, "staff-competes");

        Assert.Equal(held, await harness.ReadLeaseRowAsync(harness.CaseId));
        var projected = await harness.QueryStore.GetHeaderAsync(new(harness.CaseId, staff), default);
        Assert.Equal(ActorKind.Automation, projected?.ActiveEditLease?.HolderKind);
        Assert.Equal(automation.SubjectId, projected?.ActiveEditLease?.Holder);

        harness.TimeProvider.Advance(TimeSpan.FromMinutes(1));
        var beaten = await harness.Store.HeartbeatAsync(
            new(harness.CaseId, automation, lease.Token),
            default);
        Assert.Equal(harness.TimeProvider.GetUtcNow().AddMinutes(5), beaten.ExpiresAtUtc);

        await new PutCaseOnHold(harness.Store, harness.TimeProvider).ExecuteAsync(
            new(harness.CaseId, 0, automation, "automation-saves", "Waiting", lease.Token),
            default);

        var afterSave = await harness.ReadLeaseRowAsync(harness.CaseId);
        Assert.Equal(1, afterSave.Version);
        Assert.Equal(automation.SubjectId, afterSave.Holder);
        Assert.Equal(nameof(ActorKind.Automation), afterSave.HolderKind);
        // The save kept the Automation lease, so the holder ends it with its own token.
        await harness.Store.ReleaseAsync(
            new(harness.CaseId, automation, "mcp:automation-ends-after-save", lease.Token),
            default);
        var released = await harness.ReadLeaseRowAsync(harness.CaseId);
        Assert.Null(released.Holder);
        Assert.Null(released.HolderKind);
    }

    /// <summary>
    /// Staff holds and Automation competes: the mirror of the case above, ending
    /// with the holder releasing without saving and the competitor then claiming the free lease.
    /// </summary>
    [Fact]
    public async Task AStaffHeldLeaseRefusesEveryAutomationOperationAndTheHolderStillReleases()
    {
        await using var harness = await WorkflowHarness.CreateAsync(database);
        var staff = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var automation = ActionActor.Automation("pegasus-automation");
        var lease = await harness.Store.ClaimAsync(
            new(harness.CaseId, 0, staff, "staff-holds"),
            default);
        var held = await harness.ReadLeaseRowAsync(harness.CaseId);
        Assert.Equal(nameof(ActorKind.Staff), held.HolderKind);
        Assert.Equal(staff.SubjectId, held.Holder);

        await AssertCompetitorIsRefusedAsync(harness, automation, lease.Token, "mcp:automation-competes");

        Assert.Equal(held, await harness.ReadLeaseRowAsync(harness.CaseId));
        var projected = await harness.QueryStore.GetHeaderAsync(new(harness.CaseId, automation), default);
        Assert.Equal(ActorKind.Staff, projected?.ActiveEditLease?.HolderKind);

        var renewed = await harness.Store.RenewAsync(
            new(harness.CaseId, 0, staff, "staff-renews", lease.Token),
            default);
        Assert.Equal(lease.Token, renewed.Token);
        await harness.Store.ReleaseAsync(
            new(harness.CaseId, staff, "staff-releases", lease.Token),
            default);

        var released = await harness.ReadLeaseRowAsync(harness.CaseId);
        Assert.Equal(0, released.Version);
        Assert.Null(released.Holder);
        Assert.Null(released.HolderKind);
        Assert.Null(released.Token);
        Assert.Null(released.ExpiresAtUtc);

        var reclaimed = await harness.Store.ClaimAsync(
            new(harness.CaseId, 0, automation, "mcp:automation-claims-free-lease"),
            default);
        Assert.Equal(automation.SubjectId, reclaimed.Holder);
        Assert.Equal(
            nameof(ActorKind.Automation),
            (await harness.ReadLeaseRowAsync(harness.CaseId)).HolderKind);
    }

    /// <summary>
    /// The identity gap this closes: a holder is kind and subject together, so an
    /// Automation Actor given the staff holder's own subject text, presenting the live token, is
    /// still a competitor — and its refused attempts leave the row byte-for-byte as it was.
    /// </summary>
    [Fact]
    public async Task TheSameSubjectTextUnderAnotherActorKindIsACompetitorEvenWithTheLiveToken()
    {
        await using var harness = await WorkflowHarness.CreateAsync(database);
        var staffId = Guid.NewGuid();
        var staff = ActionActor.Staff(staffId, [StaffRole.User]);
        var impostor = ActionActor.Automation(staffId.ToString("D"));
        Assert.Equal(staff.SubjectId, impostor.SubjectId);
        var lease = await harness.Store.ClaimAsync(
            new(harness.CaseId, 0, staff, "staff-holds-guid-subject"),
            default);
        var held = await harness.ReadLeaseRowAsync(harness.CaseId);

        await AssertCompetitorIsRefusedAsync(harness, impostor, lease.Token, "mcp:same-subject");
        // The claim replay path is also identity-checked: the holder's own claim key under
        // the other kind is a different request, not a replay of the staff claim.
        await Assert.ThrowsAsync<CaseOperationConflictException>(() =>
            harness.Store.ClaimAsync(
                new(harness.CaseId, 0, impostor, "staff-holds-guid-subject"),
                default));

        Assert.Equal(held, await harness.ReadLeaseRowAsync(harness.CaseId));
        var replay = await harness.Store.ClaimAsync(
            new(harness.CaseId, 0, staff, "staff-holds-guid-subject"),
            default);
        Assert.Equal(lease, replay);
    }

    private static async Task AssertCompetitorIsRefusedAsync(
        WorkflowHarness harness,
        ActionActor competitor,
        string holdersToken,
        string operationKeyPrefix)
    {
        await Assert.ThrowsAsync<CaseEditLeaseConflictException>(() =>
            harness.Store.ClaimAsync(
                new(harness.CaseId, 0, competitor, $"{operationKeyPrefix}-claim"),
                default));
        await Assert.ThrowsAsync<CaseEditLeaseConflictException>(() =>
            new PutCaseOnHold(harness.Store, harness.TimeProvider).ExecuteAsync(
                new(harness.CaseId, 0, competitor, $"{operationKeyPrefix}-write", "Waiting", holdersToken),
                default));
        await Assert.ThrowsAsync<CaseEditLeaseConflictException>(() =>
            harness.Store.RenewAsync(
                new(harness.CaseId, 0, competitor, $"{operationKeyPrefix}-renew", holdersToken),
                default));
        await Assert.ThrowsAsync<CaseEditLeaseConflictException>(() =>
            harness.Store.HeartbeatAsync(
                new(harness.CaseId, competitor, holdersToken),
                default));
        await Assert.ThrowsAsync<CaseEditLeaseConflictException>(() =>
            harness.Store.ReleaseAsync(
                new(harness.CaseId, competitor, $"{operationKeyPrefix}-release", holdersToken),
                default));
    }

    [Fact]
    public async Task AnAbandonedLeaseExpiresAndIsReacquiredByADifferentHolder()
    {
        await using var harness = await WorkflowHarness.CreateAsync(database);
        var firstActor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var secondActor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var abandoned = await harness.Store.ClaimAsync(
            new(harness.CaseId, 0, firstActor, "claim-abandoned"),
            default);

        await Assert.ThrowsAsync<CaseEditLeaseConflictException>(() =>
            harness.Store.ClaimAsync(
                new(harness.CaseId, 0, secondActor, "claim-competing-live"),
                default));

        harness.TimeProvider.Advance(TimeSpan.FromMinutes(5));
        var reacquired = await harness.Store.ClaimAsync(
            new(harness.CaseId, 0, secondActor, "claim-after-abandonment"),
            default);

        Assert.Equal(secondActor.SubjectId, reacquired.Holder);
        Assert.NotEqual(abandoned.Token, reacquired.Token);
        Assert.Equal(
            harness.TimeProvider.GetUtcNow().AddMinutes(5),
            reacquired.ExpiresAtUtc);

        var abandonedHold = await Assert.ThrowsAsync<CaseEditLeaseConflictException>(() =>
            new PutCaseOnHold(harness.Store, harness.TimeProvider).ExecuteAsync(
                new(
                    harness.CaseId,
                    0,
                    firstActor,
                    "hold-after-abandonment",
                    "Waiting",
                    abandoned.Token),
                default));
        Assert.Equal(harness.CaseId, abandonedHold.CaseId);
        Assert.Equal(0, abandonedHold.CaseVersion);

        var held = await new PutCaseOnHold(harness.Store, harness.TimeProvider).ExecuteAsync(
            new(
                harness.CaseId,
                0,
                secondActor,
                "hold-by-reacquiring-holder",
                "Waiting",
                reacquired.Token),
            default);
        Assert.Equal(1, held.Version);
    }

    /// <summary>
    /// A competing live lease refuses a claim. A page behind the Case does not: a claim
    /// from a page rendered before the Case moved on (a.QDOS26070, 6 October 2026: the
    /// Worker's vehicle lookup and custody confirmation moved a new Case twice while its
    /// page was open) is issued at the Case's current version, and only a version from
    /// the future is refused, as every write under the lease refuses it.
    /// </summary>
    [Fact]
    public async Task CompetingLeaseAndFutureVersionAreRejectedButAPageBehindTheCaseClaims()
    {
        await using var harness = await WorkflowHarness.CreateAsync(database);
        var firstActor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var secondActor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        _ = await harness.Store.ClaimAsync(new(harness.CaseId, 0, firstActor, "claim-1"), default);

        await Assert.ThrowsAsync<CaseEditLeaseConflictException>(() =>
            harness.Store.ClaimAsync(new(harness.CaseId, 0, secondActor, "claim-2"), default));

        var lease = await harness.Store.ClaimAsync(new(harness.SecondCaseId, 0, secondActor, "claim-3"), default);
        var hold = new PutCaseOnHold(harness.Store, harness.TimeProvider);
        _ = await hold.ExecuteAsync(
            new(
                harness.SecondCaseId,
                0,
                secondActor,
                "hold-1",
                "Waiting",
                lease.Token),
            default);
        await Assert.ThrowsAsync<CaseVersionConflictException>(() =>
            harness.Store.ClaimAsync(new(harness.SecondCaseId, 2, secondActor, "claim-future"), default));

        var behind = await harness.Store.ClaimAsync(
            new(harness.SecondCaseId, 0, secondActor, "claim-behind"), default);

        Assert.Equal(1, behind.Version);
        Assert.Equal(secondActor.SubjectId, behind.Holder);
        var row = await harness.ReadLeaseRowAsync(harness.SecondCaseId);
        Assert.Equal(secondActor.SubjectId, row.Holder);
    }

    [Fact]
    public async Task CreatedInErrorCannotUseTheGenericCloseCommand()
    {
        await using var harness = await WorkflowHarness.CreateAsync(database);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        var lease = await harness.Store.ClaimAsync(
            new(harness.CaseId, 0, actor, "claim-close"),
            default);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new CloseCase(harness.Store).ExecuteAsync(
                new(
                    harness.CaseId,
                    0,
                    actor,
                    "close-invalid",
                    "Wrong principal",
                    lease.Token,
                    CaseClosureOutcome.CreatedInError),
                default));
        var directError = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Store.CloseAsync(
                new(
                    harness.CaseId,
                    0,
                    actor,
                    "close-invalid-direct",
                    "Wrong principal",
                    lease.Token,
                    CaseClosureOutcome.CreatedInError),
                default));
        Assert.Contains("atomic corrected-principal replacement", directError.Message);

        var persisted = await harness.Store.GetAsync(harness.CaseId, default);
        Assert.Equal(CaseLifecycleState.Review, persisted?.State);
        Assert.Equal(0L, persisted?.Version);
    }

    [Fact]
    public async Task AuditCaseLinkedCaseReplacementIsAllocatedLinkedAndReplayedAtomically()
    {
        await using var harness = await WorkflowHarness.CreateIsolatedAsync();
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            var principal = await context.Principals.Include(item => item.Organization)
                .SingleAsync(item => item.Code == "QDOS");
            principal.DefaultFee = 195.00m;
            context.CaseSequences.Add(new CaseSequenceEntity
            {
                SequenceLineageId = principal.SequenceLineageId,
                Year = 2026,
                LastAllocatedSequence = 9999
            });
            await context.SaveChangesAsync();
        }
        var standaloneAuditEvidenceId =
            await harness.SeedStandaloneAuditEvidenceAsync(harness.CaseId);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        var missingPrincipalLease = await harness.Store.ClaimAsync(
            new(harness.SecondCaseId, 0, actor, "claim-missing-principal"),
            default);
        var create = new CreateLinkedReplacement(harness.ReplacementStore, new DiscardingCommittedWorkPublisher());

        await Assert.ThrowsAsync<InvalidOperationException>(() => create.ExecuteAsync(
            new(
                harness.SecondCaseId,
                0,
                actor,
                "replace-missing-principal",
                "Corrected principal is outside the activated QDOS boundary",
                missingPrincipalLease.Token,
                "NOPE"),
            default));
        var unchanged = await harness.Store.GetAsync(harness.SecondCaseId, default);
        Assert.Equal(CaseLifecycleState.Review, unchanged?.State);
        Assert.Null(unchanged?.ReplacementCaseId);

        var lease = await harness.Store.ClaimAsync(
            new(harness.CaseId, 0, actor, "claim-corrected-replacement"),
            default);
        var request = new CreateLinkedReplacementRequest(
            harness.CaseId,
            0,
            actor,
            "replace-corrected-principal",
            "Original case was allocated to the wrong principal",
            lease.Token,
            "QDOS");
        var originalDataBefore = await harness.DataStore.GetAsync(
            harness.CaseId,
            CaseWorkSelector.Current,
            CancellationToken.None);
        Assert.NotNull(originalDataBefore);
        Assert.Equal("Jane Workflow", originalDataBefore.Claimant.Name.Confirmed?.Value);
        var originalIndexRowBefore = await harness.FindMatchIndexRowAsync(harness.CaseId);
        var allocated = await create.ExecuteAsync(request, default);
        var replay = await create.ExecuteAsync(request, default);

        Assert.False(allocated.IsDuplicate);
        Assert.True(replay.IsDuplicate);
        Assert.Equal(allocated.Identity, replay.Identity);
        Assert.Equal("QDOS", allocated.Identity.PrincipalCode);
        Assert.Equal(10000, allocated.Identity.Sequence);
        Assert.Equal("a.QDOS2610000", allocated.Identity.Reference);
        Assert.Equal(
            allocated.Identity.Reference,
            await harness.ReadCaseReferenceAsync(allocated.Identity.CaseId));
        // The replacement starts with its corrected Principal's default fee.
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            var fee = await context.CaseAssessmentFields.AsNoTracking().SingleAsync(item =>
                item.WorkId == allocated.Identity.CaseId && item.FieldPath == AssessmentVocabulary.AgreedFee);
            Assert.Equal("195.00", fee.Value);
            Assert.Equal(PrincipalDefaultFeePolicy.RecorderId, fee.RecordedBy);
        }
        // A standalone Audit's a. prefix is on its own Case/PO; it never
        // carries an Audit report reference.
        Assert.Null(await harness.ReadAuditReferenceAsync(allocated.Identity.CaseId));
        Assert.NotEqual(
            await harness.ReadCaseReferenceAsync(harness.CaseId),
            allocated.Identity.Reference);
        Assert.Equal(
            standaloneAuditEvidenceId,
            await harness.ReadStandaloneAuditEvidenceIdAsync(harness.CaseId));
        Assert.Equal(
            standaloneAuditEvidenceId,
            await harness.ReadStandaloneAuditEvidenceIdAsync(allocated.Identity.CaseId));
        Assert.Equal(
            2L,
            await harness.CountCasesWithStandaloneAuditEvidenceAsync(
                standaloneAuditEvidenceId));
        Assert.Equal(
            1L,
            await harness.CountStandaloneAuditEvidenceAsync(
                standaloneAuditEvidenceId));

        var original = await harness.Store.GetAsync(harness.CaseId, default);
        var replacement = await harness.Store.GetAsync(allocated.Identity.CaseId, default);
        var unrelated = await harness.Store.GetAsync(harness.NotReadyCaseId, default);
        Assert.Equal(CaseLifecycleState.CreatedInError, original?.State);
        Assert.Equal(CaseClosureOutcome.CreatedInError, original?.ClosureOutcome);
        Assert.Equal(allocated.Identity.CaseId, original?.ReplacementCaseId);
        Assert.Equal(harness.CaseId, replacement?.OriginalCaseId);
        Assert.Null(replacement?.ReplacementCaseId);
        Assert.Null(unrelated?.OriginalCaseId);
        Assert.Null(unrelated?.ReplacementCaseId);
        var originalDataAfter = await harness.DataStore.GetAsync(
            harness.CaseId,
            CaseWorkSelector.Current,
            CancellationToken.None);
        var replacementData = await harness.DataStore.GetAsync(
            allocated.Identity.CaseId,
            CaseWorkSelector.Current,
            CancellationToken.None);
        Assert.NotNull(replacementData);
        Assert.Equal(originalDataBefore.Origin, replacementData.Origin);
        Assert.Equal(originalDataBefore.Claimant.Name, replacementData.Claimant.Name);
        Assert.Equal(
            originalDataBefore.Claimant.Name.Confirmed?.Source,
            replacementData.Claimant.Name.Confirmed?.Source);
        Assert.Equal(originalDataBefore.Identity, originalDataAfter?.Identity);
        Assert.Equal(originalDataBefore.Origin, originalDataAfter?.Origin);
        Assert.Equal(originalDataBefore.Claimant.Name, originalDataAfter?.Claimant.Name);

        // Wrong-Principal correction: the replacement's reference and sequence
        // are allocated under QDOS, so its principal_code must name QDOS
        // too, as a staff-confirmed value, even though every other cloned
        // field carried the original's provenance verbatim. The projected
        // CaseMatchIndex row must follow suit, while the original — still
        // CreatedInError, keeping its own history — is unaffected.
        var replacementProvider = replacementData!.Principal.PrincipalCode.Current;
        Assert.Equal(CaseDataValueKind.Confirmed, replacementProvider?.Kind);
        Assert.Equal("QDOS", replacementProvider?.Value);
        var originalIndexRowAfter = await harness.FindMatchIndexRowAsync(harness.CaseId);
        Assert.Equal(originalIndexRowBefore?.PrincipalCode, originalIndexRowAfter?.PrincipalCode);
        var replacementIndexRow = await harness.FindMatchIndexRowAsync(allocated.Identity.CaseId);
        Assert.Equal("QDOS", replacementIndexRow?.PrincipalCode);

    }

    [Fact]
    public async Task WrongPrincipalReplacementRetainsClaimSourceOverridesAndStaffDueBy()
    {
        await using var harness = await WorkflowHarness.CreateIsolatedAsync();
        var manualDueBy = new DateOnly(2031, 5, 18);
        var replacementDeadline = new DateOnly(2031, 6, 2);
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            var originalCase = await context.Cases.SingleAsync(item => item.Id == harness.CaseId);
            originalCase.InitialState = "not_ready";
            originalCase.AcceptedInspectionDeadline = new DateOnly(2031, 5, 20);
            var originalWorkflow = await context.CaseWorkflows.SingleAsync(
                item => item.CaseId == harness.CaseId);
            originalWorkflow.State = nameof(CaseLifecycleState.NotReady);
            var snapshot = await context.CaseDataSnapshots.SingleAsync(
                item => item.WorkId == harness.CaseId);
            snapshot.ClaimSourceOverrideContactName = "Replacement contact";
            snapshot.ClaimSourceOverrideContactTelephone = "0113 999 0028";
            snapshot.ClaimSourceOverrideContactEmailAddress = "replacement-contact@example.test";
            context.CaseDueWork.Add(new CaseDueWorkEntity
            {
                CaseId = harness.CaseId,
                Workflow = originalWorkflow,
                MissingMaterialReason = "Waiting for images",
                DueBy = manualDueBy,
                DueBySetByStaff = true,
                State = nameof(CaseDueWorkState.Scheduled),
                NextChaseAtUtc = harness.TimeProvider.GetUtcNow().AddDays(3),
                Version = 0
            });
            await context.SaveChangesAsync();
        }

        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        var lease = await harness.Store.ClaimAsync(
            new(harness.CaseId, 0, actor, "replacement-local-state-lease"),
            default);
        var replacement = await new CreateLinkedReplacement(
            harness.ReplacementStore,
            new DiscardingCommittedWorkPublisher()).ExecuteAsync(
            new(
                harness.CaseId,
                0,
                actor,
                "replacement-local-state-save",
                "Original case was allocated to the wrong principal",
                lease.Token,
                "QDOS"),
            default);

        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            var snapshot = await context.CaseDataSnapshots.SingleAsync(
                item => item.WorkId == replacement.Identity.CaseId);
            var dueWork = await context.CaseDueWork.SingleAsync(
                item => item.CaseId == replacement.Identity.CaseId);
            Assert.Equal("Replacement contact", snapshot.ClaimSourceOverrideContactName);
            Assert.Equal("0113 999 0028", snapshot.ClaimSourceOverrideContactTelephone);
            Assert.Equal("replacement-contact@example.test", snapshot.ClaimSourceOverrideContactEmailAddress);
            Assert.Equal(manualDueBy, dueWork.DueBy);
            Assert.True(dueWork.DueBySetByStaff);
        }

        var replacementLease = await harness.Store.ClaimAsync(
            new(replacement.Identity.CaseId, 0, actor, "replacement-deadline-lease"),
            default);
        await new EfCaseWorkspaceStore(harness.Factory, harness.TimeProvider).SaveAsync(
            new(
                replacement.Identity.CaseId,
                0,
                actor,
                "replacement-deadline-save",
                "Recorded the replacement inspection deadline",
                replacementLease.Token)
            {
                Inspection = new(
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    replacementDeadline,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null)
            },
            default);

        await using var verification = await harness.Factory.CreateDbContextAsync();
        var replacementCase = await verification.Cases.SingleAsync(
            item => item.Id == replacement.Identity.CaseId);
        var reprojected = await verification.CaseDueWork.SingleAsync(
            item => item.CaseId == replacement.Identity.CaseId);
        Assert.Equal(replacementDeadline, replacementCase.AcceptedInspectionDeadline);
        Assert.Equal(manualDueBy, reprojected.DueBy);
        Assert.True(reprojected.DueBySetByStaff);
    }

    [Fact]
    public async Task AuditCaseReferenceFilterMatchesPrimaryAndSecondaryReferences()
    {
        await using var harness = await WorkflowHarness.CreateAsync(database);
        string secondaryReference;
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            // Only an Inspection + Audit Case carries an Audit report
            // reference, and it is always a. + its Case/PO.
            var auditCase = await context.Cases.SingleAsync(item => item.Id == harness.CaseId);
            auditCase.Type = "inspection_and_audit";
            auditCase.AuditReference = "a." + auditCase.Reference;
            secondaryReference = auditCase.AuditReference;
            await context.SaveChangesAsync();
        }
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var primaryReference = await harness.ReadCaseReferenceAsync(harness.CaseId);

        var primary = await harness.QueryStore.SearchAsync(
            new(actor, new CaseSearchFilters(CaseReference: primaryReference)),
            default);
        var secondary = await harness.QueryStore.SearchAsync(
            new(actor, new CaseSearchFilters(CaseReference: secondaryReference)),
            default);

        Assert.Contains(primary.Items, item => item.CaseId == harness.CaseId);
        Assert.Contains(secondary.Items, item => item.CaseId == harness.CaseId);
    }

    /// <summary>
    /// The Work Centre reads With Engineer's Unassigned Cases, and on Mine the
    /// viewer's with them in one read, without reading every With Engineer
    /// Case (#1213): the Engineer filters select over real SQL.
    /// </summary>
    [Fact]
    public async Task TheEngineerFiltersSelectUnassignedAndAssignedWithEngineerCases()
    {
        await using var harness = await WorkflowHarness.CreateIsolatedAsync();
        Guid? engineerId;
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            var assigned = await context.CaseWorkflows.SingleAsync(item => item.CaseId == harness.CaseId);
            assigned.State = nameof(CaseLifecycleState.ReportPreparation);
            engineerId = assigned.AssignedEngineerId;
            var unassigned = await context.CaseWorkflows.SingleAsync(item => item.CaseId == harness.SecondCaseId);
            unassigned.State = nameof(CaseLifecycleState.ReportPreparation);
            unassigned.AssignedEngineerId = null;
            await context.SaveChangesAsync();
        }
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        async Task<Guid[]> SearchAsync(CaseSearchFilters filters) =>
            [.. (await harness.QueryStore.SearchAsync(new(actor, filters), default)).Items.Select(item => item.CaseId)];

        var withEngineer = new CaseSearchFilters(States: [CaseLifecycleState.ReportPreparation]);
        Assert.Equal([harness.SecondCaseId], await SearchAsync(withEngineer with { Unassigned = true }));
        Assert.Equal([harness.CaseId], await SearchAsync(withEngineer with { EngineerId = engineerId }));
        Assert.Equal(
            new[] { harness.CaseId, harness.SecondCaseId }.Order().ToArray(),
            (await SearchAsync(withEngineer with { EngineerId = engineerId, Unassigned = true })).Order().ToArray());
    }

    [Fact]
    public async Task AccidentCircumstancesAreReadForTheOneCaseFromItsConfirmedField()
    {
        await using var harness = await WorkflowHarness.CreateAsync(database);
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            await InsertConfirmedCircumstancesAsync(context, harness.CaseId, "Rear-ended at lights");
        }
        ICaseCircumstancesQueries circumstances = harness.QueryStore;

        Assert.Equal("Rear-ended at lights", await circumstances.GetAsync(harness.CaseId, default));
        Assert.Null(await circumstances.GetAsync(harness.SecondCaseId, default));
        Assert.Null(await circumstances.GetAsync(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task AnInstructionDraftsAccidentCircumstancesWinOverTheConfirmedField()
    {
        await using var harness = await WorkflowHarness.CreateIsolatedAsync();
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            var receiptId = await context.Cases
                .Where(item => item.Id == harness.CaseId)
                .Select(item => item.OriginIntakeReceiptId)
                .SingleAsync();
            await InsertConfirmedCircumstancesAsync(context, harness.CaseId, "Rear-ended at lights");
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO InstructionDrafts (IntakeReceiptId, AccidentCircumstances) VALUES ({receiptId}, {"Struck on the roundabout"})");
        }
        ICaseCircumstancesQueries circumstances = harness.QueryStore;

        Assert.Equal("Struck on the roundabout", await circumstances.GetAsync(harness.CaseId, default));
    }

    private static Task<int> InsertConfirmedCircumstancesAsync(PegasusDbContext context, Guid caseId, string text) =>
        context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CaseDataFields (WorkId, FieldName, ValueKind, ValueType, Value, SourceKind, SourceIdentity, SourceLabel, PolicyKey, PolicyVersion, ConfirmedByActor, ConfirmedAtUtc) VALUES ({caseId}, {"accident_circumstances"}, {"confirmed"}, {"text"}, {text}, {"intake_evidence"}, {"circumstances"}, {"workflow fixture evidence"}, {"workflow-fixture"}, {1}, {"workflow-staff"}, {DateTimeOffset.UtcNow})");

    [Fact]
    public async Task WrongPrincipalReplacementCancelsTheOriginalsOpenTasksAndReplays()
    {
        await using var harness = await WorkflowHarness.CreateIsolatedAsync();
        var openTaskId = Guid.NewGuid();
        var completedTaskId = Guid.NewGuid();
        await harness.SeedCaseTaskAsync(harness.CaseId, openTaskId, CaseTaskState.Open);
        await harness.SeedCaseTaskAsync(harness.CaseId, completedTaskId, CaseTaskState.Completed);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        var lease = await harness.Store.ClaimAsync(
            new(harness.CaseId, 0, actor, "claim-task-cancelling-replacement"),
            default);
        var request = new CreateLinkedReplacementRequest(
            harness.CaseId,
            0,
            actor,
            "task-cancelling-replacement",
            "Correct the immutable principal with open work",
            lease.Token,
            "QDOS");
        var create = new CreateLinkedReplacement(harness.ReplacementStore, new DiscardingCommittedWorkPublisher());
        var initialCaseCount = await harness.CountCasesAsync();
        var initialQdosReferenceCount = await harness.CountQdosReferencesAsync();

        var allocated = await create.ExecuteAsync(request, default);

        var tasks = await harness.TaskStore.ListAsync(harness.CaseId, default);
        var cancelled = Assert.Single(tasks, item => item.Id == openTaskId);
        Assert.Equal(CaseTaskState.Cancelled, cancelled.State);
        Assert.Equal(1, cancelled.Version);
        var completed = Assert.Single(tasks, item => item.Id == completedTaskId);
        Assert.Equal(CaseTaskState.Completed, completed.State);
        Assert.Equal(0, completed.Version);
        var original = await harness.Store.GetAsync(harness.CaseId, default);
        Assert.Equal(CaseLifecycleState.CreatedInError, original?.State);
        Assert.Equal(1, original?.Version);
        var history = await ReadTaskCancellationsAsync(harness);
        var cancellation = Assert.Single(history);
        Assert.Equal(openTaskId.ToString("D"), cancellation.AggregateId);
        Assert.Equal(request.Reason, cancellation.Reason);
        Assert.Equal(nameof(ActorKind.Staff), cancellation.ActorKind);
        Assert.Equal(actor.SubjectId, cancellation.ActorSubjectId);
        Assert.Equal(request.OperationKey, cancellation.CorrelationId);

        var replay = await create.ExecuteAsync(request, default);

        Assert.False(allocated.IsDuplicate);
        Assert.True(replay.IsDuplicate);
        Assert.Equal(allocated.Identity, replay.Identity);
        Assert.Equal(initialCaseCount + 1, await harness.CountCasesAsync());
        Assert.Equal(initialQdosReferenceCount + 1, await harness.CountQdosReferencesAsync());
        Assert.Single(await ReadTaskCancellationsAsync(harness));
    }

    private static async Task<IReadOnlyList<ActionHistoryEntity>> ReadTaskCancellationsAsync(
        WorkflowHarness harness)
    {
        await using var context = await harness.Factory.CreateDbContextAsync();
        return await context.ActionHistory.AsNoTracking()
            .Where(item => item.AggregateType == "case_task"
                && item.EventKind == "case_task_cancelled")
            .ToListAsync();
    }

    [Fact]
    public async Task ConcurrentTaskCreationAndWrongPrincipalReplacementUnderOneLeaseCommitExactlyOne()
    {
        await using var harness = await WorkflowHarness.CreateIsolatedAsync();
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var lease = await harness.Store.ClaimAsync(
            new(harness.CaseId, 0, actor, "claim-concurrent-task-replacement"),
            default);
        var replacement = new CreateLinkedReplacement(harness.ReplacementStore, new DiscardingCommittedWorkPublisher()).ExecuteAsync(
            new(
                harness.CaseId,
                0,
                actor,
                "concurrent-principal-replacement",
                "Race corrected-principal terminalization with task creation",
                lease.Token,
                "QDOS"),
            default);
        var taskCreation = harness.TaskStore.CreateAsync(
            new(
                harness.CaseId,
                Guid.NewGuid(),
                0,
                actor,
                "concurrent-replacement-task",
                "Exercise terminal task serialization",
                lease.Token,
                "Task created under the lease the replacement also presents",
                null),
            default);

        await Assert.ThrowsAnyAsync<Exception>(() => Task.WhenAll(replacement, taskCreation));
        Assert.NotEqual(
            replacement.IsCompletedSuccessfully,
            taskCreation.IsCompletedSuccessfully);

        var current = Assert.IsType<CaseWorkflowRecord>(
            await harness.Store.GetAsync(harness.CaseId, default));
        var tasks = await harness.TaskStore.ListAsync(harness.CaseId, default);
        if (CaseLifecycleRules.IsTerminal(current.State))
        {
            Assert.DoesNotContain(tasks, item => item.State == CaseTaskState.Open);
            Assert.NotNull(current.ReplacementCaseId);
            Assert.Equal(4L, await harness.CountCasesAsync());
        }
        else
        {
            Assert.Equal(CaseLifecycleState.Review, current.State);
            Assert.Single(tasks, item => item.State == CaseTaskState.Open);
            Assert.Null(current.ReplacementCaseId);
            Assert.Equal(3L, await harness.CountCasesAsync());
        }
    }


    private static readonly string[] AfterSendTasks =
        ["Send the figures to the garage", "Authorise the garage"];

    /// <summary>
    /// Report sent creates the tasks the send recorded (FRD-13, CASE-20) in the transaction
    /// that records the Worker's automatic link of that send: one open, unassigned task per
    /// description, with a case_task_created event each, in the name of the staff member who
    /// sent the report. A replay of the link creates none more.
    /// </summary>
    [Fact]
    public async Task AutoLinkCreatesTheSendsAfterSendTasksOnceInTheSendersNameAndAReplayCreatesNone()
    {
        await using var harness = await WorkflowHarness.CreateIsolatedAsync();
        var staff = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var sender = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var started = await new StartCaseWork(harness.Store).ExecuteAsync(
            new ChangeCaseStateRequest(
                harness.CaseId,
                0,
                staff,
                "start-after-send-auto",
                "Inspection work started",
                (await harness.Store.ClaimAsync(
                    new(harness.CaseId, 0, staff, "claim-start-after-send-auto"),
                    default)).Token),
            default);
        harness.TimeProvider.Advance(TimeSpan.FromMinutes(3));
        var retained = await RetainReportEvidenceAsync(
            harness,
            "after-send-auto",
            harness.TimeProvider.GetUtcNow().AddMinutes(-2),
            harness.TimeProvider.GetUtcNow().AddMinutes(-1));
        var send = await SeedReportSendAsync(
            harness, sender, "immutable-item-after-send-auto", AfterSendTasks);
        var request = new AutoLinkReportEvidenceRequest(
            harness.CaseId,
            retained.EvidenceId,
            ActionActor.SystemWorker("approved-mailbox-sent-poll"),
            "auto-link-after-send-tasks",
            "Exact approved-mailbox Sent evidence and one authoritative Case identity",
            send.GenerationId,
            SendOperationId: send.OperationId);
        var sut = new AutoLinkReportEvidence(harness.Store);

        var first = await sut.ExecuteAsync(request, default);
        harness.TimeProvider.Advance(TimeSpan.FromHours(1));
        var replay = await sut.ExecuteAsync(request, default);

        Assert.Equal(AutoLinkReportEvidenceDisposition.Linked, first.Disposition);
        Assert.Equal(
            AfterSendTasks,
            first.TasksCreated.Select(task => task.Description).ToArray());
        Assert.All(first.TasksCreated, task =>
        {
            Assert.Equal(harness.CaseId, task.CaseId);
            Assert.Equal(CaseTaskState.Open, task.State);
            Assert.Null(task.AssigneeId);
        });
        // The link moves the Case's version on once and each task once more.
        Assert.Equal(started.Version + 3, first.Link?.Version);
        Assert.Equal(AutoLinkReportEvidenceDisposition.Linked, replay.Disposition);
        Assert.Empty(replay.TasksCreated);

        await using var context = await harness.Factory.CreateDbContextAsync();
        var stored = await context.CaseTasks.AsNoTracking()
            .Where(task => task.CaseId == harness.CaseId)
            .ToListAsync();
        Assert.Equal(2, stored.Count);
        Assert.Equal(
            first.TasksCreated.Select(task => task.Id).Order().ToArray(),
            stored.Select(task => task.Id).Order().ToArray());
        var events = await context.CaseWorkflowEvents.AsNoTracking()
            .Where(item => item.CaseId == harness.CaseId && item.EventType == "case_task_created")
            .ToListAsync();
        Assert.Equal(
            [$"{request.OperationKey}:task:0", $"{request.OperationKey}:task:1"],
            events.Select(item => item.OperationKey).Order(StringComparer.Ordinal).ToArray());
        await AssertCreatedInTheSendersNameAsync(harness, events, first.TasksCreated, sender);
    }

    /// <summary>
    /// The Worker's link is system work: it moves the Case's version on, with each task the
    /// send recorded, and the member of staff editing the Case keeps their edit session.
    /// </summary>
    [Fact]
    public async Task AutoLinkWithAfterSendTasksKeepsTheEditorsLease()
    {
        await using var harness = await WorkflowHarness.CreateIsolatedAsync();
        var staff = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var started = await new StartCaseWork(harness.Store).ExecuteAsync(
            new ChangeCaseStateRequest(
                harness.CaseId,
                0,
                staff,
                "start-after-send-lease",
                "Inspection work started",
                (await harness.Store.ClaimAsync(
                    new(harness.CaseId, 0, staff, "claim-start-after-send-lease"),
                    default)).Token),
            default);
        harness.TimeProvider.Advance(TimeSpan.FromMinutes(3));
        var retained = await RetainReportEvidenceAsync(
            harness,
            "after-send-lease",
            harness.TimeProvider.GetUtcNow().AddMinutes(-2),
            harness.TimeProvider.GetUtcNow().AddMinutes(-1));
        var send = await SeedReportSendAsync(
            harness, staff, "immutable-item-after-send-lease", AfterSendTasks);
        var editing = await harness.Store.ClaimAsync(
            new(harness.CaseId, started.Version, staff, "claim-edit-during-auto-link"),
            default);

        var linked = await new AutoLinkReportEvidence(harness.Store).ExecuteAsync(
            new(
                harness.CaseId,
                retained.EvidenceId,
                ActionActor.SystemWorker("approved-mailbox-sent-poll"),
                "auto-link-during-edit",
                "Exact approved-mailbox Sent evidence and one authoritative Case identity",
                send.GenerationId,
                SendOperationId: send.OperationId),
            default);

        Assert.Equal(AutoLinkReportEvidenceDisposition.Linked, linked.Disposition);
        Assert.Equal(2, linked.TasksCreated.Count);
        Assert.Equal(started.Version + 3, linked.Link?.Version);
        var header = await harness.QueryStore.GetHeaderAsync(new(harness.CaseId, staff), default);
        Assert.Equal(staff.SubjectId, header?.ActiveEditLease?.Holder);
        var stillHeld = await harness.Store.HeartbeatAsync(
            new(harness.CaseId, staff, editing.Token),
            default);
        Assert.Equal(staff.SubjectId, stillHeld.Holder);
    }

    /// <summary>
    /// The Worker's link of a Sent item it did not match to a Pegasus send creates no task,
    /// even when a report send of the Case recorded a list.
    /// </summary>
    [Fact]
    public async Task AutoLinkWithoutASendOperationCreatesNoTasks()
    {
        await using var harness = await WorkflowHarness.CreateIsolatedAsync();
        var staff = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var started = await new StartCaseWork(harness.Store).ExecuteAsync(
            new ChangeCaseStateRequest(
                harness.CaseId,
                0,
                staff,
                "start-after-send-none",
                "Inspection work started",
                (await harness.Store.ClaimAsync(
                    new(harness.CaseId, 0, staff, "claim-start-after-send-none"),
                    default)).Token),
            default);
        harness.TimeProvider.Advance(TimeSpan.FromMinutes(3));
        var retained = await RetainReportEvidenceAsync(
            harness,
            "after-send-none",
            harness.TimeProvider.GetUtcNow().AddMinutes(-2),
            harness.TimeProvider.GetUtcNow().AddMinutes(-1));
        await SeedReportSendAsync(harness, staff, "immutable-item-after-send-none", AfterSendTasks);

        var linked = await new AutoLinkReportEvidence(harness.Store).ExecuteAsync(
            new(
                harness.CaseId,
                retained.EvidenceId,
                ActionActor.SystemWorker("approved-mailbox-sent-poll"),
                "auto-link-without-send",
                "Exact approved-mailbox Sent evidence and one authoritative Case identity"),
            default);

        Assert.Equal(AutoLinkReportEvidenceDisposition.Linked, linked.Disposition);
        Assert.Empty(linked.TasksCreated);
        Assert.Equal(started.Version + 1, linked.Link?.Version);
        await using var context = await harness.Factory.CreateDbContextAsync();
        Assert.False(await context.CaseTasks.AnyAsync(task => task.CaseId == harness.CaseId));
    }

    /// <summary>
    /// Each task's creation event, in the Case's workflow and in the action history, names
    /// the staff member who sent the report, not whoever recorded the link; the task row
    /// stores the same sender and the history's instant.
    /// </summary>
    private static async Task AssertCreatedInTheSendersNameAsync(
        WorkflowHarness harness,
        IReadOnlyList<CaseWorkflowEventEntity> events,
        IReadOnlyList<CaseTaskRecord> tasks,
        ActionActor sender)
    {
        Assert.All(events, item =>
        {
            Assert.Equal(nameof(ActorKind.Staff), item.ActorKind);
            Assert.Equal(sender.SubjectId, item.ActorSubjectId);
            Assert.Equal(CaseTaskReasons.ReportSent, item.Reason);
        });
        var taskIds = tasks.Select(task => task.Id.ToString("D")).ToArray();
        await using var context = await harness.Factory.CreateDbContextAsync();
        var history = await context.ActionHistory.AsNoTracking()
            .Where(item => item.AggregateType == "case_task"
                && item.EventKind == "case_task_created"
                && taskIds.Contains(item.AggregateId))
            .ToListAsync();
        Assert.Equal(tasks.Count, history.Count);
        Assert.All(history, item =>
        {
            Assert.Equal(nameof(ActorKind.Staff), item.ActorKind);
            Assert.Equal(sender.SubjectId, item.ActorSubjectId);
        });
        var taskGuids = tasks.Select(task => task.Id).ToArray();
        var stored = await context.CaseTasks.AsNoTracking()
            .Where(item => taskGuids.Contains(item.Id))
            .ToListAsync();
        Assert.Equal(tasks.Count, stored.Count);
        Assert.All(stored, item =>
        {
            Assert.Equal(Guid.Parse(sender.SubjectId), item.CreatedByStaffId);
            Assert.Equal(
                history.Single(entry => entry.AggregateId == item.Id.ToString("D")).OccurredAtUtc,
                item.CreatedAtUtc);
        });
    }

    /// <summary>
    /// Staff Mark report sent creates the same tasks when the linked Sent item is a Pegasus report
    /// send, resolved from the Sent staff-mail operation that observed it, and reports them in the
    /// result. The tasks are in the name of the staff member who sent the report, not the one who
    /// marked it sent; the same link again creates none more.
    /// </summary>
    [Fact]
    public async Task StaffMarkReportSentCreatesTheSendsTasksOnceInTheSendersName()
    {
        await using var harness = await WorkflowHarness.CreateIsolatedAsync();
        var staff = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var sender = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var started = await new StartCaseWork(harness.Store).ExecuteAsync(
            new ChangeCaseStateRequest(
                harness.CaseId,
                0,
                staff,
                "start-after-send-staff",
                "Inspection work started",
                (await harness.Store.ClaimAsync(
                    new(harness.CaseId, 0, staff, "claim-start-after-send-staff"),
                    default)).Token),
            default);
        harness.TimeProvider.Advance(TimeSpan.FromMinutes(3));
        var retained = await RetainReportEvidenceAsync(
            harness,
            "after-send-staff",
            harness.TimeProvider.GetUtcNow().AddMinutes(-2),
            harness.TimeProvider.GetUtcNow().AddMinutes(-1));
        await SeedReportSendAsync(
            harness, sender, "immutable-item-after-send-staff", AfterSendTasks);
        var link = new LinkReportEvidence(harness.Store);
        var request = new LinkReportEvidenceRequest(
            harness.CaseId,
            started.Version,
            staff,
            "link-after-send-staff",
            "Exact approved-mailbox Sent item linked",
            (await harness.Store.ClaimAsync(
                new(harness.CaseId, started.Version, staff, "claim-link-after-send-staff"),
                default)).Token,
            retained.EvidenceId);

        var first = await link.ExecuteAsync(request, default);
        var replay = await link.ExecuteAsync(request, default);

        Assert.Equal(CaseLifecycleState.ReportSent, first.Workflow.State);
        Assert.Equal(
            AfterSendTasks,
            first.TasksCreated.Select(task => task.Description).ToArray());
        Assert.All(first.TasksCreated, task =>
        {
            Assert.Equal(CaseTaskState.Open, task.State);
            Assert.Null(task.AssigneeId);
        });
        // The link moves the Case's version on once and each task once more.
        Assert.Equal(started.Version + 3, first.Workflow.Version);
        Assert.Empty(replay.TasksCreated);
        await using var context = await harness.Factory.CreateDbContextAsync();
        Assert.Equal(2, await context.CaseTasks.CountAsync(task => task.CaseId == harness.CaseId));
        var events = await context.CaseWorkflowEvents.AsNoTracking()
            .Where(item => item.CaseId == harness.CaseId && item.EventType == "case_task_created")
            .ToListAsync();
        Assert.Equal(2, events.Count);
        await AssertCreatedInTheSendersNameAsync(harness, events, first.TasksCreated, sender);
    }

    /// <summary>
    /// Mark report sent on evidence that is not a Pegasus report send, one with no Sent staff-mail
    /// operation behind it, creates no task, even when another send of the Case froze a list.
    /// </summary>
    [Fact]
    public async Task StaffMarkReportSentOfEvidenceThatIsNotAPegasusReportSendCreatesNoTasks()
    {
        await using var harness = await WorkflowHarness.CreateIsolatedAsync();
        var staff = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var started = await new StartCaseWork(harness.Store).ExecuteAsync(
            new ChangeCaseStateRequest(
                harness.CaseId,
                0,
                staff,
                "start-after-send-foreign",
                "Inspection work started",
                (await harness.Store.ClaimAsync(
                    new(harness.CaseId, 0, staff, "claim-start-after-send-foreign"),
                    default)).Token),
            default);
        harness.TimeProvider.Advance(TimeSpan.FromMinutes(3));
        var retained = await RetainReportEvidenceAsync(
            harness,
            "after-send-foreign",
            harness.TimeProvider.GetUtcNow().AddMinutes(-2),
            harness.TimeProvider.GetUtcNow().AddMinutes(-1));
        // A Pegasus send of this Case recorded a list, but a different Sent item carried it.
        await SeedReportSendAsync(
            harness, staff, "immutable-item-someone-else", AfterSendTasks);

        var linked = await new LinkReportEvidence(harness.Store).ExecuteAsync(
            new(
                harness.CaseId,
                started.Version,
                staff,
                "link-after-send-foreign",
                "Exact approved-mailbox Sent item linked",
                (await harness.Store.ClaimAsync(
                    new(harness.CaseId, started.Version, staff, "claim-link-after-send-foreign"),
                    default)).Token,
                retained.EvidenceId),
            default);

        Assert.Equal(CaseLifecycleState.ReportSent, linked.Workflow.State);
        Assert.Empty(linked.TasksCreated);
        Assert.Equal(started.Version + 1, linked.Workflow.Version);
        await using var context = await harness.Factory.CreateDbContextAsync();
        Assert.Empty(await context.CaseTasks.Where(task => task.CaseId == harness.CaseId).ToListAsync());
    }

    /// <summary>
    /// A Pegasus report send of the Case by <paramref name="sender"/>: its report generation, the
    /// Sent CaseReport staff-mail operation that observed the Sent item
    /// <paramref name="immutableItemIdentity"/> and recorded <paramref name="afterSendTasks"/> in
    /// its report dispatch, and the operation's staff-mail-prepared history row, which records the
    /// sender's role. Returns the generation and the operation.
    /// </summary>
    private static async Task<(Guid GenerationId, Guid OperationId)> SeedReportSendAsync(
        WorkflowHarness harness,
        ActionActor sender,
        string immutableItemIdentity,
        IReadOnlyList<string> afterSendTasks)
    {
        var now = harness.TimeProvider.GetUtcNow();
        var generationId = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var operationKey = $"send-report-{operationId:N}";
        await using var context = await harness.Factory.CreateDbContextAsync();
        context.Set<CaseReportGenerationEntity>().Add(new()
        {
            Id = generationId,
            CaseId = harness.CaseId,
            WorkId = harness.CaseId,
            CaseVersion = 1,
            SnapshotHash = new string('b', 64),
            SnapshotJson = "{}",
            TemplateVersion = "test",
            RendererVersion = "test",
            State = "ready",
            GeneratedAtUtc = now.AddMinutes(-10),
            Version = 1
        });
        context.Set<StaffMailSendOperationEntity>().Add(new()
        {
            Id = operationId,
            ActorSubjectId = sender.SubjectId,
            MailboxId = Guid.NewGuid(),
            MailboxGeneration = 1,
            OperationKey = operationKey,
            PayloadHash = new string('d', 64),
            Purpose = Pegasus.Core.Operations.StaffMailPurpose.CaseReport,
            ContextId = generationId,
            ContextVersion = 1,
            ComposeMode = Pegasus.Core.Operations.StaffMailComposeMode.New,
            RecipientsJson = "[]",
            Subject = "Report",
            Body = "Please see attached.",
            AttachmentsJson = "[]",
            State = Pegasus.Core.Operations.StaffMailState.Sent,
            AttemptStage = Pegasus.Core.Operations.StaffMailAttemptStage.ObserveSent,
            CorrelationMarker = $"marker-{operationId:N}",
            CreatedAtUtc = now.AddMinutes(-4),
            RequestedAtUtc = now.AddMinutes(-4),
            ObservedSentImmutableMessageId = immutableItemIdentity,
            ProviderSentAtUtc = now.AddMinutes(-2),
            ObservedSentAtUtc = now.AddMinutes(-1),
            // What the send recorded, as the staff-mail store writes it.
            ReportDispatchJson = System.Text.Json.JsonSerializer.Serialize(
                new Pegasus.Core.Reports.ReportDispatchRecord([], [], null, null, afterSendTasks),
                System.Text.Json.JsonSerializerOptions.Web),
            Version = 3,
            ConcurrencyToken = Guid.NewGuid()
        });
        context.ActionHistory.Add(new()
        {
            Id = Guid.NewGuid(),
            AggregateType = "StaffMailSend",
            AggregateId = operationId.ToString("D"),
            EventKind = "staff-mail-prepared",
            ActorKind = sender.Kind.ToString(),
            ActorSubjectId = sender.SubjectId,
            ActorRolesJson = System.Text.Json.JsonSerializer.Serialize(
                sender.Roles.Select(role => role.ToString()).Order(StringComparer.Ordinal)),
            OccurredAtUtc = now.AddMinutes(-4),
            Outcome = "succeeded",
            CorrelationId = operationKey
        });
        await context.SaveChangesAsync();
        return (generationId, operationId);
    }

    private static Task<RetainedApprovedMailboxReportSentEvidence> RetainReportEvidenceAsync(
        WorkflowHarness harness,
        string suffix,
        DateTimeOffset sentAtUtc,
        DateTimeOffset discoveredAtUtc) =>
        new RetainApprovedMailboxReportSentEvidence(
            harness.ReportSentEvidenceStore).ExecuteAsync(
            new(
                Guid.NewGuid(),
                "instructions@collisionengineers.co.uk",
                $"sent-folder-{suffix}",
                $"immutable-item-{suffix}",
                $"internet-message-{suffix}",
                $"conversation-{suffix}",
                $"reply-chain-{suffix}",
                $"source-occurrence-{suffix}",
                new string('a', 64),
                new string('b', 64),
                sentAtUtc,
                discoveredAtUtc,
                ActionActor.SystemWorker("approved-mailbox-evidence-ingestion"),
                $"retain-{suffix}"),
            default);

    private sealed class WorkflowHarness : IAsyncDisposable
    {
        public const string ApprovedMailboxIdentity = WorkflowDatabase.ApprovedMailboxIdentity;
        public const string ApprovedMailboxAddress = WorkflowDatabase.ApprovedMailboxAddress;
        public const string ApprovedSentFolderIdentity = WorkflowDatabase.ApprovedSentFolderIdentity;

        private static readonly DateTimeOffset StartUtc = WorkflowDatabase.StartUtc;
        private readonly WorkflowDatabase database;
        private readonly bool ownsDatabase;
        private readonly WorkflowDatabase.SeededCases seeded;
        private readonly PooledDbContextFactory<PegasusDbContext> factory;

        public PooledDbContextFactory<PegasusDbContext> Factory => factory;

        private WorkflowHarness(
            WorkflowDatabase database,
            bool ownsDatabase,
            WorkflowDatabase.SeededCases seeded,
            Guid caseId,
            Guid secondCaseId,
            Guid notReadyCaseId,
            MutableTimeProvider timeProvider)
        {
            this.database = database;
            this.ownsDatabase = ownsDatabase;
            this.seeded = seeded;
            factory = database.Factory;
            CaseId = caseId;
            SecondCaseId = secondCaseId;
            NotReadyCaseId = notReadyCaseId;
            TimeProvider = timeProvider;
            Store = new EfCaseWorkflowStore(factory, timeProvider);
            QueryStore = new EfCaseQueryStore(factory, timeProvider);
            ReportSentEvidenceStore = new EfCaseReportSentEvidenceStore(
                factory,
                new EfApprovedMailboxStore(factory, timeProvider, new MemoryCache(new MemoryCacheOptions())));
            ReplacementStore = new EfLinkedCaseReplacementStore(
                factory,
                timeProvider,
                [new PrincipalCaseMatchPolicy(new QdosInstructionExtractionPolicy())]);
            TaskStore = new EfCaseTaskStore(factory, timeProvider);
            DataStore = new EfCaseDataStore(factory);
        }

        public Guid CaseId { get; }
        public Guid SecondCaseId { get; }
        public Guid NotReadyCaseId { get; }
        public MutableTimeProvider TimeProvider { get; }
        public string ConnectionString => database.ConnectionString;
        public EfCaseWorkflowStore Store { get; }
        public EfCaseQueryStore QueryStore { get; }
        public EfCaseReportSentEvidenceStore ReportSentEvidenceStore { get; }
        public EfLinkedCaseReplacementStore ReplacementStore { get; }
        public EfCaseTaskStore TaskStore { get; }
        public EfCaseDataStore DataStore { get; }

        public async Task SeedStaffAccountAsync(
            Guid staffId,
            bool isEnabled,
            StaffRole role,
            bool isSignOffEngineer = false,
            bool isDefaultSignOffEngineer = false)
        {
            await using var context = await factory.CreateDbContextAsync();
            var roleName = role.ToString();
            var normalizedRoleName = roleName.ToUpperInvariant();
            var identityRole = await context.Roles.SingleOrDefaultAsync(
                item => item.NormalizedName == normalizedRoleName);
            if (identityRole is null)
            {
                identityRole = new IdentityRole<Guid>
                {
                    Id = Guid.NewGuid(),
                    Name = roleName,
                    NormalizedName = normalizedRoleName,
                    ConcurrencyStamp = Guid.NewGuid().ToString("N")
                };
                context.Roles.Add(identityRole);
            }

            var userName = $"workflow-test-{staffId:N}";
            var signature = isSignOffEngineer
                ? new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }
                : null;
            context.Users.Add(new PegasusIdentityUser
            {
                Id = staffId,
                UserName = userName,
                NormalizedUserName = userName.ToUpperInvariant(),
                IsEnabled = isEnabled,
                MustChangePassword = false,
                IsSignOffEngineer = isSignOffEngineer,
                SignOffPrintedName = isSignOffEngineer ? "Workflow Signatory" : null,
                SignOffSignature = signature,
                SignOffSignatureDigest = signature is null
                    ? null
                    : Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(signature)),
                IsDefaultSignOffEngineer = isDefaultSignOffEngineer,
                SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString("N")
            });
            context.UserRoles.Add(new IdentityUserRole<Guid>
            {
                UserId = staffId,
                RoleId = identityRole.Id
            });
            await context.SaveChangesAsync();
        }

        public async Task SetStaffEnabledAsync(Guid staffId, bool isEnabled)
        {
            await using var context = await factory.CreateDbContextAsync();
            var user = await context.Users.SingleAsync(item => item.Id == staffId);
            user.IsEnabled = isEnabled;
            await context.SaveChangesAsync();
        }

        public async Task<Guid> SeedStandaloneAuditEvidenceAsync(Guid caseId)
        {
            var receiptId = await database.ScalarAsync<Guid>(
                $"SELECT OriginIntakeReceiptId FROM Cases WHERE Id = '{caseId:D}'");
            var assetId = Guid.NewGuid();
            var evidenceId = Guid.NewGuid();
            await database.ExecuteAsync(
                $"INSERT INTO IntakeAssets (Id, IntakeReceiptId, SourceLabel, FileName, MediaType, Kind, Disposition, ContentLength, ContentHash, StorageKey) VALUES ('{assetId:D}', '{receiptId:D}', 'original-report', 'original-report.pdf', 'application/pdf', 'attachment', 'source', 1, '{new string('a', 64)}', 'standalone-audit/{assetId:N}')");
            await database.ExecuteAsync(
                $"INSERT INTO StandaloneAuditEvidence (Id, IntakeReceiptId, OriginalReportAssetId, Assessment, ConfirmedByKind, ConfirmedBySubjectId, ConfirmedByRolesJson, ConfirmedAtUtc, OperationKey, Reason, RequestHash, ResultingReceiptVersion) VALUES ('{evidenceId:D}', '{receiptId:D}', '{assetId:D}', 'repairable', 'Staff', '{Guid.NewGuid():D}', '[\"Administrator\"]', '2026-07-29T09:00:00+00:00', 'standalone-audit-{evidenceId:N}', 'Retained original report evidence', '{new string('b', 64)}', 1)");
            await database.ExecuteAsync(
                $"UPDATE Cases SET Type = 'audit', StandaloneAuditAssessment = 'repairable', StandaloneAuditEvidenceId = '{evidenceId:D}' WHERE Id = '{caseId:D}'");
            return evidenceId;
        }

        public async Task<Guid> SeedUnverifiedReportEvidenceAsync()
        {
            await using var context = await factory.CreateDbContextAsync();
            var evidenceId = Guid.NewGuid();
            var now = TimeProvider.GetUtcNow();
            context.CaseReportSentEvidence.Add(new()
            {
                Id = evidenceId,
                MailboxIdentity = "instructions@collisionengineers.co.uk",
                SentFolderIdentity = "legacy-sent-folder",
                ImmutableItemIdentity = $"legacy-item-{evidenceId:N}",
                InternetMessageIdentity = $"legacy-message-{evidenceId:N}",
                ConversationIdentity = $"legacy-conversation-{evidenceId:N}",
                ReplyChainIdentity = $"legacy-reply-chain-{evidenceId:N}",
                SourceOccurrenceIdentity = $"legacy-occurrence-{evidenceId:N}",
                SourceSha256 = new string('7', 64),
                MimeSha256 = new string('8', 64),
                SentAtUtc = now.AddMinutes(-1),
                DiscoveredAtUtc = now,
                DiscoveredByKind = "LegacyUnverified",
                DiscoveredBySubjectId = "legacy-migration",
                RetentionOperationKey = $"legacy:{evidenceId:N}",
                RetentionRequestHash = new string('0', 64)
            });
            await context.SaveChangesAsync();
            return evidenceId;
        }

        public Task<string> ReadCaseReferenceAsync(Guid caseId) => database.ScalarAsync<string>(
            $"SELECT Reference FROM Cases WHERE Id = '{caseId:D}'");

        // The scalar reader turns a NULL into an empty string; this reads it back as null.
        public async Task<string?> ReadAuditReferenceAsync(Guid caseId)
        {
            var value = await database.ScalarAsync<string>(
                $"SELECT COALESCE(AuditReference, N'') FROM Cases WHERE Id = '{caseId:D}'");
            return value.Length == 0 ? null : value;
        }

        public Task<Guid> ReadStandaloneAuditEvidenceIdAsync(Guid caseId) =>
            database.ScalarAsync<Guid>(
                $"SELECT StandaloneAuditEvidenceId FROM Cases WHERE Id = '{caseId:D}'");

        public Task<long> CountCasesWithStandaloneAuditEvidenceAsync(Guid evidenceId) =>
            database.ScalarAsync<long>(
                $"SELECT COUNT_BIG(*) FROM Cases WHERE StandaloneAuditEvidenceId = '{evidenceId:D}'");

        public Task<long> CountStandaloneAuditEvidenceAsync(Guid evidenceId) =>
            database.ScalarAsync<long>(
                $"SELECT COUNT_BIG(*) FROM StandaloneAuditEvidence WHERE Id = '{evidenceId:D}'");

        public async Task SeedCaseTaskAsync(
            Guid caseId,
            Guid taskId,
            CaseTaskState state)
        {
            await database.ExecuteAsync(
                $"INSERT INTO CaseTasks (Id, CaseId, Description, State, Version, ConcurrencyToken, CreatedAtUtc) VALUES ('{taskId:D}', '{caseId:D}', 'Retained replacement work', '{state}', 0, '{Guid.NewGuid():D}', '{StartUtc:O}')");
        }

        public Task<long> CountCasesAsync() =>
            database.ScalarAsync<long>("SELECT COUNT_BIG(*) FROM Cases");

        public Task<long> CountQdosReferencesAsync() =>
            database.ScalarAsync<long>(
                "SELECT COUNT_BIG(*) FROM Cases WHERE Reference LIKE 'QDOS26%'");

        /// <summary>Cases of its own in the class's database, removed when the harness is disposed.</summary>
        public static Task<WorkflowHarness> CreateAsync(WorkflowDatabase database) =>
            OpenAsync(database, ownsDatabase: false);

        /// <summary>
        /// Cases in a database of its own, dropped when the harness is disposed,
        /// for a test that reads or writes across the whole database.
        /// </summary>
        public static async Task<WorkflowHarness> CreateIsolatedAsync() =>
            await OpenAsync(await WorkflowDatabase.CreateAsync(), ownsDatabase: true);

        private static async Task<WorkflowHarness> OpenAsync(WorkflowDatabase database, bool ownsDatabase)
        {
            WorkflowDatabase.SeededCases? seeded = null;
            try
            {
                await using var context = await database.Factory.CreateDbContextAsync();
                var timeProvider = new MutableTimeProvider(StartUtc);
                var organizationId = Guid.NewGuid();
                var tstLineageId = Guid.NewGuid();
                var principalId = Guid.NewGuid();
                var engineerId = Guid.NewGuid();
                var caseId = Guid.NewGuid();
                var secondCaseId = Guid.NewGuid();
                var notReadyCaseId = Guid.NewGuid();
                var caseReceiptId = Guid.NewGuid();
                var secondCaseReceiptId = Guid.NewGuid();
                var notReadyReceiptId = Guid.NewGuid();
                seeded = new(
                    organizationId,
                    tstLineageId,
                    principalId,
                    [caseId, secondCaseId, notReadyCaseId],
                    [caseReceiptId, secondCaseReceiptId, notReadyReceiptId]);

                // The Principal's code and name, each Case's reference and each
                // receipt's token and hash are unique in the database, so they come
                // from the database's counter: the first harness in a database reads
                // TST001 and the references TST26001 to TST26003.
                var number = database.NextHarnessNumber();
                var code = $"TST{number:000}";
                var firstSequence = ((number - 1) * 3) + 1;
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO Organizations (Role, Id, Name, Version) VALUES (N'principal', {organizationId}, {$"Workflow test organization {code}"}, {0L})");
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO PrincipalSequenceLineages (Id, CreatedAtUtc) VALUES ({tstLineageId}, {StartUtc})");
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO Principals (Id, OrganizationId, Code, SequenceLineageId, IsActive, ReportSendingRulesJson, Version) VALUES ({principalId}, {organizationId}, {code}, {tstLineageId}, {true}, {EfOrganizationAdministration.DefaultReportSendingJson}, {0L})");
                var engineerRoleId = await context.Roles
                    .Where(role => role.NormalizedName == "ENGINEER")
                    .Select(role => role.Id)
                    .SingleAsync();
                var engineerUserName = $"workflow-test-{engineerId:N}";
                context.Users.Add(new PegasusIdentityUser
                {
                    Id = engineerId,
                    UserName = engineerUserName,
                    NormalizedUserName = engineerUserName.ToUpperInvariant(),
                    IsEnabled = true,
                    MustChangePassword = false,
                    SecurityStamp = Guid.NewGuid().ToString("N"),
                    ConcurrencyStamp = Guid.NewGuid().ToString("N")
                });
                context.UserRoles.Add(new IdentityUserRole<Guid>
                {
                    UserId = engineerId,
                    RoleId = engineerRoleId
                });
                await context.SaveChangesAsync();
                await InsertReceiptAsync(context, caseReceiptId, firstSequence);
                await InsertReceiptAsync(context, secondCaseReceiptId, firstSequence + 1);
                await InsertReceiptAsync(context, notReadyReceiptId, firstSequence + 2);
                await InsertCaseAsync(
                    context,
                    caseId,
                    principalId,
                    tstLineageId,
                    caseReceiptId,
                    $"TST26{firstSequence:000}",
                    1);
                await InsertCaseDataSnapshotAsync(context, caseId, caseReceiptId, firstSequence);
                await InsertCaseAsync(
                    context,
                    secondCaseId,
                    principalId,
                    tstLineageId,
                    secondCaseReceiptId,
                    $"TST26{firstSequence + 1:000}",
                    2);
                await InsertCaseAsync(
                    context,
                    notReadyCaseId,
                    principalId,
                    tstLineageId,
                    notReadyReceiptId,
                    $"TST26{firstSequence + 2:000}",
                    3);
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO CaseWorkflows (CaseId, State, AssignedEngineerId, Version, ConcurrencyToken) VALUES ({caseId}, {nameof(CaseLifecycleState.Review)}, {engineerId}, {0L}, {Guid.NewGuid()})");
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO CaseWorkflows (CaseId, State, AssignedEngineerId, Version, ConcurrencyToken) VALUES ({secondCaseId}, {nameof(CaseLifecycleState.Review)}, {engineerId}, {0L}, {Guid.NewGuid()})");
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO CaseWorkflows (CaseId, State, Version, ConcurrencyToken) VALUES ({notReadyCaseId}, {nameof(CaseLifecycleState.NotReady)}, {0L}, {Guid.NewGuid()})");
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO CaseDueWork (CaseId, MissingMaterialReason, State, NextChaseAtUtc, NextChaseAtUtcTicks, Version, ConcurrencyToken) VALUES ({notReadyCaseId}, {"Waiting for images"}, {nameof(CaseDueWorkState.Scheduled)}, {timeProvider.GetUtcNow().AddDays(3)}, {timeProvider.GetUtcNow().AddDays(3).UtcDateTime.Ticks}, {0L}, {Guid.NewGuid()})");
                return new(database, ownsDatabase, seeded, caseId, secondCaseId, notReadyCaseId, timeProvider);
            }
            catch
            {
                if (ownsDatabase)
                {
                    await database.DisposeAsync();
                }
                else if (seeded is not null)
                {
                    await database.RemoveAsync(seeded);
                }

                throw;
            }
        }

        private static async Task InsertCaseAsync(
            PegasusDbContext context,
            Guid caseId,
            Guid principalId,
            Guid sequenceLineageId,
            Guid receiptId,
            string reference,
            int sequence)
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO Cases (Id, PrincipalId, SequenceLineageId, Year, Sequence, Reference, Type, InitialState, CustodyState, OriginIntakeReceiptId, InstructionComplete, ImagesComplete, CreatedAtUtc, Version, ConcurrencyToken) VALUES ({caseId}, {principalId}, {sequenceLineageId}, {2026}, {sequence}, {reference}, {"inspection"}, {"review"}, {"pending"}, {receiptId}, {true}, {true}, {StartUtc}, {0L}, {Guid.NewGuid()})");
            await CaseWorkFixture.InsertPrimaryWorksAsync(context);
        }

        private static async Task InsertCaseDataSnapshotAsync(
            PegasusDbContext context,
            Guid caseId,
            Guid receiptId,
            int sequence)
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO CaseDataSnapshots (WorkId, OriginIntakeReceiptId, OriginSourceChannel, OriginExternalReceiptToken, OriginSourceHash, OriginReceivedAtUtc, SourceReaderKey, SourceReaderVersion, ExtractionPolicyKey, ExtractionPolicyVersion, CompletenessPolicyKey, CompletenessPolicyVersion, CompletenessPolicySatisfied, AcceptedAtUtc) VALUES ({caseId}, {receiptId}, {"manual_upload"}, {$"workflow-{sequence}"}, {sequence.ToString("X64", System.Globalization.CultureInfo.InvariantCulture)}, {StartUtc}, {"workflow-test-reader"}, {"1"}, {"workflow-fixture"}, {1}, {"case-workflow"}, {1}, {true}, {StartUtc})");
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO CaseDataFields (WorkId, FieldName, ValueKind, ValueType, Value, SourceKind, SourceIdentity, SourceLabel, PolicyKey, PolicyVersion, ConfirmedByActor, ConfirmedAtUtc) VALUES ({caseId}, {"claimant_name"}, {"confirmed"}, {"text"}, {"Jane Workflow"}, {"intake_evidence"}, {receiptId.ToString("D")}, {"workflow fixture evidence"}, {"workflow-fixture"}, {1}, {"workflow-staff"}, {StartUtc})");
        }

        private static Task<int> InsertReceiptAsync(
            PegasusDbContext context,
            Guid receiptId,
            int sequence) =>
            context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO IntakeReceipts (Id, SourceFileName, MediaType, SourceLength, SourceHash, SourceChannel, ExternalReceiptToken, ReceivedAtUtc, ProcessedAtUtc, SourceReaderKey, SourceReaderVersion, Version, Decision, DecisionReason, EvidenceJson, FieldsJson, OcrCandidatesJson) VALUES ({receiptId}, {$"workflow-{sequence}.eml"}, {"message/rfc822"}, {1L}, {sequence.ToString("X64", System.Globalization.CultureInfo.InvariantCulture)}, {"manual_upload"}, {$"workflow-{sequence}"}, {StartUtc}, {StartUtc}, {"workflow-test-reader"}, {"1"}, {0L}, {"case_created"}, {"Workflow persistence fixture"}, {"""{"version":1,"data":[]}"""}, {"""{"version":1,"data":[]}"""}, {"""{"version":1,"data":[]}"""})");

        /// <summary>Every retained lease column, so a refusal can be proved to have changed none of them.</summary>
        public async Task<RetainedLeaseRow> ReadLeaseRowAsync(Guid caseId)
        {
            await using var context = await factory.CreateDbContextAsync();
            var workflow = await context.CaseWorkflows.AsNoTracking()
                .SingleAsync(item => item.CaseId == caseId);
            return new(
                workflow.Version,
                workflow.ConcurrencyToken,
                workflow.EditLeaseToken,
                workflow.EditLeaseTokenHash,
                workflow.EditLeaseRequestHash,
                workflow.EditLeaseHolder,
                workflow.EditLeaseHolderKind,
                workflow.EditLeaseOperationKey,
                workflow.EditLeaseExpiresAtUtc);
        }

        public async Task<bool> HasLeaseReplayMaterialAsync(Guid caseId)
        {
            await using var context = await factory.CreateDbContextAsync();
            var workflow = await context.CaseWorkflows.AsNoTracking()
                .SingleAsync(item => item.CaseId == caseId);
            return workflow.EditLeaseToken is not null
                || workflow.EditLeaseRequestHash is not null;
        }

        public async Task<long> LeaseOperationCountAsync(
            Guid caseId,
            string operationKey)
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context.CaseEditLeaseOperations.LongCountAsync(
                item => item.CaseId == caseId
                    && item.OperationKey == operationKey);
        }

        /// <summary>Every lease operation row the case carries, whatever its key.</summary>
        public async Task<long> LeaseOperationCountAsync(Guid caseId)
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context.CaseEditLeaseOperations.LongCountAsync(
                item => item.CaseId == caseId);
        }

        /// <summary>
        /// The operation key retained on the case row — the claim key the workspace parses back to
        /// restore edit mode, which a heartbeat must leave alone.
        /// </summary>
        public async Task<string?> LeaseOperationKeyAsync(Guid caseId)
        {
            await using var context = await factory.CreateDbContextAsync();
            var workflow = await context.CaseWorkflows.AsNoTracking()
                .SingleAsync(item => item.CaseId == caseId);
            return workflow.EditLeaseOperationKey;
        }

        /// <summary>The case's projected CaseMatchIndex row, or null when the case has no provider yet.</summary>
        public async Task<CaseMatchIndexEntity?> FindMatchIndexRowAsync(Guid caseId)
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context.CaseMatchIndex
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.CaseId == caseId);
        }

        public async Task<long> WorkflowEventCountAsync(string operationKey)
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context.Database.SqlQuery<long>(
                    $"SELECT COUNT_BIG(*) AS [Value] FROM [CaseWorkflowEvents] WHERE [OperationKey] = {operationKey}")
                .SingleAsync();
        }

        /// <summary>When the Case entered its current state.</summary>
        public async Task<DateTimeOffset?> StateEnteredAtAsync(Guid caseId)
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context.CaseWorkflows.AsNoTracking()
                .Where(item => item.CaseId == caseId)
                .Select(item => item.StateEnteredAtUtc)
                .SingleAsync();
        }

        /// <summary>The history lines one operation wrote, for who they name and what they say.</summary>
        public async Task<IReadOnlyList<CaseWorkflowEventEntity>> WorkflowEventsAsync(string operationKey)
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context.CaseWorkflowEvents.AsNoTracking()
                .Where(item => item.OperationKey == operationKey)
                .ToListAsync();
        }

        public async Task<long> ActionHistoryCountAsync(
            string eventKind,
            string operationKey)
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context.Database.SqlQuery<long>(
                    $"SELECT COUNT_BIG(*) AS [Value] FROM [ActionHistory] WHERE [EventKind] = {eventKind} AND [CorrelationId] = {operationKey}")
                .SingleAsync();
        }

        public async Task<long> PollOutcomeCountAsync(
            string immutableItemIdentity,
            string outcomeKind,
            Guid relatedEvidenceId)
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context.Database.SqlQuery<long>(
                    $"SELECT COUNT_BIG(*) AS [Value] FROM [ApprovedSentPollOutcomes] WHERE [ImmutableItemIdentity] = {immutableItemIdentity} AND [OutcomeKind] = {outcomeKind} AND [RelatedEvidenceId] = {relatedEvidenceId}")
                .SingleAsync();
        }

        public async Task<long> WorkflowEventTypeCountAsync(Guid caseId, string eventType)
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context.Database.SqlQuery<long>(
                    $"SELECT COUNT_BIG(*) AS [Value] FROM [CaseWorkflowEvents] WHERE [CaseId] = {caseId} AND [EventType] = {eventType}")
                .SingleAsync();
        }

        public async Task<long> ActionHistoryAggregateCountAsync(
            string aggregateType,
            string aggregateId,
            string eventKind)
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context.Database.SqlQuery<long>(
                    $"SELECT COUNT_BIG(*) AS [Value] FROM [ActionHistory] WHERE [AggregateType] = {aggregateType} AND [AggregateId] = {aggregateId} AND [EventKind] = {eventKind}")
                .SingleAsync();
        }

        public async Task<Guid?> ReadReportEvidenceCaseIdAsync(Guid evidenceId)
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context.CaseReportSentEvidence
                .AsNoTracking()
                .Where(item => item.Id == evidenceId)
                .Select(item => item.CaseId)
                .SingleAsync();
        }

        /// <summary>Ends a harness: drops the database it made, or removes its Cases from the shared one.</summary>
        public async ValueTask DisposeAsync()
        {
            if (ownsDatabase)
            {
                await database.DisposeAsync();
            }
            else
            {
                await database.RemoveAsync(seeded);
            }
        }
    }

    private sealed class OneItemApprovedSentSource(ApprovedSentItem item) : IApprovedSentSource
    {
        public Task<ApprovedSentPage> ReadAsync(
            ApprovedSentPollLease lease,
            int maximumItems,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new ApprovedSentPage([item], item.NextCursor, HasMore: false));
        }
    }

    public sealed record RetainedLeaseRow(
        long Version,
        Guid ConcurrencyToken,
        string? Token,
        string? TokenHash,
        string? RequestHash,
        string? Holder,
        string? HolderKind,
        string? OperationKey,
        DateTimeOffset? ExpiresAtUtc);

    public sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan interval)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(interval, TimeSpan.Zero);

            _utcNow += interval;
        }
    }
}

/// <summary>
/// The one database a class of workflow persistence tests shares: restored and
/// migrated once, with the approved Sent-evidence mailbox set up as the estate
/// has it. A test makes Cases of its own in it and removes them when it ends,
/// so the next test finds the database as the one before it did. A test that
/// reads or writes across the whole database makes a database of its own with
/// <see cref="CreateAsync"/> instead.
/// </summary>
public sealed class WorkflowDatabase : IAsyncLifetime
{
    internal const string ApprovedMailboxIdentity = "instructions";
    internal const string ApprovedMailboxAddress = "instructions@collisionengineers.co.uk";
    internal const string ApprovedSentFolderIdentity = "sent-items";

    internal static readonly DateTimeOffset StartUtc = new(2026, 7, 29, 9, 0, 0, TimeSpan.Zero);

    private LocalDbTestDatabase? database;
    private ForeignKeyRowRemoval? caseRemoval;
    private int harnessNumber;

    internal PooledDbContextFactory<PegasusDbContext> Factory { get; private set; } = null!;

    internal string ConnectionString => Database.ConnectionString;

    private LocalDbTestDatabase Database =>
        database ?? throw new InvalidOperationException("The database has not been created.");

    /// <summary>A database of its own, for a test that cannot share the class's.</summary>
    internal static async Task<WorkflowDatabase> CreateAsync()
    {
        var created = new WorkflowDatabase();
        try
        {
            await created.InitializeAsync();
        }
        catch
        {
            await created.DisposeAsync();
            throw;
        }

        return created;
    }

    public async Task InitializeAsync()
    {
        database = await LocalDbTestDatabase.CreateAsync();
        Factory = new PooledDbContextFactory<PegasusDbContext>(
            new DbContextOptionsBuilder<PegasusDbContext>().UseSqlServer(database.ConnectionString).Options);
        await using var context = await Factory.CreateDbContextAsync();

        // The Sent-evidence mailbox as the approved estate records it: the
        // stable mailbox and Sent-folder identities, activated, on the seeded
        // row whose generation is already positive.
        var mailbox = (await context.ApprovedMailboxes.FindAsync(
            await TestMailboxId.EnsureApprovedAsync(
                context, ApprovedMailboxIdentity, ApprovedMailboxAddress, StartUtc.AddDays(-1))))!;
        mailbox.AllowOutgoing = true;
        mailbox.SentFolderIdentity = ApprovedSentFolderIdentity;
        mailbox.Version = 2;
        await context.SaveChangesAsync();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO ApprovedInboxPollStates (ApprovedMailboxId, MailboxAddress, ScopeFingerprint, ActivatedAtUtc, DueAtUtc) SELECT Id, Address, REPLICATE('0', 64), {StartUtc}, {StartUtc} FROM ApprovedMailboxes WHERE Address = {ApprovedMailboxAddress}");
    }

    public async Task DisposeAsync()
    {
        if (database is not null)
        {
            await database.DisposeAsync();
        }
    }

    /// <summary>The next number for a harness's Principal code, references and receipts, each unique in the database.</summary>
    internal int NextHarnessNumber() => Interlocked.Increment(ref harnessNumber);

    internal Task<T> ScalarAsync<T>(string commandText) => Database.ScalarAsync<T>(commandText);

    internal Task ExecuteAsync(string commandText) => Database.ExecuteAsync(commandText);

    /// <summary>
    /// Removes the Cases and everything that hangs from them, and the Principal,
    /// Organization, sequence lineage and intake receipts they were seeded with.
    /// The Engineer's account stays: its name is its own and no test reads the
    /// accounts as a set.
    /// </summary>
    internal async Task RemoveAsync(SeededCases seeded)
    {
        caseRemoval ??= await ForeignKeyRowRemoval.CreateAsync(
            Database, ForeignKeyRowRemoval.CasesTable, ForeignKeyRowRemoval.InRoots, []);

        // One batch: the walk from every Case, then the history (it names a Case
        // without a key to it) and the rows the Cases were seeded from.
        var parameters = new List<(string Name, object Value)>
        {
            ("@principalId", seeded.PrincipalId),
            ("@lineageId", seeded.LineageId),
            ("@organizationId", seeded.OrganizationId),
        };
        var caseNames = ForeignKeyRowRemoval.AddParameters(parameters, "case", seeded.CaseIds);
        var receiptNames = ForeignKeyRowRemoval.AddParameters(parameters, "receipt", seeded.ReceiptIds);
        var sql = ForeignKeyRowRemoval.RootsDeclaration
            + $"INSERT @roots (Id) VALUES {string.Join(", ", caseNames.Select(name => $"({name})"))};\n"
            + caseRemoval.Sql + ";\n"
            + "DELETE FROM [dbo].[ActionHistory] WHERE AggregateId IN (SELECT LOWER(CONVERT(nvarchar(36), Id)) FROM @roots);\n"
            + "DELETE FROM [dbo].[Principals] WHERE Id = @principalId;\n"
            + "DELETE FROM [dbo].[PrincipalSequenceLineages] WHERE Id = @lineageId;\n"
            + "DELETE FROM [dbo].[Organizations] WHERE Id = @organizationId;\n"
            + $"DELETE FROM [dbo].[IntakeReceipts] WHERE Id IN ({string.Join(", ", receiptNames)})";
        await ForeignKeyRowRemoval.ExecuteAsync(Database, sql, [.. parameters]);
    }

    /// <summary>The Cases one harness seeded, with the rows they were seeded from.</summary>
    internal sealed record SeededCases(
        Guid OrganizationId, Guid LineageId, Guid PrincipalId, Guid[] CaseIds, Guid[] ReceiptIds);
}
