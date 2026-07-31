using System.Text.Json;
using Pegasus.Core.Intake;
using Microsoft.Extensions.DependencyInjection;

namespace Pegasus.IntegrationTests;

[Collection(LocalDbFixtureDefinition.Name)]
[Trait("Category", "SqlServer")]
public sealed class IntakeStablePersistenceTests
{
    [Fact]
    public async Task UnknownFormatIsRetainedAsUnsupportedWithStableCodesAndVersionOneEnvelopes()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var upload = await IntakeWebDriver.UploadAndProcessAsync(factory, client, "unknown-format.xyz",
        "application/x-unknown",
        [0x01, 0x02, 0x03]);
        var receiptId = IntakeWebDriver.ReceiptId(upload);

        await using var scope = factory.Services.CreateAsyncScope();
        var receipt = Assert.IsType<IntakeReceipt>(await scope.ServiceProvider
            .GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(receiptId, CancellationToken.None));

        Assert.Equal(IntakeDecision.Unsupported, receipt.Decision);
        Assert.Null(receipt.InstructionDraft);
        Assert.Null(receipt.ExtractionPolicyKey);
        var sourceAsset = Assert.Single(receipt.AssetRecords);
        Assert.Equal(IntakeAssetKind.Source, sourceAsset.Kind);

        Assert.Equal("unsupported", await factory.Database.ScalarAsync<string>(
            "SELECT Decision FROM IntakeReceipts"));
        Assert.Equal("manual_upload", await factory.Database.ScalarAsync<string>(
            "SELECT SourceChannel FROM IntakeReceipts"));
        Assert.Equal("source", await factory.Database.ScalarAsync<string>(
            "SELECT Kind FROM IntakeAssets"));
        Assert.Equal("source", await factory.Database.ScalarAsync<string>(
            "SELECT Disposition FROM IntakeAssets"));
        Assert.Equal("intake_receipt_recorded", await factory.Database.ScalarAsync<string>(
            "SELECT EventType FROM IntakeReceiptEvents"));
        AssertEnvelopeVersionOne(await factory.Database.ScalarAsync<string>(
            "SELECT EvidenceJson FROM IntakeReceipts"));
        AssertEnvelopeVersionOne(await factory.Database.ScalarAsync<string>(
            "SELECT FieldsJson FROM IntakeReceipts"));
        AssertEnvelopeVersionOne(await factory.Database.ScalarAsync<string>(
            "SELECT OcrCandidatesJson FROM IntakeReceipts"));
        AssertEnvelopeVersionOne(await factory.Database.ScalarAsync<string>(
            "SELECT DetailsJson FROM IntakeReceiptEvents"));
    }

    [Fact]
    public async Task UnknownPersistedDecisionCodeFailsVisibleRead()
    {
        using var factory = new IntakeWebApplicationFactory();
        var receiptId = await UploadUnknownAsync(factory);
        await ExecuteAsync(factory, "UPDATE IntakeReceipts SET Decision='future_decision'");

        await using var scope = factory.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>();
        var error = await Assert.ThrowsAsync<InvalidDataException>(
            () => queries.GetAsync(receiptId, CancellationToken.None));

        Assert.Contains("future_decision", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownPersistedDecisionCodeFailsVisibleAcrossQueueQueries()
    {
        using var factory = new IntakeWebApplicationFactory();
        await UploadUnknownAsync(factory);
        await ExecuteAsync(factory, "UPDATE IntakeReceipts SET Decision='future_decision'");

        await using var scope = factory.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>();

        var countsError = await Assert.ThrowsAsync<InvalidDataException>(
            () => queries.GetCountsAsync(CancellationToken.None));
        var unfilteredListError = await Assert.ThrowsAsync<InvalidDataException>(
            () => queries.ListAsync(null, CancellationToken.None));
        var filteredListError = await Assert.ThrowsAsync<InvalidDataException>(
            () => queries.ListAsync(IntakeDecision.Unsupported, CancellationToken.None));

        Assert.Contains("future_decision", countsError.Message, StringComparison.Ordinal);
        Assert.Contains("future_decision", unfilteredListError.Message, StringComparison.Ordinal);
        Assert.Contains("future_decision", filteredListError.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownPersistedJsonEnvelopeVersionFailsVisibleRead()
    {
        using var factory = new IntakeWebApplicationFactory();
        var receiptId = await UploadUnknownAsync(factory);
        await ExecuteAsync(factory,
            "UPDATE IntakeReceipts SET EvidenceJson='{\"version\":2,\"data\":[]}'");

        await using var scope = factory.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>();
        var error = await Assert.ThrowsAsync<InvalidDataException>(
            () => queries.GetAsync(receiptId, CancellationToken.None));

        Assert.Contains("version '2'", error.Message, StringComparison.Ordinal);
    }

    private static async Task<Guid> UploadUnknownAsync(IntakeWebApplicationFactory factory)
    {
        using var client = IntakeWebDriver.CreateClient(factory);
        return IntakeWebDriver.ReceiptId(await IntakeWebDriver.UploadAndProcessAsync(factory, client, "unknown-format.xyz",
        "application/x-unknown",
        [0x01]));
    }

    private static Task ExecuteAsync(IntakeWebApplicationFactory factory, string sql) =>
        factory.Database.ExecuteAsync(sql);


    private static void AssertEnvelopeVersionOne(string json)
    {
        using var document = JsonDocument.Parse(json);
        Assert.Equal(1, document.RootElement.GetProperty("version").GetInt32());
        Assert.True(document.RootElement.TryGetProperty("data", out _));
    }
}
