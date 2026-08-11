using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Triage;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class QdosAllocationRecoveryTests
{
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
        Assert.Equal(1, await AllocationTestData.CountAsync(factory.Services, "CaseIntakeLinks"));
        Assert.Equal(2, await AllocationTestData.CountAsync(
            factory.Services,
            "IntakeAllocationAttempts"));
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
    public async Task DistinctParallelRetriesResolveToOneCaseAggregate()
    {
        using var factory = new IntakeWebApplicationFactory();
        var receipt = await AllocationTestData.StoreDefinitiveReceiptAsync(
            factory.Services,
            CaseType.Inspection,
            "PARALLEL");
        IntakeAllocationResult? failed;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            failed = await scope.ServiceProvider.GetRequiredService<IAllocateIntake>()
                .AttemptAutomaticAsync(receipt.Id, Guid.NewGuid());
        }
        await AllocationTestData.SeedPrincipalAsync(factory.Services, "PARALLEL");
        var actor = ActionActor.Staff(
            DevelopmentOfflineIdentity.AdministratorId,
            [StaffRole.Administrator]);

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
        Assert.Equal(1, await AllocationTestData.CountAsync(factory.Services, "Cases"));
        Assert.Equal(1, await AllocationTestData.CountAsync(factory.Services, "CaseIntakeLinks"));
        Assert.Equal(1, await AllocationTestData.CountAsync(factory.Services, "CaseSequences"));
        Assert.Equal(1, await AllocationTestData.CountAsync(factory.Services, "ExternalWorkItems"));
    }
}

[Trait("Category", "SqlServer")]
public sealed class IntakeAllocationConsumerTests
{
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
    }

    [Fact]
    public async Task QualifyingTriageRemainsOneAcrossAllocationFailureAndSourceReplay()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            extractionPolicy: new ConsumerTriagePolicy());
        using var client = IntakeWebDriver.CreateClient(factory);
        var email = IntakeTestEvidence.CreateEmail(
            "triage-allocation-independence.eml",
            "QDOS instruction\r\nClaimant Name: Triage Claimant\r\nClaim Number: TRIAGE-ALLOC\r\nVehicle Registration: AB12 CDE");
        var token = Guid.NewGuid().ToString("N");

        var first = await IntakeWebDriver.UploadAndProcessAsync(
            factory,
            client,
            email.FileName,
            email.MediaType,
            email.Content,
            token);
        await IntakeWebDriver.UploadAndProcessAsync(
            factory,
            client,
            email.FileName,
            email.MediaType,
            email.Content,
            token);
        var receiptId = IntakeWebDriver.ReceiptId(first);

        IntakeReceipt receipt;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            receipt = Assert.IsType<IntakeReceipt>(
                await scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>()
                    .GetAsync(receiptId, CancellationToken.None));
            Assert.Equal(IntakeAllocationFailureKind.CaseTypeUnavailable, receipt.AllocationState?.FailureKind);
            Assert.Single(await scope.ServiceProvider.GetRequiredService<ITriageQueries>()
                .ListAsync(null, CancellationToken.None));
        }
        Assert.Equal(0, await AllocationTestData.CountAsync(factory.Services, "Cases"));

        Assert.Equal(0, await AllocationTestData.CountAsync(factory.Services, "Cases"));
    }

    private sealed class ConsumerTriagePolicy : IInstructionExtractionPolicy
    {
        private readonly QdosInstructionExtractionPolicy inner = new();

        public InstructionExtractionResult Extract(
            IntakeSourceReadResult readResult,
            DateTimeOffset processedAtUtc)
        {
            var result = inner.Extract(readResult, processedAtUtc);
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
    private static readonly DateTimeOffset RecordedAtUtc =
        new(2031, 8, 11, 9, 15, 0, TimeSpan.Zero);

    public static async Task<IntakeReceipt> StoreDefinitiveReceiptAsync(
        IServiceProvider services,
        CaseType? caseType,
        string principalCode)
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
                MailClassificationDecision: MailClassificationResult.Classified(
                    MailCategory.Received(
                        ReceivedMailFamily.NewInstructionReceived,
                        caseType == CaseType.Audit ? "audit" : "inspection"),
                    [],
                    "Definitive QDOS instruction.",
                    "qdos_mail_classification",
                    QdosMailClassificationPolicy.Version,
                    caseType)),
            CancellationToken.None);
    }

    public static async Task<Guid> SeedPrincipalAsync(
        IServiceProvider services,
        string code,
        bool isActive = true)
    {
        var organizationId = Guid.NewGuid();
        var lineageId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        await using var scope = services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
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

    public static async Task ExhaustSequenceAsync(IServiceProvider services, Guid lineageId)
    {
        await using var scope = services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CaseSequences (SequenceLineageId, Year, LastAllocatedSequence) VALUES ({lineageId}, {2031}, {999})");
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

    public static async Task<string> CaseTypeAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        return await context.Cases.Select(item => item.Type).SingleAsync();
    }
}
