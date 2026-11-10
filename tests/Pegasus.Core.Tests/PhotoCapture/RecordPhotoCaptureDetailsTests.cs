using Pegasus.Core.ImageIntake;
using Pegasus.Core.PhotoCapture;
using Pegasus.Core.Tests.ImageIntake;
using static Pegasus.Core.Tests.PhotoCapture.PhotoCaptureTestData;

namespace Pegasus.Core.Tests.PhotoCapture;

/// <summary>
/// What the sender types is recorded, and a standing capture is routed on it
/// again each time until its first photo (FRD-28 "Registration"): a
/// registration or accident date changed before then is routed on, not just
/// stored. A link sent from a Case keeps its Case's registration.
/// </summary>
public sealed class RecordPhotoCaptureDetailsTests
{
    private static readonly DateOnly TypedDate = new(2026, 10, 28);

    [Fact]
    public async Task ALinkSentFromACaseKeepsItsCasesRegistrationAndIsNeverRouted()
    {
        var store = new RecordingStore(Capture());

        var recorded = await store.Use(new CaseCandidates())
            .ExecuteAsync(store.Current, standingLink: null, Form("AB12CDE", TypedDate), CancellationToken.None);

        Assert.Empty(store.Routed);
        var details = Assert.Single(store.Details);
        Assert.Equal("RY65OFG", details.Registration);
        Assert.Equal(TypedDate, details.IncidentDate);
        Assert.Equal("RY65OFG", recorded.NormalizedRegistration);
        Assert.Equal(PhotoCaptureDestination.Case, recorded.Destination);
    }

    [Fact]
    public async Task AStandingCaptureIsRoutedAgainOnAChangedRegistrationBeforeItsFirstPhoto()
    {
        var store = new RecordingStore(Capture(PhotoCaptureOrigin.StandingStaff, PhotoCaptureDestination.VehicleImages));
        var candidate = Candidate("AB12CDE");

        var recorded = await store.Use(new CaseCandidates(candidate))
            .ExecuteAsync(store.Current, standingLink: null, Form("AB12CDE", null), CancellationToken.None);

        var routed = Assert.Single(store.Routed);
        Assert.Equal((PhotoCaptureDestination.Case, (Guid?)candidate.CaseId), routed);
        Assert.Equal("AB12CDE", Assert.Single(store.Details).Registration);
        Assert.Equal(PhotoCaptureDestination.Case, recorded.Destination);
        Assert.Equal(candidate.CaseId, recorded.CaseId);
    }

    [Fact]
    public async Task AStandingCaptureIsRoutedAgainOnAChangedAccidentDateBeforeItsFirstPhoto()
    {
        var store = new RecordingStore(Capture(PhotoCaptureOrigin.StandingStaff, PhotoCaptureDestination.Case));
        var otherAccident = Candidate("RY65OFG", new DateOnly(2026, 8, 14));

        var recorded = await store.Use(new CaseCandidates(otherAccident))
            .ExecuteAsync(store.Current, standingLink: null, Form(null, TypedDate), CancellationToken.None);

        Assert.Equal((PhotoCaptureDestination.VehicleImages, (Guid?)null), Assert.Single(store.Routed));
        Assert.Equal(TypedDate, Assert.Single(store.Details).IncidentDate);
        Assert.Equal(PhotoCaptureDestination.VehicleImages, recorded.Destination);
    }

    [Fact]
    public async Task AStandingCaptureWithAPhotoIsNotRoutedAgainAndKeepsItsRegistration()
    {
        var store = new RecordingStore(Capture(
            PhotoCaptureOrigin.StandingStaff,
            PhotoCaptureDestination.VehicleImages,
            acceptedItemCount: 1));
        var details = store.Use(new CaseCandidates(Candidate("RY65OFG")));

        await details.ExecuteAsync(store.Current, standingLink: null, Form("RY65OFG", TypedDate), CancellationToken.None);
        var refusal = await Assert.ThrowsAsync<PhotoCaptureRefusalException>(() =>
            details.ExecuteAsync(store.Current, standingLink: null, Form("AB12CDE", null), CancellationToken.None));

        Assert.Equal(PhotoCaptureRefusals.RegistrationLocked, refusal.Code);
        Assert.Empty(store.Routed);
        Assert.Single(store.Details);
    }

