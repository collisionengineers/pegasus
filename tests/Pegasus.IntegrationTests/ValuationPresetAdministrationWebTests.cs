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
    /// The table keeps every preset editable beside its status and has one
    /// inline add row. Save and Remove have independent forms and no
    /// routine-reason field.
    /// </summary>
    [Fact]
    public async Task ThePresetListRendersEditableRowsAndSeparateActionsWithoutRoutineReasons()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);

        var body = await GetPageAsync(client);
        var match = PresetListSectionRegex().Match(body);
        Assert.True(match.Success, "The valuation preset list section must render.");
        var presetList = match.Value;

        Assert.Contains("Tow bar", presetList, StringComparison.Ordinal);
        Assert.Contains("value=\"300.00\"", presetList, StringComparison.Ordinal);
        Assert.Contains("value=\"1500.00\"", presetList, StringComparison.Ordinal);
        Assert.Contains("value=\"0.00\"", presetList, StringComparison.Ordinal);
        Assert.Contains(">Enabled<", presetList, StringComparison.Ordinal);
        Assert.DoesNotContain("<details", presetList, StringComparison.Ordinal);
        Assert.Contains($"preset-save-{TowBarPresetId:N}", presetList, StringComparison.Ordinal);
        Assert.Contains($"preset-remove-{TowBarPresetId:N}", presetList, StringComparison.Ordinal);
        Assert.Contains("name=\"label\" form=\"preset-save-", presetList, StringComparison.Ordinal);
        Assert.Contains("name=\"amount\" type=\"number\"", presetList, StringComparison.Ordinal);
        Assert.Contains(">Save<", presetList, StringComparison.Ordinal);
        Assert.Contains(">Remove<", presetList, StringComparison.Ordinal);
        Assert.DoesNotContain("<th scope=\"col\">Change</th>", presetList, StringComparison.Ordinal);
        Assert.DoesNotContain("<th scope=\"col\">Save</th>", presetList, StringComparison.Ordinal);
        Assert.DoesNotContain("Reason", presetList, StringComparison.Ordinal);
        Assert.DoesNotContain("<p>", presetList, StringComparison.Ordinal);
        Assert.DoesNotContain("<aside", presetList, StringComparison.Ordinal);
        Assert.DoesNotContain("empty-state", presetList, StringComparison.Ordinal);
        Assert.Contains("class=\"input-money\"", body, StringComparison.Ordinal);

        // Nothing on this page asks for a reason; Remove posts from its row.
        Assert.DoesNotContain("name=\"reason\"", body, StringComparison.Ordinal);

        // The add row is a compact final row of the presets table (points 20,
        // 32, 33), not a separate dialog-based creation panel.
        Assert.Contains("id=\"create-label\"", presetList, StringComparison.Ordinal);
        Assert.Contains("id=\"create-amount\"", presetList, StringComparison.Ordinal);
        Assert.Contains(">Add<", presetList, StringComparison.Ordinal);
        Assert.DoesNotContain("data-dialog=\"preset-", body, StringComparison.Ordinal);

        // No dialog is defined inside the table, and removal opens none.
        Assert.DoesNotContain("data-dialog=\"", presetList, StringComparison.Ordinal);
        Assert.DoesNotContain("data-dialog=\"remove-preset-", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Remove acts on the click: the row form posts with no reason and the
    /// removal history row records none.
    /// </summary>
    [Fact]
    public async Task RemovePostsFromTheRowWithoutAReason()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);

        var page = await GetPageAsync(client);
        var form = RemoveForm(page, TowBarPresetId);
        using var response = await client.PostAsync(
            $"{Page}?handler=Remove",
            new FormUrlEncodedContent(form));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(
            1,
            await factory.Database.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM ActionHistory WHERE CorrelationId = '{form["operationKey"]}' AND Reason IS NULL;"));
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
        Assert.Contains("value=\"125.00\"", page, StringComparison.Ordinal);

        var edit = RowForm(page, TowBarPresetId, "Tow bar", "350.00", "true");
        using (var edited = await PostSaveAsync(client, edit))
        {
            Assert.Equal(HttpStatusCode.Found, edited.StatusCode);
        }

        page = await GetPageAsync(client);
        Assert.Contains("value=\"350.00\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain("value=\"300.00\"", page, StringComparison.Ordinal);

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

        using (var disabled = await PostSaveAsync(
            client,
            RowForm(
                page,
                TowBarPresetId,
                "Tow bar",
                "350.00",
                "false")))
        {
            Assert.Equal(HttpStatusCode.Found, disabled.StatusCode);
        }

        page = await GetPageAsync(client);
        Assert.DoesNotContain("checked=\"checked\"", ActiveInput(page, TowBarPresetId), StringComparison.Ordinal);
        Assert.Contains("Tow bar", page, StringComparison.Ordinal);
        using var enabled = await PostSaveAsync(
            client,
            RowForm(page, TowBarPresetId, "Tow bar", "350.00", "true"));
        Assert.Equal(HttpStatusCode.Found, enabled.StatusCode);
        page = await GetPageAsync(client);
        Assert.Contains("checked=\"checked\"", ActiveInput(page, TowBarPresetId), StringComparison.Ordinal);
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
        var page = await GetPageAsync(client);
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
        var page = await GetPageAsync(client);

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
        Assert.Contains("value=\"Tow bar\"", reloaded, StringComparison.Ordinal);
        Assert.Contains("value=\"300.00\"", reloaded, StringComparison.Ordinal);
    }

    /// <summary>
    /// The hidden add-row form's own fields, read from that form rather than
    /// from a page-wide search: every row's Remove form carries its own
    /// <c>presetId</c> input.
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
            $"id=\"preset-save-{presetId:N}\"",
            StringComparison.Ordinal);
        Assert.True(start >= 0);
        var row = page[start..page.IndexOf("</form>", start, StringComparison.Ordinal)];
        return new(StringComparer.Ordinal)
        {
            ["presetId"] = FirstValue(row, PresetIdRegex()),
            ["expectedVersion"] = FirstValue(row, ExpectedVersionRegex()),
            ["operationKey"] = FirstValue(row, OperationKeyRegex()),
            ["label"] = label,
            ["amount"] = amount,
            ["active"] = active,
            ["__RequestVerificationToken"] = Token(page)
        };
    }

    /// <summary>
    /// One preset's row Remove form: the form whose handler is Remove and
    /// whose hidden <c>presetId</c> names that preset.
    /// </summary>
    private static Dictionary<string, string> RemoveForm(string page, Guid presetId)
    {
        var marker = $"name=\"presetId\" value=\"{presetId:D}\"";
        var index = 0;
        while (true)
        {
            var start = page.IndexOf("?handler=Remove", index, StringComparison.Ordinal);
            Assert.True(start >= 0, "Missing row Remove form.");
            var end = page.IndexOf("</form>", start, StringComparison.Ordinal);
            var form = page[start..end];
            if (form.Contains(marker, StringComparison.Ordinal))
            {
                return new(StringComparer.Ordinal)
                {
                    ["presetId"] = presetId.ToString("D"),
                    ["expectedVersion"] = FirstValue(form, ExpectedVersionRegex()),
                    ["operationKey"] = FirstValue(form, OperationKeyRegex()),
                    ["__RequestVerificationToken"] = Token(page)
                };
            }
            index = end;
        }
    }

    private static Task<HttpResponseMessage> PostSaveAsync(
        HttpClient client,
        IReadOnlyDictionary<string, string> form) =>
        client.PostAsync($"{Page}?handler=Save", new FormUrlEncodedContent(form));

    private static string ActiveInput(string page, Guid presetId)
    {
        var match = Regex.Match(
            page,
            "<input name=\"active\" type=\"checkbox\" value=\"true\" form=\"preset-save-"
                + presetId.ToString("N") + "\"[^>]*>");
        Assert.True(match.Success, "The preset row must render its active-state checkbox.");
        return match.Value;
    }

    private static async Task<string> GetPageAsync(HttpClient client)
    {
        using var response = await client.GetAsync(Page);
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
    [GeneratedRegex("<input[^>]*name=\"operationKey\"[^>]*>", RegexOptions.IgnoreCase)] private static partial Regex OperationKeyRegex();
    [GeneratedRegex("<input[^>]*name=\"__RequestVerificationToken\"[^>]*>", RegexOptions.IgnoreCase)] private static partial Regex AntiforgeryRegex();
    [GeneratedRegex("value=\"(?<value>[^\"]*)\"", RegexOptions.IgnoreCase)] private static partial Regex ValueRegex();
    [GeneratedRegex("<section[^>]*aria-labelledby=\"valuation-presets-title\"[^>]*>[\\s\\S]*?</section>", RegexOptions.IgnoreCase)] private static partial Regex PresetListSectionRegex();
}
