using System.ComponentModel;
using ModelContextProtocol.Server;
using Pegasus.Core.Documents;

namespace Pegasus.Web.Mcp;

internal sealed record DocumentContentResult(
    string FileName,
    string MediaType,
    long ContentLength,
    string Sha256,
    string ContentBase64);

internal sealed record DocumentExportManifestMcpEntry(
    string FileName,
    Guid OccurrenceId,
    Guid VersionId,
    DocumentSemanticRole SemanticRole,
    long ContentLength,
    string Sha256);

internal sealed record DocumentExportResult(
    string FileName,
    IReadOnlyList<DocumentExportManifestMcpEntry> Manifest,
    string ContentBase64);

internal static class DocumentMcpContent
{
    public const int MaximumDocumentBytes = 10 * 1024 * 1024;
    public const int MaximumExportBytes = 20 * 1024 * 1024;
    private static readonly char[] InvalidFileNameCharacters = Path.GetInvalidFileNameChars();


    public static string SanitizeFileName(string value)
    {
        var fileName = Path.GetFileName(
            StaffMcpInput.RequireText(value, "fileName", 1_024));
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "document.bin";
        }

        var length = Math.Min(fileName.Length, 200);
        var characters = fileName[..length].ToCharArray();
        for (var index = 0; index < characters.Length; index++)
        {
            if (char.IsControl(characters[index])
                || Array.IndexOf(InvalidFileNameCharacters, characters[index]) >= 0)
            {
                characters[index] = '_';
            }
        }
        return new string(characters);
    }

    public static async Task<string> ReadBase64Async(
        Stream source,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        await using var buffer = new MemoryStream(capacity: Math.Min(maximumBytes, 64 * 1024));
        var chunk = new byte[64 * 1024];
        while (true)
        {
            var read = await source.ReadAsync(chunk, cancellationToken);
            if (read == 0)
            {
                break;
            }
            if (buffer.Length + read > maximumBytes)
            {
                throw new ModelContextProtocol.McpException(
                    "The requested document exceeds the MCP transfer limit.");
            }
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        return Convert.ToBase64String(buffer.GetBuffer(), 0, checked((int)buffer.Length));
    }
}

[McpServerToolType]
internal sealed class DocumentsLogicalRemoveMcpTool(
    ILogicallyRemoveDocument logicallyRemoveDocument,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.DocumentsLogicalRemove,
        Title = "Logically remove document",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Logically removes one case-scoped document occurrence while retaining immutable custody evidence.")]
    public Task<StaffMcpResult<StaffMcpMutationReceipt>> ExecuteAsync(
        Guid caseId,
        Guid occurrenceId,
        long expectedCaseVersion,
        string editLeaseToken,
        string operationKey,
        string reason,
        CancellationToken cancellationToken) =>
        StaffMcpCall.ExecuteAsync(async () =>
        {
            StaffMcpInput.RequireIdentifier(caseId, nameof(caseId));
            StaffMcpInput.RequireIdentifier(occurrenceId, nameof(occurrenceId));
            StaffMcpInput.RequireVersion(expectedCaseVersion, nameof(expectedCaseVersion));
            editLeaseToken = StaffMcpInput.RequireLease(editLeaseToken);
            operationKey = StaffMcpInput.RequireOperationKey(operationKey);
            reason = StaffMcpInput.RequireReason(reason);
            var staff = await actorResolver.RequireAsync(
                StaffMcpPolicies.WriteScope,
                cancellationToken);
            await logicallyRemoveDocument.ExecuteAsync(
                new(
                    caseId,
                    occurrenceId,
                    staff.Actor,
                    reason,
                    operationKey,
                    expectedCaseVersion,
                    editLeaseToken),
                cancellationToken);
        });
}

