using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.PhotoCapture;

namespace Pegasus.IntegrationTests;

/// <summary>
/// An upload whose receipt intake staged but whose item never recorded it,
/// its request having failed in between (FRD-28): the receipt's evaluation
/// and the batch's close adopt it, counted once, so the photo is never lost
/// from its batch, and until then the lists do not count it.
/// </summary>
public sealed partial class StandingCaptureWebTests
{
    /// <summary>
    /// The phone never retried: the receipt's evaluation records it on its
    /// item, counted once, and the capture settles with it. Before that the
    /// list counts no photograph, and the settle lists nothing.
    /// </summary>
    [Fact]
    public async Task AReceiptItsItemNeverRecordedIsAdoptedByItsEvaluationAndTheCaptureSettles()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var link = await StandingCaptureDriver.CreateContactLinkAsync(factory);
        var started = await StandingCaptureDriver.StartAsync(factory, client, link, UnheldRegistration);
        var photo = await StandingCaptureDriver.SendItemAsync(
            factory, client, started, FirstOverview, MultiFormatFixture.Photograph(1), "image/jpeg");
        var staged = photo.StagedReceiptId
            ?? throw new InvalidOperationException("The item was taken without a staged intake receipt.");
        await UnrecordReceiptAsync(factory, photo);
        await StandingCaptureDriver.CompleteAsync(client, started.Token);

        Assert.Equal(0, (await RowAsync(factory, started.Capture.Id)).Photographs);
        Assert.Equal(0, (await StandingCaptureDriver.SettleAsync(factory)).Settled);

        await DrainStagedAsync(factory, staged);

        var adopted = await ItemNowAsync(factory, photo);
        Assert.Equal(staged, adopted.StagedReceiptId);
        Assert.True(adopted.IntakeReceiptId.HasValue, "The adopted item did not learn its processed receipt.");
        Assert.Equal(1, (await StandingCaptureDriver.CaptureAsync(factory, started.Capture.Id)).AcceptedItemCount);
        Assert.Equal(1, (await RowAsync(factory, started.Capture.Id)).Photographs);

        await SettleAndReconcileAsync(factory);

        var capture = await StandingCaptureDriver.CaptureAsync(factory, started.Capture.Id);
        Assert.Equal(1, capture.SettledBatches);
        var group = await GroupAsync(factory, PhotoCaptureIntakeRules.BatchGroupId(capture.Id, 1));
        Assert.Equal(staged, Assert.Single(group.Members).StagedReceiptId);
    }

    /// <summary>
    /// The receipt is not yet evaluated when the settle closes the batch, and
    /// its upload is past the in-flight grace: the close adopts it, counted
    /// once, and waits for it rather than closing without it. The phone's
    /// retry then finds the receipt it staged in the batch that holds it, and
    /// the batch closes with both photographs in its group.
    /// </summary>
    [Fact]
    public async Task AReceiptStagedButNotRecordedHoldsItsBatchAndItsRetryFindsIt()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var link = await StandingCaptureDriver.CreateContactLinkAsync(factory);
        var started = await StandingCaptureDriver.StartAsync(factory, client, link, UnheldRegistration);
        var first = await StandingCaptureDriver.SendItemAsync(
            factory, client, started, FirstOverview, MultiFormatFixture.Photograph(1), "image/jpeg");
        await StandingCaptureDriver.DrainAsync(factory, first);
        var secondPhoto = MultiFormatFixture.Photograph(2);
        var second = await StandingCaptureDriver.SendItemAsync(
            factory, client, started, SecondOverview, secondPhoto, "image/jpeg");
        var staged = second.StagedReceiptId
            ?? throw new InvalidOperationException("The item was taken without a staged intake receipt.");
        await UnrecordReceiptAsync(factory, second);
        await StandingCaptureDriver.CompleteAsync(client, started.Token);

        await StandingCaptureDriver.SettleAsync(factory);

        var held = await StandingCaptureDriver.CaptureAsync(factory, started.Capture.Id);
        Assert.Equal(0, held.SettledBatches);
        Assert.Equal(2, held.AcceptedItemCount);
        var adopted = await ItemNowAsync(factory, second);
        Assert.Equal((1, 1, staged), (adopted.Batch, adopted.Ordinal, adopted.StagedReceiptId));

        var retried = await StandingCaptureDriver.SendItemAsync(
            factory, client, started, SecondOverview, secondPhoto, "image/jpeg", second.ClientItemId);
        Assert.Equal((1, 1, staged), (retried.Batch, retried.Ordinal, retried.StagedReceiptId));
        Assert.Equal(2, (await StandingCaptureDriver.CaptureAsync(factory, started.Capture.Id)).AcceptedItemCount);

        await DrainStagedAsync(factory, staged);
        await SettleAndReconcileAsync(factory);

        var capture = await StandingCaptureDriver.CaptureAsync(factory, started.Capture.Id);
        Assert.Equal(1, capture.SettledBatches);
        var group = await GroupAsync(factory, PhotoCaptureIntakeRules.BatchGroupId(capture.Id, 1));
        Assert.Equal(
            new[] { first.StagedReceiptId!.Value, staged }.Order(),
            group.Members.Select(member => member.StagedReceiptId).Order());
    }

    /// <summary>
    /// Stands for an upload whose receipt staged and whose request then
    /// failed before the item recorded it, past the in-flight grace: the item
    /// names no staged receipt and the capture has not counted it.
    /// </summary>
    private static async Task UnrecordReceiptAsync(IntakeWebApplicationFactory factory, PhotoCaptureItem item)
    {
        var receivedAtUtc = IntakeWebApplicationFactory.FixedUtcNow
            - SettlePhotoCaptures.UploadInFlightGrace
            - TimeSpan.FromMinutes(1);
        await using var db = await (await factory.DatabaseAsync).CreateContextAsync();
        await db.PhotoCaptureItems.Where(value => value.Id == item.Id).ExecuteUpdateAsync(update => update
            .SetProperty(value => value.StagedReceiptId, (Guid?)null)
            .SetProperty(value => value.ReceivedAtUtc, receivedAtUtc));
        await db.PhotoCaptures.Where(value => value.Id == item.CaptureId).ExecuteUpdateAsync(update => update
            .SetProperty(value => value.AcceptedItemCount, value => value.AcceptedItemCount - 1)
            .SetProperty(value => value.AcceptedBytes, value => value.AcceptedBytes - item.ContentLength));
    }

    private static async Task<PhotoCaptureItem> ItemNowAsync(IntakeWebApplicationFactory factory, PhotoCaptureItem item)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IPhotoCaptureStore>()
            .FindItemAsync(item.CaptureId, item.ClientItemId, CancellationToken.None)
            ?? throw new InvalidOperationException("The item was not found.");
    }

    private static async Task<PhotoCaptureListRow> RowAsync(IntakeWebApplicationFactory factory, Guid captureId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IPhotoCaptureQueries>()
            .GetRowAsync(captureId, CancellationToken.None)
            ?? throw new InvalidOperationException("The capture's row was not found.");
    }

    private static async Task DrainStagedAsync(IntakeWebApplicationFactory factory, Guid stagedReceiptId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await IntakeWebDriver.DrainStagedAsync(scope.ServiceProvider, stagedReceiptId);
    }
}
