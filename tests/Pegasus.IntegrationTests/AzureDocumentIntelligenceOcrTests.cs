using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Intake;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The Azure Document Intelligence adapter against a structural fake of the
/// REST contract.
///
/// No genuine OCR output for a genuine document exists on this machine, so
/// nothing here claims the provider reads a real instruction correctly — that
/// remains INCONCLUSIVE until an operator activates the resource and a genuine
/// stored response can be hashed and pinned. What IS proved is the contract the
/// adapter holds the provider to: the request shape, the pinned API version, the
/// page restriction, the operation-location polling, the response validation,
/// the response hash and the coordinate mapping. The fake returns invented
/// non-domain text, and no invented text is treated as evidence anywhere.
/// </summary>
public sealed class AzureDocumentIntelligenceOcrTests
{
    private static readonly Uri Endpoint = new("https://fixture-di.cognitiveservices.azure.com/");
    private static readonly byte[] SourceBytes = Encoding.UTF8.GetBytes("a synthesized source document");
    private static readonly string SourceHash = Convert.ToHexStringLower(SHA256.HashData(SourceBytes));

    [Fact]
    public async Task OnlyTheQualifiedPagesAreSubmittedAtThePinnedApiVersion()
    {
        var transport = new FakeTransport();
        transport.OnAnalyze = _ => Accepted("op-7");
        transport.OnResult = () => Succeeded([2, 5]);

        var result = await AnalyzeAsync(transport, [5, 2]);

        Assert.Equal(IntakeOcrState.Completed, result.State);
        var submission = Assert.Single(transport.Submissions);
        Assert.Equal(
            "/documentintelligence/documentModels/prebuilt-layout:analyze",
            submission.AbsolutePath);
        var query = submission.Query;
        Assert.Contains("api-version=2024-11-30", query, StringComparison.Ordinal);
        // Ascending and exactly the qualified pages: the caller asked out of
        // order, and a stable order is what makes a replay identical.
        Assert.Contains("pages=2,5", query, StringComparison.Ordinal);
        Assert.Equal("application/octet-stream", transport.SubmittedMediaType);
        Assert.Equal(SourceBytes, transport.SubmittedBody);
    }

    [Fact]
    public async Task TheOperationIsPolledAtTheLocationTheProviderNamedUntilItAnswers()
    {
        var transport = new FakeTransport();
        transport.OnAnalyze = _ => Accepted("op-7");
        var reads = 0;
        transport.OnResult = () => ++reads < 3 ? Running() : Succeeded([2]);

        var result = await AnalyzeAsync(transport, [2]);

        Assert.Equal(IntakeOcrState.Completed, result.State);
        Assert.Equal("op-7", result.ProviderOperationId);
        Assert.Equal(3, transport.Polls.Count);
        Assert.All(transport.Polls, poll => Assert.Equal(
            "/documentintelligence/documentModels/prebuilt-layout/analyzeResults/op-7",
            poll.AbsolutePath));
        // One submission however many times the operation was read.
        Assert.Single(transport.Submissions);
    }

