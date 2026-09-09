using Pegasus.Core.Address;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Reports;

namespace Pegasus.Core.Tests.Cases;

public sealed class OrganizationAdministrationTests
{
    private static readonly ActionActor Administrator =
        ActionActor.Staff(Guid.Parse("63f98d69-5368-48b8-b25d-a61ec91f6905"), [StaffRole.Administrator]);

    [Fact]
    public async Task PrincipalCommandsNormalizeCodesAndRequiredChangeReasons()
    {
        var store = new RecordingStore();
        var create = new CreatePrincipal(store);
        var replace = new ReplacePrincipal(store);
        var principalId = Guid.NewGuid();

        await create.ExecuteAsync(
            new("  QDOS Services  ", " qdos2 ", Administrator, " create-principal "),
            default);
        await replace.ExecuteAsync(
            new(
                principalId,
                4,
                " qdos3 ",
                Administrator,
                " replace-principal ",
                " successor required ",
                4,
                "edit-token"),
            default);

        Assert.Equal("QDOS2", Assert.Single(store.PrincipalCreates).Code);
        Assert.Equal("QDOS Services", Assert.Single(store.PrincipalCreates).Name);
        var replacement = Assert.Single(store.PrincipalReplacements);
        Assert.Equal("QDOS3", replacement.SuccessorCode);
        Assert.Equal("successor required", replacement.Reason);
        Assert.Equal(4, replacement.ExpectedContactVersion);
        Assert.Equal("edit-token", replacement.EditLeaseToken);
    }

