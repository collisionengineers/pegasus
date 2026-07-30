using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Eva;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Vehicle;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Custody;
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

    [Fact]
    public async Task StaffConfirmedThirdPartyVehicleImagesAreExcludedFromPreparationAndGeneratedBundle()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var factory = Factory(database.ConnectionString);
        var caseId = await SeedCaseAsync(factory, "Review", workflowVersion: 7, hiddenCaseVersion: 41);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        const string editLeaseToken = "eva-third-party-vehicle-lease";
        var leaseHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(editLeaseToken))).ToLowerInvariant();
        await using (var context = await factory.CreateDbContextAsync())
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE CaseWorkflows SET EditLeaseToken = {editLeaseToken}, EditLeaseTokenHash = {leaseHash}, EditLeaseRequestHash = {leaseHash}, EditLeaseHolder = {actor.SubjectId}, EditLeaseOperationKey = {"eva-third-party-vehicle"}, EditLeaseExpiresAtUtc = {DateTimeOffset.UtcNow.AddMinutes(5)} WHERE CaseId = {caseId}");
        }

        var custodyRoot = Path.Combine(Path.GetTempPath(), "pegasus-eva-third-party", Guid.NewGuid().ToString("N"));
        var contentStore = new LocalDocumentContentStore(custodyRoot);
        try
        {
            var first = await SeedImageAsync(
                factory,
                contentStore,
                caseId,
                "first.jpg",
                Now.AddMinutes(-3),
                thirdPartyVehicleConfirmed: false);
            var excluded = await SeedImageAsync(
                factory,
                contentStore,
                caseId,
                "third-party.jpg",
                Now.AddMinutes(-2),
                thirdPartyVehicleConfirmed: true);
            var second = await SeedImageAsync(
                factory,
                contentStore,
                caseId,
                "second.jpg",
                Now.AddMinutes(-1),
                thirdPartyVehicleConfirmed: false);

            var store = new EvaHandoffStore(
                factory,
                new FixedCaseDataQueries(AcceptedCaseData(caseId, version: 7)),
                new FixedVehicleEvidenceQueries(ConfirmedVehicle(caseId)),
                contentStore,
                new RecordingEvaHandoffProxy(),
                new(
                    CaseEvaMapping.MappingKey,
                    CaseEvaMapping.MappingVersion,
                    "test-accepted-eva-mapping"),
                TimeProvider.System);

            var preparation = await store.GetPreparationAsync(caseId);

            Assert.NotNull(preparation);
            Assert.True(preparation.CanGenerate);
            Assert.Equal([first.OccurrenceId, second.OccurrenceId], preparation.Images.Select(image => image.OccurrenceId));
            Assert.DoesNotContain(preparation.Images, image => image.OccurrenceId == excluded.OccurrenceId);

            var generated = await store.ExecuteAsync(
                new(
                    caseId,
                    7,
                    actor,
                    "eva:exclude-confirmed-third-party-vehicle",
                    "Generate a custody-safe offline EVA handoff.",
                    editLeaseToken),
                CancellationToken.None);

            Assert.Equal(GenerateEvaHandoffOutcome.Generated, generated.Outcome);
            Assert.NotNull(generated.Bundle);
            using var provenance = JsonDocument.Parse(generated.Bundle.ProvenanceContent);
            var generatedOccurrenceIds = provenance.RootElement
                .GetProperty("images")
                .EnumerateArray()
                .Select(image => image.GetProperty("occurrenceId").GetGuid())
                .ToArray();
            Assert.Equal(
                [first.OccurrenceId, second.OccurrenceId],
                generatedOccurrenceIds);
            Assert.DoesNotContain(excluded.OccurrenceId, generatedOccurrenceIds);
        }
        finally
        {
            if (Directory.Exists(custodyRoot))
            {
                Directory.Delete(custodyRoot, recursive: true);
            }
        }
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
        return new(
            caseId,
            expectedVersion,
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]),
            operationKey,
            "Generate the approved offline EVA handoff.",
            "unused-server-lease-token");
    }

    private static PooledDbContextFactory<PegasusDbContext> Factory(string connectionString)
    {
        var options = new DbContextOptionsBuilder<PegasusDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new(options);
    }

    private static async Task<SeededImage> SeedImageAsync(
        IDbContextFactory<PegasusDbContext> factory,
        LocalDocumentContentStore contentStore,
        Guid caseId,
        string fileName,
        DateTimeOffset recordedAtUtc,
        bool thirdPartyVehicleConfirmed)
    {
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var occurrenceId = Guid.NewGuid();
        var content = Encoding.UTF8.GetBytes($"image content for {fileName}");
        var sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

        await using var context = await factory.CreateDbContextAsync();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CaseDocuments (Id, CaseId, SourceOccurrenceIdentity) VALUES ({documentId}, {caseId}, {$"fixture:{fileName}"})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO DocumentVersions (Id, DocumentId, Version, FileName, MediaType, ContentLength, Sha256, CustodyStatus, CreatedAtUtc, CreatedBy, IsCurrent, IsLogicallyRemoved) VALUES ({versionId}, {documentId}, {1}, {fileName}, {"image/jpeg"}, {(long)content.Length}, {sha256}, {"Confirmed"}, {recordedAtUtc}, {"staff:fixture"}, {true}, {false})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO DocumentOccurrences (Id, CaseId, DocumentId, VersionId, SemanticRole, Source, SourceOccurrenceIdentity, RecordedAtUtc, OperationKey, ThirdPartyVehicleConfirmedAtUtc, ThirdPartyVehicleConfirmationReason, ThirdPartyVehicleConfirmationOperationKey) VALUES ({occurrenceId}, {caseId}, {documentId}, {versionId}, {"Image"}, {"StaffUpload"}, {$"fixture:{fileName}"}, {recordedAtUtc}, {$"fixture:{fileName}"}, {(thirdPartyVehicleConfirmed ? recordedAtUtc : null)}, {(thirdPartyVehicleConfirmed ? "Staff confirmed this is third-party vehicle evidence." : null)}, {(thirdPartyVehicleConfirmed ? "fixture:third-party-vehicle" : null)})");
        if (!thirdPartyVehicleConfirmed)
        {
            await contentStore.StoreAsync(caseId, versionId, content, sha256, CancellationToken.None);
        }

        return new(occurrenceId, versionId);
    }

    private static CaseDataProjection AcceptedCaseData(Guid caseId, long version) => new(
        new(caseId, "QDOS", 2031, 1, "QDOS001"),
        new(
            Guid.NewGuid(),
            IntakeSourceChannel.ManualUpload,
            "eva-fixture",
            new string('a', 64),
            Now,
            "fixture-reader",
            "1",
            "fixture-policy",
            1),
        Now,
        version,
        CaseLifecycleState.Review,
        new(
            new(true, true, true, true),
            new(true, "fixture-completeness", 1)),
        new(Field("QDOS provider")),
        new(Field("Fixture claimant")),
        new(Field("CLAIM-001")),
        new(
            Field("AB12CDE"),
            Field("Fixture"),
            Field("Vehicle"),
            Field(12000L),
            Field("miles")),
        new(Field(new DateOnly(2031, 4, 1)), Field("Fixture accident circumstances")),
        new(Field("Fixture contact"), Field("fixture@example.test"), Field("01234567890")),
        new(Field(new DateOnly(2031, 4, 2)), Field("VAT registered")),
        new(
            Field(new DateOnly(2031, 4, 3)),
            Field(new DateOnly(2031, 4, 10)),
            Field(CaseEvaMapping.ImageBasedAssessment),
            Field(CaseInspectionMode.ImageBasedAssessment)));

    private static CaseVehicleEvidence ConfirmedVehicle(Guid caseId) => new(
        caseId,
        new(
            VehicleField("AB12CDE"),
            VehicleField("Fixture"),
            VehicleField("Vehicle"),
            VehicleField(12000L),
            VehicleField(VehicleMileageUnit.Miles)),
        null,
        [],
        []);

    private static CaseField<T> Field<T>(T value)
        where T : notnull => new(
            new(
                value,
                CaseDataValueKind.Confirmed,
                new(CaseDataSourceKind.CaseAcceptance, "eva-fixture", "Fixture evidence", "fixture", 1),
                "staff:fixture",
                Now),
            null,
            null);

    private static ConfirmedVehicleField<T> VehicleField<T>(T value)
        where T : notnull => new(
            value,
            "staff-confirmation",
            "eva-fixture-vehicle",
            "Fixture vehicle evidence",
            "fixture-vehicle",
            1,
            "staff:fixture",
            Now,
            null);

    private sealed record SeededImage(Guid OccurrenceId, Guid VersionId);

    private sealed class FixedCaseDataQueries(CaseDataProjection data) : ICaseDataQueries
    {
        public Task<CaseDataProjection?> GetAsync(Guid caseId, CancellationToken cancellationToken) =>
            Task.FromResult<CaseDataProjection?>(data.Identity.CaseId == caseId ? data : null);
    }

    private sealed class FixedVehicleEvidenceQueries(CaseVehicleEvidence evidence) : IVehicleEvidenceQueries
    {
        public Task<CaseVehicleEvidence?> GetAsync(Guid caseId, CancellationToken cancellationToken) =>
            Task.FromResult<CaseVehicleEvidence?>(evidence.CaseId == caseId ? evidence : null);
    }

    private sealed class RecordingEvaHandoffProxy : IEvaHandoffProxy
    {
        public Task<EvaHandoffProxyReceipt> RecordFirstGenerationAsync(
            EvaHandoffProxyRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new EvaHandoffProxyReceipt(
                "test-offline-proxy",
                "1",
                DateTimeOffset.UtcNow,
                ClaimsExternalDelivery: false,
                ClaimsEngineerAssignment: false));
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
