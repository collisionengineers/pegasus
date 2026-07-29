using System.Diagnostics.CodeAnalysis;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Actors;

public static class StaffActorFactory
{
    public static bool TryCreate(
        string? subjectId,
        IEnumerable<string> roleNames,
        [NotNullWhen(true)] out ActionActor? actor)
    {
        ArgumentNullException.ThrowIfNull(roleNames);
        actor = null;
        if (!Guid.TryParse(subjectId, out var staffId) || staffId == Guid.Empty)
        {
            return false;
        }

        var roles = new HashSet<StaffRole>();
        foreach (var roleName in roleNames)
        {
            if (!Enum.TryParse<StaffRole>(roleName, ignoreCase: false, out var role)
                || !Enum.IsDefined(role))
            {
                return false;
            }

            roles.Add(role);
        }

        if (roles.Count == 0)
        {
            return false;
        }

        actor = ActionActor.Staff(staffId, roles);
        return true;
    }
}
