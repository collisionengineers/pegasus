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
        var targetGenerationId = Guid.NewGuid();
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
                ("editLeaseToken", "held-report-lease"),
                ("expectedCaseVersion", "41"),
                ("targetGenerationId", targetGenerationId.ToString("D"))));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var request = Assert.Single(recorder.Requests);
        Assert.Equal(caseId, request.CaseId);
        Assert.Equal(41, request.ExpectedCaseVersion);
        Assert.Equal("held-report-lease", request.LeaseToken);
        Assert.Equal(operationKey, request.OperationKey);
        Assert.Equal(expectedKind, request.Kind);
        Assert.Equal(
            expectedKind == CaseReportArtifactKind.FeeNote ? targetGenerationId : null,
            request.TargetGenerationId);
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
        // v26: Generate report is the head's primary inside the edit session;
        // the Include fee note choice sits in the More menu, bound to that
        // form, so it still posts as the form's own field.
        var html = await EnterEditModeAsync(client, caseId);

        Assert.Contains("data-generate-report", html, StringComparison.Ordinal);
        Assert.Contains(
            "name=\"includeFeeNote\" value=\"true\" form=\"case-generate-report-form\" data-include-fee-note",
            html,
            StringComparison.Ordinal);
        Assert.Contains(
            Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.IncludeFeeNote,
            html,
            StringComparison.Ordinal);

        List<(string Name, string Value)> fields =
        [
            ("id", caseId.ToString("D")),
            ("operationKey", Guid.NewGuid().ToString("N")),
            ("editLeaseToken", "held-report-lease"),
            ("expectedCaseVersion", "0"),
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

    [Fact]
    public async Task ReportAndSeparateFeeNoteFormsUseDistinctOperationKeys()
    {
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var caseId = Guid.NewGuid();
        using var reportFactory = Compose(
            baseFactory,
            new FakeGetCase(caseId),
            FullAssessmentProjection(caseId),
            new FakeProjectionSource(ReadyInput(caseId)),
            new FakeRenderer([1]));
        using var reportClient = Client(reportFactory);
        var reportHtml = await EnterEditModeAsync(reportClient, caseId);
        var reportForm = FormHtml(reportHtml, "GenerateReport");

        using var feeNoteFactory = Compose(
            baseFactory,
            new FakeGetCase(caseId),
            FullAssessmentProjection(caseId),
            new FakeProjectionSource(ReadyInput(caseId)),
            new FakeRenderer([1]))
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<ICaseReportGenerationStore>();
                services.AddSingleton<ICaseReportGenerationStore>(new FakeCurrentGeneration(caseId, false));
            }));
        using var feeNoteClient = Client(feeNoteFactory);
        var feeNoteHtml = await EnterEditModeAsync(feeNoteClient, caseId);
        var feeNoteForm = FormHtml(feeNoteHtml, "GenerateFeeNote");

        Assert.NotEqual(
            InputValue(reportForm, "operationKey"),
            InputValue(feeNoteForm, "operationKey"));
    }

    [Fact]
    public async Task ConfirmedReportDoesNotRenderAFreshConflictingSubmission()
    {
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var caseId = Guid.NewGuid();
        var generator = new RecordingGenerationJourney(caseId, failFirst: false);
        using var factory = Compose(
            baseFactory,
            new FakeGetCase(caseId),
            FullAssessmentProjection(caseId),
            new FakeProjectionSource(ReadyInput(caseId)),
            new FakeRenderer([1]),
            generateReport: generator)
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<ICaseReportGenerationStore>();
                services.AddSingleton<ICaseReportGenerationStore>(generator);
            }));
        using var client = Client(factory);
        var initialHtml = await EnterEditModeAsync(client, caseId);
        var form = FormHtml(initialHtml, "GenerateReport");

        using var generated = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=GenerateReport&section=report",
            Form(
                AntiforgeryValue(initialHtml),
                ("id", caseId.ToString("D")),
                ("operationKey", InputValue(form, "operationKey")),
                ("editLeaseToken", InputValue(form, "editLeaseToken")),
                ("expectedCaseVersion", InputValue(form, "expectedCaseVersion"))));

        Assert.Equal(HttpStatusCode.Redirect, generated.StatusCode);
        Assert.Single(generator.Requests);
        var confirmedHtml = await EnterEditModeAsync(client, caseId);
        Assert.Contains("data-report-artifact=\"AssessmentReport\"", confirmedHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("data-generate-report", confirmedHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"case-generate-report-form\"", confirmedHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("data-include-fee-note", confirmedHtml, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(CaseReportArtifactStatus.Pending)]
    [InlineData(CaseReportArtifactStatus.Failed)]
    [InlineData(CaseReportArtifactStatus.Unknown)]
    public async Task UnconfirmedFeeNoteKeepsRetryActionWithItsRetainedOperationKey(
        CaseReportArtifactStatus status)
    {
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var caseId = Guid.NewGuid();
        const string retainedOperationKey = "retained-fee-note-operation";
        using var factory = Compose(
            baseFactory,
            new FakeGetCase(caseId),
            FullAssessmentProjection(caseId),
            new FakeProjectionSource(ReadyInput(caseId)),
            new FakeRenderer([1]))
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<ICaseReportGenerationStore>();
                services.AddSingleton<ICaseReportGenerationStore>(new FakeCurrentGeneration(
                    caseId,
                    includeFeeNote: false,
                    feeNoteStatus: status,
                    feeNoteOperationKey: retainedOperationKey));
            }));
        using var client = Client(factory);

        var html = await EnterEditModeAsync(client, caseId);
        var feeNoteForm = FormHtml(html, "GenerateFeeNote");

        Assert.Equal(retainedOperationKey, InputValue(feeNoteForm, "operationKey"));
        Assert.NotEqual(Guid.Empty, Guid.Parse(InputValue(feeNoteForm, "targetGenerationId")));
    }

    [Fact]
    public async Task FailedReportRedirectKeepsItsFrozenCommandForASuccessfulRetry()
    {
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var caseId = Guid.NewGuid();
        var generator = new RecordingGenerationJourney(caseId, failFirst: true);
        using var factory = Compose(
            baseFactory,
            new FakeGetCase(caseId),
            FullAssessmentProjection(caseId),
            new FakeProjectionSource(ReadyInput(caseId)),
            new FakeRenderer([1]),
            generateReport: generator)
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<ICaseReportGenerationStore>();
                services.AddSingleton<ICaseReportGenerationStore>(generator);
            }));
        using var client = Client(factory);
        var initialHtml = await EnterEditModeAsync(client, caseId);
        var initialForm = FormHtml(initialHtml, "GenerateReport");
        var retainedOperationKey = InputValue(initialForm, "operationKey");

        using var failed = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=GenerateReport&section=report",
            Form(
                AntiforgeryValue(initialHtml),
                ("id", caseId.ToString("D")),
                ("operationKey", retainedOperationKey),
                ("editLeaseToken", InputValue(initialForm, "editLeaseToken")),
                ("expectedCaseVersion", InputValue(initialForm, "expectedCaseVersion")),
                ("includeFeeNote", "true")));

        Assert.Equal(HttpStatusCode.Redirect, failed.StatusCode);
        var retryHtml = await GetHtmlAsync(client, failed.Headers.Location!.OriginalString);
        var retryForm = FormHtml(retryHtml, "GenerateReport");
        Assert.Equal(retainedOperationKey, InputValue(retryForm, "operationKey"));
        Assert.Equal("true", InputValue(retryForm, "includeFeeNote"));
        Assert.DoesNotContain("data-include-fee-note", retryForm, StringComparison.Ordinal);

        using var retried = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=GenerateReport&section=report",
            Form(
                AntiforgeryValue(retryHtml),
                ("id", caseId.ToString("D")),
                ("operationKey", InputValue(retryForm, "operationKey")),
                ("editLeaseToken", InputValue(retryForm, "editLeaseToken")),
                ("expectedCaseVersion", InputValue(retryForm, "expectedCaseVersion")),
                ("includeFeeNote", InputValue(retryForm, "includeFeeNote"))));

        Assert.Equal(HttpStatusCode.Redirect, retried.StatusCode);
        Assert.Collection(
            generator.Requests,
            request =>
            {
                Assert.Equal(retainedOperationKey, request.OperationKey);
                Assert.True(request.IncludeFeeNote);
                Assert.Equal(0, request.ExpectedCaseVersion);
            },
            request =>
            {
                Assert.Equal(retainedOperationKey, request.OperationKey);
                Assert.True(request.IncludeFeeNote);
                Assert.Equal(0, request.ExpectedCaseVersion);
            });
        Assert.Equal(CaseReportGenerationOutcome.Generated, generator.LastOutcome);
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
                ("expectedCaseVersion", "0"),
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

    /// <summary>
    /// v26: the Report section's lease-carrying controls render inside the
    /// page-wide edit session, entered through the ribbon's Edit Case claim.
    /// </summary>
    private static async Task<string> EnterEditModeAsync(HttpClient client, Guid caseId)
    {
        var initial = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=report");
        using var claim = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=ClaimLease",
            Form(
                AntiforgeryValue(initial),
                ("id", caseId.ToString("D")),
                ("expectedVersion", InputValue(initial, "expectedVersion")),
                ("operationKey", InputValue(initial, "operationKey")),
                ("section", "report")));
        Assert.Equal(HttpStatusCode.Redirect, claim.StatusCode);
        var editing = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=report");
        Assert.Contains("data-case-editing=\"true\"", editing, StringComparison.Ordinal);
        return editing;
    }

    private static string InputValue(string html, string name)
    {
        var tag = System.Text.RegularExpressions.Regex.Match(
            html,
            $"<input[^>]*name=\"{System.Text.RegularExpressions.Regex.Escape(name)}\"[^>]*>",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        Assert.True(tag.Success, $"The page must render an input named {name}.");
        var value = System.Text.RegularExpressions.Regex.Match(tag.Value, "value=\"(?<value>[^\"]*)\"");
        Assert.True(value.Success, $"The input {name} must have a value.");
        return WebUtility.HtmlDecode(value.Groups["value"].Value);
    }

    private static string FormHtml(string html, string handler)
    {
        var form = System.Text.RegularExpressions.Regex.Match(
            html,
            $"<form[^>]*(?:action=\"[^\"]*handler={handler}[^\"]*\"|asp-page-handler=\"{handler}\")[^>]*>[\\s\\S]*?</form>",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
                | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        Assert.True(form.Success, $"The page must render the {handler} form.");
        return form.Value;
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
    private sealed class FakeCurrentGeneration(
        Guid caseId,
        bool includeFeeNote,
        CaseReportArtifactStatus? feeNoteStatus = null,
        string? feeNoteOperationKey = null,
        CaseReportArtifactStatus reportStatus = CaseReportArtifactStatus.Confirmed,
        string? reportOperationKey = null)
        : ICaseReportGenerationStore
    {
        private readonly CaseReportGenerationRecord record = GenerationRecord(
            caseId,
            includeFeeNote,
            feeNoteStatus,
            feeNoteOperationKey,
            reportStatus,
            reportOperationKey);

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

    }

    private sealed class RecordingGenerationJourney(Guid caseId, bool failFirst)
        : IGenerateCaseReport, ICaseReportGenerationStore
    {
        private CaseReportGenerationRecord? current;

        public List<GenerateCaseReportRequest> Requests { get; } = [];

        public CaseReportGenerationOutcome? LastOutcome { get; private set; }

        public Task<CaseReportGenerationResult> ExecuteAsync(
            GenerateCaseReportRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var status = failFirst && Requests.Count == 1
                ? CaseReportArtifactStatus.Failed
                : CaseReportArtifactStatus.Confirmed;
            current = GenerationRecord(
                caseId,
                request.IncludeFeeNote,
                feeNoteStatus: null,
                feeNoteOperationKey: null,
                reportStatus: status,
                reportOperationKey: request.OperationKey);
            LastOutcome = status == CaseReportArtifactStatus.Failed
                ? CaseReportGenerationOutcome.Failed
                : CaseReportGenerationOutcome.Generated;
            return Task.FromResult(new CaseReportGenerationResult(
                LastOutcome.Value,
                current,
                []));
        }

        public Task<CaseReportGenerationRecord?> GetCurrentAsync(
            ActionActor actor,
            Guid id,
            CancellationToken cancellationToken) => Task.FromResult(current);

        public Task<CaseReportGenerationRecord?> GetAsync(
            ActionActor actor,
            Guid id,
            Guid generationId,
            CancellationToken cancellationToken) => Task.FromResult(current);

        public Task<IReadOnlyList<CaseReportGenerationRecord>> ListAsync(
            ActionActor actor,
            Guid id,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<CaseReportGenerationRecord>>(
                current is null ? [] : [current]);

        public Task<CaseReportFreezeResult> FreezeAsync(
            FreezeCaseReportGenerationRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CaseReportGenerationRecord> ConfirmArtifactAsync(
            ConfirmCaseReportArtifactRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CaseReportGenerationRecord> RecordArtifactOutcomeAsync(
            RecordCaseReportArtifactOutcomeRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<int> MarkStaleAsync(
            Guid id,
            string reasonCode,
            CancellationToken cancellationToken) => Task.FromResult(0);

        public Task RecordDraftPreviewedAsync(
            RecordCaseReportDraftPreviewedRequest request,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private static CaseReportGenerationRecord GenerationRecord(
        Guid caseId,
        bool includeFeeNote,
        CaseReportArtifactStatus? feeNoteStatus,
        string? feeNoteOperationKey,
        CaseReportArtifactStatus reportStatus = CaseReportArtifactStatus.Confirmed,
        string? reportOperationKey = null)
    {
        reportOperationKey ??= "operation-1";
        var projected = AssessmentReportProjection.Project(ReadyInput(caseId)).Snapshot!;
        var report = projected with { IncludeFeeNote = includeFeeNote };
        var generationId = Guid.NewGuid();
        var snapshot = new CaseReportGenerationSnapshot(
            caseId, 0, "CE-100", reportOperationKey, CaseReportActor.None, ReportFixtureAtUtc,
            Guid.NewGuid(), new string('a', 64), "image/png", Guid.NewGuid(), 2,
            report.Costs, report.EngineerValue, Guid.NewGuid(),
            report.Content, report.Guides, report.ReportDate, false,
            report.AgreedFee, report.FeeDescriptionLines, [], [],
            AssessmentReportContract.TemplateVersion, "fake", report);
        var reportConfirmed = reportStatus == CaseReportArtifactStatus.Confirmed;
        List<CaseReportArtifactRecord> artifacts =
        [
            new(
                Guid.NewGuid(), generationId, CaseReportArtifactKind.AssessmentReport,
                reportStatus, reportOperationKey,
                reportConfirmed ? Guid.NewGuid() : null,
                reportConfirmed ? Guid.NewGuid() : null,
                reportConfirmed ? new string('c', 64) : null,
                reportConfirmed ? 3 : null,
                reportConfirmed ? "CE_100_assessment.pdf" : null,
                reportConfirmed ? "application/pdf" : null,
                null, null, null,
                reportStatus == CaseReportArtifactStatus.Failed ? "transient_failure" : null),
        ];
        if (feeNoteStatus is { } status)
        {
            artifacts.Add(new(
                Guid.NewGuid(), generationId, CaseReportArtifactKind.FeeNote,
                status, feeNoteOperationKey ?? "fee-note-operation", null,
                null, null, null, null, null, null, null, null,
                status == CaseReportArtifactStatus.Failed ? "transient_failure" : null));
        }
        return new(
            generationId, caseId, 0, 1, new string('b', 64), snapshot,
            AssessmentReportContract.TemplateVersion, "fake",
            reportConfirmed
                ? CaseReportGenerationState.Confirmed
                : CaseReportGenerationState.Pending,
            ReportFixtureAtUtc, null,
            artifacts);
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
