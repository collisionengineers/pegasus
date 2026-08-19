using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Pegasus.Core.Intake.Unidentified;

namespace Pegasus.Web.Mcp;

internal sealed record UnidentifiedToolItem(
    Guid Id,
    string Reference,
    string OriginKind,
    Guid OriginId,
    string ReasonCode,
    string SafeDetail,
    string State,
    DateTimeOffset CreatedAtUtc,
    long Version);

internal sealed record UnidentifiedToolDetail(
    UnidentifiedToolItem Item,
    IReadOnlyList<UnidentifiedHistoryEntry> History,
    string CorrelationId);

[McpServerToolType]
internal sealed class UnidentifiedMcpTools(
    IUnidentifiedStore store,
    IResolveUnidentified resolve,
    AutomationActorResolver resolver,
    AutomationMcpAuditor auditor)
{
    [McpServerTool(
        Name = "pegasus_unidentified_list",
        Title = "List Unidentified work",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Lists open Unidentified items, or all items when state is supplied. Each item has an immutable U-reference, canonical reason, origin and safe detail.")]
    public async Task<IReadOnlyList<UnidentifiedToolItem>> ListAsync(
        [Description("Optional exact state: Open or Resolved.")] string? state = null,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.IntakeScope, cancellationToken);
        return await auditor.RecordAsync(
            context,
            "pegasus_unidentified_list",
            "unidentified",
            null,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                UnidentifiedState? filter = null;
                if (!string.IsNullOrWhiteSpace(state))
                {
                    if (!Enum.TryParse<UnidentifiedState>(state.Trim(), true, out var parsed))
                    {
                        throw new McpException("The Unidentified state is not recognized.");
                    }
                    filter = parsed;
                }

                var rows = await store.ListAsync(filter, cancellationToken);
                return rows.Select(Map).ToArray();
            }),
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_unidentified_get",
        Title = "Get Unidentified detail",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Gets one Unidentified item by exact U-reference, including safe detail and immutable history.")]
    public async Task<UnidentifiedToolDetail> GetAsync(
        [Description("Exact canonical U-reference, for example U17.")] string reference,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.IntakeScope, cancellationToken);
        return await auditor.RecordAsync(
            context,
            "pegasus_unidentified_get",
            reference?.Trim() ?? "invalid",
            null,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                var normalizedReference = reference?.Trim()
                    ?? throw new McpException("An exact U-reference is required.");
                var item = await store.GetByReferenceAsync(normalizedReference, cancellationToken)
                    ?? throw new McpException("The Unidentified reference was not found.");
                return new UnidentifiedToolDetail(
                    Map(item),
                    await store.HistoryAsync(item.Id, cancellationToken),
                    context.TraceIdentifier);
            }),
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_unidentified_resolve",
        Title = "Resolve Unidentified work",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Resolves one Unidentified item using the same versioned Core command as Web. Requires a mcp:-prefixed operation key, reason, target and expected version.")]
    public async Task<UnidentifiedToolItem> ResolveAsync(
        string reference,
        long expectedVersion,
        string reason,
        UnidentifiedResolutionTargetKind targetKind,
        string targetId,
        string? targetReference,
        string operationKey,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.IntakeScope, cancellationToken);
        var key = AutomationMcpErrors.RequireOperationKey(operationKey);
        return await auditor.RecordAsync(
            context,
            "pegasus_unidentified_resolve",
            reference?.Trim() ?? "invalid",
            key,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                var normalizedReference = reference?.Trim()
                    ?? throw new McpException("An exact U-reference is required.");
                var item = await store.GetByReferenceAsync(normalizedReference, cancellationToken)
                    ?? throw new McpException("The Unidentified reference was not found.");
                var result = await resolve.ExecuteAsync(
                    new(
                        item.Id,
                        expectedVersion,
                        context.Actor,
                        key,
                        reason,
                        targetKind,
                        targetId,
                        targetReference,
                        DateTimeOffset.UtcNow),
                    cancellationToken);
                return Map(result.Item);
            }),
            cancellationToken);
    }

    private static UnidentifiedToolItem Map(UnidentifiedItem item) => new(
        item.Id,
        item.Reference,
        item.Origin.Kind.ToString(),
        item.Origin.Id,
        item.ReasonCode.ToString(),
        item.SafeDetail,
        item.State.ToString(),
        item.CreatedAtUtc,
        item.Version);
}
