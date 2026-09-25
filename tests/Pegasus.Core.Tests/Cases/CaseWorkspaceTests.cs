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

    [Fact]
    public void PostedReadinessIsNotPartOfTheWorkspacePayload()
    {
        // The Case save carries the two factual controls and nothing
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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ASaveNeedsNoReason(string? reason)
    {
        var request = Request(request => new SaveCaseWorkspaceRequest(
            request.CaseId, request.ExpectedVersion, request.Actor, request.OperationKey, reason, request.EditLeaseToken)
        {
            Overview = new(
                "A Claimant", null, null, null, null, null, null, null, null, null, null, null)
        });

        var normalized = CaseWorkspacePolicy.ValidateAndNormalize(request);

        Assert.NotNull(normalized);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CaseWorkspacePolicy.ValidateAndNormalize(request with { Reason = new string('x', 501) }));
    }

    [Fact]
    public void TheHistoryLineNamesWhatChanged()
    {
        var before = new CaseEditableData(ClaimantName: "A Claimant", VehicleRegistration: "AB12CDE");
        var after = before with { VehicleRegistration = "CD34EFG", IncidentDate = new DateOnly(2026, 9, 1) };
        var beforeFields = new Dictionary<string, object?> { ["assessment.outcome"] = "repairable" };
        var afterFields = new Dictionary<string, object?> { ["assessment.outcome"] = "total_loss", ["valuation.retail"] = "4500" };

        var summary = CaseWorkspaceChangeSummary.Describe(before, after, beforeFields, afterFields, estimateChanged: true, imagesPrepared: 2, reason: null);

        Assert.Equal("Vehicle registration, Incident date, Outcome, Retail, Estimate, 2 images prepared", summary);
        Assert.Equal(
            "No field changed · Checked with the repairer",
            CaseWorkspaceChangeSummary.Describe(before, before, beforeFields, beforeFields, false, 0, "  Checked with the repairer "));
    }

    [Theory]
    [InlineData(AssessmentVocabulary.ValueEngineer)]
    [InlineData(AssessmentVocabulary.ValueRetail)]
    [InlineData(AssessmentVocabulary.ValueTrade)]
    public void TheWorkspaceRefusesTheAdoptedValuationPaths(string path)
    {
        // The Engineer's Value and the retail and trade of its basis card are
        // recorded only by the adoption, which records them with the
        // calculation (one Save, 23 September 2026; operator, 24 September
        // 2026). As free fields a Case save can neither record nor clear them.
        var clear = Assert.Throws<InvalidOperationException>(() =>
            CaseWorkspacePolicy.ValidateAndNormalize(Request(request => request with
            {
                Report = new(
                    new Dictionary<string, string?>(StringComparer.Ordinal)
                    {
                        [path] = null
                    },
                    null,
                    null)
            })));
        Assert.Contains("adopts an Engineer's Value", clear.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The guide source cards have no Save of their own (23 September 2026):
    /// the Case save carries them, each checked by the valuation policy, and
    /// one source's card for one month is one answer.
    /// </summary>
    [Fact]
    public void TheCaseSaveCarriesGuideCardsCheckedByTheValuationPolicy()
    {
        var april = new DateOnly(2030, 4, 1);
        var glasses = GuideCard(ValuationSource.Glasses, april);
        var brego = GuideCard(ValuationSource.Brego, april);

        var normalized = CaseWorkspacePolicy.ValidateAndNormalize(Request(request => request with
        {
            Valuation = new([glasses, brego])
        }));

        Assert.Equal([glasses, brego], normalized.Valuation!.GuideEntries);
        Assert.Empty(CaseWorkspacePolicy.AssessmentFields(normalized));
        Assert.Throws<InvalidOperationException>(() =>
            CaseWorkspacePolicy.ValidateAndNormalize(Request(request => request with
            {
                Valuation = new([glasses, glasses with { RetailValue = 1m }])
            })));
        Assert.Throws<InvalidOperationException>(() =>
            CaseWorkspacePolicy.ValidateAndNormalize(Request(request => request with
            {
                Valuation = new([GuideCard(ValuationSource.EngineersValue, april)])
            })));
        // Any box of a card may be blank (operator, 23 September 2026).
        var partly = CaseWorkspacePolicy.ValidateAndNormalize(Request(request => request with
        {
            Valuation = new([GuideCard(ValuationSource.Glasses, null) with { TradeValue = null, Mileage = null }])
        }));
        var card = Assert.Single(partly.Valuation!.GuideEntries!);
        Assert.Null(card.GuideMonth);
        Assert.Null(card.TradeValue);
        Assert.Null(card.Mileage);
    }

    /// <summary>
    /// A calculation the Case save adopts (one Save, 23 September 2026) passes
    /// the rules the calculator's preview applies, and is a professional
    /// finding only staff record.
    /// </summary>
    [Fact]
    public void AnAdoptedCalculationIsCheckedByTheValuationRules()
    {
        var calculation = new ValuationCalculationSelection(Guid.NewGuid(), false, 0.10m, [], 0m);
        var normalized = CaseWorkspacePolicy.ValidateAndNormalize(Request(request => request with
        {
            Valuation = new([], calculation)
        }));
        Assert.Equal(0.10m, normalized.Valuation!.Adoption!.PriorTotalLossPercentage);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CaseWorkspacePolicy.ValidateAndNormalize(Request(request => request with
            {
                Valuation = new([], calculation with { PriorTotalLossPercentage = 0.15m })
            })));
        Assert.Throws<InvalidOperationException>(() =>
            CaseWorkspacePolicy.ValidateAndNormalize(Request(
                request => request with { Valuation = new([], calculation) },
                ActionActor.Automation("case-save"))));
    }

    [Fact]
    public void TheHistoryLineNamesAnAdoptedEngineersValue()
    {
        var data = new CaseEditableData();
        var fields = new Dictionary<string, object?>(StringComparer.Ordinal);

        Assert.Equal(
            "Engineer's Value applied",
            CaseWorkspaceChangeSummary.Describe(data, data, fields, fields, false, 0, null, valuationAdopted: true));
    }

    [Fact]
    public void TheHistoryLineNamesTheGuideCardsTheSaveRecorded()
    {
        var data = new CaseEditableData(ClaimantName: "A Claimant");
        var fields = new Dictionary<string, object?>();

        var summary = CaseWorkspaceChangeSummary.Describe(
            data,
            data,
            fields,
            fields,
            estimateChanged: false,
            imagesPrepared: 0,
            reason: null,
            valuationsRecorded: [GuideCard(ValuationSource.SuperCap, new DateOnly(2030, 4, 1))]);

        Assert.Equal("Valuation: Super CAP Apr 2030", summary);
    }

    [Theory]
    [InlineData(StaffRole.Administrator)]
    [InlineData(StaffRole.Engineer)]
    [InlineData(StaffRole.User)]
    public void EveryStaffRoleCanWriteAWorkspaceFinding(StaffRole role)
    {
        var normalized = CaseWorkspacePolicy.ValidateAndNormalize(
            Request(
                request => request with
                {
                    Settlement = new(new Dictionary<string, string?>(StringComparer.Ordinal)
                    {
                        [AssessmentVocabulary.Outcome] = "repairable"
                    })
                },
                ActionActor.Staff(Guid.NewGuid(), [role])));

        Assert.Equal(
            "repairable",
            CaseWorkspacePolicy.AssessmentFields(normalized)[AssessmentVocabulary.Outcome]);
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
            Damage = new([new(["left_front"], "light", "Scuffed")], null)
        }));

        var fields = CaseWorkspacePolicy.AssessmentFields(request);
        Assert.Equal(
            "[{\"areas\":[\"left_front\"],\"severity\":\"light\",\"note\":\"Scuffed\"}]",
            fields[AssessmentVocabulary.DamageImpacts]);
        Assert.DoesNotContain(AssessmentVocabulary.ImpactLocation, fields.Keys);
        Assert.Equal(
            ("left_front", "light"),
            AssessmentPolicy.DeriveImpactValues(fields[AssessmentVocabulary.DamageImpacts]));
    }

    [Fact]
    public void RepeatedDisplayUnitTogglesNeverReconvertTheStoredOdometer()
    {
        // The recorded reading and its unit are the original, and a
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

    /// <summary>
    /// The claim source's contact boxes show the effective contact in both
    /// modes, so posting back the contact copied from the record clears the
    /// Case's own override, and only a different value is kept as one.
    /// </summary>
    [Fact]
    public void AClaimSourceContactPostedAsCopiedClearsTheCasesOverride()
    {
        var sourceId = Guid.NewGuid();
        var persisted = new CaseEditableData(
            ClaimSourceId: sourceId,
            ClaimSourceContactName: "Directory Handler",
            ClaimSourceContactTelephone: "0113 000 0000",
            ClaimSourceOverrideContactName: "Case Handler",
            ClaimSourceOverrideContactTelephone: "0113 111 1111");
        var overlaid = CaseWorkspacePolicy.Overlay(
            persisted,
            Request(request => request with
            {
                Overview = new(
                    null, null, null, null, null, null, null, null, null, null, null,
                    new CaseWorkspaceClaimSource(
                        sourceId, 4, "Acme Claims", "Directory Handler", "0113 000 0000", null,
                        OverrideContactName: " Directory Handler ",
                        OverrideContactTelephone: "0113 999 9999",
                        OverrideContactEmailAddress: null))
            }));

        Assert.True(string.IsNullOrEmpty(overlaid.ClaimSourceOverrideContactName));
        Assert.Equal("0113 999 9999", overlaid.ClaimSourceOverrideContactTelephone);
        Assert.Null(overlaid.ClaimSourceOverrideContactEmailAddress);
        Assert.Null(CaseWorkspaceClaimSource.SubmittedOverride(null, "Directory Handler"));
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
    public void AnEstimateSectionIsCheckedByTheEstimatePolicy()
    {
        Assert.Throws<ArgumentException>(() =>
            CaseWorkspacePolicy.ValidateAndNormalize(Request(request => request with
            {
                Estimate = new(null, new EstimateDetails(" ", null, null, 20m), [])
            })));
    }

    [Theory]
    [InlineData(StaffRole.Administrator)]
    [InlineData(StaffRole.Engineer)]
    [InlineData(StaffRole.User)]
    public void EveryStaffRoleMaySubmitTheEstimateSection(StaffRole role)
    {
        var normalized = CaseWorkspacePolicy.ValidateAndNormalize(
            Request(
                request => request with
                {
                    Estimate = new(null, new EstimateDetails("Estimate 1", null, null, 20m), [])
                },
                ActionActor.Staff(Guid.NewGuid(), [role])));

        Assert.NotNull(normalized.Estimate);
        Assert.Empty(normalized.Estimate.Lines);
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

    private static ValuationDetails GuideCard(ValuationSource source, DateOnly? guideMonth) =>
        new(source, new DateOnly(2030, 5, 6), new TimeOnly(10, 30), 42_000, 12_500m, 10_250m, guideMonth);
}
