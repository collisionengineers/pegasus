using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Web.Authentication;

namespace Pegasus.IntegrationTests;

/// <summary>
/// What one Work Centre load costs in SQL commands, against the real stores and
/// an empty estate (Roadmap Lane D, part D4). The Work Centre reads its three
/// sections and the Activity figures together, so the count is every command they and the shell send,
/// including the write that marks the New cases feed seen. The count is a
/// ceiling, so growth is seen, and a failure lists every command sent.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class WorkCentreStatementCountWebTests
{
    [Fact]
    public async Task AFullWorkCentreLoadSendsItsPinnedStatements()
    {
        var counter = new CommandCountingInterceptor();
        using var factory = new IntakeWebApplicationFactory(
            "Development", true, useIntegrationTestAuthentication: true, commandInterceptor: counter);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });
        // The first load pays one-off work; the count is the second.
        (await client.GetAsync("/")).Dispose();

        counter.Reset();
        using var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _ = await response.Content.ReadAsStringAsync();

        // The workflow configuration is read once and shared by the attention list
        // and the AI jobs section.
        Assert.True(
            counter.CountMentioning("[WorkflowConfigurations]") == 1,
            "The workflow configuration was not read exactly once." + Environment.NewLine + counter.Describe());
        Assert.True(
            counter.Count <= WorkCentreCommands,
            $"A full Work Centre load sent {counter.Count} SQL commands; at most {WorkCentreCommands} are allowed." + Environment.NewLine + counter.Describe());
    }

    private const int WorkCentreCommands = 26;

    /// <summary>
    /// The Work Centre reads no section once per row: a load with six new Cases
    /// sends the same commands as a load with three.
    /// </summary>
    [Fact]
    public async Task TheWorkCentreSendsTheSameStatementsForSixNewCasesAsForThree()
    {
        var counter = new CommandCountingInterceptor();
        using var factory = new IntakeWebApplicationFactory(
            "Development", true, useIntegrationTestAuthentication: true, commandInterceptor: counter);
        using var client = IntakeWebDriver.CreateClient(factory);
        await AllocationTestData.SeedPrincipalAsync(factory.Services, QdosPrincipal.Code);
        var references = new List<string>();
        for (var index = 0; index < 3; index++)
        {
            references.Add(await AcceptCaseAsync(factory));
        }

        // The first load pays one-off work; the counts are later ones.
        _ = await IntakeWebDriver.GetHtmlAsync(client, "/");

        counter.Reset();
        _ = await IntakeWebDriver.GetHtmlAsync(client, "/");
        var withThreeCases = counter.Count;
        var withThreeDescription = counter.Describe();

        // Three more Cases, same request.
        for (var index = 0; index < 3; index++)
        {
            references.Add(await AcceptCaseAsync(factory));
        }

        counter.Reset();
        var html = await IntakeWebDriver.GetHtmlAsync(client, "/");
        var withSixCases = counter.Count;
        var withSixDescription = counter.Describe();

        Assert.True(withThreeCases > 0, "The interceptor observed no commands at all.");
        Assert.All(references, reference => Assert.Contains(reference, html, StringComparison.Ordinal));
        Assert.True(
            withThreeCases == withSixCases,
            $"The Work Centre load grew with the Cases: {withThreeCases} commands for three and {withSixCases} for six."
            + Environment.NewLine + "Three Cases:" + Environment.NewLine + withThreeDescription
            + Environment.NewLine + "Six Cases:" + Environment.NewLine + withSixDescription);
    }

    private static async Task<string> AcceptCaseAsync(IntakeWebApplicationFactory factory)
    {
        var receipt = await AllocationTestData.StoreDefinitiveReceiptAsync(
            factory.Services,
            CaseType.Inspection,
            QdosPrincipal.Code);
        await using var scope = factory.Services.CreateAsyncScope();
        var accepted = await scope.ServiceProvider.GetRequiredService<IAcceptIntake>()
            .ExecuteAsync(
                new(
                    receipt.Id,
                    0,
                    ActionActor.Staff(
                        DevelopmentOfflineIdentity.AdministratorId,
                        [StaffRole.Administrator]),
                    $"work-centre-statement-count:{Guid.NewGuid():N}",
                    CaseType.Inspection,
                    QdosPrincipal.Code,
                    new(true, true)),
                CancellationToken.None);
        return accepted.Identity.Reference;
    }
}
