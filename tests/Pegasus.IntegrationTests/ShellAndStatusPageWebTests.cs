using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Pegasus.Core.Identity;
using Pegasus.Core.Notifications;
using Pegasus.Core.ReleaseNotes;
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
    public async Task EveryFrameDrawsItsIconsFromTheOneFingerprintedSpriteFile()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        // The static-asset map names the file by its content (lucide-sprite.<hash>.svg); where it does
        // not, the content hash rides as ?v=. Either way the address is fingerprinted.
        var fingerprinted = new Regex(
            "^/images/lucide-sprite(?:\\.[A-Za-z0-9]+\\.svg(?:\\?v=[A-Za-z0-9_-]+)?|\\.svg\\?v=[A-Za-z0-9_-]+)$",
            RegexOptions.CultureInvariant);

        // The rail frame (the Work Centre, and the Cases rail whose tabs name their glyph from C#) and the
        // external frame, whose access-denied page draws no glyph of its own.
        var frames = new[]
        {
            (Route: "/", DrawsGlyphs: true),
            (Route: "/Cases", DrawsGlyphs: true),
            (Route: "/Account/AccessDenied", DrawsGlyphs: false)
        };
        foreach (var (route, drawsGlyphs) in frames)
        {
            using var page = await client.GetAsync(route);
            Assert.True(page.IsSuccessStatusCode, $"{route} answered {(int)page.StatusCode}.");
            var html = await page.Content.ReadAsStringAsync();

            var published = Regex.Match(html, "<html [^>]*data-icon-sprite=\"(?<url>[^\"]+)\"", RegexOptions.CultureInvariant);
            Assert.True(published.Success, $"{route} did not publish data-icon-sprite on <html>.");
            var sprite = WebUtility.HtmlDecode(published.Groups["url"].Value);
            Assert.Matches(fingerprinted, sprite);

            // Every glyph is the sprite file's address and its symbol id; none is a bare in-page fragment.
            var uses = Regex.Matches(html, "<use href=\"(?<href>[^\"]*)\"", RegexOptions.CultureInvariant)
                .Select(match => WebUtility.HtmlDecode(match.Groups["href"].Value))
                .ToList();
            if (drawsGlyphs)
            {
                Assert.NotEmpty(uses);
            }

            Assert.All(uses, href => Assert.StartsWith(sprite + "#icon-", href, StringComparison.Ordinal));

            using var file = await client.GetAsync(sprite);
            Assert.Equal(HttpStatusCode.OK, file.StatusCode);
            Assert.Equal("image/svg+xml", file.Content.Headers.ContentType?.MediaType);
            var symbols = Regex.Matches(await file.Content.ReadAsStringAsync(), "<symbol id=\"(?<id>icon-[^\"]+)\"")
                .Select(match => match.Groups["id"].Value)
                .ToHashSet(StringComparer.Ordinal);
            var drawn = uses
                .Select(href => href[(href.IndexOf('#', StringComparison.Ordinal) + 1)..])
                .ToHashSet(StringComparer.Ordinal);
            Assert.True(
                symbols.IsSupersetOf(drawn),
                $"{route} draws {string.Join(", ", drawn.Except(symbols))}, which the sprite file does not define.");
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
        // The whole navless family shares the v30 split frame.
        Assert.Contains("class=\"auth-identity\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"auth-panel\"", html, StringComparison.Ordinal);
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
    public async Task BellShowsNoCountWhenNothingIsUnreadAndListsNothingUntilItOpens()
    {
        var notifications = new StubNotifications([]);
        using var host = FactoryWith(notifications);
        using var client = host.CreateClient();

        using var response = await client.GetAsync("/Search");
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("bell-count", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">Mark all read<", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Notifications\"", html, StringComparison.Ordinal);
        // The page carries the count only: the list is asked for when the bell opens.
        Assert.Contains("data-notifications-url=\"/Notifications?handler=List\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain($">{OperatorLabels.Shell.NoNotifications}<", html, StringComparison.Ordinal);
        Assert.Equal(1, notifications.CountCalls);
        Assert.Equal(0, notifications.ListCalls);

        // The bell never carries office-wide work.
        using var list = await client.GetAsync("/Notifications?handler=List&returnUrl=%2FSearch");
        list.EnsureSuccessStatusCode();
        var listHtml = await list.Content.ReadAsStringAsync();
        Assert.Contains($">{OperatorLabels.Shell.NoNotifications}<", listHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("data-notification-list", listHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("<html", listHtml, StringComparison.Ordinal);
        Assert.Equal(1, notifications.ListCalls);
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
        // Mark all read sits in the dialog's head and the foot keeps Close (v41 S2).
        var notificationsDialog = Between(html, "data-dialog=\"notifications-dialog\"", "data-dialog=\"command-dialog\"");
        Assert.Contains(">Mark all read<", Between(notificationsDialog, "class=\"dialog-head\"", "class=\"dialog-body"), StringComparison.Ordinal);
        Assert.Contains(">Close<", notificationsDialog[notificationsDialog.IndexOf("class=\"dialog-foot\"", StringComparison.Ordinal)..], StringComparison.Ordinal);
        // The page counted the unread rows and drew none of them.
        Assert.Equal(1, notifications.CountCalls);
        Assert.Equal(0, notifications.ListCalls);
        Assert.DoesNotContain("data-notification=", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">AI Proposal ready<", html, StringComparison.Ordinal);

        // Opening the bell asks for the list, whose forms return to the page it was opened on.
        using var list = await client.GetAsync("/Notifications?handler=List&returnUrl=%2FSearch%3Fquery%3Dabc");
        list.EnsureSuccessStatusCode();
        var listHtml = await list.Content.ReadAsStringAsync();
        Assert.Contains(">AI Proposal ready<", listHtml, StringComparison.Ordinal);
        Assert.Contains(">Assigned to you<", listHtml, StringComparison.Ordinal);
        Assert.Contains(">Query received<", listHtml, StringComparison.Ordinal);
        Assert.Contains("MA59BDY", listHtml, StringComparison.Ordinal);
        Assert.Contains($">{OperatorLabels.Shell.Unread}<", listHtml, StringComparison.Ordinal);
        Assert.Contains($"data-notification=\"{unreadEstimate.Id:D}\"", listHtml, StringComparison.Ordinal);
        Assert.Contains("row-button row-button--unread", listHtml, StringComparison.Ordinal);
        Assert.Contains("name=\"returnUrl\" value=\"/Search?query=abc\"", listHtml, StringComparison.Ordinal);
        // The vendor never appears in operator copy.
        Assert.DoesNotContain("Claude", listHtml, StringComparison.Ordinal);
        // The list is the store's order: newest first, exactly as returned.
        Assert.True(
            listHtml.IndexOf("AI Proposal ready", StringComparison.Ordinal)
                < listHtml.IndexOf("Assigned to you", StringComparison.Ordinal),
            "The bell must keep the store's newest-first order.");

        // A return address that is not local falls back to the Work Centre.
        using var foreign = await client.GetAsync("/Notifications?handler=List&returnUrl=https%3A%2F%2Fexample.test%2F");
        Assert.Contains("name=\"returnUrl\" value=\"/\"", await foreign.Content.ReadAsStringAsync(), StringComparison.Ordinal);
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
    public async Task NotificationsQueryOpensTheDialogOnLoadWithTheListDrawnByThePage()
    {
        var notifications = new StubNotifications([
            new StaffNotification(
                Guid.NewGuid(), Pegasus.Web.Authentication.DevelopmentOfflineIdentity.AdministratorId, Guid.NewGuid(),
                "QDOS26214", "MA59BDY", StaffNotificationCause.CaseAssigned,
                "/Cases/x", new DateTimeOffset(2031, 5, 6, 10, 0, 0, TimeSpan.Zero), ReadAtUtc: null)]);
        using var host = FactoryWith(notifications);
        using var client = host.CreateClient();

        var html = await client.GetStringAsync("/?notifications=1");

        Assert.Contains("data-dialog=\"notifications-dialog\" data-dialog-open-on-load=\"true\"", html, StringComparison.Ordinal);
        // The dialog opens on load, so the page reads the list once and counts its unread rows.
        Assert.Contains(">Assigned to you<", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"bell-count\" aria-hidden=\"true\">1</span>", html, StringComparison.Ordinal);
        Assert.Equal(1, notifications.ListCalls);
        Assert.Equal(0, notifications.CountCalls);
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

    /// <summary>
    /// The bell's list handler fails as the page's own bell read does: the
    /// request answers 200 with the dialog's unavailable wording, nothing of
    /// the fault, and no unhandled exception.
    /// </summary>
    [Fact]
    public async Task AFailedBellListAnswersTheUnavailableStateAndNotAnError()
    {
        using var host = FactoryWith(new UnavailableNotifications());
        using var client = host.CreateClient();

        using var response = await client.GetAsync("/Notifications?handler=List&returnUrl=%2FSearch");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(OperatorLabels.Shell.NotificationsUnavailable, html, StringComparison.Ordinal);
        Assert.DoesNotContain(OperatorLabels.Shell.NoNotifications, html, StringComparison.Ordinal);
        Assert.DoesNotContain("sensitive store failure", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("bell")]
    [InlineData("release note")]
    public async Task ACommandCancelledByTheCallerLeavingPropagatesFromAShellReadAndLogsNothing(string read)
    {
        using var aborted = new CancellationTokenSource();
        aborted.Cancel();
        var logger = new CapturingLogger<ShellPageFilter>();
        IMyStaffNotifications notifications = read == "bell"
            ? new CancelledCommandNotifications()
            : new StubNotifications([]);
        IMyReleaseNotes releaseNotes = read == "bell"
            ? new NoReleaseNotes()
            : new CancelledCommandReleaseNotes();

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RunShellFilterAsync(notifications, releaseNotes, logger, aborted.Token));

        Assert.Equal(CancelledCommandWording, thrown.Message);
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task TheSameCancelledCommandOnALiveRequestIsStillAnUnavailableBell()
    {
        var logger = new CapturingLogger<ShellPageFilter>();

        var page = await RunShellFilterAsync(
            new CancelledCommandNotifications(), new NoReleaseNotes(), logger, CancellationToken.None);

        Assert.True(page.ViewData.ContainsKey("NotificationsUnavailable"));
        Assert.Equal(["The notification query is unavailable."], logger.Entries.Select(e => e.Message).ToArray());
    }

    /// <summary>
    /// Runs the shell filter around a handler that returns its page, for a
    /// signed-in person whose request token is <paramref name="requestAborted"/>.
    /// </summary>
    private static Task<ShellProbePage> RunShellFilterAsync(
        IMyStaffNotifications notifications,
        IMyReleaseNotes releaseNotes,
        ILogger<ShellPageFilter> logger,
        CancellationToken requestAborted) =>
        RunShellFilterAsync(
            notifications, releaseNotes, logger, new HandlerMethodDescriptor(), new ShellProbePage(),
            () => new PageResult(), method: "GET", requestAborted);

    private static async Task<ShellProbePage> RunShellFilterAsync(
        IMyStaffNotifications notifications,
        IMyReleaseNotes releaseNotes,
        ILogger<ShellPageFilter> logger,
        HandlerMethodDescriptor? handler,
        ShellProbePage page,
        Func<IActionResult> handlerResult,
        string method,
        CancellationToken requestAborted)
    {
        var person = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, nameof(StaffRole.User))
            ],
            authenticationType: "Test"));
        var http = new DefaultHttpContext { User = person, RequestAborted = requestAborted };
        http.Request.Method = method;
        var pageContext = new PageContext(new Microsoft.AspNetCore.Mvc.ActionContext(
            http, new Microsoft.AspNetCore.Routing.RouteData(), new CompiledPageActionDescriptor()))
        {
            ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
        };
        page.PageContext = pageContext;
        var filter = new ShellPageFilter(notifications, releaseNotes, TimeProvider.System, logger);
        var executing = new PageHandlerExecutingContext(
            pageContext,
            [],
            handler!,
            new Dictionary<string, object?>(),
            page);

        await filter.OnPageHandlerExecutionAsync(
            executing,
            () => Task.FromResult(new PageHandlerExecutedContext(
                pageContext, [], handler!, page)
            {
                Result = handlerResult()
            }));
        return page;
    }

    /// <summary>
    /// A full-page GET starts the shell's reads before its handler and a POST,
    /// a named handler or a page whose GET never renders the shell does not:
    /// the order the handler and the bell are touched in is the evidence (R347).
    /// </summary>
    [Theory]
    [InlineData("get-page", "GET", null, false, "page", "bell,handler")]
    [InlineData("get-redirect", "GET", null, false, "redirect", "bell,handler")]
    [InlineData("post-page", "POST", null, false, "page", "handler,bell")]
    [InlineData("named-get-page", "GET", "Section", false, "page", "handler,bell")]
    [InlineData("named-get-redirect", "GET", "Section", false, "redirect", "handler")]
    [InlineData("shell-free-get", "GET", null, true, "redirect", "handler")]
    [InlineData("shell-free-get-page", "GET", null, true, "page", "handler")]
    [InlineData("shell-free-post", "POST", "Open", true, "redirect", "handler")]
    public async Task OnlyAnUnnamedGetStartsTheShellReadsBeforeTheHandler(
        string name, string method, string? handlerName, bool shellFree, string result, string expectedOrder)
    {
        var events = new List<string>();
        var notifications = new StubNotifications([], events);
        var descriptor = new HandlerMethodDescriptor { HttpMethod = method, Name = handlerName };
        ShellProbePage page = shellFree ? new ShellFreeProbePage() : new ShellProbePage();

        await RunShellFilterAsync(
            notifications, new NoReleaseNotes(), new CapturingLogger<ShellPageFilter>(),
            descriptor, page,
            () =>
            {
                events.Add("handler");
                return result == "page" ? new PageResult() : new RedirectResult("/");
            },
            method, CancellationToken.None);

        Assert.True(expectedOrder == string.Join(",", events), $"{name}: the handler and the bell ran as {string.Join(",", events)}.");
        // A page result is given the count; a redirect is given nothing and its reads were drained;
        // a shell-free GET reads nothing even when it returns a page.
        Assert.Equal(
            result == "page" && !(shellFree && handlerName is null),
            page.ViewData.ContainsKey("NotificationsUnread"));
    }

    [Fact]
    public async Task AGetToAPageWithNoHandlerStartsTheShellReadsBeforeItRenders()
    {
        var events = new List<string>();

        await RunShellFilterAsync(
            new StubNotifications([], events), new NoReleaseNotes(), new CapturingLogger<ShellPageFilter>(),
            handler: null, new ShellProbePage(),
            () =>
            {
                events.Add("handler");
                return new PageResult();
            },
            method: "GET", CancellationToken.None);

        Assert.Equal("bell,handler", string.Join(",", events));
    }

    /// <summary>
    /// The Account dialog's body holds Release notes, Report a problem and
    /// Change password; its foot keeps Close and Sign out (v41 S1, signed off
    /// 19 October 2026).
    /// </summary>
    [Fact]
    public async Task TheAccountDialogHoldsItsActionsInTheBodyAndItsFootKeepsCloseAndSignOut()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        var html = await client.GetStringAsync("/Search");

        var dialog = Between(html, "data-dialog=\"account-dialog\"", "data-dialog=\"notifications-dialog\"");
        var body = Between(dialog, "class=\"dialog-body\"", "class=\"dialog-foot\"");
        var foot = dialog[dialog.IndexOf("class=\"dialog-foot\"", StringComparison.Ordinal)..];
        Assert.Contains("href=\"/ReleaseNotes\"", body, StringComparison.Ordinal);
        Assert.Contains("data-dialog-open=\"problem-dialog\"", body, StringComparison.Ordinal);
        Assert.Contains("href=\"/Account/PasswordChange\"", body, StringComparison.Ordinal);
        Assert.Contains("data-dialog-close", foot, StringComparison.Ordinal);
        Assert.Contains(">Sign out<", foot, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Count(foot, "<button "));
    }

    /// <summary>
    /// The command palette's Cases icon is the rail's Cases icon: one glyph
    /// means one thing everywhere (v41 S3).
    /// </summary>
    [Fact]
    public async Task TheCommandPaletteCasesIconMatchesTheRailsCasesIcon()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        var html = await client.GetStringAsync("/Search");

        Assert.Matches("href=\"/Cases\"[^>]*>\\s*<svg[^>]*><use href=\"[^\"]*#icon-list-checks\"", html);
        Assert.Matches("data-route=\"/Cases\">\\s*<svg[^>]*><use href=\"[^\"]*#icon-list-checks\"", html);
    }

    /// <summary>The Not found page's one action is the primary button (v41 S7).</summary>
    [Fact]
    public async Task TheNotFoundPagesOneActionIsThePrimaryButton()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        using var response = await client.GetAsync("/status/404");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains(
            $"<a class=\"btn btn--primary\" href=\"/\">Return to {OperatorLabels.Nav.WorkCentre}</a>",
            html,
            StringComparison.Ordinal);
    }

    /// <summary>The markup between two markers; the end marker's position must follow the start's.</summary>
    private static string Between(string html, string start, string end)
    {
        var from = html.IndexOf(start, StringComparison.Ordinal);
        Assert.True(from >= 0, $"'{start}' is not on the page.");
        var to = html.IndexOf(end, from + start.Length, StringComparison.Ordinal);
        Assert.True(to > from, $"'{end}' does not follow '{start}'.");
        return html[from..to];
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

        public Task<int> UnreadCountAsync(ActionActor actor, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("sensitive store failure");

        public Task<StaffNotification?> OpenAsync(ActionActor actor, Guid notificationId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("sensitive store failure");

        public Task<int> MarkAllReadAsync(ActionActor actor, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("sensitive store failure");
    }

    /// <summary>What SqlClient's cancelled command says, as the exception a store raises.</summary>
    private const string CancelledCommandWording = "Operation cancelled by user.";

    private class ShellProbePage : PageModel;

    private sealed class ShellFreeProbePage : ShellProbePage, IShellFreeGet;

    private sealed class CancelledCommandNotifications : IMyStaffNotifications
    {
        public Task<IReadOnlyList<StaffNotification>> ListAsync(ActionActor actor, CancellationToken cancellationToken) =>
            throw new InvalidOperationException(CancelledCommandWording);

        public Task<int> UnreadCountAsync(ActionActor actor, CancellationToken cancellationToken) =>
            throw new InvalidOperationException(CancelledCommandWording);

        public Task<StaffNotification?> OpenAsync(ActionActor actor, Guid notificationId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<int> MarkAllReadAsync(ActionActor actor, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class CancelledCommandReleaseNotes : IMyReleaseNotes
    {
        public Task<ReleaseNote?> GetUnacknowledgedAsync(ActionActor actor, CancellationToken cancellationToken) =>
            throw new InvalidOperationException(CancelledCommandWording);

        public Task AcknowledgeAsync(ActionActor actor, Guid noteId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<ReleaseNote>> ListPublishedAsync(ActionActor actor, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class StubNotifications(
        IReadOnlyList<StaffNotification> rows,
        List<string>? events = null) : IMyStaffNotifications
    {
        private readonly List<StaffNotification> _rows = [.. rows];

        public List<Guid> Opened { get; } = [];

        public int MarkAllReadCalls { get; private set; }

        public int ListCalls { get; private set; }

        public int CountCalls { get; private set; }

        public Task<IReadOnlyList<StaffNotification>> ListAsync(ActionActor actor, CancellationToken cancellationToken)
        {
            ListCalls++;
            events?.Add("bell");
            return Task.FromResult<IReadOnlyList<StaffNotification>>(_rows.ToArray());
        }

        public Task<int> UnreadCountAsync(ActionActor actor, CancellationToken cancellationToken)
        {
            CountCalls++;
            events?.Add("bell");
            return Task.FromResult(_rows.Count(row => row.IsUnread));
        }

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
