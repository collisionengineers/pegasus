using Pegasus.Core.Operations;

namespace Pegasus.Core.Tasks;

/// <summary>
/// What one Case supplies to the Case chaser template
/// (<see cref="EmailTemplatePurpose.CaseChaser"/>). The template, its
/// placeholders and its built-in body belong to <see cref="EmailTemplates"/>.
/// </summary>
/// <remarks>
/// A fact that was not recorded has no value, so its line is left out of the
/// rendered body. The outstanding material is the Case's missing-material
/// reason while it has due work; a Case with none renders no such line.
/// There is no Case/PO value: the reference is internal (operator,
/// 5 October 2026).
/// </remarks>
public sealed record CaseChaserFacts(
    string? Registration,
    string? OutstandingMaterial,
    string? PrincipalName,
    string? Claimant)
{
    /// <summary>The placeholder values this Case supplies, by placeholder name.</summary>
    public IReadOnlyDictionary<string, string?> Values() =>
        new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [EmailTemplates.Registration] = Registration,
            [EmailTemplates.OutstandingMaterial] = OutstandingMaterial,
            [EmailTemplates.PrincipalName] = PrincipalName,
            [EmailTemplates.Claimant] = Claimant
        };

    /// <summary>
    /// The chaser's subject: the registration and the claimant, the two facts
    /// the party chased knows the matter by, joined with an en dash; an absent
    /// part is left out, and both absent gives an empty subject for staff to
    /// write. The subject is never templated.
    /// </summary>
    public string Subject() =>
        string.Join(" – ", new[] { Registration, Claimant }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => part!.Trim()));
}
