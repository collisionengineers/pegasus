using Pegasus.Core.Workflow;

namespace Pegasus.Core.Intake;

/// <summary>
/// FRD-08: a retained received message that joins a Case, and that no Principal
/// predicate classified, takes its category from the Case's own lifecycle state:
/// Query once the report is sent, otherwise Case update — whatever else the
/// Case's state, and for a Triage Case, which has no lifecycle state
/// (operator, 13 October 2026: a linked message without a category of its own
/// is Case update). The Principal's case-match policy found the Case, so this
/// is per-Principal evidence without a per-Principal predicate. The stores
/// apply it inside the transaction that links the message, before the query
/// rule reads the classification (FRD-13 "Report sent and queries"). A
/// Classified decision, including a staff correction, is never overridden.
/// </summary>
public static class CaseStateMailClassification
{
    public const string Key = "case_state_mail_classification";
    public const int Version = 1;
    public const string Actor = "system-worker:case-state-classification";

    /// <param name="state">The linked Case's state; null for a Triage Case.</param>
    /// <returns>The derived decision, or null to leave the current one alone.</returns>
    public static MailClassificationResult? Derive(
        MailClassificationResult? current,
        CaseLifecycleState? state)
    {
        if (current is { Outcome: not MailClassificationOutcome.Unclassified })
        {
            return null;
        }

        return state == CaseLifecycleState.ReportSent
            ? MailClassificationResult.Classified(
                MailCategory.Query,
                current?.Predicates ?? [],
                "The linked Case's report had been sent when the message joined it.",
                Key,
                Version)
            : MailClassificationResult.Classified(
                MailCategory.CaseUpdate,
                current?.Predicates ?? [],
                "The message joined the linked Case without a category of its own.",
                Key,
                Version);
    }
}
