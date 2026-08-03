using Pegasus.Core.Identity;

namespace Pegasus.Core.Tests.Identity;

public sealed class AutomationActivityTests
{
    private static ActionActor Administrator() =>
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);

    [Fact]
    public async Task ListingRequiresTheAutomationClientAdministrationRight()
    {
        var queries = new RecordingQueries();
        var list = new ListAutomationActivity(queries);

        await Assert.ThrowsAsync<StaffAuthorizationException>(() => list.ExecuteAsync(
            new(ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer])),
            default));
        await Assert.ThrowsAsync<StaffAuthorizationException>(() => list.ExecuteAsync(
            new(ActionActor.Automation("pegasus-automation")),
            default));
        await Assert.ThrowsAsync<StaffAuthorizationException>(() => list.ExecuteAsync(
            new(ActionActor.SystemWorker("activity-test")),
            default));

        Assert.Null(queries.Request);
    }

    [Fact]
    public async Task ListingNormalizesTheCorrelationFilterAndForwardsThePage()
    {
        var queries = new RecordingQueries();
        var list = new ListAutomationActivity(queries);
        var actor = Administrator();

        await list.ExecuteAsync(new(actor, "  mcp:document-add:1234  ", 3, 25), default);

        var request = Assert.IsType<ListAutomationActivityRequest>(queries.Request);
        Assert.Same(actor, request.Actor);
        Assert.Equal("mcp:document-add:1234", request.CorrelationId);
        Assert.Equal(3, request.Page);
        Assert.Equal(25, request.PageSize);
    }

    [Fact]
    public async Task BlankCorrelationFiltersAreTreatedAsNoFilter()
    {
        var queries = new RecordingQueries();
        var list = new ListAutomationActivity(queries);

        await list.ExecuteAsync(new(Administrator(), "   "), default);

        Assert.Null(Assert.IsType<ListAutomationActivityRequest>(queries.Request).CorrelationId);
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(10_001, 50)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task PagesOutsideTheSupportedRangeNeverReachTheStore(int page, int pageSize)
    {
        var queries = new RecordingQueries();
        var list = new ListAutomationActivity(queries);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => list.ExecuteAsync(
            new(Administrator(), null, page, pageSize),
            default));

        Assert.Null(queries.Request);
    }

    [Fact]
    public async Task OverlongCorrelationFiltersNeverReachTheStore()
    {
        var queries = new RecordingQueries();
        var list = new ListAutomationActivity(queries);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => list.ExecuteAsync(
            new(Administrator(), new string('c', 101)),
            default));

        Assert.Null(queries.Request);
    }

    private sealed class RecordingQueries : IAutomationActivityQueries
    {
        public ListAutomationActivityRequest? Request { get; private set; }

        public Task<ListAutomationActivityResult> ListAsync(
            ListAutomationActivityRequest request,
            CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new ListAutomationActivityResult(
                [],
                request.CorrelationId,
                request.Page,
                request.PageSize,
                request.Page > 1,
                false));
        }
    }
}
