using Pegasus.Core.Cases;

namespace Pegasus.Core.Tests.Cases;

public sealed class ProviderInspectionModePolicyTests
{
    [Fact]
    public void PolicyIdentityIsStable()
    {
        Assert.Equal("provider-inspection-mode", ProviderInspectionModePolicy.PolicyKey);
        Assert.Equal(1, ProviderInspectionModePolicy.PolicyVersion);
    }

    [Theory]
    [InlineData(CaseInspectionMode.PhysicalAddress, "physical_address")]
    [InlineData(CaseInspectionMode.ImageBasedAssessment, "image_based_assessment")]
    public void CodesRoundTrip(CaseInspectionMode mode, string code)
    {
        Assert.Equal(code, ProviderInspectionModePolicy.ToCode(mode));
        Assert.Equal(mode, ProviderInspectionModePolicy.Parse(code));
    }

    [Fact]
    public void UnknownValuesFailClosed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ProviderInspectionModePolicy.ToCode((CaseInspectionMode)99));
        Assert.Throws<InvalidDataException>(
            () => ProviderInspectionModePolicy.Parse("image based"));
    }
}
