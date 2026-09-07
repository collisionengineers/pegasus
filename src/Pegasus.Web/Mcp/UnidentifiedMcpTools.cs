using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Pegasus.Core;
using Pegasus.Core.Intake;
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

internal sealed record UnidentifiedListToolResult(
    IReadOnlyList<UnidentifiedQueueToolItem> Items,
    string? NextCursor,
    int Limit,
    string CorrelationId);

internal sealed record UnidentifiedQueueToolItem(
    Guid Id,
    string Reference,
    string MediaKind,
    string? FileName,
    string? EmailSubject,
    string? EmailSender,
    DateTimeOffset ReceivedAtUtc,
    string ReasonCode);

internal sealed record UnidentifiedToolDetail(
    UnidentifiedToolItem Item,
    IReadOnlyList<UnidentifiedSourceToolItem> Sources,
    IReadOnlyList<UnidentifiedHistoryEntry> History,
    string CorrelationId);

internal sealed record UnidentifiedSourceToolItem(
    Guid ReceiptId,
    int? GroupOrdinal,
    string FileName,
    string MediaType,
    long ContentLength,
    string Sha256,
    string SourceChannel,
    string SourceIdentity);

[McpServerToolType]
internal sealed class UnidentifiedMcpTools(
    IUnidentifiedStore store,
    IListUnidentifiedQueueByCursor listQueue,
    IResolveUnidentified resolve,
    IGetIntake getIntake,
    IGetIntakeSourceMetadata getSourceMetadata,
    IDownloadIntakeSource downloadSource,
    IIntakeSubmissionGroupStore groupStore,
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
    [Description("Lists the open Unidentified queue. Each item has an immutable U-reference and canonical reason. Uses protected continuations; limit defaults to 50 and is at most 100.")]
    public async Task<UnidentifiedListToolResult> ListAsync(
        [Description("Optional media filter: Image, Email or Document.")] string? mediaKind = null,
        [Description("Opaque continuation returned by the preceding list call.")] string? cursor = null,
        [Description("Items to return; omit for the default 50, maximum 100.")] int? limit = null,
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
                UnidentifiedMediaKind? filter = null;
                if (!string.IsNullOrWhiteSpace(mediaKind))
                {
                    if (!Enum.TryParse<UnidentifiedMediaKind>(mediaKind.Trim(), ignoreCase: true, out var parsed)
                        || !Enum.IsDefined(parsed))
                    {
                        throw new McpException("The Unidentified media filter is not recognized.");
                    }
                    filter = parsed;
                }

                var effectiveLimit = CursorPaging.NormalizeLimit(limit);
                var page = await listQueue.ExecuteAsync(
                    new(context.Actor, filter, cursor, effectiveLimit), cancellationToken);
                return new UnidentifiedListToolResult(
                    page.Items.Select(MapQueue).ToArray(),
                    page.NextCursor,
                    effectiveLimit,
                    context.TraceIdentifier);
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
                var normalizedReference = RequireReference(reference);
                var item = await store.GetByReferenceAsync(normalizedReference, cancellationToken)
                    ?? throw new McpException("The Unidentified reference was not found.");
                return new UnidentifiedToolDetail(
                    Map(item),
                    await GetSourcesAsync(item, context.Actor, cancellationToken),
                    await store.HistoryAsync(item.Id, cancellationToken),
                    context.TraceIdentifier);
            }),
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_unidentified_source_download",
        Title = "Download Unidentified source",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Downloads retained source bytes for an exact U-reference. A grouped origin requires the exact member receipt identifier returned by pegasus_unidentified_get.")]
    public async Task<IntakeSourceToolResult> DownloadSourceAsync(
        string reference,
        Guid? memberReceiptId = null,
        int maxInlineBytes = 0,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.IntakeScope, cancellationToken);
        var normalizedReference = reference?.Trim() ?? "invalid";
        return await auditor.RecordAsync(
            context,
            "pegasus_unidentified_source_download",
            normalizedReference,
            null,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                normalizedReference = RequireReference(normalizedReference);
                var item = await store.GetByReferenceAsync(normalizedReference, cancellationToken)
                    ?? throw new McpException("The Unidentified reference was not found.");
                Guid receiptId;
                if (item.Origin.Kind == UnidentifiedOriginKind.Receipt)
                {
                    if (memberReceiptId is not null && memberReceiptId != item.Origin.Id)
                    {
                        throw new McpException("A receipt-origin U-reference does not accept a different member receipt identifier.");
                    }
                    receiptId = item.Origin.Id;
                }
                else
                {
                    receiptId = memberReceiptId
                        ?? throw new McpException("An exact member receipt identifier is required for a grouped U-reference.");
                    var group = await groupStore.GetAsync(item.Origin.Id, cancellationToken)
                        ?? throw new McpException("The retained Unidentified submission group was not found.");
                    if (!group.Members.Any(member => member.StagedReceiptId == receiptId))
                    {
                        throw new McpException("The receipt is not a member of this Unidentified submission group.");
                    }
                }

                return await IntakeSourceMcpContent.DownloadAsync(
                    getSourceMetadata,
                    downloadSource,
                    receiptId,
                    context.Actor,
                    maxInlineBytes,
                    context.TraceIdentifier,
                    cancellationToken);
            }),
            cancellationToken);
    }

    private async Task<IReadOnlyList<UnidentifiedSourceToolItem>> GetSourcesAsync(
        UnidentifiedItem item,
        Pegasus.Core.Identity.ActionActor actor,
        CancellationToken cancellationToken)
    {
        if (item.Origin.Kind == UnidentifiedOriginKind.Receipt)
        {
            var receipt = await getIntake.ExecuteAsync(new(item.Origin.Id, actor), cancellationToken)
                ?? throw new McpException("The retained Unidentified receipt was not found.");
            return [MapSource(receipt, null)];
        }

        var group = await groupStore.GetAsync(item.Origin.Id, cancellationToken)
            ?? throw new McpException("The retained Unidentified submission group was not found.");
        var members = await groupStore.ListMembersAsync(group.Id, cancellationToken);
        var result = new List<UnidentifiedSourceToolItem>(members.Count);
        foreach (var member in members.OrderBy(member => member.Ordinal))
        {
            var receipt = await getIntake.ExecuteAsync(new(member.StagedReceiptId, actor), cancellationToken)
                ?? throw new McpException("A retained Unidentified group member was not found.");
            result.Add(MapSource(receipt, member.Ordinal));
        }
        return result;
    }

    private static UnidentifiedSourceToolItem MapSource(IntakeReceipt receipt, int? ordinal) => new(
        receipt.Id,
        ordinal,
        receipt.SourceFileName,
        receipt.MediaType,
        receipt.SourceLength,
        receipt.SourceHash,
        receipt.SourceIdentity.Channel.ToString(),
        receipt.SourceIdentity.ExternalReceiptToken);

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
                var normalizedReference = RequireReference(reference);
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

    private static UnidentifiedQueueToolItem MapQueue(UnidentifiedQueueRow item) => new(
        item.Id,
        item.Reference,
        item.MediaKind.ToString(),
        item.FileName,
        item.EmailSubject,
        item.EmailSender,
        item.ReceivedAtUtc,
        item.ReasonCode.ToString());

    private static string RequireReference(string? reference)
    {
        var normalized = reference?.Trim();
        return UnidentifiedReferenceFormat.TryParse(normalized, out _)
            ? normalized!
            : throw new McpException("An exact canonical U-reference is required.");
    }
}
