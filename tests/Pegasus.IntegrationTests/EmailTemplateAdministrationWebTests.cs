using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Operations;
using Pegasus.Web.Presentation;

using static Pegasus.IntegrationTests.CaseWebTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Administration › E-mail templates (plan 02) over real HTTP against the real
/// database: Administrator-only, reached from the nav and the hub, the
/// built-in body at version 0, a save at version 1 with its action-log row, a
/// stale save refused and an unknown placeholder refused by name.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class EmailTemplateAdministrationWebTests
{
    private const string Page = "/Administration/EmailTemplates";
    private const EmailTemplatePurpose Purpose = EmailTemplatePurpose.TriageOutcomeReply;

    [Theory]
    [InlineData("Engineer")]
    [InlineData("User")]
    public async Task TheAreaIsForbiddenToAnyoneButAnAdministrator(string role)
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);

        using var response = await client.GetAsync(Page);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TheNavEntryAndHubCardFollowMailSettings()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = CreateClient(factory);

        var hub = await GetHtmlAsync(client, "/Administration");

        var nav = hub[hub.IndexOf("class=\"admin-nav panel\"", StringComparison.Ordinal)..];
        var mailEntry = nav.IndexOf("href=\"/Administration/Mailboxes\"", StringComparison.Ordinal);
        var templatesEntry = nav.IndexOf($"href=\"{Page}\"", StringComparison.Ordinal);
        Assert.True(mailEntry >= 0 && templatesEntry > mailEntry, "The nav entry must follow Mail settings.");
        Assert.Contains("#icon-mail-open", nav[templatesEntry..], StringComparison.Ordinal);

        var cards = hub[hub.IndexOf("admin-configuration-title", StringComparison.Ordinal)..];
        var mailCard = cards.IndexOf("class=\"admin-card\" href=\"/Administration/Mailboxes\"", StringComparison.Ordinal);
        var templatesCard = cards.IndexOf($"class=\"admin-card\" href=\"{Page}\"", StringComparison.Ordinal);
        Assert.True(mailCard >= 0 && templatesCard > mailCard, "The hub card must follow Mail settings.");
        Assert.Contains(OperatorLabels.EmailTemplates.Area, cards[templatesCard..], StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThePageShowsTheBuiltInBodyAtVersionZero()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = CreateClient(factory);

        var html = await GetHtmlAsync(client, Page);

        Assert.Contains(OperatorLabels.EmailTemplates.Name(Purpose), html, StringComparison.Ordinal);
        Assert.Contains("value=\"0\"", Dialog(html), StringComparison.Ordinal);
        Assert.Contains(
            EmailTemplates.DefaultBody(Purpose),
            WebUtility.HtmlDecode(Dialog(html)).Replace("\r\n", "\n", StringComparison.Ordinal),
            StringComparison.Ordinal);
        foreach (var placeholder in EmailTemplates.Placeholders(Purpose))
        {
            Assert.Contains($"data-insert-placeholder=\"{{{placeholder}}}\"", Dialog(html), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task SaveInsertsAtVersionOneAndEntersTheActionLog()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = CreateClient(factory);
        var html = await GetHtmlAsync(client, Page);

        using var saved = await PostSaveAsync(client, html, 0, "Result for {registration}\nRoadworthiness: {roadworthiness}");

        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        var template = await GetTemplateAsync(factory);
        Assert.Equal(1, template.Version);
        Assert.Equal("Result for {registration}\nRoadworthiness: {roadworthiness}", template.Body);

        var reloaded = await GetHtmlAsync(client, Page);
        Assert.Contains(OperatorLabels.EmailTemplates.Saved, reloaded, StringComparison.Ordinal);
        Assert.Contains("value=\"1\"", Dialog(reloaded), StringComparison.Ordinal);
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM [ActionHistory] WHERE [AggregateType] = 'email_template' AND [AggregateId] = 'TriageOutcomeReply' AND [EventKind] = 'email_template_updated'"));

        // The period's end is exclusive and the test clock is fixed, so the
        // period runs past the moment of the save.
        var savedAt = template.UpdatedAtUtc!.Value;
        var logs = await GetHtmlAsync(
            client,
            $"/Administration/Logs?Area=email_template&From={Uri.EscapeDataString(savedAt.AddDays(-1).ToString("O"))}&To={Uri.EscapeDataString(savedAt.AddDays(1).ToString("O"))}");
        Assert.Contains(OperatorLabels.EmailTemplates.ActionLogArea, logs, StringComparison.Ordinal);
        Assert.Contains(OperatorLabels.EmailTemplates.Name(Purpose), logs, StringComparison.Ordinal);
    }

    /// <summary>
    /// The Case report delivery template is one more row of the same area: its
    /// own built-in body and placeholders, saved at version 1, and a Triage
    /// placeholder refused by name.
    /// </summary>
    [Fact]
    public async Task TheReportDeliveryTemplateIsARowWithItsOwnBodyAndPlaceholders()
    {
        const EmailTemplatePurpose delivery = EmailTemplatePurpose.CaseReportDelivery;
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = CreateClient(factory);
        var html = await GetHtmlAsync(client, Page);

        Assert.Contains($"data-email-template=\"{delivery}\"", html, StringComparison.Ordinal);
        Assert.Contains(OperatorLabels.EmailTemplates.Name(delivery), html, StringComparison.Ordinal);
        var dialog = Dialog(html, delivery);
        Assert.Contains(
            EmailTemplates.DefaultBody(delivery),
            WebUtility.HtmlDecode(dialog).Replace("\r\n", "\n", StringComparison.Ordinal),
            StringComparison.Ordinal);
        foreach (var placeholder in EmailTemplates.Placeholders(delivery))
        {
            Assert.Contains($"data-insert-placeholder=\"{{{placeholder}}}\"", dialog, StringComparison.Ordinal);
        }
        Assert.DoesNotContain("data-insert-placeholder=\"{roadworthiness}\"", dialog, StringComparison.Ordinal);

        using var saved = await PostSaveAsync(
            client, html, 0, "Report {case reference} for {registration}", delivery);

        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        var template = await GetTemplateAsync(factory, delivery);
        Assert.Equal(1, template.Version);
        Assert.Equal("Report {case reference} for {registration}", template.Body);
        Assert.Equal(0, (await GetTemplateAsync(factory)).Version);

        using var refused = await PostSaveAsync(client, html, 1, "Roadworthiness: {roadworthiness}", delivery);

        Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
        Assert.Contains(
            WebUtility.HtmlEncode(OperatorLabels.EmailTemplates.UnknownPlaceholder("roadworthiness")),
            await refused.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task AStaleSaveIsRefusedAndAsksToReload()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = CreateClient(factory);
        var html = await GetHtmlAsync(client, Page);
        using (var first = await PostSaveAsync(client, html, 0, "First {registration}"))
        {
            Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        }

        using var stale = await PostSaveAsync(client, html, 0, "Second {registration}");

        Assert.Equal(HttpStatusCode.OK, stale.StatusCode);
        var refused = await stale.Content.ReadAsStringAsync();
        Assert.Contains(OperatorLabels.EmailTemplates.Stale, refused, StringComparison.Ordinal);
        // The dialog reopens with what was posted.
        Assert.Contains("data-dialog-open-on-load=\"true\"", refused, StringComparison.Ordinal);
        Assert.Contains("Second {registration}", WebUtility.HtmlDecode(Dialog(refused)), StringComparison.Ordinal);
        var template = await GetTemplateAsync(factory);
        Assert.Equal(1, template.Version);
        Assert.Equal("First {registration}", template.Body);
    }

    [Fact]
    public async Task ABodyWithAnUnknownPlaceholderCannotBeSaved()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = CreateClient(factory);
        var html = await GetHtmlAsync(client, Page);

        using var response = await PostSaveAsync(client, html, 0, "Dear {claimant}, your {registration}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            WebUtility.HtmlEncode(OperatorLabels.EmailTemplates.UnknownPlaceholder("claimant")),
            await response.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
        Assert.Equal(0, (await GetTemplateAsync(factory)).Version);
        Assert.Equal(0, await factory.Database.ScalarAsync<int>("SELECT COUNT(*) FROM [EmailTemplates]"));
    }

    private static Task<HttpResponseMessage> PostSaveAsync(
        HttpClient client,
        string page,
        long expectedVersion,
        string body,
        EmailTemplatePurpose? purpose = null) =>
        client.PostAsync(
            $"{Page}?handler=Save",
            Form(
                AntiforgeryValue(page),
                ("purpose", (purpose ?? Purpose).ToString()),
                ("expectedVersion", expectedVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", Guid.NewGuid().ToString("N")),
                ("body", body)));

    private static async Task<EmailTemplate> GetTemplateAsync(
        IntakeWebApplicationFactory factory,
        EmailTemplatePurpose? purpose = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<GetEmailTemplate>().ExecuteAsync(
            Pegasus.Core.Identity.ActionActor.Staff(
                Pegasus.Web.Authentication.DevelopmentOfflineIdentity.AdministratorId,
                [Pegasus.Core.Identity.StaffRole.Administrator]),
            purpose ?? Purpose,
            CancellationToken.None);
    }

    /// <summary>The template's Edit dialog, from its backdrop to its end.</summary>
    private static string Dialog(string html, EmailTemplatePurpose? purpose = null)
    {
        var start = html.IndexOf($"data-dialog=\"email-template-{purpose ?? Purpose}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, "The template's dialog is not rendered.");
        var end = html.IndexOf("</section>", start, StringComparison.Ordinal);
        return html[start..end];
    }

    private static HttpClient CreateClient(IntakeWebApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });
}
