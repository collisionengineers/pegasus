using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Tasks;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure;
using Pegasus.Infrastructure.Email;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class CaseWorkflowPersistenceTests
{
    [Fact]
    public async Task StartMovesDirectlyToReportPreparationAndRetainedSentEvidenceNeedsNoApproval()
    {
        await using var harness = await WorkflowHarness.CreateAsync();
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
        var start = new StartCaseWork(harness.Store, harness.EngineerEligibility);

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
        var sent = await new LinkReportEvidence(harness.Store).ExecuteAsync(
            new(
                harness.CaseId,
                started.Version,
                actor,
                "sent-1",
                "Exact approved-mailbox Sent item linked",
                sentLease.Token,
                retained.EvidenceId),
            default);

        Assert.Equal(CaseLifecycleState.PostReport, sent.State);
        Assert.Null(sent.ReportApproval);
        Assert.Equal(evidenceId, sent.ReportSentEvidence?.EvidenceId);

        var closeLease = await harness.Store.ClaimAsync(
            new(harness.CaseId, sent.Version, actor, "claim-post-report-close"),
            default);
        var closed = await new CloseCase(harness.Store).ExecuteAsync(
            new(
                harness.CaseId,
                sent.Version,
                actor,
                "close-post-report",
                "Provider cancelled after report delivery",
                closeLease.Token,
                CaseClosureOutcome.ProviderCancelled),
            default);
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
                CaseReopenDestination.PostReport),
            default);

        Assert.Equal(CaseLifecycleState.PostReport, reopened.State);
        Assert.Equal(evidenceId, reopened.ReportSentEvidence?.EvidenceId);
    }

    [Fact]
    public async Task StartRejectsEngineerDisabledAfterAssignment()
    {
        await using var harness = await WorkflowHarness.CreateAsync();
        var workflow = Assert.IsType<CaseWorkflowRecord>(
            await harness.Store.GetAsync(harness.CaseId, default));
        var engineerId = Assert.IsType<Guid>(workflow.AssignedEngineerId);
        await harness.SetStaffEnabledAsync(engineerId, false);
        var actor = ActionActor.Staff(engineerId, [StaffRole.Engineer]);
        var lease = await harness.Store.ClaimAsync(
            new(harness.CaseId, workflow.Version, actor, "claim-start-disabled-engineer"),
            default);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new StartCaseWork(harness.Store, harness.EngineerEligibility).ExecuteAsync(
                new ChangeCaseStateRequest(
                    harness.CaseId,
                    workflow.Version,
                    actor,
                    "start-disabled-engineer",
                    "Attempted start after Engineer account was disabled",
                    lease.Token),
                default));

        Assert.Contains("Engineer account is disabled", exception.Message, StringComparison.Ordinal);
        var unchanged = Assert.IsType<CaseWorkflowRecord>(
            await harness.Store.GetAsync(harness.CaseId, default));
        Assert.Equal(CaseLifecycleState.Review, unchanged.State);
        Assert.Equal(workflow.Version, unchanged.Version);
    }

    [Fact]
    public async Task ApprovedSentPollRetainsAndAutoLinksOneAuthoritativeCaseIdempotently()
    {
        await using var harness = await WorkflowHarness.CreateAsync();
        var staff = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var started = await new StartCaseWork(harness.Store, harness.EngineerEligibility).ExecuteAsync(
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

        const string mailboxId = "report-auto-link-mailbox";
        const string mailboxAddress = "instructions@collisionengineers.co.uk";
        const string sentFolderId = "report-auto-link-sent-folder";
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
        var replay = await poll.ExecuteAsync(1, 10, worker, default);

        Assert.Equal(first, replay);
        Assert.Equal(1, first.ReportEvidenceRetained);
        Assert.Equal(0, first.UnlinkedItems);
        var workflow = Assert.IsType<CaseWorkflowRecord>(
            await harness.Store.GetAsync(harness.CaseId, default));
        var linked = Assert.IsType<ApprovedMailboxReportSentEvidence>(
            workflow.ReportSentEvidence);
        var evidence = Assert.IsType<RetainedApprovedMailboxReportSentEvidence>(
            await scope.ServiceProvider
                .GetRequiredService<IApprovedMailboxReportSentEvidenceQueries>()
                .GetAsync(linked.EvidenceId, default));
        Assert.Equal(CaseLifecycleState.PostReport, workflow.State);
        Assert.Equal(immutableItemId, evidence.ImmutableItemIdentity);
        Assert.Equal(harness.CaseId, await harness.ReadReportEvidenceCaseIdAsync(evidence.EvidenceId));
        Assert.Equal(
            1L,
            await harness.PollOutcomeCountAsync(
                immutableItemId,
                nameof(SentEvidencePollOutcomeKind.ReportEvidenceAutoLinked),
                evidence.EvidenceId));
        Assert.Equal(
            1L,
            await harness.WorkflowEventTypeCountAsync(
                harness.CaseId,
                "report_evidence_auto_linked"));
        Assert.Equal(
            1L,
            await harness.ActionHistoryAggregateCountAsync(
                "report_sent_evidence",
                evidence.EvidenceId.ToString("D"),
                "report_sent_evidence_retained"));
    }

    [Fact]
    public async Task ReportApprovalIsServerStampedAndSentEvidenceFollowsPreparationAndApproval()
    {
        await using var harness = await WorkflowHarness.CreateAsync();
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var started = await new StartCaseWork(harness.Store, harness.EngineerEligibility).ExecuteAsync(
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
        var linked = await linkEvidence.ExecuteAsync(
            new(
                harness.CaseId,
                approved.Version,
                actor,
                "link-after-approval",
                "Link exact approved-mailbox evidence",
                sentLease.Token,
                qualifyingEvidence.EvidenceId),
            default);

        Assert.Equal(CaseLifecycleState.PostReport, linked.State);
        Assert.Equal(qualifyingEvidence.EvidenceId, linked.ReportSentEvidence?.EvidenceId);
        Assert.Equal(1L, await harness.WorkflowEventCountAsync("link-after-approval"));
    }

    [Fact]
    public async Task AutoLinkUsesCanonicalVersionAndHistoryClearsLeaseAndReplays()
    {
        await using var harness = await WorkflowHarness.CreateAsync();
        var staff = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var started = await new StartCaseWork(harness.Store, harness.EngineerEligibility).ExecuteAsync(
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
        Assert.Equal(CaseLifecycleState.PostReport, linked.State);
        Assert.Equal(started.Version + 1, linked.Version);
        Assert.Equal(linked, replayed);
        var details = Assert.IsType<CaseDetails>(
            await harness.QueryStore.GetAsync(new(harness.CaseId, staff), default));
        Assert.Equal(
            worker.Kind,
            details.Workflow.ReportSentEvidence?.LinkedBy.Kind);
        Assert.Equal(
            worker.SubjectId,
            details.Workflow.ReportSentEvidence?.LinkedBy.SubjectId);
        Assert.Null(first.NotLinkedReasonCode);
        Assert.Null(replay.NotLinkedReasonCode);
        Assert.False(await harness.HasLeaseReplayMaterialAsync(harness.CaseId));
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
        await using var harness = await WorkflowHarness.CreateAsync();
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
        var started = await new StartCaseWork(harness.Store, harness.EngineerEligibility).ExecuteAsync(
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
        await using var harness = await WorkflowHarness.CreateAsync();
        var staff = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var firstStarted = await new StartCaseWork(harness.Store, harness.EngineerEligibility).ExecuteAsync(
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
        var secondStarted = await new StartCaseWork(harness.Store, harness.EngineerEligibility).ExecuteAsync(
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

        var closed = await new CloseCase(harness.Store).ExecuteAsync(
            new(
                harness.CaseId,
                linkedAssociation.Version,
                staff,
                "close-before-auto-unlink",
                "Correct the automatic report-evidence association",
                (await harness.Store.ClaimAsync(
                    new(
                        harness.CaseId,
                        linkedAssociation.Version,
                        staff,
                        "claim-close-before-auto-unlink"),
                    default)).Token,
                CaseClosureOutcome.ProviderCancelled),
            default);
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

        var staffLinked = await new LinkReportEvidence(harness.Store).ExecuteAsync(
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
        await using var harness = await WorkflowHarness.CreateAsync();
        var staff = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var started = await new StartCaseWork(harness.Store, harness.EngineerEligibility).ExecuteAsync(
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
        Assert.Equal(CaseLifecycleState.PostReport, persisted.State);
        Assert.Equal(retained.EvidenceId, persisted.ReportSentEvidence?.EvidenceId);
        Assert.Equal(
            1L,
            await harness.WorkflowEventCountAsync(staffOperationKey)
                + await harness.WorkflowEventCountAsync(autoOperationKey));
        Assert.False(await harness.HasLeaseReplayMaterialAsync(harness.CaseId));
    }

    [Theory]
    [InlineData(false, true, StaffRole.Engineer, "does not exist")]
    [InlineData(true, false, StaffRole.Engineer, "is disabled")]
    [InlineData(true, true, StaffRole.User, "does not hold the Engineer role")]
    public async Task MissingDisabledOrNonEngineerStaffCannotBeAssigned(
        bool createAccount,
        bool isEnabled,
        StaffRole role,
        string expectedMessage)
    {
        await using var harness = await WorkflowHarness.CreateAsync();
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
        await using var staffContext = await harness.Factory.CreateDbContextAsync();
        var sut = new AssignCaseEngineer(
            harness.Store,
            new DefaultCaseWorkflowConfiguration(),
            harness.EngineerEligibility,
            new EfStaffAccountQueries(staffContext));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ExecuteAsync(
                new(
                    harness.CaseId,
                    before.Version,
                    actor,
                    operationKey,
                    "Attempt to assign ineligible staff",
                    lease.Token,
                    engineerId,
                    new(true, true, "accepted-readiness")),
                default));

        Assert.Contains(expectedMessage, exception.Message, StringComparison.Ordinal);
        var persisted = await harness.Store.GetAsync(harness.CaseId, default);
        Assert.Equal(before.AssignedEngineerId, persisted?.AssignedEngineerId);
        Assert.Equal(before.Version, persisted?.Version);
        Assert.Equal(0L, await harness.WorkflowEventCountAsync(operationKey));
    }

    [Fact]
    public async Task EnabledEngineerAssignmentPersistsAndExactReplaySurvivesLaterDisablement()
    {
        await using var harness = await WorkflowHarness.CreateAsync();
        var engineerId = Guid.NewGuid();
        await harness.SeedStaffAccountAsync(
            engineerId,
            true,
            StaffRole.Engineer,
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
            "assign-eligible-engineer",
            "Assign enabled Engineer",
            lease.Token,
            engineerId,
            new(true, true, "accepted-readiness"));
        await using var staffContext = await harness.Factory.CreateDbContextAsync();
        var sut = new AssignCaseEngineer(
            harness.Store,
            new DefaultCaseWorkflowConfiguration(),
            harness.EngineerEligibility,
            new EfStaffAccountQueries(staffContext));

        var assigned = await sut.ExecuteAsync(request, default);
        await harness.SetStaffEnabledAsync(engineerId, false);
        var disabled = await harness.EngineerEligibility.GetAsync(engineerId, default);
        var replay = await sut.ExecuteAsync(request, default);

        Assert.False(disabled.IsEnabled);
        Assert.Equal(engineerId, assigned.AssignedEngineerId);
        Assert.Equal(engineerId, assigned.SignOffEngineerId);
        Assert.Equal(before.Version + 1, assigned.Version);
        Assert.Equal(assigned, replay);
        Assert.Equal(1L, await harness.WorkflowEventCountAsync(request.OperationKey));
    }

    [Fact]
    public async Task SignOffEngineerSelectionPersistsWithHistoryAndExactReplay()
    {
        await using var harness = await WorkflowHarness.CreateAsync();
        var signOffEngineerId = Guid.NewGuid();
        await harness.SeedStaffAccountAsync(
            signOffEngineerId,
            true,
            StaffRole.Engineer,
            isSignOffEngineer: true,
            isDefaultSignOffEngineer: true);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        var lease = await harness.Store.ClaimAsync(
            new(harness.CaseId, 0, actor, "claim-sign-off-selection"),
            default);
        var request = new SetCaseSignOffEngineerRequest(
            harness.CaseId,
            0,
            actor,
            "select-sign-off-engineer",
            "Select report signatory",
            lease.Token,
            signOffEngineerId);
        await using var staffContext = await harness.Factory.CreateDbContextAsync();
        var sut = new SetCaseSignOffEngineer(
            harness.Store,
            new EfStaffAccountQueries(staffContext));

        var selected = await sut.ExecuteAsync(request, default);
        var replay = await sut.ExecuteAsync(request, default);

        Assert.Equal(signOffEngineerId, selected.SignOffEngineerId);
        Assert.Equal(selected, replay);
        Assert.Equal(1L, await harness.WorkflowEventCountAsync(request.OperationKey));
        await using var historyContext = await harness.Factory.CreateDbContextAsync();
        var history = await historyContext.ActionHistory.SingleAsync(item =>
            item.AggregateType == "Case"
            && item.AggregateId == harness.CaseId.ToString("D")
            && item.EventKind == "case_sign_off_engineer_selected");
        Assert.Contains(signOffEngineerId.ToString("D"), history.AfterJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ArchivedCaseProjectionRetainsPersistedSignOffEngineer()
    {
        await using var harness = await WorkflowHarness.CreateAsync();
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

        var details = Assert.IsType<CaseDetails>(
            await harness.QueryStore.GetAsync(new(harness.CaseId, archivedBy), default));

        Assert.NotNull(details.Workflow.Archive);
        Assert.Equal(signOffEngineerId, details.Workflow.SignOffEngineerId);
        Assert.NotEqual(details.Workflow.AssignedEngineerId, details.Workflow.SignOffEngineerId);
    }

    [Fact]
    public async Task FabricatedOrAlreadyLinkedSentEvidenceCannotTransitionAnotherCase()
    {
        await using var harness = await WorkflowHarness.CreateAsync();
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var started = await new StartCaseWork(harness.Store, harness.EngineerEligibility).ExecuteAsync(
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
        var linked = await linkEvidence.ExecuteAsync(linkRequest, default);
        var replay = await linkEvidence.ExecuteAsync(linkRequest, default);

        Assert.Equal(linked.CaseId, replay.CaseId);
        Assert.Equal(linked.State, replay.State);
        Assert.Equal(linked.Version, replay.Version);
        Assert.Equal(
            linked.ReportSentEvidence?.EvidenceId,
            replay.ReportSentEvidence?.EvidenceId);
        Assert.Equal(retained.EvidenceId, linked.ReportSentEvidence?.EvidenceId);
        Assert.Equal(1L, await harness.WorkflowEventCountAsync("link-exclusive-evidence"));

        var secondStarted = await new StartCaseWork(harness.Store, harness.EngineerEligibility).ExecuteAsync(
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
        Assert.Equal(CaseLifecycleState.PostReport, firstPersisted?.State);
        Assert.Equal(linked.Version, firstPersisted?.Version);
        Assert.Equal(retained.EvidenceId, firstPersisted?.ReportSentEvidence?.EvidenceId);
    }

    [Fact]
    public async Task ReportEvidenceUnlinkRequiresReportPreparationAndPreservesRetainedEvidence()
    {
        await using var harness = await WorkflowHarness.CreateAsync();
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var started = await new StartCaseWork(harness.Store, harness.EngineerEligibility).ExecuteAsync(
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
        var linked = await new LinkReportEvidence(harness.Store).ExecuteAsync(
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

        var closed = await new CloseCase(harness.Store).ExecuteAsync(
            new(
                harness.CaseId,
                linked.Version,
                actor,
                "close-before-unlink",
                "Provider cancelled after the report",
                postReportLease.Token,
                CaseClosureOutcome.ProviderCancelled),
            default);
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
        var available = await harness.ReportSentEvidenceStore.ListUnlinkedAsync(100, default);

        Assert.Equal(CaseLifecycleState.ReportPreparation, unlinked.State);
        Assert.Null(unlinked.ReportSentEvidence);
        Assert.Equal(unlinked, replay);
        Assert.Contains(available, item => item.EvidenceId == retained.EvidenceId);
        Assert.Equal(1L, await harness.WorkflowEventCountAsync("unlink-report-evidence"));
        Assert.Equal(0L, await harness.WorkflowEventCountAsync("unlink-while-post-report"));
    }

    [Fact]
    public async Task HoldReleaseRestoresReportPreparationAndNotReadyChaseInterval()
    {
        await using var harness = await WorkflowHarness.CreateAsync();
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var startLease = await harness.Store.ClaimAsync(
            new(harness.CaseId, 0, actor, "claim-start"),
            default);
        var reportPreparation = await new StartCaseWork(harness.Store, harness.EngineerEligibility).ExecuteAsync(
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
        var reportHeld = await new PutCaseOnHold(harness.Store).ExecuteAsync(
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
        var chaseHeld = await new PutCaseOnHold(harness.Store).ExecuteAsync(
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
        await using var harness = await WorkflowHarness.CreateAsync();
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
            "Requested the outstanding vehicle images",
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
    }

    [Fact]
    public async Task ReopenToPostReportWithoutRetainedSentEvidenceFailsClosed()
    {
        await using var harness = await WorkflowHarness.CreateAsync();
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        var closeLease = await harness.Store.ClaimAsync(
            new(harness.SecondCaseId, 0, actor, "claim-close"),
            default);
        var closed = await new CloseCase(harness.Store).ExecuteAsync(
            new(
                harness.SecondCaseId,
                0,
                actor,
                "close-provider",
                "Provider cancelled before report delivery",
                closeLease.Token,
                CaseClosureOutcome.ProviderCancelled),
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
                CaseReopenDestination.PostReport),
            default));

        var persisted = await harness.Store.GetAsync(harness.SecondCaseId, default);
        Assert.Equal(CaseLifecycleState.ProviderCancelled, persisted?.State);
        Assert.Equal(closed.Version, persisted?.Version);
        Assert.Null(persisted?.ReportSentEvidence);
    }

    [Fact]
    public async Task EditLeaseClaimAndRenewExpireFiveMinutesFromServerTime()
    {
        await using var harness = await WorkflowHarness.CreateAsync();
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
        await using var harness = await WorkflowHarness.CreateAsync();
        var staffId = Guid.NewGuid();
        var actor = ActionActor.Staff(
            staffId,
            [StaffRole.Engineer, StaffRole.Administrator]);
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
                    [StaffRole.Administrator, StaffRole.Engineer])
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
        await using var harness = await WorkflowHarness.CreateAsync();
        var staffId = Guid.NewGuid();
        var actor = ActionActor.Staff(
            staffId,
            [StaffRole.Administrator, StaffRole.Engineer]);
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
                    Actor = ActionActor.Staff(staffId, [StaffRole.Administrator])
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
                        [StaffRole.Administrator, StaffRole.Engineer])
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
    public async Task LeaseReleaseAndExpiryDiscardReplayCredentialBeforeReplacement()
    {
        await using var harness = await WorkflowHarness.CreateAsync();
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
    public async Task AdministratorClearsTheExactStaffLeaseAndExactReplayReturnsItsOutcome()
    {
        await using var harness = await WorkflowHarness.CreateAsync();
        var holderId = Guid.NewGuid();
        var holder = ActionActor.Staff(holderId, [StaffRole.User]);
        var administrator = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        var lease = await harness.Store.ClaimAsync(
            new(harness.CaseId, 0, holder, "claim-for-admin-clear"),
            default);
        var request = new ClearCaseEditLeaseRequest(
            harness.CaseId,
            holderId,
            lease.Generation,
            administrator,
            "admin-clear",
            "User cannot close the editor");
        var command = new ClearCaseEditLease(harness.Store);

        var cleared = await command.ExecuteAsync(request, default);
        var replay = await command.ExecuteAsync(request, default);

        Assert.Equal(cleared, replay);
        Assert.Equal(lease.Generation, cleared.LeaseGeneration);
        Assert.Equal(0, cleared.CaseVersion);
        Assert.False(await harness.HasLeaseReplayMaterialAsync(harness.CaseId));
        Assert.Equal(1, await harness.LeaseOperationCountAsync(harness.CaseId, "admin-clear"));
        Assert.Equal(
            1,
            await harness.WorkflowEventTypeCountAsync(
                harness.CaseId,
                "case_edit_lease_administratively_cleared"));
        await Assert.ThrowsAsync<CaseEditLeaseExpiredException>(() =>
            harness.Store.HeartbeatAsync(new(harness.CaseId, holder, lease.Token), default));
    }

    [Fact]
    public async Task AdministrativeClearRejectsWrongTargetGenerationAndChangedReplayMaterial()
    {
        await using var harness = await WorkflowHarness.CreateAsync();
        var holderId = Guid.NewGuid();
        var holder = ActionActor.Staff(holderId, [StaffRole.User]);
        var administrator = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        var lease = await harness.Store.ClaimAsync(
            new(harness.CaseId, 0, holder, "claim-for-admin-refusal"),
            default);
        var command = new ClearCaseEditLease(harness.Store);
        var request = new ClearCaseEditLeaseRequest(
            harness.CaseId,
            holderId,
            lease.Generation,
            administrator,
            "admin-clear-refusal",
            "User cannot close the editor");

        await Assert.ThrowsAsync<CaseEditLeaseConflictException>(() =>
            command.ExecuteAsync(request with { ExpectedHolderUserId = Guid.NewGuid() }, default));
        await Assert.ThrowsAsync<CaseEditLeaseConflictException>(() =>
            command.ExecuteAsync(request with { ExpectedLeaseGeneration = lease.Generation + 1 }, default));
        Assert.True(await harness.HasLeaseReplayMaterialAsync(harness.CaseId));

        await command.ExecuteAsync(request, default);
        await Assert.ThrowsAsync<CaseOperationConflictException>(() =>
            command.ExecuteAsync(request with { Reason = "Changed reason" }, default));
    }

    [Fact]
    public async Task EachClaimAdvancesLeaseGenerationAndStaleClearCannotRemoveReplacement()
    {
        await using var harness = await WorkflowHarness.CreateAsync();
        var holderId = Guid.NewGuid();
        var holder = ActionActor.Staff(holderId, [StaffRole.User]);
        var administrator = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
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
        await Assert.ThrowsAsync<CaseEditLeaseConflictException>(() =>
            new ClearCaseEditLease(harness.Store).ExecuteAsync(
                new(
                    harness.CaseId,
                    holderId,
                    first.Generation,
                    administrator,
                    "stale-admin-clear",
                    "Stale screen"),
                default));

        var stillHeld = await harness.Store.HeartbeatAsync(
            new(harness.CaseId, holder, replacement.Token),
            default);
        Assert.Equal(replacement.Generation, stillHeld.Generation);
    }

    [Fact]
    public async Task ConcurrentRenewAndAdministrativeClearSerializeWithTheLeaseCleared()
    {
        await using var harness = await WorkflowHarness.CreateAsync();
        var holderId = Guid.NewGuid();
        var holder = ActionActor.Staff(holderId, [StaffRole.User]);
        var administrator = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        var lease = await harness.Store.ClaimAsync(
            new(harness.CaseId, 0, holder, "claim-for-concurrent-clear"),
            default);

        var renew = Record.ExceptionAsync(() => harness.Store.RenewAsync(
            new(
                harness.CaseId,
                0,
                holder,
                "renew-concurrent-with-clear",
                lease.Token),
            default));
        var clear = new ClearCaseEditLease(harness.Store).ExecuteAsync(
            new(
                harness.CaseId,
                holderId,
                lease.Generation,
                administrator,
                "clear-concurrent-with-renew",
                "User cannot close the editor"),
            default);

        var renewException = await renew;
        await clear;

        Assert.True(renewException is null or CaseEditLeaseExpiredException);
        await Assert.ThrowsAsync<CaseEditLeaseExpiredException>(() =>
            harness.Store.HeartbeatAsync(new(harness.CaseId, holder, lease.Token), default));
    }

    [Fact]
    public async Task ALiveLeaseProjectsItsHolderAndExpiryAndAnExpiredOneProjectsNoActiveEditor()
    {
        await using var harness = await WorkflowHarness.CreateAsync();
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var claimedAtUtc = harness.TimeProvider.GetUtcNow();
        var lease = await harness.Store.ClaimAsync(
            new(harness.CaseId, 0, actor, "claim-projection"),
            default);

        var whileHeld = await harness.QueryStore.GetAsync(new(harness.CaseId, actor), default);
        var activeLease = whileHeld?.ActiveEditLease;
        Assert.NotNull(activeLease);
        Assert.Equal(actor.SubjectId, activeLease.Holder);
        Assert.Equal(claimedAtUtc.AddMinutes(5), activeLease.ExpiresAtUtc);
        Assert.Equal(lease.ExpiresAtUtc, activeLease.ExpiresAtUtc);
        Assert.Equal("claim-projection", activeLease.OperationKey);

        // One second before expiry the case still reads as held; at expiry it reads as free,
        // with no sweeper having run and the retained columns untouched.
        harness.TimeProvider.Advance(TimeSpan.FromMinutes(5) - TimeSpan.FromSeconds(1));
        var justBeforeExpiry = await harness.QueryStore.GetAsync(new(harness.CaseId, actor), default);
        Assert.NotNull(justBeforeExpiry?.ActiveEditLease);

        harness.TimeProvider.Advance(TimeSpan.FromSeconds(1));
        var afterExpiry = await harness.QueryStore.GetAsync(new(harness.CaseId, actor), default);
        Assert.Null(afterExpiry?.ActiveEditLease);
    }

    /// <summary>
    /// CASE-024: a heartbeat extends the lease and records nothing. It is issued once a minute for
    /// as long as an editor is open, so a replay row per beat would grow a table nothing prunes,
    /// and rewriting the operation key would destroy the claim key the workspace reads back to
    /// recover edit mode.
    /// </summary>
    [Fact]
    public async Task HeartbeatExtendsTheLeaseWithoutWritingAReplayRowOrTouchingTheClaimKey()
    {
        await using var harness = await WorkflowHarness.CreateAsync();
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

    [Fact]
    public async Task HeartbeatIsRefusedForANonHolderAndForALeaseThatAlreadyLapsed()
    {
        await using var harness = await WorkflowHarness.CreateAsync();
        var holder = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var other = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var lease = await harness.Store.ClaimAsync(
            new(harness.CaseId, 0, holder, "claim-refusals"),
            default);

        await Assert.ThrowsAsync<CaseEditLeaseConflictException>(() =>
            harness.Store.HeartbeatAsync(new(harness.CaseId, other, lease.Token), default));

        harness.TimeProvider.Advance(TimeSpan.FromMinutes(5));
        await Assert.ThrowsAsync<CaseEditLeaseExpiredException>(() =>
            harness.Store.HeartbeatAsync(new(harness.CaseId, holder, lease.Token), default));
    }

    /// <summary>
    /// The first requirement, end to end: an editor that keeps beating is never timed out, however
    /// long the session runs; one that stops frees the case for someone else.
    /// </summary>
    [Fact]
    public async Task RepeatedHeartbeatsHoldTheLeaseIndefinitelyAndStoppingThemLetsItLapse()
    {
        await using var harness = await WorkflowHarness.CreateAsync();
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
        await using var harness = await WorkflowHarness.CreateAsync();
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var lease = await harness.Store.ClaimAsync(
            new(harness.CaseId, 0, actor, "claim-before-save"),
            default);
        await harness.Store.HeartbeatAsync(new(harness.CaseId, actor, lease.Token), default);

        await new PutCaseOnHold(harness.Store).ExecuteAsync(
            new(harness.CaseId, 0, actor, "hold-ends-edit-mode", "Waiting", lease.Token),
            default);

        var afterSave = await harness.QueryStore.GetAsync(new(harness.CaseId, actor), default);
        Assert.Null(afterSave?.ActiveEditLease);
        await Assert.ThrowsAsync<CaseEditLeaseExpiredException>(() =>
            harness.Store.HeartbeatAsync(new(harness.CaseId, actor, lease.Token), default));
    }

    /// <summary>
    /// KANMER-005, Automation holds and staff competes: the staff claim, a write presenting the
    /// holder's own token, renew, heartbeat, and release are all refused; the retained lease is
    /// untouched by every refusal; and the holder then heartbeats, saves, and finds its lease
    /// consumed by that save exactly as before.
    /// </summary>
    [Fact]
    public async Task AnAutomationHeldLeaseRefusesEveryStaffOperationAndTheHolderStillSaves()
    {
        await using var harness = await WorkflowHarness.CreateAsync();
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
        var projected = await harness.QueryStore.GetAsync(new(harness.CaseId, staff), default);
        Assert.Equal(ActorKind.Automation, projected?.ActiveEditLease?.HolderKind);
        Assert.Equal(automation.SubjectId, projected?.ActiveEditLease?.Holder);

        harness.TimeProvider.Advance(TimeSpan.FromMinutes(1));
        var beaten = await harness.Store.HeartbeatAsync(
            new(harness.CaseId, automation, lease.Token),
            default);
        Assert.Equal(harness.TimeProvider.GetUtcNow().AddMinutes(5), beaten.ExpiresAtUtc);

        await new PutCaseOnHold(harness.Store).ExecuteAsync(
            new(harness.CaseId, 0, automation, "automation-saves", "Waiting", lease.Token),
            default);

        var afterSave = await harness.ReadLeaseRowAsync(harness.CaseId);
        Assert.Equal(1, afterSave.Version);
        Assert.Null(afterSave.Holder);
        Assert.Null(afterSave.HolderKind);
        Assert.Null(afterSave.Token);
        // The save consumed the lease, so ending it afterwards is refused as having none.
        await Assert.ThrowsAsync<CaseEditLeaseExpiredException>(() =>
            harness.Store.ReleaseAsync(
                new(harness.CaseId, automation, "mcp:automation-ends-after-save", lease.Token),
                default));
    }

    /// <summary>
    /// KANMER-005, staff holds and Automation competes: the mirror of the case above, ending
    /// with the holder releasing without saving and the competitor then claiming the free lease.
    /// </summary>
    [Fact]
    public async Task AStaffHeldLeaseRefusesEveryAutomationOperationAndTheHolderStillReleases()
    {
        await using var harness = await WorkflowHarness.CreateAsync();
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
        var projected = await harness.QueryStore.GetAsync(new(harness.CaseId, automation), default);
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
    /// The identity gap KANMER-005 closes: a holder is kind and subject together, so an
    /// Automation Actor given the staff holder's own subject text, presenting the live token, is
    /// still a competitor — and its refused attempts leave the row byte-for-byte as it was.
    /// </summary>
    [Fact]
    public async Task TheSameSubjectTextUnderAnotherActorKindIsACompetitorEvenWithTheLiveToken()
    {
        await using var harness = await WorkflowHarness.CreateAsync();
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
            new PutCaseOnHold(harness.Store).ExecuteAsync(
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
        await using var harness = await WorkflowHarness.CreateAsync();
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
            new PutCaseOnHold(harness.Store).ExecuteAsync(
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

        var held = await new PutCaseOnHold(harness.Store).ExecuteAsync(
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

    [Fact]
    public async Task StaleVersionAndCompetingLeaseAreRejected()
    {
        await using var harness = await WorkflowHarness.CreateAsync();
        var firstActor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var secondActor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        _ = await harness.Store.ClaimAsync(new(harness.CaseId, 0, firstActor, "claim-1"), default);

        await Assert.ThrowsAsync<CaseEditLeaseConflictException>(() =>
            harness.Store.ClaimAsync(new(harness.CaseId, 0, secondActor, "claim-2"), default));

        var lease = await harness.Store.ClaimAsync(new(harness.SecondCaseId, 0, secondActor, "claim-3"), default);
        var hold = new PutCaseOnHold(harness.Store);
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
            harness.Store.ClaimAsync(new(harness.SecondCaseId, 0, secondActor, "claim-stale"), default));
    }

    [Fact]
    public async Task CreatedInErrorCannotUseTheGenericCloseCommand()
    {
        await using var harness = await WorkflowHarness.CreateAsync();
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
    public async Task WrongPrincipalReplacementIsAllocatedLinkedAndReplayedAtomically()
    {
        await using var harness = await WorkflowHarness.CreateAsync();
        var standaloneAuditEvidenceId =
            await harness.SeedStandaloneAuditEvidenceAsync(harness.CaseId);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        var missingPrincipalLease = await harness.Store.ClaimAsync(
            new(harness.SecondCaseId, 0, actor, "claim-missing-principal"),
            default);
        var create = new CreateLinkedReplacement(harness.ReplacementStore, new CommittedWorkPublisherDouble());

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
            CancellationToken.None);
        Assert.NotNull(originalDataBefore);
        Assert.Equal("Jane Workflow", originalDataBefore.Claimant.Name.Confirmed?.Value);
        var allocated = await create.ExecuteAsync(request, default);
        var replay = await create.ExecuteAsync(request, default);

        Assert.False(allocated.IsDuplicate);
        Assert.True(replay.IsDuplicate);
        Assert.Equal(allocated.Identity, replay.Identity);
        Assert.Equal("QDOS", allocated.Identity.PrincipalCode);
        Assert.StartsWith("QDOS26", allocated.Identity.Reference);
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
            CancellationToken.None);
        var replacementData = await harness.DataStore.GetAsync(
            allocated.Identity.CaseId,
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
    }

    [Theory]
    [InlineData(CaseTaskState.Completed)]
    [InlineData(CaseTaskState.Cancelled)]
    public async Task WrongPrincipalReplacementRollsBackForOpenTasksThenSucceedsAndReplays(
        CaseTaskState resolvedState)
    {
        await using var harness = await WorkflowHarness.CreateAsync();
        var taskId = Guid.NewGuid();
        await harness.SeedCaseTaskAsync(harness.CaseId, taskId, CaseTaskState.Open);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        var lease = await harness.Store.ClaimAsync(
            new(harness.CaseId, 0, actor, $"claim-task-gated-replacement-{resolvedState}"),
            default);
        var request = new CreateLinkedReplacementRequest(
            harness.CaseId,
            0,
            actor,
            $"task-gated-replacement-{resolvedState}",
            "Correct the immutable principal only after resolving open work",
            lease.Token,
            "QDOS");
        var create = new CreateLinkedReplacement(harness.ReplacementStore, new CommittedWorkPublisherDouble());
        var initialCaseCount = await harness.CountCasesAsync();
        var initialQdosReferenceCount = await harness.CountQdosReferencesAsync();

        var denied = await Assert.ThrowsAsync<InvalidOperationException>(
            () => create.ExecuteAsync(request, default));

        Assert.Contains("open case task", denied.Message, StringComparison.Ordinal);
        Assert.Equal(initialCaseCount, await harness.CountCasesAsync());
        Assert.Equal(initialQdosReferenceCount, await harness.CountQdosReferencesAsync());
        var unchanged = await harness.Store.GetAsync(harness.CaseId, default);
        Assert.Equal(CaseLifecycleState.Review, unchanged?.State);
        Assert.Null(unchanged?.ReplacementCaseId);

        await harness.SetCaseTaskStateAsync(taskId, resolvedState);
        var allocated = await create.ExecuteAsync(request, default);
        await harness.SetCaseTaskStateAsync(taskId, CaseTaskState.Open);
        var replay = await create.ExecuteAsync(request, default);
        await harness.SetCaseTaskStateAsync(taskId, resolvedState);

        Assert.False(allocated.IsDuplicate);
        Assert.True(replay.IsDuplicate);
        Assert.Equal(allocated.Identity, replay.Identity);
        Assert.Equal(initialCaseCount + 1, await harness.CountCasesAsync());
        Assert.Equal(initialQdosReferenceCount + 1, await harness.CountQdosReferencesAsync());
    }

    [Fact]
    public async Task ConcurrentTaskCreationAndWrongPrincipalReplacementPreserveTheTerminalTaskInvariant()
    {
        await using var harness = await WorkflowHarness.CreateAsync();
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var lease = await harness.Store.ClaimAsync(
            new(harness.CaseId, 0, actor, "claim-concurrent-task-replacement"),
            default);
        var replacement = new CreateLinkedReplacement(harness.ReplacementStore, new CommittedWorkPublisherDouble()).ExecuteAsync(
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
                "Retained work must not survive terminalization",
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
        private static readonly DateTimeOffset StartUtc =
            new(2026, 7, 29, 9, 0, 0, TimeSpan.Zero);
        private readonly LocalDbTestDatabase database;
        private readonly PooledDbContextFactory<PegasusDbContext> factory;

        public PooledDbContextFactory<PegasusDbContext> Factory => factory;

        private WorkflowHarness(
            LocalDbTestDatabase database,
            PooledDbContextFactory<PegasusDbContext> factory,
            Guid caseId,
            Guid secondCaseId,
            Guid notReadyCaseId,
            MutableTimeProvider timeProvider)
        {
            this.database = database;
            this.factory = factory;
            CaseId = caseId;
            SecondCaseId = secondCaseId;
            NotReadyCaseId = notReadyCaseId;
            TimeProvider = timeProvider;
            Store = new EfCaseWorkflowStore(factory, timeProvider);
            QueryStore = new EfCaseQueryStore(factory, timeProvider);
            EngineerEligibility = new EfCaseEngineerEligibility(factory);
            ReportSentEvidenceStore = new EfCaseReportSentEvidenceStore(
                factory,
                new EfApprovedMailboxStore(factory, timeProvider));
            ReplacementStore = new EfLinkedCaseReplacementStore(factory, timeProvider);
            TaskStore = new EfCaseTaskStore(factory, timeProvider);
            DataStore = new EfCaseDataStore(factory, timeProvider);
        }

        public Guid CaseId { get; }
        public Guid SecondCaseId { get; }
        public Guid NotReadyCaseId { get; }
        public MutableTimeProvider TimeProvider { get; }
        public string ConnectionString => database.ConnectionString;
        public EfCaseWorkflowStore Store { get; }
        public EfCaseQueryStore QueryStore { get; }
        public EfCaseEngineerEligibility EngineerEligibility { get; }
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
                $"INSERT INTO CaseTasks (Id, CaseId, Description, State, Version, ConcurrencyToken) VALUES ('{taskId:D}', '{caseId:D}', 'Retained replacement work', '{state}', 0, '{Guid.NewGuid():D}')");
        }

        public Task SetCaseTaskStateAsync(Guid taskId, CaseTaskState state) =>
            database.ExecuteAsync(
                $"UPDATE CaseTasks SET State = '{state}', Version = Version + 1, ConcurrencyToken = '{Guid.NewGuid():D}' WHERE Id = '{taskId:D}'");

        public Task<long> CountCasesAsync() =>
            database.ScalarAsync<long>("SELECT COUNT_BIG(*) FROM Cases");

        public Task<long> CountQdosReferencesAsync() =>
            database.ScalarAsync<long>(
                "SELECT COUNT_BIG(*) FROM Cases WHERE Reference LIKE 'QDOS26%'");

        public static async Task<WorkflowHarness> CreateAsync()
        {
            var database = await LocalDbTestDatabase.CreateAsync();
            try
            {
                var options = new DbContextOptionsBuilder<PegasusDbContext>()
                    .UseSqlServer(database.ConnectionString)
                    .Options;
                var factory = new PooledDbContextFactory<PegasusDbContext>(options);
                await using var context = await factory.CreateDbContextAsync();

                var timeProvider = new MutableTimeProvider(StartUtc);
                var organizationId = Guid.NewGuid();
                var tstLineageId = Guid.NewGuid();
                var qdos = await SeededPrincipals.QdosAsync(context);
                var qdosLineageId = qdos.SequenceLineageId;
                var principalId = Guid.NewGuid();
                var engineerId = Guid.NewGuid();
                var caseId = Guid.NewGuid();
                var secondCaseId = Guid.NewGuid();
                var notReadyCaseId = Guid.NewGuid();
                var caseReceiptId = Guid.NewGuid();
                var secondCaseReceiptId = Guid.NewGuid();
                var notReadyReceiptId = Guid.NewGuid();

                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE ApprovedMailboxes SET AllowSentEvidence = {true}, Version = {2} WHERE Address = {"instructions@collisionengineers.co.uk"}");
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO Organizations (Id, Name, Version) VALUES ({organizationId}, {"Workflow test organization"}, {0L})");
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO PrincipalSequenceLineages (Id, CreatedAtUtc) VALUES ({tstLineageId}, {StartUtc})");
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO Principals (Id, OrganizationId, Code, SequenceLineageId, IsActive, Version) VALUES ({principalId}, {organizationId}, {"TST"}, {tstLineageId}, {true}, {0L})");
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
                await InsertReceiptAsync(context, caseReceiptId, 1);
                await InsertReceiptAsync(context, secondCaseReceiptId, 2);
                await InsertReceiptAsync(context, notReadyReceiptId, 3);
                await InsertCaseAsync(
                    context,
                    caseId,
                    principalId,
                    tstLineageId,
                    caseReceiptId,
                    "TST26001",
                    1);
                await InsertCaseDataSnapshotAsync(context, caseId, caseReceiptId);
                await InsertCaseAsync(
                    context,
                    secondCaseId,
                    principalId,
                    tstLineageId,
                    secondCaseReceiptId,
                    "TST26002",
                    2);
                await InsertCaseAsync(
                    context,
                    notReadyCaseId,
                    principalId,
                    tstLineageId,
                    notReadyReceiptId,
                    "TST26003",
                    3);
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO CaseWorkflows (CaseId, State, AssignedEngineerId, Version, ConcurrencyToken) VALUES ({caseId}, {nameof(CaseLifecycleState.Review)}, {engineerId}, {0L}, {Guid.NewGuid()})");
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO CaseWorkflows (CaseId, State, AssignedEngineerId, Version, ConcurrencyToken) VALUES ({secondCaseId}, {nameof(CaseLifecycleState.Review)}, {engineerId}, {0L}, {Guid.NewGuid()})");
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO CaseWorkflows (CaseId, State, Version, ConcurrencyToken) VALUES ({notReadyCaseId}, {nameof(CaseLifecycleState.NotReady)}, {0L}, {Guid.NewGuid()})");
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO CaseDueWork (CaseId, MissingMaterialReason, State, NextChaseAtUtc, NextChaseAtUtcTicks, Version, ConcurrencyToken) VALUES ({notReadyCaseId}, {"Waiting for images"}, {nameof(CaseDueWorkState.Scheduled)}, {timeProvider.GetUtcNow().AddDays(3)}, {timeProvider.GetUtcNow().AddDays(3).UtcDateTime.Ticks}, {0L}, {Guid.NewGuid()})");
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO ApprovedInboxPollStates (ApprovedMailboxId, MailboxAddress, ScopeFingerprint, ActivatedAtUtc, DueAtUtc) SELECT Id, Address, REPLICATE('0', 64), {StartUtc}, {timeProvider.GetUtcNow()} FROM ApprovedMailboxes WHERE Address = {"instructions@collisionengineers.co.uk"}");
                return new(database, factory, caseId, secondCaseId, notReadyCaseId, timeProvider);
            }
            catch
            {
                await database.DisposeAsync();
                throw;
            }
        }

        private static Task<int> InsertCaseAsync(
            PegasusDbContext context,
            Guid caseId,
            Guid principalId,
            Guid sequenceLineageId,
            Guid receiptId,
            string reference,
            int sequence) =>
            context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO Cases (Id, PrincipalId, SequenceLineageId, Year, Sequence, Reference, Type, InitialState, CustodyState, OriginIntakeReceiptId, InstructionComplete, ImagesComplete, InstructionConfirmedByStaff, ImagesConfirmedByStaff, CreatedAtUtc, Version, ConcurrencyToken) VALUES ({caseId}, {principalId}, {sequenceLineageId}, {2026}, {sequence}, {reference}, {"inspection"}, {"review"}, {"pending"}, {receiptId}, {true}, {true}, {true}, {true}, {StartUtc}, {0L}, {Guid.NewGuid()})");

        private static async Task InsertCaseDataSnapshotAsync(
            PegasusDbContext context,
            Guid caseId,
            Guid receiptId)
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO CaseDataSnapshots (CaseId, OriginIntakeReceiptId, OriginSourceChannel, OriginExternalReceiptToken, OriginSourceHash, OriginReceivedAtUtc, SourceReaderKey, SourceReaderVersion, ExtractionPolicyKey, ExtractionPolicyVersion, CompletenessPolicyKey, CompletenessPolicyVersion, CompletenessPolicySatisfied, AcceptedAtUtc) VALUES ({caseId}, {receiptId}, {"manual_upload"}, {"workflow-1"}, {1.ToString("X64", System.Globalization.CultureInfo.InvariantCulture)}, {StartUtc}, {"workflow-test-reader"}, {"1"}, {"workflow-fixture"}, {1}, {"case-workflow"}, {1}, {true}, {StartUtc})");
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO CaseDataFields (CaseId, FieldName, ValueKind, ValueType, Value, SourceKind, SourceIdentity, SourceLabel, PolicyKey, PolicyVersion, ConfirmedByActor, ConfirmedAtUtc) VALUES ({caseId}, {"claimant_name"}, {"confirmed"}, {"text"}, {"Jane Workflow"}, {"intake_evidence"}, {receiptId.ToString("D")}, {"workflow fixture evidence"}, {"workflow-fixture"}, {1}, {"workflow-staff"}, {StartUtc})");
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

        public async Task<long> WorkflowEventCountAsync(string operationKey)
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context.Database.SqlQuery<long>(
                    $"SELECT COUNT_BIG(*) AS [Value] FROM [CaseWorkflowEvents] WHERE [OperationKey] = {operationKey}")
                .SingleAsync();
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

        public async ValueTask DisposeAsync()
        {
            await database.DisposeAsync();
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
