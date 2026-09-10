using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Assessment;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The Administrator's valuation preset area, driven through the real page,
/// the real Core use cases and the real EF store over the seeded LocalDB.
/// </summary>
/// <remarks>
/// The two valuation-preset ports are composed here rather than in production
/// DI: the composition-root registration belongs to Foundation's DI patch, and
/// this suite adds exactly the same two lines so the page is proved against
/// its real collaborators until that patch lands.
/// </remarks>
[Trait("Category", "SqlServer")]
public sealed partial class ValuationPresetAdministrationWebTests
{
    private const string Page = "/Administration/ValuationPresets";

    private static readonly Guid TowBarPresetId =
        Guid.Parse("00000000-0000-4000-8000-00000000f001");

    [Fact]
    public async Task NonAdministratorCannotOpenValuationPresets()
    {
        using var factory = new IntakeWebApplicationFactory(
            useIntegrationTestAuthentication: true);
        using var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-Test-Roles", "User");

        using var response = await client.GetAsync(Page);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task NonAdministratorCannotPostAValuationPresetChange()
    {
        using var factory = new IntakeWebApplicationFactory(
            useIntegrationTestAuthentication: true);
        using var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Engineer");

        using var response = await client.PostAsync(
            $"{Page}?handler=Save",
            new FormUrlEncodedContent(new Dictionary<string, string>()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// The table is a compact read surface with one inline add row; add and
    /// edit both stay within the row's own columns and carry no
    /// routine-reason field.
    /// </summary>
    [Fact]
    public async Task ThePresetListIsReadFirstAndItsEditorHasNoRoutineReason()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);

        var body = await GetPageAsync(client);
        var match = PresetListSectionRegex().Match(body);
        Assert.True(match.Success, "The valuation preset list section must render.");
        var presetList = match.Value;

        Assert.Contains("Tow bar", presetList, StringComparison.Ordinal);
        Assert.Contains("£300.00", WebUtility.HtmlDecode(presetList), StringComparison.Ordinal);
        Assert.Contains("£1,500.00", WebUtility.HtmlDecode(presetList), StringComparison.Ordinal);
        Assert.Contains("£0.00", WebUtility.HtmlDecode(presetList), StringComparison.Ordinal);
        Assert.Contains(">Enabled<", presetList, StringComparison.Ordinal);
        Assert.DoesNotContain("<details", presetList, StringComparison.Ordinal);
        Assert.Contains(">Edit<", presetList, StringComparison.Ordinal);
        Assert.DoesNotContain("<th scope=\"col\">Change</th>", presetList, StringComparison.Ordinal);
        Assert.DoesNotContain("<th scope=\"col\">Save</th>", presetList, StringComparison.Ordinal);
        Assert.DoesNotContain("Reason", presetList, StringComparison.Ordinal);
        Assert.DoesNotContain("<p>", presetList, StringComparison.Ordinal);
        Assert.DoesNotContain("<aside", presetList, StringComparison.Ordinal);
        Assert.DoesNotContain("empty-state", presetList, StringComparison.Ordinal);
        Assert.Contains("class=\"input-money\"", body, StringComparison.Ordinal);

        // Add and edit carry no routine reason. The one reason on this page
        // belongs to the removal confirm, which is not part of the table.
        Assert.DoesNotContain("name=\"reason\"", presetList, StringComparison.Ordinal);

        // The add row is a compact final row of the presets table (points 20,
        // 32, 33), not a separate dialog-based creation panel.
        Assert.Contains("id=\"create-label\"", presetList, StringComparison.Ordinal);
        Assert.Contains("id=\"create-amount\"", presetList, StringComparison.Ordinal);
        Assert.Contains(">Add<", presetList, StringComparison.Ordinal);
        Assert.DoesNotContain("data-dialog=\"preset-", body, StringComparison.Ordinal);

        // No dialog is defined inside the table. The removal opener a row
        // carries is not a dialog of its own.
        Assert.DoesNotContain("data-dialog=\"", presetList, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdministratorCreatesEditsAndDisablesAPreset()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);
        var page = await GetPageAsync(client);

        var createForm = CreateForm(page);
        using (var created = await client.PostAsync(
            $"{Page}?handler=Create",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["presetId"] = FirstValue(createForm, PresetIdRegex()),
                ["operationKey"] = FirstValue(createForm, OperationKeyRegex()),
                ["label"] = "Roof rack",
                ["amount"] = "125.00",
                ["active"] = "true",
                ["__RequestVerificationToken"] = Token(page)
            })))
        {
            Assert.Equal(HttpStatusCode.Found, created.StatusCode);
        }

        page = await GetPageAsync(client);
        Assert.Contains("Roof rack", page, StringComparison.Ordinal);
        Assert.Contains(">£125.00<", WebUtility.HtmlDecode(page), StringComparison.Ordinal);

        var editPage = await OpenPresetEditAsync(client, TowBarPresetId, 1);
        var edit = RowForm(editPage, TowBarPresetId, "Tow bar", "350.00", "true");
        using (var edited = await PostSaveAsync(client, edit))
        {
            Assert.Equal(HttpStatusCode.Found, edited.StatusCode);
        }

        page = await GetPageAsync(client);
        Assert.Contains(">£350.00<", WebUtility.HtmlDecode(page), StringComparison.Ordinal);
        Assert.DoesNotContain(">£300.00<", WebUtility.HtmlDecode(page), StringComparison.Ordinal);

        // The version the first post consumed is stale on a second, freshly
        // keyed post, and the page says so rather than writing a second edit.
        var stale = new Dictionary<string, string>(edit, StringComparer.Ordinal)
        {
            ["operationKey"] = Guid.NewGuid().ToString("N")
        };
        using (var refused = await PostSaveAsync(client, stale))
        {
            Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
            var refusedPage = await refused.Content.ReadAsStringAsync();
            Assert.Contains(
                "The preset changed after this page was loaded.",
                refusedPage,
                StringComparison.Ordinal);
            Assert.Contains("value=\"350.00\"", refusedPage, StringComparison.Ordinal);
        }

        var disablePage = await OpenPresetEditAsync(client, TowBarPresetId, 2);
        using (var disabled = await PostSaveAsync(
            client,
            RowForm(
                disablePage,
                TowBarPresetId,
                "Tow bar",
                "350.00",
                "false")))
        {
            Assert.Equal(HttpStatusCode.Found, disabled.StatusCode);
        }

        page = await GetPageAsync(client);
        Assert.Contains(">Disabled<", page, StringComparison.Ordinal);
        Assert.Contains("Tow bar", page, StringComparison.Ordinal);
        var enablePage = await OpenPresetEditAsync(client, TowBarPresetId, 3);
        Assert.Contains(">Enabled<", enablePage, StringComparison.Ordinal);
        using var enabled = await PostSaveAsync(
            client, RowForm(enablePage, TowBarPresetId, "Tow bar", "350.00", "true"));
        Assert.Equal(HttpStatusCode.Found, enabled.StatusCode);
    }

