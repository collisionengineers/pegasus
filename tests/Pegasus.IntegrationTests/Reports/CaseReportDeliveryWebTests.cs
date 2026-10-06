using Pegasus.Core.Cases;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Assessment;
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
        var recorder = new CaseWebTestSupport.RecordingGenerateReport();
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
        Assert.Contains(StaffRole.User, request.Actor.Roles);
    }

    /// <summary>
    /// The report always ends with its fee note, so the generate form offers
    /// no fee-note choice and posts the report alone.
    /// </summary>
    [Fact]
    public async Task GenerateReportOffersNoFeeNoteChoice()
    {
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var caseId = Guid.NewGuid();
        var recorder = new CaseWebTestSupport.RecordingGenerateReport();
        using var factory = Compose(
            baseFactory,
            new FakeGetCase(caseId),
            FullAssessmentProjection(caseId),
            new FakeProjectionSource(ReadyInput(caseId)),
            new FakeRenderer([1]),
            generateReport: recorder);
        using var client = Client(factory);
        // v26: Generate report is the head's primary inside the edit session.
        var html = await EnterEditModeAsync(client, caseId);
        var generateForm = FormHtml(html, "GenerateReport");

        Assert.Contains("data-generate-report", generateForm, StringComparison.Ordinal);
        Assert.DoesNotContain("includeFeeNote", generateForm, StringComparison.Ordinal);
        Assert.DoesNotContain("form=\"case-generate-report-form\"", html, StringComparison.Ordinal);

        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=GenerateReport&section=report",
            Form(
                AntiforgeryValue(html),
                ("id", caseId.ToString("D")),
                ("operationKey", Guid.NewGuid().ToString("N")),
                ("editLeaseToken", "held-report-lease"),
                ("expectedCaseVersion", "0")));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var request = Assert.Single(recorder.Requests);
        Assert.Equal(CaseReportArtifactKind.AssessmentReport, request.Kind);
        Assert.Null(request.TargetGenerationId);
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
                services.AddSingleton<ICaseReportGenerationStore>(new FakeCurrentGeneration(caseId));
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
                    feeNoteStatus: status,
                    feeNoteOperationKey: retainedOperationKey));
            }));
        using var client = Client(factory);

        var html = await EnterEditModeAsync(client, caseId);
        var feeNoteForm = FormHtml(html, "GenerateFeeNote");

        Assert.Equal(retainedOperationKey, InputValue(feeNoteForm, "operationKey"));
        Assert.NotEqual(Guid.Empty, Guid.Parse(InputValue(feeNoteForm, "targetGenerationId")));
    }

    [Theory]
    [InlineData(CaseReportArtifactKind.RepairSpecification, CaseReportArtifactStatus.Pending)]
    [InlineData(CaseReportArtifactKind.RepairSpecification, CaseReportArtifactStatus.Failed)]
    [InlineData(CaseReportArtifactKind.RepairSpecification, CaseReportArtifactStatus.Unknown)]
    [InlineData(CaseReportArtifactKind.ImagePack, CaseReportArtifactStatus.Pending)]
    [InlineData(CaseReportArtifactKind.ImagePack, CaseReportArtifactStatus.Failed)]
    [InlineData(CaseReportArtifactKind.ImagePack, CaseReportArtifactStatus.Unknown)]
    public async Task UnconfirmedCompanionKeepsRetryActionWithItsRetainedOperationKey(
        CaseReportArtifactKind kind,
        CaseReportArtifactStatus status)
    {
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var caseId = Guid.NewGuid();
        const string retainedOperationKey = "retained-companion-operation";
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
                    repairSpecStatus: kind == CaseReportArtifactKind.RepairSpecification ? status : null,
                    repairSpecOperationKey: retainedOperationKey,
                    imagePackStatus: kind == CaseReportArtifactKind.ImagePack ? status : null,
                    imagePackOperationKey: retainedOperationKey));
            }));
        using var client = Client(factory);

        var html = await EnterEditModeAsync(client, caseId);
        var handler = kind == CaseReportArtifactKind.RepairSpecification
            ? "GenerateRepairSpec"
            : "GenerateImagePack";
        var form = FormHtml(html, handler);

        Assert.Equal(retainedOperationKey, InputValue(form, "operationKey"));
        Assert.NotEqual(Guid.Empty, Guid.Parse(InputValue(form, "targetGenerationId")));
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
                ("expectedCaseVersion", InputValue(initialForm, "expectedCaseVersion"))));

        Assert.Equal(HttpStatusCode.Redirect, failed.StatusCode);
        var retryHtml = await GetHtmlAsync(client, failed.Headers.Location!.OriginalString);
        var retryForm = FormHtml(retryHtml, "GenerateReport");
        Assert.Equal(retainedOperationKey, InputValue(retryForm, "operationKey"));

        using var retried = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=GenerateReport&section=report",
            Form(
                AntiforgeryValue(retryHtml),
                ("id", caseId.ToString("D")),
                ("operationKey", InputValue(retryForm, "operationKey")),
                ("editLeaseToken", InputValue(retryForm, "editLeaseToken")),
                ("expectedCaseVersion", InputValue(retryForm, "expectedCaseVersion"))));

        Assert.Equal(HttpStatusCode.Redirect, retried.StatusCode);
        Assert.Collection(
            generator.Requests,
            request =>
            {
                Assert.Equal(retainedOperationKey, request.OperationKey);
                Assert.Equal(0, request.ExpectedCaseVersion);
            },
            request =>
            {
                Assert.Equal(retainedOperationKey, request.OperationKey);
                Assert.Equal(0, request.ExpectedCaseVersion);
            });
        Assert.Equal(CaseReportGenerationOutcome.Generated, generator.LastOutcome);
    }

    [Fact]
    public async Task UnconfirmedReportDoesNotOfferGenerateWithoutAPreparedDraft()
    {
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var caseId = Guid.NewGuid();
        const string retainedOperationKey = "retained-report-operation";
        var projection = ReadyInput(caseId);
        using var factory = Compose(
            baseFactory,
            new FakeGetCase(caseId),
            FullAssessmentProjection(caseId),
            new UnavailableSnapshotSource(projection),
            new FakeRenderer([1]))
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<ICaseReportGenerationStore>();
                services.AddSingleton<ICaseReportGenerationStore>(new FakeCurrentGeneration(
                    caseId,
                    reportStatus: CaseReportArtifactStatus.Failed,
                    reportOperationKey: retainedOperationKey));
            }));
        using var client = Client(factory);

        var html = await EnterEditModeAsync(client, caseId);

        Assert.DoesNotContain("data-generate-report", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"case-generate-report-form\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain(retainedOperationKey, html, StringComparison.Ordinal);
    }

    /// <summary>
    /// A confirmed separate fee note is opened from the report card, beside
    /// Open report (issue 912); a fee note not yet confirmed offers no such
    /// link.
    /// </summary>
    [Theory]
    [InlineData(CaseReportArtifactStatus.Confirmed, true)]
    [InlineData(CaseReportArtifactStatus.Pending, false)]
    [InlineData(null, false)]
    public async Task TheReportCardOpensAConfirmedSeparateFeeNote(
        CaseReportArtifactStatus? feeNoteStatus,
        bool offered)
    {
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var caseId = Guid.NewGuid();
        var generation = new FakeCurrentGeneration(caseId, feeNoteStatus);
        using var factory = WithCurrentGeneration(baseFactory, caseId, generation);
        using var client = Client(factory);

        var card = ReportCard(await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=report"));

        OpenReportLink(card);
        if (offered)
        {
            var feeNote = generation.Record.Artifacts.Single(artifact => artifact.Kind == CaseReportArtifactKind.FeeNote);
            var link = CardLink(card, "data-report-fee-card", "Open fee note");
            Assert.Contains($"artifactId={feeNote.Id:D}", link, StringComparison.Ordinal);
            Assert.Contains("data-document-preview", link, StringComparison.Ordinal);
            Assert.Contains(
                $"<span>{Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.OpenFeeNote}</span>",
                link,
                StringComparison.Ordinal);
        }
        else
        {
            Assert.DoesNotContain("data-report-fee-card", card, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The card says when the report was generated and where its file stands,
    /// in the words Files uses for a file, on a chip whose colour is green only
    /// once the file is stored. A report that was never drawn gives no date. No
    /// state name from the code reaches the card.
    /// </summary>
    [Theory]
    [InlineData(CaseReportArtifactStatus.Confirmed, true, "Stored", "status--green", true)]
    [InlineData(CaseReportArtifactStatus.Pending, true, "Storing", "status--amber", true)]
    [InlineData(CaseReportArtifactStatus.Pending, false, "Not generated", "status--amber", false)]
    [InlineData(CaseReportArtifactStatus.Failed, true, "Storage failed", "status--red", true)]
    [InlineData(CaseReportArtifactStatus.Unknown, true, "Not confirmed", "status--neutral", true)]
    public async Task TheReportCardSaysWhereTheReportStandsInPlainWords(
        CaseReportArtifactStatus status, bool filed, string words, string tone, bool datesTheGeneration)
    {
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var caseId = Guid.NewGuid();
        using var factory = WithCurrentGeneration(baseFactory, caseId, status, filed);
        using var client = Client(factory);

        var card = ReportCard(WebUtility.HtmlDecode(await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=report")));

        Assert.Contains(
            $"<span class=\"status status--plain {tone}\" data-report-filing>{words}</span>",
            card,
            StringComparison.Ordinal);
        var generated = Pegasus.Core.LondonCalendar.TimeAt(ReportFixtureAtUtc)
            .ToString("d MMMM yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        var dated = $"<span>{Pegasus.Web.Presentation.CaseWorkspaceLabels.Report.Generated} <b>{generated}</b> · </span>";
        if (datesTheGeneration)
        {
            Assert.Contains(dated, card, StringComparison.Ordinal);
        }
        else
        {
            Assert.DoesNotContain(Pegasus.Web.Presentation.CaseWorkspaceLabels.Report.Generated, card, StringComparison.Ordinal);
        }

        // The card holds both facts; no list below repeats them.
        Assert.DoesNotContain("data-report-generation", card, StringComparison.Ordinal);
        var read = CaseWebTestSupport.VisibleText(card);
        Assert.DoesNotContain("State", read, StringComparison.Ordinal);
        foreach (var name in Enum.GetNames<CaseReportArtifactStatus>()
            .Concat(Enum.GetNames<CaseReportGenerationState>())
            .Concat(Enum.GetNames<CaseReportArtifactFiling>())
            .Where(name => name != words))
        {
            Assert.DoesNotContain(name, read, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Open report is offered only for a stored report, and opens it in the
    /// page's document viewer as Preview opens the draft: the same viewer
    /// attributes and the eye glyph, the file name the viewer titles it with,
    /// and a real link for a browser without script.
    /// </summary>
    [Theory]
    [InlineData(CaseReportArtifactStatus.Confirmed, true, true)]
    [InlineData(CaseReportArtifactStatus.Pending, true, false)]
    [InlineData(CaseReportArtifactStatus.Pending, false, false)]
    [InlineData(CaseReportArtifactStatus.Failed, true, false)]
    [InlineData(CaseReportArtifactStatus.Unknown, true, false)]
    public async Task OpenReportOpensTheStoredReportInTheViewer(
        CaseReportArtifactStatus status, bool filed, bool offered)
    {
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var caseId = Guid.NewGuid();
        var generation = new FakeCurrentGeneration(caseId, reportStatus: status, reportFiled: filed);
        using var factory = WithCurrentGeneration(baseFactory, caseId, generation);
        using var client = Client(factory);

        var card = ReportCard(WebUtility.HtmlDecode(await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=report")));

        if (!offered)
        {
            Assert.DoesNotContain("data-report-artifact", card, StringComparison.Ordinal);
            Assert.DoesNotContain(
                Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.OpenReport, card, StringComparison.Ordinal);
            return;
        }

        var link = OpenReportLink(card);
        var report = Assert.Single(generation.Record.Artifacts);
        Assert.Contains($"href=\"/Cases/{caseId:D}?", link, StringComparison.Ordinal);
        Assert.Contains("handler=GeneratedArtifact", link, StringComparison.Ordinal);
        Assert.Contains($"generationId={generation.Record.Id:D}", link, StringComparison.Ordinal);
        Assert.Contains($"artifactId={report.Id:D}", link, StringComparison.Ordinal);
        Assert.Contains("target=\"_blank\"", link, StringComparison.Ordinal);
        Assert.Contains("data-document-preview", link, StringComparison.Ordinal);
        Assert.Contains("data-no-inplace", link, StringComparison.Ordinal);
        Assert.Contains("data-file-name=\"CE_100_assessment.pdf\"", link, StringComparison.Ordinal);
        Assert.Contains("<use href=\"#icon-eye\" />", link, StringComparison.Ordinal);
        Assert.DoesNotContain("#icon-download", link, StringComparison.Ordinal);
        Assert.Contains(
            $"<span>{Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.OpenReport}</span>",
            link,
            StringComparison.Ordinal);
        // Only the draft's link carries the mark that names the viewer's Download draft.
        Assert.DoesNotMatch("data-report-preview(?![-\\w])", link);
        // Reading the Case is not arriving from a generation.
        Assert.DoesNotContain("data-open-on-arrival=\"true\"", link, StringComparison.Ordinal);
    }

    /// <summary>
    /// The page that follows a successful Generate report marks the report it
    /// has just stored, so the script opens it in the viewer. The mark is on
    /// that one page: reading the Case again does not carry it.
    /// </summary>
    [Fact]
    public async Task ThePageThatFollowsGenerateReportMarksTheReportToOpenOnce()
    {
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var caseId = Guid.NewGuid();
        var generator = new RecordingGenerationJourney(caseId, failFirst: false);
        using var factory = WithGenerationJourney(baseFactory, caseId, generator);
        using var client = Client(factory);
        var initialHtml = await EnterEditModeAsync(client, caseId);
        var form = FormHtml(initialHtml, "GenerateReport");

        using var generated = await PostGenerateReportAsync(client, caseId, initialHtml, form);

        Assert.Equal(HttpStatusCode.Redirect, generated.StatusCode);
        var arrived = WebUtility.HtmlDecode(await GetHtmlAsync(client, generated.Headers.Location!.OriginalString));
        Assert.Contains(
            Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.ReportGenerated, arrived, StringComparison.Ordinal);
        var link = OpenReportLink(ReportCard(arrived));
        Assert.Contains("data-open-on-arrival=\"true\"", link, StringComparison.Ordinal);
        Assert.Contains("data-document-preview", link, StringComparison.Ordinal);

        var later = WebUtility.HtmlDecode(await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=report"));
        Assert.DoesNotContain("data-open-on-arrival=\"true\"", later, StringComparison.Ordinal);
        Assert.Contains("data-document-preview", OpenReportLink(ReportCard(later)), StringComparison.Ordinal);
    }

    /// <summary>
    /// A generation that stored nothing leaves nothing to open: the page that
    /// follows a failed Generate report carries no mark.
    /// </summary>
    [Fact]
    public async Task AGenerationThatStoredNothingMarksNothingToOpen()
    {
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var caseId = Guid.NewGuid();
        var generator = new RecordingGenerationJourney(caseId, failFirst: true);
        using var factory = WithGenerationJourney(baseFactory, caseId, generator);
        using var client = Client(factory);
        var initialHtml = await EnterEditModeAsync(client, caseId);
        var form = FormHtml(initialHtml, "GenerateReport");

        using var failed = await PostGenerateReportAsync(client, caseId, initialHtml, form);

        Assert.Equal(HttpStatusCode.Redirect, failed.StatusCode);
        var arrived = await GetHtmlAsync(client, failed.Headers.Location!.OriginalString);
        Assert.DoesNotContain("data-open-on-arrival=\"true\"", arrived, StringComparison.Ordinal);
        Assert.DoesNotContain("data-report-artifact", arrived, StringComparison.Ordinal);
    }

    /// <summary>
    /// Generate report downloads nothing, so its button carries the glyph the
    /// other Generate actions carry.
    /// </summary>
    [Fact]
    public async Task GenerateReportCarriesTheGenerateGlyph()
    {
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var caseId = Guid.NewGuid();
        using var factory = Compose(
            baseFactory,
            new FakeGetCase(caseId),
            FullAssessmentProjection(caseId),
            new FakeProjectionSource(ReadyInput(caseId)),
            new FakeRenderer([1]));
        using var client = Client(factory);

        var form = FormHtml(await EnterEditModeAsync(client, caseId), "GenerateReport");

        Assert.Contains("<use href=\"#icon-file-text\" />", form, StringComparison.Ordinal);
        Assert.DoesNotContain("#icon-download", form, StringComparison.Ordinal);
    }

    /// <summary>
    /// Every change is saved as it is made (operator, 29 September 2026), so
    /// nothing is unsaved when Generate report is pressed: its form carries
    /// no save-first marker and no words for a press a save left not ready.
    /// </summary>
    [Fact]
    public async Task GenerateReportInEditModeCarriesNoSaveFirstMarker()
    {
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var caseId = Guid.NewGuid();
        using var factory = Compose(
            baseFactory,
            new FakeGetCase(caseId),
            FullAssessmentProjection(caseId),
            new FakeProjectionSource(ReadyInput(caseId)),
            new FakeRenderer([1]));
        using var client = Client(factory);

        var form = WebUtility.HtmlDecode(FormHtml(await EnterEditModeAsync(client, caseId), "GenerateReport"));

        Assert.DoesNotContain("data-case-save-first", form, StringComparison.Ordinal);
        Assert.DoesNotContain("data-save-first-dropped", form, StringComparison.Ordinal);
        Assert.Contains("name=\"editLeaseToken\"", form, StringComparison.Ordinal);
    }

    /// <summary>
    /// Delivery is the next action only once the report is stored. While its
    /// file is on its way to Box the aside says it is waited for; a report
    /// never drawn, failed or not confirmed is generated again.
    /// </summary>
    [Theory]
    [InlineData(CaseReportArtifactStatus.Confirmed, true, Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.PrepareDelivery)]
    [InlineData(CaseReportArtifactStatus.Pending, true, Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.WaitingForStorage)]
    [InlineData(CaseReportArtifactStatus.Pending, false, Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.GenerateReport)]
    [InlineData(CaseReportArtifactStatus.Failed, true, Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.GenerateReport)]
    [InlineData(CaseReportArtifactStatus.Unknown, true, Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.GenerateReport)]
    public async Task TheNextActionIsDeliveryOnlyOnceTheReportIsStored(
        CaseReportArtifactStatus status, bool filed, string expected)
    {
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var caseId = Guid.NewGuid();
        using var factory = WithCurrentGeneration(baseFactory, caseId, status, filed);
        using var client = Client(factory);

        var html = WebUtility.HtmlDecode(await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=report"));

        var nextAction = CaseWebTestSupport.NextActionRegex().Match(html);
        Assert.True(nextAction.Success, "The Case aside must state its Next action.");
        Assert.Equal(expected, NextLabelRegex().Match(nextAction.Value).Groups["label"].Value);
        var link = SectionJumpRegex().Match(nextAction.Value);
        Assert.True(link.Success, "The Next action must link to a section.");
        Assert.Equal("report", link.Groups["key"].Value);
    }

    /// <summary>
    /// A stale generation is stated once, in the aside's Next action above its
    /// Generate report line (issue 899): no page-wide bar and no second
    /// notice in the Report section.
    /// </summary>
    [Fact]
    public async Task AStaleGenerationIsStatedOnceInTheNextAction()
    {
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var caseId = Guid.NewGuid();
        using var factory = WithCurrentGeneration(
            baseFactory, caseId, new FakeCurrentGeneration(caseId, stale: true));
        using var client = Client(factory);

        var html = WebUtility.HtmlDecode(await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=report"));

        var nextAction = CaseWebTestSupport.NextActionRegex().Match(html);
        Assert.True(nextAction.Success, "The Case aside must state its Next action.");
        Assert.Contains("data-report-stale", nextAction.Value, StringComparison.Ordinal);
        Assert.Contains(
            Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.GenerationStaleNotice,
            nextAction.Value,
            StringComparison.Ordinal);
        Assert.Equal(
            Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.GenerateReport,
            NextLabelRegex().Match(nextAction.Value).Groups["label"].Value);
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Count(html, "data-report-stale"));
        Assert.DoesNotContain("data-stale-bar", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConfirmedFeeNoteDownloadIsLimitedToFeePane()
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
                services.AddSingleton<ICaseReportGenerationStore>(new FakeCurrentGeneration(
                    caseId,
                    feeNoteStatus: CaseReportArtifactStatus.Confirmed));
            }));
        using var client = Client(factory);

        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=report");
        var reportPaneStart = html.IndexOf("<div id=\"report-pane-report\"", StringComparison.Ordinal);
        var feePaneStart = html.IndexOf("<div id=\"report-pane-fee\"", StringComparison.Ordinal);
        Assert.True(reportPaneStart >= 0);
        Assert.True(feePaneStart > reportPaneStart);

        var reportPane = html[reportPaneStart..feePaneStart];
        var feePane = html[feePaneStart..];
        Assert.Contains("data-report-artifact=\"AssessmentReport\"", reportPane, StringComparison.Ordinal);
        Assert.Contains("data-report-fee-artifact", feePane, StringComparison.Ordinal);
        Assert.DoesNotContain("data-report-artifact=\"FeeNote\"", html, StringComparison.Ordinal);
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
                ("coveringMessage", "Edited by staff.\r\n\r\nKind regards"),
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
        // The message the operator submitted goes to preparation as posted;
        // Core freezes it.
        Assert.Equal("Edited by staff.\r\n\r\nKind regards", request.CoveringMessage);
        Assert.Equal(ActorKind.Staff, request.Actor.Kind);
        Assert.Equal("reviewed@recipient.example", Assert.Single(request.ReviewedRecipients!.To));
        Assert.Equal("copy@recipient.example", Assert.Single(request.ReviewedRecipients.Cc));
        Assert.Empty(send.Requests);
    }

    /// <summary>
    /// v28 P22: the delivery attaches the documents the operator ticked, and
    /// the Attach choice reaches the preparation as the operator made it.
    /// </summary>
    [Fact]
    public async Task PrepareDeliveryCarriesTheDocumentsTheOperatorChose()
    {
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var caseId = Guid.NewGuid();
        var generationId = Guid.NewGuid();
        var prepare = new RecordingPrepareDelivery(caseId, generationId);
        using var factory = Compose(
            baseFactory,
            new FakeGetCase(caseId),
            FullAssessmentProjection(caseId),
            new FakeProjectionSource(ReadyInput(caseId)),
            new FakeRenderer([1]),
            prepareDelivery: prepare);
        using var client = Client(factory);
        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=report");

        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=PrepareReportDelivery&section=report",
            Form(
                AntiforgeryValue(html),
                ("id", caseId.ToString("D")),
                ("operationKey", Guid.NewGuid().ToString("N")),
                ("editLeaseToken", "held-report-lease"),
                ("expectedCaseVersion", "0"),
                ("generationId", generationId.ToString("D")),
                ("expectedGenerationVersion", "13"),
                ("coveringMessage", "Edited by staff.\r\n\r\nKind regards"),
                ("toRecipients", "reviewed@recipient.example"),
                ("attach", nameof(CaseReportArtifactKind.AssessmentReport)),
                ("attach", nameof(CaseReportArtifactKind.ImagePack))));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var request = Assert.Single(prepare.Requests);
        Assert.Equal(
            [CaseReportArtifactKind.AssessmentReport, CaseReportArtifactKind.ImagePack],
            request.Attach);
    }

    /// <summary>
    /// The message is what staff review before Prepare delivery: a blank one
    /// never reaches preparation and the operator is told why.
    /// </summary>
    [Fact]
    public async Task PrepareDeliveryRefusesABlankMessage()
    {
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var caseId = Guid.NewGuid();
        var generationId = Guid.NewGuid();
        var prepare = new RecordingPrepareDelivery(caseId, generationId);
        using var factory = Compose(
            baseFactory,
            new FakeGetCase(caseId),
            FullAssessmentProjection(caseId),
            new FakeProjectionSource(ReadyInput(caseId)),
            new FakeRenderer([1]),
            prepareDelivery: prepare);
        using var client = Client(factory);
        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=report");

        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=PrepareReportDelivery&section=report",
            Form(
                AntiforgeryValue(html),
                ("id", caseId.ToString("D")),
                ("operationKey", Guid.NewGuid().ToString("N")),
                ("editLeaseToken", "held-report-lease"),
                ("expectedCaseVersion", "0"),
                ("generationId", generationId.ToString("D")),
                ("expectedGenerationVersion", "13"),
                ("coveringMessage", "  "),
                ("toRecipients", "reviewed@recipient.example")));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Empty(prepare.Requests);
    }

    /// <summary>
    /// A preparation whose send failed is spent: Send is not offered for it
    /// again and Prepare delivery is offered instead, so a fresh preparation
    /// can be sent once the cause is put right. An unsent preparation keeps
    /// its Send and offers no second Prepare.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData(StaffMailState.Failed)]
    public async Task AFailedSendSpendsThePreparationAndOffersPrepareDeliveryAgain(
        StaffMailState? latestSendState)
    {
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var caseId = Guid.NewGuid();
        var preparationId = Guid.NewGuid();
        var generation = new FakeCurrentGeneration(caseId);
        using var factory = Compose(
            baseFactory,
            new FakeGetCase(caseId),
            FullAssessmentProjection(caseId),
            new FakeProjectionSource(ReadyInput(caseId)),
            new FakeRenderer([1]),
            sendPreparedReport: new RecordingSendPreparedReport(StaffMailState.Failed))
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<ICaseReportGenerationStore>();
                services.AddSingleton<ICaseReportGenerationStore>(generation);
                services.RemoveAll<ICaseReportDeliveryPreparationStore>();
                services.AddSingleton<ICaseReportDeliveryPreparationStore>(
                    new FakeDeliveryPreparation(caseId, generation.Record.Id, preparationId, latestSendState));
            }));
        using var client = Client(factory);

        var html = await EnterEditModeAsync(client, caseId);

        if (latestSendState is StaffMailState.Failed)
        {
            Assert.DoesNotContain("data-send-prepared", html, StringComparison.Ordinal);
            Assert.Contains("handler=PrepareReportDelivery", FormHtml(html, "PrepareReportDelivery"), StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains("data-send-prepared", html, StringComparison.Ordinal);
            Assert.DoesNotContain("handler=PrepareReportDelivery", html, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(StaffMailState.Submitted)]
    [InlineData(StaffMailState.Unknown)]
    [InlineData(StaffMailState.Failed)]
    public async Task SendPreparedReportDerivesStableOperationKeyAndDoesNotClaimSent(
        StaffMailState returnedState)
    {
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var caseId = Guid.NewGuid();
        var preparationId = Guid.NewGuid();
        var send = new RecordingSendPreparedReport(returnedState);
        var generation = new FakeCurrentGeneration(caseId);
        var snapshots = new RecordingRepairSpecificationSnapshots();
        using var factory = Compose(
            baseFactory,
            new FakeGetCase(caseId),
            FullAssessmentProjection(caseId),
            new FakeProjectionSource(ReadyInput(caseId)),
            new FakeRenderer([1]),
            sendPreparedReport: send)
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<ICaseReportGenerationStore>();
                services.AddSingleton<ICaseReportGenerationStore>(generation);
                services.RemoveAll<ICaseReportDeliveryPreparationStore>();
                services.AddSingleton<ICaseReportDeliveryPreparationStore>(
                    new FakeDeliveryPreparation(caseId, generation.Record.Id, preparationId));
                services.RemoveAll<IRepairSpecificationSnapshotStore>();
                services.AddSingleton<IRepairSpecificationSnapshotStore>(snapshots);
            }));
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
        var expectedMessage = returnedState switch
        {
            StaffMailState.Submitted => Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.SendAccepted,
            StaffMailState.Failed => Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.SendFailed,
            _ => Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.SendUnknown
        };
        Assert.Contains(expectedMessage, reloaded, StringComparison.Ordinal);
        Assert.DoesNotContain(
            Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.SendObservedSent,
            reloaded,
            StringComparison.Ordinal);
        Assert.Empty(snapshots.Requests);
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

    /// <summary>The Case page over one current generation whose report stands as given.</summary>
    private static WebApplicationFactory<Program> WithCurrentGeneration(
        IntakeWebApplicationFactory baseFactory,
        Guid caseId,
        CaseReportArtifactStatus status,
        bool filed) =>
        WithCurrentGeneration(
            baseFactory,
            caseId,
            new FakeCurrentGeneration(caseId, reportStatus: status, reportFiled: filed));

    private static WebApplicationFactory<Program> WithCurrentGeneration(
        IntakeWebApplicationFactory baseFactory,
        Guid caseId,
        FakeCurrentGeneration generation) =>
        Compose(
            baseFactory,
            new FakeGetCase(caseId),
            FullAssessmentProjection(caseId),
            new FakeProjectionSource(ReadyInput(caseId)),
            new FakeRenderer([1]))
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<ICaseReportGenerationStore>();
                services.AddSingleton<ICaseReportGenerationStore>(generation);
            }));

    /// <summary>The Case page over a generation the page itself asks for and then reads back.</summary>
    private static WebApplicationFactory<Program> WithGenerationJourney(
        IntakeWebApplicationFactory baseFactory,
        Guid caseId,
        RecordingGenerationJourney generator) =>
        Compose(
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

    /// <summary>Generate report as the page's own form posts it.</summary>
    private static Task<HttpResponseMessage> PostGenerateReportAsync(
        HttpClient client, Guid caseId, string html, string form) =>
        client.PostAsync(
            $"/Cases/{caseId:D}?handler=GenerateReport&section=report",
            Form(
                AntiforgeryValue(html),
                ("id", caseId.ToString("D")),
                ("operationKey", InputValue(form, "operationKey")),
                ("editLeaseToken", InputValue(form, "editLeaseToken")),
                ("expectedCaseVersion", InputValue(form, "expectedCaseVersion"))));

    /// <summary>The Report section's card: its title, its status line and Open report.</summary>
    private static string ReportCard(string html)
    {
        var start = html.IndexOf("<div class=\"pv\" data-report-preview-card>", StringComparison.Ordinal);
        Assert.True(start >= 0, "The Report section must render its card.");
        var end = html.IndexOf("<div class=\"fg g4\">", start, StringComparison.Ordinal);
        Assert.True(end > start, "The report's fields must follow its card.");
        return html[start..end];
    }

    /// <summary>The card's link to the stored report.</summary>
    private static string OpenReportLink(string card) =>
        CardLink(card, "data-report-artifact=\"AssessmentReport\"", "Open report");

    /// <summary>The card's link carrying <paramref name="mark"/>.</summary>
    private static string CardLink(string card, string mark, string name)
    {
        var link = System.Text.RegularExpressions.Regex.Match(
            card,
            $"<a[^>]*{mark}[^>]*>.*?</a>",
            System.Text.RegularExpressions.RegexOptions.Singleline
                | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        Assert.True(link.Success, $"The card must offer {name}.");
        return link.Value;
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
        // All report-delivery actions in this suite run as a User. The
        // request recorders below prove the page still carries the server-side
        // actor, version and lease through generation, preparation and send.
        client.DefaultRequestHeaders.Add("X-Test-Roles", "User");
        return client;
    }

    /// <summary>
    /// One confirmed current generation, so the card the page renders can be
    /// read. Only the reads the Case page makes are answered.
    /// </summary>
    internal sealed class FakeCurrentGeneration(
        Guid caseId,
        CaseReportArtifactStatus? feeNoteStatus = null,
        string? feeNoteOperationKey = null,
        CaseReportArtifactStatus reportStatus = CaseReportArtifactStatus.Confirmed,
        string? reportOperationKey = null,
        CaseReportArtifactStatus? repairSpecStatus = null,
        string? repairSpecOperationKey = null,
        CaseReportArtifactStatus? imagePackStatus = null,
        string? imagePackOperationKey = null,
        bool reportFiled = false,
        bool stale = false)
        : ICaseReportGenerationStore
    {
        private readonly CaseReportGenerationRecord record = GenerationRecord(
            caseId,
            feeNoteStatus,
            feeNoteOperationKey,
            reportStatus,
            reportOperationKey,
            repairSpecStatus,
            repairSpecOperationKey,
            imagePackStatus,
            imagePackOperationKey,
            reportFiled) is var generation && stale
                ? generation with { State = CaseReportGenerationState.Stale }
                : generation;

        public CaseReportGenerationRecord Record => record;

        public Task<CaseReportGenerationRecord?> GetCurrentAsync(
            ActionActor actor, Guid id, CaseWorkSelector work, CancellationToken cancellationToken) =>
            Task.FromResult<CaseReportGenerationRecord?>(record);

        public Task<CaseReportGenerationRecord?> GetAsync(
            ActionActor actor, Guid id, Guid generationId, CancellationToken cancellationToken) =>
            Task.FromResult<CaseReportGenerationRecord?>(record);

        public Task<IReadOnlyList<CaseReportGenerationRecord>> ListAsync(
            ActionActor actor, Guid id, CaseWorkSelector work, CancellationToken cancellationToken) =>
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
            CaseWorkSelector work, CancellationToken cancellationToken) => Task.FromResult(current);

        public Task<CaseReportGenerationRecord?> GetAsync(
            ActionActor actor,
            Guid id,
            Guid generationId,
            CancellationToken cancellationToken) => Task.FromResult(current);

        public Task<IReadOnlyList<CaseReportGenerationRecord>> ListAsync(
            ActionActor actor,
            Guid id,
            CaseWorkSelector work, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<CaseReportGenerationRecord>>(
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
        CaseReportArtifactStatus? feeNoteStatus,
        string? feeNoteOperationKey,
        CaseReportArtifactStatus reportStatus = CaseReportArtifactStatus.Confirmed,
        string? reportOperationKey = null,
        CaseReportArtifactStatus? repairSpecStatus = null,
        string? repairSpecOperationKey = null,
        CaseReportArtifactStatus? imagePackStatus = null,
        string? imagePackOperationKey = null,
        bool reportFiled = false)
    {
        reportOperationKey ??= "operation-1";
        var input = ReadyInput(caseId);
        var report = AssessmentReportProjection.Project(input).Snapshot!;
        var generationId = Guid.NewGuid();
        var snapshot = new CaseReportGenerationSnapshot(
            caseId, 0, "CE-100", reportOperationKey, CaseReportActor.None, ReportFixtureAtUtc,
            Guid.NewGuid(), new string('a', 64), "image/png", input.CurrentEstimate!.SpecificationId, input.CurrentEstimate.Version,
            report.Costs, report.EngineerValue, Guid.NewGuid(),
            report.Content, report.Guides, report.ReportDate, false,
            report.AgreedFee, report.FeeDescriptionLines, [], [],
            AssessmentReportContract.TemplateVersion, "fake", report)
        {
            CurrentEstimate = input.CurrentEstimate
        };
        var reportConfirmed = reportStatus == CaseReportArtifactStatus.Confirmed;
        // A report that is not confirmed may still have a file: custody gave
        // it a version, which carries the document's own facts.
        var reportHasFile = reportConfirmed || reportFiled;
        List<CaseReportArtifactRecord> artifacts =
        [
            new(
                Guid.NewGuid(), generationId, CaseReportArtifactKind.AssessmentReport,
                reportStatus, reportOperationKey,
                reportHasFile ? Guid.NewGuid() : null,
                reportHasFile ? Guid.NewGuid() : null,
                reportConfirmed ? new string('c', 64) : null,
                reportHasFile ? 3 : null,
                reportHasFile ? "CE_100_assessment.pdf" : null,
                reportHasFile ? "application/pdf" : null,
                null, null,
                reportHasFile && !reportConfirmed ? "pending/ce-100-assessment" : null,
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
        AddCompanionArtifact(
            artifacts,
            generationId,
            CaseReportArtifactKind.RepairSpecification,
            repairSpecStatus,
            repairSpecOperationKey,
            "CE_100_repair_specification.pdf");
        AddCompanionArtifact(
            artifacts,
            generationId,
            CaseReportArtifactKind.ImagePack,
            imagePackStatus,
            imagePackOperationKey,
            "CE_100_images.pdf");
        return new(
            generationId, caseId, 0, 1, new string('b', 64), snapshot,
            AssessmentReportContract.TemplateVersion, "fake",
            reportConfirmed
                ? CaseReportGenerationState.Confirmed
                : CaseReportGenerationState.Pending,
            ReportFixtureAtUtc, null,
            artifacts);
    }

    private static void AddCompanionArtifact(
        List<CaseReportArtifactRecord> artifacts,
        Guid generationId,
        CaseReportArtifactKind kind,
        CaseReportArtifactStatus? status,
        string? operationKey,
        string fileName)
    {
        if (status is not { } value)
        {
            return;
        }

        var confirmed = value == CaseReportArtifactStatus.Confirmed;
        artifacts.Add(new(
            Guid.NewGuid(), generationId, kind, value,
            operationKey ?? $"{kind}-operation",
            confirmed ? Guid.NewGuid() : null,
            confirmed ? Guid.NewGuid() : null,
            confirmed ? new string('c', 64) : null,
            confirmed ? 3 : null,
            confirmed ? fileName : null,
            confirmed ? "application/pdf" : null,
            null, null, null,
            value == CaseReportArtifactStatus.Failed ? "transient_failure" : null));
    }

    private sealed class UnavailableSnapshotSource(AssessmentReportProjectionInput input)
        : IAssessmentReportProjectionSource, ICaseReportSnapshotSource
    {
        public Task<AssessmentReportProjectionInput?> GetAsync(
            Guid caseId,
            ActionActor actor,
            CaseWorkSelector work,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<AssessmentReportProjectionInput?>(input);

        Task<CaseReportFreezeInputs?> ICaseReportSnapshotSource.GetAsync(
            Guid caseId,
            ActionActor actor,
            CaseWorkSelector work, ReportProjectionReuse? reuse, CancellationToken cancellationToken) =>
            Task.FromResult<CaseReportFreezeInputs?>(null);
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

    private sealed class FakeDeliveryPreparation(
        Guid caseId, Guid generationId, Guid preparationId, StaffMailState? latestSendState = null)
        : ICaseReportDeliveryPreparationStore
    {
        private readonly CaseReportDeliveryPreparationRecord record = new(
            new(preparationId, caseId, generationId, 1, 1, [], ActionActor.SystemWorker("test"), ReportFixtureAtUtc, "fingerprint"),
            new([], [], "subject"),
            0,
            0,
            CaseReportGenerationState.Confirmed,
            true,
            1,
            [],
            LatestSendState: latestSendState);

        public Task<CaseReportDeliveryPreparationRecord> PrepareAsync(
            PrepareCaseReportDeliveryCommand command,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CaseReportDeliveryPreparationRecord?> GetAsync(
            ActionActor actor,
            Guid ownerCaseId,
            Guid ownerPreparationId,
            CancellationToken cancellationToken) =>
            Task.FromResult<CaseReportDeliveryPreparationRecord?>(
                ownerCaseId == caseId && ownerPreparationId == preparationId ? record : null);

        public Task<CaseReportDeliveryPreparationRecord?> GetCurrentAsync(
            ActionActor actor,
            Guid ownerCaseId,
            CaseWorkSelector work,
            CancellationToken cancellationToken) =>
            Task.FromResult<CaseReportDeliveryPreparationRecord?>(
                ownerCaseId == caseId ? record : null);
    }

    private sealed class RecordingRepairSpecificationSnapshots : IRepairSpecificationSnapshotStore
    {
        public List<FreezeRepairSpecificationRequest> Requests { get; } = [];

        public Task<RepairSpecificationSnapshot> FreezeAsync(
            FreezeRepairSpecificationRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new RepairSpecificationSnapshot(
                Guid.NewGuid(), request.CaseId, request.SpecificationId, 1, request.Kind,
                request.Origin, request.Actor.SubjectId, ReportFixtureAtUtc,
                new EstimateDetails("Sent", 0m, null, 0m), [], 0m, true));
        }

        public Task<IReadOnlyList<RepairSpecificationSnapshot>> ListAsync(
            Guid caseId, Guid specificationId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RepairSpecificationSnapshot>>([]);

        public Task<RepairSpecificationSnapshot?> GetAsync(
            Guid caseId, Guid snapshotId, CancellationToken cancellationToken) =>
            Task.FromResult<RepairSpecificationSnapshot?>(null);
    }

    private static FormUrlEncodedContent Form(
        string antiforgeryToken, params (string Name, string Value)[] values)
    {
        var fields = values
            .Select(item => new KeyValuePair<string, string>(item.Name, item.Value))
            .Append(new("__RequestVerificationToken", antiforgeryToken));
        return new(fields);
    }

    private static string AntiforgeryValue(string html)
    {
        var tag = AntiforgeryTagRegex().Match(html);
        Assert.True(tag.Success, "The case action must render an antiforgery token.");
        var value = ValueRegex().Match(tag.Value);
        Assert.True(value.Success, "The case antiforgery token must have a value.");
        return WebUtility.HtmlDecode(value.Groups["value"].Value);
    }

    [GeneratedRegex("<input[^>]*name=\"__RequestVerificationToken\"[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AntiforgeryTagRegex();

    [GeneratedRegex("value=\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ValueRegex();
}
