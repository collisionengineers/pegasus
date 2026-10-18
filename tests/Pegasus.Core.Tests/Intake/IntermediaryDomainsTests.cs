using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

/// <summary>
/// R35, R17, R137: a linked Claim Source's e-mail makes its exact domain an
/// intermediary for that Principal; on a shared mail domain only that mailbox
/// is. Synthetic addresses only.
/// </summary>
public sealed class IntermediaryDomainsTests
{
    [Theory]
    [InlineData("valid", "desk@claims-route.example", "reports@claims-route.example", true)]
    [InlineData("upper-case", "Desk@Claims-Route.EXAMPLE", "reports@claims-route.example", true)]
    [InlineData("upper-case-sender", "desk@claims-route.example", "REPORTS@CLAIMS-ROUTE.EXAMPLE", true)]
    [InlineData("padded", "  desk@claims-route.example  ", "reports@claims-route.example", true)]
    [InlineData("blank", "   ", "reports@claims-route.example", false)]
    [InlineData("malformed", "claims-route.example", "reports@claims-route.example", false)]
    [InlineData("display-name", "Desk <desk@claims-route.example>", "reports@claims-route.example", false)]
    [InlineData("two-addresses", "desk@claims-route.example; other@claims-route.example", "reports@claims-route.example", false)]
    [InlineData("subdomain-sender", "desk@claims-route.example", "reports@mail.claims-route.example", false)]
    [InlineData("subdomain-contact", "desk@mail.claims-route.example", "reports@claims-route.example", false)]
    [InlineData("suffix-sender", "desk@claims-route.example", "reports@claims-route.example.org", false)]
    [InlineData("malformed-sender", "desk@claims-route.example", "claims-route.example", false)]
    public void AContactEmailNamesItsExactDomain(string row, string? email, string sender, bool expected)
    {
        Assert.NotEmpty(row);
        var intermediaries = new IntermediaryDomains([(email, "QCL")]);

        Assert.Equal(expected, intermediaries.IsIntermediaryFor(sender, "QCL"));
    }

    [Fact]
    public void ANullEmailNamesNothing()
    {
        var intermediaries = new IntermediaryDomains([(null, "QCL")]);

        Assert.False(intermediaries.IsIntermediaryFor("reports@claims-route.example", "QCL"));
    }

    [Fact]
    public void ADomainMayBeAnIntermediaryForSeveralPrincipals()
    {
        var intermediaries = new IntermediaryDomains(
            [("desk@claims-route.example", "PCH"), ("accounts@claims-route.example", "QDOS")]);

        Assert.True(intermediaries.IsIntermediaryFor("reports@claims-route.example", "PCH"));
        Assert.True(intermediaries.IsIntermediaryFor("reports@claims-route.example", "QDOS"));
        Assert.False(intermediaries.IsIntermediaryFor("reports@claims-route.example", "QCL"));
        // The Principal code is matched exactly.
        Assert.False(intermediaries.IsIntermediaryFor("reports@claims-route.example", "pch"));
    }

    [Theory]
    [InlineData("same-mailbox", "claims.route@gmail.com", true)]
    [InlineData("same-mailbox-case", "Claims.Route@GMAIL.com", true)]
    [InlineData("other-mailbox", "someone.else@gmail.com", false)]
    [InlineData("other-shared-domain", "claims.route@hotmail.com", false)]
    public void ASharedMailDomainMatchesOnlyTheContactsMailbox(string row, string sender, bool expected)
    {
        Assert.NotEmpty(row);
        var intermediaries = new IntermediaryDomains([("claims.route@gmail.com", "YML")]);

        Assert.Equal(expected, intermediaries.IsIntermediaryFor(sender, "YML"));
    }

    [Fact]
    public void EmptyIsAnIntermediaryForNothing()
    {
        Assert.False(IntermediaryDomains.Empty.IsIntermediaryFor("instructions@connexus.co.uk", "PCH"));
        Assert.False(IntermediaryDomains.Empty.IsIntermediaryFor("claims.route@gmail.com", "YML"));
    }
}
