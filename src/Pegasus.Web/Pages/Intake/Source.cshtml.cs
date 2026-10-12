using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Web.Pages.Intake;

public sealed partial class SourceModel(
    IDownloadIntakeSource downloadSource,
    ILogger<SourceModel> logger) : StaffPageModel
{
    public async Task<IActionResult> OnGetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            var source = await downloadSource.ExecuteAsync(
                new DownloadIntakeSourceQuery(id, actor),
                cancellationToken);
            if (source is null)
            {
                return NotFound();
            }

            Response.Headers.XContentTypeOptions = "nosniff";
            return File(
                DocumentContentCachePublisherExtensions.StreamOf(source.Content),
                "application/octet-stream",
                IntakeDownloadFileName.Safe(source.FileName, "intake-source.bin"));
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (IntakeCustodyUnavailableException)
        {
            return CustodyUnavailable();
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
        catch (IntakeArtifactIntegrityException exception)
        {
            LogIntakeSourceIntegrityFailure(logger, id, exception);
            return new ContentResult
            {
                StatusCode = StatusCodes.Status409Conflict,
                ContentType = "text/plain; charset=utf-8",
                Content = "The retained source could not be downloaded safely."
            };
        }
    }

    private static ContentResult CustodyUnavailable() => new()
    {
        StatusCode = StatusCodes.Status409Conflict,
        ContentType = "text/plain; charset=utf-8",
        Content = "The retained file is not available until durable storage is confirmed. Refresh this record and try again."
    };

    [LoggerMessage(
        EventId = 1204,
        Level = LogLevel.Warning,
        Message = "Retained intake source integrity validation failed for receipt {ReceiptId}.")]
    private static partial void LogIntakeSourceIntegrityFailure(
        ILogger logger,
        Guid receiptId,
        Exception exception);
}
