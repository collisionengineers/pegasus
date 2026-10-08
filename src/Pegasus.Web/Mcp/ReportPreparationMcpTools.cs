using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Mcp;

internal sealed record ReportWordingToolBlock(
    string Key,
    string Title,
    string Text,
    int Order,
    bool Included,
    bool Manual,
    bool HandEdited,
    string? ComposedText,
    string? StandardTitle);

internal sealed record ReportWordingToolResult(
    Guid CaseId,
    string Work,
    long CaseVersion,
    bool Available,
    IReadOnlyList<ReportWordingToolBlock> Blocks,
    IReadOnlyList<CodeMeaningItem> StandardBlocks,
    string? OperationKey,
    string CorrelationId);

internal sealed record ReportWordingBlockToolInput(
    [property: Description("The block's key: a standard block's key (nature, comments, supplementary, commentary, unrelated, history, condition, settlement, salvage) or a paragraph the Engineer wrote, keyed 'manual:' followed by a name of your choosing.")] string Key,
    [property: Description("The heading, at most 80 characters and one line. Omit to keep it; an empty string puts a standard block's own heading back.")] string? Title = null,
    [property: Description("The wording, at most 4000 characters. Omit to keep it; an empty string puts a standard block back to the sentence composed from the Case's fields, and deletes a 'manual:' paragraph. Wording equal to the composed sentence is no change.")] string? Text = null,
    [property: Description("The block's place among the blocks, 0 or more; lower prints first. Omit to keep it.")] int? Order = null,
    [property: Description("false takes the block off the report, true puts it back. Omit to keep it.")] bool? Included = null);

internal sealed record ImageCropToolInput(
    [property: Description("Left edge, a fraction 0 to 1 of the rotated image's width, at most 7 decimal places.")] decimal Left,
    [property: Description("Top edge, a fraction 0 to 1 of the rotated image's height, at most 7 decimal places.")] decimal Top,
    [property: Description("Width, a fraction above 0 and at most 1; Left plus Width is at most 1.")] decimal Width,
    [property: Description("Height, a fraction above 0 and at most 1; Top plus Height is at most 1.")] decimal Height);

internal sealed record ImagePreparationToolInput(
    [property: Description("The image's case document occurrence (occurrenceId from pegasus_image_preparation_get).")] Guid OccurrenceId,
    [property: Description("The image's preparationVersion from pegasus_image_preparation_get; a stale value fails closed.")] long ExpectedPreparationVersion,
    [property: Description("Its place among the Supporting images of the report, 1 or more; only for an image in the report. Omit to keep it.")] int? Order = null,
    [property: Description("Clockwise rotation in degrees: 0, 90, 180 or 270. Omit to keep it.")] int? Rotation = null,
    [property: Description("The crop, as fractions of the rotated image; Left 0, Top 0, Width 1, Height 1 is the whole image. Omit to keep it.")] ImageCropToolInput? Crop = null,
    [property: Description("true prints the image on a page of its own; only for an image in the report. Omit to keep it.")] bool? FullPage = null);

internal sealed record ImagePreparationToolItem(
    Guid OccurrenceId,
    Guid VersionId,
    string FileName,
    string ContentType,
    bool CanPrint,
    bool InReport,
    string? ReportRole,
    int? ReportPlace,
    int? Order,
    int Rotation,
    ImageCropToolInput Crop,
    bool FullPage,
    long PreparationVersion,
    IReadOnlyList<Guid> TagIds,
    string? PreparedByKind,
    string? PreparedBy,
    DateTimeOffset? PreparedAtUtc);

internal sealed record ImagePreparationToolResult(
    Guid CaseId,
    long CaseVersion,
    IReadOnlyList<ImagePreparationToolItem> Images,
    string? OperationKey,
    string CorrelationId);

