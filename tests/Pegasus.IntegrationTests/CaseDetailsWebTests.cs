using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Actors;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Tasks;
using Pegasus.Core.Workflow;

namespace Pegasus.IntegrationTests;

[Collection(LocalDbFixtureDefinition.Name)]
public sealed partial class CaseDetailsWebTests
{
    [Fact]
    public async Task ManualChasePostUsesAntiforgeryServerActorLiveLeaseVersionAndReplayKey()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingCaseDetailsStore();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetCase>();
                services.RemoveAll<IAcquireCaseEditLease>();
                services.RemoveAll<IRecordManualCaseChase>();
                services.AddSingleton<IGetCase>(store);
                services.AddSingleton<IAcquireCaseEditLease>(store);
                services.AddSingleton<IRecordManualCaseChase>(store);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var initialHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        var claimOperationKey = InputValue(initialHtml, "operationKey");
        using var claimResponse = await client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=ClaimLease",
            Form(
                AntiforgeryValue(initialHtml),
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", claimOperationKey)));
        AssertPrg(claimResponse, store.CaseId);
        Assert.Single(store.Claims);
        Assert.Equal(claimOperationKey, store.Claims[0].OperationKey);

        using (var recoveryClient = factory.CreateClient(new WebApplicationFactoryClientOptions
               {
                   AllowAutoRedirect = false,
                   BaseAddress = new Uri("https://localhost")
               }))
        {
            var recoveryHtml = await GetHtmlAsync(recoveryClient, $"/Cases/{store.CaseId:D}");
            Assert.Contains("Recover edit mode", recoveryHtml, StringComparison.Ordinal);
            Assert.Equal(claimOperationKey, InputValue(recoveryHtml, "operationKey"));
            using var recoveryResponse = await recoveryClient.PostAsync(
                $"/Cases/{store.CaseId:D}?handler=ClaimLease",
                Form(
                    AntiforgeryValue(recoveryHtml),
                    ("id", store.CaseId.ToString("D")),
                    ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                    ("operationKey", claimOperationKey)));
            AssertPrg(recoveryResponse, store.CaseId);
        }
        Assert.Equal(2, store.Claims.Count);
        Assert.Equal(store.Claims[0].OperationKey, store.Claims[1].OperationKey);

        var leasedHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        var refreshedHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        Assert.Equal(
            InputValue(leasedHtml, "editLeaseToken"),
            InputValue(refreshedHtml, "editLeaseToken"));
        leasedHtml = refreshedHtml;
        Assert.Contains("Record manual chase", leasedHtml, StringComparison.Ordinal);
        var operationKey = "manual-chase-replay";
        var attemptedAtUtc = InputValue(leasedHtml, "attemptedAtUtc");
        using var firstResponse = await client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=RecordManualChase",
            ManualChaseForm(AntiforgeryValue(leasedHtml), store, operationKey, attemptedAtUtc));
        AssertPrg(firstResponse, store.CaseId);

