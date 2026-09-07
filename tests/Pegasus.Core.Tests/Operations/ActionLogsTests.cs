using Pegasus.Core.Identity;
using Pegasus.Core.Operations;

namespace Pegasus.Core.Tests.Operations;

public sealed class ActionLogsTests
{
    [Fact]
    public async Task BoundedFilterPassesTheRequestedPageToTheQuery()
    {
        var query = new Query();
        var from = new DateTimeOffset(2031, 5, 1, 0, 0, 0, TimeSpan.Zero);
        var filter = new ActionLogFilter(from, from.AddDays(31), "search", "case", "actor", "ok", "action", "case-1", "correlation", Page: 3);

        await new ListActionLogs(query).ExecuteAsync(Administrator(), filter, CancellationToken.None);

        Assert.Equal(filter, query.Filter);
    }

    [Fact]
    public async Task RejectsAnUnboundedPeriodBeforeTheQuery()
    {
        var query = new Query();
        var from = new DateTimeOffset(2031, 5, 1, 0, 0, 0, TimeSpan.Zero);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            new ListActionLogs(query).ExecuteAsync(
                Administrator(),
                new(from, from.AddDays(367), null, null, null, null, null, null, null),
                CancellationToken.None));

        Assert.Null(query.Filter);
    }

    [Fact]
    public async Task RejectsAPageWhoseOffsetCannotBeRepresentedBeforeTheQuery()
    {
        var query = new Query();
        var from = new DateTimeOffset(2031, 5, 1, 0, 0, 0, TimeSpan.Zero);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            new ListActionLogs(query).ExecuteAsync(
                Administrator(),
                new(from, from.AddDays(1), null, null, null, null, null, null, null,
                    Page: int.MaxValue, PageSize: 100),
                CancellationToken.None));

        Assert.Null(query.Filter);
    }

    private static ActionActor Administrator() =>
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);

    private sealed class Query : IActionLogQueries
    {
        public ActionLogFilter? Filter { get; private set; }

        public Task<ActionLogPage> ListAsync(ActionLogFilter filter, CancellationToken cancellationToken)
        {
            Filter = filter;
            return Task.FromResult(new ActionLogPage([], false));
        }
    }
}
