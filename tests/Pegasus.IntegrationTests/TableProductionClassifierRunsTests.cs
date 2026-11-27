using System.Net;
using System.Text;
using Azure.Core;
using Azure.Identity;
using Pegasus.Core.Operations;
using Pegasus.Infrastructure.Operations;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The test instance's read of the live classifier's runs from the live
/// storage account's <c>runs</c> table (R1319), against a fake Table service:
/// one query over the period's days that follows the service's continuation,
/// the entity the classifier writes read back as a run, one run's messages
/// by a point read, and a refused or failed read named as such.
/// </summary>
public sealed class TableProductionClassifierRunsTests
{
    private static readonly Uri Endpoint = new("https://pegclassexample.table.core.windows.net/");
    private static readonly Guid First = Guid.Parse("6f0b8f0e-0f4a-4f41-9b8e-1a2b3c4d5e6f");
    private static readonly Guid Second = Guid.Parse("7a1c9e1f-1a5b-4a52-8c9f-2b3c4d5e6f70");

    [Fact]
    public async Task ThePeriodIsReadInOneQueryOverItsDaysFollowingTheContinuation()
    {
        var service = new TableService(
            Page("""
                {"value":[{"PartitionKey":"2031-05-05","RowKey":"6f0b8f0e-0f4a-4f41-9b8e-1a2b3c4d5e6f","startedAt":"2031-05-05T23:30:00.000Z","finishedAt":"2031-05-05T23:32:00.000Z","dryRun":false,"outcome":"success","eligible":3,"awaitingDecision":3,"candidatesHidden":false,"hiddenRuns":0,"costUsd":0.42,"durationMs":120000,"counts":"{\"classified\":2,\"linked\":1,\"archived\":4}"}]}
                """, ("x-ms-continuation-NextPartitionKey", "1!20!MjAzMS0wNS0wNg--"), ("x-ms-continuation-NextRowKey", "1!48!abc")),
            Page("""
                {"value":[{"PartitionKey":"2031-05-06","RowKey":"7a1c9e1f-1a5b-4a52-8c9f-2b3c4d5e6f70","startedAt":"2031-05-06T08:00:00.000Z","dryRun":true,"maxBudgetUsd":2},{"PartitionKey":"2031-05-06","RowKey":"not-a-run","startedAt":"2031-05-06T08:00:00.000Z"},{"PartitionKey":"2031-05-06","RowKey":"8b2d0f20-2b6c-4b63-9d0a-3c4d5e6f7081","startedAt":"2031-05-06T23:59:00.000Z","finishedAt":"2031-05-07T00:01:00.000Z","outcome":"nothing eligible","costUsd":0}]}
                """));
        var reader = Reader(service);

        var runs = await reader.ListAsync(
            new DateTimeOffset(2031, 5, 5, 23, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2031, 5, 6, 12, 0, 0, TimeSpan.Zero),
            CancellationToken.None);

        Assert.Equal(2, service.Requests.Count);
        var query = Uri.UnescapeDataString(service.Requests[0].RequestUri!.Query);
        Assert.Contains("$filter=PartitionKey ge '2031-05-05' and PartitionKey le '2031-05-06'", query, StringComparison.Ordinal);
        Assert.Contains("$select=PartitionKey,RowKey,startedAt,", query, StringComparison.Ordinal);
        Assert.DoesNotContain("items", query, StringComparison.Ordinal);
        Assert.Contains("NextPartitionKey=1!20!MjAzMS0wNS0wNg--", Uri.UnescapeDataString(service.Requests[1].RequestUri!.Query), StringComparison.Ordinal);
        Assert.All(service.Requests, request =>
        {
            Assert.Equal("Bearer live-runs-token", request.Headers.Authorization?.ToString());
            Assert.Equal("2019-02-02", Assert.Single(request.Headers.GetValues("x-ms-version")));
            Assert.Contains("odata=nometadata", request.Headers.Accept.ToString(), StringComparison.Ordinal);
        });

        // The third row started after the period, and the second is not a run.
        Assert.Equal(new[] { First, Second }, runs.Select(run => run.Id));
        var finished = runs[0];
        Assert.Equal(ClassifierRunEnvironment.Live, finished.Environment);
        Assert.Equal(ClassifierRunOutcome.Completed, finished.ClosedOutcome);
        Assert.Equal(0.42m, finished.CostUsd);
        Assert.Equal(120_000, finished.DurationMs);
        Assert.Equal(2, finished.Actions[ClassifierRunAction.Classified]);
        Assert.Equal(1, finished.Actions[ClassifierRunAction.Linked]);
        Assert.Equal(2, finished.Actions.Count);
        var open = runs[1];
        Assert.Null(open.FinishedAtUtc);
        Assert.Null(open.ClosedOutcome);
        Assert.True(open.DryRun);
        Assert.Null(open.CostUsd);
    }

