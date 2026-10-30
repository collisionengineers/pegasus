using Pegasus.Core.Cases;
using Pegasus.Core.Intake;

namespace Pegasus.Web.Presentation;

/// <summary>
/// The one presentation-side vocabulary for choosing a mail type: Correspondence
/// (no category) and the four categories, with a New instruction naming its
/// Case type in the same option. The message record and the Automation MCP
/// mail tools both consume this list.
/// </summary>
public static class MailClassificationSelection
{
    public sealed record SelectionOption(string Value, string Label);

    /// <summary>The choice that corrects a message to no category.</summary>
    public const string CorrespondenceKey = "correspondence";

    public const string CorrespondenceLabel = OperatorLabels.Inbox.Correspondence;

    /// <summary>
    /// The reason a Type select on the message record records: it applies at
    /// once, so no reason is typed (operator, 22 October 2026, R179).
    /// </summary>
    public const string CorrectedFromInboxReason = "Corrected from the Inbox";

    public const string UnknownSelectionMessage = "Choose a valid type.";

    private static readonly CaseType[] WorkTypes =
        [CaseType.Inspection, CaseType.Audit, CaseType.InspectionAndAudit];

    /// <summary>
    /// The seven Type options, in order. A New instruction's value is
    /// <c>new-instruction:Inspection</c> and the same shape for the other work types.
    /// </summary>
    public static IReadOnlyList<SelectionOption> TypeSelectOptions { get; } =
    [
        new(CorrespondenceKey, OperatorLabels.Inbox.Correspondence),
        new(Combined(MailCategory.NewInstruction, CaseType.Inspection), OperatorLabels.Inbox.NewInstructionInspection),
        new(Combined(MailCategory.NewInstruction, CaseType.Audit), OperatorLabels.Inbox.NewInstructionAudit),
        new(Combined(MailCategory.NewInstruction, CaseType.InspectionAndAudit), OperatorLabels.Inbox.NewInstructionInspectionAndAudit),
        new(MailCategories.Code(MailCategory.TriageRequest), OperatorLabels.MailClassification(MailCategory.TriageRequest)),
        new(MailCategories.Code(MailCategory.Cancellation), OperatorLabels.MailClassification(MailCategory.Cancellation)),
        new(MailCategories.Code(MailCategory.PrePegasusCase), OperatorLabels.MailClassification(MailCategory.PrePegasusCase))
    ];

    /// <summary>The three work types a New instruction may name, for a split input.</summary>
    public static IReadOnlyList<SelectionOption> WorkTypeOptions { get; } =
        [.. WorkTypes.Select(type => new SelectionOption(
            type.ToString(),
            type == CaseType.InspectionAndAudit ? "Inspection and Audit" : OperatorLabels.CaseTypeName(type)))];

    public static string Combined(MailCategory category, CaseType caseType) =>
        $"{MailCategories.Code(category)}:{caseType}";

    /// <summary>The option value the current decision selects.</summary>
    public static string ValueFor(MailClassificationResult? decision) =>
        decision is { IsClassified: true, Category: MailCategory.NewInstruction, CaseType: { } caseType }
            ? Combined(MailCategory.NewInstruction, caseType)
            : decision is { IsClassified: true, Category: { } category }
                ? MailCategories.Code(category)
                : CorrespondenceKey;

    /// <summary>
    /// Parses a Type selection. Accepts the combined <c>new-instruction:Inspection</c>
    /// value, a category code with a separate work type, and <c>correspondence</c>.
    /// An unknown code is rejected.
    /// </summary>
    public static bool TryParseSelection(
        string? classificationKey,
        string? workType,
        out MailCategory? category,
        out CaseType? caseType)
    {
        category = null;
        caseType = null;
        if (string.IsNullOrWhiteSpace(classificationKey))
        {
            return false;
        }

        var key = classificationKey.Trim();
        if (string.Equals(key, CorrespondenceKey, StringComparison.Ordinal))
        {
            return string.IsNullOrWhiteSpace(workType);
        }

        var separator = key.IndexOf(':');
        if (separator >= 0)
        {
            if (!string.IsNullOrWhiteSpace(workType))
            {
                return false;
            }

            workType = key[(separator + 1)..];
            key = key[..separator];
        }

        if (!MailCategories.TryParse(key, out var parsed))
        {
            return false;
        }

        category = parsed;
        if (parsed != MailCategory.NewInstruction)
        {
            return string.IsNullOrWhiteSpace(workType);
        }

        return TryParseWorkType(workType, out caseType);
    }

    /// <summary>Parses the case type a New instruction correction names.</summary>
    public static bool TryParseWorkType(string? value, out CaseType? caseType)
    {
        caseType = Enum.TryParse<CaseType>(value, out var parsed) && WorkTypes.Contains(parsed)
            ? parsed
            : null;
        return caseType is not null;
    }
}
