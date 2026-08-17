using System.Globalization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;

namespace Pegasus.IntegrationTests;

public sealed partial class CaseDetailsWebTests
{
    [Fact]
    public async Task ReportApprovalPostUsesServerActorStableArtifactIdentityAndNoCallerTime()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new ApprovalCaseDetailsStore();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetCase>();
                services.RemoveAll<IAcquireCaseEditLease>();
                services.RemoveAll<IRecordCaseReportApproval>();
                services.AddSingleton<IGetCase>(store);
                services.AddSingleton<IAcquireCaseEditLease>(store);
                services.AddSingleton<IRecordCaseReportApproval>(store);
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

        var leasedHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        Assert.Contains("Approve immutable report artifact", leasedHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("approvedAtUtc", leasedHtml, StringComparison.Ordinal);
        var approvalId = InputValue(leasedHtml, "approvalId");
        const string approvalOperationKey = "report-approval-replay";
        using var firstResponse = await client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=RecordReportApproval",
            ApprovalForm(
                AntiforgeryValue(leasedHtml),
                store,
                approvalOperationKey,
                approvalId));
        AssertPrg(firstResponse, store.CaseId);

        var currentHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        using var replayResponse = await client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=RecordReportApproval",
            ApprovalForm(
                AntiforgeryValue(currentHtml),
                store,
                approvalOperationKey,
                approvalId));
        AssertPrg(replayResponse, store.CaseId);

        Assert.Equal(2, store.Approvals.Count);
        var request = store.Approvals[0];
        Assert.Equal(store.CaseId, request.CaseId);
        Assert.Equal(store.CaseVersion, request.ExpectedVersion);
        Assert.Equal(store.LeaseToken, request.EditLeaseToken);
        Assert.Equal(approvalOperationKey, request.OperationKey);
        var claimant = Assert.Single(store.Claims).Actor;
        Assert.Equal(claimant.Kind, request.Actor.Kind);
        Assert.Equal(claimant.SubjectId, request.Actor.SubjectId);
        Assert.Equal(claimant.Roles.OrderBy(role => role), request.Actor.Roles.OrderBy(role => role));
        Assert.Equal(Guid.Parse(approvalId), request.Approval.ApprovalId);
        Assert.Equal("case-report-v1.pdf", request.Approval.ArtifactIdentity);
        Assert.Equal(new string('A', 64), request.Approval.ArtifactSha256);
        var replay = store.Approvals[1];
        Assert.Equal(request.CaseId, replay.CaseId);
        Assert.Equal(request.ExpectedVersion, replay.ExpectedVersion);
        Assert.Equal(request.OperationKey, replay.OperationKey);
        Assert.Equal(request.Reason, replay.Reason);
        Assert.Equal(request.EditLeaseToken, replay.EditLeaseToken);
        Assert.Equal(request.Actor.Kind, replay.Actor.Kind);
        Assert.Equal(request.Actor.SubjectId, replay.Actor.SubjectId);
        Assert.Equal(request.Actor.Roles.OrderBy(role => role), replay.Actor.Roles.OrderBy(role => role));
        Assert.Equal(request.Approval, replay.Approval);
    }

    private static FormUrlEncodedContent ApprovalForm(
        string antiforgeryToken,
        ApprovalCaseDetailsStore store,
        string operationKey,
        string approvalId) => Form(
            antiforgeryToken,
            ("id", store.CaseId.ToString("D")),
            ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
            ("operationKey", operationKey),
            ("editLeaseToken", store.LeaseToken),
            ("approvalId", approvalId),
            ("artifactIdentity", "case-report-v1.pdf"),
            ("artifactSha256", new string('A', 64)),
            ("reason", "Engineer approved the immutable report artifact"),
            ("approvedAtUtc", "2099-01-01T00:00:00.0000000+00:00"));

    private sealed class ApprovalCaseDetailsStore :
        IGetCase,
        IAcquireCaseEditLease,
        IRecordCaseReportApproval
    {
        private readonly DateTimeOffset now =
            new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
        private string? leaseHolder;
        private string? leaseOperationKey;
        private ReportApprovalEvidence? approval;

        public Guid CaseId { get; } = Guid.NewGuid();
        public long CaseVersion { get; } = 7;
        public string LeaseToken { get; } = "opaque-report-approval-lease";
        public List<ClaimCaseEditLeaseRequest> Claims { get; } = [];
        public List<RecordCaseReportApprovalRequest> Approvals { get; } = [];

        public Task<CaseDetails?> ExecuteAsync(
            GetCaseQuery query,
            CancellationToken cancellationToken)
        {
            var identity = new CaseIdentity(CaseId, "QDOS", 2031, 42, "QDOS3100042");
            var workflow = new CaseWorkflowRecord(
                CaseId,
                identity,
                CaseLifecycleState.ReportPreparation,
                null,
                approval,
                null,
                null,
                null,
                null,
                null,
                approval is null ? CaseVersion : CaseVersion + 1);
            var summary = new CaseSearchItem(
                CaseId,
                identity.Reference,
                null,
                CaseType.Inspection,
                identity.PrincipalCode,
                workflow.State,
                null,
                "AB12CDE",
                "Case claimant",
                "CLM-42",
                now.AddDays(-2),
                new DateOnly(2031, 5, 5),
                "Email",
                now.AddDays(-2));
            CaseDetails details = new(
                summary,
                workflow,
                leaseHolder is null
                    ? null
                    : new(leaseHolder, now.AddMinutes(5), leaseOperationKey!),
                [],
                null,
                CaseCustodyState.Pending,
                [],
                [],
                []);
            return Task.FromResult<CaseDetails?>(details);
        }

        Task<CaseEditLease> IAcquireCaseEditLease.ExecuteAsync(
            ClaimCaseEditLeaseRequest request,
            CancellationToken cancellationToken)
        {
            leaseHolder = request.Actor.SubjectId;
            leaseOperationKey = request.OperationKey;
            Claims.Add(request);
            return Task.FromResult(
                new CaseEditLease(
                    request.CaseId,
                    LeaseToken,
                    request.Actor.SubjectId,
                    request.ExpectedVersion,
                    now.AddMinutes(5)));
        }

        Task<CaseWorkflowRecord> IRecordCaseReportApproval.ExecuteAsync(
            RecordCaseReportApprovalRequest request,
            CancellationToken cancellationToken)
        {
            Approvals.Add(request);
            approval ??= new(
                request.Approval.ApprovalId,
                request.Approval.ArtifactIdentity,
                request.Approval.ArtifactSha256,
                request.Actor,
                now);
            leaseHolder = null;
            leaseOperationKey = null;
            return Task.FromResult(
                new CaseWorkflowRecord(
                    CaseId,
                    new(CaseId, "QDOS", 2031, 42, "QDOS3100042"),
                    CaseLifecycleState.ReportPreparation,
                    null,
                    approval,
                    null,
                    null,
                    null,
                    null,
                    null,
                    CaseVersion + 1));
        }
    }
}
