using Pegasus.Core.Address;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Cases;

public sealed class CaseWorkspaceTests
{
    private static readonly ActionActor Engineer =
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
    private static readonly ActionActor Administrator =
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);

    [Fact]
    public void PostedReadinessIsNotPartOfTheWorkspacePayload()
    {
        // CASE-046: the Case save carries the two factual controls and nothing
        // that claims what they mean. There is no member on the request, on the
        // completeness section, or anywhere it composes, that a client could
        // use to assert the case is ready for Review.
        var members = typeof(SaveCaseWorkspaceRequest).GetProperties()
            .Select(property => property.Name)
            .Concat(typeof(CaseWorkspaceCompleteness).GetProperties().Select(property => property.Name))
            .ToArray();

        Assert.DoesNotContain("Readiness", members);
        Assert.DoesNotContain("EvidenceReference", members);
        Assert.DoesNotContain("InstructionsComplete", members);
        Assert.DoesNotContain("InstructionConfirmedByStaff", members);
        Assert.DoesNotContain("ImagesConfirmedByStaff", members);
        Assert.Equal(
            ["ImagesComplete", "InstructionComplete"],
            typeof(CaseWorkspaceCompleteness).GetProperties()
                .Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal));
    }

    [Fact]
    public void AnEmptyPayloadIsRefused()
    {
        Assert.Throws<ArgumentException>(() =>
            CaseWorkspacePolicy.ValidateAndNormalize(Request()));
    }

    [Fact]
    public void TheWorkspaceRefusesTheAcceptedEngineerValuePath()
    {
        // AUTO-015: adopting the Engineer's value is the valuation Apply
        // command's act, which records the suggested and the chosen amount
        // together. A Case save can neither record nor clear it.
        var record = Assert.Throws<InvalidOperationException>(() =>
            CaseWorkspacePolicy.ValidateAndNormalize(Request(request => request with
            {
                Valuation = new(new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    [AssessmentVocabulary.ValueEngineer] = "4500.00"
                })
            })));
        Assert.Contains("Apply", record.Message, StringComparison.Ordinal);

        var clear = Assert.Throws<InvalidOperationException>(() =>
            CaseWorkspacePolicy.ValidateAndNormalize(Request(request => request with
            {
                Report = new(
                    new Dictionary<string, string?>(StringComparer.Ordinal)
                    {
                        [AssessmentVocabulary.ValueEngineer] = null
                    },
                    null,
                    null)
            })));
        Assert.Contains("Apply", clear.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AdministratorWithoutEngineerRoleCannotWriteAWorkspaceFinding()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            CaseWorkspacePolicy.ValidateAndNormalize(
                Request(
                    request => request with
                    {
                        Settlement = new(new Dictionary<string, string?>(StringComparer.Ordinal)
                        {
                            [AssessmentVocabulary.Outcome] = "repairable"
                        })
                    },
                    Administrator)));

        Assert.Contains("Engineer", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AFieldSubmittedByTwoSectionsFailsClosed()
    {
        Assert.Throws<InvalidOperationException>(() =>
            CaseWorkspacePolicy.ValidateAndNormalize(Request(request => request with
            {
                Vehicle = new(null, null, null, null, new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    [AssessmentVocabulary.VehicleCondition] = "good"
                }),
                Damage = new(null, new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    [AssessmentVocabulary.VehicleCondition] = "poor"
                })
            })));
    }

    [Fact]
    public void ASectionOwnedPathCannotAlsoBePostedAsAFreeAssessmentField()
    {
        Assert.Throws<InvalidOperationException>(() =>
            CaseWorkspacePolicy.ValidateAndNormalize(Request(request => request with
            {
                Damage = new(null, new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    [AssessmentVocabulary.DamageImpacts] = "[]"
                })
            })));
    }

    [Fact]
    public void TheDamageSectionWritesItsImpactsAndTheDerivedHeadlineIsLeftToTheStore()
    {
        var request = CaseWorkspacePolicy.ValidateAndNormalize(Request(request => request with
        {
            Damage = new([new("left_front_wing", "light", "Scuffed")], null)
        }));

        var fields = CaseWorkspacePolicy.AssessmentFields(request);
        Assert.Equal(
            "[{\"zone\":\"left_front_wing\",\"severity\":\"light\",\"note\":\"Scuffed\"}]",
            fields[AssessmentVocabulary.DamageImpacts]);
        Assert.DoesNotContain(AssessmentVocabulary.ImpactLocation, fields.Keys);
        Assert.Equal(
            ("left_front", "light"),
            AssessmentPolicy.DeriveImpactValues(fields[AssessmentVocabulary.DamageImpacts]));
    }

    [Fact]
    public void RepeatedDisplayUnitTogglesNeverReconvertTheStoredOdometer()
    {
        // INTK-026: the recorded reading and its unit are the original, and a
        // display in the other unit is computed from that original every time.
        // Toggling the display is therefore idempotent — it can never feed a
        // rounded display back in as a new reading.
        const long original = 72_850;
        var kilometres = CaseOdometer.Display(
            original,
            CaseOdometerUnit.Miles,
            CaseOdometerUnit.Kilometres);
        Assert.Equal(original * 1.609344m, kilometres);

        for (var toggle = 0; toggle < 5; toggle++)
        {
            Assert.Equal(
                original,
                CaseOdometer.Display(original, CaseOdometerUnit.Miles, CaseOdometerUnit.Miles));
            Assert.Equal(
                kilometres,
                CaseOdometer.Display(original, CaseOdometerUnit.Miles, CaseOdometerUnit.Kilometres));
        }

        var overlaid = CaseWorkspacePolicy.Overlay(
            new(),
            Request(request => request with
            {
                Vehicle = new(
                    null,
                    null,
                    null,
                    new(original, CaseOdometerUnit.Miles, "odometer", CaseOdometerUnit.Kilometres),
                    null)
            }));
        Assert.Equal(original, overlaid.VehicleMileage);
        Assert.Equal("miles", overlaid.VehicleMileageUnit);
        Assert.Equal("kilometres", overlaid.VehicleMileageDisplayUnit);
    }

    [Fact]
    public void ZeroIsARecordedOdometerReadingNotAnAbsentOne()
    {
        Assert.Equal(0m, CaseOdometer.Display(0, CaseOdometerUnit.Miles, CaseOdometerUnit.Kilometres));

        var overlaid = CaseWorkspacePolicy.Overlay(
            new(),
            Request(request => request with
            {
                Vehicle = new(
                    null,
                    null,
                    null,
                    new(0, CaseOdometerUnit.Miles, null, null),
                    null)
            }));
        var normalized = CaseDataPolicy.Normalize(overlaid);

        Assert.Equal(0, normalized.VehicleMileage);
        Assert.Equal("miles", normalized.VehicleMileageUnit);
    }

    [Fact]
    public void AnUnsubmittedSectionKeepsItsPersistedFactsAndASubmittedOneReplacesThem()
    {
        var persisted = new CaseEditableData(
            ClaimantName: "Jane Example",
            VehicleRegistration: "AB12CDE",
            StorageLocation: "Yard 4, Leeds");

        var untouched = CaseWorkspacePolicy.Overlay(
            persisted,
            Request(request => request with
            {
                Completeness = new(true, null)
            }));
        Assert.Equal(persisted, untouched);

        var replaced = CaseWorkspacePolicy.Overlay(
            persisted,
            Request(request => request with
            {
                Vehicle = new("XY65ZZZ", null, null, null, null)
            }));
        Assert.Equal("XY65ZZZ", replaced.VehicleRegistration);
        Assert.Equal("Jane Example", replaced.ClaimantName);
    }

    [Fact]
    public void TheStatedTreatmentDecidesTheStoredAddressAndModeTogether()
    {
        var undetermined = CaseWorkspacePolicy.Overlay(
            new(InspectionAddress: "5 Repairer Way, Leeds", InspectionMode: CaseInspectionMode.PhysicalAddress),
            Request(request => request with
            {
                Inspection = Inspection(CaseReportAddressTreatment.Undetermined, null)
            }));
        Assert.Null(undetermined.InspectionAddress);
        Assert.Null(undetermined.InspectionMode);
        Assert.Equal(CaseReportAddressTreatment.Undetermined, undetermined.InspectionAddressTreatment);

        var imageBased = CaseWorkspacePolicy.Overlay(
            new(),
            Request(request => request with
            {
                Inspection = Inspection(CaseReportAddressTreatment.ImageBasedAssessment, null)
            }));
        Assert.Equal("Image Based Assessment", imageBased.InspectionAddress);
        Assert.Equal(CaseInspectionMode.ImageBasedAssessment, imageBased.InspectionMode);

        var physical = CaseWorkspacePolicy.Overlay(
            new(),
            Request(request => request with
            {
                Inspection = Inspection(
                    CaseReportAddressTreatment.PhysicalVehicleLocation,
                    " 5 Repairer Way, Leeds ")
            }));
        Assert.Equal("5 Repairer Way, Leeds", physical.InspectionAddress);
        Assert.Equal(CaseInspectionMode.PhysicalAddress, physical.InspectionMode);
    }

    [Fact]
    public void TheInspectionSectionCarriesTheProvenanceOfTheChosenLocation()
    {
        var sourceId = Guid.NewGuid();
        var overlaid = CaseWorkspacePolicy.Overlay(
            new(),
            Request(request => request with
            {
                Inspection = Inspection(
                    CaseReportAddressTreatment.PhysicalVehicleLocation,
                    "Yard 4, Leeds",
                    new(
                        InspectionAddressChoiceKind.StorageLocation,
                        InspectionLocationSourceKind.Storage,
                        sourceId,
                        11,
                        "Leeds Recovery Ltd"))
            }));

        var normalized = CaseDataPolicy.Normalize(overlaid);
        Assert.Equal(InspectionAddressChoiceKind.StorageLocation, normalized.InspectionLocationChoice);
        Assert.Equal(InspectionLocationSourceKind.Storage, normalized.InspectionLocationSource);
        Assert.Equal(sourceId, normalized.InspectionLocationSourceId);
        Assert.Equal(11, normalized.InspectionLocationSourceVersion);
        Assert.Equal("Leeds Recovery Ltd", normalized.InspectionLocationSourceLabel);
    }

    [Fact]
    public void ASourceIdentityWithoutItsVersionIsRefused()
    {
        Assert.Throws<InvalidOperationException>(() => CaseDataPolicy.Normalize(
            new(ClaimSourceId: Guid.NewGuid())));
        Assert.Throws<InvalidOperationException>(() => CaseDataPolicy.Normalize(
            new(StorageBusinessId: Guid.NewGuid())));
        Assert.Throws<InvalidOperationException>(() => CaseDataPolicy.Normalize(
            new(InspectionLocationSourceId: Guid.NewGuid())));
    }

    [Fact]
    public void TheStorageAmountsAreTheSettlementAmountsAndNotASecondCopy()
    {
        var request = CaseWorkspacePolicy.ValidateAndNormalize(Request(request => request with
        {
            Inspection = Inspection(CaseReportAddressTreatment.Undetermined, null) with
            {
                StoragePerDay = 18.50m,
                RecoveryCharge = 150m
            }
        }));

        var fields = CaseWorkspacePolicy.AssessmentFields(request);
        Assert.Equal("18.50", fields[AssessmentVocabulary.SettlementStoragePerDay]);
        Assert.Equal("150.00", fields[AssessmentVocabulary.CostRecoveryCharge]);
    }

    [Fact]
    public void AnEstimateSectionWithoutAHeaderOrLinesIsRefused()
    {
        Assert.Throws<ArgumentException>(() =>
            CaseWorkspacePolicy.ValidateAndNormalize(Request(request => request with
            {
                Estimate = new(null, null, null)
            })));
    }

    [Fact]
    public void OnlyAnEngineerMaySubmitTheEstimateSection()
    {
        Assert.Throws<InvalidOperationException>(() =>
            CaseWorkspacePolicy.ValidateAndNormalize(
                Request(
                    request => request with
                    {
                        Estimate = new(null, null, [])
                    },
                    Administrator)));
    }

    private static CaseWorkspaceInspection Inspection(
        CaseReportAddressTreatment treatment,
        string? address,
        CaseLocationProvenance? provenance = null) => new(
        treatment,
        address,
        provenance,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null);

    private static SaveCaseWorkspaceRequest Request(
        Func<SaveCaseWorkspaceRequest, SaveCaseWorkspaceRequest>? configure = null,
        ActionActor? actor = null)
    {
        var request = new SaveCaseWorkspaceRequest(
            Guid.NewGuid(),
            4,
            actor ?? Engineer,
            "case-workspace-save",
            "Recorded the Engineer's inspection",
            "lease-token");
        return configure is null ? request : configure(request);
    }
}
