using Pegasus.Infrastructure.Email;

namespace Pegasus.IntegrationTests;

/// <summary>
/// DevelopmentOffline's mailbox-administration resolve fake (MAIL-002): no Graph tenant
/// exists locally, so this always succeeds with a stable, address-derived identity.
/// </summary>
public sealed class LocalApprovedMailboxIdentityResolverTests
{
    [Fact]
    public async Task ResolvesTheSameIdentityForTheSameAddressEveryTime()
    {
        var resolver = new LocalApprovedMailboxIdentityResolver();

        var first = await resolver.ResolveAsync("estate@collisionengineers.co.uk", CancellationToken.None);
        var second = await resolver.ResolveAsync("Estate@CollisionEngineers.co.uk", CancellationToken.None);

        Assert.NotNull(first);
        Assert.Equal(first!.MailboxIdentity, second!.MailboxIdentity);
        Assert.Equal(first.InboxFolderIdentity, second.InboxFolderIdentity);
        Assert.Equal(first.SentFolderIdentity, second.SentFolderIdentity);
        Assert.Equal(first.FolderBindings, second.FolderBindings);
    }

    [Fact]
    public async Task ResolvesADifferentIdentityForADifferentAddress()
    {
        var resolver = new LocalApprovedMailboxIdentityResolver();

        var first = await resolver.ResolveAsync("estate@collisionengineers.co.uk", CancellationToken.None);
        var second = await resolver.ResolveAsync("another@collisionengineers.co.uk", CancellationToken.None);

        Assert.NotEqual(first!.MailboxIdentity, second!.MailboxIdentity);
    }
}
