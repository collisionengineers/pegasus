using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using Pegasus.Core.Documents;
using Pegasus.Core.Intake;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Mcp;

/// <summary>
/// The structured half of a retained-source download: identity, size, hash,
/// whether the content came back as native content blocks, and where the
/// original bytes are. Content never travels in this record.
/// </summary>
internal sealed record IntakeSourceToolResult(
    Guid ReceiptId,
    string FileName,
    string MediaType,
    long ContentLength,
    string Sha256,
    bool ContentIncluded,
    string ContentUrl,
    string? Notice,
    string CorrelationId);

/// <summary>
/// The one download of a retained intake source for the Unidentified and
/// Triage tools: authorised metadata first, then the bytes only when they can
/// be delivered inline, verified against that metadata before anything is
/// handed back (<see cref="AutomationFileContent"/>).
/// </summary>
internal static class IntakeSourceMcpContent
{
    public static string ContentUrl(Guid receiptId) => $"/automation/intake-sources/{receiptId:D}";

    public static async Task<CallToolResult> DownloadAsync(
        IGetIntakeSourceMetadata getSourceMetadata,
        IDownloadIntakeSource downloadSource,
        IRenderImageForDelivery images,
        IExtractPdfPageText pdfText,
        Guid receiptId,
        ActionActor actor,
        int maxInlineBytes,
        string correlationId,
        CancellationToken cancellationToken)
    {
        AutomationMcpErrors.RequireId(receiptId, "receipt identifier");
        var inlineLimit = AutomationFileContent.NormalizeInlineLimit(maxInlineBytes);

        var metadata = await getSourceMetadata.ExecuteAsync(
            new(receiptId, actor),
            cancellationToken)
            ?? throw new McpException("The retained intake source was not found.");
        var contentUrl = ContentUrl(receiptId);
        AutomationFileDelivery delivery;
        if (!AutomationFileContent.CanDeliverInline(metadata.MediaType, metadata.ContentLength, inlineLimit))
        {
            delivery = AutomationFileContent.Withheld(
                metadata.FileName,
                metadata.MediaType,
                metadata.ContentLength,
                metadata.Sha256,
                $"The content ({metadata.ContentLength} bytes, {metadata.MediaType}) exceeds the inline limit of {inlineLimit} bytes or is not an image, PDF or text file; fetch contentUrl with this bearer token.",
                contentUrl);
        }
        else
        {
            var download = await downloadSource.ExecuteAsync(
                new(receiptId, actor),
                cancellationToken)
                ?? throw new McpException("The retained intake source was not found.");
            EnsureMatchesMetadata(receiptId, metadata, download);
            delivery = await AutomationFileContent.DeliverAsync(
                images,
                pdfText,
                download.FileName,
                download.ContentType,
                download.ContentLength,
                download.Sha256,
                download.Content,
                inlineLimit,
                contentUrl,
                cancellationToken);
        }

        return AutomationFileContent.ToResult(
            delivery,
            new IntakeSourceToolResult(
                receiptId,
                metadata.FileName,
                metadata.MediaType,
                metadata.ContentLength,
                metadata.Sha256,
                delivery.ContentIncluded,
                contentUrl,
                delivery.Notice,
                correlationId));
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
