using Pegasus.Core.Cases;
using Pegasus.Core.PhotoCapture;
using static Pegasus.Core.Tests.PhotoCapture.PhotoCaptureTestData;

namespace Pegasus.Core.Tests.PhotoCapture;

public sealed class PhotoCaptureCaseFieldsTests
{
    [Fact]
    public void TheTypedDetailsOfferTheFiveCaseFieldsAndNeverTheRegistration()
    {
        var capture = Capture(
            incidentDate: new DateOnly(2026, 10, 28),
            details: new PhotoCaptureDetails(InspectionLocation: "7 Example Trading Estate, Birkenhead CH41 1AB", Mileage: 48_210, Roadworthy: "No"));

        var values = PhotoCaptureCaseFields.Values(capture);

        Assert.Equal("Sam Taylor", values[CaseDataFieldNames.ClaimantName]);
        Assert.Equal("07700 900123", values[CaseDataFieldNames.ClaimantContactNumber]);
        Assert.Equal("2026-10-28", values[CaseDataFieldNames.IncidentDate]);
        Assert.Equal("48210", values[CaseDataFieldNames.VehicleMileage]);
        Assert.Equal("7 Example Trading Estate, Birkenhead CH41 1AB", values[CaseDataFieldNames.InspectionAddress]);
        Assert.DoesNotContain(CaseDataFieldNames.VehicleRegistration, values.Keys);
        Assert.Equal(5, values.Count);
    }

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

    [Fact]
    public void ACaptureKnowsWhenItHasAnUnsettledSend()
    {
        Assert.False(Capture().HasUnsettledSend(Now));
        Assert.True(Capture(sentAtUtc: Now).HasUnsettledSend(Now));
        Assert.False(Capture(sentAtUtc: Now, settledAtUtc: Now.AddMinutes(1)).HasUnsettledSend(Now.AddMinutes(2)));
        Assert.True(Capture(sentAtUtc: Now.AddMinutes(5), settledAtUtc: Now).HasUnsettledSend(Now.AddMinutes(6)));
        Assert.True(Capture(acceptedItemCount: 3).HasUnsettledSend(Now.AddDays(8)));
        Assert.Equal(2, Capture(settledBatches: 1).CurrentBatch);
    }
}
