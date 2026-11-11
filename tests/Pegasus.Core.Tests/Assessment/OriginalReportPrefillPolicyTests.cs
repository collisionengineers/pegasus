using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Assessment;

/// <summary>
/// How a filed original report's printed words become the Original report
/// cells (v28 P51, #840). The expected codes are the vocabulary's literals,
/// compared independently of the policy's own tables.
/// </summary>
public sealed class OriginalReportPrefillPolicyTests
{
    private const string Sha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Theory]
    [InlineData("Yes", "roadworthy")]
    [InlineData("YES", "roadworthy")]
    [InlineData("Roadworthy", "roadworthy")]
    [InlineData("ROADWORTHY", "roadworthy")]
    [InlineData(" roadworthy ", "roadworthy")]
    [InlineData("No", "unroadworthy")]
    [InlineData("NO", "unroadworthy")]
    [InlineData("Unroadworthy", "unroadworthy")]
    [InlineData("UNROADWORTHY", "unroadworthy")]
    [InlineData("Not roadworthy", null)]
    [InlineData("Unknown", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void PrintedRoadworthinessMapsToItsCellCode(string? printed, string? expected) =>
        Assert.Equal(expected, OriginalReportPrefillPolicy.RoadworthinessCode(printed));

    [Theory]
    [InlineData("Repairable", "repairable")]
    [InlineData("REPAIRABLE", "repairable")]
    [InlineData("REPAIR", "repairable")]
    [InlineData("TOTAL LOSS", "total_loss")]
    [InlineData("Total  loss", "total_loss")]
    [InlineData("Uneconomical", null)]
    [InlineData("Cash in lieu", null)]
    [InlineData("Contract Repair", null)]
    [InlineData("CAT S", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void PrintedOutcomeMapsToItsCellCode(string? printed, string? expected) =>
        Assert.Equal(expected, OriginalReportPrefillPolicy.OutcomeCode(printed));

    [Fact]
    public void EveryCodeTheTablesProduceIsOneTheCellsHold()
    {
        var roadworthiness = AssessmentVocabulary.Definitions[AssessmentVocabulary.OriginalReportRoadworthiness].Codes!;
        var outcome = AssessmentVocabulary.Definitions[AssessmentVocabulary.OriginalReportOutcome].Codes!;

        Assert.Contains(OriginalReportPrefillPolicy.RoadworthinessCode("yes")!, roadworthiness);
        Assert.Contains(OriginalReportPrefillPolicy.RoadworthinessCode("no")!, roadworthiness);
        Assert.Contains(OriginalReportPrefillPolicy.OutcomeCode("repair")!, outcome);
        Assert.Contains(OriginalReportPrefillPolicy.OutcomeCode("total loss")!, outcome);
    }

    [Fact]
    public void AReadReportFillsAllFourCells()
    {
        var writes = OriginalReportPrefillPolicy.Writes(
            new(Sha256, "Laird Assessors", "2026-09-01", "roadworthy", "repairable", false),
            intakeVerdict: null);

        Assert.Equal(
            new Dictionary<string, string>
            {
                ["original_report.assessor"] = "Laird Assessors",
                ["original_report.report_date"] = "2026-09-01",
                ["original_report.roadworthiness"] = "roadworthy",
                ["original_report.outcome"] = "repairable"
            },
            writes);
    }

    [Theory]
    // The report printed no outcome: the intake verdict stands in.
    [InlineData(null, false, AuditAssessment.Repairable, "repairable")]
    [InlineData(null, false, AuditAssessment.TotalLoss, "total_loss")]
    [InlineData(null, false, null, null)]
    // The report printed one: it fills, and agrees with or overrides no verdict.
    [InlineData("repairable", false, null, "repairable")]
    [InlineData("total_loss", false, AuditAssessment.TotalLoss, "total_loss")]
    // The report and the verdict disagree: staff decide.
    [InlineData("repairable", false, AuditAssessment.TotalLoss, null)]
    [InlineData("total_loss", false, AuditAssessment.Repairable, null)]
    // The report printed an outcome the cells cannot hold: no fallback.
    [InlineData(null, true, AuditAssessment.Repairable, null)]
    public void RepairableStatusReconcilesTheReportWithTheIntakeVerdict(
        string? read,
        bool unreadable,
        AuditAssessment? verdict,
        string? expected)
    {
        var writes = OriginalReportPrefillPolicy.Writes(
            new(Sha256, null, null, null, read, unreadable),
            verdict);

        Assert.Equal(expected, writes.GetValueOrDefault(AssessmentVocabulary.OriginalReportOutcome));
    }

    [Fact]
    public void AnUnreadableReportLetsOnlyTheVerdictFillRepairableStatus()
    {
        Assert.Equal(
            new Dictionary<string, string> { ["original_report.outcome"] = "total_loss" },
            OriginalReportPrefillPolicy.Writes(null, AuditAssessment.TotalLoss));
        Assert.Empty(OriginalReportPrefillPolicy.Writes(null, null));
    }

    /// <summary>
    /// A recorded reading carries the report's mileage in the Case's own unit
    /// words, and only with a unit it knows: the Case saves a mileage only
    /// with its unit (R930).
    /// </summary>
    [Theory]
    [InlineData(28136L, "Miles", 28136L, "miles")]
    [InlineData(2000L, "miles", 2000L, "miles")]
    [InlineData(52000L, "Km", 52000L, "kilometres")]
    [InlineData(28136L, null, null, null)]
    [InlineData(28136L, "Furlongs", null, null)]
    [InlineData(null, "Miles", null, null)]
    public void TheReadingCarriesTheReportsMileageWithItsUnit(
        long? printed,
        string? unit,
        long? expectedMileage,
        string? expectedUnit)
    {
        var reading = OriginalReportPrefillPolicy.Read(Sha256, null, null, null, null, null, printed, unit);

        Assert.Equal(expectedMileage, reading.Mileage);
        Assert.Equal(expectedUnit, reading.MileageUnit);
    }

    [Theory]
    // No unit printed: the unit the report family's rule states.
    [InlineData(null, null, "miles", "miles")]
    [InlineData(null, SourceCandidateDisposition.Missing, "miles", "miles")]
    // A printed unit wins over the stated one.
    [InlineData("Km", SourceCandidateDisposition.Usable, "miles", "Km")]
    // A printed unit the reader could not settle is no unit.
    [InlineData(null, SourceCandidateDisposition.Conflicting, "miles", null)]
    public void ThePrintedUnitWinsAndAnUnsettledOneIsNoUnit(
        string? printed,
        SourceCandidateDisposition? disposition,
        string? stated,
        string? expected) =>
        Assert.Equal(expected, OriginalReportPrefillPolicy.MileageUnit(printed, disposition, stated));

    [Fact]
    public void TheMileageFillsNoOriginalReportCell()
    {
        var writes = OriginalReportPrefillPolicy.Writes(
            new(Sha256, null, null, null, null, false, 28136, "miles"),
            intakeVerdict: null);

        Assert.Empty(writes);
    }

    [Fact]
    public void AValueTheVocabularyRefusesIsNotFilled()
    {
        var writes = OriginalReportPrefillPolicy.Writes(
            new(Sha256, new string('x', 201), "2026-13-45", "roadworthy", null, false),
            intakeVerdict: null);

        Assert.Equal(
            new Dictionary<string, string> { ["original_report.roadworthiness"] = "roadworthy" },
            writes);
    }
}
