using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class RetainedMailDismissalPersistenceTests
{
    private const string AggregateType = "retained_mail";

    [Fact]
    public async Task DismissAndRestoreRecordOneHistoryEntryEachAndAReplayReturnsTheCommittedState()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var messageId = await SeedMessageAsync(database);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        var dismiss = new DismissRetainedMailRequest(messageId, actor, Guid.NewGuid().ToString("N"));

        var first = await DismissAsync(database, dismiss);
        Assert.NotNull(first);
        Assert.True(first.IsDismissed);
        Assert.False(first.IsReplay);

        var replay = await DismissAsync(database, dismiss);
        Assert.NotNull(replay);
        Assert.True(replay.IsReplay);
        Assert.True(replay.IsDismissed);
        Assert.Equal(first.DismissedAtUtc, replay.DismissedAtUtc);

        var restore = new RestoreRetainedMailRequest(messageId, actor, Guid.NewGuid().ToString("N"));
        var restored = await RestoreAsync(database, restore);
        Assert.NotNull(restored);
        Assert.False(restored.IsDismissed);
        Assert.False(restored.IsReplay);
        Assert.True((await RestoreAsync(database, restore))!.IsReplay);

        await using var context = await database.CreateContextAsync();
        Assert.Equal(2, await context.ActionHistory.CountAsync(item => item.AggregateType == AggregateType));
        var message = await context.Set<RetainedMailboxMessageEntity>().SingleAsync(item => item.Id == messageId);
        Assert.Null(message.DismissedAtUtc);
        Assert.Null(message.DismissedBySubjectId);
    }

    [Fact]
    public async Task AnUnknownMessageIsNotRetained()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);

        Assert.Null(await DismissAsync(
            database, new(Guid.NewGuid(), actor, Guid.NewGuid().ToString("N"))));
    }

    [Fact]
    public async Task ConcurrentReplaysAndConcurrentDismissalsOfOtherMessagesDoNotDeadlock()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        var sameMessage = await SeedMessageAsync(database);
        var sameKey = new DismissRetainedMailRequest(sameMessage, actor, Guid.NewGuid().ToString("N"));
        var others = new List<DismissRetainedMailRequest>();
        for (var index = 0; index < 4; index++)
        {
            others.Add(new(await SeedMessageAsync(database), actor, Guid.NewGuid().ToString("N")));
        }

        var results = await Task.WhenAll(
            [
                DismissAsync(database, sameKey),
                DismissAsync(database, sameKey),
                DismissAsync(database, sameKey),
                .. others.Select(request => DismissAsync(database, request))
            ]);

        Assert.All(results, result => Assert.True(result!.IsDismissed));
        Assert.Equal(1, results.Take(3).Count(result => !result!.IsReplay));
        Assert.Equal(2, results.Take(3).Count(result => result!.IsReplay));
        await using var context = await database.CreateContextAsync();
        Assert.Equal(5, await context.ActionHistory.CountAsync(item => item.AggregateType == AggregateType));
    }

    private static async Task<RetainedMailDismissal?> DismissAsync(
        LocalDbTestDatabase database, DismissRetainedMailRequest request)
    {
        await using var scope = database.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IRetainedMailDismissalStore>()
            .DismissAsync(request, default);
    }

    private static async Task<RetainedMailDismissal?> RestoreAsync(
        LocalDbTestDatabase database, RestoreRetainedMailRequest request)
    {
        await using var scope = database.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IRetainedMailDismissalStore>()
            .RestoreAsync(request, default);
    }

    private static async Task<Guid> SeedMessageAsync(LocalDbTestDatabase database)
    {
        var id = Guid.NewGuid();
        await using var context = await database.CreateContextAsync();
        context.Set<RetainedMailboxMessageEntity>().Add(new()
        {
            Id = id,
            MailboxAddress = "mailbox@example.invalid",
            FolderScope = "Inbox",
            FolderIdentity = "inbox",
            ImmutableMessageId = $"immutable-{id:N}",
            ExternalReceiptToken = $"retained:{id:N}",
            ToAddressesJson = "[]",
            CcAddressesJson = "[]",
            SourceLength = 1,
            SourceSha256 = new string('A', 64),
            ReceivedAtUtc = DateTimeOffset.UtcNow,
            RetainedAtUtc = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();
        return id;
    }
}
