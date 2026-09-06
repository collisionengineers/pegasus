using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Custody;
using Pegasus.Core.Identity;
using Pegasus.Core.Reports;
using Pegasus.Core.Vehicle;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// ENG-013: the DVLA and DVSA lookup is enrichment. What it learns fills the
/// case's own empty vehicle fields, and never displaces what the documents
/// already said — which is what stops one case showing two rival mileages.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class VehicleLookupGapFillTests
{
    private static readonly DateTimeOffset FixedUtcNow =
        new(2026, 8, 22, 18, 47, 0, TimeSpan.Zero);

    [Fact]
    public async Task ALookupFillsAMileageTheDocumentsNeverCarried()
    {
        await using var database = await CreateDatabaseAsync();
        var caseId = await SeedCaseAsync(database);

        await RecordLookupAsync(database, caseId);

        Assert.Equal("121823", await database.ScalarAsync<string>(
            $"SELECT Value FROM CaseDataFields WHERE CaseId = '{caseId:D}' AND FieldName = 'vehicle_mileage' AND ValueKind = 'suggestion'"));
        Assert.Equal("Miles", await database.ScalarAsync<string>(
            $"SELECT Value FROM CaseDataFields WHERE CaseId = '{caseId:D}' AND FieldName = 'vehicle_mileage_unit' AND ValueKind = 'suggestion'"));
        Assert.Equal("vehicle_lookup", await database.ScalarAsync<string>(
            $"SELECT SourceKind FROM CaseDataFields WHERE CaseId = '{caseId:D}' AND FieldName = 'vehicle_mileage' AND ValueKind = 'suggestion'"));
        // A suggestion, never an accepted value: only staff acceptance
        // promotes it, and only an accepted value can reach an EVA hand-off.
        Assert.Equal(0, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseDataFields WHERE CaseId = '{caseId:D}' AND FieldName = 'vehicle_mileage' AND ValueKind <> 'suggestion'"));
    }

    [Fact]
    public async Task AnExtractedMakeOutranksTheLookupsOwn()
    {
        await using var database = await CreateDatabaseAsync();
        var caseId = await SeedCaseAsync(database);
        await using (var context = await database.CreateContextAsync())
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO CaseDataFields (CaseId, FieldName, ValueKind, ValueType, Value, SourceKind, SourceIdentity, SourceLabel, PolicyKey, PolicyVersion) VALUES ({caseId}, {"vehicle_make"}, {"fact"}, {"text"}, {"MAZDA"}, {"intake_evidence"}, {"instruction.pdf"}, {"page 1"}, {"extraction"}, {1})");
        }

        await RecordLookupAsync(database, caseId);

        // Both rows exist — the lookup's finding is not thrown away — but the
        // extracted fact is what the case reads, because CaseField.Current
        // ranks Confirmed then Fact then Suggestion.
        Assert.Equal("MAZDA", await database.ScalarAsync<string>(
            $"SELECT Value FROM CaseDataFields WHERE CaseId = '{caseId:D}' AND FieldName = 'vehicle_make' AND ValueKind = 'fact'"));
        Assert.Equal("RENAULT", await database.ScalarAsync<string>(
            $"SELECT Value FROM CaseDataFields WHERE CaseId = '{caseId:D}' AND FieldName = 'vehicle_make' AND ValueKind = 'suggestion'"));
    }

    [Fact]
    public async Task ASecondLookupDoesNotDuplicateOrOverwriteTheFirst()
    {
        await using var database = await CreateDatabaseAsync();
        var caseId = await SeedCaseAsync(database);

        await RecordLookupAsync(database, caseId);
        await RecordLookupAsync(database, caseId, mileage: 999_999);

        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseDataFields WHERE CaseId = '{caseId:D}' AND FieldName = 'vehicle_mileage'"));
        Assert.Equal("121823", await database.ScalarAsync<string>(
            $"SELECT Value FROM CaseDataFields WHERE CaseId = '{caseId:D}' AND FieldName = 'vehicle_mileage' AND ValueKind = 'suggestion'"));
    }

    [Fact]
    public async Task AcceptingOneSuggestionClearsOnlyThatFieldAndMileageIsAtomic()
    {
        await using var database = await CreateDatabaseAsync();
        var caseId = await SeedCaseAsync(database);
        await RecordLookupAsync(database, caseId);
        var observationId = await database.ScalarAsync<Guid>(
            $"SELECT TOP (1) Id FROM VehicleLookupObservations WHERE Registration = 'ST66BCE' ORDER BY RecordedAtUtc DESC");
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var caseVersion = await database.ScalarAsync<long>(
            $"SELECT Version FROM CaseWorkflows WHERE CaseId = '{caseId:D}'");

        await using var scope = database.CreateAsyncScope();
        var leases = scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>();
        var accept = scope.ServiceProvider.GetRequiredService<IAcceptVehicleSuggestion>();
        var makeLease = await leases.ClaimAsync(
            new(caseId, caseVersion, actor, "gap-fill-make-lease"),
            CancellationToken.None);
        var acceptedMake = await accept.ExecuteAsync(
            new(
                caseId,
                makeLease.Version,
                observationId,
                VehicleSuggestionDecision.Accept,
                null,
                actor,
                "gap-fill-accept-make",
                "Accepted the make suggestion.",
                makeLease.Token)
            {
                Field = VehicleSuggestionField.Make
            },
            CancellationToken.None);

        Assert.Equal("RENAULT", acceptedMake.Values.Make);
        Assert.Equal(0, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseDataFields WHERE CaseId = '{caseId:D}' AND FieldName = 'vehicle_make' AND ValueKind = 'suggestion'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseDataFields WHERE CaseId = '{caseId:D}' AND FieldName = 'vehicle_model' AND ValueKind = 'suggestion'"));

        var mileageLease = await leases.ClaimAsync(
            new(caseId, acceptedMake.ResultingCaseVersion, actor, "gap-fill-mileage-lease"),
            CancellationToken.None);
        await accept.ExecuteAsync(
            new(
                caseId,
                mileageLease.Version,
                observationId,
                VehicleSuggestionDecision.Accept,
                null,
                actor,
                "gap-fill-accept-mileage",
                "Accepted the mileage suggestion.",
                mileageLease.Token)
            {
                Field = VehicleSuggestionField.Mileage
            },
            CancellationToken.None);

        Assert.Equal(0, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseDataFields WHERE CaseId = '{caseId:D}' AND FieldName IN ('vehicle_mileage', 'vehicle_mileage_unit') AND ValueKind = 'suggestion'"));
        Assert.Equal(2, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseDataFields WHERE CaseId = '{caseId:D}' AND FieldName IN ('vehicle_mileage', 'vehicle_mileage_unit') AND ValueKind = 'confirmed' AND SourceIdentity = '{observationId:D}'"));
    }

    /// <summary>
    /// Stream A review (comment 5560667174): accepting a confirmed vehicle
    /// suggestion changes frozen report inputs, so it stales the Case's
    /// current generation inside the same serializable acceptance
    /// transaction. Replay returns before staling, a superseded generation
    /// never moves, and a lookup alone — which only records suggestions —
    /// stales nothing.
    /// </summary>
    [Fact]
    public async Task AcceptingASuggestionStalesOnlyTheCurrentGeneration()
    {
        await using var database = await CreateDatabaseAsync();
        var caseId = await SeedCaseAsync(database);
        await RecordLookupAsync(database, caseId);
        var (currentId, supersededId) = await SeedGenerationsAsync(database, caseId);
        var observationId = await database.ScalarAsync<Guid>(
            $"SELECT TOP (1) Id FROM VehicleLookupObservations WHERE Registration = 'ST66BCE' ORDER BY RecordedAtUtc DESC");
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var caseVersion = await database.ScalarAsync<long>(
            $"SELECT Version FROM CaseWorkflows WHERE CaseId = '{caseId:D}'");

        await using var scope = database.CreateAsyncScope();
        var leases = scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>();
        var accept = scope.ServiceProvider.GetRequiredService<IAcceptVehicleSuggestion>();
        var lease = await leases.ClaimAsync(
            new(caseId, caseVersion, actor, "stale-accept-lease"), CancellationToken.None);
        var request = new AcceptVehicleSuggestionCommand(
            caseId,
            lease.Version,
            observationId,
            VehicleSuggestionDecision.Accept,
            null,
            actor,
            "stale-accept-make",
            "Accepted the make suggestion.",
            lease.Token)
        {
            Field = VehicleSuggestionField.Make
        };
        var accepted = await accept.ExecuteAsync(request, CancellationToken.None);

        Assert.Equal("RENAULT", accepted.Values.Make);
        Assert.Equal("Stale", await database.ScalarAsync<string>(
            $"SELECT State FROM CaseReportGenerations WHERE Id = '{currentId:D}'"));
        Assert.Equal("Confirmed", await database.ScalarAsync<string>(
            $"SELECT State FROM CaseReportGenerations WHERE Id = '{supersededId:D}'"));
        Assert.Equal(1, await StaleRowCountAsync(database, caseId));

        // Replay of the same acceptance returns before any mutation: the
        // stale row count does not move.
        var replayed = await accept.ExecuteAsync(request, CancellationToken.None);
        Assert.True(replayed.IsReplay);
        Assert.Equal(accepted.ConfirmationId, replayed.ConfirmationId);
        Assert.Equal(1, await StaleRowCountAsync(database, caseId));
    }

    [Fact]
    public async Task ALookupAloneStalesNoGeneration()
    {
        await using var database = await CreateDatabaseAsync();
        var caseId = await SeedCaseAsync(database);
        var (currentId, _) = await SeedGenerationsAsync(database, caseId);

        await RecordLookupAsync(database, caseId);

        Assert.Equal("Confirmed", await database.ScalarAsync<string>(
            $"SELECT State FROM CaseReportGenerations WHERE Id = '{currentId:D}'"));
        Assert.Equal(0, await StaleRowCountAsync(database, caseId));
    }

    private static async Task<(Guid CurrentId, Guid SupersededId)> SeedGenerationsAsync(
        LocalDbTestDatabase database,
        Guid caseId)
    {
        await using var context = await database.CreateContextAsync();
        var currentId = Guid.NewGuid();
        var supersededId = Guid.NewGuid();
        context.AddRange(
            new CaseReportGenerationEntity
            {
                Id = supersededId,
                CaseId = caseId,
                CaseVersion = 0,
                SnapshotHash = new string('1', 64),
                SnapshotJson = "{\"operationKey\":\"seed-generation-superseded\"}",
                TemplateVersion = "assessment-report/v1",
                RendererVersion = "playwright/v1",
                State = nameof(CaseReportGenerationState.Confirmed),
                GeneratedAtUtc = FixedUtcNow,
                Version = 1
            },
            new CaseReportGenerationEntity
            {
                Id = currentId,
                CaseId = caseId,
                CaseVersion = 0,
                SnapshotHash = new string('2', 64),
                SnapshotJson = "{\"operationKey\":\"seed-generation-current\"}",
                TemplateVersion = "assessment-report/v1",
                RendererVersion = "playwright/v1",
                State = nameof(CaseReportGenerationState.Confirmed),
                GeneratedAtUtc = FixedUtcNow.AddMinutes(1),
                Version = 1
            });
        await context.SaveChangesAsync();
        await database.ExecuteAsync(
            $"UPDATE CaseReportGenerations SET SupersededById = '{Guid.NewGuid():D}' WHERE Id = '{supersededId:D}'");
        return (currentId, supersededId);
    }

    private static Task<int> StaleRowCountAsync(LocalDbTestDatabase database, Guid caseId) =>
        database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM ActionHistory WHERE AggregateType = 'case' AND AggregateId = '{caseId:D}' AND EventKind = 'case_report_generation_stale'");

    private static async Task RecordLookupAsync(
        LocalDbTestDatabase database,
        Guid caseId,
        long mileage = 121_823)
    {
        var workItemId = Guid.NewGuid();
        await using (var context = await database.CreateContextAsync())
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO ExternalWorkItems (Id, CaseId, Kind, OperationKey, State, AttemptCount, DueAtUtc) VALUES ({workItemId}, {caseId}, {ExternalWorkKinds.VehicleLookup}, {$"gap-fill-{workItemId:N}"}, {"pending"}, {0}, {FixedUtcNow})");
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO VehicleLookupRequests (WorkItemId, CaseId, Registration, OperationKey, RequestFingerprint, RequestedByKind, RequestedBySubjectId, RequestedByRolesJson, RequestedAtUtc, ResultingCaseVersion) VALUES ({workItemId}, {caseId}, {"ST66BCE"}, {$"gap-fill-{workItemId:N}"}, {new string('0', 64)}, {ActorKind.Automation.ToString()}, {"vehicle-lookup-reconciliation"}, {"[]"}, {FixedUtcNow}, {0L})");
        }

        await using var scope = database.CreateAsyncScope();
        var workStore = scope.ServiceProvider.GetRequiredService<IVehicleLookupWorkStore>();
        var claimed = Assert.IsType<VehicleLookupWorkItem>(
            await workStore.ClaimProcessingAsync(
                workItemId,
                FixedUtcNow,
                TimeSpan.FromMinutes(5),
                CancellationToken.None));
        var result = new VehicleLookupResult(
            "ST66BCE",
            VehicleLookupOutcome.Current,
            "offline-replay",
            "fixture-v1",
            $"gap-fill-response-{workItemId:N}",
            FixedUtcNow,
            FixedUtcNow,
            FixedUtcNow,
            new("RENAULT", "CAPTUR", 2016, 1_461, "DIESEL"),
            [new(new(2025, 9, 25), "PASSED", new(2026, 9, 24), mileage, VehicleMileageUnit.Miles)],
            null);
        await workStore.RecordOutcomeAsync(
            workItemId,
            claimed.LeaseToken!,
            new(result, VehicleMileagePolicy.Calculate(result.MotTests)),
            VehicleLookupWorkState.Completed,
            null,
            FixedUtcNow,
            CancellationToken.None);
    }

    private static Task<LocalDbTestDatabase> CreateDatabaseAsync() =>
        LocalDbTestDatabase.CreateAsync(
            configureServices: services =>
                services.AddSingleton(VehicleLookupAvailability.DevelopmentOfflineReplay));

    private static async Task<Guid> SeedCaseAsync(LocalDbTestDatabase database)
    {
        var organizationId = Guid.NewGuid();
        var lineageId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var sequence = Math.Abs(caseId.GetHashCode() % 999) + 1;
        await using var context = await database.CreateContextAsync();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Organizations (Id, Name, Version) VALUES ({organizationId}, {$"Gap fill test {organizationId:N}"}, {0L})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO PrincipalSequenceLineages (Id, CreatedAtUtc) VALUES ({lineageId}, {FixedUtcNow})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Principals (Id, OrganizationId, Code, SequenceLineageId, IsActive, Version) VALUES ({principalId}, {organizationId}, {$"G{sequence % 997:D3}"}, {lineageId}, {true}, {0L})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO IntakeReceipts (Id, SourceFileName, MediaType, SourceLength, SourceHash, SourceChannel, ExternalReceiptToken, ReceivedAtUtc, ProcessedAtUtc, SourceReaderKey, SourceReaderVersion, Version, Decision, DecisionReason, EvidenceJson, FieldsJson, OcrCandidatesJson) VALUES ({receiptId}, {"gap-fill.eml"}, {"message/rfc822"}, {1L}, {1.ToString("X64", CultureInfo.InvariantCulture)}, {"manual_upload"}, {receiptId.ToString("D")}, {FixedUtcNow}, {FixedUtcNow}, {"gap-fill-reader"}, {"1"}, {0L}, {"case_created"}, {"Gap fill fixture"}, {"{\"version\":1,\"data\":[]}"}, {"{\"version\":1,\"data\":[]}"}, {"{\"version\":1,\"data\":[]}"})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Cases (Id, PrincipalId, SequenceLineageId, Year, Sequence, Reference, Type, InitialState, CustodyState, OriginIntakeReceiptId, InstructionComplete, ImagesComplete, InstructionConfirmedByStaff, ImagesConfirmedByStaff, CreatedAtUtc, Version, ConcurrencyToken) VALUES ({caseId}, {principalId}, {lineageId}, {2026}, {sequence}, {$"GAP{caseId:N}"[..10].ToUpperInvariant()}, {"inspection"}, {"review"}, {"confirmed"}, {receiptId}, {true}, {true}, {true}, {true}, {FixedUtcNow}, {0L}, {Guid.NewGuid()})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CaseWorkflows (CaseId, State, Version, ConcurrencyToken) VALUES ({caseId}, {CaseLifecycleState.Review.ToString()}, {0L}, {Guid.NewGuid()})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CaseDataSnapshots (CaseId, OriginIntakeReceiptId, OriginSourceChannel, OriginExternalReceiptToken, OriginSourceHash, OriginReceivedAtUtc, SourceReaderKey, SourceReaderVersion, CompletenessPolicyKey, CompletenessPolicyVersion, CompletenessPolicySatisfied, AcceptedAtUtc) VALUES ({caseId}, {receiptId}, {"manual_upload"}, {"gap-fill-source"}, {new string('1', 64)}, {FixedUtcNow}, {"gap-fill-reader"}, {"1"}, {"gap-fill-completeness"}, {1}, {true}, {FixedUtcNow})");
        return caseId;
    }
}
