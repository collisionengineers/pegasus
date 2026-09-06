using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;
using Pegasus.Web.Pages.Administration;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class AdministrationSearchAccountWebTests
{
    [Fact]
    public async Task CanonicalAdministrationSearchAndPasswordRoutesRenderRealCallers()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        foreach (var route in new[]
                 {
                     "/Administration/Configuration",
                     "/Administration/Mailboxes",
                     "/Account/PasswordChange"
                 })
        {
            using var response = await client.GetAsync(route);
            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync();
            Assert.Contains("<main", html, StringComparison.OrdinalIgnoreCase);
        }

        using var administration = await client.GetAsync("/Administration");
        var administrationHtml = await administration.Content.ReadAsStringAsync();
        Assert.Contains("/Administration/Configuration", administrationHtml, StringComparison.Ordinal);
        Assert.Contains("/Administration/Mailboxes", administrationHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("/Administration/MailCategories", administrationHtml, StringComparison.Ordinal);

        // PLAT-026 consolidated Mail categories into the Mail settings area, so the
        // old route is a permanent redirect rather than a rendered page.
        using var retiredCategories = await client.GetAsync("/Administration/MailCategories");
        Assert.Equal(HttpStatusCode.MovedPermanently, retiredCategories.StatusCode);
        Assert.Equal(
            "/Administration/Mailboxes",
            retiredCategories.Headers.Location?.OriginalString ?? string.Empty);

        using var shell = await client.GetAsync("/");
        var shellHtml = await shell.Content.ReadAsStringAsync();

        Assert.Contains("href=\"/Account/PasswordChange\"", shellHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdministrationAndPasswordFormsRenderAntiforgeryTokens()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        foreach (var route in new[]
                 {
                     "/Administration/Configuration",
                     "/Administration/Mailboxes",
                     "/Account/PasswordChange"
                 })
        {
            using var response = await client.GetAsync(route);
            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync();
            Assert.Contains("name=\"__RequestVerificationToken\"", html, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task AdministrationRoutesDenyARequestWithoutCurrentAdministratorRole()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: false);
        _ = factory.Services;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<PegasusIdentityUser>>();
            var user = await userManager.FindByIdAsync(
                DevelopmentOfflineIdentity.AdministratorId.ToString("D"));
            Assert.NotNull(user);
            Assert.True(await userManager.RemoveFromRoleAsync(
                user,
                StaffRoleNames.Administrator) is { Succeeded: true });
        }

        using var client = IntakeWebDriver.CreateClient(factory);
        foreach (var route in new[]
                 {
                     "/Administration/Configuration",
                     "/Administration/Mailboxes",
                     "/Administration/MailCategories"
                 })
        {
            using var response = await client.GetAsync(route);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public void AdministrationPageModelsDeclareAdministratorPolicy()
    {
        Assert.Equal(
            StaffRoleNames.Administrator,
            typeof(ConfigurationModel).GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.Equal(
            StaffRoleNames.Administrator,
            typeof(MailboxesModel).GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.Equal(
            StaffRoleNames.Administrator,
            typeof(MailCategoriesModel).GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.Equal(StaffRoleNames.Administrator, typeof(HealthModel).GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.Equal(StaffRoleNames.Administrator, typeof(ActionLogsModel).GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.Equal(StaffRoleNames.Administrator, typeof(AiJobsModel).GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.Equal(StaffRoleNames.Administrator, typeof(ReportsModel).GetCustomAttribute<AuthorizeAttribute>()?.Policy);
    }

    [Theory]
    [InlineData("/Administration/Health")]
    [InlineData("/Administration/ActionLogs")]
    [InlineData("/Administration/AiJobs")]
    [InlineData("/Administration/Reports")]
    public async Task NewAdministrationRoutesForbidNonAdministrators(string route)
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = IntakeWebDriver.CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-Test-Roles", "User");

        using var response = await client.GetAsync(route);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AiJobsShowsUnavailableWhenTheHostDidNotComposeAHandOffTransport()
    {
        // The default test composition carries the persistent switch but no
        // DevelopmentOffline Send-to-AI transport, matching a production host
        // where the preview capability is absent.
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        var html = await client.GetStringAsync("/Administration/AiJobs");

        Assert.Contains("Unavailable", html, StringComparison.Ordinal);
        Assert.DoesNotContain("· Active</span>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ActionLogPagerTraversesOneHundredAndOneFilteredRows()
    {
        const string actor = "pager-actor";
        var now = DateTimeOffset.UtcNow;
        using var factory = new IntakeWebApplicationFactory();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            context.ActionHistory.AddRange(Enumerable.Range(0, 101).Select(index => new ActionHistoryEntity
            {
                Id = Guid.NewGuid(),
                AggregateType = "Case",
                AggregateId = $"case-{index:000}",
                EventKind = $"action-{index:000}",
                ActorKind = "Staff",
                ActorSubjectId = actor,
                ActorRolesJson = "[]",
                OccurredAtUtc = now.AddMinutes(-index),
                Outcome = "Succeeded",
                CorrelationId = $"pager-{index:000}"
            }));
            await context.SaveChangesAsync();
        }

        using var client = IntakeWebDriver.CreateClient(factory);
        var from = Uri.EscapeDataString(now.AddDays(-1).ToString("O"));
        var to = Uri.EscapeDataString(now.AddDays(1).ToString("O"));
        var first = await client.GetStringAsync($"/Administration/ActionLogs?From={from}&To={to}&Actor={actor}");
        Assert.Contains("page=2", first, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Actor=pager-actor", first, StringComparison.OrdinalIgnoreCase);

        var second = await client.GetStringAsync($"/Administration/ActionLogs?From={from}&To={to}&Actor={actor}&page=2");
        Assert.Contains("page=3", second, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Actor=pager-actor", second, StringComparison.OrdinalIgnoreCase);

        var third = await client.GetStringAsync($"/Administration/ActionLogs?From={from}&To={to}&Actor={actor}&page=3");
        Assert.Contains("action-100", third, StringComparison.Ordinal);
        Assert.DoesNotContain("page=4", third, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ActionLogsUnifiesSecurityEventsAndFiltersTheDisplayedReference()
    {
        var now = DateTimeOffset.UtcNow;
        using var factory = new IntakeWebApplicationFactory();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            context.ActionHistory.Add(new ActionHistoryEntity
            {
                Id = Guid.NewGuid(),
                AggregateType = "Case",
                AggregateId = "case-reference",
                EventKind = "case_saved",
                ActorKind = "Staff",
                ActorSubjectId = "action-actor",
                ActorRolesJson = "[]",
                OccurredAtUtc = now.AddMinutes(-1),
                Outcome = "Succeeded",
                CorrelationId = "action-correlation"
            });
            context.SecurityEvents.Add(new SecurityEventEntity
            {
                Id = Guid.NewGuid(),
                Type = "SignInFailed",
                SubjectId = "security-subject",
                OccurredAtUtc = now,
                Outcome = "Denied",
                CorrelationId = "security-correlation",
                ReasonCode = "invalid_credentials"
            });
            await context.SaveChangesAsync();
        }

        using var client = IntakeWebDriver.CreateClient(factory);
        var from = Uri.EscapeDataString(now.AddDays(-1).ToString("O"));
        var to = Uri.EscapeDataString(now.AddDays(1).ToString("O"));

        var security = await client.GetStringAsync(
            $"/Administration/ActionLogs?From={from}&To={to}&Area=Security&Record=security-subject");
        Assert.Contains("SignInFailed", security, StringComparison.Ordinal);
        Assert.Contains("security-subject", security, StringComparison.Ordinal);
        Assert.DoesNotContain("case-reference", security, StringComparison.Ordinal);

        var action = await client.GetStringAsync(
            $"/Administration/ActionLogs?From={from}&To={to}&Record=case-reference");
        Assert.Contains("case_saved", action, StringComparison.Ordinal);
        Assert.Contains("case-reference", action, StringComparison.Ordinal);
        Assert.DoesNotContain("security-subject", action, StringComparison.Ordinal);

        var oldest = await client.GetStringAsync(
            $"/Administration/ActionLogs?From={from}&To={to}&Sort=oldest");
        Assert.True(
            oldest.IndexOf("case-reference", StringComparison.Ordinal)
            < oldest.IndexOf("security-subject", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ActionLogsPagesACombinedSqlResultSet()
    {
        const string actor = "combined-page-actor";
        var now = DateTimeOffset.UtcNow;
        using var factory = new IntakeWebApplicationFactory();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            for (var index = 0; index < 51; index++)
            {
                var occurredAt = now.AddMinutes(-index);
                context.ActionHistory.Add(new ActionHistoryEntity
                {
                    Id = Guid.NewGuid(),
                    AggregateType = "Case",
                    AggregateId = $"combined-action-{index:000}",
                    EventKind = "case_saved",
                    ActorKind = "Staff",
                    ActorSubjectId = actor,
                    ActorRolesJson = "[]",
                    OccurredAtUtc = occurredAt,
                    Outcome = "Succeeded",
                    CorrelationId = $"combined-action-{index:000}"
                });
                context.SecurityEvents.Add(new SecurityEventEntity
                {
                    Id = Guid.NewGuid(),
                    Type = $"SignInFailed{index:000}",
                    SubjectId = actor,
                    OccurredAtUtc = occurredAt.AddSeconds(-30),
                    Outcome = "Denied",
                    CorrelationId = $"combined-security-{index:000}",
                    ReasonCode = "invalid_credentials"
                });
            }
            await context.SaveChangesAsync();
        }

        using var client = IntakeWebDriver.CreateClient(factory);
        var from = Uri.EscapeDataString(now.AddDays(-1).ToString("O"));
        var to = Uri.EscapeDataString(now.AddDays(1).ToString("O"));
        var page = await client.GetStringAsync(
            $"/Administration/ActionLogs?From={from}&To={to}&Actor={actor}&page=3");

        Assert.Contains("combined-action-050", page, StringComparison.Ordinal);
        Assert.Contains("SignInFailed050", page, StringComparison.Ordinal);
        Assert.DoesNotContain("page=4", page, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ActionLogDefaultPagerKeepsItsInitialTimeWindow()
    {
        const string actor = "default-window-actor";
        using var factory = new IntakeWebApplicationFactory();
        var now = factory.Services.GetRequiredService<TimeProvider>().GetUtcNow();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            context.ActionHistory.AddRange(Enumerable.Range(0, 101).Select(index => new ActionHistoryEntity
            {
                Id = Guid.NewGuid(),
                AggregateType = "Case",
                AggregateId = $"default-window-{index:000}",
                EventKind = "case_saved",
                ActorKind = "Staff",
                ActorSubjectId = actor,
                ActorRolesJson = "[]",
                OccurredAtUtc = now.AddMinutes(-index - 1),
                Outcome = "Succeeded",
                CorrelationId = $"default-window-{index:000}"
            }));
            await context.SaveChangesAsync();
        }

        using var client = IntakeWebDriver.CreateClient(factory);
        var first = await client.GetStringAsync($"/Administration/ActionLogs?Actor={actor}");
        var next = Regex.Match(first, "href=\"([^\"]*page=2[^\"]*)\"").Groups[1].Value
            .Replace("&amp;", "&", StringComparison.Ordinal);
        Assert.NotEmpty(next);
        Assert.Matches("(?:[?&]From=)[^&\"]+", next);
        Assert.Matches("(?:[?&]To=)[^&\"]+", next);
        Assert.Contains("default-window-000", first, StringComparison.Ordinal);
        Assert.DoesNotContain("default-window-050", first, StringComparison.Ordinal);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            context.ActionHistory.Add(new ActionHistoryEntity
            {
                Id = Guid.NewGuid(),
                AggregateType = "Case",
                AggregateId = "default-window-newer",
                EventKind = "case_saved",
                ActorKind = "Staff",
                ActorSubjectId = actor,
                ActorRolesJson = "[]",
                OccurredAtUtc = now.AddMinutes(1),
                Outcome = "Succeeded",
                CorrelationId = "default-window-newer"
            });
            await context.SaveChangesAsync();
        }

        var second = await client.GetStringAsync(next);
        Assert.Contains("default-window-050", second, StringComparison.Ordinal);
        Assert.DoesNotContain("default-window-000", second, StringComparison.Ordinal);
        Assert.DoesNotContain("default-window-newer", second, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OldCasesSearchLinksRedirectToSearchWithTheirValuesIntact()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        // EPIC-011 moved the case search to /Search and the workflow tabs to
        // /Cases. A /Cases link that carries a search-only parameter is an
        // old search bookmark and lands on its results, values intact.
        const string keyword = "QDOS-search-no-match";
        using var oldLink = await client.GetAsync($"/Cases?query={keyword}");
        Assert.Equal(HttpStatusCode.MovedPermanently, oldLink.StatusCode);
        Assert.Equal(
            $"/Search?query={keyword}",
            oldLink.Headers.Location?.OriginalString ?? string.Empty);

        using var search = await client.GetAsync($"/Search?query={keyword}");
        search.EnsureSuccessStatusCode();
        var html = await search.Content.ReadAsStringAsync();
        Assert.Contains("No cases match these filters.", html, StringComparison.Ordinal);

        // A real bookmark carries a whole filter set, including the two
        // parameters the ported grid no longer draws. Every value survives the
        // move byte for byte, in its original order, and the page it lands on
        // accepts all of them.
        const string wholeFilterSet =
            "?case=QDOS3100042&registration=AB12CDE&claimant=Claimant&claimNumber=CLM42"
            + "&principal=QDOS&state=Review&receivedDate=2031-05-01"
            + "&instructionDate=2031-05-02&fromDate=2031-04-01&toDate=2031-05-31"
            + "&origin=Email&query=" + keyword + "&page=2";
        using var wholeBookmark = await client.GetAsync("/Cases" + wholeFilterSet);
        Assert.Equal(HttpStatusCode.MovedPermanently, wholeBookmark.StatusCode);
        Assert.Equal(
            "/Search" + wholeFilterSet,
            wholeBookmark.Headers.Location?.OriginalString ?? string.Empty);

        using var landed = await client.GetAsync("/Search" + wholeFilterSet);
        landed.EnsureSuccessStatusCode();
        var landedHtml = await landed.Content.ReadAsStringAsync();
        foreach (var (field, value) in new[]
                 {
                     ("search-query", keyword), ("search-registration", "AB12CDE"),
                     ("search-claimant", "Claimant"), ("search-claim-number", "CLM42"),
                     ("search-principal", "QDOS"), ("search-from-date", "2031-04-01"),
                     ("search-to-date", "2031-05-31"), ("search-origin", "Email")
                 })
        {
            Assert.Matches(
                $"id=\"{field}\"[^>]*value=\"{Regex.Escape(value)}\"",
                landedHtml);
        }
    }

    [Fact]
    public async Task ObsoleteChangePasswordRouteIsAbsent()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        using var response = await client.GetAsync("/Account/ChangePassword");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
