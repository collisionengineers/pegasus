using System.Text.Json;
using Pegasus.Core.Intake;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Pegasus.IntegrationTests;

public sealed class IntakeStablePersistenceTests
{
    [Fact]
    public async Task UnknownFormatIsRetainedAsUnsupportedWithStableCodesAndVersionOneEnvelopes()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var upload = await IntakeWebDriver.UploadAsync(
            client,
            "unknown-format.xyz",
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

        await using var connection = new SqliteConnection($"Data Source={factory.DatabasePath}");
        await connection.OpenAsync();
        Assert.Equal("unsupported", await ScalarAsync(connection,
            "SELECT Decision FROM IntakeReceipts"));
        Assert.Equal("manual_upload", await ScalarAsync(connection,
            "SELECT SourceChannel FROM IntakeReceipts"));
        Assert.Equal("source", await ScalarAsync(connection,
            "SELECT Kind FROM IntakeAssets"));
        Assert.Equal("source", await ScalarAsync(connection,
            "SELECT Disposition FROM IntakeAssets"));
        Assert.Equal("intake_receipt_recorded", await ScalarAsync(connection,
            "SELECT EventType FROM IntakeReceiptEvents"));
        AssertEnvelopeVersionOne(await ScalarAsync(connection,
            "SELECT EvidenceJson FROM IntakeReceipts"));
        AssertEnvelopeVersionOne(await ScalarAsync(connection,
            "SELECT FieldsJson FROM IntakeReceipts"));
        AssertEnvelopeVersionOne(await ScalarAsync(connection,
            "SELECT OcrCandidatesJson FROM IntakeReceipts"));
        AssertEnvelopeVersionOne(await ScalarAsync(connection,
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
        return IntakeWebDriver.ReceiptId(await IntakeWebDriver.UploadAsync(
            client,
            "unknown-format.xyz",
            "application/x-unknown",
            [0x01]));
    }

    private static async Task ExecuteAsync(IntakeWebApplicationFactory factory, string sql)
    {
        await using var connection = new SqliteConnection($"Data Source={factory.DatabasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string> ScalarAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (string)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Expected a scalar string."));
    }

    private static void AssertEnvelopeVersionOne(string json)
    {
        using var document = JsonDocument.Parse(json);
        Assert.Equal(1, document.RootElement.GetProperty("version").GetInt32());
        Assert.True(document.RootElement.TryGetProperty("data", out _));
    }
}
