using Pegasus.Core.Cases;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

public sealed class DefinitiveIntakeCaseTypeTests
{
    [Theory]
    [InlineData("AUDIT REPORT NOTIFICATION", CaseType.Audit)]
    [InlineData("ENGINEER NOTIFICATION", CaseType.Inspection)]
    [InlineData("ENGINEER NOTIFICATION (REPORT + AUDIT REPORT)", CaseType.InspectionAndAudit)]
    public void DefinitiveQdosInstructionCarriesItsTypedCaseType(
        string document,
        CaseType expected)
    {
        var result = new QdosMailClassificationPolicy().Classify(new(
            IntakeSourceReadStatus.Readable,
            [new(IntakeEvidenceSource.DocumentContent, "retained instruction", document)],
            [],
            [],
            false));

        Assert.Equal(MailClassificationOutcome.Classified, result.Outcome);
        Assert.Equal(expected, result.CaseType);
    }

    [Fact]
    public void AmbiguousInstructionCarriesNoCaseType()
    {
        var result = new QdosMailClassificationPolicy().Classify(new(
            IntakeSourceReadStatus.Readable,
            [
                new(IntakeEvidenceSource.EmailBody, "message body", "Triage Only Request"),
                new(IntakeEvidenceSource.DocumentContent, "retained instruction", "ENGINEER NOTIFICATION")
            ],
            [],
            [],
            false));

        Assert.Equal(MailClassificationOutcome.Ambiguous, result.Outcome);
        Assert.Null(result.CaseType);
    }

    [Fact]
    public void StandaloneAuditRequiresStaffEvidenceButOtherTypedWorkDoesNot()
    {
        Assert.True(IntakeDecisionPolicy.RequiresStandaloneAuditEvidence(CaseType.Audit));
        Assert.False(IntakeDecisionPolicy.RequiresStandaloneAuditEvidence(CaseType.Inspection));
        Assert.False(IntakeDecisionPolicy.RequiresStandaloneAuditEvidence(CaseType.InspectionAndAudit));
        Assert.False(IntakeDecisionPolicy.RequiresStandaloneAuditEvidence(null));
    }
}
