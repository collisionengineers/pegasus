using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// INTK-011: reproduces the production race directly against LocalDB — two
/// members of one image group processed by independent, concurrent durable
/// work items must never split (one registered, the sibling stranded at
/// <c>needs_sorting</c> through the instruction fallback). Also covers the
/// reconciliation sweep that recovers a straggler exactly like the production
/// evidence (a receipt that lost its registration race and was left with
/// neither an Image Intake nor an Unidentified reference) without manual SQL.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class GroupedImageIntakeConcurrencyTests
{
    private const int Iterations = 12;

    [Fact]
    public async Task ConcurrentGroupMembersNeverSplitAcrossRepeatedRuns()
    {
        var clock = new AdvanceableTimeProvider(new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero));
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            timeProvider: clock,
            recognitionEngine: new FakeVrmRecognitionEngine("AB12CDE"));
        using var client = IntakeWebDriver.CreateClient(factory);

        for (var iteration = 0; iteration < Iterations; iteration++)
        {
            var form = await IntakeWebDriver.GetUploadFormTokensAsync(client);
            var upload = await IntakeWebDriver.PostUploadManyAsync(
                client,
                form.AntiforgeryToken,
                form.ExternalReceiptToken,
                [
                    ($"overview-{iteration}.png", "image/png", TinyPngBytes),
                    ($"close-up-{iteration}.png", "image/png", TinyPngBytes)
                ]);
            Assert.Equal(System.Net.HttpStatusCode.Redirect, upload.StatusCode);
            var groupId = Guid.Parse(upload.Location!.OriginalString.Split('/').Last());

            Guid[] stagedReceiptIds;
            await using (var lookupScope = factory.Services.CreateAsyncScope())
            {
                var groups = lookupScope.ServiceProvider.GetRequiredService<IIntakeSubmissionGroupStore>();
                var group = await groups.GetAsync(groupId)
                    ?? throw new InvalidOperationException("The submission group was not persisted.");
                stagedReceiptIds = group.Members
                    .OrderBy(member => member.Ordinal)
                    .Select(member => member.StagedReceiptId)
                    .ToArray();
            }
            Assert.Equal(2, stagedReceiptIds.Length);

            // ProcessQueuedIntake.ExecuteAsync only claims work already
            // dispatched/dispatching/processing; move both members past
            // pending->dispatched first (no-op enqueue -- the actual
            // processing below is what races) so the concurrent race is the
            // ClaimProcessingAsync/CompleteProcessingAsync/automation path
            // itself, exactly like two independent queue-trigger deliveries.
            await using (var dispatchOnlyScope = factory.Services.CreateAsyncScope())
            {
                var dispatchOnly = new DispatchPendingIntakeWork(
                    dispatchOnlyScope.ServiceProvider.GetRequiredService<IIntakeWorkStore>(),
                    new IntakeWebDriver.NoOpIntakeWorkEnqueuer(),
                    clock);
                await dispatchOnly.ExecuteAsync(10);
            }

            // The race itself: both members' durable work processed by
            // independent, concurrent scopes -- mirrors two independent
            // Worker queue-trigger invocations racing the same group.
            await Task.WhenAll(stagedReceiptIds.Select(
                id => ProcessWithDeadlockRetryAsync(factory.Services, id)));

            // A member whose own outcome is still pending after the race
            // is recovered one of two ways: if it was deferred (this
            // ticket's fix -- GroupPending, work item left Completed),
            // calling ProcessQueuedIntake again directly is safe and cheap
            // (the replay branch, same mechanism the reconciliation sweep
            // uses). If instead a genuine transient fault hit the
            // evaluation phase itself (pre-existing, unrelated retry
            // machinery), the work item is RetryScheduled with a future
            // due time and needs a real dispatch pass once that time
            // arrives -- advance the clock and dispatch to cover it.
            for (var round = 0; round < 6 && !await AllMembersRegisteredAsync(stagedReceiptIds); round++)
            {
                await Task.WhenAll(stagedReceiptIds.Select(
                    id => ProcessWithDeadlockRetryAsync(factory.Services, id)));
                clock.Advance(TimeSpan.FromMinutes(3));
                await using var redriveScope = factory.Services.CreateAsyncScope();
                var redriveDispatcher = new DispatchPendingIntakeWork(
                    redriveScope.ServiceProvider.GetRequiredService<IIntakeWorkStore>(),
                    new ImmediateEnqueuer(factory.Services),
                    clock);
                await redriveDispatcher.ExecuteAsync(10);
            }

            await using var assertScope = factory.Services.CreateAsyncScope();
            var receiptQueries = assertScope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>();
            var imageIntakeQueries = assertScope.ServiceProvider.GetRequiredService<IImageIntakeQueries>();
            var assertWorkStore = assertScope.ServiceProvider.GetRequiredService<IIntakeWorkStore>();
            var receipts = new List<IntakeReceipt>();
            foreach (var stagedReceiptId in stagedReceiptIds)
            {
                var completed = await assertWorkStore.GetCompletedEvaluationAsync(stagedReceiptId, CancellationToken.None);
                if (completed is null)
                {
                    var workItem = await assertWorkStore.FindWorkItemAsync(stagedReceiptId, CancellationToken.None);
                    Assert.Fail(
                        $"iteration={iteration} stagedReceiptId={stagedReceiptId} has no completed evaluation. " +
                        $"workItem: State={workItem?.State} Attempts={workItem?.AttemptCount} Due={workItem?.DueAtUtc} " +
                        $"Lease={workItem?.LeaseToken} LeaseExpires={workItem?.LeaseExpiresAtUtc} FailureCode={workItem?.FailureCode}");
                }

                var receipt = await receiptQueries.GetAsync(completed!.ProcessedReceiptId, CancellationToken.None);
                Assert.NotNull(receipt);
                receipts.Add(receipt!);
            }

            var references = new HashSet<string>(StringComparer.Ordinal);
            foreach (var receipt in receipts)
            {
                // The one atomic outcome, on every run: registered, never a
                // stranded needs_sorting/instruction-fallback escape.
                Assert.Equal(IntakeDecision.ImageIntakeRegistered, receipt.Decision);
                var detail = await imageIntakeQueries.GetByOriginReceiptAsync(receipt.Id, CancellationToken.None);
                Assert.NotNull(detail);
                Assert.Equal("AB12CDE", detail!.Record.NormalizedVehicleRegistration);
                references.Add(detail.Record.ImageIntakeReference);
            }
            Assert.Equal(2, references.Count);
        }

        async Task<bool> AllMembersRegisteredAsync(IReadOnlyList<Guid> stagedReceiptIds)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var workStore = scope.ServiceProvider.GetRequiredService<IIntakeWorkStore>();
            var receiptQueries = scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>();
            foreach (var stagedReceiptId in stagedReceiptIds)
            {
                var completed = await workStore.GetCompletedEvaluationAsync(stagedReceiptId, CancellationToken.None);
                if (completed is null)
                {
                    return false;
                }

                var receipt = await receiptQueries.GetAsync(completed.ProcessedReceiptId, CancellationToken.None);
                if (receipt?.Decision != IntakeDecision.ImageIntakeRegistered)
                {
                    return false;
                }
            }

            return true;
        }
    }

    [Fact]
    public async Task ReconciliationRecoversAStrandedGroupMember()
    {
        // Mirrors the production shape exactly: one member registers its own
        // Image Intake normally, the sibling ends up stuck at needs_sorting
        // carrying the generic instruction-fallback reason, with no Image
        // Intake and no Unidentified registration -- the pre-fix shape the
        // production evidence showed. That exact combination is reproduced
        // directly against the store (rather than via the nondeterministic
        // race) so this test asserts the reconciliation mechanism in
        // isolation: given a stranded member, does the product's own
        // mechanism -- not manual SQL -- recover it.
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine("AB12CDE"));
        using var client = IntakeWebDriver.CreateClient(factory);
        var form = await IntakeWebDriver.GetUploadFormTokensAsync(client);
        var upload = await IntakeWebDriver.PostUploadManyAsync(
            client,
            form.AntiforgeryToken,
            form.ExternalReceiptToken,
            [
                ("overview.png", "image/png", TinyPngBytes),
                ("close-up.png", "image/png", TinyPngBytes)
            ]);
        var groupId = Guid.Parse(upload.Location!.OriginalString.Split('/').Last());

        Guid[] stagedReceiptIds;
        await using (var lookupScope = factory.Services.CreateAsyncScope())
        {
            var groups = lookupScope.ServiceProvider.GetRequiredService<IIntakeSubmissionGroupStore>();
            var group = await groups.GetAsync(groupId)
                ?? throw new InvalidOperationException("The submission group was not persisted.");
            stagedReceiptIds = group.Members
                .OrderBy(member => member.Ordinal)
                .Select(member => member.StagedReceiptId)
                .ToArray();
        }

        // Move both members past pending->dispatched (ProcessQueuedIntake
        // only claims already-dispatched work), then process them
        // sequentially (no contention): both register successfully, exactly
        // like a clean, non-racing group.
        await using (var dispatchOnlyScope = factory.Services.CreateAsyncScope())
        {
            var dispatchOnly = new DispatchPendingIntakeWork(
                dispatchOnlyScope.ServiceProvider.GetRequiredService<IIntakeWorkStore>(),
                new IntakeWebDriver.NoOpIntakeWorkEnqueuer(),
                dispatchOnlyScope.ServiceProvider.GetRequiredService<TimeProvider>());
            await dispatchOnly.ExecuteAsync(10);
        }

        Guid strandedReceiptId;
        await using (var firstScope = factory.Services.CreateAsyncScope())
        {
            var processor = ActivatorUtilities.CreateInstance<ProcessQueuedIntake>(firstScope.ServiceProvider);
            await processor.ExecuteAsync(stagedReceiptIds[0]);
        }

        await using (var secondScope = factory.Services.CreateAsyncScope())
        {
            var processor = ActivatorUtilities.CreateInstance<ProcessQueuedIntake>(secondScope.ServiceProvider);
            var workStore = secondScope.ServiceProvider.GetRequiredService<IIntakeWorkStore>();
            await processor.ExecuteAsync(stagedReceiptIds[1]);
            var completed = await workStore.GetCompletedEvaluationAsync(stagedReceiptIds[1], CancellationToken.None);
            strandedReceiptId = completed!.ProcessedReceiptId;
        }

        // Force the second member back to the exact pre-fix stranded shape:
        // undo its registration and its decision, leaving it with neither an
        // Image Intake nor an Unidentified reference -- reachable only by
        // its receipt, like the production straggler.
        await using (var contextFactoryScope = factory.Services.CreateAsyncScope())
        {
            var contextFactory = contextFactoryScope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            var imageIntake = await context.ImageIntakes.SingleAsync(item => item.OriginReceiptId == strandedReceiptId);
            context.ImageIntakes.Remove(imageIntake);
            // The registration operation key is unique across
            // IntakeMutationHistory; undoing the registration means undoing
            // its history row too, or a fresh registration attempt collides
            // on that same operation key.
            var registrationOperationKey = $"image-intake-register:{strandedReceiptId:N}";
            var mutationHistory = await context.IntakeMutationHistory
                .Where(item => item.OperationKey == registrationOperationKey)
                .ToArrayAsync();
            context.IntakeMutationHistory.RemoveRange(mutationHistory);
            var receipt = await context.IntakeReceipts.SingleAsync(item => item.Id == strandedReceiptId);
            receipt.Decision = "needs_sorting";
            receipt.DecisionReason =
                "No accepted intake route established the principal for automatic case creation.";
            receipt.FailureCode = null;
            receipt.FailureReason = null;
            receipt.Version++;
            await context.SaveChangesAsync();
        }

        await using (var strandedScope = factory.Services.CreateAsyncScope())
        {
            var receiptQueries = strandedScope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>();
            var strandedReceipt = await receiptQueries.GetAsync(strandedReceiptId, CancellationToken.None);
            Assert.Equal(IntakeDecision.NeedsSorting, strandedReceipt!.Decision);
            var strandedImageIntakeQueries = strandedScope.ServiceProvider.GetRequiredService<IImageIntakeQueries>();
            Assert.Null(await strandedImageIntakeQueries.GetByOriginReceiptAsync(strandedReceiptId, CancellationToken.None));
        }

        // Reconcile: this is the product's own mechanism recovering the
        // straggler -- no manual SQL. It re-drives the member's already-
        // completed work item synchronously (the safe replay branch), so no
        // separate redispatch step is needed afterwards.
        ReconcileGroupedImageIntakeResult reconcileResult;
        await using (var reconcileScope = factory.Services.CreateAsyncScope())
        {
            var processor = ActivatorUtilities.CreateInstance<ProcessQueuedIntake>(reconcileScope.ServiceProvider);
            var reconciler = ActivatorUtilities.CreateInstance<ReconcileGroupedImageIntake>(
                reconcileScope.ServiceProvider,
                (IProcessQueuedIntake)processor);
            reconcileResult = await reconciler.ExecuteAsync(50);
        }
        Assert.True(
            reconcileResult.Candidates >= 1,
            $"Reconciliation must find the stranded group member as a candidate. {reconcileResult}");
        Assert.True(
            reconcileResult.Retried >= 1,
            $"Reconciliation must re-drive the stranded member within its retry window. {reconcileResult}");

        await using var finalScope = factory.Services.CreateAsyncScope();
        var finalReceiptQueries = finalScope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>();
        var finalImageIntakeQueries = finalScope.ServiceProvider.GetRequiredService<IImageIntakeQueries>();
        var finalReceipt = await finalReceiptQueries.GetAsync(strandedReceiptId, CancellationToken.None);
        Assert.NotNull(finalReceipt);
        Assert.Equal(IntakeDecision.ImageIntakeRegistered, finalReceipt!.Decision);
        var finalDetail = await finalImageIntakeQueries.GetByOriginReceiptAsync(strandedReceiptId, CancellationToken.None);
        Assert.NotNull(finalDetail);
        Assert.Equal("AB12CDE", finalDetail!.Record.NormalizedVehicleRegistration);
    }

    private static readonly byte[] TinyPngBytes = Convert.FromBase64String(MultiFormatFixture.TinyPngBase64);

    /// <summary>
    /// Processes one staged receipt in its own scope, standing in for a Worker
    /// queue-trigger delivery. Racing deliveries legitimately deadlock against
    /// each other (SQL error 1205) -- production retries such a delivery from
    /// the queue, so the test retries here rather than failing the race it is
    /// deliberately provoking.
    /// </summary>
    private static async Task ProcessWithDeadlockRetryAsync(
        IServiceProvider services,
        Guid stagedReceiptId,
        CancellationToken cancellationToken = default)
    {
        const int maximumAttempts = 5;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var scope = services.CreateAsyncScope();
                var processor = ActivatorUtilities.CreateInstance<ProcessQueuedIntake>(scope.ServiceProvider);
                await processor.ExecuteAsync(stagedReceiptId, cancellationToken);
                return;
            }
            catch (SqlException exception) when (exception.Number == 1205 && attempt < maximumAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), cancellationToken);
            }
        }
    }
    private sealed class AdvanceableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset now = utcNow;

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan by) => now = now.Add(by);
    }

    private sealed class ImmediateEnqueuer(IServiceProvider services) : IIntakeWorkEnqueuer
    {
        public Task EnqueueAsync(Guid stagedReceiptId, CancellationToken cancellationToken) =>
            ProcessWithDeadlockRetryAsync(services, stagedReceiptId, cancellationToken);
    }
}
