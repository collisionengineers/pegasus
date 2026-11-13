namespace Pegasus.Core.Identity;

/// <summary>
/// Shared conventions binding the automation ingress writers to the
/// activity reader: ingress security events carry reason codes with this
/// prefix so denials remain queryable without a parallel store.
/// </summary>
public static class AutomationActivityConventions
{
    public const string SecurityEventReasonPrefix = "automation_";
}

/// <summary>
/// Read port over the permanent action-history and security-event stores,
/// restricted to Automation actor attribution and automation ingress denials.
/// </summary>
public interface IAutomationActivityQueries
{
    /// <summary>
    /// When the newest Automation activity record was written, or
    /// <see langword="null"/> when there is none.
    /// </summary>
    Task<DateTimeOffset?> GetNewestActivityAtUtcAsync(CancellationToken cancellationToken);
}
