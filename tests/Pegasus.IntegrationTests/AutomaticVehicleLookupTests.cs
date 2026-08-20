using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Vehicle;
using Pegasus.Core.Workflow;

namespace Pegasus.IntegrationTests;

/// <summary>
/// CASE-008: the automatic-lookup sweep enqueues one vehicle lookup for every
/// active case whose current registration (confirmed, else fact) has never
/// been looked up — leaseless, attributed to the Automation actor, and
/// idempotent per case and registration.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class AutomaticVehicleLookupTests
{
    private static readonly DateTimeOffset FixedUtcNow =
        new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task SweepEnqueuesOneLookupForFactRegistrationAndIsIdempotent()
    {
        await using var database = await CreateDatabaseAsync();
        var caseId = await SeedCaseAsync(database, CaseLifecycleState.NotReady);
        await SeedRegistrationFieldAsync(database, caseId, "AB12CDE", "fact");

        Assert.Equal(1, await SweepAsync(database));

        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM ExternalWorkItems WHERE CaseId = '{caseId:D}' AND Kind = 'vehicle_lookup' AND State = 'pending'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM VehicleLookupRequests WHERE CaseId = '{caseId:D}' AND Registration = 'AB12CDE' AND OperationKey = 'vehicle-lookup:auto:AB12CDE' AND RequestedByKind = 'Automation'"));

        Assert.Equal(0, await SweepAsync(database));
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM ExternalWorkItems WHERE CaseId = '{caseId:D}'"));
    }

    [Fact]
    public async Task SweepPrefersConfirmedOverFactAndSkipsAmbiguousFacts()
    {
        await using var database = await CreateDatabaseAsync();
        var confirmedCase = await SeedCaseAsync(database, CaseLifecycleState.Review);
        await SeedRegistrationFieldAsync(database, confirmedCase, "AB12CDE", "confirmed");
        await SeedRegistrationFieldAsync(database, confirmedCase, "XY34ZAB", "fact");
        var ambiguousCase = await SeedCaseAsync(database, CaseLifecycleState.Review);
        await SeedRegistrationFieldAsync(database, ambiguousCase, "CD56EFG", "fact");
        await SeedRegistrationFieldAsync(
            database, ambiguousCase, "EF78GHI", "fact", sourceIdentity: "second-source");

        Assert.Equal(1, await SweepAsync(database));

        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM VehicleLookupRequests WHERE CaseId = '{confirmedCase:D}' AND Registration = 'AB12CDE'"));
        Assert.Equal(0, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM VehicleLookupRequests WHERE CaseId = '{ambiguousCase:D}'"));
    }

    [Fact]
    public async Task SweepSkipsTerminalCasesAndUnusableValues()
    {
        await using var database = await CreateDatabaseAsync();
        var terminalCase = await SeedCaseAsync(database, CaseLifecycleState.PostReportComplete);
        await SeedRegistrationFieldAsync(database, terminalCase, "AB12CDE", "fact");
        var unusableCase = await SeedCaseAsync(database, CaseLifecycleState.Review);
        await SeedRegistrationFieldAsync(database, unusableCase, "???", "fact");

        Assert.Equal(0, await SweepAsync(database));
        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM VehicleLookupRequests"));
    }

    [Fact]
    public async Task CorrectedRegistrationGetsExactlyOneNewLookup()
    {
        await using var database = await CreateDatabaseAsync();
        var caseId = await SeedCaseAsync(database, CaseLifecycleState.Review);
        await SeedRegistrationFieldAsync(database, caseId, "AB12CDE", "fact");
        Assert.Equal(1, await SweepAsync(database));

        // Staff correct the registration: the confirmed value replaces the fact.
        await using (var context = await database.CreateContextAsync())
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE CaseDataFields SET Value = {"XY34ZAB"}, ValueKind = {"confirmed"}, ConfirmedByActor = {"staff"}, ConfirmedAtUtc = {FixedUtcNow} WHERE CaseId = {caseId} AND FieldName = {"vehicle_registration"}");
        }

        Assert.Equal(1, await SweepAsync(database));
        Assert.Equal(0, await SweepAsync(database));
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM VehicleLookupRequests WHERE CaseId = '{caseId:D}' AND Registration = 'XY34ZAB'"));
        Assert.Equal(2, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM VehicleLookupRequests WHERE CaseId = '{caseId:D}'"));
    }

    [Fact]
    public async Task SweepDoesNothingWhereLookupsAreNotComposed()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var caseId = await SeedCaseAsync(database, CaseLifecycleState.Review);
        await SeedRegistrationFieldAsync(database, caseId, "AB12CDE", "fact");

        Assert.Equal(0, await SweepAsync(database));
        Assert.Equal(0, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM ExternalWorkItems WHERE CaseId = '{caseId:D}'"));
    }

    private static Task<LocalDbTestDatabase> CreateDatabaseAsync() =>
        LocalDbTestDatabase.CreateAsync(
            configureServices: services =>
                services.AddSingleton(VehicleLookupAvailability.DevelopmentOfflineReplay));

    private static async Task<int> SweepAsync(LocalDbTestDatabase database)
    {
        await using var scope = database.CreateAsyncScope();
        return await scope.ServiceProvider
            .GetRequiredService<ReconcileAutomaticVehicleLookups>()
            .ExecuteAsync(50, CancellationToken.None);
    }

    private static async Task SeedRegistrationFieldAsync(
        LocalDbTestDatabase database,
        Guid caseId,
        string registration,
        string valueKind,
        string sourceIdentity = "auto-lookup-source")
    {
        await using var context = await database.CreateContextAsync();
        if (sourceIdentity != "auto-lookup-source")
        {
            // A second same-kind row for the same field needs the composite key
            // relaxed, the same way ambiguous-registration coverage does elsewhere.
            await context.Database.ExecuteSqlRawAsync(
                "IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'PK_CaseDataFields') ALTER TABLE CaseDataFields DROP CONSTRAINT PK_CaseDataFields");
        }

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CaseDataFields (CaseId, FieldName, ValueKind, ValueType, Value, SourceKind, SourceIdentity, SourceLabel, PolicyKey, PolicyVersion, ConfirmedByActor, ConfirmedAtUtc) VALUES ({caseId}, {"vehicle_registration"}, {valueKind}, {"text"}, {registration}, {"intake_evidence"}, {sourceIdentity}, {"Automatic lookup fixture"}, {"auto-lookup-test"}, {1}, {(valueKind == "confirmed" ? "staff" : null)}, {(valueKind == "confirmed" ? FixedUtcNow : (DateTimeOffset?)null)})");
    }

    private static async Task<Guid> SeedCaseAsync(
        LocalDbTestDatabase database,
        CaseLifecycleState state)
    {
        var organizationId = Guid.NewGuid();
        var lineageId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var sequence = Math.Abs(caseId.GetHashCode() % 999) + 1;
        await using var context = await database.CreateContextAsync();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Organizations (Id, Name, Version) VALUES ({organizationId}, {$"Automatic lookup test {organizationId:N}"}, {0L})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO PrincipalSequenceLineages (Id, CreatedAtUtc) VALUES ({lineageId}, {FixedUtcNow})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Principals (Id, OrganizationId, Code, SequenceLineageId, IsActive, Version) VALUES ({principalId}, {organizationId}, {$"A{sequence % 997:D3}"}, {lineageId}, {true}, {0L})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO IntakeReceipts (Id, SourceFileName, MediaType, SourceLength, SourceHash, SourceChannel, ExternalReceiptToken, ReceivedAtUtc, ProcessedAtUtc, SourceReaderKey, SourceReaderVersion, Version, Decision, DecisionReason, EvidenceJson, FieldsJson, OcrCandidatesJson) VALUES ({receiptId}, {"auto-lookup.eml"}, {"message/rfc822"}, {1L}, {1.ToString("X64", System.Globalization.CultureInfo.InvariantCulture)}, {"manual_upload"}, {receiptId.ToString("D")}, {FixedUtcNow}, {FixedUtcNow}, {"auto-lookup-reader"}, {"1"}, {0L}, {"case_created"}, {"Automatic lookup fixture"}, {"{\"version\":1,\"data\":[]}"}, {"{\"version\":1,\"data\":[]}"}, {"{\"version\":1,\"data\":[]}"})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Cases (Id, PrincipalId, SequenceLineageId, Year, Sequence, Reference, Type, InitialState, CustodyState, OriginIntakeReceiptId, InstructionComplete, ImagesComplete, InstructionConfirmedByStaff, ImagesConfirmedByStaff, CreatedAtUtc, Version, ConcurrencyToken) VALUES ({caseId}, {principalId}, {lineageId}, {2031}, {sequence}, {$"ALK{caseId:N}"[..10].ToUpperInvariant()}, {"inspection"}, {"review"}, {"pending"}, {receiptId}, {true}, {true}, {true}, {true}, {FixedUtcNow}, {0L}, {Guid.NewGuid()})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CaseWorkflows (CaseId, State, Version, ConcurrencyToken) VALUES ({caseId}, {state.ToString()}, {0L}, {Guid.NewGuid()})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CaseDataSnapshots (CaseId, OriginIntakeReceiptId, OriginSourceChannel, OriginExternalReceiptToken, OriginSourceHash, OriginReceivedAtUtc, SourceReaderKey, SourceReaderVersion, CompletenessPolicyKey, CompletenessPolicyVersion, CompletenessPolicySatisfied, AcceptedAtUtc) VALUES ({caseId}, {receiptId}, {"manual_upload"}, {"auto-lookup-source"}, {new string('1', 64)}, {FixedUtcNow}, {"auto-lookup-reader"}, {"1"}, {"auto-lookup-completeness"}, {1}, {true}, {FixedUtcNow})");
        return caseId;
    }
}
