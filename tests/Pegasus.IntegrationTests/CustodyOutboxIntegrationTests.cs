using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Cases;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Core.Intake;
using Pegasus.Core.Triage;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

public sealed class CustodyOutboxIntegrationTests
{
    private static readonly DateTimeOffset FixedUtcNow =
        new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task AcceptedOfflineCaseRecoversDispatchLeaseAndRetainsExactSourceReplaySafely()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var accepted = await AcceptDirectSourceAsync(scope.ServiceProvider);
        var store = scope.ServiceProvider.GetRequiredService<IExternalWorkStore>();
        var abandoned = Assert.IsType<ExternalWorkDispatchClaim>(
            await store.ClaimDispatchAsync(
                FixedUtcNow,
                TimeSpan.FromMinutes(1),
                CancellationToken.None));
        Assert.Equal(accepted.Outcome.CustodyWorkId, abandoned.WorkItemId);

        var timeProvider = new MutableTimeProvider(FixedUtcNow.AddMinutes(2));
        var queue = new RecordingExternalWorkQueue();
        var dispatcher = new DispatchPendingExternalWork(store, queue, timeProvider);

        Assert.Equal(1, await dispatcher.ExecuteAsync(10, CancellationToken.None));
        Assert.Equal([accepted.Outcome.CustodyWorkId], queue.WorkItemIds);
        Assert.Equal(0, await dispatcher.ExecuteAsync(10, CancellationToken.None));

        var processor = scope.ServiceProvider.GetRequiredService<IProcessQueuedCustody>();
        await processor.ExecuteAsync(accepted.Outcome.CustodyWorkId, CancellationToken.None);
        await processor.ExecuteAsync(accepted.Outcome.CustodyWorkId, CancellationToken.None);
        await new ReconcilePoisonedExternalWork(store, timeProvider)
            .ExecuteAsync(accepted.Outcome.CustodyWorkId, CancellationToken.None);

        Assert.Equal(
            "completed",
            await ReadExternalWorkStateAsync(scope.ServiceProvider, accepted.Outcome.CustodyWorkId));
        Assert.Equal(
            "confirmed",
            await ReadCaseCustodyStateAsync(scope.ServiceProvider, accepted.Outcome.Identity.CaseId));
        Assert.Equal(
            1,
            await CountCaseHistoryAsync(
                scope.ServiceProvider,
                accepted.Outcome.Identity.CaseId,
                "custody_confirmed"));
        Assert.Equal(
            0,
            await CountCaseHistoryAsync(
                scope.ServiceProvider,
                accepted.Outcome.Identity.CaseId,
                "custody_failed"));

