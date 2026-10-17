using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;

using static Pegasus.IntegrationTests.CaseWebTestSupport;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class CaseReportApprovalWebTests
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
        using var baseFactory = IntakeWebApplicationFactory.WithoutDatabase();
        var store = new RecordingCaseDetailsStore();
        var artifacts = new RecordingGeneratedArtifactStore();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                Substitute<IGetCaseEditBasis>(services, store);
                Substitute<IGetCasePageFrame>(services, store);
                Substitute<IGetAssessmentAccess>(services, new FakeGetAssessmentAccess(canOpen: true));
                Substitute<IGeneratedCaseArtifactStore>(services, artifacts);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        client.DefaultRequestHeaders.Add("X-Test-Roles", "User");
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
        using var baseFactory = IntakeWebApplicationFactory.WithoutDatabase();
        var store = new RecordingCaseDetailsStore();
        var artifacts = new RecordingGeneratedArtifactStore();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                Substitute<IGetCaseEditBasis>(services, store);
                Substitute<IGetCasePageFrame>(services, store);
                Substitute<IGetAssessmentAccess>(services, new FakeGetAssessmentAccess(canOpen: false));
                Substitute<IGeneratedCaseArtifactStore>(services, artifacts);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        client.DefaultRequestHeaders.Add("X-Test-Roles", "User");

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

    [Theory]
    [InlineData(CaseLifecycleState.ReportPreparation, true, true)]
    [InlineData(CaseLifecycleState.ReportPreparation, false, false)]
    [InlineData(CaseLifecycleState.Review, true, false)]
    public async Task ReportSentRendersOnlyWithDetectedEvidenceWhileWithEngineer(
        CaseLifecycleState state,
        bool hasEvidence,
        bool offersConfirmation)
    {
        var evidence = new RetainedApprovedMailboxReportSentEvidence(
            Guid.NewGuid(),
            "reports@collisionengineers.example",
            "sent-folder-handle",
            "immutable-item-handle",
            "internet-message-handle",
            "conversation-handle",
            "reply-chain-handle",
            "source-occurrence-handle",
            new string('b', 64),
            new string('c', 64),
            new DateTimeOffset(2031, 5, 6, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2031, 5, 6, 9, 5, 0, TimeSpan.Zero),
            ActionActor.SystemWorker("sent-mail-worker"));
        var store = new RecordingCaseDetailsStore
        {
            State = state,
            AvailableReportSentEvidence = hasEvidence ? [evidence] : []
        };
        using var workspace = await EnterEditModeAsync(store, _ => { });

        var html = await workspace.GetWorkspaceAsync();

        Assert.Equal(
            offersConfirmation,
            RecordBar(html).Contains("Mark report sent", StringComparison.Ordinal));
        Assert.Equal(
            offersConfirmation,
            html.Contains("handler=LinkReportEvidence", StringComparison.Ordinal));
        if (offersConfirmation)
        {
            var visible = VisibleText(html);
            Assert.Contains("reports@collisionengineers.example", visible, StringComparison.Ordinal);
            Assert.DoesNotContain("immutable-item-handle", visible, StringComparison.Ordinal);
            Assert.DoesNotContain("internet-message-handle", visible, StringComparison.Ordinal);
            Assert.DoesNotContain(new string('b', 64), visible, StringComparison.Ordinal);
            Assert.DoesNotContain(new string('c', 64), visible, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// D29/D30: the record is one scrolling page of ten sections in a fixed
    /// order. Every section has its stable host and its jump link, in that
    /// order, on every response.
    /// </summary>
}
