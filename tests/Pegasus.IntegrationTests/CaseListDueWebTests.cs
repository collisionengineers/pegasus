using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Tasks;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Presentation;

namespace Pegasus.IntegrationTests;

/// <summary>
/// One Case shows one Due (issue 896): a Due by date with no chase scheduled
/// is the Case's due on the Case list, in its quick detail and on Search, and
/// the quick detail's Current work is the Case's Next action, never the chase
/// schedule's state.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class CaseListDueWebTests
{
    [Fact]
    public async Task ADueByDateWithoutAChaseIsTheDueEverywhereAndCurrentWorkIsTheNextAction()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development", true, recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await ImageIntakeTestData.SeedInstructionCaseAsync(
            factory, client, "AB12 CDE", "LIST-DUE-01");

        // The state the editable Due by leaves behind: a Stopped row that
        // carries only the date.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            var workflow = await context.CaseWorkflows
                .Include(item => item.DueWork)
                .SingleAsync(item => item.CaseId == caseId);
            if (workflow.DueWork is not { } due)
            {
                due = new()
                {
                    CaseId = caseId,
                    Workflow = workflow,
                    MissingMaterialReason = "Case completeness is not confirmed",
                    State = nameof(CaseDueWorkState.Stopped),
                    Version = 0
                };
                context.CaseDueWork.Add(due);
            }
            due.DueBy = new DateOnly(2040, 6, 18);
            due.DueBySetByStaff = true;
            due.State = nameof(CaseDueWorkState.Stopped);
            due.NextChaseAtUtc = null;
            await context.SaveChangesAsync();
        }

        var list = await GetAsync(client, $"/Cases?queue=review&selected={caseId:D}");
        Assert.Contains("<td class=\"nowrap\">18 Jun 2040</td>", list, StringComparison.Ordinal);
        Assert.Matches(new Regex(@"<span>Due</span>\s*<strong>18 Jun 2040</strong>"), list);
        Assert.Matches(
            new Regex(@"<span>Current work</span>\s*<strong>" + Regex.Escape(CaseWorkspaceLabels.HandToEngineer) + "</strong>"),
            list);
        Assert.DoesNotContain("Chasing stopped", list, StringComparison.Ordinal);

        var search = await GetAsync(client, $"/Search?registration=AB12CDE&selected={caseId:D}");
        Assert.Contains("<dt>Due</dt><dd>18 Jun 2040</dd>", search, StringComparison.Ordinal);

        // A chase time beats the Due by date, and work due this instant is
        // already late (due <= now, as the Work Centre reads it).
        var now = factory.Services.GetRequiredService<TimeProvider>().GetUtcNow();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            var due = await context.CaseDueWork.SingleAsync(item => item.CaseId == caseId);
            due.NextChaseAtUtc = now;
            await context.SaveChangesAsync();
        }
        var late = await GetAsync(client, $"/Cases?queue=review&selected={caseId:D}");
        Assert.Contains(
            $"<td class=\"nowrap cases-late\">{OperatorLabels.DueDate(now)}</td>", late, StringComparison.Ordinal);
        Assert.DoesNotContain("<td class=\"nowrap\">18 Jun 2040</td>", late, StringComparison.Ordinal);
    }

    private static async Task<string> GetAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }
}
