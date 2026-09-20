using System.Net;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Pegasus.IntegrationTests;

/// <summary>Administration → Reports through the real page over the seeded LocalDB: the workbook download and the MI-01 sort.</summary>
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
        Assert.Equal(["Engineer activity", "Reports by Principal", "Turnaround", "By month"], names);
    }

    [Fact]
    public async Task ThePageOffersTheWorkbookTheSortableColumnsAndTheMonthTable()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = CreateClient(factory);

        using var response = await client.GetAsync($"{Page}?sort=queries&dir=desc");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("handler=Workbook", html, StringComparison.Ordinal);
        Assert.Contains("data-sort-toggle", html, StringComparison.Ordinal);
        Assert.Contains("aria-sort=\"descending\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"mi02-months-title\"", html, StringComparison.Ordinal);
        Assert.Contains("Disputes", html, StringComparison.Ordinal);
        Assert.Contains("Audit reports sent", html, StringComparison.Ordinal);
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

    private static HttpClient CreateClient(IntakeWebApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });
}