    [Fact]
    public async Task TheResponseIsHashedAsReceivedAndItsCoordinatesAreMappedInTheProvidersUnit()
    {
        var payload = Succeeded([2]);
        var transport = new FakeTransport();
        transport.OnAnalyze = _ => Accepted("op-7");
        transport.OnResult = () => payload;

        var result = await AnalyzeAsync(transport, [2]);

        Assert.Equal(
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload.Body))),
            result.ResponseSha256);
        Assert.Equal(IntakeOcrProviderIdentity.Provider, result.Provider);
        Assert.Equal(IntakeOcrProviderIdentity.ModelId, result.ModelId);
        Assert.Equal(IntakeOcrProviderIdentity.ApiVersion, result.ApiVersion);

        var page = Assert.Single(result.PageResults);
        Assert.Equal(2, page.Number);
        var line = Assert.Single(page.Lines);
        Assert.Equal("SYNTHETIC LINE", line.Text);
        var bounds = line.Bounds!;
        Assert.Equal(1.0, bounds.Left);
        Assert.Equal(2.0, bounds.Top);
        Assert.Equal(5.0, bounds.Right);
        Assert.Equal(6.0, bounds.Bottom);
        Assert.Equal("inch", bounds.Unit);

        var table = Assert.Single(page.Tables);
        Assert.Equal(2, table.RowCount);
        Assert.Equal(2, table.ColumnCount);
        // The provider indexes cells from zero; the intake locator counts from
        // one, as a person does.
        Assert.Equal([(1, 1), (1, 2)], table.Cells.Select(cell => (cell.Row, cell.Column)));
    }

    [Fact]
    public async Task ConfidenceIsCarriedThroughAndIsNeverWhatAcceptsAValue()
    {
        var transport = new FakeTransport();
        transport.OnAnalyze = _ => Accepted("op-7");
        transport.OnResult = () => Succeeded([2]);

        var result = await AnalyzeAsync(transport, [2]);

        var word = Assert.Single(Assert.Single(result.PageResults).Lines[0].Words);
        Assert.Equal(0.11, word.Confidence);
        // The reading is complete and acceptable although the provider is barely
        // confident: acceptance is about attribution — the pages, the version and
        // the hash — and a person reviews the text itself.
        Assert.Equal(IntakeOcrState.Completed, result.State);
        Assert.Null(IntakeOcrPolicy.Validate(Request([2]), result));
    }

    [Fact]
    public async Task AnOperationLocationOutsideTheConfiguredEndpointIsRefusedRatherThanFollowed()
    {
        var transport = new FakeTransport();
        transport.OnAnalyze = _ => new FakeResponse(
            HttpStatusCode.Accepted,
            "{}",
            OperationLocation:
            "https://someone-else.example/documentintelligence/documentModels/prebuilt-layout/analyzeResults/op-7");

        var result = await AnalyzeAsync(transport, [2]);

        Assert.Equal(IntakeOcrState.Unknown, result.State);
        Assert.Equal("ocr_operation_location_invalid", result.Failure!.Code);
        Assert.False(result.Failure.Retryable);
        Assert.Empty(transport.Polls);
    }

    [Fact]
    public async Task AThrottledSubmissionIsRetryableAndHonoursTheProvidersRetryAfter()
    {
        var transport = new FakeTransport();
        transport.OnAnalyze = _ => new FakeResponse(
            HttpStatusCode.TooManyRequests,
            "{}",
            RetryAfterSeconds: 42);

        var result = await AnalyzeAsync(transport, [2]);

        Assert.Equal(IntakeOcrState.Failed, result.State);
        Assert.Equal("ocr_provider_unavailable", result.Failure!.Code);
        Assert.True(result.Failure.Retryable);
        Assert.Equal(TimeSpan.FromSeconds(42), result.Failure.RetryAfter);
    }

    [Fact]
    public async Task AnOperationTheProviderReportsFailedIsNotRetried()
    {
        var transport = new FakeTransport();
        transport.OnAnalyze = _ => Accepted("op-7");
        transport.OnResult = () => new FakeResponse(HttpStatusCode.OK, """{"status":"failed"}""");

        var result = await AnalyzeAsync(transport, [2]);

        Assert.Equal(IntakeOcrState.Failed, result.State);
        Assert.Equal("ocr_operation_failed", result.Failure!.Code);
        Assert.False(result.Failure.Retryable);
    }

    [Fact]
    public async Task AResultFromAnotherModelIsRefused()
    {
        var transport = new FakeTransport();
        transport.OnAnalyze = _ => Accepted("op-7");
        transport.OnResult = () => new FakeResponse(
            HttpStatusCode.OK,
            """{"status":"succeeded","analyzeResult":{"apiVersion":"2024-11-30","modelId":"prebuilt-read","pages":[]}}""");

        var result = await AnalyzeAsync(transport, [2]);

        Assert.Equal(IntakeOcrState.Failed, result.State);
        Assert.Equal("ocr_model_unexpected", result.Failure!.Code);
    }

    [Fact]
    public async Task AResponseThatIsNotReadableJsonIsRefusedRatherThanPartlyBelieved()
    {
        var transport = new FakeTransport();
        transport.OnAnalyze = _ => Accepted("op-7");
        transport.OnResult = () => new FakeResponse(HttpStatusCode.OK, "not json at all");

        var result = await AnalyzeAsync(transport, [2]);

        Assert.Equal(IntakeOcrState.Failed, result.State);
        Assert.Equal("ocr_response_malformed", result.Failure!.Code);
        Assert.NotNull(result.ResponseSha256);
    }

    [Fact]
    public async Task ASourceWhoseBytesDoNotMatchTheOperationIsNeverSent()
    {
        var transport = new FakeTransport();
        var provider = new AzureDocumentIntelligenceOcr(
            AzureDocumentIntelligenceOptions.Create(Endpoint) with { PollInterval = TimeSpan.Zero },
            new HttpClient(transport) { BaseAddress = Endpoint },
            new FakeCredential(),
            TimeProvider.System);

        var result = await provider.AnalyzeAsync(
            Request([2]) with { SourceSha256 = new string('a', 64) },
            new MemoryStream(SourceBytes, writable: false),
            CancellationToken.None);

        Assert.Equal(IntakeOcrState.Failed, result.State);
        Assert.Equal("ocr_source_hash_mismatch", result.Failure!.Code);
        Assert.Empty(transport.Submissions);
    }

    [Fact]
    public async Task ReconciliationAsksAboutTheRecordedOperationAndSendsNothing()
    {
        var transport = new FakeTransport();
        transport.OnResult = () => Succeeded([2]);
        var provider = Provider(transport);

        var result = await provider.ReconcileAsync(Request([2]), "op-7", CancellationToken.None);

        Assert.Equal(IntakeOcrState.Completed, result.State);
        Assert.Equal("op-7", result.ProviderOperationId);
        Assert.Empty(transport.Submissions);
        Assert.Equal(
            "/documentintelligence/documentModels/prebuilt-layout/analyzeResults/op-7",
            Assert.Single(transport.Polls).AbsolutePath);
    }

    [Fact]
    public async Task EveryRequestCarriesTheHostsOwnBearerTokenAndNoKey()
    {
        var transport = new FakeTransport();
        transport.OnAnalyze = _ => Accepted("op-7");
        transport.OnResult = () => Succeeded([2]);

        await AnalyzeAsync(transport, [2]);

        Assert.Equal(["Bearer fixture-token", "Bearer fixture-token"], transport.Authorizations);
        Assert.All(transport.HeaderNames, names =>
            Assert.DoesNotContain("Ocp-Apim-Subscription-Key", names));
    }

    [Fact]
    public void AnEndpointThatIsNotAbsoluteHttpsIsRefusedAtConfigurationTime()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AzureDocumentIntelligenceOptions.Create(new Uri("http://insecure.example/")));
        Assert.Equal(
            "https://cognitiveservices.azure.com/.default",
            AzureDocumentIntelligenceOptions.CredentialScope);
    }

    private static Task<IntakeOcrResult> AnalyzeAsync(FakeTransport transport, int[] pages) =>
        Provider(transport).AnalyzeAsync(
            Request(pages),
            new MemoryStream(SourceBytes, writable: false),
            CancellationToken.None);

    private static AzureDocumentIntelligenceOcr Provider(FakeTransport transport) =>
        new(
            AzureDocumentIntelligenceOptions.Create(Endpoint) with { PollInterval = TimeSpan.Zero },
            new HttpClient(transport) { BaseAddress = Endpoint },
            new FakeCredential(),
            TimeProvider.System);

    private static IntakeOcrRequest Request(int[] pages) =>
        new(
            Guid.NewGuid(),
            null,
            Guid.NewGuid(),
            SourceHash,
            SourceBytes.Length,
            pages,
            "ocr-fixture-1");

    private static FakeResponse Accepted(string operationId) => new(
        HttpStatusCode.Accepted,
        "{}",
        OperationLocation:
        $"https://fixture-di.cognitiveservices.azure.com/documentintelligence/documentModels/prebuilt-layout/analyzeResults/{operationId}?api-version=2024-11-30");

    private static FakeResponse Running() => new(HttpStatusCode.OK, """{"status":"running"}""");

    /// <summary>
    /// A structurally faithful <c>succeeded</c> payload: the properties the
    /// adapter reads, in the shapes the 2024-11-30 contract states them, with
    /// invented non-domain content.
    /// </summary>
    private static FakeResponse Succeeded(int[] pages) => new(
        HttpStatusCode.OK,
        JsonSerializer.Serialize(new
        {
            status = "succeeded",
            analyzeResult = new
            {
                apiVersion = IntakeOcrProviderIdentity.ApiVersion,
                modelId = IntakeOcrProviderIdentity.ModelId,
                pages = pages.Select(page => new
                {
                    pageNumber = page,
                    unit = "inch",
                    width = 8.5,
                    height = 11.0,
                    words = new[]
                    {
                        new
                        {
                            content = "SYNTHETIC",
                            confidence = 0.11,
                            polygon = SyntheticPolygon
                        }
                    },
                    lines = new[]
                    {
                        new
                        {
                            content = "SYNTHETIC LINE",
                            polygon = SyntheticPolygon
                        }
                    }
                }),
                tables = pages.Select(page => new
                {
                    rowCount = 2,
                    columnCount = 2,
                    boundingRegions = new[] { new { pageNumber = page } },
                    cells = new[]
                    {
                        new { rowIndex = 0, columnIndex = 0, content = "LABEL" },
                        new { rowIndex = 0, columnIndex = 1, content = "VALUE" }
                    }
                })
            }
        }));

    /// <summary>
    /// One rectangle stated the way the provider states a shape: four points,
    /// flat, in the page's own unit.
    /// </summary>
    private static readonly double[] SyntheticPolygon = [1.0, 2.0, 5.0, 2.0, 5.0, 6.0, 1.0, 6.0];

    private sealed record FakeResponse(
        HttpStatusCode StatusCode,
        string Body,
        string? OperationLocation = null,
        int? RetryAfterSeconds = null);

    private sealed class FakeTransport : HttpMessageHandler
    {
        public Func<HttpRequestMessage, FakeResponse>? OnAnalyze { get; set; }

        public Func<FakeResponse>? OnResult { get; set; }

        public List<Uri> Submissions { get; } = [];

        public List<Uri> Polls { get; } = [];

        public List<string> Authorizations { get; } = [];

        public List<string[]> HeaderNames { get; } = [];

        public string? SubmittedMediaType { get; private set; }

        public byte[]? SubmittedBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Authorizations.Add(request.Headers.Authorization?.ToString() ?? string.Empty);
            HeaderNames.Add([.. request.Headers.Select(header => header.Key)]);
            FakeResponse fake;
            if (request.Method == HttpMethod.Post)
            {
                Submissions.Add(request.RequestUri!);
                SubmittedMediaType = request.Content?.Headers.ContentType?.MediaType;
                SubmittedBody = request.Content is null
                    ? null
                    : await request.Content.ReadAsByteArrayAsync(cancellationToken);
                fake = OnAnalyze?.Invoke(request)
                    ?? throw new InvalidOperationException("No submission was expected.");
            }
            else
            {
                Polls.Add(request.RequestUri!);
                fake = OnResult?.Invoke()
                    ?? throw new InvalidOperationException("No operation read was expected.");
            }

            var response = new HttpResponseMessage(fake.StatusCode)
            {
                Content = new StringContent(fake.Body, Encoding.UTF8, "application/json")
            };
            if (fake.OperationLocation is { } location)
            {
                response.Headers.TryAddWithoutValidation("Operation-Location", location);
            }

            if (fake.RetryAfterSeconds is { } seconds)
            {
                response.Headers.RetryAfter = new(TimeSpan.FromSeconds(seconds));
            }

            return response;
        }
    }

    private sealed class FakeCredential : TokenCredential
    {
        public override AccessToken GetToken(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken) =>
            new("fixture-token", DateTimeOffset.UtcNow.AddHours(1));

        public override ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken) =>
            new(GetToken(requestContext, cancellationToken));
    }
}
