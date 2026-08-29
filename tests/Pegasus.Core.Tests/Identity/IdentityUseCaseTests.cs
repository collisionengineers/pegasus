using Pegasus.Core.Identity;

namespace Pegasus.Core.Tests.Identity;

public sealed class IdentityUseCaseTests
{
    private static readonly ActionActor Administrator =
        ActionActor.Staff(Guid.Parse("ed03353a-bcb8-48d9-aa1b-37061b114af1"), [StaffRole.Administrator]);

    [Fact]
    public async Task NamedStaffQueriesAreBoundedAndRequireCurrentAdministrator()
    {
        var account = Account(Guid.NewGuid());
        var queries = new RecordingQueries(new([account], HasMoreAccounts: true));

        var list = await new ListStaffAccounts(queries).ExecuteAsync(
            new(Administrator, PageNumber: 2, PageSize: 25),
            default);
        var access = await new GetAccessReview(queries).ExecuteAsync(
            new(Administrator, MaximumResults: 10),
            default);
        var roles = await new GetRoleAssignments(queries).ExecuteAsync(
            new(Administrator, MaximumResults: 12),
            default);
        var detail = await new GetStaffAccount(queries).ExecuteAsync(
            new(Administrator, account.Id),
            default);

        Assert.Equal((25, 25), queries.ListCalls[0]);
        Assert.Equal((0, 10), queries.ListCalls[1]);
        Assert.Equal((0, 12), queries.ListCalls[2]);
        Assert.True(list.HasPreviousPage);
        Assert.True(list.HasMoreAccounts);
        Assert.True(Assert.Single(access.Accounts).ReviewIsOutstanding);
        Assert.Equal(account.Roles, Assert.Single(roles.Accounts).CurrentRoles);
        Assert.Equal(account, detail?.Account);

        var engineer = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            new ListStaffAccounts(queries).ExecuteAsync(new(engineer), default));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            new GetAccessReview(queries).ExecuteAsync(new(Administrator, 101), default));
    }

    [Fact]
    public async Task StaffCommandsNormalizeThroughTheirNarrowStores()
    {
        var store = new RecordingStaffStore();
        var staffId = Guid.NewGuid();

        await new CreateStaffAccount(store).ExecuteAsync(
            new(Administrator, "  new.user  ", "password", "  Approved starter access  ", "  create-1  "),
            default);
        await new DisableStaffAccount(store).ExecuteAsync(
            new(Administrator, staffId, "  Access removed  ", "  disable-1  "),
            default);
        await new AssignStaffRoles(store).ExecuteAsync(
            new(
                Administrator,
                staffId,
                [StaffRole.User, StaffRole.Administrator, StaffRole.User],
                "  Approved duties  ",
                "  roles-1  "),
            default);
        await new ReviewStaffAccess(store).ExecuteAsync(
            new(Administrator, staffId, "  Quarterly review  ", "  review-1  "),
            default);

        Assert.Equal("new.user", store.CreateRequest?.UserName);
        Assert.Equal("create-1", store.CreateRequest?.OperationKey);
        Assert.Equal("Approved starter access", store.CreateRequest?.Reason);
        Assert.Equal("Access removed", store.DisableRequest?.Reason);
        Assert.Equal(
            [StaffRole.Administrator, StaffRole.User],
            store.AssignRequest?.Roles);
        Assert.Equal("Approved duties", store.AssignRequest?.Reason);
        Assert.Equal("Quarterly review", store.ReviewRequest?.Reason);
    }

    [Fact]
    public async Task StaffAccountCannotDisableOrReviewItself()
    {
        var staffId = Guid.Parse("b0a0e70a-1b8c-4ee4-bc21-1de58b73cf0c");
        var actor = ActionActor.Staff(staffId, [StaffRole.Administrator]);
        var store = new RecordingStaffStore();

        var disable = await Assert.ThrowsAsync<StaffAccountAdministrationException>(() =>
            new DisableStaffAccount(store).ExecuteAsync(
                new(actor, staffId, "Disable self", "disable-self"),
                default));
        var review = await Assert.ThrowsAsync<StaffAccountAdministrationException>(() =>
            new ReviewStaffAccess(store).ExecuteAsync(
                new(actor, staffId, "Review self", "review-self"),
                default));

        Assert.Equal(StaffAccountAdministrationError.SelfAction, disable.Error);
        Assert.Equal(StaffAccountAdministrationError.SelfAction, review.Error);
        Assert.Null(store.DisableRequest);
        Assert.Null(store.ReviewRequest);
    }

    private static StaffAccountSummary Account(Guid id) =>
        new(id, "staff", true, false, [StaffRole.User], null);

    private sealed class RecordingQueries(StaffAccountQuerySlice slice) : IStaffAccountQueries
    {
        public List<(int Offset, int Limit)> ListCalls { get; } = [];

        public Task<StaffAccountQuerySlice> ListAsync(
            int offset,
            int limit,
            CancellationToken cancellationToken)
        {
            ListCalls.Add((offset, limit));
            return Task.FromResult(slice);
        }

        public Task<StaffAccountSummary?> GetAsync(
            Guid staffId,
            CancellationToken cancellationToken) =>
            Task.FromResult(slice.Accounts.SingleOrDefault(account => account.Id == staffId));
    }

    private sealed class RecordingStaffStore :
        ICreateStaffAccountStore,
        IDisableStaffAccountStore,
        IAssignStaffRolesStore,
        IReviewStaffAccessStore
    {
        public CreateStaffAccountRequest? CreateRequest { get; private set; }
        public DisableStaffAccountRequest? DisableRequest { get; private set; }
        public AssignStaffRolesRequest? AssignRequest { get; private set; }
        public ReviewStaffAccessRequest? ReviewRequest { get; private set; }

        public Task<CreateStaffAccountResult> CreateAsync(
            CreateStaffAccountRequest request,
            CancellationToken cancellationToken)
        {
            CreateRequest = request;
            return Task.FromResult(new CreateStaffAccountResult(Account(Guid.NewGuid()), false));
        }

        public Task<DisableStaffAccountResult> DisableAsync(
            DisableStaffAccountRequest request,
            CancellationToken cancellationToken)
        {
            DisableRequest = request;
            return Task.FromResult(new DisableStaffAccountResult(Account(request.StaffId), 1, 2, false));
        }

        public Task<AssignStaffRolesResult> AssignAsync(
            AssignStaffRolesRequest request,
            CancellationToken cancellationToken)
        {
            AssignRequest = request;
            return Task.FromResult(new AssignStaffRolesResult(Account(request.StaffId), 1, 2, false));
        }

        public Task<ReviewStaffAccessResult> ReviewAsync(
            ReviewStaffAccessRequest request,
            CancellationToken cancellationToken)
        {
            ReviewRequest = request;
            return Task.FromResult(new ReviewStaffAccessResult(
                request.StaffId,
                DateTimeOffset.UnixEpoch,
                false));
        }
    }

}
