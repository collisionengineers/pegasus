using System.Net;
using System.Text.Json;
using Pegasus.Core.Support;
using Pegasus.Infrastructure.Support;

namespace Pegasus.IntegrationTests;

/// <summary>The outward sink's request shape and its refusals, against a fake handler; nothing leaves the process.</summary>
public sealed class GitHubIssueProblemReportSinkTests
{
    private static readonly ProblemReport Report = new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        "The Save button did nothing.",
        new ProblemReportSnapshot(
            "0.1.0-alpha.1", new string('a', 40), new DateTimeOffset(2026, 9, 20, 15, 0, 0, TimeSpan.Zero),
            "/Cases/x?section=estimate", "GET", "00-trace", "integration-user", "Engineer", "QDOS26001", null, null,
            [], new ProblemReportClientFacts("1580x1000", "agent", false, [])),
        new DateTimeOffset(2026, 9, 20, 15, 0, 0, TimeSpan.Zero),
        ProblemReportStatus.NotSent, null, null, null, null, null, null);

    [Fact]
    public async Task PostsOneActionableIssueWithTheTokenAndLabels()
    {
        var handler = new FakeHandler(HttpStatusCode.Created, "{\"number\":12,\"html_url\":\"https://github.com/collisionengineers/pegasus/issues/12\"}");
        var sink = new GitHubIssueProblemReportSink(
            GitHubProblemReportOptions.FromConfiguration("token-value", "collisionengineers/pegasus", "problem-report, triage")!,
            new HttpClient(handler));

        var delivery = await sink.SendAsync(Report, default);

        Assert.Equal(12, delivery.IssueNumber);
        Assert.Equal("https://github.com/collisionengineers/pegasus/issues/12", delivery.IssueUrl);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("GET", handler.Requests[0].Method);
        Assert.Equal("https://api.github.com/repos/collisionengineers/pegasus", handler.Requests[0].Uri);
        var request = handler.Requests[1];
        Assert.Equal("POST", request.Method);
        Assert.Equal("https://api.github.com/repos/collisionengineers/pegasus/issues", request.Uri);
        Assert.Equal("Bearer token-value", request.Authorization);
        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal($"Pegasus: The Save button did nothing. [{Report.Id:D}]", body.RootElement.GetProperty("title").GetString());
        var issueBody = body.RootElement.GetProperty("body").GetString()!;
        Assert.Contains(Report.Id.ToString("D"), issueBody, StringComparison.Ordinal);
        Assert.Contains("QDOS26001", issueBody, StringComparison.Ordinal);
        Assert.Contains(Report.Description, issueBody, StringComparison.Ordinal);
        Assert.Contains("/Cases/", issueBody, StringComparison.Ordinal);
        Assert.Contains("00-trace", issueBody, StringComparison.Ordinal);
        Assert.Equal(["problem-report", "triage"], body.RootElement.GetProperty("labels").EnumerateArray().Select(label => label.GetString()));
    }

    [Fact]
    public async Task ARefusalNamesTheStatusAndNothingElse()
    {
        var handler = new FakeHandler(HttpStatusCode.Unauthorized, "{\"message\":\"Bad credentials\"}");
        var sink = new GitHubIssueProblemReportSink(
            GitHubProblemReportOptions.FromConfiguration("token-value", "collisionengineers/pegasus", null)!,
            new HttpClient(handler));

        var refused = await Assert.ThrowsAsync<HttpRequestException>(() => sink.SendAsync(Report, default));

        Assert.Contains("401", refused.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("token-value", refused.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "{}")]
    [InlineData(HttpStatusCode.Created, "{}")]
    public async Task UncertainPostOutcomeIsHeldForReconciliation(HttpStatusCode status, string body)
    {
        var handler = new FakeHandler(status, body);
        var sink = new GitHubIssueProblemReportSink(
            GitHubProblemReportOptions.FromConfiguration("token-value", "collisionengineers/pegasus", null)!,
            new HttpClient(handler));

        await Assert.ThrowsAsync<ProblemReportDeliveryUnknownException>(() => sink.SendAsync(Report, default));
        Assert.Equal("POST", handler.Requests[1].Method);
    }

    [Fact]
    public async Task LostPostResponseIsHeldForReconciliation()
    {
        var handler = new FakeHandler(HttpStatusCode.Created, "{}") { ThrowPost = true };
        var sink = new GitHubIssueProblemReportSink(
            GitHubProblemReportOptions.FromConfiguration("token-value", "collisionengineers/pegasus", null)!,
            new HttpClient(handler));

        await Assert.ThrowsAsync<ProblemReportDeliveryUnknownException>(() => sink.SendAsync(Report, default));
        Assert.Equal("POST", handler.Requests[1].Method);
    }

    [Theory]
    [InlineData("{\"private\":true,\"full_name\":\"example/other\"}")]
    [InlineData("{}")]
    public async Task AmbiguousOrDifferentRepositoryIsNeverPosted(string repositoryBody)
    {
        var handler = new FakeHandler(HttpStatusCode.Created, "{}", repositoryBody: repositoryBody);
        var sink = new GitHubIssueProblemReportSink(
            GitHubProblemReportOptions.FromConfiguration("token-value", "collisionengineers/pegasus", null)!,
            new HttpClient(handler));

        await Assert.ThrowsAsync<InvalidOperationException>(() => sink.SendAsync(Report, default));
        Assert.Equal("GET", Assert.Single(handler.Requests).Method);
    }

    [Fact]
    public async Task PublicRepositoryCanReceiveOnlyTheOpaqueReportReference()
    {
        var handler = new FakeHandler(HttpStatusCode.Created,
            "{\"number\":12,\"html_url\":\"https://github.com/collisionengineers/pegasus/issues/12\"}",
            repositoryBody: "{\"private\":false,\"full_name\":\"collisionengineers/pegasus\"}");
        var sink = new GitHubIssueProblemReportSink(
            GitHubProblemReportOptions.FromConfiguration("token-value", "collisionengineers/pegasus", null)!,
            new HttpClient(handler));

        await sink.SendAsync(Report, default);

        Assert.Equal("POST", handler.Requests[1].Method);
        Assert.DoesNotContain("QDOS26001", handler.Requests[1].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnreadableRepositoryVisibilityIsNeverPosted()
    {
        var handler = new FakeHandler(HttpStatusCode.Created, "{}", repositoryStatus: HttpStatusCode.Forbidden);
        var sink = new GitHubIssueProblemReportSink(
            GitHubProblemReportOptions.FromConfiguration("token-value", "collisionengineers/pegasus", null)!,
            new HttpClient(handler));

        await Assert.ThrowsAsync<HttpRequestException>(() => sink.SendAsync(Report, default));
        Assert.Equal("GET", Assert.Single(handler.Requests).Method);
    }

    [Fact]
    public void ConfigurationIsBothSettingsOrNeither()
    {
        Assert.Null(GitHubProblemReportOptions.FromConfiguration(null, null, null));
        Assert.Throws<InvalidOperationException>(() => GitHubProblemReportOptions.FromConfiguration("token", null, null));
        Assert.Throws<InvalidOperationException>(() => GitHubProblemReportOptions.FromConfiguration("token", "not-a-repository", null));
        var options = GitHubProblemReportOptions.FromConfiguration(" token ", "collisionengineers/pegasus", "")!;
        Assert.Equal("token", options.Token);
        Assert.Empty(options.Labels);
    }

    [Fact]
    public async Task TheUnconfiguredSinkKeepsEveryReportWithTheReason()
    {
        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new UnconfiguredProblemReportSink().SendAsync(Report, default));
        Assert.Equal(UnconfiguredProblemReportSink.Reason, refused.Message);
    }

    private sealed class FakeHandler(
        HttpStatusCode status,
        string body,
        HttpStatusCode repositoryStatus = HttpStatusCode.OK,
        string repositoryBody = "{\"private\":true,\"full_name\":\"collisionengineers/pegasus\"}") : HttpMessageHandler
    {
        public List<(string Method, string Uri, string? Authorization, string Body)> Requests { get; } = [];
        public bool ThrowPost { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((
                request.Method.Method,
                request.RequestUri!.ToString(),
                request.Headers.Authorization?.ToString(),
                request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken)));
            var isRepositoryRead = request.Method == HttpMethod.Get;
            if (!isRepositoryRead && ThrowPost)
                throw new HttpRequestException("Response lost after POST.");
            return new HttpResponseMessage(isRepositoryRead ? repositoryStatus : status)
            {
                Content = new StringContent(isRepositoryRead ? repositoryBody : body, System.Text.Encoding.UTF8, "application/json")
            };
        }
    }
}
