using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Microsoft.AspNetCore.DataProtection;
using Pegasus.Core.Documents;

namespace Pegasus.Web.Mcp;

internal sealed record DocumentAddToolResult(
    Guid OccurrenceId,
    Guid VersionId,
    Guid DocumentId,
    int Version,
    string FileName,
    string Sha256,
    long ContentLength,
    string SourceOccurrenceIdentity,
    bool IsReplay,
    string OperationKey,
    string CorrelationId);

internal sealed record DocumentDownloadToolResult(
    Guid CaseId,
    Guid OccurrenceId,
    Guid VersionId,
    string FileName,
    string MediaType,
    long ContentLength,
    string Sha256,
    bool ContentIncluded,
    string? ContentBase64,
    string? ContentUrl,
    string? Notice,
    string OperationKey,
    string CorrelationId);

internal sealed record DocumentExportSelectionInput(
    Guid OccurrenceId,
    Guid VersionId);

internal sealed record DocumentExportManifestToolItem(
    string FileName,
    Guid OccurrenceId,
    Guid VersionId,
    string SemanticRole,
    long ContentLength,
    string Sha256);

internal sealed record DocumentExportToolResult(
    Guid CaseId,
    string FileName,
    IReadOnlyList<DocumentExportManifestToolItem> Manifest,
    long ArchiveLength,
    bool ContentIncluded,
    string? ContentBase64,
    string? ContentUrl,
    string? Notice,
    string OperationKey,
    string CorrelationId);

