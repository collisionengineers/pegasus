using Microsoft.Data.Sqlite;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Intake;
using Pegasus.Core.Triage;

namespace Pegasus.IntegrationTests;

public sealed class QdosTriageIntegrationTests
{
    [Fact]
    [Trait("Category", "QdosAlphaAcceptance")]
    public async Task DevelopmentOfflineUploadPersistsEvaluationAndCreatesOneReplaySafeTriage()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var email = OfflineAcceptanceTests.CreateEmail(
            "triage-request.eml",
            "QDOS instruction\r\nClaimant Name: Triage Claimant\r\nClaim Number: TRIAGE-001\r\nVehicle Registration: AB12 CDE");
        const string replayToken = "77777777777777777777777777777777";

        var first = await IntakeWebDriver.UploadAsync(
            client,
            email.FileName,
            email.MediaType,
            email.Content,
            replayToken);
        var replay = await IntakeWebDriver.UploadAsync(
            client,
            email.FileName,
            email.MediaType,
            email.Content,
            replayToken);
        var receiptId = IntakeWebDriver.ReceiptId(first);

        Assert.Equal(receiptId, IntakeWebDriver.ReceiptId(replay));
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.IsType<CreateTriageFromIntake>(
            scope.ServiceProvider.GetRequiredService<ICreateTriageFromIntake>());
        var triageQueries = scope.ServiceProvider.GetRequiredService<ITriageQueries>();
        var summary = Assert.Single(
            await triageQueries.ListAsync(null, CancellationToken.None));
        var detail = Assert.IsType<TriageDetail>(
            await triageQueries.GetAsync(summary.Id, CancellationToken.None));
        var evaluation = Assert.Single(
            await GetEvaluationRevisionsAsync(factory.DatabasePath, receiptId));

        Assert.Equal(1, evaluation.Revision);
        Assert.Equal(receiptId, detail.Record.Origin.ReceiptId);
        Assert.Equal(evaluation.Id, detail.Record.Origin.EvaluationRevisionId);
        Assert.Equal("AB12CDE", detail.Record.NormalizedVehicleRegistration);
        Assert.Equal(TriageState.Open, detail.Record.State);
        Assert.Null(detail.Record.LinkedCaseId);
        Assert.Empty(detail.Findings);
        Assert.Empty(detail.ResponseEvidence);
    }

    [Fact]
    [Trait("Category", "QdosAlphaAcceptance")]
    public async Task NonQualifyingCompletedIntakePersistsEvaluationWithoutCreatingTriage()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var needsSorting = Encoding.UTF8.GetBytes(
            "From: unknown@example.test\r\n" +
            "To: intake@example.test\r\n" +
            "Subject: Unclassified correspondence\r\n" +
            "MIME-Version: 1.0\r\n" +
            "Content-Type: text/plain; charset=utf-8\r\n\r\n" +
            "This retained correspondence contains no supported instruction evidence.");
        var missingRegistration = OfflineAcceptanceTests.CreateEmail(
            "missing-registration.eml",
            "QDOS instruction\r\nClaimant Name: No Registration\r\nClaim Number: TRIAGE-002");

        var sortingUpload = await IntakeWebDriver.UploadAsync(
            client,
            "needs-sorting.eml",
            "message/rfc822",
            needsSorting);
        var missingRegistrationUpload = await IntakeWebDriver.UploadAsync(
            client,
            missingRegistration.FileName,
            missingRegistration.MediaType,
            missingRegistration.Content);
        var blockedUpload = await IntakeWebDriver.UploadAsync(
            client,
            "unsupported.txt",
            "text/plain",
            Encoding.UTF8.GetBytes("Unsupported intake source."));
        var sortingReceiptId = IntakeWebDriver.ReceiptId(sortingUpload);
        var missingRegistrationReceiptId = IntakeWebDriver.ReceiptId(missingRegistrationUpload);
        var blockedReceiptId = IntakeWebDriver.ReceiptId(blockedUpload);

        await using var scope = factory.Services.CreateAsyncScope();
        var receipts = scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>();
        var triageQueries = scope.ServiceProvider.GetRequiredService<ITriageQueries>();
        var sortingReceipt = Assert.IsType<IntakeReceipt>(
            await receipts.GetAsync(sortingReceiptId, CancellationToken.None));
        var missingRegistrationReceipt = Assert.IsType<IntakeReceipt>(
            await receipts.GetAsync(missingRegistrationReceiptId, CancellationToken.None));
        var blockedReceipt = Assert.IsType<IntakeReceipt>(
            await receipts.GetAsync(blockedReceiptId, CancellationToken.None));

        Assert.Equal(IntakeDecision.NeedsSorting, sortingReceipt.Decision);
        Assert.Equal(IntakeDecision.DraftReady, missingRegistrationReceipt.Decision);
        Assert.Null(missingRegistrationReceipt.InstructionDraft?.VehicleRegistration);
        Assert.Equal(IntakeDecision.Unsupported, blockedReceipt.Decision);
        Assert.Empty(await triageQueries.ListAsync(null, CancellationToken.None));
        Assert.Single(await GetEvaluationRevisionsAsync(factory.DatabasePath, sortingReceiptId));
        Assert.Single(await GetEvaluationRevisionsAsync(factory.DatabasePath, missingRegistrationReceiptId));
        Assert.Single(await GetEvaluationRevisionsAsync(factory.DatabasePath, blockedReceiptId));
    }

    private static async Task<IReadOnlyList<EvaluationRevision>> GetEvaluationRevisionsAsync(
        string databasePath,
        Guid receiptId)
    {
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Revision
            FROM IntakeEvaluations
            WHERE ProcessedReceiptId = $receiptId
            ORDER BY Revision
            """;
        command.Parameters.AddWithValue("$receiptId", receiptId);

        var evaluations = new List<EvaluationRevision>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            evaluations.Add(new(
                Guid.Parse(reader.GetString(0)),
                reader.GetInt32(1)));
        }

        return evaluations;
    }

    private sealed record EvaluationRevision(Guid Id, int Revision);
}
