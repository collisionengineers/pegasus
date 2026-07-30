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
    public async Task PublicClientPolicyUsesOnlyExactPlanScopesAndSecretlessMetadata()
    {
        var store = new RecordingMcpStore();
        var command = new RegisterPublicMcpClient(store);
        var metadata = Client(
            [StaffMcpClientContract.ReadScope, StaffMcpClientContract.WriteScope]);

        var result = await command.ExecuteAsync(
            new(Administrator, metadata, "  Approved client  ", "  client-1  "),
            default);

        Assert.True(result.IsPublic);
        Assert.True(result.RequiresPkceS256);
        Assert.Equal("Approved client", store.RegisterRequest?.Reason);
        Assert.Equal("client-1", store.RegisterRequest?.OperationKey);
        Assert.Equal(
            ["pegasus.read", "pegasus.write"],
            store.RegisterRequest?.Client.Scopes);

        await Assert.ThrowsAsync<ArgumentException>(() => command.ExecuteAsync(
            new(
                Administrator,
                Client(["pegasus.mcp.read"]),
                "Legacy scope",
                "client-legacy"),
            default));
    }

    [Fact]
    public async Task StaffMcpRevocationAllowsSelfButRequiresAdministratorForAnotherAccount()
    {
        var store = new RecordingMcpStore();
        var staffId = Guid.NewGuid();
        var self = ActionActor.Staff(staffId, [StaffRole.User]);
        var command = new RevokeStaffMcpAuthorizations(store);

        await command.ExecuteAsync(
            new(self, staffId, "  Password changed  ", "  revoke-self  "),
            default);

        Assert.Equal("Password changed", store.StaffRevokeRequest?.Reason);
        var engineer = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        await Assert.ThrowsAsync<StaffAuthorizationException>(() => command.ExecuteAsync(
            new(engineer, staffId, "Review", "revoke-other"),
            default));
    }

    [Fact]
    public async Task InitializeApplicationPinsManifestAndConstructsTheOnlyBootstrapActor()
    {
        var store = new RecordingInitializationStore();
        var command = new InitializeApplication(store);
        var sha = new string('a', 64);
        var request = new InitializeApplicationRequest(
            "20260729191000_OperationsProjectionIndexes",
            sha.ToUpperInvariant(),
            sha,
            "sqlserver://example.database.windows.net/pegasus",
            [
                new("approved-1", "Andrew", "password-one"),
                new("approved-2", "Alex", "password-two")
            ],
            Client([StaffMcpClientContract.ReadScope, StaffMcpClientContract.WriteScope]),
            "bootstrap-correlation");

        _ = await command.ExecuteAsync(request, default);

        Assert.Equal(ActorKind.Bootstrap, store.Request?.Actor.Kind);
        Assert.Equal(sha, store.Request?.Actor.SubjectId);
        Assert.Equal(sha, store.Request?.ManifestSha256);
        Assert.Empty(store.Request!.Actor.Roles);
        Assert.False(StaffAuthorization.IsAuthorized(
            store.Request.Actor,
            StaffAccessRight.ManageStaffAccounts));

        var altered = request with { ApprovedManifestSha256 = new string('b', 64) };
        await Assert.ThrowsAsync<ApplicationInitializationException>(() =>
            command.ExecuteAsync(altered, default));
        Assert.Equal(1, store.CallCount);
    }

    private static StaffAccountSummary Account(Guid id) =>
        new(id, "staff", true, false, [StaffRole.User], null);

    private static PublicMcpClientMetadata Client(IReadOnlyList<string> scopes) =>
        new(
            "approved-public-client",
            "Approved public client",
            [new Uri("http://127.0.0.1:7890/callback")],
            new Uri("https://pegasus.example/mcp"),
            scopes);

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

    private sealed class RecordingMcpStore : IPublicMcpClientStore, IStaffMcpAuthorizationStore
    {
        public RegisterPublicMcpClientRequest? RegisterRequest { get; private set; }
        public RevokeStaffMcpAuthorizationsRequest? StaffRevokeRequest { get; private set; }

        public Task<RegisterPublicMcpClientResult> RegisterAsync(
            RegisterPublicMcpClientRequest request,
            CancellationToken cancellationToken)
        {
            RegisterRequest = request;
            return Task.FromResult(new RegisterPublicMcpClientResult(
                request.Client,
                true,
                true,
                false));
        }

        public Task<RevokePublicMcpClientResult> RevokeAsync(
            RevokePublicMcpClientRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new RevokePublicMcpClientResult(request.ClientId, 0, 0, false));

        Task<RevokeStaffMcpAuthorizationsResult> IStaffMcpAuthorizationStore.RevokeAsync(
            RevokeStaffMcpAuthorizationsRequest request,
            CancellationToken cancellationToken)
        {
            StaffRevokeRequest = request;
            return Task.FromResult(new RevokeStaffMcpAuthorizationsResult(
                request.StaffId,
                1,
                2,
                false));
        }
    }

    private sealed class RecordingInitializationStore : IApplicationInitializationStore
    {
        public int CallCount { get; private set; }
        public InitializeApplicationStoreRequest? Request { get; private set; }

        public Task<InitializeApplicationResult> InitializeAsync(
            InitializeApplicationStoreRequest request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            Request = request;
            return Task.FromResult(new InitializeApplicationResult(
                request.ManifestSha256,
                request.ExpectedMigrationId,
                request.TargetIdentity,
                DateTimeOffset.UnixEpoch,
                [],
                new RegisterPublicMcpClientResult(request.PublicMcpClient, true, true, false)));
        }
    }
}
