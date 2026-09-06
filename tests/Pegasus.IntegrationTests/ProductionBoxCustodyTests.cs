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
            "client-secret",
            "test-holding-folder"));

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
        // PLAT-013: during provisioning App Service can pass the literal
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
            "/2.0/folders/case-folder" => Json("""{"id":"case-folder","parent":{"id":"405543781910"},"trashed_at":null}"""),
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
        var handler = new DelegateHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/2.0/folders/405543781910/items")
            {
                return Json($$"""{"entries":[{"id":"case-folder","name":"{{expectedCaseName}}","type":"folder","etag":"1"}]}""");
            }
            if (path == "/2.0/folders/case-folder/items")
            {
                return Json("""{"entries":[]}""");
            }
            if (path == "/api/2.0/files/content")
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                return Json("""{"entries":[{"id":"file-version","name":"retained","type":"file","etag":"version-1","file_version":{"id":"box-version-1"}}]}""");
            }
            return path switch
            {
                "/2.0/folders/case-folder" => Parent("405543781910"),
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
        Assert.Contains(
            handler.RequestBodies,
            body => body.Contains(expectedFileName, StringComparison.Ordinal));
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
    /// DOCS-008: the production shape. An audit root named from the audit
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

        // An audit's Box root was named from the audit reference before CASE-014.
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

        // DOCS-005: a same-name folder is the case's — the durable identity
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
        // refused before it can create Evidence (DOCS-005 kept this boundary).
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
            imageBytes.Length);

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
            imageBytes.Length);
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
        new RecordingAuthorizationHeaderProvider());

    private static BoxContentClient CreateClient(StatefulBox box) => new(
        BoxCustodyOptions.Create(
            "https://api.box.com/2.0/",
            "https://upload.box.com/api/2.0/",
            "405543781910",
            BoxConfigJson,
            "client-secret",
            "test-holding-folder"),
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

        public void CorruptFile(string path) => RequirePath(path).Content = "wrong binding"u8.ToArray();

        public void SetMediaType(string path, string mediaType) => RequirePath(path).MediaType = mediaType;

        public HttpResponseMessage Handle(HttpRequestMessage request)
        {
            var path = request.RequestUri!.AbsolutePath;
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
                : null
        };
        private static HttpResponseMessage Item(Node node) => Json(JsonSerializer.Serialize(ItemValue(node)));
    }

    private sealed class StatefulBoxHandler(StatefulBox box) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(box.Handle(request));
    }
}
