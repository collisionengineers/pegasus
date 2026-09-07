using Pegasus.Core.Vehicle;

namespace Pegasus.Core.Tests.Vehicle;

public sealed class VehicleRegistrationCandidateLookupTests
{
    [Fact]
    public void CandidateMapIsCompleteStableDistinctOrderedAndBounded()
    {
        var candidates = VehicleRegistrationCandidateLookup.GenerateCandidates(" O100IOO ");
        Assert.Equal(["O100IOO", "0100IOO", "OI00IOO", "OIO010O", "OIO0100"], candidates);
        Assert.Equal(candidates.Count, candidates.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(candidates, VehicleRegistrationCandidateLookup.GenerateCandidates(" O100IOO "));
        Assert.InRange(candidates.Count, 1, 8);

        var maximumUncappedCount = 0;
        foreach (var length in Enumerable.Range(1, 7))
        foreach (var raw in AmbiguousReadings(length))
        {
            var uncapped = VehicleRegistrationCandidateLookup.GenerateValidCandidatesUncapped(raw);
            maximumUncappedCount = Math.Max(maximumUncappedCount, uncapped.Count);
            Assert.InRange(uncapped.Count, 0, 8);
        }
        Assert.Equal(8, maximumUncappedCount);

        Assert.Equal(
            ["O0OOO", "OOO0O", "00OOO", "OOO00", "000OO", "OO000", "0000O", "O0000"],
            VehicleRegistrationCandidateLookup.GenerateCandidates("OOOOO"));
    }

    [Theory]
    [InlineData("AB12CDE")]
    [InlineData("A123BCD")]
    [InlineData("ABC123D")]
    [InlineData("1234ABC")]
    [InlineData("ABC1234")]
    [InlineData("OIZ1234")]
    public void SupportedCurrentPrefixSuffixDatelessAndNorthernIrelandFormsRemainCandidates(string raw) =>
        Assert.Contains(raw, VehicleRegistrationCandidateLookup.GenerateCandidates(raw));

    [Theory]
    [InlineData("AB-12CDE")]
    [InlineData("AB12/CDE")]
    [InlineData("AB12CDÉ")]
    [InlineData("AB12CDE9")]
    [InlineData("not a registration")]
    public void UnsupportedSeparatorsMalformedOverlongAndNonAsciiReadingsFailClosed(string raw) =>
        Assert.Empty(VehicleRegistrationCandidateLookup.GenerateCandidates(raw));

    [Theory]
    [InlineData("O123ABC", "O123ABC", "0123ABC")]
    [InlineData("AI12ABC", "AI12ABC", "A112ABC")]
    [InlineData("OI01OIO", "OI01OIO", "0101OIO")]
    public void OOnlyIOnlyAndMixedReadingsRetainOriginalBeforeAlternatives(string raw, string original, string alternative)
    {
        var candidates = VehicleRegistrationCandidateLookup.GenerateCandidates(raw);
        Assert.Equal(original, candidates[0]);
        Assert.Contains(alternative, candidates);
    }

    [Theory]
    [InlineData(MachineReadRegistrationSource.EmbeddedDocumentText)]
    [InlineData(MachineReadRegistrationSource.StaffConfirmed)]
    [InlineData(MachineReadRegistrationSource.CaseSearch)]
    public async Task NonMachineSourcesNeverInvokeLookup(MachineReadRegistrationSource source)
    {
        var adapter = new RecordingAdapter((registration, _) => Valid(registration, VehicleLookupOutcome.NotFound));
        await Assert.ThrowsAsync<ArgumentException>(() => new VehicleRegistrationCandidateLookup(adapter)
            .LookupAsync(new("O100IOO", source, "recorded-source")));
        Assert.Empty(adapter.Requests);
    }

    [Fact]
    public async Task OneViableAfterAllConclusiveNotFoundIsAcceptedAndLedgerPreserved()
    {
        var adapter = new RecordingAdapter((registration, _) => Valid(registration,
            registration == "OI00IOO" ? VehicleLookupOutcome.Current : VehicleLookupOutcome.NotFound));
        var reading = new MachineReadRegistration(" O100IOO ", MachineReadRegistrationSource.DocumentOcr, "document-7/page-2");
        var result = await new VehicleRegistrationCandidateLookup(adapter).LookupAsync(reading);
        Assert.Equal("OI00IOO", result.AcceptedRegistration);
        Assert.Equal(reading, result.Reading);
        Assert.Equal(result.Candidates, result.Attempts.Select(attempt => attempt.Registration));
        Assert.All(result.Attempts, attempt => Assert.Equal($"response-{attempt.Registration}", attempt.Result.ResponseIdentity));
        Assert.Equal(result.Candidates.Count, adapter.Requests.Count);
    }

    [Theory]
    [InlineData(VehicleLookupOutcome.Throttled)]
    [InlineData(VehicleLookupOutcome.Unavailable)]
    [InlineData(VehicleLookupOutcome.Failed)]
    public async Task AnyUnresolvedAttemptKeepsSingleViableCandidateAmbiguous(VehicleLookupOutcome unresolved)
    {
        var adapter = new RecordingAdapter((registration, order) => Valid(registration,
            order == 0 ? VehicleLookupOutcome.Partial : unresolved));
        var result = await new VehicleRegistrationCandidateLookup(adapter).LookupAsync(
            new("O100IOO", MachineReadRegistrationSource.VehicleRecognition, "image-4"));
        Assert.True(result.IsAmbiguous);
        Assert.Null(result.AcceptedRegistration);
        Assert.Contains(result.Attempts, attempt => attempt.Result.Outcome == unresolved);
    }

    [Fact]
    public async Task ConcurrentMultipleViableResultsRemainAmbiguousWithoutDiscardingEither()
    {
        var adapter = new RecordingAdapter((registration, _) => Valid(registration, VehicleLookupOutcome.Stale), delay: true);
        var result = await new VehicleRegistrationCandidateLookup(adapter).LookupAsync(
            new("O100IOO", MachineReadRegistrationSource.DocumentOcr, "ocr-operation-9"));
        Assert.True(result.IsAmbiguous);
        Assert.Null(result.AcceptedResult);
        Assert.Equal(5, result.Attempts.Count);
        Assert.True(adapter.MaximumConcurrency > 1);
    }

    [Fact]
    public async Task AllNotFoundCandidatesRemainAmbiguousWithTheCompleteLedger()
    {
        var adapter = new RecordingAdapter((registration, _) => Valid(registration, VehicleLookupOutcome.NotFound));
        var result = await new VehicleRegistrationCandidateLookup(adapter).LookupAsync(
            new("O100IOO", MachineReadRegistrationSource.DocumentOcr, "ocr-operation-11"));
        Assert.True(result.IsAmbiguous);
        Assert.Null(result.AcceptedResult);
        Assert.Equal(result.Candidates.Count, result.Attempts.Count);
    }

    [Fact]
    public async Task MalformedOrMismatchedResponseRefusesAcceptanceAfterEveryCandidateWasAttempted()
    {
        var candidates = VehicleRegistrationCandidateLookup.GenerateCandidates("O100IOO");
        var adapter = new RecordingAdapter((registration, order) => order switch
        {
            0 => Valid("MISMATCH", VehicleLookupOutcome.NotFound),
            1 => Valid(registration, VehicleLookupOutcome.NotFound) with { Provider = string.Empty },
            _ => Valid(registration, VehicleLookupOutcome.NotFound)
        });
        await Assert.ThrowsAsync<InvalidDataException>(() => new VehicleRegistrationCandidateLookup(adapter)
            .LookupAsync(new("O100IOO", MachineReadRegistrationSource.DocumentOcr, "ocr-operation-10")));
        Assert.Equal(candidates.Count, adapter.Requests.Count);
        Assert.Equal(candidates.Order(), adapter.Requests.Order());
    }

    private static IEnumerable<string> AmbiguousReadings(int length)
    {
        const string alphabet = "O0I1";
        var count = 1 << (length * 2);
        for (var ordinal = 0; ordinal < count; ordinal++)
        {
            var value = new char[length];
            var remainder = ordinal;
            for (var index = 0; index < length; index++)
            {
                value[index] = alphabet[remainder & 3];
                remainder >>= 2;
            }
            yield return new(value);
        }
    }

    private static VehicleLookupResult Valid(string registration, VehicleLookupOutcome outcome)
    {
        var evidence = outcome is VehicleLookupOutcome.Current or VehicleLookupOutcome.Stale or VehicleLookupOutcome.Partial;
        var unresolved = outcome is VehicleLookupOutcome.Throttled or VehicleLookupOutcome.Unavailable or VehicleLookupOutcome.Failed;
        return new(registration, outcome, "recording", "v1", $"response-{registration}", DateTimeOffset.UnixEpoch,
            evidence ? DateTimeOffset.UnixEpoch : null, evidence ? DateTimeOffset.UnixEpoch : null,
            evidence ? new VehicleDetails("Make", "Model", 2020, 1000, "Petrol") : null, [],
            unresolved ? new VehicleLookupFailure("temporary", true) : null);
    }

    private sealed class RecordingAdapter(Func<string, int, VehicleLookupResult> result, bool delay = false) : IVehicleLookupAdapter
    {
        private int active;
        private int sequence;
        public List<string> Requests { get; } = [];
        public int MaximumConcurrency { get; private set; }

        public async Task<VehicleLookupResult> LookupAsync(VehicleLookupRequest request, CancellationToken cancellationToken)
        {
            int order;
            lock (Requests) { order = sequence++; Requests.Add(request.Registration); }
            var current = Interlocked.Increment(ref active);
            MaximumConcurrency = Math.Max(MaximumConcurrency, current);
            if (delay) await Task.Delay(20, cancellationToken);
            Interlocked.Decrement(ref active);
            return result(request.Registration, order);
        }
    }
}
