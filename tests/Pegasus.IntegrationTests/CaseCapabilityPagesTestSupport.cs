using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The harness the capability-page tests share: a workspace client that has already entered edit
/// mode, the recording store substituted for every port the page under test calls, and the two
/// refusal paths every page inherits from <c>CaseMutationPageModel</c>.
/// </summary>
public sealed partial class CaseDetailsWebTests
{
    private static async Task<LeasedWorkspace> EnterEditModeAsync(
        RecordingCaseDetailsStore store,
        Action<IServiceCollection> substitutePorts,
        Action<IWebHostBuilder>? configureWebHost = null)
    {
        var baseFactory = new IntakeWebApplicationFactory();
        var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                Substitute<IGetCase>(services, store);
                Substitute<IAcquireCaseEditLease>(services, store);
                substitutePorts(services);
            });
            configureWebHost?.Invoke(builder);
        });
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        var initial = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        using var claim = await client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=ClaimLease",
            Form(
                AntiforgeryValue(initial),
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", InputValue(initial, "operationKey"))));
        AssertPrg(claim, store.CaseId);
        var leased = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        Assert.Equal(store.LeaseToken, InputValue(leased, "editLeaseToken"));
        return new(baseFactory, factory, client, store, AntiforgeryValue(leased));
    }

    private static void Substitute<T>(IServiceCollection services, T instance)
        where T : class
    {
        services.RemoveAll<T>();
        services.AddSingleton(instance);
    }

    /// <summary>
    /// A command the case refuses for a reason other than a lost lease or a stale version reports
    /// the refusal and keeps this browser in edit mode, so the editor can correct and resubmit.
    /// </summary>
    private static async Task AssertRefusalKeepsEditModeAsync(
        LeasedWorkspace workspace,
        string route,
        HttpContent form)
    {
        workspace.Store.NextFailure = new InvalidOperationException("The case refused the command.");
        using var refused = await workspace.PostAsync(route, form);
        AssertPrg(refused, workspace.Store.CaseId);
        var html = await workspace.GetWorkspaceAsync();
        Assert.Contains("role=\"alert\"", html, StringComparison.Ordinal);
        Assert.Equal(workspace.Store.LeaseToken, InputValue(html, "editLeaseToken"));
    }

    /// <summary>
    /// A lost lease or a stale version makes the editor reacquire rather than resubmit: the page
    /// forgets this browser's edit mode and reports the refusal.
    /// </summary>
    private static async Task AssertLostLeaseClearsEditModeAsync(
        LeasedWorkspace workspace,
        string route,
        HttpContent form)
    {
        workspace.Store.NextFailure =
            new CaseEditLeaseExpiredException(workspace.Store.CaseId, workspace.Store.CaseVersion);
        using var refused = await workspace.PostAsync(route, form);
        AssertPrg(refused, workspace.Store.CaseId);
        var html = await workspace.GetWorkspaceAsync();
        Assert.Contains("role=\"alert\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"editLeaseToken\"", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// The port received the leased workspace's envelope: the claimant, the case and its version,
    /// the lease token, and the operation key and reason the form carried.
    /// </summary>
    private static void AssertLeasedMutation(
        LeasedWorkspace workspace,
        CaseMutationRequest request,
        string operationKey,
        string reason)
    {
        AssertClaimant(workspace, request.Actor);
        Assert.Equal(workspace.Store.CaseId, request.CaseId);
        Assert.Equal(workspace.Store.CaseVersion, request.ExpectedVersion);
        Assert.Equal(workspace.Store.LeaseToken, request.EditLeaseToken);
        Assert.Equal(operationKey, request.OperationKey);
        Assert.Equal(reason, request.Reason);
    }

    /// <summary>The actor a port recorded is the staff member who entered edit mode.</summary>
    private static void AssertClaimant(LeasedWorkspace workspace, ActionActor recordedActor)
    {
        var claimant = workspace.Claimant;
        Assert.Equal(claimant.Kind, recordedActor.Kind);
        Assert.Equal(claimant.SubjectId, recordedActor.SubjectId);
        Assert.Equal(claimant.Roles.OrderBy(role => role), recordedActor.Roles.OrderBy(role => role));
    }

    private sealed class LeasedWorkspace(
        IntakeWebApplicationFactory baseFactory,
        WebApplicationFactory<Program> factory,
        HttpClient client,
        RecordingCaseDetailsStore store,
        string antiforgeryToken) : IDisposable
    {
        public HttpClient Client { get; } = client;

        public RecordingCaseDetailsStore Store { get; } = store;

        public string AntiforgeryToken { get; } = antiforgeryToken;

        public ActionActor Claimant => Assert.Single(Store.Claims).Actor;

        public Task<HttpResponseMessage> PostAsync(string route, HttpContent content) =>
            Client.PostAsync($"/Cases/{Store.CaseId:D}/{route}", content);

        public Task<string> GetWorkspaceAsync() => GetHtmlAsync(Client, $"/Cases/{Store.CaseId:D}");

        public FormUrlEncodedContent MutationForm(
            string operationKey,
            string reason,
            params (string Name, string Value)[] fields) =>
            LifecycleForm(AntiforgeryToken, Store, operationKey, reason, fields);

        public void Dispose()
        {
            Client.Dispose();
            factory.Dispose();
            baseFactory.Dispose();
        }
    }

    private sealed partial class RecordingCaseDetailsStore
    {
        /// <summary>Armed by a test so the next capability-page command is refused once.</summary>
        public Exception? NextFailure { get; set; }

        private void ThrowNextFailure()
        {
            if (NextFailure is { } failure)
            {
                NextFailure = null;
                throw failure;
            }
        }
    }
}