/// <summary>
/// Automation Actor document tools (MCP-04): thin adapters over the same
/// canonical case-document custody use cases as the staff app, guarded by the
/// automation.documents scope. Retained content is provenance-labelled with
/// the Automation document source; mutations present the case edit lease and
/// expected version like any staff save. Inline content respects client
/// result-size limits: oversized content returns a bounded manifest and the
/// retrieval identifiers instead of overflowing silently.
/// </summary>
[McpServerToolType]
internal sealed class DocumentMcpTools(
    IAddCaseDocument addDocument,
    IGetCaseDocumentMetadata getDocumentMetadata,
    IDownloadCaseDocument downloadDocument,
    IExportCaseDocuments exportDocuments,
    AutomationActorResolver resolver,
    AutomationMcpAuditor auditor,
    IDataProtectionProvider dataProtectionProvider,
    TimeProvider timeProvider)
{
    private const long MaximumExportArchiveBytes = 20 * 1024 * 1024;
    private const int DefaultInlineContentBytes = 64 * 1024;
    private const string SourceIdentityPrefix = "automation:";

    [McpServerTool(
        Name = "pegasus_document_add",
        Title = "Add case document",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Retains one document in the canonical case custody boundary, provenance-labelled as Automation-sourced. Content is base64 and is limited to 10 MiB before decoding. Requires the case edit lease from pegasus_case_edit_begin and the observed case version; replaying the same operation key with identical inputs returns the original custody record.")]
    public async Task<DocumentAddToolResult> AddAsync(
        [Description("The durable Pegasus case identifier.")] Guid caseId,
        [Description("The leaf file name; path components are rejected.")] string fileName,
        [Description("The document media type.")] string mediaType,
        [Description("The complete document content encoded as base64.")] string contentBase64,
        [Description("The document semantic role name: OriginalSource, Instruction, Image, Correspondence, EngineerReport, AuditReport, or Other.")] string semanticRole,
        [Description("The case version observed by the caller; a stale value fails closed.")] long expectedCaseVersion,
        [Description("The active edit lease token from pegasus_case_edit_begin.")] string editLeaseToken,
        [Description("Caller idempotency key prefixed 'mcp:'.")] string operationKey,
        [Description("Optional durable source-occurrence identity prefixed 'automation:'; reusing an identity records a new version of the same document. Defaults to one derived from the operation key.")] string? sourceOccurrenceIdentity = null,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.DocumentsScope, cancellationToken);
        var normalizedKey = AutomationMcpErrors.RequireOperationKey(operationKey);
        return await auditor.RecordAsync(
            context,
            "pegasus_document_add",
            caseId == Guid.Empty ? "invalid" : caseId.ToString("D"),
            normalizedKey,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                AutomationMcpErrors.RequireId(caseId, "case identifier");
                var safeFileName = AutomationMcpErrors.RequireFileName(fileName);
                var safeMediaType = AutomationMcpErrors.RequireMediaType(mediaType);
                if (!Enum.TryParse<DocumentSemanticRole>(
                        semanticRole?.Trim(),
                        ignoreCase: true,
                        out var parsedRole)
                    || !Enum.IsDefined(parsedRole))
                {
                    throw new McpException("The document semantic role is not recognized.");
                }
                if (string.IsNullOrWhiteSpace(editLeaseToken))
                {
                    throw new McpException("An active edit lease token is required.");
                }
                if (expectedCaseVersion < 0)
                {
                    throw new McpException("The expected case version cannot be negative.");
                }

                var identity = sourceOccurrenceIdentity?.Trim();
                if (string.IsNullOrEmpty(identity))
                {
                    identity = SourceIdentityPrefix + normalizedKey;
                }
                else if (!identity.StartsWith(SourceIdentityPrefix, StringComparison.Ordinal)
                    || identity.Length is <= 11 or > 512)
                {
                    throw new McpException(
                        "The source occurrence identity must start with 'automation:' and be at most 512 characters.");
                }

                var content = AutomationMcpErrors.DecodeContent(
                    contentBase64,
                    AutomationMcpErrors.MaximumDocumentBytes,
                    "The document content");
                var result = await addDocument.ExecuteAsync(
                    new(
                        caseId,
                        safeFileName,
                        safeMediaType,
                        content,
                        parsedRole,
                        DocumentSource.Automation,
                        identity,
                        context.Actor,
                        normalizedKey,
                        expectedCaseVersion,
                        editLeaseToken),
                    cancellationToken);
                return new DocumentAddToolResult(
                    result.Occurrence.Id,
                    result.Version.Id,
                    result.Occurrence.DocumentId,
                    result.Version.Version,
                    result.Version.FileName,
                    result.Version.Sha256,
                    result.Version.ContentLength,
                    result.Occurrence.SourceOccurrenceIdentity,
                    result.IsReplay,
                    normalizedKey,
                    AutomationMcpAuditor.CorrelationId(context, normalizedKey));
            }),
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_document_download",
        Title = "Download case document",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Downloads one exact case document occurrence/version from canonical custody as base64. Content larger than the inline limit returns metadata plus the SHA-256 instead; raise maxInlineBytes (up to 10 MiB) only when the client can accept a larger tool result.")]
    public async Task<DocumentDownloadToolResult> DownloadAsync(
        [Description("The durable Pegasus case identifier.")] Guid caseId,
        [Description("The case-scoped document occurrence identifier.")] Guid occurrenceId,
        [Description("The exact immutable document-version identifier.")] Guid versionId,
        [Description("Largest content size returned inline, in bytes; 0 selects the default of 65536.")] int maxInlineBytes = 0,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.DocumentsScope, cancellationToken);
        var operationKey = $"mcp:document-download:{Guid.NewGuid():N}";
        return await auditor.RecordAsync(
            context,
            "pegasus_document_download",
            caseId == Guid.Empty ? "invalid" : caseId.ToString("D"),
            operationKey,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                AutomationMcpErrors.RequireId(caseId, "case identifier");
                AutomationMcpErrors.RequireId(occurrenceId, "occurrence identifier");
                AutomationMcpErrors.RequireId(versionId, "version identifier");
                var inlineLimit = maxInlineBytes == 0
                    ? DefaultInlineContentBytes
                    : maxInlineBytes;
                if (inlineLimit is < 1 or > AutomationMcpErrors.MaximumDocumentBytes)
                {
                    throw new McpException(
                        $"maxInlineBytes must be between 1 and {AutomationMcpErrors.MaximumDocumentBytes}.");
                }

                var metadata = await getDocumentMetadata.ExecuteAsync(
                    new(caseId, occurrenceId, versionId, context.Actor),
                    cancellationToken)
                    ?? throw new McpException("The document version was not found.");
                if (metadata.ContentLength > inlineLimit)
                {
                    return new DocumentDownloadToolResult(
                        caseId, occurrenceId, versionId, metadata.FileName,
                        metadata.MediaType, metadata.ContentLength, metadata.Sha256,
                        ContentIncluded: false, ContentBase64: null,
                        ContentUrl: $"/automation/documents/{occurrenceId:D}/versions/{versionId:D}?caseId={caseId:D}",
                        Notice: $"The content ({metadata.ContentLength} bytes) exceeds the inline limit of {inlineLimit} bytes; use contentUrl with this bearer token.", operationKey,
                        AutomationMcpAuditor.CorrelationId(context, operationKey));
                }

                await using var download = await downloadDocument.ExecuteAsync(
                    new(caseId, occurrenceId, versionId, context.Actor, operationKey),
                    cancellationToken)
                    ?? throw new McpException("The document version was not found.");
                using var buffer = new MemoryStream();
                await download.Content.CopyToAsync(buffer, cancellationToken);
                return new DocumentDownloadToolResult(
                    caseId,
                    occurrenceId,
                    versionId,
                    download.FileName,
                    download.MediaType,
                    download.ContentLength,
                    download.Sha256,
                    ContentIncluded: true,
                    Convert.ToBase64String(buffer.ToArray()),
                    ContentUrl: null,
                    Notice: null,
                    operationKey,
                    AutomationMcpAuditor.CorrelationId(context, operationKey));
            }),
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_document_export",
        Title = "Export case documents",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Exports selected case document versions as one deterministic zip archive with a manifest, through the same lease-guarded Core export command as the staff app. Small archives may be returned inline; larger archives return a short-lived authenticated streaming URL.")]
    public async Task<DocumentExportToolResult> ExportAsync(
        [Description("The durable Pegasus case identifier.")] Guid caseId,
        [Description("The exact occurrence/version pairs to export; at most 32 may be selected per archive.")] IReadOnlyList<DocumentExportSelectionInput> selections,
        [Description("The case version observed by the caller; a stale value fails closed.")] long expectedCaseVersion,
        [Description("The active edit lease token from pegasus_case_edit_begin.")] string editLeaseToken,
        [Description("Caller idempotency key prefixed 'mcp:'.")] string operationKey,
        [Description("Largest archive size returned inline, in bytes; 0 selects the default of 65536.")] int maxInlineBytes = 0,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.DocumentsScope, cancellationToken);
        var normalizedKey = AutomationMcpErrors.RequireOperationKey(operationKey);
        return await auditor.RecordAsync(
            context,
            "pegasus_document_export",
            caseId == Guid.Empty ? "invalid" : caseId.ToString("D"),
            normalizedKey,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                AutomationMcpErrors.RequireId(caseId, "case identifier");
                if (selections is not { Count: > 0 })
                {
                    throw new McpException("At least one occurrence/version selection is required.");
                }
                if (selections.Count > AutomationDocumentStreaming.MaximumExportSelections)
                {
                    throw new McpException(
                        $"At most {AutomationDocumentStreaming.MaximumExportSelections} document versions may be exported at once.");
                }
                if (string.IsNullOrWhiteSpace(editLeaseToken))
                {
                    throw new McpException("An active edit lease token is required.");
                }
                if (expectedCaseVersion < 0)
                {
                    throw new McpException("The expected case version cannot be negative.");
                }

                var inlineLimit = maxInlineBytes == 0
                    ? DefaultInlineContentBytes
                    : maxInlineBytes;
                if (inlineLimit is < 1 or > AutomationMcpErrors.MaximumDocumentBytes)
                {
                    throw new McpException(
                        $"maxInlineBytes must be between 1 and {AutomationMcpErrors.MaximumDocumentBytes}.");
                }

                await using var export = await exportDocuments.ExecuteAsync(
                    new(
                        caseId,
                        selections
                            .Select(selection => new DocumentExportSelection(
                                AutomationMcpErrors.RequireId(
                                    selection.OccurrenceId,
                                    "occurrence identifier"),
                                AutomationMcpErrors.RequireId(
                                    selection.VersionId,
                                    "version identifier")))
                            .ToArray(),
                        context.Actor,
                        normalizedKey,
                        MaximumExportArchiveBytes,
                        expectedCaseVersion,
                        editLeaseToken),
                    cancellationToken);
                var manifest = export.Manifest
                    .Select(entry => new DocumentExportManifestToolItem(
                        entry.FileName,
                        entry.OccurrenceId,
                        entry.VersionId,
                        entry.SemanticRole.ToString(),
                        entry.ContentLength,
                        entry.Sha256))
                    .ToArray();

                var archiveLength = export.Content.CanSeek
                    ? export.Content.Length
                    : throw new InvalidDataException("The deterministic export stream does not expose its bounded length.");
                string? inlineContent = null;
                string? contentUrl = null;
                if (archiveLength <= inlineLimit)
                {
                    using var buffer = new MemoryStream((int)archiveLength);
                    await export.Content.CopyToAsync(buffer, cancellationToken);
                    inlineContent = Convert.ToBase64String(buffer.GetBuffer(), 0, (int)buffer.Length);
                }
                else
                {
                    var ticket = AutomationDocumentStreaming.ProtectExport(
                        dataProtectionProvider,
                        new(
                            caseId,
                            selections.Select(value => new DocumentExportSelection(
                                value.OccurrenceId, value.VersionId)).ToArray(),
                            expectedCaseVersion,
                            editLeaseToken,
                            normalizedKey,
                            context.GrantId,
                            timeProvider.GetUtcNow().AddMinutes(5)));
                    contentUrl = "/automation/document-exports"
                        + QueryString.Create("ticket", ticket).ToUriComponent();
                }
                var included = inlineContent is not null;
                return new DocumentExportToolResult(
                    caseId,
                    export.FileName,
                    manifest,
                    archiveLength,
                    included,
                    inlineContent,
                    contentUrl,
                    included
                        ? null
                        : $"The archive ({archiveLength} bytes) exceeds the inline limit of {inlineLimit} bytes; use contentUrl with this bearer token.",
                    normalizedKey,
                    AutomationMcpAuditor.CorrelationId(context, normalizedKey));
            }),
            cancellationToken);
    }
}
