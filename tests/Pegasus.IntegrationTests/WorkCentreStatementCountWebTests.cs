using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Pegasus.IntegrationTests;

/// <summary>
/// What one Work Centre load costs in SQL commands, against the real stores and
/// an empty estate (Roadmap Lane D, part D4). The Work Centre reads its three
/// sections and the Activity figures together, so the count is every command they and the shell send,
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
        // the stage counts and the Unidentified count it has already read. New
        // cases reading new Cases only (8 October 2026) took it from 30 to 28:
        // no count or read of the Automation actor's changes.
        // Counting each Activity figure in SQL on its own context (10 October 2026)
        // sends two COUNT commands per figure, ten rather than five: 28 to 33.
        // Open tasks (operator, 6 October 2026) add the one read of the open
        // Case tasks.
        Assert.True(
            counter.Count == WorkCentreCommands,
            $"A full Work Centre load sent {counter.Count} SQL commands; it is pinned at {WorkCentreCommands}." + Environment.NewLine + counter.Describe());
    }

    // 28 - 4 (#1179: shared Triage and Case reads, no rail Triage or Awaiting count)
    // + 5 (#1178: two COUNT commands per Activity figure, ten rather than five).
    // - 1 (#1198: the password-change redirect reads the account the cookie check read).
    // - 2 (PR 1271: the Completed figure is gone, two COUNT commands fewer; the stage
    //   counts stay one statement, grouped by state and open-query mark).
    // + 1 (#1015: the one read of the open Case tasks for the Open tasks rows).
    private const int WorkCentreCommands = 27;
}
