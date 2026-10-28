using Microsoft.EntityFrameworkCore;

namespace Pegasus.IntegrationTests;

/// <summary>
/// 20261027140000_AssignableCaseQueries adds the assignee columns and the
/// Query assigned notification cause. Existing query rows stay Unassigned.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class AssignableCaseQueriesMigrationTests
{
    [Fact]
    public async Task MigrationAddsTheAssigneeColumnsAndTheQueryAssignedCause()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();
        await context.Database.MigrateAsync("20261023160000_AppliedValuationCalculationRows");

        await context.Database.MigrateAsync();

        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.Equal(1, await database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM sys.columns
            WHERE object_id = OBJECT_ID(N'CaseQueries') AND name = N'AssigneeStaffId'
            """));
        Assert.Equal(1, await database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM sys.columns
            WHERE object_id = OBJECT_ID(N'CaseQueries') AND name = N'AssignedAtUtc'
            """));
        Assert.Equal(1, await database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM sys.indexes
            WHERE object_id = OBJECT_ID(N'CaseQueries')
              AND name = N'IX_CaseQueries_AssigneeStaffId_RaisedAtUtc'
              AND has_filter = 1
            """));
        Assert.Equal(1, await database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM sys.check_constraints
            WHERE parent_object_id = OBJECT_ID(N'StaffNotifications')
              AND name = N'CK_StaffNotifications_Cause'
              AND definition LIKE N'%QueryAssigned%'
            """));
    }
}
