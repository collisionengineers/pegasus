namespace Pegasus.Core.Intake;

/// <summary>
/// A versioned signature by which a document says which provider's instruction
/// it is, independently of how it arrived. Every required signal must be
/// present in the readable content and no negative signal may be; nothing else
/// takes part. There is deliberately no score, no priority, no weighting and no
/// optional-signal tie-break: the registry this is derived from
/// (<c>reference/workproviders-and-repairers/principal-identification-corpus.v1.json</c>)
/// records profiles "without numerical confidence, priority, or winner
/// selection", and a rule that ranked two matching profiles would be inventing
/// exactly that.
/// </summary>
/// <param name="DocumentRole">
/// What kind of document the signature describes — <c>instruction</c> for every
/// profile that exists today. Carried through to the recorded candidates so a
/// later profile for a different role cannot be confused with this one.
/// </param>
public sealed record InstructionDocumentSignature(
    string DocumentRole,
    IReadOnlyList<string> RequiredSignals,
    IReadOnlyList<string> NegativeSignals)
{
    /// <summary>
    /// The one document role every profile that exists today describes. Named
    /// once so the profiles, the selector's role filter and the caller cannot
    /// spell it three ways.
    /// </summary>
    public const string InstructionRole = "instruction";

    public static void Validate(InstructionDocumentSignature signature)
    {
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentException.ThrowIfNullOrWhiteSpace(signature.DocumentRole);
        ArgumentNullException.ThrowIfNull(signature.RequiredSignals);
        ArgumentNullException.ThrowIfNull(signature.NegativeSignals);
        if (signature.RequiredSignals.Count == 0)
        {
            throw new ArgumentException(
                "A document signature with no required signal would match every document.",
                nameof(signature));
        }
        if (signature.RequiredSignals.Concat(signature.NegativeSignals)
            .Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "A document signature signal must be non-empty.",
                nameof(signature));
        }
    }
}

/// <summary>
/// An extraction policy that can also be selected from a document's own
/// content. Optional: a policy without this interface is reachable only through
/// an established route, exactly as before. Implementing it is what lets
/// <see cref="AnalyzeRetainedInstruction"/> read retained material that no
/// route identified.
/// </summary>
public interface IInstructionDocumentProfile
{
    string DocumentProfileKey { get; }
    int DocumentProfileVersion { get; }
    InstructionDocumentSignature Signature { get; }

    /// <summary>
    /// The accepted template variants of THIS profile, where the registry
    /// records more than one signature for one principal - PCH's Performance
    /// and Lawshield forms. A document matches the profile only when the
    /// profile signature holds AND at least one accepted variant holds, so no
    /// variant is inferred from a logo and a variant nobody has evidenced
    /// (PCH's Everywhen) matches nothing.
    ///
    /// Two variants of ONE profile both matching is not ambiguity about which
    /// policy reads the document; it is ambiguity about which template a
    /// principal used, and it is recorded as that. A profile with no recorded
    /// variants is matched by its signature alone, exactly as before.
    /// </summary>
    IReadOnlyList<InstructionTemplateVariant> Variants => [];
}

/// <summary>
/// One accepted template signature of one profile, named so the matched
/// variant can be recorded beside the candidates rather than collapsed into
/// the profile's identity.
/// </summary>
public sealed record InstructionTemplateVariant(
    string Key,
    InstructionDocumentSignature Signature);

/// <summary>
/// Which separate role each of a policy's fields belongs to. Declared by the
/// policy from its own field definitions, so the roles the intake invariants
/// keep apart - claimant, driver, repairer, third party, principal reference,
/// insurer reference - reach the recorded candidates instead of being lost
/// between the policy and the store.
/// </summary>
public interface IInstructionFieldRoles
{
    IReadOnlyDictionary<string, InstructionFieldRole> FieldRoles { get; }
}

public sealed record InstructionFieldRole(string? PartyRole, string? ReferenceRole);

public enum InstructionPolicySelectionOutcome
{
    Selected,
    NotApplicable,
    Ambiguous
}

