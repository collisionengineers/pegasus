using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Intake;

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
            clock);
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
        Assert.Equal(1, (await reconciler.ExecuteAsync(10)).RecoveredLeases);
        Assert.Equal(0, (await reconciler.ExecuteAsync(10)).RecoveredLeases);

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
            clock);
        var received = await receiver.ExecuteAsync(
            CreateSource("immediate-dispatch"),
            "qdos-alpha:immediate-dispatch");
        var processor = services.GetRequiredService<ProcessQueuedIntake>();
        var dispatcher = new DispatchPendingIntakeWork(
            store,
            new ImmediateIntakeWorkEnqueuer(processor),
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
        var receiver = new ReceiveIntake(artifactStore, store, clock);
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
        var processor = services.GetRequiredService<ProcessQueuedIntake>();

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
            clock);
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
            clock);
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
            clock);
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

    private sealed class ImmediateIntakeWorkEnqueuer(ProcessQueuedIntake processor)
        : IIntakeWorkEnqueuer
    {
        public Task EnqueueAsync(
            Guid stagedReceiptId,
            CancellationToken cancellationToken) =>
            processor.ExecuteAsync(stagedReceiptId, cancellationToken);
    }

    private sealed class AdjustableTimeProvider(DateTimeOffset initialUtcNow) : TimeProvider
    {
        private DateTimeOffset currentUtcNow = initialUtcNow;

        public override DateTimeOffset GetUtcNow() => currentUtcNow;

        public void Advance(TimeSpan duration) => currentUtcNow = currentUtcNow.Add(duration);
    }
}
