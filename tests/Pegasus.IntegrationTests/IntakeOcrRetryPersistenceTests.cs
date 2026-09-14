using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Custody;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Retry OCR is real (Phase 6c): the Intake log offers it only when the last OCR
/// attempt failed and lists the item under OCR failed; the Core command, on the
/// real store, re-queues only the external work (Web has no UPDATE on the
/// operation), records the action with its reason, replays by operation key, and
/// is no longer offered; the Worker's store then resumes the operation once.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class IntakeOcrRetryPersistenceTests
{
    [Fact]
    public async Task AFailedOcrAttemptIsRetriedOnceThroughTheWorkerQueueAndThenNoLongerOffered()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var upload = await IntakeWebDriver.UploadAndProcessAsync(
            factory,
            client,
            "scan.png",
            "image/png",
            Convert.FromBase64String(MultiFormatFixture.TinyPngBase64),
            Guid.NewGuid().ToString("N"));
        var receiptId = IntakeWebDriver.ReceiptId(upload);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var receipts = services.GetRequiredService<IIntakeReceiptQueries>();
        var receipt = Assert.IsType<IntakeReceipt>(await receipts.GetAsync(receiptId, CancellationToken.None));
        var asset = Assert.IsType<IntakeAssetRecord>(IntakeFileIdentity.SourceAsset(receipt));
        var operationId = Guid.NewGuid();
        var dueAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5);
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            context.Set<IntakeOcrOperationEntity>().Add(new()
            {
                Id = operationId,
                IntakeAssetId = asset.Id,
                SourceSha256 = asset.ContentHash,
                QualifiedPagesJson = $$"""{"version":4,"intakeReceiptId":"{{receiptId:D}}","pages":[1],"attemptCount":6,"submitAttemptedAtUtc":null,"submittedAtUtc":null,"sourceContentLength":{{asset.ContentLength}}}""",
                OperationKey = $"intake-ocr:{operationId:N}",
                State = nameof(IntakeOcrState.Failed),
                LastError = "ocr_pages_missing: The provider returned no output for page(s) 1.",
                Version = 3,
                ConcurrencyToken = Guid.NewGuid()
            });
            context.Set<ExternalWorkItemEntity>().Add(new()
            {
                Id = operationId,
                Kind = ExternalWorkKinds.IntakeOcr,
                OperationKey = $"intake-ocr:{operationId:N}",
                State = ExternalWorkStatePersistence.Failed,
                AttemptCount = 6,
                DueAtUtc = dueAtUtc,
                FailureCode = "ocr_pages_missing",
                FailureReason = "The provider returned no output for page(s) 1."
            });
            await context.SaveChangesAsync();
        }

        var administrator = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        var log = services.GetRequiredService<IListIntakeLog>();
        Assert.True((await log.GetAsync(administrator, receiptId, CancellationToken.None))!.Actions.CanRetryOcr);
        var ocrFailed = await log.ExecuteAsync(administrator, new IntakeLogFilter(Outcome: IntakeLogOutcome.OcrFailed), 1, CancellationToken.None);
        Assert.Contains(ocrFailed.Items, row => row.ReceiptId == receiptId && row.Outcome == IntakeLogOutcome.OcrFailed);

        var retry = services.GetRequiredService<IRetryIntakeOcr>();
        var key = Guid.NewGuid().ToString("N");
        await retry.ExecuteAsync(new RetryIntakeOcrRequest(receiptId, receipt.Version, administrator, key, "The provider was unavailable."));
        // The same key again is the same action.
        await retry.ExecuteAsync(new RetryIntakeOcrRequest(receiptId, receipt.Version, administrator, key, "The provider was unavailable."));

        DateTimeOffset requeuedDueAtUtc;
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            var operation = await context.Set<IntakeOcrOperationEntity>().AsNoTracking().SingleAsync(item => item.Id == operationId);
            var work = await context.Set<ExternalWorkItemEntity>().AsNoTracking().SingleAsync(item => item.Id == operationId);
            requeuedDueAtUtc = work.DueAtUtc;
            // Web never changes the operation (it holds no UPDATE on it): the
            // request is the re-queued work item alone.
            Assert.Equal(nameof(IntakeOcrState.Failed), operation.State);
            Assert.Equal(3, operation.Version);
            // Pending and due: the Worker's external-work dispatch claims it.
            Assert.Equal(ExternalWorkStatePersistence.Pending, work.State);
            Assert.Equal(0, work.AttemptCount);
            Assert.Null(work.FailureCode);
            Assert.True(work.DueAtUtc > dueAtUtc);
            var history = await context.IntakeMutationHistory.AsNoTracking()
                .Where(item => item.IntakeReceiptId == receiptId && item.EventType == "intake_ocr_retry_queued")
                .ToListAsync();
            var recorded = Assert.Single(history);
            Assert.Equal("The provider was unavailable.", recorded.Reason);
            Assert.Equal(administrator.SubjectId, recorded.ActorSubjectId);
        }

        // Claimed at the application clock's own due time, not the test host's.
        var claim = await services.GetRequiredService<IExternalWorkStore>().ClaimDispatchAsync(
            operationId,
            requeuedDueAtUtc.AddMinutes(1),
            TimeSpan.FromMinutes(1),
            CancellationToken.None);
        Assert.NotNull(claim);

        // A re-queued retry is no longer offered, even before the Worker runs it.
        Assert.False((await log.GetAsync(administrator, receiptId, CancellationToken.None))!.Actions.CanRetryOcr);

        // The Worker resumes the requested retry: the operation returns to
        // Pending with a fresh attempt budget, once.
        var ocrStore = new EfIntakeOcrOperationStore(contextFactory);
        var resumed = Assert.IsType<IntakeOcrOperation>(
            await ocrStore.ResumeRequestedRetryAsync(operationId, 3, CancellationToken.None));
        Assert.Equal(IntakeOcrState.Pending, resumed.State);
        Assert.Equal(0, resumed.AttemptCount);
        Assert.Null(resumed.LastError);
        Assert.Equal(4, resumed.Version);
        Assert.Null(await ocrStore.ResumeRequestedRetryAsync(operationId, 4, CancellationToken.None));

        // The attempt is no longer a failure: nothing to retry, and not offered.
        var current = Assert.IsType<IntakeReceipt>(await receipts.GetAsync(receiptId, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => retry.ExecuteAsync(
            new RetryIntakeOcrRequest(receiptId, current.Version, administrator, Guid.NewGuid().ToString("N"), "Again.")));
        Assert.False((await log.GetAsync(administrator, receiptId, CancellationToken.None))!.Actions.CanRetryOcr);
    }
}
