using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Custody;
using Pegasus.Core.Identity;
using Pegasus.Core.Reports;
using Pegasus.Core.Vehicle;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The DVLA and DVSA lookup is enrichment. What it learns fills the
/// case's own empty vehicle fields as working values, and never displaces what
/// the documents already said — which is what stops one case showing two
/// rival mileages.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class VehicleLookupGapFillTests
{
    private static readonly DateTimeOffset FixedUtcNow =
        new(2026, 8, 22, 18, 47, 0, TimeSpan.Zero);

    private static readonly ActionActor Staff =
        ActionActor.Staff(Guid.Parse("33333333-3333-3333-3333-333333333333"), [StaffRole.User]);

    /// <summary>The registration the seeded Case was instructed with.</summary>
    private const string FixtureRegistration = "ST66BCE";

    /// <summary>The registration staff correct the Case to.</summary>
    private const string CorrectedRegistration = "AB12CDE";

    private static readonly DateOnly FixtureTaxDueDate = new(2027, 3, 1);

    private static readonly DateOnly FixtureMotExpiry = new(2026, 9, 24);

    /// <summary>
    /// The facts only the lookup records, in the vocabulary's canonical form,
    /// as the fixture's answer carries them.
    /// </summary>
    private static readonly (string Path, string Value)[] FixtureDerivedFacts =
    [
        (AssessmentVocabulary.VehicleEngineCc, "1461"),
        (AssessmentVocabulary.VehicleFuel, "DIESEL"),
        (AssessmentVocabulary.VehicleColour, "BLUE"),
        (AssessmentVocabulary.VehicleTaxExpiry, "2027-03-01"),
        (AssessmentVocabulary.VehicleMotExpiry, "2026-09-24")
    ];

    [Fact]
    public async Task ALookupFillsAMileageTheDocumentsNeverCarried()
    {
        await using var database = await CreateDatabaseAsync();
        var caseId = await SeedCaseAsync(database);

        await RecordLookupAsync(database, caseId);

        Assert.Equal("121823", await database.ScalarAsync<string>(
            $"SELECT Value FROM CaseDataFields WHERE WorkId = '{caseId:D}' AND FieldName = 'vehicle_mileage' AND ValueKind = 'fact'"));
        Assert.Equal("Miles", await database.ScalarAsync<string>(
            $"SELECT Value FROM CaseDataFields WHERE WorkId = '{caseId:D}' AND FieldName = 'vehicle_mileage_unit' AND ValueKind = 'fact'"));
        // The working value carries Lookup provenance, which is what the
        // report's mileage-source sentence is derived from.
        Assert.Equal("vehicle_lookup", await database.ScalarAsync<string>(
            $"SELECT SourceKind FROM CaseDataFields WHERE WorkId = '{caseId:D}' AND FieldName = 'vehicle_mileage' AND ValueKind = 'fact'"));
        // Nothing is left at the suggestion tier for anyone to accept.
        Assert.Equal(0, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseDataFields WHERE WorkId = '{caseId:D}' AND ValueKind = 'suggestion'"));
    }

    [Fact]
    public async Task ALookupFillsTheManufactureYear()
    {
        await using var database = await CreateDatabaseAsync();
        var caseId = await SeedCaseAsync(database);

        await RecordLookupAsync(database, caseId);

        Assert.Equal("2016", await database.ScalarAsync<string>(
            $"SELECT Value FROM CaseDataFields WHERE WorkId = '{caseId:D}' AND FieldName = 'vehicle_year' AND ValueKind = 'fact'"));
        Assert.Equal("vehicle_lookup", await database.ScalarAsync<string>(
            $"SELECT SourceKind FROM CaseDataFields WHERE WorkId = '{caseId:D}' AND FieldName = 'vehicle_year' AND ValueKind = 'fact'"));
    }

    [Fact]
    public async Task RecordOutcomeWritesTheDerivedVehicleTypeAsAutomation()
    {
        await using var database = await CreateDatabaseAsync();
        var caseId = await SeedCaseAsync(database);

        await RecordLookupAsync(database, caseId);

        Assert.Equal("car", await database.ScalarAsync<string>(
            $"SELECT Value FROM CaseAssessmentFields WHERE WorkId = '{caseId:D}' AND FieldPath = '{AssessmentVocabulary.VehicleType}'"));
        Assert.Equal(ActorKind.Automation.ToString(), await database.ScalarAsync<string>(
            $"SELECT RecordedByKind FROM CaseAssessmentFields WHERE WorkId = '{caseId:D}' AND FieldPath = '{AssessmentVocabulary.VehicleType}'"));
        Assert.Equal("vehicle-lookup", await database.ScalarAsync<string>(
            $"SELECT RecordedBy FROM CaseAssessmentFields WHERE WorkId = '{caseId:D}' AND FieldPath = '{AssessmentVocabulary.VehicleType}'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseAssessmentFields WHERE WorkId = '{caseId:D}' AND FieldPath = '{AssessmentVocabulary.VehicleType}' AND RecordedByKind = 'Automation'"));
        Assert.Equal("M1", await database.ScalarAsync<string>(
            $"SELECT TypeApproval FROM VehicleLookupObservations WHERE WorkItemId IN (SELECT WorkItemId FROM VehicleLookupRequests WHERE CaseId = '{caseId:D}')"));
        Assert.Equal("2 AXLE RIGID BODY", await database.ScalarAsync<string>(
            $"SELECT Wheelplan FROM VehicleLookupObservations WHERE WorkItemId IN (SELECT WorkItemId FROM VehicleLookupRequests WHERE CaseId = '{caseId:D}')"));
        Assert.Equal(1_800, await database.ScalarAsync<int>(
            $"SELECT RevenueWeightKg FROM VehicleLookupObservations WHERE WorkItemId IN (SELECT WorkItemId FROM VehicleLookupRequests WHERE CaseId = '{caseId:D}')"));
    }

    /// <summary>
    /// Engine capacity, fuel, colour, tax expiry and MOT expiry are the
    /// lookup's own facts: it records each with its own provenance, and the
    /// readiness rail names none of them.
    /// </summary>
    [Fact]
    public async Task RecordOutcomeWritesTheLookupsOwnFacts()
    {
        await using var database = await CreateDatabaseAsync();
        var caseId = await SeedCaseAsync(database);

        await RecordLookupAsync(database, caseId);

        var rows = await DerivedFactRowsAsync(database, caseId);
        Assert.Equal(FixtureDerivedFacts.Length, rows.Count);
        foreach (var (path, value) in FixtureDerivedFacts)
        {
            var row = Assert.Single(rows, item => item.FieldPath == path);
            Assert.Equal(value, row.Value);
            Assert.Equal(ActorKind.Automation.ToString(), row.RecordedByKind);
            Assert.Equal("vehicle-lookup", row.RecordedBy);
        }
        Assert.Equal("BLUE", await database.ScalarAsync<string>(
            $"SELECT Colour FROM VehicleLookupObservations WHERE WorkItemId IN (SELECT WorkItemId FROM VehicleLookupRequests WHERE CaseId = '{caseId:D}')"));
        Assert.Equal("2027-03-01", await database.ScalarAsync<string>(
            $"SELECT CONVERT(char(10), TaxDueDate, 23) FROM VehicleLookupObservations WHERE WorkItemId IN (SELECT WorkItemId FROM VehicleLookupRequests WHERE CaseId = '{caseId:D}')"));

        // The store only projects; the readiness rail is Core's to evaluate.
        await using var scope = database.CreateAsyncScope();
        var projection = Assert.IsType<CaseAssessmentProjection>(
            await scope.ServiceProvider.GetRequiredService<ICaseAssessmentStore>()
                .GetAsync(caseId, CancellationToken.None));
        Assert.DoesNotContain(
            AssessmentPolicy.EvaluateReadiness(projection),
            item => item.Field is { } field && AssessmentVocabulary.LookupDerivedPaths.Contains(field));
    }

    [Fact]
    public async Task AnUnchangedDerivedFactIsNotRestampedAndDoesNotStale()
    {
        await using var database = await CreateDatabaseAsync();
        var caseId = await SeedCaseAsync(database);
        await RecordLookupAsync(database, caseId);
        var (currentId, _) = await SeedGenerationsAsync(database, caseId);

        await RecordLookupAsync(database, caseId, recordedAtUtc: FixedUtcNow.AddMinutes(1));

        var rows = await DerivedFactRowsAsync(database, caseId);
        Assert.Equal(FixtureDerivedFacts.Length, rows.Count);
        Assert.All(rows, row => Assert.Equal(FixedUtcNow, row.RecordedAtUtc));
        Assert.Equal("Confirmed", await database.ScalarAsync<string>(
            $"SELECT State FROM CaseReportGenerations WHERE Id = '{currentId:D}'"));
        Assert.Equal(0, await StaleRowCountAsync(database, caseId));
    }

    [Fact]
    public async Task AChangedDerivedFactReplacesTheEarlierAndStalesTheReport()
    {
        await using var database = await CreateDatabaseAsync();
        var caseId = await SeedCaseAsync(database);
        await RecordLookupAsync(database, caseId);
        var (currentId, _) = await SeedGenerationsAsync(database, caseId);
        var changedAtUtc = FixedUtcNow.AddMinutes(1);

        await RecordLookupAsync(database, caseId, colour: "RED", recordedAtUtc: changedAtUtc);

        var colour = Assert.Single(
            await DerivedFactRowsAsync(database, caseId),
            item => item.FieldPath == AssessmentVocabulary.VehicleColour);
        Assert.Equal("RED", colour.Value);
        Assert.Equal(changedAtUtc, colour.RecordedAtUtc);
        Assert.Equal("Stale", await database.ScalarAsync<string>(
            $"SELECT State FROM CaseReportGenerations WHERE Id = '{currentId:D}'"));
        Assert.Equal(1, await StaleRowCountAsync(database, caseId));
        Assert.Equal(
            CaseReportStaleReasons.AssessmentFactsChanged,
            await database.ScalarAsync<string>(
                $"SELECT Reason FROM ActionHistory WHERE AggregateType = 'case' AND AggregateId = '{caseId:D}' AND EventKind = 'case_report_generation_stale'"));
    }

    /// <summary>
    /// A complete answer describes the vehicle as it now stands, so a fact it
    /// no longer carries is cleared rather than left behind from an earlier
    /// answer.
    /// </summary>
    [Fact]
    public async Task ACompleteAnswerClearsADerivedFactItNoLongerCarries()
    {
        await using var database = await CreateDatabaseAsync();
        var caseId = await SeedCaseAsync(database);
        await RecordLookupAsync(database, caseId);
        var (currentId, _) = await SeedGenerationsAsync(database, caseId);

        await RecordLookupAsync(
            database,
            caseId,
            engineCapacityCc: null,
            clearMot: true,
            recordedAtUtc: FixedUtcNow.AddMinutes(1));

        var paths = (await DerivedFactRowsAsync(database, caseId)).Select(item => item.FieldPath).ToArray();
        Assert.DoesNotContain(AssessmentVocabulary.VehicleEngineCc, paths);
        Assert.DoesNotContain(AssessmentVocabulary.VehicleMotExpiry, paths);
        Assert.Equal(3, paths.Length);
        Assert.Equal("Stale", await database.ScalarAsync<string>(
            $"SELECT State FROM CaseReportGenerations WHERE Id = '{currentId:D}'"));
        Assert.Equal(1, await StaleRowCountAsync(database, caseId));
    }

    /// <summary>
    /// A partial answer is silent about what its failed provider would have
    /// said, and that silence never erases an earlier answer.
    /// </summary>
    [Fact]
    public async Task APartialAnswerLeavesWhatItDoesNotCarry()
    {
        await using var database = await CreateDatabaseAsync();
        var caseId = await SeedCaseAsync(database);
        await RecordLookupAsync(database, caseId);

        await RecordLookupAsync(
            database,
            caseId,
            engineCapacityCc: null,
            fuel: "Diesel",
            colour: null,
            clearTax: true,
            outcome: VehicleLookupOutcome.Partial,
            failure: new("dvla_unavailable", Retryable: true),
            recordedAtUtc: FixedUtcNow.AddMinutes(1));

        var rows = await DerivedFactRowsAsync(database, caseId);
        Assert.Equal("1461", Assert.Single(rows, item => item.FieldPath == AssessmentVocabulary.VehicleEngineCc).Value);
        Assert.Equal("BLUE", Assert.Single(rows, item => item.FieldPath == AssessmentVocabulary.VehicleColour).Value);
        Assert.Equal("2027-03-01", Assert.Single(rows, item => item.FieldPath == AssessmentVocabulary.VehicleTaxExpiry).Value);
        Assert.Equal("Diesel", Assert.Single(rows, item => item.FieldPath == AssessmentVocabulary.VehicleFuel).Value);
    }

    /// <summary>
    /// An answer describes the registration it looked up. One that lands after
    /// staff corrected the registration, complete or not-found, is kept as an
    /// observation and leaves the current vehicle's facts, its Vehicle type and
    /// the report as they stand.
    /// </summary>
    [Theory]
    [InlineData(VehicleLookupOutcome.Current)]
    [InlineData(VehicleLookupOutcome.NotFound)]
    public async Task ALateAnswerForAPreviousRegistrationLeavesTheCurrentFacts(VehicleLookupOutcome outcome)
    {
        await using var database = await CreateDatabaseAsync();
        var caseId = await SeedCaseAsync(database);
        await using (var context = await database.CreateContextAsync())
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO CaseDataFields (WorkId, FieldName, ValueKind, ValueType, Value, SourceKind, SourceIdentity, SourceLabel, PolicyKey, PolicyVersion, ConfirmedByActor, ConfirmedAtUtc) VALUES ({caseId}, {"vehicle_registration"}, {"confirmed"}, {"text"}, {CorrectedRegistration}, {"staff_correction"}, {"staff"}, {"staff case-data correction"}, {"case-data-edit"}, {1}, {"staff"}, {FixedUtcNow})");
        }
        await RecordLookupAsync(database, caseId, registration: CorrectedRegistration);
        var (currentId, _) = await SeedGenerationsAsync(database, caseId);

        await RecordLookupAsync(
            database,
            caseId,
            typeApproval: "N1",
            engineCapacityCc: null,
            colour: "RED",
            clearMot: true,
            outcome: outcome,
            registration: FixtureRegistration,
            recordedAtUtc: FixedUtcNow.AddMinutes(1));

        var rows = await DerivedFactRowsAsync(database, caseId);
        Assert.Equal(FixtureDerivedFacts.Length, rows.Count);
        foreach (var (path, value) in FixtureDerivedFacts)
        {
            var row = Assert.Single(rows, item => item.FieldPath == path);
            Assert.Equal(value, row.Value);
            Assert.Equal(FixedUtcNow, row.RecordedAtUtc);
        }
        Assert.Equal("car", await database.ScalarAsync<string>(
            $"SELECT Value FROM CaseAssessmentFields WHERE WorkId = '{caseId:D}' AND FieldPath = '{AssessmentVocabulary.VehicleType}'"));
        Assert.Equal("Confirmed", await database.ScalarAsync<string>(
            $"SELECT State FROM CaseReportGenerations WHERE Id = '{currentId:D}'"));
        Assert.Equal(0, await StaleRowCountAsync(database, caseId));
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM VehicleLookupObservations WHERE Registration = '{FixtureRegistration}' AND WorkItemId IN (SELECT WorkItemId FROM VehicleLookupRequests WHERE CaseId = '{caseId:D}')"));
    }

    /// <summary>
    /// The lookup's own facts describe the vehicle it looked up, so a staff
    /// save that changes the registration removes them in its own transaction
    /// and stales the report that printed them. A lookup of the new
    /// registration records them again.
    /// </summary>
    [Fact]
    public async Task AStaffRegistrationChangeClearsTheLookupsOwnFacts()
    {
        await using var database = await CreateDatabaseAsync();
        var caseId = await SeedCaseAsync(database);
        await RecordLookupAsync(database, caseId);
        var (currentId, _) = await SeedGenerationsAsync(database, caseId);
        var version = await database.ScalarAsync<long>(
            $"SELECT Version FROM CaseWorkflows WHERE CaseId = '{caseId:D}'");

        await using (var scope = database.CreateAsyncScope())
        {
            var lease = await scope.ServiceProvider.GetRequiredService<IAcquireCaseEditLease>().ExecuteAsync(
                new(caseId, version, Staff, "registration-change-lease"),
                CancellationToken.None);
            await scope.ServiceProvider.GetRequiredService<ICaseWorkspaceStore>().SaveAsync(
                new SaveCaseWorkspaceRequest(
                    caseId,
                    version,
                    Staff,
                    "registration-change",
                    "Registration corrected",
                    lease.Token)
                {
                    Vehicle = new(CorrectedRegistration, "RENAULT", "CAPTUR", null, null)
                },
                CancellationToken.None);
        }

        Assert.Empty(await DerivedFactRowsAsync(database, caseId));
        // The Vehicle type is staff-editable working data, not the lookup's own.
        Assert.Equal("car", await database.ScalarAsync<string>(
            $"SELECT Value FROM CaseAssessmentFields WHERE WorkId = '{caseId:D}' AND FieldPath = '{AssessmentVocabulary.VehicleType}'"));
        Assert.Equal("Stale", await database.ScalarAsync<string>(
            $"SELECT State FROM CaseReportGenerations WHERE Id = '{currentId:D}'"));
        Assert.Equal(1, await StaleRowCountAsync(database, caseId));

        await RecordLookupAsync(
            database,
            caseId,
            registration: CorrectedRegistration,
            recordedAtUtc: FixedUtcNow.AddMinutes(1));

        Assert.Equal(FixtureDerivedFacts.Length, (await DerivedFactRowsAsync(database, caseId)).Count);
    }

    [Fact]
    public async Task ALookupReplacesAnotherRecordersValueOnItsOwnFact()
    {
        await using var database = await CreateDatabaseAsync();
        var caseId = await SeedCaseAsync(database);
        await using (var context = await database.CreateContextAsync())
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO CaseAssessmentFields (WorkId, FieldPath, Value, RecordedByKind, RecordedBy, RecordedAtUtc) VALUES ({caseId}, {AssessmentVocabulary.VehicleColour}, {"Green"}, {ActorKind.Automation.ToString()}, {"automation"}, {FixedUtcNow})");
        }

        await RecordLookupAsync(database, caseId, recordedAtUtc: FixedUtcNow.AddMinutes(1));

        var colour = Assert.Single(
            await DerivedFactRowsAsync(database, caseId),
            item => item.FieldPath == AssessmentVocabulary.VehicleColour);
        Assert.Equal("BLUE", colour.Value);
        Assert.Equal("vehicle-lookup", colour.RecordedBy);
    }

    [Fact]
    public async Task AStaffRecordedVehicleTypeSurvivesRelookup()
    {
        await using var database = await CreateDatabaseAsync();
        var caseId = await SeedCaseAsync(database);
        await RecordLookupAsync(database, caseId);
        await using (var context = await database.CreateContextAsync())
        {
            var vehicleType = await context.CaseAssessmentFields.SingleAsync(
                item => item.WorkId == caseId
                        && item.FieldPath == AssessmentVocabulary.VehicleType);
            vehicleType.RecordedByKind = ActorKind.Staff.ToString();
            vehicleType.RecordedBy = "staff";
            vehicleType.RecordedAtUtc = FixedUtcNow;
            await context.SaveChangesAsync();
        }

        await RecordLookupAsync(
            database,
            caseId,
            typeApproval: "N1",
            recordedAtUtc: FixedUtcNow.AddMinutes(1));

        Assert.Equal("car", await database.ScalarAsync<string>(
            $"SELECT Value FROM CaseAssessmentFields WHERE WorkId = '{caseId:D}' AND FieldPath = '{AssessmentVocabulary.VehicleType}'"));
        Assert.Equal("staff", await database.ScalarAsync<string>(
            $"SELECT RecordedBy FROM CaseAssessmentFields WHERE WorkId = '{caseId:D}' AND FieldPath = '{AssessmentVocabulary.VehicleType}'"));
    }

    [Fact]
    public async Task ALookupRecordedVehicleTypeIsRestampedOnlyWhenTheCodeChanges()
    {
        await using var database = await CreateDatabaseAsync();
        var caseId = await SeedCaseAsync(database);
        await RecordLookupAsync(database, caseId);

        await RecordLookupAsync(
            database,
            caseId,
            typeApproval: "M1",
            recordedAtUtc: FixedUtcNow.AddMinutes(1));

        Assert.Equal(FixedUtcNow, await database.ScalarAsync<DateTimeOffset>(
            $"SELECT RecordedAtUtc FROM CaseAssessmentFields WHERE WorkId = '{caseId:D}' AND FieldPath = '{AssessmentVocabulary.VehicleType}'"));

        var changedAtUtc = FixedUtcNow.AddMinutes(2);
        await RecordLookupAsync(
            database,
            caseId,
            typeApproval: "N1",
            recordedAtUtc: changedAtUtc);

        Assert.Equal("van", await database.ScalarAsync<string>(
            $"SELECT Value FROM CaseAssessmentFields WHERE WorkId = '{caseId:D}' AND FieldPath = '{AssessmentVocabulary.VehicleType}'"));
        Assert.Equal(changedAtUtc, await database.ScalarAsync<DateTimeOffset>(
            $"SELECT RecordedAtUtc FROM CaseAssessmentFields WHERE WorkId = '{caseId:D}' AND FieldPath = '{AssessmentVocabulary.VehicleType}'"));
    }

    [Fact]
    public async Task ALookupWithNoVehicleTypeClassificationWritesNothing()
    {
        await using var database = await CreateDatabaseAsync();
        var caseId = await SeedCaseAsync(database);

        await RecordLookupAsync(
            database,
            caseId,
            typeApproval: null,
            wheelplan: null,
            revenueWeightKg: null);

        Assert.Equal(0, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseAssessmentFields WHERE WorkId = '{caseId:D}' AND FieldPath = '{AssessmentVocabulary.VehicleType}'"));
    }

    [Fact]
    public async Task AnExtractedMakeOutranksTheLookupsOwn()
    {
        await using var database = await CreateDatabaseAsync();
        var caseId = await SeedCaseAsync(database);
        await using (var context = await database.CreateContextAsync())
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO CaseDataFields (WorkId, FieldName, ValueKind, ValueType, Value, SourceKind, SourceIdentity, SourceLabel, PolicyKey, PolicyVersion) VALUES ({caseId}, {"vehicle_make"}, {"fact"}, {"text"}, {"MAZDA"}, {"intake_evidence"}, {"instruction.pdf"}, {"page 1"}, {"extraction"}, {1})");
        }

        await RecordLookupAsync(database, caseId);

        // The extracted fact is untouched and the lookup wrote no rival row:
        // a field the case already answers is left alone.
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseDataFields WHERE WorkId = '{caseId:D}' AND FieldName = 'vehicle_make'"));
        Assert.Equal("MAZDA", await database.ScalarAsync<string>(
            $"SELECT Value FROM CaseDataFields WHERE WorkId = '{caseId:D}' AND FieldName = 'vehicle_make' AND ValueKind = 'fact'"));
        Assert.Equal("intake_evidence", await database.ScalarAsync<string>(
            $"SELECT SourceKind FROM CaseDataFields WHERE WorkId = '{caseId:D}' AND FieldName = 'vehicle_make' AND ValueKind = 'fact'"));
    }

    [Fact]
    public async Task AStaffConfirmedModelOutranksTheLookupsOwn()
    {
        await using var database = await CreateDatabaseAsync();
        var caseId = await SeedCaseAsync(database);
        await using (var context = await database.CreateContextAsync())
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO CaseDataFields (WorkId, FieldName, ValueKind, ValueType, Value, SourceKind, SourceIdentity, SourceLabel, PolicyKey, PolicyVersion, ConfirmedByActor, ConfirmedAtUtc) VALUES ({caseId}, {"vehicle_model"}, {"confirmed"}, {"text"}, {"CLIO"}, {"staff_correction"}, {"staff"}, {"staff case-data correction"}, {"case-data-edit"}, {1}, {"staff"}, {FixedUtcNow})");
        }

        await RecordLookupAsync(database, caseId);

        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseDataFields WHERE WorkId = '{caseId:D}' AND FieldName = 'vehicle_model'"));
        Assert.Equal("CLIO", await database.ScalarAsync<string>(
            $"SELECT Value FROM CaseDataFields WHERE WorkId = '{caseId:D}' AND FieldName = 'vehicle_model' AND ValueKind = 'confirmed'"));
    }

    [Fact]
    public async Task ASecondLookupDoesNotDuplicateOrOverwriteTheFirst()
    {
        await using var database = await CreateDatabaseAsync();
        var caseId = await SeedCaseAsync(database);

        await RecordLookupAsync(database, caseId);
        await RecordLookupAsync(database, caseId, mileage: 999_999);

        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseDataFields WHERE WorkId = '{caseId:D}' AND FieldName = 'vehicle_mileage'"));
        Assert.Equal("121823", await database.ScalarAsync<string>(
            $"SELECT Value FROM CaseDataFields WHERE WorkId = '{caseId:D}' AND FieldName = 'vehicle_mileage' AND ValueKind = 'fact'"));
    }

    [Fact]
    public async Task ALookupClearsASuggestionAnEarlierLookupLeftBehind()
    {
        await using var database = await CreateDatabaseAsync();
        var caseId = await SeedCaseAsync(database);
        await using (var context = await database.CreateContextAsync())
        {
            // The shape every estate case carried before the fill replaced the
            // suggestion chip; the migration promotes these, and a re-run must
            // not leave one orphaned behind the value that now outranks it.
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO CaseDataFields (WorkId, FieldName, ValueKind, ValueType, Value, SourceKind, SourceIdentity, SourceLabel, PolicyKey, PolicyVersion) VALUES ({caseId}, {"vehicle_make"}, {"suggestion"}, {"text"}, {"RENAULT"}, {"vehicle_lookup"}, {Guid.NewGuid().ToString("D")}, {"offline-replay/fixture-v1"}, {"vehicle-lookup-gap-fill"}, {1})");
        }

        await RecordLookupAsync(database, caseId);

        Assert.Equal(0, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseDataFields WHERE WorkId = '{caseId:D}' AND ValueKind = 'suggestion'"));
        Assert.Equal("RENAULT", await database.ScalarAsync<string>(
            $"SELECT Value FROM CaseDataFields WHERE WorkId = '{caseId:D}' AND FieldName = 'vehicle_make' AND ValueKind = 'fact'"));
    }

    /// <summary>
    /// A filled field is a frozen report input — the vehicle block reads the
    /// make, model, year, mileage and unit — so the fill stales the Case's
    /// current generation inside the same transaction that writes it. Only
    /// the current one moves: a superseded generation keeps what it issued.
    /// </summary>
    [Fact]
    public async Task ALookupThatChangesReportsVehicleFactsStalesTheCurrentGeneration()
    {
        await using var database = await CreateDatabaseAsync();
        var caseId = await SeedCaseAsync(database);
        var (currentId, supersededId) = await SeedGenerationsAsync(database, caseId);

        await RecordLookupAsync(database, caseId);

        Assert.Equal("RENAULT", await database.ScalarAsync<string>(
            $"SELECT Value FROM CaseDataFields WHERE WorkId = '{caseId:D}' AND FieldName = 'vehicle_make' AND ValueKind = 'fact'"));
        Assert.Equal("Stale", await database.ScalarAsync<string>(
            $"SELECT State FROM CaseReportGenerations WHERE Id = '{currentId:D}'"));
        Assert.Equal("Confirmed", await database.ScalarAsync<string>(
            $"SELECT State FROM CaseReportGenerations WHERE Id = '{supersededId:D}'"));
        Assert.Equal(1, await StaleRowCountAsync(database, caseId));
        Assert.Equal(
            CaseReportStaleReasons.AssessmentFactsChanged,
            await database.ScalarAsync<string>(
                $"SELECT Reason FROM ActionHistory WHERE AggregateType = 'case' AND AggregateId = '{caseId:D}' AND EventKind = 'case_report_generation_stale'"));
    }

    [Fact]
    public async Task ALookupThatChangesNoReportsVehicleFactsStalesNoGeneration()
    {
        await using var database = await CreateDatabaseAsync();
        var caseId = await SeedCaseAsync(database);
        await using (var context = await database.CreateContextAsync())
        {
            // Every field the fill could reach is already answered, so the
            // lookup records its observation and changes nothing the report
            // stands on.
            foreach (var (fieldName, valueType, value) in new[]
                     {
                         ("vehicle_make", "text", "MAZDA"),
                         ("vehicle_model", "text", "CX-5"),
                         ("vehicle_year", "text", "2018"),
                         ("vehicle_mileage", "integer", "132389"),
                         ("vehicle_mileage_unit", "text", "Miles")
                     })
            {
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO CaseDataFields (WorkId, FieldName, ValueKind, ValueType, Value, SourceKind, SourceIdentity, SourceLabel, PolicyKey, PolicyVersion) VALUES ({caseId}, {fieldName}, {"fact"}, {valueType}, {value}, {"intake_evidence"}, {"instruction.pdf"}, {"page 1"}, {"extraction"}, {1})");
            }
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO CaseAssessmentFields (WorkId, FieldPath, Value, RecordedByKind, RecordedBy, RecordedAtUtc) VALUES ({caseId}, {AssessmentVocabulary.VehicleType}, {"car"}, {ActorKind.Staff.ToString()}, {"staff"}, {FixedUtcNow})");
            await SeedFixtureDerivedFactsAsync(context, caseId);
        }

        var (currentId, _) = await SeedGenerationsAsync(database, caseId);

        await RecordLookupAsync(database, caseId);

        Assert.Equal("Confirmed", await database.ScalarAsync<string>(
            $"SELECT State FROM CaseReportGenerations WHERE Id = '{currentId:D}'"));
        Assert.Equal(0, await StaleRowCountAsync(database, caseId));
    }

    [Fact]
    public async Task ALookupThatPromotesAnEquivalentReportsVehicleFactDoesNotStale()
    {
        await using var database = await CreateDatabaseAsync();
        var caseId = await SeedCaseAsync(database);
        await using (var context = await database.CreateContextAsync())
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO CaseDataFields (WorkId, FieldName, ValueKind, ValueType, Value, SourceKind, SourceIdentity, SourceLabel, PolicyKey, PolicyVersion) VALUES ({caseId}, {"vehicle_make"}, {"suggestion"}, {"text"}, {"RENAULT"}, {"vehicle_lookup"}, {Guid.NewGuid().ToString("D")}, {"offline-replay/fixture-v1"}, {"vehicle-lookup-gap-fill"}, {1})");
            foreach (var (fieldName, valueType, value) in new[]
                     {
                         ("vehicle_model", "text", "CAPTUR"),
                         ("vehicle_year", "text", "2016"),
                         ("vehicle_mileage", "integer", "121823"),
                         ("vehicle_mileage_unit", "text", "Miles")
                     })
            {
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO CaseDataFields (WorkId, FieldName, ValueKind, ValueType, Value, SourceKind, SourceIdentity, SourceLabel, PolicyKey, PolicyVersion) VALUES ({caseId}, {fieldName}, {"fact"}, {valueType}, {value}, {"vehicle_lookup"}, {"existing-lookup"}, {"offline-replay/fixture-v1"}, {"vehicle-lookup-gap-fill"}, {1})");
            }
            // The fixture's type approval would otherwise derive a Vehicle type,
            // and its answer would record the lookup's own facts; both are
            // printed assessment facts and would stale the report.
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO CaseAssessmentFields (WorkId, FieldPath, Value, RecordedByKind, RecordedBy, RecordedAtUtc) VALUES ({caseId}, {AssessmentVocabulary.VehicleType}, {"car"}, {ActorKind.Staff.ToString()}, {"staff"}, {FixedUtcNow})");
            await SeedFixtureDerivedFactsAsync(context, caseId);
        }
        var (currentId, _) = await SeedGenerationsAsync(database, caseId);

        await RecordLookupAsync(database, caseId);

        Assert.Equal("RENAULT", await database.ScalarAsync<string>(
            $"SELECT Value FROM CaseDataFields WHERE WorkId = '{caseId:D}' AND FieldName = 'vehicle_make' AND ValueKind = 'fact'"));
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
                WorkId = caseId,
                CaseVersion = 0,
                SnapshotHash = new string('1', 64),
                SnapshotJson = ReportGenerationSnapshotFixture.Json(caseId, "seed-generation-superseded"),
                TemplateVersion = "assessment-report/v1",
                RendererVersion = "renderer/v1",
                State = nameof(CaseReportGenerationState.Confirmed),
                GeneratedAtUtc = FixedUtcNow,
                Version = 1
            },
            new CaseReportGenerationEntity
            {
                Id = currentId,
                CaseId = caseId,
                WorkId = caseId,
                CaseVersion = 0,
                SnapshotHash = new string('2', 64),
                SnapshotJson = ReportGenerationSnapshotFixture.Json(caseId, "seed-generation-current"),
                TemplateVersion = "assessment-report/v1",
                RendererVersion = "renderer/v1",
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

    /// <summary>
    /// The fixture's own facts as an earlier lookup recorded them, so an answer
    /// that repeats them changes nothing the report prints.
    /// </summary>
    private static async Task SeedFixtureDerivedFactsAsync(PegasusDbContext context, Guid caseId)
    {
        foreach (var (path, value) in FixtureDerivedFacts)
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO CaseAssessmentFields (WorkId, FieldPath, Value, RecordedByKind, RecordedBy, RecordedAtUtc) VALUES ({caseId}, {path}, {value}, {ActorKind.Automation.ToString()}, {"vehicle-lookup"}, {FixedUtcNow})");
        }
    }

    private static async Task<List<CaseAssessmentFieldEntity>> DerivedFactRowsAsync(
        LocalDbTestDatabase database,
        Guid caseId)
    {
        string[] paths = [.. AssessmentVocabulary.LookupDerivedPaths];
        await using var context = await database.CreateContextAsync();
        return await context.CaseAssessmentFields
            .AsNoTracking()
            .Where(item => item.WorkId == caseId && paths.Contains(item.FieldPath))
            .ToListAsync();
    }

    /// <summary>
    /// The fill runs on the Worker, whose least-privilege role must be able to
    /// read the confirmed mileage source (report freshness) and write the derived
    /// Vehicle type and the lookup's own facts. LocalDB tests otherwise run as
    /// dbo and never see a missing grant; Release 54 shipped exactly that gap.
    /// </summary>
    [Fact]
    public async Task TheWorkerRuntimeRoleCanRecordALookupAndWriteTheVehicleType()
    {
        await using var database = await CreateDatabaseAsync();
        var caseId = await SeedCaseAsync(database);

        await AsWorkerRuntimeRoleAsync(
            database,
            store => RecordLookupAsync(database, caseId, typeApproval: "N1", store: store));

        Assert.Equal("RENAULT", await database.ScalarAsync<string>(
            $"SELECT Value FROM CaseDataFields WHERE WorkId = '{caseId:D}' AND FieldName = 'vehicle_make' AND ValueKind = 'fact'"));
        Assert.Equal("van", await database.ScalarAsync<string>(
            $"SELECT Value FROM CaseAssessmentFields WHERE WorkId = '{caseId:D}' AND FieldPath = '{AssessmentVocabulary.VehicleType}'"));
        Assert.Equal("BLUE", await database.ScalarAsync<string>(
            $"SELECT Value FROM CaseAssessmentFields WHERE WorkId = '{caseId:D}' AND FieldPath = '{AssessmentVocabulary.VehicleColour}'"));
    }

    /// <summary>
    /// A complete answer that no longer carries one of the lookup's own facts
    /// deletes its row, so the Worker's role needs DELETE on the assessment
    /// fields, which dbo would never miss.
    /// </summary>
    [Fact]
    public async Task TheWorkerRuntimeRoleCanClearALookupDerivedFact()
    {
        await using var database = await CreateDatabaseAsync();
        var caseId = await SeedCaseAsync(database);
        await RecordLookupAsync(database, caseId);

        await AsWorkerRuntimeRoleAsync(
            database,
            store => RecordLookupAsync(
                database,
                caseId,
                engineCapacityCc: null,
                recordedAtUtc: FixedUtcNow.AddMinutes(1),
                store: store));

        Assert.Equal(0, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseAssessmentFields WHERE WorkId = '{caseId:D}' AND FieldPath = '{AssessmentVocabulary.VehicleEngineCc}'"));
    }

    /// <summary>
    /// Records through a work store whose connection runs as a member of the
    /// Worker's runtime role rather than as dbo.
    /// </summary>
    private static async Task AsWorkerRuntimeRoleAsync(
        LocalDbTestDatabase database,
        Func<IVehicleLookupWorkStore, Task> record)
    {
        await database.ExecuteAsync("""
            CREATE USER [pegasus_test_lookup_worker] WITHOUT LOGIN;
            ALTER ROLE [pegasus_worker_runtime_role] ADD MEMBER [pegasus_test_lookup_worker];
            """);
        await using var connection = database.CreateConnection();
        await connection.OpenAsync();
        await using var impersonation = connection.CreateCommand();
        impersonation.CommandText = "EXECUTE AS USER = N'pegasus_test_lookup_worker';";
        await impersonation.ExecuteNonQueryAsync();
        try
        {
            var options = new DbContextOptionsBuilder<PegasusDbContext>().UseSqlServer(connection).Options;
            await record(new EfVehicleLookupWorkStore(new ConnectedContextFactory(options)));
        }
        finally
        {
            impersonation.CommandText = "REVERT;";
            await impersonation.ExecuteNonQueryAsync();
        }
    }

    private sealed class ConnectedContextFactory(DbContextOptions<PegasusDbContext> options)
        : IDbContextFactory<PegasusDbContext>
    {
        public PegasusDbContext CreateDbContext() => new(options);
    }

    private static async Task RecordLookupAsync(
        LocalDbTestDatabase database,
        Guid caseId,
        long mileage = 121_823,
        string? typeApproval = "M1",
        string? wheelplan = "2 AXLE RIGID BODY",
        int? revenueWeightKg = 1_800,
        int? engineCapacityCc = 1_461,
        string? fuel = "DIESEL",
        string? colour = "BLUE",
        bool clearTax = false,
        bool clearMot = false,
        VehicleLookupOutcome outcome = VehicleLookupOutcome.Current,
        VehicleLookupFailure? failure = null,
        string registration = FixtureRegistration,
        DateTimeOffset? recordedAtUtc = null,
        IVehicleLookupWorkStore? store = null)
    {
        var now = recordedAtUtc ?? FixedUtcNow;
        var workItemId = Guid.NewGuid();
        await using (var context = await database.CreateContextAsync())
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO ExternalWorkItems (Id, CaseId, Kind, OperationKey, State, AttemptCount, DueAtUtc) VALUES ({workItemId}, {caseId}, {ExternalWorkKinds.VehicleLookup}, {$"gap-fill-{workItemId:N}"}, {"pending"}, {0}, {FixedUtcNow})");
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO VehicleLookupRequests (WorkItemId, CaseId, Registration, OperationKey, RequestFingerprint, RequestedByKind, RequestedBySubjectId, RequestedByRolesJson, RequestedAtUtc, ResultingCaseVersion) VALUES ({workItemId}, {caseId}, {registration}, {$"gap-fill-{workItemId:N}"}, {new string('0', 64)}, {ActorKind.Automation.ToString()}, {"vehicle-lookup-reconciliation"}, {"[]"}, {FixedUtcNow}, {0L})");
        }

        await using var scope = database.CreateAsyncScope();
        var workStore = store ?? scope.ServiceProvider.GetRequiredService<IVehicleLookupWorkStore>();
        var claimed = Assert.IsType<VehicleLookupWorkItem>(
            await workStore.ClaimProcessingAsync(
                workItemId,
                FixedUtcNow,
                TimeSpan.FromMinutes(5),
                CancellationToken.None));
        // Both providers not finding the vehicle carries no evidence at all.
        VehicleDetails? vehicle = outcome == VehicleLookupOutcome.NotFound
            ? null
            : new(
                "RENAULT",
                "CAPTUR",
                2016,
                engineCapacityCc,
                fuel,
                typeApproval,
                wheelplan,
                revenueWeightKg,
                Colour: colour,
                TaxDueDate: clearTax ? null : FixtureTaxDueDate);
        IReadOnlyList<MotTestObservation> motTests = outcome == VehicleLookupOutcome.NotFound
            ? []
            : [new(new(2025, 9, 25), "PASSED", clearMot ? null : FixtureMotExpiry, mileage, VehicleMileageUnit.Miles)];
        var result = new VehicleLookupResult(
            registration,
            outcome,
            "offline-replay",
            "fixture-v1",
            $"gap-fill-response-{workItemId:N}",
            FixedUtcNow,
            FixedUtcNow,
            FixedUtcNow,
            vehicle,
            motTests,
            failure);
        await workStore.RecordOutcomeAsync(
            workItemId,
            claimed.LeaseToken!,
            new(result, VehicleMileagePolicy.Calculate(result.MotTests)),
            VehicleLookupWorkState.Completed,
            null,
            now,
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
            $"INSERT INTO Cases (Id, PrincipalId, SequenceLineageId, Year, Sequence, Reference, Type, InitialState, CustodyState, OriginIntakeReceiptId, InstructionComplete, ImagesComplete, CreatedAtUtc, Version, ConcurrencyToken) VALUES ({caseId}, {principalId}, {lineageId}, {2026}, {sequence}, {$"GAP{caseId:N}"[..10].ToUpperInvariant()}, {"inspection"}, {"review"}, {"confirmed"}, {receiptId}, {true}, {true}, {FixedUtcNow}, {0L}, {Guid.NewGuid()})");
        await CaseWorkFixture.InsertPrimaryWorksAsync(context);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CaseWorkflows (CaseId, State, Version, ConcurrencyToken) VALUES ({caseId}, {CaseLifecycleState.Review.ToString()}, {0L}, {Guid.NewGuid()})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CaseDataSnapshots (WorkId, OriginIntakeReceiptId, OriginSourceChannel, OriginExternalReceiptToken, OriginSourceHash, OriginReceivedAtUtc, SourceReaderKey, SourceReaderVersion, CompletenessPolicyKey, CompletenessPolicyVersion, CompletenessPolicySatisfied, AcceptedAtUtc) VALUES ({caseId}, {receiptId}, {"manual_upload"}, {"gap-fill-source"}, {new string('1', 64)}, {FixedUtcNow}, {"gap-fill-reader"}, {"1"}, {"gap-fill-completeness"}, {1}, {true}, {FixedUtcNow})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CaseDataFields (WorkId, FieldName, ValueKind, ValueType, Value, SourceKind, SourceIdentity, SourceLabel, PolicyKey, PolicyVersion) VALUES ({caseId}, {"vehicle_registration"}, {"fact"}, {"text"}, {FixtureRegistration}, {"intake_evidence"}, {"instruction.pdf"}, {"page 1"}, {"extraction"}, {1})");
        return caseId;
    }
}
