using System.Net;
using System.Text.Json;
using Pegasus.Core.Documents;
using Pegasus.Infrastructure.Custody;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Box's PDF representation of one stored version, read through the fenced
/// Box client against a scripted Box that answers as the live one was observed
/// to on 11 November 2026: the representation is listed on the file when
/// <c>x-rep-hints: [pdf]</c> is sent, its own record is polled while it is
/// being made, and the whole PDF is the content address with an empty asset
/// path. No network call is made.
/// </summary>
public sealed class BoxPdfRepresentationTests
{
    private const string BoxConfigJson = """
        {"boxAppSettings":{"clientID":"client-id","appAuth":{"publicKeyID":"key-id","privateKey":"private-key","passphrase":"passphrase"}},"enterpriseID":"enterprise-id"}
        """;
    private const string ApprovedRootId = "405543781910";
    private const string CaseFolderId = "case-folder-1";
    private const string FileId = "word-file-1";
    private const string VersionId = "word-version-1";
    private const long MaximumLength = 1_000_000;

    [Fact]
    public async Task ARepresentationBoxHoldsIsFoundWithTheHintAndDownloadedWhole()
    {
        var box = NewBox();
        var client = Client(box);

        var address = await FindAsync(client);

        Assert.Equal(box.ContentAddress, address);
        var metadata = Assert.Single(box.Requests, request => request.Kind == ScriptedBoxRequestKind.FileMetadata);
        Assert.Equal("[pdf]", metadata.RepresentationHints);
        Assert.Equal("Bearer test-token", metadata.Authorization);
        Assert.Contains("representations", metadata.Uri.Query, StringComparison.Ordinal);
        Assert.Equal(0, box.CountOf(ScriptedBoxRequestKind.RepresentationInfo));

        await using var pdf = await client.DownloadPdfRepresentationAsync(address!, MaximumLength, CancellationToken.None);
        using var copy = new MemoryStream();
        await pdf.CopyToAsync(copy);
        Assert.Equal(box.Rendering, copy.ToArray());
        var download = Assert.Single(box.Requests, request => request.Kind == ScriptedBoxRequestKind.RepresentationContent);
        Assert.Equal("Bearer test-token", download.Authorization);
        Assert.Equal(box.ContentAddress, download.Uri);
    }

    /// <summary>
    /// A representation Box has not made yet is asked for again at its own
    /// record, which is also what starts it, until Box says it is made.
    /// </summary>
    [Theory]
    [InlineData("none")]
    [InlineData("pending")]
    public async Task ARepresentationStillBeingMadeIsPolledAtItsOwnRecordUntilItIsMade(string state)
    {
        var box = NewBox();
        box.State = state;
        box.AnswerNextPollWith("success");
        var client = Client(box);

        var address = await client.FindOwnedVersionPdfAsync(
            FileId, VersionId, CaseFolderId, CaseDocumentPreviews.RenditionWait, CancellationToken.None);

        Assert.Equal(box.ContentAddress, address);
        var poll = Assert.Single(box.Requests, request => request.Kind == ScriptedBoxRequestKind.RepresentationInfo);
        Assert.Equal(box.InfoAddress, poll.Uri);
        Assert.Equal("Bearer test-token", poll.Authorization);
        Assert.Equal(0, box.CountOf(ScriptedBoxRequestKind.RepresentationContent));
    }

    [Fact]
    public async Task ARepresentationStillPendingWhenTheWaitRunsOutIsATimeout()
    {
        var box = NewBox();
        box.State = "pending";
        var client = Client(box);

        // The first poll would come after 250 ms, past this wait, so none is made.
        await Assert.ThrowsAsync<TimeoutException>(() => client.FindOwnedVersionPdfAsync(
            FileId, VersionId, CaseFolderId, TimeSpan.FromMilliseconds(200), CancellationToken.None));

        Assert.Equal(0, box.CountOf(ScriptedBoxRequestKind.RepresentationInfo));
        Assert.Equal(0, box.CountOf(ScriptedBoxRequestKind.RepresentationContent));
    }

    [Fact]
    public async Task ARepresentationBoxCouldNotMakeIsNone()
    {
        var box = NewBox();
        box.State = "error";

        Assert.Null(await FindAsync(Client(box)));

        Assert.Equal(0, box.CountOf(ScriptedBoxRequestKind.RepresentationInfo));
        Assert.Equal(0, box.CountOf(ScriptedBoxRequestKind.RepresentationContent));
    }

