using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Mcp;

internal sealed record UploadBeginToolResult(
    Guid UploadId,
    int PartBytes,
    int PartCount,
    DateTimeOffset ExpiresAtUtc,
    IReadOnlyList<int> ReceivedParts,
    bool IsReplay,
    string OperationKey,
    string CorrelationId);

internal sealed record UploadPartToolResult(
    Guid UploadId,
    int PartNumber,
    string Sha256,
    bool IsReplay,
    string CorrelationId);

/// <summary>
/// A document too large for one tool argument, staged in parts for
/// <c>pegasus_document_add</c> or <c>pegasus_ai_job_complete_market_research</c>
/// (operator, 22 October 2026, R203): staging is adapter mechanics, so a
/// success writes no history row and only a refusal is appended. The upload
/// belongs to the Automation client, not the grant, so a part staged on the
/// documents server is used on the jobs server by the same client. Results
/// carry counters and identifiers, never content.
/// </summary>
[McpServerToolType]
internal sealed class DocumentUploadMcpTools(
    DocumentUploads begin,
    DocumentUploads append,
    AutomationActorResolver resolver,
    AutomationMcpAuditor auditor)
{
    private const string UploadPurpose = "document-upload";

    [McpServerTool(
        Name = "pegasus_upload_begin",
        Title = "Begin document upload",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Begins staging one document of at most 10 MiB in parts for pegasus_document_add or pegasus_ai_job_complete_market_research. Declare its size and SHA-256; the result gives the uploadId, the part size and count and when the upload expires. Replaying the same operation key with the same size and SHA-256 returns the same uploadId and the parts already received.")]
    public async Task<UploadBeginToolResult> BeginAsync(
        [Description("The whole document's size in bytes, 1 to 10485760.")] long totalBytes,
        [Description("The whole document's SHA-256, 64 hexadecimal digits.")] string sha256,
        [Description("Caller idempotency key prefixed 'mcp:'.")] string operationKey,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync("pegasus_upload_begin", cancellationToken);
        var key = AutomationMcpErrors.RequireOperationKey(operationKey);
        var uploadId = AutomationMcpErrors.DeriveOperationGuid(
            UploadPurpose,
            ActionActor.Automation(context.ClientId),
            key);
        return await auditor.RecordDenialAsync(
            context,
            "pegasus_upload_begin",
            uploadId.ToString("D"),
            key,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                var ticket = await begin.ExecuteAsync(
                    new BeginDocumentUploadRequest(uploadId, context.ClientId, totalBytes, sha256),
                    cancellationToken);
                return new UploadBeginToolResult(
                    ticket.UploadId,
                    ticket.PartBytes,
                    ticket.PartCount,
                    ticket.ExpiresAtUtc,
                    ticket.ReceivedParts,
                    ticket.IsReplay,
                    key,
                    AutomationMcpAuditor.CorrelationId(context, key));
            }),
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_upload_part",
        Title = "Send document upload part",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Sends one part of a staged upload from pegasus_upload_begin: every part is partBytes long but the last. The part's own SHA-256 is checked before it is stored, so a damaged part is refused and nothing is stored; sending the same bytes again under a part number is a no-op.")]
    public async Task<UploadPartToolResult> PartAsync(
        [Description("The uploadId from pegasus_upload_begin.")] Guid uploadId,
        [Description("The part number, 1 to partCount.")] int partNumber,
        [Description("The part's bytes encoded as base64, at most 65536 characters.")] string contentBase64,
        [Description("The part's own SHA-256, 64 hexadecimal digits.")] string sha256,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync("pegasus_upload_part", cancellationToken);
        return await auditor.RecordRefusalAsync(
            context,
            "pegasus_upload_part",
            uploadId == Guid.Empty ? "invalid" : uploadId.ToString("D"),
            null,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                AutomationMcpErrors.RequireId(uploadId, "upload identifier");
                var content = AutomationMcpErrors.DecodeContent(
                    contentBase64,
                    DocumentUploadLimits.PartBytes,
                    "The part content");
                var receipt = await append.ExecuteAsync(
                    new AppendDocumentUploadPartRequest(uploadId, context.ClientId, partNumber, content, sha256),
                    cancellationToken);
                return new UploadPartToolResult(
                    receipt.UploadId,
                    receipt.PartNumber,
                    receipt.Sha256,
                    receipt.IsReplay,
                    AutomationMcpAuditor.CorrelationId(context, null));
            }),
            cancellationToken);
    }

    /// <summary>
    /// The content of a consuming call: exactly one of the inline base64 and a
    /// staged upload, the upload opened whole and checked against its
    /// declaration before the call claims a lease or opens a transaction.
    /// </summary>
    internal static async Task<ReadOnlyMemory<byte>> ReadContentAsync(
        DocumentUploads uploads,
        AutomationActorContext context,
        string? contentBase64,
        Guid? uploadId,
        string description,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(contentBase64) == (uploadId is null))
        {
            throw new McpException("Exactly one of contentBase64 and uploadId is required.");
        }

        if (uploadId is not { } staged)
        {
            return AutomationMcpErrors.DecodeContent(
                contentBase64!,
                AutomationMcpErrors.MaximumDocumentBytes,
                description);
        }

        AutomationMcpErrors.RequireId(staged, "upload identifier");
        var opened = await uploads.ExecuteAsync(new OpenDocumentUploadRequest(staged, context.ClientId), cancellationToken);
        return opened.Content;
    }
}
