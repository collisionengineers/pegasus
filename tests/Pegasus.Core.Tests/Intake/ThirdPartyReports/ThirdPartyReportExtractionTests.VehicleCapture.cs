using Pegasus.Core.Intake;
using Pegasus.Core.Intake.ThirdPartyReports;

namespace Pegasus.Core.Tests.Intake.ThirdPartyReports;

/// <summary>
/// A Tractable capture PDF is a document type, not a Principal or an issuer
/// (FRD-02 EXT-17): its vehicle identity is read and nothing else, and it
/// yields no report verdict.
/// </summary>
public sealed partial class ThirdPartyReportExtractionTests
{
    [Fact]
    public void ACapturePdfReadsItsVehicleIdentityAndNothingElse()
    {
        var result = Capture(VehicleCaptureSamples.JulyFirstPage);

        Assert.Equal(ThirdPartySelectionReason.NonReportDocumentRole, result.Selection.Reason);
        Assert.Equal(ThirdPartyDocumentRole.VehicleCapture, result.Selection.DocumentRole);
        Assert.Null(result.Selection.Family);
        Assert.Null(result.Candidate);
        Assert.Empty(result.Findings);
        Assert.Equal("WP67YBO", Value(result, ThirdPartyReportFields.Registration));
        Assert.Equal("Toyota", Value(result, ThirdPartyReportFields.Make));
        Assert.Equal("Prius", Value(result, ThirdPartyReportFields.Model));
        Assert.Equal("JTDKB3FU703577122", Value(result, ThirdPartyReportFields.Vin));
        Assert.Equal("251525", Value(result, ThirdPartyReportFields.Mileage));
        Assert.Equal("2026-07-17", Value(result, ThirdPartyReportFields.AccidentDate));
        Assert.All(result.Candidates, row =>
            Assert.Equal(ThirdPartyReportProfiles.VehicleCaptureDocumentRole, row.DocumentRole));
        Assert.Equal(
            [
                ThirdPartyReportFields.Issuer,
                ThirdPartyReportFields.Registration,
                ThirdPartyReportFields.Make,
                ThirdPartyReportFields.Model,
                ThirdPartyReportFields.Vin,
                ThirdPartyReportFields.Mileage,
                ThirdPartyReportFields.AccidentDate
            ],
            result.Candidates.Select(row => row.Field));
        Assert.True(ThirdPartyReportAnalysis.IsRecordable(result));
    }

    [Fact]
    public void TheClaimIdLayoutPrintsNoAccidentDateAndAMixedCaseRegistration()
    {
        var result = Capture(VehicleCaptureSamples.AugustFirstPage);

        Assert.Equal("DK11SXP", Value(result, ThirdPartyReportFields.Registration));
        Assert.Equal("Audi", Value(result, ThirdPartyReportFields.Make));
        Assert.Equal("A3", Value(result, ThirdPartyReportFields.Model));
        Assert.Equal("78904", Value(result, ThirdPartyReportFields.Mileage));
        var accidentDate = Assert.Single(result.Candidates, row => row.Field == ThirdPartyReportFields.AccidentDate);
        Assert.Equal(SourceCandidateDisposition.Missing, accidentDate.Disposition);
    }

    [Fact]
    public void AnEmptyCellIsNotAValue()
    {
        var result = Capture(VehicleCaptureSamples.JulyFirstPage
            .Replace("Producer Toyota", "Producer -", StringComparison.Ordinal)
            .Replace("Model Prius", "Model -", StringComparison.Ordinal));

        Assert.Equal(
            SourceCandidateDisposition.Missing,
            Assert.Single(result.Candidates, row => row.Field == ThirdPartyReportFields.Make).Disposition);
        Assert.Equal(
            SourceCandidateDisposition.Missing,
            Assert.Single(result.Candidates, row => row.Field == ThirdPartyReportFields.Model).Disposition);
    }

    [Fact]
    public void ACapturePdfNamesNoIssuerAndDoesNotHoldBackImageAutomation()
    {
        var mail = VehicleCaptureSamples.Mail();

        Assert.False(ThirdPartyReportProfiles.HasRecognizedNonImageDocument(mail));
        Assert.DoesNotContain(ThirdPartyReportProfiles.KnownIssuers, issuer =>
            issuer.Contains("Tractable", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TheQuoteHeadingIsTheSameDocumentType()
    {
        var result = Capture(
            VehicleCaptureSamples.JulyFirstPage,
            VehicleCaptureSamples.EstimatePage.Replace("Preliminary AI Estimate", "Preliminary AI quote", StringComparison.Ordinal));

        Assert.Equal(ThirdPartyDocumentRole.VehicleCapture, result.Selection.DocumentRole);
        Assert.Equal("WP67YBO", Value(result, ThirdPartyReportFields.Registration));
    }

    private static ThirdPartyReportExtractionResult Capture(
        string firstPage,
        string estimatePage = VehicleCaptureSamples.EstimatePage) =>
        ThirdPartyReportExtraction.Extract(
            ThirdPartyReportDocuments.ForDocument(
                VehicleCaptureSamples.Mail(firstPage, estimatePage),
                VehicleCaptureSamples.PdfLabel),
            Context());
}
