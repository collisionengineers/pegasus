using System.Data.Common;
using System.Net;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Reports;

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
        using var client = CreateClient(factory);

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
    /// Design A: the period bar under the title, Person and Work in the
    /// section heads, each section's own download, one sort arrow (site.css
    /// draws it from aria-sort), a count's first click largest first, and no
    /// MI labels or Engineer activity note.
    /// </summary>
    [Fact]
    public async Task ThePageIsDesignAWithItsSortsWorkChoicesAndDownloads()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = CreateClient(factory);

        using var response = await client.GetAsync($"{Page}?sort=queries&dir=desc&work=audit");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("<select id=\"report-period\" name=\"period\" data-period-preset>", html, StringComparison.Ordinal);
        Assert.Contains("<option value=\"custom\" selected=\"selected\">Custom</option>", html, StringComparison.Ordinal);
        Assert.Contains("<select id=\"report-engineer\" name=\"engineerId\">", html, StringComparison.Ordinal);
        Assert.Contains("<option value=\"audit\" selected=\"selected\">Audit</option>", html, StringComparison.Ordinal);
        foreach (var handler in new[] { "Workbook", "Csv", "PrincipalCsv", "MonthsCsv", "OutcomesCsv", "TurnaroundCsv", "QueuesCsv" })
        {
            Assert.Contains($"handler={handler}", html, StringComparison.Ordinal);
        }

        Assert.Contains("aria-sort=\"descending\"", html, StringComparison.Ordinal);
        // The sorted column reverses; an unsorted count's first click is largest first.
        Assert.Contains("sort=queries&amp;dir=asc", html, StringComparison.Ordinal);
        Assert.Contains("msort=produced&amp;mdir=desc", html, StringComparison.Ordinal);
        Assert.Contains("msort=code&amp;mdir=asc", html, StringComparison.Ordinal);
        // The Work choice and the sort survive every link.
        Assert.Contains("work=audit", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-sort-arrow", html, StringComparison.Ordinal);
        Assert.DoesNotContain("MI01", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Queries received are credited", html, StringComparison.Ordinal);
        Assert.Contains("id=\"months-title\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"outcomes-title\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"queues-title\"", html, StringComparison.Ordinal);
        Assert.Contains("Amendment requests", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Disputes", html, StringComparison.Ordinal); // A dispute is a query.
        // Reports by Principal: four columns, the Work choice picks the figures.
        Assert.DoesNotContain("Reports produced &#xB7; Inspection", html, StringComparison.Ordinal);
    }

    /// <summary>Item B2: a period that ends before it starts draws no report and no figure, only the error and the Case list.</summary>
    [Fact]
    public async Task AnInvalidPeriodShowsTheErrorAndDrawsNoReport()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = CreateClient(factory);

        using var response = await client.GetAsync($"{Page}?from=2031-05-01T00:00&to=2031-04-01T00:00");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Choose a valid date range.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"mi01-title\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("metric-value", html, StringComparison.Ordinal);
        Assert.DoesNotContain("handler=Workbook", html, StringComparison.Ordinal);
        Assert.Contains("data-case-list", html, StringComparison.Ordinal);

        using var csv = await client.GetAsync($"{Page}?handler=Csv&from=2031-05-01T00:00&to=2031-04-01T00:00");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, csv.StatusCode);
    }

    [Fact]
    public async Task APeriodChoiceIsKeptAndEachCsvUsesThePageHeadings()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = CreateClient(factory);

        var html = await client.GetStringAsync($"{Page}?period=last-month");
        Assert.Contains("<option value=\"last-month\" selected=\"selected\">Last month</option>", html, StringComparison.Ordinal);

        using var csv = await client.GetAsync($"{Page}?handler=Csv&period=last-month");
        Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
        Assert.StartsWith(
            "Person,Queries received,Amendment requests,Reports sent,Audit reports sent,Received to sent\r\n",
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

        using var response = await client.GetAsync(Page);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("data-reports-by-month", html, StringComparison.Ordinal);
        Assert.Contains("<td colspan=\"6\" class=\"muted\">Unavailable</td>", html, StringComparison.Ordinal);

        using var workbookResponse = await client.GetAsync($"{Page}?handler=Workbook");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, workbookResponse.StatusCode);
        await AssertCsvsAsync(client, refused: ["MonthsCsv"]);
    }

    [Fact]
    public async Task OperationalMonthlyFailureRendersUnavailableWithoutFailingThePage()
    {
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IMonthlyReportActivityQueries>();
                services.AddSingleton<IMonthlyReportActivityQueries, UnavailableMonthlyReportQueries>();
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });

        using var response = await client.GetAsync(Page);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("<td colspan=\"6\" class=\"muted\">Unavailable</td>", html, StringComparison.Ordinal);

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
        Assert.Contains("<td colspan=\"6\" class=\"muted\">Unavailable</td>", html, StringComparison.Ordinal);

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
        Assert.Equal((0, 0, 0), recorder.Calls);

        using var months = await client.GetAsync($"{Page}?handler=MonthsCsv");
        Assert.Equal(HttpStatusCode.OK, months.StatusCode);
        Assert.Equal((0, 1, 0), recorder.Calls);
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

        var html = await client.GetStringAsync(Page);

        Assert.Contains("<td colspan=\"5\" class=\"muted\">Nothing is currently held.</td>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NonAdministratorsCannotDownloadTheWorkbook()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Engineer");

        using var response = await client.GetAsync($"{Page}?handler=Workbook");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ThePageIsManagementReportsAndOffersTheCaseListWithoutReportTypes()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = CreateClient(factory);

        var html = await client.GetStringAsync(Page);

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
        using var client = CreateClient(factory);
        var token = CaseWebTestSupport.AntiforgeryValue(await client.GetStringAsync(Page));

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
        using var client = CreateClient(factory);
        var token = CaseWebTestSupport.AntiforgeryValue(await client.GetStringAsync(Page));

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

    [Fact]
    public async Task APresetIsSavedChosenUpdatedAndRemoved()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = CreateClient(factory);
        var token = CaseWebTestSupport.AntiforgeryValue(await client.GetStringAsync(Page));
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
        var chosen = await client.GetStringAsync($"{Page}?preset={presetId:D}");
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
        Assert.Contains("value=\"case.type\" checked=\"checked\"", await client.GetStringAsync($"{Page}?preset={presetId:D}"), StringComparison.Ordinal);

        using var removed = await client.PostAsync($"{Page}?handler=CaseListPresetRemove", CaseListForm(
            token,
            ("PresetId", presetId.ToString("D")),
            ("ExpectedVersion", "2"),
            ("OperationKey", Guid.NewGuid().ToString("N"))));

        Assert.Equal(HttpStatusCode.Redirect, removed.StatusCode);
        Assert.DoesNotContain(">Invoicing</option>", await client.GetStringAsync(Page), StringComparison.Ordinal);
    }

    [Fact]
    public async Task NonAdministratorsCannotDownloadTheCaseListOrKeepPresets()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = CreateClient(factory);
        var token = CaseWebTestSupport.AntiforgeryValue(await client.GetStringAsync(Page));
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

    private static HttpClient CreateClient(IntakeWebApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });

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
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<EngineerActivityCounts>>(
            [new(Guid.NewGuid(), 1, 1, AverageReceivedToSent: TimeSpan.FromHours(-1))]);
    }

    private sealed class SyntheticDbException : DbException
    {
        public SyntheticDbException() : base("The monthly report database is unavailable.") { }
    }

    private sealed class RecordingReportQueries : IEngineerActivityQueries, IMonthlyReportActivityQueries, IReportOutcomeQueries
    {
        private int engineer;
        private int monthly;
        private int outcomes;

        public (int Engineer, int Monthly, int Outcomes) Calls => (engineer, monthly, outcomes);

        Task<IReadOnlyList<EngineerActivityCounts>> IEngineerActivityQueries.GetAsync(
            DateTimeOffset fromUtc,
            DateTimeOffset toUtc,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref engineer);
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
