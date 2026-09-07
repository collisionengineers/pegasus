using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Cases;

/// <summary>
/// CASE-047: the Case cursor use cases (<see cref="SearchCasesByCursor"/>,
/// <see cref="ListCaseDocumentsByCursor"/>) bound to the shared G9 <see
/// cref="ICursorProtector"/> primitive instead of an internal codec — a
/// cursor minted for one actor, filter set, order, or case is refused
/// everywhere else, because the scope the use case passes to the protector
/// binds all four; any malformed cursor the protector itself refuses
/// surfaces as <see cref="CursorRejectedException"/>, never a raw parse
/// exception.
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
    public async Task ANextCursorIsMintedThroughTheSharedProtector()
    {
        var protector = new FakeCursorProtector();
        var search = new SearchCasesByCursor(new FakeCaseQueryStore(), protector);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);

        var page = await search.ExecuteAsync(new(actor, new CaseSearchFilters(), Limit: 1), CancellationToken.None);

        Assert.NotNull(page.NextCursor);
        var minted = Assert.Single(protector.Protected);
        Assert.Equal(page.NextCursor, minted.Cursor);
        Assert.NotEqual(Guid.Empty, minted.Id);
        Assert.False(string.IsNullOrEmpty(minted.SortKey));
    }

    [Fact]
    public async Task ACursorMintedForOneActorIsRefusedForAnother()
    {
        var search = new SearchCasesByCursor(new FakeCaseQueryStore(), new FakeCursorProtector());
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
        var search = new SearchCasesByCursor(new FakeCaseQueryStore(), new FakeCursorProtector());
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
        var search = new SearchCasesByCursor(new FakeCaseQueryStore(), new FakeCursorProtector());
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
        var list = new ListCaseDocumentsByCursor(new FakeCaseQueryStore(), new FakeCursorProtector());
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var caseA = Guid.NewGuid();
        var caseB = Guid.NewGuid();

        var firstPage = await list.ExecuteAsync(new(actor, caseA, Limit: 1), CancellationToken.None);
        Assert.NotNull(firstPage.NextCursor);

        await Assert.ThrowsAsync<CursorRejectedException>(() => list.ExecuteAsync(
            new(actor, caseB, firstPage.NextCursor, 1), CancellationToken.None));
    }

    /// <summary>
    /// CASE-047, Stream A MCP review: the document list's page unit is the
    /// occurrence, so the cursor position is minted against the last row's
    /// own occurrence identity — never against a document aggregate — and a
    /// host flattening items one-for-one cannot lose occurrences of a
    /// many-occurrence document (the store's paging of that rule is
    /// <c>CaseCursorQueryPersistenceTests</c>'s job).
    /// </summary>
    [Fact]
    public async Task ADocumentCursorIsMintedAgainstTheOccurrenceIdentity()
    {
        var protector = new FakeCursorProtector();
        var list = new ListCaseDocumentsByCursor(new FakeCaseQueryStore(), protector);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);

        var page = await list.ExecuteAsync(new(actor, Guid.NewGuid(), Limit: 1), CancellationToken.None);

        var minted = Assert.Single(protector.Protected);
        var expected = Assert.Single(page.Items);
        Assert.Equal(expected.Occurrence.Id, minted.Id);
        Assert.Equal(
            CursorPaging.EncodeUtcTimestamp(expected.Occurrence.RecordedAtUtc),
            minted.SortKey);
    }

    [Fact]
    public async Task AMalformedCursorIsRefused()
    {
        var search = new SearchCasesByCursor(new FakeCaseQueryStore(), new FakeCursorProtector());
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);

        await Assert.ThrowsAsync<CursorRejectedException>(() => search.ExecuteAsync(
            new(actor, new CaseSearchFilters(), Cursor: "not-a-cursor", Limit: 1), CancellationToken.None));
    }

    [Fact]
    public async Task SearchByCursorRequiresAuthorizedStaffBeforeCallingStore() =>
        await Assert.ThrowsAsync<StaffAuthorizationException>(() => new SearchCasesByCursor(
                new FakeCaseQueryStore(), new FakeCursorProtector())
            .ExecuteAsync(
                new(ActionActor.SystemWorker("cursor-test"), new CaseSearchFilters()),
                CancellationToken.None));

    /// <summary>
    /// Returns exactly <c>fetchCount</c> synthetic rows regardless of what
    /// was asked for, so every call to a <c>*ByCursorAsync</c> method looks
    /// like it has another page — these tests are about the use case's own
    /// scope/protector rules, not the store's keyset SQL (that is
    /// <c>CaseCursorQueryPersistenceTests</c>'s job).
    /// </summary>
    private sealed class FakeCaseQueryStore : ICaseQueryStore
    {
        public Task<SearchCasesResult> SearchAsync(SearchCasesQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseDetails?> GetAsync(GetCaseQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseHeader?> GetHeaderAsync(GetCaseHeaderQuery query, CancellationToken cancellationToken) =>
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

        public Task<IReadOnlyList<CaseDocumentPageItem>> ListDocumentsByCursorAsync(
            Guid caseId,
            DateTimeOffset? afterRecordedAtUtc,
            Guid? afterId,
            int fetchCount,
            CancellationToken cancellationToken)
        {
            var now = new DateTimeOffset(2031, 5, 6, 10, 0, 0, TimeSpan.Zero);
            var rows = Enumerable.Range(0, fetchCount)
                .Select(index =>
                {
                    var occurrenceId = Guid.NewGuid();
                    var versionId = Guid.NewGuid();
                    return new CaseDocumentPageItem(
                        new DocumentOccurrence(
                            occurrenceId, caseId, Guid.NewGuid(), versionId,
                            DocumentSemanticRole.Image, DocumentSource.StaffUpload,
                            $"cursor-page:{occurrenceId:N}", now.AddMinutes(-index), null, null),
                        new DocumentVersion(
                            versionId, Guid.NewGuid(), 1, $"doc-{index}.pdf", "application/pdf",
                            1, new string('a', 64), DocumentCustodyStatus.Confirmed,
                            now, "Staff:test", IsCurrent: true, IsLogicallyRemoved: false,
                            RemovalReason: null));
                })
                .ToArray();
            return Task.FromResult<IReadOnlyList<CaseDocumentPageItem>>(rows);
        }

        public Task<IReadOnlyList<CaseHistoryEntry>> ListHistoryByCursorAsync(
            Guid caseId,
            DateTimeOffset? afterOccurredAtUtc,
            Guid? afterId,
            int fetchCount,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    /// <summary>
    /// A minimal <see cref="ICursorProtector"/> fake that records every
    /// minted scope/sortKey/id and encodes the scope into the cursor itself,
    /// so <see cref="Unprotect"/> can enforce the one rule these tests care
    /// about — a cursor decodes only against the exact scope it was minted
    /// for — the same rule the real <c>DataProtectionCursorProtector</c>
    /// enforces through Data Protection's own purpose string
    /// (<c>DataProtectionCursorTests</c> proves that one).
    /// </summary>
    private sealed class FakeCursorProtector : ICursorProtector
    {
        private const char Separator = '|';

        public List<(string Scope, string SortKey, Guid Id, string Cursor)> Protected { get; } = [];

        public string Protect(string scope, string sortKey, Guid id)
        {
            var cursor = string.Join(Separator, scope, sortKey, id);
            Protected.Add((scope, sortKey, id, cursor));
            return cursor;
        }

        public (string SortKey, Guid Id) Unprotect(string cursor, string scope)
        {
            var parts = cursor.Split(Separator);
            if (parts.Length != 3 || !string.Equals(parts[0], scope, StringComparison.Ordinal)
                || !Guid.TryParse(parts[2], out var id))
            {
                throw new CursorRejectedException();
            }
            return (parts[1], id);
        }
    }
}
