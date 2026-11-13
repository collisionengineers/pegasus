using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Pegasus.Core;
using Pegasus.Core.Documents;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Web.Intake;

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
    IReadOnlyList<RegistrationReadingToolItem> Readings,
    bool CanRegisterImages,
    string? SuggestedRegistration,
    string CorrelationId);

/// <summary>
/// One registration reading of retained image material: what the plate
/// recognition read from one image and the staff decision on it.
/// </summary>
internal sealed record RegistrationReadingToolItem(
    Guid Id,
    Guid ReceiptId,
    string Outcome,
    string? SuggestedRegistration,
    double? Confidence,
    string Disposition,
    string? DispositionReason,
    DateTimeOffset OccurredAtUtc)
{
    public static RegistrationReadingToolItem From(ImageVrmSuggestion reading) => new(
        reading.Id,
        reading.IntakeReceiptId,
        reading.Outcome.ToString(),
        reading.SuggestedRegistration,
        reading.Confidence,
        reading.Disposition.ToString(),
        reading.DispositionReason,
        reading.OccurredAtUtc);
}

internal sealed record UnidentifiedActionToolResult(
    UnidentifiedToolItem Item,
    string Action,
    Guid? ImageIntakeId,
    string? ImageIntakeReference,
    RegistrationReadingToolItem? Reading,
    bool IsReplay,
    string OperationKey,
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
    ListUnidentifiedQueueByCursor listQueue,
    IResolveUnidentified resolve,
    ICloseUnidentified close,
    ReopenUnidentified reopen,
    IGetIntake getIntake,
    GetIntakeSourceMetadata getSourceMetadata,
    IDownloadIntakeSource downloadSource,
    IRenderImageForDelivery images,
    IExtractPdfPageText pdfText,
    IIntakeSubmissionGroupStore groupStore,
    GetUnidentifiedItemContext getContext,
    IVrmSuggestionStore vrmSuggestions,
    StaffIntakeActions intakeActions,
    IImageIntakeStore imageIntakes,
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
        return await auditor.CallAsync(
            "pegasus_unidentified_list",
            "unidentified",
            async context =>
            {
                UnidentifiedMediaKind? filter = string.IsNullOrWhiteSpace(mediaKind)
                    ? null
                    : AutomationMcpErrors.ParseName<UnidentifiedMediaKind>(
                        mediaKind,
                        "The Unidentified media filter is not recognized.");

                var effectiveLimit = CursorPaging.NormalizeLimit(limit);
                var page = await listQueue.ExecuteAsync(
                    new(context.Actor, filter, cursor, effectiveLimit), cancellationToken);
                return new UnidentifiedListToolResult(
                    page.Items.Select(MapQueue).ToArray(),
                    page.NextCursor,
                    effectiveLimit,
                    context.TraceIdentifier);
            },
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
    [Description("Gets one Unidentified item by exact U-reference, including safe detail, immutable history, the registration readings the record shows (each with the readingId pegasus_unidentified_action dismiss_reading takes) and whether Register images is offered, with the registration the readings agree on.")]
    public async Task<UnidentifiedToolDetail> GetAsync(
        [Description("Exact canonical U-reference, for example U17.")] string reference,
        CancellationToken cancellationToken = default)
    {
        return await auditor.CallAsync(
            "pegasus_unidentified_get",
            reference?.Trim() ?? "invalid",
            async context =>
            {
                var normalizedReference = RequireReference(reference);
                var item = await store.GetByReferenceAsync(normalizedReference, cancellationToken)
                    ?? throw new McpException("The Unidentified reference was not found.");
                var itemContext = await getContext.ExecuteAsync(context.Actor, item.Id, cancellationToken)
                    ?? throw new McpException("The Unidentified reference was not found.");
                return new UnidentifiedToolDetail(
                    Map(item),
                    await GetSourcesAsync(item, context.Actor, cancellationToken),
                    await store.HistoryAsync(item.Id, cancellationToken),
                    ShownReadings(itemContext).Select(RegistrationReadingToolItem.From).ToArray(),
                    itemContext.CanRegisterImages,
                    itemContext.SuggestedRegistration,
                    context.TraceIdentifier);
            },
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_unidentified_source_download",
        Title = "Download Unidentified source",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Returns the retained source of an exact U-reference as native content: an image as a JPEG image block re-encoded to fit maxInlineBytes, a PDF as its page text, a small text file as text; anything else as metadata only. Every result carries the file name, media type, size, SHA-256 and an authenticated contentUrl for the original bytes (same bearer token, Intake scope). A grouped origin requires the exact member receipt identifier returned by pegasus_unidentified_get.")]
    public async Task<CallToolResult> DownloadSourceAsync(
        [Description("Exact canonical U-reference, for example U17.")] string reference,
        [Description("For a grouped origin, the exact member receipt identifier from pegasus_unidentified_get.")] Guid? memberReceiptId = null,
        [Description("Byte budget for inline content; 0 selects 100 KiB, at most 10 MiB.")] int maxInlineBytes = 0,
        CancellationToken cancellationToken = default)
    {
        var normalizedReference = reference?.Trim() ?? "invalid";
        return await auditor.CallAsync(
            "pegasus_unidentified_source_download",
            normalizedReference,
            async context =>
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
                    images,
                    pdfText,
                    receiptId,
                    context.Actor,
                    maxInlineBytes,
                    context.TraceIdentifier,
                    cancellationToken);
            },
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
        var members = group.Members.OrderBy(member => member.Ordinal).ToList();
        var receipts = (await getIntake.ExecuteManyAsync(
                members.Select(member => member.StagedReceiptId).ToArray(), actor, cancellationToken))
            .ToDictionary(receipt => receipt.Id);
        var result = new List<UnidentifiedSourceToolItem>(members.Count);
        foreach (var member in members)
        {
            result.Add(MapSource(
                receipts.TryGetValue(member.StagedReceiptId, out var receipt)
                    ? receipt
                    : throw new McpException("A retained Unidentified group member was not found."),
                member.Ordinal));
        }
        return result;
    }

    /// <summary>
    /// The readings the Unidentified record shows: a group item's every member
    /// receipt's, any other item's own.
    /// </summary>
    private static IEnumerable<ImageVrmSuggestion> ShownReadings(UnidentifiedItemContext itemContext) =>
        itemContext.Item.Origin.Kind == UnidentifiedOriginKind.SubmissionGroup && itemContext.SubmissionGroup is not null
            ? itemContext.MemberReceipts.SelectMany(receipt => itemContext.MemberReadings[receipt.Id])
            : itemContext.RegistrationReadings;

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
    [Description("Resolves one open Unidentified item using the same versioned Core commands as Web: a destination (InstructionCase, ImageIntake, Triage or ExternalReference) or Close with reason, the item's one refusal, which names no destination and keeps the U-reference reachable under Closed. Requires a mcp:-prefixed operation key, reason and expected version. pegasus_unidentified_reopen withdraws either.")]
    public async Task<UnidentifiedToolItem> ResolveAsync(
        [Description("Exact canonical U-reference, for example U17.")] string reference,
        [Description("The item version the caller observed; a stale value fails closed.")] long expectedVersion,
        [Description("Why, for the item's history, at most 500 characters. For Closed it is the closure reason.")] string reason,
        [Description("Where the item goes: InstructionCase, ImageIntake or Triage (targetId is that record's id), ExternalReference (free-form targetId), or Closed (close with reason; no targetId).")] UnidentifiedResolutionTargetKind targetKind,
        [Description("Caller idempotency key prefixed 'mcp:'.")] string operationKey,
        [Description("The destination identifier for that kind; required for every kind except Closed, which takes none.")] string? targetId = null,
        [Description("Optional display reference for the destination; not taken by Closed.")] string? targetReference = null,
        CancellationToken cancellationToken = default)
    {
        return await auditor.CallAsync(
            "pegasus_unidentified_resolve",
            reference?.Trim() ?? "invalid",
            operationKey,
            async (context, key) =>
            {
                var normalizedReference = RequireReference(reference);
                var item = await store.GetByReferenceAsync(normalizedReference, cancellationToken)
                    ?? throw new McpException("The Unidentified reference was not found.");
                if (targetKind == UnidentifiedResolutionTargetKind.Closed)
                {
                    if (targetId is not null || targetReference is not null)
                    {
                        throw new McpException("Closed names no destination; omit targetId and targetReference.");
                    }

                    var closed = await close.ExecuteAsync(
                        new(item.Id, expectedVersion, context.Actor, key, reason, DateTimeOffset.UtcNow),
                        cancellationToken);
                    return Map(closed.Item);
                }

                var result = await resolve.ExecuteAsync(
                    new(
                        item.Id,
                        expectedVersion,
                        context.Actor,
                        key,
                        reason,
                        targetKind,
                        targetId ?? throw new McpException("targetId is required for a destination."),
                        targetReference,
                        DateTimeOffset.UtcNow),
                    cancellationToken);
                return Map(result.Item);
            },
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_unidentified_action",
        Title = "Act on Unidentified work",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("One staff act on an open Unidentified item's material, through the same Core commands as the Unidentified record. register_images is Register images: the item's images become an Image Intake record (staff label Vehicle images) under vehicleRegistration, with a reason; an upload group item registers its whole group as one record. It is refused where the record does not offer Register images (pegasus_unidentified_get canRegisterImages). dismiss_reading is Dismiss on a registration reading: readingId, from pegasus_unidentified_get readings, with a reason; a reading already decided is refused. Requires a mcp:-prefixed operation key; replaying it returns what it recorded.")]
    public async Task<UnidentifiedActionToolResult> ActAsync(
        [Description("Exact canonical U-reference, for example U17.")] string reference,
        [Description("register_images or dismiss_reading.")] string action,
        [Description("Why, for the record's history, at most 500 characters.")] string reason,
        [Description("Caller idempotency key prefixed 'mcp:'.")] string operationKey,
        [Description("register_images only: the vehicle registration the images are registered under.")] string? vehicleRegistration = null,
        [Description("dismiss_reading only: the reading identifier from pegasus_unidentified_get readings.")] Guid? readingId = null,
        CancellationToken cancellationToken = default)
    {
        return await auditor.CallAsync(
            "pegasus_unidentified_action",
            reference?.Trim() ?? "invalid",
            operationKey,
            async (context, key) =>
            {
                var normalizedReference = RequireReference(reference);
                var item = await store.GetByReferenceAsync(normalizedReference, cancellationToken)
                    ?? throw new McpException("The Unidentified reference was not found.");
                var itemContext = await getContext.ExecuteAsync(context.Actor, item.Id, cancellationToken)
                    ?? throw new McpException("The Unidentified reference was not found.");
                var correlation = AutomationMcpAuditor.CorrelationId(context, key);
                switch (action?.Trim())
                {
                    case "register_images":
                    {
                        if (readingId is not null)
                        {
                            throw new McpException("register_images takes vehicleRegistration and reason.");
                        }

                        var (record, isReplay) = await RegisterImagesAsync(
                            context.Actor, itemContext, vehicleRegistration, reason, key, cancellationToken);
                        return new UnidentifiedActionToolResult(
                            await ReloadAsync(item, cancellationToken),
                            "register_images",
                            record.Id,
                            record.ImageIntakeReference,
                            Reading: null,
                            isReplay,
                            key,
                            correlation);
                    }
                    case "dismiss_reading":
                    {
                        if (vehicleRegistration is not null)
                        {
                            throw new McpException("dismiss_reading takes readingId and reason.");
                        }

                        var reading = AutomationMcpErrors.RequireId(
                            readingId ?? throw new McpException("dismiss_reading needs readingId."),
                            "reading identifier");
                        itemContext.RequireReading(reading);
                        // A reading already decided is refused unless this key decided it.
                        var decided = itemContext.RegistrationReadings.Any(candidate =>
                            candidate.Id == reading && candidate.Disposition != ImageVrmSuggestionDisposition.Pending);
                        var dismissed = await vrmSuggestions.SetDispositionAsync(
                            new(reading, ImageVrmSuggestionDisposition.Dismissed, context.Actor, reason, key),
                            cancellationToken);
                        return new UnidentifiedActionToolResult(
                            await ReloadAsync(item, cancellationToken),
                            "dismiss_reading",
                            ImageIntakeId: null,
                            ImageIntakeReference: null,
                            RegistrationReadingToolItem.From(dismissed),
                            decided,
                            key,
                            correlation);
                    }
                    default:
                        throw new McpException("action must be register_images or dismiss_reading.");
                }
            },
            cancellationToken);
    }

    /// <summary>
    /// Register images on the item as its record runs it
    /// (<see cref="StaffIntakeActions.RegisterItemImagesAsync"/>): the offer is
    /// checked there, so an item whose images are registered is refused as on
    /// the record; only then is the refusal read as a replay of the same key,
    /// which returns the record it registered.
    /// </summary>
    private async Task<(ImageIntakeRecord Record, bool IsReplay)> RegisterImagesAsync(
        Pegasus.Core.Identity.ActionActor actor,
        UnidentifiedItemContext itemContext,
        string? vehicleRegistration,
        string reason,
        string key,
        CancellationToken cancellationToken)
    {
        var operationKey = IntakeActionMcpTools.RegistrationOperationKey(actor, key);
        try
        {
            return (await intakeActions.RegisterItemImagesAsync(
                    actor, itemContext, vehicleRegistration, reason, operationKey, cancellationToken),
                false);
        }
        catch (InvalidOperationException)
        {
            var replayed = await IntakeActionMcpTools.RegistrationReplayAsync(
                imageIntakes,
                itemContext.ImageIntake?.Record,
                vehicleRegistration,
                actor,
                operationKey,
                reason,
                itemContext.SubmissionGroup?.Id,
                cancellationToken);
            if (replayed is null)
            {
                throw;
            }

            return (replayed, true);
        }
    }

    private async Task<UnidentifiedToolItem> ReloadAsync(UnidentifiedItem item, CancellationToken cancellationToken) =>
        Map(await store.GetAsync(item.Id, cancellationToken) ?? item);

    [McpServerTool(
        Name = "pegasus_unidentified_reopen",
        Title = "Reopen Unidentified work",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Reopens one resolved or closed Unidentified item with a reason, as the item's Reopen does: the resolution is withdrawn, stays in the item's history, and the item returns to the open list. Requires a mcp:-prefixed operation key and the expected version.")]
    public async Task<UnidentifiedToolItem> ReopenAsync(
        [Description("Exact canonical U-reference, for example U17.")] string reference,
        [Description("The item version the caller observed; a stale value fails closed.")] long expectedVersion,
        [Description("Why the item is reopened, for its history, at most 500 characters.")] string reason,
        [Description("Caller idempotency key prefixed 'mcp:'.")] string operationKey,
        CancellationToken cancellationToken = default)
    {
        return await auditor.CallAsync(
            "pegasus_unidentified_reopen",
            reference?.Trim() ?? "invalid",
            operationKey,
            async (context, key) =>
            {
                var normalizedReference = RequireReference(reference);
                var item = await store.GetByReferenceAsync(normalizedReference, cancellationToken)
                    ?? throw new McpException("The Unidentified reference was not found.");
                var result = await reopen.ExecuteAsync(
                    new(item.Id, expectedVersion, context.Actor, key, reason, DateTimeOffset.UtcNow),
                    cancellationToken);
                return Map(result.Item);
            },
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
