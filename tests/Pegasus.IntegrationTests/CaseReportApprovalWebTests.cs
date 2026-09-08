using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;

namespace Pegasus.IntegrationTests;

public sealed partial class CaseDetailsWebTests
{
    /// <summary>
    /// B05/B09: the artifact download reopens the confirmed artifact's
    /// immutable bytes through <see cref="IGeneratedCaseArtifactStore"/> and
    /// hands the stream to the file result, which owns it. The response must
    /// carry every byte, so the stream cannot be disposed before MVC writes
    /// it, and it must be disposed once the response is written.
    /// </summary>
    [Fact]
    public async Task GeneratedArtifactDownloadWritesTheImmutableBytesAndDisposesTheStreamAfterTheResponse()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingCaseDetailsStore();
        var artifacts = new RecordingGeneratedArtifactStore();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                Substitute<IGetCase>(services, store);
                Substitute<IGetAssessmentAccess>(services, new FakeGetAssessmentAccess(canOpen: true));
                Substitute<IGeneratedCaseArtifactStore>(services, artifacts);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        var generationId = Guid.NewGuid();
        var artifactId = Guid.NewGuid();

        using var response = await client.GetAsync(
            $"/Cases/{store.CaseId:D}?handler=GeneratedArtifact&generationId={generationId:D}&artifactId={artifactId:D}");
        var body = await response.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(
            RecordingGeneratedArtifactStore.FileName,
            response.Content.Headers.ContentDisposition?.FileNameStar);
        Assert.Equal(artifacts.Bytes, body);
        var opened = Assert.IsType<DisposalTrackingStream>(artifacts.Opened);
        Assert.True(opened.Disposed, "The file result owns the artifact stream and disposes it after the body is written.");
        var request = Assert.Single(artifacts.Requests);
        Assert.Equal(store.CaseId, request.CaseId);
        Assert.Equal(generationId, request.GenerationId);
        Assert.Equal(artifactId, request.ArtifactId);
        Assert.Equal(ActorKind.Staff, request.Actor.Kind);
    }

    /// <summary>
    /// Outside the report journey's open states the page does not reach the
    /// store at all: the guard answers first.
    /// </summary>
    [Fact]
    public async Task GeneratedArtifactDownloadIsNotFoundOutsideTheReportJourney()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingCaseDetailsStore();
        var artifacts = new RecordingGeneratedArtifactStore();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                Substitute<IGetCase>(services, store);
                Substitute<IGetAssessmentAccess>(services, new FakeGetAssessmentAccess(canOpen: false));
                Substitute<IGeneratedCaseArtifactStore>(services, artifacts);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        using var response = await client.GetAsync(
            $"/Cases/{store.CaseId:D}?handler=GeneratedArtifact&generationId={Guid.NewGuid():D}&artifactId={Guid.NewGuid():D}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(artifacts.Requests);
    }

    private sealed class RecordingGeneratedArtifactStore : IGeneratedCaseArtifactStore
    {
        public const string FileName = "QDOS3100042-assessment-report.pdf";

        public byte[] Bytes { get; } = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37, 0x0A, 1, 2, 3, 4, 5];

        public DisposalTrackingStream? Opened { get; private set; }

        public List<(ActionActor Actor, Guid CaseId, Guid GenerationId, Guid ArtifactId)> Requests { get; } = [];

        public Task<LogicalDocumentContent> OpenAsync(
            ActionActor actor,
            Guid caseId,
            Guid generationId,
            Guid artifactId,
            CancellationToken cancellationToken)
        {
            Requests.Add((actor, caseId, generationId, artifactId));
            Opened = new DisposalTrackingStream(Bytes);
            return Task.FromResult(new LogicalDocumentContent(
                Opened,
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                Convert.ToHexStringLower(SHA256.HashData(Bytes)),
                Bytes.Length,
                FileName,
                "application/pdf"));
        }
    }

    /// <summary>A read-only stream that records whether it was disposed.</summary>
    private sealed class DisposalTrackingStream(byte[] bytes) : MemoryStream(bytes, writable: false)
    {
        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

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

        // CASE-012: the workspace no longer renders the typed-SHA approval
        // form (inherited scope bans typed SHA inputs; the approval act moves
        // to the Assessment report-draft lane). The handler's contract — the
        // server actor, the stable artifact identity, replay by operation
        // key, and no caller-supplied time — is pinned here for the surface
        // that will call it.
        var initialHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        Assert.DoesNotContain("Approve report", initialHtml, StringComparison.Ordinal);
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
        Assert.DoesNotContain("approvedAtUtc", leasedHtml, StringComparison.Ordinal);
        var approvalId = Guid.NewGuid().ToString("D");
        const string approvalOperationKey = "report-approval-replay";
        using var firstResponse = await client.PostAsync(
            $"/Cases/{store.CaseId:D}/Closure?handler=RecordReportApproval",
            ApprovalForm(
                AntiforgeryValue(leasedHtml),
                store,
                approvalOperationKey,
                approvalId));
        AssertPrg(firstResponse, store.CaseId);

        var currentHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        using var replayResponse = await client.PostAsync(
            $"/Cases/{store.CaseId:D}/Closure?handler=RecordReportApproval",
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
        /// <summary>
        /// Stands in for the resolved display name <c>GetCase</c> would compute
        /// (see <see cref="Pegasus.Core.Actors.ActorDisplayNames"/>); this fake
        /// bypasses <c>GetCase</c> entirely, so it supplies the projection itself.
        /// </summary>
        internal const string ApproverDisplayName = "alex";

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
                    : new(leaseHolder, ActorKind.Staff, now.AddMinutes(5), leaseOperationKey!),
                [],
                null,
                CaseCustodyState.Pending,
                [],
                [],
                [])
            {
                ReportApprovedByDisplayName = approval is null ? null : ApproverDisplayName
            };
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
