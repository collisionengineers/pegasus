using Pegasus.Core.Identity;
using Pegasus.Core.Documents;
using Pegasus.Core.Operations;
using Pegasus.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class OperationsPersistenceTests
{
    private static readonly DateTimeOffset FixedUtcNow =
        new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task AutomationProjectionFiltersChannelsOrdersNewestAndLimitsInSql()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var newestAutomationId = await SeedAutomationReceiptsAsync(database, 55);
        await SeedNonAutomationReceiptAsync(database, "manual_upload");
        await SeedNonAutomationReceiptAsync(database, "mailbox");

        await using var scope = database.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IAutomationIntakeProjectionStore>();
        var result = await store.GetRecentAsync(50, CancellationToken.None);

        Assert.Equal(50, result.Length);
        Assert.Equal(newestAutomationId, result[0].ReceiptId);
        Assert.All(result, item => Assert.StartsWith("automation-", item.SourceFileName, StringComparison.Ordinal));
        Assert.DoesNotContain(result, item => item.SourceFileName is "manual_upload" or "mailbox");
    }

    [Fact]
    public async Task AutomationProjectionUsesTheActualCaseIntakeAssociation()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var (receiptId, caseId, caseReference) = await SeedAssociatedAutomationAsync(database);

        await using var scope = database.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IAutomationIntakeProjectionStore>();
        var item = Assert.Single(await store.GetRecentAsync(50, CancellationToken.None));

        Assert.Equal(receiptId, item.ReceiptId);
        Assert.Equal(caseId, item.CaseId);
        Assert.Equal(caseReference, item.CaseReference);
        Assert.Equal("succeeded", item.AllocationState);
    }

    [Fact]
    public async Task RequestProjectionReturnsOnlyActiveLinksAndRetryableFailures()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var ids = await SeedRequestOperationsAsync(database);

        await using var scope = database.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IRequestOperationsProjectionStore>();
        var result = await store.GetAsync(100, FixedUtcNow, CancellationToken.None);

        Assert.Contains(result.Items, item => item.Id == ids.ActiveUploadId);
        Assert.Contains(result.Items, item => item.Id == ids.RetryableFailureId);
        Assert.DoesNotContain(result.Items, item => item.Id == ids.ExpiredUploadId);
        Assert.DoesNotContain(result.Items, item => item.Id == ids.BoxRequestId);
        Assert.DoesNotContain(result.Items, item => item.Id == ids.LeasedFailureId);
        Assert.All(result.Items, item =>
            Assert.Contains(item.Kind, new[] { RequestOperationKind.PegasusUploadLink, RequestOperationKind.ExternalWork }));
    }

    private static async Task<Guid> SeedAutomationReceiptsAsync(
        LocalDbTestDatabase database,
        int count)
    {
        await using var context = await database.CreateContextAsync();
        var ids = Enumerable.Range(0, count)
            .Select(_ => Guid.NewGuid())
            .ToArray();
        for (var index = 0; index < ids.Length; index++)
        {
            context.IntakeReceipts.Add(Receipt(ids[index], "automation", $"automation-{index:00}.pdf", FixedUtcNow.AddMinutes(index)));
        }

        await context.SaveChangesAsync();
        return ids[^1];
    }

    private static async Task SeedNonAutomationReceiptAsync(
        LocalDbTestDatabase database,
        string channel)
    {
        await using var context = await database.CreateContextAsync();
        context.IntakeReceipts.Add(Receipt(Guid.NewGuid(), channel, channel, FixedUtcNow.AddDays(1)));
        await context.SaveChangesAsync();
    }

    private static async Task<(Guid ReceiptId, Guid CaseId, string CaseReference)> SeedAssociatedAutomationAsync(
        LocalDbTestDatabase database)
    {
        await using var context = await database.CreateContextAsync();
        var organizationId = Guid.NewGuid();
        var lineageId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var custodyWorkId = Guid.NewGuid();
        var reference = "OPS-ASSOCIATED";
        context.AddRange(
            new OrganizationEntity { Id = organizationId, Name = "Operations test", Version = 0 },
            new PrincipalSequenceLineageEntity { Id = lineageId, CreatedAtUtc = FixedUtcNow },
            new PrincipalEntity
            {
                Id = principalId,
                OrganizationId = organizationId,
                Code = "OPS",
                SequenceLineageId = lineageId,
                IsActive = true,
                Version = 0
            },
            Receipt(receiptId, "automation", "associated.pdf", FixedUtcNow),
            new CaseEntity
            {
                Id = caseId,
                PrincipalId = principalId,
                SequenceLineageId = lineageId,
                Year = 2031,
                Sequence = 1,
                Reference = reference,
                Type = "Inspection",
                InitialState = "NotReady",
                CustodyState = "Pending",
                OriginIntakeReceiptId = receiptId,
                CreatedAtUtc = FixedUtcNow,
                Version = 1,
                ConcurrencyToken = Guid.NewGuid()
            },
            new CaseWorkflowEntity
            {
                CaseId = caseId,
                State = "NotReady",
                Version = 1,
                ConcurrencyToken = Guid.NewGuid()
            },
            new ExternalWorkItemEntity
            {
                Id = custodyWorkId,
                CaseId = caseId,
                Kind = "case_custody",
                OperationKey = "operations-custody",
                State = "completed",
                AttemptCount = 1,
                DueAtUtc = FixedUtcNow,
                CompletedAtUtc = FixedUtcNow
            },
            new CaseIntakeLinkEntity
            {
                IntakeReceiptId = receiptId,
                CaseId = caseId,
                CustodyWorkId = custodyWorkId,
                LinkedAtUtc = FixedUtcNow,
                ActorKind = nameof(ActorKind.Automation),
                ActorSubjectId = "operations-test",
                ActorRolesJson = "[]",
                Reason = "Operations test",
                OperationKey = "operations-link"
            },
            new IntakeAllocationAttemptEntity
            {
                Id = Guid.NewGuid(),
                IntakeReceiptId = receiptId,
                AttemptNumber = 1,
                Kind = "automatic",
                Status = "succeeded",
                ExpectedReceiptVersion = 0,
                PrincipalCode = "OPS",
                InstructionComplete = true,
                ImagesComplete = true,
                ActorKind = nameof(ActorKind.Automation),
                ActorSubjectId = "operations-test",
                ActorRolesJson = "[]",
                OperationKey = "operations-allocation",
                CommandHash = new string('a', 64),
                Reason = "Operations test",
                StartedAtUtc = FixedUtcNow,
                CompletedAtUtc = FixedUtcNow,
                CaseId = caseId,
                CaseReference = reference
            });
        await context.SaveChangesAsync();
        return (receiptId, caseId, reference);
    }

    private static async Task<(Guid ActiveUploadId, Guid ExpiredUploadId, Guid BoxRequestId, Guid RetryableFailureId, Guid LeasedFailureId)> SeedRequestOperationsAsync(
        LocalDbTestDatabase database)
    {
        await using var context = await database.CreateContextAsync();
        var organizationId = Guid.NewGuid();
        var lineageId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var activeUploadId = Guid.NewGuid();
        var expiredUploadId = Guid.NewGuid();
        var boxRequestId = Guid.NewGuid();
        var retryableFailureId = Guid.NewGuid();
        var leasedFailureId = Guid.NewGuid();
        context.AddRange(
            new OrganizationEntity { Id = organizationId, Name = "Request operations test", Version = 0 },
            new PrincipalSequenceLineageEntity { Id = lineageId, CreatedAtUtc = FixedUtcNow },
            new PrincipalEntity
            {
                Id = principalId,
                OrganizationId = organizationId,
                Code = "OPS",
                SequenceLineageId = lineageId,
                IsActive = true,
                Version = 0
            },
            Receipt(receiptId, "manual_upload", "request-origin.pdf", FixedUtcNow),
            new CaseEntity
            {
                Id = caseId,
                PrincipalId = principalId,
                SequenceLineageId = lineageId,
                Year = 2031,
                Sequence = 2,
                Reference = "OPS-REQUEST",
                Type = "Inspection",
                InitialState = "NotReady",
                CustodyState = "Pending",
                OriginIntakeReceiptId = receiptId,
                CreatedAtUtc = FixedUtcNow,
                Version = 1,
                ConcurrencyToken = Guid.NewGuid()
            },
            new CaseWorkflowEntity
            {
                CaseId = caseId,
                State = "NotReady",
                Version = 1,
                ConcurrencyToken = Guid.NewGuid()
            },
            Upload(activeUploadId, caseId, RequestUploadStatus.Active, FixedUtcNow.AddHours(1)),
            Upload(expiredUploadId, caseId, RequestUploadStatus.Active, FixedUtcNow.AddMinutes(-1)),
            new BoxFileRequestEntity
            {
                Id = boxRequestId,
                CaseId = caseId,
                Status = Pegasus.Core.Documents.BoxFileRequestStatus.Active,
                CreatedAtUtc = FixedUtcNow,
                ExpiresAtUtc = FixedUtcNow.AddHours(1),
                Version = 1,
                CreateOperationKey = "box-request",
                LinkTokenDigest = new string('b', 64)
            },
            Work(retryableFailureId, caseId, null),
            Work(leasedFailureId, caseId, FixedUtcNow.AddHours(1)));
        await context.SaveChangesAsync();
        return (activeUploadId, expiredUploadId, boxRequestId, retryableFailureId, leasedFailureId);
    }

    private static IntakeReceiptEntity Receipt(Guid id, string channel, string fileName, DateTimeOffset receivedAt) => new()
    {
        Id = id,
        SourceFileName = fileName,
        MediaType = "application/pdf",
        SourceLength = 1,
        SourceHash = new string('0', 64),
        SourceChannel = channel,
        ExternalReceiptToken = $"operations:{id:N}",
        ReceivedAtUtc = receivedAt,
        ProcessedAtUtc = receivedAt,
        SourceReaderKey = "operations-test",
        SourceReaderVersion = "1",
        Version = 0,
        Decision = "case_created",
        DecisionReason = "Operations test",
        EvidenceJson = "[]",
        FieldsJson = "[]",
        OcrCandidatesJson = "[]"
    };

    private static RequestUploadLinkEntity Upload(Guid id, Guid caseId, RequestUploadStatus status, DateTimeOffset expiresAt) => new()
    {
        Id = id,
        CaseId = caseId,
        TokenDigest = $"{id:N}{new string('u', 32)}",
        Status = status,
        CreatedAtUtc = FixedUtcNow.AddMinutes(-5),
        ExpiresAtUtc = expiresAt,
        AcceptedFileCount = 1,
        AcceptedByteCount = 10,
        LimitsVersion = "test",
        Version = 1,
        CreateOperationKey = $"upload:{id:N}"
    };

    private static ExternalWorkItemEntity Work(Guid id, Guid caseId, DateTimeOffset? leaseExpiry) => new()
    {
        Id = id,
        CaseId = caseId,
        Kind = "case_custody",
        OperationKey = $"work:{id:N}",
        State = "failed",
        AttemptCount = 2,
        DueAtUtc = FixedUtcNow,
        LeaseToken = leaseExpiry is null ? null : "leased",
        LeaseExpiresAtUtc = leaseExpiry,
        FailureCode = "failed",
        FailureReason = "Operations test failure"
    };
}
