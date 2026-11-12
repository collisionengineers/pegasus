using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

/// <summary>
/// R969: a sender that matched no mail route belongs to a Principal only when
/// the Contacts directory holds exactly one active Principal for its exact
/// domain, or for its whole address on a shared mail domain. Synthetic
/// addresses only.
/// </summary>
public sealed class PrincipalSenderContactsTests
{
    [Theory]
    [InlineData("same mailbox", "desk@first-law.example", "desk@first-law.example", "ONE")]
    [InlineData("another mailbox on the domain", "desk@first-law.example", "fee.earner@first-law.example", "ONE")]
    [InlineData("case and padding", "  Desk@First-Law.EXAMPLE ", "FEE.EARNER@FIRST-LAW.EXAMPLE", "ONE")]
    [InlineData("a subdomain is another domain", "desk@first-law.example", "desk@mail.first-law.example", null)]
    [InlineData("another domain", "desk@first-law.example", "desk@second-law.example", null)]
    [InlineData("a malformed sender", "desk@first-law.example", "first-law.example", null)]
    [InlineData("a blank sender", "desk@first-law.example", "  ", null)]
    [InlineData("a shared domain needs the whole address", "firstlaw@gmail.com", "someone.else@gmail.com", null)]
    [InlineData("a shared domain's own mailbox", "firstlaw@gmail.com", "FirstLaw@gmail.com", "ONE")]
    public void ASenderBelongsToThePrincipalItsDirectoryEmailNames(
        string row, string directoryEmail, string? sender, string? expected)
    {
        Assert.NotEmpty(row);
        var contacts = new PrincipalSenderContacts([(directoryEmail, "ONE")]);

        Assert.Equal(expected, contacts.SingleFor(sender, IntermediaryDomains.Empty));
    }

    [Fact]
    public void TwoPrincipalsOnOneDomainNameNeither()
    {
        var contacts = new PrincipalSenderContacts(
            [("desk@first-law.example", "ONE"), ("accounts@first-law.example", "TWO"), (null, "THREE")]);

        Assert.Null(contacts.SingleFor("fee.earner@first-law.example", IntermediaryDomains.Empty));
    }

    [Fact]
    public void ASenderThatIsAlsoAnIntermediaryForAnotherPrincipalNamesNone()
    {
        var contacts = new PrincipalSenderContacts([("desk@first-law.example", "ONE")]);
        var forItself = new IntermediaryDomains([("claims@first-law.example", "ONE")]);
        var forAnother = new IntermediaryDomains([("claims@first-law.example", "TWO")]);

        Assert.Equal("ONE", contacts.SingleFor("fee.earner@first-law.example", forItself));
        Assert.Null(contacts.SingleFor("fee.earner@first-law.example", forAnother));
    }

    [Theory]
    [InlineData("mail@noreply.collisionengineers.co.uk", true)]
    [InlineData(" Mail@NoReply.CollisionEngineers.co.uk ", true)]
    [InlineData("info@collisionengineers.co.uk", false)]
    [InlineData(null, false)]
    public void TheWebsiteFormIsKnownByItsOneAddress(string? sender, bool expected) =>
        Assert.Equal(expected, WebsiteEnquiryMail.IsFrom(sender));

    [Fact]
    public void AnIntermediaryNamesEveryPrincipalItIsLinkedTo()
    {
        var intermediaries = new IntermediaryDomains(
            [("desk@claims-route.example", "PCH"), ("accounts@claims-route.example", "QDOS")]);

        var codes = intermediaries.PrincipalCodesFor("reports@claims-route.example");
        Assert.Equal(2, codes.Count);
        Assert.Contains("PCH", codes);
        Assert.Contains("QDOS", codes);
        Assert.Empty(intermediaries.PrincipalCodesFor("reports@elsewhere.example"));
    }
}