/// <summary>
/// How the report prints, for the Automation Actor (FRD-10, ADR-0064): the
/// Report section's wording blocks (v28 P30) and each image's preparation
/// (crop, rotation, order and page of its own), read as the Case page reads
/// them and written through the same Case save (<see cref="ISaveCaseWorkspace"/>)
/// with the same lease, version and operation-key guards. The wording is
/// composed by <see cref="ReportWordingEdits"/>, the rule the Case page's Save
/// uses, so wording equal to the composed sentence is no change.
/// </summary>
[McpServerToolType]
internal sealed class ReportPreparationMcpTools(
    IGetCaseHeader getCaseHeader,
    ICaseReportSnapshotSource reportSnapshotSource,
    ICaseAssetPreparationQueries preparations,
    ISaveCaseWorkspace saveCaseWorkspace,
    TimeProvider timeProvider,
    AutomationActorResolver resolver,
    AutomationMcpAuditor auditor,
    AutomationEditLease leases)
{
    private const string WorkDescription =
        "current (the default; the Audit once a Case has one) or inspection (the Inspection report of a Case that has its Audit).";

    private static readonly CodeMeaningItem[] StandardBlocks =
    [
        .. ReportWordingComposition.Standard.Select(block => new CodeMeaningItem(block.Key, block.Title)),
    ];

    [McpServerTool(
        Name = "pegasus_report_wording_get",
        Title = "Get report wording",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Reads the report's wording blocks for one work as the Report section offers them, in print order: each block's key, heading, wording, place, whether it is on the report, whether it is a paragraph the Engineer wrote (manual) or wording written in place of the composed sentence (handEdited), and, for a standard block, the sentence composed from the Case's fields and its standard heading. A block with nothing to say and no change is not offered. available is false while the Case's report cannot yet be projected, and then there are no blocks. Also lists the standard block keys and headings.")]
    public async Task<ReportWordingToolResult> GetWordingAsync(
        [Description("The durable Pegasus case identifier.")] Guid caseId,
        [Description(WorkDescription)] string? work = null,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.DocumentsScope, cancellationToken);
        return await auditor.RecordAsync(
            context,
            "pegasus_report_wording_get",
            Resource(caseId),
            operationKey: null,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                AutomationMcpErrors.RequireId(caseId, "case identifier");
                var selector = ReportMcpTools.ParseWork(work);
                var version = await CaseVersionAsync(caseId, context.Actor, cancellationToken);
                return await WordingResultAsync(
                    caseId, selector, version, context.Actor, null, context.TraceIdentifier, cancellationToken);
            }),
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_report_wording_save",
        Title = "Save report wording",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Changes the report's wording blocks for one work through the Case save, as the Report section's Save does: rename a heading, write wording in place of the composed sentence or put a block back to it, move a block, take it off the report or put it back, and add or delete a paragraph of your own (key 'manual:' plus a name). Each named block changes only in what it names; every other block keeps its heading, wording, place and presence. Wording or a heading equal to the composed one is no change, so the block keeps tracking its fields. Read pegasus_report_wording_get first. Refused while the Case's report cannot yet be projected. Needs the expected case version (present an edit lease token for multi-step work, or omit it and the tool holds the lease for this one command).")]
    public async Task<ReportWordingToolResult> SaveWordingAsync(
        [Description("The durable Pegasus case identifier.")] Guid caseId,
        [Description("The case version the caller observed; a stale value fails closed.")] long expectedVersion,
        [Description("Caller idempotency key prefixed 'mcp:'.")] string operationKey,
        [Description("The blocks to change, each named by its key once.")] IReadOnlyList<ReportWordingBlockToolInput> blocks,
        [Description(WorkDescription)] string? work = null,
        [Description("Why the wording is being changed (case history reason).")] string? reason = null,
        [Description("Edit lease token from pegasus_edit_begin for multi-step work, presented on every write until pegasus_edit_end; omit it and the tool holds the lease for this one command.")] string? editLeaseToken = null,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.DocumentsScope, cancellationToken);
        var key = AutomationMcpErrors.RequireOperationKey(operationKey);
        return await auditor.RecordAsync(
            context,
            "pegasus_report_wording_save",
            Resource(caseId),
            key,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                AutomationMcpErrors.RequireId(caseId, "case identifier");
                var selector = ReportMcpTools.ParseWork(work);
                var changes = RequireWordingChanges(blocks);
                var saved = await leases.RunCaseAsync(
                    caseId,
                    expectedVersion,
                    editLeaseToken,
                    context.Actor,
                    key,
                    async token =>
                    {
                        var state = await ReportWordingEdits.ReadAsync(
                            reportSnapshotSource, caseId, context.Actor, selector, timeProvider.GetUtcNow(),
                            cancellationToken);
                        return await saveCaseWorkspace.ExecuteAsync(
                            new(caseId, expectedVersion, context.Actor, key, reason, token)
                            {
                                Work = selector,
                                ReportWording = new(Merge(state, changes)),
                            },
                            cancellationToken);
                    },
                    cancellationToken);
                return await WordingResultAsync(
                    caseId, selector, saved.Version, context.Actor, key,
                    AutomationMcpAuditor.CorrelationId(context, key), cancellationToken);
            }),
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_image_preparation_get",
        Title = "Get image preparation",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Reads how each of a Case's images prints in the report, as the Case page's Files section shows it: whether it is in the report and can print, its role (Overview, CloseUp or Supporting, decided by its tags) and place in the report, its staff order, rotation, crop, whether it prints on a page of its own, its tags, and the preparationVersion pegasus_image_prepare needs.")]
    public async Task<ImagePreparationToolResult> GetImagePreparationAsync(
        [Description("The durable Pegasus case identifier.")] Guid caseId,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.DocumentsScope, cancellationToken);
        return await auditor.RecordAsync(
            context,
            "pegasus_image_preparation_get",
            Resource(caseId),
            operationKey: null,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                AutomationMcpErrors.RequireId(caseId, "case identifier");
                var version = await CaseVersionAsync(caseId, context.Actor, cancellationToken);
                return new ImagePreparationToolResult(
                    caseId,
                    version,
                    Map(await preparations.ListForCaseAsync(caseId, cancellationToken)),
                    null,
                    context.TraceIdentifier);
            }),
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_image_prepare",
        Title = "Prepare report images",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Prepares images for the report through the Case save, as the Case page's image editor does: crop, rotate, set an image's place among the Supporting images, and print it on a page of its own. Each image names its preparationVersion from pegasus_image_preparation_get; a value it omits keeps what is recorded. Order and full page are only for an image in the report; whether an image is in the report is pegasus_document_action set_in_report, and its Overview or Close-up role follows its tags. The images in the report are renumbered from 1. Needs the expected case version (present an edit lease token for multi-step work, or omit it and the tool holds the lease for this one command).")]
    public async Task<ImagePreparationToolResult> PrepareImagesAsync(
        [Description("The durable Pegasus case identifier.")] Guid caseId,
        [Description("The case version the caller observed; a stale value fails closed.")] long expectedVersion,
        [Description("Caller idempotency key prefixed 'mcp:'.")] string operationKey,
        [Description("The images to prepare, each named once.")] IReadOnlyList<ImagePreparationToolInput> images,
        [Description("Why the images are being prepared (case history reason).")] string? reason = null,
        [Description("Edit lease token from pegasus_edit_begin for multi-step work, presented on every write until pegasus_edit_end; omit it and the tool holds the lease for this one command.")] string? editLeaseToken = null,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.DocumentsScope, cancellationToken);
        var key = AutomationMcpErrors.RequireOperationKey(operationKey);
        return await auditor.RecordAsync(
            context,
            "pegasus_image_prepare",
            Resource(caseId),
            key,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                AutomationMcpErrors.RequireId(caseId, "case identifier");
                if (images is null || images.Count == 0)
                {
                    throw new McpException("Name at least one image to prepare.");
                }
                if (images.Select(image => image.OccurrenceId).Distinct().Count() != images.Count)
                {
                    throw new McpException("Each image may be named once.");
                }
                // Every input is checked before the lease is claimed, so a
                // malformed call claims nothing.
                var current = (await preparations.ListForCaseAsync(caseId, cancellationToken))
                    .ToDictionary(item => item.OccurrenceId);
                var edits = images.Select(image => Edit(image, current)).ToArray();
                var saved = await leases.RunCaseAsync(
                    caseId,
                    expectedVersion,
                    editLeaseToken,
                    context.Actor,
                    key,
                    token => saveCaseWorkspace.ExecuteAsync(
                        new(caseId, expectedVersion, context.Actor, key, reason, token)
                        {
                            ImagePreparation = new(edits),
                        },
                        cancellationToken),
                    cancellationToken);
                return new ImagePreparationToolResult(
                    caseId,
                    saved.Version,
                    Map(await preparations.ListForCaseAsync(caseId, cancellationToken)),
                    key,
                    AutomationMcpAuditor.CorrelationId(context, key));
            }),
            cancellationToken);
    }

    private async Task<long> CaseVersionAsync(Guid caseId, ActionActor actor, CancellationToken cancellationToken) =>
        (await getCaseHeader.ExecuteAsync(new(caseId, actor), cancellationToken)
            ?? throw new McpException("The case was not found.")).Workflow.Version;

    private async Task<ReportWordingToolResult> WordingResultAsync(
        Guid caseId,
        CaseWorkSelector selector,
        long caseVersion,
        ActionActor actor,
        string? operationKey,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var inputs = await reportSnapshotSource.GetAsync(caseId, actor, selector, reuse: null, cancellationToken);
        AssessmentReportSnapshot? snapshot = null;
        IReadOnlyList<ReportWordingBlock> offered = [];
        if (inputs is not null)
        {
            (snapshot, offered) = ReportWordingEdits.Offered(inputs.Projection, timeProvider.GetUtcNow());
        }
        var presentation = snapshot?.Presentation();
        return new ReportWordingToolResult(
            caseId,
            selector == CaseWorkSelector.Primary ? "inspection" : "current",
            caseVersion,
            snapshot is not null,
            [
                .. offered.Select(block => new ReportWordingToolBlock(
                    block.Key,
                    block.Title,
                    block.Text,
                    block.Order,
                    block.Included,
                    block.Manual,
                    block.HandEdited,
                    block.Manual || snapshot is null || presentation is null
                        ? null
                        : ReportWordingComposition.ComposedText(block.Key, snapshot, presentation),
                    block.Manual || presentation is null
                        ? null
                        : ReportWordingComposition.StandardTitle(block.Key, presentation))),
            ],
            StandardBlocks,
            operationKey,
            correlationId);
    }

    /// <summary>
    /// The caller's changes, each a known key named once, checked before any
    /// lease is claimed.
    /// </summary>
    private static ReportWordingBlockToolInput[] RequireWordingChanges(
        IReadOnlyList<ReportWordingBlockToolInput>? blocks)
    {
        if (blocks is null || blocks.Count == 0)
        {
            throw new McpException("Name at least one wording block to change.");
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var changes = new ReportWordingBlockToolInput[blocks.Count];
        for (var index = 0; index < blocks.Count; index++)
        {
            var block = blocks[index] ?? throw new McpException("A wording block is empty.");
            var key = block.Key?.Trim() ?? string.Empty;
            var manual = key.StartsWith(ReportWordingComposition.ManualKeyPrefix, StringComparison.Ordinal);
            if (manual ? key.Length <= ReportWordingComposition.ManualKeyPrefix.Length
                : ReportWordingComposition.StandardIndex(key) < 0)
            {
                throw new McpException(
                    $"'{key}' is not a report wording block: name a standard block's key or 'manual:' with a name.");
            }
            if (!seen.Add(key))
            {
                throw new McpException($"The wording block '{key}' is named more than once.");
            }
            changes[index] = block with { Key = key };
        }
        return changes;
    }

    /// <summary>
    /// Every block the section offers, as its Save posts them, with the
    /// caller's changes laid over the ones it names. A paragraph of the
    /// caller's own left with no wording is deleted, as the section deletes
    /// one; each block is then stored by the Case page's own rule.
    /// </summary>
    private static List<CaseReportWording> Merge(
        ReportWordingState state,
        ReportWordingBlockToolInput[] changes)
    {
        var entries = state.Offered
            .Select(block => new WordingEntry(block.Key, block.Title, block.Text, block.Order, block.Included, block.Manual))
            .ToList();
        foreach (var change in changes)
        {
            var manual = change.Key.StartsWith(ReportWordingComposition.ManualKeyPrefix, StringComparison.Ordinal);
            var index = entries.FindIndex(entry => string.Equals(entry.Key, change.Key, StringComparison.Ordinal));
            var entry = index >= 0
                ? entries[index]
                : manual && string.IsNullOrWhiteSpace(change.Text)
                    ? throw new McpException($"The new paragraph '{change.Key}' needs its text.")
                    : new WordingEntry(
                        change.Key,
                        null,
                        null,
                        manual ? null : ReportWordingComposition.StandardIndex(change.Key),
                        true,
                        manual);
            entry = entry with
            {
                Title = change.Title is null ? entry.Title : Cleared(change.Title),
                Text = change.Text is null ? entry.Text : Cleared(change.Text),
                Order = change.Order ?? entry.Order,
                Included = change.Included ?? entry.Included,
            };
            if (index >= 0)
            {
                entries[index] = entry;
            }
            else
            {
                entries.Add(entry);
            }
        }

        return
        [
            .. entries
                .Where(entry => !entry.Manual || !string.IsNullOrWhiteSpace(entry.Text))
                .Select(entry => ReportWordingEdits.ToRecord(
                    entry.Key, entry.Title, entry.Text, entry.Order, entry.Included, entry.Manual,
                    state.Snapshot, state.Presentation)),
        ];
    }

    private static string? Cleared(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static CaseAssetPreparationEdit Edit(
        ImagePreparationToolInput image,
        Dictionary<Guid, CaseAssetPreparation> current)
    {
        ArgumentNullException.ThrowIfNull(image);
        var recorded = current.GetValueOrDefault(AutomationMcpErrors.RequireId(image.OccurrenceId, "image occurrence identifier"))
            ?? throw new McpException($"'{image.OccurrenceId:D}' is not an image of this Case.");
        var rotation = image.Rotation is { } degrees
            ? Enum.IsDefined((CaseAssetRotation)degrees)
                ? (CaseAssetRotation)degrees
                : throw new McpException("Rotation must be 0, 90, 180 or 270.")
            : recorded.Rotation;
        return new(
            recorded.OccurrenceId,
            image.ExpectedPreparationVersion,
            image.Order ?? recorded.Order,
            rotation,
            image.Crop is { } crop ? new(crop.Left, crop.Top, crop.Width, crop.Height) : recorded.Crop,
            image.FullPage ?? recorded.FullPage);
    }

    private static ImagePreparationToolItem[] Map(IReadOnlyList<CaseAssetPreparation> items)
    {
        var placed = CaseAssetPreparationPolicy.ForReport(items).ToDictionary(image => image.OccurrenceId);
        return
        [
            .. items.Select(item =>
            {
                var place = placed.GetValueOrDefault(item.OccurrenceId);
                // The store stamps "Kind:SubjectId"; the result names the
                // kind and the subject apart, as the other tools' actors do.
                var preparedBy = item.PreparedBy?.Split(':', 2);
                return new ImagePreparationToolItem(
                    item.OccurrenceId,
                    item.VersionId,
                    item.SourceFileName,
                    item.SourceContentType,
                    item.CanPrint,
                    item.InReport,
                    place?.Role.ToString(),
                    place?.Order,
                    item.Order,
                    (int)item.Rotation,
                    new(item.Crop.Left, item.Crop.Top, item.Crop.Width, item.Crop.Height),
                    item.FullPage,
                    item.PreparationVersion,
                    item.TagIds,
                    preparedBy is [var kind, _] ? kind : null,
                    preparedBy is [_, var subjectId] ? subjectId : item.PreparedBy,
                    item.PreparedAtUtc);
            }),
        ];
    }

    private static string Resource(Guid id) => id == Guid.Empty ? "invalid" : id.ToString("D");

    private sealed record WordingEntry(string Key, string? Title, string? Text, int? Order, bool Included, bool Manual);
}
