using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;

namespace Pegasus.Core.Tests.Operations;

public sealed class AdministrationHealthMetricsTests
{
    [Fact]
    public async Task MetricsRequireAdministratorAutomationAccess()
    {
        var query = new Query();
        var command = new GetAdministrationHealthMetrics(query);

        await Assert.ThrowsAsync<StaffAuthorizationException>(() => command.ExecuteAsync(
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]),
            DateTimeOffset.UtcNow,
            CancellationToken.None));

        Assert.False(query.Called);
    }

    private sealed class Query : IAdministrationHealthMetricsQueries
    {
        public bool Called { get; private set; }
        public Task<AdministrationHealthMetrics> GetAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken)
        {
            Called = true;
            return Task.FromResult(new AdministrationHealthMetrics(
                0, 0, 0, null, null, 0, null, 0, 0, null, 0, 0,
                new(MailFreshnessState.Unavailable, null)));
        }
    }
}
