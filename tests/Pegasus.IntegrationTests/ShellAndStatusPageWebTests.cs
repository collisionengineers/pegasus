using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Identity;
using Pegasus.Core.Notifications;
using Pegasus.Web.Pages;
using Pegasus.Web.Presentation;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The shell contract: what every screen shares, and what the screens that are
/// not a place in the application deliberately do not share.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class ShellAndStatusPageWebTests
{
    [Fact]
    public async Task NavigationSpeaksTheBusinessVocabularyAndNeverShowsAnInertItem()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        using var response = await client.GetAsync("/");
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains(">Work Centre<", html, StringComparison.Ordinal);
        Assert.Contains(">Inbox<", html, StringComparison.Ordinal);
        Assert.Contains(">Cases<", html, StringComparison.Ordinal);
        Assert.Contains(">Search<", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Primary\"", html, StringComparison.Ordinal);

        // "intake" is internal code vocabulary; the office does not have intake
        // queues, because intake is automatic and nothing queues.
        Assert.DoesNotContain(">Intake<", html, StringComparison.Ordinal);

        // A capability a deployment has not composed is absent, not a disabled
        // nav span that says the product is broken.
        Assert.DoesNotContain("Intake unavailable", html, StringComparison.Ordinal);
        Assert.DoesNotContain("nav-link--unavailable", html, StringComparison.Ordinal);

        // v26 shell: the Collapse control at the rail's foot and the bell in
        // the utility bar. The working-set strip is removed from every page
        // (v29): no strip, no tabs and no record announcement on main.
        Assert.Contains("data-rail-toggle", html, StringComparison.Ordinal);
        Assert.Contains(">Collapse<", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-working-set", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-workspace-open", html, StringComparison.Ordinal);
        Assert.DoesNotContain("workspace-tab", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-record-kind", html, StringComparison.Ordinal);
        Assert.Contains("data-dialog-open=\"notifications-dialog\"", html, StringComparison.Ordinal);
        Assert.Contains("data-dialog=\"notifications-dialog\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RefinedMarksRenderFromFingerprintablePngRuntimeCopiesInBothFrames()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var authenticated = IntakeWebDriver.CreateClient(factory);
        var frames = new[]
        {
            (Route: "/", Asset: "pegasus-mark-refined-128.png", Hash: "1D6758A5F9D90EA4539DBB498BF0171A93C23B85E9C3868BDDC7D6ABC724A1CB", Client: authenticated),
            (Route: "/Account/AccessDenied", Asset: "pegasus-mark-refined-256.png", Hash: "E3C9712DE05E18D93BD21857F3961A8832941D91D942FA1E34F513944B545E1D", Client: authenticated)
        };

        foreach (var frame in frames)
        {
            using var frameResponse = await frame.Client.GetAsync(frame.Route);
            Assert.True(frameResponse.IsSuccessStatusCode, $"{frame.Route} answered {(int)frameResponse.StatusCode}.");
            var html = await frameResponse.Content.ReadAsStringAsync();
            // MapStaticAssets fingerprints in the file name itself
            // ("mark.tctmpgz0uo.png"), not in a query string, so the
            // match allows either shape rather than assuming one.
            var assetBase = Regex.Escape(Path.GetFileNameWithoutExtension(frame.Asset));
            var assetExtension = Regex.Escape(Path.GetExtension(frame.Asset));
            var image = Regex.Match(
                html,
                $"src=\"(?<url>/images/{assetBase}(?:\\.[A-Za-z0-9]+)?{assetExtension}(?:\\?v=[^\"]*)?)\"",
                RegexOptions.CultureInvariant);
            Assert.True(image.Success, $"{frame.Route} did not render a fingerprinted {frame.Asset} URL.");

            using var imageResponse = await frame.Client.GetAsync(image.Groups["url"].Value);
            imageResponse.EnsureSuccessStatusCode();
            Assert.Equal("image/png", imageResponse.Content.Headers.ContentType?.MediaType);
            var bytes = await imageResponse.Content.ReadAsByteArrayAsync();
            Assert.Equal(frame.Hash, Convert.ToHexString(SHA256.HashData(bytes)));
        }
    }

    [Fact]
    public async Task UnknownRecordUrlRendersTheDesignedNotFoundPageRatherThanARawBrowserError()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        using var response = await client.GetAsync($"/Cases/{Guid.NewGuid():D}");

        // The status code is still the truth of the exchange; only the body
        // changes, from Chrome's default page to a worded one.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("We could not find that page", html, StringComparison.Ordinal);
        Assert.Contains("auth-card", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FormerPublicUploadRouteUsesTheOrdinaryNotFoundSurface()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        using var response = await client.GetAsync($"/Uploads/{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("We could not find that page", html, StringComparison.Ordinal);
        Assert.Contains("auth-card", html, StringComparison.Ordinal);
        Assert.Contains("Return to Work Centre", html, StringComparison.Ordinal);
        Assert.DoesNotContain("This link is no longer active", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnlyPayloadTooLargeStatusStatesTheGenericUploadLimit()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        var payloadTooLarge = await client.GetStringAsync("/status/413");
        var badRequest = await client.GetStringAsync("/status/400");

        Assert.Contains("The upload is too large", payloadTooLarge, StringComparison.Ordinal);
        Assert.Contains("This upload exceeds the allowed size limit.", payloadTooLarge, StringComparison.Ordinal);
        Assert.DoesNotContain("10 MB", payloadTooLarge, StringComparison.Ordinal);
        Assert.Contains("We could not complete that request", badRequest, StringComparison.Ordinal);
        Assert.DoesNotContain("file is too large", badRequest, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("10 MB", badRequest, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/Account/SignIn", "Too many sign-in attempts")]
    [InlineData("/Cases/missing", "Too many requests")]
    public void RateLimitedStatusNamesOnlyItsKnownOrigin(
        string originalPath,
        string expectedHeading)
    {
        var context = new DefaultHttpContext();
        context.Features.Set<IStatusCodeReExecuteFeature>(new StatusCodeReExecuteFeature
        {
            OriginalPath = originalPath,
            OriginalPathBase = string.Empty
        });
        var page = new StatusCodeModel
        {
            PageContext = new PageContext { HttpContext = context }
        };

        page.OnGet(StatusCodes.Status429TooManyRequests);

        Assert.Equal(expectedHeading, page.Heading);
        Assert.Equal("Wait a minute, then try again.", page.Explanation);
    }

    [Fact]
    public async Task SignOutRedirectsToAConfirmationThatTheSessionEndedRatherThanABareSignInForm()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        // The navigation posts sign-out directly, so the unstyled interstitial
        // that used to answer this route was never reached; what was missing is
        // the confirmation. It is a one-time state of the sign-in page, not a
        // page of its own, so a bookmark cannot assert a sign-out that did not
        // just happen.
        var signOutPage = await client.GetStringAsync("/Search");
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = AntiforgeryValue(signOutPage)
        });
        using var response = await client.PostAsync("/Account/SignOut", content);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains(
            "signedOut=True",
            response.Headers.Location?.OriginalString ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string AntiforgeryValue(string html)
    {
        const string marker = "name=\"__RequestVerificationToken\"";
        var nameIndex = html.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(nameIndex >= 0, "The page rendered no antiforgery token.");

        const string valueMarker = "value=\"";
        var valueIndex = html.IndexOf(valueMarker, nameIndex, StringComparison.Ordinal) + valueMarker.Length;
        var end = html.IndexOf('"', valueIndex);
        return html[valueIndex..end];
    }

    [Fact]
    public async Task ScreensThatAreNotAPlaceInTheApplicationRenderWithoutTheStaffNavigation()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        using var response = await client.GetAsync("/status/404");
        var html = await response.Content.ReadAsStringAsync();

        // Around a sign-in form or an anonymous status card the navigation
        // would show a visitor the internal structure of the product.
        Assert.DoesNotContain("aria-label=\"Primary\"", html, StringComparison.Ordinal);
        Assert.Contains("auth-card", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// v28 P6-D: a signed-in person refused a Manage route reads the refusal
    /// in the navless frame with the rest of the error family — the area as
    /// the eyebrow, "Access denied", one sentence and Return to Work Centre.
    /// </summary>
    [Fact]
    public async Task AccessDeniedOnAManageRouteRendersInTheNavlessFrameAndNamesTheArea()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        using var response = await client.GetAsync("/Account/AccessDenied?ReturnUrl=%2FAdministration%2FMailboxes");
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("aria-label=\"Primary\"", html, StringComparison.Ordinal);
        Assert.Contains("auth-card", html, StringComparison.Ordinal);
        Assert.Contains("Return to Work Centre", html, StringComparison.Ordinal);
        Assert.Contains("<p class=\"eyebrow\">Administration</p>", html, StringComparison.Ordinal);
        Assert.Contains("<h1>Access denied</h1>", html, StringComparison.Ordinal);
        Assert.Contains("Administration is available to Administrators only.", html, StringComparison.Ordinal);

        // Without a Manage return route the page still refuses, in the plain
        // sentence, and names no area it cannot know.
        var plain = await client.GetStringAsync("/Account/AccessDenied");
        Assert.DoesNotContain("<p class=\"eyebrow\">", plain, StringComparison.Ordinal);
        Assert.Contains("Your account does not have access to this page.", plain, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StatusCodePagesDoNotHijackTheMachineSurfaces()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        // Health probes and the version endpoint answer programs. A
        // re-executed HTML card in place of their body would break the caller
        // rather than help anyone read it.
        using var live = await client.GetAsync("/health/live");
        Assert.DoesNotContain(
            "auth-card",
            await live.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);

        using var version = await client.GetAsync("/diagnostics/version");
        version.EnsureSuccessStatusCode();
        Assert.Contains(
            "version",
            await version.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The bell (Work Centre D10): the person's own notifications and nothing
    /// else, newest first, each row a form that opens the notification.
    /// </summary>
    [Fact]
    public async Task BellShowsTheEmptyStateAndNoCountWhenNothingIsUnread()
    {
        var notifications = new StubNotifications([]);
        using var host = FactoryWith(notifications);
        using var client = host.CreateClient();

        using var response = await client.GetAsync("/Search");
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains(">No notifications<", html, StringComparison.Ordinal);
        Assert.DoesNotContain("bell-count", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">Mark all read<", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Notifications\"", html, StringComparison.Ordinal);
        // The bell never carries office-wide work.
        Assert.DoesNotContain("data-notification-list", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BellListsPersonalNotificationsWithTheUnreadCountAndCauseWording()
    {
        var caseId = Guid.NewGuid();
        var raisedAt = new DateTimeOffset(2031, 5, 6, 10, 0, 0, TimeSpan.Zero);
        var unreadEstimate = new StaffNotification(
            Guid.NewGuid(), Pegasus.Web.Authentication.DevelopmentOfflineIdentity.AdministratorId, caseId,
            "QDOS26214", "MA59BDY", StaffNotificationCause.AiDraftReady,
            $"/Cases/{caseId:D}?section=estimate", raisedAt, ReadAtUtc: null);
        var unreadAssigned = new StaffNotification(
            Guid.NewGuid(), Pegasus.Web.Authentication.DevelopmentOfflineIdentity.AdministratorId, caseId,
            "QDOS26214", "MA59BDY", StaffNotificationCause.CaseAssigned,
            $"/Cases/{caseId:D}", raisedAt.AddMinutes(-12), ReadAtUtc: null);
        var readQuery = new StaffNotification(
            Guid.NewGuid(), Pegasus.Web.Authentication.DevelopmentOfflineIdentity.AdministratorId, caseId,
            "PCH26004", "VN71ULZ", StaffNotificationCause.QueryReceived,
            $"/Cases/{caseId:D}?section=correspondence", raisedAt.AddDays(-2), raisedAt.AddDays(-1));
        var notifications = new StubNotifications([unreadEstimate, unreadAssigned, readQuery]);
        using var host = FactoryWith(notifications);
        using var client = host.CreateClient();

        using var response = await client.GetAsync("/Search");
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("<span class=\"bell-count\" aria-hidden=\"true\">2</span>", html, StringComparison.Ordinal);
        // The middle dot is HTML-encoded by Razor; the count and word are what matter.
        Assert.Contains(" 2 unread\"", html, StringComparison.Ordinal);
        Assert.Contains(">Estimate draft ready<", html, StringComparison.Ordinal);
        Assert.Contains(">Assigned to you<", html, StringComparison.Ordinal);
        Assert.Contains(">Query received<", html, StringComparison.Ordinal);
        Assert.Contains("MA59BDY", html, StringComparison.Ordinal);
        Assert.Contains(">Unread<", html, StringComparison.Ordinal);
        Assert.Contains(">Mark all read<", html, StringComparison.Ordinal);
        Assert.Contains($"data-notification=\"{unreadEstimate.Id:D}\"", html, StringComparison.Ordinal);
        Assert.Contains("row-button row-button--unread", html, StringComparison.Ordinal);
        // The vendor never appears in operator copy.
        Assert.DoesNotContain("Claude", html, StringComparison.Ordinal);
        // The list is the store's order: newest first, exactly as returned.
        Assert.True(
            html.IndexOf("Estimate draft ready", StringComparison.Ordinal)
                < html.IndexOf("Assigned to you", StringComparison.Ordinal),
            "The bell must keep the store's newest-first order.");
    }

    [Fact]
    public async Task OpeningANotificationMarksItReadAndRedirectsToItsRoute()
    {
        var caseId = Guid.NewGuid();
        var notification = new StaffNotification(
            Guid.NewGuid(), Pegasus.Web.Authentication.DevelopmentOfflineIdentity.AdministratorId, caseId,
            "QDOS26214", "MA59BDY", StaffNotificationCause.EmailReceived,
            $"/Cases/{caseId:D}?section=files", new DateTimeOffset(2031, 5, 6, 10, 0, 0, TimeSpan.Zero), ReadAtUtc: null);
        var notifications = new StubNotifications([notification]);
        using var host = FactoryWith(notifications);
        using var client = host.CreateClient();

        var page = await client.GetStringAsync("/Search");
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = AntiforgeryValue(page),
            ["returnUrl"] = "/Search"
        });
        using var response = await client.PostAsync($"/Notifications?handler=Open&id={notification.Id:D}", content);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/Cases/{caseId:D}?section=files", response.Headers.Location?.OriginalString);
        Assert.Equal([notification.Id], notifications.Opened);

        // A notification that is not this person's opens nothing and returns
        // to the page the operator was on.
        using var missing = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = AntiforgeryValue(page),
            ["returnUrl"] = "/Search"
        });
        using var unknown = await client.PostAsync($"/Notifications?handler=Open&id={Guid.NewGuid():D}", missing);
        Assert.Equal(HttpStatusCode.Redirect, unknown.StatusCode);
        Assert.Equal("/Search", unknown.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task MarkAllReadMarksThePersonsNotificationsAndReturnsToThePage()
    {
        var notifications = new StubNotifications([]);
        using var host = FactoryWith(notifications);
        using var client = host.CreateClient();

        var page = await client.GetStringAsync("/Search");
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = AntiforgeryValue(page),
            ["returnUrl"] = "/Search?query=abc"
        });
        using var response = await client.PostAsync("/Notifications?handler=MarkAllRead", content);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Search?query=abc", response.Headers.Location?.OriginalString);
        Assert.Equal(1, notifications.MarkAllReadCalls);

        // A GET of the handler route is not a screen: it lands on the Work
        // Centre with the dialog open.
        using var get = await client.GetAsync("/Notifications");
        Assert.Equal(HttpStatusCode.Redirect, get.StatusCode);
        Assert.Equal("/?notifications=1", get.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task NotificationsQueryOpensTheDialogOnLoad()
    {
        var notifications = new StubNotifications([]);
        using var host = FactoryWith(notifications);
        using var client = host.CreateClient();

        var html = await client.GetStringAsync("/?notifications=1");

        Assert.Contains("data-dialog=\"notifications-dialog\" data-dialog-open-on-load=\"true\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NotificationFailureDoesNotPreventThePageFromRendering()
    {
        using var host = FactoryWith(new UnavailableNotifications());
        using var client = host.CreateClient();

        using var response = await client.GetAsync("/Search");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Notifications unavailable.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("No notifications", html, StringComparison.Ordinal);
        Assert.DoesNotContain("sensitive store failure", html, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(InvalidOperationException), html, StringComparison.Ordinal);
    }

    private static BellHost FactoryWith(IMyStaffNotifications notifications) => new(notifications);

    /// <summary>
    /// A host whose bell reads the given notifications. Owns the base factory
    /// (and so the test database) as well as the derived one.
    /// </summary>
    private sealed class BellHost : IDisposable
    {
        private readonly IntakeWebApplicationFactory _base = new();
        private readonly Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> _factory;

        public BellHost(IMyStaffNotifications notifications)
        {
            _factory = _base.WithWebHostBuilder(builder =>
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IMyStaffNotifications>();
                    services.AddSingleton(notifications);
                }));
        }

        public HttpClient CreateClient() => _factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost:7139")
            });

        public void Dispose()
        {
            _factory.Dispose();
            _base.Dispose();
        }
    }

    private sealed class UnavailableNotifications : IMyStaffNotifications
    {
        public Task<IReadOnlyList<StaffNotification>> ListAsync(ActionActor actor, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("sensitive store failure");

        public Task<int> CountUnreadAsync(ActionActor actor, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("sensitive store failure");

        public Task<StaffNotification?> OpenAsync(ActionActor actor, Guid notificationId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("sensitive store failure");

        public Task<int> MarkAllReadAsync(ActionActor actor, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("sensitive store failure");
    }

    private sealed class StubNotifications(IReadOnlyList<StaffNotification> rows) : IMyStaffNotifications
    {
        private readonly List<StaffNotification> _rows = [.. rows];

        public List<Guid> Opened { get; } = [];

        public int MarkAllReadCalls { get; private set; }

        public Task<IReadOnlyList<StaffNotification>> ListAsync(ActionActor actor, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StaffNotification>>(_rows.ToArray());

        public Task<int> CountUnreadAsync(ActionActor actor, CancellationToken cancellationToken) =>
            Task.FromResult(_rows.Count(row => row.IsUnread));

        public Task<StaffNotification?> OpenAsync(ActionActor actor, Guid notificationId, CancellationToken cancellationToken)
        {
            var index = _rows.FindIndex(row => row.Id == notificationId);
            if (index == -1)
            {
                return Task.FromResult<StaffNotification?>(null);
            }

            Opened.Add(notificationId);
            var opened = _rows[index] with { ReadAtUtc = DateTimeOffset.UtcNow };
            _rows[index] = opened;
            return Task.FromResult<StaffNotification?>(opened);
        }

        public Task<int> MarkAllReadAsync(ActionActor actor, CancellationToken cancellationToken)
        {
            MarkAllReadCalls++;
            var unread = _rows.Count(row => row.IsUnread);
            for (var i = 0; i < _rows.Count; i++)
            {
                _rows[i] = _rows[i] with { ReadAtUtc = _rows[i].ReadAtUtc ?? DateTimeOffset.UtcNow };
            }

            return Task.FromResult(unread);
        }
    }
}
