using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Tests.Cases;

/// <summary>
/// EXT-19/S13: the Claim Source directory policy — Administrator-only,
/// expected-version, reasoned, idempotent, and never merged with principal,
/// sender, insurer or third-party engineer roles.
/// </summary>
public sealed class ClaimSourceAdministrationTests
{
    private static readonly ActionActor Administrator =
        ActionActor.Staff(Guid.Parse("2f6c9f2a-8f0e-4e2b-9a2a-6f2c8f2a1a1a"), [StaffRole.Administrator]);
    private static readonly ActionActor Engineer =
        ActionActor.Staff(Guid.Parse("2f6c9f2a-8f0e-4e2b-9a2a-6f2c8f2a1a1b"), [StaffRole.Engineer]);

    private static SaveClaimSourceRequest Request(
        ActionActor? actor = null,
        Guid? id = null,
        long expectedVersion = 0,
        string name = " Acme Recovery Ltd ",
        string? contactName = "  Jane Doe  ",
        string? telephone = " 01234 567890 ",
        string? email = " jane@acme.example ",
        string? notes = "  Preferred out-of-hours contact  ",
        bool active = true,
        string reason = " Initial creation ",
        string operationKey = "claim-source:test:1") =>
        new(
            actor ?? Administrator,
            id ?? Guid.NewGuid(),
            expectedVersion,
            name,
            contactName,
            telephone,
            email,
            notes,
            active,
            reason,
            operationKey);

    [Fact]
    public void NormalizeTrimsEveryTextFieldAndTreatsBlankOptionalFieldsAsNull()
    {
        var normalized = ClaimSourceAdministrationPolicy.Normalize(Request(notes: "   "));

        Assert.Equal("Acme Recovery Ltd", normalized.Name);
        Assert.Equal("Jane Doe", normalized.ContactName);
        Assert.Equal("01234 567890", normalized.Telephone);
        Assert.Equal("jane@acme.example", normalized.Email);
        Assert.Null(normalized.Notes);
        Assert.Equal("Initial creation", normalized.Reason);
    }

    [Fact]
    public void NormalizeRequiresAnAdministratorActor()
    {
        Assert.Throws<StaffAuthorizationException>(() =>
            ClaimSourceAdministrationPolicy.Normalize(Request(actor: Engineer)));
    }

    [Fact]
    public void NormalizeRejectsAnEmptyStableIdentifier()
    {
        Assert.Throws<ArgumentException>(() =>
            ClaimSourceAdministrationPolicy.Normalize(Request(id: Guid.Empty)));
    }

    [Fact]
    public void NormalizeRejectsANegativeExpectedVersion()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ClaimSourceAdministrationPolicy.Normalize(Request(expectedVersion: -1)));
    }

    [Fact]
    public void NormalizeRejectsAnEmptyName()
    {
        Assert.Throws<ArgumentException>(() =>
            ClaimSourceAdministrationPolicy.Normalize(Request(name: "   ")));
    }

    [Fact]
    public void NormalizeRejectsAnEmptyReason()
    {
        Assert.Throws<ArgumentException>(() =>
            ClaimSourceAdministrationPolicy.Normalize(Request(reason: "   ")));
    }

    [Fact]
    public void RequireFoundThrowsClaimSourceNotFoundWhenAbsent()
    {
        var error = Assert.Throws<ClaimSourceAdministrationException>(() =>
            ClaimSourceAdministrationPolicy.RequireFound(found: false));

        Assert.Equal(ClaimSourceAdministrationError.ClaimSourceNotFound, error.Error);
    }

    [Fact]
    public void RequireCurrentVersionThrowsStaleVersionOnMismatch()
    {
        var error = Assert.Throws<ClaimSourceAdministrationException>(() =>
            ClaimSourceAdministrationPolicy.RequireCurrentVersion(currentVersion: 3, expectedVersion: 2));

        Assert.Equal(ClaimSourceAdministrationError.StaleVersion, error.Error);
    }

    [Fact]
    public void RequireCurrentVersionAcceptsAnExactMatch() =>
        ClaimSourceAdministrationPolicy.RequireCurrentVersion(currentVersion: 3, expectedVersion: 3);
}
