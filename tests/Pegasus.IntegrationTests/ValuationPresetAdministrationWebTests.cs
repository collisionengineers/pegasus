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
    /// Labels, values and controls, and nothing that explains them.
    /// </summary>
    [Fact]
    public async Task ThePresetListShowsLabelsAmountsStatesAndVersionsWithoutExplanatoryCopy()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);

        var body = await GetPageAsync(client);

        // Razor's default encoder writes the pound sign as a numeric entity,
        // so the printed £#,##0.00 arrives as &#xA3; followed by the figure.
        Assert.Contains("Tow bar", body, StringComparison.Ordinal);
        Assert.Contains("&#xA3;300.00", body, StringComparison.Ordinal);
        Assert.Contains("&#xA3;1,500.00", body, StringComparison.Ordinal);
        Assert.Contains("&#xA3;0.00", body, StringComparison.Ordinal);
        Assert.Contains(">Enabled<", body, StringComparison.Ordinal);
        Assert.Contains("Create preset", body, StringComparison.Ordinal);
        Assert.DoesNotContain("<p>", body, StringComparison.Ordinal);
        Assert.DoesNotContain("<aside", body, StringComparison.Ordinal);
        Assert.DoesNotContain("empty-state", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdministratorCreatesEditsAndDisablesAPreset()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);
        var page = await GetPageAsync(client);

        using (var created = await client.PostAsync(
            $"{Page}?handler=Create",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["presetId"] = LastValue(page, PresetIdRegex()),
                ["operationKey"] = LastValue(page, OperationKeyRegex()),
                ["label"] = "Roof rack",
                ["amount"] = "125.00",
                ["reason"] = "Added the roof rack allowance.",
                ["__RequestVerificationToken"] = Token(page)
            })))
        {
            Assert.Equal(HttpStatusCode.Found, created.StatusCode);
        }

        page = await GetPageAsync(client);
        Assert.Contains("Roof rack", page, StringComparison.Ordinal);
        Assert.Contains("&#xA3;125.00", page, StringComparison.Ordinal);

        var edit = RowForm(page, TowBarPresetId, "Tow bar", "350.00", "true", "The allowance rose.");
        using (var edited = await PostSaveAsync(client, edit))
        {
            Assert.Equal(HttpStatusCode.Found, edited.StatusCode);
        }

        page = await GetPageAsync(client);
        Assert.Contains("&#xA3;350.00", page, StringComparison.Ordinal);
        Assert.DoesNotContain("&#xA3;300.00", page, StringComparison.Ordinal);

        // The version the first post consumed is stale on a second, freshly
        // keyed post, and the page says so rather than writing a second edit.
        var stale = new Dictionary<string, string>(edit, StringComparer.Ordinal)
        {
            ["operationKey"] = Guid.NewGuid().ToString("N")
        };
        using (var refused = await PostSaveAsync(client, stale))
        {
            Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
            Assert.Contains(
                "The preset changed after this page was loaded.",
                await refused.Content.ReadAsStringAsync(),
                StringComparison.Ordinal);
        }

        using (var disabled = await PostSaveAsync(
            client,
            RowForm(
                page,
                TowBarPresetId,
                "Tow bar",
                "350.00",
                "false",
                "The allowance is withdrawn.")))
        {
            Assert.Equal(HttpStatusCode.Found, disabled.StatusCode);
        }

        page = await GetPageAsync(client);
        Assert.Contains(">Disabled<", page, StringComparison.Ordinal);
        Assert.Contains(">Enable<", page, StringComparison.Ordinal);
        Assert.Contains("Tow bar", page, StringComparison.Ordinal);
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
                ["amount"] = "125.00",
                ["reason"] = "   ",
                ["__RequestVerificationToken"] = Token(page)
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Enter a reason.", body, StringComparison.Ordinal);
        Assert.Contains("value=\"Roof rack\"", body, StringComparison.Ordinal);
        Assert.NotEqual(operationKey, LastValue(body, OperationKeyRegex()));
        Assert.DoesNotContain("Roof rack</td>", body, StringComparison.Ordinal);
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
        string active,
        string reason)
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
            ["operationKey"] = FirstValue(row, OperationKeyRegex()),
            ["label"] = label,
            ["amount"] = amount,
            ["active"] = active,
            ["reason"] = reason,
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
}
