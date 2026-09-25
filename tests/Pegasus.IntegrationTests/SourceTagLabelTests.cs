using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Vehicle;
using Pegasus.Web.Presentation;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The one tag system (operator, 23 September 2026): where a value came from
/// is one word in a text tag, and a value staff typed or corrected carries
/// none.
/// </summary>
public sealed class SourceTagLabelTests
{
    [Theory]
    [InlineData(CaseDataSourceKind.IntakeEvidence, "EmailBody:instruction", "Extracted", "")]
    [InlineData(CaseDataSourceKind.IntakeEvidence, "PdfContent:claim details", "Extracted", "")]
    [InlineData(CaseDataSourceKind.MailRoute, "mail route", "E-mail", "")]
    [InlineData(CaseDataSourceKind.VehicleLookup, "dvla", "Lookup", "lookup")]
    [InlineData(CaseDataSourceKind.ProviderSetting, "provider setting:QDOS", "Principal", "")]
    [InlineData(CaseDataSourceKind.CaseAcceptance, "accepted case review", "Automatic", "")]
    public void EachCaseDataSourceReadsItsOneWord(CaseDataSourceKind kind, string label, string word, string tone)
    {
        var tag = OperatorLabels.SourceTag(new CaseDataSource(kind, "identity", label, "policy", 1));

        Assert.Equal(new OperatorLabels.SourceTagWord(word, tone), tag);
    }

    [Fact]
    public void AStaffValueAndAMissingSourceCarryNoTag()
    {
        Assert.Null(OperatorLabels.SourceTag(
            new CaseDataSource(CaseDataSourceKind.StaffCorrection, "staff", "staff case-data correction", "case-data-edit", 1)));
        Assert.Null(OperatorLabels.SourceTag((CaseDataSource?)null));
    }

    [Fact]
    public void AnAssessmentValueIsTaggedByWhoRecordedIt()
    {
        var at = DateTimeOffset.UnixEpoch;
        AssessmentFieldValue Recorded(ActorKind kind, string by) =>
            new(AssessmentVocabulary.VehicleVin, "WVWZZZ1JZXW000001", kind, by, at);

        Assert.Equal(
            new OperatorLabels.SourceTagWord("Lookup", "lookup"),
            OperatorLabels.SourceTag(Recorded(ActorKind.Automation, VehicleLookupFillPolicy.RecorderId)));
        // An Original report cell filled from the filed report (v28 P51).
        Assert.Equal(
            new OperatorLabels.SourceTagWord("Extracted", ""),
            OperatorLabels.SourceTag(Recorded(ActorKind.Automation, OriginalReportPrefillPolicy.RecorderId)));
        Assert.Equal(
            new OperatorLabels.SourceTagWord("AI", "ai"),
            OperatorLabels.SourceTag(Recorded(ActorKind.Automation, "pegasus-automation")));
        Assert.Null(OperatorLabels.SourceTag(Recorded(ActorKind.Staff, "recorded-engineer")));
    }

    [Fact]
    public void TheTagRendersAsOneSpanCarryingItsWord()
    {
        using var writer = new StringWriter();
        new OperatorLabels.SourceTagWord("Lookup", "lookup").Render()
            .WriteTo(writer, System.Text.Encodings.Web.HtmlEncoder.Default);

        Assert.Equal(
            "<span class=\"src-tag src-tag--lookup\" data-provenance-word=\"Lookup\">Lookup</span>",
            writer.ToString());
    }
}