[McpServerToolType]
internal sealed class DocumentsDownloadMcpTool(
    IDownloadCaseDocument downloadDocument,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.DocumentsDownload,
        Title = "Download case document",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true,
        UseStructuredContent = true)]
    [Description("Downloads one exact case-scoped occurrence/version without exposing custody coordinates.")]
    public async Task<StaffMcpResult<DocumentContentResult>> ExecuteAsync(
        Guid caseId,
        Guid occurrenceId,
        Guid versionId,
        string operationKey,
        CancellationToken cancellationToken)
    {
        StaffMcpInput.RequireIdentifier(caseId, nameof(caseId));
        StaffMcpInput.RequireIdentifier(occurrenceId, nameof(occurrenceId));
        StaffMcpInput.RequireIdentifier(versionId, nameof(versionId));
        operationKey = StaffMcpInput.RequireOperationKey(operationKey);
        var result = await StaffMcpCall.ExecuteAsync<DocumentContentResult?>(async () =>
        {
            var staff = await actorResolver.RequireAsync(
                StaffMcpPolicies.ReadScope,
                cancellationToken);
            var download = await downloadDocument.ExecuteAsync(
                new(caseId, occurrenceId, versionId, staff.Actor, operationKey),
                cancellationToken);
            if (download is null)
            {
                return null;
            }
            await using (download)
            {
                if (download.ContentLength is < 0 or > DocumentMcpContent.MaximumDocumentBytes)
                {
                    throw new ModelContextProtocol.McpException(
                        "The requested document exceeds the MCP transfer limit.");
                }
                return new DocumentContentResult(
                    DocumentMcpContent.SanitizeFileName(download.FileName),
                    download.MediaType,
                    download.ContentLength,
                    download.Sha256,
                    await DocumentMcpContent.ReadBase64Async(
                        download.Content,
                        DocumentMcpContent.MaximumDocumentBytes,
                        cancellationToken));
            }
        });
        if (result.Outcome != StaffMcpCallOutcome.Succeeded)
        {
            return new(result.Outcome, null, result.ErrorCode, result.CurrentVersion);
        }
        return result.Value is { } content
            ? StaffMcpResult<DocumentContentResult>.Succeeded(content)
            : StaffMcpResult<DocumentContentResult>.NotFound();
    }
}

[McpServerToolType]
internal sealed class DocumentsExportMcpTool(
    IExportCaseDocuments exportDocuments,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.DocumentsExport,
        Title = "Export case documents",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true,
        UseStructuredContent = true)]
    [Description("Creates a deterministic recorded export from exact same-case occurrence/version selections.")]
    public Task<StaffMcpResult<DocumentExportResult>> ExecuteAsync(
        Guid caseId,
        long expectedCaseVersion,
        string editLeaseToken,
        IReadOnlyList<DocumentExportSelection> selections,
        string operationKey,
        CancellationToken cancellationToken) =>
        StaffMcpCall.ExecuteAsync(async () =>
        {
            StaffMcpInput.RequireIdentifier(caseId, nameof(caseId));
            StaffMcpInput.RequireVersion(expectedCaseVersion, nameof(expectedCaseVersion));
            editLeaseToken = StaffMcpInput.RequireLease(editLeaseToken);
            operationKey = StaffMcpInput.RequireOperationKey(operationKey);
            ArgumentNullException.ThrowIfNull(selections);
            if (selections.Count is < 1 or > 100)
            {
                throw new ModelContextProtocol.McpException(
                    "'selections' must contain between 1 and 100 items.");
            }
            foreach (var selection in selections)
            {
                StaffMcpInput.RequireIdentifier(selection.OccurrenceId, nameof(selection.OccurrenceId));
                StaffMcpInput.RequireIdentifier(selection.VersionId, nameof(selection.VersionId));
            }
            var staff = await actorResolver.RequireAsync(
                StaffMcpPolicies.WriteScope,
                cancellationToken);
            await using var export = await exportDocuments.ExecuteAsync(
                new(
                    caseId,
                    selections,
                    staff.Actor,
                    operationKey,
                    DocumentMcpContent.MaximumExportBytes,
                    expectedCaseVersion,
                    editLeaseToken),
                cancellationToken);
            return new DocumentExportResult(
                DocumentMcpContent.SanitizeFileName(export.FileName),
                export.Manifest.Select(entry => new DocumentExportManifestMcpEntry(
                    DocumentMcpContent.SanitizeFileName(entry.FileName),
                    entry.OccurrenceId,
                    entry.VersionId,
                    entry.SemanticRole,
                    entry.ContentLength,
                    entry.Sha256)).ToArray(),
                await DocumentMcpContent.ReadBase64Async(
                    export.Content,
                    DocumentMcpContent.MaximumExportBytes,
                    cancellationToken));
        });
}
