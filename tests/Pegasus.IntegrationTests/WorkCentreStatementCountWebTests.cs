using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Pegasus.IntegrationTests;

/// <summary>
/// What one Work Centre load costs in SQL commands, against the real stores and
/// an empty estate (Roadmap Lane D, part D4). The Work Centre reads its three
/// sections together, so the count is every command they and the shell send,
/// including the write that marks the New cases feed seen.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class WorkCentreStatementCountWebTests
{
    [Fact]
    public async Task AFullWorkCentreLoadStaysWithinItsStatementBudget()
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
        // and the AI jobs section (before Lane D it was read three times: the
        // snapshot, its drafts and the section's drafts).
        Assert.True(
            counter.CountMentioning("[WorkflowConfigurations]") <= 1,
            "The workflow configuration was read more than once." + Environment.NewLine + counter.Describe());
        // Before Lane D (2ee268507) a full load sent 37 commands including 5 of
        // authentication under the offline sign-in this harness may use; the code
        // reading says 29. To be confirmed by CI; if it prints a lower number,
        // lower the budget to it.
        Assert.True(
            counter.Count <= WorkCentreBudget,
            $"A full Work Centre load sent {counter.Count} SQL commands; the budget is {WorkCentreBudget}." + Environment.NewLine + counter.Describe());
    }

    private const int WorkCentreBudget = 29;
}
