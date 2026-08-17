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
        Assert.DoesNotContain(result.Items, item => item.Id == ids.LeasedFailureId);
        Assert.All(result.Items, item =>
            Assert.Contains(item.Kind, new[] { RequestOperationKind.PegasusUploadLink, RequestOperationKind.ExternalWork }));
    }

    private static async Task<(Guid ActiveUploadId, Guid ExpiredUploadId, Guid RetryableFailureId, Guid LeasedFailureId)> SeedRequestOperationsAsync(
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
            Work(retryableFailureId, caseId, null),
            Work(leasedFailureId, caseId, FixedUtcNow.AddHours(1)));
        await context.SaveChangesAsync();
        return (activeUploadId, expiredUploadId, retryableFailureId, leasedFailureId);
    }

    private static IntakeReceiptEntity Receipt(
        Guid id,
        string channel,
        string fileName,
        DateTimeOffset receivedAt) => new()
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
