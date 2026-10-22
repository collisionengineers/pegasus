using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Reports;
using Pegasus.Core.Tasks;
using Pegasus.Infrastructure.Persistence;
using Pegasus.IntegrationTests.Reports;
using Pegasus.Web.Authentication;
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
        Assert.Matches(new Regex(@"<dt>Due</dt>\s*<dd>18 Jun 2040</dd>"), list);
        Assert.Matches(
            new Regex(@"<dt>Current work</dt>\s*<dd>" + Regex.Escape(CaseWorkspaceLabels.AssignEngineer) + "</dd>"),
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

    /// <summary>
    /// A With Engineer Case whose report send was accepted before its Sent
    /// evidence is the one queued Case with no step: its quick detail lists no
    /// Current work row (R306), and its other facts remain.
    /// </summary>
    [Fact]
    public async Task AQueuedCaseWithNoStepListsNoCurrentWork()
    {
        using var baseFactory = new IntakeWebApplicationFactory(
            "Development", true, recognitionEngine: new FakeVrmRecognitionEngine());
        using var seedClient = IntakeWebDriver.CreateClient(baseFactory);
        var caseId = await ImageIntakeTestData.SeedInstructionCaseAsync(
            baseFactory, seedClient, "AB12 CDE", "LIST-DUE-02");
        await using (var context = await baseFactory.Database.CreateContextAsync())
        {
            var workflow = await context.CaseWorkflows.SingleAsync(item => item.CaseId == caseId);
            workflow.State = nameof(Pegasus.Core.Workflow.CaseLifecycleState.ReportPreparation);
            workflow.AssignedEngineerId = DevelopmentOfflineIdentity.AdministratorId;
            await context.SaveChangesAsync();
        }
        var reports = new CaseViewsWebTests.ReportsPerWork(caseId);
        var sends = new CaseViewsWebTests.SendsPerWork { Inspection = 1, Audit = 1 };
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            CaseWebTestSupport.Substitute<ICaseReportGenerationStore>(services, reports);
            CaseWebTestSupport.Substitute<ICaseReportSendHistoryQueries>(services, sends);
            CaseWebTestSupport.Substitute<ICaseReportSnapshotSource>(
                services,
                new AssessmentReportDraftWebTests.FakeProjectionSource(AssessmentReportDraftWebTests.ReadyInput(caseId)));
        }));
        using var client = IntakeWebDriver.CreateClient(factory);

        var list = await GetAsync(client, $"/Cases?tab=with_engineer&selected={caseId:D}");

        Assert.Contains("<dt>Engineer</dt>", list, StringComparison.Ordinal);
        Assert.DoesNotContain("<dt>Current work</dt>", list, StringComparison.Ordinal);
    }

    private static async Task<string> GetAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }
}
