using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Assessment;

/// <summary>
/// Where an AI proposal on a decision field stands. Derived from what was
/// recorded, never typed by a person: a proposal waits until a member of
/// staff records the field, which accepts it (the same value) or corrects it
/// (any other value, or none).
/// </summary>
public enum CaseFieldProposalStatus
{
    Awaiting,
    Accepted,
    Corrected
}

/// <summary>
/// The latest value the Automation actor proposed for one decision field on
/// one Case, and how staff resolved it. The recorded field row itself carries
/// only the current value, so this is the one place the proposal survives
/// staff confirming or correcting it.
/// </summary>
public sealed record CaseFieldProposal(
    string FieldPath,
    string ProposedValue,
    string ProposedBy,
    DateTimeOffset ProposedAtUtc,
    CaseFieldProposalStatus Status,
    string? ResolvedBy,
    DateTimeOffset? ResolvedAtUtc);

/// <summary>
/// The one owner of which fields carry proposals and how a write moves one.
/// Infrastructure calls <see cref="Next"/> from the single field writer, so
/// the assessment command, the Case save and the valuation Apply resolve a
/// proposal identically.
/// </summary>
public static class CaseFieldProposalPolicy
{
    /// <summary>
    /// The Settlement decision fields (Outcome, Engineer's Value, Salvage
    /// category and value, Roadworthiness and the unroadworthy reason).
    /// </summary>
    public static IReadOnlySet<string> DecisionPaths { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        AssessmentVocabulary.Outcome,
        AssessmentVocabulary.ValueEngineer,
        AssessmentVocabulary.SalvageCategory,
        AssessmentVocabulary.SalvageValue,
        AssessmentVocabulary.LegalStatus,
        AssessmentVocabulary.UnroadworthyReason
    };

    /// <summary>
    /// The proposal after one write of <paramref name="value"/> (null clears)
    /// to <paramref name="path"/>. An Automation write of a value records a
    /// fresh Awaiting proposal; a staff write resolves an Awaiting proposal —
    /// Accepted when the value is the one proposed, Corrected otherwise. A
    /// resolved proposal, a staff write with nothing proposed, an Automation
    /// clear and every non-decision path leave <paramref name="current"/>
    /// unchanged.
    /// </summary>
    public static CaseFieldProposal? Next(
        CaseFieldProposal? current,
        string path,
        string? value,
        ActorKind actorKind,
        string actorSubjectId,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!DecisionPaths.Contains(path))
        {
            return current;
        }

        if (actorKind == ActorKind.Automation)
        {
            return value is null
                ? current
                : new CaseFieldProposal(path, value, actorSubjectId, now, CaseFieldProposalStatus.Awaiting, null, null);
        }

        if (current is not { Status: CaseFieldProposalStatus.Awaiting })
        {
            return current;
        }

        var status = value is not null && string.Equals(value, current.ProposedValue, StringComparison.Ordinal)
            ? CaseFieldProposalStatus.Accepted
            : CaseFieldProposalStatus.Corrected;
        return current with { Status = status, ResolvedBy = actorSubjectId, ResolvedAtUtc = now };
    }
}

public interface ICaseFieldProposalQueries
{
    /// <summary>Every recorded proposal on the Case, one per decision field.</summary>
    Task<IReadOnlyList<CaseFieldProposal>> ListForCaseAsync(
        Guid caseId,
        CaseWorkSelector work,
        CancellationToken cancellationToken);
}
