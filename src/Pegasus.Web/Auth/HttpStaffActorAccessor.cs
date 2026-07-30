using System.Security.Claims;
using Pegasus.Core.Access;

namespace Pegasus.Web.Auth;

public sealed class HttpStaffActorAccessor(IHttpContextAccessor httpContextAccessor) : IStaffActorAccessor
{
    public StaffActor? Current
    {
        get
        {
            var principal = httpContextAccessor.HttpContext?.User;
            if (principal?.Identity?.IsAuthenticated != true) return null;
            var idValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(idValue, out var id)) return null;
            var role = principal.FindFirstValue(ClaimTypes.Role) switch
            {
                nameof(StaffRole.Administrator) => StaffRole.Administrator,
                nameof(StaffRole.Engineer) => StaffRole.Engineer,
                _ => StaffRole.User
            };
            return new StaffActor(id, principal.Identity?.Name ?? string.Empty,
                principal.FindFirstValue("display_name") ?? principal.Identity?.Name ?? string.Empty, role);
        }
    }
}
