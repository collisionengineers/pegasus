using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Intake;
using Pegasus.Core.Triage;

namespace Pegasus.IntegrationTests;

public sealed class QdosTriageIntegrationTests
{
    private const string ForwardedEmailHash = "B91F5BBC622041B088D6F55E7A949CAEC945F476BDB18C489D0756D797552FB0";

    [GenuineQdosCorpusFact]
    [Trait("Category", "Corpus")]
    public async Task GenuineEvaluatedIntakeCreatesASeparateUnlinkedTriageRecord()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var upload = await IntakeWebDriver.UploadAsync(client, GenuineQdosCorpus.Read(ForwardedEmailHash));
        var receiptId = IntakeWebDriver.ReceiptId(upload);

        await using var scope = factory.Services.CreateAsyncScope();
        var receipts = scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>();
        var createTriage = scope.ServiceProvider.GetRequiredService<ICreateTriageFromIntake>();
        var triageQueries = scope.ServiceProvider.GetRequiredService<ITriageQueries>();
        var receipt = Assert.IsType<IntakeReceipt>(
            await receipts.GetAsync(receiptId, CancellationToken.None));
        var evaluationRevisionId = await GetEvaluationRevisionIdAsync(factory.DatabasePath, receipt.Id);

        var triage = await createTriage.ExecuteAsync(
            new(
                new(receipt.Id, receipt.SourceIdentity, receipt.SourceHash, evaluationRevisionId),
                "AB12 CDE",
                "staff:integration-test",
                $"triage-create:{Guid.NewGuid():N}"),
            CancellationToken.None);
        var detail = await triageQueries.GetAsync(triage.Id, CancellationToken.None);

        Assert.Equal(TriageState.Open, triage.State);
        Assert.Null(triage.LinkedCaseId);
        Assert.NotNull(detail);
        Assert.Equal(receipt.Id, detail.Record.Origin.ReceiptId);
        Assert.Equal(TriageState.Open, detail.Record.State);
        Assert.Null(detail.Record.LinkedCaseId);
        Assert.Empty(detail.Findings);
        Assert.Empty(detail.ResponseEvidence);
    }

    private static async Task<Guid> GetEvaluationRevisionIdAsync(string databasePath, Guid receiptId)
    {
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id
            FROM IntakeEvaluations
            WHERE ProcessedReceiptId = $receiptId
            ORDER BY Revision DESC
            LIMIT 1
            """;
        command.Parameters.AddWithValue("$receiptId", receiptId.ToString("D"));

        var result = await command.ExecuteScalarAsync();
        return Guid.Parse(Assert.IsType<string>(result));
    }
}
