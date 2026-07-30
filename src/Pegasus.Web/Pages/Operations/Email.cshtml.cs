using System.Collections.Immutable;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Core.Actors;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;

namespace Pegasus.Web.Pages.Operations;

[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
[ValidateAntiForgeryToken]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class EmailModel(
    GetEmailOperations getEmailOperations,
    RetryMailboxProcessing retryMailboxProcessing) : PageModel
{
    private readonly GetEmailOperations getEmailOperations =
        getEmailOperations ?? throw new ArgumentNullException(nameof(getEmailOperations));
    private readonly RetryMailboxProcessing retryMailboxProcessing =
        retryMailboxProcessing ?? throw new ArgumentNullException(nameof(retryMailboxProcessing));

    public EmailOperationsProjection Operations { get; private set; } = new(
        ImmutableArray<EmailOperationProjection>.Empty,
        ImmutableArray<EmailOperationProjection>.Empty,
        ReceivedLimitReached: false,
        SentLimitReached: false);

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        Operations = await getEmailOperations.ExecuteAsync(actor, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostRetryAsync(
        string mailboxId,
        EmailOperationDirection direction,
        string expectedFailureCode,
        DateTimeOffset expectedDueAtUtc,
        string operationKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (!ModelState.IsValid || !Enum.IsDefined(direction))
        {
            StatusMessage = "The mailbox retry request was invalid. Refresh and try again.";
            return RedirectToPage();
        }

        try
        {
            var result = await retryMailboxProcessing.ExecuteAsync(
                new(
                    mailboxId,
                    direction,
                    expectedFailureCode,
                    expectedDueAtUtc,
                    actor,
                    operationKey),
                cancellationToken);
            StatusMessage = result.IsReplay
                ? "Mailbox processing was already scheduled for retry."
                : "Mailbox processing was scheduled for retry.";
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (ArgumentException)
        {
            StatusMessage = "The mailbox retry request was invalid. Refresh and try again.";
        }
        catch (InvalidOperationException)
        {
            StatusMessage = "The mailbox failure changed before retry. Refresh and try again.";
        }

        return RedirectToPage();
    }

    public static string StateLabel(EmailOperationState state) => state switch
    {
        EmailOperationState.Pending => "Pending",
        EmailOperationState.Succeeded => "Succeeded",
        EmailOperationState.Failed => "Failed",
        EmailOperationState.Unknown => "Unknown",
        _ => throw new InvalidOperationException(
            $"Unknown email operation state value '{(int)state}'.")
    };

    public static string NewOperationKey() => Guid.NewGuid().ToString("N");

    private bool TryGetActor(out ActionActor actor)
    {
        if (StaffActorFactory.TryCreate(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                User.FindAll(ClaimTypes.Role).Select(claim => claim.Value),
                out var resolved))
        {
            actor = resolved;
            return true;
        }

        actor = null!;
        return false;
    }
}
