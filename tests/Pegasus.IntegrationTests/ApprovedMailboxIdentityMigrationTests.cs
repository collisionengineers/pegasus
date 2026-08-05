using Microsoft.EntityFrameworkCore;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The upgrade adds identity to the approved estate without touching a single existing
/// row's meaning. The already-deployed mailbox stays Approved with its identities unset,
/// which is exactly what the read-only configuration fallback then covers.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class ApprovedMailboxIdentityMigrationTests
{
    private const string PreviousMigration = "20260803205759_SendToAiAssessmentToolset";
    private const string IdentityMigration = "20260805210236_ApprovedMailboxGraphIdentity";

    [Fact]
    public async Task UpgradeAddsNullableIdentitiesAndLeavesTheSeededMailboxPollingUnchanged()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();

        await context.Database.MigrateAsync(PreviousMigration);
        await database.ExecuteAsync(ExistingMailboxSql);
        await context.Database.MigrateAsync(IdentityMigration);

        Assert.Equal(
            "Approved",
            await database.ScalarAsync<string>(
                "SELECT State FROM ApprovedMailboxes WHERE Address = 'instructions@collisionengineers.co.uk'"));
        Assert.Equal(
            1,
            await database.ScalarAsync<int>(
                "SELECT CONVERT(int, AllowInboundIntake) FROM ApprovedMailboxes WHERE Address = 'instructions@collisionengineers.co.uk'"));

        // Every pre-existing row keeps NULL identities: no literal tenant identifier was
        // invented by the migration.
        Assert.Equal(
            0,
            await database.ScalarAsync<int>(
                """
                SELECT COUNT(*) FROM ApprovedMailboxes
                WHERE MailboxIdentity IS NOT NULL
                   OR InboxFolderIdentity IS NOT NULL
                   OR SentFolderIdentity IS NOT NULL
                """));

        foreach (var (column, length) in new[]
                 {
                     ("MailboxIdentity", 100),
                     ("InboxFolderIdentity", 200),
                     ("SentFolderIdentity", 200)
                 })
        {
            Assert.Equal(
                1,
                await database.ScalarAsync<int>(
                    $"""
                    SELECT COUNT(*) FROM sys.columns
                    WHERE object_id = OBJECT_ID(N'ApprovedMailboxes')
                      AND name = N'{column}'
                      AND is_nullable = 1
                      AND max_length = {length * 2}
                    """));
        }

        Assert.Equal(
            1,
            await database.ScalarAsync<int>(
                """
                SELECT COUNT(*) FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'ApprovedMailboxes')
                  AND name = N'IX_ApprovedMailboxes_MailboxIdentity'
                  AND is_unique = 1
                  AND has_filter = 1
                """));

        // The filter is what lets several rows wait for their identities at once while
        // still refusing two rows that claim the same mailbox.
        Assert.Equal(
            2,
            await database.ScalarAsync<int>(
                "SELECT COUNT(*) FROM ApprovedMailboxes WHERE MailboxIdentity IS NULL"));
        await database.ExecuteAsync(
            """
            UPDATE ApprovedMailboxes SET MailboxIdentity = 'bound-mailbox'
            WHERE Address = 'instructions@collisionengineers.co.uk';
            """);
        var duplicate = await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() =>
            database.ExecuteAsync(
                """
                UPDATE ApprovedMailboxes SET MailboxIdentity = 'bound-mailbox'
                WHERE Address = 'second@collisionengineers.co.uk';
                """));
        Assert.Contains(
            "IX_ApprovedMailboxes_MailboxIdentity",
            duplicate.Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// A second approved row that predates the identity columns, to prove the upgrade
    /// carries more than the seeded row.
    /// </summary>
    private const string ExistingMailboxSql =
        """
        INSERT INTO ApprovedMailboxes
            (Id, Address, AllowInboundIntake, AllowSentEvidence, State, Version)
        VALUES
            ('8b2b9a1c-4d20-4a0f-9a51-0d5a8e73c910',
             'second@collisionengineers.co.uk', 1, 0, 'Approved', 1);
        """;
}
