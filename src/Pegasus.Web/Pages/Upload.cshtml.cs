using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Core.Actors;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages;

/// <summary>
/// Manual submission, on a surface of its own.
/// </summary>
/// <remarks>
/// This was a panel above the Inbox list whose button posted to
/// <c>action=""</c>: the handler URL was never generated, so the browser POSTed
/// to the page with no handler, nothing matched, and Razor Pages silently
/// re-rendered. HTTP 200, no receipt, no work item, no error shown — the only
/// manual submission path in the product was a dead button.
///
/// The route is declared here as a plain page with an unnamed handler, so the
/// form posts to its own URL and there is no handler name to fail to generate.
///
/// A successful request ends after the source bytes and Pending work item are
/// durable. Worker owns every later processing transition.
/// </remarks>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
public sealed partial class UploadModel(
    IGroupedIntakeSubmission groupedSubmission,
    TimeProvider timeProvider,
    ILogger<UploadModel> logger) : PageModel
{
    public static string MaximumSizeLabel =>
        OperatorLabels.FileSize(IntakeEnvelopeLimits.MaximumContentLength);

    public static int MaximumFileCount => IntakeEnvelopeLimits.MaximumBatchFileCount;

    [BindProperty]
    public IFormFile[] Upload { get; set; } = [];

    [BindProperty]
    public string ExternalReceiptToken { get; set; } = string.Empty;

    public void OnGet()
    {
        ExternalReceiptToken = Guid.NewGuid().ToString("N");
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        // The upload receipt is the replay key. A malformed one means the form
        // state cannot be trusted, so the post is refused rather than quietly
        // given a fresh key — which would turn a replay into a second receipt.
        if (Guid.TryParseExact(ExternalReceiptToken, "N", out var token))
        {
            ExternalReceiptToken = token.ToString("N");
        }
        else
        {
            ModelState.AddModelError(
                string.Empty,
                "The upload receipt is invalid. Refresh the page and try again.");
        }

        if (Upload.Length == 0)
        {
            ModelState.AddModelError(nameof(Upload), "Choose a file to upload.");
        }
        else if (Upload.Length > IntakeEnvelopeLimits.MaximumBatchFileCount)
        {
            ModelState.AddModelError(
                nameof(Upload),
                $"You selected {Upload.Length} files. Submit {IntakeEnvelopeLimits.MaximumBatchFileCount} "
                + "or fewer at a time.");
        }
        for (var index = 0; index < Upload.Length; index++)
        {
            var file = Upload[index];
            if (file.Length == 0)
            {
                ModelState.AddModelError(nameof(Upload), $"File {index + 1} is empty.");
            }
            else if (file.Length > IntakeEnvelopeLimits.MaximumContentLength)
            {
                ModelState.AddModelError(
                    nameof(Upload),
                    $"File {index + 1} is {OperatorLabels.FileSize(file.Length)}. "
                    + $"Files must be {MaximumSizeLabel} or smaller.");
            }
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (!StaffActorFactory.TryCreate(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                User.FindAll(ClaimTypes.Role).Select(claim => claim.Value),
                out var actor))
        {
            return Forbid();
        }

        try
        {
            var files = new List<GroupedIntakeFile>(Upload.Length);
            for (var index = 0; index < Upload.Length; index++)
            {
                var file = Upload[index];
                await using var memory = new MemoryStream((int)file.Length);
                await file.CopyToAsync(memory, cancellationToken);
                files.Add(new(
                    index,
                    new(
                        Path.GetFileName(file.FileName),
                        string.IsNullOrWhiteSpace(file.ContentType)
                            ? "application/octet-stream"
                            : file.ContentType,
                        memory.ToArray(),
                        timeProvider.GetUtcNow(),
                        $"staff:{actor.SubjectId}",
                        new(IntakeSourceChannel.ManualUpload, ExternalReceiptToken))));
            }

            var result = await groupedSubmission.ExecuteAsync(
                new(
                    ExternalReceiptToken,
                    $"staff:{actor.SubjectId}",
                    timeProvider.GetUtcNow(),
                    files),
                cancellationToken);

            // A one-member group is the existing single-file upload flow: it
            // keeps its own status page and replay notice rather than sending
            // the operator to a group page for a group of one.
            if (result.Members.Count == 1)
            {
                var member = result.Members[0];
                return RedirectToPage(
                    "/UploadStatus",
                    new
                    {
                        id = member.StagedReceiptId,
                        duplicate = member.IsDuplicate ? "true" : null
                    });
            }

            return RedirectToPage(
                "/UploadGroupStatus",
                new { id = result.Group.Id });
        }
        catch (IntakeSourceIdentityConflictException)
        {
            ModelState.AddModelError(
                string.Empty,
                "That upload was already used for a different file. Try again.");
        }
        catch (IntakeArtifactRetentionException)
        {
            ModelState.AddModelError(
                string.Empty,
                "The file could not be stored. Try again, or contact an administrator if it keeps failing.");
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            // The operator is told to try again; without this nobody can tell
            // them why, because the only record of the cause was the message
            // itself, which deliberately does not carry one.
            LogUploadFailed(logger, Upload.FirstOrDefault()?.FileName ?? "(batch)", exception);
            ModelState.AddModelError(
                string.Empty,
                "The file could not be processed. Try again, or contact an administrator if it keeps failing.");
        }

        return Page();
    }

    [LoggerMessage(
        EventId = 1310,
        Level = LogLevel.Warning,
        Message = "A staff upload of {FileName} could not be processed.")]
    private static partial void LogUploadFailed(ILogger logger, string fileName, Exception exception);

}
