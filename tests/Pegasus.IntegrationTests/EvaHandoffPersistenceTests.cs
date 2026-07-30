using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Pegasus.Core.Eva;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
[Collection(LocalDbFixtureDefinition.Name)]
public sealed class EvaHandoffPersistenceTests
{
    private static readonly DateTimeOffset Now = new(2031, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task TerminalCasesBlockEveryNewGenerationWithoutRecordingProxyEvidence()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var factory = Factory(database.ConnectionString);
        var caseId = await SeedCaseAsync(factory, "Review", workflowVersion: 7, hiddenCaseVersion: 41);
        var store = Store(factory);
        var terminalStates = new[]
        {
            "PostReportComplete",
            "ProviderCancelled",
            "CollisionEngineersRejected",
            "CreatedInError"
        };

        foreach (var state in terminalStates)
        {
            await using (var context = await factory.CreateDbContextAsync())
            {
                var workflow = await context.CaseWorkflows.SingleAsync(item => item.CaseId == caseId);
                workflow.State = state;
                await context.SaveChangesAsync();
            }

            var result = await store.ExecuteAsync(
                Request(caseId, expectedVersion: 7, operationKey: $"terminal:{state}"),
                CancellationToken.None);

            Assert.Equal(GenerateEvaHandoffOutcome.Blocked, result.Outcome);
            Assert.Contains(result.Reasons, reason => reason.Contains("Terminal cases", StringComparison.Ordinal));
        }

        await using var verification = await factory.CreateDbContextAsync();
        Assert.Empty(await verification.EvaHandoffRevisions.ToArrayAsync());
        Assert.Empty(await verification.EvaFirstHandoffProxies.ToArrayAsync());
        Assert.Empty(await verification.EvaHandoffOperations.ToArrayAsync());
    }

    [Fact]
    public async Task ConcurrentStaleCallersCannotUseDivergedHiddenCaseVersion()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var factory = Factory(database.ConnectionString);
        var caseId = await SeedCaseAsync(factory, "Review", workflowVersion: 7, hiddenCaseVersion: 41);
        var store = Store(factory);

        var results = await Task.WhenAll(
            store.ExecuteAsync(
                Request(caseId, expectedVersion: 41, operationKey: "eva:hidden-case-version:1"),
                CancellationToken.None),
            store.ExecuteAsync(
                Request(caseId, expectedVersion: 41, operationKey: "eva:hidden-case-version:2"),
                CancellationToken.None));

