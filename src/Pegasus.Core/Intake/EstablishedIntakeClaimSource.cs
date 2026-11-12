using Pegasus.Core.Cases;

namespace Pegasus.Core.Intake;

/// <summary>Which evidence gave a new Case its Claim Source.</summary>
public enum IntakeClaimSourceOrigin
{
    /// <summary>The instruction named it.</summary>
    Instruction,

    /// <summary>The e-mail came from it.</summary>
    Sender
}

/// <summary>
/// The Claim Source a received item establishes for the Case made from it.
/// <paramref name="Field"/> is the instruction field that named it, for an
/// <see cref="IntakeClaimSourceOrigin.Instruction"/> origin.
/// </summary>
public sealed record EstablishedClaimSource(
    ContactChoice ClaimSource,
    IntakeClaimSourceOrigin Origin,
    InstructionReviewField? Field = null);

/// <summary>
/// The Claim Source a received item has established: the one owner of that
/// answer for every route that makes a Case from the item (operator, 12
/// November 2026; R1014, R1017). The instruction comes first: a claim source
/// it names is the Case's when that name is an active Claim Source's, ignoring
/// capitals. Otherwise the sender: mail accepted through an intermediary gives
/// the one active Claim Source linked to the route's Principal that the sender
/// matches. Two or more matches, or none, establish nothing, and a name that
/// is not a Claim Source never becomes one.
/// </summary>
public static class EstablishedIntakeClaimSource
{
    /// <summary>The instruction field that names a claim source on DFD's form.</summary>
    public const string ClaimSourceField = "Claim source";

    /// <summary>The instruction field that names one on SBL's and OAK's instructions.</summary>
    public const string IntroducerField = "Introducer";

    /// <summary>
    /// Whether the item carries anything this rule reads, so a caller reads
    /// the Claim Sources only for an item that could take one.
    /// </summary>
    public static bool HasEvidence(
        IEnumerable<InstructionReviewField> fields,
        string? intermediaryPrincipalCode) =>
        NamedIn(fields) is not null || !string.IsNullOrWhiteSpace(intermediaryPrincipalCode);

    /// <summary>
    /// The one claim source the instruction names, or null when it names none,
    /// its field is conflicted, or two fields name different ones.
    /// </summary>
    public static InstructionReviewField? NamedIn(IEnumerable<InstructionReviewField> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var named = fields
            .Where(field => field.Name is ClaimSourceField or IntroducerField
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
    /// <param name="intermediaryPrincipalCode">The accepted mail route's Principal when
    /// that route is an intermediary's; otherwise null.</param>
    /// <param name="effectiveSenderAddress">The route's effective sender.</param>
    /// <param name="activeClaimSources">Every active Claim Source, with its linked Principals.</param>
    public static EstablishedClaimSource? For(
        IEnumerable<InstructionReviewField> fields,
        string? intermediaryPrincipalCode,
        string? effectiveSenderAddress,
        IReadOnlyList<ContactChoice> activeClaimSources)
    {
        ArgumentNullException.ThrowIfNull(activeClaimSources);
        if (NamedIn(fields) is { } field)
        {
            var name = field.SuggestedValue!.Trim();
            var named = activeClaimSources
                .Where(source => string.Equals(source.Name.Trim(), name, StringComparison.OrdinalIgnoreCase))
                .Take(2)
                .ToArray();
            if (named.Length == 1)
            {
                return new(named[0], IntakeClaimSourceOrigin.Instruction, field);
            }
        }

        if (string.IsNullOrWhiteSpace(intermediaryPrincipalCode)
            || IntermediaryDomains.IdentityOf(effectiveSenderAddress?.Trim()) is not { } sender)
        {
            return null;
        }

        var senders = activeClaimSources
            .Where(source => source.IsLinkedTo(intermediaryPrincipalCode)
                && string.Equals(
                    IntermediaryDomains.IdentityOf(source.Email?.Trim()),
                    sender,
                    StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToArray();
        return senders.Length == 1 ? new(senders[0], IntakeClaimSourceOrigin.Sender) : null;
    }
}
