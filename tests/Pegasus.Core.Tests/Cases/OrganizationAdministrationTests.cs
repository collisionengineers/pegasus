using Pegasus.Core.Address;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Reports;

namespace Pegasus.Core.Tests.Cases;

public sealed class OrganizationAdministrationTests
{
    private static readonly ActionActor Administrator =
        ActionActor.Staff(Guid.Parse("63f98d69-5368-48b8-b25d-a61ec91f6905"), [StaffRole.Administrator]);

    [Fact]
    public async Task PrincipalCommandsNormalizeCodesAndOptionalChangeReasons()
    {
        var store = new RecordingStore();
        var replace = new ReplacePrincipal(store);
        var principalId = Guid.NewGuid();

        await replace.ExecuteAsync(
            new(
                principalId,
                4,
                " qdos3 ",
                Administrator,
                " replace-principal ",
                " successor required ",
                4),
            default);

        var replacement = Assert.Single(store.PrincipalReplacements);
        Assert.Equal("QDOS3", replacement.SuccessorCode);
        Assert.Equal("successor required", replacement.Reason);
        Assert.Equal(4, replacement.ExpectedContactVersion);
        Assert.Equal("replace-principal", replacement.OperationKey);
    }

    [Fact]
    public async Task NonAdministratorCannotReachMutationOrQueryPorts()
    {
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var store = new RecordingStore();
        var queries = new RecordingQueries();

        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            new ReplacePrincipal(store).ExecuteAsync(
                new(Guid.NewGuid(), 0, "DENIED", actor, "denied-replace", null, 0),
                default));

        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            new GetPrincipal(queries).ExecuteAsync(actor, Guid.NewGuid(), default));

        Assert.Empty(store.PrincipalReplacements);
        Assert.Equal(0, queries.GetCalls);
    }

    [Fact]
    public async Task InvalidReplacementVersionFailsBeforePersistenceAndABlankReasonIsOptional()
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
            0);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => command.ExecuteAsync(request, default));
        Assert.Empty(store.PrincipalReplacements);

        await command.ExecuteAsync(request with { ExpectedVersion = 0, Reason = " " }, default);
        Assert.Null(Assert.Single(store.PrincipalReplacements).Reason);
    }

    [Fact]
    public void NormalizeDefaultInspectionLocationClearsAddressFieldsForImageBasedAssessment()
    {
        var request = new UpdatePrincipalDefaultInspectionLocationRequest(
            Administrator,
            Guid.NewGuid(),
            3,
            "  op-key  ",
            InspectionAddressEvidenceKind.ImageBasedAssessment,
            Label: "Should be cleared",
            Address: "Should be cleared",
            Postcode: "Should be cleared",
            SourceKind: "directory",
            SourceRecordId: Guid.NewGuid(),
            SourceVersion: 5,
            ExpectedContactVersion: 3);

        var normalized = OrganizationAdministrationPolicy.Normalize(request);

        Assert.Equal("op-key", normalized.OperationKey);
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
            InspectionAddressEvidenceKind.PhysicalAddress,
            Label: "Yard",
            Address: "  1 Test Street  ",
            Postcode: "  TE1 1ST  ",
            SourceKind: "manual",
            SourceRecordId: null,
            SourceVersion: null,
            ExpectedContactVersion: 0);

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
            InspectionAddressEvidenceKind.PhysicalAddress,
            Label: null,
            Address: "   ",
            Postcode: null,
            SourceKind: null,
            SourceRecordId: null,
            SourceVersion: null,
            ExpectedContactVersion: 0);

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
            (InspectionAddressEvidenceKind)99,
            null, null, null, null, null, null,
            0);

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
                    InspectionAddressEvidenceKind.ImageBasedAssessment,
                    null, null, null, null, null, null,
                    0),
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
                InspectionAddressEvidenceKind.PhysicalAddress,
                "Yard",
                "  1 Test Street  ",
                "  TE1 1ST  ",
                "manual",
                null,
                null,
                2),
            default);

        var request = Assert.Single(store.DefaultInspectionLocationUpdates);
        Assert.Equal(principalId, request.PrincipalId);
        Assert.Equal("op-key", request.OperationKey);
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

    [Fact]
    public void ASuccessorInheritsItsPredecessorsSalvageMatrix()
    {
        var predecessor = Principal(version: 3) with { SalvageMatrix = Matrix(2m) };

        var replacement = OrganizationAdministrationPolicy.PlanPrincipalReplacement(
            predecessor,
            3,
            Guid.NewGuid(),
            "NEXT",
            codeAlreadyExists: false);

        Assert.Equal(Matrix(2m), replacement.Successor.SalvageMatrix);
        Assert.Equal(Matrix(2m), replacement.Predecessor.SalvageMatrix);
    }

    [Fact]
    public void ASuccessorInheritsItsPredecessorsDefaultFee()
    {
        var predecessor = Principal(version: 3) with { DefaultFee = 210.00m };

        var replacement = OrganizationAdministrationPolicy.PlanPrincipalReplacement(
            predecessor,
            3,
            Guid.NewGuid(),
            "NEXT",
            codeAlreadyExists: false);

        Assert.Equal(210.00m, replacement.Successor.DefaultFee);
    }

    /// <summary>
    /// The default fee is saved with the report settings and moves the
    /// version only when it changes.
    /// </summary>
    [Fact]
    public void TheDefaultFeeChangesWithTheReportSettings()
    {
        var current = Principal(version: 3) with { ReportRecipients = PrincipalReportRecipientSettings.None };

        var updated = OrganizationAdministrationPolicy.PlanPrincipalReportSettingsUpdate(
            current,
            expectedVersion: 3,
            reportGenerationPolicy: current.ReportGenerationPolicy,
            reportRecipients: PrincipalReportRecipientSettings.None,
            defaultFee: 200.00m);

        Assert.Equal(200.00m, updated.DefaultFee);
        Assert.Equal(4, updated.Version);
        Assert.Equal(4, OrganizationAdministrationPolicy.PlanPrincipalReportSettingsUpdate(
            updated,
            expectedVersion: 4,
            reportGenerationPolicy: updated.ReportGenerationPolicy,
            reportRecipients: PrincipalReportRecipientSettings.None,
            defaultFee: 200m).Version);
    }

    /// <summary>
    /// The default fee is required: more than £0, in whole pence, the
    /// agreed fee's own rule.
    /// </summary>
    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("180.005")]
    public void ADefaultFeeTheAgreedFeeWouldRefuseIsRefused(string fee)
    {
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        var request = new UpdatePrincipalReportSettingsRequest(
            Guid.NewGuid(), 1, actor, "fee-op", null, PrincipalReportGenerationPolicy.Pegasus,
            PrincipalReportRecipientSettings.None,
            decimal.Parse(fee, System.Globalization.CultureInfo.InvariantCulture), 1);

        Assert.ThrowsAny<ArgumentException>(() => OrganizationAdministrationPolicy.Normalize(request));
        Assert.ThrowsAny<ArgumentException>(() =>
            OrganizationAdministrationPolicy.PlanPrincipalReportSettingsUpdate(
                Principal(version: 3),
                expectedVersion: 3,
                reportGenerationPolicy: PrincipalReportGenerationPolicy.Pegasus,
                reportRecipients: PrincipalReportRecipientSettings.None,
                defaultFee: request.DefaultFee));
    }

    [Fact]
    public void TheSalvageMatrixChangesInPlaceAndMovesTheVersionOnlyWhenChanged()
    {
        var current = Principal(version: 3);

        var set = OrganizationAdministrationPolicy.PlanPrincipalSalvageMatrixUpdate(current, 3, Matrix(2m));
        Assert.Equal(Matrix(2m), set.SalvageMatrix);
        Assert.Equal(4, set.Version);
        Assert.Equal(current.Code, set.Code);
        Assert.Equal(current.ReportGenerationPolicy, set.ReportGenerationPolicy);

        var unchanged = OrganizationAdministrationPolicy.PlanPrincipalSalvageMatrixUpdate(set, 4, Matrix(2m));
        Assert.Equal(4, unchanged.Version);

        var cleared = OrganizationAdministrationPolicy.PlanPrincipalSalvageMatrixUpdate(set, 4, null);
        Assert.Null(cleared.SalvageMatrix);
        Assert.Equal(5, cleared.Version);
    }

    [Fact]
    public void TheSalvageMatrixRefusesAStaleVersionAndADisabledPrincipal()
    {
        Assert.Equal(
            OrganizationAdministrationError.StaleVersion,
            Assert.Throws<OrganizationAdministrationException>(() =>
                OrganizationAdministrationPolicy.PlanPrincipalSalvageMatrixUpdate(
                    Principal(version: 4), 3, Matrix(2m))).Error);
        Assert.Equal(
            OrganizationAdministrationError.PrincipalInactive,
            Assert.Throws<OrganizationAdministrationException>(() =>
                OrganizationAdministrationPolicy.PlanPrincipalSalvageMatrixUpdate(
                    Principal(version: 3) with { IsActive = false }, 3, Matrix(2m))).Error);
    }

    [Fact]
    public async Task UpdatePrincipalSalvageMatrixNormalizesBeforeCallingPersistence()
    {
        var store = new RecordingStore();
        var command = new UpdatePrincipalSalvageMatrix(store);
        var unsorted = new SalvageMatrix(
        [
            new("N", 1000.01m, 2500m, 4m),
            new("N", 0.01m, 1000m, 0m)
        ]);

        await command.ExecuteAsync(
            new(Guid.NewGuid(), 2, Administrator, " salvage-op ", unsorted, 5),
            default);

        var request = Assert.Single(store.SalvageMatrixUpdates);
        Assert.Equal("salvage-op", request.OperationKey);
        Assert.Equal(0.01m, request.SalvageMatrix!.Bands[0].From);
        Assert.Equal(1000.01m, request.SalvageMatrix.Bands[1].From);
        Assert.Equal(5, request.ExpectedContactVersion);
    }

    [Fact]
    public async Task UpdatePrincipalSalvageMatrixRefusesBeforePersistence()
    {
        var store = new RecordingStore();
        var command = new UpdatePrincipalSalvageMatrix(store);
        var overlapping = new SalvageMatrix(
        [
            new("S", 0.01m, 1000m, 0m),
            new("S", 1000m, 2500m, 2m)
        ]);

        await Assert.ThrowsAsync<StaffAuthorizationException>(() => command.ExecuteAsync(
            new(Guid.NewGuid(), 0, ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]), "denied", Matrix(2m), 0),
            default));
        await Assert.ThrowsAsync<SalvageMatrixException>(() => command.ExecuteAsync(
            new(Guid.NewGuid(), 0, Administrator, "overlap", overlapping, 0),
            default));

        Assert.Empty(store.SalvageMatrixUpdates);
    }

    [Fact]
    public void ASuccessorInheritsItsPredecessorsReportSendingRules()
    {
        var predecessor = Principal(version: 3) with { ReportSending = SendingRules("one@example.com") };

        var replacement = OrganizationAdministrationPolicy.PlanPrincipalReplacement(
            predecessor,
            3,
            Guid.NewGuid(),
            "NEXT",
            codeAlreadyExists: false);

        Assert.Equal(SendingRules("one@example.com"), replacement.Successor.ReportSending);
        Assert.Equal(SendingRules("one@example.com"), replacement.Predecessor.ReportSending);
    }

    [Fact]
    public void TheReportSendingRulesChangeInPlaceAndMoveTheVersionOnlyWhenChanged()
    {
        var current = Principal(version: 3);

        var set = OrganizationAdministrationPolicy.PlanPrincipalReportSendingUpdate(
            current, 3, SendingRules("one@example.com"));
        Assert.Equal(SendingRules("one@example.com"), set.ReportSending);
        Assert.Equal(4, set.Version);
        Assert.Equal(current.Code, set.Code);
        Assert.Equal(current.ReportGenerationPolicy, set.ReportGenerationPolicy);

        var unchanged = OrganizationAdministrationPolicy.PlanPrincipalReportSendingUpdate(
            set, 4, SendingRules("ONE@example.com"));
        Assert.Equal(4, unchanged.Version);

        var changed = OrganizationAdministrationPolicy.PlanPrincipalReportSendingUpdate(
            set, 4, SendingRules("two@example.com"));
        Assert.Equal(5, changed.Version);
    }

    [Fact]
    public void TheReportSendingRulesAreNormalizedBeforeTheyAreKept()
    {
        var updated = OrganizationAdministrationPolicy.PlanPrincipalReportSendingUpdate(
            Principal(version: 3),
            3,
            PrincipalReportSendingRules.Default with { Cc = [" one@example.com ", "", "ONE@example.com"] });

        Assert.Equal("one@example.com", Assert.Single(updated.ReportSending!.Cc));
    }

    [Fact]
    public void TheReportSendingRulesRefuseAStaleVersionAndADisabledPrincipal()
    {
        Assert.Equal(
            OrganizationAdministrationError.StaleVersion,
            Assert.Throws<OrganizationAdministrationException>(() =>
                OrganizationAdministrationPolicy.PlanPrincipalReportSendingUpdate(
                    Principal(version: 4), 3, SendingRules("one@example.com"))).Error);
        Assert.Equal(
            OrganizationAdministrationError.PrincipalInactive,
            Assert.Throws<OrganizationAdministrationException>(() =>
                OrganizationAdministrationPolicy.PlanPrincipalReportSendingUpdate(
                    Principal(version: 3) with { IsActive = false }, 3, SendingRules("one@example.com"))).Error);
    }

    [Fact]
    public void TheReportSendingRulesRefuseWhatNormalizeRefuses()
    {
        var error = Assert.Throws<ReportSendingRulesException>(() =>
            OrganizationAdministrationPolicy.PlanPrincipalReportSendingUpdate(
                Principal(version: 3), 3, SendingRules("not an address")));

        Assert.Equal(ReportSendingRulesRule.InvalidAddress, error.Rule);
    }

    [Fact]
    public async Task UpdatePrincipalReportSendingNormalizesBeforeCallingPersistence()
    {
        var store = new RecordingStore();
        var command = new UpdatePrincipalReportSending(store);

        await command.ExecuteAsync(
            new(Guid.NewGuid(), 2, Administrator, " sending-op ", SendingRules(" one@example.com "), 5),
            default);

        var request = Assert.Single(store.ReportSendingUpdates);
        Assert.Equal("sending-op", request.OperationKey);
        Assert.Equal("one@example.com", Assert.Single(request.Rules!.SendTo));
        Assert.Equal(5, request.ExpectedContactVersion);
    }

    [Fact]
    public async Task UpdatePrincipalReportSendingRefusesBeforePersistence()
    {
        var store = new RecordingStore();
        var command = new UpdatePrincipalReportSending(store);

        await Assert.ThrowsAsync<StaffAuthorizationException>(() => command.ExecuteAsync(
            new(Guid.NewGuid(), 0, ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]), "denied", SendingRules("one@example.com"), 0),
            default));
        await Assert.ThrowsAsync<ReportSendingRulesException>(() => command.ExecuteAsync(
            new(Guid.NewGuid(), 0, Administrator, "invalid", SendingRules("not an address"), 0),
            default));

        Assert.Empty(store.ReportSendingUpdates);
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
            reportRecipients: PrincipalReportRecipientSettings.None,
            defaultFee: PrincipalDefaultFeePolicy.Standard);

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
            reportRecipients: PrincipalReportRecipientSettings.None,
            defaultFee: PrincipalDefaultFeePolicy.Standard);

        Assert.Equal(3, updated.Version);
    }

    [Fact]
    public void NotesOnEveryCaseChangeInPlaceWithTheSettings()
    {
        var current = Principal(version: 3) with { ReportRecipients = PrincipalReportRecipientSettings.None };

        var updated = OrganizationAdministrationPolicy.PlanPrincipalReportSettingsUpdate(
            current,
            expectedVersion: 3,
            reportGenerationPolicy: current.ReportGenerationPolicy,
            reportRecipients: PrincipalReportRecipientSettings.None,
            defaultFee: PrincipalDefaultFeePolicy.Standard,
            notesOnEveryCase: "Always copy the fleet manager.");

        Assert.Equal("Always copy the fleet manager.", updated.NotesOnEveryCase);
        Assert.Equal(4, updated.Version);

        var unchanged = OrganizationAdministrationPolicy.PlanPrincipalReportSettingsUpdate(
            updated,
            expectedVersion: 4,
            reportGenerationPolicy: updated.ReportGenerationPolicy,
            reportRecipients: PrincipalReportRecipientSettings.None,
            defaultFee: PrincipalDefaultFeePolicy.Standard,
            notesOnEveryCase: "Always copy the fleet manager.");
        Assert.Equal(4, unchanged.Version);
    }

    [Fact]
    public void NotesOnEveryCaseAreTrimmedAndBounded()
    {
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        var request = new UpdatePrincipalReportSettingsRequest(
            Guid.NewGuid(), 1, actor, "notes-op", null, PrincipalReportGenerationPolicy.Pegasus,
            PrincipalReportRecipientSettings.None, PrincipalDefaultFeePolicy.Standard, 1, "  Always copy the fleet manager.  ");

        Assert.Equal("Always copy the fleet manager.", OrganizationAdministrationPolicy.Normalize(request).NotesOnEveryCase);
        Assert.Null(OrganizationAdministrationPolicy.Normalize(request with { NotesOnEveryCase = "   " }).NotesOnEveryCase);
        Assert.ThrowsAny<ArgumentException>(() =>
            OrganizationAdministrationPolicy.Normalize(request with { NotesOnEveryCase = new string('x', 2001) }));
    }

    [Fact]
    public void ReportSettingsRefuseAStaleVersion()
    {
        var error = Assert.Throws<OrganizationAdministrationException>(() =>
            OrganizationAdministrationPolicy.PlanPrincipalReportSettingsUpdate(
                Principal(version: 4),
                expectedVersion: 3,
                reportGenerationPolicy: PrincipalReportGenerationPolicy.EvaManualApi,
                reportRecipients: PrincipalReportRecipientSettings.None,
                defaultFee: PrincipalDefaultFeePolicy.Standard));

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
                reportRecipients: PrincipalReportRecipientSettings.None,
                defaultFee: PrincipalDefaultFeePolicy.Standard));

        Assert.Equal(OrganizationAdministrationError.PrincipalInactive, error.Error);
    }

    private static PrincipalReportSendingRules SendingRules(string sendTo) =>
        PrincipalReportSendingRules.Default with { SendTo = [sendTo] };

    private static SalvageMatrix Matrix(decimal percentage) =>
        SalvageMatrix.Normalize([new("S", 0.01m, 9999999.99m, percentage)])!;

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
                request.ReportRecipients,
                DefaultFee: request.DefaultFee));
        }

        public List<UpdatePrincipalReportSendingRequest> ReportSendingUpdates { get; } = [];

        public Task<Principal> UpdatePrincipalReportSendingAsync(
            UpdatePrincipalReportSendingRequest request,
            CancellationToken cancellationToken)
        {
            ReportSendingUpdates.Add(request);
            return Task.FromResult(new Principal(
                request.PrincipalId,
                Guid.NewGuid(),
                "QDOS",
                Guid.NewGuid(),
                null,
                null,
                true,
                request.ExpectedVersion + 1,
                ReportSending: request.Rules));
        }

        public List<UpdatePrincipalSalvageMatrixRequest> SalvageMatrixUpdates { get; } = [];

        public Task<Principal> UpdatePrincipalSalvageMatrixAsync(
            UpdatePrincipalSalvageMatrixRequest request,
            CancellationToken cancellationToken)
        {
            SalvageMatrixUpdates.Add(request);
            return Task.FromResult(new Principal(
                request.PrincipalId,
                Guid.NewGuid(),
                "QDOS",
                Guid.NewGuid(),
                null,
                null,
                true,
                request.ExpectedVersion + 1,
                SalvageMatrix: request.SalvageMatrix));
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
        public int GetCalls { get; private set; }

        public Task<PrincipalAdministrationDetails?> GetPrincipalAsync(
            Guid principalId, CancellationToken cancellationToken)
        {
            GetCalls++;
            return Task.FromResult<PrincipalAdministrationDetails?>(null);
        }
    }
}
