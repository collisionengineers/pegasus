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
    /// records unconfirmed working data. The caller supplies the provenance
    /// because it may come from the current request or from a selected source
    /// row; deciding that source and whether it confirms are business policy.
    /// </summary>
    public static CaseAssessmentFieldEntity Write(
        PegasusDbContext context,
        CaseEntity owningCase,
        Guid caseId,
        CaseAssessmentFieldEntity? existing,
        string path,
        string value,
        ActorKind recordedByKind,
        string recordedBy,
        DateTimeOffset recordedAtUtc,
        string? confirmedBy)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (existing is null)
        {
            var created = new CaseAssessmentFieldEntity
            {
                CaseId = caseId,
                Case = owningCase,
                FieldPath = path,
                Value = value,
                RecordedByKind = recordedByKind.ToString(),
                RecordedBy = recordedBy,
                RecordedAtUtc = recordedAtUtc,
                ConfirmedBy = confirmedBy,
                ConfirmedAtUtc = confirmedBy is null ? null : recordedAtUtc
            };
            context.CaseAssessmentFields.Add(created);
            return created;
        }

        existing.Value = value;
        existing.RecordedByKind = recordedByKind.ToString();
        existing.RecordedBy = recordedBy;
        existing.RecordedAtUtc = recordedAtUtc;
        existing.ConfirmedBy = confirmedBy;
        existing.ConfirmedAtUtc = confirmedBy is null ? null : recordedAtUtc;
        return existing;
    }
}
