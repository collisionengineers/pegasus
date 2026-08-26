using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class RecoveryTests
{
    [Fact]
    [Trait("Category", "QdosAlphaAcceptance")]
    public async Task DurableIntakeReplayAndExpiredDispatchLeaseRecoverIdempotently()
    {
        var clock = new AdjustableTimeProvider(new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero));
        using var factory = new IntakeWebApplicationFactory(clock);
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var store = services.GetRequiredService<IIntakeWorkStore>();
        var receiver = new ReceiveIntake(
            services.GetRequiredService<IIntakeArtifactStore>(),
            store,
            clock,
            new CommittedWorkPublisherDouble());
        var source = CreateSource("lease-recovery");

        var first = await receiver.ExecuteAsync(source, "qdos-alpha:lease-recovery");
        var replay = await receiver.ExecuteAsync(source, "qdos-alpha:lease-recovery");

        Assert.False(first.IsDuplicate);
        Assert.True(replay.IsDuplicate);
        Assert.Equal(first.StagedReceiptId, replay.StagedReceiptId);
        var claimed = await store.ClaimDispatchAsync(
            clock.GetUtcNow(),
            TimeSpan.FromMinutes(1),
            CancellationToken.None);
        Assert.NotNull(claimed);
        var claimedWork = claimed!;
        Assert.Equal(first.StagedReceiptId, claimedWork.StagedReceiptId);
        Assert.Equal(IntakeWorkState.Dispatching, claimedWork.State);

        clock.Advance(TimeSpan.FromMinutes(1));
        var reconciler = new ReconcileStagedArtifacts(
            store,
            services.GetRequiredService<IStagedArtifactAuthority>(),
            services.GetRequiredService<IIntakeArtifactStore>(),
            clock);
        Assert.Equal(1, (await reconciler.ExecuteAsync(10)).RecoveredWorkItems);
        Assert.Equal(0, (await reconciler.ExecuteAsync(10)).RecoveredWorkItems);

        var recovered = await store.ClaimDispatchAsync(
            clock.GetUtcNow(),
            TimeSpan.FromMinutes(1),
            CancellationToken.None);
        Assert.NotNull(recovered);
        var recoveredWork = recovered!;
        Assert.Equal(first.StagedReceiptId, recoveredWork.StagedReceiptId);
        Assert.NotEqual(claimedWork.LeaseToken, recoveredWork.LeaseToken);
    }

    [Fact]
    public async Task LostDispatchedMessageIsRecoveredAfterOneMinuteAndProcessedOnce()
    {
        var clock = new AdjustableTimeProvider(new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero));
        using var factory = new IntakeWebApplicationFactory(clock);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var store = services.GetRequiredService<IIntakeWorkStore>();
        var received = await new ReceiveIntake(
            services.GetRequiredService<IIntakeArtifactStore>(),
            store,
            clock,
            new CommittedWorkPublisherDouble()).ExecuteAsync(
                CreateSource("lost-dispatched-message"),
                "qdos-alpha:lost-dispatched-message");
        var firstDispatch = Assert.IsType<IntakeWorkItem>(await store.ClaimDispatchAsync(
            clock.GetUtcNow(),
            TimeSpan.FromMinutes(1),
            CancellationToken.None));
        await store.MarkDispatchedAsync(
            firstDispatch.Id,
            firstDispatch.LeaseToken!,
            clock.GetUtcNow(),
            CancellationToken.None);
        var reconciler = new ReconcileStagedArtifacts(
            store,
            services.GetRequiredService<IStagedArtifactAuthority>(),
            services.GetRequiredService<IIntakeArtifactStore>(),
            clock);

        clock.Advance(TimeSpan.FromSeconds(59));
        Assert.Equal(0, (await reconciler.ExecuteAsync(10)).RecoveredWorkItems);
        Assert.Null(await store.ClaimDispatchAsync(
            clock.GetUtcNow(),
            TimeSpan.FromMinutes(1),
            CancellationToken.None));

        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(1, (await reconciler.ExecuteAsync(10)).RecoveredWorkItems);
        Assert.Equal(0, (await reconciler.ExecuteAsync(10)).RecoveredWorkItems);
        var recoveredDispatch = Assert.IsType<IntakeWorkItem>(await store.ClaimDispatchAsync(
            clock.GetUtcNow(),
            TimeSpan.FromMinutes(1),
            CancellationToken.None));
        Assert.Equal(received.StagedReceiptId, recoveredDispatch.StagedReceiptId);
        Assert.Equal(firstDispatch.AttemptCount, recoveredDispatch.AttemptCount);
        await store.MarkDispatchedAsync(
            recoveredDispatch.Id,
            recoveredDispatch.LeaseToken!,
            clock.GetUtcNow(),
            CancellationToken.None);

        var processor = IntakeWebDriver.CreateProcessor(services);
        Assert.Equal(
            QueuedIntakeProcessingOutcome.Completed,
            await processor.ExecuteAsync(received.StagedReceiptId));
        Assert.Equal(
            QueuedIntakeProcessingOutcome.NoOp,
            await processor.ExecuteAsync(received.StagedReceiptId));
        var evaluation = Assert.IsType<IntakeEvaluationRevision>(
            await store.GetCompletedEvaluationAsync(
                received.StagedReceiptId,
                CancellationToken.None));
        Assert.Equal(1, evaluation.Revision);
    }

    [Fact]
    public async Task RecoveryLimitUsesTheTimeEachWorkItemBecameRecoverable()
    {
        var nowUtc = new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
        var clock = new AdjustableTimeProvider(nowUtc);
        using var factory = new IntakeWebApplicationFactory(clock);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var store = services.GetRequiredService<IIntakeWorkStore>();
        var receiver = new ReceiveIntake(
            services.GetRequiredService<IIntakeArtifactStore>(),
            store,
            clock,
            new CommittedWorkPublisherDouble());

        var lostDispatch = await receiver.ExecuteAsync(
            CreateSource("recovery-order-dispatched"),
            "qdos-alpha:recovery-order-dispatched");
        var dispatchClaim = Assert.IsType<IntakeWorkItem>(await store.ClaimDispatchAsync(
            nowUtc,
            TimeSpan.FromMinutes(1),
            CancellationToken.None));
        await store.MarkDispatchedAsync(
            dispatchClaim.Id,
            dispatchClaim.LeaseToken!,
            nowUtc,
            CancellationToken.None);

        var expiredProcessing = await receiver.ExecuteAsync(
            CreateSource("recovery-order-processing"),
            "qdos-alpha:recovery-order-processing");
        var processingDispatch = Assert.IsType<IntakeWorkItem>(await store.ClaimDispatchAsync(
            nowUtc,
            TimeSpan.FromMinutes(1),
            CancellationToken.None));
        await store.MarkDispatchedAsync(
            processingDispatch.Id,
            processingDispatch.LeaseToken!,
            nowUtc,
            CancellationToken.None);
        Assert.NotNull(await store.ClaimProcessingAsync(
            expiredProcessing.StagedReceiptId,
            nowUtc,
            TimeSpan.FromMinutes(5),
            CancellationToken.None));

        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.IntakeWorkItems
                .Where(item => item.StagedReceiptId == lostDispatch.StagedReceiptId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.DueAtUtc, nowUtc - TimeSpan.FromSeconds(70)));
            await context.IntakeWorkItems
                .Where(item => item.StagedReceiptId == expiredProcessing.StagedReceiptId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.LeaseExpiresAtUtc, nowUtc - TimeSpan.FromSeconds(30)));
        }

        Assert.Equal(1, await store.RecoverInterruptedWorkAsync(
            nowUtc,
            nowUtc - TimeSpan.FromMinutes(1),
            1,
            CancellationToken.None));

        await using var verification = await contextFactory.CreateDbContextAsync();
        Assert.Equal(
            "dispatched",
            await verification.IntakeWorkItems
                .Where(item => item.StagedReceiptId == lostDispatch.StagedReceiptId)
                .Select(item => item.State)
                .SingleAsync());
        Assert.Equal(
            "retry_scheduled",
            await verification.IntakeWorkItems
                .Where(item => item.StagedReceiptId == expiredProcessing.StagedReceiptId)
                .Select(item => item.State)
                .SingleAsync());
    }

    [Fact]
    [Trait("Category", "QdosAlphaAcceptance")]
    public async Task ImmediateQueueDeliveryDuringDispatchIsProcessedBeforePublisherAcknowledgement()
    {
        var clock = new AdjustableTimeProvider(new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero));
        using var factory = new IntakeWebApplicationFactory(clock);
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var store = services.GetRequiredService<IIntakeWorkStore>();
        var receiver = new ReceiveIntake(
            services.GetRequiredService<IIntakeArtifactStore>(),
            store,
            clock,
            new CommittedWorkPublisherDouble());
        var received = await receiver.ExecuteAsync(
            CreateSource("immediate-dispatch"),
            "qdos-alpha:immediate-dispatch");
        var processor = IntakeWebDriver.CreateProcessor(services);
        var dispatcher = new DispatchPendingIntakeWork(
            store,
            new IntakeWebDriver.ImmediateIntakeWorkEnqueuer(processor),
            clock);

        Assert.Equal(1, await dispatcher.ExecuteAsync(1, CancellationToken.None));

        var evaluation = Assert.IsType<IntakeEvaluationRevision>(
            await store.GetCompletedEvaluationAsync(
                received.StagedReceiptId,
                CancellationToken.None));
        Assert.Equal(received.StagedReceiptId, evaluation.StagedReceiptId);
        Assert.Null(await store.ClaimProcessingAsync(
            received.StagedReceiptId,
            clock.GetUtcNow(),
            TimeSpan.FromMinutes(5),
            CancellationToken.None));
    }

    [Fact]
    [Trait("Category", "QdosAlphaAcceptance")]
    public async Task QueuedCallerProcessesAStagedSourceExactlyOnce()
    {
        var clock = new AdjustableTimeProvider(new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero));
        using var factory = new IntakeWebApplicationFactory(clock);
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var store = services.GetRequiredService<IIntakeWorkStore>();
        var artifactStore = services.GetRequiredService<IIntakeArtifactStore>();
        var receiver = new ReceiveIntake(artifactStore, store, clock, new CommittedWorkPublisherDouble());
        var received = await receiver.ExecuteAsync(
            CreateSource("process-once"),
            "qdos-alpha:process-once");
        var dispatch = await store.ClaimDispatchAsync(
            clock.GetUtcNow(),
            TimeSpan.FromMinutes(1),
            CancellationToken.None);
        Assert.NotNull(dispatch);
        var dispatchWork = dispatch!;
        await store.MarkDispatchedAsync(
            dispatchWork.Id,
            dispatchWork.LeaseToken!,
            clock.GetUtcNow(),
            CancellationToken.None);
        var statusQueries = services.GetRequiredService<IQueuedIntakeStatusQueries>();
        Assert.Equal(
            QueuedIntakeStatusKind.Received,
            Assert.IsType<QueuedIntakeStatus>(await statusQueries.GetAsync(received.StagedReceiptId)).Status);
        var processor = IntakeWebDriver.CreateProcessor(services);

        await processor.ExecuteAsync(received.StagedReceiptId);
        await processor.ExecuteAsync(received.StagedReceiptId);

        var receipts = services.GetRequiredService<IIntakeReceiptQueries>();
        var retained = Assert.Single((await receipts.ListAsync(null, 1, 100, CancellationToken.None)).Items);
        Assert.Equal(IntakeDecision.CaseCreated, retained.Decision);
        var evaluation = Assert.IsType<IntakeEvaluationRevision>(
            await store.GetCompletedEvaluationAsync(
                received.StagedReceiptId,
                CancellationToken.None));
        Assert.Equal(received.StagedReceiptId, evaluation.StagedReceiptId);
        Assert.Equal(retained.Id, evaluation.ProcessedReceiptId);
        Assert.Equal(1, evaluation.Revision);
        var completedStatus = Assert.IsType<QueuedIntakeStatus>(
            await statusQueries.GetAsync(received.StagedReceiptId));
        Assert.Equal(QueuedIntakeStatusKind.Complete, completedStatus.Status);
        Assert.Equal(retained.Id, completedStatus.ProcessedReceiptId);
        Assert.Null(await store.ClaimProcessingAsync(
            received.StagedReceiptId,
            clock.GetUtcNow(),
            TimeSpan.FromMinutes(5),
            CancellationToken.None));
    }

    [Fact]
    [Trait("Category", "QdosAlphaAcceptance")]
    public async Task ConcurrentDistinctSourcesAreStagedWithoutSerializationFailures()
    {
        var clock = new AdjustableTimeProvider(new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero));
        using var factory = new IntakeWebApplicationFactory(clock);
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var store = services.GetRequiredService<IIntakeWorkStore>();
        var receiver = new ReceiveIntake(
            services.GetRequiredService<IIntakeArtifactStore>(),
            store,
            clock,
            new CommittedWorkPublisherDouble());
        var sources = Enumerable.Range(1, 8)
            .Select(index => CreateSource($"parallel-receive-{index}"))
            .ToArray();

        var received = await Task.WhenAll(sources.Select((source, index) =>
            receiver.ExecuteAsync(
                source,
                $"qdos-alpha:parallel-receive:{index}",
                CancellationToken.None)));

        Assert.Equal(8, received.Select(item => item.StagedReceiptId).Distinct().Count());
        Assert.All(received, item => Assert.False(item.IsDuplicate));
        foreach (var source in sources)
        {
            Assert.NotNull(await store.FindBySourceIdentityAsync(
                source.SourceIdentity,
                CancellationToken.None));
        }
    }

    [Fact]
    [Trait("Category", "QdosAlphaAcceptance")]
    public async Task ConcurrentDuplicateSourceIsStagedExactlyOnce()
    {
        var clock = new AdjustableTimeProvider(new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero));
        using var factory = new IntakeWebApplicationFactory(clock);
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var receiver = new ReceiveIntake(
            services.GetRequiredService<IIntakeArtifactStore>(),
            services.GetRequiredService<IIntakeWorkStore>(),
            clock,
            new CommittedWorkPublisherDouble());
        var source = CreateSource("parallel-duplicate");

        var received = await Task.WhenAll(Enumerable.Range(1, 8).Select(index =>
            receiver.ExecuteAsync(
                source,
                $"qdos-alpha:parallel-duplicate:{index}",
                CancellationToken.None)));

        Assert.Single(received.Select(item => item.StagedReceiptId).Distinct());
        Assert.Single(received, item => !item.IsDuplicate);
        Assert.Equal(7, received.Count(item => item.IsDuplicate));
    }

    [Fact]
    [Trait("Category", "QdosAlphaAcceptance")]
    public async Task PoisonReconciliationFailsClosedAndIsSafeToReplay()
    {
        var clock = new AdjustableTimeProvider(new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero));
        using var factory = new IntakeWebApplicationFactory(clock);
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var store = services.GetRequiredService<IIntakeWorkStore>();
        var receiver = new ReceiveIntake(
            services.GetRequiredService<IIntakeArtifactStore>(),
            store,
            clock,
            new CommittedWorkPublisherDouble());
        var received = await receiver.ExecuteAsync(
            CreateSource("poison-replay"),
            "qdos-alpha:poison-replay");
        var poison = new ReconcilePoisonedIntakeWork(store, clock);

        await poison.ExecuteAsync(received.StagedReceiptId);
        await poison.ExecuteAsync(received.StagedReceiptId);

        Assert.Null(await store.ClaimDispatchAsync(
            clock.GetUtcNow(),
            TimeSpan.FromMinutes(1),
            CancellationToken.None));
        Assert.Null(await store.ClaimProcessingAsync(
            received.StagedReceiptId,
            clock.GetUtcNow(),
            TimeSpan.FromMinutes(5),
            CancellationToken.None));
        await IntakeTestEvidence.AssertNoDurableIntakeReceiptsAsync(factory);
    }

    [Theory]
    [InlineData("io")]
    [InlineData("dependency")]
    [InlineData("wrapped-database")]
    public async Task TransientProcessingFailureSchedulesARetry(string failureKind)
    {
        var clock = new AdjustableTimeProvider(new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero));
        var artifactStore = new ReadFailureArtifactStore(failureKind);
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            clock,
            artifactStore);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var store = services.GetRequiredService<IIntakeWorkStore>();
        var received = await StageAndDispatchAsync(store, artifactStore, clock, failureKind);

        var outcome = await IntakeWebDriver.CreateProcessor(services)
            .ExecuteAsync(received.StagedReceiptId);

        Assert.Equal(QueuedIntakeProcessingOutcome.RetryScheduled, outcome);
        var work = Assert.IsType<IntakeWorkItem>(await store.FindWorkItemAsync(
            received.StagedReceiptId,
            CancellationToken.None));
        Assert.Equal(IntakeWorkState.RetryScheduled, work.State);
        Assert.Equal("intake_processing_failure", work.FailureCode);
        var status = Assert.IsType<QueuedIntakeStatus>(
            await services.GetRequiredService<IQueuedIntakeStatusQueries>()
                .GetAsync(received.StagedReceiptId));
        Assert.Equal(QueuedIntakeStatusKind.Processing, status.Status);
        Assert.Equal(work.DueAtUtc, status.RetryDueAtUtc);
        await IntakeTestEvidence.AssertNoDurableIntakeReceiptsAsync(factory);
    }

    [Fact]
    public async Task UnexpectedProcessingFailureIsPersistedThenRethrown()
    {
        var clock = new AdjustableTimeProvider(new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero));
        var artifactStore = new ReadFailureArtifactStore("unexpected");
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            clock,
            artifactStore);
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var store = services.GetRequiredService<IIntakeWorkStore>();
        var received = await StageAndDispatchAsync(store, artifactStore, clock, "unexpected");
        var processor = IntakeWebDriver.CreateProcessor(services);

        // The fault reaches the host: that is where it is logged in full.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => processor.ExecuteAsync(received.StagedReceiptId));

        var work = Assert.IsType<IntakeWorkItem>(await store.FindWorkItemAsync(
            received.StagedReceiptId,
            CancellationToken.None));
        Assert.Equal(IntakeWorkState.Failed, work.State);
        Assert.Equal("unexpected_intake_processing_failure", work.FailureCode);

        // The redelivery that follows the failed invocation finds the work
        // failed and does nothing, so the message is consumed, not poisoned.
        Assert.Equal(
            QueuedIntakeProcessingOutcome.NoOp,
            await processor.ExecuteAsync(received.StagedReceiptId));

        using var failedPage = await client.GetAsync(
            $"/Upload/Status/{received.StagedReceiptId:D}");
        failedPage.EnsureSuccessStatusCode();
        var html = await failedPage.Content.ReadAsStringAsync();
        Assert.Contains("<h1>Failed</h1>", html, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "unexpected_intake_processing_failure",
            html,
            StringComparison.Ordinal);
        await IntakeTestEvidence.AssertNoDurableIntakeReceiptsAsync(factory);
    }

    /// <summary>
    /// Stages one source and walks it to <c>dispatched</c>, the state a queue
    /// delivery finds it in.
    /// </summary>
    private static async Task<ReceivedIntake> StageAndDispatchAsync(
        IIntakeWorkStore store,
        IIntakeArtifactStore artifactStore,
        TimeProvider clock,
        string name)
    {
        var received = await new ReceiveIntake(artifactStore, store, clock, new CommittedWorkPublisherDouble()).ExecuteAsync(
            CreateSource($"{name}-failure"),
            $"qdos-alpha:failure:{name}");
        var dispatch = Assert.IsType<IntakeWorkItem>(await store.ClaimDispatchAsync(
            clock.GetUtcNow(),
            TimeSpan.FromMinutes(1),
            CancellationToken.None));
        await store.MarkDispatchedAsync(
            dispatch.Id,
            dispatch.LeaseToken!,
            clock.GetUtcNow(),
            CancellationToken.None);
        return received;
    }

    [Fact]
    public async Task QueuedStatusProjectsAnActiveProcessingLease()
    {
        var clock = new AdjustableTimeProvider(new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero));
        using var factory = new IntakeWebApplicationFactory(clock);
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var store = services.GetRequiredService<IIntakeWorkStore>();
        var received = await new ReceiveIntake(
            services.GetRequiredService<IIntakeArtifactStore>(),
            store,
            clock,
            new CommittedWorkPublisherDouble()).ExecuteAsync(
                CreateSource("processing-status"),
                "qdos-alpha:processing-status");
        var dispatch = Assert.IsType<IntakeWorkItem>(await store.ClaimDispatchAsync(
            clock.GetUtcNow(),
            TimeSpan.FromMinutes(1),
            CancellationToken.None));
        await store.MarkDispatchedAsync(
            dispatch.Id,
            dispatch.LeaseToken!,
            clock.GetUtcNow(),
            CancellationToken.None);
        Assert.NotNull(await store.ClaimProcessingAsync(
            received.StagedReceiptId,
            clock.GetUtcNow(),
            TimeSpan.FromMinutes(5),
            CancellationToken.None));

        var status = Assert.IsType<QueuedIntakeStatus>(
            await services.GetRequiredService<IQueuedIntakeStatusQueries>()
                .GetAsync(received.StagedReceiptId));
        Assert.Equal(QueuedIntakeStatusKind.Processing, status.Status);
    }

    [Fact]
    public async Task TransientProcessingFailureExhaustsTheBoundedRetrySchedule()
    {
        var clock = new AdjustableTimeProvider(new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero));
        var artifactStore = new ReadFailureArtifactStore("io");
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            clock,
            artifactStore);
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var store = services.GetRequiredService<IIntakeWorkStore>();
        var received = await new ReceiveIntake(artifactStore, store, clock, new CommittedWorkPublisherDouble()).ExecuteAsync(
            CreateSource("retry-exhaustion"),
            "qdos-alpha:retry-exhaustion");
        var processor = IntakeWebDriver.CreateProcessor(services);

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var dispatch = Assert.IsType<IntakeWorkItem>(await store.ClaimDispatchAsync(
                clock.GetUtcNow(),
                TimeSpan.FromMinutes(1),
                CancellationToken.None));
            await store.MarkDispatchedAsync(
                dispatch.Id,
                dispatch.LeaseToken!,
                clock.GetUtcNow(),
                CancellationToken.None);
            var outcome = await processor.ExecuteAsync(received.StagedReceiptId);
            Assert.Equal(
                attempt < 5
                    ? QueuedIntakeProcessingOutcome.RetryScheduled
                    : QueuedIntakeProcessingOutcome.Failed,
                outcome);
            clock.Advance(TimeSpan.FromHours(3));
        }

        var work = Assert.IsType<IntakeWorkItem>(await store.FindWorkItemAsync(
            received.StagedReceiptId,
            CancellationToken.None));
        Assert.Equal(5, work.AttemptCount);
        Assert.Equal(IntakeWorkState.Failed, work.State);
        Assert.Null(await store.ClaimDispatchAsync(
            clock.GetUtcNow(),
            TimeSpan.FromMinutes(1),
            CancellationToken.None));
    }

    private static IntakeSource CreateSource(string identity)
    {
        var email = IntakeTestEvidence.CreateEmail(
            $"{identity}.eml",
            $"QDOS instruction\r\nClaimant Name: Recovery Claimant\r\nClaim Number: {identity}\r\nVehicle Registration: AB12 CDE");
        return new(
            email.FileName,
            email.MediaType,
            email.Content,
            new DateTimeOffset(2031, 5, 6, 10, 29, 0, TimeSpan.Zero),
            "QDOS offline acceptance recovery",
            new(IntakeSourceChannel.ManualUpload, $"qdos-alpha:{identity}"));
    }

    /// <summary>
    /// Fails the read of its own stored artifact with one controlled fault, so
    /// the processor's classification of that fault is what the test observes.
    /// </summary>
    private sealed class ReadFailureArtifactStore(string failureKind) : IIntakeArtifactStore
    {
        private string? storageKey;

        public Task<string> StoreAsync(
            string contentHash,
            ReadOnlyMemory<byte> value,
            CancellationToken cancellationToken)
        {
            storageKey = $"test/{contentHash}";
            return Task.FromResult(storageKey);
        }

        public Task<ReadOnlyMemory<byte>?> ReadAsync(
            string key,
            CancellationToken cancellationToken) =>
            string.Equals(key, storageKey, StringComparison.Ordinal)
                ? Task.FromException<ReadOnlyMemory<byte>?>(Failure())
                : Task.FromResult<ReadOnlyMemory<byte>?>(null);

        private Exception Failure() => failureKind switch
        {
            "io" => new IOException("Controlled transient read failure."),
            "dependency" => new IntakeDependencyUnavailableException(
                "Controlled remote dependency failure."),
            // EF surfaces SaveChanges faults as DbUpdateException wrapping the
            // provider exception; the processor must look through the wrapper.
            "wrapped-database" => new DbUpdateException(
                "Controlled wrapped database failure.",
                new ControlledDbException()),
            "unexpected" => new InvalidOperationException("Controlled unexpected read failure."),
            _ => throw new ArgumentOutOfRangeException(nameof(failureKind), failureKind, null)
        };
    }

    private sealed class ControlledDbException() : DbException("Controlled database fault.");

    private sealed class AdjustableTimeProvider(DateTimeOffset initialUtcNow) : TimeProvider
    {
        private DateTimeOffset currentUtcNow = initialUtcNow;

        public override DateTimeOffset GetUtcNow() => currentUtcNow;

        public void Advance(TimeSpan duration) => currentUtcNow = currentUtcNow.Add(duration);
    }
}
