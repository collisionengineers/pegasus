using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;
using Pegasus.Core.Documents;
using Pegasus.Infrastructure.Custody;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Behavioral proof for the production managed-document content store against
/// an in-memory Box: the managed layout, hash/length verification, replay
/// semantics, delete idempotence, and child lookup beyond one Box page. No
/// network call is made.
/// </summary>
public sealed class BoxDocumentContentStoreTests
{
    private const string BoxConfigJson = """
        {"boxAppSettings":{"clientID":"client-id","appAuth":{"publicKeyID":"key-id","privateKey":"private-key","passphrase":"passphrase"}},"enterpriseID":"enterprise-id"}
        """;
    private const string ApprovedRootId = "405543781910";

    private static readonly Guid CaseId = Guid.Parse("10213243-5465-7687-98a9-bacbdcedfe0f");
    private static readonly Guid VersionId = Guid.Parse("20314253-6475-8697-a8b9-cadbecfd0e1f");
    private static readonly Guid OccurrenceId = Guid.Parse("30415263-7485-96a7-b8c9-daebfc0d1e2f");
    private static readonly Guid DocumentId = Guid.Parse("40516273-8495-a6b7-c8d9-eafb0c1d2e3f");
    private const string CaseReference = "QDOS31001";

    [Fact]
    public async Task StoreThenOpenReadRoundTripsThroughTheManagedLayout()
    {
        var box = new InMemoryBox();
        box.BindCaseRoot();
        var store = CreateStore(box);
        var content = Encoding.UTF8.GetBytes("managed document content");
        var hash = Sha256(content);

        await store.StoreVersionAsync(Address(), content, hash, CancellationToken.None);

        var expectedPath = $"{CaseReference}/Evidence/Images/002 evidence.jpg/Revision 001/evidence.jpg";
        Assert.True(box.PathExists(expectedPath), $"Expected Box path {expectedPath} to exist.");

        await using var stream = await store.OpenReadVersionAsync(
            Address(), hash, content.Length, CancellationToken.None);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        Assert.Equal(content, buffer.ToArray());
    }

    [Fact]
    public async Task IdenticalRepeatStoreIsAReplayNotASecondUpload()
    {
        var box = new InMemoryBox();
        box.BindCaseRoot();
        var store = CreateStore(box);
        var content = Encoding.UTF8.GetBytes("replayed content");
        var hash = Sha256(content);

        var first = await store.StoreVersionAsync(Address(), content, hash, CancellationToken.None);
        var uploadsAfterFirst = box.UploadCount;
        var replay = await store.StoreVersionAsync(Address(), content, hash, CancellationToken.None);

        Assert.Equal(DocumentContentWriteDisposition.Created, first.Disposition);
        Assert.Equal(DocumentContentWriteDisposition.Replay, replay.Disposition);
        Assert.Equal(2, uploadsAfterFirst); // immutable Case binding plus managed content
        Assert.Equal(2, box.UploadCount);
    }

