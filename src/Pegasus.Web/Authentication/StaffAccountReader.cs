using Microsoft.AspNetCore.Identity;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.Web.Authentication;

/// <summary>
/// The one read of a staff account's authority: the staff cookie makes it on
/// every request (<see cref="StaffPrincipalValidator"/>), and a Pegasus
/// Desktop token when it is issued or renewed (ADR-0092). It is kept by
/// <see cref="StaffAccountReads"/> until a write to the account evicts it, so
/// both see a disable, a role change, a password reset and a forced sign-out
/// on the next request.
/// </summary>
internal static class StaffAccountReader
{
    /// <summary>
    /// The kept read of <paramref name="staffId"/>, or a new one made with the
    /// request's own <see cref="UserManager{TUser}"/>; null when the id names
    /// no account.
    /// </summary>
    public static Task<StaffAccountRead?> ReadAsync(
        IServiceProvider services,
        Guid staffId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.GetRequiredService<StaffAccountReads>().GetOrReadAsync(
            staffId,
            async _ =>
            {
                // The request's own UserManager: a page that asks for the
                // signed-in user afterwards is handed the row read here,
                // without a second read of it.
                var userManager = services.GetRequiredService<UserManager<PegasusIdentityUser>>();
                var user = await userManager.FindByIdAsync(staffId.ToString());
                if (user is null)
                {
                    return null;
                }

                // The check never throws on an account it cannot place: one
                // without exactly one known role reads as holding none.
                var roleNames = await userManager.GetRolesAsync(user);
                StaffRole? role = roleNames is [var roleName]
                    && Enum.TryParse<StaffRole>(roleName, ignoreCase: false, out var parsed)
                    && Enum.IsDefined(parsed)
                        ? parsed
                        : null;
                return new StaffAccountRead(user.SecurityStamp, user.IsEnabled, user.MustChangePassword, role);
            },
            cancellationToken);
    }
}