    [Fact]
    public async Task NonAdministratorCannotReachMutationOrQueryPorts()
    {
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var store = new RecordingStore();
        var queries = new RecordingQueries();

        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            new CreatePrincipal(store).ExecuteAsync(
                new("QDOS Services", "DENIED", actor, "denied-create"),
                default));

        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            new ListPrincipals(queries).ExecuteAsync(actor, 1, default));
        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            new GetPrincipal(queries).ExecuteAsync(actor, Guid.NewGuid(), default));

        Assert.Empty(store.PrincipalCreates);
        Assert.Equal(0, queries.ListCalls);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("QDOS\u0001")]
    public async Task InvalidCustomerNameFailsBeforePersistence(string name)
    {
        var store = new RecordingStore();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            new CreatePrincipal(store).ExecuteAsync(
                new(name, "QDOS2", Administrator, "invalid-name"), default));
        Assert.Empty(store.PrincipalCreates);
    }

    [Fact]
    public async Task PrincipalListBoundsTheFlatProjection()
    {
        var queries = new RecordingQueries();
        var query = new ListPrincipals(queries);

        var page = await query.ExecuteAsync(Administrator, 3, default);

        Assert.Equal(50, queries.Offset);
        Assert.Equal(26, queries.Limit);
        Assert.Equal(3, page.PageNumber);
        Assert.False(page.HasMore);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            query.ExecuteAsync(Administrator, 0, default));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            query.ExecuteAsync(Administrator, int.MaxValue, default));
    }

    [Fact]
    public async Task InvalidReplacementVersionAndReasonFailBeforePersistence()
    {
        var store = new RecordingStore();
        var command = new ReplacePrincipal(store);
        var request = new ReplacePrincipalRequest(
            Guid.NewGuid(),
            -1,
            "NEXT",
            Administrator,
            "replace",
            "reason",
            0,
            "edit-token");

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
            SourceVersion: 5,
            ExpectedContactVersion: 3,
            EditLeaseToken: "edit-token");

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
            SourceVersion: null,
            ExpectedContactVersion: 0,
            EditLeaseToken: "edit-token");

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
            SourceVersion: null,
            ExpectedContactVersion: 0,
            EditLeaseToken: "edit-token");

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
            null, null, null, null, null, null,
            0, "edit-token");

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
            null, null, null, null, null, null,
            0, "edit-token");

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
                    null, null, null, null, null, null,
                    0, "edit-token"),
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
                null,
                2,
                "edit-token"),
            default);

        var request = Assert.Single(store.DefaultInspectionLocationUpdates);
        Assert.Equal(principalId, request.PrincipalId);
        Assert.Equal("op-key", request.OperationKey);
        Assert.Equal("physical override reason", request.Reason);
        Assert.Equal("1 Test Street", request.Address);
        Assert.Equal("TE1 1ST", request.Postcode);
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
        var successorId = Guid.NewGuid();

        var replacement = OrganizationAdministrationPolicy.PlanPrincipalReplacement(
            predecessor,
            3,
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
        Assert.Equal(predecessor.OrganizationId, replacement.Successor.OrganizationId);
        Assert.Equal(predecessor.SequenceLineageId, replacement.Successor.SequenceLineageId);
        Assert.Equal(predecessor.Id, replacement.Successor.PredecessorId);
        Assert.True(replacement.Successor.IsActive);
        Assert.Equal(0, replacement.Successor.Version);
    }

    /// <summary>
    /// EXT-04. The report policy changes in place and nothing else does — the
    /// code, the organization and the lineage are what a replacement is for.
    /// </summary>
    [Fact]
    public void ReportSettingsChangeInPlaceAndMoveTheVersion()
    {
        var current = Principal(version: 3);

        var updated = OrganizationAdministrationPolicy.PlanPrincipalReportSettingsUpdate(
            current,
            expectedVersion: 3,
            reportGenerationPolicy: PrincipalReportGenerationPolicy.EvaManualApi,
            reportRecipients: PrincipalReportRecipientSettings.None);

        Assert.Equal(PrincipalReportGenerationPolicy.EvaManualApi, updated.ReportGenerationPolicy);
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
    public void SavingUnchangedReportSettingsLeavesTheVersionAlone()
    {
        var current = Principal(version: 3) with { ReportGenerationPolicy = PrincipalReportGenerationPolicy.EvaManualApi, ReportRecipients = PrincipalReportRecipientSettings.None };

        var updated = OrganizationAdministrationPolicy.PlanPrincipalReportSettingsUpdate(
            current,
            expectedVersion: 3,
            reportGenerationPolicy: PrincipalReportGenerationPolicy.EvaManualApi,
            reportRecipients: PrincipalReportRecipientSettings.None);

        Assert.Equal(3, updated.Version);
    }

    [Fact]
    public void ReportSettingsRefuseAStaleVersion()
    {
        var error = Assert.Throws<OrganizationAdministrationException>(() =>
            OrganizationAdministrationPolicy.PlanPrincipalReportSettingsUpdate(
                Principal(version: 4),
                expectedVersion: 3,
                reportGenerationPolicy: PrincipalReportGenerationPolicy.EvaManualApi,
                reportRecipients: PrincipalReportRecipientSettings.None));

        Assert.Equal(OrganizationAdministrationError.StaleVersion, error.Error);
    }

    /// <summary>
    /// A replaced principal keeps its settings as a record of what it did.
    /// Its successor is the one that decides what happens next.
    /// </summary>
    [Fact]
    public void ADisabledPrincipalsReportSettingsCannotBeChanged()
    {
        var error = Assert.Throws<OrganizationAdministrationException>(() =>
            OrganizationAdministrationPolicy.PlanPrincipalReportSettingsUpdate(
                Principal(version: 3) with { IsActive = false },
                expectedVersion: 3,
                reportGenerationPolicy: PrincipalReportGenerationPolicy.EvaManualApi,
                reportRecipients: PrincipalReportRecipientSettings.None));

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
        public List<CreatePrincipalRequest> PrincipalCreates { get; } = [];
        public List<ReplacePrincipalRequest> PrincipalReplacements { get; } = [];
        public List<UpdatePrincipalReportSettingsRequest> ReportSettingsUpdates { get; } = [];

        public Task<Principal> UpdatePrincipalReportSettingsAsync(
            UpdatePrincipalReportSettingsRequest request,
            CancellationToken cancellationToken)
        {
            ReportSettingsUpdates.Add(request);
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
                request.ReportGenerationPolicy,
                request.ReportRecipients));
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
                ReportGenerationPolicy: PrincipalReportGenerationPolicy.Pegasus,
                ReportRecipients: PrincipalReportRecipientSettings.None,
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
                Guid.NewGuid(),
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
                Guid.NewGuid(),
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

        public Task<IReadOnlyList<PrincipalAdministrationDetails>> ListPrincipalsAsync(
            int offset, int limit, CancellationToken cancellationToken)
        {
            ListCalls++;
            Offset = offset;
            Limit = limit;
            return Task.FromResult<IReadOnlyList<PrincipalAdministrationDetails>>([]);
        }

        public Task<PrincipalAdministrationDetails?> GetPrincipalAsync(
            Guid principalId, CancellationToken cancellationToken) =>
            Task.FromResult<PrincipalAdministrationDetails?>(null);

    }
}
