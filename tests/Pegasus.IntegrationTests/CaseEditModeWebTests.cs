using System.Globalization;
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
        Assert.Contains("Edit mode was renewed until", afterRenewal, StringComparison.Ordinal);
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
        IReleaseCaseEditLease
    {
        public string RenewedLeaseToken { get; } = "opaque-renewed-case-lease";
        public List<RenewCaseEditLeaseRequest> LeaseRenewals { get; } = [];
        public List<ReleaseCaseEditLeaseRequest> LeaseReleases { get; } = [];

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
