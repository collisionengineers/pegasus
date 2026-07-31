namespace Pegasus.Core.Actors;

/// <summary>
/// Owns the fixed staff-session and sign-in throttling contract shared by authenticated callers.
/// Transport middleware remains responsible for cookie protection and trusted-client address resolution.
/// </summary>
public static class StaffSessionPolicy
{
    public static readonly TimeSpan IdleLifetime = TimeSpan.FromHours(2);
    public static readonly TimeSpan AbsoluteLifetime = TimeSpan.FromHours(8);

    public const int SignInAttemptsPerClientPerMinute = 10;
    public const int SignInAttemptsGlobalPerMinute = 100;
}
