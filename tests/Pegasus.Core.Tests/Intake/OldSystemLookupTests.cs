using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

/// <summary>
/// The two read-only old-system lookups (operator, 30 October 2026, R561):
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

    // R592: the old-system folders are named by the firm's own Case reference,
    // so a reference-shaped term keeps only the folders that name it as a
    // whole token; Box's full-text hits for anything else pass through.
    [Theory]
    [InlineData("QDOS26112", "QDOS26112|a.QDOS26112 Smith|Smith (QDOS26112)")]
    [InlineData("a.QDOS26112", "QDOS26112|a.QDOS26112 Smith|Smith (QDOS26112)")]
    [InlineData("t.QDOS26112", "QDOS26112|a.QDOS26112 Smith|Smith (QDOS26112)")]
    [InlineData("qdos26112", "QDOS26112|a.QDOS26112 Smith|Smith (QDOS26112)")]
    [InlineData("AB12CDE", "AB12CDE Jones")]
    [InlineData("AB12CDE.2", "AB12CDE.2 Jones")]
    [InlineData("a.AB12CDE", "AB12CDE Jones")]
    public async Task AReferenceShapedTermKeepsOnlyTheFoldersThatNameIt(string term, string expectedNames)
    {
        var folders = new RecordingFolderSearch
        {
            Hits =
            [
                "QDOS26112",
                "a.QDOS26112 Smith",
                "Smith (QDOS26112)",
                "QDOS261125",
                "QDOS26112B",
                "Smith QDOS",
                "AB12CDE Jones",
                "AB12CDE.2 Jones",
                "AB12CDEF",
            ],
        };

        var result = await new SearchOldSystemCaseFolders(folders).ExecuteAsync(Automation, term);

        Assert.Equal(expectedNames.Split('|'), result.Items.Select(folder => folder.Name).ToArray());
        Assert.Equal(OldSystemLookupState.Available, result.State);
    }

    [Theory]
    [InlineData("Smith")]
    [InlineData("AMA/49679/1")]
    [InlineData("49679")]
    [InlineData("SD71 KBU")]
    public async Task ATermThatIsNotAReferenceKeepsEveryHitTheSourceReturned(string term)
    {
        string[] hits = ["Smith QDOS", "AMA-49679-1 Gutaj", "unrelated"];
        var folders = new RecordingFolderSearch { Hits = hits };

        var result = await new SearchOldSystemCaseFolders(folders).ExecuteAsync(Automation, term);

        Assert.Equal(hits, result.Items.Select(folder => folder.Name).ToArray());
    }

    [Fact]
    public async Task TheFilterKeepsTheSourcesTruncationAndState()
    {
        var folders = new RecordingFolderSearch { Hits = ["QDOS261125"], IsTruncated = true };

        var result = await new SearchOldSystemCaseFolders(folders).ExecuteAsync(Automation, "QDOS26112");

        Assert.Empty(result.Items);
        Assert.True(result.IsTruncated);
        Assert.Equal(OldSystemLookupState.Available, result.State);
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

        public string[] Hits { get; init; } = [];

        public bool IsTruncated { get; init; }

        public Task<OldSystemCaseFolderSearchResult> SearchAsync(
            string term,
            int maximumItems,
            CancellationToken cancellationToken)
        {
            Terms.Add((term, maximumItems));
            return Task.FromResult(new OldSystemCaseFolderSearchResult(
                Hits.Select((name, index) => new OldSystemCaseFolder(index.ToString(), name, null)).ToArray(),
                IsTruncated));
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
