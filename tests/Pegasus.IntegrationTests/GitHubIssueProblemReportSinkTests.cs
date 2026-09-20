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
        ProblemReportStatus.NotSent, null, null, null, null);

    [Fact]
    public async Task PostsOneIssueWithTheTokenTheLabelsAndThePersonsWordsFirst()
    {
        var handler = new FakeHandler(HttpStatusCode.Created, "{\"number\":12,\"html_url\":\"https://github.com/example/pegasus/issues/12\"}");
        var sink = new GitHubIssueProblemReportSink(
            GitHubProblemReportOptions.FromConfiguration("token-value", "example/pegasus", "problem-report, triage")!,
            new HttpClient(handler));

        var delivery = await sink.SendAsync(Report, default);

        Assert.Equal(12, delivery.IssueNumber);
        Assert.Equal("https://github.com/example/pegasus/issues/12", delivery.IssueUrl);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://api.github.com/repos/example/pegasus/issues", request.Uri);
        Assert.Equal("Bearer token-value", request.Authorization);
        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal("Problem report: /Cases/x?section=estimate (QDOS26001)", body.RootElement.GetProperty("title").GetString());
        Assert.StartsWith("The Save button did nothing.", body.RootElement.GetProperty("body").GetString(), StringComparison.Ordinal);
        Assert.Equal(["problem-report", "triage"], body.RootElement.GetProperty("labels").EnumerateArray().Select(label => label.GetString()));
    }

    [Fact]
    public async Task ARefusalNamesTheStatusAndNothingElse()
    {
        var handler = new FakeHandler(HttpStatusCode.Unauthorized, "{\"message\":\"Bad credentials\"}");
        var sink = new GitHubIssueProblemReportSink(
            GitHubProblemReportOptions.FromConfiguration("token-value", "example/pegasus", null)!,
            new HttpClient(handler));

        var refused = await Assert.ThrowsAsync<HttpRequestException>(() => sink.SendAsync(Report, default));

        Assert.Contains("401", refused.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("token-value", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfigurationIsBothSettingsOrNeither()
    {
        Assert.Null(GitHubProblemReportOptions.FromConfiguration(null, null, null));
        Assert.Throws<InvalidOperationException>(() => GitHubProblemReportOptions.FromConfiguration("token", null, null));
        Assert.Throws<InvalidOperationException>(() => GitHubProblemReportOptions.FromConfiguration("token", "not-a-repository", null));
        var options = GitHubProblemReportOptions.FromConfiguration(" token ", "example/pegasus", "")!;
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

    private sealed class FakeHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public List<(string Uri, string? Authorization, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((
                request.RequestUri!.ToString(),
                request.Headers.Authorization?.ToString(),
                request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken)));
            return new HttpResponseMessage(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
        }
    }
}
