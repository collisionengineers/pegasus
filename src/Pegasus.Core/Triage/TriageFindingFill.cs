using Pegasus.Core.Assessment;

namespace Pegasus.Core.Triage;

/// <summary>
/// What a Triage finding fills on the instruction Case it is linked to
/// (operator, 7 October 2026): its Roadworthiness and repair outcome, once,
/// when the Triage is linked with a finding or a linked Triage records its
/// first one. A value fills only a cell the Case holds nothing in; a
/// superseding finding, a later link and an unlink change nothing.
/// </summary>
public static class TriageFindingFill
{
    /// <summary>The Automation actor a filled value is recorded as.</summary>
    public const string RecorderId = "triage-finding";

    /// <summary>A value fills only where the work holds none.</summary>
    public static bool Fills(string? existing) => string.IsNullOrWhiteSpace(existing);

    /// <summary>
    /// The finding's recorded dimensions as the Case's assessment cells; a
    /// dimension the finding left unrecorded fills nothing.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Values(
        RoadworthinessFinding? roadworthiness,
        AssessmentFinding? assessment)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (roadworthiness is { } legalStatus)
        {
            values[AssessmentVocabulary.LegalStatus] = legalStatus switch
            {
                RoadworthinessFinding.Roadworthy => "roadworthy",
                RoadworthinessFinding.Unroadworthy => "unroadworthy",
                _ => throw new ArgumentOutOfRangeException(nameof(roadworthiness))
            };
        }

        if (assessment is { } outcome)
        {
            values[AssessmentVocabulary.Outcome] = outcome switch
            {
                AssessmentFinding.Repairable => "repairable",
                AssessmentFinding.TotalLoss => "total_loss",
                _ => throw new ArgumentOutOfRangeException(nameof(assessment))
            };
        }

        return values;
    }
}
