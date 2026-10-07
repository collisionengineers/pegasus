using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake.Classification;

/// <summary>
/// FRD-03: the accepted Triage match is the classification's own evidence, so
/// a correction to or from Triage request keeps the receipt's evidence in step.
/// </summary>
public sealed class MailTriageMatchTests
{
    private static readonly MailClassificationResult TriageRequest = MailClassificationResult.Classified(
        MailCategory.Received(ReceivedMailFamily.PreInstructionEmails, MailCategory.TriageRequestSubtype),
        [new("subject.triage-only", true, "The subject names a Triage.")],
        "Triage request.",
        "principal_mail_classification",
        2);

    private static readonly MailClassificationResult Autoreply = MailClassificationResult.Classified(
        MailCategory.Received(ReceivedMailFamily.General, "autoreply"), [], "Autoreply.", "principal_mail_classification", 2);

    [Fact]
    public void TheAutomaticMatchNamesThePolicyAndItsPredicates()
    {
        var evidence = MailTriageMatch.Evidence(TriageRequest);

        Assert.NotNull(evidence);
        Assert.Equal(IntakeEvidenceSource.SystemDefault, evidence!.Source);
        Assert.Equal(IntakeEvidenceStrength.Strong, evidence.Strength);
        Assert.Equal(IntakeEvidenceFinding.AcceptedTriageMatch, evidence.Finding);
        Assert.Equal(MailCategory.TriageRequestSubtype, evidence.Signal);
        Assert.Contains("subject.triage-only", evidence.Detail, StringComparison.Ordinal);
        Assert.Equal("principal_mail_classification", evidence.MatcherKey);
        Assert.Equal(2, evidence.MatcherVersion);
    }

    [Fact]
    public void AStaffCorrectionIsItsOwnSourceAndAReplyNamesNoMatch()
    {
        var staff = MailTriageMatch.Evidence(TriageRequest, staffCorrected: true);
        var reply = MailTriageMatch.Evidence(TriageRequest with
        {
            Category = MailCategory.Received(ReceivedMailFamily.PreInstructionEmails, MailCategory.TriageRequestSubtype, isReplyContext: true)
        });

        Assert.Equal(IntakeEvidenceSource.StaffCorrection, staff!.Source);
        Assert.Equal(IntakeEvidenceFinding.AcceptedTriageMatch, staff.Finding);
        Assert.Equal("principal_mail_classification", staff.MatcherKey);
        Assert.Null(reply);
        Assert.Null(MailTriageMatch.Evidence(Autoreply));
        Assert.Null(MailTriageMatch.Evidence(null));
    }

    [Fact]
    public void ReconcileAddsKeepsOrRemovesTheMatchToFollowTheClassification()
    {
        IntakeEvidence other = new(IntakeEvidenceSource.Sender, IntakeEvidenceStrength.Weak, IntakeEvidenceFinding.Information, "sender", "A sender.");
        var automatic = MailTriageMatch.Evidence(TriageRequest)!;

        // Becoming a Triage request adds the staff match beside the rest.
        var added = MailTriageMatch.Reconcile([other], TriageRequest);
        Assert.Equal([other], added!.Where(item => item.Finding != IntakeEvidenceFinding.AcceptedTriageMatch));
        Assert.Equal(IntakeEvidenceSource.StaffCorrection, Assert.Single(added!, item => item.Finding == IntakeEvidenceFinding.AcceptedTriageMatch).Source);

        // Still a Triage request: the recorded match stays as it was.
        Assert.Null(MailTriageMatch.Reconcile([other, automatic], TriageRequest));

        // No longer one: every match goes; nothing to remove changes nothing.
        Assert.Equal([other], MailTriageMatch.Reconcile([other, automatic], Autoreply));
        Assert.Null(MailTriageMatch.Reconcile([other], Autoreply));
    }
}
