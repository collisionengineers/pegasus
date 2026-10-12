using Pegasus.Core.Cases;
using Pegasus.Core.Intake;

namespace Pegasus.Web.Presentation;

/// <summary>
/// The one presentation-side vocabulary for choosing a corrected mail
/// classification: every mail category by its code, and Unclassified. The
/// mail message page and the Automation MCP mail tools both consume this
/// single list, so a category appears — or disappears — for both callers at
/// once.
/// </summary>
public static class MailClassificationSelection
{
    public sealed record SelectionOption(string Value, string Label);

    /// <summary>The choice that corrects a message to no category.</summary>
    public const string UnclassifiedKey = "unclassified";

    public const string UnclassifiedLabel = "Unclassified";

    /// <summary>The one-click Tag as query correction (operator, 11 October 2026).</summary>
    public static readonly string QueryKey = MailCategories.Code(MailCategory.Query);

    /// <summary>The reason Tag as query records in the message's classification history.</summary>
    public const string TaggedAsQueryReason = "Tagged as query";

    // Labels resolve through the one operator label map, so the picker and
    // every read-only rendering of a classification use the same words.
    public static IReadOnlyList<SelectionOption> Options { get; } =
        [
            .. MailCategories.All.Select(category =>
                new SelectionOption(MailCategories.Code(category), OperatorLabels.MailClassification(category))),
            new(UnclassifiedKey, UnclassifiedLabel)
        ];

    /// <summary>
    /// Parses the case type a New instruction correction names. Only the three
    /// work types are accepted; a Triage is a Case type, not instructed work.
    /// </summary>
    public static bool TryParseWorkType(string? value, out CaseType? caseType)
    {
        caseType = Enum.TryParse<CaseType>(value, out var parsed)
            && parsed is CaseType.Inspection or CaseType.Audit or CaseType.InspectionAndAudit
            ? parsed
            : null;
        return caseType is not null;
    }

    /// <summary>
    /// Parses a selected key: a category code, or <see cref="UnclassifiedKey"/>
    /// for no category. Returns false — never a guessed category — for any
    /// other key.
    /// </summary>
    public static bool TryParse(string? key, out MailCategory? category)
    {
        category = null;
        if (string.Equals(key, UnclassifiedKey, StringComparison.Ordinal))
        {
            return true;
        }
        if (!MailCategories.TryParse(key, out var parsed))
        {
            return false;
        }
        category = parsed;
        return true;
    }
}
