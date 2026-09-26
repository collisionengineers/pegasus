using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Web.Presentation;
using Labels = Pegasus.Web.Presentation.OperatorLabels.Upload;

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
///
/// Opened from a Case page (Add evidence, <c>?caseId=</c>), the surface
/// carries that Case as the upload's declared destination (FRD-18): the
/// member of staff has already decided where the files go, so processing
/// links them there and runs no identification, and the operator returns to
/// the Case's Files panel.
/// </remarks>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
[RequestSizeLimit(IntakeEnvelopeLimits.MaximumBatchContentLength)]
[RequestFormLimits(
    BufferBody = true,
    BufferBodyLengthLimit = IntakeEnvelopeLimits.MaximumBatchContentLength,
    MultipartBodyLengthLimit = IntakeEnvelopeLimits.MaximumBatchContentLength,
    MemoryBufferThreshold = 64 * 1024)]
public sealed partial class UploadModel(
    IGroupedIntakeSubmission groupedSubmission,
    IIntakeAssociationDestinationQueries destinations,
    TimeProvider timeProvider,
    ILogger<UploadModel> logger) : StaffPageModel
{
    public static string MaximumSizeLabel =>
        OperatorLabels.FileSize(IntakeEnvelopeLimits.MaximumContentLength);

    /// <summary>The limits the picker states and upload.js checks before posting, from their one owner (FRD-18).</summary>
    public static int MaximumFileCount => IntakeEnvelopeLimits.MaximumBatchFileCount;

    public static long MaximumFileBytes => IntakeEnvelopeLimits.MaximumContentLength;

    public static long MaximumTotalBytes => IntakeEnvelopeLimits.MaximumBatchFileContentLength;

    [BindProperty]
    public IFormFile[] Upload { get; set; } = [];

    [BindProperty]
    public string ExternalReceiptToken { get; set; } = string.Empty;

    /// <summary>The Case declared before the upload, when Add evidence on a Case page opened this surface.</summary>
    [BindProperty]
    public Guid? DeclaredCaseId { get; set; }

    /// <summary>The declared destination's facts, drawn above the picker; null for an ordinary upload.</summary>
    public UploadReviewDestination? Destination { get; private set; }

    /// <summary>A Case was asked for but is unknown or archived: the picker still works, for an upload whose destination is chosen after.</summary>
    public bool DestinationUnavailable { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid? caseId, CancellationToken cancellationToken)
    {
        ExternalReceiptToken = Guid.NewGuid().ToString("N");
        if (caseId is { } declared)
        {
            if (!TryGetActor(out var actor))
            {
                return Forbid();
            }

            await LoadDestinationAsync(declared, actor, cancellationToken);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        // The declared Case is read again at post: the form's id is a claim,
        // the current Case row is the authority.
        if (DeclaredCaseId is { } declared)
        {
            await LoadDestinationAsync(declared, actor, cancellationToken);
            if (Destination is null)
            {
                ModelState.AddModelError(string.Empty, Labels.DestinationUnavailable);
            }
        }

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
        if (Upload.Sum(file => file.Length) > IntakeEnvelopeLimits.MaximumBatchFileContentLength)
        {
            ModelState.AddModelError(nameof(Upload), "The selected files exceed the 200 MiB total upload limit.");
        }
        for (var index = 0; index < Upload.Length; index++)
        {
            var file = Upload[index];
            if (file.Length == 0)
            {
                ModelState.AddModelError(
                    nameof(Upload),
                    Upload.Length == 1 ? "That file is empty." : $"File {index + 1} is empty.");
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

        var uploader = IntakeActorIdentity.Staff(actor.SubjectId);
        try
        {
            var files = new List<StreamedGroupedIntakeFile>(Upload.Length);
            for (var index = 0; index < Upload.Length; index++)
            {
                var file = Upload[index];
                var header = new byte[Math.Min(file.Length, 40)];
                await using (var stream = file.OpenReadStream())
                {
                    await stream.ReadExactlyAsync(header, cancellationToken);
                }
                if (!IntakeUploadFilePolicy.IsAccepted(file.FileName, file.ContentType, header))
                {
                    ModelState.AddModelError(
                        nameof(Upload),
                        $"File {index + 1} has an unsupported type or its contents do not match the selected file type.");
                    continue;
                }
                files.Add(new(
                    index,
                    new(
                        Path.GetFileName(file.FileName),
                        string.IsNullOrWhiteSpace(file.ContentType)
                            ? "application/octet-stream"
                            : file.ContentType,
                        file.Length,
                        _ => ValueTask.FromResult<Stream>(file.OpenReadStream()),
                        timeProvider.GetUtcNow(),
                        uploader,
                        new(IntakeSourceChannel.ManualUpload, ExternalReceiptToken))));
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var result = await groupedSubmission.ExecuteStreamedAsync(
                new StreamedGroupedIntakeSubmissionRequest(
                    ExternalReceiptToken,
                    uploader,
                    timeProvider.GetUtcNow(),
                    files,
                    IntakeSourceChannel.ManualUpload,
                    DeclaredCaseId: DeclaredCaseId),
                cancellationToken);

            // A declared destination needs no review: the decision was made on
            // the Case, so the operator goes back to it (FRD-18).
            if (DeclaredCaseId is { } declaredCaseId && Destination is { } destination)
            {
                TempData["CaseStatus"] = result.Members.All(member => member.IsDuplicate)
                    ? Labels.AlreadyReceivedForCase(destination.Reference)
                    : Labels.ReceivedForCase(result.Members.Count, destination.Reference);
                return RedirectToPage(
                    "/Cases/Details",
                    new { id = declaredCaseId, section = "files" });
            }

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

    private async Task LoadDestinationAsync(Guid caseId, ActionActor actor, CancellationToken cancellationToken)
    {
        var destination = await destinations.GetAsync(caseId, actor, cancellationToken);
        if (destination is null)
        {
            Destination = null;
            DestinationUnavailable = true;
            DeclaredCaseId = null;
            return;
        }

        DeclaredCaseId = destination.CaseId;
        Destination = new UploadReviewDestination(
            destination.Reference,
            destination.Registration,
            destination.Claimant,
            destination.Principal,
            OperatorLabels.AssociationDestinationState(destination),
            $"/Cases/{destination.CaseId:D}");
    }

    [LoggerMessage(
        EventId = 1310,
        Level = LogLevel.Warning,
        Message = "A staff upload of {FileName} could not be processed.")]
    private static partial void LogUploadFailed(ILogger logger, string fileName, Exception exception);

}
