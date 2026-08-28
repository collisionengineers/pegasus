using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;

namespace Pegasus.IntegrationTests;

/// <summary>
/// ENG-025: the ported assessment page carries no explanatory copy
/// (required state is visual, hint sentences stay out), the D11 gate
/// renders the "Assessment unavailable" surface naming the export
/// condition instead of 404-ing, and a refused case cannot post
/// assessment changes.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed partial class AssessmentCopyWebTests
{
    [Fact]
    public async Task PageCarriesNoHintSentencesOrExplainerCards()
    {
        var caseId = Guid.NewGuid();
        using var factory = Compose(caseId, out _);
        using var client = EngineerClient(factory);

        using var response = await client.GetAsync($"/Cases/{caseId:D}/Assessment");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain(">Required.", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">Optional.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("The principal sets the default", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Guide figures stay on this screen", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Most of the report is written for you", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Each line is worth its work units", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Remove-and-refit lines are not named", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// D11: refused access renders the contract's unavailable surface —
    /// the case is named, the export condition stated, the way back
    /// offered — while the case itself still resolves for the header.
    /// </summary>
    [Fact]
    public async Task RefusedAccessRendersTheUnavailableSurfaceNamingTheExport()
    {
        var caseId = Guid.NewGuid();
        using var factory = Compose(caseId, out _, canOpen: false);
        using var client = EngineerClient(factory);

        using var response = await client.GetAsync($"/Cases/{caseId:D}/Assessment");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Assessment unavailable", html, StringComparison.Ordinal);
        Assert.Contains("QDOS-2026-00042", html, StringComparison.Ordinal);
        Assert.Contains("A current Review-cycle EVA export is required", html, StringComparison.Ordinal);
        Assert.Contains("Back to Case", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InaccessibleCaseCannotPostAssessmentChanges()
    {
        var caseId = Guid.NewGuid();
        using var factory = Compose(caseId, out _, canOpen: false);
        using var client = EngineerClient(factory);
        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}/Assessment");

        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}/Assessment?handler=SendToClaude",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = AntiforgeryValue(html),
                ["id"] = caseId.ToString("D"),
                ["operationKey"] = Guid.NewGuid().ToString("N"),
                ["direction"] = "Draft the estimate",
                ["targetPercent"] = "80",
            }));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static WebApplicationFactory<Program> Compose(
        Guid caseId,
        out FakeGetCase source,
        bool canOpen = true)
    {
        var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        source = new FakeGetCase(caseId);
        var fakeSource = source;
        return baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetCase>();
                services.RemoveAll<IGetAssessmentAccess>();
                services.RemoveAll<IGetAssessmentWorkspace>();
                services.AddSingleton<IGetCase>(fakeSource);
                services.AddSingleton<IGetAssessmentAccess>(new FakeGetAssessmentAccess(canOpen));
                services.AddSingleton<IGetAssessmentWorkspace>(fakeSource);
            }));
    }

    private static HttpClient EngineerClient(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139"),
        });
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Engineer");
        return client;
    }

    private static async Task<string> GetHtmlAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static string AntiforgeryValue(string html)
    {
        var tag = AntiforgeryTagRegex().Match(html);
        Assert.True(tag.Success, "The page must render an antiforgery token.");
        var value = Regex.Match(tag.Value, "value=\"(?<value>[^\"]+)\"");
        Assert.True(value.Success, "The antiforgery token must have a value.");
        return WebUtility.HtmlDecode(value.Groups["value"].Value);
    }

    [GeneratedRegex("<input[^>]*name=\"__RequestVerificationToken\"[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AntiforgeryTagRegex();

    private sealed class FakeGetCase(Guid caseId) : IGetCase, IGetAssessmentWorkspace
    {
        public Task<CaseDetails?> ExecuteAsync(GetCaseQuery query, CancellationToken cancellationToken)
        {
            if (query.CaseId != caseId)
            {
                return Task.FromResult<CaseDetails?>(null);
            }

            var identity = new CaseIdentity(caseId, "QDOS", 2026, 42, "QDOS-2026-00042");
            var workflow = new CaseWorkflowRecord(
                caseId, identity, CaseLifecycleState.Review, null, null,
                null, null, null, null, null, 7);
            var summary = new CaseSearchItem(
                caseId, identity.Reference, null, CaseType.Inspection, "Approved Principal",
                workflow.State, null, "AB12CDE", "Alex Example", "P-100",
                DateTimeOffset.UtcNow, new DateOnly(2026, 8, 1), "Email", DateTimeOffset.UtcNow);
            CaseDetails details = new(
                summary, workflow, null, [], null, CaseCustodyState.Pending, [], [], []);
            return Task.FromResult<CaseDetails?>(details);
        }

        public async Task<AssessmentWorkspace?> ExecuteAsync(
            GetAssessmentWorkspaceQuery query,
            CancellationToken cancellationToken = default)
        {
            var details = await ExecuteAsync(new GetCaseQuery(query.CaseId, query.Actor), cancellationToken);
            if (details is null)
            {
                return null;
            }
            var assessment = new CaseAssessmentProjection(
                caseId, "QDOS-2026-00042", 7, CaseLifecycleState.Review, null, [], [],
                new(null, null, null, null, null, null, null, null, null));
            return AssessmentWorkspaceTestData.Create(details, assessment);
        }
    }
}
