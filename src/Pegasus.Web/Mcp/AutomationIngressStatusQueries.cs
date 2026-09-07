using Pegasus.Core.Operations;

namespace Pegasus.Web.Mcp;

/// <summary>
/// The Service health snapshot's read of the Automation client kill switch:
/// the same per-request check the token endpoint applies, for the same
/// single seeded client. Read-only; the Administrator action stays on
/// <see cref="AutomationClientRegistry"/>.
/// </summary>
internal sealed class AutomationIngressStatusQueries(
    AutomationClientRegistry? registry = null,
    AutomationMcpOptions? options = null) : IAutomationIngressStatusQueries
{
    public Task<bool> IsEnabledAsync(CancellationToken cancellationToken) =>
        options is null
            ? Task.FromResult(false)
            : (registry ?? throw new InvalidOperationException("The configured Automation ingress has no client registry."))
                .IsEnabledAsync(options.ClientId, cancellationToken);
}
