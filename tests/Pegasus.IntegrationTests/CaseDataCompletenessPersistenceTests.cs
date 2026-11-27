using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Pegasus.Core.Address;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.PrincipalApi;
using Pegasus.Core.Triage;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class CaseDataCompletenessPersistenceTests
{
    [Fact]
    public async Task TheCaseDataReadCarriesTheConfiguredAiDraftTargetFromItsOneCommand()
    {
        await using var harness = await CaseDataHarness.CreateAsync();
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            var configuration = await context.Set<WorkflowConfigurationEntity>().SingleAsync();
            configuration.AiDraftTargetDays = 9;
            configuration.Version++;
            await context.SaveChangesAsync();
        }

        var data = await harness.GetRequiredDataAsync();

        Assert.Equal(9, data.AiDraftTargetDays);
    }

    [Fact]
    public async Task TheWorkspaceSaveEvaluatesCompletenessAgainstThePersistedConfiguration()
    {
        await using var harness = await CaseDataHarness.CreateAsync();
        var initial = await harness.GetRequiredDataAsync();
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            var configuration = await context.Set<WorkflowConfigurationEntity>().SingleAsync();
            configuration.RequireImages = false;
            configuration.Version++;
            await context.SaveChangesAsync();
        }
        var lease = await harness.AcquireLeaseAsync(initial.Version, harness.StaffActor, "edit-configured-readiness");
        var saved = await harness.WorkspaceStore.SaveAsync(
            new SaveCaseWorkspaceRequest(
                harness.CaseId,
                initial.Version,
                harness.StaffActor,
                "save-configured-readiness",
                null,
                lease.Token)
            {
                Completeness = new(true, false)
            },
            default);
        Assert.Equal(CaseLifecycleState.Review, saved.Data.State);
        Assert.True(saved.Completeness.Evaluation.SatisfiesPolicy);
        Assert.False(saved.Completeness.Values.ImagesComplete);
        var reloaded = await harness.GetRequiredDataAsync();
        Assert.Empty(reloaded.Completeness.Evaluation.MissingRequirements);
        Assert.True(reloaded.Completeness.Evaluation.PolicyVersion > 1);
    }

    [Fact]
    public async Task AnAcceptedCaseWithNoMileageIsNotReadyUntilItsMileageIsRecorded()
    {
        // R759: no mileage keeps a Case out of Review on every route, the
        // definitive instruction's included; the requirement names it, and a
        // saved mileage moves the Case on by itself.
        await using var harness = await CaseDataHarness.CreateAsync(mileage: null);
        var initial = await harness.GetRequiredDataAsync();
        Assert.Equal(CaseLifecycleState.NotReady, initial.State);
        Assert.False(initial.Completeness.Evaluation.SatisfiesPolicy);
        Assert.Equal(
            [CaseWorkflowConfiguration.VehicleMileageRequirement],
            initial.Completeness.Evaluation.MissingRequirements);

        var lease = await harness.AcquireLeaseAsync(initial.Version, harness.StaffActor, "edit-mileage-gate");
        var saved = await harness.WorkspaceStore.SaveAsync(
            new SaveCaseWorkspaceRequest(
                harness.CaseId,
                initial.Version,
                harness.StaffActor,
                "save-mileage-gate",
                null,
                lease.Token)
            {
                // The section saves whole: the vehicle as it stands, and the mileage.
                Vehicle = new(
                    initial.Vehicle.Registration.Current?.Value,
                    initial.Vehicle.Make.Current?.Value,
                    initial.Vehicle.Model.Current?.Value,
                    new(52_300, CaseOdometerUnit.Miles, null),
                    null,
                    initial.Vehicle.Year.Current?.Value)
            },
            default);

        Assert.Equal(CaseLifecycleState.Review, saved.Data.State);
        Assert.Empty((await harness.GetRequiredDataAsync()).Completeness.Evaluation.MissingRequirements);
    }

    [Fact]
    public async Task AnAcceptedCaseWithNoRegistrationStaysNotReadyWithItsMileageUntilARegistrationIsEntered()
    {
        // R1081: an instruction read without a registration creates its Case
        // Not ready even with its mileage, and the requirement names the
        // registration; a saved mileage alone does not move it, and the saved
        // registration moves it on by itself.
        await using var harness = await CaseDataHarness.CreateAsync(registered: false);
        var initial = await harness.GetRequiredDataAsync();
        Assert.Equal(CaseLifecycleState.NotReady, initial.State);
        Assert.Null(initial.Vehicle.Registration.Current);
        Assert.NotNull(initial.Vehicle.Mileage.Current);
        Assert.Equal(
            [CaseWorkflowConfiguration.VehicleRegistrationRequirement],
            initial.Completeness.Evaluation.MissingRequirements);

        var mileageLease = await harness.AcquireLeaseAsync(initial.Version, harness.StaffActor, "edit-registration-gate-mileage");
        var mileageSaved = await harness.WorkspaceStore.SaveAsync(
            new SaveCaseWorkspaceRequest(
                harness.CaseId,
                initial.Version,
                harness.StaffActor,
                "save-registration-gate-mileage",
                null,
                mileageLease.Token)
            {
                Vehicle = new(
                    null,
                    initial.Vehicle.Make.Current?.Value,
                    initial.Vehicle.Model.Current?.Value,
                    new(52_300, CaseOdometerUnit.Miles, null),
                    null,
                    initial.Vehicle.Year.Current?.Value)
            },
            default);

        Assert.Equal(CaseLifecycleState.NotReady, mileageSaved.Data.State);
        Assert.Equal(
            [CaseWorkflowConfiguration.VehicleRegistrationRequirement],
            (await harness.GetRequiredDataAsync()).Completeness.Evaluation.MissingRequirements);

        var registrationLease = await harness.AcquireLeaseAsync(
            mileageSaved.Version, harness.StaffActor, "edit-registration-gate-registration");
        var registrationSaved = await harness.WorkspaceStore.SaveAsync(
            new SaveCaseWorkspaceRequest(
                harness.CaseId,
                mileageSaved.Version,
                harness.StaffActor,
                "save-registration-gate-registration",
                null,
                registrationLease.Token)
            {
                Vehicle = new(
                    "AB12CDE",
                    initial.Vehicle.Make.Current?.Value,
                    initial.Vehicle.Model.Current?.Value,
                    new(52_300, CaseOdometerUnit.Miles, null),
                    null,
                    initial.Vehicle.Year.Current?.Value)
            },
            default);

        Assert.Equal(CaseLifecycleState.Review, registrationSaved.Data.State);
        Assert.Empty((await harness.GetRequiredDataAsync()).Completeness.Evaluation.MissingRequirements);
    }

    [Fact]
    public async Task ANotReadyCaseWhoseExtractedMileageStaffEmptiedStaysNotReadyAndNamesIt()
    {
        // R1077 with R759 and R1081: a mileage staff emptied is no mileage,
        // whatever the instruction said, so the Case waits for one as well as
        // for its registration.
        await using var harness = await CaseDataHarness.CreateAsync(registered: false);
        var initial = await harness.GetRequiredDataAsync();
        Assert.Equal(CaseLifecycleState.NotReady, initial.State);
        Assert.NotNull(initial.Vehicle.Mileage.Fact);

        var lease = await harness.AcquireLeaseAsync(initial.Version, harness.StaffActor, "edit-clear-mileage-gate");
        var saved = await harness.WorkspaceStore.SaveAsync(
            new SaveCaseWorkspaceRequest(
                harness.CaseId,
                initial.Version,
                harness.StaffActor,
                "save-clear-mileage-gate",
                null,
                lease.Token)
            {
                Vehicle = new(
                    null,
                    initial.Vehicle.Make.Current?.Value,
                    initial.Vehicle.Model.Current?.Value,
                    null,
                    null,
                    initial.Vehicle.Year.Current?.Value)
            },
            default);

        Assert.Equal(CaseLifecycleState.NotReady, saved.Data.State);
        Assert.True(saved.Data.Vehicle.Mileage.Cleared);
        var missing = (await harness.GetRequiredDataAsync()).Completeness.Evaluation.MissingRequirements;
        Assert.Contains(CaseWorkflowConfiguration.VehicleRegistrationRequirement, missing);
        Assert.Contains(CaseWorkflowConfiguration.VehicleMileageRequirement, missing);
    }

    [Fact]
    public async Task ACaseInReviewIsNeverSentBackForItsRegistration()
    {
        // The gate stands only on the way in (R1081, as R631): a Review Case
        // with no registration recorded stays in Review when it is saved.
        await using var harness = await CaseDataHarness.CreateAsync();
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            await context.CaseDataFields
                .Where(item => item.WorkId == harness.CaseId
                    && item.FieldName == CaseDataFieldNames.VehicleRegistration)
                .ExecuteDeleteAsync();
        }
        var initial = await harness.GetRequiredDataAsync();
        Assert.Equal(CaseLifecycleState.Review, initial.State);
        Assert.Null(initial.Vehicle.Registration.Current);
        Assert.Empty(initial.Completeness.Evaluation.MissingRequirements);

        var lease = await harness.AcquireLeaseAsync(initial.Version, harness.StaffActor, "edit-review-no-registration");
        var saved = await harness.WorkspaceStore.SaveAsync(
            new SaveCaseWorkspaceRequest(
                harness.CaseId,
                initial.Version,
                harness.StaffActor,
                "save-review-no-registration",
                null,
                lease.Token)
            {
                Completeness = new(true, true)
            },
            default);

        Assert.Equal(CaseLifecycleState.Review, saved.Data.State);
    }

    [Fact]
    public async Task ACaseInReviewIsNeverSentBackForItsMileage()
    {
        // The gate stands only on the way in (R759, as R631): a Review Case
        // whose mileage is not recorded stays in Review when it is saved.
        await using var harness = await CaseDataHarness.CreateAsync();
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            await context.CaseDataFields
                .Where(item => item.WorkId == harness.CaseId
                    && (item.FieldName == CaseDataFieldNames.VehicleMileage
                        || item.FieldName == CaseDataFieldNames.VehicleMileageUnit))
                .ExecuteDeleteAsync();
        }
        var initial = await harness.GetRequiredDataAsync();
        Assert.Equal(CaseLifecycleState.Review, initial.State);
        Assert.Null(initial.Vehicle.Mileage.Current);
        Assert.Empty(initial.Completeness.Evaluation.MissingRequirements);

        var lease = await harness.AcquireLeaseAsync(initial.Version, harness.StaffActor, "edit-review-no-mileage");
        var saved = await harness.WorkspaceStore.SaveAsync(
            new SaveCaseWorkspaceRequest(
                harness.CaseId,
                initial.Version,
                harness.StaffActor,
                "save-review-no-mileage",
                null,
                lease.Token)
            {
                Completeness = new(true, true)
            },
            default);

        Assert.Equal(CaseLifecycleState.Review, saved.Data.State);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IntakeFieldCandidatesRetainProvenanceAcrossReceiptPersistence(bool located)
    {
        // Structural tokens exercise the receipt's existing JSON mapping,
        // independently of any provider's extraction grammar.
        var candidate = new InstructionFieldCandidate(
            "selected value", IntakeEvidenceSource.DocumentContent, "selected-source",
            located ? IntakeSourceLocator.ForCell(1, 4, 2, page: 1, occurrence: 2) : null,
            located ? "  selected  value  " : null);
        var original = new InstructionReviewField(
            "Vehicle registration", candidate.Value, [candidate], false, false);

        var restored = Assert.Single(EfIntakeReceiptStore.DeserializeFields(
            EfIntakeReceiptStore.SerializeFields([original])));

        Assert.Equal(original.Name, restored.Name);
        Assert.Equal(original.SuggestedValue, restored.SuggestedValue);
        Assert.Equal(original.IsDefaulted, restored.IsDefaulted);
        Assert.Equal(original.HasConflict, restored.HasConflict);
        var restoredCandidate = Assert.Single(restored.Candidates);
        Assert.Equal(candidate, restoredCandidate);
        Assert.Equal(candidate.SourceValue, restoredCandidate.SourceValue);
    }

    [Theory]
    [InlineData("Claimant mobile telephone", "Claimant home telephone")]
    [InlineData("Claimant home telephone", "Claimant mobile telephone")]
    public void TypedPhoneSourceIgnoresConflictOnTheUnusedAlternative(string selectedName, string unusedName)
    {
        // Structural provenance tokens: the typed extractor's selected value
        // is supplied, not a second implementation of PCH's phone priority.
        var snapshot = PhoneSnapshot([
            new(selectedName, "selected", [new("selected", IntakeEvidenceSource.PdfContent, "selected-source")], false, false),
            new(unusedName, null,
                [new("alternative-a", IntakeEvidenceSource.PdfContent, "unused-a"),
                 new("alternative-b", IntakeEvidenceSource.PdfContent, "unused-b")], false, true)
        ]);
        var field = Assert.Single(snapshot.Fields);
        Assert.Equal(CaseDataFieldNames.ClaimantContactNumber, field.FieldName);
        Assert.Equal("selected", field.Value);
        Assert.Equal("PdfContent:selected-source", field.SourceLabel);
        Assert.Equal(CaseDataCodes.IntakeEvidence, field.SourceKind);
    }

    [Fact]
    public void TypedPhoneSourceStillRejectsSelectedConflict()
    {
        Assert.Throws<InvalidDataException>(() => PhoneSnapshot([
            new("Claimant mobile telephone", "selected",
                [new("selected", IntakeEvidenceSource.PdfContent, "conflict")], false, true)
        ]));
    }

    [Fact]
    public void TypedPhoneSourceUsesTheFirstEqualSourceBinding()
    {
        var snapshot = PhoneSnapshot([
            new("Claimant mobile telephone", "selected",
                [new("selected", IntakeEvidenceSource.PdfContent, "mobile")], false, false),
            new("Claimant home telephone", "selected",
                [new("selected", IntakeEvidenceSource.PdfContent, "home")], false, false)
        ]);

        var field = Assert.Single(snapshot.Fields);
        Assert.Equal(CaseDataFieldNames.ClaimantContactNumber, field.FieldName);
        Assert.Equal("selected", field.Value);
        Assert.Equal("PdfContent:mobile", field.SourceLabel);
        Assert.Equal(CaseDataCodes.IntakeEvidence, field.SourceKind);
    }

    [Fact]
    public void ExtractedRepairerNameAndAddressBecomeCaseFactsWithTheirOwnProvenance()
    {
        // The instruction's repairer reaches the Case as ordinary
        // source facts. A directory link is a separate staff decision and is
        // not inferred from the text here.
        var snapshot = PhoneSnapshot([
            new("Claimant mobile telephone", "selected",
                [new("selected", IntakeEvidenceSource.PdfContent, "selected-source")], false, false),
            new("Repairer name", "Kingsway Accident Repair",
                [new("Kingsway Accident Repair", IntakeEvidenceSource.PdfContent, "repairer-block")], false, false),
            new("Repairer address", "12 Kingsway, Leeds LS1 1AA",
                [new("12 Kingsway, Leeds LS1 1AA", IntakeEvidenceSource.PdfContent, "repairer-block")], false, false)
        ]);

        var name = Assert.Single(snapshot.Fields, field => field.FieldName == CaseDataFieldNames.RepairerName);
        Assert.Equal(CaseDataCodes.Fact, name.ValueKind);
        Assert.Equal("Kingsway Accident Repair", name.Value);
        Assert.Equal(CaseDataCodes.IntakeEvidence, name.SourceKind);
        Assert.Equal("PdfContent:repairer-block", name.SourceLabel);
        var address = Assert.Single(snapshot.Fields, field => field.FieldName == CaseDataFieldNames.RepairerAddress);
        Assert.Equal(CaseDataCodes.Fact, address.ValueKind);
        Assert.Equal("12 Kingsway, Leeds LS1 1AA", address.Value);
        Assert.DoesNotContain(snapshot.Fields, field => field.FieldName is
            CaseDataFieldNames.RepairerId or CaseDataFieldNames.RepairerVersion);
    }

    [Fact]
    public void ConflictedRepairerTextIsLeftOutRatherThanRecorded()
    {
        var snapshot = PhoneSnapshot([
            new("Claimant mobile telephone", "selected",
                [new("selected", IntakeEvidenceSource.PdfContent, "selected-source")], false, false),
            new("Repairer name", null,
                [new("Kingsway Accident Repair", IntakeEvidenceSource.PdfContent, "repairer-block"),
                 new("Kingsway Bodyshop", IntakeEvidenceSource.PdfContent, "footer")], false, true)
        ]);

        Assert.DoesNotContain(snapshot.Fields, field => field.FieldName == CaseDataFieldNames.RepairerName);
    }

    [Fact]
    public void TheClaimSourceAnInstructionNamedIsCopiedOntoTheCaseAsAStaffChoiceIs()
    {
        // R1014, R1017: the record's identity, version, name and contact, as
        // confirmed rows a later edit replaces or clears, sourced to the
        // instruction line that named it.
        var field = new InstructionReviewField(
            EstablishedIntakeClaimSource.IntroducerField, "FLEET ROUTE SOLUTIONS",
            [new("FLEET ROUTE SOLUTIONS", IntakeEvidenceSource.PdfContent, "introducer-line")], false, false);
        var contact = new ContactChoice(
            Guid.NewGuid(), "Fleet Route Solutions", ContactRole.ClaimSource, 4,
            "desk@fleet-route.example", "0113 000 0002", null, null, null);

        var snapshot = PhoneSnapshot(
            [
                new("Claimant mobile telephone", "selected",
                    [new("selected", IntakeEvidenceSource.PdfContent, "selected-source")], false, false),
                field
            ],
            new(contact, IntakeClaimSourceOrigin.Instruction, field));

        string Confirmed(string fieldName)
        {
            var row = Assert.Single(snapshot.Fields, item => item.FieldName == fieldName);
            Assert.Equal(CaseDataCodes.Confirmed, row.ValueKind);
            Assert.Equal(CaseDataCodes.IntakeEvidence, row.SourceKind);
            Assert.Equal("PdfContent:introducer-line", row.SourceLabel);
            Assert.Equal(PchInstructionExtractionPolicy.Key, row.PolicyKey);
            Assert.Equal("system-worker:intake-processing", row.ConfirmedByActor);
            return row.Value;
        }

        Assert.Equal(contact.OrganizationId.ToString("D"), Confirmed(CaseDataFieldNames.ClaimSourceId));
        Assert.Equal("4", Confirmed(CaseDataFieldNames.ClaimSourceVersion));
        Assert.Equal("Fleet Route Solutions", Confirmed(CaseDataFieldNames.ClaimSourceName));
        // A Claim Source record holds no contact name to copy (R1047).
        Assert.DoesNotContain(snapshot.Fields, item => item.FieldName == CaseDataFieldNames.ClaimSourceContactName);
        Assert.Equal("0113 000 0002", Confirmed(CaseDataFieldNames.ClaimSourceContactTelephone));
        Assert.Equal("desk@fleet-route.example", Confirmed(CaseDataFieldNames.ClaimSourceContactEmailAddress));
    }

    [Fact]
    public void TheClaimSourceASenderProvedIsSourcedToItsMailRoute()
    {
        var contact = new ContactChoice(
            Guid.NewGuid(), "Claims Route", ContactRole.ClaimSource, 1,
            "desk@claims-route.example", null, null, null, null);

        var snapshot = PhoneSnapshot(
            [
                new("Claimant mobile telephone", "selected",
                    [new("selected", IntakeEvidenceSource.PdfContent, "selected-source")], false, false)
            ],
            new(contact, IntakeClaimSourceOrigin.Sender),
            new()
            {
                Disposition = "accepted", RouteOwnerCode = "claims-route.example", RouteKind = "intermediary",
                PrincipalCode = "PCH", PredicatesJson = "{\"version\":1,\"data\":[]}", Reason = "probe",
                PolicyKey = "principal_mail_route", PolicyVersion = 1,
                TransportIdentitiesJson = "{\"version\":1,\"data\":[]}",
                OriginalIdentitiesJson = "{\"version\":1,\"data\":[]}",
                EffectiveSenderAddress = "reports@claims-route.example", EffectiveSenderSourceLabel = "probe"
            });

        var name = Assert.Single(snapshot.Fields, item => item.FieldName == CaseDataFieldNames.ClaimSourceName);
        Assert.Equal("Claims Route", name.Value);
        Assert.Equal(CaseDataCodes.Confirmed, name.ValueKind);
        Assert.Equal(CaseDataCodes.MailRoute, name.SourceKind);
        Assert.Equal("claims-route.example", name.SourceLabel);
        Assert.Equal("principal_mail_route", name.PolicyKey);
        // A contact the record does not hold is not written as an empty fact.
        Assert.DoesNotContain(snapshot.Fields, item => item.FieldName is
            CaseDataFieldNames.ClaimSourceContactName or CaseDataFieldNames.ClaimSourceContactTelephone);
    }

    [Fact]
    public void AnItemThatEstablishesNoClaimSourceLeavesTheCaseWithNone()
    {
        var snapshot = PhoneSnapshot([
            new("Claimant mobile telephone", "selected",
                [new("selected", IntakeEvidenceSource.PdfContent, "selected-source")], false, false)
        ]);

        Assert.DoesNotContain(snapshot.Fields, item => item.FieldName == CaseDataFieldNames.ClaimSourceId);
    }

    [Fact]
    public void TheRepairerAnInstructionNamedIsLinkedOnTheCaseAsAStaffChoiceIs()
    {
        // R1027: the record's identity, version and name, as confirmed rows a
        // later edit replaces or clears, sourced to the instruction line that
        // named it. The address stays the one the instruction gave.
        var name = new InstructionReviewField(
            "Repairer name", "HARBOUR BODY REPAIRS",
            [new("HARBOUR BODY REPAIRS", IntakeEvidenceSource.PdfContent, "bodyshop-name-line")], false, false);
        var address = new InstructionReviewField(
            "Repairer address", "1 Quay Road, Leeds, LS1 1AA",
            [new("1 Quay Road, Leeds, LS1 1AA", IntakeEvidenceSource.PdfContent, "bodyshop-address-line")], false, false);
        var contact = new ContactChoice(
            Guid.NewGuid(), "Harbour Body Repairs", ContactRole.Repairer, 3,
            null, null, "9 Directory Street", null, null);

        var snapshot = PhoneSnapshot(
            [
                new("Claimant mobile telephone", "selected",
                    [new("selected", IntakeEvidenceSource.PdfContent, "selected-source")], false, false),
                name,
                address
            ],
            repairer: new(contact, name));

        string Confirmed(string fieldName)
        {
            var row = Assert.Single(
                snapshot.Fields,
                item => item.FieldName == fieldName && item.ValueKind == CaseDataCodes.Confirmed);
            Assert.Equal(CaseDataCodes.IntakeEvidence, row.SourceKind);
            Assert.Equal("PdfContent:bodyshop-name-line", row.SourceLabel);
            Assert.Equal(PchInstructionExtractionPolicy.Key, row.PolicyKey);
            Assert.Equal("system-worker:intake-processing", row.ConfirmedByActor);
            return row.Value;
        }

        Assert.Equal(contact.OrganizationId.ToString("D"), Confirmed(CaseDataFieldNames.RepairerId));
        Assert.Equal("3", Confirmed(CaseDataFieldNames.RepairerVersion));
        Assert.Equal("Harbour Body Repairs", Confirmed(CaseDataFieldNames.RepairerName));
        // The instruction's own address is the Case's; the directory's is not copied over it.
        var kept = Assert.Single(snapshot.Fields, item => item.FieldName == CaseDataFieldNames.RepairerAddress);
        Assert.Equal("1 Quay Road, Leeds, LS1 1AA", kept.Value);
        Assert.NotEqual(CaseDataCodes.Confirmed, kept.ValueKind);
    }

    [Fact]
    public void AnItemThatEstablishesNoRepairerLeavesTheCaseUnlinked()
    {
        var snapshot = PhoneSnapshot([
            new("Claimant mobile telephone", "selected",
                [new("selected", IntakeEvidenceSource.PdfContent, "selected-source")], false, false),
            new("Repairer name", "A garage the directory does not hold",
                [new("A garage the directory does not hold", IntakeEvidenceSource.PdfContent, "bodyshop-name-line")], false, false)
        ]);

        Assert.DoesNotContain(snapshot.Fields, item => item.FieldName == CaseDataFieldNames.RepairerId);
        Assert.DoesNotContain(
            snapshot.Fields,
            item => item.FieldName == CaseDataFieldNames.RepairerName && item.ValueKind == CaseDataCodes.Confirmed);
    }

    /// <summary>
    /// R1053: a repairer and a claim source a Principal stated over the
    /// Principal API are linked the way an instruction's are, recorded with
    /// the Principal API provenance every other declared value has.
    /// </summary>
    [Fact]
    public void ADeclaredRepairerAndClaimSourceAreRecordedAsPrincipalApiValues()
    {
        var name = new InstructionReviewField(
            "Repairer name", "Harbour Body Repairs",
            [new("Harbour Body Repairs", IntakeEvidenceSource.PrincipalDeclaration, PrincipalInstructionPolicy.PolicyKey)],
            false, false);
        var address = new InstructionReviewField(
            "Repairer address", "1 Quay Road, Leeds",
            [new("1 Quay Road, Leeds", IntakeEvidenceSource.PrincipalDeclaration, PrincipalInstructionPolicy.PolicyKey)],
            false, false);
        var claimSourceField = new InstructionReviewField(
            EstablishedIntakeClaimSource.ClaimSourceField, "Fleet Route Solutions",
            [new("Fleet Route Solutions", IntakeEvidenceSource.PrincipalDeclaration, PrincipalInstructionPolicy.PolicyKey)],
            false, false);
        var repairer = new ContactChoice(
            Guid.NewGuid(), "Harbour Body Repairs", ContactRole.Repairer, 3, null, null, null, null, null);
        var claimSource = new ContactChoice(
            Guid.NewGuid(), "Fleet Route Solutions", ContactRole.ClaimSource, 4, null, null, null, null, null);

        var snapshot = PhoneSnapshot(
            [
                new("Claimant mobile telephone", "selected",
                    [new("selected", IntakeEvidenceSource.PdfContent, "selected-source")], false, false),
                name,
                address,
                claimSourceField
            ],
            new(claimSource, IntakeClaimSourceOrigin.Instruction, claimSourceField),
            repairer: new(repairer, name));

        Assert.All(
            snapshot.Fields.Where(item => item.FieldName is CaseDataFieldNames.RepairerId
                or CaseDataFieldNames.RepairerName or CaseDataFieldNames.RepairerAddress
                or CaseDataFieldNames.ClaimSourceId or CaseDataFieldNames.ClaimSourceName),
            item => Assert.Equal(CaseDataCodes.PrincipalApi, item.SourceKind));
        Assert.Contains(snapshot.Fields, item => item.FieldName == CaseDataFieldNames.RepairerId
            && item.Value == repairer.OrganizationId.ToString("D"));
        Assert.Contains(snapshot.Fields, item => item.FieldName == CaseDataFieldNames.ClaimSourceId
            && item.Value == claimSource.OrganizationId.ToString("D"));
        Assert.Contains(snapshot.Fields, item => item.FieldName == CaseDataFieldNames.RepairerAddress
            && item.Value == "1 Quay Road, Leeds");
    }

    private static CaseDataSnapshotEntity PhoneSnapshot(
        IReadOnlyList<InstructionReviewField> fields,
        EstablishedClaimSource? claimSource = null,
        IntakeMailRouteDecisionEntity? route = null,
        EstablishedRepairer? repairer = null)
    {
        var receiptId = Guid.NewGuid();
        var receipt = new IntakeReceiptEntity
        {
            Id = receiptId, SourceFileName = "provenance-probe", MediaType = "application/pdf",
            SourceHash = "source-hash", SourceChannel = "manual_upload", ExternalReceiptToken = "provenance-probe",
            SourceReaderKey = "structural-probe", SourceReaderVersion = "1",
            ExtractionPolicyKey = PchInstructionExtractionPolicy.Key,
            ExtractionPolicyVersion = PchInstructionExtractionPolicy.Version,
            Decision = "case_created", DecisionReason = "provenance-probe",
            EvidenceJson = "{\"version\":1,\"data\":[]}", OcrCandidatesJson = "{\"version\":1,\"data\":[]}",
            FieldsJson = EfIntakeReceiptStore.SerializeFields(fields),
            InstructionDraft = new() { SuggestedPrincipalCode = "PCH", ClaimantContactNumber = "selected" },
            MailRouteDecision = route
        };
        var accepted = new CaseEntity
        {
            Id = Guid.NewGuid(), OriginIntakeReceiptId = receiptId, Reference = "provenance-probe",
            Type = "inspection", InitialState = "not_ready", CustodyState = "pending"
        };
        return CaseDataSnapshotFactory.Create(accepted, receipt,
            new(receiptId, 1, ActionActor.SystemWorker("system-worker:intake-processing"),
                "provenance-probe", CaseType.Inspection, "PCH",
                new(true, false), new(false, "completeness-probe", 1),
                new Pegasus.Core.Workflow.CaseWorkflowConfiguration("completeness-probe", 1), CaseInspectionMode.PhysicalAddress),
            DateTimeOffset.UtcNow,
            claimSource,
            repairer);
    }

    /// <summary>
    /// R1264: an instruction whose attachment text names one active Repairer
    /// linked to the Case's Principal creates its Case holding that Repairer,
    /// sourced to the instruction's text. An inactive linked Repairer and an
    /// unlinked one named in the same text are not counted.
    /// </summary>
    [Fact]
    public async Task AnAttachmentNamingALinkedRepairerCreatesTheCaseHoldingIt()
    {
        var linkedId = Guid.NewGuid();
        await using var harness = await CaseDataHarness.CreateAsync(
            beforeAcceptance: async (seed, receiptId) =>
            {
                var qdos = await SeededPrincipals.QdosAsync(seed);
                var inactiveId = Guid.NewGuid();
                var unlinkedId = Guid.NewGuid();
                seed.Organizations.AddRange(
                    new OrganizationEntity
                    {
                        Id = linkedId, Name = "Text Fill Fixture Quayside Coachworks", Role = "repairer", Active = true, Version = 0
                    },
                    new OrganizationEntity
                    {
                        Id = inactiveId, Name = "Text Fill Fixture Moorland Garage", Role = "repairer", Active = false, Version = 0
                    },
                    new OrganizationEntity
                    {
                        Id = unlinkedId, Name = "Text Fill Fixture Harbour Motors", Role = "repairer", Active = true, Version = 0
                    });
                seed.Add(new ContactPrincipalLinkEntity { PrincipalId = qdos.Id, OrganizationId = linkedId });
                seed.Add(new ContactPrincipalLinkEntity { PrincipalId = qdos.Id, OrganizationId = inactiveId });
                seed.IntakeSearchDocuments.AddRange(
                    new IntakeSearchDocumentEntity
                    {
                        Id = Guid.NewGuid(), IntakeReceiptId = receiptId, Ordinal = 0,
                        SourceLabel = "message body", Text = "Please inspect the attached instruction."
                    },
                    new IntakeSearchDocumentEntity
                    {
                        Id = Guid.NewGuid(), IntakeReceiptId = receiptId, Ordinal = 1, AttachmentOrdinal = 0,
                        SourceLabel = "attachment 1", AttachmentFileName = "instructions.pdf",
                        Text = "The vehicle is at TEXT FILL FIXTURE QUAYSIDECOACHWORKS. It was first taken to "
                            + "Text Fill Fixture Moorland Garage and Text Fill Fixture Harbour Motors."
                    });
                await seed.SaveChangesAsync();
            });

        await using var read = await harness.Factory.CreateDbContextAsync();
        var repairerId = await read.CaseDataFields.AsNoTracking()
            .SingleAsync(item => item.WorkId == harness.CaseId
                && item.FieldName == CaseDataFieldNames.RepairerId
                && item.ValueKind == CaseDataCodes.Confirmed);
        Assert.Equal(linkedId.ToString("D"), repairerId.Value);
        Assert.Equal(CaseDataCodes.IntakeEvidence, repairerId.SourceKind);
        Assert.Equal("instruction text", repairerId.SourceLabel);
        Assert.Equal(NamedInText.PolicyKey, repairerId.PolicyKey);
        Assert.Equal(NamedInText.PolicyVersion, repairerId.PolicyVersion);
        var repairerName = await read.CaseDataFields.AsNoTracking()
            .SingleAsync(item => item.WorkId == harness.CaseId
                && item.FieldName == CaseDataFieldNames.RepairerName
                && item.ValueKind == CaseDataCodes.Confirmed);
        Assert.Equal("Text Fill Fixture Quayside Coachworks", repairerName.Value);
    }

    [Fact]
    public async Task AcceptanceSnapshotsTypedSourceProvenanceWithAutoAddedValues()
    {
        await using var harness = await CaseDataHarness.CreateAsync();

        var projection = await harness.DataStore.GetAsync(
            harness.CaseId,
            CaseWorkSelector.Current,
            CancellationToken.None);

        Assert.NotNull(projection);
        Assert.Equal(CaseLifecycleState.Review, projection.State);
        Assert.True(projection.Completeness.Evaluation.SatisfiesPolicy);
        Assert.Equal(harness.ReceiptId, projection.Origin.IntakeReceiptId);
        Assert.Equal("mailbox-item-immutable-1", projection.Origin.ExternalReceiptToken);
        Assert.Equal(harness.SourceHash, projection.Origin.SourceHash);
        Assert.Equal("QDOS", projection.Principal.PrincipalCode.Fact?.Value);
        Assert.True(projection.Principal.PrincipalCode.Fact?.IsAccepted);
        Assert.Equal(
            CaseDataSourceKind.MailRoute,
            projection.Principal.PrincipalCode.Fact?.Source.Kind);
        Assert.Equal(
            "qdos_mail_route",
            projection.Principal.PrincipalCode.Fact?.Source.PolicyKey);
        Assert.Equal(2, projection.Principal.PrincipalCode.Fact?.Source.PolicyVersion);
        // An unambiguous extracted value is auto-added (Fact),
        // not parked as a suggestion awaiting confirmation.
        Assert.Null(projection.Claimant.Name.Suggestion);
        Assert.Equal("Jane Example", projection.Claimant.Name.Fact?.Value);
        Assert.Null(projection.Claimant.Name.Confirmed);
        Assert.Equal("qdos_instruction", projection.Claimant.Name.Fact?.Source.PolicyKey);
        Assert.Contains("instructions.pdf", projection.Claimant.Name.Fact?.Source.Label);
        Assert.Equal("1 Test Street, London", projection.Inspection.Address.Fact?.Value);
        Assert.Equal(
            CaseDataSourceKind.IntakeEvidence,
            projection.Inspection.Address.Fact?.Source.Kind);
        Assert.Equal("qdos_instruction", projection.Inspection.Address.Fact?.Source.PolicyKey);
        Assert.Equal(
            Ext18InspectionAddressPolicy.ImageBasedAssessment,
            projection.Inspection.Address.Confirmed?.Value);
        Assert.Equal(
            harness.StaffActor.SubjectId,
            projection.Inspection.Address.Confirmed?.ConfirmedByActor);
        Assert.Equal(
            CaseDataSourceKind.PrincipalSetting,
            projection.Inspection.Address.Confirmed?.Source.Kind);
        Assert.Equal(
            PrincipalInspectionModePolicy.PolicyKey,
            projection.Inspection.Address.Confirmed?.Source.PolicyKey);
        Assert.Equal(
            CaseInspectionMode.ImageBasedAssessment,
            projection.Inspection.Mode.Confirmed?.Value);
        Assert.Null(projection.Instruction.VatStatus.Current);
        Assert.Equal(new DateOnly(2031, 5, 6), projection.Instruction.ReceivedDate);

        var currentAddress = await harness.AddressStore.GetAsync(
            harness.ReceiptId,
            CancellationToken.None);
        Assert.NotNull(currentAddress?.Evaluation.Suggestion);
        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.AddressStore.ResolveAsync(
            new(
                harness.ReceiptId,
                currentAddress!.ReceiptVersion,
                currentAddress.Evaluation.Suggestion!.Fingerprint,
                InspectionAddressStaffDecision.AcceptSuggestion,
                null,
                harness.StaffActor,
                Guid.NewGuid(),
                "address-after-acceptance"),
            CancellationToken.None));
    }

    [Fact]
    public async Task CorrectedInspectionAddressRetainsExtractedValueAndRecordsStaffCorrectionSource()
    {
        await using var harness = await CaseDataHarness.CreateAsync(
            InspectionAddressStaffDecision.CorrectSuggestion,
            "2 Corrected Street, London");

        var projection = await harness.GetRequiredDataAsync();

        Assert.Equal("1 Test Street, London", projection.Inspection.Address.Fact?.Value);
        Assert.Equal(
            CaseDataSourceKind.IntakeEvidence,
            projection.Inspection.Address.Fact?.Source.Kind);
        Assert.Equal(
            "qdos_instruction",
            projection.Inspection.Address.Fact?.Source.PolicyKey);
        Assert.Equal(
            Ext18InspectionAddressPolicy.ImageBasedAssessment,
            projection.Inspection.Address.Confirmed?.Value);
        Assert.Equal(
            CaseDataSourceKind.PrincipalSetting,
            projection.Inspection.Address.Confirmed?.Source.Kind);
        Assert.Equal(
            PrincipalInspectionModePolicy.PolicyKey,
            projection.Inspection.Address.Confirmed?.Source.PolicyKey);
        Assert.Equal(
            CaseInspectionMode.ImageBasedAssessment,
            projection.Inspection.Mode.Confirmed?.Value);

        var retainedAddress = await harness.AddressStore.GetAsync(
            harness.ReceiptId,
            CancellationToken.None);
        Assert.NotNull(retainedAddress);
        Assert.Equal(InspectionAddressResolutionState.Corrected, retainedAddress.State);
        Assert.Equal("2 Corrected Street, London", retainedAddress.ResolvedValue);
        Assert.Equal(ActorKind.Staff, retainedAddress.ResolvedByKind);
        Assert.Equal(harness.StaffActor.SubjectId, retainedAddress.ResolvedBy);
        Assert.NotNull(retainedAddress.ResolvedAtUtc);
        var extractedAddress = Assert.Single(retainedAddress.Evaluation.Suggestion!.Provenance);
        Assert.Equal("qdos_instruction", extractedAddress.PolicyKey);
        Assert.Equal(1, extractedAddress.PolicyVersion);
    }

    /// <summary>
    /// Internal, not private, so the Case workspace persistence tests build
    /// their fixture from this one accepted-case recipe instead of a second
    /// copy of it.
    /// </summary>
    internal sealed class CaseDataHarness : IAsyncDisposable
    {
        private static readonly DateTimeOffset StartUtc =
            new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
        private readonly LocalDbTestDatabase database;
        private readonly CaseWorkspaceDatabase? shared;
        private readonly PooledDbContextFactory<PegasusDbContext> factory;
        private readonly CaseEditLeases leases;

        private CaseDataHarness(
            LocalDbTestDatabase database,
            CaseWorkspaceDatabase? shared,
            PooledDbContextFactory<PegasusDbContext> factory,
            MutableTimeProvider timeProvider,
            Guid receiptId,
            Guid caseId,
            string sourceHash,
            ActionActor staffActor,
            InspectionAddressResolutionStore addressStore,
            EfCaseDataStore dataStore,
            CaseEditLeases leases,
            EfCaseWorkflowStore workflowStore,
            EfCaseWorkspaceStore workspaceStore)
        {
            Factory = factory;
            WorkflowStore = workflowStore;
            WorkspaceStore = workspaceStore;
            this.database = database;
            this.shared = shared;
            this.factory = factory;
            TimeProvider = timeProvider;
            ReceiptId = receiptId;
            CaseId = caseId;
            SourceHash = sourceHash;
            StaffActor = staffActor;
            AddressStore = addressStore;
            DataStore = dataStore;
            this.leases = leases;
        }

        public IDbContextFactory<PegasusDbContext> Factory { get; }
        public EfCaseWorkflowStore WorkflowStore { get; }
        public EfCaseWorkspaceStore WorkspaceStore { get; }
        public MutableTimeProvider TimeProvider { get; }
        public Guid ReceiptId { get; }
        public Guid CaseId { get; }
        public string SourceHash { get; }
        public ActionActor StaffActor { get; }
        public InspectionAddressResolutionStore AddressStore { get; }
        public EfCaseDataStore DataStore { get; }

        /// <param name="mileage">
        /// The instruction's mileage; without one the Case is created Not
        /// ready (R759).
        /// </param>
        /// <param name="registered">
        /// Whether the instruction states the registration; without one the
        /// Case is created Not ready (R1081).
        /// </param>
        public static async Task<CaseDataHarness> CreateAsync(
            InspectionAddressStaffDecision addressDecision =
                InspectionAddressStaffDecision.AcceptSuggestion,
            string? correctedAddress = null,
            long? mileage = FixtureMileage,
            bool registered = true,
            Func<PegasusDbContext, Guid, Task>? beforeAcceptance = null)
        {
            var database = await LocalDbTestDatabase.CreateAsync();
            var factory = new PooledDbContextFactory<PegasusDbContext>(
                new DbContextOptionsBuilder<PegasusDbContext>()
                    .UseSqlServer(database.ConnectionString)
                    .Options);
            return await OpenAsync(
                database, shared: null, factory, addressDecision, correctedAddress, mileage, registered, beforeAcceptance);
        }

        /// <summary>The instruction's mileage in the fixture.</summary>
        public const long FixtureMileage = 41_200;

        /// <summary>
        /// The accepted Case in the class's database, removed with the intake it
        /// came from when the harness is disposed.
        /// </summary>
        public static Task<CaseDataHarness> CreateAsync(CaseWorkspaceDatabase shared, long? mileage = FixtureMileage) =>
            OpenAsync(
                shared.Database,
                shared,
                shared.Factory,
                InspectionAddressStaffDecision.AcceptSuggestion,
                correctedAddress: null,
                mileage,
                registered: true,
                beforeAcceptance: null);

        private static async Task<CaseDataHarness> OpenAsync(
            LocalDbTestDatabase database,
            CaseWorkspaceDatabase? shared,
            PooledDbContextFactory<PegasusDbContext> factory,
            InspectionAddressStaffDecision addressDecision,
            string? correctedAddress,
            long? mileage,
            bool registered,
            Func<PegasusDbContext, Guid, Task>? beforeAcceptance)
        {
            var receiptId = Guid.NewGuid();
            try
            {
                var timeProvider = new MutableTimeProvider(StartUtc);
                var staffId = Guid.NewGuid();
                var staffActor = ActionActor.Staff(staffId, [StaffRole.User]);
                var sourceHash = new string('a', 64);
                await SeedAsync(factory, receiptId, sourceHash, mileage, registered);
                if (beforeAcceptance is not null)
                {
                    await using var seedContext = await factory.CreateDbContextAsync();
                    await beforeAcceptance(seedContext, receiptId);
                }

                var addressStore = new InspectionAddressResolutionStore(factory, timeProvider);
                var address = await addressStore.GetAsync(receiptId, CancellationToken.None);
                var suggestion = address?.Evaluation.Suggestion
                    ?? throw new InvalidOperationException("The address fixture did not produce a suggestion.");
                var resolved = await addressStore.ResolveAsync(
                    new(
                        receiptId,
                        address!.ReceiptVersion,
                        suggestion.Fingerprint,
                        addressDecision,
                        correctedAddress,
                        staffActor,
                        Guid.NewGuid(),
                        "address-before-acceptance"),
                    CancellationToken.None);

                var configuration = new FixedConfiguration();
                var acceptanceStore = new EfCaseAcceptanceStore(factory, timeProvider);
                var accept = new AcceptIntake(
                    acceptanceStore,
                    configuration,
                    new EfPrincipalInspectionModeStore(factory),
                    new DiscardingCommittedWorkPublisher(),
                    new TriageCasePairing(new EfTriageStore(factory,
                        [new PrincipalCaseMatchPolicy(new QdosInstructionExtractionPolicy())], timeProvider)));
                var outcome = await accept.ExecuteAsync(
                    new(
                        receiptId,
                        resolved.ReceiptVersion,
                        staffActor,
                        "accept-case-data-fixture",
                        CaseType.Inspection,
                        "QDOS",
                        new(
                            true,
                            true),
                        AcceptedInspectionDeadline: new DateOnly(2031, 5, 20)),
                    CancellationToken.None);
                await using (var divergenceContext = await factory.CreateDbContextAsync())
                {
                    await divergenceContext.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE Cases SET Version = {41L} WHERE Id = {outcome.Identity.CaseId}");
                }


                var workflowStore = new EfCaseWorkflowStore(factory, timeProvider);
                var dataStore = new EfCaseDataStore(factory);
                return new(
                    database,
                    shared,
                    factory,
                    timeProvider,
                    receiptId,
                    outcome.Identity.CaseId,
                    sourceHash,
                    staffActor,
                    addressStore,
                    dataStore,
                    new CaseEditLeases(workflowStore, new InProcessCaseEditSignal()),
                    workflowStore,
                    new EfCaseWorkspaceStore(factory, timeProvider));
            }
            catch
            {
                if (shared is null)
                {
                    await database.DisposeAsync();
                }
                else
                {
                    await shared.RemoveAsync(receiptId);
                }

                throw;
            }
        }

        public Task<CaseEditLease> AcquireLeaseAsync(
            long version,
            ActionActor actor,
            string operationKey) => leases.ClaimAsync(
            new(CaseId, version, actor, operationKey),
            CancellationToken.None);

        public async Task<CaseDataProjection> GetRequiredDataAsync() =>
            await DataStore.GetAsync(CaseId, CaseWorkSelector.Current, CancellationToken.None)
            ?? throw new InvalidOperationException("The case-data fixture was not persisted.");
        public async Task<long> HiddenCaseVersionAsync()
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context.Database.SqlQuery<long>(
                    $"SELECT [Version] AS [Value] FROM [Cases] WHERE [Id] = {CaseId}")
                .SingleAsync();
        }


        public async Task<long> HistoryCountAsync()
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context.Database.SqlQuery<long>(
                    $"SELECT COUNT_BIG(*) AS [Value] FROM [ActionHistory] WHERE [AggregateType] = {"case"} AND [AggregateId] = {CaseId.ToString("D")}")
                .SingleAsync();
        }

        public async ValueTask DisposeAsync()
        {
            if (shared is null)
            {
                await database.DisposeAsync();
            }
            else
            {
                await shared.RemoveAsync(ReceiptId);
            }
        }

        private static async Task SeedAsync(
            IDbContextFactory<PegasusDbContext> factory,
            Guid receiptId,
            string sourceHash,
            long? mileage,
            bool registered)
        {
            await using var context = await factory.CreateDbContextAsync();
            var principal = await SeededPrincipals.QdosAsync(context);
            var organizationId = principal.OrganizationId;
            var lineageId = principal.SequenceLineageId;
            var principalId = principal.Id;
            var fieldsJson =
                """{"version":1,"data":[{"name":"Claimant name","suggestedValue":"Jane Example","candidates":[{"value":"Jane Example","source":"pdf_content","sourceLabel":"instructions.pdf"}],"isDefaulted":false,"hasConflict":false},{"name":"Claim number","suggestedValue":"QDOS-123","candidates":[{"value":"QDOS-123","source":"pdf_content","sourceLabel":"instructions.pdf"}],"isDefaulted":false,"hasConflict":false},{"name":"Vehicle registration","suggestedValue":"AB12 CDE","candidates":[{"value":"AB12 CDE","source":"pdf_content","sourceLabel":"instructions.pdf"}],"isDefaulted":false,"hasConflict":false},{"name":"Inspection address","suggestedValue":"1 Test Street, London","candidates":[{"value":"1 Test Street, London","source":"pdf_content","sourceLabel":"instructions.pdf"}],"isDefaulted":false,"hasConflict":false},{"name":"Inspection date","suggestedValue":"2031-05-20","candidates":[{"value":"2031-05-20","source":"pdf_content","sourceLabel":"instructions.pdf"}],"isDefaulted":false,"hasConflict":false}]}""";
            // The draft's mileage carries its review field, as an extracted
            // value must, with the unit it is saved beside (R759: without a
            // mileage the Case is created Not ready).
            if (mileage is { } miles)
            {
                fieldsJson = fieldsJson[..^2]
                    + $$""",{"name":"Vehicle mileage","suggestedValue":"{{miles}} miles","candidates":[{"value":"{{miles}} miles","source":"pdf_content","sourceLabel":"instructions.pdf"}],"isDefaulted":false,"hasConflict":false}]}""";
            }
            // An instruction read without a registration (R981) is accepted
            // without its review field and its draft value (R1081).
            if (!registered)
            {
                fieldsJson = fieldsJson.Replace(
                    """{"name":"Vehicle registration","suggestedValue":"AB12 CDE","candidates":[{"value":"AB12 CDE","source":"pdf_content","sourceLabel":"instructions.pdf"}],"isDefaulted":false,"hasConflict":false},""",
                    string.Empty,
                    StringComparison.Ordinal);
            }
            var emptyEnvelope = """{"version":1,"data":[]}""";

            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO IntakeReceipts (Id, SourceFileName, MediaType, SourceLength, SourceHash, SourceChannel, ExternalReceiptToken, ReceivedAtUtc, ProcessedAtUtc, SourceReaderKey, SourceReaderVersion, ExtractionPolicyKey, ExtractionPolicyVersion, Version, Decision, DecisionReason, EvidenceJson, FieldsJson, OcrCandidatesJson) VALUES ({receiptId}, {"qdos.eml"}, {"message/rfc822"}, {100L}, {sourceHash}, {"mailbox"}, {"mailbox-item-immutable-1"}, {StartUtc}, {StartUtc}, {"fixture-reader"}, {"1"}, {"qdos_instruction"}, {1}, {0L}, {"case_created"}, {"Ready fixture"}, {emptyEnvelope}, {fieldsJson}, {emptyEnvelope})");
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO InstructionDrafts (IntakeReceiptId, SuggestedPrincipalCode, ClaimantName, ClaimNumber, VehicleRegistration, VehicleMileage, InspectionAddress, InspectionDate) VALUES ({receiptId}, {"QDOS"}, {"Jane Example"}, {"QDOS-123"}, {(registered ? "AB12CDE" : null)}, {mileage}, {"1 Test Street, London"}, {new DateOnly(2031, 5, 20)})");
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO IntakeMailRouteDecisions (IntakeReceiptId, Disposition, RouteOwnerCode, RouteKind, PrincipalCode, PredicatesJson, Reason, PolicyKey, PolicyVersion, TransportIdentitiesJson, OriginalIdentitiesJson) VALUES ({receiptId}, {"accepted"}, {"QDOS"}, {"direct_principal"}, {"QDOS"}, {emptyEnvelope}, {"Accepted QDOS route"}, {"qdos_mail_route"}, {2}, {emptyEnvelope}, {emptyEnvelope})");
        }
    }

    internal sealed class FixedConfiguration : ICaseWorkflowConfiguration
    {
        private static readonly CaseWorkflowConfiguration Configuration = new(
            "case-workflow",
            1);

        public Task<CaseWorkflowConfiguration> GetCurrentAsync(
            CancellationToken cancellationToken) => Task.FromResult(Configuration);
    }

    public sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset current = utcNow;

        public override DateTimeOffset GetUtcNow() => current;

        public void Advance(TimeSpan interval)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(interval, TimeSpan.Zero);
            current += interval;
        }
    }
}
