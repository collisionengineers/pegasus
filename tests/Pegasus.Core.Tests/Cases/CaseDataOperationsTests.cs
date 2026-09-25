using Pegasus.Core.Address;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Cases;

public sealed class CaseDataOperationsTests
{
    private static readonly CaseWorkflowConfiguration Configuration = new(
        "test-case-workflow",
        7);

    [Fact]
    public void AccidentCircumstancesKeepTheBlankLineAboveTheDamageArea()
    {
        // This is the one case text field that keeps its line
        // structure, because EVA is sent the labelled damage-area block below
        // the prose verbatim. Every other text field still collapses.
        var normalized = CaseDataPolicy.Normalize(new(
            AccidentCircumstances: "The insured reversed into the claimant's vehicle.\n"
                + "\n"
                + "Damage Area: rear",
            ClaimantName: "Mrs   Caroline\nReynolds"));

        Assert.Equal(
            "The insured reversed into the claimant's vehicle.\n\nDamage Area: rear",
            normalized.AccidentCircumstances);
        Assert.Equal("Mrs Caroline Reynolds", normalized.ClaimantName);
    }

    [Fact]
    public void AccidentCircumstancesCollapseWhitespaceWithinAndBetweenLines()
    {
        var normalized = CaseDataPolicy.Normalize(new(
            AccidentCircumstances: "\n\n  Prose   with    gaps  \n\n\n\nDamage Area:   rear  \n\n"));

        Assert.Equal("Prose with gaps\n\nDamage Area: rear", normalized.AccidentCircumstances);
    }

    [Fact]
    public void CompletenessPolicyDependsOnlyOnCompleteInstructionsAndImages()
    {
        var evaluation = CaseCompletenessPolicy.Evaluate(
            new(true, true),
            Configuration);

        Assert.True(evaluation.SatisfiesPolicy);
        Assert.Equal("test-case-workflow", evaluation.PolicyKey);
        Assert.Equal(7, evaluation.PolicyVersion);
    }

    [Fact]
    public void NormalizeRequiresInspectionAddressAndModeTogether()
    {
        Assert.Throws<InvalidOperationException>(() => CaseDataPolicy.Normalize(
            new(InspectionAddress: "1 Test Street, London")));
        Assert.Throws<InvalidOperationException>(() => CaseDataPolicy.Normalize(
            new(InspectionMode: CaseInspectionMode.PhysicalAddress)));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("Image Based Assessment", CaseInspectionMode.ImageBasedAssessment)]
    [InlineData("1 Test Street", CaseInspectionMode.PhysicalAddress)]
    public void InspectionModeIsInferredFromTheAddress(
        string? address,
        CaseInspectionMode? expected)
    {
        Assert.Equal(expected, CaseDataPolicy.InferInspectionMode(address));
    }

    [Fact]
    public void NormalizeStorageLocationUsesTheCaseTextPolicy()
    {
        var normalized = CaseDataPolicy.Normalize(new(
            StorageLocation: "  14 Storage   Lane\nLeeds  "));

        Assert.Equal("14 Storage Lane Leeds", normalized.StorageLocation);
    }

    [Fact]
    public void NormalizeVehicleYearUsesTheCaseTextPolicy()
    {
        var normalized = CaseDataPolicy.Normalize(new(VehicleYear: "  2016 "));

        Assert.Equal("2016", normalized.VehicleYear);
        Assert.Null(CaseDataPolicy.Normalize(new(VehicleYear: "   ")).VehicleYear);
        Assert.Throws<ArgumentOutOfRangeException>(() => CaseDataPolicy.Normalize(
            new(VehicleYear: new string('9', 11))));
    }

    [Fact]
    public void InspectionAddressChoicesHaveTheD33OrderAndAvailability()
    {
        var choices = InspectionAddressChoices.Resolve(new(
            "1 Claimant Road",
            RepairerAddress: null,
            "3 Storage Way",
            ["2 Previous Street", "1 Older Avenue"]));

        Assert.Equal(
            [
                InspectionAddressChoiceKind.ImageBasedAssessment,
                InspectionAddressChoiceKind.ClaimantAddress,
                InspectionAddressChoiceKind.RepairerLocation,
                InspectionAddressChoiceKind.StorageLocation,
                InspectionAddressChoiceKind.PreviousAddress,
                InspectionAddressChoiceKind.PreviousAddress,
                InspectionAddressChoiceKind.ManualEntry
            ],
            choices.Select(choice => choice.Kind));
        Assert.False(choices[2].IsAvailable);
        Assert.All(choices.Where((_, index) => index != 2), choice => Assert.True(choice.IsAvailable));
    }

