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
}

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
    IReadOnlyList<IInstructionExtractionPolicy> Matches)
{
    public static InstructionPolicySelection Selected(IInstructionExtractionPolicy policy) =>
        new(InstructionPolicySelectionOutcome.Selected, policy, [policy]);

    public static InstructionPolicySelection NotApplicable() =>
        new(InstructionPolicySelectionOutcome.NotApplicable, null, []);

    public static InstructionPolicySelection Ambiguous(
        IReadOnlyList<IInstructionExtractionPolicy> matches) =>
        new(InstructionPolicySelectionOutcome.Ambiguous, null, matches);
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

    public InstructionPolicySelection Select(IntakeSourceReadResult readResult)
    {
        ArgumentNullException.ThrowIfNull(readResult);
        if (readResult.Status != IntakeSourceReadStatus.Readable)
        {
            return InstructionPolicySelection.NotApplicable();
        }

        var text = Text(readResult);
        var matches = this.policies
            .Where(policy => policy is IInstructionDocumentProfile profile
                && Matches(profile.Signature, text))
            .OrderBy(policy => policy.PrincipalCode, StringComparer.Ordinal)
            .ToArray();

        return matches.Length switch
        {
            0 => InstructionPolicySelection.NotApplicable(),
            1 => InstructionPolicySelection.Selected(matches[0]),
            _ => InstructionPolicySelection.Ambiguous(matches)
        };
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
