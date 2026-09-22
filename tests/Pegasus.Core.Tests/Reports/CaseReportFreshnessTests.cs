using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Reports;

namespace Pegasus.Core.Tests.Reports;

public sealed class CaseReportFreshnessTests
{
    [Fact]
    public void NoteOnlyWorkspaceChangesDoNotStaleTheGeneration()
    {
        var beforeData = new CaseEditableData(ClientNotes: "Original note");
        var afterData = beforeData with { ClientNotes = "Updated note" };
        var beforeAssessment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [AssessmentVocabulary.VehicleEngineerNotes] = "Original engineer note",
        };
        var afterAssessment = new Dictionary<string, string?>(beforeAssessment, StringComparer.Ordinal)
        {
            [AssessmentVocabulary.VehicleEngineerNotes] = "Updated engineer note",
        };

        var decision = CaseReportFreshness.ClassifyWorkspace(
            beforeData, afterData, wordingChanged: false, beforeAssessment, afterAssessment, null, null);

        Assert.False(decision.IsStale);
        Assert.Null(decision.ReasonCode);
    }

    [Fact]
    public void PrintedAssessmentFactChangesStaleWithTheAssessmentReason()
    {
        var before = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [AssessmentVocabulary.ValueEngineer] = "5000.00",
        };
        var after = new Dictionary<string, string?>(before, StringComparer.Ordinal)
        {
            [AssessmentVocabulary.ValueEngineer] = "5250.00",
        };

        var decision = CaseReportFreshness.ClassifyAssessment(before, after);

        Assert.True(decision.IsStale);
        Assert.Equal(CaseReportStaleReasons.AssessmentFactsChanged, decision.ReasonCode);
    }

    [Theory]
    [InlineData(AssessmentVocabulary.ReportDiscloseGuideSource)]
    [InlineData(AssessmentVocabulary.ReportValuationCommentary)]
    [InlineData(AssessmentVocabulary.ReportIncludeUnrelatedDamage)]
    [InlineData(AssessmentVocabulary.ReportDateOverride)]
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
    [InlineData(AssessmentVocabulary.ReportDateOverride)]
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
            CaseAssetReportRole.CloseUp,
            null,
            rotation,
            CaseAssetCrop.Full,
            1,
            "Staff:test",
            DateTimeOffset.UnixEpoch);
}
