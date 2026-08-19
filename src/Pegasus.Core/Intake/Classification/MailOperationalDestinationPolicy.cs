namespace Pegasus.Core.Intake;

/// <summary>
/// Application work views are distinct from both the detailed classification and the
/// Outlook folder recommendation. Needs sorting is an abstention, never a category.
/// </summary>
public enum MailOperationalDestination
{
    ReceivingWork,
    Queries,
    Other,
    NeedsSorting,
    Triage
}

public sealed record MailOperationalDestinationResult(
    MailOperationalDestination Destination,
    string PolicyKey,
    int PolicyVersion,
    string Reason);

public static class MailOperationalDestinationPolicy
{
    public const string Key = "mail_operational_destination";
    public const int Version = 1;

    public static MailOperationalDestinationResult Map(MailClassificationResult classification)
    {
        ArgumentNullException.ThrowIfNull(classification);

        if (classification.Outcome is not MailClassificationOutcome.Classified
            || classification.Category is null)
        {
            return Result(
                MailOperationalDestination.NeedsSorting,
                "The classification is absent or ambiguous; no operational destination is inferred.");
        }

        var category = classification.Category;
        if (category.Direction is MailDirection.Sent || category.IsOther)
        {
            return Result(
                MailOperationalDestination.Other,
                "The settled classification remains visible in the Other work view.");
        }

        return category.ReceivedFamily switch
        {
            ReceivedMailFamily.NewInstructionReceived => Result(
                MailOperationalDestination.ReceivingWork,
                "A confirmed new instruction enters Receiving work."),
            ReceivedMailFamily.PostReportEmails => Result(
                MailOperationalDestination.Queries,
                "Post-report correspondence enters Queries."),
            ReceivedMailFamily.Billing when category.Subtype == "billing-query" => Result(
                MailOperationalDestination.Queries,
                "A billing query enters Queries; other billing classifications remain separately named in Other."),
            ReceivedMailFamily.PreInstructionEmails when category.Subtype == "triage-request" => Result(
                        MailOperationalDestination.Triage,
                        "An accepted Triage predicate routes to the separate Triage workflow."),
            _ => Result(
                MailOperationalDestination.Other,
                "The named classification remains visible in the aggregate Other work view.")
        };
    }

    private static MailOperationalDestinationResult Result(
        MailOperationalDestination destination,
        string reason) => new(destination, Key, Version, reason);
}