    [Fact]
    public void NormalizeRequiresMileageAndUnitTogether()
    {
        Assert.Throws<InvalidOperationException>(() => CaseDataPolicy.Normalize(
            new(VehicleMileage: 72_850)));
        Assert.Throws<InvalidOperationException>(() => CaseDataPolicy.Normalize(
            new(VehicleMileageUnit: "miles")));

        var normalized = CaseDataPolicy.Normalize(
            new(VehicleMileage: 72_850, VehicleMileageUnit: " miles "));

        Assert.Equal(72_850, normalized.VehicleMileage);
        Assert.Equal("miles", normalized.VehicleMileageUnit);
    }

    [Fact]
    public void NormalizeRequiresTheExactValueForImageBasedAssessmentMode()
    {
        Assert.Throws<InvalidOperationException>(() => CaseDataPolicy.Normalize(
            new(
                InspectionAddress: "1 Test Street, London",
                InspectionMode: CaseInspectionMode.ImageBasedAssessment)));
        Assert.Throws<InvalidOperationException>(() => CaseDataPolicy.Normalize(
            new(
                InspectionAddress: "image based assessment",
                InspectionMode: CaseInspectionMode.ImageBasedAssessment)));

        var normalized = CaseDataPolicy.Normalize(
            new(
                InspectionAddress: "Image Based Assessment",
                InspectionMode: CaseInspectionMode.ImageBasedAssessment));
        Assert.Equal("Image Based Assessment", normalized.InspectionAddress);
        Assert.Equal(CaseInspectionMode.ImageBasedAssessment, normalized.InspectionMode);
    }

    [Fact]
    public void NormalizeRejectsTheImageBasedAssessmentValueAsAPhysicalAddress()
    {
        Assert.Throws<InvalidOperationException>(() => CaseDataPolicy.Normalize(
            new(
                InspectionAddress: "Image Based Assessment",
                InspectionMode: CaseInspectionMode.PhysicalAddress)));
        Assert.Throws<InvalidOperationException>(() => CaseDataPolicy.Normalize(
            new(
                InspectionAddress: "IMAGE BASED ASSESSMENT",
                InspectionMode: CaseInspectionMode.PhysicalAddress)));

        var normalized = CaseDataPolicy.Normalize(
            new(
                InspectionAddress: "5 Repairer Way, Leeds",
                InspectionMode: CaseInspectionMode.PhysicalAddress));
        Assert.Equal("5 Repairer Way, Leeds", normalized.InspectionAddress);
        Assert.Equal(CaseInspectionMode.PhysicalAddress, normalized.InspectionMode);
    }

    [Fact]
    public void PhysicalTreatmentRefusesABlankAddressAndTheImageBasedLiteral()
    {
        // Every CE assessment is desktop, so the report-address treatment is
        // stated by the operator and never inferred from the text of an
        // address. A physical vehicle location therefore needs a real address,
        // and the accepted Image Based Assessment instruction is not one.
        Assert.Throws<InvalidOperationException>(() => CaseDataPolicy.ResolveInspection(
            CaseReportAddressTreatment.PhysicalVehicleLocation,
            null));
        Assert.Throws<InvalidOperationException>(() => CaseDataPolicy.ResolveInspection(
            CaseReportAddressTreatment.PhysicalVehicleLocation,
            "   "));
        Assert.Throws<InvalidOperationException>(() => CaseDataPolicy.ResolveInspection(
            CaseReportAddressTreatment.PhysicalVehicleLocation,
            "Image Based Assessment"));
        Assert.Throws<InvalidOperationException>(() => CaseDataPolicy.ResolveInspection(
            CaseReportAddressTreatment.PhysicalVehicleLocation,
            "IMAGE BASED ASSESSMENT"));

        Assert.Equal(
            ("5 Repairer Way, Leeds", CaseInspectionMode.PhysicalAddress),
            CaseDataPolicy.ResolveInspection(
                CaseReportAddressTreatment.PhysicalVehicleLocation,
                " 5 Repairer Way, Leeds "));
    }

