using Pegasus.Core.Operations;
using Pegasus.Core.Tasks;

namespace Pegasus.Core.Tests.Tasks;

/// <summary>
/// The Case chaser (operator, 5 October 2026): the values a Case supplies to
/// its template, its subject, and the addresses it opens addressed to.
/// </summary>
public sealed class CaseChaserTests
{
    [Fact]
    public void TheFactsSupplyEachChaserPlaceholder()
    {
        var values = new CaseChaserFacts("PK12TMZ", "Images", "Principal Ltd", "Jane Driver").Values();

        Assert.Equal(
            EmailTemplates.Placeholders(EmailTemplatePurpose.CaseChaser).Order(StringComparer.Ordinal),
            values.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("PK12TMZ", values[EmailTemplates.Registration]);
        Assert.Equal("Images", values[EmailTemplates.OutstandingMaterial]);
        Assert.Equal("Principal Ltd", values[EmailTemplates.PrincipalName]);
        Assert.Equal("Jane Driver", values[EmailTemplates.Claimant]);
    }

    [Theory]
    [InlineData("PK12TMZ", "Jane Driver", "PK12TMZ – Jane Driver")]
    [InlineData("PK12TMZ", null, "PK12TMZ")]
    [InlineData(" ", "Jane Driver", "Jane Driver")]
    [InlineData(null, "", "")]
    public void TheSubjectJoinsTheRegistrationAndClaimantThatAreRecorded(
        string? registration,
        string? claimant,
        string expected)
    {
        Assert.Equal(expected, new CaseChaserFacts(registration, null, null, claimant).Subject());
    }

    [Fact]
    public void TheChaserIsAddressedInstructionSenderFirstThenImageSourcesThenRepairer()
    {
        var to = CaseChaserAddressing.To(new(
            "instructions@principal.example",
            ["images@repairer.example", "photos@garage.example"],
            "office@repairer.example"));

        Assert.Equal(
            new[]
            {
                "instructions@principal.example", "images@repairer.example",
                "photos@garage.example", "office@repairer.example"
            },
            to);
    }

    [Fact]
    public void EachAddressIsUsedOnceWhateverItsCase()
    {
        var to = CaseChaserAddressing.To(new(
            "Office@Repairer.example",
            [" office@repairer.example ", "images@repairer.example"],
            "OFFICE@REPAIRER.EXAMPLE"));

        Assert.Equal(new[] { "Office@Repairer.example", "images@repairer.example" }, to);
    }

    [Fact]
    public void AnAddressThatIsNotValidIsLeftOut()
    {
        var to = CaseChaserAddressing.To(new(
            "not an address",
            ["", "images@repairer.example", "   "],
            null));

        Assert.Equal(new[] { "images@repairer.example" }, to);
    }

    [Fact]
    public void ACaseWithNoRecordedAddressOpensUnaddressed()
    {
        Assert.Empty(CaseChaserAddressing.To(CaseChaserRecipients.None));
        Assert.Empty(CaseChaserAddressing.To(new(" ", [null!, ""], "")));
    }
}
