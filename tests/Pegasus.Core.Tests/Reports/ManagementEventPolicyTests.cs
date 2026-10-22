using Pegasus.Core.Operations;
using Pegasus.Core.Reports;

namespace Pegasus.Core.Tests.Reports;

/// <summary>
/// The management event after each report or fee-note send (FRD-21; operator
/// ruling R205, 22 October 2026): only a Case report send records one, and it
/// carries the documents by kind, the reports among them and the distinct
/// recipients.
/// </summary>
public sealed class ManagementEventPolicyTests
{
    [Theory]
    [InlineData(StaffMailPurpose.CaseReport, true)]
    [InlineData(StaffMailPurpose.GeneralCorrespondence, false)]
    [InlineData(StaffMailPurpose.TriageChaser, false)]
    [InlineData(StaffMailPurpose.TriageOutcomeReply, false)]
    [InlineData(StaffMailPurpose.CaseChaser, false)]
    public void OnlyACaseReportSendIsAManagementEvent(StaffMailPurpose purpose, bool recorded)
    {
        Assert.Equal(recorded, ManagementEventPolicy.IsManagementEvent(purpose));
    }

    [Fact]
    public void TheEventCarriesTheDocumentsTheReportCountAndTheDistinctRecipients()
    {
        var one = ManagementEventPolicy.ForSend(
            [CaseReportArtifactKind.FeeNote, CaseReportArtifactKind.AssessmentReport],
            [new("handler@insurer.example", "Handler"), new("claims@insurer.example", null)],
            [new("Handler@Insurer.example", null)]);

        Assert.Equal(
            [nameof(CaseReportArtifactKind.AssessmentReport), nameof(CaseReportArtifactKind.FeeNote)],
            one.Documents);
        Assert.Equal(1, one.Reports);
        Assert.Equal(2, one.Recipients);

        // Both works' reports in one email are one send carrying two reports.
        var both = ManagementEventPolicy.ForSend(
            [CaseReportArtifactKind.ImagePack, CaseReportArtifactKind.AssessmentReport,
                CaseReportArtifactKind.AssessmentReport],
            [new("handler@insurer.example", null)],
            []);

        Assert.Equal(
            [nameof(CaseReportArtifactKind.AssessmentReport), nameof(CaseReportArtifactKind.AssessmentReport),
                nameof(CaseReportArtifactKind.ImagePack)],
            both.Documents);
        Assert.Equal(2, both.Reports);
        Assert.Equal(1, both.Recipients);
    }

    [Fact]
    public void ThePayloadIsWrittenUnderItsThreeNamesAndTheEventIsKeyedToItsSend()
    {
        var operationId = Guid.NewGuid();

        Assert.Equal(
            """{"documents":["AssessmentReport","RepairSpecification"],"reports":1,"recipients":3}""",
            ManagementEventPolicy.Serialize(new(
                [nameof(CaseReportArtifactKind.AssessmentReport), nameof(CaseReportArtifactKind.RepairSpecification)],
                1,
                3)));
        Assert.Equal($"management-event:{operationId:N}", ManagementEventPolicy.CorrelationId(operationId));
    }
}
