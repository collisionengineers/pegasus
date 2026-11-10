using Pegasus.Core.Cases;
using Pegasus.Core.PhotoCapture;
using Pegasus.Core.Vehicle;
using static Pegasus.Core.Tests.PhotoCapture.PhotoCaptureTestData;

namespace Pegasus.Core.Tests.PhotoCapture;

public sealed class PhotoCaptureCaseFieldsTests
{
    /// <summary>
    /// The typed inspection location stays on the capture with the photos;
    /// staff type the address if they want it (operator, 9 November 2026;
    /// R811, narrowing R783).
    /// </summary>
    [Fact]
    public void TheTypedDetailsOfferTheFourCaseFieldsAndNeverTheRegistrationOrTheLocation()
    {
        var capture = Capture(
            incidentDate: new DateOnly(2026, 10, 28),
            details: new PhotoCaptureDetails(InspectionLocation: "7 Example Trading Estate, Birkenhead CH41 1AB", Mileage: 48_210, Roadworthy: "No"));

        var values = PhotoCaptureCaseFields.Values(capture);

        Assert.Equal("Sam Taylor", values[CaseDataFieldNames.ClaimantName]);
        Assert.Equal("07700 900123", values[CaseDataFieldNames.ClaimantContactNumber]);
        Assert.Equal("2026-10-28", values[CaseDataFieldNames.IncidentDate]);
        Assert.Equal("48210", values[CaseDataFieldNames.VehicleMileage]);
        Assert.DoesNotContain(CaseDataFieldNames.InspectionAddress, values.Keys);
        Assert.DoesNotContain(CaseDataFieldNames.VehicleRegistration, values.Keys);
        Assert.Equal(4, values.Count);
    }

    /// <summary>
    /// A typed mileage ranks below anything from the instruction and above
    /// the DVSA estimate, so the estimate never replaces it, and the report
    /// keeps the instructing principal's sentence whoever typed it
    /// (operator, 9 November 2026; R810). The fill writes it as a Fact with
    /// Photo link provenance, and a lookup fills only a field that holds no
    /// Fact.
    /// </summary>
    [Fact]
    public void TheEstimateNeverReplacesAPhotoLinkMileageAndTheReportKeepsThePrincipalSentence()
    {
        var photoLinkMileage = new CaseDataSource(
            CaseDataSourceKind.PhotoLink,
            Guid.NewGuid().ToString("D"),
            "Photo link",
            PhotoCaptureCaseFields.PolicyKey,
            PhotoCaptureCaseFields.PolicyVersion);

        Assert.False(VehicleLookupFillPolicy.Fills(hasFact: true, hasConfirmed: false));
        Assert.False(CaseVehicleMileageSourcePolicy.IsEstimated(photoLinkMileage));
        Assert.Equal(
            CaseVehicleMileageSourcePolicy.Principal,
            CaseVehicleMileageSourcePolicy.Resolve(photoLinkMileage.Kind, hasMileage: true));
    }

    /// <summary>
    /// A typed mileage replaces a DVSA estimate, and only that: a value from
    /// the instruction, one staff entered, an exact MOT reading and a
    /// confirmed value are never replaced (operator, 10 November 2026; R843).
    /// </summary>
    [Fact]
    public void ATypedMileageReplacesTheDvsaEstimateAndItsUnit()
    {
        var estimate = Source(CaseDataSourceKind.VehicleLookup, MotMileageEstimationPolicy.MethodKey);

        Assert.True(PhotoCaptureCaseFields.Replaces(CaseDataFieldNames.VehicleMileage, estimate, hasConfirmed: false));
        Assert.True(PhotoCaptureCaseFields.Replaces(CaseDataFieldNames.VehicleMileageUnit, estimate, hasConfirmed: false));
    }

    [Fact]
    public void AnExactMotReadingIsNotReplaced() =>
        Assert.False(PhotoCaptureCaseFields.Replaces(
            CaseDataFieldNames.VehicleMileage,
            Source(CaseDataSourceKind.VehicleLookup, VehicleMileagePolicy.MethodKey),
            hasConfirmed: false));

    [Theory]
    [InlineData(CaseDataSourceKind.IntakeEvidence)]
    [InlineData(CaseDataSourceKind.CaseAcceptance)]
    [InlineData(CaseDataSourceKind.StaffCorrection)]
    [InlineData(CaseDataSourceKind.PhotoLink)]
    public void AMileageFromAnyOtherSourceIsNotReplaced(CaseDataSourceKind kind) =>
        Assert.False(PhotoCaptureCaseFields.Replaces(
            CaseDataFieldNames.VehicleMileage, Source(kind, "instruction-extraction"), hasConfirmed: false));

