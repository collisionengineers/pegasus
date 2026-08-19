namespace Pegasus.Core.Intake;

/// <summary>
/// Application work views are distinct from both the detailed classification and the
/// Outlook folder recommendation. Needs sorting is an abstention, never a category.
/// </summary>
public enum MailOperationalDestination
{
    ReceivingWork,
    Queries,
    DetailedClassification,
    Other,
    NeedsSorting,
    Triage
}

public sealed record MailOperationalDestinationResult(
    MailOperationalDestination Destination,
    MailCategory? Classification,
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
                null,
                "The classification is absent or ambiguous; no operational destination is inferred.");
        }

        var category = classification.Category;
        if (category.IsOther)
        {
            return Result(
                MailOperationalDestination.Other,
                category,
                "A reasoned novel classification uses the reserved Other destination.");
        }

        return category.ReceivedFamily switch
        {
            ReceivedMailFamily.NewInstructionReceived => Result(
                MailOperationalDestination.ReceivingWork,
                category,
                "A confirmed new instruction enters Receiving work."),
            ReceivedMailFamily.PostReportEmails => Result(
                MailOperationalDestination.Queries,
                category,
                "Post-report correspondence enters Queries."),
            ReceivedMailFamily.Billing when category.Subtype == "billing-query" => Result(
                MailOperationalDestination.Queries,
                category,
                "A billing query enters Queries."),
            ReceivedMailFamily.PreInstructionEmails when category.Subtype == "triage-request" => Result(
                MailOperationalDestination.Triage,
                category,
                "An accepted Triage predicate routes to the separate Triage workflow."),
            _ => Result(
                MailOperationalDestination.DetailedClassification,
                category,
                $"The known classification '{CategoryKey(category)}' retains its own operational view.")
        };
    }

    private static MailOperationalDestinationResult Result(
        MailOperationalDestination destination,
        MailCategory? classification,
        string reason) => new(destination, classification, Key, Version, reason);

    private static string CategoryKey(MailCategory category) => category.Subtype is null
        ? category.Name
        : $"{category.Name}/{category.Subtype}";
}
