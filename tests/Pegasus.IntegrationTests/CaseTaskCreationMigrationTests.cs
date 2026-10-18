using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Pegasus.IntegrationTests;

/// <summary>
/// 20261018070000_CaseTaskCreation: every task takes its creation instant from
/// its earliest <c>case_task_created</c> history row and its creator from that
/// row only when a staff member made it; the instant is then NOT NULL with no
/// default; and a task with no such row stops the migration, which is then not
/// recorded as applied.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class CaseTaskCreationMigrationTests
{
    private const string PreviousMigration = "20261018060000_PersistNormalizedVehicleRegistration";
    private const string Migration = "20261018070000_CaseTaskCreation";
    private const string CaseId = "c1000000-0000-0000-0000-000000000001";
    private const string StaffTaskId = "c1000000-0000-0000-0000-0000000000a1";
    private const string SystemTaskId = "c1000000-0000-0000-0000-0000000000a2";
    private const string StaffId = "c1000000-0000-0000-0000-0000000000b1";
    private const string Recorded = "2031-05-06T10:30:00+00:00";

    private static readonly DateTimeOffset StaffCreatedAtUtc = new(2031, 5, 6, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SystemCreatedAtUtc = new(2031, 5, 6, 11, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task MigrationFillsEachTaskFromItsCreationHistoryAndNamesOnlyAStaffCreator()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();
        await context.Database.MigrateAsync(PreviousMigration);
        await database.ExecuteAsync(CaseSql);
        await database.ExecuteAsync(TasksSql);

        await context.Database.MigrateAsync(Migration);

        Assert.Equal(Migration, (await context.Database.GetAppliedMigrationsAsync()).Last());
        Assert.Equal(StaffCreatedAtUtc, await database.ScalarAsync<DateTimeOffset>(
            $"SELECT CreatedAtUtc FROM CaseTasks WHERE Id = '{StaffTaskId}'"));
        Assert.Equal(Guid.Parse(StaffId), await database.ScalarAsync<Guid>(
            $"SELECT CreatedByStaffId FROM CaseTasks WHERE Id = '{StaffTaskId}'"));
        Assert.Equal(SystemCreatedAtUtc, await database.ScalarAsync<DateTimeOffset>(
            $"SELECT CreatedAtUtc FROM CaseTasks WHERE Id = '{SystemTaskId}'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseTasks WHERE Id = '{SystemTaskId}' AND CreatedByStaffId IS NULL"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'CaseTasks') AND name = N'CreatedAtUtc' AND is_nullable = 0"));
        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'CaseTasks')"));
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task MigrationStopsWhenATaskHasNoCreationHistoryAndIsNotRecorded()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();
        await context.Database.MigrateAsync(PreviousMigration);
        await database.ExecuteAsync(CaseSql);
        await database.ExecuteAsync(TasksSql);
        await database.ExecuteAsync(
            $"""
            INSERT INTO CaseTasks (Id, CaseId, Description, State, Version, ConcurrencyToken)
            VALUES ('c1000000-0000-0000-0000-0000000000a3', '{CaseId}', N'No creation history', N'Open', 0, NEWID());
            """);

        var refused = await Assert.ThrowsAsync<SqlException>(() => context.Database.MigrateAsync(Migration));

        Assert.Contains("no case_task_created history row", refused.Message, StringComparison.Ordinal);
        Assert.Equal(PreviousMigration, (await context.Database.GetAppliedMigrationsAsync()).Last());
        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'CaseTasks') AND name IN (N'CreatedAtUtc', N'CreatedByStaffId')"));
    }

    // One Case with its workflow, as a task needs.
    private const string CaseSql =
        $"""
        INSERT INTO Organizations (Role, Id, Name, Version)
        VALUES (N'principal', 'c2000000-0000-0000-0000-000000000001', N'Case task creation provider', 0);

        INSERT INTO PrincipalSequenceLineages (Id, CreatedAtUtc)
        VALUES ('c2000000-0000-0000-0000-000000000002', '{Recorded}');

        INSERT INTO Principals
            (Id, OrganizationId, Code, SequenceLineageId, PredecessorId, SuccessorId, IsActive, Version)
        VALUES
            ('c2000000-0000-0000-0000-000000000003', 'c2000000-0000-0000-0000-000000000001', N'CTCM',
             'c2000000-0000-0000-0000-000000000002', NULL, NULL, 1, 0);

        INSERT INTO Cases
            (Id, PrincipalId, SequenceLineageId, Year, Sequence, Reference, Type, InitialState,
             CustodyState, InstructionComplete, ImagesComplete, CreatedAtUtc, Version, ConcurrencyToken)
        VALUES
            ('{CaseId}', 'c2000000-0000-0000-0000-000000000003', 'c2000000-0000-0000-0000-000000000002',
             2031, 1, N'CTCM31001', N'inspection', N'review', N'pending', 1, 1, '{Recorded}', 0, NEWID());

        INSERT INTO CaseWorkflows (CaseId, State, Version, EditLeaseGeneration, ConcurrencyToken)
        VALUES ('{CaseId}', N'Review', 1, 0, NEWID());
        """;

    // A staff-created task and a system-created one, each with the lowercase
    // task id the history records. The system actor's subject is shaped like a
    // staff id, so only the actor kind keeps it from being named a creator.
    private const string TasksSql =
        $"""
        INSERT INTO CaseTasks (Id, CaseId, Description, State, Version, ConcurrencyToken)
        VALUES
            ('{StaffTaskId}', '{CaseId}', N'Chase the payment', N'Open', 0, NEWID()),
            ('{SystemTaskId}', '{CaseId}', N'Check the report was received', N'Open', 0, NEWID());

        INSERT INTO ActionHistory
            (Id, AggregateType, AggregateId, EventKind, ActorKind, ActorSubjectId, ActorRolesJson,
             OccurredAtUtc, Outcome, CorrelationId)
        VALUES
            ('c3000000-0000-0000-0000-000000000001', N'case_task', N'{StaffTaskId}', N'case_task_created',
             N'Staff', N'{StaffId}', N'[]', '2031-05-06T09:00:00+00:00', N'Succeeded', N'create-staff'),
            ('c3000000-0000-0000-0000-000000000002', N'case_task', N'{StaffTaskId}', N'case_task_assigned',
             N'Staff', N'c1000000-0000-0000-0000-0000000000b2', N'[]', '2031-05-06T10:00:00+00:00',
             N'Succeeded', N'assign-staff'),
            ('c3000000-0000-0000-0000-000000000003', N'case_task', N'{SystemTaskId}', N'case_task_created',
             N'SystemWorker', N'{StaffId}', N'[]', '2031-05-06T11:00:00+00:00', N'Succeeded', N'create-system');
        """;
}