    [Fact]
    public void UndeterminedTreatmentSavesNeitherAddressNorMode()
    {
        Assert.Equal(
            (null, (CaseInspectionMode?)null),
            CaseDataPolicy.ResolveInspection(CaseReportAddressTreatment.Undetermined, null));
        Assert.Equal(
            (null, (CaseInspectionMode?)null),
            CaseDataPolicy.ResolveInspection(CaseReportAddressTreatment.Undetermined, "  "));
        Assert.Throws<InvalidOperationException>(() => CaseDataPolicy.ResolveInspection(
            CaseReportAddressTreatment.Undetermined,
            "5 Repairer Way, Leeds"));
    }

    [Fact]
    public void ImageBasedTreatmentAlwaysStoresTheAcceptedInstructionValue()
    {
        Assert.Equal(
            ("Image Based Assessment", CaseInspectionMode.ImageBasedAssessment),
            CaseDataPolicy.ResolveInspection(CaseReportAddressTreatment.ImageBasedAssessment, null));
        Assert.Equal(
            ("Image Based Assessment", CaseInspectionMode.ImageBasedAssessment),
            CaseDataPolicy.ResolveInspection(
                CaseReportAddressTreatment.ImageBasedAssessment,
                " Image Based Assessment "));
        Assert.Throws<InvalidOperationException>(() => CaseDataPolicy.ResolveInspection(
            CaseReportAddressTreatment.ImageBasedAssessment,
            "image based assessment"));
    }

    [Fact]
    public void ZeroOdometerIsAPresentValueNotAnAbsentOne()
    {
        var normalized = CaseDataPolicy.Normalize(
            new(VehicleMileage: 0, VehicleMileageUnit: "miles"));

        Assert.Equal(0, normalized.VehicleMileage);
        Assert.Equal("miles", normalized.VehicleMileageUnit);
        Assert.Throws<InvalidOperationException>(() => CaseDataPolicy.Normalize(
            new(VehicleMileage: 0)));
    }

    [Fact]
    public void TheOdometerDisplayUnitMustBeOneOfTheTwoUnitsTheRecordConvertsBetween()
    {
        Assert.Equal(
            "kilometres",
            CaseDataPolicy.Normalize(new(VehicleMileageDisplayUnit: " KM ")).VehicleMileageDisplayUnit);
        Assert.Throws<ArgumentException>(() => CaseDataPolicy.Normalize(
            new(VehicleMileageDisplayUnit: "furlongs")));
    }

    [Fact]
    public void TheReceivedDateIsTheOriginReceiptsLondonDayOrTheCasesCreation()
    {
        var created = new DateTimeOffset(2026, 7, 3, 9, 0, 0, TimeSpan.Zero);
        // 23:30 UTC on 1 July is 00:30 on 2 July in London (BST).
        Assert.Equal(new DateOnly(2026, 7, 2), CaseDataPolicy.ReceivedDate(new DateTimeOffset(2026, 7, 1, 23, 30, 0, TimeSpan.Zero), created));
        // A manual Case has no receipt: it was received when staff created it.
        Assert.Equal(new DateOnly(2026, 7, 3), CaseDataPolicy.ReceivedDate(null, created));
    }

    [Fact]
    public async Task SaveCaseNormalizesExplicitConfirmedValuesWithoutAnIdentityField()
    {
        var store = new RecordingStore();
        var command = new SaveCase(store);
        var staff = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var request = new SaveCaseRequest(
            Guid.NewGuid(),
            4,
            staff,
            "save-case",
            "Confirmed reviewed values",
            "lease",
            new(
                ClaimantName: "  Jane   Example ",
                VehicleRegistration: " ab 12 cde "));

        await Assert.ThrowsAsync<NotSupportedException>(
            () => command.ExecuteAsync(request, CancellationToken.None));

        Assert.NotNull(store.SavedRequest);
        Assert.Equal("Jane Example", store.SavedRequest.Data.ClaimantName);
        Assert.Equal("AB12CDE", store.SavedRequest.Data.VehicleRegistration);
        Assert.Equal(request.CaseId, store.SavedRequest.CaseId);
    }

    private sealed class RecordingStore : ICaseDataStore
    {
        public SaveCaseRequest? SavedRequest { get; private set; }

        public Task<CaseDataProjection?> GetAsync(
            Guid caseId,
            CaseWorkSelector work, CancellationToken cancellationToken) => Task.FromResult<CaseDataProjection?>(null);

        public Task<CaseDataProjection> SaveAsync(
            SaveCaseRequest request,
            CancellationToken cancellationToken)
        {
            SavedRequest = request;
            throw new NotSupportedException();
        }
    }
}
