using Pegasus.Core.Address;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Tests.Address;

/// <summary>
/// A reviewed inspection location as the writer accepts it. FRD-06: the
/// pipeline keeps "a complete display address with a normalised postcode";
/// whether an address is full is the register review's decision.
/// </summary>
public sealed class InspectionLocationPolicyTests
{
    private static readonly ActionActor Administrator =
        ActionActor.Staff(Guid.Parse("6a1f0c2e-3b4d-4e5f-8a9b-0c1d2e3f4a5b"), [StaffRole.Administrator]);

    private static readonly Guid PrincipalId = Guid.Parse("1c2d3e4f-5a6b-4c7d-8e9f-0a1b2c3d4e5f");

    [Theory]
    [InlineData("ol13qr", "OL1 3QR")]
    [InlineData("EX16 7HZ", "EX16 7HZ")]
    [InlineData("  sw1a   1aa ", "SW1A 1AA")]
    [InlineData("M11AE", "M1 1AE")]
    [InlineData("b 3 3 8 t h", "B33 8TH")]
    [InlineData("bfpo  1234", "BFPO 1234")]
    [InlineData("not\ta postcode", "NOT A POSTCODE")]
    public void NormalizePostcodeSetsOneSpaceBeforeTheLastThree(string postcode, string expected)
    {
        Assert.Equal(expected, InspectionLocationPolicy.NormalizePostcode(postcode));
    }

    [Fact]
    public void NormalizeRefusesBlankFieldsADefaultReviewDateAndRepeatedPrincipals()
    {
        var normalized = InspectionLocationPolicy.Normalize(Request() with
        {
            Name = "  Test Body Centre ",
            Address = " 1 Test Street, Testtown ",
            Postcode = " te1 1st "
        });
        Assert.Equal("Test Body Centre", normalized.Name);
        Assert.Equal("1 Test Street, Testtown", normalized.Address);
        Assert.Equal("TE1 1ST", normalized.Postcode);
        Assert.Equal("register", normalized.Source);
        Assert.Equal([PrincipalId], normalized.PrincipalIds);

        foreach (var refused in new[]
        {
            Request() with { Name = " " },
            Request() with { Address = "" },
            Request() with { Postcode = "  " },
            Request() with { Source = "" },
            Request() with { SourceRecordId = " " },
            Request() with { OperationKey = "" },
            Request() with { ReviewedAtUtc = default },
            Request() with { Name = new string('N', ContactDirectoryPolicy.MaximumNameLength + 1) },
            Request() with { Address = new string('A', ContactDirectoryPolicy.MaximumAddressLength + 1) },
            Request() with { SourceRecordId = new string('R', InspectionLocationPolicy.MaximumSourceLength + 1) },
            Request() with { PrincipalIds = [Guid.Empty] },
            Request() with { PrincipalIds = [PrincipalId, PrincipalId] }
        })
        {
            Assert.ThrowsAny<ArgumentException>(() => InspectionLocationPolicy.Normalize(refused));
        }
    }

    [Fact]
    public void OnlyAnAdministratorMaySave()
    {
        Assert.Throws<StaffAuthorizationException>(() => InspectionLocationPolicy.Normalize(Request() with
        {
            Actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User])
        }));
        Assert.Throws<StaffAuthorizationException>(() => InspectionLocationPolicy.Normalize(Request() with
        {
            Actor = ActionActor.Automation("automation-actor")
        }));
    }

    private static SaveInspectionLocationRequest Request() => new(
        Administrator,
        "Test Body Centre",
        "1 Test Street, Testtown",
        "TE1 1ST",
        Active: true,
        "register",
        "row-1",
        new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero),
        [PrincipalId],
        "inspection-location:row-1");
}