    [Fact]
    public async Task StoreRejectsContentThatDoesNotMatchTheCustodyHash()
    {
        var box = new InMemoryBox();
        box.BindCaseRoot();
        var store = CreateStore(box);
        var content = Encoding.UTF8.GetBytes("actual content");
        var wrongHash = Sha256(Encoding.UTF8.GetBytes("different content"));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            store.StoreVersionAsync(Address(), content, wrongHash, CancellationToken.None));
        Assert.Equal(1, box.UploadCount);
    }

    [Fact]
    public async Task OpenReadFailsClosedWhenStoredContentDoesNotVerify()
    {
        var box = new InMemoryBox();
        box.BindCaseRoot();
        var store = CreateStore(box);
        var content = Encoding.UTF8.GetBytes("original content");
        var hash = Sha256(content);
        await store.StoreVersionAsync(Address(), content, hash, CancellationToken.None);

        box.CorruptFile($"{CaseReference}/Evidence/Images/002 evidence.jpg/Revision 001/evidence.jpg");

        await Assert.ThrowsAsync<InvalidDataException>(() => store.OpenReadVersionAsync(
            Address(), hash, content.Length, CancellationToken.None));
    }

    [Fact]
    public async Task UncommittedWriteRollbackIsTheOnlyDeleteAndNeverRemovesAcceptedCustody()
    {
        var box = new InMemoryBox();
        box.BindCaseRoot();
        var store = CreateStore(box);
        var content = Encoding.UTF8.GetBytes("deletable content");
        var hash = Sha256(content);
        var created = await store.StoreVersionAsync(Address(), content, hash, CancellationToken.None);

        await store.DeleteAsync(CaseId, CaseReference, VersionId, CancellationToken.None);
        await store.DeleteAsync(CaseId, CaseReference, VersionId, CancellationToken.None);

        Assert.Equal(DocumentContentWriteDisposition.Created, created.Disposition);
        Assert.False(box.PathExists($"{CaseReference}/Evidence/Images/002 evidence.jpg/Revision 001/evidence.jpg"));
        Assert.True(box.PathExists($"{CaseReference}/pegasus-case-binding.json"));
        Assert.Equal(1, box.DeleteCount);
        await Assert.ThrowsAsync<FileNotFoundException>(() => store.OpenReadVersionAsync(
            Address(), hash, content.Length, CancellationToken.None));
    }

    [Fact]
    public async Task ChildLookupPaginatesBeyondOneBoxPage()
    {
        // The approved Box root grows by one Case/PO folder per case forever,
        // so the lookup must keep paging past Box's 1000-item page rather than
        // silently missing a later Case/PO.
        var box = new InMemoryBox();
        for (var i = 0; i < 1000; i++)
        {
            box.AddFolder(ApprovedRootId, $"aaaa-decoy-case-{i:D4}");
        }
        box.BindCaseRoot();
        var store = CreateStore(box);
        var content = Encoding.UTF8.GetBytes("second page content");
        var hash = Sha256(content);
        await store.StoreVersionAsync(Address(), content, hash, CancellationToken.None);

        await using var stream = await store.OpenReadVersionAsync(
            Address(), hash, content.Length, CancellationToken.None);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);

        Assert.Equal(content, buffer.ToArray());
        Assert.True(box.LargestItemsRequestOffset >= 1000,
            "Expected the child lookup to request a second Box page.");
    }

    private static BoxDocumentContentStore CreateStore(InMemoryBox box) => new(
        new BoxContentClient(
            BoxCustodyOptions.Create(
                "https://api.box.com/2.0/",
                "https://upload.box.com/api/2.0/",
                ApprovedRootId,
                BoxConfigJson,
                "client-secret"),
            new HttpClient(new InMemoryBoxHandler(box)),
            new StaticAuthorizationHeaderProvider()));

    private static ManagedDocumentContentAddress Address() => new(
        CaseId,
        CaseReference,
        OccurrenceId,
        2,
        DocumentId,
        VersionId,
        1,
        DocumentSemanticRole.Image,
        "evidence.jpg",
        "image/jpeg");

    private static string Sha256(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    private sealed class StaticAuthorizationHeaderProvider : IBoxAuthorizationHeaderProvider
    {
        public Task<string> GetAuthorizationHeaderAsync(CancellationToken cancellationToken) =>
            Task.FromResult("Bearer test-token");
    }

    /// <summary>Minimal stateful Box: folders, files, paged listing.</summary>
    private sealed class InMemoryBox
    {
        private sealed record Node(string Id, string Name, string Type, string? ParentId);

        private readonly Dictionary<string, Node> nodes = new(StringComparer.Ordinal);
        private readonly Dictionary<string, byte[]> fileBytes = new(StringComparer.Ordinal);
        private int nextId;

        public InMemoryBox() =>
            nodes[ApprovedRootId] = new Node(ApprovedRootId, "pegasus", "folder", null);

        public int UploadCount { get; private set; }
        public int DeleteCount { get; private set; }
        public int LargestItemsRequestOffset { get; private set; }

        public string CreateFolderPath(string path)
        {
            var current = ApprovedRootId;
            foreach (var segment in path.Split('/'))
            {
                current = FindChild(current, segment, "folder")?.Id
                    ?? AddFolder(current, segment);
            }
            return current;
        }

        public string AddFolder(string parentId, string name)
        {
            var id = $"folder-{++nextId}";
            nodes[id] = new Node(id, name, "folder", parentId);
            return id;
        }

        public void BindCaseRoot()
        {
            if (FindChild(ApprovedRootId, CaseReference, "folder") is not null)
            {
                return;
            }
            var root = AddFolder(ApprovedRootId, CaseReference);
            var bindingId = $"file-{++nextId}";
            nodes[bindingId] = new Node(bindingId, "pegasus-case-binding.json", "file", root);
            fileBytes[bindingId] = JsonSerializer.SerializeToUtf8Bytes(new
            {
                schemaVersion = 1,
                caseId = CaseId,
                caseReference = CaseReference
            });
            UploadCount++;
        }

        public bool PathExists(string path)
        {
            var current = ApprovedRootId;
            foreach (var segment in path.Split('/'))
            {
                var child = FindChild(current, segment, "folder") ?? FindChild(current, segment, "file");
                if (child is null)
                {
                    return false;
                }
                current = child.Id;
            }
            return true;
        }

        public void CorruptFile(string path)
        {
            var fileId = RequireByPath(path);
            fileBytes[fileId] = Encoding.UTF8.GetBytes("corrupted");
        }

        public HttpResponseMessage Handle(HttpRequestMessage request)
        {
            var path = request.RequestUri!.AbsolutePath;
            var query = HttpUtility.ParseQueryString(request.RequestUri.Query);

            if (request.Method == HttpMethod.Get && path.StartsWith("/2.0/folders/", StringComparison.Ordinal)
                && path.EndsWith("/items", StringComparison.Ordinal))
            {
                var folderId = path["/2.0/folders/".Length..^"/items".Length];
                var limit = int.Parse(query["limit"] ?? "1000", CultureInfo.InvariantCulture);
                var offset = int.Parse(query["offset"] ?? "0", CultureInfo.InvariantCulture);
                LargestItemsRequestOffset = Math.Max(LargestItemsRequestOffset, offset);
                var children = nodes.Values
                    .Where(node => node.ParentId == folderId)
                    .OrderBy(node => node.Id, StringComparer.Ordinal)
                    .Skip(offset)
                    .Take(limit)
                    .Select(node => new { id = node.Id, name = node.Name, type = node.Type, etag = "1" });
                return Json(JsonSerializer.Serialize(new { entries = children }));
            }

            if (request.Method == HttpMethod.Get
                && (path.StartsWith("/2.0/folders/", StringComparison.Ordinal)
                    || path.StartsWith("/2.0/files/", StringComparison.Ordinal))
                && !path.EndsWith("/content", StringComparison.Ordinal))
            {
                var id = path[(path.LastIndexOf('/') + 1)..];
                if (!nodes.TryGetValue(id, out var node))
                {
                    return new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("missing") };
                }
                var parent = node.ParentId is null ? null : new { id = node.ParentId };
                return Json(JsonSerializer.Serialize(new { id = node.Id, parent, trashed_at = (string?)null }));
            }

            if (request.Method == HttpMethod.Post && path == "/2.0/folders")
            {
                using var body = JsonDocument.Parse(ReadString(request.Content!));
                var name = body.RootElement.GetProperty("name").GetString()!;
                var parentId = body.RootElement.GetProperty("parent").GetProperty("id").GetString()!;
                var id = AddFolder(parentId, name);
                return Json(JsonSerializer.Serialize(new { id, name, type = "folder", etag = "1" }));
            }

            if (request.Method == HttpMethod.Post && path == "/api/2.0/files/content")
            {
                var multipart = Assert.IsType<MultipartFormDataContent>(request.Content);
                var attributes = ReadString(multipart.First(part =>
                    part.Headers.ContentDisposition?.Name?.Trim('"') == "attributes"));
                using var parsed = JsonDocument.Parse(attributes);
                var name = parsed.RootElement.GetProperty("name").GetString()!;
                var parentId = parsed.RootElement.GetProperty("parent").GetProperty("id").GetString()!;
                var bytes = multipart.First(part =>
                        part.Headers.ContentDisposition?.Name?.Trim('"') == "file")
                    .ReadAsByteArrayAsync().GetAwaiter().GetResult();
                var id = $"file-{++nextId}";
                nodes[id] = new Node(id, name, "file", parentId);
                fileBytes[id] = bytes;
                UploadCount++;
                return Json(JsonSerializer.Serialize(new
                {
                    entries = new[] { new { id, name, type = "file", etag = "1" } }
                }));
            }

            if (request.Method == HttpMethod.Get && path.StartsWith("/2.0/files/", StringComparison.Ordinal)
                && path.EndsWith("/content", StringComparison.Ordinal))
            {
                var id = path["/2.0/files/".Length..^"/content".Length];
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(fileBytes[id])
                };
            }

            if (request.Method == HttpMethod.Delete && path.StartsWith("/2.0/files/", StringComparison.Ordinal))
            {
                var id = path["/2.0/files/".Length..];
                nodes.Remove(id);
                fileBytes.Remove(id);
                DeleteCount++;
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            throw new InvalidOperationException($"Unexpected Box request: {request.Method} {request.RequestUri}");
        }

        private Node? FindChild(string parentId, string name, string type) =>
            nodes.Values.SingleOrDefault(node =>
                node.ParentId == parentId && node.Name == name && node.Type == type);

        private string RequireByPath(string path)
        {
            var current = ApprovedRootId;
            foreach (var segment in path.Split('/'))
            {
                current = (FindChild(current, segment, "folder") ?? FindChild(current, segment, "file"))?.Id
                    ?? throw new InvalidOperationException($"Missing Box path segment '{segment}'.");
            }
            return current;
        }

        private static string ReadString(HttpContent content) =>
            content.ReadAsStringAsync().GetAwaiter().GetResult();

        private static HttpResponseMessage Json(string body) =>
            new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }

    private sealed class InMemoryBoxHandler(InMemoryBox box) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(box.Handle(request));
    }
}
