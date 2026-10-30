using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

/// <summary>
/// The two read-only old-system lookups (operator, 30 October 2026, R550):
/// casework authorisation, the term and cap bounds of the Deleted Items
/// search they replace, and an Unavailable source reported as such.
/// </summary>
public sealed class OldSystemLookupTests
{
    private static readonly ActionActor Automation = ActionActor.Automation("pegasus-automation");

    [Fact]
    public async Task BothLookupsRequireCaseworkAndAdmitTheAutomationActor()
    {
        var folders = new RecordingFolderSearch();
        var mailboxes = new RecordingMailboxSearch();
        var principal = ActionActor.Principal(Guid.NewGuid());

        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            new SearchOldSystemCaseFolders(folders).ExecuteAsync(principal, "QDOS26070"));
        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            new SearchApprovedMailboxes(mailboxes).ExecuteAsync(principal, null, "QDOS26070"));
        Assert.Empty(folders.Terms);
        Assert.Empty(mailboxes.Terms);

        await new SearchOldSystemCaseFolders(folders).ExecuteAsync(Automation, "  QDOS26070 ", 10);
        await new SearchApprovedMailboxes(mailboxes).ExecuteAsync(Automation, null, " AB12 CDE ", 25);

        Assert.Equal(("QDOS26070", 10), Assert.Single(folders.Terms));
        Assert.Equal(("AB12 CDE", 25), Assert.Single(mailboxes.Terms));
        await new SearchOldSystemCaseFolders(folders).ExecuteAsync(
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]), "QDOS26070");
        Assert.Equal(2, folders.Terms.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task ABlankTermIsRefusedBeforeAnySourceIsAsked(string? term)
    {
        var folders = new RecordingFolderSearch();
        var mailboxes = new RecordingMailboxSearch();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new SearchOldSystemCaseFolders(folders).ExecuteAsync(Automation, term!));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            new SearchApprovedMailboxes(mailboxes).ExecuteAsync(Automation, null, term!));
        Assert.Empty(folders.Terms);
        Assert.Empty(mailboxes.Terms);
    }

    [Fact]
    public async Task TheTermAndTheCapAreBounded()
    {
        var folders = new RecordingFolderSearch();
        var mailboxes = new RecordingMailboxSearch();
        var tooLong = new string('x', OldSystemLookupBounds.MaximumTermLength + 1);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new SearchOldSystemCaseFolders(folders).ExecuteAsync(Automation, tooLong));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            new SearchOldSystemCaseFolders(folders).ExecuteAsync(Automation, "QDOS26070", 0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            new SearchApprovedMailboxes(mailboxes).ExecuteAsync(
                Automation, null, "QDOS26070", OldSystemLookupBounds.MaximumItems + 1));
        Assert.Empty(folders.Terms);
        Assert.Empty(mailboxes.Terms);
    }

    [Fact]
    public async Task AnUnavailableSourceIsReportedAsSuchNotAsNothingFound()
    {
        var folders = await new SearchOldSystemCaseFolders(new UnavailableOldSystemCaseFolderSearch())
            .ExecuteAsync(Automation, "QDOS26070");
        var mailboxes = await new SearchApprovedMailboxes(new UnavailableApprovedMailboxSearchSource())
            .ExecuteAsync(Automation, null, "QDOS26070");

        Assert.Equal(OldSystemLookupState.Unavailable, folders.State);
        Assert.Empty(folders.Items);
        Assert.Equal(OldSystemLookupState.Unavailable, mailboxes.State);
        Assert.Empty(mailboxes.Items);
    }

    [Fact]
    public void AMailboxSearchIsAvailableWhenAnyMailboxAnswered()
    {
        var admitted = new MailboxSearchMailbox(Guid.NewGuid(), "desk@example.test", OldSystemLookupState.Available);
        var refused = new MailboxSearchMailbox(Guid.NewGuid(), "info@example.test", OldSystemLookupState.Unavailable);

        Assert.Equal(OldSystemLookupState.Available, new MailboxSearchResult([], [admitted, refused], false).State);
        Assert.Equal(OldSystemLookupState.Unavailable, new MailboxSearchResult([], [refused], false).State);
        Assert.Equal(OldSystemLookupState.Unavailable, new MailboxSearchResult([], [], false).State);
    }

    private sealed class RecordingFolderSearch : IOldSystemCaseFolderSearch
    {
        public List<(string Term, int Maximum)> Terms { get; } = [];

        public Task<OldSystemCaseFolderSearchResult> SearchAsync(
            string term,
            int maximumItems,
            CancellationToken cancellationToken)
        {
            Terms.Add((term, maximumItems));
            return Task.FromResult(new OldSystemCaseFolderSearchResult([], false));
        }
    }

    private sealed class RecordingMailboxSearch : IApprovedMailboxSearchSource
    {
        public List<(string Term, int Maximum)> Terms { get; } = [];

        public Task<MailboxSearchResult> SearchAsync(
            Guid? mailboxId,
            string term,
            int maximumItemsPerMailbox,
            CancellationToken cancellationToken)
        {
            Terms.Add((term, maximumItemsPerMailbox));
            return Task.FromResult(new MailboxSearchResult([], [], false));
        }
    }
}
