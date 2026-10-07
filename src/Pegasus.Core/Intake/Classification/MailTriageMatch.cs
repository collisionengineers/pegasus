namespace Pegasus.Core.Intake;

/// <summary>
/// The accepted Triage match, derived from the message's classification rather
/// than from a separate matcher (FRD-03 "How a Triage starts"; ADR-0008 makes
/// the route policy the only owner of message-type classification). The
/// evidence is kept in step with the current classification: processing writes
/// it from the automatic decision, and a staff correction to or from Triage
/// request writes or removes it, so Open the Triage reads one fact.
/// </summary>
public static class MailTriageMatch
{
    /// <summary>
    /// The evidence a Triage-request classification puts on its receipt, or null
    /// when the classification is not one. A reply is correspondence about a
    /// Triage, not a new assessment request, and names none.
    /// </summary>
    /// <param name="staffCorrected">
    /// True when staff corrected the message to Triage request: the source is
    /// then the correction, not a policy judgement over the message.
    /// </param>
    public static IntakeEvidence? Evidence(MailClassificationResult? classification, bool staffCorrected = false)
    {
        if (classification is not { IsTriageRequest: true }
            || classification.Category?.IsReplyContext == true)
        {
            return null;
        }

        if (staffCorrected)
        {
            return new(
                IntakeEvidenceSource.StaffCorrection,
                IntakeEvidenceStrength.Strong,
                IntakeEvidenceFinding.AcceptedTriageMatch,
                MailCategory.TriageRequestSubtype,
                "A staff correction classified this message as a Triage request.",
                classification.PolicyKey,
                classification.PolicyVersion);
        }

        var matched = string.Join(
            ", ",
            classification.Predicates.Where(predicate => predicate.Matched).Select(predicate => predicate.Key));
        return new(
            IntakeEvidenceSource.SystemDefault,
            IntakeEvidenceStrength.Strong,
            IntakeEvidenceFinding.AcceptedTriageMatch,
            MailCategory.TriageRequestSubtype,
            $"The accepted route classification recorded this message as a Triage request (predicates: {matched}).",
            classification.PolicyKey,
            classification.PolicyVersion);
    }

    /// <summary>
    /// The receipt's evidence with its accepted Triage match brought into step
    /// with <paramref name="classification"/>: an existing match is kept while
    /// the classification is still a Triage request, the staff match is added
    /// when it becomes one, and every match goes when it stops being one.
    /// Returns null when nothing changes.
    /// </summary>
    public static IReadOnlyList<IntakeEvidence>? Reconcile(
        IReadOnlyList<IntakeEvidence> evidence,
        MailClassificationResult classification)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(classification);
        var existing = evidence.Where(item => item.Finding == IntakeEvidenceFinding.AcceptedTriageMatch).ToArray();
        var wanted = Evidence(classification, staffCorrected: true);
        if (wanted is null)
        {
            return existing.Length == 0
                ? null
                : evidence.Where(item => item.Finding != IntakeEvidenceFinding.AcceptedTriageMatch).ToArray();
        }
        return existing.Length == 1
            ? null
            : [.. evidence.Where(item => item.Finding != IntakeEvidenceFinding.AcceptedTriageMatch), wanted];
    }
}