        var currentHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        Assert.Contains("Telephone", currentHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"editLeaseToken\"", currentHtml, StringComparison.Ordinal);
        Assert.Contains("Awaiting requested photographs", currentHtml, StringComparison.Ordinal);
        using var replayResponse = await client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=RecordManualChase",
            ManualChaseForm(AntiforgeryValue(currentHtml), store, operationKey, attemptedAtUtc));
        AssertPrg(replayResponse, store.CaseId);

        Assert.Equal(2, store.ManualChases.Count);
        var command = store.ManualChases[0];
        var replay = store.ManualChases[1];
        Assert.Equal(command with { Actor = replay.Actor }, replay);
        Assert.Equal(command.Actor.Kind, replay.Actor.Kind);
        Assert.Equal(command.Actor.SubjectId, replay.Actor.SubjectId);
        Assert.Equal(command.Actor.Roles, replay.Actor.Roles);
        Assert.Equal(store.CaseId, command.CaseId);
        Assert.Equal(store.CaseVersion, command.ExpectedCaseVersion);
        Assert.Equal(store.LeaseToken, command.EditLeaseToken);
        Assert.Equal(operationKey, command.OperationKey);
        Assert.Equal(ActorKind.Staff, command.Actor.Kind);
        Assert.Equal(
            DateTimeOffset.Parse(attemptedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            command.AttemptedAtUtc);
        Assert.NotEmpty(command.Actor.Roles);
        Assert.Equal("Telephone", command.Channel);
        Assert.Equal("Provider claims team", command.TargetPartyOrAddress);
        Assert.Equal("Awaiting requested photographs", command.Outcome);
        Assert.Equal("Asked provider for missing images", command.Note);
        Assert.Equal("Missing evidence follow-up", command.Reason);
    }

    [Fact]
    public async Task LifecyclePostsBindHoldReleaseAndReportPreparationToAuthenticatedLease()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingCaseDetailsStore();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetCase>();
                services.RemoveAll<IAcquireCaseEditLease>();
                services.RemoveAll<IHoldCase>();
                services.RemoveAll<IReleaseCase>();
                services.RemoveAll<ITransitionCase>();
                services.AddSingleton<IGetCase>(store);
                services.AddSingleton<IAcquireCaseEditLease>(store);
                services.AddSingleton<IHoldCase>(store);
                services.AddSingleton<IReleaseCase>(store);
                services.AddSingleton<ITransitionCase>(store);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var initialHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        using var claimResponse = await client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=ClaimLease",
            Form(
                AntiforgeryValue(initialHtml),
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", InputValue(initialHtml, "operationKey"))));
        AssertPrg(claimResponse, store.CaseId);

        var leasedHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        Assert.Contains("Hold case", leasedHtml, StringComparison.Ordinal);
        Assert.Contains("Release hold", leasedHtml, StringComparison.Ordinal);
        Assert.Contains("Transition to report preparation", leasedHtml, StringComparison.Ordinal);
        var antiforgeryToken = AntiforgeryValue(leasedHtml);
        using var holdResponse = await client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=Hold",
            LifecycleForm(antiforgeryToken, store, "hold-case", "Awaiting provider"));
        using var releaseResponse = await client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=ReleaseHold",
            LifecycleForm(antiforgeryToken, store, "release-case", "Provider replied"));
        using var startResponse = await client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=StartWork",
            LifecycleForm(antiforgeryToken, store, "start-report-preparation", "Engineer work started"));

        AssertPrg(holdResponse, store.CaseId);
        AssertPrg(releaseResponse, store.CaseId);
        AssertPrg(startResponse, store.CaseId);
        var actorSubjectId = Assert.Single(store.Claims).Actor.SubjectId;
        var hold = Assert.Single(store.Holds);
        var release = Assert.Single(store.Releases);
        var transition = Assert.Single(store.Transitions);
        Assert.Equal(actorSubjectId, hold.Actor.SubjectId);
        Assert.Equal(actorSubjectId, release.Actor.SubjectId);
        Assert.Equal(actorSubjectId, transition.Actor.SubjectId);
        Assert.Equal(store.CaseVersion, hold.ExpectedVersion);
        Assert.Equal(store.CaseVersion, release.ExpectedVersion);
        Assert.Equal(store.CaseVersion, transition.ExpectedVersion);
        Assert.Equal(store.LeaseToken, hold.EditLeaseToken);
        Assert.Equal(store.LeaseToken, release.EditLeaseToken);
        Assert.Equal(store.LeaseToken, transition.EditLeaseToken);
        Assert.Equal("hold-case", hold.OperationKey);
        Assert.Equal("release-case", release.OperationKey);
        Assert.Equal("start-report-preparation", transition.OperationKey);
        Assert.Equal(CaseTransitionDestination.ReportPreparation, transition.Destination);
    }

    [Fact]
    public async Task WrongHolderProjectionClearsProtectedLeaseAuthorityAndFallsBackToRecovery()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingCaseDetailsStore();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetCase>();
                services.RemoveAll<IAcquireCaseEditLease>();
                services.AddSingleton<IGetCase>(store);
                services.AddSingleton<IAcquireCaseEditLease>(store);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var initialHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        var claimOperationKey = InputValue(initialHtml, "operationKey");
        using var claimResponse = await client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=ClaimLease",
            Form(
                AntiforgeryValue(initialHtml),
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", claimOperationKey)));
        AssertPrg(claimResponse, store.CaseId);

        var claimant = Assert.Single(store.Claims).Actor.SubjectId;
        store.LeaseHolder = "different-staff";
        var wrongHolderHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        Assert.Contains("Another staff member currently holds edit authority", wrongHolderHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"editLeaseToken\"", wrongHolderHtml, StringComparison.Ordinal);

        store.LeaseHolder = claimant;
        var recoveryHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        Assert.Contains("Recover edit mode", recoveryHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"editLeaseToken\"", recoveryHtml, StringComparison.Ordinal);
        Assert.Equal(claimOperationKey, InputValue(recoveryHtml, "operationKey"));
    }

    private static FormUrlEncodedContent ManualChaseForm(
        string antiforgeryToken,
        RecordingCaseDetailsStore store,
        string operationKey,
        string attemptedAtUtc) => Form(
            antiforgeryToken,
            ("id", store.CaseId.ToString("D")),
            ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
            ("operationKey", operationKey),
            ("editLeaseToken", store.LeaseToken),
            ("reason", "Missing evidence follow-up"),
            ("attemptedAtUtc", attemptedAtUtc),
            ("channel", "Telephone"),
            ("targetPartyOrAddress", "Provider claims team"),
            ("outcome", "Awaiting requested photographs"),
            ("note", "Asked provider for missing images"));

    private static FormUrlEncodedContent LifecycleForm(
        string antiforgeryToken,
        RecordingCaseDetailsStore store,
        string operationKey,
        string reason) => Form(
            antiforgeryToken,
            ("id", store.CaseId.ToString("D")),
            ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
            ("operationKey", operationKey),
            ("editLeaseToken", store.LeaseToken),
            ("reason", reason));

    private static async Task<string> GetHtmlAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static FormUrlEncodedContent Form(
        string antiforgeryToken,
        params (string Name, string Value)[] values)
    {
        var fields = values.ToDictionary(item => item.Name, item => item.Value, StringComparer.Ordinal);
        fields["__RequestVerificationToken"] = antiforgeryToken;
        return new(fields);
    }

    private static string InputValue(string html, string name)
    {
        var tag = Regex.Match(
            html,
            $"<input[^>]*name=\\\"{Regex.Escape(name)}\\\"[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(tag.Success, $"The case action must render '{name}'.");
        var value = ValueRegex().Match(tag.Value);
        Assert.True(value.Success, $"The case field '{name}' must have a value.");
        return WebUtility.HtmlDecode(value.Groups["value"].Value);
    }

    private static string AntiforgeryValue(string html)
    {
        var tag = AntiforgeryTagRegex().Match(html);
        Assert.True(tag.Success, "The case action must render an antiforgery token.");
        var value = ValueRegex().Match(tag.Value);
        Assert.True(value.Success, "The case antiforgery token must have a value.");
        return WebUtility.HtmlDecode(value.Groups["value"].Value);
    }

    private static void AssertPrg(HttpResponseMessage response, Guid caseId)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/Cases/{caseId:D}", response.Headers.Location?.OriginalString);
    }

    [GeneratedRegex("<input[^>]*name=\"__RequestVerificationToken\"[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AntiforgeryTagRegex();

    [GeneratedRegex("value=\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ValueRegex();

    private sealed class RecordingCaseDetailsStore :
        IGetCase,
        IAcquireCaseEditLease,
        IRecordManualCaseChase,
        IHoldCase,
        IReleaseCase,
        ITransitionCase
    {
        private readonly DateTimeOffset _now = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
        private CaseDueWork _dueWork;
        private string? _leaseHolder;
        private string? _leaseOperationKey;

        public RecordingCaseDetailsStore()
        {
            _dueWork = new(
                CaseId,
                "Vehicle images",
                new DateOnly(2031, 5, 10),
                CaseDueWorkState.Scheduled,
                _now.AddDays(1),
                null,
                null,
                null,
                null,
                null,
                3);
        }

        public Guid CaseId { get; } = Guid.NewGuid();

        public long CaseVersion { get; } = 7;

        public string LeaseToken { get; } = "opaque-live-case-lease";

        public List<ClaimCaseEditLeaseRequest> Claims { get; } = [];
        public string? LeaseHolder
        {
            get => _leaseHolder;
            set => _leaseHolder = value;
        }

        public List<ManualChaseRecord> ManualChases { get; } = [];
        public List<PutCaseOnHoldRequest> Holds { get; } = [];
        public List<CaseMutationRequest> Releases { get; } = [];
        public List<TransitionCaseRequest> Transitions { get; } = [];

        public Task<CaseDetails?> ExecuteAsync(GetCaseQuery query, CancellationToken cancellationToken)
        {
            var workflow = CreateWorkflow();
            var summary = new CaseSearchItem(
                CaseId,
                workflow.Identity.Reference,
                null,
                CaseType.Inspection,
                workflow.Identity.PrincipalCode,
                workflow.State,
                null,
                "AB12CDE",
                "Case claimant",
                "CLM-42",
                _now.AddDays(-2),
                new DateOnly(2031, 5, 5),
                "Email",
                _now.AddDays(-2));
            CaseDetails details = new(
                summary,
                workflow,
                _leaseHolder is null ? null : new(_leaseHolder, _now.AddMinutes(5), _leaseOperationKey!),
                [],
                [],
                [],
                [],
                []);
            return Task.FromResult<CaseDetails?>(details);
        }

        Task<CaseEditLease> IAcquireCaseEditLease.ExecuteAsync(
            ClaimCaseEditLeaseRequest request,
            CancellationToken cancellationToken)
        {
            _leaseHolder = request.Actor.SubjectId;
            _leaseOperationKey = request.OperationKey;
            Claims.Add(request);
            return Task.FromResult(
                new CaseEditLease(
                    request.CaseId,
                    LeaseToken,
                    request.Actor.SubjectId,
                    request.ExpectedVersion,
                    _now.AddMinutes(5)));
        }


        Task<CaseWorkflowRecord> IHoldCase.ExecuteAsync(
            PutCaseOnHoldRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Holds.Add(request);
            return Task.FromResult(CreateWorkflow() with { State = CaseLifecycleState.Held });
        }

        Task<CaseWorkflowRecord> IReleaseCase.ExecuteAsync(
            CaseMutationRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Releases.Add(request);
            return Task.FromResult(CreateWorkflow() with { State = CaseLifecycleState.Review });
        }

        Task<CaseWorkflowRecord> ITransitionCase.ExecuteAsync(
            TransitionCaseRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Transitions.Add(request);
            return Task.FromResult(CreateWorkflow() with
            {
                State = request.Destination == CaseTransitionDestination.ReportPreparation
                    ? CaseLifecycleState.ReportPreparation
                    : CaseLifecycleState.Review
            });
        }
        private CaseWorkflowRecord CreateWorkflow() =>
            new(
                CaseId,
                new(CaseId, "QDOS", 2031, 42, "QDOS3100042"),
                CaseLifecycleState.NotReady,
                null,
                null,
                null,
                _dueWork,
                null,
                null,
                null,
                CaseVersion);

        Task<CaseDueWork> IRecordManualCaseChase.ExecuteAsync(
            ManualChaseRecord request,
            CancellationToken cancellationToken)
        {
            ManualChases.Add(request);
            _dueWork = _dueWork with
            {
                NextChaseAtUtc = _now.AddDays(7),
                MostRecentChannel = request.Channel,
                MostRecentOutcome = request.Outcome,
                MostRecentNote = request.Note,
                Version = _dueWork.Version + 1
            };
            _leaseHolder = null;
            _leaseOperationKey = null;
            return Task.FromResult(_dueWork);
        }
    }
}
