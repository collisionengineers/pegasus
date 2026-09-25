using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.ThirdPartyReports;

namespace Pegasus.Core.Tests.Intake.ThirdPartyReports;

/// <summary>
/// What each report family's printed layout fills on an Audit's Original
/// report cells (v28 P51, #840): the extraction read through
/// <see cref="OriginalReportPrefillPolicy.Read"/>. The expected cells are the
/// vocabulary's own literals, written out rather than taken from the policy.
/// </summary>
public sealed partial class ThirdPartyReportExtractionTests
{
    private const string LairdAssessment = """
            Our Reference     Your Reference                       Date

             26-                   AMA/46320/1                     1st Sep 2026
             1868852/2512559

         Repairable Damage Assessment Report

            Re: Mr Lee Rowland

            Status        Repairable
            Legal Status  Roadworthy

        Email: enquiries@laird-assessors.com   Web:  www.laird-assessors.com
        """;

    [Fact]
    public void TheConnexusNarrativeFillsEveryOriginalReportCell()
    {
        var reading = Cells(ConnexusHeader);

        Assert.Equal("Connexus Vehicle Assessors", reading.Assessor);
        Assert.Equal("2026-03-09", reading.ReportDate);
        Assert.Equal("unroadworthy", reading.Roadworthiness);
        Assert.Equal("repairable", reading.Outcome);
        Assert.False(reading.OutcomeUnreadable);
    }

    [Fact]
    public void TheNarrativeTitleIsReadAsTheReportOutcome()
    {
        var connexus = Row(Read(ConnexusHeader), ThirdPartyReportFields.Outcome, string.Empty);
        var exclusive = Row(Read(ExclusiveErehrHeader), ThirdPartyReportFields.Outcome, string.Empty);

        Assert.Equal(SourceCandidateDisposition.Usable, connexus!.Disposition);
        Assert.Equal("Repairable", connexus.NormalizedValue);
        Assert.Equal(SourceCandidateDisposition.Usable, exclusive!.Disposition);
        Assert.Equal("REPAIRABLE", exclusive.NormalizedValue);
    }

    [Fact]
    public void TheExclusiveNarrativeFillsEveryOriginalReportCell()
    {
        var reading = Cells(ExclusiveErehrHeader);

        Assert.Equal("Exclusive Vehicle Assessors", reading.Assessor);
        Assert.Equal("2026-03-11", reading.ReportDate);
        Assert.Equal("roadworthy", reading.Roadworthiness);
        Assert.Equal("repairable", reading.Outcome);
    }

    [Fact]
    public void MontgomeryPrintsNoReportDateSoOnlyItsOutcomeAndAssessorFill()
    {
        var reading = Cells(MontgomeryCosts + "\nRoadworthy     NO\n");

        Assert.Equal("Montgomery Assessors", reading.Assessor);
        Assert.Null(reading.ReportDate);
        Assert.Equal("unroadworthy", reading.Roadworthiness);
        Assert.Equal("repairable", reading.Outcome);
    }

    [Fact]
    public void LairdsStatusAndLegalStatusFillTheOutcomeAndRoadworthiness()
    {
        var reading = Cells(LairdAssessment);

        Assert.Equal("Laird Assessors", reading.Assessor);
        Assert.Equal("2026-09-01", reading.ReportDate);
        Assert.Equal("roadworthy", reading.Roadworthiness);
        Assert.Equal("repairable", reading.Outcome);
    }

    [Fact]
    public void SPrintsVehicleStatusAndRoadworthinessWordFillTheirCells()
    {
        var reading = Cells(SPrintTotals + """

            Vehicle Status   : REPAIRABLE
            Condition        : UNROADWORTHY
            """);

        Assert.Equal("sPrint Assessors", reading.Assessor);
        Assert.Equal("2026-08-18", reading.ReportDate);
        Assert.Equal("unroadworthy", reading.Roadworthiness);
        Assert.Equal("repairable", reading.Outcome);
    }

