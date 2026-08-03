using System.Security.Claims;
using ModelContextProtocol;
using OpenIddict.Abstractions;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Mcp;

internal sealed record AutomationActorContext(
    ActionActor Actor,
    string ClientId,
    string TraceIdentifier);

/// <summary>
/// Resolves the authenticated Automation client principal into the Core
/// Automation actor before any tool touches a use case. Fails closed on a
/// missing principal, a disabled registration (the immediate kill switch),
/// or a token that lacks the tool's per-area scope, writing an attributable
/// security event for every material denial.
/// </summary>
internal sealed class AutomationActorResolver(
    IHttpContextAccessor httpContextAccessor,
    AutomationClientRegistry registry,
    ISecurityEventWriter securityEvents,
    TimeProvider timeProvider)
{
    public async Task<AutomationActorContext> RequireAsync(
        string requiredScope,
        CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new McpException("The automation request context is unavailable.");
        var principal = httpContext.User;
        var clientId = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.GetClaim(OpenIddictConstants.Claims.Subject);
        if (principal.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(clientId))
        {
            await DenyAsync(
                httpContext,
                SecurityEventType.Token,
                "anonymous",
                "automation_token_rejected",
                cancellationToken);
            throw new McpException("The automation authorization is not valid.");
        }

        if (!await registry.IsEnabledAsync(clientId, cancellationToken))
        {
            await DenyAsync(
                httpContext,
                SecurityEventType.Client,
                clientId,
                "automation_client_disabled",
                cancellationToken);
            throw new McpException("The Automation client registration is disabled.");
        }

        if (!principal.HasScope(requiredScope))
        {
            await DenyAsync(
                httpContext,
                SecurityEventType.Token,
                clientId,
                "automation_scope_denied",
                cancellationToken);
            throw new McpException(
                $"The '{requiredScope}' scope is required for this tool.");
        }

        return new(
            ActionActor.Automation(clientId),
            clientId,
            httpContext.TraceIdentifier);
    }

    private Task DenyAsync(
        HttpContext httpContext,
        SecurityEventType type,
        string subjectId,
        string reasonCode,
        CancellationToken cancellationToken) =>
        securityEvents.AppendAsync(
            new SecurityEvent(
                Guid.NewGuid(),
                type,
                SecurityEventOutcome.Denied,
                subjectId,
                timeProvider.GetUtcNow(),
                httpContext.TraceIdentifier,
                reasonCode),
            cancellationToken);
}

/// <summary>
/// Writes one permanent action-history entry per MCP tool invocation,
/// attributed to the Automation actor with the operation's correlation
/// identifier: the idempotency operation key when the tool carries one,
/// otherwise the request trace identifier. Business history keeps being
/// written by the Core use cases themselves; this is the ingress attribution
/// record that the Admin activity view consolidates.
/// </summary>
internal sealed class AutomationMcpAuditor(
    IActionHistoryWriter actionHistory,
    TimeProvider timeProvider)
{
    private const string AggregateType = "automation_mcp";

    public async Task<TResult> RecordAsync<TResult>(
        AutomationActorContext context,
        string toolName,
        string aggregateId,
        string? operationKey,
        Func<Task<TResult>> action,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
        ArgumentNullException.ThrowIfNull(action);
        try
        {
            var result = await action();
            await AppendAsync(
                context,
                toolName,
                aggregateId,
                operationKey,
                "Succeeded",
                reason: null,
                cancellationToken);
            return result;
        }
        catch (Exception exception)
        {
            await AppendAsync(
                context,
                toolName,
                aggregateId,
                operationKey,
                "Failed",
                Reason(exception),
                cancellationToken);
            throw;
        }
    }

    public static string CorrelationId(AutomationActorContext context, string? operationKey) =>
        NormalizedOperationKey(operationKey) ?? context.TraceIdentifier;

    private Task AppendAsync(
        AutomationActorContext context,
        string toolName,
        string aggregateId,
        string? operationKey,
        string outcome,
        string? reason,
        CancellationToken cancellationToken) =>
        actionHistory.AppendAsync(
            new ActionHistoryEntry(
                Guid.NewGuid(),
                AggregateType,
                Truncate(aggregateId, 200),
                toolName,
                context.Actor,
                timeProvider.GetUtcNow(),
                outcome,
                CorrelationId(context, operationKey),
                reason),
            cancellationToken);

    private static string? NormalizedOperationKey(string? operationKey)
    {
        var normalized = operationKey?.Trim();
        return normalized is { Length: > 0 and <= 100 } ? normalized : null;
    }

    private static string Reason(Exception exception)
    {
        var reason = $"{exception.GetType().Name}: {exception.Message}";
        return Truncate(reason, 1000);
    }

    private static string Truncate(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : value[..maximumLength];
}
