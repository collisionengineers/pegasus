using Pegasus.Core.Workflow;

namespace Pegasus.Core.Intake;

/// <summary>
/// FRD-08: a retained received message that joins a Case, and that no Principal
/// predicate classified, takes its category from the Case's own lifecycle state:
/// Query once the report is sent, Case update before. The Principal's
/// case-match policy found the Case, so this is per-Principal evidence without
/// a per-Principal predicate. The stores apply it inside the transaction that
/// links the message, before the query rule reads the classification
/// (FRD-13 "Report sent and queries"). A Classified decision, including a staff
/// correction, is never overridden.
/// </summary>
public static class CaseStateMailClassification
{
    public const string Key = "case_state_mail_classification";
    public const int Version = 1;
    public const string Actor = "system-worker:case-state-classification";

    /// <returns>The derived decision, or null to leave the current one alone.</returns>
    public static MailClassificationResult? Derive(
        MailClassificationResult? current,
        CaseLifecycleState state)
    {
        if (current is { Outcome: not MailClassificationOutcome.Unclassified })
        {
            return null;
        }

        (MailCategory? category, string? reason) = state switch
        {
            CaseLifecycleState.ReportSent => (
                MailCategory.Query,
                "The linked Case's report had been sent when the message joined it."),
            CaseLifecycleState.NotReady
                or CaseLifecycleState.Held
                or CaseLifecycleState.Review
                or CaseLifecycleState.ReportPreparation => (
                    MailCategory.CaseUpdate,
                    "The linked Case was still in progress when the message joined it."),
            _ => (null, null)
        };
        return category is null
            ? null
            : MailClassificationResult.Classified(category.Value, current?.Predicates ?? [], reason!, Key, Version);
    }
}
