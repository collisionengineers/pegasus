using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Cases;
using Pegasus.Core.Custody;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Tasks;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class CaseTaskArchivePersistenceTests
{
    [Fact]
    public async Task TaskMutationsFailClosedWithoutTheCurrentActorsActiveLease()
    {
        await using var harness = await Harness.CreateAsync();
        var taskId = Guid.NewGuid();
        var lease = await harness.AcquireLease.ExecuteAsync(
            new(harness.TaskCaseId, 0, harness.UserActor, "claim-task-denials"),
            default);

        await Assert.ThrowsAsync<ArgumentException>(() => harness.CreateTask.ExecuteAsync(
            CreateRequest(harness, taskId, 0, harness.UserActor, "task-missing-lease", ""),
            default));
        await Assert.ThrowsAsync<CaseEditLeaseConflictException>(() => harness.CreateTask.ExecuteAsync(
            CreateRequest(harness, taskId, 0, harness.UserActor, "task-wrong-token", "not-the-token"),
            default));
        await Assert.ThrowsAsync<CaseEditLeaseConflictException>(() => harness.CreateTask.ExecuteAsync(
            CreateRequest(harness, taskId, 0, harness.AdministratorActor, "task-wrong-holder", lease.Token),
            default));

        harness.TimeProvider.Advance(TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(1)));
        await Assert.ThrowsAsync<CaseEditLeaseExpiredException>(() => harness.CreateTask.ExecuteAsync(
            CreateRequest(harness, taskId, 0, harness.UserActor, "task-expired-lease", lease.Token),
            default));

        Assert.Equal(0L, await harness.CountAsync("CaseTasks"));
        Assert.Equal(0L, await harness.CountHistoryAsync("case_task"));
    }
    public static IEnumerable<object[]> TerminalTaskMutations
    {
        get
        {
            CaseLifecycleState[] states =
            [
                CaseLifecycleState.PostReportComplete,
                CaseLifecycleState.ProviderCancelled,
                CaseLifecycleState.CollisionEngineersRejected,
                CaseLifecycleState.CreatedInError
            ];
            string[] mutations = ["create", "assign", "complete", "cancel"];
            foreach (var state in states)
            {
                foreach (var mutation in mutations)
                {
                    yield return [state, mutation];
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(TerminalTaskMutations))]
    public async Task EveryTaskMutationRequiresAReasonedReopenFromEveryTerminalState(
        CaseLifecycleState terminalState,
        string mutation)
    {
        await using var harness = await Harness.CreateAsync();
        var existingTaskId = Guid.NewGuid();
        await harness.SeedOpenTaskAsync(existingTaskId);
        await harness.SetWorkflowStateAsync(harness.TaskCaseId, terminalState);
        var lease = await harness.AcquireLease.ExecuteAsync(
            new(harness.TaskCaseId, 0, harness.AdministratorActor, $"claim-{terminalState}-{mutation}"),
            default);

        Task<CaseTaskRecord> execute = mutation switch
        {
            "create" => harness.CreateTask.ExecuteAsync(
                CreateRequest(
                    harness,
                    Guid.NewGuid(),
                    0,
                    harness.AdministratorActor,
                    $"terminal-{terminalState}-{mutation}",
                    lease.Token),
                default),
            "assign" => harness.AssignTask.ExecuteAsync(
                new(
                    harness.TaskCaseId,
                    existingTaskId,
                    0,
                    0,
                    harness.AdministratorActor,
                    $"terminal-{terminalState}-{mutation}",
                    "A closed case cannot change task assignment",
                    lease.Token,
                    harness.EngineerId),
                default),
            "complete" => harness.CompleteTask.ExecuteAsync(
                new(
                    harness.TaskCaseId,
                    existingTaskId,
                    0,
                    0,
                    harness.AdministratorActor,
                    $"terminal-{terminalState}-{mutation}",
                    "A closed case cannot complete a task",
                    lease.Token),
                default),
            "cancel" => harness.CancelTask.ExecuteAsync(
                new(
                    harness.TaskCaseId,
                    existingTaskId,
                    0,
                    0,
                    harness.AdministratorActor,
                    $"terminal-{terminalState}-{mutation}",
                    "A closed case cannot cancel a task",
                    lease.Token),
                default),
            _ => throw new ArgumentOutOfRangeException(nameof(mutation))
        };

        var error = await Assert.ThrowsAsync<CaseTerminalMutationException>(() => execute);
        Assert.Equal(harness.TaskCaseId, error.CaseId);
        Assert.Contains("reopen", error.Message, StringComparison.OrdinalIgnoreCase);
        var retained = Assert.Single(
            await harness.TaskQueries.ListAsync(harness.TaskCaseId, default));
        Assert.Equal(existingTaskId, retained.Id);
        Assert.Equal(CaseTaskState.Open, retained.State);
        Assert.Equal(0, retained.Version);
        Assert.Equal(0L, await harness.CountHistoryAsync("case_task"));
    }


    [Theory]
    [InlineData(CaseClosureOutcome.PostReportComplete)]
    [InlineData(CaseClosureOutcome.ProviderCancelled)]
    [InlineData(CaseClosureOutcome.CollisionEngineersRejected)]
    [InlineData(CaseClosureOutcome.CreatedInError)]
    public async Task EveryTerminalCloseOutcomeRejectsOpenTasks(
        CaseClosureOutcome outcome)
    {
        await using var harness = await Harness.CreateAsync();
        await harness.SeedOpenTaskAsync(Guid.NewGuid());
        var lease = await harness.AcquireLease.ExecuteAsync(
            new(harness.TaskCaseId, 0, harness.UserActor, $"claim-close-{outcome}"),
            default);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.WorkflowStore.CloseAsync(
                new(
                    harness.TaskCaseId,
                    0,
                    harness.UserActor,
                    $"close-{outcome}",
                    "Exercise the open task closure gate",
                    lease.Token,
                    outcome),
                default));

        Assert.Contains("open case task", error.Message, StringComparison.Ordinal);
        var retained = await harness.WorkflowStore.GetAsync(harness.TaskCaseId, default);
        Assert.Equal(CaseLifecycleState.Review, retained?.State);
        Assert.Equal(0, retained?.Version);
    }

    [Fact]
    public async Task ArchiveRejectsALegacyTerminalCaseWithAnOpenTask()
    {
        await using var harness = await Harness.CreateAsync();
        await harness.SeedOpenTaskAsync(Guid.NewGuid());
        await harness.SetWorkflowStateAsync(
            harness.TaskCaseId,
            CaseLifecycleState.ProviderCancelled);
        await harness.SetCustodyStateAsync(harness.TaskCaseId, "confirmed");
        var lease = await harness.AcquireLease.ExecuteAsync(
            new(harness.TaskCaseId, 0, harness.UserActor, "claim-legacy-terminal-archive"),
            default);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.ArchiveCase.ExecuteAsync(
                new(
                    harness.TaskCaseId,
                    0,
                    harness.UserActor,
                    "archive-legacy-terminal-open-task",
                    "Exercise the archive open task gate",
                    lease.Token),
                default));

        Assert.Contains("open case task", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ArchiveRequiresConfirmedAuditCustodyAndCompletedAuditCustodyWork()
    {
        await using var harness = await Harness.CreateAsync();
        await harness.SetCaseTypeAsync(harness.TaskCaseId, "audit");
        var request = await PrepareArchiveRequestAsync(harness, "audit-custody");
        await harness.SetAuditCustodyAsync(harness.TaskCaseId, confirmed: false);
        var auditCustodyWorkId = await harness.AddExternalWorkAsync(
            harness.TaskCaseId,
            "failed",
            ExternalWorkKinds.CreateAuditReferenceCustody);

        var missingConfirmation = await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.ArchiveCase.ExecuteAsync(request, default));
        Assert.Contains("custody is confirmed", missingConfirmation.Message, StringComparison.Ordinal);

        await harness.SetAuditCustodyAsync(harness.TaskCaseId, confirmed: true);
        var failedRequiredWork = await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.ArchiveCase.ExecuteAsync(request, default));
        Assert.Contains("required durable work", failedRequiredWork.Message, StringComparison.Ordinal);

        await harness.SetExternalWorkStateAsync(auditCustodyWorkId, "completed");
        _ = await harness.AddExternalWorkAsync(
            harness.TaskCaseId,
            "failed",
            ExternalWorkKinds.VehicleLookup);
        var archived = await harness.ArchiveCase.ExecuteAsync(request, default);
        Assert.NotNull(archived.Archive);
    }

    [Fact]
    public async Task ArchiveFailsClosedForAnUnknownExternalWorkKindEvenWhenCompleted()
    {
        await using var harness = await Harness.CreateAsync();
        var request = await PrepareArchiveRequestAsync(harness, "unknown-work");
        var unknownWorkId = await harness.AddExternalWorkAsync(
            harness.TaskCaseId,
            "pending",
            "unrecognized_archive_kind");
        await harness.SetExternalWorkStateAsync(unknownWorkId, "completed");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.ArchiveCase.ExecuteAsync(request, default));

        Assert.Contains("unrecognized work", error.Message, StringComparison.Ordinal);
        var workflow = await harness.WorkflowStore.GetAsync(harness.TaskCaseId, default);
        Assert.Null(workflow?.Archive);
    }


    [Fact]
    public async Task ConcurrentTaskCreationAndClosureCannotLeaveOpenWorkOnATerminalCase()
    {
        await using var harness = await Harness.CreateAsync();
        var lease = await harness.AcquireLease.ExecuteAsync(
            new(harness.TaskCaseId, 0, harness.UserActor, "claim-create-close-race"),
            default);
        var create = harness.CreateTask.ExecuteAsync(
            CreateRequest(
                harness,
                Guid.NewGuid(),
                0,
                harness.UserActor,
                "race-task-create",
                lease.Token),
            default);
        var close = harness.WorkflowStore.CloseAsync(
            new(
                harness.TaskCaseId,
                0,
                harness.UserActor,
                "race-case-close",
                "Exercise task creation and closure serialization",
                lease.Token,
                CaseClosureOutcome.ProviderCancelled),
            default);

        await Assert.ThrowsAnyAsync<Exception>(() => Task.WhenAll(create, close));
        Assert.NotEqual(create.IsCompletedSuccessfully, close.IsCompletedSuccessfully);

        var workflow = await harness.WorkflowStore.GetAsync(
            harness.TaskCaseId,
            default);
        var current = Assert.IsType<CaseWorkflowRecord>(workflow);
        var tasks = await harness.TaskQueries.ListAsync(
            harness.TaskCaseId,
            default);
        if (CaseLifecycleRules.IsTerminal(current.State))
        {
            Assert.DoesNotContain(tasks, item => item.State == CaseTaskState.Open);
        }
        else
        {
            Assert.Equal(CaseLifecycleState.Review, current.State);
            Assert.Single(tasks, item => item.State == CaseTaskState.Open);
        }
    }

    [Fact]
    public async Task TaskTransitionsAreVersionedReplaySafePermanentAndArchivedCasesStayReadOnly()
    {
        await using var harness = await Harness.CreateAsync();
        var firstTaskId = Guid.NewGuid();
        var secondTaskId = Guid.NewGuid();

        var createLease = await harness.AcquireLease.ExecuteAsync(
            new(harness.TaskCaseId, 0, harness.UserActor, "claim-task-create"),
            default);
        var createRequest = CreateRequest(
            harness,
            firstTaskId,
            0,
            harness.UserActor,
            "task-create",
            createLease.Token,
            harness.EngineerId);
        var created = await harness.CreateTask.ExecuteAsync(createRequest, default);
        var immediateReplay = await harness.CreateTask.ExecuteAsync(createRequest, default);

        Assert.Equal(created, immediateReplay);
        Assert.Equal(CaseTaskState.Open, created.State);
        Assert.Equal(harness.EngineerId, created.AssigneeId);
        Assert.Equal(0, created.Version);
        Assert.Equal(1, created.CaseVersion);
        await Assert.ThrowsAsync<CaseOperationConflictException>(() => harness.CreateTask.ExecuteAsync(
            createRequest with { Description = "A different task payload" },
            default));

        var assignLease = await harness.AcquireLease.ExecuteAsync(
            new(harness.TaskCaseId, created.CaseVersion, harness.UserActor, "claim-task-reassign"),
            default);
        await Assert.ThrowsAsync<CaseVersionConflictException>(() => harness.AssignTask.ExecuteAsync(
            new(
                harness.TaskCaseId,
                firstTaskId,
                0,
                created.Version,
                harness.UserActor,
                "task-stale-case",
                "Exercise the case concurrency gate",
                assignLease.Token,
                harness.AdministratorId),
            default));
        var reassigned = await harness.AssignTask.ExecuteAsync(
            new(
                harness.TaskCaseId,
                firstTaskId,
                created.CaseVersion,
                created.Version,
                harness.UserActor,
                "task-reassign",
                "Move the task to another staff member",
                assignLease.Token,
                harness.AdministratorId),
            default);
        Assert.Equal(harness.AdministratorId, reassigned.AssigneeId);
        Assert.Equal(1, reassigned.Version);

        var unassignLease = await harness.AcquireLease.ExecuteAsync(
            new(harness.TaskCaseId, reassigned.CaseVersion, harness.AdministratorActor, "claim-task-unassign"),
            default);
        var unassigned = await harness.AssignTask.ExecuteAsync(
            new(
                harness.TaskCaseId,
                firstTaskId,
                reassigned.CaseVersion,
                reassigned.Version,
                harness.AdministratorActor,
                "task-unassign",
                "Return the task to the shared queue",
                unassignLease.Token,
                null),
            default);
        Assert.Null(unassigned.AssigneeId);
        Assert.Equal(2, unassigned.Version);

        var completeLease = await harness.AcquireLease.ExecuteAsync(
            new(harness.TaskCaseId, unassigned.CaseVersion, harness.EngineerActor, "claim-task-complete"),
            default);
        var taskConflict = await Assert.ThrowsAsync<CaseTaskVersionConflictException>(
            () => harness.CompleteTask.ExecuteAsync(
                new(
                    harness.TaskCaseId,
                    firstTaskId,
                    unassigned.CaseVersion,
                    unassigned.Version - 1,
                    harness.EngineerActor,
                    "task-stale-task",
                    "Exercise the task concurrency gate",
                    completeLease.Token),
                default));
        Assert.Equal(unassigned.Version, taskConflict.ActualVersion);
        var completed = await harness.CompleteTask.ExecuteAsync(
            new(
                harness.TaskCaseId,
                firstTaskId,
                unassigned.CaseVersion,
                unassigned.Version,
                harness.EngineerActor,
                "task-complete",
                "The work has been completed",
                completeLease.Token),
            default);
        Assert.Equal(CaseTaskState.Completed, completed.State);
        Assert.Equal(3, completed.Version);

        var secondCreateLease = await harness.AcquireLease.ExecuteAsync(
            new(harness.TaskCaseId, completed.CaseVersion, harness.UserActor, "claim-second-task-create"),
            default);
        var second = await harness.CreateTask.ExecuteAsync(
            CreateRequest(
                harness,
                secondTaskId,
                completed.CaseVersion,
                harness.UserActor,
                "second-task-create",
                secondCreateLease.Token),
            default);
        var cancelLease = await harness.AcquireLease.ExecuteAsync(
            new(harness.TaskCaseId, second.CaseVersion, harness.UserActor, "claim-second-task-cancel"),
            default);
        var cancelled = await harness.CancelTask.ExecuteAsync(
            new(
                harness.TaskCaseId,
                secondTaskId,
                second.CaseVersion,
                second.Version,
                harness.UserActor,
                "second-task-cancel",
                "The work is no longer required",
                cancelLease.Token),
            default);
        Assert.Equal(CaseTaskState.Cancelled, cancelled.State);

        var closeLease = await harness.AcquireLease.ExecuteAsync(
            new(harness.TaskCaseId, cancelled.CaseVersion, harness.UserActor, "claim-case-close"),
            default);
        var closeRequest = new CloseCaseRequest(
            harness.TaskCaseId,
            cancelled.CaseVersion,
            harness.UserActor,
            "case-close-before-archive",
            "The provider cancelled this case",
            closeLease.Token,
            CaseClosureOutcome.ProviderCancelled);
        var closed = await harness.CloseCase.ExecuteAsync(closeRequest, default);
        var closeReplay = await harness.CloseCase.ExecuteAsync(closeRequest, default);
        Assert.Equal(closed, closeReplay);
        var archiveLease = await harness.AcquireLease.ExecuteAsync(
            new(harness.TaskCaseId, closed.Version, harness.UserActor, "claim-case-archive"),
            default);
        var archiveRequest = new ArchiveCaseRequest(
            harness.TaskCaseId,
            closed.Version,
            harness.UserActor,
            "case-archive",
            "Retain the completed file as read-only",
            archiveLease.Token);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.ArchiveCase.ExecuteAsync(archiveRequest, default));
        await harness.SetCustodyStateAsync(harness.TaskCaseId, "confirmed");
        var pendingWorkId = await harness.AddExternalWorkAsync(
            harness.TaskCaseId,
            "pending");
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.ArchiveCase.ExecuteAsync(archiveRequest, default));
        await harness.SetExternalWorkStateAsync(pendingWorkId, "completed");
        var archived = await harness.ArchiveCase.ExecuteAsync(archiveRequest, default);
        var archiveReplay = await harness.ArchiveCase.ExecuteAsync(archiveRequest, default);

        Assert.Equal(archived.CaseId, archiveReplay.CaseId);
        Assert.Equal(archived.Identity.CaseId, archiveReplay.Identity.CaseId);
        Assert.Equal(archived.Identity.PrincipalCode, archiveReplay.Identity.PrincipalCode);
        Assert.Equal(archived.Identity.Year, archiveReplay.Identity.Year);
        Assert.Equal(archived.Identity.Sequence, archiveReplay.Identity.Sequence);
        Assert.Equal(archived.Identity.Reference, archiveReplay.Identity.Reference);
        Assert.Equal(archived.Identity.AuditReference, archiveReplay.Identity.AuditReference);
        Assert.Equal(archived.State, archiveReplay.State);
        Assert.Equal(archived.AssignedEngineerId, archiveReplay.AssignedEngineerId);
        Assert.Equal(archived.ReportApproval, archiveReplay.ReportApproval);
        Assert.Equal(archived.ReportSentEvidence, archiveReplay.ReportSentEvidence);
        Assert.Equal(archived.DueWork, archiveReplay.DueWork);
        Assert.Equal(archived.ClosureOutcome, archiveReplay.ClosureOutcome);
        Assert.Equal(archived.OriginalCaseId, archiveReplay.OriginalCaseId);
        Assert.Equal(archived.ReplacementCaseId, archiveReplay.ReplacementCaseId);
        Assert.Equal(archived.Version, archiveReplay.Version);
        var originalArchive = Assert.IsType<CaseArchive>(archived.Archive);
        var replayedArchive = Assert.IsType<CaseArchive>(archiveReplay.Archive);
        Assert.Equal(originalArchive.ArchivedAtUtc, replayedArchive.ArchivedAtUtc);
        Assert.Equal(originalArchive.Reason, replayedArchive.Reason);
        Assert.Equal(originalArchive.ArchivedBy.Kind, replayedArchive.ArchivedBy.Kind);
        Assert.Equal(
            originalArchive.ArchivedBy.SubjectId,
            replayedArchive.ArchivedBy.SubjectId);
        Assert.Equal(
            originalArchive.ArchivedBy.Roles.OrderBy(role => role).ToArray(),
            replayedArchive.ArchivedBy.Roles.OrderBy(role => role).ToArray());
        Assert.NotNull(archived.Archive);
        Assert.Equal("Retain the completed file as read-only", archived.Archive.Reason);
        Assert.Equal(CaseLifecycleState.ProviderCancelled, archived.State);
        await Assert.ThrowsAsync<CaseArchivedException>(() => harness.AcquireLease.ExecuteAsync(
            new(harness.TaskCaseId, archived.Version, harness.AdministratorActor, "claim-archived-case"),
            default));
        await Assert.ThrowsAsync<CaseArchivedException>(() => harness.CreateTask.ExecuteAsync(
            CreateRequest(
                harness,
                Guid.NewGuid(),
                archived.Version,
                harness.AdministratorActor,
                "task-after-archive",
                "unavailable-after-archive"),
            default));

        var lateCreateReplay = await harness.CreateTask.ExecuteAsync(createRequest, default);
        Assert.Equal(created, lateCreateReplay);
        var lateAssignReplay = await harness.AssignTask.ExecuteAsync(
            new(
                harness.TaskCaseId,
                firstTaskId,
                created.CaseVersion,
                created.Version,
                harness.UserActor,
                "task-reassign",
                "Move the task to another staff member",
                assignLease.Token,
                harness.AdministratorId),
            default);
        var lateCompleteReplay = await harness.CompleteTask.ExecuteAsync(
            new(
                harness.TaskCaseId,
                firstTaskId,
                unassigned.CaseVersion,
                unassigned.Version,
                harness.EngineerActor,
                "task-complete",
                "The work has been completed",
                completeLease.Token),
            default);
        var lateCancelReplay = await harness.CancelTask.ExecuteAsync(
            new(
                harness.TaskCaseId,
                secondTaskId,
                second.CaseVersion,
                second.Version,
                harness.UserActor,
                "second-task-cancel",
                "The work is no longer required",
                cancelLease.Token),
            default);
        Assert.Equal(reassigned, lateAssignReplay);
        Assert.Equal(completed, lateCompleteReplay);
        Assert.Equal(cancelled, lateCancelReplay);
        Assert.Equal(2L, await harness.CountAsync("CaseTasks"));
        Assert.Equal(6L, await harness.CountHistoryAsync("case_task"));
        Assert.Equal(1L, await harness.CountHistoryAsync("case", "case_archived"));
        Assert.Equal(1L, await harness.CountStateAsync(CaseTaskState.Completed));
        Assert.Equal(1L, await harness.CountStateAsync(CaseTaskState.Cancelled));
        Assert.Equal(1L, await harness.CountCaseAsync(harness.TaskCaseId));
    }

    [Fact]
    public async Task EngineerFindingRequiresReportPreparationButExactReplayBypassesThePostStateGate()
    {
        await using var harness = await Harness.CreateAsync();
        await harness.SetCaseVersionAsync(harness.FindingCaseId, 37);
        var reviewLease = await harness.AcquireLease.ExecuteAsync(
            new(harness.FindingCaseId, 0, harness.EngineerActor, "claim-finding-review"),
            default);

        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.RecordEngineerFinding.ExecuteAsync(
            new(
                harness.FindingCaseId,
                0,
                harness.EngineerActor,
                "finding-denied-in-review",
                "The finding must not allocate an Audit identity in Review",
                reviewLease.Token,
                AuditAssessment.Repairable),
            default));

        var reportPreparation = await harness.TransitionCase.ExecuteAsync(
            new(
                harness.FindingCaseId,
                0,
                harness.EngineerActor,
                "finding-transition-to-report-preparation",
                "Inspection work is starting",
                reviewLease.Token,
                CaseTransitionDestination.ReportPreparation),
            default);
        var findingLease = await harness.AcquireLease.ExecuteAsync(
            new(
                harness.FindingCaseId,
                reportPreparation.Version,
                harness.EngineerActor,
                "claim-finding-report-preparation"),
            default);
        var request = new RecordEngineerFindingRequest(
            harness.FindingCaseId,
            reportPreparation.Version,
            harness.EngineerActor,
            "finding-record",
            "The inspection and audit assessment is complete",
            findingLease.Token,
            AuditAssessment.Repairable);
        var identity = await harness.RecordEngineerFinding.ExecuteAsync(request, default);

        Assert.NotNull(identity.AuditReference);
        Assert.Equal(1L, await harness.CountFindingAsync(harness.FindingCaseId));
        Assert.Equal(1L, await harness.CountHistoryAsync("case", "engineer_finding_recorded"));
        Assert.Equal(37L, await harness.ReadCaseVersionAsync(harness.FindingCaseId));
        Assert.Equal(2L, await harness.ReadWorkflowVersionAsync(harness.FindingCaseId));

        await harness.SetWorkflowStateAsync(harness.FindingCaseId, CaseLifecycleState.Review);
        var replay = await harness.RecordEngineerFinding.ExecuteAsync(request, default);

        Assert.Equal(identity, replay);
        Assert.Equal(1L, await harness.CountFindingAsync(harness.FindingCaseId));
        Assert.Equal(1L, await harness.CountHistoryAsync("case", "engineer_finding_recorded"));
    }

    [Fact]
    public async Task EngineerFindingKeepsTheLaterAuditBoxFolderManualAcrossReplayAndConcurrency()
    {
        await using var harness = await Harness.CreateAsync();
        var custody = harness.Services.GetRequiredService<ICaseCustody>();
        var root = await custody.CreateCaseRootAsync(
            harness.FindingCaseId,
            "QDOS26002",
            CustodyCreationOwner.Create(),
            "finding-case-root",
            default);
        await harness.ExecuteSqlAsync(
            $"UPDATE Cases SET CustodyState = 'confirmed', CustodyRootRemoteId = '{root.RemoteId}', CustodyConfirmedAtUtc = '{harness.TimeProvider.GetUtcNow():O}' WHERE Id = '{harness.FindingCaseId:D}'");

        var reviewLease = await harness.AcquireLease.ExecuteAsync(
            new(harness.FindingCaseId, 0, harness.EngineerActor, "finding-audit-transition-lease"), default);
        var reportPreparation = await harness.TransitionCase.ExecuteAsync(new(
            harness.FindingCaseId,
            0,
            harness.EngineerActor,
            "finding-audit-transition",
            "Inspection work is starting.",
            reviewLease.Token,
            CaseTransitionDestination.ReportPreparation), default);
        var findingLease = await harness.AcquireLease.ExecuteAsync(new(
            harness.FindingCaseId,
            reportPreparation.Version,
            harness.EngineerActor,
            "finding-audit-record-lease"), default);
        var request = new RecordEngineerFindingRequest(
            harness.FindingCaseId,
            reportPreparation.Version,
            harness.EngineerActor,
            "finding-audit-record",
            "The attributable inspection and Audit finding is complete.",
            findingLease.Token,
            AuditAssessment.Repairable);

        var results = await Task.WhenAll(
            harness.RecordEngineerFinding.ExecuteAsync(request, default),
            harness.RecordEngineerFinding.ExecuteAsync(request, default));

        Assert.Equal(results[0], results[1]);
        Assert.NotNull(results[0].AuditReference);
        Assert.Equal(0L, await harness.ScalarAsync<long>(
            $"SELECT COUNT_BIG(*) FROM ExternalWorkItems WHERE CaseId = '{harness.FindingCaseId:D}' AND Kind = '{ExternalWorkKinds.CreateAuditReferenceCustody}'"));
        Assert.Equal(0L, await harness.ScalarAsync<long>(
            $"SELECT COUNT_BIG(*) FROM CaseHistory WHERE CaseId = '{harness.FindingCaseId:D}' AND EventType = 'audit_custody_confirmed'"));
        Assert.True(string.IsNullOrWhiteSpace(await harness.ScalarAsync<string>(
            $"SELECT AuditCustodyRemoteId FROM Cases WHERE Id = '{harness.FindingCaseId:D}'")));
    }

    private static async Task<ArchiveCaseRequest> PrepareArchiveRequestAsync(
        Harness harness,
        string operationSuffix)
    {
        var closeLease = await harness.AcquireLease.ExecuteAsync(
            new(
                harness.TaskCaseId,
                0,
                harness.UserActor,
                $"claim-close-{operationSuffix}"),
            default);
        var closed = await harness.CloseCase.ExecuteAsync(
            new(
                harness.TaskCaseId,
                0,
                harness.UserActor,
                $"close-{operationSuffix}",
                "Prepare a terminal case for archive readiness testing",
                closeLease.Token,
                CaseClosureOutcome.ProviderCancelled),
            default);
        await harness.SetCustodyStateAsync(harness.TaskCaseId, "confirmed");
        var archiveLease = await harness.AcquireLease.ExecuteAsync(
            new(
                harness.TaskCaseId,
                closed.Version,
                harness.UserActor,
                $"claim-archive-{operationSuffix}"),
            default);
        return new(
            harness.TaskCaseId,
            closed.Version,
            harness.UserActor,
            $"archive-{operationSuffix}",
            "Exercise exact archive readiness policy",
            archiveLease.Token);
    }

    private static CreateCaseTaskRequest CreateRequest(
        Harness harness,
        Guid taskId,
        long caseVersion,
        ActionActor actor,
        string operationKey,
        string leaseToken,
        Guid? assigneeId = null) =>
        new(
            harness.TaskCaseId,
            taskId,
            caseVersion,
            actor,
            operationKey,
            "The task is required for case progression",
            leaseToken,
            "Review the retained case material",
            assigneeId);

    private sealed class Harness : IAsyncDisposable
    {
        private static readonly DateTimeOffset StartUtc =
            new(2026, 7, 30, 9, 0, 0, TimeSpan.Zero);
        private readonly LocalDbTestDatabase database;
        private readonly AsyncServiceScope scope;

        private Harness(
            LocalDbTestDatabase database,
            AsyncServiceScope scope,
            MutableCaseTimeProvider timeProvider,
            Guid taskCaseId,
            Guid findingCaseId,
            Guid administratorId,
            Guid engineerId,
            Guid userId)
        {
            this.database = database;
            this.scope = scope;
            TimeProvider = timeProvider;
            TaskCaseId = taskCaseId;
            FindingCaseId = findingCaseId;
            AdministratorId = administratorId;
            EngineerId = engineerId;
            UserId = userId;
            AdministratorActor = ActionActor.Staff(administratorId, [StaffRole.Administrator]);
            EngineerActor = ActionActor.Staff(engineerId, [StaffRole.Engineer]);
            UserActor = ActionActor.Staff(userId, [StaffRole.User]);

            var services = scope.ServiceProvider;
            AcquireLease = services.GetRequiredService<IAcquireCaseEditLease>();
            WorkflowStore = services.GetRequiredService<ICaseWorkflowStore>();
            TaskQueries = services.GetRequiredService<ICaseTaskQueries>();
            CreateTask = services.GetRequiredService<ICreateCaseTask>();
            AssignTask = services.GetRequiredService<IAssignCaseTask>();
            CompleteTask = services.GetRequiredService<ICompleteCaseTask>();
            CancelTask = services.GetRequiredService<ICancelCaseTask>();
            TransitionCase = services.GetRequiredService<ITransitionCase>();
            CloseCase = services.GetRequiredService<ICloseCase>();
            ArchiveCase = services.GetRequiredService<IArchiveCase>();
            RecordEngineerFinding = services.GetRequiredService<IRecordEngineerFinding>();
        }

        public MutableCaseTimeProvider TimeProvider { get; }
        public IServiceProvider Services => scope.ServiceProvider;
        public Guid TaskCaseId { get; }
        public Guid FindingCaseId { get; }
        public Guid AdministratorId { get; }
        public Guid EngineerId { get; }
        public Guid UserId { get; }
        public ActionActor AdministratorActor { get; }
        public ActionActor EngineerActor { get; }
        public ActionActor UserActor { get; }
        public IAcquireCaseEditLease AcquireLease { get; }
        public ICaseWorkflowStore WorkflowStore { get; }
        public ICaseTaskQueries TaskQueries { get; }
        public ICreateCaseTask CreateTask { get; }
        public IAssignCaseTask AssignTask { get; }
        public ICompleteCaseTask CompleteTask { get; }
        public ICancelCaseTask CancelTask { get; }
        public ITransitionCase TransitionCase { get; }
        public ICloseCase CloseCase { get; }
        public IArchiveCase ArchiveCase { get; }
        public IRecordEngineerFinding RecordEngineerFinding { get; }

        public Task ExecuteSqlAsync(string sql) => database.ExecuteAsync(sql);
        public Task<T> ScalarAsync<T>(string sql) => database.ScalarAsync<T>(sql);

        public static async Task<Harness> CreateAsync()
        {
            var timeProvider = new MutableCaseTimeProvider(StartUtc);
            var database = await LocalDbTestDatabase.CreateAsync(
                configureServices: services =>
                {
                    services.AddSingleton<TimeProvider>(timeProvider);
                    services.RemoveAll<ICaseCustody>();
                    services.AddSingleton<ICaseCustody, HarnessCustody>();
                });
            try
            {
                var administratorId = Guid.NewGuid();
                var engineerId = Guid.NewGuid();
                var userId = Guid.NewGuid();
                await using var seedScope = database.CreateAsyncScope();
                var principal = await SeededPrincipals.QdosAsync(seedScope.ServiceProvider);
                var organizationId = principal.OrganizationId;
                var lineageId = principal.SequenceLineageId;
                var principalId = principal.Id;
                var taskCaseId = Guid.NewGuid();
                var findingCaseId = Guid.NewGuid();

                await using (var context = await database.CreateContextAsync())
                {
                    var engineerRoleId = await context.Roles
                        .Where(role => role.NormalizedName == "ENGINEER")
                        .Select(role => role.Id)
                        .SingleAsync();
                    context.Users.AddRange(
                        Staff(administratorId, "administrator"),
                        Staff(engineerId, "engineer"),
                        Staff(userId, "user"));
                    context.UserRoles.Add(new IdentityUserRole<Guid>
                    {
                        UserId = engineerId,
                        RoleId = engineerRoleId
                    });
                    await context.SaveChangesAsync();
                    await InsertReceiptAsync(context, Guid.Parse("10000000-0000-0000-0000-000000000001"), 1);
                    await InsertReceiptAsync(context, Guid.Parse("10000000-0000-0000-0000-000000000002"), 2);
                    await InsertCaseAsync(
                        context,
                        taskCaseId,
                        principalId,
                        lineageId,
                        Guid.Parse("10000000-0000-0000-0000-000000000001"),
                        "QDOS26001",
                        "inspection",
                        1);
                    await InsertCaseAsync(
                        context,
                        findingCaseId,
                        principalId,
                        lineageId,
                        Guid.Parse("10000000-0000-0000-0000-000000000002"),
                        "QDOS26002",
                        "inspection_and_audit",
                        2);
                    await context.Database.ExecuteSqlInterpolatedAsync(
                        $"INSERT INTO CaseWorkflows (CaseId, State, AssignedEngineerId, Version, ConcurrencyToken) VALUES ({taskCaseId}, {nameof(CaseLifecycleState.Review)}, {engineerId}, {0L}, {Guid.NewGuid()})");
                    await context.Database.ExecuteSqlInterpolatedAsync(
                        $"INSERT INTO CaseWorkflows (CaseId, State, AssignedEngineerId, Version, ConcurrencyToken) VALUES ({findingCaseId}, {nameof(CaseLifecycleState.Review)}, {engineerId}, {0L}, {Guid.NewGuid()})");
                }

                var scope = database.CreateAsyncScope();
                return new(
                    database,
                    scope,
                    timeProvider,
                    taskCaseId,
                    findingCaseId,
                    administratorId,
                    engineerId,
                    userId);
            }
            catch
            {
                await database.DisposeAsync();
                throw;
            }
        }

        public Task SeedOpenTaskAsync(Guid taskId) => database.ExecuteAsync(
            $"INSERT INTO CaseTasks (Id, CaseId, Description, State, Version, ConcurrencyToken) VALUES ('{taskId:D}', '{TaskCaseId:D}', 'Retained open task', 'Open', 0, '{Guid.NewGuid():D}')");

        public Task<long> CountAsync(string table) => database.ScalarAsync<long>(
            table == "CaseTasks"
                ? "SELECT COUNT_BIG(*) FROM CaseTasks"
                : throw new ArgumentOutOfRangeException(nameof(table)));

        public Task<long> CountHistoryAsync(string aggregateType, string? eventKind = null) =>
            database.ScalarAsync<long>(
                eventKind is null
                    ? $"SELECT COUNT_BIG(*) FROM ActionHistory WHERE AggregateType = '{aggregateType}'"
                    : $"SELECT COUNT_BIG(*) FROM ActionHistory WHERE AggregateType = '{aggregateType}' AND EventKind = '{eventKind}'");

        public Task<long> CountStateAsync(CaseTaskState state) => database.ScalarAsync<long>(
            $"SELECT COUNT_BIG(*) FROM CaseTasks WHERE State = '{state}'");

        public Task<long> CountCaseAsync(Guid caseId) => database.ScalarAsync<long>(
            $"SELECT COUNT_BIG(*) FROM Cases WHERE Id = '{caseId:D}'");

        public Task<long> CountFindingAsync(Guid caseId) => database.ScalarAsync<long>(
            $"SELECT COUNT_BIG(*) FROM CaseEngineerFindings WHERE CaseId = '{caseId:D}'");

        public Task SetCaseVersionAsync(Guid caseId, long version) => database.ExecuteAsync(
            $"UPDATE Cases SET Version = {version} WHERE Id = '{caseId:D}'");

        public Task<long> ReadCaseVersionAsync(Guid caseId) => database.ScalarAsync<long>(
            $"SELECT Version FROM Cases WHERE Id = '{caseId:D}'");

        public Task<long> ReadWorkflowVersionAsync(Guid caseId) => database.ScalarAsync<long>(
            $"SELECT Version FROM CaseWorkflows WHERE CaseId = '{caseId:D}'");

        public Task SetCustodyStateAsync(Guid caseId, string state) => database.ExecuteAsync(
            $"UPDATE Cases SET CustodyState = '{state}' WHERE Id = '{caseId:D}'");

        public Task SetCaseTypeAsync(Guid caseId, string type) => database.ExecuteAsync(
            $"UPDATE Cases SET Type = '{type}' WHERE Id = '{caseId:D}'");

        public async Task<Guid> AddExternalWorkAsync(
            Guid caseId,
            string state,
            string kind = ExternalWorkKinds.VehicleLookup)
        {
            var workId = Guid.NewGuid();
            await database.ExecuteAsync(
                $"INSERT INTO ExternalWorkItems (Id, CaseId, Kind, OperationKey, State, AttemptCount, DueAtUtc) VALUES ('{workId:D}', '{caseId:D}', '{kind}', 'archive-readiness-{workId:N}', '{state}', 0, '2026-07-30T09:00:00+00:00')");
            return workId;
        }

        public Task SetAuditCustodyAsync(Guid caseId, bool confirmed) =>
            database.ExecuteAsync(confirmed
                ? $"UPDATE Cases SET AuditReference = 'Audit QDOS/26/100001', AuditCustodyRemoteId = 'audit-custody-remote-id', AuditCustodyConfirmedAtUtc = '2026-07-30T09:01:00+00:00' WHERE Id = '{caseId:D}'"
                : $"UPDATE Cases SET AuditReference = 'Audit QDOS/26/100001', AuditCustodyRemoteId = NULL, AuditCustodyConfirmedAtUtc = NULL WHERE Id = '{caseId:D}'");

        public Task SetExternalWorkStateAsync(Guid workId, string state) => database.ExecuteAsync(
            $"UPDATE ExternalWorkItems SET State = '{state}', CompletedAtUtc = '2026-07-30T09:01:00+00:00' WHERE Id = '{workId:D}'");

        public Task SetWorkflowStateAsync(Guid caseId, CaseLifecycleState state) => database.ExecuteAsync(
            $"UPDATE CaseWorkflows SET State = '{state}' WHERE CaseId = '{caseId:D}'");

        public async ValueTask DisposeAsync()
        {
            await scope.DisposeAsync();
            await database.DisposeAsync();
        }

        private static PegasusIdentityUser Staff(Guid id, string suffix) => new()
        {
            Id = id,
            UserName = $"{suffix}@example.test",
            NormalizedUserName = $"{suffix.ToUpperInvariant()}@EXAMPLE.TEST",
            Email = $"{suffix}@example.test",
            NormalizedEmail = $"{suffix.ToUpperInvariant()}@EXAMPLE.TEST",
            EmailConfirmed = true,
            IsEnabled = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N")
        };

        private sealed class HarnessCustody : ICaseCustody
        {
            public Task<CaseCustodyRoot> CreateCaseRootAsync(
                Guid caseId, string caseReference, string creationOwnerToken, string operationKey,
                CancellationToken cancellationToken) => Task.FromResult(
                    new CaseCustodyRoot(caseId, $"case-{caseReference}", caseReference));

            public Task<CaseCustodyRoot> GetExistingCaseRootAsync(
                Guid caseId, string caseReference, CancellationToken cancellationToken) => Task.FromResult(
                    new CaseCustodyRoot(caseId, $"case-{caseReference}", caseReference));

            public Task<CustodyDocumentVersion> RetainAcceptedIntakeSourceAsync(
                CaseCustodyRoot root, IntakeSourceCustodyReference source, string operationKey,
                CancellationToken cancellationToken) => Task.FromResult(
                    new CustodyDocumentVersion(root.CaseId, "source", source.SourceHash, "fixture"));

            public Task<string> CreateAuditReferenceFolderAsync(
                CaseCustodyRoot root, string auditReference, string creationOwnerToken, string operationKey,
                CancellationToken cancellationToken) => Task.FromResult($"{root.RemoteId}/{auditReference}");
        }

        private static Task<int> InsertCaseAsync(
            PegasusDbContext context,
            Guid caseId,
            Guid principalId,
            Guid lineageId,
            Guid receiptId,
            string reference,
            string caseType,
            int sequence) =>
            context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO Cases (Id, PrincipalId, SequenceLineageId, Year, Sequence, Reference, Type, InitialState, CustodyState, OriginIntakeReceiptId, InstructionComplete, ImagesComplete, InstructionConfirmedByStaff, ImagesConfirmedByStaff, CreatedAtUtc, Version, ConcurrencyToken) VALUES ({caseId}, {principalId}, {lineageId}, {2026}, {sequence}, {reference}, {caseType}, {"review"}, {"pending"}, {receiptId}, {true}, {true}, {true}, {true}, {StartUtc}, {0L}, {Guid.NewGuid()})");

        private static async Task InsertReceiptAsync(
            PegasusDbContext context,
            Guid receiptId,
            int sequence)
        {
            var sourceHash = sequence.ToString("X64", System.Globalization.CultureInfo.InvariantCulture);
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO IntakeReceipts (Id, SourceFileName, MediaType, SourceLength, SourceHash, SourceChannel, ExternalReceiptToken, ReceivedAtUtc, ProcessedAtUtc, SourceReaderKey, SourceReaderVersion, Version, Decision, DecisionReason, EvidenceJson, FieldsJson, OcrCandidatesJson) VALUES ({receiptId}, {$"case-task-{sequence}.eml"}, {"message/rfc822"}, {1L}, {sequence.ToString("X64", System.Globalization.CultureInfo.InvariantCulture)}, {"manual_upload"}, {$"case-task-{sequence}"}, {StartUtc}, {StartUtc}, {"case-task-test-reader"}, {"1"}, {0L}, {"case_created"}, {"Case task persistence fixture"}, {"{\"version\":1,\"data\":[]}"}, {"{\"version\":1,\"data\":[]}"}, {"{\"version\":1,\"data\":[]}"})");
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO IntakeAssets (Id, IntakeReceiptId, SourceLabel, FileName, MediaType, Kind, Disposition, ContentLength, ContentHash, StorageKey) VALUES ({Guid.NewGuid()}, {receiptId}, {"Original instruction"}, {$"case-task-{sequence}.eml"}, {"message/rfc822"}, {"source"}, {"source"}, {1L}, {sourceHash}, {$"fixture-source-{sequence}"})");
        }
    }

    private sealed class MutableCaseTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => utcNow;

        public void Advance(TimeSpan interval) => utcNow = utcNow.Add(interval);
    }
}
