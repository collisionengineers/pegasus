using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Core.Actors;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Web.Pages.Intake;

public sealed partial class SourceModel(
    IDownloadIntakeSource downloadSource,
    ILogger<SourceModel> logger) : PageModel
{
    public async Task<IActionResult> OnGetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (!StaffActorFactory.TryCreate(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                User.FindAll(ClaimTypes.Role).Select(claim => claim.Value),
                out var actor))
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
                source.Content.ToArray(),
                "application/octet-stream",
                SafeFileName(source.FileName));
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
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

    private static string SafeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(name))
        {
            return "intake-source.bin";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var safe = string.Concat(name.Where(character =>
            !char.IsControl(character)
            && character != '"'
            && character != '\''
            && character != ';'
            && !invalid.Contains(character)));
        return string.IsNullOrWhiteSpace(safe) ? "intake-source.bin" : safe;
    }

    [LoggerMessage(
        EventId = 1204,
        Level = LogLevel.Warning,
        Message = "Retained intake source integrity validation failed for receipt {ReceiptId}.")]
    private static partial void LogIntakeSourceIntegrityFailure(
        ILogger logger,
        Guid receiptId,
        Exception exception);
}
