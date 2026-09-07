using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Uploads;

[AllowAnonymous]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed partial class RequestModel(
    IGetRequestUpload getRequestUpload,
    IUploadToRequest uploadToRequest,
    RequestUploadAttemptLimiter attemptLimiter,
    ILogger<RequestModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Token { get; set; } = string.Empty;

    [BindProperty]
    public IFormFile? Upload { get; set; }

    [BindProperty]
    public string OperationKey { get; set; } = string.Empty;

    [BindProperty]
    public Guid? ReplacementOccurrenceId { get; set; }

    public RequestUploadPublicView? UploadPolicy { get; private set; }

    public string? StatusMessage { get; private set; }
    private const string CompletionStatusKey = "RequestUploadCompletion";


    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        UploadPolicy = await getRequestUpload.ExecuteAsync(Token, cancellationToken);
        if (TempData[CompletionStatusKey] is string completionStatus)
        {
            StatusMessage = completionStatus;
            if (UploadPolicy is not null)
            {
                OperationKey = NextOperationKey(UploadPolicy);
            }
            return Page();
        }
        if (UploadPolicy is null)
        {
            return NotFound();
        }

        OperationKey = NextOperationKey(UploadPolicy);
        return Page();
    }

    /// <summary>
    /// The operation key this page hands the sender. A submission the link has
    /// already taken and not resolved keeps its own key, so the sender's retry
    /// is the same submission and reconciles; a new key is minted only when
    /// the link has nothing outstanding, which is what makes it a new
    /// deliberate submission rather than a duplicate of one custody may hold.
    /// </summary>
    private static string NextOperationKey(RequestUploadPublicView view) =>
        view.UnresolvedOperationKey ?? StaffPageModel.NewOperationKey();

    public async Task<IActionResult> OnPostUploadAsync(CancellationToken cancellationToken)
    {
        UploadPolicy = await getRequestUpload.ExecuteAsync(Token, cancellationToken);
        if (UploadPolicy is null)
        {
            return NotFound();
        }

        // Either shape this server issues: the key minted for a new
        // submission, or the derived key a second file sent while the first
        // was outstanding was given. A key of any other shape is not one of
        // ours and is refused rather than handed on.
        if (!RequestUploadOperationKey.TryNormalize(OperationKey, out var operationKey))
        {
            ModelState.AddModelError(string.Empty, "The upload operation is invalid. Reload the link and try again.");
        }

        if (Upload is null)
        {
            ModelState.AddModelError(nameof(Upload), "Choose a document to upload.");
        }
        else if (Upload.Length == 0)
        {
            ModelState.AddModelError(nameof(Upload), "The selected document is empty.");
        }
        else if (Upload.Length > UploadPolicy.MaximumFileBytes || Upload.Length > int.MaxValue)
        {
            ModelState.AddModelError(nameof(Upload), $"The selected document is larger than the {FormatBytes(Math.Min(UploadPolicy.MaximumFileBytes, int.MaxValue))} limit.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (!attemptLimiter.TryAcquire(Token, out var attemptsInCurrentWindow))
        {
            Response.StatusCode = StatusCodes.Status429TooManyRequests;
            ModelState.AddModelError(
                string.Empty,
                OperatorLabels.Upload.RequestTooManyAttempts);
            return Page();
        }

        await using var content = new MemoryStream((int)Upload!.Length);
        await Upload.CopyToAsync(content, cancellationToken);

        try
        {
            var result = await uploadToRequest.ExecuteAsync(
                new(
                    Token,
                    new(
                        Path.GetFileName(Upload.FileName),
                        string.IsNullOrWhiteSpace(Upload.ContentType)
                            ? "application/octet-stream"
                            : Upload.ContentType,
                        content.ToArray(),
                        operationKey),
                    attemptsInCurrentWindow,
                    ReplacementOccurrenceId),
                cancellationToken);

            switch (result.Decision)
            {
                case RequestUploadDecision.Accepted:
                case RequestUploadDecision.Replay:
                    TempData[CompletionStatusKey] =
                        OperatorLabels.Upload.RetainedCompletionMessage;
                    return RedirectToPage("/Uploads/Request", new { token = Token });
                case RequestUploadDecision.AcceptedPending:
                    // Custody took the bytes durably but has not confirmed
                    // them. The submission stands and must not be sent again,
                    // and nothing here says "retained securely" before custody
                    // has said so.
                    TempData[CompletionStatusKey] =
                        OperatorLabels.Upload.StoringCompletionMessage;
                    return RedirectToPage("/Uploads/Request", new { token = Token });
                case RequestUploadDecision.RateLimited:
                    Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    ModelState.AddModelError(
                        string.Empty,
                        OperatorLabels.Upload.RequestTooManyAttempts);
                    break;
                case RequestUploadDecision.InvalidFile:
                    ModelState.AddModelError(nameof(Upload), "This file type cannot be accepted. Choose one of the permitted document types.");
                    break;
                case RequestUploadDecision.LimitExceeded:
                    ModelState.AddModelError(nameof(Upload), "This request has reached its document or size limit.");
                    break;
                case RequestUploadDecision.OperationConflict:
                    ModelState.AddModelError(string.Empty, "This upload operation was already used for different content. Reload the link and try again.");
                    break;
                case RequestUploadDecision.LimitsVersionMismatch:
                    // The link outlived a limits change. The sender did
                    // nothing wrong and nothing about the Case is disclosed;
                    // they need a new link from whoever sent this one.
                    ModelState.AddModelError(
                        string.Empty,
                        OperatorLabels.Upload.RequestLinkInvalid);
                    break;
                case RequestUploadDecision.NotRetained:
                    // Custody did not take the file, or did not say whether it
                    // did. Nothing was kept and nothing about the Case is
                    // disclosed; the same upload operation is the safe retry.
                    ModelState.AddModelError(string.Empty, "The document could not be retained. Try again using the same upload operation.");
                    break;
                case RequestUploadDecision.Unavailable:
                    return NotFound();
                default:
                    return NotFound();
            }

            return Page();
        }
        // Custody declined the authority this link carries. That is a refusal
        // of this submission and not an uncertainty: the arrival is already
        // recorded refused, so the next page load carries a new operation key,
        // and what the sender sees is a plain sentence rather than the 500 an
        // unhandled authorization fault would put on a public page.
        catch (StaffAuthorizationException exception)
        {
            LogPublicRequestUploadFailure(logger, exception);
            ModelState.AddModelError(string.Empty, OperatorLabels.Upload.RequestRefused);
            return Page();
        }
        // The submission path now puts a remote custody adapter behind this
        // call, so its transport faults belong here too: a dropped connection
        // or a timed-out request is the plain retry message, never a 500 on a
        // page a member of the public is looking at.
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException
            or IOException
            or UnauthorizedAccessException
            or HttpRequestException
            or TimeoutException
            or System.Net.Sockets.SocketException
            or Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            LogPublicRequestUploadFailure(logger, exception);
            ModelState.AddModelError(string.Empty, "The document could not be retained. Try again using the same upload operation.");
            return Page();
        }
    }

    public async Task<IActionResult> OnPostFinalizeAsync(CancellationToken cancellationToken)
    {
        UploadPolicy = await getRequestUpload.ExecuteAsync(Token, cancellationToken);
        if (UploadPolicy is null)
        {
            return NotFound();
        }

        // Finish is anonymous, opens a transaction and runs four queries, so
        // it is guarded by the same per-token window the upload handler
        // already keeps rather than by a second limiter with its own policy.
        if (!attemptLimiter.TryAcquire(Token, out _))
        {
            Response.StatusCode = StatusCodes.Status429TooManyRequests;
            return FinalizeError(OperatorLabels.Upload.RequestTooManyAttempts);
        }

        var result = await uploadToRequest.FinalizeAsync(Token, cancellationToken);
        return result.Decision switch
        {
            // A replay is the same finished submission, and the page it lands
            // on says so; there is nothing different to tell the sender.
            RequestUploadDecision.Accepted =>
                RedirectToPage("/Uploads/Request", new { token = Token }),
            RequestUploadDecision.LimitsVersionMismatch =>
                FinalizeError(OperatorLabels.Upload.RequestLinkInvalid),
            RequestUploadDecision.NotRetained =>
                FinalizeError(OperatorLabels.Upload.RequestNotFinished(result.BlockingState)),
            _ => NotFound()
        };
    }

    private PageResult FinalizeError(string message)
    {
        ModelState.AddModelError(string.Empty, message);
        OperationKey = NextOperationKey(UploadPolicy!);
        return Page();
    }

    public string AcceptedMediaTypes => UploadPolicy is null
        ? string.Empty
        : string.Join(',', UploadPolicy.AllowedMediaTypes.Order(StringComparer.OrdinalIgnoreCase));

    public string MaximumFileSize => UploadPolicy is null
        ? string.Empty
        : FormatBytes(UploadPolicy.MaximumFileBytes);

    private static string FormatBytes(long bytes) => bytes % (1024 * 1024) == 0
        ? $"{bytes / (1024 * 1024)} MB"
        : $"{bytes / 1024} KB";

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "A public document request upload failed.")]
    private static partial void LogPublicRequestUploadFailure(ILogger logger, Exception exception);
}

