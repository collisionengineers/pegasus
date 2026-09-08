using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Pegasus.IntegrationTests;

/// <summary>
/// ENG-013's backfill. The code fills a case's empty vehicle fields when a
/// lookup runs, which only ever helps cases whose lookup is still to come.
/// Every case already in the estate had its lookup first, so its findings sat
/// on the observation and nowhere else — QDOS26011 read "Not recorded" for
/// mileage over an observation holding 121,823 miles.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class VehicleLookupBackfillTests
{
    [Fact]
    public async Task TheMigrationGivesAnAlreadyLookedUpCaseItsMileage()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false, useTemplate: false);
        await MigrateToPreviousAsync(database);
        var caseId = await SeedCaseWithObservationAsync(database, extractedMake: null);

        await ApplyBackfillAsync(database);

        Assert.Equal("121823", await ReadAsync(database, caseId, "vehicle_mileage", "suggestion"));
        Assert.Equal("Miles", await ReadAsync(database, caseId, "vehicle_mileage_unit", "suggestion"));
        Assert.Equal("MAZDA", await ReadAsync(database, caseId, "vehicle_make", "suggestion"));
        Assert.Equal("latest-mot-observation", await database.ScalarAsync<string>(
            $"SELECT PolicyKey FROM CaseDataFields WHERE CaseId = '{caseId:D}' AND FieldName = 'vehicle_mileage' AND ValueKind = 'suggestion'"));
    }

    [Fact]
    public async Task AnExtractedFactIsNotDisplacedAndNotDuplicated()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false, useTemplate: false);
        await MigrateToPreviousAsync(database);
        var caseId = await SeedCaseWithObservationAsync(database, extractedMake: "HONDA");

        await ApplyBackfillAsync(database);

        // The fact stands; the lookup's own value lands only at the suggestion
        // tier, which CaseField.Current ranks below it.
        Assert.Equal("HONDA", await ReadAsync(database, caseId, "vehicle_make", "fact"));
        Assert.Equal("MAZDA", await ReadAsync(database, caseId, "vehicle_make", "suggestion"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseDataFields WHERE CaseId = '{caseId:D}' AND FieldName = 'vehicle_make' AND ValueKind = 'fact'"));
    }

    [Fact]
    public async Task RunningItTwiceWritesNothingTheSecondTime()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false, useTemplate: false);
        await MigrateToPreviousAsync(database);
        var caseId = await SeedCaseWithObservationAsync(database, extractedMake: null);

        await ApplyBackfillAsync(database);
        // The migration is recorded as applied, so a second call is a no-op;
        // what this guards is the NOT EXISTS clause, exercised by the code path
        // that runs on every subsequent lookup.
        await ApplyBackfillAsync(database);

        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseDataFields WHERE CaseId = '{caseId:D}' AND FieldName = 'vehicle_mileage'"));
    }

    /// <summary>
    /// Runs the estate's own migration, not a paraphrase of it. The database
    /// is brought up to the migration immediately before this one, seeded with
    /// a case whose lookup has already happened - the exact shape of every case
    /// in production when ENG-013 shipped - and only then migrated to head.
    /// </summary>
    private static async Task ApplyBackfillAsync(LocalDbTestDatabase database)
    {
        await using var context = await database.CreateContextAsync();
        await context.Database.MigrateAsync();
    }

    private static async Task MigrateToPreviousAsync(LocalDbTestDatabase database)
    {
        await using var context = await database.CreateContextAsync();
        await context.GetService<IMigrator>()
            .MigrateAsync("20260822195419_CorrectIntakePhotographSemanticRole");
    }

    private static Task<string> ReadAsync(
        LocalDbTestDatabase database,
        Guid caseId,
        string field,
        string kind) =>
        database.ScalarAsync<string>(
            $"SELECT Value FROM CaseDataFields WHERE CaseId = '{caseId:D}' AND FieldName = '{field}' AND ValueKind = '{kind}'");

    private static async Task<Guid> SeedCaseWithObservationAsync(
        LocalDbTestDatabase database,
        string? extractedMake)
    {
        var now = new DateTimeOffset(2026, 8, 22, 18, 47, 0, TimeSpan.Zero);
        var organizationId = Guid.NewGuid();
        var lineageId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var workItemId = Guid.NewGuid();
        var observationId = Guid.NewGuid();
        var sequence = Math.Abs(caseId.GetHashCode() % 999) + 1;

        await using var context = await database.CreateContextAsync();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Organizations (Id, Name, Version) VALUES ({organizationId}, {$"Backfill {organizationId:N}"}, {0L})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO PrincipalSequenceLineages (Id, CreatedAtUtc) VALUES ({lineageId}, {now})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Principals (Id, OrganizationId, Code, SequenceLineageId, IsActive, Version) VALUES ({principalId}, {organizationId}, {$"B{sequence % 997:D3}"}, {lineageId}, {true}, {0L})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO IntakeReceipts (Id, SourceFileName, MediaType, SourceLength, SourceHash, SourceChannel, ExternalReceiptToken, ReceivedAtUtc, ProcessedAtUtc, SourceReaderKey, SourceReaderVersion, Version, Decision, DecisionReason, EvidenceJson, FieldsJson, OcrCandidatesJson) VALUES ({receiptId}, {"backfill.eml"}, {"message/rfc822"}, {1L}, {new string('1', 64)}, {"mailbox"}, {receiptId.ToString("D")}, {now}, {now}, {"backfill-reader"}, {"1"}, {0L}, {"case_created"}, {"Backfill fixture"}, {"{\"version\":1,\"data\":[]}"}, {"{\"version\":1,\"data\":[]}"}, {"{\"version\":1,\"data\":[]}"})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Cases (Id, PrincipalId, SequenceLineageId, Year, Sequence, Reference, Type, InitialState, CustodyState, OriginIntakeReceiptId, InstructionComplete, ImagesComplete, InstructionConfirmedByStaff, ImagesConfirmedByStaff, CreatedAtUtc, Version, ConcurrencyToken) VALUES ({caseId}, {principalId}, {lineageId}, {2026}, {sequence}, {$"BF{caseId:N}"[..10].ToUpperInvariant()}, {"inspection"}, {"review"}, {"confirmed"}, {receiptId}, {true}, {true}, {true}, {true}, {now}, {0L}, {Guid.NewGuid()})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CaseWorkflows (CaseId, State, Version, ConcurrencyToken) VALUES ({caseId}, {"Review"}, {0L}, {Guid.NewGuid()})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CaseDataSnapshots (CaseId, OriginIntakeReceiptId, OriginSourceChannel, OriginExternalReceiptToken, OriginSourceHash, OriginReceivedAtUtc, SourceReaderKey, SourceReaderVersion, CompletenessPolicyKey, CompletenessPolicyVersion, CompletenessPolicySatisfied, AcceptedAtUtc) VALUES ({caseId}, {receiptId}, {"mailbox"}, {"backfill-source"}, {new string('1', 64)}, {now}, {"backfill-reader"}, {"1"}, {"backfill-completeness"}, {1}, {true}, {now})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO ExternalWorkItems (Id, CaseId, Kind, OperationKey, State, AttemptCount, DueAtUtc) VALUES ({workItemId}, {caseId}, {"vehicle_lookup"}, {$"backfill-{workItemId:N}"}, {"completed"}, {1}, {now})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO VehicleLookupRequests (WorkItemId, CaseId, Registration, OperationKey, RequestFingerprint, RequestedByKind, RequestedBySubjectId, RequestedByRolesJson, RequestedAtUtc, ResultingCaseVersion) VALUES ({workItemId}, {caseId}, {"ST66BCE"}, {$"backfill-{workItemId:N}"}, {new string('0', 64)}, {"Automation"}, {"vehicle-lookup-reconciliation"}, {"[]"}, {now}, {0L})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO VehicleLookupObservations (Id, WorkItemId, AttemptNumber, Outcome, Registration, Provider, ProviderVersion, ResponseIdentity, RetrievedAtUtc, Make, MileageValue, MileageUnit, MileageObservedOn, MileageMethodKey, MileageMethodVersion, MileageSupportingObservationCount, MotTestsJson, RecordedAtUtc) VALUES ({observationId}, {workItemId}, {1}, {"current"}, {"ST66BCE"}, {"offline-replay"}, {"fixture-v1"}, {$"resp-{observationId:N}"}, {now}, {"MAZDA"}, {121823L}, {"Miles"}, {new DateOnly(2025, 9, 25)}, {"latest-mot-observation"}, {2}, {1}, {"{\"version\":1,\"tests\":[]}"}, {now})");

        if (extractedMake is not null)
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO CaseDataFields (CaseId, FieldName, ValueKind, ValueType, Value, SourceKind, SourceIdentity, SourceLabel, PolicyKey, PolicyVersion) VALUES ({caseId}, {"vehicle_make"}, {"fact"}, {"text"}, {extractedMake}, {"intake_evidence"}, {"letter.pdf"}, {"page 1"}, {"extraction"}, {1})");
        }

        return caseId;
    }
}
