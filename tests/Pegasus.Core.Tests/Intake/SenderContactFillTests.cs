using Pegasus.Core.Cases;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

/// <summary>
/// A new Case takes its contact from the instruction e-mail: the sender's
/// name and e-mail first, the signature's telephone number second (operator,
/// 28 October 2026; R462). The signature shapes are the corpus instructions'.
/// </summary>
public sealed class SenderContactFillTests
{
    private const string Sender = "r.pownall@dfd-solicitors.example";

    private const string SignedBody = """
        Please find attached our instruction for Mr Oxley.
        Mobile Number 07392 995 761

        Kind regards,
        Richard Pownall
        Davison Flynn Duke Solicitors
        Tel: 0151 513 3333
        Direct: 07834 386 978
        """;

    [Fact]
    public void TheSenderGivesTheNameEmailAndTheSignatureTelephone()
    {
        var values = SenderContactFill.CaseDataValues(Sender, Sender, "Richard Pownall", SignedBody);

        Assert.Equal(
            new Dictionary<string, string>
            {
                [CaseDataFieldNames.ContactName] = "Richard Pownall",
                [CaseDataFieldNames.ContactEmailAddress] = Sender,
                [CaseDataFieldNames.ContactPhoneNumber] = "0151 513 3333"
            },
            values);
    }

    [Fact]
    public void NoProvedSenderOffersNothing()
    {
        Assert.Empty(SenderContactFill.CaseDataValues(null, Sender, "Richard Pownall", SignedBody));
    }

    [Fact]
    public void AStaffForwardOffersTheOriginalSendersEmailOnly()
    {
        var values = SenderContactFill.CaseDataValues(
            Sender, "desk@collisionengineers.co.uk", "Desk", SignedBody);

        Assert.Equal(
            new Dictionary<string, string> { [CaseDataFieldNames.ContactEmailAddress] = Sender },
            values);
    }

    [Fact]
    public void ADisplayNameThatIsTheAddressIsNotAName()
    {
        var values = SenderContactFill.CaseDataValues(Sender, Sender, Sender.ToUpperInvariant(), null);

        Assert.Equal(
            new Dictionary<string, string> { [CaseDataFieldNames.ContactEmailAddress] = Sender },
            values);
    }

    [Fact]
    public void ANumberAboveTheSignOffIsNeverTheSignatureTelephone()
    {
        const string body = """
            Please call the claimant on 07392 995 761 to book the inspection.

            Many thanks
            """;

        Assert.Null(SenderContactFill.SignatureTelephone(body));
    }

    [Fact]
    public void ABodyWithNoSignOffHasNoSignatureTelephone()
    {
        Assert.Null(SenderContactFill.SignatureTelephone("Tel: 0151 513 3333"));
    }

    [Fact]
    public void TheOutlookLinkDecorationIsNotPartOfTheNumber()
    {
        const string body = """
            Yours faithfully
            Samuel Berlyne
            [https://www.qdosassist.example/ASSIST-EMAIL-SIGNATURES/Phone%2050.png]
            0800 093 0982<tel:0800%20093%200982>
            """;

        Assert.Equal("0800 093 0982", SenderContactFill.SignatureTelephone(body));
    }

    [Fact]
    public void AnInternationalNumberIsReadAsPrinted()
    {
        const string body = """
            Kind Regards
            Administration Team
            T: +44 (0) 1204 659 191 | F: +44 (0) 1204 659 214
            """;

        Assert.Equal("+44 (0) 1204 659 191", SenderContactFill.SignatureTelephone(body));
    }

    [Fact]
    public void AnEarlierMessageBelowTheSendersOwnIsNotRead()
    {
        const string body = """
            Thanks.

            From: Claims <claims@principal.example>
            Sent: 12 August 2026 14:00
            To: desk@collisionengineers.co.uk
            Subject: New instruction
            Tel: 0161 904 4660
            """;

        Assert.Null(SenderContactFill.SignatureTelephone(body));
    }
}
