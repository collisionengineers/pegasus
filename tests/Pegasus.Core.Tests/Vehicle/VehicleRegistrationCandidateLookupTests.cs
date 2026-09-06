using Pegasus.Core.Vehicle;

namespace Pegasus.Core.Tests.Vehicle;

public sealed class VehicleRegistrationCandidateLookupTests
{
    [Fact]
    public void CandidateMapIsStableDistinctAndBounded()
    {
        var candidates = VehicleRegistrationCandidateLookup.GenerateCandidates(" OI-01 OIO ");
        Assert.InRange(candidates.Count, 1, 8);
        Assert.Equal(candidates.Count, candidates.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(candidates, VehicleRegistrationCandidateLookup.GenerateCandidates(" OI-01 OIO "));
        Assert.Equal("OI01OIO", candidates[0]);
        Assert.All(candidates, candidate => Assert.DoesNotContain(' ', candidate));
    }

    [Theory]
    [InlineData(MachineReadRegistrationSource.EmbeddedDocumentText)]
    [InlineData(MachineReadRegistrationSource.StaffConfirmed)]
    [InlineData(MachineReadRegistrationSource.CaseSearch)]
    public async Task NonMachineSourcesNeverInvokeLookup(MachineReadRegistrationSource source)
    {
        var adapter = new RecordingAdapter(_ => VehicleLookupOutcome.NotFound);
        var lookup = new VehicleRegistrationCandidateLookup(adapter);
        await Assert.ThrowsAsync<ArgumentException>(() => lookup.LookupAsync(new("OI01OIO", source, "recorded-source")));
        Assert.Empty(adapter.Requests);
    }

    [Fact]
    public async Task OneViableAfterAllConclusiveNotFoundIsAcceptedAndLedgerPreserved()
    {
        var adapter = new RecordingAdapter(registration => registration == "OI01OIO" ? VehicleLookupOutcome.Current : VehicleLookupOutcome.NotFound);
        var reading = new MachineReadRegistration(" OI-01 OIO ", MachineReadRegistrationSource.DocumentOcr, "document-7/page-2");
        var result = await new VehicleRegistrationCandidateLookup(adapter).LookupAsync(reading);
        Assert.Equal("OI01OIO", result.AcceptedRegistration);
        Assert.Equal(reading, result.Reading);
        Assert.Equal(result.Candidates, adapter.Requests);
        Assert.Equal(result.Candidates, result.Attempts.Select(attempt => attempt.Registration));
    }

    [Theory]
    [InlineData(VehicleLookupOutcome.Throttled)]
    [InlineData(VehicleLookupOutcome.Unavailable)]
    [InlineData(VehicleLookupOutcome.Failed)]
    public async Task AnyUnresolvedAttemptKeepsSingleViableCandidateAmbiguous(VehicleLookupOutcome unresolved)
    {
        var adapter = new RecordingAdapter(registration => registration == "OI01OIO" ? VehicleLookupOutcome.Partial : unresolved);
        var result = await new VehicleRegistrationCandidateLookup(adapter).LookupAsync(new("OI01OIO", MachineReadRegistrationSource.VehicleRecognition, "image-4"));
        Assert.True(result.IsAmbiguous);
        Assert.Null(result.AcceptedRegistration);
        Assert.Contains(result.Attempts, attempt => attempt.Result.Outcome == unresolved);
    }

    [Fact]
    public async Task ConcurrentMultipleViableResultsRemainAmbiguousWithoutDiscardingEither()
    {
        var adapter = new RecordingAdapter(_ => VehicleLookupOutcome.Stale, delay: true);
        var result = await new VehicleRegistrationCandidateLookup(adapter).LookupAsync(new("OI01OIO", MachineReadRegistrationSource.DocumentOcr, "ocr-operation-9"));
        Assert.True(result.IsAmbiguous);
        Assert.Null(result.AcceptedResult);
        Assert.Equal(result.Candidates.Count, result.Attempts.Count);
        Assert.True(adapter.MaximumConcurrency > 1);
    }

    private sealed class RecordingAdapter(Func<string, VehicleLookupOutcome> outcome, bool delay = false) : IVehicleLookupAdapter
    {
        private int active;
        public List<string> Requests { get; } = [];
        public int MaximumConcurrency { get; private set; }
        public async Task<VehicleLookupResult> LookupAsync(VehicleLookupRequest request, CancellationToken cancellationToken)
        {
            lock (Requests) Requests.Add(request.Registration);
            var current = Interlocked.Increment(ref active);
            MaximumConcurrency = Math.Max(MaximumConcurrency, current);
            if (delay) await Task.Delay(20, cancellationToken);
            Interlocked.Decrement(ref active);
            return new(request.Registration, outcome(request.Registration), "recording", "v1", $"response-{request.Registration}", DateTimeOffset.UnixEpoch, null, null, null, [], null);
        }
    }
}
