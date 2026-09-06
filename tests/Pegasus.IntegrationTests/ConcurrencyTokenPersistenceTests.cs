using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Pegasus.Core.Cases;
using Pegasus.Core.Intake;
using Pegasus.Core.Triage;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

public sealed class ConcurrencyTokenPersistenceTests
{
    private const string CaseEntityName = "Pegasus.Infrastructure.Persistence.CaseEntity";
    private const string TriageEntityName = "Pegasus.Infrastructure.Persistence.TriageEntity";
    private static readonly DateTimeOffset FixedUtcNow =
        new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);


    [Fact]
    [Trait("Category", "SqlServer")]
    public async Task FreshLocalDbCaseAcceptanceAndTriageInsertUpdateGenerateTokensAndRejectStaleWrites()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var options = new DbContextOptionsBuilder<PegasusDbContext>()
            .UseSqlServer(database.ConnectionString)
            .Options;
        var factory = new PooledDbContextFactory<PegasusDbContext>(options);
        await using (var context = await factory.CreateDbContextAsync())
        {
            Assert.False(context.Database.HasPendingModelChanges());
            Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        }

        await AssertPersistenceContractAsync(factory);
    }

    private static async Task AssertPersistenceContractAsync(
        IDbContextFactory<PegasusDbContext> factory)
    {
        var seed = await SeedPrerequisitesAsync(factory);
        await AssertApplicationManagedMetadataAsync(factory);

        var timeProvider = new FixedTimeProvider(FixedUtcNow);
        var caseStore = new EfCaseAcceptanceStore(factory, timeProvider);
        var acceptedCase = await caseStore.AcceptAsync(
            new(
                seed.CaseReceiptId,
                0,
                Pegasus.Core.Identity.ActionActor.SystemWorker("concurrency-test"),
                "accept-case-concurrency-test",
                "Concurrency token persistence fixture",
                CaseType.Inspection,
                "QDOS",
                new(true, true, true, true),
                new(true, "concurrency-test-policy", 1),
                CaseInspectionMode.PhysicalAddress),
            CancellationToken.None);

        var acceptedMatch = new IntakeEvidence(
            IntakeEvidenceSource.EmailBody,
            IntakeEvidenceStrength.Strong,
            IntakeEvidenceFinding.AcceptedTriageMatch,
            "concurrency-triage-match",
            "Accepted match fixture.",
            "concurrency-test-matcher",
            1);

        var triageStore = new EfTriageStore(factory, timeProvider);
        var triage = await triageStore.CreateAsync(
            new(
                new(
                    seed.TriageReceiptId,
                    new(IntakeSourceChannel.Mailbox, "triage-concurrency-source"),
                    seed.TriageSourceHash,
                    seed.EvaluationId),
                "AB12CDE",
                acceptedMatch,
                Pegasus.Core.Identity.ActionActor.SystemWorker("concurrency-test"),
                "create-triage-concurrency-test"),
            CancellationToken.None);

        await AssertTokenLifecycleAsync(
            factory,
            CaseEntityName,
            acceptedCase.Identity.CaseId,
            "CustodyState",
            "confirmed",
            "failed",
            "pending");
        await AssertTokenLifecycleAsync(
            factory,
            TriageEntityName,
            triage.Id,
            "State",
            "awaiting_information",
            "completed",
            "cancelled");
    }

    private static async Task AssertApplicationManagedMetadataAsync(
        IDbContextFactory<PegasusDbContext> factory)
    {
        await using var context = await factory.CreateDbContextAsync();
        foreach (var entityName in new[] { CaseEntityName, TriageEntityName })
        {
            var entityType = context.Model.FindEntityType(entityName)
                ?? throw new InvalidOperationException($"EF model entity '{entityName}' is missing.");
            var property = entityType.FindProperty("ConcurrencyToken")
                ?? throw new InvalidOperationException($"EF model entity '{entityName}' has no concurrency token.");
            Assert.True(property.IsConcurrencyToken);
            Assert.False(property.IsNullable);
            Assert.Equal(ValueGenerated.Never, property.ValueGenerated);
            var versionProperty = entityType.FindProperty("Version")
                ?? throw new InvalidOperationException(
                    $"EF model entity '{entityName}' has no optimistic version.");
            Assert.True(versionProperty.IsConcurrencyToken);
        }
    }

    private static async Task AssertTokenLifecycleAsync(
        IDbContextFactory<PegasusDbContext> factory,
        string entityName,
        Guid id,
        string mutableProperty,
        string successfulValue,
        string winningValue,
        string staleValue)
    {
        Guid insertedToken;
        long unchangedVersion;
        await using (var context = await factory.CreateDbContextAsync())
        {
            var entity = await FindAsync(context, entityName, id);
            insertedToken = ReadToken(context, entity);
            unchangedVersion = ReadVersion(context, entity);
        }
        Assert.NotEqual(Guid.Empty, insertedToken);

        Guid updatedToken;
        await using (var context = await factory.CreateDbContextAsync())
        {
            var entity = await FindAsync(context, entityName, id);
            context.Entry(entity).Property(mutableProperty).CurrentValue = successfulValue;
            await context.SaveChangesAsync();
            updatedToken = ReadToken(context, entity);
            Assert.Equal(unchangedVersion, ReadVersion(context, entity));
        }
        Assert.NotEqual(Guid.Empty, updatedToken);
        Assert.NotEqual(insertedToken, updatedToken);

        await using var winningContext = await factory.CreateDbContextAsync();
        await using var staleContext = await factory.CreateDbContextAsync();
        var winningEntity = await FindAsync(winningContext, entityName, id);
        var staleEntity = await FindAsync(staleContext, entityName, id);
        winningContext.Entry(winningEntity).Property(mutableProperty).CurrentValue = winningValue;
        staleContext.Entry(staleEntity).Property(mutableProperty).CurrentValue = staleValue;

        await winningContext.SaveChangesAsync();
        Assert.NotEqual(updatedToken, ReadToken(winningContext, winningEntity));
        Assert.Equal(unchangedVersion, ReadVersion(winningContext, winningEntity));
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => staleContext.SaveChangesAsync());
    }

    private static async Task<object> FindAsync(
        PegasusDbContext context,
        string entityName,
        Guid id)
    {
        var entityType = context.Model.FindEntityType(entityName)
            ?? throw new InvalidOperationException($"EF model entity '{entityName}' is missing.");
        return await context.FindAsync(
            entityType.ClrType,
            new object?[] { id },
            CancellationToken.None)
            ?? throw new InvalidOperationException($"EF model entity '{entityName}' with id '{id}' is missing.");
    }

    private static Guid ReadToken(PegasusDbContext context, object entity) =>
        Assert.IsType<Guid>(context.Entry(entity).Property("ConcurrencyToken").CurrentValue);

    private static long ReadVersion(PegasusDbContext context, object entity) =>
        Assert.IsType<long>(context.Entry(entity).Property("Version").CurrentValue);

    private static async Task<Seed> SeedPrerequisitesAsync(
        IDbContextFactory<PegasusDbContext> factory)
    {
        var organizationId = Guid.NewGuid();
        var lineageId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var caseReceiptId = Guid.NewGuid();
        var triageReceiptId = Guid.NewGuid();
        var stagedReceiptId = Guid.NewGuid();
        var evaluationId = Guid.NewGuid();
        var caseSourceHash = new string('a', 64);
        var triageSourceHash = new string('b', 64);

        await using var context = await factory.CreateDbContextAsync();
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO Organizations (Id, Name, Version)
            VALUES ({organizationId}, {"Concurrency Test Organization"}, {0L})
            """);
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO PrincipalSequenceLineages (Id, CreatedAtUtc)
            VALUES ({lineageId}, {FixedUtcNow})
            """);
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO Principals
                (Id, OrganizationId, Code, SequenceLineageId, IsActive, Version)
            VALUES
                ({principalId}, {organizationId}, {"QDOS"}, {lineageId}, {true}, {0L})
            """);
        await InsertReceiptAsync(
            context,
            caseReceiptId,
            "case-concurrency-source",
            caseSourceHash);
        await InsertReceiptAsync(
            context,
            triageReceiptId,
            "triage-concurrency-source",
            triageSourceHash,
            """{"version":1,"data":[{"source":"email_body","strength":"strong","finding":"accepted_triage_match","signal":"concurrency-triage-match","detail":"Accepted match fixture.","matcherKey":"concurrency-test-matcher","matcherVersion":1}]}""");
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO IntakeStagedReceipts
                (Id, SourceFileName, MediaType, SourceLength, SourceHash, SourceChannel,
                 ExternalReceiptToken, ReceivedAtUtc, Actor, StorageKey, StagedAtUtc)
            VALUES
                ({stagedReceiptId}, {"triage-source.eml"}, {"message/rfc822"}, {1L},
                 {triageSourceHash}, {"mailbox"}, {"triage-concurrency-source"}, {FixedUtcNow},
                 {"staff:concurrency-test"}, {"triage-source-storage"}, {FixedUtcNow})
            """);
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO IntakeEvaluations
                (Id, StagedReceiptId, ProcessedReceiptId, Revision, EvaluatedAtUtc)
            VALUES
                ({evaluationId}, {stagedReceiptId}, {triageReceiptId}, {1}, {FixedUtcNow})
            """);

        return new(caseReceiptId, triageReceiptId, triageSourceHash, evaluationId);
    }

    private static Task<int> InsertReceiptAsync(
        PegasusDbContext context,
        Guid receiptId,
        string externalReceiptToken,
        string sourceHash,
        string evidenceJson = """{"version":1,"data":[]}""") =>
        context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO IntakeReceipts
                (Id, SourceFileName, MediaType, SourceLength, SourceHash, SourceChannel,
                 ExternalReceiptToken, ReceivedAtUtc, ProcessedAtUtc, SourceReaderKey,
                 SourceReaderVersion, Version, Decision, DecisionReason, EvidenceJson,
                 FieldsJson, OcrCandidatesJson)
            VALUES
                ({receiptId}, {"source.eml"}, {"message/rfc822"}, {1L}, {sourceHash},
                 {"mailbox"}, {externalReceiptToken}, {FixedUtcNow}, {FixedUtcNow},
                 {"integration-test"}, {"1"}, {0L}, {"case_created"}, {"test"}, {evidenceJson},
                 {"[]"}, {"[]"})
            """);

    private sealed record Seed(
        Guid CaseReceiptId,
        Guid TriageReceiptId,
        string TriageSourceHash,
        Guid EvaluationId);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
