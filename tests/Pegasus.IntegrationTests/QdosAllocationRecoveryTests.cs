using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;
using Pegasus.Core.Triage;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;
using Pegasus.Web.Mcp;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class QdosAllocationRecoveryTests
{
    [Fact]
    public async Task ClassificationNegativeAndAmbiguityFixturesPersistWithoutInventedCaseTypes()
    {
        using var factory = new IntakeWebApplicationFactory();
        var policy = new QdosMailClassificationPolicy();
        var fixtures = new[]
        {
            (
                Name: "marker-only",
                Content: (IReadOnlyList<IntakeContentFragment>)
                [new(IntakeEvidenceSource.DocumentContent, "instruction", "REPORT + AUDIT REPORT")],
                Expected: (CaseType?)null),
            (
                Name: "different-attachment",
                Content: (IReadOnlyList<IntakeContentFragment>)
                [
                    new(IntakeEvidenceSource.DocumentContent, "instruction", "ENGINEER NOTIFICATION"),
                    new(IntakeEvidenceSource.DocumentContent, "other attachment", "REPORT + AUDIT REPORT")
                ],
                Expected: (CaseType?)CaseType.Inspection),
            (
                Name: "nested-combined",
                Content: (IReadOnlyList<IntakeContentFragment>)
                [
                    new(IntakeEvidenceSource.DocumentContent, "instruction", "ENGINEER NOTIFICATION"),
                    new(IntakeEvidenceSource.DocumentContent, "message body, attached email 1, attached letter", "ENGINEER NOTIFICATION (REPORT + AUDIT REPORT)")
                ],
                Expected: (CaseType?)CaseType.Inspection),
            (
                Name: "simultaneous-titles",
                Content: (IReadOnlyList<IntakeContentFragment>)
                [
                    new(IntakeEvidenceSource.DocumentContent, "audit instruction", "AUDIT REPORT NOTIFICATION"),
                    new(IntakeEvidenceSource.DocumentContent, "engineer instruction", "ENGINEER NOTIFICATION")
                ],
                Expected: (CaseType?)null)
        };

        foreach (var fixture in fixtures)
        {
            var classification = policy.Classify(new(
                IntakeSourceReadStatus.Readable,
                fixture.Content,
                [],
                [],
                false));
            var receipt = await AllocationTestData.StoreDefinitiveReceiptAsync(
                factory.Services,
                classification.CaseType,
                $"NEG{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
                classificationDecision: classification);
            await using var scope = factory.Services.CreateAsyncScope();
            var persisted = Assert.IsType<IntakeReceipt>(
                await scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>()
                    .GetAsync(receipt.Id, CancellationToken.None));
            Assert.Equal(fixture.Expected, persisted.MailClassificationDecision?.CaseType);
        }

        Assert.Equal(0, await AllocationTestData.CountAsync(factory.Services, "Cases"));
    }

    [Fact]
    public async Task PersistedStaffForwardRetainsOuterTransportAndOriginalQdosIdentity()
    {
        using var factory = new IntakeWebApplicationFactory();
        var route = new QdosMailRoutePolicy().Evaluate(new(
            IntakeSourceReadStatus.Readable,
            [],
            [
                new(
                    IntakeEvidenceSource.Sender,
                    "staff@collisionengineers.co.uk",
                    IntakeSenderIdentityKind.Transport,
                    "outer message"),
                new(
                    IntakeEvidenceSource.Sender,
                    "instructions@qdosassist.co.uk",
                    IntakeSenderIdentityKind.AttachedOriginal,
                    "attached original")
            ],
            [],
            false));
        var receipt = await AllocationTestData.StoreDefinitiveReceiptAsync(
            factory.Services,
            CaseType.Inspection,
            "QDOS",
            route);

        await using var scope = factory.Services.CreateAsyncScope();
        var persisted = Assert.IsType<IntakeReceipt>(
            await scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>()
                .GetAsync(receipt.Id, CancellationToken.None));
        Assert.Equal("staff@collisionengineers.co.uk", Assert.Single(persisted.MailRouteDecision!.TransportIdentities).Address);
        Assert.Equal("instructions@qdosassist.co.uk", Assert.Single(persisted.MailRouteDecision.OriginalIdentities).Address);
        Assert.Equal("instructions@qdosassist.co.uk", persisted.MailRouteDecision.EffectiveSender?.Address);
        Assert.Equal(QdosMailRoutePolicy.Version, persisted.MailRouteDecision.PolicyVersion);
    }

    [Fact]
    public async Task AtomicSuccessSurvivesAnExceptionAfterAcceptanceWithoutAFalseFailure()
    {
        using var factory = new IntakeWebApplicationFactory();
        await AllocationTestData.SeedPrincipalAsync(factory.Services, "POSTCOMMIT");
        var receipt = await AllocationTestData.StoreDefinitiveReceiptAsync(
            factory.Services,
            CaseType.Inspection,
            "POSTCOMMIT");
        var logs = new CapturingLogger<EfIntakeAllocationStore>();

        IntakeAllocationResult? result;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var store = new EfIntakeAllocationStore(
                scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>(),
                logs);
            var allocate = new AllocateIntake(
                scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>(),
                store,
                new AfterCommitAcceptIntake(
                    scope.ServiceProvider.GetRequiredService<IAcceptIntake>(),
                    cancel: false),
                scope.ServiceProvider.GetRequiredService<TimeProvider>());
            result = await allocate.AttemptAutomaticAsync(receipt.Id, Guid.NewGuid());
        }

        Assert.Equal(IntakeAllocationProjectionStatus.Succeeded, result?.State.Status);
        Assert.Equal(1, await AllocationTestData.CountAsync(factory.Services, "Cases"));
        Assert.Equal(1, await AllocationTestData.AllocationEventCountAsync(factory.Services));
        Assert.Equal(0, await AllocationTestData.FailedAllocationEventCountAsync(factory.Services));
        Assert.DoesNotContain(logs.Entries, entry => entry.EventId.Id == 4721);
    }

    [Fact]
    public async Task CancellationAfterAtomicSuccessRethrowsWithoutDeletingOrFailingTheOutcome()
    {
        using var factory = new IntakeWebApplicationFactory();
        await AllocationTestData.SeedPrincipalAsync(factory.Services, "POSTCANCEL");
        var receipt = await AllocationTestData.StoreDefinitiveReceiptAsync(
            factory.Services,
            CaseType.Inspection,
            "POSTCANCEL");
        var logs = new CapturingLogger<EfIntakeAllocationStore>();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var store = new EfIntakeAllocationStore(
                scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>(),
                logs);
            var allocate = new AllocateIntake(
                scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>(),
                store,
                new AfterCommitAcceptIntake(
                    scope.ServiceProvider.GetRequiredService<IAcceptIntake>(),
                    cancel: true),
                scope.ServiceProvider.GetRequiredService<TimeProvider>());
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                allocate.AttemptAutomaticAsync(receipt.Id, Guid.NewGuid()));

            var current = await store.GetCurrentAsync(receipt.Id, CancellationToken.None);
            Assert.Equal(IntakeAllocationAttemptStatus.Succeeded, current?.Status);
        }

        Assert.Equal(1, await AllocationTestData.CountAsync(factory.Services, "Cases"));
        Assert.Equal(1, await AllocationTestData.AllocationEventCountAsync(factory.Services));
        Assert.Equal(0, await AllocationTestData.FailedAllocationEventCountAsync(factory.Services));
        Assert.DoesNotContain(logs.Entries, entry => entry.EventId.Id == 4721);
    }

    [Fact]
    public async Task InterruptedPendingOperationResumesThroughIdempotentAtomicAcceptance()
    {
        using var factory = new IntakeWebApplicationFactory();
        await AllocationTestData.SeedPrincipalAsync(factory.Services, "PENDING");
        var receipt = await AllocationTestData.StoreDefinitiveReceiptAsync(
            factory.Services,
            CaseType.Inspection,
            "PENDING");
        var evaluationId = Guid.NewGuid();
        var operationKey = $"intake-allocation:{evaluationId:N}";
        const string reason = "Created automatically from a definitive authorised instruction.";
        var command = new IntakeAllocationCommand(
            receipt.Id,
            receipt.Version,
            CaseType.Inspection,
            "PENDING",
            // The seeded pending attempt must carry the same command the
            // automatic route builds, or the resumed attempt is a different one
            // and the operation key conflicts.
            //
            // The instruction half is asserted by the route's own precondition
            // (CASE-013). The images half is observed from the receipt, and this
            // receipt is seeded with no assets, so it is false (CASE-021). It
            // was a hardcoded true here because it was a hardcoded true in the
            // production path.
            new(true, false, false, false),
            null,
            receipt.InstructionDraft?.InspectionDate);
        var actor = ActionActor.SystemWorker("system-worker:intake-processing");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IIntakeAllocationStore>();
            await store.BeginAsync(
                new(
                    IntakeAllocationAttemptKind.Automatic,
                    command,
                    actor,
                    operationKey,
                    AllocationTestData.CommandHash(
                        IntakeAllocationAttemptKind.Automatic,
                        command,
                        actor,
                        operationKey,
                        reason),
                    reason,
                    null,
                    scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow()),
                CancellationToken.None);

            var result = await scope.ServiceProvider.GetRequiredService<IAllocateIntake>()
                .AttemptAutomaticAsync(receipt.Id, evaluationId);
            Assert.Equal(IntakeAllocationProjectionStatus.Succeeded, result?.State.Status);
            Assert.True(result?.IsReplay);
        }

        Assert.Equal(1, await AllocationTestData.CountAsync(factory.Services, "IntakeAllocationAttempts"));
        Assert.Equal(1, await AllocationTestData.CountAsync(factory.Services, "Cases"));
        Assert.Equal(1, await AllocationTestData.CountAsync(factory.Services, "CaseIntakeLinks"));
        Assert.Equal(1, await AllocationTestData.AllocationEventCountAsync(factory.Services));
    }

    [Theory]
    [InlineData(CaseType.Inspection, "inspection")]
    [InlineData(CaseType.InspectionAndAudit, "inspection_and_audit")]
    public async Task DefinitiveTypedInstructionAllocatesOneExistingCaseAggregate(
        CaseType caseType,
        string persistedType)
    {
        using var factory = new IntakeWebApplicationFactory();
        var principal = $"T{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        await AllocationTestData.SeedPrincipalAsync(factory.Services, principal);
        var receipt = await AllocationTestData.StoreDefinitiveReceiptAsync(
            factory.Services,
            caseType,
            principal);

        IntakeAllocationResult? result;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            result = await scope.ServiceProvider.GetRequiredService<IAllocateIntake>()
                .AttemptAutomaticAsync(receipt.Id, Guid.NewGuid());
        }

        Assert.Equal(IntakeAllocationProjectionStatus.Succeeded, result?.State.Status);
        Assert.Equal(persistedType, await AllocationTestData.CaseTypeAsync(factory.Services));
        Assert.Equal(1, await AllocationTestData.CountAsync(factory.Services, "Cases"));
        Assert.Equal(1, await AllocationTestData.CountAsync(factory.Services, "CaseIntakeLinks"));
        Assert.Equal(1, await AllocationTestData.CountAsync(factory.Services, "CaseSequences"));
        Assert.Equal(1, await AllocationTestData.CountAsync(factory.Services, "CaseWorkflows"));
        Assert.Equal(1, await AllocationTestData.CountAsync(factory.Services, "ExternalWorkItems"));
        Assert.Equal(0, await AllocationTestData.CountAsync(factory.Services, "Triage"));
        Assert.Equal(1, await AllocationTestData.AllocationEventCountAsync(factory.Services));
    }

    [Fact]
    public async Task UniqueExistingCaseAssociationBypassesNewAllocationExactlyOnce()
    {
        using var factory = new IntakeWebApplicationFactory();
        var principal = $"E{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        await AllocationTestData.SeedPrincipalAsync(factory.Services, principal);
        var original = await AllocationTestData.StoreDefinitiveReceiptAsync(
            factory.Services,
            CaseType.Inspection,
            principal);
        Guid existingCaseId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var allocated = await scope.ServiceProvider.GetRequiredService<IAllocateIntake>()
                .AttemptAutomaticAsync(original.Id, Guid.NewGuid());
            existingCaseId = Assert.IsType<Guid>(allocated?.State.CaseId);
        }

        var match = new CaseMatchEvaluationResult(
            CaseMatchOutcome.UniqueMatch,
            existingCaseId,
            null,
            new("EXISTING/1", "AB12CDE", "EXAMPLE", "J", new DateOnly(2031, 8, 10)),
            [new(existingCaseId, ["claim-reference", "vehicle-registration"], [])],
            "Exactly one existing case matched.",
            "qdos_case_match",
            1);
        var followOn = await AllocationTestData.StoreDefinitiveReceiptAsync(
            factory.Services,
            CaseType.Inspection,
            principal,
            caseMatchDecision: match);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var association = scope.ServiceProvider.GetRequiredService<IAutomaticCaseAssociationStore>();
            var first = await association.AssociateFromMatchAsync(
                new(
                    followOn.Id,
                    existingCaseId,
                    match.PolicyKey,
                    match.PolicyVersion,
                    "system-worker:intake-processing",
                    $"case-match-association:{Guid.NewGuid():N}",
                    "Automatic association from the recorded unique match."),
                scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow(),
                CancellationToken.None);
            Assert.Equal(AutomaticCaseAssociationOutcome.Associated, first);
            Assert.Null(await scope.ServiceProvider.GetRequiredService<IAllocateIntake>()
                .AttemptAutomaticAsync(followOn.Id, Guid.NewGuid()));
        }

        Assert.Equal(1, await AllocationTestData.CountAsync(factory.Services, "Cases"));
        Assert.Equal(1, await AllocationTestData.CountAsync(factory.Services, "CaseIntakeLinks"));
        Assert.Equal(2, await AllocationTestData.CountAsync(factory.Services, "IntakeManualAssociations"));
        Assert.Equal(1, await AllocationTestData.CountAsync(factory.Services, "CaseSequences"));
        Assert.Equal(1, await AllocationTestData.CountAsync(factory.Services, "CaseWorkflows"));
        Assert.Equal(1, await AllocationTestData.CountAsync(factory.Services, "ExternalWorkItems"));
    }

    [Fact]
    public async Task MissingPrincipalFailurePersistsAndReasonedStaffRetryAllocatesExactlyOnce()
    {
        using var factory = new IntakeWebApplicationFactory();
        var receipt = await AllocationTestData.StoreDefinitiveReceiptAsync(
            factory.Services,
            CaseType.Inspection,
            "RECOVER");

        IntakeAllocationResult? first;
        IntakeAllocationResult? suppressed;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var allocate = scope.ServiceProvider.GetRequiredService<IAllocateIntake>();
            first = await allocate.AttemptAutomaticAsync(receipt.Id, Guid.NewGuid());
            suppressed = await allocate.AttemptAutomaticAsync(receipt.Id, Guid.NewGuid());
        }

        Assert.Equal(IntakeAllocationProjectionStatus.FailedRecoverable, first?.State.Status);
        Assert.Equal(IntakeAllocationFailureKind.PrincipalUnavailable, first?.State.FailureKind);
        Assert.True(suppressed?.IsSuppressed);
        Assert.Equal(1, await AllocationTestData.CountAsync(
            factory.Services,
            "IntakeAllocationAttempts"));
        Assert.Equal(0, await AllocationTestData.CountAsync(factory.Services, "Cases"));

        await AllocationTestData.SeedPrincipalAsync(factory.Services, "RECOVER");
        await AllocationTestData.ChangePersistedClassificationCaseTypeAsync(
            factory.Services,
            receipt.Id,
            "inspection_and_audit");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var completedReplay = await scope.ServiceProvider.GetRequiredService<IAllocateIntake>()
                .AttemptAutomaticAsync(receipt.Id, Guid.NewGuid());
            Assert.True(completedReplay?.IsSuppressed);
            Assert.Equal(IntakeAllocationProjectionStatus.FailedRecoverable, completedReplay?.State.Status);
        }
        Assert.Equal(0, await AllocationTestData.CountAsync(factory.Services, "Cases"));

        var actor = ActionActor.Staff(
            DevelopmentOfflineIdentity.AdministratorId,
            [StaffRole.Administrator]);
        var retry = new RetryIntakeAllocationRequest(
            receipt.Id,
            receipt.Version,
            Assert.IsType<Guid>(first?.State.AttemptId),
            actor,
            $"allocation-retry:{Guid.NewGuid():N}",
            "Principal was corrected and the case allocation was reviewed.");

        IntakeAllocationResult succeeded;
        IntakeAllocationResult replay;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var allocate = scope.ServiceProvider.GetRequiredService<IAllocateIntake>();
            succeeded = await allocate.RetryAsync(retry);
            replay = await allocate.RetryAsync(retry);
        }

        Assert.Equal(IntakeAllocationProjectionStatus.Succeeded, succeeded.State.Status);
        Assert.Equal(succeeded.State.CaseId, replay.State.CaseId);
        Assert.True(replay.IsReplay);
        Assert.Equal(1, await AllocationTestData.CountAsync(factory.Services, "Cases"));
        Assert.Equal("inspection", await AllocationTestData.CaseTypeAsync(factory.Services));
        Assert.Equal(1, await AllocationTestData.CountAsync(factory.Services, "CaseIntakeLinks"));
        Assert.Equal(2, await AllocationTestData.CountAsync(
            factory.Services,
            "IntakeAllocationAttempts"));
        Assert.Equal(2, await AllocationTestData.AllocationEventCountAsync(factory.Services));
    }

    [Fact]
    public async Task PrincipalCorrectionAndCompletedSourceRedeliveryCannotAllocateBeforeStaffRetry()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            useIntegrationTestAuthentication: true,
            initializeDevelopmentOffline: false,
            mailClassificationPolicy: new ConsumerTypedClassificationPolicy());
        await AllocationTestData.DisableQdosAsync(factory.Services);
        var email = IntakeTestEvidence.CreateEmail(
            "qdos-allocation-redelivery.eml",
            "QDOS instruction\r\nClaimant Name: Redelivery Claimant\r\nClaim Number: RED-1\r\nVehicle Registration: AB12 CDE");
        var token = Guid.NewGuid().ToString("N");

        Guid first;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            first = await AllocationTestData.SubmitAndProcessAsync(scope.ServiceProvider,
                new(
                    email.FileName,
                    email.MediaType,
                    email.Content,
                    scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow(),
                    "system-worker:approved-inbox-poller",
                    new(IntakeSourceChannel.Mailbox, token)),
                $"mailbox-submit:{Guid.NewGuid():N}");
        }
        var receiptId = first;
        await AllocationTestData.SeedPrincipalAsync(factory.Services, "QDOS");
        Guid replay;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            replay = await AllocationTestData.SubmitAndProcessAsync(scope.ServiceProvider,
                new(
                    email.FileName,
                    email.MediaType,
                    email.Content,
                    scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow(),
                    "system-worker:approved-inbox-poller",
                    new(IntakeSourceChannel.Mailbox, token)),
                $"mailbox-submit:{Guid.NewGuid():N}");
        }

        Assert.Equal(receiptId, replay);
        Assert.Equal(0, await AllocationTestData.CountAsync(factory.Services, "Cases"));
        await using (var diagnosticScope = factory.Services.CreateAsyncScope())
        {
            var diagnostic = Assert.IsType<IntakeReceipt>(
                await diagnosticScope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>()
                    .GetAsync(receiptId, CancellationToken.None));
            Assert.True(
                await AllocationTestData.CountAsync(factory.Services, "IntakeAllocationAttempts") == 1,
                $"decision={diagnostic.Decision}; route={diagnostic.MailRouteDecision?.Disposition}/{diagnostic.MailRouteDecision?.SelectedRoute?.WorkProviderCode}; classification={diagnostic.MailClassificationDecision?.Outcome}/{diagnostic.MailClassificationDecision?.CaseType}");
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var receipt = Assert.IsType<IntakeReceipt>(
                await scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>()
                    .GetAsync(receiptId, CancellationToken.None));
            var failed = Assert.IsType<IntakeAllocationState>(receipt.AllocationState);
            Assert.True(
                failed.Status == IntakeAllocationProjectionStatus.FailedRecoverable,
                $"Allocation={failed.Status}/{failed.FailureKind}; classification={receipt.MailClassificationDecision?.Outcome}/{receipt.MailClassificationDecision?.CaseType}; reason={failed.SafeReason}");
            var result = await scope.ServiceProvider.GetRequiredService<IAllocateIntake>().RetryAsync(new(
                receipt.Id,
                receipt.Version,
                failed.AttemptId,
                ActionActor.Staff(
                    DevelopmentOfflineIdentity.AdministratorId,
                    [StaffRole.Administrator]),
                $"allocation-retry:{Guid.NewGuid():N}",
                "Principal corrected after completed-source redelivery was suppressed."));
            Assert.Equal(IntakeAllocationProjectionStatus.Succeeded, result.State.Status);
        }

        Assert.Equal(1, await AllocationTestData.CountAsync(factory.Services, "Cases"));
        Assert.Equal(1, await AllocationTestData.CountAsync(factory.Services, "CaseIntakeLinks"));
        Assert.Equal(2, await AllocationTestData.CountAsync(factory.Services, "IntakeAllocationAttempts"));
        Assert.Equal(2, await AllocationTestData.AllocationEventCountAsync(factory.Services));
    }

    [Fact]
    public async Task SameFailedOperationReplaysButChangedReasonConflictsAndNewRetryRecordsOnce()
    {
        using var factory = new IntakeWebApplicationFactory();
        var receipt = await AllocationTestData.StoreDefinitiveReceiptAsync(
            factory.Services,
            CaseType.Inspection,
            "MISSING");
        var evaluationId = Guid.NewGuid();
        IntakeAllocationResult? first;
        IntakeAllocationResult? replay;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var allocate = scope.ServiceProvider.GetRequiredService<IAllocateIntake>();
            first = await allocate.AttemptAutomaticAsync(receipt.Id, evaluationId);
            replay = await allocate.AttemptAutomaticAsync(receipt.Id, evaluationId);
        }

        Assert.True(replay?.IsReplay);
        Assert.False(replay?.IsSuppressed);
        Assert.Equal(first?.State.AttemptId, replay?.State.AttemptId);
        Assert.Equal(1, await AllocationTestData.AllocationEventCountAsync(factory.Services));

        var otherReceipt = await AllocationTestData.StoreDefinitiveReceiptAsync(
            factory.Services,
            CaseType.Inspection,
            "MISSING");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await Assert.ThrowsAsync<IntakeAllocationOperationConflictException>(() =>
                scope.ServiceProvider.GetRequiredService<IAllocateIntake>()
                    .AttemptAutomaticAsync(otherReceipt.Id, evaluationId));
        }

        var actor = ActionActor.Staff(
            DevelopmentOfflineIdentity.AdministratorId,
            [StaffRole.Administrator]);
        var retryKey = $"retry:{Guid.NewGuid():N}";
        var retry = new RetryIntakeAllocationRequest(
            receipt.Id,
            receipt.Version,
            first!.State.AttemptId,
            actor,
            retryKey,
            "Retry before correction.");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var allocate = scope.ServiceProvider.GetRequiredService<IAllocateIntake>();
            var failedRetry = await allocate.RetryAsync(retry);
            Assert.Equal(IntakeAllocationFailureKind.PrincipalUnavailable, failedRetry.State.FailureKind);
            await Assert.ThrowsAsync<IntakeAllocationOperationConflictException>(() =>
                allocate.RetryAsync(retry with { Reason = "A different reason." }));
        }

        Assert.Equal(2, await AllocationTestData.AllocationEventCountAsync(factory.Services));
        Assert.Equal(2, await AllocationTestData.CountAsync(factory.Services, "IntakeAllocationAttempts"));
        Assert.Equal(0, await AllocationTestData.CountAsync(factory.Services, "Cases"));
    }

    [Fact]
    public async Task CompletedSourceReplayRecoversAllocationLostBeforeItPersisted()
    {
        // Defect B: automatic allocation runs after CompleteProcessingAsync and
        // outside the try/catch. If it is lost before it persists any attempt (a
        // transient begin failure), the receipt is a definitive case_created with
        // no case and zero attempts. The completed-work replay branch must
        // re-drive allocation and mint the stranded case, without double-allocating.
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            useIntegrationTestAuthentication: true,
            initializeDevelopmentOffline: false,
            mailClassificationPolicy: new ConsumerTypedClassificationPolicy());
        await AllocationTestData.SeedPrincipalAsync(factory.Services, "QDOS");
        var email = IntakeTestEvidence.CreateEmail(
            "qdos-replay-recovery.eml",
            "QDOS instruction\r\nClaimant Name: Replay Claimant\r\nClaim Number: REP-1\r\nVehicle Registration: AB12 CDE");
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var clock = services.GetRequiredService<TimeProvider>();
        var store = services.GetRequiredService<IIntakeWorkStore>();
        var artifactStore = services.GetRequiredService<IIntakeArtifactStore>();

        var received = await new ReceiveIntake(artifactStore, store, clock, new CommittedWorkPublisherDouble()).ExecuteAsync(
            new(
                email.FileName,
                email.MediaType,
                email.Content,
                clock.GetUtcNow(),
                "system-worker:approved-inbox-poller",
                new(IntakeSourceChannel.Mailbox, Guid.NewGuid().ToString("N"))),
            $"qdos-alpha:replay-recovery:{Guid.NewGuid():N}");
        var dispatch = await store.ClaimDispatchAsync(
            clock.GetUtcNow(), TimeSpan.FromMinutes(1), CancellationToken.None);
        Assert.NotNull(dispatch);
        await store.MarkDispatchedAsync(
            dispatch!.Id, dispatch.LeaseToken!, clock.GetUtcNow(), CancellationToken.None);

        var spy = new FirstAutomaticAllocationLost(
            services.GetRequiredService<IAllocateIntake>());
        var processor = new ProcessQueuedIntake(
            store,
            artifactStore,
            services.GetRequiredService<ProcessIntake>(),
            services.GetRequiredService<IIntakeReceiptQueries>(),
            services.GetRequiredService<ICreateTriageFromIntake>(),
            services.GetRequiredService<IAutomaticCaseAssociationStore>(),
            spy,
            clock,
            services.GetRequiredService<Pegasus.Core.Documents.IReadLogicalDocumentVersion>(),
            services.GetService<IImageIntakeAutomation>());

        // First pass: processes to a definitive receipt, but the automatic
        // allocation is lost before it persists — no case, no attempt.
        await processor.ExecuteAsync(received.StagedReceiptId);
        Assert.Equal(0, await AllocationTestData.CountAsync(factory.Services, "Cases"));
        Assert.Equal(0, await AllocationTestData.CountAsync(factory.Services, "IntakeAllocationAttempts"));

        // Replay: the work item is already completed, so it enters the
        // completed-work replay branch, which now re-drives allocation and mints
        // the stranded case.
        await processor.ExecuteAsync(received.StagedReceiptId);
        Assert.Equal(1, await AllocationTestData.CountAsync(factory.Services, "Cases"));
        Assert.Equal(1, await AllocationTestData.CountAsync(factory.Services, "IntakeAllocationAttempts"));

        // A further replay does not double-allocate.
        await processor.ExecuteAsync(received.StagedReceiptId);
        Assert.Equal(1, await AllocationTestData.CountAsync(factory.Services, "Cases"));
        Assert.Equal(1, await AllocationTestData.CountAsync(factory.Services, "IntakeAllocationAttempts"));
        Assert.True(spy.AutomaticCalls >= 2);
    }

    [Fact]
    public Task LiveQueuedProcessingRunsMailAssociationBeforeAllocation() =>
        ProveQueuedMailAssociationCallerAsync(completedReplay: false);

    [Fact]
    public Task CompletedQueuedReplayRunsMailAssociationBeforeAllocation() =>
        ProveQueuedMailAssociationCallerAsync(completedReplay: true);

    private static async Task ProveQueuedMailAssociationCallerAsync(bool completedReplay)
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            useIntegrationTestAuthentication: true,
            initializeDevelopmentOffline: false,
            mailClassificationPolicy: new ConsumerTypedClassificationPolicy());
        await AllocationTestData.SeedPrincipalAsync(factory.Services, "QDOS");
        var original = await AllocationTestData.StoreDefinitiveReceiptAsync(
            factory.Services,
            CaseType.Inspection,
            "QDOS");
        Guid existingCaseId;
        await using (var allocationScope = factory.Services.CreateAsyncScope())
        {
            var result = await allocationScope.ServiceProvider
                .GetRequiredService<IAllocateIntake>()
                .AttemptAutomaticAsync(original.Id, Guid.NewGuid());
            existingCaseId = Assert.IsType<Guid>(result?.State.CaseId);
        }

        var email = IntakeTestEvidence.CreateEmail(
            completedReplay ? "mail-09-replay.eml" : "mail-09-live.eml",
            "QDOS instruction\r\nClaimant Name: Mail Association\r\nClaim Number: MAIL-09\r\nVehicle Registration: AB12 CDE");
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var clock = services.GetRequiredService<TimeProvider>();
        var workStore = new RetainingWorkStore(
            services.GetRequiredService<IIntakeWorkStore>(),
            factory.Services);
        var artifactStore = services.GetRequiredService<IIntakeArtifactStore>();
        var received = await new ReceiveIntake(artifactStore, workStore, clock, new CommittedWorkPublisherDouble()).ExecuteAsync(
            new(
                email.FileName,
                email.MediaType,
                email.Content,
                clock.GetUtcNow(),
                "system-worker:approved-inbox-poller",
                new(IntakeSourceChannel.Mailbox, Guid.NewGuid().ToString("N"))),
            $"mailbox-submit:{Guid.NewGuid():N}");
        var dispatch = Assert.IsType<IntakeWorkItem>(await workStore.ClaimDispatchAsync(
            clock.GetUtcNow(),
            TimeSpan.FromMinutes(1),
            CancellationToken.None));
        await workStore.MarkDispatchedAsync(
            dispatch.Id,
            Assert.IsType<string>(dispatch.LeaseToken),
            clock.GetUtcNow(),
            CancellationToken.None);

        var events = new List<string>();
        if (completedReplay)
        {
            var setupProcessor = CreateMailAssociationProcessor(
                services,
                workStore,
                artifactStore,
                new RecordingProviderAssociationStore(events),
                new NoOpAllocateIntake(),
                clock,
                automaticMailCaseAssociation: null);
            await setupProcessor.ExecuteAsync(received.StagedReceiptId);
            events.Clear();
        }

        var efStore = services.GetRequiredService<EfIntakeMutationStore>();
        var evidence = new RecordingMailEvidenceQueries(efStore, events);
        var automaticMailAssociation = new AssociateRetainedMailWithCase(
            evidence,
            efStore,
            clock);
        var allocation = new ObservingAllocateIntake(
            services.GetRequiredService<IAllocateIntake>(),
            services.GetRequiredService<IIntakeReceiptQueries>(),
            events);
        var processor = CreateMailAssociationProcessor(
            services,
            workStore,
            artifactStore,
            new RecordingProviderAssociationStore(events),
            allocation,
            clock,
            automaticMailAssociation);

        await processor.ExecuteAsync(received.StagedReceiptId);

        Assert.Equal(["provider", "mail", "allocation"], events);
        Assert.Equal(existingCaseId, allocation.CurrentCaseIdSeen);
        Assert.Equal(1, await AllocationTestData.CountAsync(factory.Services, "Cases"));
        var completed = Assert.IsType<IntakeEvaluationRevision>(
            await workStore.GetCompletedEvaluationAsync(received.StagedReceiptId, CancellationToken.None));
        await using var verificationContext = await services
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        var association = Assert.Single(await verificationContext.IntakeManualAssociations
            .AsNoTracking()
            .Where(item => item.IntakeReceiptId == completed.ProcessedReceiptId)
            .ToListAsync());
        Assert.Equal(existingCaseId, association.CaseId);

        events.Clear();
        await processor.ExecuteAsync(received.StagedReceiptId);
        Assert.Equal(["allocation"], events);
        Assert.Equal(existingCaseId, allocation.CurrentCaseIdSeen);
    }

    private static ProcessQueuedIntake CreateMailAssociationProcessor(
        IServiceProvider services,
        IIntakeWorkStore workStore,
        IIntakeArtifactStore artifactStore,
        IAutomaticCaseAssociationStore providerAssociationStore,
        IAllocateIntake allocateIntake,
        TimeProvider clock,
        AssociateRetainedMailWithCase? automaticMailCaseAssociation) => new(
            workStore,
            artifactStore,
            services.GetRequiredService<ProcessIntake>(),
            services.GetRequiredService<IIntakeReceiptQueries>(),
            services.GetRequiredService<ICreateTriageFromIntake>(),
            providerAssociationStore,
            allocateIntake,
            clock,
            services.GetRequiredService<Pegasus.Core.Documents.IReadLogicalDocumentVersion>(),
            automaticMailCaseAssociation: automaticMailCaseAssociation);

    private sealed class RecordingProviderAssociationStore(List<string> events)
        : IAutomaticCaseAssociationStore
    {
        public Task<AutomaticCaseAssociationOutcome> AssociateFromMatchAsync(
            AutomaticCaseAssociationRequest request,
            DateTimeOffset occurredAtUtc,
            CancellationToken cancellationToken)
        {
            // The provider attempt is deliberately recorded but does not write,
            // leaving this fixture unassociated so MAIL-09 gets its exact turn.
            events.Add("provider");
            return Task.FromResult(AutomaticCaseAssociationOutcome.AlreadyAssociated);
        }
    }

    private sealed class RecordingMailEvidenceQueries(
        IAutomaticMailCaseAssociationEvidenceQueries inner,
        List<string> events) : IAutomaticMailCaseAssociationEvidenceQueries
    {
        public Task<AutomaticMailCaseAssociationEvidence?> GetAsync(
            Guid intakeReceiptId,
            CancellationToken cancellationToken)
        {
            events.Add("mail");
            return inner.GetAsync(intakeReceiptId, cancellationToken);
        }
    }

    private sealed class ObservingAllocateIntake(
        IAllocateIntake inner,
        IIntakeReceiptQueries receipts,
        List<string> events) : IAllocateIntake
    {
        public Guid? CurrentCaseIdSeen { get; private set; }

        public async Task<IntakeAllocationResult?> AttemptAutomaticAsync(
            Guid receiptId,
            Guid evaluationId,
            CancellationToken cancellationToken = default)
        {
            events.Add("allocation");
            CurrentCaseIdSeen = (await receipts.GetAsync(receiptId, cancellationToken))?.CurrentCaseId;
            return await inner.AttemptAutomaticAsync(receiptId, evaluationId, cancellationToken);
        }

        public Task<IntakeAllocationResult> AttemptStaffCreateAsync(
            AcceptIntakeRequest request,
            CancellationToken cancellationToken = default) =>
            inner.AttemptStaffCreateAsync(request, cancellationToken);

        public Task<IntakeAllocationResult> RetryAsync(
            RetryIntakeAllocationRequest request,
            CancellationToken cancellationToken = default) =>
            inner.RetryAsync(request, cancellationToken);
    }

    private sealed class NoOpAllocateIntake : IAllocateIntake
    {
        public Task<IntakeAllocationResult?> AttemptAutomaticAsync(
            Guid receiptId,
            Guid evaluationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IntakeAllocationResult?>(null);

        public Task<IntakeAllocationResult> AttemptStaffCreateAsync(
            AcceptIntakeRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IntakeAllocationResult> RetryAsync(
            RetryIntakeAllocationRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RetainingWorkStore(
        IIntakeWorkStore inner,
        IServiceProvider services) : IIntakeWorkStore
    {
        public Task<IntakeStagedReceipt?> FindBySourceIdentityAsync(IntakeSourceIdentity sourceIdentity, CancellationToken cancellationToken) => inner.FindBySourceIdentityAsync(sourceIdentity, cancellationToken);
        public Task<ReceivedIntake> ReceiveAsync(IntakeStagedReceipt receipt, string operationKey, CancellationToken cancellationToken) => inner.ReceiveAsync(receipt, operationKey, cancellationToken);
        public Task<IntakeWorkItem?> ClaimDispatchAsync(DateTimeOffset nowUtc, TimeSpan leaseDuration, CancellationToken cancellationToken) => inner.ClaimDispatchAsync(nowUtc, leaseDuration, cancellationToken);
        public Task<IntakeWorkItem?> ClaimDispatchAsync(Guid stagedReceiptId, DateTimeOffset nowUtc, TimeSpan leaseDuration, CancellationToken cancellationToken) => inner.ClaimDispatchAsync(stagedReceiptId, nowUtc, leaseDuration, cancellationToken);
        public Task<IntakeWorkItem?> FindWorkItemAsync(Guid stagedReceiptId, CancellationToken cancellationToken) => inner.FindWorkItemAsync(stagedReceiptId, cancellationToken);
        public Task MarkDispatchedAsync(Guid workItemId, string leaseToken, DateTimeOffset nowUtc, CancellationToken cancellationToken) => inner.MarkDispatchedAsync(workItemId, leaseToken, nowUtc, cancellationToken);
        public Task ReleaseDispatchAsync(Guid workItemId, string leaseToken, DateTimeOffset dueAtUtc, CancellationToken cancellationToken) => inner.ReleaseDispatchAsync(workItemId, leaseToken, dueAtUtc, cancellationToken);
        public Task<(IntakeWorkItem WorkItem, IntakeStagedReceipt Receipt)?> ClaimProcessingAsync(Guid stagedReceiptId, DateTimeOffset nowUtc, TimeSpan leaseDuration, CancellationToken cancellationToken) => inner.ClaimProcessingAsync(stagedReceiptId, nowUtc, leaseDuration, cancellationToken);

        public async Task<IntakeEvaluationRevision> CompleteProcessingAsync(
            Guid workItemId,
            string leaseToken,
            Guid processedReceiptId,
            DateTimeOffset completedAtUtc,
            CancellationToken cancellationToken)
        {
            var result = await inner.CompleteProcessingAsync(workItemId, leaseToken, processedReceiptId, completedAtUtc, cancellationToken);
            await using var scope = services.CreateAsyncScope();
            var receipt = Assert.IsType<IntakeReceipt>(await scope.ServiceProvider
                .GetRequiredService<IIntakeReceiptQueries>()
                .GetAsync(processedReceiptId, cancellationToken));
            await AllocationTestData.SeedRetainedMessageForReceiptAsync(services, receipt);
            return result;
        }

        public Task<IntakeEvaluationRevision?> GetCompletedEvaluationAsync(Guid stagedReceiptId, CancellationToken cancellationToken) => inner.GetCompletedEvaluationAsync(stagedReceiptId, cancellationToken);
        public Task RetryProcessingAsync(Guid workItemId, string leaseToken, DateTimeOffset dueAtUtc, string failureCode, bool terminal, CancellationToken cancellationToken) => inner.RetryProcessingAsync(workItemId, leaseToken, dueAtUtc, failureCode, terminal, cancellationToken);
        public Task MarkPoisonedAsync(Guid stagedReceiptId, DateTimeOffset failedAtUtc, CancellationToken cancellationToken) => inner.MarkPoisonedAsync(stagedReceiptId, failedAtUtc, cancellationToken);
        public Task<int> RecoverInterruptedWorkAsync(DateTimeOffset nowUtc, DateTimeOffset staleDispatchedBeforeUtc, int maximumItems, CancellationToken cancellationToken) => inner.RecoverInterruptedWorkAsync(nowUtc, staleDispatchedBeforeUtc, maximumItems, cancellationToken);
        public Task ScheduleReevaluationAsync(Guid stagedReceiptId, DateTimeOffset dueAtUtc, CancellationToken cancellationToken) => inner.ScheduleReevaluationAsync(stagedReceiptId, dueAtUtc, cancellationToken);
        public Task<Guid?> FindStagedReceiptIdForReceiptAsync(Guid intakeReceiptId, CancellationToken cancellationToken) => inner.FindStagedReceiptIdForReceiptAsync(intakeReceiptId, cancellationToken);
    }

    private sealed class FirstAutomaticAllocationLost(IAllocateIntake inner) : IAllocateIntake
    {
        private int automaticCalls;

        public int AutomaticCalls => automaticCalls;

        public Task<IntakeAllocationResult?> AttemptAutomaticAsync(
            Guid receiptId,
            Guid evaluationId,
            CancellationToken cancellationToken = default)
        {
            // The first automatic attempt is lost before it persists anything,
            // mirroring a transient allocation-begin failure after completion.
            if (Interlocked.Increment(ref automaticCalls) == 1)
            {
                return Task.FromResult<IntakeAllocationResult?>(null);
            }

            return inner.AttemptAutomaticAsync(receiptId, evaluationId, cancellationToken);
        }

        public Task<IntakeAllocationResult> AttemptStaffCreateAsync(
            AcceptIntakeRequest request,
            CancellationToken cancellationToken = default) =>
            inner.AttemptStaffCreateAsync(request, cancellationToken);

        public Task<IntakeAllocationResult> RetryAsync(
            RetryIntakeAllocationRequest request,
            CancellationToken cancellationToken = default) =>
            inner.RetryAsync(request, cancellationToken);
    }

    [Fact]
    public async Task MissingTypeDisabledPrincipalAndExhaustedSequenceUseExactTaxonomy()
    {
        using var factory = new IntakeWebApplicationFactory();
        var missingType = await AllocationTestData.StoreDefinitiveReceiptAsync(
            factory.Services,
            null,
            "ANY");
        var disabledCode = "DISABLED";
        await AllocationTestData.SeedPrincipalAsync(factory.Services, disabledCode, isActive: false);
        var disabled = await AllocationTestData.StoreDefinitiveReceiptAsync(
            factory.Services,
            CaseType.Inspection,
            disabledCode);
        var exhaustedCode = "EXHAUSTED";
        var lineage = await AllocationTestData.SeedPrincipalAsync(factory.Services, exhaustedCode);
        await AllocationTestData.ExhaustSequenceAsync(factory.Services, lineage);
        var exhausted = await AllocationTestData.StoreDefinitiveReceiptAsync(
            factory.Services,
            CaseType.Inspection,
            exhaustedCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var allocate = scope.ServiceProvider.GetRequiredService<IAllocateIntake>();
        var missingTypeResult = await allocate.AttemptAutomaticAsync(missingType.Id, Guid.NewGuid());
        var disabledResult = await allocate.AttemptAutomaticAsync(disabled.Id, Guid.NewGuid());
        var exhaustedResult = await allocate.AttemptAutomaticAsync(exhausted.Id, Guid.NewGuid());

        Assert.Equal(IntakeAllocationFailureKind.CaseTypeUnavailable, missingTypeResult?.State.FailureKind);
        Assert.Equal(IntakeAllocationRecoveryDisposition.ManualReview, missingTypeResult?.State.RecoveryDisposition);
        Assert.Equal(IntakeAllocationFailureKind.PrincipalUnavailable, disabledResult?.State.FailureKind);
        Assert.Equal(IntakeAllocationRecoveryDisposition.RetryAfterCorrection, disabledResult?.State.RecoveryDisposition);
        Assert.Equal(IntakeAllocationFailureKind.SequenceExhausted, exhaustedResult?.State.FailureKind);
        Assert.Equal(IntakeAllocationRecoveryDisposition.Blocked, exhaustedResult?.State.RecoveryDisposition);
        Assert.Equal(0, await AllocationTestData.CountAsync(factory.Services, "Cases"));
    }

    [Fact]
    public async Task ConcurrencyAndUnexpectedFailuresUseExactTaxonomyAndOneStructuredLogEach()
    {
        using var factory = new IntakeWebApplicationFactory();
        await AllocationTestData.SeedPrincipalAsync(factory.Services, "FAULTS");
        var concurrencyReceipt = await AllocationTestData.StoreDefinitiveReceiptAsync(
            factory.Services,
            CaseType.Inspection,
            "FAULTS");
        var unexpectedReceipt = await AllocationTestData.StoreDefinitiveReceiptAsync(
            factory.Services,
            CaseType.Inspection,
            "FAULTS");
        var logs = new CapturingLogger<EfIntakeAllocationStore>();

        async Task<IntakeAllocationResult?> ExecuteAsync(Guid receiptId, Exception exception)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var store = new EfIntakeAllocationStore(
                scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>(),
                logs);
            return await new AllocateIntake(
                    scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>(),
                    store,
                    new ThrowingAcceptIntake(exception),
                    scope.ServiceProvider.GetRequiredService<TimeProvider>())
                .AttemptAutomaticAsync(receiptId, Guid.NewGuid());
        }

        var concurrency = await ExecuteAsync(
            concurrencyReceipt.Id,
            new IntakeVersionConflictException());
        var unexpected = await ExecuteAsync(
            unexpectedReceipt.Id,
            new InvalidOperationException("test-only acceptance fault"));

        Assert.Equal(IntakeAllocationFailureKind.ConcurrencyConflict, concurrency?.State.FailureKind);
        Assert.Equal(IntakeAllocationRecoveryDisposition.ReloadThenRetry, concurrency?.State.RecoveryDisposition);
        Assert.Equal(IntakeAllocationFailureKind.Unexpected, unexpected?.State.FailureKind);
        Assert.Equal(IntakeAllocationRecoveryDisposition.ReloadThenRetry, unexpected?.State.RecoveryDisposition);
        Assert.Equal(2, logs.Entries.Count(entry => entry.EventId.Id == 4721));
        Assert.All(
            logs.Entries.Where(entry => entry.EventId.Id == 4721),
            entry =>
            {
                Assert.Contains("ReceiptId", entry.Properties.Keys);
                Assert.Contains("CaseType", entry.Properties.Keys);
                Assert.Contains("FailureKind", entry.Properties.Keys);
                Assert.NotNull(entry.Exception);
            });
        Assert.Equal(2, await AllocationTestData.FailedAllocationEventCountAsync(factory.Services));
        Assert.Equal(0, await AllocationTestData.CountAsync(factory.Services, "Cases"));
    }

    [Fact]
    public async Task ConcurrentAutomaticAuditAndInspectionAllocationsForOnePrincipalBothSucceed()
    {
        // INTK-044: the live shape of 2026-08-27 — two automatic acceptances
        // for one principal overlapping under Serializable, one of them a
        // standalone Audit — must converge on two cases. Any allocation
        // failure is reported with the exception the store logged, which is
        // exactly what production could not keep.
        using var factory = new IntakeWebApplicationFactory();
        await AllocationTestData.SeedPrincipalAsync(factory.Services, "PAIRED");
        var logs = new CapturingLogger<EfIntakeAllocationStore>();
        const int rounds = 6;

        async Task<IntakeAllocationResult?> AllocateAsync(Guid receiptId)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            return await new AllocateIntake(
                    services.GetRequiredService<IIntakeReceiptQueries>(),
                    new EfIntakeAllocationStore(
                        services.GetRequiredService<IDbContextFactory<PegasusDbContext>>(),
                        logs),
                    services.GetRequiredService<IAcceptIntake>(),
                    services.GetRequiredService<TimeProvider>(),
                    services.GetRequiredService<IStandaloneAuditEvidenceQueries>())
                .AttemptAutomaticAsync(receiptId, Guid.NewGuid());
        }

        for (var round = 0; round < rounds; round++)
        {
            var inspection = await AllocationTestData.StoreDefinitiveReceiptAsync(
                factory.Services,
                CaseType.InspectionAndAudit,
                "PAIRED");
            var audit = await AllocationTestData.StoreDefinitiveReceiptAsync(
                factory.Services,
                CaseType.Audit,
                "PAIRED");
            await AllocationTestData.SeedAutomaticAuditEvidenceAsync(factory.Services, audit.Id);

            var inspectionAllocation = AllocateAsync(inspection.Id);
            var auditAllocation = AllocateAsync(audit.Id);
            var results = await Task.WhenAll(inspectionAllocation, auditAllocation);

            if (results.Any(result => result?.State.Status != IntakeAllocationProjectionStatus.Succeeded))
            {
                Assert.Fail(
                    $"Round {round}: "
                    + string.Join(", ", results.Select(result =>
                        $"{result?.State.Status}/{result?.State.FailureKind}/{result?.State.RecoveryDisposition}"))
                    + Environment.NewLine
                    + string.Join(
                        Environment.NewLine,
                        logs.Entries
                            .Where(entry => entry.EventId.Id == 4721)
                            .Select(entry => entry.Exception?.ToString())));
            }
            Assert.StartsWith("a.", (await auditAllocation)!.State.CaseReference, StringComparison.Ordinal);
        }

        Assert.Equal(rounds * 2, await AllocationTestData.CountAsync(factory.Services, "Cases"));
    }

    [Fact]
    public async Task DistinctParallelRetriesResolveToOneCaseAggregate()
    {
        // Convergence under contention, repeatedly — not merely no-throw once
        // (CASE-005). The per-receipt allocation lock makes the previously
        // deadlocking interleaving queue instead, so no round may fail or
        // fork a second aggregate.
        using var factory = new IntakeWebApplicationFactory();
        var actor = ActionActor.Staff(
            DevelopmentOfflineIdentity.AdministratorId,
            [StaffRole.Administrator]);
        string[] principals = ["PARA", "PARB", "PARC", "PARD", "PARE"];

        for (var round = 0; round < principals.Length; round++)
        {
            var receipt = await AllocationTestData.StoreDefinitiveReceiptAsync(
                factory.Services,
                CaseType.Inspection,
                principals[round]);
            IntakeAllocationResult? failed;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                failed = await scope.ServiceProvider.GetRequiredService<IAllocateIntake>()
                    .AttemptAutomaticAsync(receipt.Id, Guid.NewGuid());
            }
            await AllocationTestData.SeedPrincipalAsync(factory.Services, principals[round]);

            async Task<IntakeAllocationResult> RetryAsync(string key)
            {
                await using var scope = factory.Services.CreateAsyncScope();
                return await scope.ServiceProvider.GetRequiredService<IAllocateIntake>().RetryAsync(new(
                    receipt.Id,
                    receipt.Version,
                    failed!.State.AttemptId,
                    actor,
                    key,
                    "Parallel reasoned retry."));
            }

            var results = await Task.WhenAll(
                RetryAsync($"parallel-a:{Guid.NewGuid():N}"),
                RetryAsync($"parallel-b:{Guid.NewGuid():N}"));

            Assert.All(results, result =>
                Assert.Equal(IntakeAllocationProjectionStatus.Succeeded, result.State.Status));
            Assert.Single(results.Select(result => result.State.CaseId).Distinct());
            Assert.Equal(round + 1, await AllocationTestData.CountAsync(factory.Services, "Cases"));
            Assert.Equal(round + 1, await AllocationTestData.CountAsync(factory.Services, "CaseIntakeLinks"));
            Assert.Equal(round + 1, await AllocationTestData.CountAsync(factory.Services, "CaseSequences"));
            Assert.Equal(round + 1, await AllocationTestData.CountAsync(factory.Services, "ExternalWorkItems"));
        }
    }

    private sealed class AfterCommitAcceptIntake(IAcceptIntake inner, bool cancel) : IAcceptIntake
    {
        public async Task<CaseAcceptanceOutcome> ExecuteAsync(
            AcceptIntakeRequest request,
            CancellationToken cancellationToken)
        {
            _ = await inner.ExecuteAsync(request, cancellationToken);
            if (cancel)
            {
                throw new OperationCanceledException("test-only post-commit cancellation");
            }

            throw new InvalidOperationException("test-only post-commit observer failure");
        }
    }
}

internal sealed class ThrowingAcceptIntake(Exception exception) : IAcceptIntake
{
    public Task<CaseAcceptanceOutcome> ExecuteAsync(
        AcceptIntakeRequest request,
        CancellationToken cancellationToken) => Task.FromException<CaseAcceptanceOutcome>(exception);
}

[Trait("Category", "SqlServer")]
public sealed class IntakeAllocationConsumerTests
{
    [Fact]
    public async Task TriageIsIndependentOfSuccessfulAllocationReplayAndRetryInBothDirections()
    {
        using var triageFactory = new IntakeWebApplicationFactory(
            "Development",
            true,
            extractionPolicy: new ConsumerTriagePolicy(),
            useIntegrationTestAuthentication: true,
            initializeDevelopmentOffline: false,
            mailClassificationPolicy: new ConsumerTypedClassificationPolicy());
        await AllocationTestData.SeedPrincipalAsync(triageFactory.Services, "QDOS");
        var email = IntakeTestEvidence.CreateEmail(
            "triage-success-independence.eml",
            "QDOS instruction\r\nClaimant Name: Triage Success\r\nClaim Number: TRIAGE-SUCCESS\r\nVehicle Registration: AB12 CDE");
        var token = Guid.NewGuid().ToString("N");

        Guid first;
        await using (var scope = triageFactory.Services.CreateAsyncScope())
        {
            var source = new IntakeSource(
                email.FileName,
                email.MediaType,
                email.Content,
                scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow(),
                "system-worker:approved-inbox-poller",
                new(IntakeSourceChannel.Mailbox, token));
            first = await AllocationTestData.SubmitAndProcessAsync(scope.ServiceProvider, source, $"mailbox-submit:{Guid.NewGuid():N}");
            _ = await AllocationTestData.SubmitAndProcessAsync(scope.ServiceProvider, source, $"mailbox-submit:{Guid.NewGuid():N}");
        }
        var receiptId = first;
        await using (var scope = triageFactory.Services.CreateAsyncScope())
        {
            Assert.Single(await scope.ServiceProvider.GetRequiredService<ITriageQueries>()
                .ListAsync(null, CancellationToken.None));
            var receipt = Assert.IsType<IntakeReceipt>(
                await scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>()
                    .GetAsync(receiptId, CancellationToken.None));
            var succeeded = Assert.IsType<IntakeAllocationState>(receipt.AllocationState);
            Assert.True(
                succeeded.Status == IntakeAllocationProjectionStatus.Succeeded,
                $"Allocation={succeeded.Status}/{succeeded.FailureKind}; classification={receipt.MailClassificationDecision?.Outcome}/{receipt.MailClassificationDecision?.CaseType}; reason={succeeded.SafeReason}");
            var retry = await scope.ServiceProvider.GetRequiredService<IAllocateIntake>().RetryAsync(new(
                receipt.Id,
                receipt.Version,
                succeeded.AttemptId,
                ActionActor.Staff(
                    DevelopmentOfflineIdentity.AdministratorId,
                    [StaffRole.Administrator]),
                $"allocation-retry:{Guid.NewGuid():N}",
                "Successful allocation replay must not alter Triage."));
            Assert.Equal(IntakeAllocationProjectionStatus.Succeeded, retry.State.Status);
            Assert.True(retry.IsSuppressed);
            Assert.Single(await scope.ServiceProvider.GetRequiredService<ITriageQueries>()
                .ListAsync(null, CancellationToken.None));
        }
        Assert.Equal(1, await AllocationTestData.CountAsync(triageFactory.Services, "Cases"));
        Assert.Equal(1, await AllocationTestData.CountAsync(triageFactory.Services, "Triage"));

        using var nonTriageFactory = new IntakeWebApplicationFactory(
            "Development",
            true,
            useIntegrationTestAuthentication: true,
            initializeDevelopmentOffline: false,
            mailClassificationPolicy: new ConsumerTypedClassificationPolicy());
        await AllocationTestData.SeedPrincipalAsync(nonTriageFactory.Services, "QDOS");
        await using (var scope = nonTriageFactory.Services.CreateAsyncScope())
        {
            _ = await AllocationTestData.SubmitAndProcessAsync(scope.ServiceProvider,
                new(
                    email.FileName,
                    email.MediaType,
                    email.Content,
                    scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow(),
                    "system-worker:approved-inbox-poller",
                    new(IntakeSourceChannel.Mailbox, Guid.NewGuid().ToString("N"))),
                $"mailbox-submit:{Guid.NewGuid():N}");
        }
        Assert.Equal(1, await AllocationTestData.CountAsync(nonTriageFactory.Services, "Cases"));
        Assert.Equal(0, await AllocationTestData.CountAsync(nonTriageFactory.Services, "Triage"));
    }

    [Fact]
    public async Task ReceivedProjectionSeparatesProcessingDecisionFromFailedAllocation()
    {
        using var factory = new IntakeWebApplicationFactory();
        var receipt = await AllocationTestData.StoreDefinitiveReceiptAsync(
            factory.Services,
            CaseType.InspectionAndAudit,
            "ABSENT");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IAllocateIntake>()
                .AttemptAutomaticAsync(receipt.Id, Guid.NewGuid());
        }

        IntakeListPage page;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            page = await scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>()
                .ListAsync(null, 1, 25, CancellationToken.None);
        }

        var row = Assert.Single(page.Items, item => item.Id == receipt.Id);
        Assert.Equal(IntakeDecision.CaseCreated, row.Decision);
        Assert.Null(row.CaseId);
        Assert.Equal(
            IntakeAllocationProjectionStatus.FailedRecoverable,
            row.AllocationState?.Status);
        Assert.Equal(CaseType.InspectionAndAudit, row.AllocationState?.AttemptedCaseType);
        Assert.Equal("failed_recoverable", IntakeMcpTools.AllocationCode(row));

        await AllocationTestData.SeedRetainedMessageForReceiptAsync(factory.Services, receipt);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var retained = Assert.Single((await scope.ServiceProvider
                .GetRequiredService<IRetainedMailQueries>()
                .ListAsync(new(null, MailFolderScope.Inbox), 1, 25, CancellationToken.None)).Items);
            Assert.Equal(IntakeDecision.CaseCreated, retained.ProcessingOutcome);
            Assert.Null(retained.CaseId);
            Assert.Null(retained.CaseReference);
            Assert.Equal(IntakeAllocationProjectionStatus.FailedRecoverable, retained.AllocationState?.Status);

            var dashboard = scope.ServiceProvider.GetRequiredService<IDashboardQueries>();
            var stages = await dashboard.GetCaseStageCountsAsync(CancellationToken.None);
            Assert.Equal(new(0, 0, 0, 0), stages);
        }

        using var mcpFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Features:AutomationMcp", "true");
            builder.UseSetting("AutomationMcp:ClientId", AutomationClientId);
            builder.UseSetting("AutomationMcp:ClientSecret", AutomationClientSecret);
            builder.UseSetting("AutomationMcp:PublicOrigin", "http://localhost/");
            builder.UseSetting("AutomationMcp:RegistrationCacheSeconds", "0");
        });
        using var client = mcpFactory.CreateClient();
        var accessToken = await RequestAutomationTokenAsync(client);

        using (var response = await PostAutomationMcpAsync(
            client,
            accessToken,
            ToolCallPayload(
                1,
                "pegasus_intake_queue_list",
                new { page = 1, pageSize = 25 })))
        {
            using var document = await ReadJsonRpcAsync(response);
            var item = Assert.Single(
                document.RootElement.GetProperty("result").GetProperty("structuredContent")
                    .GetProperty("items").EnumerateArray());
            Assert.Equal(receipt.Id, item.GetProperty("receiptId").GetGuid());
            Assert.Equal("case_created", item.GetProperty("processingDecision").GetString());
            Assert.Equal("failed_recoverable", item.GetProperty("allocationStatus").GetString());
            Assert.False(item.TryGetProperty("caseId", out var failedCaseId) && failedCaseId.ValueKind != JsonValueKind.Null);
            Assert.False(item.TryGetProperty("caseReference", out var failedCaseReference) && failedCaseReference.ValueKind != JsonValueKind.Null);
        }

        await AllocationTestData.SeedPrincipalAsync(factory.Services, "ABSENT");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var current = Assert.IsType<IntakeReceipt>(
                await scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>()
                    .GetAsync(receipt.Id, CancellationToken.None));
            var failed = Assert.IsType<IntakeAllocationState>(current.AllocationState);
            var retry = await scope.ServiceProvider.GetRequiredService<IAllocateIntake>().RetryAsync(new(
                current.Id,
                current.Version,
                failed.AttemptId,
                ActionActor.Staff(
                    DevelopmentOfflineIdentity.AdministratorId,
                    [StaffRole.Administrator]),
                $"allocation-retry:{Guid.NewGuid():N}",
                "Principal was corrected for the MCP queue projection proof."));
            Assert.Equal(IntakeAllocationProjectionStatus.Succeeded, retry.State.Status);
        }

        using (var response = await PostAutomationMcpAsync(
            client,
            accessToken,
            ToolCallPayload(
                2,
                "pegasus_intake_queue_list",
                new { page = 1, pageSize = 25 })))
        {
            using var document = await ReadJsonRpcAsync(response);
            var item = Assert.Single(
                document.RootElement.GetProperty("result").GetProperty("structuredContent")
                    .GetProperty("items").EnumerateArray());
            Assert.Equal(receipt.Id, item.GetProperty("receiptId").GetGuid());
            Assert.Equal("case_created", item.GetProperty("processingDecision").GetString());
            Assert.Equal("case_created", item.GetProperty("allocationStatus").GetString());
            Assert.NotEqual(Guid.Empty, item.GetProperty("caseId").GetGuid());
            Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("caseReference").GetString()));
        }
    }

    private const string AutomationClientId = "pegasus-automation";
    private const string AutomationClientSecret = "integration-test-automation-secret-0123456789";

    private static async Task<string> RequestAutomationTokenAsync(HttpClient client)
    {
        using var response = await client.PostAsync(
            "/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = AutomationClientId,
                ["client_secret"] = AutomationClientSecret,
                ["scope"] = "automation.intake"
            }));
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"Token issuance failed: {body}");
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("The token response is missing access_token.");
    }

    private static async Task<HttpResponseMessage> PostAutomationMcpAsync(
        HttpClient client,
        string accessToken,
        string payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp");
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await client.SendAsync(request);
    }

    private static async Task<JsonDocument> ReadJsonRpcAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        if (response.Content.Headers.ContentType?.MediaType == "text/event-stream")
        {
            var data = body
                .Split('\n')
                .Select(line => line.TrimEnd('\r'))
                .Where(line => line.StartsWith("data:", StringComparison.Ordinal))
                .Select(line => line[5..].Trim())
                .First(line => line.Length > 0);
            return JsonDocument.Parse(data);
        }

        return JsonDocument.Parse(body);
    }

    private static string ToolCallPayload(int id, string tool, object arguments) =>
        JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id,
            method = "tools/call",
            @params = new
            {
                name = tool,
                arguments
            }
        });

    [Fact]
    public async Task QualifyingTriageRemainsOneAcrossAllocationFailureAndSourceReplay()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            extractionPolicy: new ConsumerTriagePolicy(),
            useIntegrationTestAuthentication: true,
            initializeDevelopmentOffline: false,
            mailClassificationPolicy: new ConsumerTypedClassificationPolicy());
        await AllocationTestData.DisableQdosAsync(factory.Services);
        var email = IntakeTestEvidence.CreateEmail(
            "triage-allocation-independence.eml",
            "QDOS instruction\r\nClaimant Name: Triage Claimant\r\nClaim Number: TRIAGE-ALLOC\r\nVehicle Registration: AB12 CDE");
        var token = Guid.NewGuid().ToString("N");

        Guid first;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var source = new IntakeSource(
                email.FileName,
                email.MediaType,
                email.Content,
                scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow(),
                "system-worker:approved-inbox-poller",
                new(IntakeSourceChannel.Mailbox, token));
            first = await AllocationTestData.SubmitAndProcessAsync(scope.ServiceProvider, source, $"mailbox-submit:{Guid.NewGuid():N}");
        }
        var receiptId = first;

        Guid failedReplay;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var source = new IntakeSource(
                email.FileName,
                email.MediaType,
                email.Content,
                scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow(),
                "system-worker:approved-inbox-poller",
                new(IntakeSourceChannel.Mailbox, token));
            failedReplay = await AllocationTestData.SubmitAndProcessAsync(scope.ServiceProvider, source, $"mailbox-submit:{Guid.NewGuid():N}");
        }
        Assert.Equal(receiptId, failedReplay);
        Assert.Equal(0, await AllocationTestData.CountAsync(factory.Services, "Cases"));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            Assert.Single(await scope.ServiceProvider.GetRequiredService<ITriageQueries>().ListAsync(null, CancellationToken.None));
        }

        IntakeReceipt receipt;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            receipt = Assert.IsType<IntakeReceipt>(
                await scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>()
                    .GetAsync(receiptId, CancellationToken.None));
            var failed = Assert.IsType<IntakeAllocationState>(receipt.AllocationState);
            Assert.Equal(IntakeAllocationFailureKind.PrincipalUnavailable, failed.FailureKind);
            Assert.Equal(IntakeAllocationProjectionStatus.FailedRecoverable, failed.Status);
            Assert.Single(await scope.ServiceProvider.GetRequiredService<ITriageQueries>().ListAsync(null, CancellationToken.None));
        }
        Assert.Equal(0, await AllocationTestData.CountAsync(factory.Services, "Cases"));

        await AllocationTestData.SeedPrincipalAsync(factory.Services, "QDOS");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            receipt = Assert.IsType<IntakeReceipt>(
                await scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>()
                    .GetAsync(receiptId, CancellationToken.None));
            var failed = Assert.IsType<IntakeAllocationState>(receipt.AllocationState);
            var retry = await scope.ServiceProvider.GetRequiredService<IAllocateIntake>().RetryAsync(new(
                receipt.Id,
                receipt.Version,
                failed.AttemptId,
                ActionActor.Staff(
                    DevelopmentOfflineIdentity.AdministratorId,
                    [StaffRole.Administrator]),
                $"allocation-retry:{Guid.NewGuid():N}",
                "Principal corrected after qualifying Triage allocation failure."));
            Assert.Equal(IntakeAllocationProjectionStatus.Succeeded, retry.State.Status);
            Assert.Single(await scope.ServiceProvider.GetRequiredService<ITriageQueries>().ListAsync(null, CancellationToken.None));
        }

        Guid replay;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var source = new IntakeSource(
                email.FileName,
                email.MediaType,
                email.Content,
                scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow(),
                "system-worker:approved-inbox-poller",
                new(IntakeSourceChannel.Mailbox, token));
            replay = await AllocationTestData.SubmitAndProcessAsync(scope.ServiceProvider, source, $"mailbox-submit:{Guid.NewGuid():N}");
        }
        Assert.Equal(receiptId, replay);
        Assert.Equal(1, await AllocationTestData.CountAsync(factory.Services, "Cases"));
        Assert.Equal(1, await AllocationTestData.CountAsync(factory.Services, "CaseIntakeLinks"));
        Assert.Equal(2, await AllocationTestData.CountAsync(factory.Services, "IntakeAllocationAttempts"));
        Assert.Equal(2, await AllocationTestData.AllocationEventCountAsync(factory.Services));
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM TriageHistory WHERE EventType = N'triage_created'"));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            Assert.Single(await scope.ServiceProvider.GetRequiredService<ITriageQueries>().ListAsync(null, CancellationToken.None));
        }
    }

    private sealed class ConsumerTriagePolicy : IInstructionExtractionPolicy
    {
        private readonly QdosInstructionExtractionPolicy inner = new();

        public string PrincipalCode => inner.PrincipalCode;

        public InstructionExtractionResult Extract(
            IntakeSourceReadResult readResult,
            DateTimeOffset processedAtUtc,
            EstablishedPrincipalContext principalContext)
        {
            var result = inner.Extract(readResult, processedAtUtc, principalContext);
            if (result.Applicability != InstructionPolicyApplicability.Applicable)
            {
                return result;
            }

            return result with
            {
                Evidence =
                [
                    .. result.Evidence,
                    new(
                        IntakeEvidenceSource.EmailBody,
                        IntakeEvidenceStrength.Strong,
                        IntakeEvidenceFinding.AcceptedTriageMatch,
                        "accepted-triage-allocation-independence",
                        "The repository test fixture represents an independently accepted Triage matcher result.",
                        "allocation-consumer-triage-matcher",
                        1)
                ]
            };
        }
    }
}

