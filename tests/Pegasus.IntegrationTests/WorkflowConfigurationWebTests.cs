using System.Net;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed partial class WorkflowConfigurationWebTests
{
    private const string Route = "/Administration/Configuration";

    private static readonly string[] WorkflowPanelHeadings =
        ["Required before Review", "Case/PO", "Chasing and due targets", "Labour-rate cards"];

    private static readonly string[] WorkflowPanelHandlers = ["SaveReview", "SaveReference", "SaveTargets", "SaveCards"];

    // What each panel's form posts besides its antiforgery token, in ordinal order.
    private static readonly string[] ReviewFields =
        ["RequireImages", "RequireInstructions", "WorkflowExpectedVersion", "WorkflowOperationKey"];

    private static readonly string[] ReferenceFields =
        ["CaseReferenceSuffix", "WorkflowExpectedVersion", "WorkflowOperationKey"];

    private static readonly string[] TargetFields =
    [
        "AiDraftTargetDays", "ChaseIntervalDays", "HeldTargetDays", "ReviewTargetDays",
        "TriageTargetDays", "UnidentifiedTargetDays", "WorkflowExpectedVersion", "WorkflowOperationKey"
    ];

    private static readonly string[] CardFields = ["CardIds", "CardOperationKeys", "CardVersions", "HourlyRates"];

    /// <summary>
    /// One panel per concern, each with its own Save (v50 A to E; R910):
    /// Required before Review, Case/PO, Chasing and due targets and the
    /// labour-rate cards. The day boxes carry "days" after them rather than in
    /// their labels, and Send a photo link's settings are not on this page
    /// (R911).
    /// </summary>
    [Fact]
    public async Task AdministratorSeesEditableWorkflowAndRateCardForms()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var html = await GetPageAsync(client);
        foreach (var heading in WorkflowPanelHeadings)
        {
            Assert.Contains($">{HtmlEncoder.Default.Encode(heading)}</h2>", html, StringComparison.Ordinal);
        }

        Assert.Contains("Chase interval", html, StringComparison.Ordinal);
        Assert.DoesNotContain("(days)", html, StringComparison.Ordinal);
        Assert.Equal(6, Regex.Count(html, "<span class=\"workflow-day__unit\"[^>]*>days</span>"));
        Assert.DoesNotContain("There are no editable settings", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"LeaseToken\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"WorkflowExpectedVersion\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"ChaseIntervalDays\"", html, StringComparison.Ordinal);
        foreach (var handler in WorkflowPanelHandlers)
        {
            Assert.Contains($"handler={handler}\"", html, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("handler=SaveWorkflow", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Save workflow settings", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-photo-link-settings", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"PhotoLinkStartMode\"", html, StringComparison.Ordinal);
        // The two built-in cards, Standard first, with no way to add another.
        Assert.DoesNotContain("handler=NewCard", html, StringComparison.Ordinal);
        Assert.True(html.IndexOf("<td>Standard</td>", StringComparison.Ordinal) is > 0 and var standard
            && standard < html.IndexOf("<td>Prestige/Aluminium</td>", StringComparison.Ordinal));
    }

    /// <summary>
    /// Each panel's form carries only its own settings with the version and
    /// operation key: Review's two switches, the Case/PO switch, the six day
    /// boxes, and the cards' rows.
    /// </summary>
    [Fact]
    public async Task EachPanelPostsOnlyItsOwnSettings()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var html = await GetPageAsync(client);

        Assert.Equal(ReviewFields, FieldNamesOf(PanelForm(html, "SaveReview")));
        Assert.Equal(ReferenceFields, FieldNamesOf(PanelForm(html, "SaveReference")));
        Assert.Equal(TargetFields, FieldNamesOf(PanelForm(html, "SaveTargets")));
        Assert.Equal(CardFields, FieldNamesOf(PanelForm(html, "SaveCards")));
    }

    [Fact]
    public async Task WorkflowSettingsSaveDirectlyAndKeepRangeValidation()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var html = await GetPageAsync(client);
        Assert.Matches("name=\"ChaseIntervalDays\"[^>]*min=\"1\"[^>]*max=\"365\"", html);
        foreach (var field in new[] { "UnidentifiedTargetDays", "TriageTargetDays", "HeldTargetDays", "ReviewTargetDays", "AiDraftTargetDays" })
        {
            Assert.Matches($"name=\"{field}\"[^>]*min=\"0\"[^>]*max=\"365\"", html);
        }

        var fields = new Dictionary<string, string>
        {
            ["WorkflowExpectedVersion"] = Field(html, "WorkflowExpectedVersion"),
            ["WorkflowOperationKey"] = Field(html, "WorkflowOperationKey"),
            ["__RequestVerificationToken"] = Field(html, "__RequestVerificationToken"),
            ["ChaseIntervalDays"] = "7", ["UnidentifiedTargetDays"] = "0",
            ["TriageTargetDays"] = "400", ["HeldTargetDays"] = "7",
            ["ReviewTargetDays"] = "1", ["AiDraftTargetDays"] = "1"
        };
        using var refused = await client.PostAsync(Route + "?handler=SaveTargets", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
        Assert.Contains("Triage target must be between 0 and 365 days.", await refused.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        fields["TriageTargetDays"] = "3";
        fields["UnidentifiedTargetDays"] = "2";
        fields["HeldTargetDays"] = "4";
        fields["ReviewTargetDays"] = "5";
        fields["AiDraftTargetDays"] = "6";
        using var saved = await client.PostAsync(Route + "?handler=SaveTargets", new FormUrlEncodedContent(fields));
        Assert.True(
            saved.StatusCode == HttpStatusCode.Redirect,
            $"Expected a redirect but got {saved.StatusCode}. {await ValidationMessagesAsync(saved)}");
        var after = await GetPageAsync(client);
        Assert.Contains("Workflow settings saved.", after, StringComparison.Ordinal);
        Assert.Matches("name=\"TriageTargetDays\"[^>]*value=\"3\"", after);
        Assert.Matches("name=\"AiDraftTargetDays\"[^>]*value=\"6\"", after);
    }

    [Fact]
    public async Task AnOmittedWorkflowSettingIsRefusedWithItsRangeAndNothingIsSaved()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var html = await GetPageAsync(client);
        var storedVersion = Field(html, "WorkflowExpectedVersion");
        var storedTriage = Field(html, "TriageTargetDays");
        var fields = new Dictionary<string, string>
        {
            ["WorkflowExpectedVersion"] = storedVersion,
            ["WorkflowOperationKey"] = Field(html, "WorkflowOperationKey"),
            ["__RequestVerificationToken"] = Field(html, "__RequestVerificationToken"),
            ["ChaseIntervalDays"] = "7", ["UnidentifiedTargetDays"] = "0",
            ["HeldTargetDays"] = "7", ["ReviewTargetDays"] = "1", ["AiDraftTargetDays"] = "1"
        };

        using var refused = await client.PostAsync(Route + "?handler=SaveTargets", new FormUrlEncodedContent(fields));

        Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
        Assert.Contains("Triage target must be between 0 and 365 days.", await refused.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        var after = await GetPageAsync(client);
        Assert.Equal(storedVersion, Field(after, "WorkflowExpectedVersion"));
        Assert.Equal(storedTriage, Field(after, "TriageTargetDays"));
    }

    /// <summary>
    /// The temporary Case/PO suffix switch saves from its own panel and the
    /// page shows it as stored (operator, 4 November 2026; R656; v50 C,
    /// R910); it is its own record, so the workflow settings' version does not
    /// move. The suite's databases start with it off.
    /// </summary>
    [Fact]
    public async Task TheCaseReferenceSuffixSwitchSavesFromItsOwnPanel()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var html = await GetPageAsync(client);
        Assert.Contains("Add -p to new Case/PO", html, StringComparison.Ordinal);
        Assert.DoesNotMatch("name=\"CaseReferenceSuffix\"[^>]*checked", html);
        var before = await CurrentSettingsAsync(factory);

        var fields = new Dictionary<string, string>
        {
            ["WorkflowExpectedVersion"] = Field(html, "WorkflowExpectedVersion"),
            ["WorkflowOperationKey"] = Field(html, "WorkflowOperationKey"),
            ["__RequestVerificationToken"] = Field(html, "__RequestVerificationToken"),
            ["CaseReferenceSuffix"] = "true"
        };
        using var saved = await client.PostAsync(Route + "?handler=SaveReference", new FormUrlEncodedContent(fields));
        Assert.True(
            saved.StatusCode == HttpStatusCode.Redirect,
            $"Expected a redirect but got {saved.StatusCode}. {await ValidationMessagesAsync(saved)}");

        Assert.Matches("name=\"CaseReferenceSuffix\"[^>]*checked", await GetPageAsync(client));
        Assert.Equal(before.PolicyVersion, (await CurrentSettingsAsync(factory)).PolicyVersion);
    }

    /// <summary>
    /// Saving the Case/PO panel with the switch as stored writes nothing: the
    /// switch is set only when it changes, so no history is written (R656),
    /// and the workflow settings are not saved at all.
    /// </summary>
    [Fact]
    public async Task SavingTheCasePoPanelUnchangedWritesNothing()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var suffix = new RecordingCaseReferenceSuffix();
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            CaseWebTestSupport.Substitute<ICaseReferenceSuffixControl>(services, suffix)));
        using var client = IntakeWebDriver.CreateClient(factory);
        var html = await GetPageAsync(client);
        var before = await CurrentSettingsAsync(factory);

        // An unticked box posts nothing: the switch stays off, as stored.
        var fields = new Dictionary<string, string>
        {
            ["WorkflowExpectedVersion"] = Field(html, "WorkflowExpectedVersion"),
            ["WorkflowOperationKey"] = Field(html, "WorkflowOperationKey"),
            ["__RequestVerificationToken"] = Field(html, "__RequestVerificationToken")
        };
        using (var saved = await client.PostAsync(Route + "?handler=SaveReference", new FormUrlEncodedContent(fields)))
        {
            Assert.True(
                saved.StatusCode == HttpStatusCode.Redirect,
                $"Expected a redirect but got {saved.StatusCode}. {await ValidationMessagesAsync(saved)}");
        }

        Assert.Equal(0, suffix.SetCalls);
        Assert.Equal(before.PolicyVersion, (await CurrentSettingsAsync(factory)).PolicyVersion);
    }

    /// <summary>
    /// A panel saved over a version another save has moved on is refused with
    /// the reload sentence and writes nothing, though the other save touched a
    /// different panel (v50; R910).
    /// </summary>
    [Fact]
    public async Task APanelSavedOverAnotherSaveIsRefused()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var html = await GetPageAsync(client);
        var stale = Field(html, "WorkflowExpectedVersion");
        using (var targets = await client.PostAsync(Route + "?handler=SaveTargets", new FormUrlEncodedContent(TargetsForm(html, chaseIntervalDays: "9"))))
        {
            Assert.Equal(HttpStatusCode.Redirect, targets.StatusCode);
        }

        var saved = await CurrentSettingsAsync(factory);
        var fields = new Dictionary<string, string>
        {
            ["WorkflowExpectedVersion"] = stale,
            ["WorkflowOperationKey"] = Guid.NewGuid().ToString("N"),
            ["__RequestVerificationToken"] = Field(html, "__RequestVerificationToken"),
            ["RequireInstructions"] = "true"
        };
        using var refused = await client.PostAsync(Route + "?handler=SaveReview", new FormUrlEncodedContent(fields));

        Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
        Assert.Contains(
            "Workflow settings changed. Reload the page before trying again.",
            await refused.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
        var after = await CurrentSettingsAsync(factory);
        Assert.Equal(saved.PolicyVersion, after.PolicyVersion);
        Assert.Equal(saved.RequireImages, after.RequireImages);
    }

    /// <summary>
    /// Configuration (13 September): the page renders the workflow's six
    /// editable settings and carries versioned form metadata directly on load.
    /// </summary>
    [Fact]
    public async Task WorkflowSettingsCarryVersionedFormMetadataOnLoad()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var html = await GetPageAsync(client);
        Assert.Contains("name=\"WorkflowExpectedVersion\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"WorkflowOperationKey\"", html, StringComparison.Ordinal);
    }

    private static string Field(string html, string name)
    {
        var tag = Regex.Match(html, "<input[^>]*name=\"" + Regex.Escape(name) + "\"[^>]*>");
        Assert.True(tag.Success, "Missing field " + name);
        return WebUtility.HtmlDecode(Regex.Match(tag.Value, "value=\"([^\"]*)\"").Groups[1].Value);
    }

    /// <summary>
    /// The labour-rate cards save together from one Save (v50 E): only the
    /// card whose rate changed is written, each under its own version, with
    /// no edit scope opened.
    /// </summary>
    [Fact]
    public async Task RateCardRateCanBeSavedWithoutOpeningAnEditScope()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var html = await GetPageAsync(client);
        var cards = PanelForm(html, "SaveCards");
        var before = await RateCardsAsync(factory);
        var standard = Assert.Single(before, card => card.Name == "Standard");
        var ids = FieldValues(cards, "CardIds");
        var rates = FieldValues(cards, "HourlyRates");
        Assert.Equal(before.Count, ids.Length);
        var fields = new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", Field(html, "__RequestVerificationToken"))
        };
        for (var index = 0; index < ids.Length; index++)
        {
            fields.Add(new("CardIds", ids[index]));
            fields.Add(new("CardVersions", FieldValues(cards, "CardVersions")[index]));
            fields.Add(new("CardOperationKeys", FieldValues(cards, "CardOperationKeys")[index]));
            fields.Add(new("HourlyRates", ids[index] == standard.Id.ToString("D") ? "92.50" : rates[index]));
        }

        using var saved = await client.PostAsync(Route + "?handler=SaveCards", new FormUrlEncodedContent(fields));
        Assert.True(
            saved.StatusCode == HttpStatusCode.Redirect,
            $"Expected a redirect but got {saved.StatusCode}. {await ValidationMessagesAsync(saved)}");
        var after = await GetPageAsync(client);
        Assert.Contains("value=\"92.50\"", after, StringComparison.Ordinal);
        Assert.Contains("The labour-rate card was saved.", after, StringComparison.Ordinal);

        var stored = await RateCardsAsync(factory);
        Assert.Equal(92.50m, Assert.Single(stored, card => card.Id == standard.Id).HourlyRate);
        foreach (var unchanged in before.Where(card => card.Id != standard.Id))
        {
            Assert.Equal(unchanged, Assert.Single(stored, card => card.Id == unchanged.Id));
        }
    }

    /// <summary>A rate the number box cannot hold is not the page's post.</summary>
    [Fact]
    public async Task AnUnreadableRateIsABadRequest()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var html = await GetPageAsync(client);
        var cards = PanelForm(html, "SaveCards");
        List<KeyValuePair<string, string>> fields =
        [
            new("__RequestVerificationToken", Field(html, "__RequestVerificationToken")),
            new("CardIds", FieldValues(cards, "CardIds")[0]),
            new("CardVersions", FieldValues(cards, "CardVersions")[0]),
            new("CardOperationKeys", FieldValues(cards, "CardOperationKeys")[0]),
            new("HourlyRates", "ninety")
        ];
        var before = await RateCardsAsync(factory);

        using var refused = await client.PostAsync(Route + "?handler=SaveCards", new FormUrlEncodedContent(fields));

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal(before, await RateCardsAsync(factory));
    }

    /// <summary>The Chasing and due targets form as the page holds it, with the chase interval given.</summary>
    private static List<KeyValuePair<string, string>> TargetsForm(string html, string chaseIntervalDays) =>
    [
        new("WorkflowExpectedVersion", Field(html, "WorkflowExpectedVersion")),
        new("WorkflowOperationKey", Field(html, "WorkflowOperationKey")),
        new("__RequestVerificationToken", Field(html, "__RequestVerificationToken")),
        new("ChaseIntervalDays", chaseIntervalDays),
        new("UnidentifiedTargetDays", Field(html, "UnidentifiedTargetDays")),
        new("TriageTargetDays", Field(html, "TriageTargetDays")),
        new("HeldTargetDays", Field(html, "HeldTargetDays")),
        new("ReviewTargetDays", Field(html, "ReviewTargetDays")),
        new("AiDraftTargetDays", Field(html, "AiDraftTargetDays"))
    ];

    /// <summary>The form posting to the handler, from its tag to its end.</summary>
    private static string PanelForm(string html, string handler)
    {
        var form = Regex.Match(html, "<form[^>]*action=\"[^\"]*handler=" + Regex.Escape(handler) + "\"[^>]*>[\\s\\S]*?</form>");
        Assert.True(form.Success, $"The page has no {handler} form.");
        return form.Value;
    }

    /// <summary>The distinct names a form posts, the antiforgery token aside, in ordinal order.</summary>
    private static string[] FieldNamesOf(string form) =>
    [
        .. Regex.Matches(form, "<(?:input|select|textarea)[^>]*name=\"(?<name>[^\"]+)\"")
            .Select(match => match.Groups["name"].Value)
            .Where(name => name != "__RequestVerificationToken")
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
    ];

    /// <summary>Every value posted under the name, in the order the form holds them.</summary>
    private static string[] FieldValues(string form, string name) =>
    [
        .. Regex.Matches(form, "<input[^>]*name=\"" + Regex.Escape(name) + "\"[^>]*>")
            .Select(tag => WebUtility.HtmlDecode(Regex.Match(tag.Value, "value=\"([^\"]*)\"").Groups[1].Value))
    ];

    private static async Task<IReadOnlyList<LabourRateCard>> RateCardsAsync(IntakeWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ILabourRateCardStore>().ListAsync(CancellationToken.None);
    }

    /// <summary>The Case/PO switch, off, counting each time it is set.</summary>
    private sealed class RecordingCaseReferenceSuffix : ICaseReferenceSuffixControl
    {
        private int setCalls;

        public int SetCalls => Volatile.Read(ref setCalls);

        public Task<bool> IsEnabledAsync(CancellationToken cancellationToken) => Task.FromResult(false);

        public Task<bool> SetEnabledAsync(bool enabled, ActionActor actor, string operationKey, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref setCalls);
            return Task.FromResult(enabled);
        }
    }

    private static async Task<string> ValidationMessagesAsync(HttpResponseMessage response)
    {
        var html = await response.Content.ReadAsStringAsync();
        var messages = Regex.Matches(html, "<li>(?<message>[\\s\\S]*?)</li>")
            .Cast<Match>()
            .Select(match => WebUtility.HtmlDecode(Regex.Replace(match.Groups["message"].Value, "<[^>]+>", string.Empty)).Trim())
            .Where(message => message.Length > 0);
        var summary = string.Join(" | ", messages);
        return summary.Length == 0 ? "No validation messages were rendered." : summary;
    }
    [Fact]
    public async Task ComposedAutomationIsListedInTheHubAndThisPageAdministrationRail()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var mcpFactory = AutomationMcpTestSupport.WithAutomationMcp(factory);
        using var client = mcpFactory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost:7139")
            });

        // The hub's cards are its navigation, so it carries no rail of its own.
        using var hub = await client.GetAsync("/Administration");
        Assert.Equal(HttpStatusCode.OK, hub.StatusCode);
        Assert.Contains("href=\"/Administration/Automation\"", await hub.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        // A sibling page proves the ingress really is composed in this host,
        // so the assertion below cannot pass by both rails being short.
        var siblingRail = await AdminRailAsync(client, "/Administration/Mailboxes");
        Assert.Contains("Automation &amp; AI", siblingRail, StringComparison.Ordinal);

        // This page's rail lists the same areas as every sibling administration page.
        var rail = await AdminRailAsync(client, Route);
        Assert.Equal(AdminRailLinks(siblingRail), AdminRailLinks(rail));
    }

    [Fact]
    public async Task NonAdministratorCannotReachWorkflowConfiguration()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = IntakeWebDriver.CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-Test-Roles", "User");

        using var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task<string> AdminRailAsync(HttpClient client, string route)
    {
        using var response = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rail = AdminNavRegex().Match(await response.Content.ReadAsStringAsync());
        Assert.True(rail.Success, $"'{route}' must render the administration rail.");
        return rail.Value;
    }

    private static string[] AdminRailLinks(string rail) =>
        [.. AdminRailLinkRegex().Matches(rail).Select(link => link.Groups["href"].Value)];

    private static async Task<string> GetPageAsync(HttpClient client)
    {
        using var response = await client.GetAsync(Route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    [GeneratedRegex(
        "<nav[^>]*class=\"admin-nav[^\"]*\"[^>]*>[\\s\\S]*?</nav>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AdminNavRegex();

    [GeneratedRegex(
        "<a[^>]*href=\"(?<href>[^\"]*)\"",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AdminRailLinkRegex();
}
