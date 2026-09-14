using System.Net;
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
        Assert.Contains($"href=\"/Received/{receiptId:D}/Source\"", html, StringComparison.Ordinal);
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

    private static async Task<(Guid ReceiptId, Guid ItemId, string Reference)> SeedOpenItemAsync(
        IntakeWebApplicationFactory factory,
        UnidentifiedReasonCode reasonCode)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var receivedAt = services.GetRequiredService<TimeProvider>().GetUtcNow();
        var receipt = await services.GetRequiredService<IIntakeReceiptStore>().StoreAsync(
            new IntakeReceiptDraft(
                "unreadable-document.pdf",
                "application/pdf",
                2048,
                Guid.NewGuid().ToString("N"),
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
                null),
            CancellationToken.None);
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