    /// <summary>
    /// The production reader's shape of the same two layouts: Laird's header
    /// broken one cell per line, and sPrint's label keeping its column padding.
    /// </summary>
    [Fact]
    public void TheProductionReadersLairdAndSPrintShapesFillTheSameCells()
    {
        var laird = Cells("""
            Our Reference
            26-1918326/2561054
            Your Reference
            REB/ND/48099/1
            Date
            1st Sep 2026
            Repairable Damage Assessment Report
            Status Repairable
            Legal Status Roadworthy
            Email: enquiries@laird-assessors.com
            """);
        var sprint = Cells(SPrintTotals + """

            Transmission : MANUAL Vehicle  Status : REPAIRABLE
            Colour : WHITE : ROADWORTHY
            """);

        Assert.Equal("2026-09-01", laird.ReportDate);
        Assert.Equal("repairable", laird.Outcome);
        Assert.Equal("roadworthy", laird.Roadworthiness);
        Assert.Equal("repairable", sprint.Outcome);
        Assert.Equal("roadworthy", sprint.Roadworthiness);
    }

    [Fact]
    public void ConflictingRoadworthinessFillsNothingAndLeavesTheOtherCells()
    {
        var reading = Cells(ConnexusHeader + "\n     Roadworthy: Yes\n");

        Assert.Null(reading.Roadworthiness);
        Assert.Equal("Connexus Vehicle Assessors", reading.Assessor);
        Assert.Equal("repairable", reading.Outcome);
    }

    [Fact]
    public void AStatusTheCellsCannotHoldIsUnreadableAndKeepsTheVerdictOut()
    {
        var reading = Cells(LairdAssessment.Replace(
            "Status        Repairable", "Status        Uneconomical", StringComparison.Ordinal));

        Assert.Null(reading.Outcome);
        Assert.True(reading.OutcomeUnreadable);
        Assert.False(OriginalReportPrefillPolicy.Writes(reading, AuditAssessment.Repairable)
            .ContainsKey(AssessmentVocabulary.OriginalReportOutcome));
    }

    [Fact]
    public void AnOutcomeAndAStatusThatDisagreeAreUnreadable()
    {
        var candidate = Read(ConnexusHeader).Candidate!;
        var outcome = candidate.Damage.Outcome!;
        var disagreeing = candidate with
        {
            Damage = candidate.Damage with
            {
                Repairability = new ThirdPartyReportFact<string?>("TOTAL LOSS", outcome.Source)
            }
        };

        var reading = OriginalReportPrefillPolicy.Read(disagreeing, new string('a', 64));

        Assert.Null(reading.Outcome);
        Assert.True(reading.OutcomeUnreadable);
    }

    [Fact]
    public void AReportNoSignatureMatchesFillsNothingButLetsTheVerdictStandIn()
    {
        var result = Read("A letter that is not an engineer report.");
        var reading = OriginalReportPrefillPolicy.Read(result.Candidate, new string('a', 64));

        Assert.Null(result.Candidate);
        Assert.Equal(
            new Dictionary<string, string> { ["original_report.outcome"] = "total_loss" },
            OriginalReportPrefillPolicy.Writes(reading, AuditAssessment.TotalLoss));
    }

    [Fact]
    public void EveryFilledAssessorIsOneTheSectionOffers()
    {
        foreach (var text in new[] { ConnexusHeader, ExclusiveErehrHeader, MontgomeryCosts, LairdAssessment, SPrintTotals })
        {
            Assert.Contains(Cells(text).Assessor!, ThirdPartyReportProfiles.KnownIssuers);
        }
    }

    private static OriginalReportReading Cells(string text)
    {
        var result = Read(text);
        Assert.NotNull(result.Candidate);
        return OriginalReportPrefillPolicy.Read(result.Candidate, new string('a', 64));
    }
}
