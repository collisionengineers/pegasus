using Microsoft.EntityFrameworkCore;

namespace Pegasus.IntegrationTests;

/// <summary>
/// 20261027120000_RetainedMailReplyIdentities: a retained message keeps the
/// exact Message-IDs it answers, so automatic association can link a reply to
/// the Case of the message it answers (D13, item T; operator, 27 October 2026).
/// The column is additive and nullable, so a row retained before the migration
/// keeps its evidence and links only by the signals it already carried.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class RetainedMailReplyIdentitiesMigrationTests
{
    private const string PreviousMigration = "20261023160000_AppliedValuationCalculationRows";
    private const string MessageId = "a7000000-0000-0000-0000-0000000000a1";
    private const string Recorded = "2031-05-06T10:30:00+00:00";

    [Fact]
    public async Task MigrationAddsANullableReplyIdentityColumnAndKeepsExistingRows()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();
        await context.Database.MigrateAsync(PreviousMigration);
        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'RetainedMailboxMessages') AND name = N'InReplyToIdentitiesJson'"));

        await database.ExecuteAsync($"""
            INSERT INTO RetainedMailboxMessages
                (Id, MailboxId, MailboxAddress, FolderScope, FolderIdentity, ImmutableMessageId,
                 ConversationIdentity, ExternalReceiptToken, ToAddressesJson, CcAddressesJson,
                 SourceLength, SourceSha256, ReceivedAtUtc, RetainedAtUtc, IsRead)
            VALUES
                ('{MessageId}', NULL, N'intake@example.test', N'inbox', N'inbox-folder',
                 N'immutable-before', N'conversation-before', N'retained-before', N'[]', N'[]',
                 1, REPLICATE(N'1', 64), '{Recorded}', '{Recorded}', 0);
            """);

        await context.Database.MigrateAsync();

        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'RetainedMailboxMessages') AND name = N'InReplyToIdentitiesJson' AND is_nullable = 1"));
        Assert.Equal(
            "retained-before",
            await database.ScalarAsync<string>(
                $"SELECT ExternalReceiptToken FROM RetainedMailboxMessages WHERE Id = '{MessageId}'"));
        Assert.Null(await database.ScalarAsync<string>(
            $"SELECT InReplyToIdentitiesJson FROM RetainedMailboxMessages WHERE Id = '{MessageId}'"));
    }
}
