using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Cases;
using Pegasus.Core.Custody;
using Pegasus.Core.Identity;
using Pegasus.Core.Vehicle;
using Pegasus.Core.Workflow;

namespace Pegasus.IntegrationTests;

/// <summary>
/// CASE-008: the automatic-lookup sweep enqueues one vehicle lookup for every
/// active case whose current registration (confirmed, else fact) has never
/// been looked up — leaseless, attributed to the Automation actor, and
/// idempotent per case and registration.
///
/// WP8: Case creation now enqueues and publishes the same work item inside its
/// own transaction, so the sweep is the recovery path rather than the first
/// route. Both write the same request row, so neither doubles the other.
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
            $"SELECT COUNT(*) FROM VehicleLookupRequests WHERE CaseId = '{caseId:D}' AND Registration = 'AB12CDE' AND OperationKey = 'vehicle-lookup:auto:{caseId:N}:AB12CDE' AND RequestedByKind = 'Automation'"));

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
    public async Task SweepSkipsClosedCasesAndUnusableValues()
    {
        await using var database = await CreateDatabaseAsync();
        var terminalCase = await SeedCaseAsync(database, CaseLifecycleState.ProviderCancelled);
        await SeedRegistrationFieldAsync(database, terminalCase, "AB12CDE", "fact");
        var unusableCase = await SeedCaseAsync(database, CaseLifecycleState.Review);
        await SeedRegistrationFieldAsync(database, unusableCase, "???", "fact");

        Assert.Equal(0, await SweepAsync(database));
        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM VehicleLookupRequests"));
    }

    [Theory]
    [InlineData(CaseLifecycleState.PostReportComplete)]
    [InlineData(CaseLifecycleState.Query)]
    public async Task SweepEnqueuesLookupsForCompletedAndQueryCases(CaseLifecycleState state)
    {
        await using var database = await CreateDatabaseAsync();
        var caseId = await SeedCaseAsync(database, state);
        await SeedRegistrationFieldAsync(database, caseId, "AB12CDE", "fact");

        Assert.Equal(1, await SweepAsync(database));
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM VehicleLookupRequests WHERE CaseId = '{caseId:D}'"));
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

    /// <summary>
    /// WP8: a new Case enqueues its own lookup inside the creation
    /// transaction and publishes it on commit, so DVLA/MOT evidence starts
    /// arriving with the Case instead of on the next ten-second sweep. The
    /// sweep must then find nothing left to do — both paths write the same
    /// (CaseId, Registration) request under the same operation key.
    /// </summary>
    [Fact]
    public async Task ManualCreationEnqueuesAndPublishesOneLookupAndTheSweepAddsNone()
    {
        var publisher = new RecordingExternalWorkPublisher();
        await using var database = await CreateDatabaseAsync(publisher);
        var principalCode = await SeedPrincipalAsync(database);

        var caseId = await CreateManualCaseAsync(database, principalCode, "ab 12 cde");

        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM VehicleLookupRequests WHERE CaseId = '{caseId:D}' AND Registration = 'AB12CDE' AND OperationKey = 'vehicle-lookup:auto:{caseId:N}:AB12CDE' AND RequestedByKind = 'Automation'"));
        var workItemId = await database.ScalarAsync<Guid>(
            $"SELECT Id FROM ExternalWorkItems WHERE CaseId = '{caseId:D}' AND Kind = 'vehicle_lookup' AND State = 'pending'");
        Assert.Contains(workItemId, publisher.WorkItemIds);

        Assert.Equal(0, await SweepAsync(database));
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM ExternalWorkItems WHERE CaseId = '{caseId:D}' AND Kind = 'vehicle_lookup'"));
    }

    /// <summary>
    /// The operation key is scoped by Case, not just registration:
    /// <c>ExternalWorkItems.OperationKey</c> is globally unique, and the same
    /// plate is routinely looked up for more than one Case. Two Cases created
    /// with the same registration must each get their own lookup request
    /// instead of the second creation throwing on a duplicate key.
    /// </summary>
    [Fact]
    public async Task TwoCasesWithTheSameRegistrationEachGetTheirOwnLookup()
    {
        var publisher = new RecordingExternalWorkPublisher();
        await using var database = await CreateDatabaseAsync(publisher);
        var principalCode = await SeedPrincipalAsync(database);

        var firstCaseId = await CreateManualCaseAsync(database, principalCode, "AB12CDE");
        var secondCaseId = await CreateManualCaseAsync(database, principalCode, "AB12CDE");

        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM VehicleLookupRequests WHERE CaseId = '{firstCaseId:D}' AND Registration = 'AB12CDE' AND OperationKey = 'vehicle-lookup:auto:{firstCaseId:N}:AB12CDE' AND RequestedByKind = 'Automation'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM VehicleLookupRequests WHERE CaseId = '{secondCaseId:D}' AND Registration = 'AB12CDE' AND OperationKey = 'vehicle-lookup:auto:{secondCaseId:N}:AB12CDE' AND RequestedByKind = 'Automation'"));

        var firstWorkItemId = await database.ScalarAsync<Guid>(
            $"SELECT Id FROM ExternalWorkItems WHERE CaseId = '{firstCaseId:D}' AND Kind = 'vehicle_lookup' AND State = 'pending'");
        var secondWorkItemId = await database.ScalarAsync<Guid>(
            $"SELECT Id FROM ExternalWorkItems WHERE CaseId = '{secondCaseId:D}' AND Kind = 'vehicle_lookup' AND State = 'pending'");
        Assert.Contains(firstWorkItemId, publisher.WorkItemIds);
        Assert.Contains(secondWorkItemId, publisher.WorkItemIds);

        Assert.Equal(0, await SweepAsync(database));
    }

    /// <summary>
    /// Where lookups are not composed the creation transaction enqueues
    /// nothing and publishes nothing — the same silence the sweep keeps.
    /// </summary>
    [Fact]
    public async Task ManualCreationEnqueuesNothingWhereLookupsAreNotComposed()
    {
        var publisher = new RecordingExternalWorkPublisher();
        await using var database = await LocalDbTestDatabase.CreateAsync(
            configureServices: services =>
                services.AddSingleton<ICommittedExternalWorkPublisher>(publisher));
        var principalCode = await SeedPrincipalAsync(database);

        var caseId = await CreateManualCaseAsync(database, principalCode, "AB12CDE");

        Assert.Equal(0, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM VehicleLookupRequests WHERE CaseId = '{caseId:D}'"));
        Assert.Equal(0, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM ExternalWorkItems WHERE CaseId = '{caseId:D}' AND Kind = 'vehicle_lookup'"));
        Assert.Empty(publisher.WorkItemIds);
    }

    private static async Task<Guid> CreateManualCaseAsync(
        LocalDbTestDatabase database,
        string principalCode,
        string registration)
    {
        await using var scope = database.CreateAsyncScope();
        var identity = await scope.ServiceProvider
            .GetRequiredService<ICreateManualCase>()
            .ExecuteAsync(
                new(
                    ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]),
                    $"manual-create:{Guid.NewGuid():N}",
                    principalCode,
                    CaseType.Inspection,
                    new(
                        ClaimantName: "Jane Doe",
                        ClaimNumber: "C-1",
                        VehicleRegistration: registration)),
                CancellationToken.None);
        return identity.CaseId;
    }

    private static async Task<string> SeedPrincipalAsync(LocalDbTestDatabase database)
    {
        var organizationId = Guid.NewGuid();
        var lineageId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var code = $"C{Math.Abs(principalId.GetHashCode() % 997):D3}";
        await using var context = await database.CreateContextAsync();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Organizations (Id, Name, Version) VALUES ({organizationId}, {$"Automatic lookup test {organizationId:N}"}, {0L})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO PrincipalSequenceLineages (Id, CreatedAtUtc) VALUES ({lineageId}, {FixedUtcNow})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Principals (Id, OrganizationId, Code, SequenceLineageId, IsActive, Version) VALUES ({principalId}, {organizationId}, {code}, {lineageId}, {true}, {0L})");
        return code;
    }

    private sealed class RecordingExternalWorkPublisher : ICommittedExternalWorkPublisher
    {
        private readonly List<Guid> published = [];

        public IReadOnlyList<Guid> WorkItemIds
        {
            get
            {
                lock (published)
                {
                    return published.ToArray();
                }
            }
        }

        public Task PublishAsync(Guid workItemId, CancellationToken cancellationToken)
        {
            lock (published)
            {
                published.Add(workItemId);
            }

            return Task.CompletedTask;
        }
    }

    private static Task<LocalDbTestDatabase> CreateDatabaseAsync(
        ICommittedExternalWorkPublisher? publisher = null) =>
        LocalDbTestDatabase.CreateAsync(
            configureServices: services =>
            {
                services.AddSingleton(VehicleLookupAvailability.DevelopmentOfflineReplay);
                if (publisher is not null)
                {
                    services.AddSingleton(publisher);
                }
            });

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
            $"INSERT INTO Cases (Id, PrincipalId, SequenceLineageId, Year, Sequence, Reference, Type, InitialState, CustodyState, OriginIntakeReceiptId, InstructionComplete, ImagesComplete, CreatedAtUtc, Version, ConcurrencyToken) VALUES ({caseId}, {principalId}, {lineageId}, {2031}, {sequence}, {$"ALK{caseId:N}"[..10].ToUpperInvariant()}, {"inspection"}, {"review"}, {"pending"}, {receiptId}, {true}, {true}, {FixedUtcNow}, {0L}, {Guid.NewGuid()})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CaseWorkflows (CaseId, State, Version, ConcurrencyToken) VALUES ({caseId}, {state.ToString()}, {0L}, {Guid.NewGuid()})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CaseDataSnapshots (CaseId, OriginIntakeReceiptId, OriginSourceChannel, OriginExternalReceiptToken, OriginSourceHash, OriginReceivedAtUtc, SourceReaderKey, SourceReaderVersion, CompletenessPolicyKey, CompletenessPolicyVersion, CompletenessPolicySatisfied, AcceptedAtUtc) VALUES ({caseId}, {receiptId}, {"manual_upload"}, {"auto-lookup-source"}, {new string('1', 64)}, {FixedUtcNow}, {"auto-lookup-reader"}, {"1"}, {"auto-lookup-completeness"}, {1}, {true}, {FixedUtcNow})");
        return caseId;
    }
}
