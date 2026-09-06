using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;

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

    public RequestUploadPublicView? UploadPolicy { get; private set; }

    public string? StatusMessage { get; private set; }
    private const string CompletionStatusKey = "RequestUploadCompletion";

    /// <summary>
    /// What a confirmed submission is allowed to say. Custody holds the exact
    /// bytes under a known identity, so the claim is true.
    /// </summary>
    private const string RetainedCompletionMessage =
        "Your document was received and retained securely.";

    /// <summary>
    /// What a durable but unconfirmed submission is allowed to say. It states
    /// only what is true — the document arrived and is being stored — and makes
    /// no claim about custody, because custody has not made one.
    /// </summary>
    /// <remarks>
    /// This belongs beside the other sender-facing strings in
    /// <c>OperatorLabels</c> and moves there with C08's labels batch; that file
    /// is not this slice's to edit.
    /// </remarks>
    private const string StoringCompletionMessage =
        "Your document was received and is being stored. You do not need to send it again.";

    /// <summary>
    /// What a refused submission is allowed to say. Custody declined this
    /// submission outright, so it is not "try the same operation again" - the
    /// next page load carries a new one - and it discloses nothing about the
    /// Case, the link or the reason.
    /// </summary>
    /// <remarks>
    /// Belongs beside the other sender-facing strings in <c>OperatorLabels</c>
    /// and moves there with C08's labels batch, like the two above it.
    /// </remarks>
    private const string RefusedMessage =
        "This document was not accepted. Reload the link and try again.";


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

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        UploadPolicy = await getRequestUpload.ExecuteAsync(Token, cancellationToken);
        if (UploadPolicy is null)
        {
            return NotFound();
        }

        if (!Guid.TryParseExact(OperationKey, "N", out var operationId))
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
            ModelState.AddModelError(string.Empty, "Too many upload attempts were made. Wait before trying again.");
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
                        operationId.ToString("N")),
                    attemptsInCurrentWindow),
                cancellationToken);

            switch (result.Decision)
            {
                case RequestUploadDecision.Accepted:
                case RequestUploadDecision.Replay:
                    TempData[CompletionStatusKey] = RetainedCompletionMessage;
                    return RedirectToPage("/Uploads/Request", new { token = Token });
                case RequestUploadDecision.AcceptedPending:
                    // Custody took the bytes durably but has not confirmed
                    // them. The submission stands and must not be sent again,
                    // and nothing here says "retained securely" before custody
                    // has said so.
                    TempData[CompletionStatusKey] = StoringCompletionMessage;
                    return RedirectToPage("/Uploads/Request", new { token = Token });
                case RequestUploadDecision.RateLimited:
                    Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    ModelState.AddModelError(string.Empty, "Too many upload attempts were made. Wait before trying again.");
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
                    ModelState.AddModelError(string.Empty, "This link is no longer valid. Ask for a new one.");
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
            ModelState.AddModelError(string.Empty, RefusedMessage);
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
