using Pegasus.Core.Cases;

namespace Pegasus.Core.Intake;

/// <summary>
/// The Repairer a received item establishes for the Case made from it, and
/// the instruction field that named it; <paramref name="Field"/> is null when
/// the instruction's text named it instead (R1264).
/// </summary>
public sealed record EstablishedRepairer(ContactChoice Repairer, InstructionReviewField? Field);

/// <summary>
/// The Repairer a received item has established: the one owner of that answer
/// for every route that makes a Case from the item (operator, 12 November
/// 2026; R1025, R1027). It is the rule <see cref="EstablishedIntakeClaimSource"/>
/// applies to an instruction's claim source: the repairer the instruction
/// names is the Case's when that name is an active Repairer's, ignoring
/// capitals. Otherwise the instruction's text: the one active Repairer linked
/// to the Case's Principal that its subject, body or attachment text names,
/// ignoring capitals and spaces (operator, 25 November 2026; R1264). Two or
/// more matches, or none, establish nothing, and a name that is not a
/// Repairer never becomes one.
/// </summary>
public static class EstablishedIntakeRepairer
{
    /// <summary>
    /// The one repairer the instruction names, or null when it names none,
    /// its field is conflicted, or two fields name different ones.
    /// </summary>
    public static InstructionReviewField? NamedIn(IEnumerable<InstructionReviewField> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var named = fields
            .Where(field => field.ToCaseDataFieldName() == CaseDataFieldNames.RepairerName
                && !field.HasConflict
                && !string.IsNullOrWhiteSpace(field.SuggestedValue))
            .ToArray();
        return named.Select(field => field.SuggestedValue!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() == 1
                ? named[0]
                : null;
    }

    /// <param name="fields">The item's instruction review fields.</param>
    /// <param name="activeRepairers">Every active Repairer, with its linked Principals.</param>
    /// <param name="casePrincipalCode">The Principal the Case is made for.</param>
    /// <param name="instructionText">The instruction's subject, body and attachment text, or null.</param>
    public static EstablishedRepairer? For(
        IEnumerable<InstructionReviewField> fields,
        IReadOnlyList<ContactChoice> activeRepairers,
        string? casePrincipalCode = null,
        string? instructionText = null)
    {
        ArgumentNullException.ThrowIfNull(activeRepairers);
        if (NamedIn(fields) is { } field)
        {
            var name = field.SuggestedValue!.Trim();
            var named = activeRepairers
                .Where(repairer => string.Equals(repairer.Name.Trim(), name, StringComparison.OrdinalIgnoreCase))
                .Take(2)
                .ToArray();
            if (named.Length == 1)
            {
                return new(named[0], field);
            }
        }

        return NamedInText.LinkedContact(instructionText, casePrincipalCode, activeRepairers) is { } mentioned
            ? new(mentioned, null)
            : null;
    }
}