    /// <summary>
    /// A refused post comes back with its own operation key replaced, so a
    /// corrected retry cannot replay the key the server already saw.
    /// </summary>
    [Fact]
    public async Task ARefusedCreateReMintsTheOperationKeyAndKeepsTheTypedValues()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);
        var page = await OpenPresetEditAsync(client, TowBarPresetId, 1);
        var operationKey = LastValue(page, OperationKeyRegex());

        using var response = await client.PostAsync(
            $"{Page}?handler=Create",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["presetId"] = LastValue(page, PresetIdRegex()),
                ["operationKey"] = operationKey,
                ["label"] = "Roof rack",
                ["amount"] = "300.123",
                ["active"] = "true",
                ["__RequestVerificationToken"] = Token(page)
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Enter an amount of", body, StringComparison.Ordinal);
        Assert.Contains("value=\"Roof rack\"", body, StringComparison.Ordinal);
        Assert.NotEqual(operationKey, LastValue(body, OperationKeyRegex()));
        Assert.DoesNotContain("Roof rack</td>", body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("not-a-number")]
    [InlineData("-1.00")]
    [InlineData("300.123")]
    public async Task ARefusedRowEditKeepsItsAttemptedLabelAndRawAmount(string amount)
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);
        var page = await OpenPresetEditAsync(client, TowBarPresetId, 1);

        using var response = await PostSaveAsync(
            client,
            RowForm(
                page,
                TowBarPresetId,
                "Updated tow bar",
                amount,
                "true"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Enter an amount of", body, StringComparison.Ordinal);
        Assert.Contains("value=\"Updated tow bar\"", body, StringComparison.Ordinal);
        Assert.Contains("name=\"amount\" type=\"number\" inputmode=\"decimal\"", body, StringComparison.Ordinal);
        Assert.Contains($"value=\"{amount}\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "name=\"reason\"",
            PresetListSectionRegex().Match(body).Value,
            StringComparison.Ordinal);
        var reloaded = await GetPageAsync(client);
        Assert.DoesNotContain("Updated tow bar", reloaded, StringComparison.Ordinal);
        Assert.Contains(">Tow bar<", reloaded, StringComparison.Ordinal);
        Assert.Contains(">£300.00<", WebUtility.HtmlDecode(reloaded), StringComparison.Ordinal);
    }

    /// <summary>
    /// The operator is never blocked by their own edit. A second window is
    /// named as such and offers Take over; a window that leaves beacons its
    /// release, after which the editor reopens with no take-over at all.
    /// </summary>
    [Fact]
    public async Task AnOperatorIsNeverBlockedByTheirOwnPresetEdit()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);

        var editing = await OpenPresetEditAsync(client, TowBarPresetId, 1);
        var firstToken = FirstValue(editing, EditLeaseTokenRegex());

        // A second window while the first is still live says whose edit it is
        // and offers the one control the holder is entitled to.
        var second = await OpenPresetEditAsync(client, TowBarPresetId, 1);
        Assert.Contains(
            "You are editing this preset in another window.",
            second,
            StringComparison.Ordinal);
        Assert.Contains(">Take over<", second, StringComparison.Ordinal);
        Assert.DoesNotContain("is editing it", second, StringComparison.Ordinal);

        // Taking over rotates the token, so the abandoned window's own token no
        // longer saves. It is a post, never a followed link: taking over ends
        // another window's claim, which a GET must never do.
        var takenOver = await PostTakeOverAsync(client, second);
        var secondToken = FirstValue(takenOver, EditLeaseTokenRegex());
        Assert.NotEqual(firstToken, secondToken);
        Assert.DoesNotContain("another window", takenOver, StringComparison.Ordinal);

        // The release a leaving page beacons is idempotent and answers 204
        // whether or not a scope was still there.
        using (var released = await PostBeaconAsync(client, takenOver, secondToken))
        {
            Assert.Equal(HttpStatusCode.NoContent, released.StatusCode);
        }
        using (var again = await PostBeaconAsync(client, takenOver, secondToken))
        {
            Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        }

        // Reopening immediately after the beacon is an ordinary claim.
        var reopened = await OpenPresetEditAsync(client, TowBarPresetId, 1);
        Assert.NotEqual(secondToken, FirstValue(reopened, EditLeaseTokenRegex()));
        Assert.DoesNotContain("another window", reopened, StringComparison.Ordinal);
        Assert.DoesNotContain(">Take over<", reopened, StringComparison.Ordinal);
    }

    /// <summary>
    /// Posts the Take over form the page rendered for the operator's own
    /// other window: the same fields the form carries as hidden inputs, plus
    /// the page's antiforgery token.
    /// </summary>
    private static async Task<string> PostTakeOverAsync(HttpClient client, string page)
    {
        var form = TakeOverForm(page);
        using var response = await client.PostAsync(
            $"{Page}?handler=Edit",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["presetId"] = FirstValue(form, PresetIdRegex()),
                ["expectedVersion"] = FirstValue(form, ExpectedVersionRegex()),
                ["operationKey"] = FirstValue(form, OperationKeyRegex()),
                ["takeOver"] = "true",
                ["__RequestVerificationToken"] = Token(page)
            }));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// The Take over form itself, found by the button text rather than by
    /// position: it is the only form on the row offering that control.
    /// </summary>
    private static string TakeOverForm(string page)
    {
        var buttonIndex = page.IndexOf(">Take over<", StringComparison.Ordinal);
        Assert.True(buttonIndex >= 0);
        var formStart = page.LastIndexOf("<form", buttonIndex, StringComparison.Ordinal);
        Assert.True(formStart >= 0);
        var formEnd = page.IndexOf("</form>", buttonIndex, StringComparison.Ordinal);
        Assert.True(formEnd >= 0);
        return page[formStart..formEnd];
    }

    private static Task<HttpResponseMessage> PostBeaconAsync(
        HttpClient client,
        string page,
        string editLeaseToken) =>
        client.PostAsync(
            $"{Page}?handler=ReleaseScopeBeacon",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["presetId"] = TowBarPresetId.ToString("D"),
                ["editLeaseToken"] = editLeaseToken,
                ["__RequestVerificationToken"] = Token(page)
            }));

    /// <summary>
    /// The hidden add-row form's own fields, read from that form rather than
    /// from the last matching input on the page: the removal dialogs each
    /// carry their own <c>presetId</c> input after it, so a page-wide search
    /// would find one of those instead of the create form's own.
    /// </summary>
    private static string CreateForm(string page)
    {
        var start = page.IndexOf("id=\"preset-create\"", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var end = page.IndexOf("</form>", start, StringComparison.Ordinal);
        Assert.True(end >= 0);
        return page[start..end];
    }

    /// <summary>
    /// The hidden fields of one preset's own row form, read from that row
    /// rather than from the first one on the page: the rows are ordered by
    /// label, so the row a test means is the row it names.
    /// </summary>
    private static Dictionary<string, string> RowForm(
        string page,
        Guid presetId,
        string label,
        string amount,
        string active)
    {
        var start = page.IndexOf(
            $"id=\"preset-{presetId:D}\"",
            StringComparison.Ordinal);
        Assert.True(start >= 0);
        var row = page[start..page.IndexOf("</form>", start, StringComparison.Ordinal)];
        return new(StringComparer.Ordinal)
        {
            ["presetId"] = FirstValue(row, PresetIdRegex()),
            ["expectedVersion"] = FirstValue(row, ExpectedVersionRegex()),
            ["editLeaseToken"] = FirstValue(row, EditLeaseTokenRegex()),
            ["operationKey"] = FirstValue(row, OperationKeyRegex()),
            ["label"] = label,
            ["amount"] = amount,
            ["active"] = active,
            ["__RequestVerificationToken"] = Token(page)
        };
    }

    private static Task<HttpResponseMessage> PostSaveAsync(
        HttpClient client,
        IReadOnlyDictionary<string, string> form) =>
        client.PostAsync($"{Page}?handler=Save", new FormUrlEncodedContent(form));

    private static async Task<string> GetPageAsync(HttpClient client)
    {
        using var response = await client.GetAsync(Page);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private static async Task<string> OpenPresetEditAsync(
        HttpClient client,
        Guid presetId,
        long expectedVersion)
    {
        using var response = await client.GetAsync(
            $"{Page}?editPresetId={presetId:D}&expectedVersion={expectedVersion}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Composes the two valuation-preset ports the page needs, over the
    /// factory's own LocalDB, and returns a client that does not follow the
    /// post-redirect-get on its own.
    /// </summary>
    private static HttpClient CreateClient(IntakeWebApplicationFactory factory) =>
        factory
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.AddScoped<EfValuationPresetStore>();
                services.AddScoped<IValuationPresetStore>(provider =>
                    provider.GetRequiredService<EfValuationPresetStore>());
                services.AddScoped<IListValuationPresets, ListValuationPresets>();
                services.AddScoped<ISaveValuationPreset, SaveValuationPreset>();
                services.AddScoped<IRemoveValuationPreset, RemoveValuationPreset>();
            }))
            .CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost:7139")
            });

    private static string Token(string page) => Value(AntiforgeryRegex().Match(page).Value);

    private static string FirstValue(string page, Regex regex)
    {
        var match = regex.Match(page);
        Assert.True(match.Success);
        return Value(match.Value);
    }

    private static string LastValue(string page, Regex regex)
    {
        var matches = regex.Matches(page);
        Assert.NotEmpty(matches);
        return Value(matches[^1].Value);
    }

    private static string Value(string tag)
    {
        var match = ValueRegex().Match(tag);
        Assert.True(match.Success);
        return match.Groups["value"].Value;
    }

    [GeneratedRegex("<input[^>]*name=\"presetId\"[^>]*>", RegexOptions.IgnoreCase)] private static partial Regex PresetIdRegex();
    [GeneratedRegex("<input[^>]*name=\"expectedVersion\"[^>]*>", RegexOptions.IgnoreCase)] private static partial Regex ExpectedVersionRegex();
    [GeneratedRegex("<input[^>]*name=\"editLeaseToken\"[^>]*>", RegexOptions.IgnoreCase)] private static partial Regex EditLeaseTokenRegex();
    [GeneratedRegex("<input[^>]*name=\"operationKey\"[^>]*>", RegexOptions.IgnoreCase)] private static partial Regex OperationKeyRegex();
    [GeneratedRegex("<input[^>]*name=\"__RequestVerificationToken\"[^>]*>", RegexOptions.IgnoreCase)] private static partial Regex AntiforgeryRegex();
    [GeneratedRegex("value=\"(?<value>[^\"]*)\"", RegexOptions.IgnoreCase)] private static partial Regex ValueRegex();
    [GeneratedRegex("<section[^>]*aria-labelledby=\"valuation-presets-title\"[^>]*>[\\s\\S]*?</section>", RegexOptions.IgnoreCase)] private static partial Regex PresetListSectionRegex();
}
