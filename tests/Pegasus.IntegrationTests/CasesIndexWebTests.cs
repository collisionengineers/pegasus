using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Actors;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.IntegrationTests;

[Collection(LocalDbFixtureDefinition.Name)]
public sealed class CasesIndexWebTests
{
    [Fact]
    public async Task SearchUsesAuthorizedCoreQueryAndPreservesEveryFilterInPagingUrl()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var search = new RecordingSearchCases();
        using var factory = Configure(baseFactory, search);
        using var client = CreateClient(factory);
        var engineerId = Guid.NewGuid();
        var path = "/Cases?case=QDOS3100042&registration=AB12CDE&claimant=Claimant&claimNumber=CLM42"
            + $"&principal=QDOS&state=Review&engineerId={engineerId:D}"
            + "&receivedDate=2031-05-01&instructionDate=2031-05-02"
            + "&fromDate=2031-04-01&toDate=2031-05-31&origin=Email&query=needle&page=2";

        using var response = await client.GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore == true);
        var query = Assert.IsType<SearchCasesQuery>(search.Query);
        Assert.Equal(ActorKind.Staff, query.Actor.Kind);
        Assert.NotEmpty(query.Actor.Roles);
        Assert.Equal(2, query.Page);
        Assert.Equal(25, query.PageSize);
        Assert.Equal("QDOS3100042", query.Filters.CaseReference);
        Assert.Equal("AB12CDE", query.Filters.Registration);
        Assert.Equal("Claimant", query.Filters.Claimant);
        Assert.Equal("CLM42", query.Filters.ClaimNumber);
        Assert.Equal("QDOS", query.Filters.Principal);
        Assert.Equal(CaseLifecycleState.Review, query.Filters.State);
        Assert.Equal(engineerId, query.Filters.EngineerId);
        Assert.Equal(new DateOnly(2031, 5, 1), query.Filters.ReceivedDate);
        Assert.Equal(new DateOnly(2031, 5, 2), query.Filters.InstructionDate);
        Assert.Equal(new DateOnly(2031, 4, 1), query.Filters.FromDate);
        Assert.Equal(new DateOnly(2031, 5, 31), query.Filters.ToDate);
        Assert.Equal("Email", query.Filters.Origin);
        Assert.Equal("needle", query.Filters.Query);
        Assert.Contains($"href=\"/Cases/{search.CaseId:D}\"", html, StringComparison.Ordinal);

        var next = Regex.Match(
            html,
            "<a[^>]*href=\"(?<href>[^\"]+)\"[^>]*>Next</a>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(next.Success, "The bounded search result must render a next-page URL.");
        var href = Uri.UnescapeDataString(WebUtility.HtmlDecode(next.Groups["href"].Value));
        foreach (var expected in new[]
                 {
                     "case=QDOS3100042", "registration=AB12CDE", "claimant=Claimant",
                     "claimNumber=CLM42", "principal=QDOS", "state=Review",
                     $"engineerId={engineerId:D}", "receivedDate=2031-05-01",
                     "instructionDate=2031-05-02", "fromDate=2031-04-01", "toDate=2031-05-31",
                     "origin=Email", "query=needle", "page=3"
                 })
        {
            Assert.Contains(expected, href, StringComparison.OrdinalIgnoreCase);
        }
        Assert.DoesNotContain("total", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EmptyAndUnavailableQueriesRenderDistinctNonLeakingStates()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var search = new RecordingSearchCases { ReturnEmpty = true };
        using var factory = Configure(baseFactory, search);
        using var client = CreateClient(factory);

        using var emptyResponse = await client.GetAsync("/Cases?principal=QDOS");
        var emptyHtml = await emptyResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, emptyResponse.StatusCode);
        Assert.Contains("No matching cases", emptyHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("unauthorised", emptyHtml, StringComparison.OrdinalIgnoreCase);

        search.ThrowUnavailable = true;
        using var failedResponse = await client.GetAsync("/Cases?principal=QDOS");
        var failedHtml = await failedResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failedResponse.StatusCode);
        Assert.Contains("Cases are unavailable", failedHtml, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(InvalidOperationException), failedHtml, StringComparison.Ordinal);
    }

    private static WebApplicationFactory<Program> Configure(
        IntakeWebApplicationFactory baseFactory,
        RecordingSearchCases search) => baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISearchCases>();
                services.AddSingleton<ISearchCases>(search);
            }));

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

    private sealed class RecordingSearchCases : ISearchCases
    {
        public Guid CaseId { get; } = Guid.NewGuid();

        public SearchCasesQuery? Query { get; private set; }

        public bool ReturnEmpty { get; set; }

        public bool ThrowUnavailable { get; set; }

        public Task<SearchCasesResult> ExecuteAsync(
            SearchCasesQuery query,
            CancellationToken cancellationToken)
        {
            Query = query;
            if (ThrowUnavailable)
            {
                throw new InvalidOperationException("sensitive store failure");
            }

            IReadOnlyList<CaseSearchItem> items = ReturnEmpty
                ? []
                :
                [
                    new(
                        CaseId,
                        "QDOS3100042",
                        null,
                        CaseType.Inspection,
                        "QDOS",
                        CaseLifecycleState.Review,
                        query.Filters.EngineerId,
                        "AB12CDE",
                        "Claimant",
                        "CLM42",
                        new DateTimeOffset(2031, 5, 1, 10, 0, 0, TimeSpan.Zero),
                        new DateOnly(2031, 5, 2),
                        "Email",
                        new DateTimeOffset(2031, 5, 1, 10, 0, 0, TimeSpan.Zero))
                ];
            return Task.FromResult(new SearchCasesResult(items, query.Page, query.PageSize, true, true));
        }
    }
}
