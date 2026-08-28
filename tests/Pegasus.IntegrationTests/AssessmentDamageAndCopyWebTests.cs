using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;

namespace Pegasus.IntegrationTests;

/// <summary>
/// ENG-006: the assessment page carries no explanatory copy (required state is
/// visual), the damage diagram is clickable and saves the case's impact
/// location through the assessment save seam, and the method radios preselect
/// the case's recorded inspection mode.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed partial class AssessmentDamageAndCopyWebTests
{
    [Theory]
    [InlineData("vehicle")]
    [InlineData("incident")]
    [InlineData("inspection")]
    [InlineData("valuation")]
    [InlineData("estimate")]
    [InlineData("report")]
    public async Task SectionsCarryNoHintSentencesOrExplainerCards(string section)
    {
        var caseId = Guid.NewGuid();
        using var factory = Compose(caseId, out _);
        using var client = EngineerClient(factory);

        using var response = await client.GetAsync($"/Cases/{caseId:D}/Assessment?section={section}");
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

    [Fact]
    public async Task DamageRegionClickSavesTheImpactLocationThroughTheAssessmentSeam()
    {
        var caseId = Guid.NewGuid();
        using var factory = Compose(caseId, out var recorder);
        using var client = EngineerClient(factory);

        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}/Assessment?section=report");
        Assert.Contains("class=\"damage-region\"", html, StringComparison.Ordinal);
        var token = AntiforgeryValue(html);
        var operationKey = Guid.NewGuid().ToString("N");

        // CASE-024: the save runs under edit mode the operator entered, not under a lease the
        // handler claims for itself, so the assessment presents the token it was rendered with.
        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}/Assessment?handler=SaveDamage",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["id"] = caseId.ToString("D"),
                ["operationKey"] = operationKey,
                ["editLeaseToken"] = "lease-token",
                ["impactLocation"] = "left_front",
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var saved = Assert.Single(recorder.SavedRequests);
        Assert.Equal(caseId, saved.CaseId);
        Assert.Equal(operationKey, saved.OperationKey);
        Assert.Equal("lease-token", saved.EditLeaseToken);
        Assert.Equal("left_front", Assert.Single(saved.Fields).Value);
        Assert.Equal(AssessmentVocabulary.ImpactLocation, Assert.Single(saved.Fields).Key);

        recorder.SavedImpactLocation = "left_front";
        var after = await GetHtmlAsync(client, $"/Cases/{caseId:D}/Assessment?section=report");
        Assert.Contains(
            "value=\"left_front\"\n                                class=\"damage-region\" aria-pressed=\"true\"",
            after.Replace("\r\n", "\n"),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task InaccessibleCaseCannotPostAssessmentChanges()
    {
        var caseId = Guid.NewGuid();
        using var factory = Compose(caseId, out var recorder, canOpen: false);
        using var client = EngineerClient(factory);
        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}/Assessment?section=report");

        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}/Assessment?handler=SaveDamage",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = AntiforgeryValue(html),
                ["id"] = caseId.ToString("D"),
                ["operationKey"] = Guid.NewGuid().ToString("N"),
                ["impactLocation"] = "left_front",
            }));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(recorder.SavedRequests);
    }

    [Fact]
    public async Task MethodRadioPreselectsTheRecordedInspectionMode()
    {
        var caseId = Guid.NewGuid();
        using var factory = Compose(caseId, out _, CaseInspectionMode.ImageBasedAssessment);
        using var client = EngineerClient(factory);

        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}/Assessment?section=inspection");

        Assert.Contains("value=\"image_based\"", html, StringComparison.Ordinal);
        var imageRadio = Regex.Match(html, "<input[^>]*id=\"method-image\"[^>]*>").Value;
        Assert.Contains("checked", imageRadio, StringComparison.Ordinal);
        var physicalRadio = Regex.Match(html, "<input[^>]*id=\"method-physical\"[^>]*>").Value;
        Assert.DoesNotContain("checked", physicalRadio, StringComparison.Ordinal);
    }

    private static WebApplicationFactory<Program> Compose(
        Guid caseId,
        out Recorder recorder,
        CaseInspectionMode? mode = null,
        bool canOpen = true)
    {
        var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var stores = new Recorder(caseId, mode);
        recorder = stores;
        return baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetCase>();
                services.RemoveAll<IGetCaseAssessment>();
                services.RemoveAll<IGetAssessmentAccess>();
                services.RemoveAll<IGetAssessmentWorkspace>();
                services.RemoveAll<ISaveAssessment>();
                services.RemoveAll<IAcquireCaseEditLease>();
                services.AddSingleton<IGetCase>(stores);
                services.AddSingleton<IGetCaseAssessment>(stores);
                services.AddSingleton<IGetAssessmentAccess>(new FakeGetAssessmentAccess(canOpen));
                services.AddSingleton<IGetAssessmentWorkspace>(stores);
                services.AddSingleton<ISaveAssessment>(stores);
                services.AddSingleton<IAcquireCaseEditLease>(stores);
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

    private sealed class Recorder(Guid caseId, CaseInspectionMode? mode)
        : IGetCase, IGetCaseAssessment, IGetAssessmentWorkspace, ISaveAssessment, IAcquireCaseEditLease
    {
        public List<SaveAssessmentRequest> SavedRequests { get; } = [];

        public string? SavedImpactLocation { get; set; }

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
                summary, workflow, null, [], null, CaseCustodyState.Pending, [], [], [])
            {
                Data = mode is null ? null : Projection(identity),
            };
            return Task.FromResult<CaseDetails?>(details);
        }

        private CaseDataProjection Projection(CaseIdentity identity) => new(
            identity,
            new(Guid.NewGuid(), IntakeSourceChannel.Mailbox, "token", new string('1', 64),
                DateTimeOffset.UtcNow, "reader", "1", null, null),
            DateTimeOffset.UtcNow,
            7,
            CaseLifecycleState.Review,
            new(new(true, true, false, false), new(true, "policy", 1)),
            new(Empty<string>()),
            new(Empty<string>(), Empty<string>(), Empty<string>()),
            new(Empty<string>()),
            new(Empty<string>(), Empty<string>(), Empty<string>(), Empty<long>(), Empty<string>()),
            new(Empty<DateOnly>(), Empty<string>()),
            new(Empty<string>(), Empty<string>(), Empty<string>()),
            new(Empty<DateOnly>(), Empty<string>()),
            new(
                Empty<DateOnly>(),
                Empty<DateOnly>(),
                Empty<string>(),
                new(
                    new(
                        mode!.Value,
                        CaseDataValueKind.Fact,
                        new(CaseDataSourceKind.ProviderSetting, "QDOS", "Provider setting", "policy", 1)),
                    null,
                    null)));

        private static CaseField<T> Empty<T>() where T : notnull => new(null, null, null);

        Task<CaseAssessmentProjection?> IGetCaseAssessment.ExecuteAsync(
            Guid id, CancellationToken cancellationToken)
        {
            if (id != caseId)
            {
                return Task.FromResult<CaseAssessmentProjection?>(null);
            }

            IReadOnlyList<AssessmentFieldValue> fields = SavedImpactLocation is null
                ? []
                : [new(AssessmentVocabulary.ImpactLocation, SavedImpactLocation, ActorKind.Staff, "staff", DateTimeOffset.UtcNow, null, null)];
            return Task.FromResult<CaseAssessmentProjection?>(new(
                caseId, "QDOS-2026-00042", 7, CaseLifecycleState.Review, null, fields, [], EmptyOwned));
        }

        async Task<AssessmentWorkspace?> IGetAssessmentWorkspace.ExecuteAsync(
            GetAssessmentWorkspaceQuery query,
            CancellationToken cancellationToken)
        {
            var details = await ExecuteAsync(new GetCaseQuery(query.CaseId, query.Actor), cancellationToken);
            var assessment = await ((IGetCaseAssessment)this).ExecuteAsync(query.CaseId, cancellationToken);
            return details is null || assessment is null
                ? null
                : AssessmentWorkspaceTestData.Create(details, assessment);
        }

        public Task<CaseAssessmentProjection> ExecuteAsync(
            SaveAssessmentRequest request, CancellationToken cancellationToken)
        {
            SavedRequests.Add(request);
            return Task.FromResult(new CaseAssessmentProjection(
                request.CaseId, "QDOS-2026-00042", 8, CaseLifecycleState.Review, null, [], [], EmptyOwned));
        }

        private static readonly AssessmentCaseOwnedData EmptyOwned =
            new(null, null, null, null, null, null, null, null, null);

        public Task<CaseEditLease> ExecuteAsync(
            ClaimCaseEditLeaseRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new CaseEditLease(
                request.CaseId,
                "lease-token",
                "engineer",
                request.ExpectedVersion,
                DateTimeOffset.UtcNow.AddMinutes(5)));
    }
}
