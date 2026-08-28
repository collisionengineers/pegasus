using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Pegasus.Core.Workflow;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The workspace's own edit-mode actions that stay on <c>DetailsModel</c>: renewing the lease
/// and leaving it. Claiming and recovery are covered by the workspace tests.
/// </summary>
public sealed partial class CaseDetailsWebTests
{
    [Fact]
    public async Task WorkspaceRenewsAndLeavesEditModeWithTheOperationKeysItRendered()
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await EnterEditModeAsync(store, services =>
        {
            Substitute<IRenewCaseEditLease>(services, store);
            Substitute<IReleaseCaseEditLease>(services, store);
        });
        var leased = await workspace.GetWorkspaceAsync();
        var renewKey = HandlerFormInputValue(leased, "RenewLease", "operationKey");
        var releaseKey = HandlerFormInputValue(leased, "ReleaseLease", "operationKey");

        using var renewed = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=RenewLease",
            Form(
                workspace.AntiforgeryToken,
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", renewKey),
                ("editLeaseToken", store.LeaseToken)));
        AssertPrg(renewed, store.CaseId);
        var renewal = Assert.Single(store.LeaseRenewals);
        AssertClaimant(workspace, renewal.Actor);
        Assert.Equal(store.CaseId, renewal.CaseId);
        Assert.Equal(store.CaseVersion, renewal.ExpectedVersion);
        Assert.Equal(store.LeaseToken, renewal.LeaseToken);
        Assert.Equal(renewKey, renewal.OperationKey);
        var afterRenewal = await workspace.GetWorkspaceAsync();
        Assert.Contains("Edit mode was renewed.", afterRenewal, StringComparison.Ordinal);
        Assert.Equal(store.RenewedLeaseToken, InputValue(afterRenewal, "editLeaseToken"));
        Assert.NotEqual(renewKey, HandlerFormInputValue(afterRenewal, "RenewLease", "operationKey"));

        // A refusal that is not a lost lease keeps edit mode and the same renew key for the retry.
        store.NextFailure = new InvalidOperationException("The lease store is unavailable.");
        var retryKey = HandlerFormInputValue(afterRenewal, "RenewLease", "operationKey");
        using var refused = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=RenewLease",
            Form(
                workspace.AntiforgeryToken,
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", retryKey),
                ("editLeaseToken", store.RenewedLeaseToken)));
        AssertPrg(refused, store.CaseId);
        var afterRefusal = await workspace.GetWorkspaceAsync();
        Assert.Contains("Edit mode could not be renewed", afterRefusal, StringComparison.Ordinal);
        Assert.Equal(store.RenewedLeaseToken, InputValue(afterRefusal, "editLeaseToken"));
        Assert.Equal(retryKey, HandlerFormInputValue(afterRefusal, "RenewLease", "operationKey"));

        using var left = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=ReleaseLease",
            Form(
                workspace.AntiforgeryToken,
                ("id", store.CaseId.ToString("D")),
                ("operationKey", releaseKey),
                ("editLeaseToken", store.RenewedLeaseToken)));
        AssertPrg(left, store.CaseId);
        var release = Assert.Single(store.LeaseReleases);
        AssertClaimant(workspace, release.Actor);
        Assert.Equal(store.CaseId, release.CaseId);
        Assert.Equal(store.RenewedLeaseToken, release.LeaseToken);
        Assert.Equal(releaseKey, release.OperationKey);
        var afterRelease = await workspace.GetWorkspaceAsync();
        Assert.Contains("Edit mode was left safely.", afterRelease, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"editLeaseToken\"", afterRelease, StringComparison.Ordinal);
        Assert.Contains("handler=ClaimLease", afterRelease, StringComparison.Ordinal);
    }

    /// <summary>
    /// CASE-024: the workspace renders a heartbeat form so an open editor is never timed out
    /// mid-edit, and answers it without a redirect, a status message, or - crucially - any
    /// TempData write. TempData here is cookie-backed, so a beat that re-issued that cookie could
    /// race a form post the operator did make and lose them the token they are editing under.
    /// </summary>
    [Fact]
    public async Task WorkspaceHeartbeatKeepsEditingAliveWithoutDisturbingTheOperatorsLeaseState()
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await EnterEditModeAsync(store, services =>
        {
            Substitute<IHeartbeatCaseEditLease>(services, store);
        });
        var leased = await workspace.GetWorkspaceAsync();
        var renewKey = HandlerFormInputValue(leased, "RenewLease", "operationKey");
        Assert.Contains("data-edit-heartbeat", leased, StringComparison.Ordinal);
        Assert.Contains(
            $"data-heartbeat-seconds=\"{(int)CaseEditAuthority.HeartbeatInterval.TotalSeconds}\"",
            leased,
            StringComparison.Ordinal);

        using var beat = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=HeartbeatLease",
            Form(
                workspace.AntiforgeryToken,
                ("id", store.CaseId.ToString("D")),
                ("editLeaseToken", store.LeaseToken)));

        Assert.Equal(HttpStatusCode.NoContent, beat.StatusCode);
        var heartbeat = Assert.Single(store.LeaseHeartbeats);
        AssertClaimant(workspace, heartbeat.Actor);
        Assert.Equal(store.CaseId, heartbeat.CaseId);
        Assert.Equal(store.LeaseToken, heartbeat.LeaseToken);

        // The operator is exactly where they were: same token, same keys, no message.
        var afterBeat = await workspace.GetWorkspaceAsync();
        Assert.Equal(store.LeaseToken, InputValue(afterBeat, "editLeaseToken"));
        Assert.Equal(renewKey, HandlerFormInputValue(afterBeat, "RenewLease", "operationKey"));
        Assert.DoesNotContain("Edit mode was renewed", afterBeat, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WorkspaceHeartbeatReportsALostLeaseAndIsRefusedWithoutItsAntiforgeryToken()
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await EnterEditModeAsync(store, services =>
        {
            Substitute<IHeartbeatCaseEditLease>(services, store);
        });

        store.NextFailure = new CaseEditLeaseExpiredException(store.CaseId, store.CaseVersion);
        using var lost = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=HeartbeatLease",
            Form(
                workspace.AntiforgeryToken,
                ("id", store.CaseId.ToString("D")),
                ("editLeaseToken", store.LeaseToken)));

        // 409 is the browser's signal to stop beating; the page it lands on next already shows
        // the case's real edit state, so nothing is said here.
        Assert.Equal(HttpStatusCode.Conflict, lost.StatusCode);

        using var unprotected = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=HeartbeatLease",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["id"] = store.CaseId.ToString("D"),
                ["editLeaseToken"] = store.LeaseToken,
            }));

        Assert.Equal(HttpStatusCode.BadRequest, unprotected.StatusCode);
    }

    /// <summary>The value of one hidden input inside the form that posts to the named handler.</summary>
    private static string HandlerFormInputValue(string html, string handler, string name)
    {
        var form = Regex.Match(
            html,
            $"<form[^>]*handler={Regex.Escape(handler)}[^>]*>.*?</form>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(form.Success, $"The workspace must render the '{handler}' form.");
        return InputValue(form.Value, name);
    }

    private sealed partial class RecordingCaseDetailsStore :
        IRenewCaseEditLease,
        IHeartbeatCaseEditLease,
        IReleaseCaseEditLease
    {
        public string RenewedLeaseToken { get; } = "opaque-renewed-case-lease";
        public List<RenewCaseEditLeaseRequest> LeaseRenewals { get; } = [];
        public List<HeartbeatCaseEditLeaseRequest> LeaseHeartbeats { get; } = [];
        public List<ReleaseCaseEditLeaseRequest> LeaseReleases { get; } = [];

        Task<CaseEditLease> IHeartbeatCaseEditLease.ExecuteAsync(
            HeartbeatCaseEditLeaseRequest request,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            LeaseHeartbeats.Add(request);
            return Task.FromResult(new CaseEditLease(
                request.CaseId,
                request.LeaseToken,
                request.Actor.SubjectId,
                CaseVersion,
                _now.AddMinutes(5)));
        }

        Task<CaseEditLease> IRenewCaseEditLease.ExecuteAsync(
            RenewCaseEditLeaseRequest request,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            LeaseRenewals.Add(request);
            return Task.FromResult(new CaseEditLease(
                request.CaseId,
                RenewedLeaseToken,
                request.Actor.SubjectId,
                request.ExpectedVersion,
                _now.AddMinutes(10)));
        }

        Task IReleaseCaseEditLease.ExecuteAsync(
            ReleaseCaseEditLeaseRequest request,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            LeaseReleases.Add(request);
            _leaseHolder = null;
            _leaseOperationKey = null;
            return Task.CompletedTask;
        }
    }
}