/// <summary>
/// Exactly one of: <c>Selected</c> with the single matching policy;
/// <c>NotApplicable</c> when none matched; <c>Ambiguous</c> with EVERY matching
/// policy listed, so a member of staff sees which profiles competed rather than
/// a silently chosen winner.
/// </summary>
public sealed record InstructionPolicySelection(
    InstructionPolicySelectionOutcome Outcome,
    IInstructionExtractionPolicy? Policy,
    IReadOnlyList<IInstructionExtractionPolicy> Matches,
    IReadOnlyList<string> MatchedVariantKeys)
{
    public static InstructionPolicySelection Selected(
        IInstructionExtractionPolicy policy,
        IReadOnlyList<string>? variantKeys = null) =>
        new(InstructionPolicySelectionOutcome.Selected, policy, [policy], variantKeys ?? []);

    public static InstructionPolicySelection NotApplicable() =>
        new(InstructionPolicySelectionOutcome.NotApplicable, null, [], []);

    public static InstructionPolicySelection Ambiguous(
        IReadOnlyList<IInstructionExtractionPolicy> matches) =>
        new(InstructionPolicySelectionOutcome.Ambiguous, null, matches, []);

    /// <summary>
    /// True when the profile is settled but WHICH of its accepted templates
    /// the document used is not - the two PCH footers co-occur in four of the
    /// five recorded originals. The principal is not in doubt; the template is,
    /// and staff are shown both rather than one picked by order.
    /// </summary>
    public bool HasAmbiguousVariant => this.MatchedVariantKeys.Count > 1;
}

/// <summary>
/// Chooses an instruction extraction policy from what a document SAYS, never
/// from how it arrived. The pipeline's ordinary path establishes the principal
/// from an accepted mail route and then requires the extraction policy to agree
/// with it; retained material that no route identified has no such anchor, so
/// the document itself must propose the principal — and only propose it: the
/// selector allocates nothing and decides nothing beyond which policy reads the
/// document.
///
/// Every policy that declares a signature is evaluated; the first match never
/// wins by being first, and the enumeration order of the injected policies
/// cannot change the answer.
/// </summary>
public sealed class InstructionExtractionPolicySelector(
    IEnumerable<IInstructionExtractionPolicy> policies)
{
    private readonly IEnumerable<IInstructionExtractionPolicy> policies =
        policies ?? throw new ArgumentNullException(nameof(policies));

    /// <param name="documentRole">
    /// The role of document being read - <c>instruction</c> for the one caller
    /// that exists today. A signature is matched on its role AND its signals:
    /// a profile written for a different document role is not a candidate
    /// here, however well its labels happen to read.
    /// </param>
    public InstructionPolicySelection Select(IntakeSourceReadResult readResult, string documentRole)
    {
        ArgumentNullException.ThrowIfNull(readResult);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentRole);
        if (readResult.Status != IntakeSourceReadStatus.Readable)
        {
            return InstructionPolicySelection.NotApplicable();
        }

        var text = Text(readResult);
        var matches = this.policies
            .Select(policy => (Policy: policy, Profile: policy as IInstructionDocumentProfile))
            .Where(entry => entry.Profile is not null
                && string.Equals(
                    entry.Profile.Signature.DocumentRole,
                    documentRole,
                    StringComparison.OrdinalIgnoreCase)
                && Matches(entry.Profile.Signature, text))
            .Select(entry => (entry.Policy, Variants: MatchingVariants(entry.Profile!, text)))
            .Where(entry => entry.Variants is not null)
            .OrderBy(entry => entry.Policy.PrincipalCode, StringComparer.Ordinal)
            .ToArray();

        return matches.Length switch
        {
            0 => InstructionPolicySelection.NotApplicable(),
            1 => InstructionPolicySelection.Selected(matches[0].Policy, matches[0].Variants),
            _ => InstructionPolicySelection.Ambiguous([.. matches.Select(entry => entry.Policy)])
        };
    }

    /// <summary>
    /// The accepted variant keys this document satisfies, or null when the
    /// profile records variants and the document satisfies none - an unproved
    /// template stays unmatched rather than borrowing its principal's identity.
    /// A profile with no recorded variants returns the empty list, which is a
    /// match.
    /// </summary>
    private static string[]? MatchingVariants(
        IInstructionDocumentProfile profile,
        string text)
    {
        if (profile.Variants.Count == 0)
        {
            return [];
        }

        var matched = profile.Variants
            .Where(variant => Matches(variant.Signature, text))
            .Select(variant => variant.Key)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
        return matched.Length == 0 ? null : matched;
    }

    /// <summary>
    /// Signals are matched case-insensitively against the document's readable
    /// content — the labels a provider prints vary in case between templates,
    /// and the corpus records them as printed rather than as a canonical form.
    /// Transport evidence is deliberately excluded: a sender address is route
    /// identity, which is the very thing this selection must not rely on.
    /// </summary>
    private static bool Matches(InstructionDocumentSignature signature, string text)
    {
        InstructionDocumentSignature.Validate(signature);
        return signature.RequiredSignals.All(signal =>
                text.Contains(signal, StringComparison.OrdinalIgnoreCase))
            && !signature.NegativeSignals.Any(signal =>
                text.Contains(signal, StringComparison.OrdinalIgnoreCase));
    }

    private static string Text(IntakeSourceReadResult readResult) =>
        string.Join('\n', readResult.Content.Select(fragment => fragment.Text));
}
