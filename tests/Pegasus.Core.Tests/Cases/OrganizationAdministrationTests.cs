using Pegasus.Core.Address;
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

    // C06 review R-4: item 6 (the principal's default inspection location)
    // had a recording store but nothing exercised
    // OrganizationAdministrationPolicy.Normalize(UpdatePrincipalDefaultInspectionLocationRequest)
    // or captured the values UpdatePrincipalDefaultInspectionLocation sends
    // to the store.

    [Fact]
    public void NormalizeDefaultInspectionLocationClearsAddressFieldsForImageBasedAssessment()
    {
        var request = new UpdatePrincipalDefaultInspectionLocationRequest(
            Administrator,
            Guid.NewGuid(),
            3,
            "  op-key  ",
            "  because reasons  ",
            InspectionAddressEvidenceKind.ImageBasedAssessment,
            Label: "Should be cleared",
            Address: "Should be cleared",
            Postcode: "Should be cleared",
            SourceKind: "directory",
            SourceRecordId: Guid.NewGuid(),
            SourceVersion: 5);

        var normalized = OrganizationAdministrationPolicy.Normalize(request);

        Assert.Equal("op-key", normalized.OperationKey);
        Assert.Equal("because reasons", normalized.Reason);
        Assert.Null(normalized.Label);
        Assert.Null(normalized.Address);
        Assert.Null(normalized.Postcode);
        Assert.Null(normalized.SourceKind);
        Assert.Null(normalized.SourceRecordId);
        Assert.Null(normalized.SourceVersion);
    }

    [Fact]
    public void NormalizeDefaultInspectionLocationTrimsAPhysicalAddressAndPostcode()
    {
        var request = new UpdatePrincipalDefaultInspectionLocationRequest(
            Administrator,
            Guid.NewGuid(),
            0,
            "op-key",
            "reason",
            InspectionAddressEvidenceKind.PhysicalAddress,
            Label: "Yard",
            Address: "  1 Test Street  ",
            Postcode: "  TE1 1ST  ",
            SourceKind: "manual",
            SourceRecordId: null,
            SourceVersion: null);

        var normalized = OrganizationAdministrationPolicy.Normalize(request);

        Assert.Equal("1 Test Street", normalized.Address);
        Assert.Equal("TE1 1ST", normalized.Postcode);
    }

    [Fact]
    public void NormalizeDefaultInspectionLocationRequiresAnAddressForAPhysicalChoice()
    {
        var request = new UpdatePrincipalDefaultInspectionLocationRequest(
            Administrator,
            Guid.NewGuid(),
            0,
            "op-key",
            "reason",
            InspectionAddressEvidenceKind.PhysicalAddress,
            Label: null,
            Address: "   ",
            Postcode: null,
            SourceKind: null,
            SourceRecordId: null,
            SourceVersion: null);

        Assert.Throws<ArgumentException>(() => OrganizationAdministrationPolicy.Normalize(request));
    }

    [Fact]
    public void NormalizeDefaultInspectionLocationRequiresAReason()
    {
        var request = new UpdatePrincipalDefaultInspectionLocationRequest(
            Administrator,
            Guid.NewGuid(),
            0,
            "op-key",
            "   ",
            InspectionAddressEvidenceKind.ImageBasedAssessment,
            null, null, null, null, null, null);

        Assert.Throws<ArgumentException>(() => OrganizationAdministrationPolicy.Normalize(request));
    }

    [Fact]
    public void NormalizeDefaultInspectionLocationRejectsAnUndefinedKind()
    {
        var request = new UpdatePrincipalDefaultInspectionLocationRequest(
            Administrator,
            Guid.NewGuid(),
            0,
            "op-key",
            "reason",
            (InspectionAddressEvidenceKind)99,
            null, null, null, null, null, null);

        Assert.Throws<ArgumentOutOfRangeException>(() => OrganizationAdministrationPolicy.Normalize(request));
    }

    [Fact]
    public async Task UpdatePrincipalDefaultInspectionLocationDeniesNonAdministratorBeforePersistence()
    {
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var store = new RecordingStore();
        var command = new UpdatePrincipalDefaultInspectionLocation(store);

        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            command.ExecuteAsync(
                new(
                    actor,
                    Guid.NewGuid(),
                    0,
                    "op-key",
                    "reason",
                    InspectionAddressEvidenceKind.ImageBasedAssessment,
                    null, null, null, null, null, null),
                default));

        Assert.Empty(store.DefaultInspectionLocationUpdates);
    }

    [Fact]
    public async Task UpdatePrincipalDefaultInspectionLocationNormalizesInputBeforeCallingPersistencePort()
    {
        var store = new RecordingStore();
        var command = new UpdatePrincipalDefaultInspectionLocation(store);
        var principalId = Guid.NewGuid();

        await command.ExecuteAsync(
            new(
                Administrator,
                principalId,
                2,
                "  op-key  ",
                "  physical override reason  ",
                InspectionAddressEvidenceKind.PhysicalAddress,
                "Yard",
                "  1 Test Street  ",
                "  TE1 1ST  ",
                "manual",
                null,
                null),
            default);

        var request = Assert.Single(store.DefaultInspectionLocationUpdates);
        Assert.Equal(principalId, request.PrincipalId);
        Assert.Equal("op-key", request.OperationKey);
        Assert.Equal("physical override reason", request.Reason);
        Assert.Equal("1 Test Street", request.Address);
        Assert.Equal("TE1 1ST", request.Postcode);
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
    /// EXT-04. The manual setting changes in place and nothing else does — the
    /// code, the organization and the lineage are what a replacement is for.
    /// </summary>
    [Fact]
    public void EvaSubmissionSettingsChangeInPlaceAndMoveTheVersion()
    {
        var current = Principal(version: 3);

        var updated = OrganizationAdministrationPolicy.PlanPrincipalEvaSubmissionUpdate(
            current,
            expectedVersion: 3,
            evaManualSubmission: true);

        Assert.True(updated.EvaManualSubmission);
        Assert.Equal(4, updated.Version);
        Assert.Equal(current.Id, updated.Id);
        Assert.Equal(current.Code, updated.Code);
        Assert.Equal(current.OrganizationId, updated.OrganizationId);
        Assert.Equal(current.SequenceLineageId, updated.SequenceLineageId);
    }

    /// <summary>
    /// EXT-18 item 7: automatic EVA submission is retired from this
    /// administration surface. Even a principal persisted with it already
    /// true (historical data) is forced back to false the next time its
    /// manual setting is saved, and it can never be turned on again here.
    /// </summary>
    [Fact]
    public void AutomaticEvaSubmissionIsAlwaysClearedByThisUpdate()
    {
        var current = Principal(version: 3) with { EvaAutomaticSubmission = true };

        var updated = OrganizationAdministrationPolicy.PlanPrincipalEvaSubmissionUpdate(
            current,
            expectedVersion: 3,
            evaManualSubmission: false);

        Assert.False(updated.EvaAutomaticSubmission);
        Assert.Equal(4, updated.Version);
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
            evaManualSubmission: true);

        Assert.Equal(3, updated.Version);
    }

    [Fact]
    public void EvaSubmissionSettingsRefuseAStaleVersion()
    {
        var error = Assert.Throws<OrganizationAdministrationException>(() =>
            OrganizationAdministrationPolicy.PlanPrincipalEvaSubmissionUpdate(
                Principal(version: 4),
                expectedVersion: 3,
                evaManualSubmission: true));

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
                evaManualSubmission: true));

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
                EvaAutomaticSubmission: false));
        }

        public List<UpdatePrincipalDefaultInspectionLocationRequest> DefaultInspectionLocationUpdates
        { get; } = [];

        public Task<PrincipalAdministrationSummary> UpdatePrincipalDefaultInspectionLocationAsync(
            UpdatePrincipalDefaultInspectionLocationRequest request,
            CancellationToken cancellationToken)
        {
            DefaultInspectionLocationUpdates.Add(request);
            return Task.FromResult(new PrincipalAdministrationSummary(
                request.PrincipalId,
                Guid.NewGuid(),
                "QDOS",
                Guid.NewGuid(),
                null,
                null,
                true,
                request.ExpectedVersion + 1,
                0,
                CaseInspectionMode.PhysicalAddress,
                EvaManualSubmission: false,
                request.Label,
                request.Address,
                request.Postcode,
                request.SourceKind,
                request.SourceRecordId,
                request.SourceVersion));
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
