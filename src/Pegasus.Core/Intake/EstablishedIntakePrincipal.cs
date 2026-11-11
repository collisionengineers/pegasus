namespace Pegasus.Core.Intake;

/// <summary>
/// The Principal a received item has established: the one owner of that
/// answer for Open the Triage, Create case on the item and automatic
/// allocation. An instruction draft's suggested Principal is it; with no
/// draft, an accepted mail route's Principal is, unless the instruction
/// profile selected for the item conflicts with that route or is ambiguous
/// (operator, 11 November 2026; R966). A sender that matched no route
/// establishes none.
/// </summary>
public static class EstablishedIntakePrincipal
{
    public static string? CodeFor(IntakeReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        return Normalize(receipt.InstructionDraft?.SuggestedPrincipalCode)
            ?? (SelectionConflicts(receipt.Evidence) ? null : RouteCode(receipt.MailRouteDecision));
    }

    /// <summary>The accepted mail route's Principal, whatever the profile selection said.</summary>
    public static string? RouteCode(MailRouteEvaluationResult? route) =>
        route is { Disposition: MailRouteDisposition.Accepted, SelectedRoute: { } selected }
            ? Normalize(selected.PrincipalCode)
            : null;

    /// <summary>
    /// Whether intake recorded that the item's instruction profile was
    /// ambiguous or named a different Principal from its route.
    /// </summary>
    public static bool SelectionConflicts(IEnumerable<IntakeEvidence> evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        return evidence.Any(item => item.Signal
            is IntakeEvidenceSignals.AmbiguousInstructionSelection
            or IntakeEvidenceSignals.ConflictingInstructionSelection);
    }

    public static string? Normalize(string? principalCode) =>
        string.IsNullOrWhiteSpace(principalCode) ? null : principalCode.Trim().ToUpperInvariant();
}
