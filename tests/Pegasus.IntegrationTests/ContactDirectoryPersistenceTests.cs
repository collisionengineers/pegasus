using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class ContactDirectoryPersistenceTests
{
    private static readonly ActionActor Administrator = ActionActor.Staff(
        Guid.Parse("4fa2d0a6-2f1c-4b6e-8f9a-3d4a9e8a7c11"),
        [StaffRole.Administrator]);

    [Fact]
    public async Task SaveKeepsEachPrincipalAssociationOnItsSelectedRole()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var firstPrincipal = await SaveAsync(database, NewPrincipal("First principal", "CP01"));
        var secondPrincipal = await SaveAsync(database, NewPrincipal("Second principal", "CP02"));
        var contactId = Guid.NewGuid();

        var saved = await SaveAsync(database, new(
            Administrator,
            contactId,
            0,
            "Multi-role contact",
            null,
            null,
            null,
            "1 Contact Street",
            "AB1 2CD",
            true,
            [ContactRole.ClaimSource, ContactRole.Repairer],
            null,
            CaseInspectionMode.PhysicalAddress,
            [
                new(contactId, ContactRole.ClaimSource, firstPrincipal.OwnPrincipalIds!.Single()),
                new(contactId, ContactRole.Repairer, secondPrincipal.OwnPrincipalIds!.Single())
            ],
            Guid.NewGuid().ToString("N"),
            string.Empty));

        Assert.Equal("AB1 2CD", saved.Postcode);
        Assert.Equal(
            [
                new ContactPrincipalAssociation(contactId, ContactRole.ClaimSource, firstPrincipal.OwnPrincipalIds!.Single()),
                new ContactPrincipalAssociation(contactId, ContactRole.Repairer, secondPrincipal.OwnPrincipalIds!.Single())
            ],
            saved.PrincipalAssociations!.OrderBy(item => item.Role).ToArray());

        await using var scope = database.CreateAsyncScope();
        var contacts = scope.ServiceProvider.GetRequiredService<IContactDirectoryQueries>();
        var reread = await contacts.GetAsync(Administrator, contactId, CancellationToken.None);
        Assert.NotNull(reread);
        Assert.Equal(saved.PrincipalAssociations, reread.PrincipalAssociations);

        await using var context = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        var links = await context.Set<ContactPrincipalLinkEntity>()
            .Where(item => item.OrganizationId == contactId)
            .OrderBy(item => item.Role)
            .ToArrayAsync();
        Assert.Collection(
            links,
            link => Assert.Equal((firstPrincipal.OwnPrincipalIds!.Single(), "claim_source"), (link.PrincipalId, link.Role)),
            link => Assert.Equal((secondPrincipal.OwnPrincipalIds!.Single(), "repairer"), (link.PrincipalId, link.Role)));
    }

    [Fact]
    public async Task ExistingVersionZeroContactStillRequiresItsEditScope()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var contactId = Guid.NewGuid();
        await using (var scope = database.CreateAsyncScope())
        {
            var context = await scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
                .CreateDbContextAsync();
            await using (context)
            {
                context.Organizations.Add(new OrganizationEntity
                {
                    Id = contactId,
                    Name = "Migrated contact",
                    Active = true,
                    Version = 0
                });
                context.Set<ContactRoleEntity>().Add(new()
                {
                    OrganizationId = contactId,
                    Role = "claim_source"
                });
                await context.SaveChangesAsync();
            }
        }

        var request = NewContact(contactId, expectedVersion: 0) with
        {
            Name = "Migrated contact renamed"
        };
        await Assert.ThrowsAsync<EditScopeExpiredException>(() => SaveAsync(database, request));

        await using var editScope = database.CreateAsyncScope();
        var token = (await editScope.ServiceProvider.GetRequiredService<IEditScopeLeases>().ClaimAsync(
            new(EditScopeKind.Contact, contactId, 0, Administrator, Guid.NewGuid().ToString("N")),
            CancellationToken.None)).Token;
        var saved = await SaveAsync(database, request with { EditLeaseToken = token });
        Assert.Equal(1, saved.Version);
        Assert.Equal("Migrated contact renamed", saved.Name);
    }

    [Fact]
    public async Task DisablingAContactDisablesOnlyItsCurrentPrincipal()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var request = NewPrincipal("Current principal", "CP03");
        var created = await SaveAsync(database, request);

        await using var scope = database.CreateAsyncScope();
        var token = (await scope.ServiceProvider.GetRequiredService<IEditScopeLeases>().ClaimAsync(
            new(EditScopeKind.Contact, request.OrganizationId, created.Version, Administrator,
                Guid.NewGuid().ToString("N")),
            CancellationToken.None)).Token;
        _ = await SaveAsync(database, request with
        {
            ExpectedVersion = created.Version,
            Active = false,
            EditLeaseToken = token,
            OperationKey = Guid.NewGuid().ToString("N")
        });

        await using var context = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        Assert.False(await context.Principals
            .Where(item => item.OrganizationId == request.OrganizationId && item.SuccessorId == null)
            .Select(item => item.IsActive)
            .SingleAsync());
    }

    [Fact]
    public async Task ClearingAndReplacingGuidanceDoesNotReuseItsAppliedVersion()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var request = NewContact(Guid.NewGuid(), 0) with { GuidanceTemplate = "Initial guidance" };
        var saved = await SaveAsync(database, request);
        Assert.Equal(1, saved.GuidanceTemplateVersion);
        foreach (var template in new string?[] { null, "Replacement guidance" })
        {
            await using var scope = database.CreateAsyncScope();
            var lease = await scope.ServiceProvider.GetRequiredService<IEditScopeLeases>().ClaimAsync(
                new(EditScopeKind.Contact, saved.OrganizationId, saved.Version, Administrator,
                    Guid.NewGuid().ToString("N")), CancellationToken.None);
            var previousVersion = saved.GuidanceTemplateVersion;
            saved = await SaveAsync(database, request with
            {
                ExpectedVersion = saved.Version,
                GuidanceTemplate = template,
                EditLeaseToken = lease.Token,
                OperationKey = Guid.NewGuid().ToString("N")
            });
            Assert.Equal(previousVersion + 1, saved.GuidanceTemplateVersion);
        }
    }

    [Fact]
    public async Task ListOrdersEveryModeAndRetainsContactsWithoutCases()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        const string fixturePrefix = "List-sort fixture ";
        var claimSource = await SaveAsync(database, NewContact(Guid.NewGuid(), 0) with
        {
            Name = fixturePrefix + "Zulu claim source"
        });
        var firstPrincipal = await SaveAsync(database, NewPrincipal(fixturePrefix + "Alpha principal", "LCD01"));
        var secondPrincipal = await SaveAsync(database, NewPrincipal(fixturePrefix + "Bravo principal", "LCD02"));
        var repairer = await SaveAsync(database, NewContact(Guid.NewGuid(), 0) with
        {
            Name = fixturePrefix + "Mike repairer",
            Roles = [ContactRole.Repairer]
        });
        var receivedAtUtc = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        await using (var scope = database.CreateAsyncScope())
        {
            var context = await scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
                .CreateDbContextAsync();
            await using (context)
            {
                var principalIds = new[]
                {
                    firstPrincipal.OwnPrincipalIds!.Single(),
                    secondPrincipal.OwnPrincipalIds!.Single()
                };
                var principals = await context.Principals
                    .Where(item => principalIds.Contains(item.Id))
                    .ToDictionaryAsync(item => item.Id);
                var first = principals[firstPrincipal.OwnPrincipalIds!.Single()];
                var second = principals[secondPrincipal.OwnPrincipalIds!.Single()];
                context.Cases.AddRange(
                    Case(first, 1, "CASE-B", receivedAtUtc),
                    Case(first, 2, "CASE-A", receivedAtUtc),
                    Case(second, 1, "CASE-C", receivedAtUtc));
                await context.SaveChangesAsync();
            }
        }

        await using var queryScope = database.CreateAsyncScope();
        var queries = queryScope.ServiceProvider.GetRequiredService<IContactDirectoryQueries>();
        async Task<IReadOnlyList<ContactDirectoryRecord>> ListAsync(ContactSort sort) =>
            await queries.ListAsync(new(Administrator, null, sort, fixturePrefix), CancellationToken.None);

        var byName = await ListAsync(ContactSort.Name);
        var byType = await ListAsync(ContactSort.Type);
        var byLastCase = await ListAsync(ContactSort.LastCase);

        Assert.Equal(
            [firstPrincipal.OrganizationId, secondPrincipal.OrganizationId, repairer.OrganizationId, claimSource.OrganizationId],
            byName.Select(item => item.OrganizationId));
        Assert.Equal(
            [claimSource.OrganizationId, firstPrincipal.OrganizationId, secondPrincipal.OrganizationId, repairer.OrganizationId],
            byType.Select(item => item.OrganizationId));
        Assert.Equal(
            [firstPrincipal.OrganizationId, secondPrincipal.OrganizationId, repairer.OrganizationId, claimSource.OrganizationId],
            byLastCase.Select(item => item.OrganizationId));

        var latest = Assert.Single(byLastCase, item => item.OrganizationId == firstPrincipal.OrganizationId);
        Assert.Equal(receivedAtUtc, latest.LastCaseReceivedAtUtc);
        Assert.Equal("CASE-A", latest.LastCaseReference);
        Assert.All(
            byLastCase.Where(item => item.OrganizationId == claimSource.OrganizationId
                || item.OrganizationId == repairer.OrganizationId),
            item =>
            {
                Assert.Null(item.LastCaseReceivedAtUtc);
                Assert.Null(item.LastCaseReference);
            });
    }

    private static SaveContactRequest NewPrincipal(string name, string code) => new(
        Administrator,
        Guid.NewGuid(),
        0,
        name,
        null,
        null,
        null,
        null,
        null,
        true,
        [ContactRole.Principal],
        code,
        CaseInspectionMode.PhysicalAddress,
        [],
        Guid.NewGuid().ToString("N"),
        string.Empty);

    private static CaseEntity Case(
        PrincipalEntity principal,
        int sequence,
        string reference,
        DateTimeOffset receivedAtUtc) => new()
    {
        Id = Guid.NewGuid(),
        PrincipalId = principal.Id,
        SequenceLineageId = principal.SequenceLineageId,
        Year = receivedAtUtc.Year,
        Sequence = sequence,
        Reference = reference,
        Type = "Inspection",
        InitialState = "NotReady",
        CustodyState = "Pending",
        CreatedAtUtc = receivedAtUtc,
        Version = 0,
        ConcurrencyToken = Guid.NewGuid()
    };

    private static SaveContactRequest NewContact(Guid contactId, long expectedVersion) => new(
        Administrator,
        contactId,
        expectedVersion,
        "Migrated contact",
        null,
        null,
        null,
        null,
        null,
        true,
        [ContactRole.ClaimSource],
        null,
        CaseInspectionMode.PhysicalAddress,
        [],
        Guid.NewGuid().ToString("N"),
        string.Empty);

    private static async Task<ContactDirectoryRecord> SaveAsync(
        LocalDbTestDatabase database,
        SaveContactRequest request)
    {
        await using var scope = database.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IContactDirectoryAdministration>()
            .SaveAsync(request, CancellationToken.None);
    }
}
