using Pegasus.Core.Cases;
using Pegasus.Web.Presentation;

namespace Pegasus.IntegrationTests;

/// <summary>
/// R1013, R1015, R1016: a Case's Claim Source and Repairer lists draw the
/// contacts linked to its Principal first, under that Principal's code, then
/// the others under their own heading; with none linked the list is plain.
/// </summary>
public sealed class LinkedContactChoicesTests
{
    private const string Others = "Other claim sources";

    private static readonly ContactChoice Alpha = Choice("Alpha Claims");
    private static readonly ContactChoice Bravo = Choice("Bravo Claims", "QDOS", "PCH");
    private static readonly ContactChoice Charlie = Choice("Charlie Claims", "PCH");
    private static readonly ContactChoice Delta = Choice("Delta Claims", "QDOS");

    [Fact]
    public void TheLinkedContactsComeFirstUnderThePrincipalsCodeEachPartInItsGivenOrder()
    {
        var groups = LinkedContactChoices.For([Alpha, Bravo, Charlie, Delta], " qdos ", Others);

        Assert.Equal(2, groups.Count);
        Assert.Equal("QDOS", groups[0].Label);
        Assert.Equal(new[] { Bravo, Delta }, groups[0].Choices);
        Assert.Equal(Others, groups[1].Label);
        Assert.Equal(new[] { Alpha, Charlie }, groups[1].Choices);
    }

    [Theory]
    [InlineData("KBS")]
    [InlineData("")]
    [InlineData(null)]
    public void APrincipalWithNoLinkedContactKeepsTheOnePlainList(string? principalCode)
    {
        var group = Assert.Single(LinkedContactChoices.For([Alpha, Bravo, Charlie, Delta], principalCode, Others));

        Assert.Null(group.Label);
        Assert.Equal(new[] { Alpha, Bravo, Charlie, Delta }, group.Choices);
    }

    [Fact]
    public void WhenEveryContactIsLinkedThereIsNoOthersHeading()
    {
        var group = Assert.Single(LinkedContactChoices.For([Bravo, Delta], "QDOS", Others));

        Assert.Equal("QDOS", group.Label);
        Assert.Equal(new[] { Bravo, Delta }, group.Choices);
    }

    private static ContactChoice Choice(string name, params string[] linkedPrincipalCodes) =>
        new(
            Guid.NewGuid(), name, ContactRole.ClaimSource, 1, null, null, null, null, null,
            linkedPrincipalCodes);
}
