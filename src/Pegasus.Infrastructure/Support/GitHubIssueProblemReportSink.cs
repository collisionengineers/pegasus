using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pegasus.Core.Support;

namespace Pegasus.Infrastructure.Support;

/// <summary>
/// Where public-safe problem report references go (ADR-0055): one repository's issues. The token is
/// a fine-grained personal access token with Issues read and write on that
/// repository only, read from configuration and never logged.
/// </summary>
public sealed record GitHubProblemReportOptions(string Token, string Repository, IReadOnlyList<string> Labels)
{
    public const string ApprovedRepository = "collisionengineers/pegasus";
    public const string TokenKey = "GitHub:ProblemReports:Token";
    public const string RepositoryKey = "GitHub:ProblemReports:Repository";
    public const string LabelsKey = "GitHub:ProblemReports:Labels";

    /// <summary>Both settings, or null when the sink is not configured; a half configuration is refused.</summary>
    public static GitHubProblemReportOptions? FromConfiguration(string? token, string? repository, string? labels)
    {
        var hasToken = !string.IsNullOrWhiteSpace(token);
        var hasRepository = !string.IsNullOrWhiteSpace(repository);
        if (!hasToken && !hasRepository)
        {
            return null;
        }

        if (!hasToken || !hasRepository)
        {
            throw new InvalidOperationException(
                $"{TokenKey} and {RepositoryKey} are configured together or not at all.");
        }

        var parts = repository!.Trim().Split('/');
        if (parts.Length != 2 || parts.Any(part => part.Length == 0 || part.Any(character => !(char.IsLetterOrDigit(character) || character is '-' or '_' or '.'))))
        {
            throw new InvalidOperationException($"{RepositoryKey} is owner/name.");
        }
        if (!string.Equals(repository.Trim(), ApprovedRepository, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{RepositoryKey} must name the approved Pegasus repository.");
        }

        var labelList = (labels ?? string.Empty)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return new GitHubProblemReportOptions(token!.Trim(), repository.Trim(), labelList);
    }
}

public sealed class GitHubIssueProblemReportSink(GitHubProblemReportOptions options, HttpClient client) : IProblemReportSink
{
    public const string HttpClientName = nameof(GitHubIssueProblemReportSink);

    private readonly GitHubProblemReportOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly HttpClient _client = client ?? throw new ArgumentNullException(nameof(client));

    public async Task<ProblemReportDelivery> SendAsync(ProblemReport report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        using var repositoryRequest = CreateRequest(HttpMethod.Get, $"https://api.github.com/repos/{_options.Repository}", report);
        using var repositoryResponse = await _client.SendAsync(repositoryRequest, cancellationToken);
        if (!repositoryResponse.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"GitHub repository identity could not be verified: {(int)repositoryResponse.StatusCode} {repositoryResponse.ReasonPhrase}.");
        }

        var repository = await repositoryResponse.Content.ReadFromJsonAsync<RepositoryResponse>(cancellationToken);
        if (repository is null
            || !string.Equals(repository.FullName, _options.Repository, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The configured GitHub repository identity could not be verified.");
        }

        using var request = CreateRequest(HttpMethod.Post, $"https://api.github.com/repos/{_options.Repository}/issues", report);
        request.Content = JsonContent.Create(new IssueRequest(
            ProblemReportPolicy.Title(report),
            ProblemReportPolicy.Body(report),
            _options.Labels));

        HttpResponseMessage posted;
        try
        {
            posted = await _client.SendAsync(request, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException
            || exception is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new ProblemReportDeliveryUnknownException(
                "The GitHub issue POST outcome is unknown. Reconcile by report ID.", exception);
        }

        using var response = posted;
        if (!response.IsSuccessStatusCode)
        {
            if ((int)response.StatusCode >= 500 || response.StatusCode is System.Net.HttpStatusCode.RequestTimeout
                or System.Net.HttpStatusCode.TooManyRequests)
            {
                throw new ProblemReportDeliveryUnknownException(
                    "GitHub returned an uncertain issue outcome. Reconcile by report ID.");
            }

            throw new HttpRequestException(
                $"GitHub refused the issue with {(int)response.StatusCode} {response.ReasonPhrase}.");
        }

        IssueResponse? issue;
        try
        {
            issue = await response.Content.ReadFromJsonAsync<IssueResponse>(cancellationToken);
        }
        catch (Exception exception) when (exception is JsonException or HttpRequestException
            || exception is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new ProblemReportDeliveryUnknownException(
                "GitHub accepted the issue but its response could not be read. Reconcile by report ID.", exception);
        }
        if (issue is null || issue.Number <= 0 || string.IsNullOrWhiteSpace(issue.HtmlUrl))
        {
            throw new ProblemReportDeliveryUnknownException(
                "GitHub accepted the issue without a usable issue identity. Reconcile by report ID.");
        }

        return new ProblemReportDelivery(issue.Number, issue.HtmlUrl);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string uri, ProblemReport report)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.Token);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Pegasus", report.Snapshot.Version));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        return request;
    }

    private sealed record IssueRequest(
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("body")] string Body,
        [property: JsonPropertyName("labels")] IReadOnlyList<string> Labels);

    private sealed record IssueResponse(
        [property: JsonPropertyName("number")] int Number,
        [property: JsonPropertyName("html_url")] string? HtmlUrl);

    private sealed record RepositoryResponse(
        [property: JsonPropertyName("full_name")] string? FullName);
}

/// <summary>The sink when no repository is configured: every report stays Not sent, with the reason.</summary>
public sealed class UnconfiguredProblemReportSink : IProblemReportSink
{
    public const string Reason = "Problem reports are not connected to a repository.";

    public Task<ProblemReportDelivery> SendAsync(ProblemReport report, CancellationToken cancellationToken) =>
        Task.FromException<ProblemReportDelivery>(new InvalidOperationException(Reason));
}
