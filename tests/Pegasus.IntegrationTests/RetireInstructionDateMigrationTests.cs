using Microsoft.EntityFrameworkCore;

namespace Pegasus.IntegrationTests;

/// <summary>
/// 20260925120000_RetireInstructionDate: the Case's instruction date is its
/// Received date, so every instruction_date case-data row goes (fact,
/// suggestion and staff-confirmed alike), the draft's InstructionDate column
/// is dropped, every other case-data row survives, and the migration cannot
/// be reverted.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class RetireInstructionDateMigrationTests
{
    private const string PreviousMigration = "20260925090000_VehicleLookupDerivedFacts";
    private const string Migration = "20260925120000_RetireInstructionDate";
    private const string CaseId = "b1000000-0000-0000-0000-000000000001";
    private const string Recorded = "2031-05-06T10:30:00+00:00";

    [Fact]
    public async Task MigrationDeletesEveryInstructionDateRowAndDropsTheDraftColumn()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();
        await context.Database.MigrateAsync(PreviousMigration);
        await database.ExecuteAsync(SeedSql);

        await context.Database.MigrateAsync(Migration);

        Assert.Equal(Migration, (await context.Database.GetAppliedMigrationsAsync()).Last());
        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM CaseDataFields WHERE FieldName = N'instruction_date'"));
        Assert.Equal("Retired instruction date claimant", await database.ScalarAsync<string>(
            $"SELECT Value FROM CaseDataFields WHERE WorkId = '{CaseId}' AND FieldName = N'claimant_name' AND ValueKind = N'fact'"));
        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'InstructionDrafts') AND name = N'InstructionDate'"));
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task MigrationCannotBeReverted()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false, useTemplate: false);
        await using var context = await database.CreateContextAsync();
        await context.Database.MigrateAsync(Migration);

        var exception = await Assert.ThrowsAsync<NotSupportedException>(
            () => context.Database.MigrateAsync(PreviousMigration));

        Assert.StartsWith("RetireInstructionDate is forward-only", exception.Message, StringComparison.Ordinal);
        Assert.Equal(Migration, (await context.Database.GetAppliedMigrationsAsync()).Last());
    }

    // One Case with its primary work and accepted snapshot at the schema
    // before RetireInstructionDate: an instruction date as an intake fact, a
    // suggestion and a staff confirmation, beside a claimant name that stays.
    private const string SeedSql =
        $"""
        INSERT INTO Organizations (Id, Name, Version)
        VALUES ('b2000000-0000-0000-0000-000000000001', N'Retire instruction date provider', 0);

        INSERT INTO PrincipalSequenceLineages (Id, CreatedAtUtc)
        VALUES ('b2000000-0000-0000-0000-000000000002', '{Recorded}');

        INSERT INTO Principals
            (Id, OrganizationId, Code, SequenceLineageId, PredecessorId, SuccessorId, IsActive, Version)
        VALUES
            ('b2000000-0000-0000-0000-000000000003', 'b2000000-0000-0000-0000-000000000001', N'RIDT',
             'b2000000-0000-0000-0000-000000000002', NULL, NULL, 1, 0);

        INSERT INTO Cases
            (Id, PrincipalId, SequenceLineageId, Year, Sequence, Reference, Type, InitialState,
             CustodyState, InstructionComplete, ImagesComplete, CreatedAtUtc, Version, ConcurrencyToken)
        VALUES
            ('{CaseId}', 'b2000000-0000-0000-0000-000000000003', 'b2000000-0000-0000-0000-000000000002',
             2031, 1, N'RIDT31001', N'inspection', N'review', N'pending', 1, 1, '{Recorded}', 0, NEWID());

        INSERT INTO CaseWorks (Id, CaseId, Kind, CreatedAtUtc)
        VALUES ('{CaseId}', '{CaseId}', N'primary', '{Recorded}');

        INSERT INTO CaseDataSnapshots
            (WorkId, AcceptedAtUtc, CompletenessPolicyKey, CompletenessPolicySatisfied, CompletenessPolicyVersion)
        VALUES ('{CaseId}', '{Recorded}', N'case-workflow', 1, 1);

        INSERT INTO CaseDataFields
            (WorkId, FieldName, ValueKind, ValueType, Value, SourceKind, SourceIdentity, SourceLabel,
             PolicyKey, PolicyVersion, ConfirmedByActor, ConfirmedAtUtc)
        VALUES
            ('{CaseId}', N'instruction_date', N'fact', N'date', N'2031-05-01', N'intake_evidence',
             N'migration-test', N'Migration test', N'case-workflow', 1, NULL, NULL),
            ('{CaseId}', N'instruction_date', N'suggestion', N'date', N'2031-05-02', N'intake_evidence',
             N'migration-test', N'Migration test', N'case-workflow', 1, NULL, NULL),
            ('{CaseId}', N'instruction_date', N'confirmed', N'date', N'2031-05-03', N'staff_correction',
             N'migration-test', N'Migration test', N'case-workflow', 1, N'staff-1', '{Recorded}'),
            ('{CaseId}', N'claimant_name', N'fact', N'text', N'Retired instruction date claimant',
             N'intake_evidence', N'migration-test', N'Migration test', N'case-workflow', 1, NULL, NULL);
        """;
}
