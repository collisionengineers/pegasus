using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

/// <summary>
/// The bounds and the authorisation the mail workspace reads through, and the
/// freshness rule the screen puts in front of an operator.
/// </summary>
public sealed class RetainedMailTests
{
    private static readonly DateTimeOffset NowUtc = new(2031, 9, 1, 8, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, 25)]
    [InlineData(10_001, 25)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task ListRefusesAPageOrSizeOutsideTheSupportedRange(int page, int pageSize)
    {
        var queries = new Queries();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            new ListRetainedMail(queries).ExecuteAsync(
                Caseworker(),
                new(null, MailFolderScope.Inbox),
                page,
                pageSize,
                CancellationToken.None));

        Assert.Empty(queries.Scopes);
    }

    [Fact]
    public async Task ListRefusesAFolderScopeThatIsNotDefined()
    {
        var queries = new Queries();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            new ListRetainedMail(queries).ExecuteAsync(
                Caseworker(),
                new(null, (MailFolderScope)7),
                1,
                25,
                CancellationToken.None));

        Assert.Empty(queries.Scopes);
    }

    [Fact]
    public async Task ListRefusesAMailboxIdentityLongerThanTheColumn()
    {
        var queries = new Queries();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new ListRetainedMail(queries).ExecuteAsync(
                Caseworker(),
                new(new string('m', 101), MailFolderScope.Inbox),
                1,
                25,
                CancellationToken.None));

        Assert.Empty(queries.Scopes);
    }

    [Fact]
    public async Task ListRequiresCaseworkAuthorization()
    {
        var queries = new Queries();

        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            new ListRetainedMail(queries).ExecuteAsync(
                ActionActor.RequestLink(Guid.NewGuid()),
                new(null, MailFolderScope.Inbox),
                1,
                25,
                CancellationToken.None));

        Assert.Empty(queries.Scopes);
    }

    [Fact]
    public async Task ListPassesTheRequestedScopeThrough()
    {
        var queries = new Queries();

        await new ListRetainedMail(queries).ExecuteAsync(
            Caseworker(),
            new("mailbox-a", MailFolderScope.Sent),
            3,
            25,
            CancellationToken.None);

        var scope = Assert.Single(queries.Scopes);
        Assert.Equal("mailbox-a", scope.Scope.MailboxId);
        Assert.Equal(MailFolderScope.Sent, scope.Scope.Folder);
        Assert.Equal(3, scope.Page);
        Assert.Equal(25, scope.PageSize);
    }

    [Fact]
    public async Task GetRequiresCaseworkAuthorizationAndAnIdentifier()
    {
        var queries = new Queries();

        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            new GetRetainedMail(queries).ExecuteAsync(
                ActionActor.RequestLink(Guid.NewGuid()),
                Guid.NewGuid(),
                CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            new GetRetainedMail(queries).ExecuteAsync(
                Caseworker(),
                Guid.Empty,
                CancellationToken.None));
    }

    [Fact]
    public async Task CorrectionPreservesEvidenceAndAppendsAnAttributedBeforeAfterEntry()
    {
        var original = MailClassificationResult.Unclassified(
            [new("provider-route", false, "No accepted provider route matched.")],
            "No supported category matched.",
            "shared-mail-policy",
            7);
        var store = new ClassificationStore(new(1, original, "system-worker:poll", NowUtc.AddMinutes(-1), []));
        var staffId = Guid.NewGuid();
        var sut = new CorrectRetainedMailClassification(store, new FixedTimeProvider(NowUtc));

        var result = await sut.ExecuteAsync(
            ActionActor.Staff(staffId, [StaffRole.User]),
            new(Guid.NewGuid(), 1, MailCategory.Received(ReceivedMailFamily.General, "acknowledgement"),
                "Confirmed from the retained message."));

        Assert.Equal(2, result!.Version);
        Assert.Equal("shared-mail-policy", result.Current.PolicyKey);
        Assert.Equal(7, result.Current.PolicyVersion);
        Assert.Equal(original.Predicates, result.Current.Predicates);
        var history = Assert.Single(result.History);
        Assert.Same(original, history.Before);
        Assert.Equal(result.Current, history.After);
        Assert.Equal($"staff:{staffId:D}", history.Actor);
        Assert.Equal(NowUtc, history.CorrectedAtUtc);
    }

    [Fact]
    public async Task CorrectionFailsClosedForAStaleVersionWithoutWriting()
    {
        var original = MailClassificationResult.Unclassified([], "No match.", "policy", 1);
        var store = new ClassificationStore(new(2, original, "system-worker:poll", NowUtc.AddMinutes(-1), []));
        var sut = new CorrectRetainedMailClassification(store, new FixedTimeProvider(NowUtc));

        await Assert.ThrowsAsync<MailClassificationConcurrencyException>(() => sut.ExecuteAsync(
            Caseworker(),
            new(Guid.NewGuid(), 1, MailCategory.Received(ReceivedMailFamily.InternalCc), "Reviewed.")));

        Assert.Equal(0, store.AppendCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CorrectionRequiresAnAttributableReason(string reason)
    {
        var original = MailClassificationResult.Unclassified([], "No match.", "policy", 1);
        var store = new ClassificationStore(new(1, original, "system-worker:poll", NowUtc.AddMinutes(-1), []));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new CorrectRetainedMailClassification(store, new FixedTimeProvider(NowUtc)).ExecuteAsync(
                Caseworker(),
                new(Guid.NewGuid(), 1, MailCategory.Received(ReceivedMailFamily.InternalCc), reason)));

        Assert.Equal(0, store.AppendCount);
    }

    [Fact]
    public void FreshnessIsUnavailableWhenNothingHasEverPolled() =>
        Assert.Equal(
            new MailFreshness(MailFreshnessState.Unavailable, null),
            GetRetainedMailFreshness.Evaluate([], NowUtc));

    [Fact]
    public void FreshnessIsUnavailableWhenEveryMailboxIsBackingOffAfterAFailure() =>
        Assert.Equal(
            MailFreshnessState.Unavailable,
            GetRetainedMailFreshness.Evaluate(
                [
                    new("mailbox-a", NowUtc.AddMinutes(-1), "mailbox_access_denied", NowUtc.AddMinutes(1)),
                    new("mailbox-b", NowUtc.AddMinutes(-2), "mailbox_poll_failure", NowUtc.AddMinutes(2))
                ],
                NowUtc).State);

    [Fact]
    public void OneHealthyMailboxIsEnoughToReportTheNewestSuccessfulPoll()
    {
        var freshness = GetRetainedMailFreshness.Evaluate(
            [
                new("mailbox-a", NowUtc.AddMinutes(-1), "mailbox_access_denied", NowUtc.AddMinutes(1)),
                new("mailbox-b", NowUtc.AddSeconds(-30), null, NowUtc)
            ],
            NowUtc);

        Assert.Equal(MailFreshnessState.Current, freshness.State);
        Assert.Equal(NowUtc.AddSeconds(-30), freshness.LastSuccessfulUpdateAtUtc);
    }

    [Fact]
    public void FreshnessTurnsStaleOnceThePollIsOlderThanTheThreshold()
    {
        var justInside = GetRetainedMailFreshness.Evaluate(
            [new("mailbox-a", NowUtc - GetRetainedMailFreshness.StaleAfter, null, NowUtc)],
            NowUtc);
        var justOutside = GetRetainedMailFreshness.Evaluate(
            [new("mailbox-a", NowUtc - GetRetainedMailFreshness.StaleAfter - TimeSpan.FromSeconds(1), null, NowUtc)],
            NowUtc);

        Assert.Equal(MailFreshnessState.Current, justInside.State);
        Assert.Equal(MailFreshnessState.Stale, justOutside.State);
    }

    [Fact]
    public void AMailboxThatHasNeverCompletedAPollIsUnavailableRatherThanInfinitelyStale() =>
        Assert.Equal(
            new MailFreshness(MailFreshnessState.Unavailable, null),
            GetRetainedMailFreshness.Evaluate(
                [new("mailbox-a", null, null, NowUtc)],
                NowUtc));

    private static ActionActor Caseworker() =>
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);

    private sealed class Queries : IRetainedMailQueries
    {
        internal List<(MailWorkspaceScope Scope, int Page, int PageSize)> Scopes { get; } = [];

        public Task<RetainedMailPage> ListAsync(
            MailWorkspaceScope scope,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
        {
            Scopes.Add((scope, page, pageSize));
            return Task.FromResult(new RetainedMailPage([], page, pageSize, 0, false));
        }

        public Task<RetainedMailDetail?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<RetainedMailDetail?>(null);

        public Task<IReadOnlyList<RetainedMailMailbox>> ListMailboxesAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RetainedMailMailbox>>([]);

        public Task<IReadOnlyList<MailPollHealth>> ListPollHealthAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MailPollHealth>>([]);
    }

    private sealed class ClassificationStore(MailClassificationDossier dossier)
        : IRetainedMailClassificationStore
    {
        internal int AppendCount { get; private set; }

        public Task<MailClassificationDossier?> GetClassificationAsync(
            Guid messageId,
            CancellationToken cancellationToken) => Task.FromResult<MailClassificationDossier?>(dossier);

        public Task<MailClassificationDossier> AppendCorrectionAsync(
            Guid messageId,
            int expectedVersion,
            MailClassificationResult before,
            MailClassificationResult after,
            string actor,
            string reason,
            DateTimeOffset correctedAtUtc,
            CancellationToken cancellationToken)
        {
            AppendCount++;
            return Task.FromResult(new MailClassificationDossier(
                expectedVersion + 1,
                after,
                actor,
                correctedAtUtc,
                [.. dossier.History, new(expectedVersion + 1, before, after, actor, reason, correctedAtUtc)]));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
