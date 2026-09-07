using Pegasus.Core.Assessment;
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

/// <summary>
/// The write set one assessment save turns into row changes: the fields the
/// caller asked for plus the values derived from the damage impacts, and the
/// merged state the cross-field rules are checked against. The assessment
/// command and the Case workspace save share it, so the derived impact
/// location and severity cannot be recorded one way through one route and a
/// different way through the other.
/// </summary>
internal static class AssessmentWriteSet
{
    public static (Dictionary<string, string?> ToWrite, Dictionary<string, string> Merged) Build(
        IReadOnlyDictionary<string, string?> requested,
        IEnumerable<CaseAssessmentFieldEntity> persisted)
    {
        ArgumentNullException.ThrowIfNull(requested);
        ArgumentNullException.ThrowIfNull(persisted);
        var merged = persisted.ToDictionary(
            item => item.FieldPath,
            item => item.Value,
            StringComparer.Ordinal);
        foreach (var (path, value) in requested)
        {
            if (value is null)
            {
                merged.Remove(path);
            }
            else
            {
                merged[path] = value;
            }
        }

        var toWrite = new Dictionary<string, string?>(requested, StringComparer.Ordinal);
        if (requested.TryGetValue(AssessmentVocabulary.DamageImpacts, out var impacts))
        {
            var derived = AssessmentPolicy.DeriveImpactValues(impacts);
            toWrite[AssessmentVocabulary.ImpactLocation] = derived.Location;
            toWrite[AssessmentVocabulary.ImpactSeverity] = derived.Severity;
            if (derived.Location is null)
            {
                merged.Remove(AssessmentVocabulary.ImpactLocation);
                merged.Remove(AssessmentVocabulary.ImpactSeverity);
            }
            else
            {
                merged[AssessmentVocabulary.ImpactLocation] = derived.Location;
                merged[AssessmentVocabulary.ImpactSeverity] = derived.Severity!;
            }
        }

        return (toWrite, merged);
    }

    /// <summary>
    /// Applies the write set to the tracked rows and returns the before/after
    /// evidence the history record carries.
    /// </summary>
    public static (Dictionary<string, object?> Before, Dictionary<string, object?> After) Apply(
        PegasusDbContext context,
        CaseEntity owningCase,
        Guid caseId,
        List<CaseAssessmentFieldEntity> fields,
        IReadOnlyDictionary<string, string?> toWrite,
        ActionActor actor,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(toWrite);
        ArgumentNullException.ThrowIfNull(actor);
        var confirmedBy = actor.Kind == ActorKind.Staff ? actor.SubjectId : null;
        var before = new Dictionary<string, object?>(StringComparer.Ordinal);
        var after = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (path, value) in toWrite)
        {
            var existing = fields.SingleOrDefault(item => item.FieldPath == path);
            before[path] = existing is null
                ? null
                : new { existing.Value, existing.ConfirmedBy };
            if (value is null)
            {
                if (existing is not null)
                {
                    context.CaseAssessmentFields.Remove(existing);
                    fields.Remove(existing);
                }
                after[path] = null;
                continue;
            }

            if (existing is not null
                && confirmedBy is null
                && string.Equals(existing.Value, value, StringComparison.Ordinal))
            {
                // An automation resubmission of a value that has not changed
                // leaves the record alone: saving unchanged data must not
                // reset readiness or advisory state (FRD-01 case identity and
                // lifecycle, the progression rules), so a value a staff Engineer
                // already confirmed stays confirmed and keeps its
                // provenance. A staff save still re-stamps, because that is
                // how an Engineer confirms a value.
            }
            else
            {
                var written = AssessmentFieldWriter.Write(
                    context,
                    owningCase,
                    caseId,
                    existing,
                    path,
                    value,
                    actor.Kind,
                    actor.SubjectId,
                    now,
                    confirmedBy);
                if (existing is null)
                {
                    fields.Add(written);
                }
                existing = written;
            }

            after[path] = new { existing.Value, existing.ConfirmedBy };
        }

        return (before, after);
    }
}
