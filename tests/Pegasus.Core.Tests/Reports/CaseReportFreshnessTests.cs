using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Reports;

namespace Pegasus.Core.Tests.Reports;

public sealed class CaseReportFreshnessTests
{
    [Fact]
    public void UnprintedWorkspaceChangesDoNotStaleTheGeneration()
    {
        var beforeData = new CaseEditableData(ClientNotes: "Original note");
        var afterData = beforeData with { ClientNotes = "Updated note" };
        var beforeAssessment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [AssessmentVocabulary.OriginalReportAssessor] = "Original assessor",
        };
        var afterAssessment = new Dictionary<string, string?>(beforeAssessment, StringComparer.Ordinal)
        {
            [AssessmentVocabulary.OriginalReportAssessor] = "Corrected assessor",
        };

        var decision = CaseReportFreshness.ClassifyWorkspace(
            beforeData, afterData, wordingChanged: false, beforeAssessment, afterAssessment, null, null);

        Assert.False(decision.IsStale);
        Assert.Null(decision.ReasonCode);
    }

    /// <summary>
    /// Every assessment fact the report's snapshot carries: the template's
    /// vehicle facts, the damage its diagram and narrative read, the three
    /// values, the outcome and its salvage, roadworthiness, the history check
    /// and the agreed contract sum.
    /// </summary>
    [Theory]
    [InlineData(AssessmentVocabulary.VehicleVin)]
    [InlineData(AssessmentVocabulary.VehicleEngineCc)]
    [InlineData(AssessmentVocabulary.VehicleType)]
    [InlineData(AssessmentVocabulary.VehicleFuel)]
    [InlineData(AssessmentVocabulary.VehicleCondition)]
    [InlineData(AssessmentVocabulary.ImpactSeverity)]
    [InlineData(AssessmentVocabulary.ImpactLocation)]
    [InlineData(AssessmentVocabulary.DamageImpacts)]
    [InlineData(AssessmentVocabulary.DamageUnrelated)]
    [InlineData(AssessmentVocabulary.ValueRetail)]
    [InlineData(AssessmentVocabulary.ValueTrade)]
    [InlineData(AssessmentVocabulary.ValueEngineer)]
    [InlineData(AssessmentVocabulary.Outcome)]
    [InlineData(AssessmentVocabulary.LegalStatus)]
    [InlineData(AssessmentVocabulary.UnroadworthyReason)]
    [InlineData(AssessmentVocabulary.SalvageCategory)]
    [InlineData(AssessmentVocabulary.SalvageValue)]
    [InlineData(AssessmentVocabulary.HistoryCheck)]
    [InlineData(AssessmentVocabulary.SettlementContractSum)]
    public void PrintedAssessmentFactChangesStaleWithTheAssessmentReason(string path)
    {
        var before = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [path] = "5000.00",
        };
        var after = new Dictionary<string, string?>(before, StringComparer.Ordinal)
        {
            [path] = "5250.00",
        };

        var decision = CaseReportFreshness.ClassifyAssessment(before, after);

        Assert.True(decision.IsStale);
        Assert.Equal(CaseReportStaleReasons.AssessmentFactsChanged, decision.ReasonCode);
    }

    /// <summary>
    /// The report prints only what the template prints (operator, 27
    /// September 2026). The tyres and belts, the airbags, the temporary
    /// repairs, the vehicle's colour, body, transmission and expiry
    /// dates, the unrelated damage deduction, the material transfer, the
    /// recovery charge and the settlement facts beyond the agreed contract sum
    /// stay on the Case, so changing one leaves a generated report current.
    /// </summary>
    [Theory]
    [InlineData(AssessmentVocabulary.VehicleTransmission)]
    [InlineData(AssessmentVocabulary.VehicleColour)]
    [InlineData(AssessmentVocabulary.VehicleBody)]
    [InlineData(AssessmentVocabulary.VehicleTaxExpiry)]
    [InlineData(AssessmentVocabulary.VehicleMotExpiry)]
    [InlineData(AssessmentVocabulary.VehicleAirbagsDeployed)]
    [InlineData(AssessmentVocabulary.VehicleTemporaryRepairsPossible)]
    [InlineData(AssessmentVocabulary.VehicleTemporaryRepairMethod)]
    [InlineData(AssessmentVocabulary.VehicleTemporaryRepairCost)]
    [InlineData(AssessmentVocabulary.DamageTyreRightFront)]
    [InlineData(AssessmentVocabulary.DamageTyreLeftFront)]
    [InlineData(AssessmentVocabulary.DamageTyreRightRear)]
    [InlineData(AssessmentVocabulary.DamageTyreLeftRear)]
    [InlineData(AssessmentVocabulary.DamageBeltRightFront)]
    [InlineData(AssessmentVocabulary.DamageBeltLeftFront)]
    [InlineData(AssessmentVocabulary.DamageBeltRightRear)]
    [InlineData(AssessmentVocabulary.DamageBeltLeftRear)]
    [InlineData(AssessmentVocabulary.DamageSpareTyre)]
    [InlineData(AssessmentVocabulary.DamageCentreBelt)]
    [InlineData(AssessmentVocabulary.DamageUnrelatedDeduction)]
    [InlineData(AssessmentVocabulary.DamageMaterialTransfer)]
    [InlineData(AssessmentVocabulary.CostRecoveryCharge)]
    [InlineData(AssessmentVocabulary.CostStorageCharge)]
    [InlineData(AssessmentVocabulary.SettlementExcess)]
    [InlineData(AssessmentVocabulary.SettlementBetterment)]
    [InlineData(AssessmentVocabulary.SettlementClaimantVatRegistered)]
    [InlineData(AssessmentVocabulary.SettlementReserve)]
    [InlineData(AssessmentVocabulary.SettlementRepairDelays)]
    [InlineData(AssessmentVocabulary.SettlementReportDelay)]
    [InlineData(AssessmentVocabulary.SettlementStoragePerDay)]
    [InlineData(AssessmentVocabulary.SettlementHireStart)]
    [InlineData(AssessmentVocabulary.SettlementHireDailyCost)]
    [InlineData(AssessmentVocabulary.SettlementDiminution)]
    [InlineData(AssessmentVocabulary.SettlementSalvageAt)]
    [InlineData(AssessmentVocabulary.SettlementSalvageAgent)]
    [InlineData(AssessmentVocabulary.SettlementSalvageAgentReference)]
    [InlineData(AssessmentVocabulary.SettlementSalvageMoved)]
    [InlineData(AssessmentVocabulary.SettlementSalvageOwnerRetains)]
    [InlineData(AssessmentVocabulary.SettlementSalvageValueAgreed)]
    [InlineData(AssessmentVocabulary.SettlementSalvageSettled)]
    public void AFactTheReportDoesNotPrintDoesNotStale(string path)
    {
        var before = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [path] = "before",
        };
        var after = new Dictionary<string, string?>(before, StringComparer.Ordinal)
        {
            [path] = "after",
        };

        var decision = CaseReportFreshness.ClassifyAssessment(before, after);

        Assert.False(decision.IsStale);
        Assert.Null(decision.ReasonCode);
        Assert.False(CaseReportFreshness.ClassifyAssessment(
            new Dictionary<string, string?>(StringComparer.Ordinal), after).IsStale);
    }

    [Theory]
    [InlineData(AssessmentVocabulary.ReportDiscloseGuideSource)]
    [InlineData(AssessmentVocabulary.ReportValuationCommentary)]
    [InlineData(AssessmentVocabulary.ReportIncludeUnrelatedDamage)]
    public void SavingAnAbsentReportSwitchAsFalseDoesNotStale(string path)
    {
        var before = new Dictionary<string, string?>(StringComparer.Ordinal);
        var after = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [path] = "false",
        };

        var decision = CaseReportFreshness.ClassifyAssessment(before, after);

        Assert.False(decision.IsStale);
        Assert.Null(decision.ReasonCode);
    }

    [Theory]
    [InlineData(AssessmentVocabulary.ReportDiscloseGuideSource)]
    [InlineData(AssessmentVocabulary.ReportValuationCommentary)]
    [InlineData(AssessmentVocabulary.ReportIncludeUnrelatedDamage)]
    public void EnablingAReportSwitchStalesWithTheReportContentReason(string path)
    {
        var before = new Dictionary<string, string?>(StringComparer.Ordinal);
        var after = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [path] = "true",
        };

        var decision = CaseReportFreshness.ClassifyAssessment(before, after);

        Assert.True(decision.IsStale);
        Assert.Equal(CaseReportStaleReasons.ReportContentChanged, decision.ReasonCode);
    }

    [Fact]
    public void RecordingAReportDateStalesWithTheReportContentReason()
    {
        // A recorded Report date is the date the report prints.
        var before = new Dictionary<string, string?>(StringComparer.Ordinal);
        var after = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [AssessmentVocabulary.ReportDate] = "2026-10-01",
        };

        var decision = CaseReportFreshness.ClassifyAssessment(before, after);

        Assert.True(decision.IsStale);
        Assert.Equal(CaseReportStaleReasons.ReportContentChanged, decision.ReasonCode);
    }

    [Fact]
    public void ChangedResolvedMileageSourceStalesWithTheAssessmentReason()
    {
        var before = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [AssessmentVocabulary.VehicleMileageSource] = "owner",
        };
        var after = new Dictionary<string, string?>(before, StringComparer.Ordinal)
        {
            [AssessmentVocabulary.VehicleMileageSource] = "repairer",
        };

        var decision = CaseReportFreshness.ClassifyAssessment(
            before,
            after,
            CaseVehicleMileageSourcePolicy.Owner,
            CaseVehicleMileageSourcePolicy.Repairer);

        Assert.True(decision.IsStale);
        Assert.Equal(CaseReportStaleReasons.AssessmentFactsChanged, decision.ReasonCode);
    }

    [Fact]
    public void MileageChoiceDoesNotStaleWhenProvenanceKeepsTheResolvedSourceUnchanged()
    {
        var before = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [AssessmentVocabulary.VehicleMileageSource] = "owner",
        };
        var after = new Dictionary<string, string?>(before, StringComparer.Ordinal)
        {
            [AssessmentVocabulary.VehicleMileageSource] = "repairer",
        };

        var decision = CaseReportFreshness.ClassifyAssessment(
            before,
            after,
            CaseVehicleMileageSourcePolicy.OnlineData,
            CaseVehicleMileageSourcePolicy.OnlineData);

        Assert.False(decision.IsStale);
        Assert.Null(decision.ReasonCode);
    }

    [Fact]
    public void PrintedCaseFactChangesStaleWithTheAssessmentReason()
    {
        var before = new CaseEditableData(
            VehicleMileage: 12_000,
            VehicleMileageUnit: "miles",
            ClientNotes: "Same note");
        var after = before with { VehicleMileage = 12_001 };

        var decision = CaseReportFreshness.ClassifyCaseData(before, after);

        Assert.True(decision.IsStale);
        Assert.Equal(CaseReportStaleReasons.AssessmentFactsChanged, decision.ReasonCode);
    }

    /// <summary>
    /// The report says the damage was assessed on the Case's Inspection date
    /// (#834), so changing it stales the report like any printed Case fact.
    /// </summary>
    [Fact]
    public void ChangedInspectionDateStalesWithTheAssessmentReason()
    {
        var before = new CaseEditableData(InspectionDate: new DateOnly(2026, 8, 3));
        var after = before with { InspectionDate = new DateOnly(2026, 8, 4) };

        var decision = CaseReportFreshness.ClassifyCaseData(before, after);
        var workspace = CaseReportFreshness.ClassifyWorkspace(
            before,
            after,
            wordingChanged: false,
            new Dictionary<string, string?>(),
            new Dictionary<string, string?>(),
            null,
            null);

        Assert.True(decision.IsStale);
        Assert.Equal(CaseReportStaleReasons.AssessmentFactsChanged, decision.ReasonCode);
        Assert.True(workspace.IsStale);
        Assert.Equal(CaseReportStaleReasons.AssessmentFactsChanged, workspace.ReasonCode);
    }

    /// <summary>
    /// The inspection deadline is a working date the report never prints.
    /// </summary>
    [Fact]
    public void ChangedInspectionDeadlineDoesNotStale()
    {
        var before = new CaseEditableData(InspectionDeadline: new DateOnly(2026, 8, 10));
        var after = before with { InspectionDeadline = new DateOnly(2026, 8, 11) };

        var decision = CaseReportFreshness.ClassifyCaseData(before, after);

        Assert.False(decision.IsStale);
        Assert.Null(decision.ReasonCode);
    }

    [Fact]
    public void SignOffEngineerChangesStaleWithTheSignatoryReason()
    {
        var decision = CaseReportFreshness.ClassifyWorkspace(
            new CaseEditableData(),
            new CaseEditableData(),
            wordingChanged: false,
            new Dictionary<string, string?>(),
            new Dictionary<string, string?>(),
            Guid.NewGuid(),
            Guid.NewGuid());

        Assert.True(decision.IsStale);
        Assert.Equal(CaseReportStaleReasons.SignatoryChanged, decision.ReasonCode);
    }

    [Fact]
    public void UnchangedResolvedSignOffEngineerDoesNotStale()
    {
        var engineerId = Guid.NewGuid();

        var decision = CaseReportFreshness.ClassifyWorkspace(
            new CaseEditableData(),
            new CaseEditableData(),
            wordingChanged: false,
            new Dictionary<string, string?>(),
            new Dictionary<string, string?>(),
            engineerId,
            engineerId);

        Assert.False(decision.IsStale);
        Assert.Null(decision.ReasonCode);
    }

    [Fact]
    public void ChangedReportWordingStalesWithTheReportContentReason()
    {
        var decision = CaseReportFreshness.ClassifyWorkspace(
            new CaseEditableData(),
            new CaseEditableData(),
            wordingChanged: true,
            new Dictionary<string, string?>(),
            new Dictionary<string, string?>(),
            null,
            null);

        Assert.True(decision.IsStale);
        Assert.Equal(CaseReportStaleReasons.ReportContentChanged, decision.ReasonCode);
    }

    [Fact]
    public void EffectivePreparedImageChangesStaleWithTheImageReason()
    {
        var caseId = Guid.NewGuid();
        var occurrenceId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var before = Preparation(caseId, occurrenceId, versionId, CaseAssetRotation.None);
        var after = before with { Rotation = CaseAssetRotation.Clockwise90 };

        var decision = CaseReportFreshness.ClassifyImages([before], [after]);

        Assert.True(decision.IsStale);
        Assert.Equal(CaseReportStaleReasons.ImagePreparationChanged, decision.ReasonCode);
    }

    [Fact]
    public void ChangingFullPageStalesWithTheImageReason()
    {
        var caseId = Guid.NewGuid();
        var occurrenceId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var before = Preparation(caseId, occurrenceId, versionId, CaseAssetRotation.None);
        var after = before with { FullPage = true };

        var decision = CaseReportFreshness.ClassifyImages([before], [after]);

        Assert.True(decision.IsStale);
        Assert.Equal(CaseReportStaleReasons.ImagePreparationChanged, decision.ReasonCode);
    }

    [Fact]
    public void UnchangedValuationDependenciesDoNotStale()
    {
        var dependencies = new CaseReportValuationDependencies(
            UsesGlassesValuationGuide: true,
            EngineersValue: "12000.00",
            AppliedValuationId: Guid.NewGuid(),
            AcceptedEngineerValue: 12000m,
            AppliedValuationReason: "Accepted guide calculation");

        var decision = CaseReportFreshness.ClassifyValuation(dependencies, dependencies);

        Assert.False(decision.IsStale);
        Assert.Null(decision.ReasonCode);
    }

    [Fact]
    public void ChangedValuationDependenciesStaleWithTheValuationReason()
    {
        var before = new CaseReportValuationDependencies(
            UsesGlassesValuationGuide: false,
            EngineersValue: null,
            AppliedValuationId: null,
            AcceptedEngineerValue: null,
            AppliedValuationReason: null);

        var decision = CaseReportFreshness.ClassifyValuation(
            before,
            before with { UsesGlassesValuationGuide = true });

        Assert.True(decision.IsStale);
        Assert.Equal(CaseReportStaleReasons.ValuationChanged, decision.ReasonCode);
    }

    [Fact]
    public void ReselectingTheCurrentEstimateDoesNotStale()
    {
        var dependencies = new CaseReportEstimateDependencies(Guid.NewGuid(), 2);

        var decision = CaseReportFreshness.ClassifyEstimate(dependencies, dependencies);

        Assert.False(decision.IsStale);
        Assert.Null(decision.ReasonCode);
    }

    [Fact]
    public void ChangingTheCurrentEstimateStalesWithTheEstimateReason()
    {
        var before = new CaseReportEstimateDependencies(Guid.NewGuid(), 2);
        var after = new CaseReportEstimateDependencies(Guid.NewGuid(), 3);

        var decision = CaseReportFreshness.ClassifyEstimate(before, after);

        Assert.True(decision.IsStale);
        Assert.Equal(CaseReportStaleReasons.EstimateChanged, decision.ReasonCode);
    }

    [Fact]
    public void ChangedEffectiveVehicleFactsStaleWithTheAssessmentReason()
    {
        var before = new CaseReportVehicleDependencies(
            null, null, null, null, null, CaseVehicleMileageSourcePolicy.ToBeConfirmed);
        var after = new CaseReportVehicleDependencies(
            "RENAULT", "CAPTUR", "2016", 121_823, "Miles",
            CaseVehicleMileageSourcePolicy.OnlineData);

        var decision = CaseReportFreshness.ClassifyVehicle(before, after);

        Assert.True(decision.IsStale);
        Assert.Equal(CaseReportStaleReasons.AssessmentFactsChanged, decision.ReasonCode);
    }

    [Fact]
    public void UnchangedEffectiveVehicleFactsDoNotStale()
    {
        var dependencies = new CaseReportVehicleDependencies(
            "RENAULT", "CAPTUR", "2016", 121_823, "Miles",
            CaseVehicleMileageSourcePolicy.OnlineData);

        var decision = CaseReportFreshness.ClassifyVehicle(dependencies, dependencies);

        Assert.False(decision.IsStale);
        Assert.Null(decision.ReasonCode);
    }

    private static CaseAssetPreparation Preparation(
        Guid caseId,
        Guid occurrenceId,
        Guid versionId,
        CaseAssetRotation rotation) => new(
            caseId,
            occurrenceId,
            Guid.NewGuid(),
            versionId,
            1,
            new string('a', 64),
            "image/jpeg",
            true,
            null,
            rotation,
            CaseAssetCrop.Full,
            1,
            "Staff:test",
            DateTimeOffset.UnixEpoch,
            false)
        {
            SourceFileName = "image.jpg",
            RecordedAtUtc = DateTimeOffset.UnixEpoch,
            CanPrint = true
        };
}
