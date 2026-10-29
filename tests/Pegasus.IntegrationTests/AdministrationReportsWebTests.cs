using System.Collections.Concurrent;
using System.Data.Common;
using System.Net;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Pegasus.Core.Reports;
using OperatorLabels = Pegasus.Web.Presentation.OperatorLabels;
using ReportsModel = Pegasus.Web.Pages.Administration.ReportsModel;

namespace Pegasus.IntegrationTests;

/// <summary>Administration → Management Reports through the real page over the seeded LocalDB: Design A, the downloads and the sorts.</summary>
[Trait("Category", "SqlServer")]
public sealed class AdministrationReportsWebTests
{
    private const string Page = "/Administration/Reports";

    [Fact]
    public async Task TheWorkbookDownloadsOneSheetPerReportForThePeriod()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = IntakeWebDriver.CreateClient(factory);

        using var response = await client.GetAsync($"{Page}?handler=Workbook&from=2031-04-01T00:00&to=2031-05-01T00:00");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("administration-reports-2031-04-01-2031-05-01.xlsx", response.Content.Headers.ContentDisposition!.FileName!.Trim('"'));
        using var stream = new MemoryStream(await response.Content.ReadAsByteArrayAsync());
        using var document = SpreadsheetDocument.Open(stream, false);
        var names = document.WorkbookPart!.Workbook!.Sheets!.Elements<Sheet>().Select(sheet => sheet.Name!.Value!).ToArray();
        Assert.Equal(["Engineer activity", "Reports by Principal", "By month", "Outcomes", "Turnaround", "Queues"], names);

