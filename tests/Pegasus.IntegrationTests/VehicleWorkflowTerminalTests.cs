using System.Security.Cryptography;
using System.Text;
using Pegasus.Core.Custody;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Core.Vehicle;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Collection(LocalDbFixtureDefinition.Name)]
public sealed class VehicleWorkflowTerminalTests
{
    private static readonly DateTimeOffset FixedUtcNow =
        new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
    private static readonly ActionActor Staff =
        ActionActor.Staff(Guid.Parse("11111111-1111-1111-1111-111111111111"), [StaffRole.User]);

    [Theory]
    [InlineData(CaseLifecycleState.PostReportComplete)]
    [InlineData(CaseLifecycleState.ProviderCancelled)]
    [InlineData(CaseLifecycleState.CollisionEngineersRejected)]
    [InlineData(CaseLifecycleState.CreatedInError)]
    public async Task TerminalCaseRejectsVehicleRequestAndAcceptance(CaseLifecycleState terminalState)
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(
            configureServices: services =>
                services.AddSingleton(VehicleLookupAvailability.DevelopmentOfflineReplay));
        var caseId = await SeedCaseAsync(database, terminalState);
        await using var scope = database.CreateAsyncScope();

        var requestException = await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<IRequestVehicleLookup>().ExecuteAsync(
                new(caseId, 0, "AB12CDE", Staff, "terminal-vehicle-request", "lease-token"),
                CancellationToken.None));
        Assert.Contains("read-only", requestException.Message, StringComparison.Ordinal);

        var acceptanceException = await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<IAcceptVehicleSuggestion>().ExecuteAsync(
                new(
                    caseId,
                    0,
                    Guid.NewGuid(),
                    VehicleSuggestionDecision.Accept,
                    null,
                    Staff,
                    "terminal-vehicle-accept",
                    "Terminal cases must remain immutable.",
                    "lease-token"),
                CancellationToken.None));
        Assert.Contains("read-only", acceptanceException.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RequestFailsClosedWithoutOneConfirmedCanonicalRegistration()
    {
        await using var database = await CreateDatabaseAsync();
        var caseId = await SeedCaseAsync(database, CaseLifecycleState.Review);
        var editLeaseToken = await PrepareCanonicalRegistrationAsync(database, caseId, null);
        await using var scope = database.CreateAsyncScope();

        var exception = await Assert.ThrowsAsync<ConfirmedVehicleRegistrationRequiredException>(() =>
            RequestAsync(scope.ServiceProvider, caseId, "AB12CDE", "missing-registration", editLeaseToken));

        Assert.Equal(0, exception.ConfirmedRegistrationCount);
        Assert.Equal(0, await ExternalWorkCountAsync(database, caseId));
    }

    [Fact]
    public async Task RequestRejectsCommandThatDiffersFromConfirmedCanonicalRegistration()
    {
        await using var database = await CreateDatabaseAsync();
        var caseId = await SeedCaseAsync(database, CaseLifecycleState.Review);
        var editLeaseToken = await PrepareCanonicalRegistrationAsync(database, caseId, "AB12CDE");
        await using var scope = database.CreateAsyncScope();

        await Assert.ThrowsAsync<ConfirmedVehicleRegistrationConflictException>(() =>
            RequestAsync(scope.ServiceProvider, caseId, "XY34ZAB", "mismatched-registration", editLeaseToken));

        Assert.Equal(0, await ExternalWorkCountAsync(database, caseId));
    }

    [Fact]
    public async Task RequestDeniesAmbiguousConfirmedCanonicalRegistrations()
    {
        await using var database = await CreateDatabaseAsync();
        var caseId = await SeedCaseAsync(database, CaseLifecycleState.Review);
        var editLeaseToken = await PrepareCanonicalRegistrationAsync(database, caseId, "AB12CDE");
        await database.ExecuteAsync(
            "ALTER TABLE CaseDataFields DROP CONSTRAINT PK_CaseDataFields");
        await using (var context = await database.CreateContextAsync())
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO CaseDataFields (CaseId, FieldName, ValueKind, ValueType, Value, SourceKind, SourceIdentity, SourceLabel, PolicyKey, PolicyVersion, ConfirmedByActor, ConfirmedAtUtc) VALUES ({caseId}, {"vehicle_registration"}, {"confirmed"}, {"text"}, {"XY34ZAB"}, {"staff_correction"}, {"ambiguous-second-source"}, {"Ambiguous retained source"}, {"case-data-test"}, {1}, {Staff.SubjectId}, {FixedUtcNow})");
        }
        await using var scope = database.CreateAsyncScope();

        var exception = await Assert.ThrowsAsync<ConfirmedVehicleRegistrationRequiredException>(() =>
            RequestAsync(scope.ServiceProvider, caseId, "AB12CDE", "ambiguous-registration", editLeaseToken));

        Assert.Equal(2, exception.ConfirmedRegistrationCount);
        Assert.Equal(0, await ExternalWorkCountAsync(database, caseId));
    }

    [Fact]
    public async Task ExactConfirmedRegistrationCreatesOneWorkItemAndReplaysExactly()
    {
        await using var database = await CreateDatabaseAsync();
        var caseId = await SeedCaseAsync(database, CaseLifecycleState.Review);
        var editLeaseToken = await PrepareCanonicalRegistrationAsync(database, caseId, "AB12CDE");
        await using var scope = database.CreateAsyncScope();

        var first = await RequestAsync(
            scope.ServiceProvider,
            caseId,
            "AB12CDE",
            "exact-confirmed-registration",
            editLeaseToken);
        var replay = await RequestAsync(
            scope.ServiceProvider,
            caseId,
            "AB12CDE",
            "exact-confirmed-registration",
            editLeaseToken);

        Assert.False(first.IsReplay);
        Assert.True(replay.IsReplay);
        Assert.Equal(first.WorkItemId, replay.WorkItemId);
        Assert.Equal(1, await ExternalWorkCountAsync(database, caseId));
        Assert.Equal(1L, await database.ScalarAsync<long>(
            $"SELECT Version FROM CaseWorkflows WHERE CaseId = '{caseId:D}'"));
        Assert.Equal(0L, await database.ScalarAsync<long>(
            $"SELECT Version FROM Cases WHERE Id = '{caseId:D}'"));
    }

    [Fact]
    public async Task QueuedOutcomeAdvancesCanonicalWorkflowAndMakesConcurrentEditorStale()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(
            configureServices: services =>
                services.AddSingleton(VehicleLookupAvailability.DevelopmentOfflineReplay));
        var caseId = await SeedCaseAsync(database, CaseLifecycleState.Review);
        var workItemId = Guid.NewGuid();
        const string editLeaseToken = "active-editor-lease";
        var leaseHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(editLeaseToken)));
        await using (var context = await database.CreateContextAsync())
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE CaseWorkflows SET EditLeaseToken = {editLeaseToken}, EditLeaseTokenHash = {leaseHash}, EditLeaseRequestHash = {leaseHash}, EditLeaseHolder = {Staff.SubjectId}, EditLeaseOperationKey = {"active-editor"}, EditLeaseExpiresAtUtc = {FixedUtcNow.AddMinutes(5)} WHERE CaseId = {caseId}");
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO ExternalWorkItems (Id, CaseId, Kind, OperationKey, State, AttemptCount, DueAtUtc) VALUES ({workItemId}, {caseId}, {ExternalWorkKinds.VehicleLookup}, {"seeded-vehicle-work"}, {"pending"}, {0}, {FixedUtcNow})");
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO VehicleLookupRequests (WorkItemId, CaseId, Registration, OperationKey, RequestFingerprint, RequestedByKind, RequestedBySubjectId, RequestedByRolesJson, RequestedAtUtc, ResultingCaseVersion) VALUES ({workItemId}, {caseId}, {"AB12CDE"}, {"seeded-vehicle-work"}, {new string('0', 64)}, {ActorKind.Staff.ToString()}, {Staff.SubjectId}, {"[\"User\"]"}, {FixedUtcNow}, {0L})");
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
            "AB12CDE",
            VehicleLookupOutcome.NotFound,
            "offline-replay",
            "fixture-v1",
            "not-found-response",
            FixedUtcNow,
            null,
            null,
            null,
            [],
            null);
        await workStore.RecordOutcomeAsync(
            workItemId,
            claimed.LeaseToken!,
            new(result, VehicleMileagePolicy.Calculate(result.MotTests)),
            VehicleLookupWorkState.Completed,
            null,
            FixedUtcNow,
            CancellationToken.None);

        Assert.Equal(1L, await database.ScalarAsync<long>(
            $"SELECT Version FROM CaseWorkflows WHERE CaseId = '{caseId:D}'"));
        Assert.Equal(0L, await database.ScalarAsync<long>(
            $"SELECT Version FROM Cases WHERE Id = '{caseId:D}'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseWorkflowEvents WHERE CaseId = '{caseId:D}' AND EventType = 'vehicle_lookup_not_found'"));
        Assert.Equal(0, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseWorkflows WHERE CaseId = '{caseId:D}' AND EditLeaseTokenHash IS NOT NULL"));

        await Assert.ThrowsAsync<CaseVersionConflictException>(() =>
            scope.ServiceProvider.GetRequiredService<IRequestVehicleLookup>().ExecuteAsync(
                new(
                    caseId,
                    0,
                    "AB12CDE",
                    Staff,
                    "stale-editor-vehicle-request",
                    editLeaseToken),
                CancellationToken.None));
    }

    private static Task<LocalDbTestDatabase> CreateDatabaseAsync() =>
        LocalDbTestDatabase.CreateAsync(
            configureServices: services =>
                services.AddSingleton(VehicleLookupAvailability.DevelopmentOfflineReplay));

    private static Task<RequestedVehicleLookup> RequestAsync(
        IServiceProvider services,
        Guid caseId,
        string registration,
        string operationKey,
        string editLeaseToken) =>
        services.GetRequiredService<IRequestVehicleLookup>().ExecuteAsync(
            new(caseId, 0, registration, Staff, operationKey, editLeaseToken),
            CancellationToken.None);

    private static Task<int> ExternalWorkCountAsync(
        LocalDbTestDatabase database,
        Guid caseId) =>
        database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM ExternalWorkItems WHERE CaseId = '{caseId:D}'");

    private static async Task<string> PrepareCanonicalRegistrationAsync(
        LocalDbTestDatabase database,
        Guid caseId,
        string? registration)
    {
        const string editLeaseToken = "canonical-registration-lease";
        var leaseHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(editLeaseToken)));
        await using var context = await database.CreateContextAsync();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE CaseWorkflows SET EditLeaseToken = {editLeaseToken}, EditLeaseTokenHash = {leaseHash}, EditLeaseRequestHash = {leaseHash}, EditLeaseHolder = {Staff.SubjectId}, EditLeaseOperationKey = {"canonical-registration-edit"}, EditLeaseExpiresAtUtc = {FixedUtcNow.AddMinutes(5)} WHERE CaseId = {caseId}");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CaseDataSnapshots (CaseId, OriginIntakeReceiptId, OriginSourceChannel, OriginExternalReceiptToken, OriginSourceHash, OriginReceivedAtUtc, SourceReaderKey, SourceReaderVersion, CompletenessPolicyKey, CompletenessPolicyVersion, CompletenessPolicySatisfied, AcceptedAtUtc) SELECT Id, OriginIntakeReceiptId, {"manual_upload"}, {"canonical-registration-source"}, {new string('1', 64)}, {FixedUtcNow}, {"vehicle-test-reader"}, {"1"}, {"vehicle-test-completeness"}, {1}, {true}, {FixedUtcNow} FROM Cases WHERE Id = {caseId}");
        if (registration is not null)
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO CaseDataFields (CaseId, FieldName, ValueKind, ValueType, Value, SourceKind, SourceIdentity, SourceLabel, PolicyKey, PolicyVersion, ConfirmedByActor, ConfirmedAtUtc) VALUES ({caseId}, {"vehicle_registration"}, {"confirmed"}, {"text"}, {registration}, {"case_acceptance"}, {"canonical-registration-source"}, {"Canonical accepted registration"}, {"vehicle-test"}, {1}, {Staff.SubjectId}, {FixedUtcNow})");
        }
        return editLeaseToken;
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
        await using var context = await database.CreateContextAsync();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Organizations (Id, Name, Version) VALUES ({organizationId}, {"Vehicle terminal test"}, {0L})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO PrincipalSequenceLineages (Id, CreatedAtUtc) VALUES ({lineageId}, {FixedUtcNow})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Principals (Id, OrganizationId, Code, SequenceLineageId, IsActive, Version) VALUES ({principalId}, {organizationId}, {"VTL"}, {lineageId}, {true}, {0L})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO IntakeReceipts (Id, SourceFileName, MediaType, SourceLength, SourceHash, SourceChannel, ExternalReceiptToken, ReceivedAtUtc, ProcessedAtUtc, SourceReaderKey, SourceReaderVersion, Version, Decision, DecisionReason, EvidenceJson, FieldsJson, OcrCandidatesJson) VALUES ({receiptId}, {"vehicle-terminal.eml"}, {"message/rfc822"}, {1L}, {1.ToString("X64", System.Globalization.CultureInfo.InvariantCulture)}, {"manual_upload"}, {receiptId.ToString("D")}, {FixedUtcNow}, {FixedUtcNow}, {"vehicle-test-reader"}, {"1"}, {0L}, {"draft_ready"}, {"Vehicle terminal fixture"}, {"{\"version\":1,\"data\":[]}"}, {"{\"version\":1,\"data\":[]}"}, {"{\"version\":1,\"data\":[]}"})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Cases (Id, PrincipalId, SequenceLineageId, Year, Sequence, Reference, Type, InitialState, CustodyState, OriginIntakeReceiptId, InstructionComplete, ImagesComplete, InstructionConfirmedByStaff, ImagesConfirmedByStaff, CreatedAtUtc, Version, ConcurrencyToken) VALUES ({caseId}, {principalId}, {lineageId}, {2031}, {1}, {"VTL31001"}, {"inspection"}, {"review"}, {"pending"}, {receiptId}, {true}, {true}, {true}, {true}, {FixedUtcNow}, {0L}, {Guid.NewGuid()})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CaseWorkflows (CaseId, State, Version, ConcurrencyToken) VALUES ({caseId}, {state.ToString()}, {0L}, {Guid.NewGuid()})");
        return caseId;
    }
}
