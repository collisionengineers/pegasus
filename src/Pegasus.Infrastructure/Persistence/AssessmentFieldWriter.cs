using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The one place a case assessment field row is materialised with its
/// provenance. The assessment save writes the whole surface through it, and
/// the Engineer's Value valuation writes the single confirmed
/// <c>assessment.values.engineer</c> field through it, so the row shape and
/// the provenance stamped on it have exactly one owner.
/// </summary>
internal static class AssessmentFieldWriter
{
    /// <summary>
    /// Adds or restamps one field row. A null <paramref name="confirmedBy"/>
    /// records unconfirmed working data; the caller decides whether its actor
    /// confirms, because that is business policy.
    /// </summary>
    public static CaseAssessmentFieldEntity Write(
        PegasusDbContext context,
        CaseEntity owningCase,
        Guid caseId,
        CaseAssessmentFieldEntity? existing,
        string path,
        string value,
        ActionActor actor,
        string? confirmedBy,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(actor);
        if (existing is null)
        {
            var created = new CaseAssessmentFieldEntity
            {
                CaseId = caseId,
                Case = owningCase,
                FieldPath = path,
                Value = value,
                RecordedByKind = actor.Kind.ToString(),
                RecordedBy = actor.SubjectId,
                RecordedAtUtc = nowUtc,
                ConfirmedBy = confirmedBy,
                ConfirmedAtUtc = confirmedBy is null ? null : nowUtc
            };
            context.CaseAssessmentFields.Add(created);
            return created;
        }

        existing.Value = value;
        existing.RecordedByKind = actor.Kind.ToString();
        existing.RecordedBy = actor.SubjectId;
        existing.RecordedAtUtc = nowUtc;
        existing.ConfirmedBy = confirmedBy;
        existing.ConfirmedAtUtc = confirmedBy is null ? null : nowUtc;
        return existing;
    }
}
