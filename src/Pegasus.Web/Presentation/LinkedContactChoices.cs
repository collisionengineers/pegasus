using Pegasus.Core.Cases;

namespace Pegasus.Web.Presentation;

/// <summary>
/// A Case's Claim Source or Repairer list in the order it is drawn: the
/// contacts linked to the Case's Principal first, under that Principal's code,
/// then the others under their own heading, each part in name order
/// (operator, 12 November 2026; R1013, R1015, R1016). A Principal with no
/// linked contact, and a Case that names no Principal, keep the one plain
/// list. The Case record's two lists and New case's share this owner, so they
/// cannot order differently.
/// </summary>
public static class LinkedContactChoices
{
    /// <summary>One drawn part of the list; a null label is the plain list.</summary>
    public sealed record Group(string? Label, IReadOnlyList<ContactChoice> Choices);

    public static IReadOnlyList<Group> For(
        IReadOnlyList<ContactChoice> choices,
        string? principalCode,
        string othersLabel)
    {
        ArgumentNullException.ThrowIfNull(choices);
        var linked = choices.Where(choice => choice.IsLinkedTo(principalCode)).ToArray();
        if (linked.Length == 0)
        {
            return [new(null, choices)];
        }

        var others = choices.Where(choice => !choice.IsLinkedTo(principalCode)).ToArray();
        var heading = principalCode!.Trim().ToUpperInvariant();
        return others.Length == 0
            ? [new(heading, linked)]
            : [new(heading, linked), new(othersLabel, others)];
    }
}
