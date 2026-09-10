using Pegasus.Core.Actors;
using Pegasus.Core.AiWork;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Presentation;

/// <summary>
/// The AI job ledger's shared Web-side reading rules: where a job's subject
/// record lives, and who started it. The Operations AI Job List is the live
/// work queue and Administration Action Logs is the recorded history of the
/// same jobs, so both resolve a job the same way here rather than each
/// carrying its own map.
/// </summary>
public static class AiJobActions
{
    /// <summary>
    /// The record page a job's subject opens, or <see langword="null"/> when
    /// the job names no record. A queue pass is the only such kind: its
    /// subject is the Unidentified queue itself.
    /// </summary>
    public static string? RecordPage(AiJobSubjectKind subjectKind) => subjectKind switch
    {
        AiJobSubjectKind.Case => "/Cases/Details",
        AiJobSubjectKind.Unidentified => "/Unidentified/Details",
        _ => null
    };

    /// <summary>
    /// Who started one job, by the same rule the Action Logs Actor column
    /// uses: a staff subject resolves to a username (a removed account to
    /// "Former staff"), the Automation client to its registered name and the
    /// Worker to the product's own name. A raw subject identifier is never
    /// printed.
    /// </summary>
    /// <param name="staffNames">
    /// Usernames already resolved for the staff subjects on the list, from
    /// <see cref="ActorDisplayNames.ResolveStaffNamesAsync"/>.
    /// </param>
    /// <param name="configuredClientId">
    /// The composed Automation client identifier, when one is composed, so a
    /// recorded client resolves to its display name.
    /// </param>
    public static string StartedBy(
        AiJobRecord job,
        IReadOnlyDictionary<Guid, string> staffNames,
        string? configuredClientId)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(staffNames);
        return job.CreatedByKind switch
        {
            ActorKind.Automation =>
                OperatorLabels.AutomationActorLabel(job.CreatedBy, configuredClientId),
            ActorKind.SystemWorker => OperatorLabels.SystemActorLabel,
            _ => ActorDisplayNames.Resolve(job.CreatedByKind, job.CreatedBy, staffNames)
        };
    }

    /// <summary>
    /// The staff subject ids named as the creators of these jobs, for one
    /// name resolution per list rather than per row.
    /// </summary>
    public static IEnumerable<Guid> StaffCreatorIds(IEnumerable<AiJobRecord> jobs)
    {
        ArgumentNullException.ThrowIfNull(jobs);
        return jobs
            .Where(job => job.CreatedByKind == ActorKind.Staff)
            .Select(job => Guid.TryParse(job.CreatedBy, out var staffId) ? staffId : Guid.Empty)
            .Where(staffId => staffId != Guid.Empty)
            .Distinct();
    }
}
