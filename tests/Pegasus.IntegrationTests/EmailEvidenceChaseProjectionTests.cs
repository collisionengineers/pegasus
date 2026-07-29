using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

public sealed class SqliteEmailEvidenceChaseProjectionTests
{
    [Fact]
    public async Task ScheduledChasesReturnOnlyDueItemsInStableOrder()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<PegasusDbContext>()
            .UseSqlite(connection)
            .Options;
        var factory = new PooledDbContextFactory<PegasusDbContext>(options);
        await using (var context = await factory.CreateDbContextAsync())
        {
            await context.Database.EnsureCreatedAsync();
        }

        await EmailEvidenceChaseProjectionTestData.AssertDueProjectionAsync(factory);
    }
}

[Collection(LocalDbFixtureDefinition.Name)]
[Trait("Category", "SqlServer")]
public sealed class SqlServerEmailEvidenceChaseProjectionTests
{
    [Fact]
    public async Task ScheduledChasesReturnOnlyDueItemsInStableOrder()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var options = new DbContextOptionsBuilder<PegasusDbContext>()
            .UseSqlServer(database.ConnectionString)
            .Options;
        var factory = new PooledDbContextFactory<PegasusDbContext>(options);

        await EmailEvidenceChaseProjectionTestData.AssertDueProjectionAsync(factory);
    }
}

internal static class EmailEvidenceChaseProjectionTestData
{
    private static readonly DateTimeOffset AsOfUtc =
        new(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid FirstDueId =
        Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid SecondDueId =
        Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid ThirdDueId =
        Guid.Parse("00000000-0000-0000-0000-000000000003");
    private static readonly Guid FourthDueId =
        Guid.Parse("00000000-0000-0000-0000-000000000004");
    private static readonly Guid FutureId =
        Guid.Parse("00000000-0000-0000-0000-000000000005");
    private static readonly Guid RespondedId =
        Guid.Parse("00000000-0000-0000-0000-000000000006");

    public static async Task AssertDueProjectionAsync(
        IDbContextFactory<PegasusDbContext> factory)
    {
        await SeedAsync(factory);
        var store = new EfEmailEvidenceStore(factory);

        var allDue = await store.GetDueAsync(AsOfUtc, 10, CancellationToken.None);
        var boundedDue = await store.GetDueAsync(AsOfUtc, 3, CancellationToken.None);
        var replay = await store.GetDueAsync(AsOfUtc, 10, CancellationToken.None);

        Assert.Equal(
            [FirstDueId, SecondDueId, ThirdDueId, FourthDueId],
            allDue.Select(item => item.SentEvidenceId));
        Assert.All(allDue, item => Assert.True(item.ChaseDueAtUtc <= AsOfUtc));
        Assert.DoesNotContain(allDue, item => item.SentEvidenceId == FutureId);
        Assert.DoesNotContain(allDue, item => item.SentEvidenceId == RespondedId);
        Assert.Equal(
            [FirstDueId, SecondDueId, ThirdDueId],
            boundedDue.Select(item => item.SentEvidenceId));
        Assert.Equal(
            allDue.Select(item => item.SentEvidenceId),
            replay.Select(item => item.SentEvidenceId));
    }

    private static async Task SeedAsync(IDbContextFactory<PegasusDbContext> factory)
    {
        var receiptId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var triageId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        var sentAtUtc = AsOfUtc.AddDays(-1);
        await using var context = await factory.CreateDbContextAsync();

        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO IntakeReceipts
                (Id, SourceFileName, MediaType, SourceLength, SourceHash, SourceChannel,
                 ExternalReceiptToken, ReceivedAtUtc, ProcessedAtUtc, SourceReaderKey,
                 SourceReaderVersion, Version, Decision, DecisionReason, EvidenceJson,
                 FieldsJson, OcrCandidatesJson)
            VALUES
                ({receiptId}, {"source.eml"}, {"message/rfc822"}, {1L}, {new string('a', 64)},
                 {"mailbox"}, {"chase-projection-receipt"}, {sentAtUtc}, {sentAtUtc},
                 {"integration-test"}, {"1"}, {0L}, {"draft_ready"}, {"test"}, {"[]"},
                 {"[]"}, {"[]"})
            """);

        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO Triage
                (Id, OriginReceiptId, SourceChannel, ExternalReceiptToken, SourceHash,
                 EvaluationRevisionId, NormalizedVehicleRegistration, State, CreatedAtUtc,
                 CreationOperationKey, Version, ConcurrencyToken)
            VALUES
                ({triageId}, {receiptId}, {"mailbox"}, {"chase-projection-triage"},
                 {new string('b', 64)}, {Guid.Parse("30000000-0000-0000-0000-000000000001")},
                 {"AB12CDE"}, {"Open"}, {sentAtUtc}, {"chase-projection-create"}, {0L},
                 {Guid.Parse("30000000-0000-0000-0000-000000000002")})
            """);

        await InsertSentEvidenceAsync(
            context,
            triageId,
            FirstDueId,
            new DateTimeOffset(2030, 1, 1, 12, 30, 0, TimeSpan.FromHours(1)));
        await InsertSentEvidenceAsync(
            context,
            triageId,
            SecondDueId,
            new DateTimeOffset(2030, 1, 1, 6, 45, 0, TimeSpan.FromHours(-5)));
        await InsertSentEvidenceAsync(
            context,
            triageId,
            FourthDueId,
            AsOfUtc);
        await InsertSentEvidenceAsync(
            context,
            triageId,
            ThirdDueId,
            new DateTimeOffset(2030, 1, 1, 7, 0, 0, TimeSpan.FromHours(-5)));
        await InsertSentEvidenceAsync(
            context,
            triageId,
            FutureId,
            new DateTimeOffset(2030, 1, 1, 11, 30, 0, TimeSpan.FromHours(-1)));
        await InsertSentEvidenceAsync(
            context,
            triageId,
            RespondedId,
            AsOfUtc.AddHours(-2));

        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO EmailResponseEvidence
                (Id, SentEvidenceId, MessageIdentity, MimeSha256, ReceivedAtUtc, Actor,
                 OperationKey, RequestHash)
            VALUES
                ({Guid.Parse("40000000-0000-0000-0000-000000000001")}, {RespondedId},
                 {"response-message"}, {new string('d', 64)}, {AsOfUtc.AddHours(-1)},
                 {"staff:integration-test"}, {"response-operation"}, {new string('e', 64)})
            """);
    }

    private static Task<int> InsertSentEvidenceAsync(
        PegasusDbContext context,
        Guid triageId,
        Guid evidenceId,
        DateTimeOffset chaseDueAtUtc)
    {
        var identity = $"message-{evidenceId:N}";
        var operationKey = $"sent-{evidenceId:N}";
        var requestHash = evidenceId.ToString("N").PadRight(64, '0');
        return context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO SentEmailEvidence
                (Id, TriageId, MessageIdentity, Subject, RecipientsJson, MimeSha256, SentAtUtc,
                 ChaseDueAtUtc, Actor, OperationKey, RequestHash, Version)
            VALUES
                ({evidenceId}, {triageId}, {identity}, {"Evidence request"},
                 {"[\"recipient@example.test\"]"}, {new string('c', 64)}, {AsOfUtc.AddDays(-1)},
                 {chaseDueAtUtc}, {"staff:integration-test"}, {operationKey}, {requestHash}, {0L})
            """);
    }
}
