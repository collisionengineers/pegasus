using Pegasus.Core.Cases;
using Pegasus.Core.Intake;

namespace Pegasus.Web.Presentation;

/// <summary>
/// The one presentation-side vocabulary for choosing a corrected mail
/// classification: the canonical option keys offered to staff, and the parse
/// from a chosen key back to the Core <see cref="MailCategory"/>. The mail
/// message page and the Automation MCP mail tools both consume this single
/// list, so a corrected taxonomy entry appears — or disappears — for both
/// callers at once.
/// </summary>
public static class MailClassificationSelection
{
    public sealed record SelectionOption(string Value, string Label);

    // Labels resolve through the one operator label map, so the picker and
    // every read-only rendering of a classification use the same words.
    public static IReadOnlyList<SelectionOption> Options { get; } =
        [
            .. Enum.GetValues<ReceivedMailFamily>().SelectMany(family =>
                MailTaxonomy.ConfirmedReceivedSubtypes[family].Length == 0
                    ? [new SelectionOption(
                        $"received:{family}",
                        OperatorLabels.MailClassification(MailCategory.Received(family)))]
                    : MailTaxonomy.ConfirmedReceivedSubtypes[family].Select(subtype =>
                        new SelectionOption(
                            $"received:{family}:{subtype}",
                            OperatorLabels.MailClassification(MailCategory.Received(family, subtype)))))
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
    /// Parses a selected classification key into the canonical category.
    /// Returns false — never a guessed category — for an unknown key or an
    /// unregistered subtype.
    /// </summary>
    public static bool TryParse(string? value, out MailCategory? category)
    {
        category = null;
        var parts = value?.Split(':');
        if (parts is ["received", var received]
            && Enum.TryParse<ReceivedMailFamily>(received, out var receivedFamily)
            && Enum.IsDefined(receivedFamily))
        {
            category = MailCategory.Received(receivedFamily);
            return true;
        }
        if (parts is ["received", var receivedWithSubtype, var subtype]
            && Enum.TryParse<ReceivedMailFamily>(receivedWithSubtype, out var subtypeFamily))
        {
            try
            {
                category = MailCategory.Received(subtypeFamily, subtype);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
        return false;
    }
}
