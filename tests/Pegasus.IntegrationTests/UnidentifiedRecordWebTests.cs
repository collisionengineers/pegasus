using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.Unidentified;

namespace Pegasus.IntegrationTests;

/// <summary>
/// v26 Unidentified record (received file D2, D5): the record hosts what the
/// removed received-file page did. Close with reason moves the item under the
/// Cases list's Closed filter with its reason; Reopen brings it back; the page
/// announces itself to the working set and links to no receipt page.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class UnidentifiedRecordWebTests
{
    [Fact]
    public async Task TheRecordAnnouncesItselfAndLinksToNoReceiptPage()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var (receiptId, itemId, reference) = await SeedOpenItemAsync(factory, UnidentifiedReasonCode.UnreadableOrCorruptContent);

        var html = await IntakeWebDriver.GetHtmlAsync(client, $"/Unidentified/{itemId:D}");

        Assert.Contains("data-record-kind=\"unidentified\"", html, StringComparison.Ordinal);
        Assert.Contains($"data-record-ref=\"{reference}\"", html, StringComparison.Ordinal);
        Assert.Contains("data-unidentified-state=\"open\"", html, StringComparison.Ordinal);
        Assert.Contains("data-unidentified-action=\"close\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain($"href=\"/Received/{receiptId:D}/Source\"", html, StringComparison.Ordinal);
        Assert.Contains("data-unidentified-source-unavailable", html, StringComparison.Ordinal);
        Assert.DoesNotContain("View file", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CloseWithReasonListsTheItemUnderClosedAndReopenBringsItBack()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var (_, itemId, reference) = await SeedOpenItemAsync(factory, UnidentifiedReasonCode.UnreadableOrCorruptContent);
        const string reason = "The claim numbers do not match; PCH asked to reissue the instruction";

        using var closed = await PostAsync(client, $"/Unidentified/{itemId:D}?handler=Close", new()
        {
            ["expectedVersion"] = (await VersionAsync(factory, itemId)).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["operationKey"] = Guid.NewGuid().ToString("N"),
            ["reason"] = reason
        });
        Assert.Equal(HttpStatusCode.Redirect, closed.StatusCode);

        var record = await IntakeWebDriver.GetHtmlAsync(client, $"/Unidentified/{itemId:D}");
        Assert.Contains("data-unidentified-state=\"closed\"", record, StringComparison.Ordinal);
        Assert.Contains("data-unidentified-action=\"reopen\"", record, StringComparison.Ordinal);
        Assert.Contains(reason, WebUtility.HtmlDecode(record), StringComparison.Ordinal);
        Assert.DoesNotContain("data-unidentified-action=\"close\"", record, StringComparison.Ordinal);

        var open = await IntakeWebDriver.GetHtmlAsync(client, "/Cases?tab=unidentified");
        Assert.DoesNotContain($">{reference}</a>", open, StringComparison.Ordinal);
        var closedList = await IntakeWebDriver.GetHtmlAsync(client, "/Cases?tab=unidentified&show=closed");
        Assert.Contains($">{reference}</a>", closedList, StringComparison.Ordinal);
        Assert.Contains($"Closed · {reason}", WebUtility.HtmlDecode(closedList), StringComparison.Ordinal);

        using var reopened = await PostAsync(client, $"/Unidentified/{itemId:D}?handler=Reopen", new()
        {
            ["expectedVersion"] = (await VersionAsync(factory, itemId)).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["operationKey"] = Guid.NewGuid().ToString("N"),
            ["reason"] = "PCH reissued the instruction under the same claim."
        });
        Assert.Equal(HttpStatusCode.Redirect, reopened.StatusCode);

        var back = await IntakeWebDriver.GetHtmlAsync(client, "/Cases?tab=unidentified");
        Assert.Contains($">{reference}</a>", back, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACloseWithoutAReasonIsRefusedAndTheItemStaysOpen()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var (_, itemId, _) = await SeedOpenItemAsync(factory, UnidentifiedReasonCode.UnreadableOrCorruptContent);

        using var refused = await PostAsync(client, $"/Unidentified/{itemId:D}?handler=Close", new()
        {
            ["expectedVersion"] = (await VersionAsync(factory, itemId)).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["operationKey"] = Guid.NewGuid().ToString("N"),
            ["reason"] = " "
        });

        Assert.Equal(HttpStatusCode.Redirect, refused.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var item = await scope.ServiceProvider.GetRequiredService<IUnidentifiedStore>().GetAsync(itemId, CancellationToken.None);
        Assert.Equal(UnidentifiedState.Open, item!.State);
    }

    [Fact]
    public async Task ACouldNotBeReadItemSaysSo()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var (_, itemId, _) = await SeedOpenItemAsync(factory, UnidentifiedReasonCode.CouldNotBeRead);

        var html = await IntakeWebDriver.GetHtmlAsync(client, $"/Unidentified/{itemId:D}");

        Assert.Contains("data-unidentified-unreadable", html, StringComparison.Ordinal);
        Assert.Contains("Could not be read", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APdfWithSelectedPhotographsShowsOnlyThosePhotographsAndTheirCustodyState()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        const string sourceHash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
        var source = new IntakeAssetRecord(
            Guid.NewGuid(), "uploaded source", "vehicle-images.pdf", "application/pdf",
            IntakeAssetKind.Source, IntakeAssetDisposition.Source, 2048, sourceHash,
            "test-source-key", null, null, null, null);
        var banner = new IntakeAssetRecord(
            Guid.NewGuid(), "vehicle-images.pdf, page 1, image 1", "letterhead.png", "image/png",
            IntakeAssetKind.EmbeddedImage, IntakeAssetDisposition.Embedded, 110_783,
            "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB", "test-banner-key",
            1, null, 1990, 437);
        var firstPhoto = new IntakeAssetRecord(
            Guid.NewGuid(), "vehicle-images.pdf, page 1, image 2", "damage-one.jpg", "image/jpeg",
            IntakeAssetKind.EmbeddedImage, IntakeAssetDisposition.Embedded, 121_652,
            "CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC", "test-photo-one-key",
            1, null, 547, 650, IncomingArtifactCustodyState.Confirmed);
        var secondPhoto = new IntakeAssetRecord(
            Guid.NewGuid(), "vehicle-images.pdf, page 1, image 3", "damage-two.jpg", "image/jpeg",
            IntakeAssetKind.EmbeddedImage, IntakeAssetDisposition.Embedded, 125_101,
            "DDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDD", "test-photo-two-key",
            1, null, 552, 650, IncomingArtifactCustodyState.Confirmed);
        var (receiptId, itemId, _) = await SeedOpenItemAsync(
            factory,
            UnidentifiedReasonCode.NoUsableIdentification,
            [source, banner, firstPhoto, secondPhoto]);

        var html = await IntakeWebDriver.GetHtmlAsync(client, $"/Unidentified/{itemId:D}");

        Assert.Contains("2 photographs extracted from the retained file.", html, StringComparison.Ordinal);
        Assert.Contains($"/Received/{receiptId:D}/Asset/{firstPhoto.Id:D}", html, StringComparison.Ordinal);
        Assert.Contains($"/Received/{receiptId:D}/Asset/{secondPhoto.Id:D}", html, StringComparison.Ordinal);
        Assert.DoesNotContain(banner.FileName, html, StringComparison.Ordinal);
        Assert.Contains("Storage status unknown", html, StringComparison.Ordinal);
        Assert.Contains("Original file", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AGroupOfProcessedPdfReceiptsShowsEachOriginalAndSelectedPhotograph()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var (itemId, first, second) = await SeedGroupedPdfItemAsync(factory);

        var html = await IntakeWebDriver.GetHtmlAsync(client, $"/Unidentified/{itemId:D}");

        Assert.Contains(first.Source.FileName, html, StringComparison.Ordinal);
        Assert.Contains(second.Source.FileName, html, StringComparison.Ordinal);
        Assert.Contains($"/Received/{first.ReceiptId:D}/Asset/{first.Photo.Id:D}", html, StringComparison.Ordinal);
        Assert.Contains($"/Received/{second.ReceiptId:D}/Asset/{second.Photo.Id:D}", html, StringComparison.Ordinal);
        Assert.Contains("2 photographs extracted from the retained files.", html, StringComparison.Ordinal);
    }

    private static async Task<(Guid ReceiptId, Guid ItemId, string Reference)> SeedOpenItemAsync(
        IntakeWebApplicationFactory factory,
        UnidentifiedReasonCode reasonCode,
        IReadOnlyList<IntakeAssetRecord>? assets = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var receivedAt = services.GetRequiredService<TimeProvider>().GetUtcNow();
        const string sourceHash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
        assets ??=
        [
            new(
                Guid.NewGuid(), "uploaded source", "unreadable-document.pdf", "application/pdf",
                IntakeAssetKind.Source, IntakeAssetDisposition.Source, 2048, sourceHash,
                "test-source-key", null, null, null, null)
        ];
        var receipt = await services.GetRequiredService<IIntakeReceiptStore>().StoreAsync(
            new IntakeReceiptDraft(
                "unreadable-document.pdf",
                "application/pdf",
                2048,
                sourceHash,
                new IntakeSourceIdentity(IntakeSourceChannel.ManualUpload, Guid.NewGuid().ToString("N")),
                receivedAt,
                receivedAt,
                "test-actor",
                IntakeDecision.NeedsSorting,
                "test decision reason",
                [],
                [],
                null,
                [],
                null,
                null,
                "test-reader",
                "1",
                null,
                null,
                Assets: assets),
            CancellationToken.None);
        var confirmedAssetIds = assets
            .Where(asset => asset.CustodyState == IncomingArtifactCustodyState.Confirmed)
            .Select(asset => asset.Id)
            .ToArray();
        if (confirmedAssetIds.Length != 0)
        {
            // StoreAsync records receipt metadata; custody confirmation is a
            // separate persisted fact, so make the synthetic fixture honest.
            await using var context = await factory.Database.CreateContextAsync();
            await context.IntakeAssets
                .Where(asset => confirmedAssetIds.Contains(asset.Id))
                .ExecuteUpdateAsync(update => update
                    .SetProperty(asset => asset.CustodyStatus, "confirmed"));
        }
        var registered = await services.GetRequiredService<IRegisterUnidentified>().ExecuteAsync(
            new(
                UnidentifiedOrigin.Receipt(receipt.Id),
                reasonCode,
                "The document could not be read.",
                ActionActor.SystemWorker("test-worker"),
                $"unidentified-record-test:{Guid.NewGuid():N}",
                receivedAt),
            CancellationToken.None);
        return (receipt.Id, registered.Item.Id, registered.Item.Reference);
    }

    private static async Task<(Guid ItemId, (Guid ReceiptId, IntakeAssetRecord Source, IntakeAssetRecord Photo) First, (Guid ReceiptId, IntakeAssetRecord Source, IntakeAssetRecord Photo) Second)> SeedGroupedPdfItemAsync(
        IntakeWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var receivedAt = services.GetRequiredService<TimeProvider>().GetUtcNow();
        var groupId = Guid.NewGuid();
        var groupToken = Guid.NewGuid().ToString("N");
        var groupStore = services.GetRequiredService<IIntakeSubmissionGroupStore>();
        await groupStore.GetOrCreateAsync(
            groupId,
            IntakeSourceChannel.ManualUpload,
            groupToken,
            2,
            "test-actor",
            receivedAt,
            null,
            CancellationToken.None);

        var first = await StoreGroupedPdfReceiptAsync(0, "damage-report-one.pdf");
        var second = await StoreGroupedPdfReceiptAsync(1, "damage-report-two.pdf");
        var registered = await services.GetRequiredService<IRegisterUnidentified>().ExecuteAsync(
            new(
                UnidentifiedOrigin.SubmissionGroup(groupId),
                UnidentifiedReasonCode.NoUsableIdentification,
                "No readable registration was found in the photographs.",
                ActionActor.SystemWorker("test-worker"),
                $"unidentified-group-pdf-test:{Guid.NewGuid():N}",
                receivedAt),
            CancellationToken.None);
        return (registered.Item.Id, first, second);

        async Task<(Guid ReceiptId, IntakeAssetRecord Source, IntakeAssetRecord Photo)> StoreGroupedPdfReceiptAsync(
            int ordinal,
            string sourceFileName)
        {
            var sourceHash = ordinal == 0
                ? "1111111111111111111111111111111111111111111111111111111111111111"
                : "2222222222222222222222222222222222222222222222222222222222222222";
            var source = new IntakeAssetRecord(
                Guid.NewGuid(), "uploaded source", sourceFileName, "application/pdf",
                IntakeAssetKind.Source, IntakeAssetDisposition.Source, 2048, sourceHash,
                $"group-pdf-source-{ordinal}", null, null, null, null);
            var photo = new IntakeAssetRecord(
                Guid.NewGuid(), $"{sourceFileName}, page 1, image 1", $"damage-{ordinal + 1}.jpg", "image/jpeg",
                IntakeAssetKind.EmbeddedImage, IntakeAssetDisposition.Embedded, 121_652,
                ordinal == 0
                    ? "3333333333333333333333333333333333333333333333333333333333333333"
                    : "4444444444444444444444444444444444444444444444444444444444444444",
                $"group-pdf-photo-{ordinal}", 1, null, 547, 650,
                IncomingArtifactCustodyState.Confirmed);
            var sourceIdentity = new IntakeSourceIdentity(
                IntakeSourceChannel.ManualUpload,
                GroupedIntakeMemberToken.Create(groupToken, ordinal));
            var receipt = await services.GetRequiredService<IIntakeReceiptStore>().StoreAsync(
                new IntakeReceiptDraft(
                    sourceFileName,
                    "application/pdf",
                    source.ContentLength,
                    sourceHash,
                    sourceIdentity,
                    receivedAt,
                    receivedAt,
                    "test-actor",
                    IntakeDecision.NeedsSorting,
                    "test decision reason",
                    [], [], null, [], null, null, "test-reader", "1", null, null,
                    Assets: [source, photo]),
                CancellationToken.None);
            await using (var context = await factory.Database.CreateContextAsync())
            {
                await context.IntakeAssets
                    .Where(asset => asset.Id == photo.Id)
                    .ExecuteUpdateAsync(update => update
                        .SetProperty(asset => asset.CustodyStatus, "confirmed"));
            }
            var staged = new IntakeStagedReceipt(
                Guid.NewGuid(), sourceFileName, "application/pdf", source.ContentLength, sourceHash,
                sourceIdentity, receivedAt, "test-actor", $"group-pdf-staged-{ordinal}", receivedAt);
            var workStore = services.GetRequiredService<IIntakeWorkStore>();
            var received = await workStore.ReceiveAsync(
                staged,
                $"group-pdf-work:{ordinal}:{Guid.NewGuid():N}",
                CancellationToken.None);
            await groupStore.AddMemberAsync(groupId, ordinal, received, CancellationToken.None);
            // Processing claims follow the same durable dispatch transition
            // as the Worker path rather than claiming a newly received item.
            var dispatch = Assert.IsType<IntakeWorkItem>(await workStore.ClaimDispatchAsync(
                staged.Id, receivedAt, TimeSpan.FromMinutes(1), CancellationToken.None));
            await workStore.MarkDispatchedAsync(
                dispatch.Id, dispatch.LeaseToken!, receivedAt, CancellationToken.None);
            var claimed = await workStore.ClaimProcessingAsync(
                staged.Id, receivedAt, TimeSpan.FromMinutes(1), CancellationToken.None)
                ?? throw new InvalidOperationException("The test work item was not claimable.");
            await workStore.RecordEvaluationAsync(
                claimed.WorkItem.Id, claimed.WorkItem.LeaseToken!, receipt.Id, receivedAt, false, CancellationToken.None);
            await workStore.CompleteProcessingAsync(
                claimed.WorkItem.Id, claimed.WorkItem.LeaseToken!, receivedAt, CancellationToken.None);
            return (receipt.Id, source, photo);
        }
    }

    private static async Task<long> VersionAsync(IntakeWebApplicationFactory factory, Guid itemId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var item = await scope.ServiceProvider.GetRequiredService<IUnidentifiedStore>().GetAsync(itemId, CancellationToken.None);
        return item!.Version;
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, Dictionary<string, string> fields)
    {
        fields["__RequestVerificationToken"] = await IntakeWebDriver.GetAntiforgeryTokenAsync(client);
        return await client.PostAsync(path, new FormUrlEncodedContent(fields));
    }
}
