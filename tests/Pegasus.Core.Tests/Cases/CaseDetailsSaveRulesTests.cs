using Pegasus.Core.Cases;

namespace Pegasus.Core.Tests.Cases;

/// <summary>
/// The rules the Case page's save and pegasus_case_update_details share when
/// they rebuild a Case's sections (R1042).
/// </summary>
public sealed class CaseDetailsSaveRulesTests
{
    private static readonly Guid HeldId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly CaseWorkspaceRepairer Held = new(HeldId, 4, "Held Motors");

    [Fact]
    public void NamingTheHeldRepairerKeepsItsLinkAndCopyWithoutReadingTheDirectory()
    {
        // The held link may since have been made inactive: it is kept, never read again (R1022).
        Assert.True(CaseDetailsSaveRules.KeepsRepairerLink(Held, HeldId));
        Assert.Null(CaseDetailsSaveRules.RepairerToLink(Held, HeldId));
        Assert.Equal(
            new CaseWorkspaceRepairer(HeldId, 4, "Typed for this Case"),
            CaseDetailsSaveRules.Repairer(Held, null, unlinked: false, "Typed for this Case"));
    }

    [Fact]
    public void NamingAnotherRepairerReadsItAndCopiesItsIdentityVersionAndName()
    {
        Assert.False(CaseDetailsSaveRules.KeepsRepairerLink(Held, OtherId));
        Assert.Equal(OtherId, CaseDetailsSaveRules.RepairerToLink(Held, OtherId));
        Assert.Equal(OtherId, CaseDetailsSaveRules.RepairerToLink(null, OtherId));

        var linked = Choice(OtherId, ContactRole.Repairer, 9, "Other Motors");
        Assert.Equal(
            new CaseWorkspaceRepairer(OtherId, 9, "Other Motors"),
            CaseDetailsSaveRules.Repairer(Held, linked, unlinked: false, "Typed for this Case"));
    }

    [Fact]
    public void UnlinkingDropsTheLinkAndKeepsTheTypedName()
    {
        Assert.Null(CaseDetailsSaveRules.RepairerToLink(Held, null));
        Assert.False(CaseDetailsSaveRules.KeepsRepairerLink(Held, null));
        Assert.Equal(
            new CaseWorkspaceRepairer(null, null, "Typed for this Case"),
            CaseDetailsSaveRules.Repairer(Held, null, unlinked: true, "Typed for this Case"));
    }

    [Fact]
    public void TheSameClaimSourceKeepsItsSnapshotAndAnotherIsCopiedWithItsContact()
    {
        var held = new CaseWorkspaceClaimSource(HeldId, 2, "Held Claims", "Ann", "0100", "ann@example.test");

        Assert.Null(CaseDetailsSaveRules.ClaimSourceToCopy(held, HeldId));
        Assert.Null(CaseDetailsSaveRules.ClaimSourceToCopy(held, null));
        Assert.Equal(OtherId, CaseDetailsSaveRules.ClaimSourceToCopy(held, OtherId));
        Assert.Equal(
            new CaseWorkspaceClaimSource(OtherId, 3, "Other Claims", "Bob", "0200", "bob@example.test"),
            CaseDetailsSaveRules.ClaimSourceCopy(
                Choice(OtherId, ContactRole.ClaimSource, 3, "Other Claims") with
                {
                    ContactPerson = "Bob",
                    Telephone = "0200",
                    Email = "bob@example.test",
                }));
    }

    [Fact]
    public void TheInspectionTreatmentIsTheNamedModeThenTheRecordedOneThenTheAddress()
    {
        const CaseReportAddressTreatment recorded = CaseReportAddressTreatment.PhysicalVehicleLocation;

        Assert.Equal(
            CaseReportAddressTreatment.ImageBasedAssessment,
            CaseDetailsSaveRules.InspectionTreatment(
                CaseInspectionMode.ImageBasedAssessment, addressNamed: true, recorded, "1 Garage Lane"));
        Assert.Equal(
            recorded,
            CaseDetailsSaveRules.InspectionTreatment(null, addressNamed: false, recorded, "Image Based Assessment"));
        Assert.Equal(
            CaseReportAddressTreatment.ImageBasedAssessment,
            CaseDetailsSaveRules.InspectionTreatment(null, addressNamed: true, recorded, "Image Based Assessment"));
        Assert.Equal(
            CaseReportAddressTreatment.Undetermined,
            CaseDetailsSaveRules.InspectionTreatment(null, addressNamed: false, null, null));
    }

    [Fact]
    public void AMileageWithoutAUnitIsInMilesAndNoMileageHasNoUnit()
    {
        Assert.Equal(CaseOdometerUnit.Miles, CaseDetailsSaveRules.MileageUnit(42_000, null));
        Assert.Equal(CaseOdometerUnit.Kilometres, CaseDetailsSaveRules.MileageUnit(42_000, CaseOdometerUnit.Kilometres));
        Assert.Null(CaseDetailsSaveRules.MileageUnit(null, CaseOdometerUnit.Kilometres));
    }

    [Fact]
    public void TheRecordedReportDateIsReadAsAnIsoDate()
    {
        Assert.Equal(new DateOnly(2026, 11, 13), CaseDetailsSaveRules.RecordedReportDate("2026-11-13"));
        Assert.Null(CaseDetailsSaveRules.RecordedReportDate("13/11/2026"));
        Assert.Null(CaseDetailsSaveRules.RecordedReportDate(null));
    }

    private static ContactChoice Choice(Guid id, ContactRole role, long version, string name) =>
        new(id, name, role, version, null, null, null, $"{name} address", null, null);
}
