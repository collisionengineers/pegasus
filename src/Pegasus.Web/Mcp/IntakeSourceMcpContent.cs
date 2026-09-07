using ModelContextProtocol;
using Pegasus.Core.Intake;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Mcp;

internal sealed record IntakeSourceToolResult(
    Guid ReceiptId,
    string FileName,
    string MediaType,
    long ContentLength,
    string Sha256,
    bool ContentIncluded,
    string? ContentBase64,
    string? Notice,
    string CorrelationId);

internal static class IntakeSourceMcpContent
{
    private const int DefaultInlineContentBytes = 64 * 1024;
    private const int MaximumInlineContentBytes = 10 * 1024 * 1024;

    public static async Task<IntakeSourceToolResult> DownloadAsync(
        IGetIntakeSourceMetadata getSourceMetadata,
        IDownloadIntakeSource downloadSource,
        Guid receiptId,
        ActionActor actor,
        int maxInlineBytes,
        string correlationId,
        CancellationToken cancellationToken)
    {
        AutomationMcpErrors.RequireId(receiptId, "receipt identifier");
        var inlineLimit = maxInlineBytes == 0 ? DefaultInlineContentBytes : maxInlineBytes;
        if (inlineLimit is < 1 or > MaximumInlineContentBytes)
        {
            throw new McpException(
                $"maxInlineBytes must be between 1 and {MaximumInlineContentBytes}.");
        }

        var metadata = await getSourceMetadata.ExecuteAsync(
            new(receiptId, actor),
            cancellationToken)
            ?? throw new McpException("The retained intake source was not found.");
        var included = metadata.ContentLength <= inlineLimit;
        if (!included)
        {
            return new(
                receiptId,
                metadata.FileName,
                metadata.MediaType,
                metadata.ContentLength,
                metadata.Sha256,
                false,
                null,
                $"The content ({metadata.ContentLength} bytes) exceeds the inline limit of {inlineLimit} bytes; retry with a larger maxInlineBytes when the client can accept it.",
                correlationId);
        }

        var download = await downloadSource.ExecuteAsync(
            new(receiptId, actor),
            cancellationToken)
            ?? throw new McpException("The retained intake source was not found.");
        EnsureMatchesMetadata(receiptId, metadata, download);
        return new(
            receiptId,
            download.FileName,
            download.ContentType,
            download.ContentLength,
            download.Sha256,
            included,
            Convert.ToBase64String(download.Content.Span),
            null,
            correlationId);
    }

    private static void EnsureMatchesMetadata(
        Guid receiptId,
        IntakeFileMetadata metadata,
        IntakeSourceDownload download)
    {
        if (metadata.ReceiptId != receiptId
            || !string.Equals(metadata.FileName, download.FileName, StringComparison.Ordinal)
            || !string.Equals(metadata.MediaType, download.ContentType, StringComparison.Ordinal)
            || metadata.ContentLength != download.ContentLength
            || !string.Equals(metadata.Sha256, download.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The retained intake source no longer matches its authorized metadata.");
        }
    }
}
