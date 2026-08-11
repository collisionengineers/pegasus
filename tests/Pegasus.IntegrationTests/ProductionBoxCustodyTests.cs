using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
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
        var binding = BoxCaseCustody.CaseBinding(caseId, expectedName);
        var handler = new DelegateHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/2.0/folders/405543781910/items" => Json($$"""{"entries":[{"id":"case-folder","name":"{{expectedName}}","type":"folder","etag":"1"}]}"""),
            "/2.0/folders/case-folder" => Json("""{"id":"case-folder","parent":{"id":"405543781910"},"trashed_at":null}"""),
            "/2.0/folders/case-folder/items" => Json("""{"entries":[{"id":"case-binding","name":"pegasus-case-binding.json","type":"file","etag":"1"}]}"""),
            "/2.0/files/case-binding" => Json("""{"id":"case-binding","parent":{"id":"case-folder"},"trashed_at":null}"""),
            "/2.0/files/case-binding/content" => Bytes(binding),
            _ => throw new InvalidOperationException(request.RequestUri.AbsoluteUri)
        });
        var custody = Create(handler);

        var root = await custody.GetExistingCaseRootAsync(caseId, "QDOS31001", CancellationToken.None);

        Assert.Equal("case-folder", root.RemoteId);
        Assert.All(handler.Methods, method => Assert.Equal(HttpMethod.Get, method));
        Assert.All(handler.AuthorizationHeaders, header => Assert.StartsWith("Bearer test-token-", header));
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
        var expectedFileName = "001 instruction.eml";
        var binding = BoxCaseCustody.CaseBinding(caseId, expectedCaseName);
        var handler = new DelegateHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/2.0/folders/405543781910/items")
            {
                return Json($$"""{"entries":[{"id":"case-folder","name":"{{expectedCaseName}}","type":"folder","etag":"1"}]}""");
            }
            if (path == "/2.0/folders/case-folder/items")
            {
                return Json("""{"entries":[{"id":"case-binding","name":"pegasus-case-binding.json","type":"file","etag":"1"},{"id":"evidence","name":"Evidence","type":"folder","etag":"1"}]}""");
            }
            if (path == "/2.0/folders/evidence/items")
            {
                return Json("""{"entries":[{"id":"instruction","name":"Original instruction","type":"folder","etag":"1"}]}""");
            }
            if (path == "/2.0/folders/instruction/items")
            {
                return Json("""{"entries":[]}""");
            }
            if (path == "/2.0/files/case-binding/content")
            {
                return Bytes(binding);
            }
            if (path == "/api/2.0/files/content")
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                return Json("""{"entries":[{"id":"file-version","name":"retained","type":"file","etag":"version-1"}]}""");
            }
            return path switch
            {
                "/2.0/folders/case-folder" => Parent("405543781910"),
                "/2.0/files/case-binding" => Parent("case-folder"),
                "/2.0/folders/evidence" => Parent("case-folder"),
                "/2.0/folders/instruction" => Parent("evidence"),
                "/2.0/files/file-version" => Parent("instruction"),
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

    [Fact]
    public async Task ExactBusinessHierarchyBindsCaseSourceDocumentsVersionsAndAuditWithoutOpaqueNames()
    {
        var box = new StatefulBox();
        var sourceBytes = Encoding.UTF8.GetBytes("accepted source");
        var custody = new BoxCaseCustody(new MemoryArtifactStore(sourceBytes), CreateClient(box));
        var caseId = Guid.Parse("10213243-5465-7687-98a9-bacbdcedfe0f");
        var root = await custody.CreateCaseRootAsync(
            caseId, "QDOS31001", "0123456789ABCDEFGHJKMNPQRS", "case-create", default);
        await custody.RetainAcceptedIntakeSourceAsync(
            root,
            new(Guid.NewGuid(), "instruction.eml", "message/rfc822", Sha256(sourceBytes), "source", sourceBytes.Length),
            "source-retain",
            default);
        await custody.CreateAuditReferenceFolderAsync(
            root, "AUD31001", "123456789ABCDEFGHJKMNPQRS0", "audit-create", default);

        var documents = new BoxDocumentContentStore(CreateClient(box));
        var sameName = "damage photo.jpg";
        var first = Encoding.UTF8.GetBytes("first image revision");
        var second = Encoding.UTF8.GetBytes("second image revision");
        var other = Encoding.UTF8.GetBytes("second occurrence");
        await documents.StoreVersionAsync(Address(caseId, 2, 1, sameName), first, Sha256(first), default);
        await documents.StoreVersionAsync(Address(caseId, 2, 2, sameName), second, Sha256(second), default);
        await documents.StoreVersionAsync(Address(caseId, 3, 1, sameName), other, Sha256(other), default);

        Assert.True(box.PathExists("QDOS31001/pegasus-case-binding.json"));
        Assert.True(box.PathExists("QDOS31001/Evidence/Original instruction/001 instruction.eml"));
        Assert.True(box.PathExists("QDOS31001/Evidence/Images/002 damage photo.jpg/Revision 001/damage photo.jpg"));
        Assert.True(box.PathExists("QDOS31001/Evidence/Images/002 damage photo.jpg/Revision 002/damage photo.jpg"));
        Assert.True(box.PathExists("QDOS31001/Evidence/Images/003 damage photo.jpg/Revision 001/damage photo.jpg"));
        Assert.True(box.PathExists("QDOS31001/AUD31001/pegasus-audit-binding.json"));
        Assert.Equal(2, box.RenameCount);
        Assert.Equal(0, box.DeleteCount);
        Assert.DoesNotContain(box.FinalPathSegments, segment =>
            segment.StartsWith(".pegasus-create-", StringComparison.Ordinal)
            || Guid.TryParse(segment, out _)
            || (segment.Length == 64 && segment.All(char.IsAsciiHexDigit)));
    }

    [Fact]
    public async Task WrongBindingTypeBytesAndAncestryFailClosedWithoutMutation()
    {
        var caseId = Guid.Parse("10213243-5465-7687-98a9-bacbdcedfe0f");

        var wrongBinding = new StatefulBox();
        wrongBinding.SeedBoundCase("QDOS31001", Encoding.UTF8.GetBytes("wrong binding"));
        await Assert.ThrowsAsync<InvalidDataException>(() => new BoxCaseCustody(
            new EmptyArtifactStore(), CreateClient(wrongBinding)).GetExistingCaseRootAsync(
                caseId, "QDOS31001", default));

        var wrongType = new StatefulBox();
        wrongType.SeedFileAtRoot("QDOS31001", []);
        await Assert.ThrowsAsync<InvalidDataException>(() => new BoxCaseCustody(
            new EmptyArtifactStore(), CreateClient(wrongType)).CreateCaseRootAsync(
                caseId, "QDOS31001", "0123456789ABCDEFGHJKMNPQRS", "wrong-type", default));

        var wrongAncestry = new StatefulBox();
        wrongAncestry.SeedBoundCase("QDOS31001", BoxCaseCustody.CaseBinding(caseId, "QDOS31001"));
        wrongAncestry.MakeCaseMetadataOutside("QDOS31001");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new BoxCaseCustody(
            new EmptyArtifactStore(), CreateClient(wrongAncestry)).GetExistingCaseRootAsync(
                caseId, "QDOS31001", default));

        Assert.Equal(0, wrongBinding.MutationCount);
        Assert.Equal(0, wrongType.MutationCount);
        Assert.Equal(0, wrongAncestry.MutationCount);
    }

    [Fact]
    public async Task TerminationAndLostResponsesReconcileOnlyPredeclaredCaseAndAuditCreationMarkers()
    {
        var box = new StatefulBox { LoseNextFolderCreateResponse = true };
        var custody = new BoxCaseCustody(new EmptyArtifactStore(), CreateClient(box));
        var caseId = Guid.Parse("10213243-5465-7687-98a9-bacbdcedfe0f");
        const string caseToken = "0123456789ABCDEFGHJKMNPQRS";
        const string auditToken = "123456789ABCDEFGHJKMNPQRS0";

        await Assert.ThrowsAsync<HttpRequestException>(() => custody.CreateCaseRootAsync(
            caseId, "QDOS31001", caseToken, "case-create", default));
        Assert.True(box.PathExists($".pegasus-create-{caseToken}"));
        var root = await custody.CreateCaseRootAsync(caseId, "QDOS31001", caseToken, "case-create", default);

        box.LoseNextFolderCreateResponse = true;
        await Assert.ThrowsAsync<HttpRequestException>(() => custody.CreateAuditReferenceFolderAsync(
            root, "AUD31001", auditToken, "audit-create", default));
        Assert.True(box.PathExists($"QDOS31001/.pegasus-create-{auditToken}"));
        var audit = await custody.CreateAuditReferenceFolderAsync(
            root, "AUD31001", auditToken, "audit-create", default);

        Assert.NotEmpty(audit);
        Assert.True(box.PathExists("QDOS31001/pegasus-case-binding.json"));
        Assert.True(box.PathExists("QDOS31001/AUD31001/pegasus-audit-binding.json"));
        Assert.False(box.PathExists($".pegasus-create-{caseToken}"));
        Assert.False(box.PathExists($"QDOS31001/.pegasus-create-{auditToken}"));
        Assert.Equal(2, box.RenameCount);
        Assert.Equal(0, box.DeleteCount);

        var unrelated = new StatefulBox();
        unrelated.SeedEmptyCase("QDOS31001");
        await Assert.ThrowsAsync<InvalidDataException>(() => new BoxCaseCustody(
            new EmptyArtifactStore(), CreateClient(unrelated)).CreateCaseRootAsync(
                caseId, "QDOS31001", caseToken, "case-create", default));
    }

    private static ManagedDocumentContentAddress Address(Guid caseId, int ordinal, int version, string fileName) => new(
        caseId,
        "QDOS31001",
        Guid.Parse($"10000000-0000-0000-0000-{ordinal:D12}"),
        ordinal,
        Guid.Parse($"20000000-0000-0000-0000-{ordinal:D12}"),
        Guid.Parse($"30000000-0000-0000-{version:D4}-{ordinal:D12}"),
        version,
        DocumentSemanticRole.Image,
        fileName,
        "image/jpeg");

    private static string Sha256(ReadOnlySpan<byte> content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

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

    private static BoxContentClient CreateClient(StatefulBox box) => new(
        BoxCustodyOptions.Create(
            "https://api.box.com/2.0/",
            "https://upload.box.com/api/2.0/",
            "405543781910",
            BoxConfigJson,
            "client-secret"),
        new HttpClient(new StatefulBoxHandler(box)),
        new RecordingAuthorizationHeaderProvider());

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Bytes(ReadOnlyMemory<byte> body) =>
        new(HttpStatusCode.OK) { Content = new ByteArrayContent(body.ToArray()) };

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

    private sealed class StatefulBox
    {
        private sealed class Node(string id, string name, string type, string? parentId, byte[]? content = null)
        {
            public string Id { get; } = id;
            public string Name { get; set; } = name;
            public string Type { get; } = type;
            public string? ParentId { get; } = parentId;
            public byte[]? Content { get; } = content;
            public string? MetadataParentOverride { get; set; }
        }

        private const string Root = "405543781910";
        private readonly Dictionary<string, Node> nodes = new(StringComparer.Ordinal);
        private int sequence;

        public StatefulBox() => nodes[Root] = new(Root, "pegasus", "folder", null);

        public bool LoseNextFolderCreateResponse { get; set; }
        public int RenameCount { get; private set; }
        public int DeleteCount { get; private set; }
        public int MutationCount { get; private set; }
        public IEnumerable<string> FinalPathSegments => nodes.Values.Select(node => node.Name);

        public void SeedEmptyCase(string name) => Add("folder", Root, name, null, countMutation: false);
        public void SeedFileAtRoot(string name, byte[] bytes) => Add("file", Root, name, bytes, countMutation: false);
        public void SeedBoundCase(string name, byte[] binding)
        {
            var root = Add("folder", Root, name, null, countMutation: false);
            Add("file", root.Id, "pegasus-case-binding.json", binding, countMutation: false);
        }
        public void MakeCaseMetadataOutside(string name)
        {
            var item = Find(Root, name)!;
            item.MetadataParentOverride = "outside";
            nodes["outside"] = new("outside", "outside", "folder", null);
        }

        public bool PathExists(string path)
        {
            var parent = Root;
            foreach (var segment in path.Split('/'))
            {
                var item = Find(parent, segment);
                if (item is null) return false;
                parent = item.Id;
            }
            return true;
        }

        public HttpResponseMessage Handle(HttpRequestMessage request)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get && path.StartsWith("/2.0/folders/", StringComparison.Ordinal)
                && path.EndsWith("/items", StringComparison.Ordinal))
            {
                var parent = path["/2.0/folders/".Length..^"/items".Length];
                var entries = nodes.Values.Where(node => node.ParentId == parent)
                    .Select(node => new { id = node.Id, name = node.Name, type = node.Type, etag = "1" });
                return Json(JsonSerializer.Serialize(new { entries }));
            }
            if (request.Method == HttpMethod.Get && path.EndsWith("/content", StringComparison.Ordinal))
            {
                var id = path["/2.0/files/".Length..^"/content".Length];
                return Bytes(nodes[id].Content ?? []);
            }
            if (request.Method == HttpMethod.Get && (path.StartsWith("/2.0/folders/", StringComparison.Ordinal)
                || path.StartsWith("/2.0/files/", StringComparison.Ordinal)))
            {
                var id = path[(path.LastIndexOf('/') + 1)..];
                var node = nodes[id];
                var parentId = node.MetadataParentOverride ?? node.ParentId;
                var parent = parentId is null ? null : new { id = parentId };
                return Json(JsonSerializer.Serialize(new
                {
                    id = node.Id,
                    name = node.Name,
                    type = node.Type,
                    etag = "1",
                    parent,
                    trashed_at = (string?)null
                }));
            }
            if (request.Method == HttpMethod.Post && path == "/2.0/folders")
            {
                using var body = JsonDocument.Parse(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
                var name = body.RootElement.GetProperty("name").GetString()!;
                var parent = body.RootElement.GetProperty("parent").GetProperty("id").GetString()!;
                var existing = Find(parent, name);
                if (existing is not null)
                {
                    return new(HttpStatusCode.Conflict) { Content = new StringContent("conflict") };
                }
                var created = Add("folder", parent, name, null);
                if (LoseNextFolderCreateResponse)
                {
                    LoseNextFolderCreateResponse = false;
                    return new(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("lost response") };
                }
                return Item(created);
            }
            if (request.Method == HttpMethod.Post && path == "/api/2.0/files/content")
            {
                var multipart = Assert.IsType<MultipartFormDataContent>(request.Content);
                var attributes = multipart.First(part => part.Headers.ContentDisposition?.Name?.Trim('"') == "attributes")
                    .ReadAsStringAsync().GetAwaiter().GetResult();
                using var parsed = JsonDocument.Parse(attributes);
                var name = parsed.RootElement.GetProperty("name").GetString()!;
                var parent = parsed.RootElement.GetProperty("parent").GetProperty("id").GetString()!;
                var bytes = multipart.First(part => part.Headers.ContentDisposition?.Name?.Trim('"') == "file")
                    .ReadAsByteArrayAsync().GetAwaiter().GetResult();
                var created = Add("file", parent, name, bytes);
                return Json(JsonSerializer.Serialize(new { entries = new[] { ItemValue(created) } }));
            }
            if (request.Method == HttpMethod.Put && path.StartsWith("/2.0/folders/", StringComparison.Ordinal))
            {
                var id = path["/2.0/folders/".Length..];
                using var body = JsonDocument.Parse(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
                nodes[id].Name = body.RootElement.GetProperty("name").GetString()!;
                RenameCount++;
                MutationCount++;
                return Item(nodes[id]);
            }
            if (request.Method == HttpMethod.Delete)
            {
                DeleteCount++;
                throw new InvalidOperationException("Folder/custody deletion was not expected.");
            }
            throw new InvalidOperationException($"Unexpected Box request: {request.Method} {request.RequestUri}");
        }

        private Node Add(string type, string parent, string name, byte[]? content, bool countMutation = true)
        {
            var node = new Node($"{type}-{++sequence}", name, type, parent, content);
            nodes[node.Id] = node;
            if (countMutation) MutationCount++;
            return node;
        }
        private Node? Find(string parent, string name) =>
            nodes.Values.SingleOrDefault(node => node.ParentId == parent && node.Name == name);
        private static object ItemValue(Node node) => new { id = node.Id, name = node.Name, type = node.Type, etag = "1" };
        private static HttpResponseMessage Item(Node node) => Json(JsonSerializer.Serialize(ItemValue(node)));
    }

    private sealed class StatefulBoxHandler(StatefulBox box) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(box.Handle(request));
    }
}
