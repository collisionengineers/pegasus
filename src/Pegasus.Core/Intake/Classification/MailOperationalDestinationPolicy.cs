namespace Pegasus.Core.Intake;

/// <summary>
/// Application work views are distinct from both the detailed classification and the
/// Outlook folder recommendation. Unidentified is an abstention, never a category.
/// </summary>
public enum MailOperationalDestination
{
    ReceivingWork,
    Queries,
    DetailedClassification,
    Other,
    Unidentified,
    Triage
}

public sealed record MailOperationalDestinationResult(
    MailOperationalDestination Destination,
    MailCategory? Classification,
    string PolicyKey,
    int PolicyVersion,
    string Reason);

/// <summary>
/// The SQL-facing description of one aggregate operational destination. The
/// persistence adapter translates these facts against the classification row;
/// it does not own another classification-to-destination table.
/// </summary>
public sealed record MailOperationalDestinationQuery(
    bool IncludesUnidentified = false,
    bool IncludesOther = false,
    IReadOnlyList<ReceivedMailFamily>? ReceivedFamilies = null,
    MailCategory? ExactClassification = null)
{
    public IReadOnlyList<ReceivedMailFamily> Families => ReceivedFamilies ?? [];
}

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
                MailOperationalDestination.Unidentified,
                null,
                "The classification is absent or ambiguous; no operational destination is inferred.");
        }

        return Map(classification.Category);
    }

    public static MailOperationalDestinationResult Map(MailCategory category)
    {
        ArgumentNullException.ThrowIfNull(category);
        category.ValidateCanonical();

        var destination = AggregateDestinations
            .Cast<MailOperationalDestination?>()
            .FirstOrDefault(candidate => Matches(Query(candidate!.Value), category));
        return destination switch
        {
            MailOperationalDestination.ReceivingWork => Result(
                MailOperationalDestination.ReceivingWork,
                category,
                "A confirmed new instruction enters Receiving work."),
            MailOperationalDestination.Queries => Result(
                MailOperationalDestination.Queries,
                category,
                category.ReceivedFamily == ReceivedMailFamily.Billing
                    ? "A billing query enters Queries."
                    : "Post-report correspondence enters Queries."),
            MailOperationalDestination.Other => Result(
                MailOperationalDestination.Other,
                category,
                "A reasoned novel classification uses the reserved Other destination."),
            MailOperationalDestination.Triage => Result(
                MailOperationalDestination.Triage,
                category,
                "An accepted Triage predicate routes to the separate Triage workflow."),
            _ => Result(
                MailOperationalDestination.DetailedClassification,
                category,
                $"The known classification '{CategoryKey(category)}' retains its own operational view.")
        };
    }

    public static MailOperationalDestinationQuery Query(MailOperationalDestination destination) =>
        destination switch
        {
            MailOperationalDestination.ReceivingWork => new(
                ReceivedFamilies: [ReceivedMailFamily.NewInstructionReceived]),
            MailOperationalDestination.Queries => new(
                ReceivedFamilies: [ReceivedMailFamily.PostReportEmails],
                ExactClassification: MailCategory.Received(
                    ReceivedMailFamily.Billing,
                    "billing-query")),
            MailOperationalDestination.Other => new(IncludesOther: true),
            MailOperationalDestination.Unidentified => new(IncludesUnidentified: true),
            MailOperationalDestination.Triage => new(
                ExactClassification: MailCategory.Received(
                    ReceivedMailFamily.PreInstructionEmails,
                    MailCategory.TriageRequestSubtype)),
            MailOperationalDestination.DetailedClassification => throw new ArgumentException(
                "Detailed mail views require one exact canonical classification.",
                nameof(destination)),
            _ => throw new ArgumentOutOfRangeException(nameof(destination), destination, null)
        };

    private static readonly MailOperationalDestination[] AggregateDestinations =
    [
        MailOperationalDestination.ReceivingWork,
        MailOperationalDestination.Queries,
        MailOperationalDestination.Other,
        MailOperationalDestination.Triage
    ];

    private static bool Matches(
        MailOperationalDestinationQuery query,
        MailCategory category)
    {
        if (query.IncludesOther && category.IsOther)
        {
            return true;
        }
        if (category.ReceivedFamily is { } family && query.Families.Contains(family))
        {
            return true;
        }
        return query.ExactClassification is { } exact
            && exact.Direction == category.Direction
            && exact.ReceivedFamily == category.ReceivedFamily
            && exact.SentFamily == category.SentFamily
            && string.Equals(exact.Subtype, category.Subtype, StringComparison.Ordinal);
    }

    private static MailOperationalDestinationResult Result(
        MailOperationalDestination destination,
        MailCategory? classification,
        string reason) => new(destination, classification, Key, Version, reason);

    private static string CategoryKey(MailCategory category) => category.Subtype is null
        ? category.Name
        : $"{category.Name}/{category.Subtype}";
}
