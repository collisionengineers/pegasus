namespace Pegasus.Core.Actors;

/// <summary>
/// Owns the fixed staff-session and sign-in throttling contract shared by authenticated callers.
/// Transport middleware remains responsible for cookie protection and trusted-client address resolution.
/// </summary>
public static class StaffSessionPolicy
{
    public static readonly TimeSpan IdleLifetime = TimeSpan.FromHours(2);
    public static readonly TimeSpan AbsoluteLifetime = TimeSpan.FromHours(8);

    // The longest a change made outside Pegasus (direct SQL, the intake wipe) takes
    // to reach a signed-in session; changes made in Pegasus apply on the next request.
    public static readonly TimeSpan AccountReadLifetime = TimeSpan.FromSeconds(300);

    public const int SignInAttemptsPerClientPerMinute = 10;
    public const int SignInAttemptsGlobalPerMinute = 100;
}
