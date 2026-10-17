using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Address;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The reviewed inspection locations' one writer. FRD-06: the pipeline
/// "keeps operator-confirmed rows across refreshes, and is deterministic and
/// auditable".
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class InspectionLocationPersistenceTests
{
    private static readonly ActionActor Administrator = ActionActor.Staff(
        Guid.Parse("8d3c2b1a-0f9e-4d8c-b7a6-5e4d3c2b1a09"),
        [StaffRole.Administrator]);

    private static readonly DateTimeOffset FirstReview = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ReplayingAKeyWritesOnceAndAChangedRequestConflicts()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var qdos = await QdosIdAsync(database);
        var request = Request("row-1", FirstReview, [qdos], "inspection-location:replay");

        var created = await SaveAsync(database, request);
        var replay = await SaveAsync(database, request);

        Assert.Equal(InspectionLocationSaveOutcome.Created, created.Outcome);
        Assert.Equal(0L, created.Version);
        Assert.Equal(created, replay);
        Assert.Equal(1, await database.ScalarAsync<int>("SELECT COUNT(*) FROM InspectionLocations"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM OrganizationAdministrationOperations WHERE OperationKey = N'inspection-location:replay' AND CommandKind = N'save_inspection_location'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM ActionHistory WHERE CorrelationId = N'inspection-location:replay' AND AggregateType = N'inspection_location' AND EventKind = N'inspection_location_created'"));
        Assert.Equal(
            "TE1 1ST",
            await database.ScalarAsync<string>($"SELECT Postcode FROM InspectionLocations WHERE Id = '{created.Id:D}'"));

        var conflict = await Assert.ThrowsAsync<ContactDirectoryException>(() =>
            SaveAsync(database, request with { Name = "Another Body Centre" }));
        Assert.Equal(ContactDirectoryError.OperationConflict, conflict.Error);
    }

    [Fact]
    public async Task ALaterReviewReplacesAndAnOlderOneKeepsTheRow()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var qdos = await QdosIdAsync(database);
        var other = await SavePrincipalAsync(database, "Second test principal", "ILP02", active: true);
        var created = await SaveAsync(database, Request("row-1", FirstReview, [qdos], "inspection-location:first"));

        // The same review, or an older one, keeps the stored row as it is.
        foreach (var (reviewedAtUtc, key) in new[]
        {
            (FirstReview, "inspection-location:same-review"),
            (FirstReview.AddDays(-1), "inspection-location:older-review")
        })
        {
            var kept = await SaveAsync(database, Request("row-1", reviewedAtUtc, [other], key) with
            {
                Name = "Unconfirmed name"
            });
            Assert.Equal(new InspectionLocationSaveResult(created.Id, 0, InspectionLocationSaveOutcome.KeptExisting), kept);
            Assert.Equal(0, await database.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM ActionHistory WHERE CorrelationId = N'{key}'"));
            Assert.Equal(0, await database.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM OrganizationAdministrationOperations WHERE OperationKey = N'{key}'"));
        }
        Assert.Equal("Test Body Centre", await database.ScalarAsync<string>(
            $"SELECT Name FROM InspectionLocations WHERE Id = '{created.Id:D}'"));
        Assert.Equal([qdos], await LinkedPrincipalsAsync(database, created.Id));

        // A later review replaces the fields and the whole link set.
        var updated = await SaveAsync(database, Request("row-1", FirstReview.AddDays(1), [other], "inspection-location:later-review") with
        {
            Name = "Renamed Body Centre",
            Address = "2 Test Street, Testtown",
            Postcode = "te2 2st",
            Active = false
        });

        Assert.Equal(new InspectionLocationSaveResult(created.Id, 1, InspectionLocationSaveOutcome.Updated), updated);
        await using var context = await database.CreateContextAsync();
        var stored = await context.InspectionLocations.AsNoTracking().SingleAsync(item => item.Id == created.Id);
        Assert.Equal(
            ("Renamed Body Centre", "2 Test Street, Testtown", "TE2 2ST", false, FirstReview.AddDays(1), 1L),
            (stored.Name, stored.Address, stored.Postcode, stored.Active, stored.ReviewedAtUtc, stored.Version));
        Assert.Equal([other], await LinkedPrincipalsAsync(database, created.Id));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM ActionHistory WHERE CorrelationId = N'inspection-location:later-review' AND EventKind = N'inspection_location_saved' AND BeforeJson IS NOT NULL"));
    }

    [Fact]
    public async Task LinksMayNameAnInactivePrincipalAndRefuseAnUnknownOne()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var inactive = await SavePrincipalAsync(database, "Inactive test principal", "ILP03", active: false);

        var saved = await SaveAsync(database, Request("row-1", FirstReview, [inactive], "inspection-location:inactive"));

        Assert.Equal(InspectionLocationSaveOutcome.Created, saved.Outcome);
        Assert.Equal([inactive], await LinkedPrincipalsAsync(database, saved.Id));
        var unknown = await Assert.ThrowsAsync<ContactDirectoryException>(() =>
            SaveAsync(database, Request("row-2", FirstReview, [Guid.NewGuid()], "inspection-location:unknown")));
        Assert.Equal(ContactDirectoryError.InvalidPrincipalAssociation, unknown.Error);
        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM InspectionLocations WHERE SourceRecordId = N'row-2'"));
    }

    [Fact]
    public async Task NoRefreshEverDeactivatesAnAbsentRow()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var qdos = await QdosIdAsync(database);
        var kept = await SaveAsync(database, Request("row-1", FirstReview, [qdos], "inspection-location:refresh-1a"));
        var absent = await SaveAsync(database, Request("row-2", FirstReview, [qdos], "inspection-location:refresh-1b"));

        // The next refresh sends only row-1.
        await SaveAsync(database, Request("row-1", FirstReview.AddDays(1), [qdos], "inspection-location:refresh-2a"));

        await using var context = await database.CreateContextAsync();
        var refreshed = await context.InspectionLocations.AsNoTracking().SingleAsync(item => item.Id == kept.Id);
        var untouched = await context.InspectionLocations.AsNoTracking().SingleAsync(item => item.Id == absent.Id);
        Assert.Equal((true, 1L, FirstReview.AddDays(1)), (refreshed.Active, refreshed.Version, refreshed.ReviewedAtUtc));
        Assert.Equal((true, 0L, FirstReview), (untouched.Active, untouched.Version, untouched.ReviewedAtUtc));
        Assert.Equal([qdos], await LinkedPrincipalsAsync(database, absent.Id));
    }

    private static async Task<Guid> QdosIdAsync(LocalDbTestDatabase database)
    {
        await using var context = await database.CreateContextAsync();
        return (await SeededPrincipals.QdosAsync(context)).Id;
    }

    private static SaveInspectionLocationRequest Request(
        string sourceRecordId,
        DateTimeOffset reviewedAtUtc,
        IReadOnlyList<Guid> principalIds,
        string operationKey) => new(
        Administrator,
        "Test Body Centre",
        "1 Test Street, Testtown",
        "te1 1st",
        Active: true,
        "register",
        sourceRecordId,
        reviewedAtUtc,
        principalIds,
        operationKey);

    private static async Task<InspectionLocationSaveResult> SaveAsync(
        LocalDbTestDatabase database,
        SaveInspectionLocationRequest request)
    {
        await using var scope = database.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IInspectionLocationAdministration>()
            .SaveAsync(request, CancellationToken.None);
    }

    private static async Task<Guid> SavePrincipalAsync(
        LocalDbTestDatabase database,
        string name,
        string code,
        bool active)
    {
        await using var scope = database.CreateAsyncScope();
        var saved = await scope.ServiceProvider.GetRequiredService<IContactDirectoryAdministration>().SaveAsync(
            new(
                Administrator,
                Guid.NewGuid(),
                0,
                name,
                null,
                null,
                null,
                null,
                null,
                active,
                ContactRole.Principal,
                code,
                CaseInspectionMode.PhysicalAddress,
                [],
                Guid.NewGuid().ToString("N")),
            CancellationToken.None);
        return saved.OwnPrincipalIds!.Single();
    }

    private static async Task<Guid[]> LinkedPrincipalsAsync(LocalDbTestDatabase database, Guid locationId)
    {
        await using var context = await database.CreateContextAsync();
        return await context.InspectionLocationPrincipalLinks.AsNoTracking()
            .Where(link => link.InspectionLocationId == locationId)
            .Select(link => link.PrincipalId)
            .ToArrayAsync();
    }
}
