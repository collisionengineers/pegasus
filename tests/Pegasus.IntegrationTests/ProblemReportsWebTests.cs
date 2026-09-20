using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Support;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Report a problem (FRD-12, FRD-17, ADR-0055) through the real pages, Core
/// and the EF store over the seeded LocalDB, with the outward sink replaced:
/// a report is kept, raised, and listed; a failed raise is kept as Not sent
/// and an Administrator retries it.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class ProblemReportsWebTests
{
    private const string ListPage = "/Administration/ProblemReports";

    [Fact]
    public async Task AReportFromTheShellIsKeptRaisedAndListed()
    {
        var sink = new RecordingSink();
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = CreateClient(factory, sink, "Engineer");

        var home = await GetHtmlAsync(client, "/");
        Assert.Contains("data-dialog=\"problem-dialog\"", home, StringComparison.Ordinal);
        Assert.Contains("data-problem-form", home, StringComparison.Ordinal);

        using var response = await client.PostAsync("/ProblemReports?handler=Report", Form(home, new()
        {
            ["returnUrl"] = "/Cases",
            ["route"] = "/Cases?tab=review",
            ["traceId"] = "00-abc-01",
            ["caseReference"] = "QDOS26001",
            ["viewport"] = "1580x1000",
            ["editing"] = "true",
            ["errors"] = "[\"2026-09-20T10:00:00Z TypeError: x is undefined\"]",
            ["description"] = "The Save button did nothing."
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Cases", response.Headers.Location!.OriginalString);

        var sent = Assert.Single(sink.Sent);
        Assert.Equal("The Save button did nothing.", sent.Description);
        Assert.Equal("/Cases?tab=review", sent.Snapshot.Route);
        Assert.Equal("QDOS26001", sent.Snapshot.CaseReference);
        Assert.Equal("00-abc-01", sent.Snapshot.TraceId);
        Assert.True(sent.Snapshot.Client.Editing);
        Assert.Equal("1580x1000", sent.Snapshot.Client.Viewport);
        Assert.Single(sent.Snapshot.Client.RecentErrors);
        Assert.Equal(40, sent.Snapshot.SourceSha.Length);

        var after = await GetHtmlAsync(client, "/Cases");
        Assert.Contains("Reported as #7.", after, StringComparison.Ordinal);

        using var administrator = CreateClient(factory, sink);
        var list = await GetHtmlAsync(administrator, ListPage);
        Assert.Contains("The Save button did nothing.", list, StringComparison.Ordinal);
        Assert.Contains("href=\"https://github.com/example/pegasus/issues/7\"", list, StringComparison.Ordinal);
        Assert.Contains(">Sent<", list, StringComparison.Ordinal);
        Assert.DoesNotContain("Retry", list, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFailedRaiseIsKeptAsNotSentAndAnAdministratorRetriesIt()
    {
        var sink = new RecordingSink { Failure = new HttpRequestException("GitHub refused the issue with 401 Unauthorized.") };
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = CreateClient(factory, sink, "User");

        var home = await GetHtmlAsync(client, "/");
        using var response = await client.PostAsync("/ProblemReports?handler=Report", Form(home, new()
        {
            ["returnUrl"] = "/",
            ["description"] = "Nothing loads."
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var after = await GetHtmlAsync(client, "/");
        Assert.Contains("The report was kept but could not be sent.", after, StringComparison.Ordinal);

        using var user = CreateClient(factory, sink, "User");
        using var forbidden = await user.GetAsync(ListPage);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        using var administrator = CreateClient(factory, sink);
        var list = await GetHtmlAsync(administrator, ListPage);
        Assert.Contains(">Not sent<", list, StringComparison.Ordinal);
        Assert.Contains("GitHub refused the issue with 401 Unauthorized.", list, StringComparison.Ordinal);
        var id = Regex.Match(list, "data-problem-report=\"(?<id>[^\"]+)\"").Groups["id"].Value;

        sink.Failure = null;
        using var retried = await administrator.PostAsync($"{ListPage}?handler=Retry&id={id}", Form(list, new()
        {
            ["operationKey"] = Guid.NewGuid().ToString("N")
        }));
        Assert.Equal(HttpStatusCode.Redirect, retried.StatusCode);
        list = await GetHtmlAsync(administrator, ListPage);
        Assert.Contains(">Sent<", list, StringComparison.Ordinal);
        Assert.Contains("Reported as #7.", list, StringComparison.Ordinal);
        Assert.Single(sink.Sent);
    }

    [Fact]
    public async Task AnEmptyReportIsRefusedAndNothingIsRaised()
    {
        var sink = new RecordingSink();
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = CreateClient(factory, sink, "Engineer");
        var home = await GetHtmlAsync(client, "/");

        using var response = await client.PostAsync("/ProblemReports?handler=Report", Form(home, new()
        {
            ["returnUrl"] = "/",
            ["description"] = "   "
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Empty(sink.Sent);
        using var administrator = CreateClient(factory, sink);
        Assert.Contains("No problem reports", await GetHtmlAsync(administrator, ListPage), StringComparison.Ordinal);
    }

    private static HttpClient CreateClient(IntakeWebApplicationFactory factory, RecordingSink sink, string? role = null)
    {
        var client = factory
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<IProblemReportSink>();
                services.AddSingleton<IProblemReportSink>(sink);
            }))
            .CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost:7139")
            });
        if (role is not null)
        {
            client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        }

        return client;
    }

    private static async Task<string> GetHtmlAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static FormUrlEncodedContent Form(string page, Dictionary<string, string> fields)
    {
        var token = Regex.Match(page, "<input[^>]*name=\"__RequestVerificationToken\"[^>]*>", RegexOptions.IgnoreCase);
        Assert.True(token.Success, "The page carries no antiforgery token.");
        fields["__RequestVerificationToken"] = WebUtility.HtmlDecode(
            Regex.Match(token.Value, "value=\"(?<value>[^\"]*)\"", RegexOptions.IgnoreCase).Groups["value"].Value);
        return new FormUrlEncodedContent(fields);
    }

    private sealed class RecordingSink : IProblemReportSink
    {
        public List<ProblemReport> Sent { get; } = [];
        public Exception? Failure { get; set; }

        public Task<ProblemReportDelivery> SendAsync(ProblemReport report, CancellationToken cancellationToken)
        {
            if (Failure is not null)
            {
                return Task.FromException<ProblemReportDelivery>(Failure);
            }

            Sent.Add(report);
            return Task.FromResult(new ProblemReportDelivery(7, "https://github.com/example/pegasus/issues/7"));
        }
    }
}