    [Fact]
    public void AConfirmedValueIsNeverReplacedAndAnEmptyFieldIsFilled()
    {
        var estimate = Source(CaseDataSourceKind.VehicleLookup, MotMileageEstimationPolicy.MethodKey);

        Assert.False(PhotoCaptureCaseFields.Replaces(CaseDataFieldNames.VehicleMileage, estimate, hasConfirmed: true));
        Assert.False(PhotoCaptureCaseFields.Replaces(CaseDataFieldNames.VehicleMileage, existingFact: null, hasConfirmed: true));
        Assert.True(PhotoCaptureCaseFields.Replaces(CaseDataFieldNames.VehicleMileage, existingFact: null, hasConfirmed: false));
    }

    /// <summary>Only the mileage gives way to an estimate: any other field's fact stands.</summary>
    [Fact]
    public void AnotherFieldsFactIsNotReplaced() =>
        Assert.False(PhotoCaptureCaseFields.Replaces(
            CaseDataFieldNames.ClaimantName,
            Source(CaseDataSourceKind.VehicleLookup, MotMileageEstimationPolicy.MethodKey),
            hasConfirmed: false));

    [Fact]
    public void NothingTypedOffersNothing()
    {
        var capture = Capture(customerName: null, mobile: null);

        Assert.Empty(PhotoCaptureCaseFields.Values(capture));
    }

    [Theory]
    [InlineData(PhotoCaptureOrigin.CaseLink, "Photo link")]
    [InlineData(PhotoCaptureOrigin.Website, "Photo link (website)")]
    [InlineData(PhotoCaptureOrigin.StandingStaff, "Photo link (standing)")]
    public void TheSourceLabelNamesWhereTheDetailsCameFrom(PhotoCaptureOrigin origin, string label) =>
        Assert.Equal(label, PhotoCaptureCaseFields.SourceLabel(Capture(origin: origin, destination: PhotoCaptureDestination.VehicleImages)));

    /// <summary>
    /// A batch is due on a Send since the last settle, an expiry or a
    /// cancellation, whose photos still settle (operator, 9 November 2026;
    /// R794).
    /// </summary>
    [Fact]
    public void ACaptureIsDueToSettleOnASendSinceItsLastSettleAnExpiryOrACancellation()
    {
        Assert.False(Capture().IsDueToSettle(Now));
        Assert.True(Capture(sentAtUtc: Now).IsDueToSettle(Now));
        Assert.False(Capture(sentAtUtc: Now, settledAtUtc: Now.AddMinutes(1)).IsDueToSettle(Now.AddMinutes(2)));
        Assert.True(Capture(sentAtUtc: Now.AddMinutes(5), settledAtUtc: Now).IsDueToSettle(Now.AddMinutes(6)));
        Assert.True(Capture(acceptedItemCount: 3).IsDueToSettle(Now.AddDays(8)));
        Assert.True(Capture(revokedAtUtc: Now).IsDueToSettle(Now));
        Assert.Equal(2, Capture(settledBatches: 1).CurrentBatch);
    }

    /// <summary>
    /// Originals follow only while a capture files on its Case as each photo
    /// arrives: not before it has a Case, not once its batch went to a
    /// Vehicle images record, and not when the link was sent without them
    /// (operator, 9 November 2026; R801).
    /// </summary>
    [Fact]
    public void AnItemDeclaresTheCapturesCaseOnlyWhileItsPhotosFileThere()
    {
        var onCase = Capture(origin: PhotoCaptureOrigin.StandingStaff);

        Assert.Equal(onCase.CaseId, onCase.DeclaredCaseId);
        Assert.Null(Capture(origin: PhotoCaptureOrigin.Website, destination: PhotoCaptureDestination.VehicleImages).DeclaredCaseId);
        Assert.Null(Capture(origin: PhotoCaptureOrigin.Website, destination: PhotoCaptureDestination.Undecided).DeclaredCaseId);
    }

    [Fact]
    public void OnlyACaptureFilingOnACaseAsItArrivesTakesOriginals()
    {
        Assert.True(Capture().TakesOriginals);
        Assert.True(Capture(origin: PhotoCaptureOrigin.StandingStaff).TakesOriginals);
        Assert.False(Capture(origin: PhotoCaptureOrigin.Website, destination: PhotoCaptureDestination.Undecided).TakesOriginals);
        Assert.False(Capture(origin: PhotoCaptureOrigin.Website, destination: PhotoCaptureDestination.VehicleImages).TakesOriginals);
        Assert.False((Capture() with { SubmissionGroupId = Guid.NewGuid() }).TakesOriginals);
        Assert.False((Capture() with { OriginalsInBackground = false }).TakesOriginals);
    }

    private static CaseDataSource Source(CaseDataSourceKind kind, string policyKey) =>
        new(kind, Guid.NewGuid().ToString("D"), "fixture", policyKey, 1);
}
