using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Actors;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;

namespace Pegasus.IntegrationTests.Reports;

/// <summary>
/// Proves the DELIV-012 report-draft entry point is actually reachable from
/// the web: a complete case renders and returns a PDF, and an incomplete
/// case fails closed with its readiness reasons named instead of throwing.
/// <see cref="IAssessmentReportRenderer"/> is substituted with a fast fake so
/// this suite does not need a Chromium install — the real Playwright
/// renderer already has its own coverage in
/// <c>tests/Pegasus.IntegrationTests/Reports/AssessmentReportRendererTests.cs</c>.
/// Everything upstream of the renderer (the projection, the readiness gate,
/// the page wiring, authorisation) is exercised for real.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed partial class AssessmentReportDraftWebTests
{
    private static readonly DateTimeOffset ReportFixtureAtUtc =
        new(2026, 8, 3, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CompleteCaseRendersAndReturnsThePdf()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var caseId = Guid.NewGuid();
        var pdfBytes = new byte[] { 1, 2, 3, 4 };
        using var factory = Compose(
            baseFactory,
            new FakeGetCase(caseId),
            FullAssessmentProjection(caseId),
            new FakeProjectionSource(ReadyInput(caseId)),
            new FakeRenderer(pdfBytes));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=report");
        Assert.Contains(AssessmentReportProjection.RepairCostRequirement, html, StringComparison.Ordinal);

        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=GenerateReportDraft&section=report",
            Form(AntiforgeryValue(html), ("id", caseId.ToString("D")), ("operationKey", NewOperationKey())));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(pdfBytes, await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task PreviewRemainsAGetOnTheCaseHandlerAndReturnsThePdf()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var caseId = Guid.NewGuid();
        var pdfBytes = new byte[] { 4, 3, 2, 1 };
        using var factory = Compose(
            baseFactory,
            new FakeGetCase(caseId),
            FullAssessmentProjection(caseId),
            new FakeProjectionSource(ReadyInput(caseId)),
            new FakeRenderer(pdfBytes));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        using var response = await client.GetAsync(
            $"/Cases/{caseId:D}?handler=PreviewReportDraft&section=report");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(pdfBytes, await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task IncompleteCaseFailsClosedNamingWhatIsMissingInsteadOfThrowing()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var caseId = Guid.NewGuid();
        using var factory = Compose(
            baseFactory,
            new FakeGetCase(caseId),
            FullAssessmentProjection(caseId),
            new FakeProjectionSource(ReadyInput(caseId) with { CurrentEstimate = null }),
            new FakeRenderer([1]));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=report");
        Assert.Contains("Not ready", html, StringComparison.Ordinal);
        Assert.Contains(AssessmentReportProjection.RepairCostRequirement, html, StringComparison.Ordinal);
        // FRD-11: the control stays, disabled with its condition — no
        // submittable Generate form and no Preview link are offered.
        Assert.DoesNotContain("handler=\"GenerateReportDraft\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Preview report draft", html, StringComparison.Ordinal);

        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=GenerateReportDraft&section=report",
            Form(AntiforgeryValue(html), ("id", caseId.ToString("D")), ("operationKey", NewOperationKey())));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/Cases/{caseId:D}?section=estimate", response.Headers.Location?.OriginalString);

        var afterHtml = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=report");
        Assert.Contains(AssessmentReportProjection.RepairCostRequirement, afterHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CaseOutsideTheCurrentExportedReviewCycleCannotGenerateDirectly()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var caseId = Guid.NewGuid();
        using var factory = Compose(
            baseFactory,
            new FakeGetCase(caseId),
            FullAssessmentProjection(caseId),
            new FakeProjectionSource(ReadyInput(caseId)),
            new FakeRenderer([1]),
            canOpen: false);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=report");
        // D11: the workspace has not opened, so the control stays disabled
        // with its condition instead of offering a form that 404s.
        Assert.DoesNotContain("handler=\"GenerateReportDraft\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Preview report draft", html, StringComparison.Ordinal);

        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=GenerateReportDraft&section=report",
            Form(AntiforgeryValue(html), ("id", caseId.ToString("D")), ("operationKey", NewOperationKey())));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static WebApplicationFactory<Program> Compose(
        IntakeWebApplicationFactory baseFactory,
        IGetCase getCase,
        CaseAssessmentProjection assessment,
        IAssessmentReportProjectionSource projectionSource,
        IAssessmentReportRenderer renderer,
        bool canOpen = true) =>
        baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetCase>();
                services.RemoveAll<IGetCaseAssessment>();
                services.RemoveAll<IGetAssessmentAccess>();
                services.RemoveAll<IGetAssessmentWorkspace>();
                services.RemoveAll<IAssessmentReportProjectionSource>();
                services.RemoveAll<IAssessmentReportRenderer>();
                services.RemoveAll<IDocumentContentStore>();
                services.AddSingleton(getCase);
                services.AddSingleton<IGetCaseAssessment>(new FakeGetCaseAssessment(assessment));
                services.AddSingleton<IGetAssessmentAccess>(new FakeGetAssessmentAccess(canOpen));
                services.AddSingleton<IGetAssessmentWorkspace>(new FakeGetAssessmentWorkspace(
                    AssessmentWorkspaceTestData.Create(assessment)));
                services.AddSingleton(projectionSource);
                services.AddSingleton(renderer);
                services.AddSingleton<IDocumentContentStore>(new ThrowingDocumentContentStore());
            }));

    internal static AssessmentReportProjectionInput ReadyInput(Guid caseId)
    {
        var image = new byte[] { 137, 80, 78, 71, 1, 2, 3, 4 };
        var photo = new ReportImageEvidence(
            "site.jpg", "image/jpeg", image, Convert.ToHexStringLower(SHA256.HashData(image)));
        var source = new AcceptedReportSource("instruction.pdf", "1", new string('a', 64));
        return new AssessmentReportProjectionInput(
            FullAssessmentProjection(caseId),
            ClaimantName: "Alex Example",
            OurReference: "CE-100",
            YourReference: "P-100",
            ReportFor: ["Approved Principal"],
            ReportDate: new DateOnly(2026, 8, 19),
            Photos: [photo],
            Sources: [source],
            CurrentEstimate: CurrentEstimate(),
            Signatory: new ReportSignatory("Ed Mawdsley", "ATA VDA AQP", [1, 2, 3], "image/png"));
    }

    /// <summary>
    /// The Current estimate the ready fixture prices from: 50 parts, five
    /// panel hours at 30, 20 materials and 5 specialist, at 20 per cent VAT.
    /// </summary>
    internal static RepairSpecificationVersion CurrentEstimate() => new(
        Guid.NewGuid(), Guid.NewGuid(), 2, RepairSpecificationState.Accepted,
        new(RepairSpecificationSourceRoute.Manual, null, null, null),
        [
            EstimateLine(1, "repair", "Nearside door", 5m, null),
            EstimateLine(2, "new_part", "Door skin", null, 50m),
        ],
        null, "engineer-1", ReportFixtureAtUtc, "engineer-1", ReportFixtureAtUtc, null, null,
        new EstimateDetails("Repairer", null, 30m, 20m, 5m, 20m, null), IsCurrent: true);

    private static CaseEstimateLineRecord EstimateLine(
        int position, string type, string description, decimal? workUnits, decimal? price) => new(
            Guid.NewGuid(), position, type, null, description, workUnits, price, false, null, null,
            "confirmed", "case", "Test evidence",
            ActorKind.Staff, "engineer-1", ReportFixtureAtUtc, "engineer-1", ReportFixtureAtUtc,
            Quantity: 1);

    /// <summary>
    /// Every assessment field <see cref="AssessmentPolicy.EvaluateReadiness"/>
    /// requires, confirmed — the same fixture shape as the Core projection
    /// tests (<c>tests/Pegasus.Core.Tests/Reports/AssessmentReportProjectionTests.cs</c>),
    /// so a "ready" web test genuinely reaches the renderer rather than
    /// tripping over the shared readiness rail.
    /// </summary>
    internal static CaseAssessmentProjection FullAssessmentProjection(Guid caseId)
    {
        var confirmedAt = DateTimeOffset.UtcNow;
        AssessmentFieldValue Field(string path, string value) => new(
            path, value, ActorKind.Staff, "engineer-1", confirmedAt, "engineer-1", confirmedAt);

        var fields = new[]
        {
            Field(AssessmentVocabulary.VehicleType, "car"),
            Field(AssessmentVocabulary.VehicleYear, "2012"),
            Field(AssessmentVocabulary.VehicleMileageSource, "online_data"),
            Field(AssessmentVocabulary.VehicleCondition, "good"),
            Field(AssessmentVocabulary.IncidentAssessed, "2026-08-03"),
            Field(AssessmentVocabulary.ImpactSeverity, "moderate"),
            Field(AssessmentVocabulary.ImpactLocation, "right_rear"),
            Field(AssessmentVocabulary.ValueRetail, "5000.00"),
            Field(AssessmentVocabulary.ValueTrade, "4000.00"),
            Field(AssessmentVocabulary.ValueEngineer, "5000.00"),
            Field(AssessmentVocabulary.CostRepairerVatRegistered, "true"),
            Field(AssessmentVocabulary.Outcome, "repairable"),
            Field(AssessmentVocabulary.LegalStatus, "roadworthy"),
            Field(AssessmentVocabulary.HistoryCheck, "History clear"),
            Field(AssessmentVocabulary.EngineerName, "A Patterson"),
            Field(AssessmentVocabulary.EngineerQualifications, "M.Inst.IAEA"),
            Field(AssessmentVocabulary.EngineerSignature, "andy_patterson"),
            Field(AssessmentVocabulary.AgreedFee, "120.00"),
        };
        var caseOwned = new AssessmentCaseOwnedData(
            Registration: "PK12TMZ",
            Make: "Ford",
            Model: "Focus",
            Mileage: 80_000,
            MileageUnit: "miles",
            IncidentDate: new DateOnly(2026, 8, 1),
            InstructionDate: new DateOnly(2026, 8, 2),
            InspectionMode: "ImageBasedAssessment",
            InspectionAddress: null);
        return new CaseAssessmentProjection(
            caseId, "CE-100", 0, CaseLifecycleState.Review, Guid.NewGuid(), fields, [], caseOwned);
    }

    private static string NewOperationKey() => Guid.NewGuid().ToString("N");

    private static async Task<string> GetHtmlAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static FormUrlEncodedContent Form(
        string antiforgeryToken, params (string Name, string Value)[] values)
    {
        var fields = values.ToDictionary(item => item.Name, item => item.Value, StringComparer.Ordinal);
        fields["__RequestVerificationToken"] = antiforgeryToken;
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

    private sealed class FakeGetCase(Guid caseId) : IGetCase
    {
        public Task<CaseDetails?> ExecuteAsync(GetCaseQuery query, CancellationToken cancellationToken)
        {
            if (query.CaseId != caseId)
            {
                return Task.FromResult<CaseDetails?>(null);
            }

            var identity = new CaseIdentity(caseId, "QDOS", 2026, 42, "QDOS-2026-00042");
            var workflow = new CaseWorkflowRecord(
                caseId, identity, CaseLifecycleState.ReportPreparation, null, null,
                null, null, null, null, null, 0);
            var summary = new CaseSearchItem(
                caseId, identity.Reference, null, CaseType.Inspection, "Approved Principal",
                workflow.State, null, "AB12CDE", "Alex Example", "P-100",
                DateTimeOffset.UtcNow, new DateOnly(2026, 8, 1), "Email", DateTimeOffset.UtcNow);
            CaseDetails details = new(
                summary, workflow, null, [], null, CaseCustodyState.Pending, [], [], []);
            return Task.FromResult<CaseDetails?>(details);
        }
    }

    private sealed class FakeGetCaseAssessment(CaseAssessmentProjection projection) : IGetCaseAssessment
    {
        public Task<CaseAssessmentProjection?> ExecuteAsync(Guid caseId, CancellationToken cancellationToken) =>
            Task.FromResult<CaseAssessmentProjection?>(projection);
    }

    private sealed class FakeProjectionSource(AssessmentReportProjectionInput input)
        : IAssessmentReportProjectionSource
    {
        public Task<AssessmentReportProjectionInput?> GetAsync(
            Guid caseId, ActionActor actor, CancellationToken cancellationToken = default) =>
            Task.FromResult<AssessmentReportProjectionInput?>(input);
    }

    private sealed class FakeRenderer(byte[] pdfBytes) : IAssessmentReportRenderer
    {
        public string EngineVersion => "fake";

        public Task<RenderedReportArtifact> RenderAsync(
            AssessmentReportSnapshot snapshot,
            CaseReportArtifactKind kind,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new RenderedReportArtifact(
                $"{kind}.pdf", pdfBytes, 1,
                Convert.ToHexStringLower(SHA256.HashData(pdfBytes)),
                AssessmentReportContract.TemplateVersion, EngineVersion));
    }

    private sealed class ThrowingDocumentContentStore : IDocumentContentStore
    {
        public Task StoreAsync(
            Guid caseId, string caseReference, Guid versionId, ReadOnlyMemory<byte> content,
            string expectedSha256, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Assessment GET must not write document content.");

        public Task<Stream> OpenReadAsync(
            Guid caseId, string caseReference, Guid versionId, string expectedSha256,
            long expectedLength, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Assessment GET must not read document content.");

        public Task DeleteAsync(
            Guid caseId, string caseReference, Guid versionId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Assessment GET must not delete document content.");
    }
}
