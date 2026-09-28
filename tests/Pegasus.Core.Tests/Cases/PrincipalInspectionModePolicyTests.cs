using Pegasus.Core.Cases;

namespace Pegasus.Core.Tests.Cases;

public sealed class PrincipalInspectionModePolicyTests
{
    [Fact]
    public void PolicyIdentityIsStable()
    {
        Assert.Equal("principal-inspection-mode", PrincipalInspectionModePolicy.PolicyKey);
        Assert.Equal(1, PrincipalInspectionModePolicy.PolicyVersion);
    }

    [Theory]
    [InlineData(CaseInspectionMode.PhysicalAddress, "physical_address")]
    [InlineData(CaseInspectionMode.ImageBasedAssessment, "image_based_assessment")]
    public void CodesRoundTrip(CaseInspectionMode mode, string code)
    {
        Assert.Equal(code, PrincipalInspectionModePolicy.ToCode(mode));
        Assert.Equal(mode, PrincipalInspectionModePolicy.Parse(code));
    }

    [Fact]
    public void UnknownValuesFailClosed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PrincipalInspectionModePolicy.ToCode((CaseInspectionMode)99));
        Assert.Throws<InvalidDataException>(
            () => PrincipalInspectionModePolicy.Parse("image based"));
    }
}
