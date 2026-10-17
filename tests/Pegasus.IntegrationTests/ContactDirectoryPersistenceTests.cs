using System.Text.Json;
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

    private static readonly JsonSerializerOptions HistoryJson = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task SaveKeepsTheLinkedPrincipalsAndTheOneFixedType()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var firstPrincipal = await SaveAsync(database, NewPrincipal("First principal", "CP01"));
        var secondPrincipal = await SaveAsync(database, NewPrincipal("Second principal", "CP02"));
        var contactId = Guid.NewGuid();
        Guid[] linked = [.. new[] { firstPrincipal.OwnPrincipalIds!.Single(), secondPrincipal.OwnPrincipalIds!.Single() }.Order()];

        var request = new SaveContactRequest(
            Administrator,
            contactId,
            0,
            "Linked repairer",
            null,
            null,
            null,
            "1 Contact Street",
            "AB1 2CD",
            true,
            ContactRole.Repairer,
            null,
            CaseInspectionMode.PhysicalAddress,
            linked,
            Guid.NewGuid().ToString("N"));
        var saved = await SaveAsync(database, request);

        Assert.Equal("AB1 2CD", saved.Postcode);
        Assert.Equal(ContactRole.Repairer, saved.Role);
        Assert.Equal(linked, saved.LinkedPrincipalIds);

        await using var scope = database.CreateAsyncScope();
        var contacts = scope.ServiceProvider.GetRequiredService<IContactDirectoryQueries>();
        var reread = await contacts.GetAsync(Administrator, contactId, CancellationToken.None);
        Assert.NotNull(reread);
        Assert.Equal(ContactRole.Repairer, reread.Role);
        Assert.Equal(linked, reread.LinkedPrincipalIds);

        await using var context = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        Assert.Equal(
            linked,
            (await context.Set<ContactPrincipalLinkEntity>()
                .Where(item => item.OrganizationId == contactId)
                .Select(item => item.PrincipalId)
                .ToArrayAsync()).Order());

        // The type is chosen when the contact is added and never changes.
        await Assert.ThrowsAsync<ArgumentException>(() => SaveAsync(database, request with
        {
            ExpectedVersion = saved.Version,
            Role = ContactRole.Storage,
            OperationKey = Guid.NewGuid().ToString("N")
        }));
    }

    [Fact]
    public async Task ExistingVersionZeroContactCanSaveDirectlyAndRejectsStaleVersion()
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
                    Role = "claim_source",
                    Active = true,
                    Version = 0
                });
                await context.SaveChangesAsync();
            }
        }

        var request = NewContact(contactId, expectedVersion: 0) with
        {
            Name = "Migrated contact renamed"
        };
        var saved = await SaveAsync(database, request);
        Assert.Equal(1, saved.Version);
        Assert.Equal("Migrated contact renamed", saved.Name);
        var stale = await Assert.ThrowsAsync<ContactDirectoryException>(() =>
            SaveAsync(database, request with { OperationKey = Guid.NewGuid().ToString("N") }));
        Assert.Equal(ContactDirectoryError.StaleVersion, stale.Error);
    }

    [Fact]
    public async Task DisablingAContactDisablesOnlyItsCurrentPrincipal()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var request = NewPrincipal("Current principal", "CP03");
        var created = await SaveAsync(database, request);

        await using var scope = database.CreateAsyncScope();
        _ = await SaveAsync(database, request with
        {
            ExpectedVersion = created.Version,
            Active = false,
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

    /// <summary>
    /// R118: a save sets an existing Principal's inspection mode; the same mode
    /// changes nothing and leaves the version, and a new one is the contact's
    /// history event with the mode before and after.
    /// </summary>
    [Fact]
    public async Task SaveSetsAnExistingPrincipalsInspectionModeOnlyWhenItDiffers()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var request = NewPrincipal("Mode principal", "CPM01");
        var created = await SaveAsync(database, request);
        Assert.Equal(CaseInspectionMode.PhysicalAddress, created.PrincipalInspectionMode);

        var unchanged = await SaveAsync(database, request with
        {
            ExpectedVersion = created.Version,
            OperationKey = Guid.NewGuid().ToString("N")
        });
        Assert.Equal(created.Version, unchanged.Version);
        Assert.Equal(CaseInspectionMode.PhysicalAddress, unchanged.PrincipalInspectionMode);

        var changedKey = Guid.NewGuid().ToString("N");
        var changed = await SaveAsync(database, request with
        {
            ExpectedVersion = unchanged.Version,
            PrincipalInspectionMode = CaseInspectionMode.ImageBasedAssessment,
            OperationKey = changedKey
        });
        Assert.Equal(unchanged.Version + 1, changed.Version);
        Assert.Equal(CaseInspectionMode.ImageBasedAssessment, changed.PrincipalInspectionMode);

        await using var scope = database.CreateAsyncScope();
        var reread = await scope.ServiceProvider.GetRequiredService<IContactDirectoryQueries>()
            .GetAsync(Administrator, request.OrganizationId, CancellationToken.None);
        Assert.Equal(CaseInspectionMode.ImageBasedAssessment, reread!.PrincipalInspectionMode);
        await using var context = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        Assert.Equal(
            PrincipalInspectionModePolicy.ImageBasedAssessmentCode,
            await context.Principals
                .Where(item => item.OrganizationId == request.OrganizationId)
                .Select(item => item.InspectionMode)
                .SingleAsync());
        var history = await context.ActionHistory.SingleAsync(item => item.CorrelationId == changedKey);
        Assert.Equal(("contact", request.OrganizationId.ToString("D"), "contact_saved"), (history.AggregateType, history.AggregateId, history.EventKind));
        Assert.Equal(CaseInspectionMode.PhysicalAddress, HistoryRecord(history.BeforeJson).PrincipalInspectionMode);
        Assert.Equal(CaseInspectionMode.ImageBasedAssessment, HistoryRecord(history.AfterJson).PrincipalInspectionMode);
    }

    /// <summary>
    /// R119: a contact may link an inactive Principal, and the Principal choices
    /// offer an inactive one only to the contact that already links it.
    /// </summary>
    [Fact]
    public async Task ALinkMayNameAnInactivePrincipalWhichOnlyItsContactIsOffered()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var activePrincipal = await SaveAsync(database, NewPrincipal("Linked active principal", "CPL01"));
        var inactiveRequest = NewPrincipal("Linked inactive principal", "CPL02");
        var inactivePrincipal = await SaveAsync(database, inactiveRequest);
        _ = await SaveAsync(database, inactiveRequest with
        {
            ExpectedVersion = inactivePrincipal.Version,
            Active = false,
            OperationKey = Guid.NewGuid().ToString("N")
        });
        var activeId = activePrincipal.OwnPrincipalIds!.Single();
        var inactiveId = inactivePrincipal.OwnPrincipalIds!.Single();
        Guid[] linked = [.. new[] { activeId, inactiveId }.Order()];

        var linking = await SaveAsync(database, NewContact(Guid.NewGuid(), 0) with
        {
            Name = "Inactive link repairer",
            Role = ContactRole.Repairer,
            LinkedPrincipalIds = linked
        });
        var other = await SaveAsync(database, NewContact(Guid.NewGuid(), 0) with
        {
            Name = "Unlinked repairer",
            Role = ContactRole.Repairer
        });

        await using var scope = database.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IContactDirectoryQueries>();
        Assert.Equal(linked, (await queries.GetAsync(Administrator, linking.OrganizationId, CancellationToken.None))!.LinkedPrincipalIds);
        async Task<Guid[]> FixtureChoices(Guid? contactId) =>
            [.. (await queries.ListPrincipalChoicesAsync(Administrator, contactId, CancellationToken.None))
                .Select(choice => choice.Principal.Id)
                .Where(id => linked.Contains(id))
                .Order()];
        Assert.Equal(linked, await FixtureChoices(linking.OrganizationId));
        Assert.Equal([activeId], await FixtureChoices(other.OrganizationId));
        Assert.Equal([activeId], await FixtureChoices(null));
    }

    [Fact]
    public async Task ListOrdersByNameAndRetainsContactsWithoutCases()
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
            Role = ContactRole.Repairer
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
        async Task<IReadOnlyList<ContactDirectoryRecord>> ListOfType(ContactRole role) =>
            (await queries.ListAsync(new(Administrator, role, fixturePrefix), CancellationToken.None)).Items;
        var listedPrincipals = await ListOfType(ContactRole.Principal);
        var listedRepairers = await ListOfType(ContactRole.Repairer);
        var listedClaimSources = await ListOfType(ContactRole.ClaimSource);

        Assert.Equal(
            [firstPrincipal.OrganizationId, secondPrincipal.OrganizationId],
            listedPrincipals.Select(item => item.OrganizationId));
        Assert.Equal([repairer.OrganizationId], listedRepairers.Select(item => item.OrganizationId));
        Assert.Equal([claimSource.OrganizationId], listedClaimSources.Select(item => item.OrganizationId));

        var latest = Assert.Single(listedPrincipals, item => item.OrganizationId == firstPrincipal.OrganizationId);
        Assert.Equal(receivedAtUtc, latest.LastCaseReceivedAtUtc);
        Assert.Equal("CASE-A", latest.LastCaseReference);
        Assert.Equal("LCD01", latest.PrincipalCode);
        Assert.All(
            listedRepairers.Concat(listedClaimSources),
            item =>
            {
                Assert.Null(item.LastCaseReceivedAtUtc);
                Assert.Null(item.LastCaseReference);
                Assert.Null(item.PrincipalCode);
            });

        var repairers = await queries.ListByRoleAsync(Administrator, ContactRole.Repairer, CancellationToken.None);
        var listedRepairer = Assert.Single(repairers, item => item.OrganizationId == repairer.OrganizationId);
        Assert.Null(listedRepairer.LastCaseReceivedAtUtc);
        Assert.Null(listedRepairer.LastCaseReference);
    }

    [Fact]
    public async Task ListPagesATypeByNameWithoutSkippingOrRepeatingARow()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        const string repairerPrefix = "Paged repairer ";
        // Zero-padded, so name order is number order; added newest-first in one save.
        string[] repairerNames = [.. Enumerable.Range(0, 120).Select(number => $"{repairerPrefix}{number:D3}")];
        await using (var scope = database.CreateAsyncScope())
        {
            var context = await scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
                .CreateDbContextAsync();
            await using (context)
            {
                context.Organizations.AddRange(repairerNames.OrderDescending().Select(name => new OrganizationEntity
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    Role = "repairer",
                    Active = true,
                    Version = 0
                }));
                await context.SaveChangesAsync();
            }
        }
        const string principalPrefix = "Paged principal ";
        string[] principalNames = [.. Enumerable.Range(1, 3).Select(number => $"{principalPrefix}{number}")];
        foreach (var number in Enumerable.Range(1, 3))
        {
            await SaveAsync(database, NewPrincipal($"{principalPrefix}{number}", $"PGP{number}"));
        }

        await using var queryScope = database.CreateAsyncScope();
        var queries = queryScope.ServiceProvider.GetRequiredService<IContactDirectoryQueries>();
        Task<ContactDirectoryPage> ReadPage(ContactRole role, int pageNumber, string search) =>
            queries.ListAsync(new(Administrator, role, search, pageNumber), CancellationToken.None);

        var pages = new[]
        {
            await ReadPage(ContactRole.Repairer, 1, repairerPrefix),
            await ReadPage(ContactRole.Repairer, 2, repairerPrefix),
            await ReadPage(ContactRole.Repairer, 3, repairerPrefix)
        };
        Assert.Equal([1, 2, 3], pages.Select(page => page.PageNumber));
        Assert.Equal([50, 50, 20], pages.Select(page => page.Items.Count));
        Assert.Equal([true, true, false], pages.Select(page => page.HasNextPage));
        Assert.Equal(repairerNames, pages.SelectMany(page => page.Items).Select(item => item.Name));

        var beyond = await ReadPage(ContactRole.Repairer, 4, repairerPrefix);
        Assert.Empty(beyond.Items);
        Assert.False(beyond.HasNextPage);

        var principals = await ReadPage(ContactRole.Principal, 1, principalPrefix);
        Assert.Equal(principalNames, principals.Items.Select(item => item.Name));
        Assert.False(principals.HasNextPage);

        // A prefix narrows to 100 rows, so the second page holds exactly a full page and no more.
        var narrowedFirst = await ReadPage(ContactRole.Repairer, 1, repairerPrefix + "0");
        var narrowedSecond = await ReadPage(ContactRole.Repairer, 2, repairerPrefix + "0");
        Assert.Equal(
            repairerNames.Take(100),
            narrowedFirst.Items.Concat(narrowedSecond.Items).Select(item => item.Name));
        Assert.True(narrowedFirst.HasNextPage);
        Assert.False(narrowedSecond.HasNextPage);
        Assert.Empty((await ReadPage(ContactRole.Repairer, 3, repairerPrefix + "0")).Items);
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
        ContactRole.Principal,
        code,
        CaseInspectionMode.PhysicalAddress,
        [],
        Guid.NewGuid().ToString("N"));

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
        Type = "inspection",
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
        ContactRole.ClaimSource,
        null,
        CaseInspectionMode.PhysicalAddress,
        [],
        Guid.NewGuid().ToString("N"),
        string.Empty);

    private static ContactDirectoryRecord HistoryRecord(string? json) =>
        JsonSerializer.Deserialize<ContactDirectoryRecord>(json!, HistoryJson)!;

    private static async Task<ContactDirectoryRecord> SaveAsync(
        LocalDbTestDatabase database,
        SaveContactRequest request)
    {
        await using var scope = database.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IContactDirectoryAdministration>()
            .SaveAsync(request, CancellationToken.None);
    }
}
