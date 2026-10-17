using Pegasus.Core.Intake;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The <c>Principals</c> row is where a Principal exists; the code catalogue
/// says which rows have an accepted e-mail route (operator, 13 October 2026).
/// A fresh database agrees with the catalogue only because the seed migrations
/// say so.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class PrincipalRouteSeedIntegrationTests
{
    [Fact]
    public async Task AFreshDatabaseHoldsAnActivePrincipalForEveryAcceptedRoute()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();

        foreach (var code in PrincipalMailRoutePolicy.AcceptedIdentities.Keys)
        {
            Assert.Equal(1, await database.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM Principals WHERE Code = N'{code}' AND IsActive = 1"));
        }
    }
}
