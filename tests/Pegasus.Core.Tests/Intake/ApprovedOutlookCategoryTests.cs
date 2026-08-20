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
    public async Task MailActionReloadsOnlyActiveInternalId()
    {
        var id = Guid.NewGuid();
        var store = new FakeStore { Active = new(id, "Awaiting engineer", ApprovedOutlookCategoryState.Active, 1) };
        var resolver = new ResolveApprovedOutlookCategory(store);

        Assert.Equal("Awaiting engineer", (await resolver.ExecuteAsync(id, User, default)).DisplayName);
        store.Active = null;
        await Assert.ThrowsAsync<ApprovedOutlookCategoryUnavailableException>(() =>
            resolver.ExecuteAsync(id, User, default));
    }

    private sealed class FakeStore : IApprovedOutlookCategoryStore, IApprovedOutlookCategoryResolver
    {
        public UpdateApprovedOutlookCategoryRequest? LastRequest { get; private set; }
        public ApprovedOutlookCategory? Active { get; set; }
        public Task<IReadOnlyList<ApprovedOutlookCategory>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ApprovedOutlookCategory>>(Active is null ? [] : [Active]);
        public Task<ApprovedOutlookCategory?> ResolveActiveAsync(Guid categoryId, CancellationToken cancellationToken) =>
            Task.FromResult(Active?.Id == categoryId ? Active : null);
        public Task<ApprovedOutlookCategory> UpdateAsync(UpdateApprovedOutlookCategoryRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new ApprovedOutlookCategory(request.CategoryId, request.DisplayName, request.State, request.ExpectedVersion + 1));
        }
    }
}