    [Fact]
    public async Task ARunsMessagesAreReadByItsDateAndIdWithWhatTheInstanceCannotReadOfProductionsMail()
    {
        var caseId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var items = $$"""[{"messageId":"{{messageId}}","action":"linked","caseId":"{{caseId}}","caseReference":"QDOS26002","reason":"The registration matches.","at":"2031-05-06T08:01:00.000Z","subject":"Re: QDOS26002","sender":"desk@principal.test"},{"messageId":"{{Guid.NewGuid()}}","action":"archived"}]""";
        var entity = $$"""
            {"PartitionKey":"2031-05-06","RowKey":"{{First}}","startedAt":"2031-05-06T08:00:00.000Z","finishedAt":"2031-05-06T08:02:00.000Z","dryRun":false,"model":"claude-sonnet-5-5","batch":20,"scanPages":10,"maxBudgetUsd":2.0,"agentVersion":"1.0.0","outcome":"error_max_budget_usd","eligible":2,"verdicts":1,"failed":0,"deferred":0,"turns":"12","costUsd":"2.01","durationMs":120000,"summary":"One link.","items":{{System.Text.Json.JsonSerializer.Serialize(items)}},"itemsTruncated":true}
            """;
        var service = new TableService(Page(entity), new HttpResponseMessage(HttpStatusCode.NotFound));
        var reader = Reader(service);

        var detail = Assert.IsType<ClassifierRunDetail>(await reader.GetAsync(First, new DateOnly(2031, 5, 6), CancellationToken.None));

        Assert.Equal(
            $"https://pegclassexample.table.core.windows.net/runs(PartitionKey='2031-05-06',RowKey='{First}')",
            Uri.UnescapeDataString(service.Requests[0].RequestUri!.AbsoluteUri));
        Assert.Equal(ClassifierRunOutcome.CutShort, detail.Summary.ClosedOutcome);
        Assert.Equal("error_max_budget_usd", detail.Summary.RawOutcome);
        Assert.Equal(2.01m, detail.Summary.CostUsd);
        Assert.Equal(12, detail.Turns);
        Assert.Equal(new ClassifierRunFacts(false, "claude-sonnet-5-5", 20, 10, 2m, "1.0.0"), detail.Facts);
        Assert.Equal("One link.", detail.ModelSummary);
        Assert.Null(detail.RecordedBy);
        // The unknown act is left out, and the classifier said it cut the list.
        Assert.True(detail.ItemsCut);
        var item = Assert.Single(detail.Items);
        Assert.Equal(new ClassifierRunItem(
            messageId, ClassifierRunAction.Linked, null, caseId, "QDOS26002", "The registration matches.", null,
            new DateTimeOffset(2031, 5, 6, 8, 1, 0, TimeSpan.Zero)), item.Item);
        Assert.Equal("Re: QDOS26002", item.Subject);
        Assert.Equal("desk@principal.test", item.Sender);
        Assert.False(item.MessageIsHere);
        Assert.Equal(1, detail.Summary.Actions[ClassifierRunAction.Linked]);

        Assert.Null(await reader.GetAsync(Second, new DateOnly(2031, 5, 6), CancellationToken.None));
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, true)]
    [InlineData(HttpStatusCode.Unauthorized, true)]
    [InlineData(HttpStatusCode.InternalServerError, false)]
    public async Task ARefusedOrFailedReadIsNamedAsSuch(HttpStatusCode status, bool refused)
    {
        var reader = Reader(new TableService(new HttpResponseMessage(status)));

        var exception = await Assert.ThrowsAsync<ProductionClassifierRunsUnavailableException>(() => reader.ListAsync(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow, CancellationToken.None));

        Assert.Equal(refused, exception.Refused);
    }

    [Fact]
    public async Task ASignInTheRegistrationIsRefusedIsARefusedRead()
    {
        var service = new TableService();
        var reader = new TableProductionClassifierRuns(new HttpClient(service), new RefusedCredential(), Endpoint);

        var exception = await Assert.ThrowsAsync<ProductionClassifierRunsUnavailableException>(() => reader.ListAsync(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow, CancellationToken.None));

        Assert.True(exception.Refused);
        Assert.Empty(service.Requests);
    }

    private static TableProductionClassifierRuns Reader(TableService service) =>
        new(new HttpClient(service), new FixedCredential(), Endpoint);

    private static HttpResponseMessage Page(string json, params (string Name, string Value)[] headers)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        foreach (var (name, value) in headers)
        {
            response.Headers.Add(name, value);
        }

        return response;
    }

    /// <summary>A Table service that answers each request with the next response it holds.</summary>
    private sealed class TableService(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> answers = new(responses);

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(answers.Dequeue());
        }
    }

    private sealed class FixedCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            new("live-runs-token", DateTimeOffset.UtcNow.AddHours(1));

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            Assert.Equal("https://storage.azure.com/.default", Assert.Single(requestContext.Scopes));
            return ValueTask.FromResult(GetToken(requestContext, cancellationToken));
        }
    }

    private sealed class RefusedCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            throw new AuthenticationFailedException("The client secret is not valid.");

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            throw new AuthenticationFailedException("The client secret is not valid.");
    }
}
