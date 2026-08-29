using Pegasus.Core.Operations;

namespace Pegasus.Web.Mcp;

/// <summary>
/// The Service health snapshot's read of the Automation client kill switch:
/// the same per-request check the token endpoint applies, for the same
/// single seeded client. Read-only; the Administrator action stays on
/// <see cref="AutomationClientRegistry"/>.
/// </summary>
internal sealed class AutomationIngressStatusQueries(
    AutomationClientRegistry registry,
    AutomationMcpOptions options) : IAutomationIngressStatusQueries
{
    public Task<bool> IsEnabledAsync(CancellationToken cancellationToken) =>
        registry.IsEnabledAsync(options.ClientId, cancellationToken);
}