internal static class AllocationTestData
{
    /// <summary>
    /// Stages one source and drains it through the Worker path; the processed
    /// receipt id is what every caller reads next.
    /// </summary>
    public static async Task<Guid> SubmitAndProcessAsync(
        IServiceProvider services,
        IntakeSource source,
        string operationKey)
    {
        var received = await services.GetRequiredService<IIntakeSubmission>()
            .ExecuteAsync(source, operationKey);
        var evaluation = await IntakeWebDriver.DrainStagedAsync(services, received.StagedReceiptId);
        return evaluation.ProcessedReceiptId;
    }

    public static async Task PointCompletedWorkAtReceiptAsync(
        IServiceProvider services,
        Guid stagedReceiptId,
        Guid processedReceiptId)
    {
        await using var scope = services.CreateAsyncScope();
        await using var context = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        var work = await context.IntakeWorkItems.SingleAsync(
            item => item.StagedReceiptId == stagedReceiptId);
        Assert.Equal("completed", work.State);
        work.ProcessedReceiptId = processedReceiptId;
        await context.SaveChangesAsync();
    }

    private static readonly DateTimeOffset RecordedAtUtc =
        new(2031, 8, 11, 9, 15, 0, TimeSpan.Zero);

    public static async Task<IntakeReceipt> StoreDefinitiveReceiptAsync(
        IServiceProvider services,
        CaseType? caseType,
        string principalCode,
        MailRouteEvaluationResult? routeDecision = null,
        MailClassificationResult? classificationDecision = null,
        CaseMatchEvaluationResult? caseMatchDecision = null)
    {
        var token = Guid.NewGuid().ToString("N");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IIntakeReceiptStore>().StoreAsync(
            new(
                "retained-qdos-instruction.pdf",
                "application/pdf",
                100,
                hash,
                new(IntakeSourceChannel.Mailbox, token),
                RecordedAtUtc,
                RecordedAtUtc,
                "QDOS allocation recovery integration test",
                IntakeDecision.CaseCreated,
                "Eligible for case allocation.",
                [],
                [new(
                    "Vehicle registration",
                    "AB12CDE",
                    [new("AB12CDE", IntakeEvidenceSource.DocumentContent, "retained instruction")],
                    false,
                    false)],
                new(principalCode, null, null, "AB12CDE", null, null, null, null, null, null, null),
                [],
                null,
                null,
                "qdos-test-reader",
                "1",
                "qdos-test-policy",
                1,
                MailRouteDecision: routeDecision ?? new(
                    MailRouteDisposition.Accepted,
                    new(principalCode, MailRouteKind.DirectProvider, principalCode),
                    [],
                    "Accepted allocation test route.",
                    "allocation-test-route",
                    1,
                    [new($"instructions@{principalCode.ToLowerInvariant()}.example", "outer message")],
                    [],
                    new($"instructions@{principalCode.ToLowerInvariant()}.example", "outer message")),
                MailClassificationDecision: classificationDecision ?? MailClassificationResult.Classified(
                    MailCategory.Received(
                        ReceivedMailFamily.NewInstructionReceived,
                        caseType == CaseType.Audit ? "audit" : "inspection"),
                    [],
                    "Definitive QDOS instruction.",
                    "qdos_mail_classification",
                    QdosMailClassificationPolicy.Version,
                    caseType),
                CaseMatchDecision: caseMatchDecision),
            CancellationToken.None);
    }

