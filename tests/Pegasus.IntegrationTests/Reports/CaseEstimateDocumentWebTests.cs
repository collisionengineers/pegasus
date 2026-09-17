using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Core.Reports;
using Pegasus.IntegrationTests.Reports;
using static Pegasus.IntegrationTests.CaseWebTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>The estimate document handler on the Case page: authorised inline PDF, preview record, refusals.</summary>
[Trait("Category", "SqlServer")]
public sealed class CaseEstimateDocumentWebTests
{
    [Fact]
    public async Task ReportsHandlerReturnsTheEstimatePdfInlineAndRecordsTheViewForAuthorisedStaff()
    {
        var caseId = Guid.NewGuid();
        var estimateId = Guid.NewGuid();
        var renderer = new StubEstimateDocumentUseCase(new(
            RenderCaseEstimateDocumentOutcome.Rendered,
            new("QDOS26001_ESTIMATE_1_v3_estimate.pdf", "%PDF-estimate"u8.ToArray(), 1,
                new string('a', 64), EstimateDocumentContract.TemplateVersion, "test/1"),
            [],
            3));
        var presentations = new RecordingEstimateDocumentPresentations();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            Substitute<IRenderCaseEstimateDocument>(services, renderer);
            Substitute<IEstimateDocumentPresentationStore>(services, presentations);
        }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });
        client.DefaultRequestHeaders.Add("X-Test-Roles", "User");

        var response = await client.GetAsync(
            $"/Cases/{caseId:D}?handler=EstimateDocument&estimateId={estimateId:D}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("inline", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("%PDF-estimate"u8.ToArray(), await response.Content.ReadAsByteArrayAsync());
        var recorded = Assert.Single(presentations.Requests);
        Assert.Equal(caseId, recorded.CaseId);
        Assert.Equal(estimateId, recorded.EstimateId);
        Assert.Equal(3, recorded.EstimateVersion);
    }

    [Fact]
    public async Task ReportsHandlerDoesNotDiscloseAnEstimateOutsideTheAuthorisedCase()
    {
        var caseId = Guid.NewGuid();
        var estimateId = Guid.NewGuid();
        var workspace = new ControlledEstimateWorkspace(AssessmentWorkspaceTestData.Create(
            AssessmentReportDraftWebTests.FullAssessmentProjection(caseId)));
        var estimates = new ControlledEstimateStore(
            AssessmentReportDraftWebTests.CurrentEstimate() with
            {
                SpecificationId = estimateId,
                CaseId = Guid.NewGuid(),
            });
        var renderer = new RecordingEstimateDocumentRenderer();
        var useCase = new RenderCaseEstimateDocument(
            workspace, estimates, renderer, TimeProvider.System);
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            Substitute<IRenderCaseEstimateDocument>(services, useCase);
            Substitute<IEstimateDocumentPresentationStore>(services, new RecordingEstimateDocumentPresentations());
        }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });
        client.DefaultRequestHeaders.Add("X-Test-Roles", "User");

        var response = await client.GetAsync(
            $"/Cases/{caseId:D}?handler=EstimateDocument&estimateId={estimateId:D}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(1, workspace.Calls);
        Assert.Equal(1, estimates.Calls);
        Assert.Equal(0, renderer.Calls);
    }

    [Fact]
    public async Task ReportsHandlerReturnsATypedEnhancedPreviewRefusalForANonRenderableEstimate()
    {
        var renderer = new StubEstimateDocumentUseCase(new(
            RenderCaseEstimateDocumentOutcome.NotRenderable, null, ["The estimate has no lines."]));
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            Substitute<IRenderCaseEstimateDocument>(services, renderer);
            Substitute<IEstimateDocumentPresentationStore>(services, new RecordingEstimateDocumentPresentations());
        }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Engineer");
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/Cases/{Guid.NewGuid():D}?handler=EstimateDocument&estimateId={Guid.NewGuid():D}");
        request.Headers.Add("X-Pegasus-Document-Preview", "1");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("The estimate has no lines.", problem.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task ReportsHandlerRefusesRolelessStaff()
    {
        var caseId = Guid.NewGuid();
        var estimateId = Guid.NewGuid();
        var workspace = new ControlledEstimateWorkspace(AssessmentWorkspaceTestData.Create(
            AssessmentReportDraftWebTests.FullAssessmentProjection(caseId)));
        var estimates = new ControlledEstimateStore(
            AssessmentReportDraftWebTests.CurrentEstimate() with
            {
                SpecificationId = estimateId,
                CaseId = caseId,
            });
        var renderer = new RecordingEstimateDocumentRenderer();
        var useCase = new RenderCaseEstimateDocument(
            workspace, estimates, renderer, TimeProvider.System);
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            Substitute<IRenderCaseEstimateDocument>(services, useCase);
            Substitute<IEstimateDocumentPresentationStore>(services, new RecordingEstimateDocumentPresentations());
        }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/Cases/{caseId:D}?handler=EstimateDocument&estimateId={estimateId:D}");
        request.Headers.Add("X-Test-Roleless", "1");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, workspace.Calls);
        Assert.Equal(0, estimates.Calls);
        Assert.Equal(0, renderer.Calls);
    }

    private sealed class StubEstimateDocumentUseCase(RenderCaseEstimateDocumentResult result)
        : IRenderCaseEstimateDocument
    {
        public int Calls { get; private set; }

        public Task<RenderCaseEstimateDocumentResult> ExecuteAsync(
            Guid caseId,
            Guid estimateId,
            ActionActor actor,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(result);
        }
    }

    private sealed class RecordingEstimateDocumentPresentations : IEstimateDocumentPresentationStore
    {
        public List<RecordEstimateDocumentPreviewRequest> Requests { get; } = [];

        public Task RecordPreviewedAsync(
            RecordEstimateDocumentPreviewRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.CompletedTask;
        }
    }

    private sealed class ControlledEstimateWorkspace(AssessmentWorkspace workspace)
        : IGetAssessmentWorkspace
    {
        public int Calls { get; private set; }

        public Task<AssessmentWorkspace?> ExecuteAsync(
            GetAssessmentWorkspaceQuery query,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult<AssessmentWorkspace?>(
                query.CaseId == workspace.Header.CaseId ? workspace : null);
        }
    }

    private sealed class ControlledEstimateStore(RepairSpecificationVersion version)
        : IRepairSpecificationStore
    {
        public int Calls { get; private set; }

        public Task<RepairSpecificationVersion?> GetVersionAsync(
            Guid caseId,
            Guid specificationId,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<RepairSpecificationVersion?>(
                specificationId == version.SpecificationId ? version : null);
        }

        public Task RequireImportAuthorityAsync(
            ImportRawEstimateRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RepairSpecificationVersion> SaveImportedEstimateAsync(
            SaveEstimateRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RepairSpecificationVersion> StartDraftAsync(
            StartRepairSpecificationDraftRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RepairSpecificationVersion> AcceptAsync(
            AcceptRepairSpecificationRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RepairSpecificationVersion?> GetCurrentAcceptedAsync(
            Guid caseId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RepairSpecificationVersion?> GetCurrentDraftAsync(
            Guid caseId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RepairSpecificationVersion> SaveEstimateAsync(
            SaveEstimateRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RepairSpecificationVersion> DuplicateEstimateAsync(
            DuplicateEstimateRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RepairSpecificationVersion> DiscardEstimateAsync(
            DiscardEstimateRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RepairSpecificationVersion> SetCurrentEstimateAsync(
            SetCurrentEstimateRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<RepairSpecificationVersion>> ListEstimatesAsync(
            Guid caseId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<CaseEstimatePageItem>> ListByCursorAsync(
            Guid caseId,
            int? afterVersion,
            Guid? afterId,
            int fetchCount,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingEstimateDocumentRenderer : IEstimateDocumentRenderer
    {
        public string EngineVersion => "test/1";
        public int Calls { get; private set; }

        public Task<RenderedReportArtifact> RenderAsync(
            EstimateDocumentSnapshot snapshot,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            throw new InvalidOperationException("The access boundary did not refuse the render.");
        }
    }
}
