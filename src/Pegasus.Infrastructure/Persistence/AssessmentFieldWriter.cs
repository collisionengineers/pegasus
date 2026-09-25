using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The one place a case assessment field row is materialised with its
/// provenance. The assessment save and the Case save write the whole surface
/// through it, the valuation adoption writes <c>assessment.values.engineer</c>,
/// <c>assessment.values.retail</c> and <c>assessment.values.trade</c> through
/// it, and the vehicle lookup and the original-report prefill write the values
/// they fill through it, so the row shape and the provenance stamped on it
/// have exactly one owner. A recorded value is the Case's value whoever
/// recorded it (operator, 25 September 2026); there is no confirmation state.
/// </summary>
internal static class AssessmentFieldWriter
{
    /// <summary>
    /// Adds or restamps one field row. The caller supplies the provenance
    /// because it may come from the current request or from a selected source
    /// row; deciding that source is business policy.
    /// </summary>
    public static CaseAssessmentFieldEntity Write(
        PegasusDbContext context,
        Guid workId,
        CaseAssessmentFieldEntity? existing,
        string path,
        string value,
        ActorKind recordedByKind,
        string recordedBy,
        DateTimeOffset recordedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (existing is null)
        {
            var created = new CaseAssessmentFieldEntity
            {
                WorkId = workId,
                FieldPath = path,
                Value = value,
                RecordedByKind = recordedByKind.ToString(),
                RecordedBy = recordedBy,
                RecordedAtUtc = recordedAtUtc
            };
            context.CaseAssessmentFields.Add(created);
            return created;
        }

        existing.Value = value;
        existing.RecordedByKind = recordedByKind.ToString();
        existing.RecordedBy = recordedBy;
        existing.RecordedAtUtc = recordedAtUtc;
        return existing;
    }

    /// <summary>The kind of actor that recorded the row, or null when there is no row.</summary>
    public static ActorKind? RecordedByKind(CaseAssessmentFieldEntity? existing) =>
        existing is null ? null : Enum.Parse<ActorKind>(existing.RecordedByKind);
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
        IEnumerable<CaseAssessmentFieldEntity> persisted,
        ActorKind actorKind)
    {
        ArgumentNullException.ThrowIfNull(requested);
        ArgumentNullException.ThrowIfNull(persisted);
        var merged = persisted.ToDictionary(
            item => item.FieldPath,
            item => item.Value,
            StringComparer.Ordinal);
        var toWrite = new Dictionary<string, string?>(requested, StringComparer.Ordinal);
        AssessmentPolicy.CompleteCoupledWrites(toWrite, merged, actorKind);
        foreach (var (path, value) in toWrite)
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

        if (toWrite.TryGetValue(AssessmentVocabulary.DamageImpacts, out var impacts))
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
    /// values the history record carries. A value that has not changed is
    /// left as it stands, whoever posted it: the one Case Save posts every
    /// control, and an untouched value keeps its provenance and its source
    /// tag (operator, 25 September 2026). Only a changed value is recorded
    /// with the actor's provenance.
    /// </summary>
    public static (Dictionary<string, object?> Before, Dictionary<string, object?> After) Apply(
        PegasusDbContext context,
        Guid workId,
        List<CaseAssessmentFieldEntity> fields,
        IReadOnlyDictionary<string, string?> toWrite,
        ActionActor actor,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(toWrite);
        ArgumentNullException.ThrowIfNull(actor);
        var before = new Dictionary<string, object?>(StringComparer.Ordinal);
        var after = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (path, value) in toWrite)
        {
            var existing = fields.SingleOrDefault(item => item.FieldPath == path);
            before[path] = existing?.Value;
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

            if (existing is null)
            {
                fields.Add(AssessmentFieldWriter.Write(
                    context, workId, null, path, value, actor.Kind, actor.SubjectId, now));
            }
            else if (!string.Equals(existing.Value, value, StringComparison.Ordinal))
            {
                AssessmentFieldWriter.Write(
                    context, workId, existing, path, value, actor.Kind, actor.SubjectId, now);
            }

            after[path] = value;
        }

        return (before, after);
    }
}