    public static async Task<Guid> SeedPrincipalAsync(
        IServiceProvider services,
        string code,
        bool isActive = true)
    {
        await using var scope = services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        if (code == QdosPrincipal.Code && isActive)
        {
            var principal = await context.Principals.SingleAsync(item => item.Code == QdosPrincipal.Code);
            if (!principal.IsActive)
            {
                principal.IsActive = true;
                await context.SaveChangesAsync();
            }

            return (await SeededPrincipals.QdosAsync(context)).SequenceLineageId;
        }

        var organizationId = Guid.NewGuid();
        var lineageId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Organizations (Id, Name, Version) VALUES ({organizationId}, {$"Recovery provider {code}"}, {0L})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO PrincipalSequenceLineages (Id, CreatedAtUtc) VALUES ({lineageId}, {RecordedAtUtc})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO Principals
                (Id, OrganizationId, Code, SequenceLineageId, PredecessorId, SuccessorId, IsActive, Version)
            VALUES
                ({principalId}, {organizationId}, {code}, {lineageId}, NULL, NULL, {isActive}, {0L})
            """);
        return lineageId;
    }

    public static async Task DisableQdosAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        var principal = await context.Principals.SingleAsync(item => item.Code == QdosPrincipal.Code);
        principal.IsActive = false;
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Records automatic standalone-Audit evidence for a receipt in the exact
    /// shape the automatic route persists — system-worker literal record and
    /// a 64-character hexadecimal request hash — or acceptance refuses the
    /// audit. The original report is the receipt's first retained attachment
    /// when one arrived (custody then reads its real bytes); a receipt stored
    /// without assets gets one synthetic PDF attachment row instead.
    /// </summary>
    public static async Task<Guid> SeedAutomaticAuditEvidenceAsync(
        IServiceProvider services,
        Guid receiptId)
    {
        await using var scope = services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var assetId = await context.Set<IntakeAssetEntity>()
            .Where(asset => asset.IntakeReceiptId == receiptId && asset.Kind == "attachment")
            .Select(asset => (Guid?)asset.Id)
            .FirstOrDefaultAsync();
        if (assetId is null)
        {
            assetId = Guid.NewGuid();
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO IntakeAssets
                    (Id, IntakeReceiptId, SourceLabel, FileName, MediaType, Kind, Disposition, ContentLength, ContentHash, StorageKey)
                VALUES
                    ({assetId}, {receiptId}, {"outer message, attachment Bodyshopreport-V1.pdf"}, {"Bodyshopreport-V1.pdf"}, {"application/pdf"}, {"attachment"}, {"attachment"}, {100L}, {new string('c', 64)}, {$"test-audit-report/{receiptId:N}"})
                """);
        }

        var evidenceId = Guid.NewGuid();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO StandaloneAuditEvidence
                (Id, IntakeReceiptId, OriginalReportAssetId, Assessment, ConfirmedByKind, ConfirmedBySubjectId, ConfirmedByRolesJson, ConfirmedAtUtc, OperationKey, Reason, RequestHash, ResultingReceiptVersion)
            VALUES
                ({evidenceId}, {receiptId}, {assetId}, {"repairable"}, {"SystemWorker"}, {"system-worker:automatic-standalone-audit"}, {"[]"}, {RecordedAtUtc}, {$"standalone-audit-{evidenceId:N}"}, {"Retained original report evidence"}, {new string('b', 64)}, {0L})
            """);
        return evidenceId;
    }

    public static string CommandHash(
        IntakeAllocationAttemptKind kind,
        IntakeAllocationCommand command,
        ActionActor actor,
        string operationKey,
        string reason)
    {
        var material = JsonSerializer.Serialize(new
        {
            SchemaVersion = 1,
            Kind = kind.ToString(),
            Command = command,
            ActorKind = actor.Kind.ToString(),
            actor.SubjectId,
            Roles = actor.Roles.OrderBy(role => role).Select(role => role.ToString()),
            OperationKey = operationKey,
            Reason = reason
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)))
            .ToLowerInvariant();
    }

    public static async Task ExhaustSequenceAsync(IServiceProvider services, Guid lineageId)
    {
        await using var scope = services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CaseSequences (SequenceLineageId, Year, LastAllocatedSequence) VALUES ({lineageId}, {2031}, {999})");
    }

    public static async Task ChangePersistedClassificationCaseTypeAsync(
        IServiceProvider services,
        Guid receiptId,
        string caseType)
    {
        await using var scope = services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE IntakeMailClassificationDecisions SET CaseType = {caseType} WHERE IntakeReceiptId = {receiptId}");
    }

    public static async Task SeedRetainedMessageForReceiptAsync(
        IServiceProvider services,
        IntakeReceipt receipt)
    {
        var mailboxId = TestMailboxId.From("allocation-recovery");
        const string mailboxAddress = "allocation-recovery@example.invalid";
        await using (var scope = services.CreateAsyncScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            await using var context = await factory.CreateDbContextAsync();
            await TestMailboxId.EnsureApprovedAsync(
                context, "allocation-recovery", mailboxAddress, receipt.ReceivedAtUtc.AddDays(-1));
            context.ApprovedInboxPollStates.Add(new()
            {
                ApprovedMailboxId = mailboxId,
                MailboxAddress = mailboxAddress,
                ScopeFingerprint = new string('A', 64),
                ActivatedAtUtc = receipt.ReceivedAtUtc.AddDays(-1),
                DueAtUtc = receipt.ReceivedAtUtc,
                LastCompletedAtUtc = receipt.ReceivedAtUtc
            });
            await context.SaveChangesAsync();
        }

        await using (var scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<EfRetainedMailboxMessageStore>()
                .RetainAsync(new(
                    mailboxId,
                    mailboxAddress,
                    $"message-{receipt.Id:N}",
                    receipt.SourceIdentity.ExternalReceiptToken,
                    receipt.ReceivedAtUtc,
                    receipt.SourceLength,
                    receipt.SourceHash,
                    new(
                        "inbox",
                        $"conversation-{receipt.Id:N}",
                        $"<{receipt.Id:N}@example.invalid>",
                        "sender@example.invalid",
                        "Retained sender",
                        ["intake@example.invalid"],
                        [],
                        "Retained allocation recovery",
                        "Retained allocation recovery fixture.",
                        [],
                        IsRead: false),
                    receipt.ReceivedAtUtc),
                    CancellationToken.None);
        }
    }

    public static async Task<int> CountAsync(IServiceProvider services, string table)
    {
        await using var scope = services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        return table switch
        {
            "IntakeAllocationAttempts" => await context.IntakeAllocationAttempts.CountAsync(),
            "Cases" => await context.Cases.CountAsync(),
            "CaseIntakeLinks" => await context.CaseIntakeLinks.CountAsync(),
            "CaseSequences" => await context.CaseSequences.CountAsync(),
            "CaseWorkflows" => await context.CaseWorkflows.CountAsync(),
            "ExternalWorkItems" => await context.ExternalWorkItems.CountAsync(),
            "IntakeManualAssociations" => await context.IntakeManualAssociations.CountAsync(),
            "Triage" => await context.Triage.CountAsync(),
            _ => throw new ArgumentOutOfRangeException(nameof(table))
        };
    }

    public static async Task<int> AllocationEventCountAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        return await context.IntakeReceiptEvents.CountAsync(
            item => item.EventType == "intake_allocation_succeeded"
                || item.EventType == "intake_allocation_failed");
    }

    public static async Task<int> FailedAllocationEventCountAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        return await context.IntakeReceiptEvents.CountAsync(
            item => item.EventType == "intake_allocation_failed");
    }

    public static async Task<string> CaseTypeAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        return await context.Cases.Select(item => item.Type).SingleAsync();
    }
}


internal sealed class ConsumerTypedClassificationPolicy : IMailClassificationPolicy
{
    public string WorkProviderCode => "QDOS";

    public string PolicyKey => "qdos-allocation-recovery-test-classification";

    public int PolicyVersion => 1;

    public MailClassificationResult Classify(IntakeSourceReadResult readResult) =>
        MailClassificationResult.Classified(
            MailCategory.Received(ReceivedMailFamily.NewInstructionReceived, "inspection"),
            [],
            "Deterministic typed classification for the SQL allocation caller fixture.",
            PolicyKey,
            PolicyVersion,
            CaseType.Inspection);
}

internal sealed class CapturingLogger<T> : ILogger<T>
{
    public List<CapturedLog> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var properties = state is IEnumerable<KeyValuePair<string, object?>> pairs
            ? pairs.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal)
            : new Dictionary<string, object?>(StringComparer.Ordinal);
        Entries.Add(new(logLevel, eventId, formatter(state, exception), properties, exception));
    }
}

internal sealed record CapturedLog(
    LogLevel Level,
    EventId EventId,
    string Message,
    IReadOnlyDictionary<string, object?> Properties,
    Exception? Exception);
