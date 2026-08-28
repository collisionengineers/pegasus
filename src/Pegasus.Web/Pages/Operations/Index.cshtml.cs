using System.Collections.Immutable;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    RetryExternalWork retryExternalWork,
    IAcquireCaseEditLease acquireCaseEditLease,
    IReleaseCaseEditLease releaseCaseEditLease,
    IRevokeRequestUploadLink revokeRequestUploadLink,
    TimeProvider timeProvider,
    GetServiceHealth? getServiceHealth = null) : StaffPageModel
{
    private const string PreservedReasonKey = "OperationsRequestReason";
    private const string PreservedRequestIdKey = "OperationsRequestReasonId";

    private readonly GetRequestOperations getRequestOperations =
        getRequestOperations ?? throw new ArgumentNullException(nameof(getRequestOperations));
    private readonly RetryExternalWork retryExternalWork =
        retryExternalWork ?? throw new ArgumentNullException(nameof(retryExternalWork));
    private readonly IAcquireCaseEditLease acquireCaseEditLease =
        acquireCaseEditLease ?? throw new ArgumentNullException(nameof(acquireCaseEditLease));
    private readonly IReleaseCaseEditLease releaseCaseEditLease =
        releaseCaseEditLease ?? throw new ArgumentNullException(nameof(releaseCaseEditLease));
    private readonly IRevokeRequestUploadLink revokeRequestUploadLink =
        revokeRequestUploadLink ?? throw new ArgumentNullException(nameof(revokeRequestUploadLink));
    private readonly TimeProvider timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <summary>
    /// When this list was last read. Set only after the query returns, so a
    /// failed load never claims to be fresh (FRD-12).
    /// </summary>
    public DateTimeOffset? LoadedAtUtc { get; private set; }

    public RequestOperationsProjection Operations { get; private set; } = new(
        ImmutableArray<RequestOperationProjection>.Empty,
        LimitReached: false);

    /// <summary>
    /// The Service health snapshot, when this deployment composes it. The
    /// query is registered with the Automation Actor ingress, so a deployment
    /// without that feature carries no snapshot and the page shows no Service
    /// health section — an uncomposed capability is absent, never broken.
    /// </summary>
    public ServiceHealthSnapshot? ServiceHealth { get; private set; }

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
        PreservedRequestId = ReadGuidTempData(PreservedRequestIdKey);
        PreservedReason = TempData[PreservedReasonKey] as string;
        if (getServiceHealth is not null)
        {
            ServiceHealth = await getServiceHealth.ExecuteAsync(actor, cancellationToken);
        }
        LoadedAtUtc = timeProvider.GetUtcNow();
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
            StatusMessage = "This link's case is open for editing by someone else.";
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

    public static string StateLabel(RequestOperationState state) =>
        Presentation.OperatorLabels.RequestOperationState(state);

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

}
