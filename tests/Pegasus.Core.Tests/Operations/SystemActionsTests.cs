using Pegasus.Core.Identity;
using Pegasus.Core.Operations;

namespace Pegasus.Core.Tests.Operations;

/// <summary>
/// System Actions (v42 Design A): the New cases window, newest first, one page
/// of fifty, narrowed by the sub-filter and by Find without changing the chips.
/// </summary>
public sealed class SystemActionsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 10, 0, 0, TimeSpan.Zero);
    private static readonly ActionActor Staff = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);

    [Fact]
    public void TheWindowIsTheNewCasesWindow()
    {
        Assert.Equal(RecentCasesPolicy.WindowStart(Now), SystemActionsPolicy.WindowStart(Now));
    }

    [Theory]
    [InlineData(SystemActionFilter.AutoMatched, SystemActionKind.AutoMatchedMail, true)]
    [InlineData(SystemActionFilter.AutoMatched, SystemActionKind.CancellationMatched, false)]
    [InlineData(SystemActionFilter.ImagesPaired, SystemActionKind.VehicleImagesPaired, true)]
    [InlineData(SystemActionFilter.ImagesPaired, SystemActionKind.AutoMatchedMail, false)]
    [InlineData(SystemActionFilter.RulesAndQueries, SystemActionKind.QueryOpenedAutomatically, true)]
    [InlineData(SystemActionFilter.RulesAndQueries, SystemActionKind.CancellationMatched, true)]
    [InlineData(SystemActionFilter.RulesAndQueries, SystemActionKind.VehicleImagesPaired, false)]
    [InlineData(SystemActionFilter.All, SystemActionKind.CancellationMatched, true)]
    public void TheSubFilterNamesTheKindOrTheRuleKinds(SystemActionFilter filter, SystemActionKind kind, bool expected)
    {
        Assert.Equal(expected, SystemActionsPolicy.Matches(filter, kind));
    }

    [Theory]
    [InlineData("QDOS26190", true)]
    [InlineData("AB12 CDE", true)]
    [InlineData("ab12-cde", true)]
    [InlineData("Focus", true)]
    [InlineData("conversation thread", true)]
    [InlineData("storage", true)]
    [InlineData("Nope", false)]
    public void FindMatchesTheReferenceTheVehicleTheSummaryOrTheSignal(string search, bool expected)
    {
        var row = Row(SystemActionKind.AutoMatchedMail, Now) with
        {
            Reference = "QDOS26190",
            Registration = "AB12 CDE",
            VehicleMake = "Ford",
            VehicleModel = "Focus",
            Summary = "Storage charges dispute",
            Signal = "Automatic association from conversation thread."
        };

        Assert.Equal(expected, SystemActionsPolicy.MatchesSearch(row, search));
    }

    [Fact]
    public async Task TheReadIsTheWindowAndThePageThePortReturns()
    {
        var page = new SystemActionPage([], 1, SystemActionsPolicy.PageSize, 0, Counts());
        var queries = new StubQueries(page);

        var result = await new ListSystemActions(queries, new FixedTime(Now))
            .ExecuteAsync(Staff, SystemActionFilter.RulesAndQueries, "AB12", 2, default);

        Assert.Same(page, result);
        Assert.Equal(RecentCasesPolicy.WindowStart(Now), queries.Since);
        Assert.Equal(SystemActionFilter.RulesAndQueries, queries.Filter);
        Assert.Equal("AB12", queries.Search);
        Assert.Equal(2, queries.Page);
        Assert.Equal(SystemActionsPolicy.PageSize, queries.PageSize);
    }

    [Fact]
    public async Task OnlyAStaffMemberReadsTheList()
    {
        var queries = new StubQueries(new SystemActionPage([], 1, SystemActionsPolicy.PageSize, 0, Counts()));

        await Assert.ThrowsAsync<StaffAuthorizationException>(() => new ListSystemActions(queries, new FixedTime(Now))
            .ExecuteAsync(ActionActor.Automation("grant-1"), SystemActionFilter.All, null, 1, default));
        Assert.Equal(0, queries.Reads);
    }

    [Fact]
    public void AQueryRowCarriesItsQueryAndWhetherItIsUnassigned()
    {
        var queryId = Guid.NewGuid();
        // A query row is keyed by the query itself: dismissing it hides only that query.
        var row = Row(SystemActionKind.QueryOpenedAutomatically, Now) with
        {
            RecordId = queryId,
            QueryId = queryId,
            QueryUnassigned = true
        };

        Assert.Equal(queryId, row.RecordId);
        Assert.Equal(queryId, row.QueryId);
        Assert.True(row.QueryUnassigned);
    }

    private static SystemActionRow Row(SystemActionKind kind, DateTimeOffset occurredAtUtc)
    {
        var recordId = Guid.NewGuid();
        return new SystemActionRow(
            recordId,
            kind,
            Guid.NewGuid(),
            "QDOS26001",
            null,
            null,
            null,
            "Summary",
            "Signal",
            occurredAtUtc,
            kind == SystemActionKind.QueryOpenedAutomatically ? recordId : null,
            false);
    }

    private static Dictionary<SystemActionKind, int> Counts() =>
        Enum.GetValues<SystemActionKind>().ToDictionary(kind => kind, _ => 0);

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class StubQueries(SystemActionPage answer) : ISystemActionQueries
    {
        public DateTimeOffset? Since { get; private set; }

        public SystemActionFilter Filter { get; private set; }

        public string? Search { get; private set; }

        public int Page { get; private set; }

        public int PageSize { get; private set; }

        public int Reads { get; private set; }

        public Task<SystemActionPage> ListAsync(
            DateTimeOffset sinceUtc,
            SystemActionFilter filter,
            string? search,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
        {
            Since = sinceUtc;
            Filter = filter;
            Search = search;
            Page = page;
            PageSize = pageSize;
            Reads++;
            return Task.FromResult(answer);
        }
    }
}
