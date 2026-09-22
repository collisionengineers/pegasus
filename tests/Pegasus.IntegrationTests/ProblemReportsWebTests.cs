using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Identity;
using Pegasus.Core.ReleaseNotes;
using Pegasus.Core.Support;
using Pegasus.Web.Pages;
using Pegasus.Web.Presentation;

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
        Assert.Contains("name=\"method\" value=\"GET\"", home, StringComparison.Ordinal);

        using var response = await client.PostAsync("/ProblemReports?handler=Report", Form(home, new()
        {
            ["returnUrl"] = "/Cases",
            ["route"] = "/Cases?section=estimate&claimant=claimant-secret&mail=mail-secret",
            ["method"] = "GET",
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
        Assert.Equal("/Cases?section=estimate", sent.Snapshot.Route);
        var body = ProblemReportPolicy.Body(sent);
        Assert.Contains("route      GET /Cases?section=estimate", body, StringComparison.Ordinal);
        Assert.DoesNotContain("claimant-secret", body, StringComparison.Ordinal);
        Assert.DoesNotContain("mail-secret", body, StringComparison.Ordinal);
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
    public void ErrorPostInitializationRetainsOriginatingFaultContext()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var http = new DefaultHttpContext
        {
            TraceIdentifier = "00-error-trace"
        };
        http.Request.Method = "POST";
        var feature = new ExceptionHandlerFeature
        {
            Path = "/Cases",
            Error = new InvalidOperationException("The Save request failed.")
        };
        http.Features.Set<IExceptionHandlerFeature>(feature);
        http.Features.Set<IExceptionHandlerPathFeature>(feature);

        var model = new ErrorModel(cache)
        {
            PageContext = new PageContext { HttpContext = http }
        };
        model.OnPost();

        var request = ProblemReportRequests.Build(
            http,
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]),
            new ApplicationBuild("0.1.0-alpha.1", new string('a', 40)),
            cache,
            new ProblemReportRequests.PostedFacts(
                "The Save request failed.",
                model.ReturnPath,
                http.Request.Method,
                model.RequestId,
                null,
                null,
                false,
                null));

        Assert.Equal("POST", request.Method);
        Assert.Equal("00-error-trace", request.TraceId);
        Assert.Equal("/Cases", request.Route);
        Assert.Equal(typeof(InvalidOperationException).FullName, request.ExceptionType);
        Assert.Equal("The Save request failed.", request.ExceptionMessage);
    }

    [Fact]
    public async Task TheAdministrationListKeepsTheOldestFailedReportBeyondTwoHundredRows()
    {
        var sink = new RecordingSink { Failure = new HttpRequestException("first report failed") };
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = CreateClient(factory, sink, "Engineer");
        var home = await GetHtmlAsync(client, "/");

        using var first = await client.PostAsync("/ProblemReports?handler=Report", Form(home, new()
        {
            ["returnUrl"] = "/",
            ["description"] = "Oldest retained report"
        }));
        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);

        sink.Failure = null;
        for (var index = 1; index <= 200; index++)
        {
            using var response = await client.PostAsync("/ProblemReports?handler=Report", Form(home, new()
            {
                ["returnUrl"] = "/",
                ["description"] = $"Retained report {index}"
            }));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        using var administrator = CreateClient(factory, sink);
        var list = await GetHtmlAsync(administrator, ListPage);
        var descriptionIndex = list.IndexOf("Oldest retained report", StringComparison.Ordinal);
        Assert.True(descriptionIndex >= 0);
        var reportStart = list.LastIndexOf("<tr", descriptionIndex, StringComparison.Ordinal);
        var reportEnd = list.IndexOf("</tr>", reportStart, StringComparison.Ordinal);
        var row = list[reportStart..reportEnd];
        Assert.Contains("Oldest retained report", row, StringComparison.Ordinal);
        Assert.Contains(">Not sent<", row, StringComparison.Ordinal);
        Assert.Contains(">Retry<", row, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SameOriginFallbackRouteKeepsReturnUrlButSanitisesTheCapturedSnapshot()
    {
        var sink = new RecordingSink();
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = CreateClient(factory, sink, "Engineer");
        client.DefaultRequestHeaders.Referrer = new Uri("https://localhost:7139/Search?section=estimate&claimant=claimant-secret&mail=mail-secret");

        var home = await GetHtmlAsync(client, "/");
        using var response = await client.PostAsync("/ProblemReports?handler=Report", Form(home, new()
        {
            ["description"] = "The filtered page failed."
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Search?section=estimate&claimant=claimant-secret&mail=mail-secret", response.Headers.Location!.OriginalString);
        var sent = Assert.Single(sink.Sent);
        Assert.Equal("/Search?section=estimate", sent.Snapshot.Route);
        var body = ProblemReportPolicy.Body(sent);
        Assert.DoesNotContain("claimant-secret", body, StringComparison.Ordinal);
        Assert.DoesNotContain("mail-secret", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConcurrentRetriesClaimOneReportBeforeCallingTheSink()
    {
        var sink = new RecordingSink { Failure = new HttpRequestException("temporary failure") };
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = CreateClient(factory, sink, "User");

        var home = await GetHtmlAsync(client, "/");
        using var created = await client.PostAsync("/ProblemReports?handler=Report", Form(home, new()
        {
            ["returnUrl"] = "/",
            ["description"] = "Nothing loads."
        }));
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);

        using var administrator = CreateClient(factory, sink);
        var list = await GetHtmlAsync(administrator, ListPage);
        var id = Regex.Match(list, "data-problem-report=\"(?<id>[^\"]+)\"").Groups["id"].Value;
        Assert.NotEmpty(id);

        sink.Failure = null;
        sink.BlockSuccessfulSends();
        var first = RetryAsync(administrator, list, id, Guid.NewGuid().ToString("N"));
        await sink.Entered.Task;

        using var second = await RetryAsync(administrator, list, id, Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.Redirect, second.StatusCode);
        Assert.Equal(1, sink.SuccessfulCalls);

        sink.Release.TrySetResult(true);
        using var firstResponse = await first;
        Assert.Equal(HttpStatusCode.Redirect, firstResponse.StatusCode);
        Assert.Single(sink.Sent);
        list = await GetHtmlAsync(administrator, ListPage);
        Assert.Contains(">Sent<", list, StringComparison.Ordinal);
        Assert.Contains("Reported as #7.", list, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExpiredDispatchClaimCanBeReclaimedThroughTheAdministrationRetry()
    {
        var sink = new RecordingSink { Failure = new HttpRequestException("temporary failure") };
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = CreateClient(factory, sink, "User");

        var home = await GetHtmlAsync(client, "/");
        using var created = await client.PostAsync("/ProblemReports?handler=Report", Form(home, new()
        {
            ["returnUrl"] = "/",
            ["description"] = "Nothing loads."
        }));
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);

        using var administrator = CreateClient(factory, sink);
        var list = await GetHtmlAsync(administrator, ListPage);
        var id = Guid.Parse(Regex.Match(list, "data-problem-report=\"(?<id>[^\"]+)\"").Groups["id"].Value);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IProblemReportStore>();
            var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow();
            Assert.NotNull(await store.TryClaimAsync(
                id,
                "abandoned",
                now - ProblemReportPolicy.DispatchClaimLease,
                ProblemReportPolicy.DispatchClaimLease,
                default));
        }

        sink.Failure = null;
        using var retried = await administrator.PostAsync($"{ListPage}?handler=Retry&id={id}", Form(list, new()
        {
            ["operationKey"] = Guid.NewGuid().ToString("N")
        }));

        Assert.Equal(HttpStatusCode.Redirect, retried.StatusCode);
        Assert.Single(sink.Sent);
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

    private static Task<HttpResponseMessage> RetryAsync(HttpClient client, string page, string id, string key) =>
        client.PostAsync($"{ListPage}?handler=Retry&id={id}", Form(page, new()
        {
            ["operationKey"] = key
        }));

    private sealed class RecordingSink : IProblemReportSink
    {
        public List<ProblemReport> Sent { get; } = [];
        public Exception? Failure { get; set; }
        public TaskCompletionSource<bool> Entered { get; private set; } = NewSignal();
        public TaskCompletionSource<bool> Release { get; private set; } = NewSignal();
        public bool Block { get; private set; }
        public int SuccessfulCalls;

        public void BlockSuccessfulSends()
        {
            Block = true;
            Entered = NewSignal();
            Release = NewSignal();
        }

        public async Task<ProblemReportDelivery> SendAsync(ProblemReport report, CancellationToken cancellationToken)
        {
            if (Failure is not null)
            {
                throw Failure;
            }

            Interlocked.Increment(ref SuccessfulCalls);
            if (Block)
            {
                Entered.TrySetResult(true);
                await Release.Task.WaitAsync(cancellationToken);
            }

            Sent.Add(report);
            return new ProblemReportDelivery(7, "https://github.com/example/pegasus/issues/7");
        }

        private static TaskCompletionSource<bool> NewSignal() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
