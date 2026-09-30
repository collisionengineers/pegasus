using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Pegasus.IntegrationTests;

/// <summary>
/// What one Work Centre load costs in SQL commands, against the real stores and
/// an empty estate (Roadmap Lane D, part D4). The Work Centre reads its three
/// sections together, so the count is every command they and the shell send,
/// including the write that marks the New cases feed seen. It is pinned at its
/// exact count, so any change is seen, and a mismatch lists every command sent.
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
        // and the AI jobs section (before Lane D it was read three times: the
        // snapshot, its drafts and the section's drafts).
        Assert.True(
            counter.CountMentioning("[WorkflowConfigurations]") == 1,
            "The workflow configuration was not read exactly once." + Environment.NewLine + counter.Describe());
        // Measured with LocalDB in this scenario: 33 commands before Lane D
        // (2ee268507) and 25 after it. The load no longer counts the intake queue,
        // reads the workflow configuration and the open AI jobs once each rather
        // than three times, derives the AI drafts in memory, and hands the shell
        // the stage counts and the Unidentified count it has already read.
        Assert.True(
            counter.Count == WorkCentreCommands,
            $"A full Work Centre load sent {counter.Count} SQL commands; it is pinned at {WorkCentreCommands}." + Environment.NewLine + counter.Describe());
    }

    private const int WorkCentreCommands = 25;
}
