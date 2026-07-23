using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using CollisionSpike.Core.Intake.Qdos;
using CollisionSpike.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MimeKit;

namespace CollisionSpike.IntegrationTests;

public sealed class QdosTypedDraftWebTests
{
    private const string MediaType = "message/rfc822";
    private const string CompleteUploadName = "controlled-typed-intake.eml";

    [Fact]
    public async Task SameManualUploadTokenReplaysOneReceiptDraftAndAssetSet()
    {
        const string externalReceiptToken = "77777777777777777777777777777777";
        using var factory = new QdosWebApplicationFactory();
        using var client = QdosWebDriver.CreateClient(factory);
        var bytes = CreateEmail(CompleteBody());

        var first = await QdosWebDriver.UploadAsync(
            client, CompleteUploadName, MediaType, bytes, externalReceiptToken);
        var replay = await QdosWebDriver.UploadAsync(
            client, CompleteUploadName, MediaType, bytes, externalReceiptToken);

        var firstId = QdosWebDriver.ReceiptId(first);
        Assert.Equal(firstId, QdosWebDriver.ReceiptId(replay));
        var receipt = await GetReceiptAsync(factory, firstId);
        Assert.Equal(IntakeSourceChannel.ManualUpload, receipt.SourceIdentity.Channel);
        Assert.Equal(externalReceiptToken, receipt.SourceIdentity.ExternalReceiptToken);
        Assert.NotEmpty(receipt.AssetRecords);
        using var replayReview = await client.GetAsync(replay.Location);
        var replayHtml = await replayReview.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, replayReview.StatusCode);
        Assert.Contains("already processed", replayHtml, StringComparison.Ordinal);
        Assert.Equal(1, await CountRowsAsync(factory, "QdosIntakeReceipts"));
        Assert.Equal(1, await CountRowsAsync(factory, "QdosTypedDrafts"));
        Assert.Equal(receipt.AssetRecords.Count, await CountRowsAsync(factory, "QdosIntakeAssets"));
        Assert.Equal(1, await CountRowsAsync(factory, "AuditEvents"));
    }

    [Fact]
    public async Task SameManualUploadTokenWithDifferentBytesShowsConflictWithoutSecondPersistenceOrArtifact()
    {
        const string externalReceiptToken = "cccccccccccccccccccccccccccccccc";
        using var factory = new QdosWebApplicationFactory();
        using var client = QdosWebDriver.CreateClient(factory);
        var firstBytes = CreateEmail(CompleteBody());
        var changedBytes = CreateEmail(
            CompleteBody().Replace(
                "PROTOCOL-2031-001",
                "PROTOCOL-2031-CHANGED",
                StringComparison.Ordinal));
        var firstHash = Convert.ToHexString(SHA256.HashData(firstBytes));
        var changedHash = Convert.ToHexString(SHA256.HashData(changedBytes));

        var first = await QdosWebDriver.UploadAsync(
            client, CompleteUploadName, MediaType, firstBytes, externalReceiptToken);
        var firstReceipt = await GetReceiptAsync(factory, QdosWebDriver.ReceiptId(first));
        var conflict = await QdosWebDriver.UploadAsync(
            client, CompleteUploadName, MediaType, changedBytes, externalReceiptToken);

        Assert.Equal(HttpStatusCode.OK, conflict.StatusCode);
        Assert.Null(conflict.Location);
        Assert.Contains(
            "already used for different content",
            conflict.ResponseBody,
            StringComparison.Ordinal);
        Assert.Equal(firstHash, firstReceipt.SourceHash);
        Assert.NotEqual(firstHash, changedHash);
        Assert.Equal(1, await CountRowsAsync(factory, "QdosIntakeReceipts"));
        Assert.Equal(1, await CountRowsAsync(factory, "QdosTypedDrafts"));
        Assert.Equal(firstReceipt.AssetRecords.Count, await CountRowsAsync(factory, "QdosIntakeAssets"));
        Assert.Equal(1, await CountRowsAsync(factory, "AuditEvents"));
        await using var scope = factory.Services.CreateAsyncScope();
        var artifactStore = scope.ServiceProvider.GetRequiredService<IIntakeArtifactStore>();
        Assert.NotNull(await artifactStore.ReadAsync(StorageKey(firstHash), CancellationToken.None));
        Assert.Null(await artifactStore.ReadAsync(StorageKey(changedHash), CancellationToken.None));
    }

    [Fact]
    public async Task IdenticalBytesWithDifferentTokensPersistDistinctSourceIdentitiesWithMatchingHashes()
    {
        const string firstToken = "88888888888888888888888888888888";
        const string secondToken = "99999999999999999999999999999999";
        using var factory = new QdosWebApplicationFactory();
        using var client = QdosWebDriver.CreateClient(factory);
        var bytes = CreateEmail(CompleteBody());
        var expectedHash = Convert.ToHexString(SHA256.HashData(bytes));

        var first = await QdosWebDriver.UploadAsync(client, CompleteUploadName, MediaType, bytes, firstToken);
        var second = await QdosWebDriver.UploadAsync(client, CompleteUploadName, MediaType, bytes, secondToken);
        var firstId = QdosWebDriver.ReceiptId(first);
        var secondId = QdosWebDriver.ReceiptId(second);
        var firstReceipt = await GetReceiptAsync(factory, firstId);
        var secondReceipt = await GetReceiptAsync(factory, secondId);

        Assert.NotEqual(firstId, secondId);
        Assert.Equal(expectedHash, firstReceipt.SourceHash);
        Assert.Equal(firstReceipt.SourceHash, secondReceipt.SourceHash);
        Assert.Equal(firstToken, firstReceipt.SourceIdentity.ExternalReceiptToken);
        Assert.Equal(secondToken, secondReceipt.SourceIdentity.ExternalReceiptToken);
        Assert.NotEmpty(firstReceipt.AssetRecords);
        Assert.Equal(firstReceipt.AssetRecords.Count, secondReceipt.AssetRecords.Count);
        Assert.Equal(2, await CountRowsAsync(factory, "QdosIntakeReceipts"));
        Assert.Equal(2, await CountRowsAsync(factory, "QdosTypedDrafts"));
        Assert.Equal(2 * firstReceipt.AssetRecords.Count, await CountRowsAsync(factory, "QdosIntakeAssets"));
        Assert.Equal(2, await CountRowsAsync(factory, "AuditEvents"));
    }

    [Fact]
    public async Task UploadAndReviewPersistAllTypedFieldsWithoutCaseOrCounterSchema()
    {
        using var factory = new QdosWebApplicationFactory();
        using var client = QdosWebDriver.CreateClient(factory);
        var upload = await QdosWebDriver.UploadAsync(
            client,
            CompleteUploadName,
            MediaType,
            CreateEmail(CompleteBody()),
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        var receipt = await GetReceiptAsync(factory, QdosWebDriver.ReceiptId(upload));

        Assert.Equal(QdosIntakeDecision.DraftReady, receipt.Decision);
        var typed = Assert.IsType<QdosTypedDraft>(receipt.TypedDraft);
        Assert.Equal("QDOS", typed.PrincipalCode);
        Assert.Equal("Controlled Claimant", typed.ClaimantName);
        Assert.Equal("PROTOCOL-2031-001", typed.ClaimNumber);
        Assert.Equal("AB12CDE", typed.VehicleRegistration);
        Assert.Equal("Example Make", typed.VehicleMake);
        Assert.Equal("Example Model", typed.VehicleModel);
        Assert.Equal(12345L, typed.VehicleMileage);
        Assert.Equal("Controlled protocol circumstances", typed.AccidentCircumstances);
        Assert.Equal(new DateOnly(2031, 3, 4), typed.DateOfIncident);
        Assert.Equal(new DateOnly(2031, 3, 5), typed.InstructionDate);
        Assert.Equal("Image Based Assessment", typed.InspectionAddress);
        Assert.Equal(10, receipt.Fields.Count);

        using var review = await client.GetAsync(upload.Location);
        var html = await review.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, review.StatusCode);
        foreach (var value in new[]
                 {
                     "QDOS", "Controlled Claimant", "PROTOCOL-2031-001", "AB12CDE",
                     "Example Make", "Example Model", "12,345", "Controlled protocol circumstances",
                     "04 Mar 2031", "05 Mar 2031", "Image Based Assessment"
                 })
        {
            Assert.Contains(value, html, StringComparison.Ordinal);
        }

        Assert.False(await TableExistsAsync(factory, "Cases"));
        Assert.False(await TableExistsAsync(factory, "PrincipalYearCounters"));
    }

    [Fact]
    public async Task InvalidAndConflictingValuesRemainReviewableWithNullTypedValues()
    {
        using var factory = new QdosWebApplicationFactory();
        using var client = QdosWebDriver.CreateClient(factory);
        var upload = await QdosWebDriver.UploadAsync(
            client,
            "controlled-invalid-values.eml",
            MediaType,
            CreateEmail(
                """
                QDOS instruction
                Claim Number: PROTOCOL-INVALID
                Vehicle Registration: AB12 CDE
                Vehicle Mileage: awaiting confirmation
                Date of Incident: 04/03/2031
                Date of Incident: 05/03/2031
                """),
            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
        var receipt = await GetReceiptAsync(factory, QdosWebDriver.ReceiptId(upload));

        Assert.Equal(QdosIntakeDecision.DraftReady, receipt.Decision);
        var typed = Assert.IsType<QdosTypedDraft>(receipt.TypedDraft);
        Assert.Null(typed.VehicleMileage);
        Assert.Null(typed.DateOfIncident);
        var mileage = Assert.Single(receipt.Fields, field => field.Name == "Vehicle mileage");
        Assert.Equal("awaiting confirmation", mileage.SuggestedValue);
        var mileageCandidate = Assert.Single(mileage.Candidates);
        Assert.Equal(QdosEvidenceSource.EmailBody, mileageCandidate.Source);
        Assert.Contains("email body", mileageCandidate.SourceLabel, StringComparison.Ordinal);
        var incidentDate = Assert.Single(receipt.Fields, field => field.Name == "Date of incident");
        Assert.True(incidentDate.HasConflict);
        Assert.Null(incidentDate.SuggestedValue);
        Assert.Equal(["04/03/2031", "05/03/2031"],
            incidentDate.Candidates.Select(candidate => candidate.Value).ToArray());

        using var review = await client.GetAsync(upload.Location);
        var html = await review.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, review.StatusCode);
        Assert.Contains("awaiting confirmation", html, StringComparison.Ordinal);
        Assert.Contains("Conflicting suggestions", html, StringComparison.Ordinal);
        Assert.Contains("uploaded controlled-invalid-values.eml, email body", html, StringComparison.Ordinal);
    }

    private static string CompleteBody() =>
        """
        QDOS instruction
        Claimant Name: Controlled Claimant
        Claim Number: PROTOCOL-2031-001
        Vehicle Registration: AB12 CDE
        Vehicle Make: Example Make
        Vehicle Model: Example Model
        Vehicle Mileage: 12,345 miles
        Accident Circumstances: Controlled protocol circumstances
        Date of Incident: 04/03/2031
        Instruction Date: 05/03/2031
        Inspection Address: Image Based Assessment
        """;

    private static byte[] CreateEmail(string body)
    {
        var message = new MimeMessage
        {
            Subject = "Controlled QDOS protocol fixture",
            Date = new DateTimeOffset(2031, 3, 5, 10, 30, 0, TimeSpan.Zero),
            Body = new TextPart("plain") { Text = body }
        };
        message.From.Add(new MailboxAddress("Protocol sender", "protocol-sender@example.invalid"));
        message.To.Add(new MailboxAddress("Intake", "intake@example.invalid"));
        using var stream = new MemoryStream();
        message.WriteTo(stream);
        return stream.ToArray();
    }

    private static string StorageKey(string hash) => $"sha256/{hash[..2]}/{hash}";

    private static async Task<QdosIntakeRecord> GetReceiptAsync(
        QdosWebApplicationFactory factory,
        Guid id)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return Assert.IsType<QdosIntakeRecord>(await scope.ServiceProvider
            .GetRequiredService<IQdosIntakeQueries>()
            .GetAsync(id, CancellationToken.None));
    }

    private static async Task<int> CountRowsAsync(QdosWebApplicationFactory factory, string tableName)
    {
        var allowed = tableName switch
        {
            "QdosIntakeReceipts" or "QdosTypedDrafts" or "QdosIntakeAssets" or "AuditEvents" => tableName,
            _ => throw new ArgumentOutOfRangeException(nameof(tableName))
        };
        return await ScalarAsync<int>(factory, $"SELECT COUNT(*) FROM [{allowed}]");
    }

    private static async Task<bool> TableExistsAsync(QdosWebApplicationFactory factory, string tableName) =>
        1 == await ScalarAsync<long>(
            factory,
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = @tableName",
            tableName);

    private static async Task<T> ScalarAsync<T>(
        QdosWebApplicationFactory factory,
        string commandText,
        string? tableName = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<CollisionSpikeDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        await context.Database.OpenConnectionAsync();
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = commandText;
            if (tableName is not null)
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = "@tableName";
                parameter.Value = tableName;
                command.Parameters.Add(parameter);
            }

            var result = await command.ExecuteScalarAsync();
            Assert.NotNull(result);
            return (T)Convert.ChangeType(result, typeof(T), CultureInfo.InvariantCulture);
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }
}
