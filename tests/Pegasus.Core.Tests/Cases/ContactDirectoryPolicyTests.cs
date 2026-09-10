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

    private static SaveContactRequest RequestWith(string? telephone) => new(
        Administrator,
        Guid.NewGuid(),
        ExpectedVersion: 0,
        Name: "Acme Repairs",
        ContactPerson: null,
        Email: null,
        Telephone: telephone,
        Address: null,
        Postcode: null,
        Active: true,
        Roles: [ContactRole.Repairer],
        PrincipalCode: null,
        PrincipalInspectionMode: CaseInspectionMode.PhysicalAddress,
        PrincipalAssociations: [],
        OperationKey: "telephone-policy-test",
        EditLeaseToken: string.Empty);
}
