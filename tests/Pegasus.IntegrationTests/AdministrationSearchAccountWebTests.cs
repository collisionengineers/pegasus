using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Actors;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;
using Pegasus.Web.Pages.Administration;
using Pegasus.Web.Presentation;

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

    [Theory]
    [InlineData("", "NewPassword123", "NewPassword123", "Enter your current password.")]
    [InlineData("ShortCurrent", "Sh0rt!", "Sh0rt!", "The new password must be at least 8 characters.")]
    [InlineData("MismatchCurrent", "NewPassword123", "ConfirmPassword123", "The passwords do not match.")]
    [InlineData("WrongCurrent", "NewPassword123", "NewPassword123", "The current password is incorrect.")]
    public async Task PasswordChangeRefusalsShowErrorsWithoutReRenderingPasswords(
        string currentPassword,
        string newPassword,
        string confirmPassword,
        string expectedError)
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        using var page = await client.GetAsync("/Account/PasswordChange");
        var pageHtml = await page.Content.ReadAsStringAsync();
        page.EnsureSuccessStatusCode();

        using var response = await client.PostAsync(
            "/Account/PasswordChange",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = FormValue(pageHtml, "__RequestVerificationToken"),
                ["OperationKey"] = FormValue(pageHtml, "OperationKey"),
                ["CurrentPassword"] = currentPassword,
                ["NewPassword"] = newPassword,
                ["ConfirmPassword"] = confirmPassword
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains(expectedError, html, StringComparison.Ordinal);
        Assert.Equal(string.Empty, InputValueOrEmpty(html, "CurrentPassword"));
        Assert.Equal(string.Empty, InputValueOrEmpty(html, "NewPassword"));
        Assert.Equal(string.Empty, InputValueOrEmpty(html, "ConfirmPassword"));
        foreach (var password in new[] { currentPassword, newPassword, confirmPassword })
        {
            if (!string.IsNullOrEmpty(password))
            {
                Assert.DoesNotContain(password, html, StringComparison.Ordinal);
            }
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
    public async Task ActionLogsSelectPeopleByDisplayNameAndKeepAdministrationDiagnostics()
    {
        var now = DateTimeOffset.UtcNow;
        using var factory = new IntakeWebApplicationFactory();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            context.ActionHistory.AddRange(
                new ActionHistoryEntity
                {
                    Id = Guid.NewGuid(),
                    AggregateType = "Administration",
                    AggregateId = "selected-person",
                    EventKind = "selected_person_action",
                    ActorKind = "Staff",
                    ActorSubjectId = DevelopmentOfflineIdentity.AdministratorId.ToString("D"),
                    ActorRolesJson = "[]",
                    OccurredAtUtc = now,
                    Outcome = "Succeeded",
                    CorrelationId = "selected-person"
                },
                new ActionHistoryEntity
                {
                    Id = Guid.NewGuid(),
                    AggregateType = "Administration",
                    AggregateId = "other-person",
                    EventKind = "other_person_action",
                    ActorKind = "Staff",
                    ActorSubjectId = Guid.NewGuid().ToString("D"),
                    ActorRolesJson = "[]",
                    OccurredAtUtc = now,
                    Outcome = "Succeeded",
                    CorrelationId = "other-person"
                });
            await context.SaveChangesAsync();
        }

        using var client = IntakeWebDriver.CreateClient(factory);
        var from = Uri.EscapeDataString(now.AddDays(-1).ToString("O"));
        var to = Uri.EscapeDataString(now.AddDays(1).ToString("O"));
        var actor = DevelopmentOfflineIdentity.AdministratorId.ToString("D");

        var html = await client.GetStringAsync(
            $"/Administration/ActionLogs?From={from}&To={to}&Actor={actor}");

        Assert.Contains($"value=\"{actor}\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(DevelopmentOfflineIdentity.UserName, html, StringComparison.Ordinal);
        Assert.Contains("selected_person_action", html, StringComparison.Ordinal);
        Assert.DoesNotContain("other_person_action", html, StringComparison.Ordinal);
        Assert.Contains("Recorded counts and processing times", html, StringComparison.Ordinal);
        Assert.Contains("Mailbox failures", html, StringComparison.Ordinal);
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
        Assert.Contains("Page 1", first, StringComparison.Ordinal);
        Assert.Contains("page=2", first, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Actor=pager-actor", first, StringComparison.OrdinalIgnoreCase);

        var second = await client.GetStringAsync($"/Administration/ActionLogs?From={from}&To={to}&Actor={actor}&page=2");
        Assert.Contains("Page 2", second, StringComparison.Ordinal);
        Assert.Contains("page=1", second, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("page=3", second, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Actor=pager-actor", second, StringComparison.OrdinalIgnoreCase);

        var third = await client.GetStringAsync($"/Administration/ActionLogs?From={from}&To={to}&Actor={actor}&page=3");
        Assert.Contains("Page 3", third, StringComparison.Ordinal);
        Assert.Contains("page=2", third, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("action-100", third, StringComparison.Ordinal);
        Assert.DoesNotContain("page=4", third, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ActionLogPageValidationIsSeparateFromUtcPeriodValidation()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        var html = await client.GetStringAsync("/Administration/ActionLogs?page=0");

        Assert.Contains("Choose a valid page.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Choose a valid UTC period.", html, StringComparison.Ordinal);
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

    /// <summary>
    /// A security event names the account it was about, so before the acting
    /// principal was recorded every Security-area row read as an unknown user.
    /// The acting operator is now stored beside the subject, and the recorded
    /// kind is parsed case-insensitively because it is whatever a writer stored.
    /// </summary>
    [Fact]
    public async Task ActionLogsNameTheOperatorWhoActedOnASecurityEvent()
    {
        var now = DateTimeOffset.UtcNow;
        var targetAccount = Guid.NewGuid();
        using var factory = new IntakeWebApplicationFactory();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            context.SecurityEvents.Add(new SecurityEventEntity
            {
                Id = Guid.NewGuid(),
                Type = "SecurityStampChanged",
                SubjectId = targetAccount.ToString("D"),
                OccurredAtUtc = now,
                Outcome = "Succeeded",
                CorrelationId = "acting-operator-recorded",
                ReasonCode = "staff_account_disabled",
                ActorKind = "staff",
                ActorSubjectId = DevelopmentOfflineIdentity.AdministratorId.ToString("D")
            });
            await context.SaveChangesAsync();
        }

        using var client = IntakeWebDriver.CreateClient(factory);
        var from = Uri.EscapeDataString(now.AddDays(-1).ToString("O"));
        var to = Uri.EscapeDataString(now.AddDays(1).ToString("O"));

        var html = await client.GetStringAsync(
            $"/Administration/ActionLogs?From={from}&To={to}&Area=Security");
        var row = ActionLogRow(html, "SecurityStampChanged");

        Assert.Contains(DevelopmentOfflineIdentity.UserName, row, StringComparison.Ordinal);
        Assert.DoesNotContain("Unknown user", row, StringComparison.Ordinal);
        Assert.DoesNotContain(
            DevelopmentOfflineIdentity.AdministratorId.ToString("D"),
            row,
            StringComparison.OrdinalIgnoreCase);

        // The person filter still finds the row by the account it was about,
        // and now also by the operator who acted.
        var byActor = await client.GetStringAsync(
            $"/Administration/ActionLogs?From={from}&To={to}&Actor={DevelopmentOfflineIdentity.AdministratorId:D}");
        Assert.Contains("SecurityStampChanged", byActor, StringComparison.Ordinal);
        var bySubject = await client.GetStringAsync(
            $"/Administration/ActionLogs?From={from}&To={to}&Actor={targetAccount:D}");
        Assert.Contains("SecurityStampChanged", bySubject, StringComparison.Ordinal);
    }

    /// <summary>
    /// Rows written before the acting principal was recorded are not backfilled
    /// with a guess: they are labelled by what the event is, which is honest and
    /// is never "Unknown user".
    /// </summary>
    [Fact]
    public async Task ALegacySecurityEventIsLabelledByWhatItIsRatherThanAnUnknownUser()
    {
        var now = DateTimeOffset.UtcNow;
        using var factory = new IntakeWebApplicationFactory();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            context.SecurityEvents.Add(new SecurityEventEntity
            {
                Id = Guid.NewGuid(),
                Type = "SignIn",
                SubjectId = "unknown",
                OccurredAtUtc = now,
                Outcome = "Denied",
                CorrelationId = "legacy-security-row",
                ReasonCode = "invalid_security_stamp"
            });
            await context.SaveChangesAsync();
        }

        using var client = IntakeWebDriver.CreateClient(factory);
        var from = Uri.EscapeDataString(now.AddDays(-1).ToString("O"));
        var to = Uri.EscapeDataString(now.AddDays(1).ToString("O"));

        var html = await client.GetStringAsync(
            $"/Administration/ActionLogs?From={from}&To={to}&Area=Security");
        // The reason code is not a column; the recorded event type is.
        var row = ActionLogRow(html, "SignIn");

        Assert.Contains("Sign-in", row, StringComparison.Ordinal);
        Assert.DoesNotContain(ActorDisplayNames.UnknownStaff, row, StringComparison.Ordinal);
    }

    /// <summary>
    /// AI work, the application's own work and a colleague's work are three
    /// different answers to "who did this", and a removed colleague is a former
    /// colleague rather than an unknown one.
    /// </summary>
    [Fact]
    public async Task ActionLogsSeparateAiWorkSystemWorkAndFormerStaffFromColleagues()
    {
        var now = DateTimeOffset.UtcNow;
        var removedStaff = Guid.NewGuid();
        using var factory = new IntakeWebApplicationFactory();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            context.ActionHistory.AddRange(
                HistoryRow("ai_job", "ai-job-row", "ai_job_taken", "Automation", "pegasus-automation", now),
                HistoryRow("Case", "system-row", "case_chased", "SystemWorker", "worker", now.AddSeconds(-1)),
                HistoryRow("Case", "former-staff-row", "case_saved", "Staff", removedStaff.ToString("D"), now.AddSeconds(-2)));
            await context.SaveChangesAsync();
        }

        using var client = IntakeWebDriver.CreateClient(factory);
        var from = Uri.EscapeDataString(now.AddDays(-1).ToString("O"));
        var to = Uri.EscapeDataString(now.AddDays(1).ToString("O"));

        var html = await client.GetStringAsync($"/Administration/ActionLogs?From={from}&To={to}");

        var aiRow = ActionLogRow(html, "ai_job_taken");
        Assert.Contains(">AI<", aiRow, StringComparison.Ordinal);
        Assert.Contains("AI job", aiRow, StringComparison.Ordinal);

        var systemRow = ActionLogRow(html, "case_chased");
        Assert.Contains(OperatorLabels.SystemActorLabel, systemRow, StringComparison.Ordinal);
        Assert.DoesNotContain(">AI<", systemRow, StringComparison.Ordinal);

        var formerRow = ActionLogRow(html, "former-staff-row");
        Assert.Contains(ActorDisplayNames.FormerStaff, formerRow, StringComparison.Ordinal);
        Assert.DoesNotContain(ActorDisplayNames.UnknownStaff, formerRow, StringComparison.Ordinal);
        Assert.DoesNotContain(removedStaff.ToString("D"), formerRow, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// "What did the AI do" is a question about a class of actor, not about one
    /// subject id, so the log filters by actor kind as well as by person.
    /// </summary>
    [Fact]
    public async Task TheActorTypeFilterSelectsOneClassOfActor()
    {
        var now = DateTimeOffset.UtcNow;
        using var factory = new IntakeWebApplicationFactory();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            context.ActionHistory.AddRange(
                HistoryRow("ai_job", "kind-filter-ai", "ai_job_created", "Automation", "pegasus-automation", now),
                HistoryRow("Case", "kind-filter-staff", "case_saved", "Staff", DevelopmentOfflineIdentity.AdministratorId.ToString("D"), now.AddSeconds(-1)),
                HistoryRow("Case", "kind-filter-system", "case_chased", "SystemWorker", "worker", now.AddSeconds(-2)));
            context.SecurityEvents.Add(new SecurityEventEntity
            {
                Id = Guid.NewGuid(),
                Type = "PasswordChanged",
                SubjectId = DevelopmentOfflineIdentity.AdministratorId.ToString("D"),
                OccurredAtUtc = now.AddSeconds(-3),
                Outcome = "Succeeded",
                CorrelationId = "kind-filter-security",
                ReasonCode = "password_changed",
                ActorKind = "Staff",
                ActorSubjectId = DevelopmentOfflineIdentity.AdministratorId.ToString("D")
            });
            await context.SaveChangesAsync();
        }

        using var client = IntakeWebDriver.CreateClient(factory);
        var from = Uri.EscapeDataString(now.AddDays(-1).ToString("O"));
        var to = Uri.EscapeDataString(now.AddDays(1).ToString("O"));

        var ai = await client.GetStringAsync(
            $"/Administration/ActionLogs?From={from}&To={to}&ActorType=Automation");
        Assert.Contains("kind-filter-ai", ai, StringComparison.Ordinal);
        Assert.DoesNotContain("kind-filter-staff", ai, StringComparison.Ordinal);
        Assert.DoesNotContain("kind-filter-system", ai, StringComparison.Ordinal);
        Assert.DoesNotContain("PasswordChanged", ai, StringComparison.Ordinal);

        var staff = await client.GetStringAsync(
            $"/Administration/ActionLogs?From={from}&To={to}&ActorType=Staff");
        Assert.Contains("kind-filter-staff", staff, StringComparison.Ordinal);
        Assert.Contains("PasswordChanged", staff, StringComparison.Ordinal);
        Assert.DoesNotContain("kind-filter-ai", staff, StringComparison.Ordinal);

        var system = await client.GetStringAsync(
            $"/Administration/ActionLogs?From={from}&To={to}&ActorType=SystemWorker");
        Assert.Contains("kind-filter-system", system, StringComparison.Ordinal);
        Assert.DoesNotContain("kind-filter-ai", system, StringComparison.Ordinal);

        // A value that is not an actor kind carries no meaning, so it is dropped
        // rather than turned into a refused request.
        var unrecognised = await client.GetStringAsync(
            $"/Administration/ActionLogs?From={from}&To={to}&ActorType=not-a-kind");
        Assert.Contains("kind-filter-ai", unrecognised, StringComparison.Ordinal);
        Assert.Contains("kind-filter-staff", unrecognised, StringComparison.Ordinal);
    }

    /// <summary>
    /// Minutes are the smallest unit the log is filtered and read by: storage
    /// keeps its sub-second precision, but neither the period pickers nor the
    /// Time column show seconds, and a minute-only value still binds.
    /// </summary>
    [Fact]
    public async Task TheActionLogPeriodPickersAndTimeColumnStopAtMinutes()
    {
        var occurredAt = new DateTimeOffset(2026, 9, 10, 11, 43, 27, 123, TimeSpan.Zero);
        using var factory = new IntakeWebApplicationFactory();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            context.ActionHistory.Add(HistoryRow(
                "Case", "minute-precision-row", "case_saved", "Staff",
                DevelopmentOfflineIdentity.AdministratorId.ToString("D"), occurredAt));
            await context.SaveChangesAsync();
        }

        using var client = IntakeWebDriver.CreateClient(factory);
        // A whole day, because a minute-only value carries no offset and binds in
        // the host's zone: the window has to hold the row on a UTC host and on a
        // British-summer-time one alike.
        var html = await client.GetStringAsync(
            "/Administration/ActionLogs?From=2026-09-10T00%3A00&To=2026-09-11T00%3A00");

        Assert.Contains("\"2026-09-10T00:00\"", html, StringComparison.Ordinal);
        Assert.Contains("\"2026-09-11T00:00\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("2026-09-10T00:00:00", html, StringComparison.Ordinal);
        Assert.Contains("step=\"60\"", html, StringComparison.Ordinal);

        var row = ActionLogRow(html, "minute-precision-row");
        Assert.Contains(OperatorLabels.OfficeTime(occurredAt), row, StringComparison.Ordinal);
        // The machine-readable instant keeps full precision; the words do not.
        // Razor's attribute encoder writes the offset's '+' as &#x2B;.
        Assert.Contains(
            System.Text.Encodings.Web.HtmlEncoder.Default.Encode(occurredAt.ToString("O")),
            row,
            StringComparison.Ordinal);
        Assert.DoesNotContain(occurredAt.ToString("u"), row, StringComparison.Ordinal);
    }

    private static ActionHistoryEntity HistoryRow(
        string area,
        string reference,
        string eventKind,
        string actorKind,
        string actorSubjectId,
        DateTimeOffset occurredAtUtc) =>
        new()
        {
            Id = Guid.NewGuid(),
            AggregateType = area,
            AggregateId = reference,
            EventKind = eventKind,
            ActorKind = actorKind,
            ActorSubjectId = actorSubjectId,
            ActorRolesJson = "[]",
            OccurredAtUtc = occurredAtUtc,
            Outcome = "Succeeded",
            CorrelationId = reference
        };

    private static string ActionLogRow(string html, string content)
    {
        var match = Regex.Match(
            html,
            $"<tr>(?:(?!</tr>).)*{Regex.Escape(content)}(?:(?!</tr>).)*</tr>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);
        Assert.True(match.Success, $"The action-log row containing '{content}' must render.");
        return match.Value;
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

    [Fact]
    public async Task ReportsRendersReportsByPrincipalCountsAndTheMatchingCsvForThePeriod()
    {
        var from = new DateTimeOffset(2032, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddDays(31);
        var generatedAt = from.AddDays(2);
        var sentAt = from.AddDays(3);
        using var factory = new IntakeWebApplicationFactory();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var principal = await SeededPrincipals.QdosAsync(scope.ServiceProvider);
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            var receiptId = Guid.NewGuid();
            var caseId = Guid.NewGuid();
            var generationId = Guid.NewGuid();
            var documentId = Guid.NewGuid();
            var versionId = Guid.NewGuid();
            var sha256 = new string('a', 64);
            context.AddRange(
                new IntakeReceiptEntity
                {
                    Id = receiptId,
                    SourceFileName = "origin.pdf",
                    MediaType = "application/pdf",
                    SourceLength = 1,
                    SourceHash = new string('1', 64),
                    SourceChannel = "manual_upload",
                    ExternalReceiptToken = $"origin:{receiptId:N}",
                    ReceivedAtUtc = from,
                    ProcessedAtUtc = from,
                    SourceReaderKey = "test",
                    SourceReaderVersion = "1",
                    Version = 0,
                    Decision = "case_created",
                    DecisionReason = "test",
                    EvidenceJson = "[]",
                    FieldsJson = "[]",
                    OcrCandidatesJson = "[]"
                },
                new CaseEntity
                {
                    Id = caseId,
                    PrincipalId = principal.Id,
                    SequenceLineageId = principal.SequenceLineageId,
                    Year = 2032,
                    Sequence = 1,
                    Reference = "QDOS32001",
                    Type = "Inspection",
                    InitialState = "Review",
                    CustodyState = "Confirmed",
                    OriginIntakeReceiptId = receiptId,
                    CreatedAtUtc = from,
                    Version = 1,
                    ConcurrencyToken = Guid.NewGuid()
                },
                new CaseReportGenerationEntity
                {
                    Id = generationId,
                    CaseId = caseId,
                    CaseVersion = 1,
                    SnapshotHash = new string('b', 64),
                    SnapshotJson = "{}",
                    TemplateVersion = "test",
                    RendererVersion = "test",
                    State = "ready",
                    GeneratedAtUtc = generatedAt,
                    Version = 1
                },
                new CaseDocumentEntity
                {
                    Id = documentId,
                    CaseId = caseId,
                    Ordinal = 1,
                    SourceOccurrenceIdentity = "report-output"
                },
                new DocumentVersionEntity
                {
                    Id = versionId,
                    DocumentId = documentId,
                    Version = 1,
                    FileName = "report.pdf",
                    MediaType = "application/pdf",
                    ContentLength = 1,
                    Sha256 = sha256,
                    CustodyStatus = DocumentCustodyStatus.Confirmed,
                    CreatedAtUtc = generatedAt,
                    CreatedBy = "test",
                    IsCurrent = true
                },
                new GeneratedCaseArtifactEntity
                {
                    Id = Guid.NewGuid(),
                    GenerationId = generationId,
                    VersionId = versionId,
                    Kind = "AssessmentReport",
                    Sha256 = sha256,
                    State = "Confirmed",
                    OperationKey = $"artifact:{Guid.NewGuid():N}"
                },
                new StaffMailSendOperationEntity
                {
                    Id = Guid.NewGuid(),
                    ActorSubjectId = Guid.NewGuid().ToString("D"),
                    MailboxId = Guid.NewGuid(),
                    MailboxGeneration = 1,
                    OperationKey = $"send:{Guid.NewGuid():N}",
                    PayloadHash = new string('2', 64),
                    Purpose = StaffMailPurpose.CaseReport,
                    ContextId = generationId,
                    ContextVersion = 1,
                    ComposeMode = StaffMailComposeMode.New,
                    RecipientsJson = "[]",
                    Subject = "report",
                    Body = "report",
                    AttachmentsJson = "[]",
                    State = StaffMailState.Sent,
                    CorrelationMarker = "test",
                    CreatedAtUtc = sentAt,
                    RequestedAtUtc = sentAt,
                    ObservedSentAtUtc = sentAt,
                    Version = 1,
                    ConcurrencyToken = Guid.NewGuid()
                });
            await context.SaveChangesAsync();
        }

        using var client = IntakeWebDriver.CreateClient(factory);
        var fromParam = Uri.EscapeDataString(from.ToString("yyyy-MM-ddTHH:mm", System.Globalization.CultureInfo.InvariantCulture));
        var toParam = Uri.EscapeDataString(to.ToString("yyyy-MM-ddTHH:mm", System.Globalization.CultureInfo.InvariantCulture));

        var html = await client.GetStringAsync($"/Administration/Reports?from={fromParam}&to={toParam}");

        Assert.Contains("Reports by Principal", html, StringComparison.Ordinal);
        Assert.Contains("QDOS", html, StringComparison.Ordinal);
        Assert.Contains("Report 1", html, StringComparison.Ordinal);
        Assert.DoesNotContain("generated artifact", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pending or failed", html, StringComparison.OrdinalIgnoreCase);

        using var csvResponse = await client.GetAsync(
            $"/Administration/Reports?handler=PrincipalCsv&from={fromParam}&to={toParam}");
        csvResponse.EnsureSuccessStatusCode();
        Assert.StartsWith("text/csv", csvResponse.Content.Headers.ContentType?.MediaType, StringComparison.Ordinal);
        var csv = await csvResponse.Content.ReadAsStringAsync();
        Assert.Contains("Principal,Reports produced,Reports sent,Report types", csv, StringComparison.Ordinal);
        Assert.Contains("QDOS,1,1,Report 1", csv, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReportsRendersTurnaroundHoldingAgeAndTheMatchingCsvForThePeriod()
    {
        var from = new DateTimeOffset(2032, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddDays(31);
        var generatedAt = from.AddDays(2);
        var readyAt = from.AddDays(3);
        var sentAt = from.AddDays(4);
        var heldAt = from.AddDays(5);
        using var factory = new IntakeWebApplicationFactory();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var principal = await SeededPrincipals.QdosAsync(scope.ServiceProvider);
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            var receiptId = Guid.NewGuid();
            var caseId = Guid.NewGuid();
            var generationId = Guid.NewGuid();
            var documentId = Guid.NewGuid();
            var versionId = Guid.NewGuid();
            var sha256 = new string('a', 64);
            context.AddRange(
                new IntakeReceiptEntity
                {
                    Id = receiptId,
                    SourceFileName = "origin.pdf",
                    MediaType = "application/pdf",
                    SourceLength = 1,
                    SourceHash = new string('1', 64),
                    SourceChannel = "manual_upload",
                    ExternalReceiptToken = $"origin:{receiptId:N}",
                    ReceivedAtUtc = from,
                    ProcessedAtUtc = from,
                    SourceReaderKey = "test",
                    SourceReaderVersion = "1",
                    Version = 0,
                    Decision = "case_created",
                    DecisionReason = "test",
                    EvidenceJson = "[]",
                    FieldsJson = "[]",
                    OcrCandidatesJson = "[]"
                },
                new CaseEntity
                {
                    Id = caseId,
                    PrincipalId = principal.Id,
                    SequenceLineageId = principal.SequenceLineageId,
                    Year = 2032,
                    Sequence = 2,
                    Reference = "QDOS32002",
                    Type = "Inspection",
                    InitialState = "Review",
                    CustodyState = "Confirmed",
                    OriginIntakeReceiptId = receiptId,
                    CreatedAtUtc = from,
                    Version = 1,
                    ConcurrencyToken = Guid.NewGuid()
                },
                new CaseWorkflowEntity
                {
                    CaseId = caseId,
                    State = "Held",
                    PreHoldState = "Review",
                    Version = 1,
                    ConcurrencyToken = Guid.NewGuid()
                },
                new CaseWorkflowEventEntity
                {
                    Id = Guid.NewGuid(),
                    CaseId = caseId,
                    EventType = "case_held",
                    OperationKey = "hold:mi03-test",
                    RequestHash = new string('c', 64),
                    ActorKind = "Staff",
                    ActorSubjectId = Guid.NewGuid().ToString("D"),
                    ActorRolesJson = "[]",
                    Reason = "Waiting for evidence",
                    OccurredAtUtc = heldAt,
                    BeforeVersion = 0,
                    AfterVersion = 1
                },
                new CaseReportGenerationEntity
                {
                    Id = generationId,
                    CaseId = caseId,
                    CaseVersion = 1,
                    SnapshotHash = new string('b', 64),
                    SnapshotJson = "{}",
                    TemplateVersion = "test",
                    RendererVersion = "test",
                    State = "ready",
                    GeneratedAtUtc = generatedAt,
                    Version = 2
                },
                new CaseDocumentEntity
                {
                    Id = documentId,
                    CaseId = caseId,
                    Ordinal = 1,
                    SourceOccurrenceIdentity = "report-output"
                },
                new DocumentVersionEntity
                {
                    Id = versionId,
                    DocumentId = documentId,
                    Version = 1,
                    FileName = "report.pdf",
                    MediaType = "application/pdf",
                    ContentLength = 1,
                    Sha256 = sha256,
                    CustodyStatus = DocumentCustodyStatus.Confirmed,
                    CreatedAtUtc = generatedAt,
                    CreatedBy = "test",
                    IsCurrent = true
                },
                new GeneratedCaseArtifactEntity
                {
                    Id = Guid.NewGuid(),
                    GenerationId = generationId,
                    VersionId = versionId,
                    Kind = "AssessmentReport",
                    Sha256 = sha256,
                    State = "Confirmed",
                    OperationKey = $"artifact:{Guid.NewGuid():N}"
                },
                new ActionHistoryEntity
                {
                    Id = Guid.NewGuid(),
                    AggregateType = "case",
                    AggregateId = caseId.ToString("D"),
                    EventKind = "case_report_generation_ready",
                    ActorKind = "Staff",
                    ActorSubjectId = Guid.NewGuid().ToString("D"),
                    ActorRolesJson = "[]",
                    OccurredAtUtc = readyAt,
                    Outcome = "Succeeded",
                    CorrelationId = "report-ready:mi03-test",
                    AfterJson = $"{{\"generationId\":\"{generationId:D}\"}}"
                },
                new StaffMailSendOperationEntity
                {
                    Id = Guid.NewGuid(),
                    ActorSubjectId = Guid.NewGuid().ToString("D"),
                    MailboxId = Guid.NewGuid(),
                    MailboxGeneration = 1,
                    OperationKey = $"send:{Guid.NewGuid():N}",
                    PayloadHash = new string('2', 64),
                    Purpose = StaffMailPurpose.CaseReport,
                    ContextId = generationId,
                    ContextVersion = 1,
                    ComposeMode = StaffMailComposeMode.New,
                    RecipientsJson = "[]",
                    Subject = "report",
                    Body = "report",
                    AttachmentsJson = "[]",
                    State = StaffMailState.Sent,
                    CorrelationMarker = "test",
                    CreatedAtUtc = sentAt,
                    RequestedAtUtc = sentAt,
                    ObservedSentAtUtc = sentAt,
                    Version = 1,
                    ConcurrencyToken = Guid.NewGuid()
                });
            await context.SaveChangesAsync();
        }

        using var client = IntakeWebDriver.CreateClient(factory);
        var fromParam = Uri.EscapeDataString(from.ToString("yyyy-MM-ddTHH:mm", System.Globalization.CultureInfo.InvariantCulture));
        var toParam = Uri.EscapeDataString(to.ToString("yyyy-MM-ddTHH:mm", System.Globalization.CultureInfo.InvariantCulture));

        var html = await client.GetStringAsync($"/Administration/Reports?from={fromParam}&to={toParam}");

        Assert.Contains("Turnaround", html, StringComparison.Ordinal);
        Assert.Contains("QDOS", html, StringComparison.Ordinal);
        Assert.Contains(OperatorLabels.OfficeTime(heldAt), html, StringComparison.Ordinal);
        Assert.Contains("2 days", html, StringComparison.Ordinal);
        Assert.Contains("3 days", html, StringComparison.Ordinal);
        Assert.Contains("4 days", html, StringComparison.Ordinal);
        Assert.DoesNotContain("missing origin", html, StringComparison.OrdinalIgnoreCase);

        using var csvResponse = await client.GetAsync(
            $"/Administration/Reports?handler=TurnaroundCsv&from={fromParam}&to={toParam}");
        csvResponse.EnsureSuccessStatusCode();
        Assert.StartsWith("text/csv", csvResponse.Content.Headers.ContentType?.MediaType, StringComparison.Ordinal);
        var csv = await csvResponse.Content.ReadAsStringAsync();
        Assert.Contains(
            "Principal,Currently held,Oldest held since,Time to produce,Time to ready,Time to send",
            csv,
            StringComparison.Ordinal);
        Assert.Contains($"QDOS,1,{OperatorLabels.OfficeTime(heldAt)},2 days,3 days,4 days", csv, StringComparison.Ordinal);
    }

    private static string FormValue(string html, string name)
    {
        var tag = Regex.Match(
            html,
            $"<input[^>]*name=\"{Regex.Escape(name)}\"[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(tag.Success, $"The form must render '{name}'.");
        return InputValueOrEmpty(tag.Value, name);
    }

    private static string InputValueOrEmpty(string html, string name)
    {
        var tag = Regex.Match(
            html,
            $"<input[^>]*name=\"{Regex.Escape(name)}\"[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(tag.Success, $"The form must render '{name}'.");
        var value = Regex.Match(
            tag.Value,
            "value=\"(?<value>[^\"]*)\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return value.Success ? System.Net.WebUtility.HtmlDecode(value.Groups["value"].Value) : string.Empty;
    }
}
