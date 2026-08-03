using System.Collections.Immutable;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Actors;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
using Pegasus.Core.Workflow;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed partial class OperationsWebTests
{
    private static readonly DateTimeOffset FixedUtcNow =
        new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task ExactOperationsRoutesRenderBoundedStatesActionsAndDeepLinks()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingOperationsStore();
        using var factory = Configure(baseFactory, store);
        using var client = CreateClient(factory);

        using var emailResponse = await client.GetAsync("/Operations/Email");
        var emailHtml = await emailResponse.Content.ReadAsStringAsync();
        using var requestsResponse = await client.GetAsync("/Operations/Requests");
        var initialRequestsHtml = await requestsResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, emailResponse.StatusCode);
        Assert.Contains("Received", emailHtml, StringComparison.Ordinal);
        Assert.Contains("Sent", emailHtml, StringComparison.Ordinal);
        Assert.Contains("Pending", emailHtml, StringComparison.Ordinal);
        Assert.Contains("Succeeded", emailHtml, StringComparison.Ordinal);
        Assert.Contains("Failed", emailHtml, StringComparison.Ordinal);
        Assert.Contains("Unknown", emailHtml, StringComparison.Ordinal);
        Assert.Contains("Retry Received processing", emailHtml, StringComparison.Ordinal);
        Assert.Contains("Retry Sent processing", emailHtml, StringComparison.Ordinal);
        Assert.Contains("Open Intake receipt", emailHtml, StringComparison.Ordinal);
        Assert.Contains("Open Triage", emailHtml, StringComparison.Ordinal);
        Assert.Contains("Open Case", emailHtml, StringComparison.Ordinal);

        Assert.Equal(HttpStatusCode.OK, requestsResponse.StatusCode);
        foreach (var state in new[]
                 {
                     "Active", "Expired", "Exhausted", "Revoked", "Failed", "Pending", "Unknown external"
                 })
        {
            Assert.Contains(state, initialRequestsHtml, StringComparison.Ordinal);
        }
        Assert.Contains("Enter edit mode to revoke", initialRequestsHtml, StringComparison.Ordinal);
        Assert.Contains("Retry external work", initialRequestsHtml, StringComparison.Ordinal);
        Assert.Contains($"href=\"/Cases/{store.CaseId:D}\"", initialRequestsHtml, StringComparison.Ordinal);

        var claimOperationKey = OperationKeyValue(initialRequestsHtml);
        using var claim = await client.PostAsync(
            "/Operations/Requests?handler=ClaimLease",
            Form(
                AntiforgeryValue(initialRequestsHtml),
                ("caseId", store.CaseId.ToString("D")),
                ("expectedCaseVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", claimOperationKey)));
        AssertPrg(claim, "/Operations/Requests");
        Assert.Equal(claimOperationKey, store.LeaseOperationKey);

        using var leasedResponse = await client.GetAsync("/Operations/Requests");
        var leasedHtml = await leasedResponse.Content.ReadAsStringAsync();
        Assert.True(leasedResponse.Headers.CacheControl?.NoStore == true);
        Assert.DoesNotContain(store.LeaseToken, leasedHtml, StringComparison.Ordinal);
        Assert.Contains("Revoke Box file request", leasedHtml, StringComparison.Ordinal);
        Assert.Contains("Revoke Pegasus upload link", leasedHtml, StringComparison.Ordinal);
        Assert.Contains("Renew edit mode", leasedHtml, StringComparison.Ordinal);
        Assert.Contains("Leave edit mode", leasedHtml, StringComparison.Ordinal);
        Assert.Contains("__RequestVerificationToken", emailHtml, StringComparison.Ordinal);
        Assert.Contains("__RequestVerificationToken", leasedHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OperationsCockpitLinksBothExactWorkspaces()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        using var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("href=\"/Operations/Email\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/Operations/Requests\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OperationsPostsUseAntiforgeryServerActorLeaseVersionsAndPrg()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingOperationsStore();
        using var factory = Configure(baseFactory, store);
        using var client = CreateClient(factory);

        var emailHtml = await GetHtmlAsync(client, "/Operations/Email");
        using var emailPost = await client.PostAsync(
            "/Operations/Email?handler=Retry",
            Form(
                AntiforgeryValue(emailHtml),
                ("mailboxId", store.ReceivedMailboxId),
                ("direction", nameof(EmailOperationDirection.Received)),
                ("expectedFailureCode", store.MailboxFailureCode),
                ("expectedDueAtUtc", store.MailboxFailureDueAtUtc.ToString("O")),
                ("operationKey", "mailbox-retry")));

        var requestsHtml = await GetHtmlAsync(client, "/Operations/Requests");
        using var externalPost = await client.PostAsync(
            "/Operations/Requests?handler=RetryExternal",
            Form(
                AntiforgeryValue(requestsHtml),
                ("workItemId", store.ExternalWorkId.ToString("D")),
                ("expectedAttemptCount", store.ExternalAttemptCount.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", "external-retry")));

        using var revokeWithoutLease = await client.PostAsync(
            "/Operations/Requests?handler=RevokeBox",
            Form(
                AntiforgeryValue(requestsHtml),
                ("requestId", store.BoxRequestId.ToString("D")),
                ("caseId", store.CaseId.ToString("D")),
                ("expectedVersion", "3"),
                ("expectedCaseVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("reason", "No longer required"),
                ("operationKey", "box-revoke-without-lease")));
        AssertPrg(revokeWithoutLease, "/Operations/Requests");
        Assert.Null(store.BoxRevoke);

        await ClaimLeaseAsync(client, store);
        requestsHtml = await GetHtmlAsync(client, "/Operations/Requests");
        using var boxPost = await client.PostAsync(
            "/Operations/Requests?handler=RevokeBox",
            Form(
                AntiforgeryValue(requestsHtml),
                ("requestId", store.BoxRequestId.ToString("D")),
                ("caseId", store.CaseId.ToString("D")),
                ("expectedVersion", "3"),
                ("expectedCaseVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("reason", "No longer required"),
                ("operationKey", "box-revoke")));

        await ClaimLeaseAsync(client, store);
        requestsHtml = await GetHtmlAsync(client, "/Operations/Requests");
        using var pegasusPost = await client.PostAsync(
            "/Operations/Requests?handler=RevokePegasus",
            Form(
                AntiforgeryValue(requestsHtml),
                ("requestId", store.PegasusRequestId.ToString("D")),
                ("caseId", store.CaseId.ToString("D")),
                ("expectedVersion", "4"),
                ("expectedCaseVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("reason", "No longer required"),
                ("operationKey", "pegasus-revoke")));

        AssertPrg(emailPost, "/Operations/Email");
        AssertPrg(externalPost, "/Operations/Requests");
        AssertPrg(boxPost, "/Operations/Requests");
        AssertPrg(pegasusPost, "/Operations/Requests");
        Assert.Equal(ActorKind.Staff, store.MailboxRetry!.Actor.Kind);
        Assert.Equal(EmailOperationDirection.Received, store.MailboxRetry.Direction);
        Assert.Equal(store.MailboxFailureDueAtUtc, store.MailboxRetry.ExpectedDueAtUtc);
        Assert.Equal(ActorKind.Staff, store.ExternalRetry!.Actor.Kind);
        Assert.Equal(store.ExternalAttemptCount, store.ExternalRetry.ExpectedAttemptCount);
        Assert.Equal(ActorKind.Staff, store.BoxRevoke!.Actor.Kind);
        Assert.Equal(3, store.BoxRevoke.ExpectedFileRequestVersion);
        Assert.Equal(store.CaseVersion, store.BoxRevoke.ExpectedCaseVersion);
        Assert.Equal(store.LeaseToken, store.BoxRevoke.EditLeaseToken);
        Assert.Equal(ActorKind.Staff, store.PegasusRevoke!.Actor.Kind);
        Assert.Equal(4, store.PegasusRevoke.ExpectedRequestVersion);
        Assert.Equal(store.CaseVersion, store.PegasusRevoke.ExpectedCaseVersion);
        Assert.Equal(store.LeaseToken, store.PegasusRevoke.EditLeaseToken);
    }

    private static async Task ClaimLeaseAsync(
        HttpClient client,
        RecordingOperationsStore store)
    {
        var html = await GetHtmlAsync(client, "/Operations/Requests");
        using var response = await client.PostAsync(
            "/Operations/Requests?handler=ClaimLease",
            Form(
                AntiforgeryValue(html),
                ("caseId", store.CaseId.ToString("D")),
                ("expectedCaseVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", OperationKeyValue(html))));
        AssertPrg(response, "/Operations/Requests");
    }

    private static async Task<string> GetHtmlAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static WebApplicationFactory<Program> Configure(
        IntakeWebApplicationFactory baseFactory,
        RecordingOperationsStore store) => baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IEmailOperationsProjectionStore>();
                services.RemoveAll<IRequestOperationsProjectionStore>();
                services.RemoveAll<IMailboxProcessingRetryStore>();
                services.RemoveAll<IExternalWorkRetryStore>();
                services.RemoveAll<IAcquireCaseEditLease>();
                services.RemoveAll<IRenewCaseEditLease>();
                services.RemoveAll<IReleaseCaseEditLease>();
                services.RemoveAll<IRevokeBoxFileRequest>();
                services.RemoveAll<IRevokeRequestUploadLink>();
                services.AddSingleton<IEmailOperationsProjectionStore>(store);
                services.AddSingleton<IRequestOperationsProjectionStore>(store);
                services.AddSingleton<IMailboxProcessingRetryStore>(store);
                services.AddSingleton<IExternalWorkRetryStore>(store);
                services.AddSingleton<IAcquireCaseEditLease>(store);
                services.AddSingleton<IRenewCaseEditLease>(store);
                services.AddSingleton<IReleaseCaseEditLease>(store);
                services.AddSingleton<IRevokeBoxFileRequest>(store);
                services.AddSingleton<IRevokeRequestUploadLink>(store);
            }));

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

    private static FormUrlEncodedContent Form(
        string antiforgeryToken,
        params (string Name, string Value)[] values)
    {
        var fields = values.ToDictionary(item => item.Name, item => item.Value, StringComparer.Ordinal);
        fields["__RequestVerificationToken"] = antiforgeryToken;
        return new(fields);
    }

    private static string AntiforgeryValue(string html)
    {
        var tag = AntiforgeryTagRegex().Match(html);
        Assert.True(tag.Success, "The operations action must render an antiforgery token.");
        var value = ValueRegex().Match(tag.Value);
        Assert.True(value.Success, "The operations antiforgery token must have a value.");
        return WebUtility.HtmlDecode(value.Groups["value"].Value);
    }

    private static string OperationKeyValue(string html)
    {
        var tag = OperationKeyTagRegex().Match(html);
        Assert.True(tag.Success, "The operations lease action must render an operation key.");
        var value = ValueRegex().Match(tag.Value);
        Assert.True(value.Success, "The operations lease operation key must have a value.");
        return WebUtility.HtmlDecode(value.Groups["value"].Value);
    }

    private static void AssertPrg(HttpResponseMessage response, string expectedPath)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(expectedPath, response.Headers.Location?.OriginalString);
    }

    [GeneratedRegex("<input[^>]*name=\"__RequestVerificationToken\"[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AntiforgeryTagRegex();

    [GeneratedRegex("<input[^>]*name=\"operationKey\"[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OperationKeyTagRegex();

    [GeneratedRegex("value=\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ValueRegex();

    private sealed class RecordingOperationsStore :
        IEmailOperationsProjectionStore,
        IRequestOperationsProjectionStore,
        IMailboxProcessingRetryStore,
        IExternalWorkRetryStore,
        IAcquireCaseEditLease,
        IRenewCaseEditLease,
        IReleaseCaseEditLease,
        IRevokeBoxFileRequest,
        IRevokeRequestUploadLink
    {
        public Guid CaseId { get; } = Guid.NewGuid();
        public Guid IntakeId { get; } = Guid.NewGuid();
        public Guid TriageId { get; } = Guid.NewGuid();
        public Guid BoxRequestId { get; } = Guid.NewGuid();
        public Guid PegasusRequestId { get; } = Guid.NewGuid();
        public Guid ExternalWorkId { get; } = Guid.NewGuid();
        public long CaseVersion { get; } = 10;
        public string LeaseToken { get; } = "opaque-operations-lease";
        public string ReceivedMailboxId { get; } = "approved-inbox";
        public string MailboxFailureCode { get; } = "source_unavailable";
        public DateTimeOffset MailboxFailureDueAtUtc { get; } = FixedUtcNow.AddMinutes(5);
        public int ExternalAttemptCount { get; } = 5;
        public RetryMailboxProcessingCommand? MailboxRetry { get; private set; }
        public RetryExternalWorkCommand? ExternalRetry { get; private set; }
        public RevokeBoxFileRequestCommand? BoxRevoke { get; private set; }
        public RevokeRequestUploadLinkCommand? PegasusRevoke { get; private set; }
        private bool LeaseIsActive { get; set; }
        private string? LeaseHolder { get; set; }
        public string? LeaseOperationKey { get; private set; }

        public Task<EmailOperationsProjection> GetAsync(
            int maximumItemsPerDirection,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken) => Task.FromResult(new EmailOperationsProjection(
                ImmutableArray.Create(
                    Email("received-failed", EmailOperationDirection.Received, EmailOperationState.Failed, MailboxFailureCode, ReceivedMailboxId, MailboxFailureDueAtUtc),
                    Email("received-pending", EmailOperationDirection.Received, EmailOperationState.Pending),
                    Email("received-intake", EmailOperationDirection.Received, EmailOperationState.Succeeded, intakeId: IntakeId),
                    Email("received-unknown", EmailOperationDirection.Received, EmailOperationState.Unknown)),
                ImmutableArray.Create(
                    Email("sent-failed", EmailOperationDirection.Sent, EmailOperationState.Failed, "sent_source_unavailable", "approved-sent", FixedUtcNow.AddMinutes(10)),
                    Email("sent-triage", EmailOperationDirection.Sent, EmailOperationState.Succeeded, triageId: TriageId),
                    Email("sent-case", EmailOperationDirection.Sent, EmailOperationState.Succeeded, caseId: CaseId, caseReference: "QD31001", principalCode: "QD")),
                ReceivedLimitReached: false,
                SentLimitReached: false));

        Task<RequestOperationsProjection> IRequestOperationsProjectionStore.GetAsync(
            int maximumItems,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken) => Task.FromResult(new RequestOperationsProjection(
                ImmutableArray.Create(
                    Request(BoxRequestId, RequestOperationKind.BoxFileRequest, RequestOperationState.Active, version: 3, canRevoke: true),
                    Request(PegasusRequestId, RequestOperationKind.PegasusUploadLink, RequestOperationState.Active, version: 4, canRevoke: true),
                    Request(Guid.NewGuid(), RequestOperationKind.PegasusUploadLink, RequestOperationState.Expired),
                    Request(Guid.NewGuid(), RequestOperationKind.PegasusUploadLink, RequestOperationState.Exhausted),
                    Request(Guid.NewGuid(), RequestOperationKind.PegasusUploadLink, RequestOperationState.Revoked),
                    Request(ExternalWorkId, RequestOperationKind.ExternalWork, RequestOperationState.Failed, canRetry: true, attemptCount: ExternalAttemptCount),
                    Request(Guid.NewGuid(), RequestOperationKind.ExternalWork, RequestOperationState.Pending),
                    Request(Guid.NewGuid(), RequestOperationKind.ExternalWork, RequestOperationState.UnknownExternal)),
                LimitReached: false));

        public Task<OperationsRetryResult> RetryAsync(
            RetryMailboxProcessingCommand command,
            DateTimeOffset retryAtUtc,
            CancellationToken cancellationToken)
        {
            MailboxRetry = command;
            return Task.FromResult(new OperationsRetryResult(IsReplay: false));
        }

        public Task<OperationsRetryResult> RetryAsync(
            RetryExternalWorkCommand command,
            DateTimeOffset retryAtUtc,
            CancellationToken cancellationToken)
        {
            ExternalRetry = command;
            return Task.FromResult(new OperationsRetryResult(IsReplay: false));
        }

        Task<CaseEditLease> IAcquireCaseEditLease.ExecuteAsync(
            ClaimCaseEditLeaseRequest request,
            CancellationToken cancellationToken)
        {
            LeaseIsActive = true;
            LeaseHolder = request.Actor.SubjectId;
            LeaseOperationKey = request.OperationKey;
            return Task.FromResult(new CaseEditLease(
                request.CaseId,
                LeaseToken,
                request.Actor.SubjectId,
                CaseVersion,
                FixedUtcNow.AddMinutes(5)));
        }

        Task<CaseEditLease> IRenewCaseEditLease.ExecuteAsync(
            RenewCaseEditLeaseRequest request,
            CancellationToken cancellationToken)
        {
            LeaseIsActive = true;
            LeaseHolder = request.Actor.SubjectId;
            LeaseOperationKey = request.OperationKey;
            return Task.FromResult(new CaseEditLease(
                request.CaseId,
                LeaseToken,
                request.Actor.SubjectId,
                CaseVersion,
                FixedUtcNow.AddMinutes(5)));
        }

        Task IReleaseCaseEditLease.ExecuteAsync(
            ReleaseCaseEditLeaseRequest request,
            CancellationToken cancellationToken)
        {
            LeaseIsActive = false;
            LeaseHolder = null;
            LeaseOperationKey = null;
            return Task.CompletedTask;
        }

        public Task<BoxFileRequest> ExecuteAsync(
            RevokeBoxFileRequestCommand command,
            CancellationToken cancellationToken = default)
        {
            BoxRevoke = command;
            LeaseIsActive = false;
            LeaseHolder = null;
            LeaseOperationKey = null;
            return Task.FromResult(new BoxFileRequest(
                command.FileRequestId,
                command.CaseId,
                BoxFileRequestStatus.Deactivated,
                FixedUtcNow.AddHours(-1),
                FixedUtcNow.AddDays(1),
                FixedUtcNow,
                command.ExpectedFileRequestVersion + 1));
        }

        public Task ExecuteAsync(
            RevokeRequestUploadLinkCommand command,
            CancellationToken cancellationToken = default)
        {
            PegasusRevoke = command;
            LeaseIsActive = false;
            LeaseHolder = null;
            LeaseOperationKey = null;
            return Task.CompletedTask;
        }

        private static EmailOperationProjection Email(
            string id,
            EmailOperationDirection direction,
            EmailOperationState state,
            string? failureCode = null,
            string? retryMailboxId = null,
            DateTimeOffset? retryDueAtUtc = null,
            Guid? intakeId = null,
            Guid? triageId = null,
            Guid? caseId = null,
            string? caseReference = null,
            string? principalCode = null) => new(
                id,
                direction,
                state,
                "operations@example.invalid",
                FixedUtcNow,
                intakeId,
                triageId,
                caseId,
                caseReference,
                principalCode,
                failureCode,
                retryMailboxId,
                retryDueAtUtc);

        private RequestOperationProjection Request(
            Guid id,
            RequestOperationKind kind,
            RequestOperationState state,
            long? version = null,
            bool canRetry = false,
            bool canRevoke = false,
            int? attemptCount = null) => new(
                id,
                kind,
                state,
                CaseId,
                "QD31001",
                "QD",
                FixedUtcNow,
                FixedUtcNow.AddDays(1),
                version,
                AcceptedFileCount: kind == RequestOperationKind.PegasusUploadLink ? 1 : null,
                AcceptedByteCount: kind == RequestOperationKind.PegasusUploadLink ? 1024 : null,
                MaximumFileCount: kind == RequestOperationKind.PegasusUploadLink ? 10 : null,
                MaximumByteCount: kind == RequestOperationKind.PegasusUploadLink ? 52_428_800 : null,
                LimitsVersion: kind == RequestOperationKind.PegasusUploadLink ? "limits-v1" : null,
                ExternalKind: kind == RequestOperationKind.ExternalWork ? "vehicle_lookup" : null,
                attemptCount,
                FailureCode: state == RequestOperationState.Failed ? "queue_poisoned" : null,
                FailureReason: state == RequestOperationState.Failed ? "The retry policy was exhausted." : null,
                canRetry,
                canRevoke,
                CaseVersion,
                LeaseIsActive
                    ? RequestCaseEditLeaseState.Active
                    : RequestCaseEditLeaseState.Available,
                LeaseIsActive ? FixedUtcNow.AddMinutes(5) : null)
            {
                ActiveEditLease = LeaseIsActive
                    ? new CaseEditLeaseSnapshot(
                        LeaseHolder!,
                        FixedUtcNow.AddMinutes(5),
                        LeaseOperationKey!)
                    : null
            };
    }
}