    [Fact]
    public async Task AFileWithNoPdfRepresentationListedHasNone()
    {
        var otherOnly = NewBox();
        otherOnly.RepresentationName = "jpg";
        Assert.Null(await FindAsync(Client(otherOnly)));

        var unlisted = NewBox();
        unlisted.ListsRepresentations = false;
        Assert.Null(await FindAsync(Client(unlisted)));

        Assert.Equal(0, otherOnly.CountOf(ScriptedBoxRequestKind.RepresentationInfo)
            + unlisted.CountOf(ScriptedBoxRequestKind.RepresentationInfo));
    }

    /// <summary>
    /// A representation describes the file's current version, so a stored
    /// version Box has since replaced has none: nothing is polled or fetched.
    /// </summary>
    [Fact]
    public async Task AStoredVersionThatIsNoLongerCurrentHasNoRepresentation()
    {
        var box = NewBox();
        box.CurrentVersionId = "word-version-2";

        Assert.Null(await FindAsync(Client(box)));

        Assert.Equal(1, box.CountOf(ScriptedBoxRequestKind.FileMetadata));
        Assert.Equal(0, box.CountOf(ScriptedBoxRequestKind.RepresentationInfo));
        Assert.Equal(0, box.CountOf(ScriptedBoxRequestKind.RepresentationContent));
    }

    /// <summary>
    /// The file passes the read path's checks first. A trashed file, or one
    /// outside its expected folder, is refused, and the refusal forgets the
    /// folder, so the next read walks its ancestry again.
    /// </summary>
    [Theory]
    [InlineData("trashed")]
    [InlineData("moved")]
    public async Task ARefusedFileIsNotAskedForItsRepresentationAndTheRefusalForgetsTheFolder(string state)
    {
        var box = NewBox();
        var client = Client(box);
        Assert.NotNull(await FindAsync(client));
        Assert.NotNull(await FindAsync(client));
        // The second read trusted the remembered folder.
        Assert.Equal(1, box.CountOf(ScriptedBoxRequestKind.FolderRead));

        if (state == "trashed")
        {
            box.Trashed = true;
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => FindAsync(client));
            box.Trashed = false;
        }
        else
        {
            box.ParentId = "another-folder";
            await Assert.ThrowsAsync<InvalidDataException>(() => FindAsync(client));
            box.ParentId = CaseFolderId;
        }

        Assert.Equal(1, box.CountOf(ScriptedBoxRequestKind.FolderRead));
        Assert.NotNull(await FindAsync(client));
        Assert.Equal(2, box.CountOf(ScriptedBoxRequestKind.FolderRead));
        Assert.Equal(0, box.CountOf(ScriptedBoxRequestKind.RepresentationContent));
    }

    [Fact]
    public async Task BoxsRateLimitOnTheFileIsAThrottle()
    {
        var box = NewBox();
        box.MetadataStatus = HttpStatusCode.TooManyRequests;

        await Assert.ThrowsAsync<BoxThrottledException>(() => FindAsync(Client(box)));

        Assert.Equal(0, box.CountOf(ScriptedBoxRequestKind.FolderRead));
    }

    /// <summary>
    /// The bearer token goes only to Box's own hosts over HTTPS, so a content
    /// address anywhere else is no representation, and nothing is sent there.
    /// </summary>
    [Theory]
    [InlineData("https://example.org/api/2.0/internal_files/word-file-1/versions/word-version-1/representations/pdf/content/{+asset_path}")]
    [InlineData("http://dl.boxcloud.com/api/2.0/internal_files/word-file-1/versions/word-version-1/representations/pdf/content/{+asset_path}")]
    [InlineData("https://dl.boxcloud.com.example.org/representations/pdf/content/{+asset_path}")]
    [InlineData("https://evilboxcloud.com/representations/pdf/content/{+asset_path}")]
    public async Task AContentAddressOffBoxsOwnHostsIsNoRepresentation(string template)
    {
        var box = NewBox();
        box.ContentTemplate = template;

        Assert.Null(await FindAsync(Client(box)));

        Assert.All(box.Requests, request => Assert.Equal("api.box.com", request.Uri.Host));
        Assert.Equal(0, box.CountOf(ScriptedBoxRequestKind.RepresentationInfo));
    }

    [Fact]
    public async Task ADownloadBoxHasNotFinishedIsATimeout()
    {
        var box = NewBox();
        box.ContentStatus = HttpStatusCode.Accepted;

        await Assert.ThrowsAsync<TimeoutException>(() =>
            Client(box).DownloadPdfRepresentationAsync(box.ContentAddress, MaximumLength, CancellationToken.None));
    }

    [Fact]
    public async Task AMissingRepresentationDownloadIsNotFound()
    {
        var box = NewBox();
        box.ContentStatus = HttpStatusCode.NotFound;

        var error = await Assert.ThrowsAsync<HttpRequestException>(() =>
            Client(box).DownloadPdfRepresentationAsync(box.ContentAddress, MaximumLength, CancellationToken.None));

        Assert.Equal(HttpStatusCode.NotFound, error.StatusCode);
    }

    [Fact]
    public async Task ARepresentationLongerThanTheLimitIsRefused()
    {
        var box = NewBox();

        await Assert.ThrowsAsync<InvalidDataException>(() => Client(box).DownloadPdfRepresentationAsync(
            box.ContentAddress, box.Rendering.LongLength - 1, CancellationToken.None));
    }

    [Fact]
    public async Task AnAddressThatIsNotBoxsIsNeverDownloaded()
    {
        var box = NewBox();
        var client = Client(box);

        await Assert.ThrowsAsync<ArgumentException>(() => client.DownloadPdfRepresentationAsync(
            new Uri("https://example.org/representations/pdf/content/"), MaximumLength, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => client.DownloadPdfRepresentationAsync(
            new Uri("http://dl.boxcloud.com/representations/pdf/content/"), MaximumLength, CancellationToken.None));

        Assert.Empty(box.Requests);
    }

    private static ScriptedPdfRepresentationBox NewBox() =>
        new(ApprovedRootId, CaseFolderId, FileId, VersionId);

    private static Task<Uri?> FindAsync(BoxContentClient client) =>
        client.FindOwnedVersionPdfAsync(
            FileId, VersionId, CaseFolderId, CaseDocumentPreviews.RenditionWait, CancellationToken.None);

    private static BoxContentClient Client(ScriptedPdfRepresentationBox box) =>
        new(
            BoxCustodyOptions.Create(
                "https://api.box.com/2.0/",
                "https://upload.box.com/api/2.0/",
                ApprovedRootId,
                BoxConfigJson,
                "client-secret",
                "holding-folder"),
            new HttpClient(box),
            new StaticAuthorizationHeaderProvider(),
            TimeProvider.System,
            TimeProviderMemoryCache.Create(TimeProvider.System));

    private sealed class StaticAuthorizationHeaderProvider : IBoxAuthorizationHeaderProvider
    {
        public Task<string> GetAuthorizationHeaderAsync(CancellationToken cancellationToken) =>
            Task.FromResult("Bearer test-token");

        public Task<bool> RenewIfDueAsync(CancellationToken cancellationToken) =>
            Task.FromResult(false);
    }
}

