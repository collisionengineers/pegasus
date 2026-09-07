using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Triage;
using Pegasus.Web.Authentication;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The global Triage reference allocator, proved against the real database.
/// </summary>
/// <remarks>
/// Every case here creates its Triage through the production
/// <c>ICreateTriageFromIntake</c>/<c>ITriageStore</c> path over real SQL, with
/// its receipt and evaluation prepared beforehand. The full web ingest is used
/// once, to show the allocated reference reaching the operator's page; driving
/// eight of those concurrently would contend on the intake work dispatch claim
/// — a single-sweeper path by design — and prove nothing about this allocator.
/// The receipt and evaluation fixtures are <c>TriageQueuesWebTests</c>' own,
/// reused rather than copied.
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

        var first = await OpenTriageAsync(services, "AB12CDE", "TRIAGE-ALLOC-A");
        var second = await OpenTriageAsync(services, "XY12ZZZ", "TRIAGE-ALLOC-B");

        Assert.Equal("T-00001", first.Reference);
        Assert.Equal("T-00002", second.Reference);

        var summaries = await ListTriageAsync(services);
        var firstSummary = Assert.Single(summaries, item => item.Id == first.Id);
        // The provider claim number is a fact about the sender and keeps its
        // own member: it is no longer what the queue calls the reference.
        Assert.Equal("T-00001", firstSummary.Reference);
        Assert.Equal("TRIAGE-ALLOC-A", firstSummary.ClaimNumber);
        Assert.Equal(
            "T-00002",
            Assert.Single(summaries, item => item.Id == second.Id).Reference);
    }

    [Fact]
    public async Task CreationReplayReturnsTheOriginalReferenceAndTakesNoSecondNumber()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var operationKey = $"triage-alloc-replay:{Guid.NewGuid():N}";

        var prepared = await PrepareAsync(services, "AB12CDE", "TRIAGE-ALLOC-REPLAY");
        var created = await CreateAsync(services, prepared, operationKey);
        var replayed = await CreateAsync(services, prepared, operationKey);

        Assert.Equal("T-00001", created.Reference);
        Assert.Equal(created.Reference, replayed.Reference);
        Assert.Equal(created.Id, replayed.Id);
        Assert.Single(await ListTriageAsync(services));

        // The replay consumed nothing, so the next genuine creation takes the
        // very next number.
        var next = await OpenTriageAsync(services, "XY12ZZZ", "TRIAGE-ALLOC-NEXT");
        Assert.Equal("T-00002", next.Reference);
    }

    [Fact]
    public async Task ConcurrentCreationsAllocateDistinctReferencesAndToleratesGaps()
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
            prepared.Add(await PrepareAsync(services, $"AL{index:00}CAT", $"TRIAGE-ALLOC-{index:00}"));
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

        // Distinct is the invariant, and no reference is ever handed out
        // twice. The numbers need not be contiguous: one taken by a creation
        // that then rolled back is never reissued, so gaps are expected.
        Assert.Equal(concurrentCreations, sequences.Distinct().Count());
        Assert.Equal(
            concurrentCreations,
            created.Select(record => record.Reference).Distinct(StringComparer.Ordinal).Count());

        // Every allocation is also durable and unique on the read side.
        var listed = await ListTriageAsync(services);
        Assert.Equal(concurrentCreations, listed.Count);
        Assert.Equal(
            concurrentCreations,
            listed.Select(item => item.Reference).Distinct(StringComparer.Ordinal).Count());

        // Each creator's reference is the one persisted against its own
        // Triage. Distinctness alone would still hold if two concurrent
        // allocations were recorded against each other's rows, so the
        // id-to-reference correspondence is asserted per creation.
        var queries = services.GetRequiredService<ITriageQueries>();
        foreach (var record in created)
        {
            var detail = await queries.GetAsync(record.Id, CancellationToken.None);
            Assert.Equal(
                record.Reference,
                Assert.IsType<TriageDetail>(detail).Record.Reference);
        }
    }

    [Fact]
    public async Task TheAllocatedReferenceReachesTheOperatorsPage()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var triage = await OpenTriageAsync(services, "AB12CDE", "TRIAGE-ALLOC-PAGE");

        Assert.Equal("T-00001", triage.Reference);

        using var response = await client.GetAsync($"/Triage/{triage.Id:D}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("T-00001", html, StringComparison.Ordinal);
        Assert.Contains("Triage reference", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheReferenceSurvivesEveryLaterMutation()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var created = await OpenTriageAsync(services, "AB12CDE", "TRIAGE-ALLOC-IMMUTABLE");

        var awaited = await services.GetRequiredService<IAwaitTriageInformation>().ExecuteAsync(
            new TriageMutationRequest(
                created.Id,
                created.Version,
                ActionActor.Staff(
                    DevelopmentOfflineIdentity.AdministratorId,
                    [StaffRole.Administrator]),
                $"alloc-immutable-await:{Guid.NewGuid():N}",
                "Further retained information is required"),
            CancellationToken.None);

        Assert.Equal("T-00001", created.Reference);
        Assert.Equal(created.Reference, awaited.Reference);
        var reread = Assert.IsType<TriageDetail>(
            await services.GetRequiredService<ITriageQueries>()
                .GetAsync(created.Id, CancellationToken.None));
        Assert.Equal(created.Reference, reread.Record.Reference);
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
        string registration,
        string claimNumber)
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
        var receiptId = await TriageQueuesWebTests.StoreMinimalReceiptAsync(
            services,
            $"triage-allocation-{claimNumber.ToLowerInvariant()}.pdf",
            new InstructionDraft(
                SuggestedPrincipalCode: "QDOS",
                ClaimantName: null,
                ClaimNumber: claimNumber,
                VehicleRegistration: registration,
                VehicleMake: null,
                VehicleModel: null,
                VehicleMileage: null,
                AccidentCircumstances: null,
                DateOfIncident: null,
                InstructionDate: null,
                InspectionAddress: null),
            [acceptedMatch],
            sourceIdentity,
            sourceHash);
        var evaluationRevisionId = await TriageQueuesWebTests.StageAndCompleteEvaluationAsync(
            services,
            receiptId);
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
        string registration,
        string claimNumber)
    {
        var prepared = await PrepareAsync(services, registration, claimNumber);
        return await CreateAsync(
            services,
            prepared,
            $"triage-alloc-create:{prepared.ReceiptId:N}");
    }

    private static async Task<IReadOnlyList<TriageSummary>> ListTriageAsync(IServiceProvider services) =>
        await services.GetRequiredService<ITriageQueries>()
            .ListAsync(null, CancellationToken.None);
}
