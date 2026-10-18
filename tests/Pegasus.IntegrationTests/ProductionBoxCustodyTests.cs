using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Logging;
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
            "client-secret",
            "test-holding-folder"));

        Assert.Contains("405543781910", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A host names the one root it may write under. The development root is
    /// accepted only when a host asks for it, and the production root is then
    /// refused in turn: neither root is ever a free setting.
    /// </summary>
    [Fact]
    public void ConfigurationAcceptsOnlyTheRootTheHostNames()
    {
        var development = BoxCustodyOptions.Create(
            "https://api.box.com/2.0/",
            "https://upload.box.com/api/2.0/",
            BoxCustodyOptions.DevelopmentRootFolderId,
            BoxConfigJson,
            "client-secret",
            "test-holding-folder",
            BoxCustodyOptions.DevelopmentRootFolderId);
        var productionRootUnderDevelopmentHost = Assert.Throws<InvalidOperationException>(() => BoxCustodyOptions.Create(
            "https://api.box.com/2.0/",
            "https://upload.box.com/api/2.0/",
            BoxCustodyOptions.ProductionRootFolderId,
            BoxConfigJson,
            "client-secret",
            "test-holding-folder",
            BoxCustodyOptions.DevelopmentRootFolderId));
        var developmentRootUnderProductionHost = Assert.Throws<InvalidOperationException>(() => BoxCustodyOptions.Create(
            "https://api.box.com/2.0/",
            "https://upload.box.com/api/2.0/",
            BoxCustodyOptions.DevelopmentRootFolderId,
            BoxConfigJson,
            "client-secret",
            "test-holding-folder"));

        Assert.Equal("425169015650", development.RootFolderId);
        Assert.Contains("425169015650", productionRootUnderDevelopmentHost.Message, StringComparison.Ordinal);
        Assert.Contains("405543781910", developmentRootUnderProductionHost.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfigurationRejectsMissingOrMalformedJwtMaterial()
    {
        var missingConfiguration = Assert.Throws<InvalidOperationException>(() => BoxCustodyOptions.Create(
            "https://api.box.com/2.0/",
            "https://upload.box.com/api/2.0/",
            "405543781910",
            null,
            "client-secret",
            "test-holding-folder"));
        var missingSecret = Assert.Throws<InvalidOperationException>(() => BoxCustodyOptions.Create(
            "https://api.box.com/2.0/",
            "https://upload.box.com/api/2.0/",
            "405543781910",
            BoxConfigJson,
            null,
            "test-holding-folder"));
        var malformedConfiguration = Assert.Throws<InvalidOperationException>(() => BoxCustodyOptions.Create(
            "https://api.box.com/2.0/",
            "https://upload.box.com/api/2.0/",
            "405543781910",
            "{}",
            "client-secret",
            "test-holding-folder"));

        Assert.Contains("ConfigJson", missingConfiguration.Message, StringComparison.Ordinal);
        Assert.Contains("ClientSecret", missingSecret.Message, StringComparison.Ordinal);
        Assert.Contains("valid Box JWT", malformedConfiguration.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfigurationNamesAnUnresolvedKeyVaultReferenceDirectly()
    {
        // During provisioning App Service can pass the literal
        // @Microsoft.KeyVault(...) placeholder. That state must be named, not
        // reported as a malformed Box JWT configuration.
        var unresolvedConfig = Assert.Throws<InvalidOperationException>(() => BoxCustodyOptions.Create(
            "https://api.box.com/2.0/",
            "https://upload.box.com/api/2.0/",
            "405543781910",
            "@Microsoft.KeyVault(SecretUri=https://example.vault.azure.net/secrets/box-config-json)",
            "client-secret",
            "test-holding-folder"));
        var unresolvedSecret = Assert.Throws<InvalidOperationException>(() => BoxCustodyOptions.Create(
            "https://api.box.com/2.0/",
            "https://upload.box.com/api/2.0/",
            "405543781910",
            BoxConfigJson,
            "@Microsoft.KeyVault(SecretUri=https://example.vault.azure.net/secrets/box-client-secret)",
            "test-holding-folder"));

        Assert.Contains("Box:ConfigJson is an unresolved Key Vault reference", unresolvedConfig.Message, StringComparison.Ordinal);
        Assert.Contains("Box:ClientSecret is an unresolved Key Vault reference", unresolvedSecret.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExistingCaseRootIsReturnedOnlyAfterAncestryReachesTheApprovedRoot()
    {
        var caseId = Guid.Parse("10213243-5465-7687-98a9-bacbdcedfe0f");
        var expectedName = "QDOS31001";
        var handler = new DelegateHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/2.0/folders/405543781910/items" => Json($$"""{"entries":[{"id":"case-folder","name":"{{expectedName}}","type":"folder","etag":"1"}]}"""),
            "/2.0/folders/case-folder" => Json($$"""{"id":"case-folder","name":"{{expectedName}}","type":"folder","parent":{"id":"405543781910"},"trashed_at":null}"""),
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
            "/2.0/folders/case-folder" => Json($$"""{"id":"case-folder","name":"{{expectedName}}","type":"folder","parent":{"id":"outside"},"trashed_at":null}"""),
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
        var handler = new DelegateHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/api/2.0/files/content")
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                return Json("""{"entries":[{"id":"file-version","name":"retained","type":"file","etag":"version-1","file_version":{"id":"box-version-1"},"parent":{"id":"case-folder"}}]}""");
            }
            return path switch
            {
                "/2.0/folders/case-folder" => Json($$"""{"id":"case-folder","name":"{{expectedCaseName}}","type":"folder","parent":{"id":"405543781910"},"trashed_at":null}"""),
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
        // One read proves the Case folder and one upload files the source:
        // no root listing, no name lookup, no walk after the upload.
        Assert.Equal(2, handler.Uris.Count);
        Assert.Equal("/2.0/folders/case-folder", handler.Uris[0].AbsolutePath);
        Assert.Equal("/api/2.0/files/content", handler.Uris[1].AbsolutePath);
        Assert.All(handler.Methods, method => Assert.Contains(method, new[] { HttpMethod.Get, HttpMethod.Post }));
        Assert.DoesNotContain(handler.Uris, uri =>
            uri.AbsolutePath.Contains("delete", StringComparison.OrdinalIgnoreCase)
            || uri.AbsolutePath.Contains("move", StringComparison.OrdinalIgnoreCase)
            || uri.AbsolutePath.Contains("copy", StringComparison.OrdinalIgnoreCase)
            || uri.AbsolutePath.Contains("share", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            handler.RequestBodies,
            body => body.Contains(expectedFileName, StringComparison.Ordinal));
    }

    /// <summary>
    /// A piece of work carries one root through all its files, so it proves the
    /// Case folder with one read however many files it files, and it never
    /// lists the approved root to do it.
    /// </summary>
    [Fact]
    public async Task AWorkItemProvesTheCaseFolderOnceWhateverItsFileCount()
    {
        var box = new StatefulBox();
        var bytes = Encoding.UTF8.GetBytes("retained image bytes");
        var custody = new BoxCaseCustody(new MemoryArtifactStore(bytes), CreateClient(box));
        var root = await custody.CreateCaseRootAsync(
            Guid.NewGuid(), "AB12CDE-01", "0123456789ABCDEFGHJKMNPQRS", "image-root", default);
        var created = box.Requests.Count;

        for (var ordinal = 1; ordinal <= 3; ordinal++)
        {
            await custody.RetainImageCaseAssetAsync(
                root, ImagePhoto(bytes, $"photo {ordinal}.jpg"), ordinal, $"retain-{ordinal}", default);
        }

        var requests = box.Requests.Skip(created).ToArray();
        Assert.Equal(1, requests.Count(request => IsCaseFolderProof(request, root.RemoteId)));
        Assert.DoesNotContain(requests, request => request.StartsWith(
            "GET /2.0/folders/405543781910/items", StringComparison.Ordinal));
        Assert.Equal(3, requests.Count(IsUpload));
        // Before this change every file cost eight requests besides its upload
        // (two listings of the approved root, a folder read, an ancestry read,
        // a listing of the Case folder, the upload's fence read, and two reads
        // after it). Now the only request besides the uploads is the one proof.
        Assert.Equal(1, requests.Count(request => !IsUpload(request)));
    }

    /// <summary>
    /// Creating a fresh owned folder lists its parent once before the create,
    /// where it listed the parent for the final name and again for the staging
    /// name.
    /// </summary>
    [Fact]
    public async Task CreatingAnOwnedFolderListsItsParentBeforeTheCreateOnce()
    {
        var box = new StatefulBox();
        var custody = new BoxCaseCustody(new MemoryArtifactStore(Array.Empty<byte>()), CreateClient(box));
        var before = box.Requests.Count;

        await custody.CreateCaseRootAsync(
            Guid.NewGuid(), "AB12CDE-01", "0123456789ABCDEFGHJKMNPQRS", "image-root", default);

        var requests = box.Requests.Skip(before).ToArray();
        var firstCreate = Array.FindIndex(requests, request => request.StartsWith("POST /2.0/folders", StringComparison.Ordinal));
        Assert.True(firstCreate > 0, string.Join("\n", requests));
        Assert.Equal(1, requests.Take(firstCreate).Count(request =>
            request.Contains("/items", StringComparison.Ordinal)));
    }

    /// <summary>
    /// Files filed at the same time share the one proof of their root: however
    /// many arrive together, the Case folder is read once.
    /// </summary>
    [Fact]
    public async Task FilesFiledTogetherShareOneProofOfTheirRoot()
    {
        var box = new StatefulBox();
        var bytes = Encoding.UTF8.GetBytes("retained image bytes");
        var custody = new BoxCaseCustody(new MemoryArtifactStore(bytes), CreateClient(box));
        var root = await custody.CreateCaseRootAsync(
            Guid.NewGuid(), "AB12CDE-01", "0123456789ABCDEFGHJKMNPQRS", "image-root", default);
        var created = box.Requests.Count;

        await Task.WhenAll(Enumerable.Range(1, 3).Select(ordinal => custody.RetainImageCaseAssetAsync(
            root, ImagePhoto(bytes, $"photo {ordinal}.jpg"), ordinal, $"retain-{ordinal}", default)));

        var requests = box.Requests.Skip(created).ToArray();
        Assert.Equal(1, requests.Count(request => IsCaseFolderProof(request, root.RemoteId)));
        Assert.Equal(3, requests.Count(IsUpload));
        Assert.Equal(4, requests.Length);
        Assert.True(box.PathExists("AB12CDE-01/001 photo 1.jpg"));
        Assert.True(box.PathExists("AB12CDE-01/003 photo 3.jpg"));
    }

    /// <summary>
    /// A proof that fails is not kept: the next file to use the same root proves
    /// it again, so a folder put right in Box is not refused for good.
    /// </summary>
    [Fact]
    public async Task AProofThatFailedIsMadeAgainByTheNextFile()
    {
        var current = "trashed";
        var handler = new DelegateHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/2.0/folders/case-folder" => current == "trashed"
                ? CaseFolder("QDOS31001", "405543781910", trashedAt: "2031-01-01T00:00:00Z")
                : CaseFolder("QDOS31001", "405543781910"),
            "/api/2.0/files/content" => Json(
                """{"entries":[{"id":"file-1","name":"001 instruction.eml","type":"file","etag":"1","file_version":{"id":"v1"},"parent":{"id":"case-folder"}}]}"""),
            _ => throw new InvalidOperationException(request.RequestUri.AbsoluteUri)
        });
        var bytes = Encoding.UTF8.GetBytes("accepted source");
        var custody = Create(handler, new MemoryArtifactStore(bytes));
        var root = new CaseCustodyRoot(Guid.NewGuid(), "case-folder", "QDOS31001");
        var source = new IntakeSourceCustodyReference(
            Guid.NewGuid(), "instruction.eml", "message/rfc822", Sha256(bytes), "source-key", bytes.Length);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            custody.RetainAcceptedIntakeSourceAsync(root, source, "retain-operation", default));
        current = "restored";
        var retained = await custody.RetainAcceptedIntakeSourceAsync(root, source, "retain-operation", default);

        Assert.Equal("file-1", retained.RemoteId);
    }

    /// <summary>
    /// The upload is the first thing asked for a file. A name Box already
    /// holds comes back as its 409, and only then is the existing file found,
    /// and accepted when its content is the content offered.
    /// </summary>
    [Fact]
    public async Task ANameBoxAlreadyHoldsIsResolvedFromItsConflictAnswer()
    {
        var box = new StatefulBox();
        var bytes = Encoding.UTF8.GetBytes("retained image bytes");
        var custody = new BoxCaseCustody(new MemoryArtifactStore(bytes), CreateClient(box));
        var root = await custody.CreateCaseRootAsync(
            Guid.NewGuid(), "AB12CDE-01", "0123456789ABCDEFGHJKMNPQRS", "image-root", default);
        var photo = ImagePhoto(bytes, "photo one.jpg");
        var first = await custody.RetainImageCaseAssetAsync(root, photo, 1, "retain-1", default);
        var mutations = box.MutationCount;
        var before = box.Requests.Count;

        var replay = await custody.RetainImageCaseAssetAsync(root, photo, 1, "retain-1", default);

        var requests = box.Requests.Skip(before).ToArray();
        Assert.True(IsUpload(requests[0]), requests[0]);
        Assert.Equal(first.RemoteId, replay.RemoteId);
        Assert.Equal(first.BoxVersionId, replay.BoxVersionId);
        Assert.Equal(mutations, box.MutationCount);

        box.CorruptFile("AB12CDE-01/001 photo one.jpg");
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            custody.RetainImageCaseAssetAsync(root, photo, 1, "retain-1", default));
    }

    /// <summary>
    /// The occupant of the name is checked before its bytes are: it must be a
    /// file, of the content's length and, when Box says, its type.
    /// </summary>
    [Theory]
    [InlineData("folder")]
    [InlineData("other-length")]
    [InlineData("other-type")]
    public async Task ANameHeldByAnythingButThisFileIsRefused(string occupant)
    {
        var box = new StatefulBox();
        var bytes = Encoding.UTF8.GetBytes("retained image bytes");
        var custody = new BoxCaseCustody(new MemoryArtifactStore(bytes), CreateClient(box));
        var root = await custody.CreateCaseRootAsync(
            Guid.NewGuid(), "AB12CDE-01", "0123456789ABCDEFGHJKMNPQRS", "image-root", default);
        switch (occupant)
        {
            case "folder":
                await CreateClient(box).CreateFolderAsync(root.RemoteId, "001 photo one.jpg", default);
                break;
            default:
                await custody.RetainImageCaseAssetAsync(
                    root, ImagePhoto(bytes, "photo one.jpg"), 1, "retain-1", default);
                if (occupant == "other-length")
                {
                    box.CorruptFile("AB12CDE-01/001 photo one.jpg");
                }
                else
                {
                    box.SetMediaType("AB12CDE-01/001 photo one.jpg", "application/pdf");
                }
                break;
        }
        var mutations = box.MutationCount;

        await Assert.ThrowsAsync<InvalidDataException>(() => custody.RetainImageCaseAssetAsync(
            root, ImagePhoto(bytes, "photo one.jpg"), 1, "retain-1", default));

        Assert.Equal(mutations, box.MutationCount);
    }

    /// <summary>
    /// The 409 codes an upload can meet that are not "name in use". Box says
    /// "later" for a name it has reserved and for a folder busy with another
    /// operation, and the failure is the one the queue retries.
    /// </summary>
    [Theory]
    [InlineData("name_temporarily_reserved", "temporarily reserved")]
    [InlineData("operation_blocked_temporary", "blocked the upload")]
    [InlineData("conflict", "rejected")]
    public async Task AnUploadConflictThatIsNotANameInUseIsAFailureTheQueueRetries(string code, string message)
    {
        var box = new StatefulBox { NextUploadConflictCode = code };
        var bytes = Encoding.UTF8.GetBytes("retained image bytes");
        var custody = new BoxCaseCustody(new MemoryArtifactStore(bytes), CreateClient(box));
        var root = await custody.CreateCaseRootAsync(
            Guid.NewGuid(), "AB12CDE-01", "0123456789ABCDEFGHJKMNPQRS", "image-root", default);
        var mutations = box.MutationCount;
        var before = box.Requests.Count;

        var failure = await Assert.ThrowsAsync<HttpRequestException>(() =>
            custody.RetainImageCaseAssetAsync(root, ImagePhoto(bytes, "photo one.jpg"), 1, "retain-1", default));

        Assert.Equal(HttpStatusCode.Conflict, failure.StatusCode);
        Assert.Contains(message, failure.Message, StringComparison.Ordinal);
        Assert.Equal(mutations, box.MutationCount);
        Assert.DoesNotContain(box.Requests.Skip(before), request => request.Contains("/items", StringComparison.Ordinal));
    }

    /// <summary>
    /// A folder the database records is proved to be the Case's own by one
    /// read: named for the Case, directly under the approved root, not in the
    /// trash. Anything else is refused, and the read memory forgets the folder.
    /// </summary>
    [Theory]
    [InlineData("trashed", typeof(UnauthorizedAccessException))]
    [InlineData("renamed", typeof(UnauthorizedAccessException))]
    [InlineData("moved", typeof(UnauthorizedAccessException))]
    [InlineData("missing", typeof(HttpRequestException))]
    public async Task ARecordedFolderThatIsNotTheCasesOwnIsRefusedAndForgotten(string state, Type refusal)
    {
        var requests = 0;
        var current = "good";
        var handler = new DelegateHandler(request =>
        {
            requests++;
            Assert.Equal("/2.0/folders/case-folder", request.RequestUri!.AbsolutePath);
            return current switch
            {
                "trashed" => CaseFolder("QDOS31001", "405543781910", trashedAt: "2031-01-01T00:00:00Z"),
                "renamed" => CaseFolder("QDOS99999", "405543781910"),
                "moved" => CaseFolder("QDOS31001", "another-folder"),
                "missing" => new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("missing")
                },
                _ => CaseFolder("QDOS31001", "405543781910")
            };
        });
        var client = CreateClient(handler);
        var custody = new BoxCaseCustody(new EmptyArtifactStore(), client);
        await client.EnsureLiveFolderForReadAsync("case-folder", default);
        await client.EnsureLiveFolderForReadAsync("case-folder", default);
        Assert.Equal(1, requests);

        current = state;
        await Assert.ThrowsAsync(refusal, () => custody.GetExistingCaseRootAsync(
            Guid.NewGuid(), "QDOS31001", "case-folder", default));

        current = "good";
        var before = requests;
        await client.EnsureLiveFolderForReadAsync("case-folder", default);
        Assert.Equal(before + 1, requests);
    }

    /// <summary>
    /// No walk follows an upload. Box's own answer must name the proved folder
    /// as the file's parent, and an answer that names another one, or none, is
    /// refused.
    /// </summary>
    [Theory]
    [InlineData(",\"parent\":{\"id\":\"another-folder\"}")]
    [InlineData("")]
    public async Task AnUploadWhoseAnswerDoesNotNameTheProvedFolderIsRefused(string parent)
    {
        var handler = new DelegateHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/2.0/folders/case-folder" => CaseFolder("QDOS31001", "405543781910"),
            "/api/2.0/files/content" => Json(
                """{"entries":[{"id":"file-1","name":"001 instruction.eml","type":"file","etag":"1","file_version":{"id":"v1"}"""
                + parent + "}]}"),
            _ => throw new InvalidOperationException(request.RequestUri.AbsoluteUri)
        });
        var bytes = Encoding.UTF8.GetBytes("accepted source");
        var custody = Create(handler, new MemoryArtifactStore(bytes));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => custody.RetainAcceptedIntakeSourceAsync(
            new CaseCustodyRoot(Guid.NewGuid(), "case-folder", "QDOS31001"),
            new IntakeSourceCustodyReference(
                Guid.NewGuid(), "instruction.eml", "message/rfc822", Sha256(bytes), "source-key", bytes.Length),
            "retain-operation",
            default));

        Assert.DoesNotContain(handler.Uris, uri =>
            uri.AbsolutePath.StartsWith("/2.0/files/", StringComparison.Ordinal));
    }

    /// <summary>
    /// Over Box's 50 MB direct-upload limit a file goes up in parts through an
    /// upload session: one create, each part with its own range and SHA-1
    /// digest, one commit carrying the whole file's, and no direct upload.
    /// Every path that files bytes takes it.
    /// </summary>
    [Theory]
    [InlineData("custody-memory")]
    [InlineData("store-memory")]
    [InlineData("store-verified-stream")]
    public async Task AFileOverFiftyMegabytesIsFiledInParts(string route)
    {
        var bytes = OverTheSinglePartLimit();
        var box = new StatefulBox();
        var caseId = Guid.NewGuid();
        var custody = new BoxCaseCustody(new MemoryArtifactStore(bytes), CreateClient(box));
        var root = await custody.CreateCaseRootAsync(
            caseId, "QDOS31001", "0123456789ABCDEFGHJKMNPQRS", "case-root", default);
        var documents = new BoxDocumentContentStore(CreateClient(box));
        var address = Address(caseId, root.RemoteId, 2, 1, "large.pdf");
        using var stream = new MemoryStream(bytes, writable: false);
        var mutations = box.MutationCount;
        var before = box.Requests.Count;

        var path = "QDOS31001/002 large.pdf";
        switch (route)
        {
            case "custody-memory":
                await custody.RetainAcceptedIntakeAttachmentAsync(
                    root,
                    new IntakeSourceCustodyReference(
                        Guid.NewGuid(), "large.pdf", "application/pdf", Sha256(bytes), "source", bytes.Length),
                    3,
                    "attachment-retain",
                    default);
                path = "QDOS31001/003 large.pdf";
                break;
            case "store-memory":
                Assert.Equal(
                    DocumentContentWriteDisposition.Created,
                    (await documents.StoreVersionAsync(address, bytes, Sha256(bytes), default)).Disposition);
                break;
            default:
                Assert.Equal(
                    DocumentContentWriteDisposition.Created,
                    (await documents.StoreVerifiedStreamAsync(
                        address, stream, bytes.Length, Sha256(bytes), default)).Disposition);
                Assert.Equal(0L, stream.Position);
                break;
        }

        var requests = box.Requests.Skip(before).ToArray();
        var partCount = (int)((bytes.Length + box.PartSize - 1) / box.PartSize);
        Assert.DoesNotContain(requests, IsUpload);
        Assert.Equal(1, requests.Count(request => request == "POST /api/2.0/files/upload_sessions"));
        Assert.Equal(partCount, requests.Count(IsPartUpload));
        Assert.Equal(1, requests.Count(IsCommit));
        Assert.DoesNotContain(requests, IsSessionAbort);
        Assert.Equal(partCount, box.Parts.Count);
        for (var index = 0; index < partCount; index++)
        {
            var offset = index * box.PartSize;
            var length = (int)Math.Min(box.PartSize, bytes.Length - offset);
            Assert.Equal($"bytes {offset}-{offset + length - 1}/{bytes.Length}", box.Parts[index].ContentRange);
            Assert.Equal(Sha1Digest(bytes.AsSpan((int)offset, length)), box.Parts[index].Digest);
        }
        Assert.True(box.Parts[^1].Length < box.PartSize);
        Assert.Equal(Sha1Digest(bytes), Assert.Single(box.CommitDigests));
        Assert.Equal(Sha256(bytes), Sha256(box.ContentAt(path)));
        Assert.Equal(mutations + 1, box.MutationCount);
    }

    /// <summary>
    /// Up to and including the limit, a file is still one direct upload, from
    /// memory or from a stream.
    /// </summary>
    [Theory]
    [InlineData("ten-megabytes", 10_000_000)]
    [InlineData("at-limit", 50_000_000)]
    public async Task AFileAtTheSinglePartLimitStillUsesOneRequest(string size, int length)
    {
        var (box, client, folder) = await PartsEstateAsync();
        var bytes = new byte[length];
        using var stream = new MemoryStream(bytes, writable: false);
        var before = box.Requests.Count;

        var fromMemory = await client.UploadAsync(
            folder, $"{size} memory.pdf", bytes, "application/pdf", default);
        var fromStream = await client.UploadVerifiedAsync(
            folder, $"{size} stream.pdf", stream, bytes.Length, "application/pdf", Sha256(bytes), default);

        var requests = box.Requests.Skip(before).ToArray();
        Assert.Equal(2, requests.Length);
        Assert.All(requests, request => Assert.True(IsUpload(request), request));
        Assert.True(fromMemory.Created);
        Assert.True(fromStream.Created);
    }

    /// <summary>
    /// A name Box already holds can meet an upload in parts at the session's
    /// create or at its commit. Either way it is resolved as for a direct
    /// upload: the same bytes are the file already filed, and different bytes
    /// are refused. A session that was opened is aborted.
    /// </summary>
    [Theory]
    [InlineData("at-create")]
    [InlineData("at-commit")]
    public async Task ANameBoxAlreadyHoldsIsResolvedFromAPartsUpload(string stage)
    {
        var bytes = OverTheSinglePartLimit();
        var different = bytes.ToArray();
        different[^1] ^= 0xFF;
        var (box, client, folder) = await PartsEstateAsync();
        using var stream = new MemoryStream(bytes, writable: false);
        var mutations = box.MutationCount;
        var before = box.Requests.Count;

        box.OccupyNameBefore = (stage, bytes);
        var fromMemory = await client.UploadAsync(folder, "memory.pdf", bytes, "application/pdf", default);
        box.OccupyNameBefore = (stage, bytes);
        var fromStream = await client.UploadVerifiedAsync(
            folder, "stream.pdf", stream, bytes.Length, "application/pdf", Sha256(bytes), default);
        box.OccupyNameBefore = (stage, different);
        await Assert.ThrowsAsync<InvalidDataException>(() => client.UploadAsync(
            folder, "different memory.pdf", bytes, "application/pdf", default));
        box.OccupyNameBefore = (stage, different);
        await Assert.ThrowsAsync<InvalidDataException>(() => client.UploadVerifiedAsync(
            folder, "different stream.pdf", stream, bytes.Length, "application/pdf", Sha256(bytes), default));

        Assert.False(fromMemory.Created);
        Assert.False(fromStream.Created);
        Assert.Equal(mutations, box.MutationCount);
        Assert.Equal(
            stage == "at-commit" ? 4 : 0,
            box.Requests.Skip(before).Count(IsSessionAbort));
    }

    /// <summary>
    /// Box answers 202 to a commit while it is still processing the parts. The
    /// commit is sent again after Box's <c>Retry-After</c>, and a commit Box
    /// has still not finished after the attempts allowed is a failure.
    /// </summary>
    [Fact]
    public async Task ACommitBoxHasNotFinishedIsSentAgain()
    {
        var bytes = OverTheSinglePartLimit();
        var (box, client, folder) = await PartsEstateAsync();

        box.CommitStillProcessingAnswers = 1;
        var before = box.Requests.Count;
        var upload = await client.UploadAsync(folder, "processed.pdf", bytes, "application/pdf", default);
        Assert.True(upload.Created);
        Assert.Equal(2, box.Requests.Skip(before).Count(IsCommit));

        box.CommitStillProcessingAnswers = int.MaxValue;
        var mutations = box.MutationCount;
        before = box.Requests.Count;
        var failure = await Assert.ThrowsAsync<HttpRequestException>(() => client.UploadAsync(
            folder, "unprocessed.pdf", bytes, "application/pdf", default));
        var requests = box.Requests.Skip(before).ToArray();
        Assert.Equal(HttpStatusCode.Accepted, failure.StatusCode);
        Assert.Equal(BoxDocumentContentStore.MaximumAttempts, requests.Count(IsCommit));
        Assert.Equal(1, requests.Count(IsSessionAbort));
        Assert.Equal(mutations, box.MutationCount);
    }

    /// <summary>
    /// A part Box refuses ends the upload with Box's own failure, the one a
    /// direct upload would raise: nothing is committed and the session is
    /// aborted. An abort that itself fails is logged and changes nothing.
    /// </summary>
    [Theory]
    [InlineData("unavailable", HttpStatusCode.ServiceUnavailable, false, typeof(HttpRequestException))]
    [InlineData("throttled", HttpStatusCode.TooManyRequests, false, typeof(BoxThrottledException))]
    [InlineData("abort-refused", HttpStatusCode.ServiceUnavailable, true, typeof(HttpRequestException))]
    public async Task AFailedPartFilesNothingAndRaisesBoxsFailure(
        string failure,
        HttpStatusCode status,
        bool abortFails,
        Type raised)
    {
        var bytes = OverTheSinglePartLimit();
        var logger = new RecordingLogger();
        var (box, client, folder) = await PartsEstateAsync(logger);
        box.FailPart = (4, status);
        box.FailSessionAbort = abortFails;
        var mutations = box.MutationCount;
        var before = box.Requests.Count;

        var exception = await Assert.ThrowsAsync(raised, () => client.UploadAsync(
            folder, $"{failure}.pdf", bytes, "application/pdf", default));

        var requests = box.Requests.Skip(before).ToArray();
        Assert.Equal(status, Assert.IsAssignableFrom<HttpRequestException>(exception).StatusCode);
        Assert.Equal(4, requests.Count(IsPartUpload));
        Assert.DoesNotContain(requests, IsCommit);
        Assert.Equal(1, requests.Count(IsSessionAbort));
        Assert.Equal(mutations, box.MutationCount);
        Assert.Equal(abortFails ? 1 : 0, logger.Levels.Count(level => level == LogLevel.Warning));
    }

    /// <summary>
    /// A committed file is held to the same fence as a direct upload's: Box's
    /// answer must name the proved folder as its parent. The session was
    /// committed, so it is not aborted.
    /// </summary>
    [Fact]
    public async Task ACommitAnswerOutsideTheProvedFolderIsRefused()
    {
        var bytes = OverTheSinglePartLimit();
        var (box, client, folder) = await PartsEstateAsync();
        box.CommitAnswerParent = "another-folder";
        var before = box.Requests.Count;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => client.UploadAsync(
            folder, "outside.pdf", bytes, "application/pdf", default));

        var requests = box.Requests.Skip(before).ToArray();
        Assert.Equal(1, requests.Count(IsCommit));
        Assert.DoesNotContain(requests, IsSessionAbort);
    }

    /// <summary>
    /// A stream that ends before its declared length fails as the direct
    /// upload's bounded read does: no commit, the session aborted, and the
    /// stream left where it started.
    /// </summary>
    [Fact]
    public async Task ASourceThatEndsBeforeItsDeclaredLengthSendsNoCommit()
    {
        var bytes = RandomNumberGenerator.GetBytes(20_000_000);
        var (box, client, folder) = await PartsEstateAsync();
        using var stream = new MemoryStream(bytes, writable: false);
        var before = box.Requests.Count;

        await Assert.ThrowsAsync<EndOfStreamException>(() => client.UploadVerifiedAsync(
            folder, "short.pdf", stream, 60_000_000, "application/pdf", Sha256(bytes), default));

        var requests = box.Requests.Skip(before).ToArray();
        Assert.Equal(2, requests.Count(IsPartUpload));
        Assert.DoesNotContain(requests, IsCommit);
        Assert.Equal(1, requests.Count(IsSessionAbort));
        Assert.Equal(0L, stream.Position);
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

    /// <summary>
    /// The production shape. An audit root named from the audit
    /// reference, the source, then the eight attachments QDOS26010 actually
    /// carried. Both production audits failed custody with an unclassified
    /// exception after their files had reached Box, and nothing exercises
    /// BoxCaseCustody at that shape.
    /// </summary>
    [Fact]
    public async Task AnAuditRootRetainsTheSourceAndEveryAttachment()
    {
        var box = new StatefulBox();
        var bytes = Encoding.UTF8.GetBytes("accepted source");
        var custody = new BoxCaseCustody(new MemoryArtifactStore(bytes), CreateClient(box));
        var caseId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();

        // An audit's Box root was named from the audit reference before audits kept one identity.
        var root = await custody.CreateCaseRootAsync(
            caseId, "a.QDOS26010", "0123456789ABCDEFGHJKMNPQRS", "case-create", default);
        await custody.RetainAcceptedIntakeSourceAsync(
            root,
            new IntakeSourceCustodyReference(
                receiptId, "instruction.eml", "message/rfc822", Sha256(bytes), "source", bytes.Length),
            "source-retain",
            default);

        var names = new[]
        {
            "1_Bodyshopreport295952-V1.pdf", "1_Mileage-V1.jpg", "11_Vin-V1.jpg",
            "2_CLVDriversideandfrontreg-V1.jpg", "3_CLVDamage1-V1.jpg",
            "34939_1_LtrtoAuditEngin.pdf", "4_CLVDamageredpaintandrearreg-V1.jpg",
            "CLVDamage2-V1.jpg"
        };
        for (var index = 0; index < names.Length; index++)
        {
            await custody.RetainAcceptedIntakeAttachmentAsync(
                root,
                new IntakeSourceCustodyReference(
                    receiptId, names[index], "application/pdf", Sha256(bytes), "source", bytes.Length),
                index + 2,
                $"attachment-retain-{index}",
                default);
        }
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
        var source = new IntakeSourceCustodyReference(
            Guid.NewGuid(),
            "instruction.eml",
            "message/rfc822",
            Sha256(sourceBytes),
            "source",
            sourceBytes.Length);
        await custody.RetainAcceptedIntakeSourceAsync(root, source, "source-retain", default);
        var attachment = new IntakeSourceCustodyReference(
            source.IntakeReceiptId,
            "estimate.pdf",
            "application/pdf",
            Sha256(sourceBytes),
            "source",
            sourceBytes.Length);
        await custody.RetainAcceptedIntakeAttachmentAsync(
            root, attachment, 2, "attachment-retain", default);
        await custody.CreateAuditReferenceFolderAsync(
            root, "AUD31001", "123456789ABCDEFGHJKMNPQRS0", "audit-create", default);

        var documents = new BoxDocumentContentStore(CreateClient(box));
        var sameName = "damage photo.jpg";
        var first = Encoding.UTF8.GetBytes("first image revision");
        var second = Encoding.UTF8.GetBytes("second image revision");
        var other = Encoding.UTF8.GetBytes("second occurrence");
        await documents.StoreVersionAsync(Address(caseId, root.RemoteId, 2, 1, sameName), first, Sha256(first), default);
        await documents.StoreVersionAsync(Address(caseId, root.RemoteId, 2, 2, sameName), second, Sha256(second), default);
        await documents.StoreVersionAsync(Address(caseId, root.RemoteId, 3, 1, sameName), other, Sha256(other), default);

        Assert.False(box.PathExists("QDOS31001/pegasus-case-binding.json"));
        Assert.True(box.PathExists("QDOS31001/001 instruction.eml"));
        Assert.True(box.PathExists("QDOS31001/002 estimate.pdf"));
        Assert.True(box.PathExists("QDOS31001/002 damage photo.jpg"));
        Assert.True(box.PathExists("QDOS31001/002 damage photo (revision 002).jpg"));
        Assert.True(box.PathExists("QDOS31001/003 damage photo.jpg"));
        Assert.False(box.PathExists("QDOS31001/AUD31001/pegasus-audit-binding.json"));
        Assert.True(box.PathExists("QDOS31001/AUD31001"));
        Assert.Equal(2, box.RenameCount);
        Assert.Equal(0, box.DeleteCount);
        Assert.DoesNotContain(box.FinalPathSegments, segment =>
            segment.StartsWith(".pegasus-create-", StringComparison.Ordinal)
            || Guid.TryParse(segment, out _)
            || (segment.Length == 64 && segment.All(char.IsAsciiHexDigit)));

        box.SetMediaType(
            "QDOS31001/001 instruction.eml",
            "application/octet-stream");
        await Assert.ThrowsAsync<InvalidDataException>(() => custody.RetainAcceptedIntakeSourceAsync(
            root, source, "source-wrong-media", default));
        box.SetMediaType(
            "QDOS31001/001 instruction.eml",
            "message/rfc822");
        var lostSourceResponse = new StatefulBox();
        var lostResponseCustody = new BoxCaseCustody(
            new MemoryArtifactStore(sourceBytes), CreateClient(lostSourceResponse));
        var lostResponseRoot = await lostResponseCustody.CreateCaseRootAsync(
            caseId, "QDOS31001", "23456789ABCDEFGHJKMNPQRS01", "lost-source-root", default);
        lostSourceResponse.LoseNextFileUploadResponseForName = "001 instruction.eml";
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            lostResponseCustody.RetainAcceptedIntakeSourceAsync(
                lostResponseRoot, source, "lost-source", default));
        var reconciledSource = await lostResponseCustody.RetainAcceptedIntakeSourceAsync(
            lostResponseRoot, source, "lost-source", default);
        Assert.NotEmpty(reconciledSource.RemoteId);
    }

    [Fact]
    public async Task WrongTypeAndAncestryFailClosedWithoutMutation()
    {
        var caseId = Guid.Parse("10213243-5465-7687-98a9-bacbdcedfe0f");

        var wrongType = new StatefulBox();
        wrongType.SeedFileAtRoot("QDOS31001", []);
        await Assert.ThrowsAsync<InvalidDataException>(() => new BoxCaseCustody(
            new EmptyArtifactStore(), CreateClient(wrongType)).CreateCaseRootAsync(
                caseId, "QDOS31001", "0123456789ABCDEFGHJKMNPQRS", "wrong-type", default));

        var wrongAncestry = new StatefulBox();
        wrongAncestry.SeedEmptyCase("QDOS31001");
        wrongAncestry.MakeCaseMetadataOutside("QDOS31001");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new BoxCaseCustody(
            new EmptyArtifactStore(), CreateClient(wrongAncestry)).GetExistingCaseRootAsync(
                caseId, "QDOS31001", default));

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
        Assert.True(box.PathExists("QDOS31001"));
        Assert.True(box.PathExists("QDOS31001/AUD31001"));
        Assert.False(box.PathExists("QDOS31001/pegasus-case-binding.json"));
        Assert.False(box.PathExists("QDOS31001/AUD31001/pegasus-audit-binding.json"));
        Assert.False(box.PathExists($".pegasus-create-{caseToken}"));
        Assert.False(box.PathExists($"QDOS31001/.pegasus-create-{auditToken}"));
        Assert.Equal(2, box.RenameCount);
        Assert.Equal(0, box.DeleteCount);

        // A same-name folder is the case's — the durable identity
        // lives in the database, not in a marker file.
        var preExisting = new StatefulBox();
        preExisting.SeedEmptyCase("QDOS31001");
        var adopted = await new BoxCaseCustody(
            new EmptyArtifactStore(), CreateClient(preExisting)).CreateCaseRootAsync(
                caseId, "QDOS31001", caseToken, "case-create", default);
        Assert.Equal(caseId, adopted.CaseId);
        Assert.Equal(0, preExisting.MutationCount);

        var expiredBeforeCreate = new StatefulBox();
        await Assert.ThrowsAsync<CustodyProcessingLeaseLostException>(() => new BoxCaseCustody(
            new EmptyArtifactStore(), CreateClient(expiredBeforeCreate)).CreateCaseRootAsync(
                caseId,
                "QDOS31001",
                caseToken,
                "expired-before-create",
                new CustodyEffectLeaseGuard(_ => Task.FromResult(false)),
                default));
        Assert.Equal(0, expiredBeforeCreate.MutationCount);

        var expiresAfterStaging = new StatefulBox();
        var stagingChecks = 0;
        await Assert.ThrowsAsync<CustodyProcessingLeaseLostException>(() => new BoxCaseCustody(
            new EmptyArtifactStore(), CreateClient(expiresAfterStaging)).CreateCaseRootAsync(
                caseId,
                "QDOS31001",
                caseToken,
                "expires-after-staging",
                new CustodyEffectLeaseGuard(_ =>
                    Task.FromResult(Interlocked.Increment(ref stagingChecks) == 1)),
                default));
        Assert.True(expiresAfterStaging.PathExists($".pegasus-create-{caseToken}"));
        Assert.False(expiresAfterStaging.PathExists("QDOS31001"));
        Assert.Equal(1, expiresAfterStaging.MutationCount);

        var expiresAfterPromotion = new StatefulBox();
        var promotionChecks = 0;
        var guardedCustody = new BoxCaseCustody(
            new MemoryArtifactStore("accepted source"u8.ToArray()),
            CreateClient(expiresAfterPromotion));
        // The unbound create spends two guarded effects (staging create,
        // promotion); the budget ends exactly there so the follow-on retain is
        // refused before it can create Evidence (the custody design kept this boundary).
        var promotionGuard = new CustodyEffectLeaseGuard(_ =>
            Task.FromResult(Interlocked.Increment(ref promotionChecks) <= 2));
        var promotedRoot = await guardedCustody.CreateCaseRootAsync(
            caseId,
            "QDOS31001",
            caseToken,
            "promotion-boundary",
            promotionGuard,
            default);
        var sourceBytes = "accepted source"u8.ToArray();
        await Assert.ThrowsAsync<CustodyProcessingLeaseLostException>(() =>
            guardedCustody.RetainAcceptedIntakeSourceAsync(
                promotedRoot,
                new(
                    Guid.Parse("20314253-6475-8697-a8b9-cadbecfd0e1f"),
                    "instruction.eml",
                    "message/rfc822",
                    Sha256(sourceBytes),
                    "source-key",
                    sourceBytes.Length),
                "source-boundary",
                promotionGuard,
                default));
        Assert.True(expiresAfterPromotion.PathExists("QDOS31001"));
        Assert.False(expiresAfterPromotion.PathExists("QDOS31001/Evidence"));
    }

    private static ManagedDocumentContentAddress Address(
        Guid caseId,
        string caseRootRemoteId,
        int ordinal,
        int version,
        string fileName) => new(
        caseId,
        "QDOS31001",
        caseRootRemoteId,
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

    [Fact]
    public async Task ImageCaseFolderStoresOrdinalNamedImagesIdempotently()
    {
        var box = new StatefulBox();
        var imageBytes = Encoding.UTF8.GetBytes("retained image bytes");
        var custody = new BoxCaseCustody(new MemoryArtifactStore(imageBytes), CreateClient(box));
        var imageIntakeId = Guid.Parse("20213243-5465-7687-98a9-bacbdcedfe10");
        var imageRoot = await custody.CreateCaseRootAsync(
            imageIntakeId, "AB12CDE-01", "0123456789ABCDEFGHJKMNPQRS", "image-root-create", default);
        var source = new IntakeSourceCustodyReference(
            Guid.NewGuid(),
            "photo one.jpg",
            "image/jpeg",
            Sha256(imageBytes),
            "source",
            imageBytes.Length,
            IntakeAssetId: Guid.NewGuid());

        await custody.RetainImageCaseAssetAsync(imageRoot, source, 1, "image-retain-1", default);
        Assert.False(box.PathExists("AB12CDE-01/pegasus-case-binding.json"));
        Assert.True(box.PathExists("AB12CDE-01/001 photo one.jpg"));

        // A replayed retention verifies the immutable content instead of
        // mutating anything again.
        var mutationsAfterFirst = box.MutationCount;
        await custody.RetainImageCaseAssetAsync(imageRoot, source, 1, "image-retain-1", default);
        Assert.Equal(mutationsAfterFirst, box.MutationCount);

        // Corrupted remote content fails the replay closed.
        box.CorruptFile("AB12CDE-01/001 photo one.jpg");
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            custody.RetainImageCaseAssetAsync(imageRoot, source, 1, "image-retain-1", default));
    }

    [Fact]
    public async Task MergeFoldsImageFilesIntoTheCaseEvidenceAndRemovesTheEmptiedFolder()
    {
        var box = new StatefulBox { AllowDeletes = true };
        var imageBytes = Encoding.UTF8.GetBytes("retained image bytes");
        var custody = new BoxCaseCustody(new MemoryArtifactStore(imageBytes), CreateClient(box));
        var source = new IntakeSourceCustodyReference(
            Guid.NewGuid(),
            "photo one.jpg",
            "image/jpeg",
            Sha256(imageBytes),
            "source",
            imageBytes.Length,
            IntakeAssetId: Guid.NewGuid());
        var firstImageRoot = await custody.CreateCaseRootAsync(
            Guid.Parse("20213243-5465-7687-98a9-bacbdcedfe10"),
            "AB12CDE-01", "0123456789ABCDEFGHJKMNPQRS", "first-image-root", default);
        await custody.RetainImageCaseAssetAsync(firstImageRoot, source, 1, "first-retain", default);
        var secondImageRoot = await custody.CreateCaseRootAsync(
            Guid.Parse("30213243-5465-7687-98a9-bacbdcedfe11"),
            "AB12CDE-02", "123456789ABCDEFGHJKMNPQRS0", "second-image-root", default);
        await custody.RetainImageCaseAssetAsync(secondImageRoot, source, 1, "second-retain", default);
        var caseRoot = await custody.CreateCaseRootAsync(
            Guid.Parse("40213243-5465-7687-98a9-bacbdcedfe12"),
            "QDOS31001", "23456789ABCDEFGHJKMNPQRS01", "case-root", default);

        await custody.MergeImageCaseContentsAsync(firstImageRoot, caseRoot, "first-fold", default);
        Assert.True(box.PathExists("QDOS31001/001 photo one.jpg"));
        Assert.False(box.PathExists("AB12CDE-01"));

        // A replayed fold after the folder is gone is an idempotent no-op.
        var mutationsAfterFold = box.MutationCount;
        await custody.MergeImageCaseContentsAsync(firstImageRoot, caseRoot, "first-fold", default);
        Assert.Equal(mutationsAfterFold, box.MutationCount);

        // A same-named file from a second Image intake keeps a unique name by
        // carrying its source reference.
        await custody.MergeImageCaseContentsAsync(secondImageRoot, caseRoot, "second-fold", default);
        Assert.True(box.PathExists("QDOS31001/AB12CDE-02 001 photo one.jpg"));
        Assert.False(box.PathExists("AB12CDE-02"));
    }

    /// <summary>A standalone Audit's folder is named by its own a. Case/PO, like every Case root.</summary>
    [Fact]
    public async Task MergeFoldsImageFilesIntoAStandaloneAuditRoot()
    {
        var box = new StatefulBox { AllowDeletes = true };
        var imageBytes = Encoding.UTF8.GetBytes("retained image bytes");
        var custody = new BoxCaseCustody(new MemoryArtifactStore(imageBytes), CreateClient(box));
        var source = new IntakeSourceCustodyReference(
            Guid.NewGuid(),
            "photo one.jpg",
            "image/jpeg",
            Sha256(imageBytes),
            "source",
            imageBytes.Length,
            IntakeAssetId: Guid.NewGuid());
        var imageRoot = await custody.CreateCaseRootAsync(
            Guid.Parse("20213243-5465-7687-98a9-bacbdcedfe10"),
            "AB12CDE-01", "0123456789ABCDEFGHJKMNPQRS", "image-root", default);
        await custody.RetainImageCaseAssetAsync(imageRoot, source, 1, "image-retain", default);
        var auditRoot = await custody.CreateCaseRootAsync(
            Guid.Parse("40213243-5465-7687-98a9-bacbdcedfe12"),
            "a.QDOS31001", "23456789ABCDEFGHJKMNPQRS01", "audit-root", default);

        await custody.MergeImageCaseContentsAsync(imageRoot, auditRoot, "fold", default);

        Assert.True(box.PathExists("a.QDOS31001/001 photo one.jpg"));
        Assert.False(box.PathExists("AB12CDE-01"));
    }

    [Fact]
    public async Task MergeFailsClosedOnUnexpectedContentAndTheRootCanNeverBeRemoved()
    {
        var box = new StatefulBox { AllowDeletes = true };
        var imageBytes = Encoding.UTF8.GetBytes("retained image bytes");
        var custody = new BoxCaseCustody(new MemoryArtifactStore(imageBytes), CreateClient(box));
        var imageRoot = await custody.CreateCaseRootAsync(
            Guid.Parse("20213243-5465-7687-98a9-bacbdcedfe10"),
            "AB12CDE-01", "0123456789ABCDEFGHJKMNPQRS", "image-root", default);
        var caseRoot = await custody.CreateCaseRootAsync(
            Guid.Parse("40213243-5465-7687-98a9-bacbdcedfe12"),
            "QDOS31001", "23456789ABCDEFGHJKMNPQRS01", "case-root", default);
        await CreateClient(box).CreateFolderAsync(imageRoot.RemoteId, "Nested", default);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            custody.MergeImageCaseContentsAsync(imageRoot, caseRoot, "fold", default));
        Assert.True(box.PathExists("AB12CDE-01/Nested"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CreateClient(box).DeleteFolderAsync("405543781910", default));
    }

    /// <summary>
    /// Folding an image folder into a Case reads each of the two folders a
    /// fixed number of times, however many files it moves. Each file is read
    /// before and after its move, and nothing else is asked of Box for it.
    /// </summary>
    [Fact]
    public async Task AMergeReadsItsFoldersNoMoreOftenForFourFilesThanForOne()
    {
        var one = await FoldImageFilesAsync(1);
        var four = await FoldImageFilesAsync(4);

        Assert.True(one.CaseFolderReads >= 1);
        Assert.Equal(one.ImageFolderReads, four.ImageFolderReads);
        Assert.Equal(one.CaseFolderReads, four.CaseFolderReads);
        Assert.Single(one.Files);
        Assert.Equal(4, four.Files.Length);
        Assert.All(one.Files.Concat(four.Files), file => Assert.Equal((2, 1), file));
    }

    /// <summary>
    /// A fence failure partway through a fold ends the fold's proofs: the files
    /// already moved stay moved, and the next fold into the same Case proves
    /// the Case folder again.
    /// </summary>
    [Fact]
    public async Task AFenceFailureMidMergeDropsTheProofsAndTheNextMergeProvesAgain()
    {
        var box = new StatefulBox { AllowDeletes = true };
        var bytes = Encoding.UTF8.GetBytes("retained image bytes");
        var custody = new BoxCaseCustody(new MemoryArtifactStore(bytes), CreateClient(box));
        var imageRoot = await custody.CreateCaseRootAsync(
            Guid.NewGuid(), "AB12CDE-01", "0123456789ABCDEFGHJKMNPQRS", "image-root", default);
        for (var ordinal = 1; ordinal <= 2; ordinal++)
        {
            await custody.RetainImageCaseAssetAsync(
                imageRoot, ImagePhoto(bytes, $"photo {ordinal}.jpg"), ordinal, $"retain-{ordinal}", default);
        }
        var caseRoot = await custody.CreateCaseRootAsync(
            Guid.NewGuid(), "QDOS31001", "23456789ABCDEFGHJKMNPQRS01", "case-root", default);
        box.MakeFileMetadataOutside("AB12CDE-01/002 photo 2.jpg");
        var before = box.Requests.Count;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            custody.MergeImageCaseContentsAsync(imageRoot, caseRoot, "fold", default));

        Assert.True(box.PathExists("QDOS31001/001 photo 1.jpg"));
        Assert.True(box.PathExists("AB12CDE-01/002 photo 2.jpg"));
        Assert.Equal(1, box.Requests.Skip(before).Count(request => IsCaseFolderProof(request, caseRoot.RemoteId)));

        box.RestoreFileMetadata("AB12CDE-01/002 photo 2.jpg");
        before = box.Requests.Count;
        await custody.MergeImageCaseContentsAsync(imageRoot, caseRoot, "fold", default);

        Assert.Equal(1, box.Requests.Skip(before).Count(request => IsCaseFolderProof(request, caseRoot.RemoteId)));
        Assert.True(box.PathExists("QDOS31001/002 photo 2.jpg"));
        Assert.False(box.PathExists("AB12CDE-01"));
    }

    /// <summary>
    /// Folds an image folder of <paramref name="files"/> files into a Case, in a
    /// Box and an adapter of their own, and counts what the fold asked of Box:
    /// the reads of each folder, and for each file its reads and its moves.
    /// </summary>
    private static async Task<(int ImageFolderReads, int CaseFolderReads, (int Reads, int Moves)[] Files)>
        FoldImageFilesAsync(int files)
    {
        var box = new StatefulBox { AllowDeletes = true };
        var bytes = Encoding.UTF8.GetBytes("retained image bytes");
        var custody = new BoxCaseCustody(new MemoryArtifactStore(bytes), CreateClient(box));
        var imageRoot = await custody.CreateCaseRootAsync(
            Guid.NewGuid(), "AB12CDE-01", "0123456789ABCDEFGHJKMNPQRS", "image-root", default);
        for (var ordinal = 1; ordinal <= files; ordinal++)
        {
            await custody.RetainImageCaseAssetAsync(
                imageRoot, ImagePhoto(bytes, $"photo {ordinal}.jpg"), ordinal, $"retain-{ordinal}", default);
        }
        var caseRoot = await custody.CreateCaseRootAsync(
            Guid.NewGuid(), "QDOS31001", "23456789ABCDEFGHJKMNPQRS01", "case-root", default);
        var before = box.Requests.Count;

        await custody.MergeImageCaseContentsAsync(imageRoot, caseRoot, "fold", default);

        var requests = box.Requests.Skip(before).ToArray();
        Assert.False(box.PathExists("AB12CDE-01"));
        return (
            requests.Count(request => IsFolderRead(request, imageRoot.RemoteId)),
            requests.Count(request => IsFolderRead(request, caseRoot.RemoteId)),
            Enumerable.Range(1, files).Select(ordinal =>
            {
                var id = box.IdAt($"QDOS31001/{ordinal:000} photo {ordinal}.jpg");
                return (
                    requests.Count(request => request.StartsWith($"GET /2.0/files/{id}?", StringComparison.Ordinal)),
                    requests.Count(request => request == $"PUT /2.0/files/{id}"));
            }).ToArray());
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
            "client-secret",
            "test-holding-folder"),
        new HttpClient(handler),
        new RecordingAuthorizationHeaderProvider(),
        TimeProvider.System);

    private static BoxContentClient CreateClient(StatefulBox box, ILogger<BoxContentClient>? logger = null) => new(
        BoxCustodyOptions.Create(
            "https://api.box.com/2.0/",
            "https://upload.box.com/api/2.0/",
            "405543781910",
            BoxConfigJson,
            "client-secret",
            "test-holding-folder"),
        new HttpClient(new StatefulBoxHandler(box)),
        new RecordingAuthorizationHeaderProvider(),
        TimeProvider.System,
        logger);

    /// <summary>A Case folder in a fresh Box, proved for uploads made straight through the client.</summary>
    private static async Task<(StatefulBox Box, BoxContentClient Client, BoxContentClient.ProvedFolder Folder)>
        PartsEstateAsync(ILogger<BoxContentClient>? logger = null)
    {
        var box = new StatefulBox();
        var client = CreateClient(box, logger);
        var root = await new BoxCaseCustody(new EmptyArtifactStore(), client).CreateCaseRootAsync(
            Guid.NewGuid(), "QDOS31001", "0123456789ABCDEFGHJKMNPQRS", "case-root", default);
        return (box, client, await client.ProveFolderAsync(root.RemoteId, default));
    }

    /// <summary>Synthetic content over Box's 50 MB direct-upload limit.</summary>
    private static byte[] OverTheSinglePartLimit() => RandomNumberGenerator.GetBytes(60_000_000);

    /// <summary>What Box's <c>Digest</c> header carries: <c>sha=</c> and the base64 SHA-1.</summary>
    private static string Sha1Digest(ReadOnlySpan<byte> content) =>
        $"sha={Convert.ToBase64String(Sha1(content))}";

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security",
        "CA5350:Do Not Use Weak Cryptographic Algorithms",
        Justification = "Box's upload Digest header and file sha1 are SHA-1; the test computes what Box does.")]
    private static byte[] Sha1(ReadOnlySpan<byte> content) => SHA1.HashData(content);

    private static bool IsPartUpload(string request) =>
        request.StartsWith("PUT /api/2.0/files/upload_sessions/", StringComparison.Ordinal);

    private static bool IsCommit(string request) =>
        request.StartsWith("POST /api/2.0/files/upload_sessions/", StringComparison.Ordinal)
        && request.EndsWith("/commit", StringComparison.Ordinal);

    private static bool IsSessionAbort(string request) =>
        request.StartsWith("DELETE /api/2.0/files/upload_sessions/", StringComparison.Ordinal);

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Bytes(ReadOnlyMemory<byte> body) =>
        new(HttpStatusCode.OK) { Content = new ByteArrayContent(body.ToArray()) };

    /// <summary>What Box answers when the Case folder's proof read asks for it.</summary>
    private static HttpResponseMessage CaseFolder(string name, string parentId, string? trashedAt = null) =>
        Json(JsonSerializer.Serialize(new
        {
            id = "case-folder",
            name,
            type = "folder",
            parent = new { id = parentId },
            trashed_at = trashedAt
        }));

    private static IntakeSourceCustodyReference ImagePhoto(byte[] bytes, string fileName) => new(
        Guid.NewGuid(),
        fileName,
        "image/jpeg",
        Sha256(bytes),
        "source",
        bytes.Length,
        IntakeAssetId: Guid.NewGuid());

    /// <summary>The one read that proves a Case folder: its name, type, parent and trash state.</summary>
    private static bool IsCaseFolderProof(string request, string folderId) =>
        request.StartsWith($"GET /2.0/folders/{folderId}?", StringComparison.Ordinal)
        && request.EndsWith("fields=id,name,type,parent,trashed_at", StringComparison.Ordinal);

    /// <summary>Any read of a folder's own record, whether the walk or the proof makes it; listing its items is not one.</summary>
    private static bool IsFolderRead(string request, string folderId) =>
        request.StartsWith($"GET /2.0/folders/{folderId}?", StringComparison.Ordinal);

    private static bool IsUpload(string request) =>
        request.StartsWith("POST /api/2.0/files/content", StringComparison.Ordinal);

    private sealed class EmptyArtifactStore : IIntakeArtifactStore
    {
        public Task<string> StoreAsync(string contentHash, ReadOnlyMemory<byte> content, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<StagedArtifactInventoryItem> StageAsync(Guid stagedReceiptId, string contentHash, Stream content, long contentLength, DateTimeOffset firstSeenAtUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ReadOnlyMemory<byte>?> ReadAsync(string storageKey, CancellationToken cancellationToken) => Task.FromResult<ReadOnlyMemory<byte>?>(null);
    }

    private sealed class MemoryArtifactStore(ReadOnlyMemory<byte> content) : IIntakeArtifactStore
    {
        public Task<string> StoreAsync(string contentHash, ReadOnlyMemory<byte> value, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<StagedArtifactInventoryItem> StageAsync(Guid stagedReceiptId, string contentHash, Stream value, long contentLength, DateTimeOffset firstSeenAtUtc, CancellationToken cancellationToken) =>
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

        public Task<bool> RenewIfDueAsync(CancellationToken cancellationToken) =>
            Task.FromResult(false);
    }

    /// <summary>The level of every entry the client logs.</summary>
    private sealed class RecordingLogger : ILogger<BoxContentClient>
    {
        public System.Collections.Concurrent.ConcurrentQueue<LogLevel> Levels { get; } = new();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Levels.Enqueue(logLevel);
    }

    private sealed class StatefulBox
    {
        private sealed class Node(
            string id,
            string name,
            string type,
            string? parentId,
            byte[]? content = null,
            string? mediaType = null)
        {
            public string Id { get; } = id;
            public string Name { get; set; } = name;
            public string Type { get; } = type;
            public string? ParentId { get; set; } = parentId;
            public byte[]? Content { get; set; } = content;
            public string? MediaType { get; set; } = mediaType;
            public string? MetadataParentOverride { get; set; }
        }

        private const string Root = "405543781910";
        private readonly Dictionary<string, Node> nodes = new(StringComparer.Ordinal);
        private int sequence;

        public StatefulBox() => nodes[Root] = new(Root, "pegasus", "folder", null);

        public bool LoseNextFolderCreateResponse { get; set; }
        public string? LoseNextFileUploadResponseForName { get; set; }

        /// <summary>When set, the next upload is refused with a 409 carrying this Box error code.</summary>
        public string? NextUploadConflictCode { get; set; }

        /// <summary>The part size Box names when it opens an upload session (8 MiB, as Box does for these sizes).</summary>
        public long PartSize { get; } = 8 * 1024 * 1024;

        /// <summary>When set, the part with this number (from 1) is answered with this status and not taken.</summary>
        public (int Number, HttpStatusCode Status)? FailPart { get; set; }

        /// <summary>How many commits are answered 202 with <c>Retry-After: 0</c> before Box commits.</summary>
        public int CommitStillProcessingAnswers { get; set; }

        /// <summary>
        /// When set, another writer files these bytes under the session's name
        /// just before that stage ("at-create" or "at-commit") is answered.
        /// </summary>
        public (string Stage, byte[] Bytes)? OccupyNameBefore { get; set; }

        /// <summary>When set, a commit's answer names this folder as the file's parent.</summary>
        public string? CommitAnswerParent { get; set; }

        /// <summary>When set, aborting an upload session is answered 500.</summary>
        public bool FailSessionAbort { get; set; }

        /// <summary>Each part Box has taken, in order: its <c>Content-Range</c>, its <c>Digest</c> and its length.</summary>
        public List<(string ContentRange, string Digest, int Length)> Parts { get; } = [];

        /// <summary>The <c>Digest</c> each commit carried, in order.</summary>
        public List<string> CommitDigests { get; } = [];

        private readonly Dictionary<string, UploadSession> sessions = new(StringComparer.Ordinal);

        private sealed record UploadSession(
            string ParentId,
            string Name,
            byte[] Content,
            List<(string PartId, long Offset, int Size, string Sha1)> Parts);

        public bool AllowDeletes { get; set; }
        public int RenameCount { get; private set; }
        public int MoveCount { get; private set; }
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
        public void MakeCaseMetadataOutside(string name) => MakeMetadataOutside(Find(Root, name)!);

        /// <summary>The file's metadata names a parent outside the approved root, as Box's would after an outside move.</summary>
        public void MakeFileMetadataOutside(string path) => MakeMetadataOutside(RequirePath(path));

        public void RestoreFileMetadata(string path) => RequirePath(path).MetadataParentOverride = null;

        public string IdAt(string path) => RequirePath(path).Id;

        private void MakeMetadataOutside(Node item)
        {
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

        public void CorruptFile(string path) => RequirePath(path).Content = "wrong binding"u8.ToArray();

        public byte[] ContentAt(string path) => RequirePath(path).Content!;

        public void SetMediaType(string path, string mediaType) => RequirePath(path).MediaType = mediaType;

        /// <summary>Every request Box has received, as "METHOD /path?query", in order.</summary>
        public System.Collections.Concurrent.ConcurrentQueue<string> Requests { get; } = new();

        private readonly object gate = new();

        /// <summary>One request at a time, as Box's answers to files filed together would be ordered.</summary>
        public HttpResponseMessage Handle(HttpRequestMessage request)
        {
            Requests.Enqueue($"{request.Method} {request.RequestUri!.PathAndQuery}");
            lock (gate)
            {
                return HandleCore(request);
            }
        }

        private HttpResponseMessage HandleCore(HttpRequestMessage request)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.StartsWith(UploadSessions, StringComparison.Ordinal))
            {
                return HandleUploadSession(request, path);
            }
            if (request.Method == HttpMethod.Get && path.StartsWith("/2.0/folders/", StringComparison.Ordinal)
                && path.EndsWith("/items", StringComparison.Ordinal))
            {
                var parent = path["/2.0/folders/".Length..^"/items".Length];
                var entries = nodes.Values.Where(node => node.ParentId == parent)
                    .Select(node => new
                    {
                        id = node.Id,
                        name = node.Name,
                        type = node.Type,
                        etag = "1",
                        size = node.Content?.LongLength,
                        content_type = node.MediaType,
                        file_version = node.Type == "file"
                            ? new { id = $"{node.Id}-version-1" }
                            : null,
                        parent = node.ParentId is null ? null : new { id = node.ParentId }
                    });
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
                    size = node.Content?.LongLength,
                    content_type = node.MediaType,
                    file_version = node.Type == "file"
                        ? new { id = $"{node.Id}-version-1" }
                        : null,
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
                var mediaType = multipart.First(part => part.Headers.ContentDisposition?.Name?.Trim('"') == "file")
                    .Headers.ContentType?.MediaType;
                if (NextUploadConflictCode is { } conflictCode)
                {
                    NextUploadConflictCode = null;
                    return Conflict(conflictCode);
                }
                if (Find(parent, name) is not null)
                {
                    // Box refuses a second item of one name in a folder.
                    return Conflict("item_name_in_use");
                }
                var created = Add("file", parent, name, bytes, mediaType: mediaType);
                if (string.Equals(LoseNextFileUploadResponseForName, name, StringComparison.Ordinal))
                {
                    LoseNextFileUploadResponseForName = null;
                    return new(HttpStatusCode.ServiceUnavailable)
                    {
                        Content = new StringContent("lost upload response")
                    };
                }
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
            if (request.Method == HttpMethod.Put && path.StartsWith("/2.0/files/", StringComparison.Ordinal))
            {
                var id = path["/2.0/files/".Length..];
                using var body = JsonDocument.Parse(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
                var name = body.RootElement.GetProperty("name").GetString()!;
                var parent = body.RootElement.GetProperty("parent").GetProperty("id").GetString()!;
                var conflict = Find(parent, name);
                if (conflict is not null && !string.Equals(conflict.Id, id, StringComparison.Ordinal))
                {
                    return new(HttpStatusCode.Conflict) { Content = new StringContent("conflict") };
                }
                nodes[id].Name = name;
                nodes[id].ParentId = parent;
                MoveCount++;
                MutationCount++;
                return Item(nodes[id]);
            }
            if (request.Method == HttpMethod.Delete)
            {
                DeleteCount++;
                if (!AllowDeletes)
                {
                    throw new InvalidOperationException("Folder/custody deletion was not expected.");
                }
                if (path.StartsWith("/2.0/folders/", StringComparison.Ordinal))
                {
                    var id = path["/2.0/folders/".Length..];
                    if (nodes.Values.Any(node => node.ParentId == id))
                    {
                        return new(HttpStatusCode.Conflict) { Content = new StringContent("not empty") };
                    }
                    nodes.Remove(id);
                    MutationCount++;
                    return new(HttpStatusCode.NoContent);
                }
                if (path.StartsWith("/2.0/files/", StringComparison.Ordinal))
                {
                    nodes.Remove(path["/2.0/files/".Length..]);
                    MutationCount++;
                    return new(HttpStatusCode.NoContent);
                }
                throw new InvalidOperationException($"Unexpected Box delete: {request.RequestUri}");
            }
            throw new InvalidOperationException($"Unexpected Box request: {request.Method} {request.RequestUri}");
        }

        private const string UploadSessions = "/api/2.0/files/upload_sessions";

        /// <summary>
        /// Box's upload session: create, one part per PUT, commit and abort,
        /// checked as Box checks them. The session's own endpoints name another
        /// host, and every request must reach the configured upload host, so a
        /// client that followed them would fail here.
        /// </summary>
        private HttpResponseMessage HandleUploadSession(HttpRequestMessage request, string path)
        {
            Assert.Equal("upload.box.com", request.RequestUri!.Host);
            if (request.Method == HttpMethod.Post && path == UploadSessions)
            {
                using var body = JsonDocument.Parse(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
                var parent = body.RootElement.GetProperty("folder_id").GetString()!;
                var name = body.RootElement.GetProperty("file_name").GetString()!;
                var size = body.RootElement.GetProperty("file_size").GetInt64();
                Occupy("at-create", parent, name);
                if (Find(parent, name) is not null)
                {
                    return Conflict("item_name_in_use");
                }
                var id = $"session-{++sequence}";
                sessions[id] = new(parent, name, new byte[size], []);
                var elsewhere = $"https://elsewhere.example/api/2.0/files/upload_sessions/{id}";
                return Answer(HttpStatusCode.Created, new
                {
                    id,
                    type = "upload_session",
                    part_size = PartSize,
                    total_parts = (size + PartSize - 1) / PartSize,
                    session_endpoints = new { upload_part = elsewhere, commit = $"{elsewhere}/commit", abort = elsewhere }
                });
            }
            var rest = path[(UploadSessions.Length + 1)..];
            var committing = rest.EndsWith("/commit", StringComparison.Ordinal);
            var sessionId = committing ? rest[..^"/commit".Length] : rest;
            if (request.Method == HttpMethod.Delete)
            {
                if (FailSessionAbort)
                {
                    return new(HttpStatusCode.InternalServerError) { Content = new StringContent("abort failed") };
                }
                return new(sessions.Remove(sessionId) ? HttpStatusCode.NoContent : HttpStatusCode.NotFound);
            }
            var session = sessions[sessionId];
            if (request.Method == HttpMethod.Put)
            {
                var number = session.Parts.Count + 1;
                if (FailPart is { } failure && failure.Number == number)
                {
                    FailPart = null;
                    return new(failure.Status) { Content = new StringContent("part refused") };
                }
                var content = request.Content!;
                var range = content.Headers.ContentRange!;
                var first = range.From!.Value;
                var last = range.To!.Value;
                var digest = string.Join(",", request.Headers.GetValues("Digest"));
                var bytes = content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                Assert.Equal("application/octet-stream", content.Headers.ContentType?.MediaType);
                Assert.Equal(session.Content.LongLength, range.Length);
                Assert.Equal(0L, first % PartSize);
                Assert.Equal(last - first + 1, bytes.LongLength);
                Assert.True(bytes.Length == PartSize || last == session.Content.LongLength - 1);
                if (digest != Sha1Digest(bytes))
                {
                    return new(HttpStatusCode.PreconditionFailed) { Content = new StringContent("digest mismatch") };
                }
                bytes.CopyTo(session.Content, first);
                Parts.Add((string.Join(",", content.Headers.GetValues("Content-Range")), digest, bytes.Length));
                var part = (
                    PartId: $"part-{number}",
                    Offset: first,
                    Size: bytes.Length,
                    Sha1: Convert.ToHexStringLower(Sha1(bytes)));
                session.Parts.Add(part);
                return Answer(HttpStatusCode.OK, new
                {
                    part = new { part_id = part.PartId, offset = part.Offset, size = part.Size, sha1 = part.Sha1 }
                });
            }
            if (request.Method == HttpMethod.Post && committing)
            {
                var digest = string.Join(",", request.Headers.GetValues("Digest"));
                CommitDigests.Add(digest);
                if (CommitStillProcessingAnswers > 0)
                {
                    CommitStillProcessingAnswers--;
                    return new(HttpStatusCode.Accepted)
                    {
                        Headers = { RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero) }
                    };
                }
                Occupy("at-commit", session.ParentId, session.Name);
                if (Find(session.ParentId, session.Name) is not null)
                {
                    return Conflict("item_name_in_use");
                }
                using var body = JsonDocument.Parse(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
                var listed = body.RootElement.GetProperty("parts").EnumerateArray()
                    .Select(part => (
                        part.GetProperty("part_id").GetString()!,
                        part.GetProperty("offset").GetInt64(),
                        part.GetProperty("size").GetInt32(),
                        part.GetProperty("sha1").GetString()!))
                    .ToList();
                if (!listed.SequenceEqual(session.Parts) || digest != Sha1Digest(session.Content))
                {
                    return new(HttpStatusCode.BadRequest) { Content = new StringContent("parts or digest differ") };
                }
                var created = Add("file", session.ParentId, session.Name, session.Content);
                sessions.Remove(sessionId);
                return Answer(HttpStatusCode.Created, new
                {
                    total_count = 1,
                    entries = new[]
                    {
                        new
                        {
                            id = created.Id,
                            name = created.Name,
                            type = "file",
                            etag = "1",
                            size = session.Content.LongLength,
                            sha1 = Convert.ToHexStringLower(Sha1(session.Content)),
                            file_version = new { id = $"{created.Id}-version-1" },
                            parent = new { id = CommitAnswerParent ?? created.ParentId }
                        }
                    }
                });
            }
            throw new InvalidOperationException($"Unexpected Box upload session request: {request.Method} {request.RequestUri}");
        }

        /// <summary>Files the occupant <see cref="OccupyNameBefore"/> names once its stage is reached.</summary>
        private void Occupy(string stage, string parentId, string name)
        {
            if (OccupyNameBefore is { } occupant && occupant.Stage == stage)
            {
                OccupyNameBefore = null;
                Add("file", parentId, name, occupant.Bytes, countMutation: false);
            }
        }

        private static HttpResponseMessage Answer(HttpStatusCode status, object value) => new(status)
        {
            Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json")
        };

        private Node Add(
            string type,
            string parent,
            string name,
            byte[]? content,
            bool countMutation = true,
            string? mediaType = null)
        {
            var node = new Node($"{type}-{++sequence}", name, type, parent, content, mediaType);
            nodes[node.Id] = node;
            if (countMutation) MutationCount++;
            return node;
        }
        private Node? Find(string parent, string name) =>
            nodes.Values.SingleOrDefault(node => node.ParentId == parent && node.Name == name);
        private Node RequirePath(string path)
        {
            var parent = Root;
            Node? item = null;
            foreach (var segment in path.Split('/'))
            {
                item = Find(parent, segment)
                    ?? throw new InvalidOperationException($"Missing Box path '{path}'.");
                parent = item.Id;
            }
            return item!;
        }
        private static object ItemValue(Node node) => new
        {
            id = node.Id,
            name = node.Name,
            type = node.Type,
            etag = "1",
            file_version = node.Type == "file"
                ? new { id = $"{node.Id}-version-1" }
                : null,
            parent = node.ParentId is null ? null : new { id = node.ParentId }
        };
        private static HttpResponseMessage Item(Node node) => Json(JsonSerializer.Serialize(ItemValue(node)));
        private static HttpResponseMessage Conflict(string code) => new(HttpStatusCode.Conflict)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { code }), Encoding.UTF8, "application/json")
        };
    }

    private sealed class StatefulBoxHandler(StatefulBox box) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(box.Handle(request));
    }
}