/// <summary>What one request to the scripted representation Box asked for.</summary>
internal enum ScriptedBoxRequestKind
{
    FileMetadata,
    FolderRead,
    RepresentationInfo,
    RepresentationContent,
    Other
}

/// <summary>One request the scripted representation Box was sent, as it was sent.</summary>
internal sealed record ScriptedBoxRequest(
    ScriptedBoxRequestKind Kind,
    Uri Uri,
    string? Authorization,
    string? RepresentationHints);

/// <summary>
/// A scripted Box holding one Word document in one Case folder directly under
/// the approved root, with a PDF representation of its current version. Every
/// request is recorded; what the file, the representation and its content say
/// is set by the test.
/// </summary>
internal sealed class ScriptedPdfRepresentationBox(
    string rootFolderId,
    string folderId,
    string fileId,
    string versionId) : HttpMessageHandler
{
    private readonly string root = rootFolderId;
    private readonly string folder = folderId;
    private readonly string file = fileId;
    private readonly Lock gate = new();
    private readonly List<ScriptedBoxRequest> requests = [];
    private readonly Queue<string> pollStates = new();
    private readonly TaskCompletionSource metadataReached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource metadataReleased = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>The representation's state on the file's own record.</summary>
    public string State { get; set; } = "success";

    /// <summary>The version Box reports as the file's current one.</summary>
    public string CurrentVersionId { get; set; } = versionId;

    /// <summary>The folder Box reports as the file's parent.</summary>
    public string ParentId { get; set; } = folderId;

    public bool Trashed { get; set; }

    /// <summary>When false, the file's record carries no representations at all.</summary>
    public bool ListsRepresentations { get; set; } = true;

    /// <summary>The one representation the file lists.</summary>
    public string RepresentationName { get; set; } = "pdf";

    /// <summary>When set, the file's record is answered with this status alone.</summary>
    public HttpStatusCode? MetadataStatus { get; set; }

    /// <summary>The content address template Box gives, when not its own.</summary>
    public string? ContentTemplate { get; set; }

    /// <summary>How the content address answers.</summary>
    public HttpStatusCode ContentStatus { get; set; } = HttpStatusCode.OK;

    /// <summary>The bytes the content address serves.</summary>
    public byte[] Rendering { get; set; } = "%PDF-1.7\n%scripted Box rendering\n%%EOF\n"u8.ToArray();

    /// <summary>When set, the file's record is not answered until <see cref="ReleaseMetadata"/>.</summary>
    public bool HoldMetadata { get; set; }

    /// <summary>Completes when a held file record is first asked for.</summary>
    public Task MetadataReached => metadataReached.Task;

    public void ReleaseMetadata() => metadataReleased.TrySetResult();

    public Uri InfoAddress =>
        new($"https://api.box.com/2.0/internal_files/{file}/versions/{CurrentVersionId}/representations/pdf");

    public Uri ContentAddress =>
        new($"https://dl.boxcloud.com/api/2.0/internal_files/{file}/versions/{CurrentVersionId}/representations/pdf/content/");

    public IReadOnlyList<ScriptedBoxRequest> Requests
    {
        get
        {
            lock (gate)
            {
                return [.. requests];
            }
        }
    }

    public int CountOf(ScriptedBoxRequestKind kind)
    {
        lock (gate)
        {
            return requests.Count(request => request.Kind == kind);
        }
    }

    /// <summary>The state the next poll of the representation's record answers.</summary>
    public void AnswerNextPollWith(string state)
    {
        lock (gate)
        {
            pollStates.Enqueue(state);
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        var kind = KindOf(uri);
        var hints = request.Headers.TryGetValues("x-rep-hints", out var values)
            ? string.Join(",", values)
            : null;
        lock (gate)
        {
            requests.Add(new(kind, uri, request.Headers.Authorization?.ToString(), hints));
        }
        if (kind == ScriptedBoxRequestKind.FileMetadata && HoldMetadata)
        {
            metadataReached.TrySetResult();
            await metadataReleased.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        }
        return kind switch
        {
            ScriptedBoxRequestKind.FileMetadata => FileRecord(),
            ScriptedBoxRequestKind.FolderRead => Json(JsonSerializer.Serialize(new
            {
                id = folder,
                name = "QDOS091",
                type = "folder",
                etag = "1",
                parent = new { id = root }
            })),
            ScriptedBoxRequestKind.RepresentationInfo => Json(JsonSerializer.Serialize(new
            {
                representation = "pdf",
                properties = new { },
                status = new { state = NextPollState() }
            })),
            ScriptedBoxRequestKind.RepresentationContent => ContentStatus == HttpStatusCode.OK
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Rendering) }
                : new HttpResponseMessage(ContentStatus),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("missing") }
        };
    }

    private ScriptedBoxRequestKind KindOf(Uri uri)
    {
        if (string.Equals(uri.Host, "dl.boxcloud.com", StringComparison.OrdinalIgnoreCase))
        {
            return ScriptedBoxRequestKind.RepresentationContent;
        }
        if (!string.Equals(uri.Host, "api.box.com", StringComparison.OrdinalIgnoreCase))
        {
            return ScriptedBoxRequestKind.Other;
        }
        var path = uri.AbsolutePath;
        if (string.Equals(path, $"/2.0/files/{file}", StringComparison.Ordinal))
        {
            return ScriptedBoxRequestKind.FileMetadata;
        }
        if (string.Equals(path, $"/2.0/folders/{folder}", StringComparison.Ordinal))
        {
            return ScriptedBoxRequestKind.FolderRead;
        }
        return path.StartsWith("/2.0/internal_files/", StringComparison.Ordinal)
            ? ScriptedBoxRequestKind.RepresentationInfo
            : ScriptedBoxRequestKind.Other;
    }

    private HttpResponseMessage FileRecord()
    {
        if (MetadataStatus is { } status)
        {
            return new HttpResponseMessage(status) { Content = new StringContent("""{"code":"scripted"}""") };
        }
        var representations = new
        {
            entries = new[]
            {
                new
                {
                    representation = RepresentationName,
                    properties = new { },
                    info = new { url = InfoAddress.AbsoluteUri },
                    status = new { state = State },
                    content = new { url_template = ContentTemplate ?? ContentAddress.AbsoluteUri + "{+asset_path}" }
                }
            }
        };
        return Json(JsonSerializer.Serialize(new
        {
            id = file,
            name = "letter.doc",
            type = "file",
            etag = "1",
            file_version = new { id = CurrentVersionId },
            size = 4096,
            content_type = "application/msword",
            parent = new { id = ParentId },
            trashed_at = Trashed ? "2031-01-01T00:00:00Z" : null,
            representations = ListsRepresentations ? representations : null
        }));
    }

    private string NextPollState()
    {
        lock (gate)
        {
            return pollStates.TryDequeue(out var state) ? state : State;
        }
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
        };
}
