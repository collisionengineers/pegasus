using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

/// <summary>
/// The inbox de-clutter policy for a Collision Engineers staff forward: leaked
/// inline-image content-id tokens are removed, and for a staff forward the
/// forwarder's preamble and signature above the quoted original are dropped so
/// the work provider's message is the focus.
/// </summary>
public sealed class StaffForwardBodyCleanerTests
{
    private const string StaffForward =
        "[cid:4931302a-3f0a-4510-af7f-a979337b17ab]\n\n" +
        "Alex Mercer\n" +
        "IT Systems & Automations Developer\n" +
        "Contact: engineers@collisionengineers.co.uk\n\n" +
        "From: Nicholas Duncombe <nduncombe@qdosassist.co.uk>\n" +
        "Sent: 12 August 2026 14:00\n" +
        "To: desk@collisionengineers.co.uk\n" +
        "Subject: (EREF18) RTA on 08/07/2026\n\n" +
        "Please find attached the audit instruction for the vehicle.";

    [Fact]
    public void StripsContentIdTokens()
    {
        var cleaned = StaffForwardBodyCleaner.Clean(
            "See the logo [cid:image001.png@01D] and <cid:image002.png> here.",
            isStaffForward: false);

        Assert.DoesNotContain("cid:", cleaned, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("See the logo", cleaned, StringComparison.Ordinal);
        Assert.Contains("here.", cleaned, StringComparison.Ordinal);
    }

    [Fact]
    public void StaffForwardFocusesTheProviderOriginalAndDropsTheForwarderSignature()
    {
        var cleaned = StaffForwardBodyCleaner.Clean(StaffForward, isStaffForward: true);

        Assert.StartsWith("From: Nicholas Duncombe", cleaned, StringComparison.Ordinal);
        Assert.Contains("Please find attached the audit instruction", cleaned, StringComparison.Ordinal);
        Assert.DoesNotContain("Alex Mercer", cleaned, StringComparison.Ordinal);
        Assert.DoesNotContain("IT Systems & Automations Developer", cleaned, StringComparison.Ordinal);
        Assert.DoesNotContain("cid:", cleaned, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NonStaffForwardKeepsTheBodyButStillStripsContentIds()
    {
        var cleaned = StaffForwardBodyCleaner.Clean(StaffForward, isStaffForward: false);

        // Not a staff forward: no forwarded original is assumed, so the body is
        // preserved (only the content-id token is removed).
        Assert.Contains("Alex Mercer", cleaned, StringComparison.Ordinal);
        Assert.Contains("Please find attached the audit instruction", cleaned, StringComparison.Ordinal);
        Assert.DoesNotContain("cid:", cleaned, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StaffForwardWithoutAForwardedHeaderKeepsTheBody()
    {
        // A staff forward whose body carries no From/Sent/To/Subject boundary
        // (e.g. an attached-original forward whose provider body is already the
        // focus) is left intact apart from content-id removal.
        const string body = "Please review the attached QDOS audit instruction. [cid:x@y]";

        var cleaned = StaffForwardBodyCleaner.Clean(body, isStaffForward: true);

        Assert.Contains("Please review the attached QDOS audit instruction.", cleaned, StringComparison.Ordinal);
        Assert.DoesNotContain("cid:", cleaned, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SplitForwardedHeaderSeparatesTheLeadingBlock()
    {
        const string body = """
            From: Neil Duncombe <n@qdosassist.co.uk>
            Sent: 12 August 2026
            To: Desk <desk@ce.co.uk>
            Subject: (EREF9) RTA

            Neil Duncombe
            Senior Claims Handler
            """;

        var (header, rest) = StaffForwardBodyCleaner.SplitForwardedHeader(body);

        Assert.Equal(4, header.Count);
        Assert.StartsWith("From: Neil Duncombe", header[0], StringComparison.Ordinal);
        Assert.StartsWith("Subject: (EREF9) RTA", header[3], StringComparison.Ordinal);
        Assert.StartsWith("Neil Duncombe", rest, StringComparison.Ordinal);
    }

    [Fact]
    public void SplitForwardedHeaderLeavesABodyWithoutTheLeadingBlockIntact()
    {
        const string body = """
            Good morning

            From: someone quoted later
            Sent: x
            To: y
            Subject: z
            """;

        var (header, rest) = StaffForwardBodyCleaner.SplitForwardedHeader(body);

        Assert.Empty(header);
        Assert.Equal(body, rest);
    }

    [Fact]
    public void ProviderFooterIsTrimmedAtTheEarliestMarkerKeepingTheSignOff()
    {
        // The letter-with-signature shape measured in the corpus: the
        // provider's message and sign-off, then the signature block opening
        // with an image placeholder and running through decorated contact
        // links, membership, registration and the disclaimer.
        const string body = """
            Our Client: Mr Cheddae Singh
            Registration: J16DET

            Please let us have your report as soon as possible.

            Yours faithfully
            Neil Duncombe
            [https://www.qdosassist.co.uk/ASSIST-EMAIL-SIGNATURES/QAA%20Logo%2050.png]
            0800 093 0982<tel:0800%20093%200982>
            nduncombe@qdosassist.co.uk<mailto:nduncombe@qdosassist.co.uk>
            Proud members of:
            You are dealing with QDOS Accident Assistance Limited, registration number 5179995.
            The registered office is C/O Higsons Chartered Accountants.
            This email and any attachments are for the exclusive use of the intended recipient.
            """;

        var trimmed = StaffForwardBodyCleaner.TrimProviderFooter(body);

        Assert.Contains("Our Client: Mr Cheddae Singh", trimmed, StringComparison.Ordinal);
        Assert.Contains("Please let us have your report as soon as possible.", trimmed, StringComparison.Ordinal);
        Assert.EndsWith("Neil Duncombe", trimmed.TrimEnd(), StringComparison.Ordinal);
        Assert.DoesNotContain("You are dealing with", trimmed, StringComparison.Ordinal);
        Assert.DoesNotContain("[https://", trimmed, StringComparison.Ordinal);
        Assert.DoesNotContain("<tel:", trimmed, StringComparison.Ordinal);
    }

    [Fact]
    public void ASignatureOnlyBodyIsShownWhole()
    {
        // Nine corpus bodies are footer from the first line — the live
        // signature-only QDOS instructions. Trimming would leave nothing, so
        // nothing is trimmed.
        const string body = """
            [https://www.qdosassist.co.uk/ASSIST-EMAIL-SIGNATURES/QAA%20Logo%2050.png]
            0800 093 0982<tel:0800%20093%200982>
            You are dealing with QDOS Accident Assistance Limited.
            """;

        Assert.Equal(body, StaffForwardBodyCleaner.TrimProviderFooter(body));
    }

    [Fact]
    public void AMarkerlessBodyIsShownWhole()
    {
        const string body = """
            Please cancel this inspection for vehicle AB12 CDE.

            Thanks,
            Dawn
            """;

        Assert.Equal(body, StaffForwardBodyCleaner.TrimProviderFooter(body));
    }

    /// <summary>
    /// MAIL-011. U34: a QDOS triage instruction and its photograph went
    /// unidentified, and the inbox rendered the message as from the
    /// forwarding desk, because the header block carried a Cc line and the
    /// pattern demanded To: and Subject: be adjacent. This is that block,
    /// as retained.
    /// </summary>
    private const string CopiedForward =
        "Contact: engineers@collisionengineers.co.uk\n\n" +
        "________________________________\n" +
        "From: Robin Anderson <randerson@qdosassist.co.uk>\n" +
        "Sent: 21 August 2026 11:18 PM\n" +
        "To: Desk <desk@collisionengineers.co.uk>\n" +
        "Cc: Qdos NewClaims <NewClaims@qdosassist.co.uk>\n" +
        "Subject: Engineer Triage - Our Claim Reference 47939/1\n\n" +
        "Can you kindly advise if the vehicle would be considered repairable.";

    [Fact]
    public void ReadsTheSenderThroughACopiedRecipientLine()
    {
        Assert.Equal(
            "randerson@qdosassist.co.uk",
            StaffForwardBodyCleaner.ForwardedSenderAddress(CopiedForward));
    }

    [Fact]
    public void ReadsTheSenderThroughBothCopiedRecipientLines()
    {
        var body = CopiedForward.Replace(
            "Subject: Engineer",
            "Bcc: Audit <audit@qdosassist.co.uk>\nSubject: Engineer",
            StringComparison.Ordinal);

        Assert.Equal(
            "randerson@qdosassist.co.uk",
            StaffForwardBodyCleaner.ForwardedSenderAddress(body));
    }

    [Fact]
    public void ACopiedRecipientBelongsToTheHeaderNotTheMessage()
    {
        var (headerLines, body) = StaffForwardBodyCleaner.SplitForwardedHeader(
            StaffForwardBodyCleaner.Clean(CopiedForward, isStaffForward: true));

        Assert.Contains(headerLines, line => line.StartsWith("Cc:", StringComparison.Ordinal));
        Assert.StartsWith("Can you kindly advise", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// The shape is widened, not loosened: the block must still be an address
    /// header that ends in Subject:.
    /// </summary>
    [Fact]
    public void AHeaderWithoutASubjectIsStillNotAForwardedHeader()
    {
        var truncated = CopiedForward[..CopiedForward.IndexOf(
            "Subject:", StringComparison.Ordinal)];

        Assert.Null(StaffForwardBodyCleaner.ForwardedSenderAddress(truncated));
        Assert.Empty(StaffForwardBodyCleaner.SplitForwardedHeader(truncated).HeaderLines);
    }

    [Fact]
    public void EmptyBodyIsReturnedEmpty()
    {
        Assert.Equal(string.Empty, StaffForwardBodyCleaner.Clean(string.Empty, isStaffForward: true));
        Assert.Equal(string.Empty, StaffForwardBodyCleaner.Clean("   \r\n  ", isStaffForward: false));
    }
}
