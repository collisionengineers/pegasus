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

    public const string OtherReceivedKey = "other-received";
    public const string OtherSentKey = "other-sent";

    public static IReadOnlyList<SelectionOption> Options { get; } =
        [
            .. Enum.GetValues<ReceivedMailFamily>().SelectMany(family =>
                MailTaxonomy.ConfirmedReceivedSubtypes[family].Length == 0
                    ? [new SelectionOption($"received:{family}", MailTaxonomy.CategoryName(family))]
                    : MailTaxonomy.ConfirmedReceivedSubtypes[family].Select(subtype =>
                        new SelectionOption($"received:{family}:{subtype}", $"{MailTaxonomy.CategoryName(family)}/{subtype}"))),
            .. Enum.GetValues<SentMailFamily>().Select(family =>
                new SelectionOption($"sent:{family}", $"Sent: {MailTaxonomy.CategoryName(family)}")),
            new(OtherReceivedKey, "Other received classification"),
            new(OtherSentKey, "Other sent classification")
        ];

    /// <summary>
    /// Parses a selected classification key into the canonical category.
    /// Returns false — never a guessed category — for an unknown key, an
    /// unregistered subtype, or Other details that are absent or outside the
    /// canonical bounds.
    /// </summary>
    public static bool TryParse(
        string? key,
        string? otherName,
        string? otherReasoning,
        out MailCategory? category)
    {
        category = null;
        var parts = key?.Split(':');
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
        if (parts is ["sent", var sent]
            && Enum.TryParse<SentMailFamily>(sent, out var sentFamily)
            && Enum.IsDefined(sentFamily))
        {
            category = MailCategory.Sent(sentFamily);
            return true;
        }
        var otherDirection = key switch
        {
            OtherReceivedKey => MailDirection.Received,
            OtherSentKey => MailDirection.Sent,
            _ => (MailDirection?)null
        };
        if (otherDirection is null
            || string.IsNullOrWhiteSpace(otherName)
            || string.IsNullOrWhiteSpace(otherReasoning)
            || otherName.Trim().Length > MailCategory.OtherNameMaxLength
            || otherReasoning.Trim().Length > MailCategory.OtherReasoningMaxLength)
        {
            return false;
        }
        category = MailCategory.Other(
            otherDirection.Value,
            otherName,
            otherReasoning);
        return true;
    }
}
