using System.Collections.Immutable;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Actors;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Pages.Operations;

[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
[ValidateAntiForgeryToken]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class IndexModel(
    GetRequestOperations getRequestOperations,
    GetAutomationIntakeActivity getAutomationIntakeActivity,
    RetryExternalWork retryExternalWork,
    IAcquireCaseEditLease acquireCaseEditLease,
    IRenewCaseEditLease renewCaseEditLease,
    IReleaseCaseEditLease releaseCaseEditLease,
    IRevokeRequestUploadLink revokeRequestUploadLink) : PageModel
{
    private const string LeaseCaseIdKey = "OperationsRequestLeaseCaseId";
    private const string LeaseTokenKey = "OperationsRequestLeaseToken";
    private const string LeaseOperationKeyKey = "OperationsRequestLeaseOperationKey";
    private const string PreservedReasonKey = "OperationsRequestReason";
    private const string PreservedRequestIdKey = "OperationsRequestReasonId";

    private readonly GetRequestOperations getRequestOperations =
        getRequestOperations ?? throw new ArgumentNullException(nameof(getRequestOperations));
    private readonly GetAutomationIntakeActivity getAutomationIntakeActivity =
        getAutomationIntakeActivity ?? throw new ArgumentNullException(nameof(getAutomationIntakeActivity));
    private readonly RetryExternalWork retryExternalWork =
        retryExternalWork ?? throw new ArgumentNullException(nameof(retryExternalWork));
    private readonly IAcquireCaseEditLease acquireCaseEditLease =
        acquireCaseEditLease ?? throw new ArgumentNullException(nameof(acquireCaseEditLease));
    private readonly IRenewCaseEditLease renewCaseEditLease =
        renewCaseEditLease ?? throw new ArgumentNullException(nameof(renewCaseEditLease));
    private readonly IReleaseCaseEditLease releaseCaseEditLease =
        releaseCaseEditLease ?? throw new ArgumentNullException(nameof(releaseCaseEditLease));
    private readonly IRevokeRequestUploadLink revokeRequestUploadLink =
        revokeRequestUploadLink ?? throw new ArgumentNullException(nameof(revokeRequestUploadLink));

    public RequestOperationsProjection Operations { get; private set; } = new(
        ImmutableArray<RequestOperationProjection>.Empty,
        LimitReached: false);

    public ImmutableArray<AutomationIntakeProjection> ReceivedThroughApi { get; private set; } =
        ImmutableArray<AutomationIntakeProjection>.Empty;

    public Guid? LeaseCaseId { get; private set; }
    public ImmutableHashSet<Guid> RecoverableLeaseCaseIds { get; private set; } =
        ImmutableHashSet<Guid>.Empty;
    public Guid? PreservedRequestId { get; private set; }
    public string? PreservedReason { get; private set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        Operations = await getRequestOperations.ExecuteAsync(actor, cancellationToken);
        ReceivedThroughApi = await getAutomationIntakeActivity.ExecuteAsync(actor, cancellationToken);
        RestoreLeaseState(actor);
        PreservedRequestId = ReadGuidTempData(PreservedRequestIdKey);
        PreservedReason = TempData[PreservedReasonKey] as string;
        return Page();
    }

    public async Task<IActionResult> OnPostRetryExternalAsync(
        Guid workItemId,
        int expectedAttemptCount,
        string operationKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (!ModelState.IsValid || workItemId == Guid.Empty)
        {
            StatusMessage = "The external work retry request was invalid. Refresh and try again.";
            return RedirectToPage();
        }

        try
        {
            var result = await retryExternalWork.ExecuteAsync(
                new(workItemId, expectedAttemptCount, actor, operationKey),
                cancellationToken);
            StatusMessage = result.IsReplay
                ? "External work was already scheduled for retry."
                : "External work was scheduled for retry.";
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (ArgumentException)
        {
            StatusMessage = "The external work retry request was invalid. Refresh and try again.";
        }
        catch (InvalidOperationException)
        {
            StatusMessage = "The external work failure changed before retry. Refresh and try again.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostClaimLeaseAsync(
        Guid caseId,
        long expectedCaseVersion,
        string operationKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (!ModelState.IsValid || caseId == Guid.Empty || expectedCaseVersion < 0)
        {
            StatusMessage = "Edit mode could not be entered. Refresh and try again.";
            return RedirectToPage();
        }

        try
        {
            var lease = await acquireCaseEditLease.ExecuteAsync(
                new(caseId, expectedCaseVersion, actor, operationKey),
                cancellationToken);
            StoreLease(lease.CaseId, lease.Token, operationKey);
            StatusMessage = $"Edit mode is active for this case until {lease.ExpiresAtUtc:u}.";
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or DbUpdateConcurrencyException)
        {
            ClearLease();
            StatusMessage = "Edit mode could not be entered because the case changed or is being edited by another member of staff.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRenewLeaseAsync(
        Guid caseId,
        long expectedCaseVersion,
        string operationKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (!TryGetLease(caseId, out var leaseToken))
        {
            StatusMessage = "Edit mode is unavailable. Enter edit mode again.";
            return RedirectToPage();
        }

        try
        {
            var lease = await renewCaseEditLease.ExecuteAsync(
                new(caseId, expectedCaseVersion, actor, operationKey, leaseToken),
                cancellationToken);
            StoreLease(lease.CaseId, lease.Token, operationKey);
            StatusMessage = $"Edit mode was renewed until {lease.ExpiresAtUtc:u}.";
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or DbUpdateConcurrencyException)
        {
            ClearLease();
            StatusMessage = "Edit mode could not be renewed. Refresh and enter edit mode again.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostReleaseLeaseAsync(
        Guid caseId,
        string operationKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (!TryGetLease(caseId, out var leaseToken))
        {
            ClearLease();
            StatusMessage = "Edit mode was already inactive.";
            return RedirectToPage();
        }

        try
        {
            await releaseCaseEditLease.ExecuteAsync(
                new(caseId, actor, operationKey, leaseToken),
                cancellationToken);
            StatusMessage = "Edit mode was left safely.";
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or DbUpdateConcurrencyException)
        {
            StatusMessage = "Edit mode could not be released cleanly; it will expire automatically.";
        }
        finally
        {
            ClearLease();
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRevokePegasusAsync(
        Guid requestId,
        Guid caseId,
        long expectedVersion,
        long expectedCaseVersion,
        string reason,
        string operationKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (!ModelState.IsValid || requestId == Guid.Empty || caseId == Guid.Empty ||
            !TryGetLease(caseId, out var leaseToken))
        {
            PreserveReason(requestId, reason);
            StatusMessage = "The Pegasus upload-link withdrawal requires current edit mode. Refresh and try again.";
            return RedirectToPage();
        }

        try
        {
            await revokeRequestUploadLink.ExecuteAsync(
                new(caseId, requestId, actor, reason, operationKey, expectedVersion, expectedCaseVersion, leaseToken),
                cancellationToken);
            ClearLease();
            StatusMessage = "The Pegasus upload link was withdrawn.";
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (ArgumentException)
        {
            PreserveReason(requestId, reason);
            StatusMessage = "The Pegasus upload-link withdrawal was invalid. Refresh and try again.";
        }
        catch (InvalidOperationException)
        {
            PreserveReason(requestId, reason);
            StatusMessage = "The Pegasus upload link or edit mode changed before withdrawal. Refresh and try again.";
        }
        catch (DbUpdateConcurrencyException)
        {
            PreserveReason(requestId, reason);
            StatusMessage = "The Pegasus upload link changed before withdrawal. Refresh and try again.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRevokeLinkAsync(
        Guid requestId,
        Guid caseId,
        long expectedVersion,
        long expectedCaseVersion,
        string reason,
        string operationKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (!ModelState.IsValid || requestId == Guid.Empty || caseId == Guid.Empty)
        {
            PreserveReason(requestId, reason);
            StatusMessage = "The link could not be withdrawn. Refresh and try again.";
            return RedirectToPage();
        }

        var leaseOperationKey = NewOperationKey();
        CaseEditLease lease;
        try
        {
            lease = await acquireCaseEditLease.ExecuteAsync(
                new(caseId, expectedCaseVersion, actor, leaseOperationKey),
                cancellationToken);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or DbUpdateConcurrencyException)
        {
            PreserveReason(requestId, reason);
            StatusMessage = "This link's case is open for editing by someone else. Try again in a few minutes.";
            return RedirectToPage();
        }

        try
        {
            await revokeRequestUploadLink.ExecuteAsync(
                new(caseId, requestId, actor, reason, operationKey, expectedVersion, expectedCaseVersion, lease.Token),
                cancellationToken);
            StatusMessage = "The link was withdrawn.";
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or DbUpdateConcurrencyException)
        {
            PreserveReason(requestId, reason);
            StatusMessage = "The link changed before it could be withdrawn. Refresh and try again.";
            await ReleaseQuietlyAsync(caseId, actor, lease.Token, cancellationToken);
            return RedirectToPage();
        }

        await ReleaseQuietlyAsync(caseId, actor, lease.Token, cancellationToken);
        return RedirectToPage();
    }

    public static string StateLabel(RequestOperationState state) => state switch
    {
        RequestOperationState.Pending => "Pending",
        RequestOperationState.Active => "Active",
        RequestOperationState.Expired => "Expired",
        RequestOperationState.Exhausted => "Exhausted",
        RequestOperationState.Revoked => "Revoked",
        RequestOperationState.Failed => "Failed",
        RequestOperationState.Completed => "Completed",
        RequestOperationState.UnknownExternal => "Unknown external",
        _ => throw new InvalidOperationException($"Unknown request operation state value '{(int)state}'.")
    };

    public static string NewOperationKey() => Guid.NewGuid().ToString("N");

    private async Task ReleaseQuietlyAsync(
        Guid caseId,
        ActionActor actor,
        string leaseToken,
        CancellationToken cancellationToken)
    {
        try
        {
            await releaseCaseEditLease.ExecuteAsync(
                new(caseId, actor, NewOperationKey(), leaseToken),
                cancellationToken);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or DbUpdateConcurrencyException)
        {
        }

        ClearLease();
    }

    private void StoreLease(Guid caseId, string token, string? operationKey = null)
    {
        TempData[LeaseCaseIdKey] = caseId;
        TempData[LeaseTokenKey] = new[] { token };
        if (!string.IsNullOrWhiteSpace(operationKey))
        {
            TempData[LeaseOperationKeyKey] = new[] { operationKey.Trim() };
        }
    }

    private void ClearLease()
    {
        TempData.Remove(LeaseCaseIdKey);
        TempData.Remove(LeaseTokenKey);
        TempData.Remove(LeaseOperationKeyKey);
    }

    private Guid? ReadLeaseCaseId()
    {
        var caseId = ReadGuidTempData(LeaseCaseIdKey, peek: true);
        var token = ReadStringTempData(LeaseTokenKey, peek: true);
        var operationKey = ReadStringTempData(LeaseOperationKeyKey, peek: true);
        if (caseId is not null && !string.IsNullOrWhiteSpace(token) && !string.IsNullOrWhiteSpace(operationKey))
        {
            TempData.Keep(LeaseCaseIdKey);
            TempData.Keep(LeaseTokenKey);
            TempData.Keep(LeaseOperationKeyKey);
            return caseId;
        }

        ClearLease();
        return null;
    }

    private void RestoreLeaseState(ActionActor actor)
    {
        var storedCaseId = ReadLeaseCaseId();
        var storedOperationKey = ReadStringTempData(LeaseOperationKeyKey, peek: true);
        if (storedCaseId is { } caseId && Operations.Items.Any(item =>
                item.CaseId == caseId &&
                item.CaseEditLeaseState == RequestCaseEditLeaseState.Active &&
                item.ActiveEditLease is { } activeLease &&
                string.Equals(activeLease.Holder, actor.SubjectId, StringComparison.Ordinal) &&
                string.Equals(activeLease.OperationKey, storedOperationKey, StringComparison.Ordinal)))
        {
            LeaseCaseId = caseId;
            return;
        }

        ClearLease();
        RecoverableLeaseCaseIds = Operations.Items
            .Where(item => item.CanRevoke &&
                item.CaseEditLeaseState == RequestCaseEditLeaseState.Active &&
                item.ActiveEditLease is { } activeLease &&
                string.Equals(activeLease.Holder, actor.SubjectId, StringComparison.Ordinal))
            .Select(item => item.CaseId)
            .ToImmutableHashSet();
    }

    private Guid? ReadGuidTempData(string key, bool peek = false)
    {
        var value = peek ? TempData.Peek(key) : TempData[key];
        return value switch
        {
            Guid parsed => parsed,
            string text when Guid.TryParse(text, out var parsed) => parsed,
            _ => null
        };
    }

    private string? ReadStringTempData(string key, bool peek = false)
    {
        var value = peek ? TempData.Peek(key) : TempData[key];
        return value switch
        {
            string text when !string.IsNullOrWhiteSpace(text) => text,
            string[] values when values.Length == 1 && !string.IsNullOrWhiteSpace(values[0]) => values[0],
            _ => null
        };
    }

    private bool TryGetLease(Guid caseId, out string leaseToken)
    {
        var storedCaseId = ReadLeaseCaseId();
        var storedToken = ReadStringTempData(LeaseTokenKey, peek: true);
        if (storedCaseId == caseId && !string.IsNullOrWhiteSpace(storedToken))
        {
            leaseToken = storedToken;
            return true;
        }

        leaseToken = string.Empty;
        return false;
    }

    private void PreserveReason(Guid requestId, string? reason)
    {
        if (requestId == Guid.Empty || string.IsNullOrWhiteSpace(reason))
        {
            return;
        }

        var normalized = reason.Trim();
        if (normalized.Length > 500)
        {
            return;
        }

        TempData[PreservedRequestIdKey] = requestId.ToString("D");
        TempData[PreservedReasonKey] = normalized;
    }

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
