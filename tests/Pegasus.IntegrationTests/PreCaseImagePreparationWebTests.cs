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
