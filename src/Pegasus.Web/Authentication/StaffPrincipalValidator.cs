using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Pegasus.Core.Actors;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.Web.Authentication;

/// <summary>
/// The staff cookie's check on every request. It loads the account once and
/// accepts the cookie only while its security stamp is the account's current
/// stamp, the session is inside its absolute lifetime and the account is
/// enabled. It never rebuilds the principal: every change to a staff
/// account's roles, claims, enabled state or password rotates the stamp, so a
/// cookie issued before such a change is refused on the next request instead
/// of being refreshed with the new authority.
/// </summary>
internal static class StaffPrincipalValidator
{
    public const string InvalidSecurityStamp = "invalid_security_stamp";
    public const string AbsoluteSessionExpired = "absolute_session_expired";
    public const string DisabledOrMissingStaff = "disabled_or_missing_staff";

    /// <summary>
    /// Null when the cookie is still valid; otherwise the refusal's reason
    /// code. The caller rejects the principal and records the refusal.
    /// </summary>
    public static async Task<string?> ValidateAsync(
        CookieValidatePrincipalContext context,
        string originalIssueClaim)
    {
        ArgumentNullException.ThrowIfNull(context);
        var principal = context.Principal;
        if (principal is null)
        {
            return InvalidSecurityStamp;
        }

        var services = context.HttpContext.RequestServices;
        var userManager = services.GetRequiredService<UserManager<PegasusIdentityUser>>();
        var stampClaimType = services.GetRequiredService<IOptions<IdentityOptions>>()
            .Value.ClaimsIdentity.SecurityStampClaimType;
        // The one account read. It is tracked on the request's context, so a
        // later lookup of the same account in this request does not repeat it.
        var userId = userManager.GetUserId(principal);
        var user = string.IsNullOrEmpty(userId) ? null : await userManager.FindByIdAsync(userId);
        if (user is null || !StampMatches(user.SecurityStamp, principal.FindFirstValue(stampClaimType)))
        {
            return InvalidSecurityStamp;
        }

        var nowSeconds = services.GetRequiredService<TimeProvider>().GetUtcNow().ToUnixTimeSeconds();
        if (!long.TryParse(
                principal.FindFirst(originalIssueClaim)?.Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var issuedSeconds)
            || issuedSeconds < 0
            || issuedSeconds > nowSeconds
            || nowSeconds - issuedSeconds >= (long)StaffSessionPolicy.AbsoluteLifetime.TotalSeconds)
        {
            return AbsoluteSessionExpired;
        }

        if (!user.IsEnabled)
        {
            return DisabledOrMissingStaff;
        }

        // Validation never reissues the cookie. The cookie handler's own
        // sliding-expiration refresh is separate and still applies; reissuing
        // here would make private, immutable document previews uncacheable.
        context.ShouldRenew = false;
        return null;
    }

    private static bool StampMatches(string? current, string? presented) =>
        !string.IsNullOrEmpty(current)
        && presented is not null
        && CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(current),
            Encoding.UTF8.GetBytes(presented));
}
