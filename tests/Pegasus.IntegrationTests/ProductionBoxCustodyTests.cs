using System.Net;
using System.Security.Cryptography;
using System.Text;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Custody;

namespace Pegasus.IntegrationTests;

public sealed class ProductionBoxCustodyTests
{
    private const string BoxConfigJson = """
        {"boxAppSettings":{"clientID":"client-id","appAuth":{"publicKeyID":"key-id","privateKey":"private-key","passphrase":"passphrase"}},"enterpriseID":"enterprise-id"}
        """;

    [Fact]
    public void ConfigurationRejectsAnyRootOtherThanTheApprovedFolder()
    {
        var error = Assert.Throws<InvalidOperationException>(() => BoxCustodyOptions.Create(
            "https://api.box.com/2.0/",
            "https://upload.box.com/api/2.0/",
            "0",
            BoxConfigJson,
            "client-secret"));

        Assert.Contains("405543781910", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfigurationRejectsMissingOrMalformedJwtMaterial()
    {
        var missingConfiguration = Assert.Throws<InvalidOperationException>(() => BoxCustodyOptions.Create(
            "https://api.box.com/2.0/",
            "https://upload.box.com/api/2.0/",
            "405543781910",
            null,
            "client-secret"));
        var missingSecret = Assert.Throws<InvalidOperationException>(() => BoxCustodyOptions.Create(
            "https://api.box.com/2.0/",
            "https://upload.box.com/api/2.0/",
            "405543781910",
            BoxConfigJson,
            null));
        var malformedConfiguration = Assert.Throws<InvalidOperationException>(() => BoxCustodyOptions.Create(
            "https://api.box.com/2.0/",
            "https://upload.box.com/api/2.0/",
            "405543781910",
            "{}",
            "client-secret"));

        Assert.Contains("ConfigJson", missingConfiguration.Message, StringComparison.Ordinal);
        Assert.Contains("ClientSecret", missingSecret.Message, StringComparison.Ordinal);
        Assert.Contains("valid Box JWT", malformedConfiguration.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExistingCaseRootIsReturnedOnlyAfterAncestryReachesTheApprovedRoot()
    {
        var caseId = Guid.Parse("10213243-5465-7687-98a9-bacbdcedfe0f");
        var expectedName = "QDOS31001";
        var handler = new DelegateHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/2.0/folders/405543781910/items" => Json($$"""{"entries":[{"id":"case-folder","name":"{{expectedName}}","type":"folder","etag":"1"}]}"""),
            "/2.0/folders/case-folder" => Json("""{"id":"case-folder","parent":{"id":"405543781910"},"trashed_at":null}"""),
            _ => throw new InvalidOperationException(request.RequestUri.AbsoluteUri)
        });
        var custody = Create(handler);

        var root = await custody.GetExistingCaseRootAsync(caseId, "QDOS31001", CancellationToken.None);

        Assert.Equal("case-folder", root.RemoteId);
        Assert.All(handler.Methods, method => Assert.Equal(HttpMethod.Get, method));
        Assert.Equal(["Bearer test-token-1", "Bearer test-token-2"], handler.AuthorizationHeaders);
    }

    [Fact]
    public async Task ExistingCaseRootOutsideTheApprovedAncestryIsDenied()
    {
        var caseId = Guid.Parse("10213243-5465-7687-98a9-bacbdcedfe0f");
        var expectedName = "QDOS31001";
        var handler = new DelegateHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/2.0/folders/405543781910/items" => Json($$"""{"entries":[{"id":"case-folder","name":"{{expectedName}}","type":"folder","etag":"1"}]}"""),
            "/2.0/folders/case-folder" => Json("""{"id":"case-folder","parent":{"id":"outside"},"trashed_at":null}"""),
            "/2.0/folders/outside" => Json("""{"id":"outside","parent":null,"trashed_at":null}"""),
            _ => throw new InvalidOperationException(request.RequestUri.AbsoluteUri)
        });
        var custody = Create(handler);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            custody.GetExistingCaseRootAsync(caseId, "QDOS31001", CancellationToken.None));
    }

    [Fact]
    public async Task RetainingAcceptedSourceCreatesOneVersionAndUsesNoProhibitedOperation()
    {
        var caseId = Guid.Parse("10213243-5465-7687-98a9-bacbdcedfe0f");
        var receiptId = Guid.Parse("20314253-6475-8697-a8b9-cadbecfd0e1f");
        var bytes = Encoding.UTF8.GetBytes("accepted source");
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        var expectedCaseName = "QDOS31001";
        var expectedFileName = $"{hash.ToLowerInvariant()}-instruction.eml";
        var handler = new DelegateHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/2.0/folders/405543781910/items")
            {
                return Json($$"""{"entries":[{"id":"case-folder","name":"{{expectedCaseName}}","type":"folder","etag":"1"}]}""");
            }
            if (path == "/2.0/folders/case-folder/items")
            {
                return Json("""{"entries":[{"id":"documents","name":"documents","type":"folder","etag":"1"}]}""");
            }
            if (path == "/2.0/folders/documents/items")
            {
                return Json($$"""{"entries":[{"id":"receipt","name":"{{receiptId:N}}","type":"folder","etag":"1"}]}""");
            }
            if (path == "/2.0/folders/receipt/items")
            {
                return Json("""{"entries":[]}""");
            }
            if (path == "/api/2.0/files/content")
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                return Json("""{"entries":[{"id":"file-version","name":"retained","type":"file","etag":"version-1"}]}""");
            }
            return path switch
            {
                "/2.0/folders/case-folder" => Parent("405543781910"),
                "/2.0/folders/documents" => Parent("case-folder"),
                "/2.0/folders/receipt" => Parent("documents"),
                "/2.0/files/file-version" => Parent("receipt"),
                _ => throw new InvalidOperationException(request.RequestUri.AbsoluteUri)
            };
        });
        var custody = Create(handler, new MemoryArtifactStore(bytes));
        var root = new Pegasus.Core.Custody.CaseCustodyRoot(caseId, "case-folder", "QDOS31001");
        var source = new Pegasus.Core.Custody.IntakeSourceCustodyReference(
            receiptId,
            "instruction.eml",
            "message/rfc822",
            hash,
            "source-key");

        var retained = await custody.RetainAcceptedIntakeSourceAsync(
            root, source, "retain-operation", CancellationToken.None);

        Assert.Equal("file-version", retained.RemoteId);
        Assert.Equal("version-1", retained.ETag);
        Assert.Contains(handler.Uris, uri => uri.AbsolutePath == "/api/2.0/files/content");
        Assert.All(handler.Methods, method => Assert.Contains(method, new[] { HttpMethod.Get, HttpMethod.Post }));
        Assert.DoesNotContain(handler.Uris, uri =>
            uri.AbsolutePath.Contains("delete", StringComparison.OrdinalIgnoreCase)
            || uri.AbsolutePath.Contains("move", StringComparison.OrdinalIgnoreCase)
            || uri.AbsolutePath.Contains("copy", StringComparison.OrdinalIgnoreCase)
            || uri.AbsolutePath.Contains("share", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedFileName, handler.RequestBodies.Single(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task BoxFailureRemainsVisibleToTheCallerWithoutBackgroundRetry()
    {
        var calls = 0;
        var handler = new DelegateHandler(_ =>
        {
            calls++;
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("unavailable")
            };
        });
        var custody = Create(handler);

        await Assert.ThrowsAsync<HttpRequestException>(() => custody.GetExistingCaseRootAsync(
            Guid.Parse("10213243-5465-7687-98a9-bacbdcedfe0f"),
            "QDOS31001",
            CancellationToken.None));

        Assert.Equal(1, calls);
    }

    private static BoxCaseCustody Create(DelegateHandler handler, IIntakeArtifactStore? artifactStore = null) => new(
        artifactStore ?? new EmptyArtifactStore(),
        CreateClient(handler));

    private static BoxContentClient CreateClient(DelegateHandler handler) => new(
        BoxCustodyOptions.Create(
            "https://api.box.com/2.0/",
            "https://upload.box.com/api/2.0/",
            "405543781910",
            BoxConfigJson,
            "client-secret"),
        new HttpClient(handler),
        new RecordingAuthorizationHeaderProvider());

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Parent(string parentId) =>
        Json($$"""{"id":"item","parent":{"id":"{{parentId}}"},"trashed_at":null}""");

    private sealed class EmptyArtifactStore : IIntakeArtifactStore
    {
        public Task<string> StoreAsync(string contentHash, ReadOnlyMemory<byte> content, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ReadOnlyMemory<byte>?> ReadAsync(string storageKey, CancellationToken cancellationToken) => Task.FromResult<ReadOnlyMemory<byte>?>(null);
    }

    private sealed class MemoryArtifactStore(ReadOnlyMemory<byte> content) : IIntakeArtifactStore
    {
        public Task<string> StoreAsync(string contentHash, ReadOnlyMemory<byte> value, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ReadOnlyMemory<byte>?> ReadAsync(string storageKey, CancellationToken cancellationToken) =>
            Task.FromResult<ReadOnlyMemory<byte>?>(content);
    }

    private sealed class DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        public List<HttpMethod> Methods { get; } = [];
        public List<Uri> Uris { get; } = [];
        public List<string> RequestBodies { get; } = [];
        public List<string> AuthorizationHeaders { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Methods.Add(request.Method);
            Uris.Add(request.RequestUri!);
            AuthorizationHeaders.Add(request.Headers.Authorization?.ToString() ?? string.Empty);
            if (request.Content is not null)
            {
                RequestBodies.Add(request.Content.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult());
            }
            return Task.FromResult(handler(request));
        }
    }

    private sealed class RecordingAuthorizationHeaderProvider : IBoxAuthorizationHeaderProvider
    {
        private int calls;

        public Task<string> GetAuthorizationHeaderAsync(CancellationToken cancellationToken) =>
            Task.FromResult($"Bearer test-token-{Interlocked.Increment(ref calls)}");
    }
}
