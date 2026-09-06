using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Triage;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The global Triage reference allocator, proved against the real database.
/// </summary>
/// <remarks>
/// Every case here creates its Triage through the production
/// <c>ICreateTriageFromIntake</c>/<c>ITriageStore</c> path over real SQL, with
/// its receipt and evaluation prepared beforehand. The receipt and evaluation
/// fixtures are this file's own — deliberately self-contained, so the proof
/// does not widen another suite's private helpers.
/// </remarks>
[Trait("Category", "SqlServer")]
public sealed class TriageReferenceAllocationTests
{
    [Fact]
    public async Task TheFirstTwoTriagesTakeTheFirstTwoGlobalReferences()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;

        var first = await OpenTriageAsync(services, "AB12CDE");
        var second = await OpenTriageAsync(services, "XY12ZZZ");

        Assert.Equal("T-00001", first.Reference);
        Assert.Equal("T-00002", second.Reference);

        // The reference is durable and reaches the read side: `GetAsync` maps
        // the persisted row, so this fails if the mapping drops it.
        var reread = await services.GetRequiredService<ITriageQueries>()
            .GetAsync(first.Id, CancellationToken.None);
        Assert.Equal("T-00001", Assert.IsType<TriageDetail>(reread).Record.Reference);
    }

    [Fact]
    public async Task CreationReplayReturnsTheOriginalReferenceAndTakesNoSecondNumber()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var operationKey = $"triage-alloc-replay:{Guid.NewGuid():N}";

        var prepared = await PrepareAsync(services, "AB12CDE");
        var created = await CreateAsync(services, prepared, operationKey);
        var replayed = await CreateAsync(services, prepared, operationKey);

        Assert.Equal("T-00001", created.Reference);
        Assert.Equal(created.Reference, replayed.Reference);
        Assert.Equal(created.Id, replayed.Id);
        Assert.Single(await ListTriageAsync(services));

        // The replay consumed nothing, so the next genuine creation takes the
        // very next number.
        var next = await OpenTriageAsync(services, "XY12ZZZ");
        Assert.Equal("T-00002", next.Reference);
    }

    [Fact]
    public async Task ConcurrentCreationsAllocateDistinctDurableReferences()
    {
        const int concurrentCreations = 8;
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;

        // Receipts and evaluations are prepared sequentially; only the eight
        // allocations race, so the one global counter row is the only thing
        // under contention.
        var prepared = new List<PreparedTriage>();
        for (var index = 0; index < concurrentCreations; index++)
        {
            prepared.Add(await PrepareAsync(services, $"AL{index:00}CAT"));
        }

        var created = await Task.WhenAll(prepared.Select(item => Task.Run(
            () => CreateAsync(
                factory.Services,
                item,
                $"triage-alloc-concurrent:{item.ReceiptId:N}"))));

        Assert.Equal(concurrentCreations, created.Length);
        var sequences = new List<long>();
        foreach (var record in created)
        {
            Assert.True(
                TriageReferenceFormat.TryParse(record.Reference, out var sequence),
                $"'{record.Reference}' is not a Triage reference.");
            // A sequence of zero could not have been persisted at all — the
            // Triage table's CK_Triage_Sequence check constraint refuses it —
            // so a created row proves the allocator never handed one out.
            Assert.True(sequence > 0);
            sequences.Add(sequence);
        }

        // This verifies uniqueness of committed references under contention.
        // It does not assert whether uncommitted allocations leave gaps.
        Assert.Equal(concurrentCreations, sequences.Distinct().Count());
        Assert.Equal(
            concurrentCreations,
            created.Select(record => record.Reference).Distinct(StringComparer.Ordinal).Count());

        // Every allocation is also durable and unique on the read side.
        var listed = await ListTriageAsync(services);
        Assert.Equal(concurrentCreations, listed.Count);
        var queries = services.GetRequiredService<ITriageQueries>();
        var persisted = new List<string>();
        foreach (var record in created)
        {
            var detail = await queries.GetAsync(record.Id, CancellationToken.None);
            var reference = Assert.IsType<TriageDetail>(detail).Record.Reference;
            Assert.Equal(record.Reference, reference);
            persisted.Add(reference!);
        }

        Assert.Equal(
            concurrentCreations,
            persisted.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// One receipt with its retained accepted-match evidence and its
    /// evaluation revision — everything Triage creation requires, and nothing
    /// that races.
    /// </summary>
    private sealed record PreparedTriage(
        Guid ReceiptId,
        Guid EvaluationRevisionId,
        IntakeSourceIdentity SourceIdentity,
        string SourceHash,
        string Registration,
        IntakeEvidence AcceptedMatch);

    private static async Task<PreparedTriage> PrepareAsync(
        IServiceProvider services,
        string registration)
    {
        var sourceIdentity = new IntakeSourceIdentity(
            IntakeSourceChannel.ManualUpload,
            Guid.NewGuid().ToString("N"));
        var sourceHash = new string('a', 64);
        var acceptedMatch = new IntakeEvidence(
            IntakeEvidenceSource.SystemDefault,
            IntakeEvidenceStrength.Strong,
            IntakeEvidenceFinding.AcceptedTriageMatch,
            registration,
            "Accepted Triage match for the reference-allocation test.",
            MatcherKey: "triage-allocation-test",
            MatcherVersion: 1);
        var receiptId = await StoreMinimalReceiptAsync(
            services,
            $"triage-allocation-{registration.ToLowerInvariant()}.pdf",
            [acceptedMatch],
            sourceIdentity,
            sourceHash);
        var evaluationRevisionId = await StageAndCompleteEvaluationAsync(services, receiptId);
        return new(
            receiptId,
            evaluationRevisionId,
            sourceIdentity,
            sourceHash,
            registration,
            acceptedMatch);
    }

    private static async Task<TriageRecord> CreateAsync(
        IServiceProvider services,
        PreparedTriage prepared,
        string operationKey)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ICreateTriageFromIntake>().ExecuteAsync(
            new(
                new TriageOrigin(
                    prepared.ReceiptId,
                    prepared.SourceIdentity,
                    prepared.SourceHash,
                    prepared.EvaluationRevisionId),
                prepared.Registration,
                prepared.AcceptedMatch,
                // Creation is the intake worker's route, the one that admits
                // the system worker.
                ActionActor.SystemWorker("test-worker"),
                operationKey),
            CancellationToken.None);
    }

    private static async Task<TriageRecord> OpenTriageAsync(
        IServiceProvider services,
        string registration)
    {
        var prepared = await PrepareAsync(services, registration);
        return await CreateAsync(
            services,
            prepared,
            $"triage-alloc-create:{prepared.ReceiptId:N}");
    }

    private static async Task<IReadOnlyList<TriageSummary>> ListTriageAsync(IServiceProvider services) =>
        await services.GetRequiredService<ITriageQueries>()
            .ListAsync(null, CancellationToken.None);

    /// <summary>
    /// Stages and completes one durable intake work item so the receipt has a
    /// real <c>IntakeEvaluations</c> row, the FK <see cref="TriageOrigin"/>
    /// requires. Mirrors the queued-intake completion path
    /// (<c>IIntakeWorkStore.ReceiveAsync</c>/<c>CompleteProcessingAsync</c>)
    /// without going through the full mail-decision pipeline.
    /// </summary>
    private static async Task<Guid> StageAndCompleteEvaluationAsync(
        IServiceProvider services,
        Guid processedReceiptId)
    {
        var workStore = services.GetRequiredService<IIntakeWorkStore>();
        var now = DateTimeOffset.UtcNow;
        var staged = new IntakeStagedReceipt(
            Guid.NewGuid(),
            "triage-allocation-evaluation.pdf",
            "application/pdf",
            1024,
            Guid.NewGuid().ToString("N"),
            new IntakeSourceIdentity(IntakeSourceChannel.ManualUpload, Guid.NewGuid().ToString("N")),
            now,
            "test-actor",
            $"test-storage-key/{Guid.NewGuid():N}",
            now);
        await workStore.ReceiveAsync(staged, $"triage-allocation-receive:{Guid.NewGuid():N}", CancellationToken.None);
        var dispatchClaim = await workStore.ClaimDispatchAsync(now, TimeSpan.FromMinutes(1), CancellationToken.None)
            ?? throw new InvalidOperationException("Expected the staged evaluation work item to be claimable.");
        await workStore.MarkDispatchedAsync(dispatchClaim.Id, dispatchClaim.LeaseToken!, now, CancellationToken.None);
        var processingClaim = await workStore.ClaimProcessingAsync(staged.Id, now, TimeSpan.FromMinutes(1), CancellationToken.None)
            ?? throw new InvalidOperationException("Expected the dispatched evaluation work item to be claimable for processing.");
        var evaluation = await workStore.CompleteProcessingAsync(
            processingClaim.WorkItem.Id,
            processingClaim.WorkItem.LeaseToken!,
            processedReceiptId,
            now,
            CancellationToken.None);
        return evaluation.Id;
    }

    private static async Task<Guid> StoreMinimalReceiptAsync(
        IServiceProvider services,
        string sourceFileName,
        IReadOnlyList<IntakeEvidence> evidence,
        IntakeSourceIdentity sourceIdentity,
        string sourceHash)
    {
        var receiptStore = services.GetRequiredService<IIntakeReceiptStore>();
        var receipt = await receiptStore.StoreAsync(
            new IntakeReceiptDraft(
                sourceFileName,
                "application/pdf",
                1024,
                sourceHash,
                sourceIdentity,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                "test-actor",
                IntakeDecision.NeedsSorting,
                "test decision reason",
                evidence,
                [],
                null,
                [],
                null,
                null,
                "test-reader",
                "1",
                null,
                null),
            CancellationToken.None);
        return receipt.Id;
    }
}
