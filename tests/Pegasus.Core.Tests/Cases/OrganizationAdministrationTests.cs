using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Tests.Cases;

public sealed class OrganizationAdministrationTests
{
    private static readonly ActionActor Administrator =
        ActionActor.Staff(Guid.Parse("63f98d69-5368-48b8-b25d-a61ec91f6905"), [StaffRole.Administrator]);

    [Fact]
    public async Task CreateOrganizationNormalizesInputBeforeCallingPersistencePort()
    {
        var store = new RecordingStore();
        var command = new CreateOrganization(store);

        await command.ExecuteAsync(
            new(
                "  QDOS Services  ",
                [
                    OrganizationRole.InstructionIntermediary,
                    OrganizationRole.WorkProvider,
                    OrganizationRole.WorkProvider
                ],
                Administrator,
                "  create-qdos  "),
            default);

        var request = Assert.Single(store.OrganizationCreates);
        Assert.Equal("QDOS Services", request.Name);
        Assert.Equal(
            [OrganizationRole.WorkProvider, OrganizationRole.InstructionIntermediary],
            request.Roles);
        Assert.Equal("create-qdos", request.OperationKey);
    }

    [Fact]
    public async Task PrincipalCommandsNormalizeCodesAndRequiredChangeReasons()
    {
        var store = new RecordingStore();
        var create = new CreatePrincipal(store);
        var replace = new ReplacePrincipal(store);
        var organizationId = Guid.NewGuid();
        var principalId = Guid.NewGuid();

        await create.ExecuteAsync(
            new(organizationId, " qdos2 ", Administrator, " create-principal "),
            default);
        await replace.ExecuteAsync(
            new(
                principalId,
                4,
                organizationId,
                " qdos3 ",
                Administrator,
                " replace-principal ",
                " successor required "),
            default);

        Assert.Equal("QDOS2", Assert.Single(store.PrincipalCreates).Code);
        var replacement = Assert.Single(store.PrincipalReplacements);
        Assert.Equal("QDOS3", replacement.SuccessorCode);
        Assert.Equal("successor required", replacement.Reason);
    }

    [Fact]
    public async Task EmptyRolesFailBeforePersistence()
    {
        var store = new RecordingStore();
        var create = new CreateOrganization(store);

        var exception = await Assert.ThrowsAsync<OrganizationAdministrationException>(
            () => create.ExecuteAsync(
                new("No roles", [], Administrator, "empty-roles"),
                default));

        Assert.Equal(OrganizationAdministrationError.EmptyOrganizationRoles, exception.Error);
        Assert.Empty(store.OrganizationCreates);
    }

