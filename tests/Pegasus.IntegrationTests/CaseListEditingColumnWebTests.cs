using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;
using Pegasus.Web.Authentication;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The Case list and Search say who is editing a Case (15 September 2026
/// walk): a live edit lease names its staff holder in the Editing column,
/// and a released one names nobody, so an operator does not open a Case only
/// to find it taken.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class CaseListEditingColumnWebTests
{
    [Fact]
    public async Task ALiveLeaseNamesItsHolderOnTheListAndSearchAndAReleasedOneNamesNobody()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development", true, recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await ImageIntakeTestData.SeedInstructionCaseAsync(
            factory, client, "AB12 CDE", "EDITING-COLUMN-01");
        var holder = ActionActor.Staff(
            DevelopmentOfflineIdentity.AdministratorId, [StaffRole.Administrator]);
        var editingCell = $"<td>{DevelopmentOfflineIdentity.UserName}</td>";

        await using var scope = factory.Services.CreateAsyncScope();
        var workflows = scope.ServiceProvider.GetRequiredService<ICaseWorkflowStore>();
        var workflow = await workflows.GetAsync(caseId, CancellationToken.None);
        Assert.NotNull(workflow);
        var lease = await workflows.ClaimAsync(
            new(caseId, workflow!.Version, holder, "editing-column-claim"),
            CancellationToken.None);

        var search = await GetAsync(client, "/Search?registration=AB12CDE");
        Assert.Contains(editingCell, search, StringComparison.Ordinal);
        var list = await GetAsync(client, "/Cases?queue=review");
        Assert.Contains(editingCell, list, StringComparison.Ordinal);

        await workflows.ReleaseAsync(
            new(caseId, holder, "editing-column-release", lease.Token),
            CancellationToken.None);

        var released = await GetAsync(client, "/Search?registration=AB12CDE");
        Assert.Contains($"href=\"/Cases/{caseId:D}\"", released, StringComparison.Ordinal);
        Assert.DoesNotContain(editingCell, released, StringComparison.Ordinal);
    }

    private static async Task<string> GetAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }
}
