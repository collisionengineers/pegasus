using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Tests.Cases;

/// <summary>
/// D7: a telephone accepts digits, spaces and a single leading <c>+</c> (UK
/// numbers start with 0 and are written with spaces); letters and other
/// punctuation are refused as a named field error rather than silently
/// dropped.
/// </summary>
public sealed class ContactDirectoryPolicyTests
{
    private static readonly ActionActor Administrator =
        ActionActor.Staff(Guid.Parse("2f7f6f0a-2f0a-4e3c-9e3a-7a4a5a6b7c8d"), [StaffRole.Administrator]);

    [Theory]
    [InlineData("01234 567890", "01234 567890")]
    [InlineData("+44 1234 567890", "+44 1234 567890")]
    [InlineData("  01234   567890  ", "01234 567890")]
    [InlineData("01234\t567890", "01234 567890")]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    public void AcceptedTelephonesAreTrimmedAndCollapsed(string? input, string? expected)
    {
        var normalized = ContactDirectoryPolicy.Normalize(RequestWith(input));

        Assert.Equal(expected, normalized.Telephone);
    }

    [Theory]
    [InlineData("01234 567ABC")]
    [InlineData("phone me")]
    [InlineData("01234-567890")]
    [InlineData("+44 (1234) 567890")]
    [InlineData("++441234567890")]
    public void RejectedTelephonesFailAsANamedFieldError(string input)
    {
        var exception = Assert.Throws<ArgumentException>(() => ContactDirectoryPolicy.Normalize(RequestWith(input)));

        Assert.Equal("Telephone", exception.ParamName);
        Assert.StartsWith("Telephone must be digits.", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-5, 1)]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(7, 7)]
    [InlineData(int.MaxValue, ContactDirectoryPolicy.MaximumPage)]
    public void ThePageNumberIsClampedIntoRange(int requested, int expected)
    {
        Assert.Equal(expected, ContactDirectoryPolicy.NormalizePage(requested));
    }

    private static SaveContactRequest RequestWith(string? telephone) => new(
        Administrator,
        Guid.NewGuid(),
        ExpectedVersion: 0,
        Name: "Acme Repairs",
        Email: null,
        Telephone: telephone,
        Address: null,
        Postcode: null,
        Active: true,
        Role: ContactRole.Repairer,
        PrincipalCode: null,
        PrincipalInspectionMode: CaseInspectionMode.PhysicalAddress,
        LinkedPrincipalIds: [],
        OperationKey: "telephone-policy-test");
}

public sealed class ContactNotesOnEveryCaseTests
{
    private static readonly ActionActor Administrator =
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);

    [Fact]
    public void AClaimSourceKeepsItsNotesAndOtherContactTypesDoNot()
    {
        var claimSource = ContactDirectoryPolicy.Normalize(Request(ContactRole.ClaimSource, "  Ask for the policy number.  "));
        var repairer = ContactDirectoryPolicy.Normalize(Request(ContactRole.Repairer, "Ask for the policy number."));

        Assert.Equal("Ask for the policy number.", claimSource.NotesOnEveryCase);
        Assert.Null(repairer.NotesOnEveryCase);
        Assert.Null(ContactDirectoryPolicy.Normalize(Request(ContactRole.ClaimSource, " ")).NotesOnEveryCase);
        Assert.ThrowsAny<ArgumentException>(() =>
            ContactDirectoryPolicy.Normalize(Request(ContactRole.ClaimSource, new string('x', 2001))));
    }

    [Fact]
    public void OnlyANonPrincipalContactLinksToPrincipalsAndEachOnce()
    {
        var principalId = Guid.NewGuid();

        Assert.Equal(
            [principalId],
            ContactDirectoryPolicy.Normalize(Request(ContactRole.Repairer, null) with { LinkedPrincipalIds = [principalId] }).LinkedPrincipalIds);
        Assert.Throws<ArgumentException>(() => ContactDirectoryPolicy.Normalize(
            Request(ContactRole.Principal, null) with { PrincipalCode = "ACME", LinkedPrincipalIds = [principalId] }));
        Assert.Throws<ArgumentException>(() => ContactDirectoryPolicy.Normalize(
            Request(ContactRole.Repairer, null) with { LinkedPrincipalIds = [principalId, principalId] }));
    }

    private static SaveContactRequest Request(ContactRole role, string? notes) => new(
        Administrator,
        Guid.NewGuid(),
        ExpectedVersion: 0,
        Name: "Acme Claims",
        Email: null,
        Telephone: null,
        Address: null,
        Postcode: null,
        Active: true,
        Role: role,
        PrincipalCode: null,
        PrincipalInspectionMode: CaseInspectionMode.PhysicalAddress,
        LinkedPrincipalIds: [],
        OperationKey: "notes-policy-test",
        NotesOnEveryCase: notes);
}

