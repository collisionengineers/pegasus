namespace Pegasus.Core.Intake;

/// <summary>
/// The stable business vocabulary an approved mailbox can bind to exact Outlook
/// folders. These are not operational queues and do not contain transport ids.
/// </summary>
public enum MailLogicalFolderType
{
    Instructions,
    Audits,
    Diminution,
    NewClients,
    CaseQueries,
    Enquiries,
    Billing,
    PreInstructions,
    NoAction,
    Images,
    Cancellations,
    CaseUpdates,
    Other
}

public sealed record MailLogicalFolderDefinition(
    MailLogicalFolderType Type,
    string Key,
    string Label);

public static class MailLogicalFolders
{
    public static IReadOnlyList<MailLogicalFolderDefinition> All { get; } =
    [
        new(MailLogicalFolderType.Instructions, "instructions", "Instructions"),
        new(MailLogicalFolderType.Audits, "audits", "Audits"),
        new(MailLogicalFolderType.Diminution, "diminution", "Diminution"),
        new(MailLogicalFolderType.NewClients, "new-clients", "New clients"),
        new(MailLogicalFolderType.CaseQueries, "case-queries", "Case queries"),
        new(MailLogicalFolderType.Enquiries, "enquiries", "Enquiries"),
        new(MailLogicalFolderType.Billing, "billing", "Billing"),
        new(MailLogicalFolderType.PreInstructions, "pre-instructions", "Pre-instructions"),
        new(MailLogicalFolderType.NoAction, "no-action", "No action"),
        new(MailLogicalFolderType.Images, "images", "Images"),
        new(MailLogicalFolderType.Cancellations, "cancellations", "Cancellations"),
        new(MailLogicalFolderType.CaseUpdates, "case-updates", "Case updates"),
        new(MailLogicalFolderType.Other, "other", "Other")
    ];

    public static MailLogicalFolderDefinition Definition(MailLogicalFolderType type) =>
        All.SingleOrDefault(item => item.Type == type)
        ?? throw new ArgumentOutOfRangeException(nameof(type), type, null);
}

public sealed record MailLogicalFolderResult(
    MailLogicalFolderType? FolderType,
    MailCategory? Classification,
    string PolicyKey,
    int PolicyVersion,
    string Reason);

/// <summary>
/// Maps the settled detailed classification to its separate logical Outlook folder.
/// Exact folder identities belong to the approved-mailbox binding, not this policy.
/// </summary>
public static class MailLogicalFolderPolicy
{
    public const string Key = "mail_logical_folder";
    public const int Version = 1;

    public static MailLogicalFolderResult Map(MailClassificationResult classification)
    {
        ArgumentNullException.ThrowIfNull(classification);
        if (classification.Outcome is not MailClassificationOutcome.Classified
            || classification.Category is null)
        {
            return Result(
                null,
                null,
                "The classification is absent or ambiguous; no Outlook folder is inferred.");
        }

        var category = classification.Category;
        if (category.IsOther)
        {
            return Result(
                MailLogicalFolderType.Other,
                category,
                "A reasoned novel classification uses the approved Other folder binding.");
        }

        var folder = category.Direction switch
        {
            MailDirection.Sent => MailLogicalFolderType.Other,
            MailDirection.Received => ReceivedFolder(category),
            _ => throw new ArgumentOutOfRangeException(
                nameof(classification),
                "The mail direction is not recognized.")
        };
        return Result(
            folder,
            category,
            $"The detailed classification maps to the '{MailLogicalFolders.Definition(folder).Label}' logical folder.");
    }

    private static MailLogicalFolderType ReceivedFolder(MailCategory category) =>
        category.ReceivedFamily switch
        {
            ReceivedMailFamily.General when category.Subtype == "general-chase" =>
                MailLogicalFolderType.CaseQueries,
            ReceivedMailFamily.General => MailLogicalFolderType.NoAction,
            ReceivedMailFamily.Billing => MailLogicalFolderType.Billing,
            ReceivedMailFamily.NewInstructionReceived => category.Subtype switch
            {
                "audit" => MailLogicalFolderType.Audits,
                "diminution" => MailLogicalFolderType.Diminution,
                "inspection" => MailLogicalFolderType.Instructions,
                "new-client" => MailLogicalFolderType.NewClients,
                "website-enquiry" => MailLogicalFolderType.Enquiries,
                _ => throw Unsupported(category)
            },
            ReceivedMailFamily.NonClientRelated => MailLogicalFolderType.Other,
            ReceivedMailFamily.InProgressCases when category.Subtype == "cancellation" =>
                MailLogicalFolderType.Cancellations,
            ReceivedMailFamily.InProgressCases => MailLogicalFolderType.CaseUpdates,
            ReceivedMailFamily.PostReportEmails => MailLogicalFolderType.CaseQueries,
            ReceivedMailFamily.PreInstructionEmails when category.Subtype == "images-received" =>
                MailLogicalFolderType.Images,
            ReceivedMailFamily.PreInstructionEmails => MailLogicalFolderType.PreInstructions,
            ReceivedMailFamily.InternalCc => MailLogicalFolderType.Other,
            _ => throw Unsupported(category)
        };

    private static ArgumentException Unsupported(MailCategory category) => new(
        $"The registered classification '{category.Name}/{category.Subtype}' has no logical folder outcome.",
        nameof(category));

    private static MailLogicalFolderResult Result(
        MailLogicalFolderType? folderType,
        MailCategory? classification,
        string reason) => new(folderType, classification, Key, Version, reason);
}
