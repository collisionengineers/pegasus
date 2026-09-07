using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
using Pegasus.Core.Reports;

namespace Pegasus.IntegrationTests.Reports;

public sealed partial class AssessmentReportDraftWebTests
{
    [Theory]
    [InlineData("GenerateReport", CaseReportArtifactKind.AssessmentReport)]
    [InlineData("GenerateFeeNote", CaseReportArtifactKind.FeeNote)]
    public async Task ImmutableArtifactPostsCarryServerActorCaseVersionAndLease(
        string handler,
        CaseReportArtifactKind expectedKind)
    {
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var caseId = Guid.NewGuid();
        var operationKey = Guid.NewGuid().ToString("N");
        var recorder = new RecordingGenerateReport();
        using var factory = Compose(
            baseFactory,
            new FakeGetCase(caseId),
            FullAssessmentProjection(caseId),
            new FakeProjectionSource(ReadyInput(caseId)),
            new FakeRenderer([1]),
            generateReport: recorder);
        using var client = Client(factory);
        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=report");

        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}?handler={handler}&section=report",
            Form(
                AntiforgeryValue(html),
                ("id", caseId.ToString("D")),
                ("operationKey", operationKey),
                ("editLeaseToken", "held-report-lease")));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var request = Assert.Single(recorder.Requests);
        Assert.Equal(caseId, request.CaseId);
        Assert.Equal(0, request.ExpectedCaseVersion);
        Assert.Equal("held-report-lease", request.LeaseToken);
        Assert.Equal(operationKey, request.OperationKey);
        Assert.Equal(expectedKind, request.Kind);
        Assert.Equal(ActorKind.Staff, request.Actor.Kind);
        Assert.Contains(StaffRole.Engineer, request.Actor.Roles);
    }

    [Fact]
    public async Task PrepareDeliveryCarriesGenerationAndServerAuthorityWithoutSending()
    {
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var caseId = Guid.NewGuid();
        var generationId = Guid.NewGuid();
        var operationKey = Guid.NewGuid().ToString("N");
        var prepare = new RecordingPrepareDelivery(caseId, generationId);
        var send = new RecordingSendPreparedReport(StaffMailState.Submitted);
        using var factory = Compose(
            baseFactory,
            new FakeGetCase(caseId),
            FullAssessmentProjection(caseId),
            new FakeProjectionSource(ReadyInput(caseId)),
            new FakeRenderer([1]),
            prepareDelivery: prepare,
            sendPreparedReport: send);
        using var client = Client(factory);
        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=report");

        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=PrepareReportDelivery&section=report",
            Form(
                AntiforgeryValue(html),
                ("id", caseId.ToString("D")),
                ("operationKey", operationKey),
                ("editLeaseToken", "held-report-lease"),
                ("generationId", generationId.ToString("D")),
                ("expectedGenerationVersion", "13")));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var request = Assert.Single(prepare.Requests);
        Assert.Equal(caseId, request.CaseId);
        Assert.Equal(0, request.ExpectedCaseVersion);
        Assert.Equal("held-report-lease", request.LeaseToken);
        Assert.Equal(generationId, request.GenerationId);
        Assert.Equal(13, request.ExpectedGenerationVersion);
        Assert.Equal(operationKey, request.OperationKey);
        Assert.Equal(ActorKind.Staff, request.Actor.Kind);
        Assert.Empty(send.Requests);
    }

    [Theory]
    [InlineData(StaffMailState.Submitted)]
    [InlineData(StaffMailState.Unknown)]
    public async Task SendPreparedReportDerivesStableOperationKeyAndDoesNotClaimSent(
        StaffMailState returnedState)
    {
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var caseId = Guid.NewGuid();
        var preparationId = Guid.NewGuid();
        var send = new RecordingSendPreparedReport(returnedState);
        using var factory = Compose(
            baseFactory,
            new FakeGetCase(caseId),
            FullAssessmentProjection(caseId),
            new FakeProjectionSource(ReadyInput(caseId)),
            new FakeRenderer([1]),
            sendPreparedReport: send);
        using var client = Client(factory);
        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=report");

        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=SendPreparedReport&section=report",
            Form(
                AntiforgeryValue(html),
                ("id", caseId.ToString("D")),
                ("preparationId", preparationId.ToString("D")),
                ("expectedPreparationVersion", "9"),
                ("operationKey", "client-supplied-is-ignored")));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var request = Assert.Single(send.Requests);
        Assert.Equal(caseId, request.CaseId);
        Assert.Equal(preparationId, request.PreparationId);
        Assert.Equal(9, request.ExpectedPreparationVersion);
        Assert.Equal(preparationId.ToString("N"), request.OperationKey);
        Assert.Equal(ActorKind.Staff, request.Actor.Kind);

        var reloaded = await GetHtmlAsync(client, response.Headers.Location!.OriginalString!);
        var expectedMessage = returnedState == StaffMailState.Submitted
            ? Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.SendAccepted
            : Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.SendUnknown;
        Assert.Contains(expectedMessage, reloaded, StringComparison.Ordinal);
        Assert.DoesNotContain(
            Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.SendObservedSent,
            reloaded,
            StringComparison.Ordinal);
    }

    private static HttpClient Client(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Engineer");
        return client;
    }

    private sealed class RecordingGenerateReport : IGenerateCaseReport
    {
        public List<GenerateCaseReportRequest> Requests { get; } = [];

        public Task<CaseReportGenerationResult> ExecuteAsync(
            GenerateCaseReportRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new CaseReportGenerationResult(
                CaseReportGenerationOutcome.Pending, null, []));
        }
    }

    private sealed class RecordingPrepareDelivery(Guid caseId, Guid generationId)
        : IPrepareCaseReportDelivery
    {
        public List<PrepareCaseReportDeliveryRequest> Requests { get; } = [];

        public Task<CaseReportDeliveryPreparation> ExecuteAsync(
            PrepareCaseReportDeliveryRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new CaseReportDeliveryPreparation(
                Guid.NewGuid(), caseId, generationId, request.ExpectedGenerationVersion,
                1, [], request.Actor, ReportFixtureAtUtc));
        }
    }

    private sealed class RecordingSendPreparedReport(StaffMailState state)
        : ISendPreparedCaseReport
    {
        public List<SendPreparedCaseReportRequest> Requests { get; } = [];

        public Task<StaffMailOperation> ExecuteAsync(
            SendPreparedCaseReportRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new StaffMailOperation(
                Guid.NewGuid(), state, null, 1, ReportFixtureAtUtc,
                state == StaffMailState.Submitted ? ReportFixtureAtUtc : null,
                null, null, Guid.NewGuid(), 1, new string('a', 64), null, null,
                StaffMailPurpose.CaseReport, request.CaseId,
                request.ExpectedPreparationVersion, null));
        }
    }
}