        // Every Inspection and Audit split the page's Work choice draws from
        // sits beside its total on both the per-Principal and month sheets.
        var workbookPart = document.WorkbookPart!;
        foreach (var (sheetName, measures) in new[]
        {
            ("Reports by Principal", new[] { "Reports produced", "Reports sent", "Agreed fees" }),
            ("By month", new[] { "Reports produced", "Fee notes produced", "Reports sent", "Agreed fees" })
        })
        {
            var sheet = workbookPart.Workbook!.Sheets!.Elements<Sheet>().Single(candidate => candidate.Name!.Value == sheetName);
            var worksheet = (WorksheetPart)workbookPart.GetPartById(sheet.Id!.Value!);
            var header = worksheet.Worksheet!.GetFirstChild<SheetData>()!.Elements<Row>().First()
                .Elements<Cell>().Select(cell => cell.InlineString!.Text!.Text).ToArray();
            foreach (var measure in measures)
            {
                var total = Array.IndexOf(header, measure);
                Assert.True(total >= 0, $"{sheetName} lacks {measure}.");
                Assert.Equal([$"{measure} · Inspection", $"{measure} · Audit"], header.Skip(total + 1).Take(2));
            }
        }
    }

    /// <summary>
    /// Design A: the period bar under the title, four tabs after it (v41 A26),
    /// Person and Work in the section heads, each section's own download, one
    /// sort arrow (site.css draws it from aria-sort), a count's first click
    /// largest first, and no MI labels or Engineer activity note.
    /// </summary>
    [Fact]
    public async Task ThePageIsDesignAWithItsTabsSortsWorkChoicesAndDownloads()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = IntakeWebDriver.CreateClient(factory);

        using var response = await client.GetAsync($"{Page}?sort=queries&dir=desc&work=audit");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("<select id=\"report-period\" name=\"period\" data-period-preset>", html, StringComparison.Ordinal);
        Assert.Contains("<option value=\"custom\" selected=\"selected\">Custom</option>", html, StringComparison.Ordinal);
        // Four tabs as links that keep the choices; the Engineers tab is the default.
        foreach (var (value, label) in ReportsModel.Tabs)
        {
            Assert.Contains($"tab={value}", html, StringComparison.Ordinal);
            Assert.Contains($">{label}</a>", html, StringComparison.Ordinal);
        }
        Assert.Matches("aria-current=\"page\">" + OperatorLabels.ReportTabs.Engineers + "</a>", html);
        Assert.Contains("handler=Workbook", html, StringComparison.Ordinal);

        // Engineers: Engineer activity, its Person choice, sort and download.
        Assert.Contains("<select id=\"report-engineer\" name=\"engineerId\">", html, StringComparison.Ordinal);
        Assert.Contains("handler=Csv", html, StringComparison.Ordinal);
        Assert.Contains("aria-sort=\"descending\"", html, StringComparison.Ordinal);
        // The sorted column reverses; an unsorted count's first click is largest first.
        Assert.Contains("sort=queries&amp;dir=asc", html, StringComparison.Ordinal);
        Assert.Contains("id=\"mi01-title\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-sort-arrow", html, StringComparison.Ordinal);
        Assert.DoesNotContain("MI01", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Queries received are credited", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Amendment requests", html, StringComparison.Ordinal); // An amendment request is a query.
        Assert.DoesNotContain("Disputes", html, StringComparison.Ordinal); // A dispute is a query.

        // Principals: Reports by Principal, By month, Outcomes and Turnaround.
        var principals = await client.GetStringAsync($"{Page}?tab=principals&work=audit");
        Assert.Contains("<option value=\"audit\" selected=\"selected\">Audit</option>", principals, StringComparison.Ordinal);
        foreach (var handler in new[] { "PrincipalCsv", "MonthsCsv", "OutcomesCsv", "TurnaroundCsv" })
        {
            Assert.Contains($"handler={handler}", principals, StringComparison.Ordinal);
        }
        Assert.Contains("msort=produced&amp;mdir=desc", principals, StringComparison.Ordinal);
        Assert.Contains("msort=code&amp;mdir=asc", principals, StringComparison.Ordinal);
        // The Work choice survives every link.
        Assert.Contains("work=audit", principals, StringComparison.Ordinal);
        Assert.Contains("id=\"mi02-title\"", principals, StringComparison.Ordinal);
        Assert.Contains("id=\"months-title\"", principals, StringComparison.Ordinal);
        Assert.Contains("id=\"outcomes-title\"", principals, StringComparison.Ordinal);
        Assert.Contains("id=\"mi03-title\"", principals, StringComparison.Ordinal);
        // Reports by Principal: four columns, the Work choice picks the figures.
        Assert.DoesNotContain("Reports produced &#xB7; Inspection", principals, StringComparison.Ordinal);

        // Queues.
        var queues = await client.GetStringAsync($"{Page}?tab=queues");
        Assert.Contains("id=\"queues-title\"", queues, StringComparison.Ordinal);
        Assert.Contains("handler=QueuesCsv", queues, StringComparison.Ordinal);
    }

    /// <summary>Item B2: a period that ends before it starts draws no report and no figure, only the error and the Case list tab.</summary>
    [Fact]
    public async Task AnInvalidPeriodShowsTheErrorAndDrawsNoReport()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = IntakeWebDriver.CreateClient(factory);

        using var response = await client.GetAsync($"{Page}?from=2031-05-01T00:00&to=2031-04-01T00:00");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Choose a valid date range.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"mi01-title\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("metric-value", html, StringComparison.Ordinal);
        Assert.DoesNotContain("handler=Workbook", html, StringComparison.Ordinal);

        var caseList = await client.GetStringAsync($"{Page}?tab=case-list&from=2031-05-01T00:00&to=2031-04-01T00:00");
        Assert.Contains("data-case-list", caseList, StringComparison.Ordinal);

        using var csv = await client.GetAsync($"{Page}?handler=Csv&from=2031-05-01T00:00&to=2031-04-01T00:00");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, csv.StatusCode);
    }

    [Fact]
    public async Task APeriodChoiceIsKeptAndEachCsvUsesThePageHeadings()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = IntakeWebDriver.CreateClient(factory);

        var html = await client.GetStringAsync($"{Page}?period=last-month");
        Assert.Contains("<option value=\"last-month\" selected=\"selected\">Last month</option>", html, StringComparison.Ordinal);

        using var csv = await client.GetAsync($"{Page}?handler=Csv&period=last-month");
        Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, (await csv.Content.ReadAsByteArrayAsync())[..3]);
        Assert.StartsWith(
            "Person,Queries received,Reports sent,Audit reports sent,Received to sent\r\n",
            await csv.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
        using var turnaround = await client.GetAsync($"{Page}?handler=TurnaroundCsv");
        Assert.StartsWith("Principal,Time to produce,Time to ready,Time to send\r\n", await turnaround.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidMonthlyDataRendersUnavailableWithoutFailingThePage()
    {
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IMonthlyReportActivityQueries>();
                services.AddSingleton<IMonthlyReportActivityQueries, InvalidMonthlyReportQueries>();
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });

        using var response = await client.GetAsync($"{Page}?tab=principals");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("data-reports-by-month", html, StringComparison.Ordinal);
        Assert.Contains("<td colspan=\"6\" class=\"muted\">Unavailable</td>", html, StringComparison.Ordinal);

        using var workbookResponse = await client.GetAsync($"{Page}?handler=Workbook");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, workbookResponse.StatusCode);
        await AssertCsvsAsync(client, refused: ["MonthsCsv"]);
    }

    /// <summary>
    /// #1575: a failed read still renders Unavailable, and the page logs it
    /// once, as a Warning naming the read and the fault without the exception
    /// object (engineering.md, fault handling shape).
    /// </summary>
    [Fact]
    public async Task OperationalMonthlyFailureRendersUnavailableWithoutFailingThePage()
    {
        var logs = new CapturingLoggerProvider();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IMonthlyReportActivityQueries>();
                services.AddSingleton<IMonthlyReportActivityQueries, UnavailableMonthlyReportQueries>();
                services.AddSingleton<ILoggerProvider>(logs);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });

        using var response = await client.GetAsync($"{Page}?tab=principals");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("<td colspan=\"6\" class=\"muted\">Unavailable</td>", html, StringComparison.Ordinal);
        var logged = Assert.Single(logs.Entries, entry => entry.Category == typeof(ReportsModel).FullName);
        Assert.Equal(LogLevel.Warning, logged.Level);
        Assert.Null(logged.Exception);
        Assert.Contains("by_month", logged.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(SyntheticDbException), logged.Message, StringComparison.Ordinal);

        using var workbookResponse = await client.GetAsync($"{Page}?handler=Workbook");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, workbookResponse.StatusCode);
        await AssertCsvsAsync(client, refused: ["MonthsCsv"]);
    }

    [Fact]
    public async Task InvalidEngineerDataRendersUnavailableWithoutFailingThePage()
    {
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IEngineerActivityQueries>();
                services.AddSingleton<IEngineerActivityQueries, InvalidEngineerActivityQueries>();
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });

        using var response = await client.GetAsync(Page);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("<span class=\"metric-value\">Unavailable</span>", html, StringComparison.Ordinal);
        Assert.Contains("<td colspan=\"5\" class=\"muted\">Unavailable</td>", html, StringComparison.Ordinal);

        using var workbookResponse = await client.GetAsync($"{Page}?handler=Workbook");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, workbookResponse.StatusCode);
        await AssertCsvsAsync(client, refused: ["Csv"]);
    }

    [Fact]
    public async Task WorkbookRefusesUnavailablePrincipalData()
    {
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IV1ActivityReportQueries>();
                services.AddSingleton<IV1ActivityReportQueries, InvalidPrincipalReportQueries>();
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });

        using var response = await client.GetAsync($"{Page}?handler=Workbook");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        await AssertCsvsAsync(client, refused: ["PrincipalCsv", "TurnaroundCsv", "QueuesCsv"]);
    }

    /// <summary>#1157: a CSV runs its own report's read and no other.</summary>
    [Fact]
    public async Task EachCsvRunsOnlyItsOwnReportsRead()
    {
        var recorder = new RecordingReportQueries();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IEngineerActivityQueries>();
                services.RemoveAll<IMonthlyReportActivityQueries>();
                services.RemoveAll<IReportOutcomeQueries>();
                services.AddSingleton<IEngineerActivityQueries>(recorder);
                services.AddSingleton<IMonthlyReportActivityQueries>(recorder);
                services.AddSingleton<IReportOutcomeQueries>(recorder);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });

        using var queues = await client.GetAsync($"{Page}?handler=QueuesCsv");
        Assert.Equal(HttpStatusCode.OK, queues.StatusCode);
        Assert.Equal((0, 0, 0, 0), recorder.Calls);

        using var months = await client.GetAsync($"{Page}?handler=MonthsCsv");
        Assert.Equal(HttpStatusCode.OK, months.StatusCode);
        Assert.Equal((0, 1, 0, 0), recorder.Calls);
    }

    /// <summary>
    /// F254 (R326): with a Person chosen, the period before and the Engineer CSV
    /// read only that sender; the Engineers tab's own period read stays
    /// unfiltered because it draws the Person choices.
    /// </summary>
    [Fact]
    public async Task AChosenPersonNarrowsThePreviousPeriodAndTheCsvReadsButNotTheChoices()
    {
        var recorder = new RecordingReportQueries();
        var person = Guid.NewGuid();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IEngineerActivityQueries>();
                services.AddSingleton<IEngineerActivityQueries>(recorder);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });

        using (var engineers = await client.GetAsync($"{Page}?engineerId={person:D}"))
        {
            Assert.Equal(HttpStatusCode.OK, engineers.StatusCode);
        }
        Assert.Equal(new Guid?[] { null, person }, recorder.EngineerIds);

        recorder.Reset();
        using (var csv = await client.GetAsync($"{Page}?handler=Csv&engineerId={person:D}"))
        {
            Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
        }
        Assert.Equal(new Guid?[] { person }, recorder.EngineerIds);
    }

    /// <summary>
    /// A chosen Person with no activity is kept in the Engineers picker
    /// wherever the account sorts, past the hundredth included (#1542, FRD-17).
    /// </summary>
    [Fact]
    public async Task TheEngineerPickerKeepsAChosenAccountBeyondTheHundredth()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        await IntakeWebDriver.SeedUserAccountsAsync(factory, 101);
        var beyond = await IntakeWebDriver.StaffIdAsync(factory, "bulk-100");
        using var client = IntakeWebDriver.CreateClient(factory);

        var html = await IntakeWebDriver.GetHtmlAsync(client, $"{Page}?engineerId={beyond:D}");

        Assert.Contains($"<option value=\"{beyond:D}\" selected=\"selected\">bulk-100</option>", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// PERF-WEB-08: a tab runs its own reports' reads and no other tab's. The
    /// Engineers and Principals tabs also read the period just before for the
    /// totals that show it (R156); Queues and Case list read none.
    /// </summary>
    [Fact]
    public async Task EachTabRunsOnlyItsOwnReads()
    {
        var recorder = new RecordingReportQueries();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IEngineerActivityQueries>();
                services.RemoveAll<IMonthlyReportActivityQueries>();
                services.RemoveAll<IReportOutcomeQueries>();
                services.RemoveAll<IV1ActivityReportQueries>();
                services.AddSingleton<IEngineerActivityQueries>(recorder);
                services.AddSingleton<IMonthlyReportActivityQueries>(recorder);
                services.AddSingleton<IReportOutcomeQueries>(recorder);
                services.AddSingleton<IV1ActivityReportQueries>(recorder);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });

        // The default tab is Engineers: the period and the period before.
        using (var engineers = await client.GetAsync(Page))
        {
            Assert.Equal(HttpStatusCode.OK, engineers.StatusCode);
        }
        Assert.Equal((2, 0, 0, 0), recorder.Calls);

        recorder.Reset();
        using (var principals = await client.GetAsync($"{Page}?tab={ReportsModel.PrincipalsTab}"))
        {
            Assert.Equal(HttpStatusCode.OK, principals.StatusCode);
        }
        Assert.Equal((0, 1, 1, 2), recorder.Calls);

        recorder.Reset();
        using (var queues = await client.GetAsync($"{Page}?tab={ReportsModel.QueuesTab}"))
        {
            Assert.Equal(HttpStatusCode.OK, queues.StatusCode);
        }
        Assert.Equal((0, 0, 0, 1), recorder.Calls);

        recorder.Reset();
        using (var caseList = await client.GetAsync($"{Page}?tab={ReportsModel.CaseListTab}"))
        {
            Assert.Equal(HttpStatusCode.OK, caseList.StatusCode);
        }
        Assert.Equal((0, 0, 0, 0), recorder.Calls);
    }

    /// <summary>#1159: with nothing held, the Queues table says so rather than drawing nothing.</summary>
    [Fact]
    public async Task TheQueuesTableSaysWhenNothingIsHeld()
    {
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IV1ActivityReportQueries>();
                services.AddSingleton<IV1ActivityReportQueries, EmptyPrincipalReportQueries>();
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });

        var html = await client.GetStringAsync($"{Page}?tab=queues");

        Assert.Contains("<td colspan=\"5\" class=\"muted\">Nothing is currently held.</td>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NonAdministratorsCannotDownloadTheWorkbook()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = IntakeWebDriver.CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Engineer");

        using var response = await client.GetAsync($"{Page}?handler=Workbook");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ThePageIsManagementReportsAndOffersTheCaseListWithoutReportTypes()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = IntakeWebDriver.CreateClient(factory);

        var html = await client.GetStringAsync($"{Page}?tab=case-list");

        Assert.Contains("<title>Management Reports", html, StringComparison.Ordinal);
        Assert.Contains("data-case-list", html, StringComparison.Ordinal);
        Assert.Contains("value=\"case.reference\" checked=\"checked\"", html, StringComparison.Ordinal);
        Assert.Contains("value=\"original.agrees\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("value=\"original.agrees\" checked", html, StringComparison.Ordinal);
        Assert.Contains("handler=CaseListCsv", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Report types", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheCaseListDownloadsTheChosenColumnsAsCsvAndWorkbook()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = IntakeWebDriver.CreateClient(factory);
        var token = CaseWebTestSupport.AntiforgeryValue(await client.GetStringAsync($"{Page}?tab=case-list"));

        using var csv = await client.PostAsync($"{Page}?handler=CaseListCsv", CaseListForm(token, ("AllTime", "true"), ("Columns", "case.type"), ("Columns", "case.reference")));

        Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
        Assert.Equal("case-list-all-time.csv", csv.Content.Headers.ContentDisposition!.FileName!.Trim('"'));
        Assert.StartsWith("Case/PO,Case type\r\n", await csv.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        using var workbook = await client.PostAsync($"{Page}?handler=CaseListWorkbook", CaseListForm(
            token, ("ReceivedFrom", "2031-04-01"), ("ReceivedTo", "2031-04-30"), ("Columns", "case.received")));

        Assert.Equal(HttpStatusCode.OK, workbook.StatusCode);
        Assert.Equal("case-list-2031-04-01-2031-04-30.xlsx", workbook.Content.Headers.ContentDisposition!.FileName!.Trim('"'));
        using var stream = new MemoryStream(await workbook.Content.ReadAsByteArrayAsync());
        using var document = SpreadsheetDocument.Open(stream, false);
        Assert.Equal(CaseListTables.SheetName, document.WorkbookPart!.Workbook!.Sheets!.Elements<Sheet>().Single().Name!.Value);
    }

    [Fact]
    public async Task ARefusedCaseListTellsAScriptWhyAndShowsThePageWithoutOne()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = IntakeWebDriver.CreateClient(factory);
        var token = CaseWebTestSupport.AntiforgeryValue(await client.GetStringAsync($"{Page}?tab=case-list"));

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{Page}?handler=CaseListCsv")
        {
            Content = CaseListForm(token, ("AllTime", "true"))
        };
        request.Headers.Add("X-Requested-With", "fetch");
        using var scripted = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, scripted.StatusCode);
        Assert.Equal("text/plain", scripted.Content.Headers.ContentType!.MediaType);
        Assert.Equal("Choose at least one column.", await scripted.Content.ReadAsStringAsync());

        using var plain = await client.PostAsync($"{Page}?handler=CaseListCsv", CaseListForm(
            token, ("ReceivedFrom", "2031-04-01"), ("Columns", "case.reference")));

        Assert.Equal(HttpStatusCode.OK, plain.StatusCode);
        var html = await plain.Content.ReadAsStringAsync();
        Assert.Contains("Choose both received dates", html, StringComparison.Ordinal);
        Assert.Contains("value=\"case.reference\" checked=\"checked\"", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// #1573 (FRD-17): the Case list form carries the page's period, sorts,
    /// Work choices and Person, so a refused download redraws the page with
    /// them; its buttons post to a handler address without the query string.
    /// </summary>
    [Fact]
    public async Task ARefusedCaseListDownloadKeepsThePeriodSortWorkAndPerson()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = IntakeWebDriver.CreateClient(factory);
        var state = PageState(Guid.NewGuid());
        var html = await client.GetStringAsync($"{Page}?{string.Join('&', state.Select(field => $"{field.Name}={field.Value}"))}");
        AssertCarries(CaseListFormOf(html), state);

        using var refused = await client.PostAsync($"{Page}?handler=CaseListCsv", CaseListForm(
            CaseWebTestSupport.AntiforgeryValue(html), [.. state, ("AllTime", "true")]));

        Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
        var redrawn = await refused.Content.ReadAsStringAsync();
        Assert.Contains(OperatorLabels.CaseList.ChooseColumns, redrawn, StringComparison.Ordinal);
        Assert.Contains($"<option value=\"last-month\" selected=\"selected\">{OperatorLabels.Reports.LastMonth}</option>", redrawn, StringComparison.Ordinal);
        AssertCarries(CaseListFormOf(redrawn), state);
    }

    /// <summary>#1573: a saved preset's redirect keeps the page's state beside the preset.</summary>
    [Fact]
    public async Task APresetSaveRedirectKeepsThePeriodSortWorkAndPerson()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = IntakeWebDriver.CreateClient(factory);
        var token = CaseWebTestSupport.AntiforgeryValue(await client.GetStringAsync($"{Page}?tab=case-list"));
        var state = PageState(Guid.NewGuid());
        var presetId = Guid.NewGuid();

        using var created = await client.PostAsync($"{Page}?handler=CaseListPresetCreate", CaseListForm(
            token,
            [
                .. state,
                ("PresetName", $"Kept state {presetId:N}"),
                ("NewPresetId", presetId.ToString("D")),
                ("OperationKey", Guid.NewGuid().ToString("N")),
                ("Columns", "case.reference")
            ]));

        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        var location = created.Headers.Location!.OriginalString;
        foreach (var (name, value) in state.Append(("preset", presetId.ToString("D"))))
        {
            Assert.Contains($"{name}={value}", location, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task APresetIsSavedChosenUpdatedAndRemoved()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = IntakeWebDriver.CreateClient(factory);
        var token = CaseWebTestSupport.AntiforgeryValue(await client.GetStringAsync($"{Page}?tab=case-list"));
        var presetId = Guid.NewGuid();

        using var created = await client.PostAsync($"{Page}?handler=CaseListPresetCreate", CaseListForm(
            token,
            ("PresetName", "Invoicing"),
            ("NewPresetId", presetId.ToString("D")),
            ("OperationKey", Guid.NewGuid().ToString("N")),
            ("Columns", "case.reference"),
            ("Columns", "agreed_fee.inspection")));

        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        Assert.Contains($"preset={presetId:D}", created.Headers.Location!.OriginalString, StringComparison.Ordinal);
        var chosen = await client.GetStringAsync($"{Page}?tab=case-list&preset={presetId:D}");
        Assert.Contains("value=\"agreed_fee.inspection\" checked=\"checked\"", chosen, StringComparison.Ordinal);
        Assert.DoesNotContain("value=\"case.type\" checked", chosen, StringComparison.Ordinal);
        Assert.Contains("value=\"Invoicing\"", chosen, StringComparison.Ordinal);

        using var duplicate = await client.PostAsync($"{Page}?handler=CaseListPresetCreate", CaseListForm(
            token,
            ("PresetName", "invoicing"),
            ("NewPresetId", Guid.NewGuid().ToString("D")),
            ("OperationKey", Guid.NewGuid().ToString("N")),
            ("Columns", "case.reference")));

        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        Assert.Contains("Another preset already has that name.", await duplicate.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        using var saved = await client.PostAsync($"{Page}?handler=CaseListPresetSave", CaseListForm(
            token,
            ("PresetId", presetId.ToString("D")),
            ("ExpectedVersion", "1"),
            ("PresetName", "Invoicing"),
            ("OperationKey", Guid.NewGuid().ToString("N")),
            ("Columns", "case.type")));

        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        Assert.Contains("value=\"case.type\" checked=\"checked\"", await client.GetStringAsync($"{Page}?tab=case-list&preset={presetId:D}"), StringComparison.Ordinal);

        using var removed = await client.PostAsync($"{Page}?handler=CaseListPresetRemove", CaseListForm(
            token,
            ("PresetId", presetId.ToString("D")),
            ("ExpectedVersion", "2"),
            ("OperationKey", Guid.NewGuid().ToString("N"))));

        Assert.Equal(HttpStatusCode.Redirect, removed.StatusCode);
        Assert.DoesNotContain(">Invoicing</option>", await client.GetStringAsync($"{Page}?tab=case-list"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task NonAdministratorsCannotDownloadTheCaseListOrKeepPresets()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = IntakeWebDriver.CreateClient(factory);
        var token = CaseWebTestSupport.AntiforgeryValue(await client.GetStringAsync($"{Page}?tab=case-list"));
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Engineer");

        foreach (var handler in new[] { "CaseListCsv", "CaseListWorkbook", "CaseListPresetCreate" })
        {
            using var response = await client.PostAsync($"{Page}?handler={handler}", CaseListForm(
                token, ("AllTime", "true"), ("Columns", "case.reference"), ("PresetName", "Mine"), ("OperationKey", Guid.NewGuid().ToString("N"))));
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    private static FormUrlEncodedContent CaseListForm(string token, params (string Name, string Value)[] fields) =>
        new([new("__RequestVerificationToken", token), .. fields.Select(field => new KeyValuePair<string, string>(field.Name, field.Value))]);

    /// <summary>The Case list tab with a period, an Engineer activity sort, a Work choice and a Person, as query fields.</summary>
    private static (string Name, string Value)[] PageState(Guid person) =>
    [
        ("tab", ReportsModel.CaseListTab),
        ("period", "last-month"),
        ("engineerId", person.ToString("D")),
        ("sort", "queries"),
        ("dir", "desc"),
        ("work", "audit")
    ];

    /// <summary>The Case list's own post form, found by its preset field rather than its attributes.</summary>
    private static string CaseListFormOf(string html)
    {
        var field = html.IndexOf("name=\"NewPresetId\"", StringComparison.Ordinal);
        Assert.True(field >= 0, "The page has no Case list form.");
        var start = html.LastIndexOf("<form", field, StringComparison.Ordinal);
        var end = html.IndexOf("</form>", field, StringComparison.Ordinal);
        return html[start..end];
    }

    private static void AssertCarries(string form, IEnumerable<(string Name, string Value)> state)
    {
        foreach (var (name, value) in state)
        {
            Assert.Contains($"<input type=\"hidden\" name=\"{name}\" value=\"{value}\" />", form, StringComparison.Ordinal);
        }
    }

    /// <summary>Item L: a report's CSV refuses only when its own read failed.</summary>
    private static async Task AssertCsvsAsync(HttpClient client, string[] refused)
    {
        foreach (var handler in new[] { "Csv", "PrincipalCsv", "MonthsCsv", "OutcomesCsv", "TurnaroundCsv", "QueuesCsv" })
        {
            using var response = await client.GetAsync($"{Page}?handler={handler}");
            Assert.Equal(
                refused.Contains(handler) ? HttpStatusCode.UnprocessableEntity : HttpStatusCode.OK,
                response.StatusCode);
        }
    }

    private sealed class InvalidMonthlyReportQueries : IMonthlyReportActivityQueries
    {
        public Task<IReadOnlyList<MonthlyReportActivity>> GetAsync(
            DateTimeOffset fromUtc,
            DateTimeOffset toUtc,
            CancellationToken cancellationToken) =>
            Task.FromException<IReadOnlyList<MonthlyReportActivity>>(
                new InvalidDataException("Malformed monthly report snapshot."));
    }

    private sealed class UnavailableMonthlyReportQueries : IMonthlyReportActivityQueries
    {
        public Task<IReadOnlyList<MonthlyReportActivity>> GetAsync(
            DateTimeOffset fromUtc,
            DateTimeOffset toUtc,
            CancellationToken cancellationToken) =>
            Task.FromException<IReadOnlyList<MonthlyReportActivity>>(new SyntheticDbException());
    }

    private sealed class InvalidEngineerActivityQueries : IEngineerActivityQueries
    {
        public Task<IReadOnlyList<EngineerActivityCounts>> GetAsync(
            DateTimeOffset fromUtc,
            DateTimeOffset toUtc,
            Guid? engineerId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<EngineerActivityCounts>>(
            [new(Guid.NewGuid(), 1, 1, AverageReceivedToSent: TimeSpan.FromHours(-1))]);
    }

    private sealed class SyntheticDbException : DbException
    {
        public SyntheticDbException() : base("The monthly report database is unavailable.") { }
    }

    private sealed record LogEntry(string Category, LogLevel Level, string Message, Exception? Exception);

    /// <summary>Every log entry the host writes, by category.</summary>
    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<LogEntry> entries = new();

        public IReadOnlyList<LogEntry> Entries => [.. entries];

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, entries);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(string category, ConcurrentQueue<LogEntry> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter) =>
                entries.Enqueue(new(category, logLevel, formatter(state, exception), exception));
        }
    }

    private sealed class RecordingReportQueries : IEngineerActivityQueries, IMonthlyReportActivityQueries, IReportOutcomeQueries, IV1ActivityReportQueries
    {
        private int engineer;
        private int monthly;
        private int outcomes;
        private int principal;
        private readonly System.Collections.Concurrent.ConcurrentQueue<Guid?> engineerIds = new();

        /// <summary>The sender each Engineer read asked for, in the order the reads started; null is every sender.</summary>
        public IReadOnlyList<Guid?> EngineerIds => [.. engineerIds];

        public (int Engineer, int Monthly, int Outcomes, int Principal) Calls => (engineer, monthly, outcomes, principal);

        public void Reset()
        {
            engineer = 0;
            engineerIds.Clear();
            monthly = 0;
            outcomes = 0;
            principal = 0;
        }

        Task<IReadOnlyList<PrincipalReportActivity>> IV1ActivityReportQueries.GetAsync(
            DateTimeOffset fromUtc,
            DateTimeOffset toUtc,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref principal);
            return Task.FromResult<IReadOnlyList<PrincipalReportActivity>>([]);
        }

        Task<IReadOnlyList<EngineerActivityCounts>> IEngineerActivityQueries.GetAsync(
            DateTimeOffset fromUtc,
            DateTimeOffset toUtc,
            Guid? engineerId,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref engineer);
            engineerIds.Enqueue(engineerId);
            return Task.FromResult<IReadOnlyList<EngineerActivityCounts>>([]);
        }

        Task<IReadOnlyList<MonthlyReportActivity>> IMonthlyReportActivityQueries.GetAsync(
            DateTimeOffset fromUtc,
            DateTimeOffset toUtc,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref monthly);
            return Task.FromResult<IReadOnlyList<MonthlyReportActivity>>([]);
        }

        Task<IReadOnlyList<ReportOutcomeFact>> IReportOutcomeQueries.GetAsync(
            DateTimeOffset fromUtc,
            DateTimeOffset toUtc,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref outcomes);
            return Task.FromResult<IReadOnlyList<ReportOutcomeFact>>([]);
        }
    }

    private sealed class EmptyPrincipalReportQueries : IV1ActivityReportQueries
    {
        public Task<IReadOnlyList<PrincipalReportActivity>> GetAsync(
            DateTimeOffset fromUtc,
            DateTimeOffset toUtc,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PrincipalReportActivity>>([]);
    }

    private sealed class InvalidPrincipalReportQueries : IV1ActivityReportQueries
    {
        public Task<IReadOnlyList<PrincipalReportActivity>> GetAsync(
            DateTimeOffset fromUtc,
            DateTimeOffset toUtc,
            CancellationToken cancellationToken) =>
            Task.FromException<IReadOnlyList<PrincipalReportActivity>>(
                new InvalidDataException("Malformed principal report snapshot."));
    }
}
