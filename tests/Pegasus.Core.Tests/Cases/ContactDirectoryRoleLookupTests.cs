using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Tests.Cases;

/// <summary>
/// The one lookup the case screens and the MCP tools share to link an active
/// directory organisation holding a role: the record when the role lists it,
/// null otherwise, so each caller keeps its own refusal.
/// </summary>
public sealed class ContactDirectoryRoleLookupTests
{
    private static readonly ActionActor Staff =
        ActionActor.Staff(Guid.Parse("2f7f6f0a-2f0a-4e3c-9e3a-7a4a5a6b7c8d"), [StaffRole.Engineer]);

    [Fact]
    public async Task FindActiveByRoleReturnsTheListedOrganisation()
    {
        var wanted = Record(Guid.NewGuid());
        var queries = new RoleListing(ContactRole.Repairer, [Record(Guid.NewGuid()), wanted]);

        var found = await queries.FindActiveByRoleAsync(
            Staff, ContactRole.Repairer, wanted.OrganizationId, CancellationToken.None);

        Assert.Same(wanted, found);
    }

    [Fact]
    public async Task FindActiveByRoleIsNullWhenTheRoleDoesNotListIt()
    {
        var organisation = Record(Guid.NewGuid());
        var queries = new RoleListing(ContactRole.Repairer, [organisation]);

        Assert.Null(await queries.FindActiveByRoleAsync(
            Staff, ContactRole.ClaimSource, organisation.OrganizationId, CancellationToken.None));
        Assert.Null(await queries.FindActiveByRoleAsync(
            Staff, ContactRole.Repairer, Guid.NewGuid(), CancellationToken.None));
    }

    private static ContactChoice Record(Guid organizationId) =>
        new(organizationId, "Acme Repairs", ContactRole.Repairer, 1, null, null, null, null, null, null);

    private sealed class RoleListing(
        ContactRole role,
        IReadOnlyList<ContactChoice> records) : IContactDirectoryQueries
    {
        public Task<IReadOnlyList<ContactChoice>> ListByRoleAsync(
            ActionActor actor, ContactRole requested, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ContactChoice>>(requested == role ? records : []);

        public Task<ContactDirectoryRecord?> GetAsync(
            ActionActor actor, Guid organizationId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ContactDirectoryPage> ListAsync(
            ContactDirectoryQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<ContactChoice>> FindPossibleMatchesAsync(
            ActionActor actor, string name, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<PrincipalAdministrationDetails>> ListPrincipalChoicesAsync(
            ActionActor actor, Guid? contactId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