        var expectedHash = Convert.ToHexString(SHA256.HashData(accepted.Content)).ToLowerInvariant();
        var retainedPath = Path.Combine(
            factory.ArtifactDirectory,
            "custody",
            "cases",
            accepted.Outcome.Identity.CaseId.ToString("N"),
            "documents",
            accepted.ReceiptId.ToString("N"),
            expectedHash,
            "content");
        Assert.Equal(accepted.Content, await File.ReadAllBytesAsync(retainedPath));
    }

    [Fact]
    public async Task QueuedCustodyResolvesTheProcessedReceiptThroughItsStagedLineage()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var accepted = await AcceptQueuedSourceAsync(scope.ServiceProvider);

        var processor = scope.ServiceProvider.GetRequiredService<IProcessQueuedCustody>();
        await processor.ExecuteAsync(accepted.Outcome.CustodyWorkId, CancellationToken.None);

        Assert.Equal(
            "confirmed",
            await ReadCaseCustodyStateAsync(scope.ServiceProvider, accepted.Outcome.Identity.CaseId));
        var expectedHash = Convert.ToHexString(SHA256.HashData(accepted.Content)).ToLowerInvariant();
        var retainedPath = Path.Combine(
            factory.ArtifactDirectory,
            "custody",
            "cases",
            accepted.Outcome.Identity.CaseId.ToString("N"),
            "documents",
            accepted.ReceiptId.ToString("N"),
            expectedHash,
            "content");
        Assert.Equal(accepted.Content, await File.ReadAllBytesAsync(retainedPath));
    }

    [Fact]
    public async Task PoisonedCustodyIsTerminallyRecordedWithoutRedispatchOrDuplicateHistory()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var accepted = await AcceptDirectSourceAsync(scope.ServiceProvider);
        var store = scope.ServiceProvider.GetRequiredService<IExternalWorkStore>();
        var reconciliation = new ReconcilePoisonedExternalWork(
            store,
            new MutableTimeProvider(FixedUtcNow));
        var initialQueue = new RecordingExternalWorkQueue();
        Assert.Equal(
            1,
            await new DispatchPendingExternalWork(
                    store,
                    initialQueue,
                    new MutableTimeProvider(FixedUtcNow))
                .ExecuteAsync(10, CancellationToken.None));
        Assert.Equal([accepted.Outcome.CustodyWorkId], initialQueue.WorkItemIds);


        await reconciliation.ExecuteAsync(accepted.Outcome.CustodyWorkId, CancellationToken.None);
        await reconciliation.ExecuteAsync(accepted.Outcome.CustodyWorkId, CancellationToken.None);

        var replayQueue = new RecordingExternalWorkQueue();
        Assert.Equal(
            0,
            await new DispatchPendingExternalWork(
                    store,
                    replayQueue,
                    new MutableTimeProvider(FixedUtcNow.AddMinutes(10)))
                .ExecuteAsync(10, CancellationToken.None));
        Assert.Empty(replayQueue.WorkItemIds);
        Assert.Equal(
            "failed",
            await ReadExternalWorkStateAsync(scope.ServiceProvider, accepted.Outcome.CustodyWorkId));
        Assert.Equal(
            "failed",
            await ReadCaseCustodyStateAsync(scope.ServiceProvider, accepted.Outcome.Identity.CaseId));
        Assert.Equal(
            1,
            await CountCaseHistoryAsync(
                scope.ServiceProvider,
                accepted.Outcome.Identity.CaseId,
                "custody_failed"));
    }

    [Fact]
    public async Task LogicallyRemovedVersionCannotBeDownloadedOrExported()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var accepted = await AcceptDirectSourceAsync(scope.ServiceProvider);
        var content = "retained document"u8.ToArray();
        var added = await scope.ServiceProvider.GetRequiredService<IAddCaseDocument>()
            .ExecuteAsync(
                new(
                    accepted.Outcome.Identity.CaseId,
                    "evidence.txt",
                    "text/plain",
                    content,
                    DocumentSemanticRole.Other,
                    DocumentSource.StaffUpload,
                    $"staff:{Guid.NewGuid():N}",
                    "custody-test",
                    $"document-add:{Guid.NewGuid():N}",
                    null),
                CancellationToken.None);

        await using (var download = Assert.IsType<DocumentDownload>(
                         await scope.ServiceProvider.GetRequiredService<IDownloadCaseDocument>()
                             .ExecuteAsync(
                                 new(
                                     accepted.Outcome.Identity.CaseId,
                                     added.Occurrence.Id,
                                     added.Version.Id,
                                     "custody-test"),
                                 CancellationToken.None)))
        {
            using var copy = new MemoryStream();
            await download.Content.CopyToAsync(copy);
            Assert.Equal(content, copy.ToArray());
        }

        await using (var export = await scope.ServiceProvider.GetRequiredService<IExportCaseDocuments>()
                         .ExecuteAsync(
                             new(
                                 accepted.Outcome.Identity.CaseId,
                                 [new(added.Occurrence.Id, added.Version.Id)],
                                 "custody-test",
                                 $"document-export:{Guid.NewGuid():N}"),
                             CancellationToken.None))
        {
            Assert.Equal(added.Version.Id, Assert.Single(export.Manifest).VersionId);
        }

        await scope.ServiceProvider.GetRequiredService<ILogicallyRemoveDocument>()
            .ExecuteAsync(
                new(
                    accepted.Outcome.Identity.CaseId,
                    added.Occurrence.Id,
                    "custody-test",
                    "Removed from the active case file.",
                    $"document-remove:{Guid.NewGuid():N}",
                    1),
                CancellationToken.None);

        Assert.Null(await scope.ServiceProvider.GetRequiredService<IDownloadCaseDocument>()
            .ExecuteAsync(
                new(
                    accepted.Outcome.Identity.CaseId,
                    added.Occurrence.Id,
                    added.Version.Id,
                    "custody-test"),
                CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<IExportCaseDocuments>()
                .ExecuteAsync(
                    new(
                        accepted.Outcome.Identity.CaseId,
                        [new(added.Occurrence.Id, added.Version.Id)],
                        "custody-test",
                        $"document-export-removed:{Guid.NewGuid():N}"),
                    CancellationToken.None));
    }

    private static async Task<AcceptedSource> AcceptDirectSourceAsync(IServiceProvider services)
    {
        var source = CreateSource();
        var receipt = await services.GetRequiredService<ProcessIntake>()
            .ExecuteAsync(source.Source, CancellationToken.None);
        Assert.Equal(IntakeDecision.DraftReady, receipt.Decision);
        return new(
            await AcceptAsync(services, receipt.Id),
            receipt.Id,
            source.Content);
    }

    private static async Task<AcceptedSource> AcceptQueuedSourceAsync(IServiceProvider services)
    {
        var source = CreateSource();
        var received = await services.GetRequiredService<ReceiveIntake>()
            .ExecuteAsync(
                source.Source,
                $"intake-receive:{Guid.NewGuid():N}",
                CancellationToken.None);
        var store = services.GetRequiredService<IIntakeWorkStore>();
        var dispatchClaim = Assert.IsType<IntakeWorkItem>(
            await store.ClaimDispatchAsync(
                FixedUtcNow,
                TimeSpan.FromMinutes(1),
                CancellationToken.None));
        await store.MarkDispatchedAsync(
            dispatchClaim.Id,
            Assert.IsType<string>(dispatchClaim.LeaseToken),
            FixedUtcNow,
            CancellationToken.None);
        await new ProcessQueuedIntake(
                store,
                services.GetRequiredService<IIntakeArtifactStore>(),
                services.GetRequiredService<ProcessIntake>(),
                services.GetRequiredService<IIntakeReceiptQueries>(),
                services.GetRequiredService<ICreateTriageFromIntake>(),
                services.GetRequiredService<TimeProvider>())
            .ExecuteAsync(received.StagedReceiptId, CancellationToken.None);
        var receipt = Assert.IsType<IntakeReceipt>(
            await services.GetRequiredService<IIntakeReceiptStore>()
                .FindBySourceIdentityAsync(source.Source.SourceIdentity, CancellationToken.None));
        Assert.Equal(IntakeDecision.DraftReady, receipt.Decision);
        return new(
            await AcceptAsync(services, receipt.Id),
            receipt.Id,
            source.Content);
    }

    private static async Task<CaseAcceptanceOutcome> AcceptAsync(
        IServiceProvider services,
        Guid receiptId)
    {
        var principalCode = await SeedPrincipalAsync(services);
        return await services.GetRequiredService<IAcceptIntake>()
            .ExecuteAsync(
                new(
                    receiptId,
                    0,
                    "custody-test",
                    $"case-accept:{Guid.NewGuid():N}",
                    CaseType.Inspection,
                    principalCode,
                    new(true, true, true, true)),
                CancellationToken.None);
    }

    private static SourceFixture CreateSource()
    {
        var email = OfflineAcceptanceTests.CreateEmail(
            $"custody-{Guid.NewGuid():N}.eml",
            "QDOS instruction\r\nClaimant Name: Custody Test\r\nClaim Number: CUS-001\r\nVehicle Registration: AB12 CDE");
        var identity = new IntakeSourceIdentity(
            IntakeSourceChannel.ManualUpload,
            $"custody-source:{Guid.NewGuid():N}");
        return new(
            new(
                email.FileName,
                email.MediaType,
                email.Content,
                FixedUtcNow,
                "custody-test",
                identity),
            email.Content);
    }

    private static async Task<string> SeedPrincipalAsync(IServiceProvider services)
    {
        var organizationId = Guid.NewGuid();
        var lineageId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var principalCode = $"TST{principalId:N}"[..15].ToUpperInvariant();
        const string organizationName = "Custody test organization";
        const string organizationRole = "work_provider";
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"Organizations\" (\"Id\", \"Name\", \"Version\") VALUES ({organizationId}, {organizationName}, {0L})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"OrganizationRoles\" (\"OrganizationId\", \"Role\") VALUES ({organizationId}, {organizationRole})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"PrincipalSequenceLineages\" (\"Id\", \"CreatedAtUtc\") VALUES ({lineageId}, {FixedUtcNow})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"Principals\" (\"Id\", \"OrganizationId\", \"Code\", \"SequenceLineageId\", \"PredecessorId\", \"SuccessorId\", \"IsActive\", \"Version\") VALUES ({principalId}, {organizationId}, {principalCode}, {lineageId}, NULL, NULL, {true}, {0L})");
        await transaction.CommitAsync();
        return principalCode;
    }

    private static async Task<string> ReadExternalWorkStateAsync(
        IServiceProvider services,
        Guid workItemId)
    {
        await using var context = await services
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        return await context.Database.SqlQuery<string>(
                $"SELECT \"State\" AS \"Value\" FROM \"ExternalWorkItems\" WHERE \"Id\" = {workItemId}")
            .SingleAsync();
    }

    private static async Task<string> ReadCaseCustodyStateAsync(
        IServiceProvider services,
        Guid caseId)
    {
        await using var context = await services
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        return await context.Database.SqlQuery<string>(
                $"SELECT \"CustodyState\" AS \"Value\" FROM \"Cases\" WHERE \"Id\" = {caseId}")
            .SingleAsync();
    }

    private static async Task<int> CountCaseHistoryAsync(
        IServiceProvider services,
        Guid caseId,
        string eventType)
    {
        await using var context = await services
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        return await context.Database.SqlQuery<int>(
                $"SELECT COUNT(*) AS \"Value\" FROM \"CaseHistory\" WHERE \"CaseId\" = {caseId} AND \"EventType\" = {eventType}")
            .SingleAsync();
    }

    private sealed class RecordingExternalWorkQueue : IExternalWorkEnqueuer
    {
        public List<Guid> WorkItemIds { get; } = [];

        public Task EnqueueAsync(Guid workItemId, CancellationToken cancellationToken)
        {
            WorkItemIds.Add(workItemId);
            return Task.CompletedTask;
        }
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed record SourceFixture(IntakeSource Source, byte[] Content);

    private sealed record AcceptedSource(
        CaseAcceptanceOutcome Outcome,
        Guid ReceiptId,
        byte[] Content);
}
