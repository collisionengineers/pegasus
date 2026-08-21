using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

public sealed class ApprovedOutlookCategoryTests
{
    private static readonly ActionActor Administrator = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
    private static readonly ActionActor User = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);

    [Fact]
    public async Task ManagementNormalizesAndRequiresAdministrator()
    {
        var store = new FakeStore();
        var command = new UpdateApprovedOutlookCategory(store);
        var request = new UpdateApprovedOutlookCategoryRequest(
            Guid.NewGuid(), "  Awaiting engineer  ", ApprovedOutlookCategoryState.Active, 0,
            Administrator, " Add approved name ", " operation-1 ");

        var result = await command.ExecuteAsync(request, default);

        Assert.Equal("Awaiting engineer", result.DisplayName);
        Assert.Equal("Add approved name", store.LastRequest!.Reason);
        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            command.ExecuteAsync(request with { Actor = User }, default));
    }

    [Fact]
    public async Task CatalogueListRequiresAdministrator()
    {
        var query = new ListApprovedOutlookCategories(new FakeStore());

        Assert.Empty(await query.ExecuteAsync(Administrator, default));
        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            query.ExecuteAsync(User, default));
    }

    [Theory]
    [InlineData("", "Reason", "operation-1")]
    [InlineData("Awaiting engineer", "", "operation-1")]
    [InlineData("Awaiting engineer", "Reason", "")]
    [InlineData("Awaiting\nengineer", "Reason", "operation-1")]
    public async Task ManagementRejectsInvalidText(string displayName, string reason, string operationKey)
    {
        var command = new UpdateApprovedOutlookCategory(new FakeStore());
        var request = new UpdateApprovedOutlookCategoryRequest(
            Guid.NewGuid(), displayName, ApprovedOutlookCategoryState.Active, 0,
            Administrator, reason, operationKey);

        await Assert.ThrowsAsync<ArgumentException>(() => command.ExecuteAsync(request, default));
    }

    [Fact]
    public async Task ManagementRejectsInvalidIdentityVersionAndState()
    {
        var command = new UpdateApprovedOutlookCategory(new FakeStore());
        var request = new UpdateApprovedOutlookCategoryRequest(
            Guid.NewGuid(), "Awaiting engineer", ApprovedOutlookCategoryState.Active, 0,
            Administrator, "Reason", "operation-1");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            command.ExecuteAsync(request with { CategoryId = Guid.Empty }, default));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            command.ExecuteAsync(request with { ExpectedVersion = -1 }, default));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            command.ExecuteAsync(request with { State = (ApprovedOutlookCategoryState)99 }, default));
    }

    [Fact]
    public async Task MailActionReloadsOnlyActiveInternalId()
    {
        var id = Guid.NewGuid();
        var store = new FakeStore { Configured = new(id, "Awaiting engineer", ApprovedOutlookCategoryState.Active, 1) };
        var resolver = new ResolveApprovedOutlookCategory(store);

        Assert.Equal("Awaiting engineer", (await resolver.ExecuteAsync(id, User, default)).DisplayName);
        store.Configured = store.Configured with { State = ApprovedOutlookCategoryState.Disabled };
        await Assert.ThrowsAsync<ApprovedOutlookCategoryUnavailableException>(() =>
            resolver.ExecuteAsync(id, User, default));
        await Assert.ThrowsAsync<ApprovedOutlookCategoryUnavailableException>(() =>
            resolver.ExecuteAsync(Guid.Empty, User, default));
        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            resolver.ExecuteAsync(id, ActionActor.SystemWorker("mail-category-test"), default));
    }

    private sealed class FakeStore : IApprovedOutlookCategoryStore, IApprovedOutlookCategoryResolver
    {
        public UpdateApprovedOutlookCategoryRequest? LastRequest { get; private set; }
        public ApprovedOutlookCategory? Configured { get; set; }
        public Task<IReadOnlyList<ApprovedOutlookCategory>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ApprovedOutlookCategory>>(Configured is null ? [] : [Configured]);
        public Task<ApprovedOutlookCategory?> ResolveActiveAsync(Guid categoryId, CancellationToken cancellationToken) =>
            Task.FromResult(Configured is { State: ApprovedOutlookCategoryState.Active }
                && Configured.Id == categoryId ? Configured : null);
        public Task<ApprovedOutlookCategory> UpdateAsync(UpdateApprovedOutlookCategoryRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new ApprovedOutlookCategory(request.CategoryId, request.DisplayName, request.State, request.ExpectedVersion + 1));
        }
    }
}
