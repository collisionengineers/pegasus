using Pegasus.Core.Cases;
using Pegasus.Core.Intake.ThirdPartyReports;

namespace Pegasus.Core.Tests.Intake.ThirdPartyReports;

/// <summary>
/// The Case a capture PDF's photographs joined takes the vehicle identity
/// intake recorded from it (operator, 12 October 2026).
/// </summary>
public sealed class VehicleCaptureFillTests
{
    private static readonly Guid CaseId = Guid.NewGuid();

    [Fact]
    public void TheReadingOffersMakeModelMileageAndAccidentDateButNeverTheRegistration()
    {
        var values = VehicleCaptureFill.CaseDataValues(Reading(make: "Toyota", model: "Prius", mileage: 251_525,
            accidentDate: new DateOnly(2026, 7, 17)));

        Assert.Equal(
            new Dictionary<string, string>
            {
                [CaseDataFieldNames.VehicleMake] = "Toyota",
                [CaseDataFieldNames.VehicleModel] = "Prius",
                [CaseDataFieldNames.VehicleMileage] = "251525",
                [CaseDataFieldNames.IncidentDate] = "2026-07-17"
            },
            values);
    }

    [Fact]
    public void AValueTheDocumentDidNotPrintIsNotOffered()
    {
        Assert.Empty(VehicleCaptureFill.CaseDataValues(Reading()));
    }

    [Fact]
    public async Task TheRecordedReadingFillsTheCase()
    {
        var pdf = VehicleCaptureFixture.Pdf();
        var receipt = VehicleCaptureFixture.Receipt(pdf);
        var analyses = new VehicleCaptureFixture.Analyses().Record(receipt, pdf, VehicleCaptureFixture.Rows(
            (ThirdPartyReportFields.Registration, "WP67YBO"),
            (ThirdPartyReportFields.Make, "Toyota"),
            (ThirdPartyReportFields.Vin, "JTDKB3FU703577122"),
            (ThirdPartyReportFields.Mileage, "251525"),
            (ThirdPartyReportFields.AccidentDate, "2026-07-17")));
        var fill = new VehicleCaptureFixture.FillSpy();

        await Command(analyses, fill, receipt).ExecuteAsync(CaseId, receipt.Id, CancellationToken.None);

        var (caseId, reading) = Assert.Single(fill.Calls);
        Assert.Equal(CaseId, caseId);
        Assert.Equal(pdf.Id, reading.IntakeAssetId);
        Assert.Equal("WP67YBO", reading.Registration);
        Assert.Equal("Toyota", reading.Make);
        Assert.Null(reading.Model);
        Assert.Equal("JTDKB3FU703577122", reading.Vin);
        Assert.Equal(251_525, reading.Mileage);
        Assert.Equal(new DateOnly(2026, 7, 17), reading.AccidentDate);
    }

    [Fact]
    public async Task AReceiptWithNoCaptureReadingFillsNothing()
    {
        var receipt = VehicleCaptureFixture.Receipt();
        var fill = new VehicleCaptureFixture.FillSpy();

        await Command(new VehicleCaptureFixture.Analyses(), fill, receipt)
            .ExecuteAsync(CaseId, receipt.Id, CancellationToken.None);

        Assert.Empty(fill.Calls);
    }

    [Fact]
    public async Task TwoCaptureDocumentsOnOneReceiptFillNothing()
    {
        var first = VehicleCaptureFixture.Pdf("first.pdf");
        var second = VehicleCaptureFixture.Pdf("second.pdf");
        var receipt = VehicleCaptureFixture.Receipt(first, second);
        var analyses = new VehicleCaptureFixture.Analyses()
            .Record(receipt, first, VehicleCaptureFixture.Rows((ThirdPartyReportFields.Registration, "WP67YBO")))
            .Record(receipt, second, VehicleCaptureFixture.Rows((ThirdPartyReportFields.Registration, "DK11SXP")));
        var fill = new VehicleCaptureFixture.FillSpy();

        await Command(analyses, fill, receipt).ExecuteAsync(CaseId, receipt.Id, CancellationToken.None);

        Assert.Empty(fill.Calls);
    }

    [Fact]
    public async Task AFaultReadingTheRecordingNeverFailsTheFiling()
    {
        var receipt = VehicleCaptureFixture.Receipt();
        var fill = new VehicleCaptureFixture.FillSpy();

        await Command(new VehicleCaptureFixture.Analyses { Fails = true }, fill, receipt)
            .ExecuteAsync(CaseId, receipt.Id, CancellationToken.None);

        Assert.Empty(fill.Calls);
    }

    private static FillCaseFromVehicleCapture Command(
        VehicleCaptureFixture.Analyses analyses,
        VehicleCaptureFixture.FillSpy fill,
        params Pegasus.Core.Intake.IntakeReceipt[] receipts) =>
        new(new VehicleCaptureFixture.Receipts(receipts), new RecordedVehicleCaptureReading(analyses), fill);

    private static VehicleCaptureReading Reading(
        string? make = null,
        string? model = null,
        long? mileage = null,
        DateOnly? accidentDate = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "LINE_LEVEL_ESTIMATE.pdf", "WP67YBO", make, model, null, mileage, accidentDate);
}
