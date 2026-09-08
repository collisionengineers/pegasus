using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Web.Authentication;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The intake source and asset boundary through the composed host: what a
/// connector is told before any byte is served, who may be told it, and that
/// the metadata it was given actually describes the bytes it then receives.
///
/// The point of the pairing is that metadata and content are one boundary. A
/// surface that let a caller learn a file's name, size and hash without the
/// right to read it would have leaked the thing the authorization exists to
/// protect.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class IntakeSourceAccessTests
{
    private static readonly byte[] Bytes = [0x25, 0x50, 0x44, 0x46, 0x2D];

    private static ActionActor Staff() => ActionActor.Staff(
        DevelopmentOfflineIdentity.AdministratorId,
        [StaffRole.Administrator]);

    [Fact]
    public async Task MetadataDescribesExactlyTheBytesTheBoundaryThenServes()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var upload = await IntakeWebDriver.UploadAndProcessAsync(
            factory, client, "instruction.xyz", "application/x-unknown", Bytes);
        var receiptId = IntakeWebDriver.ReceiptId(upload);

        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var receipt = await services.GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(receiptId, CancellationToken.None);
        var sourceAsset = Assert.Single(receipt!.AssetRecords);

        var metadata = await new GetIntakeSourceMetadata(
                services.GetRequiredService<IIntakeReceiptQueries>())
            .ExecuteAsync(new(receiptId, Staff()));

        Assert.NotNull(metadata);
        Assert.Equal(receiptId, metadata!.ReceiptId);
        Assert.Equal(receipt.Version, metadata.ReceiptVersion);
        Assert.Equal(sourceAsset.Id, metadata.AssetId);
        Assert.Equal(0, metadata.Occurrence);

        // The bytes the boundary serves match what the metadata promised,
        // through the real artifact store.
        var download = await services.GetRequiredService<IDownloadIntakeAsset>().ExecuteAsync(
            new(receiptId, sourceAsset.Id, Staff()));
        Assert.NotNull(download);
        Assert.Equal(metadata.Sha256, download!.Sha256, ignoreCase: true);
        Assert.Equal(metadata.ContentLength, download.ContentLength);
        Assert.Equal(metadata.FileName, download.FileName);
        Assert.Equal(metadata.MediaType, download.ContentType);
    }

    [Fact]
    public async Task MetadataAndContentRefuseTheSameUnauthorizedActors()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var upload = await IntakeWebDriver.UploadAndProcessAsync(
            factory, client, "instruction.xyz", "application/x-unknown", Bytes);
        var receiptId = IntakeWebDriver.ReceiptId(upload);

        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var receipt = await services.GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(receiptId, CancellationToken.None);
        var assetId = Assert.Single(receipt!.AssetRecords).Id;

        ActionActor[] forbidden =
        [
            ActionActor.SystemWorker("intake-processing"),
            ActionActor.RequestLink(Guid.NewGuid()),
            ActionActor.Provider(Guid.NewGuid())
        ];

        foreach (var actor in forbidden)
        {
            await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
                new GetIntakeSourceMetadata(services.GetRequiredService<IIntakeReceiptQueries>())
                    .ExecuteAsync(new(receiptId, actor)));
            await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
                new GetIntakeAssetMetadata(services.GetRequiredService<IIntakeReceiptQueries>())
                    .ExecuteAsync(new(receiptId, assetId, actor)));
            await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
                services.GetRequiredService<IDownloadIntakeAsset>()
                    .ExecuteAsync(new(receiptId, assetId, actor)));
            await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
                services.GetRequiredService<IDownloadIntakeSource>()
                    .ExecuteAsync(new(receiptId, actor)));
        }
    }

    /// <summary>
    /// The Automation Actor is the connector's identity, and the receipt id
    /// scopes every lookup: an asset id from elsewhere is not served under this
    /// receipt, however well-formed it is.
    /// </summary>
    [Fact]
    public async Task TheConnectorActorIsAdmittedAndTheReceiptScopesTheLookup()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var first = IntakeWebDriver.ReceiptId(await IntakeWebDriver.UploadAndProcessAsync(
            factory, client, "first.xyz", "application/x-unknown", Bytes));
        var second = IntakeWebDriver.ReceiptId(await IntakeWebDriver.UploadAndProcessAsync(
            factory, client, "second.xyz", "application/x-unknown", [0x01, 0x02, 0x03]));

        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var receipts = services.GetRequiredService<IIntakeReceiptQueries>();
        var connector = ActionActor.Automation("intake-connector");

        Assert.NotNull(await new GetIntakeSourceMetadata(receipts)
            .ExecuteAsync(new(first, connector)));

        var secondAsset = Assert.Single(
            (await receipts.GetAsync(second, CancellationToken.None))!.AssetRecords).Id;
        Assert.Null(await services.GetRequiredService<IDownloadIntakeAsset>()
            .ExecuteAsync(new(first, secondAsset, connector)));
        Assert.Null(await new GetIntakeAssetMetadata(receipts)
            .ExecuteAsync(new(first, secondAsset, connector)));
    }
}
