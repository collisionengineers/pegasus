using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Triage;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The global Triage reference allocator, proved against the real database.
/// Written as a partial of <see cref="QdosTriageIntegrationTests"/> so it
/// reuses that class's <c>AcceptedTriageMatchPolicy</c> and Triage read
/// helpers rather than growing a second copy of either.
/// </summary>
public sealed partial class QdosTriageIntegrationTests
{
    [Fact]
    public async Task TheFirstTwoTriagesTakeTheFirstTwoGlobalReferences()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            extractionPolicy: new AcceptedTriageMatchPolicy());
        using var client = IntakeWebDriver.CreateClient(factory);

        await OpenTriageAsync(factory, client, "AB12 CDE", "TRIAGE-ALLOC-A", "alloc-a.eml");
        await OpenTriageAsync(factory, client, "XY12 ZZZ", "TRIAGE-ALLOC-B", "alloc-b.eml");

        var summaries = await ListTriageAsync(factory.Services);
        var first = Assert.Single(summaries, item => item.NormalizedVehicleRegistration == "AB12CDE");
        var second = Assert.Single(summaries, item => item.NormalizedVehicleRegistration == "XY12ZZZ");

        Assert.Equal("T-00001", first.Reference);
        Assert.Equal("T-00002", second.Reference);
        // The provider claim number is a fact about the sender and keeps its
        // own member: it is no longer what the queue calls the reference.
        Assert.Equal("TRIAGE-ALLOC-A", first.ClaimNumber);
        Assert.Equal("TRIAGE-ALLOC-B", second.ClaimNumber);

        var detail = await GetTriageAsync(factory.Services, first.Id);
        Assert.Equal("T-00001", detail.Record.Reference);
    }

    [Fact]
    public async Task CreationReplayReturnsTheOriginalReferenceAndTakesNoSecondNumber()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            extractionPolicy: new AcceptedTriageMatchPolicy());
        using var client = IntakeWebDriver.CreateClient(factory);
        const string replayToken = "abcdef01234567890123456789abcdef";
        var email = IntakeTestEvidence.CreateEmail(
            "alloc-replay.eml",
            "QDOS instruction\r\nClaimant Name: Replay Claimant\r\nClaim Number: TRIAGE-ALLOC-REPLAY\r\nVehicle Registration: AB12 CDE");

        _ = await IntakeWebDriver.UploadAndProcessAsync(
            factory, client, email.FileName, email.MediaType, email.Content, replayToken);
        _ = await IntakeWebDriver.UploadAndProcessAsync(
            factory, client, email.FileName, email.MediaType, email.Content, replayToken);

        var replayed = Assert.Single(await ListTriageAsync(factory.Services));
        Assert.Equal("T-00001", replayed.Reference);

        // The replay consumed nothing, so the next genuine creation takes the
        // very next number.
        await OpenTriageAsync(factory, client, "XY12 ZZZ", "TRIAGE-ALLOC-NEXT", "alloc-next.eml");
        var next = Assert.Single(
            await ListTriageAsync(factory.Services),
            item => item.NormalizedVehicleRegistration == "XY12ZZZ");
        Assert.Equal("T-00002", next.Reference);
    }

    [Fact]
    public async Task ConcurrentCreationsAllocateDistinctReferencesAndToleratesGaps()
    {
        const int concurrentCreations = 8;
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            extractionPolicy: new AcceptedTriageMatchPolicy());
        using var client = IntakeWebDriver.CreateClient(factory);

        // Every upload is staged first; the eight Triage creations then race
        // the one global counter row when their staged work is drained
        // together.
        var uploads = new List<UploadResult>();
        for (var index = 0; index < concurrentCreations; index++)
        {
            var registration = $"AB{index:00}CDE";
            var email = IntakeTestEvidence.CreateEmail(
                $"alloc-concurrent-{index}.eml",
                "QDOS instruction\r\n"
                + $"Claimant Name: Concurrent Claimant {index}\r\n"
                + $"Claim Number: TRIAGE-ALLOC-{index:00}\r\n"
                + $"Vehicle Registration: {registration}");
            uploads.Add(await IntakeWebDriver.UploadAsync(
                client,
                email.FileName,
                email.MediaType,
                email.Content));
        }

        await Task.WhenAll(uploads.Select(upload =>
            Task.Run(() => IntakeWebDriver.ProcessQueuedAsync(factory, upload))));

        var summaries = await ListTriageAsync(factory.Services);
        Assert.Equal(concurrentCreations, summaries.Count);

        var sequences = new List<long>();
        foreach (var summary in summaries)
        {
            Assert.True(
                TriageReferenceFormat.TryParse(summary.Reference, out var sequence),
                $"'{summary.Reference}' is not a Triage reference.");
            // A sequence of zero could not have been persisted at all — the
            // Triage table's CK_Triage_Sequence check constraint refuses it —
            // so a created row proves the allocator never handed one out.
            Assert.True(sequence > 0);
            sequences.Add(sequence);
        }

        // Distinct is the invariant. The numbers need not be contiguous: a
        // number taken by a creation that then rolled back is never reissued,
        // so gaps are expected and tolerated.
        Assert.Equal(concurrentCreations, sequences.Distinct().Count());
    }

    [Fact]
    public async Task TheReferenceSurvivesEveryLaterMutation()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            extractionPolicy: new AcceptedTriageMatchPolicy());
        using var client = IntakeWebDriver.CreateClient(factory);
        await OpenTriageAsync(factory, client, "AB12 CDE", "TRIAGE-ALLOC-IMMUTABLE", "alloc-immutable.eml");

        var created = await GetOnlyTriageAsync(factory.Services);
        Assert.Equal("T-00001", created.Record.Reference);

        await using var scope = factory.Services.CreateAsyncScope();
        var awaitInformation = scope.ServiceProvider.GetRequiredService<IAwaitTriageInformation>();
        var awaited = await awaitInformation.ExecuteAsync(
            new TriageMutationRequest(
                created.Record.Id,
                created.Record.Version,
                Pegasus.Web.Authentication.DevelopmentOfflineIdentity.AdministratorId.ToString("D"),
                "alloc-immutable-await",
                "Further retained information is required"),
            CancellationToken.None);

        Assert.Equal("T-00001", awaited.Reference);
        var reread = await GetTriageAsync(factory.Services, created.Record.Id);
        Assert.Equal("T-00001", reread.Record.Reference);
    }

    private static async Task OpenTriageAsync(
        IntakeWebApplicationFactory factory,
        HttpClient client,
        string registration,
        string claimNumber,
        string fileName)
    {
        var email = IntakeTestEvidence.CreateEmail(
            fileName,
            "QDOS instruction\r\n"
            + $"Claimant Name: Allocation Claimant\r\nClaim Number: {claimNumber}\r\n"
            + $"Vehicle Registration: {registration}");
        _ = await IntakeWebDriver.UploadAndProcessAsync(
            factory,
            client,
            email.FileName,
            email.MediaType,
            email.Content);
    }

    private static async Task<IReadOnlyList<TriageSummary>> ListTriageAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<ITriageQueries>();
        return await queries.ListAsync(null, CancellationToken.None);
    }
}
