using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Crop and tag on a pre-Case image (v26, 13 September): the Unidentified
/// record's viewer offers Crop (Apply / Clear) and the Tag select, the one owner
/// records them against the retained image with its version and operation key,
/// and intake custody carries them onto the Case occurrence.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class PreCaseImagePreparationWebTests
{
    [Fact]
    public async Task TheViewerAppliesClearsAndTagsTheImageAndTheCaseOccurrenceInheritsIt()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var upload = await IntakeWebDriver.UploadAndProcessAsync(
            factory,
            client,
            "vehicle.png",
            "image/png",
            Convert.FromBase64String(MultiFormatFixture.TinyPngBase64),
            Guid.NewGuid().ToString("N"));
        var receiptId = IntakeWebDriver.ReceiptId(upload);
        var (itemId, assetId) = await ResolveAsync(factory, receiptId);
        var record = $"/Unidentified/{itemId:D}";

        var before = await IntakeWebDriver.GetHtmlAsync(client, record);
        Assert.Contains($"data-precase-asset=\"{assetId:D}\"", before, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-precase-version=\"0\"", before, StringComparison.Ordinal);
        Assert.Contains("data-precase-tools", before, StringComparison.Ordinal);
        Assert.Contains("data-precase-crop-apply", before, StringComparison.Ordinal);
        Assert.Contains("data-precase-crop-clear", before, StringComparison.Ordinal);
        Assert.Contains("data-precase-crop-cancel", before, StringComparison.Ordinal);
        Assert.Contains("data-precase-tag-select", before, StringComparison.Ordinal);
        Assert.DoesNotContain("data-precase-cropped", before, StringComparison.Ordinal);
        // Nothing recorded: the tile is still a rendering at tile size, named
        // by the content, "no preparation" and the renderer, and the viewer's
        // link and Open file stay the original.
        Assert.Contains("data-precase-tile=\"original\"", before, StringComparison.Ordinal);
        var assetRoute = $"/Received/{receiptId:D}/Asset/{assetId:D}";
        var thumbRoute = $"{assetRoute}?size=thumb";
        var hash = await ContentHashAsync(factory, receiptId);
        var renderer = CaseDocumentThumbnails.RendererIdentity;
        var unpreparedTile = $"{thumbRoute}&v={hash}&prep=0&renderer={renderer}";
        Assert.Contains(
            $"src=\"{unpreparedTile.Replace("&", "&amp;", StringComparison.Ordinal)}\"",
            before,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"href=\"{assetRoute}?v={hash}\"", before, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("image/jpeg", await ContentTypeAsync(client, unpreparedTile));
        // The address that names the content, "no preparation" and the
        // renderer is kept for a week under a validator that says so; a
        // different preparation or content is never kept.
        using (var keptUnprepared = await client.GetAsync(unpreparedTile))
        {
            Assert.True(keptUnprepared.Headers.CacheControl!.Private);
            Assert.Equal(TimeSpan.FromDays(7), keptUnprepared.Headers.CacheControl.MaxAge);
            Assert.Equal(
                $"\"{hash.ToLowerInvariant()}-p0-{renderer}\"",
                keptUnprepared.Headers.ETag!.Tag);
        }
        foreach (var uncached in new[]
        {
            thumbRoute,
            $"{thumbRoute}&v={hash}&prep=1&renderer={renderer}",
            $"{thumbRoute}&v={new string('0', 64)}&prep=0&renderer={renderer}",
            $"{thumbRoute}&v={hash}&prep=0&renderer=r0"
        })
        {
            using var response = await client.GetAsync(uncached);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);
            Assert.True(response.Headers.CacheControl!.NoStore);
            Assert.Null(response.Headers.ETag);
        }
        // The original is unchanged: the whole file, byte for byte.
        var original = Convert.FromBase64String(MultiFormatFixture.TinyPngBase64);
        Assert.Equal(original, await client.GetByteArrayAsync($"{assetRoute}?v={hash}"));

        var token = await IntakeWebDriver.GetAntiforgeryTokenAsync(client);
        var applyKey = Guid.NewGuid().ToString("N");
        await PostAsync(client, "Crop", token, record, new()
        {
            ["intakeAssetId"] = assetId.ToString("D"),
            ["expectedVersion"] = "0",
            ["rotation"] = "90",
            ["cropLeft"] = "0.1",
            ["cropTop"] = "0.2",
            ["cropWidth"] = "0.5",
            ["cropHeight"] = "0.6",
            ["clear"] = "false",
            ["operationKey"] = applyKey
        });
        // A resubmitted Apply is the same action, not a second one.
        await PostAsync(client, "Crop", token, record, new()
        {
            ["intakeAssetId"] = assetId.ToString("D"),
            ["expectedVersion"] = "0",
            ["rotation"] = "90",
            ["cropLeft"] = "0.1",
            ["cropTop"] = "0.2",
            ["cropWidth"] = "0.5",
            ["cropHeight"] = "0.6",
            ["clear"] = "false",
            ["operationKey"] = applyKey
        });

        var applied = await ReadAsync(factory, assetId);
        Assert.Equal(1, applied.Version);
        Assert.Equal(CaseAssetRotation.Clockwise90, applied.Rotation);
        Assert.Equal(new CaseAssetCrop(0.1m, 0.2m, 0.5m, 0.6m), applied.Crop);
        var afterApply = await IntakeWebDriver.GetHtmlAsync(client, record);
        Assert.Contains("data-precase-version=\"1\"", afterApply, StringComparison.Ordinal);
        Assert.Contains("data-precase-rotation=\"90\"", afterApply, StringComparison.Ordinal);
        Assert.Contains("data-precase-cropped", afterApply, StringComparison.Ordinal);
        // The tile draws the recorded region, as a Case tile does; the viewer's
        // link and Open file stay the original.
        Assert.Contains("data-precase-tile=\"prepared\"", afterApply, StringComparison.Ordinal);
        // The tile's address names the content, the preparation version and
        // the renderer, so a new crop is a new address.
        Assert.Contains($"src=\"{thumbRoute}&amp;v=", afterApply, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            $"&amp;prep=1&amp;renderer={CaseDocumentThumbnails.RendererIdentity}\"",
            afterApply,
            StringComparison.Ordinal);
        Assert.Equal("image/jpeg", await ContentTypeAsync(client, thumbRoute + "&prep=1"));
        // Only the address naming the current content, preparation and
        // renderer may be kept by the browser; the old preparation's may not.
        using (var kept = await client.GetAsync($"{thumbRoute}&v={hash}&prep=1&renderer={renderer}"))
        {
            Assert.Equal(TimeSpan.FromDays(7), kept.Headers.CacheControl!.MaxAge);
            Assert.NotNull(kept.Headers.ETag);
        }
        using (var stale = await client.GetAsync($"{thumbRoute}&v={hash}&prep=0&renderer={renderer}"))
        {
            Assert.True(stale.Headers.CacheControl!.NoStore);
            Assert.Null(stale.Headers.ETag);
        }
        Assert.Equal("image/png", await ContentTypeAsync(client, assetRoute));

        // A stale version is refused and says so above the gallery.
        await PostAsync(client, "Crop", token, record, new()
        {
            ["intakeAssetId"] = assetId.ToString("D"),
            ["expectedVersion"] = "0",
            ["rotation"] = "0",
            ["clear"] = "true",
            ["operationKey"] = Guid.NewGuid().ToString("N")
        });
        var refused = await IntakeWebDriver.GetHtmlAsync(client, record);
        Assert.Contains("data-precase-error", refused, StringComparison.Ordinal);
        Assert.Contains("changed while you were working", refused, StringComparison.Ordinal);
        Assert.Equal(1, (await ReadAsync(factory, assetId)).Version);

        await PostAsync(client, "Tag", token, record, new()
        {
            ["intakeAssetId"] = assetId.ToString("D"),
            ["tagId"] = ImageTagVocabulary.OverviewId.ToString("D"),
            ["applied"] = "true",
            ["operationKey"] = Guid.NewGuid().ToString("N")
        });
        var tagged = await IntakeWebDriver.GetHtmlAsync(client, record);
        Assert.Contains("data-precase-tag-chips", tagged, StringComparison.Ordinal);
        Assert.Contains($"data-precase-tags=\"{ImageTagVocabulary.OverviewId:D}\"", tagged, StringComparison.OrdinalIgnoreCase);

        // Carried to the Case: the occurrence intake custody records for this
        // image takes the rotation, the crop and the tag.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            var occurrence = new DocumentOccurrenceEntity { Id = Guid.NewGuid() };
            await EfPreCaseImagePreparationStore.CopyToOccurrenceAsync(context, assetId, occurrence, CancellationToken.None);
            Assert.Equal(90, occurrence.RotationDegrees);
            Assert.Equal(0.1m, occurrence.CropLeft);
            Assert.Equal(0.6m, occurrence.CropHeight);
            var carriedTag = Assert.Single(context.ChangeTracker.Entries<DocumentOccurrenceTagEntity>());
            Assert.Equal(ImageTagVocabulary.OverviewId, carriedTag.Entity.TagId);
            Assert.Equal(occurrence.Id, carriedTag.Entity.OccurrenceId);
            // Overview decides how it prints; the image arrives in the report.
            Assert.True(occurrence.InReport);
        }

        await PostAsync(client, "Tag", token, record, new()
        {
            ["intakeAssetId"] = assetId.ToString("D"),
            ["tagId"] = ImageTagVocabulary.OverviewId.ToString("D"),
            ["applied"] = "false",
            ["operationKey"] = Guid.NewGuid().ToString("N")
        });
        await PostAsync(client, "Crop", token, record, new()
        {
            ["intakeAssetId"] = assetId.ToString("D"),
            ["expectedVersion"] = "1",
            ["rotation"] = "90",
            ["clear"] = "true",
            ["operationKey"] = Guid.NewGuid().ToString("N")
        });

        var cleared = await ReadAsync(factory, assetId);
        Assert.Equal(2, cleared.Version);
        Assert.False(cleared.IsPrepared);
        Assert.Empty(cleared.Tags);
        var afterClear = await IntakeWebDriver.GetHtmlAsync(client, record);
        Assert.DoesNotContain("data-precase-cropped", afterClear, StringComparison.Ordinal);
        Assert.DoesNotContain("data-precase-tag-chips", afterClear, StringComparison.Ordinal);
        Assert.Contains("data-precase-tile=\"original\"", afterClear, StringComparison.Ordinal);
        // A cleared crop draws the same whole frame as an image never
        // prepared, so its tile has the address it had before the crop, and
        // the cleared version's own address is never kept.
        Assert.Contains(
            $"src=\"{unpreparedTile.Replace("&", "&amp;", StringComparison.Ordinal)}\"",
            afterClear,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal("image/jpeg", await ContentTypeAsync(client, thumbRoute + "&prep=2"));
        using (var clearedVersion = await client.GetAsync($"{thumbRoute}&v={hash}&prep=2&renderer={renderer}"))
        {
            Assert.True(clearedVersion.Headers.CacheControl!.NoStore);
            Assert.Null(clearedVersion.Headers.ETag);
        }
        using (var whole = await client.GetAsync(unpreparedTile))
        {
            Assert.Equal(TimeSpan.FromDays(7), whole.Headers.CacheControl!.MaxAge);
            Assert.Equal($"\"{hash.ToLowerInvariant()}-p0-{renderer}\"", whole.Headers.ETag!.Tag);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            var events = await context.ActionHistory.AsNoTracking()
                .Where(item => item.AggregateType == "intake_asset" && item.AggregateId == assetId.ToString("D"))
                .Select(item => item.EventKind)
                .ToListAsync();
            Assert.Equal(2, events.Count(kind => kind == EfPreCaseImagePreparationStore.CroppedEventKind));
            Assert.Single(events, kind => kind == EfPreCaseImagePreparationStore.TaggedEventKind);
            Assert.Single(events, kind => kind == EfPreCaseImagePreparationStore.UntaggedEventKind);
        }
    }

    /// <summary>
    /// A new image is in the report (operator, 26 September 2026), and one
    /// tagged Third party or Reflection before it reached the Case arrives
    /// out of it, as tagging it on the Case would leave it.
    /// </summary>
    [Theory]
    [InlineData("00000000-0000-4000-8000-0000000017a3")]
    [InlineData("00000000-0000-4000-8000-0000000017a4")]
    public async Task AnImageTaggedThirdPartyOrReflectionBeforeTheCaseArrivesOutOfTheReport(string tag)
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var upload = await IntakeWebDriver.UploadAndProcessAsync(
            factory,
            client,
            "vehicle.png",
            "image/png",
            Convert.FromBase64String(MultiFormatFixture.TinyPngBase64),
            Guid.NewGuid().ToString("N"));
        var (itemId, assetId) = await ResolveAsync(factory, IntakeWebDriver.ReceiptId(upload));

        // Nothing recorded on it yet: it arrives in the report.
        Assert.True((await CopiedToANewOccurrenceAsync(factory, assetId)).InReport);

        var token = await IntakeWebDriver.GetAntiforgeryTokenAsync(client);
        await PostAsync(client, "Tag", token, $"/Unidentified/{itemId:D}", new()
        {
            ["intakeAssetId"] = assetId.ToString("D"),
            ["tagId"] = tag,
            ["applied"] = "true",
            ["operationKey"] = Guid.NewGuid().ToString("N")
        });

        Assert.False((await CopiedToANewOccurrenceAsync(factory, assetId)).InReport);
    }

    /// <summary>The occurrence intake custody would record for this image, not saved.</summary>
    private static async Task<DocumentOccurrenceEntity> CopiedToANewOccurrenceAsync(
        IntakeWebApplicationFactory factory, Guid assetId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var occurrence = new DocumentOccurrenceEntity { Id = Guid.NewGuid() };
        await EfPreCaseImagePreparationStore.CopyToOccurrenceAsync(context, assetId, occurrence, CancellationToken.None);
        return occurrence;
    }

    private static async Task PostAsync(
        HttpClient client,
        string handler,
        string token,
        string returnUrl,
        Dictionary<string, string> fields)
    {
        fields["__RequestVerificationToken"] = token;
        fields["returnUrl"] = returnUrl;
        using var response = await client.PostAsync(
            $"/PreCaseImages?handler={handler}",
            new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(returnUrl, response.Headers.Location?.OriginalString);
    }

    private static async Task<string?> ContentTypeAsync(HttpClient client, string route)
    {
        using var response = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return response.Content.Headers.ContentType?.MediaType;
    }

    private static async Task<string> ContentHashAsync(IntakeWebApplicationFactory factory, Guid receiptId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var receipt = await scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(receiptId, CancellationToken.None);
        return Assert.IsType<IntakeAssetRecord>(
            IntakeFileIdentity.SourceAsset(Assert.IsType<IntakeReceipt>(receipt))).ContentHash;
    }

    private static async Task<(Guid ItemId, Guid AssetId)> ResolveAsync(IntakeWebApplicationFactory factory, Guid receiptId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var receipt = await scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(receiptId, CancellationToken.None);
        var asset = IntakeFileIdentity.SourceAsset(Assert.IsType<IntakeReceipt>(receipt));
        var item = await scope.ServiceProvider.GetRequiredService<IUnidentifiedStore>()
            .GetByOriginAsync(UnidentifiedOrigin.Receipt(receiptId), CancellationToken.None);
        return (Assert.IsType<UnidentifiedItem>(item).Id, Assert.IsType<IntakeAssetRecord>(asset).Id);
    }

    private static async Task<PreCaseImagePreparation> ReadAsync(IntakeWebApplicationFactory factory, Guid assetId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var read = await scope.ServiceProvider.GetRequiredService<IGetPreCaseImagePreparations>()
            .ExecuteAsync(ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]), [assetId], CancellationToken.None);
        return read.GetValueOrDefault(assetId) ?? PreCaseImagePreparation.Original(assetId);
    }
}
