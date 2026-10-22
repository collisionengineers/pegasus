using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
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

/// <summary>
/// The structured half of a download: the file's identity, size and hash,
/// whether its content came back as native content blocks, and where the
/// original bytes are. Content never travels in this record.
/// </summary>
internal sealed record DocumentDownloadToolResult(
    Guid CaseId,
    Guid OccurrenceId,
    Guid VersionId,
    string FileName,
    string MediaType,
    long ContentLength,
    string Sha256,
    bool ContentIncluded,
    string ContentUrl,
    string? Notice,
    string OperationKey,
    string CorrelationId);

internal sealed record DocumentDownloadBatchItemInput(
    [property: Description("The case-scoped document occurrence identifier.")] Guid OccurrenceId,
    [property: Description("The exact immutable document-version identifier.")] Guid VersionId);

internal sealed record DocumentBatchDownloadToolResult(
    Guid CaseId,
    IReadOnlyList<DocumentDownloadToolResult> Documents,
    string OperationKey,
    string CorrelationId);

/// <summary>
/// Automation Actor document tools (MCP-04): thin adapters over the same
/// canonical case-document custody use cases as the staff app, guarded by the
/// automation.documents scope. Retained content is provenance-labelled with
/// the Automation document source; a mutation presents the case edit lease
/// and expected version like any staff save, or holds the lease for its one
/// command when none is presented. A download hands the content back as
/// native MCP content (<see cref="AutomationFileContent"/>) with the original
/// always reachable at the authenticated content URL.
/// </summary>
[McpServerToolType]
internal sealed class DocumentMcpTools(
    IAddCaseDocument addDocument,
    IGetCaseDocumentMetadata getDocumentMetadata,
    IDownloadCaseDocument downloadDocument,
    IOpenCaseDocumentBatch openBatch,
    IRenderImageForDelivery images,
    IExtractPdfPageText pdfText,
    AutomationEditLease leases,
    AutomationActorResolver resolver,
    AutomationMcpAuditor auditor)
{
    public const int MaximumBatchDocuments = 25;
    private const string SourceIdentityPrefix = "automation:";

    [McpServerTool(
        Name = "pegasus_document_add",
        Title = "Add case document",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Retains one document in the canonical case custody boundary, provenance-labelled as Automation-sourced. Content is base64 and is limited to 10 MiB before decoding. Needs the observed case version; present an edit lease token from pegasus_edit_begin for multi-step work, or omit it and the tool holds the lease for this one command. Replaying the same operation key with identical inputs returns the original custody record.")]
    public async Task<DocumentAddToolResult> AddAsync(
        [Description("The durable Pegasus case identifier.")] Guid caseId,
        [Description("The leaf file name; path components are rejected.")] string fileName,
        [Description("The document media type.")] string mediaType,
        [Description("The complete document content encoded as base64.")] string contentBase64,
        [Description("The document semantic role name: OriginalSource, Instruction, Image, Correspondence, EngineerReport, AuditReport, MarketResearch, or Other.")] string semanticRole,
        [Description("The case version observed by the caller; a stale value fails closed.")] long expectedCaseVersion,
        [Description("Caller idempotency key prefixed 'mcp:'.")] string operationKey,
        [Description("Edit lease token from pegasus_edit_begin for multi-step work, presented on every write until pegasus_edit_end; omit it and the tool holds the lease for this one command.")] string? editLeaseToken = null,
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
                var result = await leases.RunCaseAsync(
                    caseId,
                    expectedCaseVersion,
                    editLeaseToken,
                    context.Actor,
                    normalizedKey,
                    token => addDocument.ExecuteAsync(
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
                            token),
                        cancellationToken),
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
        OpenWorld = false)]
    [Description("Returns one exact case document version as native content the client can show: an image as one JPEG image block re-encoded to fit maxInlineBytes (longest edge 1568 px), a PDF as its page text, a small text file as its text. Any other type, or a file over 10 MiB, returns metadata only. Every result carries the file name, media type, size, SHA-256 and an authenticated contentUrl for the original bytes (same bearer token, Documents scope). Document identifiers come from pegasus_case_get.")]
    public async Task<CallToolResult> DownloadAsync(
        [Description("The durable Pegasus case identifier.")] Guid caseId,
        [Description("The case-scoped document occurrence identifier.")] Guid occurrenceId,
        [Description("The exact immutable document-version identifier.")] Guid versionId,
        [Description("Byte budget for inline content; 0 selects 100 KiB, at most 10 MiB. An image is re-encoded to fit it; PDF text is cut at this many characters; a text file must already fit it.")] int maxInlineBytes = 0,
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
                var inlineLimit = AutomationFileContent.NormalizeInlineLimit(maxInlineBytes);

                var metadata = await getDocumentMetadata.ExecuteAsync(
                    new(caseId, occurrenceId, versionId, context.Actor),
                    cancellationToken)
                    ?? throw new McpException("The document version was not found.");
                var contentUrl = ContentUrl(caseId, occurrenceId, versionId);
                var delivery = await DeliverMetadataAsync(
                    metadata,
                    context.Actor,
                    operationKey,
                    inlineLimit,
                    contentUrl,
                    cancellationToken);

                return AutomationFileContent.ToResult(
                    delivery,
                    new DocumentDownloadToolResult(
                        caseId,
                        occurrenceId,
                        versionId,
                        metadata.FileName,
                        metadata.MediaType,
                        metadata.ContentLength,
                        metadata.Sha256,
                        delivery.ContentIncluded,
                        contentUrl,
                        delivery.Notice,
                        operationKey,
                        AutomationMcpAuditor.CorrelationId(context, operationKey)));
            }),
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_document_download_batch",
        Title = "Download case documents",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Returns 1 to 25 exact case document versions in one call as native content the client can show. maxInlineBytes is charged at the encoded size of what is sent, items in request order, and the batch stops at the first item that does not fit whole: a whole file either fits or is withheld with its reason, and every later item returns metadata only. An image is re-encoded as JPEG up to 1568 px to fit what remains, a PDF comes back as its page text and a small text file as its text; any other type, or a file over 10 MiB, returns metadata only. Every item carries its file name, media type, size, SHA-256 and authenticated contentUrl in request order. Document identifiers come from pegasus_case_get.")]
    public async Task<CallToolResult> DownloadBatchAsync(
        [Description("The durable Pegasus case identifier.")] Guid caseId,
        [Description("The 1 to 25 exact document versions to download, each named once.")] IReadOnlyList<DocumentDownloadBatchItemInput> documents,
        [Description("Total byte budget for inline content across the batch, charged at the encoded size of what is sent (UTF-8 text, base64 image data) in request order; 0 selects 100 KiB, at most 10 MiB.")] int maxInlineBytes = 0,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.DocumentsScope, cancellationToken);
        var operationKey = $"mcp:document-download-batch:{Guid.NewGuid():N}";
        return await auditor.RecordAsync(
            context,
            "pegasus_document_download_batch",
            caseId == Guid.Empty ? "invalid" : caseId.ToString("D"),
            operationKey,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                AutomationMcpErrors.RequireId(caseId, "case identifier");
                if (documents is null || documents.Count is < 1 or > MaximumBatchDocuments)
                {
                    throw new McpException($"Between 1 and {MaximumBatchDocuments} documents are required.");
                }

                var seen = new HashSet<(Guid OccurrenceId, Guid VersionId)>(documents.Count);
                foreach (var item in documents)
                {
                    if (item is null)
                    {
                        throw new McpException($"Between 1 and {MaximumBatchDocuments} documents are required.");
                    }
                    AutomationMcpErrors.RequireId(item.OccurrenceId, "occurrence identifier");
                    AutomationMcpErrors.RequireId(item.VersionId, "version identifier");
                    if (!seen.Add((item.OccurrenceId, item.VersionId)))
                    {
                        throw new McpException("Each document version may be named once.");
                    }
                }

                var inlineLimit = AutomationFileContent.NormalizeInlineLimit(maxInlineBytes);
                // One read for the whole set, refused whole before any content
                // is read when any version is not the Case's confirmed current one.
                var batch = await openBatch.ExecuteAsync(
                    new(
                        caseId,
                        [.. documents.Select(item => new CaseDocumentBatchItem(item.OccurrenceId, item.VersionId))],
                        context.Actor,
                        operationKey),
                    cancellationToken)
                    ?? throw new McpException("The document version was not found.");
                var items = new AutomationFileItem[batch.Metadata.Count];
                for (var index = 0; index < items.Length; index++)
                {
                    var metadata = batch.Metadata[index];
                    items[index] = new AutomationFileItem(
                        metadata.FileName,
                        metadata.MediaType,
                        metadata.ContentLength,
                        metadata.Sha256,
                        ContentUrl(caseId, metadata.OccurrenceId, metadata.VersionId));
                }

                var deliveries = await AutomationFileContent.DeliverBatchAsync(
                    images,
                    pdfText,
                    items,
                    inlineLimit,
                    batch.ReadContentAsync,
                    cancellationToken);
                // A download row only for what is handed back, all in one save
                // after the walk, so a failure part-way through leaves none.
                await batch.RecordDownloadedAsync(
                    [.. Enumerable.Range(0, deliveries.Length).Where(index => deliveries[index].ContentIncluded)],
                    cancellationToken);

                var correlationId = AutomationMcpAuditor.CorrelationId(context, operationKey);
                var results = new DocumentDownloadToolResult[items.Length];
                for (var index = 0; index < items.Length; index++)
                {
                    var metadata = batch.Metadata[index];
                    var item = items[index];
                    var delivery = deliveries[index];
                    results[index] = new DocumentDownloadToolResult(
                        caseId,
                        metadata.OccurrenceId,
                        metadata.VersionId,
                        item.FileName,
                        item.MediaType,
                        item.ContentLength,
                        item.Sha256,
                        delivery.ContentIncluded,
                        item.ContentUrl,
                        delivery.Notice,
                        batch.ItemOperationKeys[index],
                        correlationId);
                }

                return AutomationFileContent.ToResult(
                    deliveries.SelectMany(delivery => delivery.Blocks),
                    new DocumentBatchDownloadToolResult(
                        caseId,
                        results,
                        operationKey,
                        correlationId));
            }),
            cancellationToken);
    }

    private static string ContentUrl(Guid caseId, Guid occurrenceId, Guid versionId) =>
        $"/automation/documents/{occurrenceId:D}/versions/{versionId:D}?caseId={caseId:D}";

    private async Task<AutomationFileDelivery> DeliverMetadataAsync(
        CaseDocumentMetadata metadata,
        Pegasus.Core.Identity.ActionActor actor,
        string operationKey,
        int inlineLimit,
        string contentUrl,
        CancellationToken cancellationToken)
    {
        if (!AutomationFileContent.CanDeliverInline(metadata.MediaType, metadata.ContentLength, inlineLimit))
        {
            return AutomationFileContent.Withheld(
                metadata.FileName,
                metadata.MediaType,
                metadata.ContentLength,
                metadata.Sha256,
                AutomationFileContent.InlineLimitNotice(metadata.ContentLength, metadata.MediaType, inlineLimit),
                contentUrl);
        }

        await using var download = await downloadDocument.ExecuteAsync(
            new(metadata.CaseId, metadata.OccurrenceId, metadata.VersionId, actor, operationKey),
            cancellationToken)
            ?? throw new McpException("The document version was not found.");
        using var buffer = new MemoryStream(
            (int)Math.Min(download.ContentLength, AutomationMcpErrors.MaximumDocumentBytes));
        await download.Content.CopyToAsync(buffer, cancellationToken);
        return await AutomationFileContent.DeliverAsync(
            images,
            pdfText,
            download.FileName,
            download.MediaType,
            download.ContentLength,
            download.Sha256,
            buffer.GetBuffer().AsMemory(0, (int)buffer.Length),
            inlineLimit,
            contentUrl,
            cancellationToken);
    }
}