    /// <summary>
    /// The store refuses a new routing once an item row exists, which a photo
    /// still uploading has before it counts as received: the refusal comes
    /// before anything is written, so the typed details are not stored
    /// against a routing that never saw them.
    /// </summary>
    [Fact]
    public async Task ARoutingTheStoreRefusesWritesNoDetails()
    {
        var store = new RecordingStore(Capture(PhotoCaptureOrigin.Website, PhotoCaptureDestination.VehicleImages))
        {
            RefusesRouting = true
        };

        var refusal = await Assert.ThrowsAsync<PhotoCaptureRefusalException>(() =>
            store.Use(new CaseCandidates(Candidate("AB12CDE")))
                .ExecuteAsync(store.Current, standingLink: null, Form("AB12CDE", null), CancellationToken.None));

        Assert.Equal(PhotoCaptureRefusals.RegistrationLocked, refusal.Code);
        Assert.Empty(store.Details);
    }

    private static StandingCaptureForm Form(string? registration, DateOnly? incidentDate) =>
        new(registration, incidentDate, CustomerName: null, Mobile: null, Email: null, PhotoCaptureDetails.Empty);

    private static ImageIntakeCaseCandidate Candidate(string registration, DateOnly? incidentDate = null) =>
        new(
            Guid.NewGuid(),
            "QDOS26001",
            CaseVersion: 3,
            registration,
            new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.Zero),
            IncidentDate: incidentDate);

    private sealed class CaseCandidates(params ImageIntakeCaseCandidate[] candidates) : IImageIntakeCaseCandidates
    {
        public Task<IReadOnlyList<ImageIntakeCaseCandidate>> FindEligibleByRegistrationAsync(
            string normalizedVehicleRegistration,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ImageIntakeCaseCandidate>>(candidates);
    }

    /// <summary>The capture store's two writes this use case makes, kept in order; any other call is not expected.</summary>
    private sealed class RecordingStore
    {
        private readonly AutomaticImageIntakeTests.Forwarder forwarder =
            AutomaticImageIntakeTests.Forwarder.For<IPhotoCaptureStore>(null);

        public RecordingStore(Core.PhotoCapture.PhotoCapture capture)
        {
            Current = capture;
            forwarder.Overrides[nameof(IPhotoCaptureStore.RecordRoutingAsync)] = args =>
            {
                if (RefusesRouting)
                {
                    return Task.FromException<Core.PhotoCapture.PhotoCapture>(new PhotoCaptureRefusalException(
                        PhotoCaptureRefusals.RegistrationLocked,
                        "The registration cannot change once photos have been sent."));
                }

                var destination = (PhotoCaptureDestination)args[2]!;
                var caseId = (Guid?)args[3];
                Routed.Add((destination, caseId));
                Current = Current with
                {
                    Destination = destination,
                    CaseId = caseId,
                    RoutingReason = (string)args[4]!,
                    Version = Current.Version + 1
                };
                return Task.FromResult(Current);
            };
            forwarder.Overrides[nameof(IPhotoCaptureStore.RecordDetailsAsync)] = args =>
            {
                var registration = (string?)args[1];
                var incidentDate = (DateOnly?)args[2];
                Details.Add((registration, incidentDate));
                Current = Current with
                {
                    NormalizedRegistration = registration,
                    IncidentDate = incidentDate,
                    Version = Current.Version + 1
                };
                return Task.FromResult(Current);
            };
        }

        public Core.PhotoCapture.PhotoCapture Current { get; private set; }

        public bool RefusesRouting { get; init; }

        public List<(PhotoCaptureDestination Destination, Guid? CaseId)> Routed { get; } = [];

        public List<(string? Registration, DateOnly? IncidentDate)> Details { get; } = [];

        public RecordPhotoCaptureDetails Use(IImageIntakeCaseCandidates candidates) =>
            new((IPhotoCaptureStore)forwarder.Proxy, new RoutePhotoCapture(candidates));
    }
}
