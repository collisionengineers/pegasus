using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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

    /// <summary>
    /// R34B: one checkbox on the generate form decides whether the fee note
    /// is part of the report. An unticked box posts nothing, so the request
    /// defaults to the separate document the fee-note action still produces.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GenerateReportCarriesTheOperatorsFeeNotePackagingChoice(bool includeFeeNote)
    {
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var caseId = Guid.NewGuid();
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

        Assert.Contains("name=\"includeFeeNote\"", html, StringComparison.Ordinal);
        Assert.Contains(
            Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.IncludeFeeNote,
            html,
            StringComparison.Ordinal);

        List<(string Name, string Value)> fields =
        [
            ("id", caseId.ToString("D")),
            ("operationKey", Guid.NewGuid().ToString("N")),
            ("editLeaseToken", "held-report-lease"),
        ];
        if (includeFeeNote)
        {
            fields.Add(("includeFeeNote", "true"));
        }

        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=GenerateReport&section=report",
            Form(AntiforgeryValue(html), [.. fields]));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var request = Assert.Single(recorder.Requests);
        Assert.Equal(CaseReportArtifactKind.AssessmentReport, request.Kind);
        Assert.Equal(includeFeeNote, request.IncludeFeeNote);
    }

    /// <summary>
    /// The generated card names what the operator actually issued, so a
    /// combined document is never offered as if a separate fee note existed.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TheGeneratedReportCardNamesTheCombinedDocument(bool includeFeeNote)
    {
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var caseId = Guid.NewGuid();
        using var factory = Compose(
            baseFactory,
            new FakeGetCase(caseId),
            FullAssessmentProjection(caseId),
            new FakeProjectionSource(ReadyInput(caseId)),
            new FakeRenderer([1]))
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<ICaseReportGenerationStore>();
                services.AddSingleton<ICaseReportGenerationStore>(
                    new FakeCurrentGeneration(caseId, includeFeeNote));
            }));
        using var client = Client(factory);

        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=report");

        var combined = Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.DownloadReportWithFeeNote;
        if (includeFeeNote)
        {
            Assert.Contains(combined, html, StringComparison.Ordinal);
        }
        else
        {
            Assert.DoesNotContain(combined, html, StringComparison.Ordinal);
            Assert.Contains(
                $"<span>{Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.DownloadReport}</span>",
                html,
                StringComparison.Ordinal);
        }
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
                ("expectedGenerationVersion", "13"),
                ("toRecipients", "reviewed@recipient.example"),
                ("ccRecipients", "copy@recipient.example")));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var request = Assert.Single(prepare.Requests);
        Assert.Equal(caseId, request.CaseId);
        Assert.Equal(0, request.ExpectedCaseVersion);
        Assert.Equal("held-report-lease", request.LeaseToken);
        Assert.Equal(generationId, request.GenerationId);
        Assert.Equal(13, request.ExpectedGenerationVersion);
        Assert.Equal(operationKey, request.OperationKey);
        Assert.Equal(ActorKind.Staff, request.Actor.Kind);
        Assert.Equal("reviewed@recipient.example", Assert.Single(request.ReviewedRecipients!.To));
        Assert.Equal("copy@recipient.example", Assert.Single(request.ReviewedRecipients.Cc));
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

    /// <summary>
    /// One confirmed current generation, so the card the page renders can be
    /// read. Only the reads the Case page makes are answered.
    /// </summary>
    private sealed class FakeCurrentGeneration(Guid caseId, bool includeFeeNote)
        : ICaseReportGenerationStore
    {
        private readonly CaseReportGenerationRecord record = Record(caseId, includeFeeNote);

        public Task<CaseReportGenerationRecord?> GetCurrentAsync(
            ActionActor actor, Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<CaseReportGenerationRecord?>(record);

        public Task<CaseReportGenerationRecord?> GetAsync(
            ActionActor actor, Guid id, Guid generationId, CancellationToken cancellationToken) =>
            Task.FromResult<CaseReportGenerationRecord?>(record);

        public Task<IReadOnlyList<CaseReportGenerationRecord>> ListAsync(
            ActionActor actor, Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CaseReportGenerationRecord>>([record]);

        public Task<CaseReportFreezeResult> FreezeAsync(
            FreezeCaseReportGenerationRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseReportGenerationRecord> ConfirmArtifactAsync(
            ConfirmCaseReportArtifactRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseReportGenerationRecord> RecordArtifactOutcomeAsync(
            RecordCaseReportArtifactOutcomeRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<int> MarkStaleAsync(Guid id, string reasonCode, CancellationToken cancellationToken) =>
            Task.FromResult(0);

        public Task RecordDraftPreviewedAsync(
            RecordCaseReportDraftPreviewedRequest request, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        private static CaseReportGenerationRecord Record(Guid caseId, bool includeFeeNote)
        {
            var projected = AssessmentReportProjection.Project(ReadyInput(caseId)).Snapshot!;
            var report = projected with { IncludeFeeNote = includeFeeNote };
            var generationId = Guid.NewGuid();
            var snapshot = new CaseReportGenerationSnapshot(
                caseId, 0, "CE-100", "operation-1", CaseReportActor.None, ReportFixtureAtUtc,
                Guid.NewGuid(), new string('a', 64), "image/png", Guid.NewGuid(), 2,
                report.Costs, report.EngineerValue, Guid.NewGuid(),
                report.Content, report.Guides, report.ReportDate, false,
                report.AgreedFee, report.FeeDescriptionLines, [], [],
                AssessmentReportContract.TemplateVersion, "fake", report);
            return new(
                generationId, caseId, 0, 1, new string('b', 64), snapshot,
                AssessmentReportContract.TemplateVersion, "fake",
                CaseReportGenerationState.Confirmed, ReportFixtureAtUtc, null,
                [
                    new CaseReportArtifactRecord(
                        Guid.NewGuid(), generationId, CaseReportArtifactKind.AssessmentReport,
                        CaseReportArtifactStatus.Confirmed, "operation-1", Guid.NewGuid(),
                        Guid.NewGuid(), new string('c', 64), 3, "CE_100_assessment.pdf",
                        "application/pdf", null, null, null, null),
                ]);
        }
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
                1, [], request.Actor, ReportFixtureAtUtc, new string('a', 64)));
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