    [Fact]
    public async Task NonAdministratorCannotReachMutationOrQueryPorts()
    {
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var store = new RecordingStore();
        var queries = new RecordingQueries();

        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            new CreatePrincipal(store).ExecuteAsync(
                new(Guid.NewGuid(), "DENIED", actor, "denied-create"),
                default));
        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            new ListOrganizations(queries).ExecuteAsync(new(actor), default));

        Assert.Empty(store.PrincipalCreates);
        Assert.Equal(0, queries.ListCalls);
    }

    [Fact]
    public async Task ListOrganizationsAuthorizesAndBoundsTheProjectionPort()
    {
        var queries = new RecordingQueries();
        var query = new ListOrganizations(queries);

        var page = await query.ExecuteAsync(new(Administrator, 3, 10), default);

        Assert.Equal(20, queries.Offset);
        Assert.Equal(10, queries.Limit);
        Assert.True(page.HasPreviousPage);
        Assert.True(page.HasMoreOrganizations);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            query.ExecuteAsync(
                new(Administrator, 1, ListOrganizations.MaximumPageSize + 1),
                default));
    }

    [Fact]
    public async Task InvalidReplacementVersionAndReasonFailBeforePersistence()
    {
        var store = new RecordingStore();
        var command = new ReplacePrincipal(store);
        var request = new ReplacePrincipalRequest(
            Guid.NewGuid(),
            -1,
            Guid.NewGuid(),
            "NEXT",
            Administrator,
            "replace",
            "reason");

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => command.ExecuteAsync(request, default));
        await Assert.ThrowsAsync<ArgumentException>(
            () => command.ExecuteAsync(request with { ExpectedVersion = 0, Reason = " " }, default));
        Assert.Empty(store.PrincipalReplacements);
    }

    [Fact]
    public void RoleUpdatePolicyOwnsVersionAndActivePrincipalGuard()
    {
        var current = new Organization(
            Guid.NewGuid(),
            "Provider",
            [OrganizationRole.WorkProvider],
            7);

        var guard = Assert.Throws<OrganizationAdministrationException>(() =>
            OrganizationAdministrationPolicy.PlanRoleUpdate(
                current,
                7,
                [OrganizationRole.InstructionIntermediary],
                hasActivePrincipals: true));
        Assert.Equal(
            OrganizationAdministrationError.ActivePrincipalsRequireWorkProvider,
            guard.Error);

        var updated = OrganizationAdministrationPolicy.PlanRoleUpdate(
            current,
            7,
            [
                OrganizationRole.WorkProvider,
                OrganizationRole.InstructionIntermediary
            ],
            hasActivePrincipals: true);
        Assert.Equal(8, updated.Version);
        Assert.Equal(
            [OrganizationRole.WorkProvider, OrganizationRole.InstructionIntermediary],
            updated.Roles);
        var unchanged = OrganizationAdministrationPolicy.PlanRoleUpdate(
            updated,
            8,
            updated.Roles,
            hasActivePrincipals: true);
        Assert.Equal(8, unchanged.Version);

        var stale = Assert.Throws<OrganizationAdministrationException>(() =>
            OrganizationAdministrationPolicy.PlanRoleUpdate(
                current,
                6,
                [OrganizationRole.WorkProvider],
                hasActivePrincipals: false));
        Assert.Equal(OrganizationAdministrationError.StaleVersion, stale.Error);
    }

    [Fact]
    public void ReplacementPolicyLinksSuccessorWithoutMutatingOriginalIdentity()
    {
        var predecessor = new Principal(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "QDOS",
            Guid.NewGuid(),
            null,
            null,
            true,
            3);
        var successorOrganization = new Organization(
            Guid.NewGuid(),
            "Successor provider",
            [OrganizationRole.WorkProvider],
            2);
        var successorId = Guid.NewGuid();

        var replacement = OrganizationAdministrationPolicy.PlanPrincipalReplacement(
            predecessor,
            3,
            successorOrganization,
            successorId,
            " next ",
            codeAlreadyExists: false);

        Assert.True(predecessor.IsActive);
        Assert.Null(predecessor.SuccessorId);
        Assert.Equal("QDOS", predecessor.Code);
        Assert.False(replacement.Predecessor.IsActive);
        Assert.Equal(successorId, replacement.Predecessor.SuccessorId);
        Assert.Equal(4, replacement.Predecessor.Version);
        Assert.Equal("NEXT", replacement.Successor.Code);
        Assert.Equal(successorOrganization.Id, replacement.Successor.OrganizationId);
        Assert.Equal(predecessor.SequenceLineageId, replacement.Successor.SequenceLineageId);
        Assert.Equal(predecessor.Id, replacement.Successor.PredecessorId);
        Assert.True(replacement.Successor.IsActive);
        Assert.Equal(0, replacement.Successor.Version);
    }

    /// <summary>
    /// EXT-04. The settings change in place and nothing else does — the code,
    /// the organization and the lineage are what a replacement is for.
    /// </summary>
    [Fact]
    public void EvaSubmissionSettingsChangeInPlaceAndMoveTheVersion()
    {
        var current = Principal(version: 3);

        var updated = OrganizationAdministrationPolicy.PlanPrincipalEvaSubmissionUpdate(
            current,
            expectedVersion: 3,
            evaManualSubmission: true,
            evaAutomaticSubmission: true);

        Assert.True(updated.EvaManualSubmission);
        Assert.True(updated.EvaAutomaticSubmission);
        Assert.Equal(4, updated.Version);
        Assert.Equal(current.Id, updated.Id);
        Assert.Equal(current.Code, updated.Code);
        Assert.Equal(current.OrganizationId, updated.OrganizationId);
        Assert.Equal(current.SequenceLineageId, updated.SequenceLineageId);
    }

    /// <summary>
    /// Saving the settings unchanged is not a change, so it does not move the
    /// version and cannot invalidate another administrator's open form.
    /// </summary>
    [Fact]
    public void SavingUnchangedEvaSubmissionSettingsLeavesTheVersionAlone()
    {
        var current = Principal(version: 3) with { EvaManualSubmission = true };

        var updated = OrganizationAdministrationPolicy.PlanPrincipalEvaSubmissionUpdate(
            current,
            expectedVersion: 3,
            evaManualSubmission: true,
            evaAutomaticSubmission: false);

        Assert.Equal(3, updated.Version);
    }

    [Fact]
    public void EvaSubmissionSettingsRefuseAStaleVersion()
    {
        var error = Assert.Throws<OrganizationAdministrationException>(() =>
            OrganizationAdministrationPolicy.PlanPrincipalEvaSubmissionUpdate(
                Principal(version: 4),
                expectedVersion: 3,
                evaManualSubmission: true,
                evaAutomaticSubmission: false));

        Assert.Equal(OrganizationAdministrationError.StaleVersion, error.Error);
    }

    /// <summary>
    /// A replaced principal keeps its settings as a record of what it did.
    /// Its successor is the one that decides what happens next.
    /// </summary>
    [Fact]
    public void ADisabledPrincipalsEvaSubmissionSettingsCannotBeChanged()
    {
        var error = Assert.Throws<OrganizationAdministrationException>(() =>
            OrganizationAdministrationPolicy.PlanPrincipalEvaSubmissionUpdate(
                Principal(version: 3) with { IsActive = false },
                expectedVersion: 3,
                evaManualSubmission: true,
                evaAutomaticSubmission: false));

        Assert.Equal(OrganizationAdministrationError.PrincipalInactive, error.Error);
    }

    private static Principal Principal(long version) => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        "QDOS",
        Guid.NewGuid(),
        null,
        null,
        true,
        version);

    private sealed class RecordingStore : IOrganizationAdministrationStore
    {
        public List<CreateOrganizationRequest> OrganizationCreates { get; } = [];
        public List<UpdateOrganizationRolesRequest> OrganizationUpdates { get; } = [];
        public List<CreatePrincipalRequest> PrincipalCreates { get; } = [];
        public List<ReplacePrincipalRequest> PrincipalReplacements { get; } = [];
        public List<UpdatePrincipalEvaSubmissionRequest> EvaSubmissionUpdates { get; } = [];

        public Task<Organization> CreateOrganizationAsync(
            CreateOrganizationRequest request,
            CancellationToken cancellationToken)
        {
            OrganizationCreates.Add(request);
            return Task.FromResult(new Organization(
                Guid.NewGuid(),
                request.Name,
                request.Roles,
                0));
        }

        public Task<Organization> UpdateOrganizationRolesAsync(
            UpdateOrganizationRolesRequest request,
            CancellationToken cancellationToken)
        {
            OrganizationUpdates.Add(request);
            return Task.FromResult(new Organization(
                request.OrganizationId,
                "Organization",
                request.Roles,
                request.ExpectedVersion + 1));
        }

        public Task<Principal> UpdatePrincipalEvaSubmissionAsync(
            UpdatePrincipalEvaSubmissionRequest request,
            CancellationToken cancellationToken)
        {
            EvaSubmissionUpdates.Add(request);
            return Task.FromResult(new Principal(
                request.PrincipalId,
                Guid.NewGuid(),
                "QDOS",
                Guid.NewGuid(),
                null,
                null,
                true,
                request.ExpectedVersion + 1,
                CaseInspectionMode.PhysicalAddress,
                request.EvaManualSubmission,
                request.EvaAutomaticSubmission));
        }

        public Task<Principal> CreatePrincipalAsync(
            CreatePrincipalRequest request,
            CancellationToken cancellationToken)
        {
            PrincipalCreates.Add(request);
            return Task.FromResult(new Principal(
                Guid.NewGuid(),
                request.OrganizationId,
                request.Code,
                Guid.NewGuid(),
                null,
                null,
                true,
                0));
        }

        public Task<Principal> ReplacePrincipalAsync(
            ReplacePrincipalRequest request,
            CancellationToken cancellationToken)
        {
            PrincipalReplacements.Add(request);
            return Task.FromResult(new Principal(
                Guid.NewGuid(),
                request.SuccessorOrganizationId,
                request.SuccessorCode,
                Guid.NewGuid(),
                request.PrincipalId,
                null,
                true,
                0));
        }
    }

    private sealed class RecordingQueries : IOrganizationAdministrationQueries
    {
        public int ListCalls { get; private set; }
        public int Offset { get; private set; }
        public int Limit { get; private set; }

        public Task<OrganizationQuerySlice> ListAsync(
            int offset,
            int limit,
            CancellationToken cancellationToken)
        {
            ListCalls++;
            Offset = offset;
            Limit = limit;
            return Task.FromResult(new OrganizationQuerySlice([], true));
        }

        public Task<OrganizationDetails?> GetAsync(
            Guid organizationId,
            int principalLimit,
            Guid? requiredPrincipalId,
            CancellationToken cancellationToken) =>
            Task.FromResult<OrganizationDetails?>(null);
    }
}