/// <summary>
/// A Principal contact's Save carries the Principal's Case defaults (operator,
/// 25 November 2026; R1266, R1267): the default fee, the default inspection
/// location for a physical address, and the Send from mailbox. No other
/// contact type takes them.
/// </summary>
public sealed class ContactPrincipalCaseDefaultsTests
{
    private static readonly ActionActor Administrator =
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);

    [Fact]
    public void APrincipalKeepsItsCaseDefaultsTrimmed()
    {
        var normalized = ContactDirectoryPolicy.Normalize(Principal() with
        {
            DefaultFee = 215.50m,
            DefaultInspectionLocationLabel = "  Yard  ",
            DefaultInspectionAddress = "  1 Test Street  ",
            DefaultInspectionPostcode = "  TE1 1ST  ",
            SendFromMailbox = " engineers@collisionengineers.co.uk "
        });

        Assert.Equal(215.50m, normalized.DefaultFee);
        Assert.Equal("Yard", normalized.DefaultInspectionLocationLabel);
        Assert.Equal("1 Test Street", normalized.DefaultInspectionAddress);
        Assert.Equal("TE1 1ST", normalized.DefaultInspectionPostcode);
        Assert.Equal("engineers@collisionengineers.co.uk", normalized.SendFromMailbox);
    }

    [Fact]
    public void APhysicalAddressMayHaveNoDefaultLocationAndABlankSendFromIsNone()
    {
        var normalized = ContactDirectoryPolicy.Normalize(Principal() with
        {
            DefaultInspectionLocationLabel = " ",
            DefaultInspectionAddress = "",
            DefaultInspectionPostcode = null,
            SendFromMailbox = "  "
        });

        Assert.Null(normalized.DefaultInspectionLocationLabel);
        Assert.Null(normalized.DefaultInspectionAddress);
        Assert.Null(normalized.DefaultInspectionPostcode);
        Assert.Null(normalized.SendFromMailbox);
    }

    [Fact]
    public void ImageBasedAssessmentClearsTheDefaultLocation()
    {
        var normalized = ContactDirectoryPolicy.Normalize(Principal() with
        {
            PrincipalInspectionMode = CaseInspectionMode.ImageBasedAssessment,
            DefaultInspectionLocationLabel = "Yard",
            DefaultInspectionAddress = "1 Test Street",
            DefaultInspectionPostcode = "TE1 1ST"
        });

        Assert.Equal(CaseInspectionMode.ImageBasedAssessment, normalized.PrincipalInspectionMode);
        Assert.Null(normalized.DefaultInspectionLocationLabel);
        Assert.Null(normalized.DefaultInspectionAddress);
        Assert.Null(normalized.DefaultInspectionPostcode);
    }

    [Theory]
    [InlineData("label", "Yard", null)]
    [InlineData("postcode", null, "TE1 1ST")]
    public void ALabelOrPostcodeWithoutAnAddressIsRefused(string row, string? label, string? postcode)
    {
        Assert.NotEmpty(row);
        var error = Assert.Throws<ArgumentException>(() => ContactDirectoryPolicy.Normalize(Principal() with
        {
            DefaultInspectionLocationLabel = label,
            DefaultInspectionPostcode = postcode
        }));

        Assert.Equal(nameof(SaveContactRequest.DefaultInspectionAddress), error.ParamName);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("180.005")]
    public void ADefaultFeeTheAgreedFeeWouldRefuseIsRefused(string fee)
    {
        var request = Principal() with { DefaultFee = decimal.Parse(fee, System.Globalization.CultureInfo.InvariantCulture) };

        var error = Assert.ThrowsAny<ArgumentException>(() => ContactDirectoryPolicy.Normalize(request));
        Assert.Equal(nameof(SaveContactRequest.DefaultFee), error.ParamName);
        Assert.ThrowsAny<ArgumentException>(() => ContactDirectoryPolicy.PrincipalDefaultFee(request, creatingPrincipal: true));
    }

    [Theory]
    [InlineData("not an address")]
    [InlineData("a@")]
    public void AnInvalidSendFromIsRefused(string address)
    {
        var error = Assert.Throws<ArgumentException>(() =>
            ContactDirectoryPolicy.Normalize(Principal() with { SendFromMailbox = address }));

        Assert.Equal(nameof(SaveContactRequest.SendFromMailbox), error.ParamName);
    }

    [Theory]
    [InlineData("fee")]
    [InlineData("label")]
    [InlineData("address")]
    [InlineData("postcode")]
    [InlineData("send from")]
    public void AnotherContactTypeTakesNoPrincipalCaseDefault(string field)
    {
        var repairer = Principal() with { Role = ContactRole.Repairer, PrincipalCode = null };
        var request = field switch
        {
            "fee" => repairer with { DefaultFee = 180m },
            "label" => repairer with { DefaultInspectionLocationLabel = "Yard" },
            "address" => repairer with { DefaultInspectionAddress = "1 Test Street" },
            "postcode" => repairer with { DefaultInspectionPostcode = "TE1 1ST" },
            _ => repairer with { SendFromMailbox = "engineers@collisionengineers.co.uk" }
        };

        Assert.Throws<ArgumentException>(() => ContactDirectoryPolicy.Normalize(request));
        // With none of them the same contact saves.
        Assert.Null(ContactDirectoryPolicy.Normalize(repairer).DefaultFee);
    }

    /// <summary>
    /// A new Principal that posts no fee takes the standard one (the New
    /// Principal dialog posts none); an existing Principal must post one.
    /// </summary>
    [Fact]
    public void TheFeeASaveWritesIsThePostedOneOrForANewPrincipalTheStandard()
    {
        Assert.Equal(215m, ContactDirectoryPolicy.PrincipalDefaultFee(Principal() with { DefaultFee = 215m }, creatingPrincipal: false));
        Assert.Equal(215m, ContactDirectoryPolicy.PrincipalDefaultFee(Principal() with { DefaultFee = 215m }, creatingPrincipal: true));
        Assert.Equal(
            PrincipalDefaultFeePolicy.Standard,
            ContactDirectoryPolicy.PrincipalDefaultFee(Principal(), creatingPrincipal: true));

        var error = Assert.Throws<ArgumentException>(() =>
            ContactDirectoryPolicy.PrincipalDefaultFee(Principal(), creatingPrincipal: false));
        Assert.Equal(nameof(SaveContactRequest.DefaultFee), error.ParamName);
    }

    private static SaveContactRequest Principal() => new(
        Administrator,
        Guid.NewGuid(),
        ExpectedVersion: 0,
        Name: "Acme Claims",
        Email: null,
        Telephone: null,
        Address: null,
        Postcode: null,
        Active: true,
        Role: ContactRole.Principal,
        PrincipalCode: "ACME",
        PrincipalInspectionMode: CaseInspectionMode.PhysicalAddress,
        LinkedPrincipalIds: [],
        OperationKey: "case-defaults-test");
}
