using Microsoft.EntityFrameworkCore;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class ProviderInspectionModeMigrationTests
{
    private const string PreviousMigration = "20260801220500_GrantWebMigrationHistoryRead";
    private const string SettingMigration = "20260803014608_ProviderInspectionModeSetting";

    [Fact]
    public async Task UpgradeSeedsQdosImageBasedAndDefaultsOtherPrincipalsToPhysicalAddress()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();

        await context.Database.MigrateAsync(PreviousMigration);
        await database.ExecuteAsync(ExistingPrincipalsSql);
        await context.Database.MigrateAsync(SettingMigration);

        Assert.Equal(
            "image_based_assessment",
            await database.ScalarAsync<string>(
                "SELECT InspectionMode FROM Principals WHERE Code = 'QDOS'"));
        Assert.Equal(
            "physical_address",
            await database.ScalarAsync<string>(
                "SELECT InspectionMode FROM Principals WHERE Code = 'OTHER'"));
        Assert.Equal(
            1,
            await database.ScalarAsync<int>(
                "SELECT COUNT(*) FROM sys.check_constraints WHERE name = 'CK_Principals_InspectionMode'"));
        Assert.Equal(
            1,
            await database.ScalarAsync<int>(
                "SELECT COUNT(*) FROM sys.check_constraints WHERE name = 'CK_CaseDataFields_SourceKind' AND definition LIKE '%provider_setting%'"));
    }

    private const string ExistingPrincipalsSql =
        """
        INSERT INTO Organizations (Id, Name, Version)
        VALUES ('86000000-0000-0000-0000-000000000010', 'Inspection mode provider', 0);

        INSERT INTO PrincipalSequenceLineages (Id, CreatedAtUtc)
        VALUES
            ('86000000-0000-0000-0000-000000000011', '2031-06-01T09:00:00+00:00'),
            ('86000000-0000-0000-0000-000000000013', '2031-06-01T09:00:00+00:00');

        INSERT INTO Principals
            (Id, OrganizationId, Code, SequenceLineageId, IsActive, Version)
        VALUES
            ('86000000-0000-0000-0000-000000000012',
             '86000000-0000-0000-0000-000000000010', 'QDOS',
             '86000000-0000-0000-0000-000000000011', 1, 0),
            ('86000000-0000-0000-0000-000000000014',
             '86000000-0000-0000-0000-000000000010', 'OTHER',
             '86000000-0000-0000-0000-000000000013', 1, 0);
        """;
}