public sealed class RequestUploadAttemptLimiter(RequestUploadLimits limits, TimeProvider timeProvider)
{
    private readonly object sync = new();
    private readonly Dictionary<string, AttemptWindow> windows = new(StringComparer.Ordinal);

    public bool TryAcquire(string token, out int attemptsInCurrentWindow)
    {
        string digest;
        try
        {
            digest = RequestUploadToken.ComputeDigest(token);
        }
        catch (ArgumentException)
        {
            attemptsInCurrentWindow = limits.RateLimit;
            return false;
        }

        lock (sync)
        {
            var now = timeProvider.GetUtcNow();
            if (!windows.TryGetValue(digest, out var window)
                || now - window.StartedAtUtc >= limits.RateLimitWindow)
            {
                attemptsInCurrentWindow = 0;
                windows[digest] = new(now, 1);
                RemoveExpiredWindows(now, digest);
                return true;
            }

            attemptsInCurrentWindow = window.Attempts;
            if (window.Attempts >= limits.RateLimit)
            {
                return false;
            }

            windows[digest] = window with { Attempts = checked(window.Attempts + 1) };
            return true;
        }
    }

    private void RemoveExpiredWindows(DateTimeOffset now, string currentDigest)
    {
        if (windows.Count < 1024)
        {
            return;
        }

        foreach (var entry in windows.ToArray())
        {
            if (!string.Equals(entry.Key, currentDigest, StringComparison.Ordinal)
                && now - entry.Value.StartedAtUtc >= limits.RateLimitWindow)
            {
                windows.Remove(entry.Key);
            }
        }
    }

    private sealed record AttemptWindow(DateTimeOffset StartedAtUtc, int Attempts);
}