        Assert.All(results, result =>
        {
            Assert.Equal(GenerateEvaHandoffOutcome.Conflict, result.Outcome);
            Assert.Contains(
                result.Reasons,
                reason => reason.Contains("case changed", StringComparison.OrdinalIgnoreCase));
        });
        await using var verification = await factory.CreateDbContextAsync();
        Assert.Empty(await verification.EvaHandoffOperations.ToArrayAsync());
    }

    [Fact]
    public async Task RevisionDownloadReturnsOnlyExactIntegrityCheckedPersistedCaseRevision()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var factory = Factory(database.ConnectionString);
        var caseId = await SeedCaseAsync(factory, "Review", workflowVersion: 7, hiddenCaseVersion: 41);
        var content = "persisted EVA bundle bytes"u8.ToArray();
        var sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        await using (var context = await factory.CreateDbContextAsync())
        {
            context.EvaHandoffRevisions.Add(new()
            {
                Id = Guid.NewGuid(),
                CaseId = caseId,
                Revision = 3,
                AcceptedCaseVersion = 7,
                SchemaVersion = EvaBundleSchema.SchemaVersion,
                InputFingerprint = sha256,
                FileName = "EVA-QDOS001.zip",
                BundleContent = content,
                BundleSha256 = sha256,
                JsonContent = "{}"u8.ToArray(),
                JsonSha256 = new string('b', 64),
                ProvenanceContent = "{}"u8.ToArray(),
                ProvenanceSha256 = new string('c', 64),
                ManifestContent = "manifest"u8.ToArray(),
                GeneratedAtUtc = Now,
                GeneratedBy = "staff:test"
            });
            await context.SaveChangesAsync();
        }

        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var store = Store(factory);
        var artifact = await store.GetRevisionAsync(caseId, 3, actor);

        Assert.NotNull(artifact);
        Assert.Equal(3, artifact.Revision);
        Assert.Equal("EVA-QDOS001.zip", artifact.FileName);
        Assert.Equal("application/zip", EvaHandoffRevisionArtifact.MediaType);
        Assert.Equal(content.LongLength, artifact.ContentLength);
        Assert.Equal(content, artifact.Content);
        Assert.Equal(sha256, artifact.BundleSha256);
        Assert.Null(await store.GetRevisionAsync(caseId, 2, actor));
        Assert.Null(await store.GetRevisionAsync(Guid.NewGuid(), 3, actor));

        await using (var context = await factory.CreateDbContextAsync())
        {
            var revision = await context.EvaHandoffRevisions
                .SingleAsync(item => item.CaseId == caseId && item.Revision == 3);
            revision.FileName = "../outside.zip";
            await context.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<InvalidDataException>(
            () => store.GetRevisionAsync(caseId, 3, actor));

        await using (var context = await factory.CreateDbContextAsync())
        {
            var revision = await context.EvaHandoffRevisions
                .SingleAsync(item => item.CaseId == caseId && item.Revision == 3);
            revision.FileName = "EVA-QDOS001.zip";
            revision.BundleSha256 = new string('f', 64);
            await context.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<InvalidDataException>(
            () => store.GetRevisionAsync(caseId, 3, actor));
    }

    [Fact]
    public async Task NonStaffActorIsRejectedBeforePersistenceOrProxyAccess()
    {
        var store = new EvaHandoffStore(
            null!,
            null!,
            null!,
            null!,
            null!,
            EvaMappingAcceptance.Unaccepted,
            TimeProvider.System);
        var request = Request(Guid.NewGuid(), 0, "eva:unauthorized") with
        {
            Actor = ActionActor.SystemWorker("eva-test-worker")
        };

        await Assert.ThrowsAsync<StaffAuthorizationException>(
            () => store.ExecuteAsync(request, CancellationToken.None));
        await Assert.ThrowsAsync<StaffAuthorizationException>(
            () => store.GetRevisionAsync(
                Guid.NewGuid(),
                1,
                ActionActor.SystemWorker("eva-test-worker"),
                CancellationToken.None));
    }

    private static EvaHandoffStore Store(IDbContextFactory<PegasusDbContext> factory) => new(
        factory,
        null!,
        null!,
        null!,
        null!,
        EvaMappingAcceptance.Unaccepted,
        TimeProvider.System);

    private static GenerateEvaHandoffRequest Request(
        Guid caseId,
        long expectedVersion,
        string operationKey)
    {
        var overview = Guid.NewGuid();
        var damage = Guid.NewGuid();
        return new(
            caseId,
            expectedVersion,
            overview,
            damage,
            [overview, damage],
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]),
            operationKey,
            "Generate the approved offline EVA handoff.",
            "unused-server-lease-token");
    }

    private static PooledDbContextFactory<PegasusDbContext> Factory(string connectionString)
    {
        var options = new DbContextOptionsBuilder<PegasusDbContext>()
            .UseOpenIddict()
            .UseSqlServer(connectionString)
            .Options;
        return new(options);
    }

    private static async Task<Guid> SeedCaseAsync(
        IDbContextFactory<PegasusDbContext> factory,
        string workflowState,
        long workflowVersion,
        long hiddenCaseVersion)
    {
        var organizationId = Guid.NewGuid();
        var lineageId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var sourceHash = new string('a', 64);
        var emptyEnvelope = """{"version":1,"data":[]}""";

        await using var context = await factory.CreateDbContextAsync();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Organizations (Id, Name, Version) VALUES ({organizationId}, {"QDOS provider"}, {0L})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO PrincipalSequenceLineages (Id, CreatedAtUtc) VALUES ({lineageId}, {Now})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Principals (Id, OrganizationId, Code, SequenceLineageId, IsActive, Version) VALUES ({principalId}, {organizationId}, {"QDOS"}, {lineageId}, {true}, {0L})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO IntakeReceipts (Id, SourceFileName, MediaType, SourceLength, SourceHash, SourceChannel, ExternalReceiptToken, ReceivedAtUtc, ProcessedAtUtc, SourceReaderKey, SourceReaderVersion, ExtractionPolicyKey, ExtractionPolicyVersion, Version, Decision, DecisionReason, EvidenceJson, FieldsJson, OcrCandidatesJson) VALUES ({receiptId}, {"qdos.eml"}, {"message/rfc822"}, {100L}, {sourceHash}, {"mailbox"}, {"eva-fixture"}, {Now}, {Now}, {"fixture-reader"}, {"1"}, {"qdos_instruction"}, {1}, {0L}, {"draft_ready"}, {"Ready fixture"}, {emptyEnvelope}, {emptyEnvelope}, {emptyEnvelope})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Cases (Id, PrincipalId, SequenceLineageId, Year, Sequence, Reference, Type, InitialState, CustodyState, OriginIntakeReceiptId, InstructionComplete, ImagesComplete, InstructionConfirmedByStaff, ImagesConfirmedByStaff, CreatedAtUtc, Version, ConcurrencyToken, CustodyConfirmedAtUtc) VALUES ({caseId}, {principalId}, {lineageId}, {2031}, {1}, {"QDOS001"}, {"inspection"}, {"review"}, {"confirmed"}, {receiptId}, {true}, {true}, {true}, {true}, {Now}, {hiddenCaseVersion}, {Guid.NewGuid()}, {Now})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CaseWorkflows (CaseId, State, Version, ConcurrencyToken) VALUES ({caseId}, {workflowState}, {workflowVersion}, {Guid.NewGuid()})");
        return caseId;
    }
}
