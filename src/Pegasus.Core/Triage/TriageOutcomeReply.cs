using Pegasus.Core.Operations;

namespace Pegasus.Core.Triage;

/// <summary>
/// Reply with outcome: what one Triage supplies to the Triage outcome reply
/// template (<see cref="EmailTemplatePurpose.TriageOutcomeReply"/>). The
/// template, its placeholders and its built-in body belong to
/// <see cref="EmailTemplates"/>; staff edit the rendered text before Send, and
/// the subject stays the origin's with one "Re:".
/// </summary>
/// <remarks>
/// A dimension that was not recorded has no value, so its line is left out
/// of the rendered body.
/// </remarks>
public sealed record TriageOutcomeReply(string Registration, TriageFinding? Finding)
{
    /// <summary>The placeholder values this Triage supplies, by placeholder name.</summary>
    public IReadOnlyDictionary<string, string?> Values() =>
        new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [EmailTemplates.Registration] = Registration,
            [EmailTemplates.Roadworthiness] = Finding?.Roadworthiness is { } roadworthiness ? Label(roadworthiness) : null,
            [EmailTemplates.RepairOutcome] = Finding?.Assessment is { } assessment ? Label(assessment) : null,
            [EmailTemplates.FindingReason] = Finding?.Reason
        };

    public static string Label(RoadworthinessFinding finding) => finding switch
    {
        RoadworthinessFinding.Roadworthy => "Roadworthy",
        RoadworthinessFinding.Unroadworthy => "Unroadworthy",
        _ => throw new InvalidOperationException(
            $"Unknown roadworthiness finding value '{(int)finding}'.")
    };

    public static string Label(AssessmentFinding finding) => finding switch
    {
        AssessmentFinding.Repairable => "Repairable",
        AssessmentFinding.TotalLoss => "Total loss",
        _ => throw new InvalidOperationException(
            $"Unknown assessment finding value '{(int)finding}'.")
    };
}
