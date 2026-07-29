using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Pegasus.Core.Documents;

namespace Pegasus.Web.Mcp;

internal sealed record DocumentContentResult(
    string FileName,
    string MediaType,
    long ContentLength,
    string Sha256,
    string ContentBase64);

internal sealed record DocumentExportResult(
    string FileName,
    IReadOnlyList<DocumentExportManifestEntry> Manifest,
    string ContentBase64);

[McpServerToolType]
internal sealed class DocumentMcpTools(
    IAddCaseDocument addDocument,
    IDownloadCaseDocument downloadDocument,
    IExportCaseDocuments exportDocuments,
    StaffMcpActorResolver actorResolver)
{
    private const int MaximumDocumentBytes = 10 * 1024 * 1024;
    private const int MaximumExportBytes = 20 * 1024 * 1024;
    private const int MaximumBase64Characters = ((MaximumDocumentBytes + 2) / 3) * 4;

    [McpServerTool(
        Name = "pegasus_document_upload",
        Title = "Upload case document",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Retains one staff-provided document in the canonical case custody boundary. Content is base64 and is limited to 10 MiB before decoding.")]
    public async Task<AddCaseDocumentResult> UploadAsync(
        [Description("The durable Pegasus case identifier.")] Guid caseId,
        [Description("The leaf file name; path components are rejected.")] string fileName,
        [Description("The document media type.")] string mediaType,
        [Description("The complete document content encoded as base64.")] string contentBase64,
        [Description("The document's exact semantic role.")] DocumentSemanticRole semanticRole,
        [Description("The case version observed by the caller, when enforcing a version boundary.")] long? expectedCaseVersion,
        [Description("A caller-generated idempotency identifier for this upload.")] Guid operationId,
        CancellationToken cancellationToken)
    {
        var staff = await actorResolver.RequireAsync(
            StaffMcpPolicies.WriteScope,
            cancellationToken);
        RequireNonEmpty(caseId, nameof(caseId));
        RequireNonEmpty(operationId, nameof(operationId));
        if (expectedCaseVersion < 0)
        {
            throw new McpException("The expected case version cannot be negative.");
        }
        if (!Enum.IsDefined(semanticRole))
        {
            throw new McpException("The document semantic role is not recognized.");
        }

        fileName = RequireText(fileName, nameof(fileName), 255);
        if (!Path.GetFileName(fileName).Equals(fileName, StringComparison.Ordinal)
            || fileName is "." or "..")
        {
            throw new McpException("The file name must not contain path components.");
        }
        mediaType = RequireText(mediaType, nameof(mediaType), 255);
        if (mediaType.Contains('\r') || mediaType.Contains('\n'))
        {
            throw new McpException("The media type is invalid.");
        }
        if (string.IsNullOrWhiteSpace(contentBase64)
            || contentBase64.Length > MaximumBase64Characters)
        {
            throw new McpException("The document content is required and must be 10 MiB or smaller.");
        }

        byte[] content;
        try
        {
            content = Convert.FromBase64String(contentBase64);
        }
        catch (FormatException)
        {
            throw new McpException("The document content is not valid base64.");
        }
        if (content.Length is 0 or > MaximumDocumentBytes)
        {
            throw new McpException("The document content must be between 1 byte and 10 MiB.");
        }

        return await addDocument.ExecuteAsync(
            new(
                caseId,
                fileName,
                mediaType,
                content,
                semanticRole,
                DocumentSource.StaffUpload,
                $"mcp-upload:{operationId:N}",
                $"staff:{staff.HistoryActor}",
                $"mcp:document-upload:{operationId:N}",
                expectedCaseVersion),
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_document_download",
        Title = "Download case document",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Downloads one exact case document occurrence/version from canonical custody. Content is returned as base64 and is limited to 10 MiB.")]
    public async Task<DocumentContentResult> DownloadAsync(
        [Description("The durable Pegasus case identifier.")] Guid caseId,
        [Description("The case-scoped document occurrence identifier.")] Guid occurrenceId,
        [Description("The exact immutable document-version identifier.")] Guid versionId,
        CancellationToken cancellationToken)
    {
        var staff = await actorResolver.RequireAsync(
            StaffMcpPolicies.ReadScope,
            cancellationToken);
        RequireNonEmpty(caseId, nameof(caseId));
        RequireNonEmpty(occurrenceId, nameof(occurrenceId));
        RequireNonEmpty(versionId, nameof(versionId));

        await using var download = await downloadDocument.ExecuteAsync(
            new(caseId, occurrenceId, versionId, $"staff:{staff.HistoryActor}"),
            cancellationToken)
            ?? throw new McpException("The requested document version was not found.");
        if (download.ContentLength is < 0 or > MaximumDocumentBytes)
        {
            throw new McpException("The document is larger than the MCP download limit.");
        }

        var contentBase64 = await ReadBase64Async(
            download.Content,
            MaximumDocumentBytes,
            cancellationToken);
        return new(
            download.FileName,
            download.MediaType,
            download.ContentLength,
            download.Sha256,
            contentBase64);
    }

    [McpServerTool(
        Name = "pegasus_document_export",
        Title = "Export case documents",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Creates a deterministic export from exact case document occurrence/version selections. The resulting archive is returned as base64 and limited to 20 MiB.")]
    public async Task<DocumentExportResult> ExportAsync(
        [Description("The durable Pegasus case identifier.")] Guid caseId,
        [Description("The exact occurrence/version pairs to export.")] IReadOnlyList<DocumentExportSelection> selections,
        [Description("A caller-generated idempotency identifier for this export.")] Guid operationId,
        CancellationToken cancellationToken)
    {
        var staff = await actorResolver.RequireAsync(
            StaffMcpPolicies.WriteScope,
            cancellationToken);
        RequireNonEmpty(caseId, nameof(caseId));
        RequireNonEmpty(operationId, nameof(operationId));
        if (selections is null
            || selections.Count == 0
            || selections.Count > 100
            || selections.Any(selection =>
                selection.OccurrenceId == Guid.Empty || selection.VersionId == Guid.Empty)
            || selections.Distinct().Count() != selections.Count)
        {
            throw new McpException(
                "Select between 1 and 100 unique, non-empty occurrence/version pairs.");
        }

        await using var export = await exportDocuments.ExecuteAsync(
            new(
                caseId,
                selections,
                $"staff:{staff.HistoryActor}",
                $"mcp:document-export:{operationId:N}"),
            cancellationToken);
        return new(
            export.FileName,
            export.Manifest,
            await ReadBase64Async(export.Content, MaximumExportBytes, cancellationToken));
    }

    private static async Task<string> ReadBase64Async(
        Stream source,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream(capacity: Math.Min(maximumBytes, 64 * 1024));
        var transfer = new byte[64 * 1024];
        while (true)
        {
            var read = await source.ReadAsync(transfer, cancellationToken);
            if (read == 0)
            {
                break;
            }
            if (buffer.Length + read > maximumBytes)
            {
                throw new McpException("The generated content is larger than the MCP transfer limit.");
            }

            buffer.Write(transfer, 0, read);
        }

        return Convert.ToBase64String(buffer.GetBuffer(), 0, checked((int)buffer.Length));
    }

    private static void RequireNonEmpty(Guid value, string name)
    {
        if (value == Guid.Empty)
        {
            throw new McpException($"'{name}' must be a non-empty identifier.");
        }
    }

    private static string RequireText(string? value, string name, int maximumLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > maximumLength)
        {
            throw new McpException(
                $"'{name}' is required and must be no longer than {maximumLength} characters.");
        }

        return normalized;
    }
}
