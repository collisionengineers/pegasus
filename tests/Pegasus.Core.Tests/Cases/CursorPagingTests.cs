using System.Buffers.Text;
using System.Text;
using System.Text.Json;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Cases;

/// <summary>
/// CASE-047: the stable-cursor primitives (<see cref="CursorPaging"/>, the
/// internal <c>CursorToken</c> codec) and the fingerprint rule every cursor
/// query shares — a cursor minted for one actor, filter set, order, or case
/// is refused everywhere else.
/// </summary>
public sealed class CursorPagingTests
{
    [Fact]
    public void ANullLimitTakesTheDefault() =>
        Assert.Equal(CursorPaging.DefaultLimit, CursorPaging.NormalizeLimit(null));

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(CursorPaging.MaximumLimit)]
    public void AnInRangeLimitPassesThroughUnchanged(int limit) =>
        Assert.Equal(limit, CursorPaging.NormalizeLimit(limit));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(CursorPaging.MaximumLimit + 1)]
    public void AnOutOfRangeLimitIsRefusedNeverClamped(int limit) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => CursorPaging.NormalizeLimit(limit));

    [Fact]
    public void ATokenRoundTripsItsSortKeyAndId()
    {
        var id = Guid.NewGuid();

        var token = CursorToken.Encode("2031-05-06T10:00:00", id, "fingerprint-a");
        var (sortKey, decodedId) = CursorToken.Decode(token, "fingerprint-a");

        Assert.Equal("2031-05-06T10:00:00", sortKey);
        Assert.Equal(id, decodedId);
    }

    [Fact]
    public void TicksRoundTripThroughTheDateTimeOffsetSortKeyHelpers()
    {
        var value = new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

        Assert.Equal(value, CursorToken.DecodeTicks(CursorToken.EncodeTicks(value)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-cursor")]
    [InlineData("!!!not-base64url!!!")]
    public void AMalformedCursorIsRefused(string cursor) =>
        Assert.Throws<CursorRejectedException>(() => CursorToken.Decode(cursor, "fingerprint-a"));

    [Fact]
    public void ACursorWithAForeignFingerprintIsRefused()
    {
        var token = CursorToken.Encode("k", Guid.NewGuid(), "fingerprint-a");

        Assert.Throws<CursorRejectedException>(() => CursorToken.Decode(token, "fingerprint-b"));
    }

    [Fact]
    public void AStaleTokenVersionIsRefusedRegardlessOfFingerprint()
    {
        var payload = JsonSerializer.Serialize(new { v = 2, k = "k", id = Guid.NewGuid(), f = "fingerprint-a" });
        var token = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(payload));

        Assert.Throws<CursorRejectedException>(() => CursorToken.Decode(token, "fingerprint-a"));
    }

    [Fact]
    public async Task ACursorMintedForOneActorIsRefusedForAnother()
    {
        var search = new SearchCasesByCursor(new FakeCaseQueryStore());
        var actorA = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var actorB = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var filters = new CaseSearchFilters();

        var firstPage = await search.ExecuteAsync(new(actorA, filters, Limit: 1), CancellationToken.None);
        Assert.NotNull(firstPage.NextCursor);

        await Assert.ThrowsAsync<CursorRejectedException>(() => search.ExecuteAsync(
            new(actorB, filters, Cursor: firstPage.NextCursor, Limit: 1), CancellationToken.None));
    }

    [Fact]
    public async Task ACursorMintedForOneFilterSetIsRefusedForAnother()
    {
        var search = new SearchCasesByCursor(new FakeCaseQueryStore());
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);

        var firstPage = await search.ExecuteAsync(
            new(actor, new CaseSearchFilters(Origin: "Email"), Limit: 1), CancellationToken.None);
        Assert.NotNull(firstPage.NextCursor);

        await Assert.ThrowsAsync<CursorRejectedException>(() => search.ExecuteAsync(
            new(actor, new CaseSearchFilters(Origin: "Manual upload"), Cursor: firstPage.NextCursor, Limit: 1),
            CancellationToken.None));
    }

    [Fact]
    public async Task ACursorMintedForOneOrderIsRefusedForAnother()
    {
        var search = new SearchCasesByCursor(new FakeCaseQueryStore());
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var filters = new CaseSearchFilters();

        var firstPage = await search.ExecuteAsync(
            new(actor, filters, CaseSearchOrder.ReceivedDesc, Limit: 1), CancellationToken.None);
        Assert.NotNull(firstPage.NextCursor);

        await Assert.ThrowsAsync<CursorRejectedException>(() => search.ExecuteAsync(
            new(actor, filters, CaseSearchOrder.ReferenceAsc, firstPage.NextCursor, 1), CancellationToken.None));
    }

    [Fact]
    public async Task ACursorMintedForOneCaseIsRefusedForAnother()
    {
        var list = new ListCaseDocumentsByCursor(new FakeCaseQueryStore());
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var caseA = Guid.NewGuid();
        var caseB = Guid.NewGuid();

        var firstPage = await list.ExecuteAsync(new(actor, caseA, Limit: 1), CancellationToken.None);
        Assert.NotNull(firstPage.NextCursor);

        await Assert.ThrowsAsync<CursorRejectedException>(() => list.ExecuteAsync(
            new(actor, caseB, firstPage.NextCursor, 1), CancellationToken.None));
    }

    [Fact]
    public async Task SearchByCursorRequiresAuthorizedStaffBeforeCallingStore() =>
        await Assert.ThrowsAsync<StaffAuthorizationException>(() => new SearchCasesByCursor(new FakeCaseQueryStore())
            .ExecuteAsync(
                new(ActionActor.SystemWorker("cursor-test"), new CaseSearchFilters()),
                CancellationToken.None));

    /// <summary>
    /// Returns exactly <c>fetchCount</c> synthetic rows regardless of what
    /// was asked for, so every call to a <c>*ByCursorAsync</c> method looks
    /// like it has another page — these tests are about the cursor's own
    /// fingerprint/version rules, not the store's keyset SQL (that is
    /// <c>CaseCursorQueryPersistenceTests</c>'s job).
    /// </summary>
    private sealed class FakeCaseQueryStore : ICaseQueryStore
    {
        public Task<SearchCasesResult> SearchAsync(SearchCasesQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseDetails?> GetAsync(GetCaseQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<CaseSearchItem>> SearchByCursorAsync(
            CaseSearchFilters filters,
            CaseSearchOrder order,
            DateTimeOffset? afterReceivedAtUtc,
            string? afterSortText,
            Guid? afterId,
            int fetchCount,
            CancellationToken cancellationToken)
        {
            var now = new DateTimeOffset(2031, 5, 6, 10, 0, 0, TimeSpan.Zero);
            var rows = Enumerable.Range(0, fetchCount)
                .Select(index => new CaseSearchItem(
                    Guid.NewGuid(), $"REF-{index}", null, CaseType.Inspection, "QDOS",
                    CaseLifecycleState.Review, null, null, null, null,
                    now.AddMinutes(-index), null, "Email", now))
                .ToArray();
            return Task.FromResult<IReadOnlyList<CaseSearchItem>>(rows);
        }

        public Task<IReadOnlyList<CaseDocument>> ListDocumentsByCursorAsync(
            Guid caseId,
            DateTimeOffset? afterRecordedAtUtc,
            Guid? afterId,
            int fetchCount,
            CancellationToken cancellationToken)
        {
            var rows = Enumerable.Range(0, fetchCount)
                .Select(_ => new CaseDocument(Guid.NewGuid(), caseId, [], []))
                .ToArray();
            return Task.FromResult<IReadOnlyList<CaseDocument>>(rows);
        }

        public Task<IReadOnlyList<CaseHistoryEntry>> ListHistoryByCursorAsync(
            Guid caseId,
            DateTimeOffset? afterOccurredAtUtc,
            Guid? afterId,
            int fetchCount,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
