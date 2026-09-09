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

        StaffRole? assignedRole = null;
        foreach (var roleName in roleNames)
        {
            if (!Enum.TryParse<StaffRole>(roleName, ignoreCase: false, out var role)
                || !Enum.IsDefined(role))
            {
                return false;
            }

            if (assignedRole is not null)
            {
                return false;
            }

            assignedRole = role;
        }

        if (assignedRole is null)
        {
            return false;
        }

        actor = ActionActor.Staff(staffId, [assignedRole.Value]);
        return true;
    }
}
