using System.Net;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Pegasus.Web.Presentation;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Administration › Unroadworthy reasons (operator, 11 November 2026; R952 to
/// R954), driven through the real page, the real Core use cases and the real
/// EF store over the seeded LocalDB.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed partial class UnroadworthyReasonAdministrationWebTests
{
    private const string Page = "/Administration/UnroadworthyReasons";
    private const string FluidsWording = "as there is a loss of essential fluids";

    private static readonly Guid FluidsWordingId =
        Guid.Parse("00000000-0000-4000-8000-0000000b0007");

    [Theory]
    [InlineData("User")]
    [InlineData("Engineer")]
    public async Task NonAdministratorCanNeitherOpenNorChangeTheBank(string role)
    {
        using var factory = new IntakeWebApplicationFactory(
            useIntegrationTestAuthentication: true);
        using var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);

        using var opened = await client.GetAsync(Page);
        using var posted = await client.PostAsync(
            $"{Page}?handler=Remove",
            new FormUrlEncodedContent(new Dictionary<string, string>()));

        Assert.Equal(HttpStatusCode.Forbidden, opened.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, posted.StatusCode);
    }

    /// <summary>
    /// The Valuation presets table: every wording editable at once, Save and
    /// Remove in each row, one inline add row, no dialog. The hub and the side
    /// navigation both list the area.
    /// </summary>
    [Fact]
    public async Task TheBankRendersTheStandardWordingsAsEditableRows()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);

        var body = await GetPageAsync(client);
        var match = WordingListSectionRegex().Match(body);
        Assert.True(match.Success, "The wording list section must render.");
        var list = match.Value;

        Assert.Contains(">7 wordings<", list, StringComparison.Ordinal);
        Assert.Contains(
            "value=\"as the steering and suspension geometry has been compromised\"", list, StringComparison.Ordinal);
        Assert.Contains($"value=\"{FluidsWording}\"", list, StringComparison.Ordinal);
        Assert.Contains($"wording-save-{FluidsWordingId:N}", list, StringComparison.Ordinal);
        Assert.Contains("maxlength=\"300\"", list, StringComparison.Ordinal);
        Assert.Contains(">Save<", list, StringComparison.Ordinal);
        Assert.Contains(">Remove<", list, StringComparison.Ordinal);
        Assert.Contains("id=\"create-text\"", list, StringComparison.Ordinal);
        Assert.Contains(">Add<", list, StringComparison.Ordinal);
        Assert.DoesNotContain("data-dialog=\"", list, StringComparison.Ordinal);
        Assert.Matches(
            "<a(?=[^>]*href=\"/Administration/UnroadworthyReasons\")(?=[^>]*aria-current=\"page\")", body);

        using var hub = await client.GetAsync("/Administration");
        hub.EnsureSuccessStatusCode();
        Assert.Contains(
            "href=\"/Administration/UnroadworthyReasons\"",
            await hub.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdministratorAddsRewordsAndRemovesAWording()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);
        var page = await GetPageAsync(client);

        using (var added = await PostAsync(client, "Create", page, new() { ["text"] = "The brake line is severed." }))
        {
            Assert.Equal(HttpStatusCode.Found, added.StatusCode);
        }
        page = await GetPageAsync(client);
        Assert.Contains("value=\"the brake line is severed\"", page, StringComparison.Ordinal);
        Assert.Contains(">8 wordings<", page, StringComparison.Ordinal);

        // A wording the bank already offers is refused and the typed text stays.
        using (var duplicate = await PostAsync(client, "Create", page, new() { ["text"] = FluidsWording }))
        {
            Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
            var refused = await duplicate.Content.ReadAsStringAsync();
            Assert.Contains(Encoded(OperatorLabels.UnroadworthyReasons.AlreadyOffered), refused, StringComparison.Ordinal);
            Assert.Contains(">8 wordings<", refused, StringComparison.Ordinal);
        }

        var reword = new Dictionary<string, string>
        {
            ["wordingId"] = FluidsWordingId.ToString("D"),
            ["renderedText"] = FluidsWording,
            ["text"] = "As the engine has lost its coolant."
        };
        using (var reworded = await PostAsync(client, "Save", page, reword))
        {
            Assert.Equal(HttpStatusCode.Found, reworded.StatusCode);
        }
        page = await GetPageAsync(client);
        Assert.Contains("value=\"as the engine has lost its coolant\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain(FluidsWording, page, StringComparison.Ordinal);

        // The wording the first page rendered is stale now: nothing changes
        // and the page shows the current wording, not the typed one.
        reword["text"] = "as the sump is cracked";
        using (var stale = await PostAsync(client, "Save", page, reword))
        {
            Assert.Equal(HttpStatusCode.OK, stale.StatusCode);
            var refused = await stale.Content.ReadAsStringAsync();
            Assert.Contains(Encoded(OperatorLabels.UnroadworthyReasons.Stale), refused, StringComparison.Ordinal);
            Assert.Contains("value=\"as the engine has lost its coolant\"", refused, StringComparison.Ordinal);
            Assert.DoesNotContain("as the sump is cracked", refused, StringComparison.Ordinal);
        }

        // An empty wording is refused before the store.
        using (var empty = await PostAsync(client, "Save", page, new()
        {
            ["wordingId"] = FluidsWordingId.ToString("D"),
            ["renderedText"] = "as the engine has lost its coolant",
            ["text"] = " . "
        }))
        {
            Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
            Assert.Contains(
                Encoded(OperatorLabels.UnroadworthyReasons.WordingRequired),
                await empty.Content.ReadAsStringAsync(),
                StringComparison.Ordinal);
        }

        using (var removed = await PostAsync(client, "Remove", page, new()
        {
            ["wordingId"] = FluidsWordingId.ToString("D"),
            ["renderedText"] = "as the engine has lost its coolant"
        }))
        {
            Assert.Equal(HttpStatusCode.Found, removed.StatusCode);
        }
        page = await GetPageAsync(client);
        Assert.DoesNotContain("as the engine has lost its coolant", page, StringComparison.Ordinal);
        Assert.Contains(">7 wordings<", page, StringComparison.Ordinal);
        Assert.Equal(
            7,
            await (await factory.DatabaseAsync).ScalarAsync<int>("SELECT COUNT(*) FROM UnroadworthyReasons;"));
    }

    private static Task<HttpResponseMessage> PostAsync(
        HttpClient client, string handler, string page, Dictionary<string, string> form)
    {
        form["__RequestVerificationToken"] = Value(AntiforgeryRegex().Match(page).Value);
        return client.PostAsync($"{Page}?handler={handler}", new FormUrlEncodedContent(form));
    }

    private static async Task<string> GetPageAsync(HttpClient client)
    {
        using var response = await client.GetAsync(Page);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private static HttpClient CreateClient(IntakeWebApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });

    private static string Encoded(string text) => HtmlEncoder.Default.Encode(text);

    private static string Value(string tag)
    {
        var match = ValueRegex().Match(tag);
        Assert.True(match.Success);
        return match.Groups["value"].Value;
    }

    [GeneratedRegex("<input[^>]*name=\"__RequestVerificationToken\"[^>]*>", RegexOptions.IgnoreCase)] private static partial Regex AntiforgeryRegex();
    [GeneratedRegex("value=\"(?<value>[^\"]*)\"", RegexOptions.IgnoreCase)] private static partial Regex ValueRegex();
    [GeneratedRegex("<section[^>]*aria-labelledby=\"unroadworthy-reasons-title\"[^>]*>[\\s\\S]*?</section>", RegexOptions.IgnoreCase)] private static partial Regex WordingListSectionRegex();
}
