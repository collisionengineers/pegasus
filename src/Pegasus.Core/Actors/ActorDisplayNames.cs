using Pegasus.Core.Identity;

namespace Pegasus.Core.Actors;

/// <summary>
/// The single place a persisted <see cref="ActionActor"/> (kind + subject id) becomes
/// an operator-facing name. Business records keep only the raw kind and subject —
/// see <see cref="StaffActorFactory"/> for the reverse direction — so every read
/// model that shows "who did this" resolves through here rather than printing the
/// subject id (a raw GUID for a staff actor) directly.
/// </summary>
public static class ActorDisplayNames
{
    public const string UnknownStaff = "Unknown user";
    public const string SystemWorker = "System";
    public const string Automation = "Automation";
    public const string RequestLink = "Request link";

    /// <summary>
    /// Resolves the distinct staff subject ids referenced by a set of actors into
    /// their current username, in one query per distinct account. An id that no
    /// longer resolves (a deleted account) is simply absent from the result; callers
    /// fall back to <see cref="UnknownStaff"/> rather than inventing a name.
    /// </summary>
    public static async Task<IReadOnlyDictionary<Guid, string>> ResolveStaffNamesAsync(
        IStaffAccountQueries staffAccounts,
        IEnumerable<Guid> staffIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(staffAccounts);
        ArgumentNullException.ThrowIfNull(staffIds);

        var names = new Dictionary<Guid, string>();
        foreach (var staffId in staffIds.Where(id => id != Guid.Empty).Distinct())
        {
            var account = await staffAccounts.GetAsync(staffId, cancellationToken);
            if (account is not null)
            {
                names[staffId] = account.UserName;
            }
        }

        return names;
    }

    /// <summary>
    /// The display name for one actor. A staff subject that parses and resolves
    /// shows its username; every other case (a non-staff actor kind, or a staff
    /// subject that no longer resolves) shows an honest, never-a-GUID label.
    /// </summary>
    public static string Resolve(
        ActorKind kind,
        string subjectId,
        IReadOnlyDictionary<Guid, string> staffNames)
    {
        ArgumentNullException.ThrowIfNull(staffNames);
        return kind switch
        {
            ActorKind.Staff => Guid.TryParse(subjectId, out var staffId)
                && staffNames.TryGetValue(staffId, out var name)
                    ? name
                    : UnknownStaff,
            ActorKind.SystemWorker => SystemWorker,
            ActorKind.Automation => Automation,
            ActorKind.RequestLink => RequestLink,
            _ => UnknownStaff
        };
    }
}
