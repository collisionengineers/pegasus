using System.Globalization;
using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Core.PhotoCapture;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;
using Pegasus.Web.Presentation;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The staff pages of guided photo capture after the product owner's answers
/// of 10 November 2026: standing link changes write action history (R846),
/// the "Contact inactive" chip (R865), the Website
/// link panel's own pause words (R867), the four labels (R869), one
/// "Reminded · name" line per reminder (R870), and the refusal sentences
/// shown beside their forms (R868); and after the v50 design round: the
/// website link on Administration › Photo links (R911), the rows' acts and
/// More (R912), the link page's header (R913), Resend as the one act that
/// sends a link again (R914) and the composer every photo-link e-mail goes
/// through (R915).
/// </summary>
public sealed partial class PhotoCaptureWebTests
{
    /// <summary>The Action logs period around the test clock, in office time.</summary>
    private const string LogsPeriod = "From=2031-05-05T00:00&To=2031-05-08T00:00";

    /// <summary>
    /// Every change to a contact's standing link, creating it included,
    /// writes one action history event filed under the contact, naming who
    /// made it under the form's operation key with before and after holding
    /// only what changed; the same form sent again writes nothing and changes
    /// nothing; Administration › Logs shows each in the operator's words
    /// (FRD-04; R846).
    /// </summary>
    [Fact]
    public async Task EachContactLinkChangeWritesOneHistoryEventAndARepeatedKeyWritesNothing()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var organizationId = await QdosOrganizationIdAsync(factory);
        var contactPage = $"/Administration/Contacts/Edit/{organizationId:D}";
        var keys = new Dictionary<string, string>(StringComparer.Ordinal);

        async Task PostTwiceAsync(
            string handler,
            string act,
            StandingPhotoLink? link,
            Dictionary<string, string>? fields = null,
            bool repeat = true)
        {
            var key = NewOperationKey();
            keys[act] = key;
            var form = fields is null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(fields, StringComparer.Ordinal);
            form["PhotoLinkOperationKey"] = key;
            if (link is not null)
            {
                form["StandingLinkVersion"] = link.Version.ToString(CultureInfo.InvariantCulture);
            }

            // The second post is the same form sent again, over the same read.
            for (var attempt = 0; attempt < (repeat ? 2 : 1); attempt++)
            {
                using var posted = await PostStaffFormAsync(client, $"{contactPage}?handler={handler}", form);
                Assert.Equal(HttpStatusCode.Redirect, posted.StatusCode);
            }
        }

        await PostTwiceAsync("CreatePhotoLink", StandingPhotoLinkActs.Created, link: null);
        var created = await ContactLinkAsync(factory, organizationId);
        await PostTwiceAsync("PausePhotoLink", StandingPhotoLinkActs.Paused, created);
        var paused = await StandingLinkAsync(factory, created.Id);
        Assert.Equal(created.Version + 1, paused.Version);
        await PostTwiceAsync("ResumePhotoLink", StandingPhotoLinkActs.Resumed, paused);
        var resumed = await StandingLinkAsync(factory, created.Id);
        await PostTwiceAsync("ReplacePhotoLink", StandingPhotoLinkActs.Replaced, resumed);
        var replaced = await StandingLinkAsync(factory, created.Id);
        await PostTwiceAsync("UpdatePhotoLinkDefaults", StandingPhotoLinkActs.DefaultsSaved, replaced, new()
        {
            ["LinkForwardedMode"] = nameof(PhotoCaptureMode.Quick),
            ["LinkLiveCameraOnly"] = "false",
            ["LinkLifetimeDays"] = "14",
            ["LinkOriginalsInBackground"] = replaced.OriginalsInBackground ? "true" : "false"
        });
        var defaults = await StandingLinkAsync(factory, created.Id);
        await PostTwiceAsync("AddPhotoLinkAddress", StandingPhotoLinkActs.AddressAdded, defaults, new()
        {
            ["SavedAddressName"] = "Back yard",
            ["SavedAddressText"] = "1 Example Street, Exampleton"
        });
        var added = await StandingLinkAsync(factory, created.Id);
        var saved = Assert.Single((await AddressBookAsync(factory, created.Id)).Saved);
        await PostTwiceAsync("ChangePhotoLinkAddress", StandingPhotoLinkActs.AddressChanged, added, new()
        {
            ["SavedAddressId"] = saved.Id.ToString("D"),
            ["SavedAddressName"] = "Front yard",
            ["SavedAddressText"] = "1 Example Street, Exampleton"
        });
        var changed = await StandingLinkAsync(factory, created.Id);
        await PostTwiceAsync("RemovePhotoLinkAddress", StandingPhotoLinkActs.AddressRemoved, changed, new()
        {
            ["SavedAddressId"] = saved.Id.ToString("D")
        });
        var removed = await StandingLinkAsync(factory, created.Id);
        // A retired link is no longer the contact's, so its form is sent once.
        await PostTwiceAsync("RetirePhotoLink", StandingPhotoLinkActs.Retired, removed, repeat: false);

        // Each act moved the link on once, whichever form was sent twice.
        var retired = await StandingLinkAsync(factory, created.Id);
        Assert.Equal(created.Version + 8, retired.Version);
        Assert.NotNull(retired.RetiredAtUtc);
        Assert.Empty((await AddressBookAsync(factory, created.Id)).Saved);

        var events = await StandingLinkHistoryAsync(factory, EfStandingPhotoLinkStore.ContactHistoryArea, organizationId.ToString("D"));
        Assert.Equal(9, events.Length);
        foreach (var (act, key) in keys)
        {
            var recorded = Assert.Single(events, item => item.EventKind == act);
            Assert.Equal(key, recorded.CorrelationId);
            Assert.Equal(nameof(ActorKind.Staff), recorded.ActorKind);
            Assert.Equal(DevelopmentOfflineIdentity.AdministratorId, Guid.Parse(recorded.ActorSubjectId));
            Assert.Equal(IntakeWebApplicationFactory.FixedUtcNow, recorded.OccurredAtUtc);
        }

        AssertFields(events, StandingPhotoLinkActs.Created, before: "", after: "slug forwardedMode liveCameraOnly linkLifetimeDays originalsInBackground");
        AssertFields(events, StandingPhotoLinkActs.Paused, before: "", after: "pausedAtUtc");
        AssertFields(events, StandingPhotoLinkActs.Resumed, before: "pausedAtUtc", after: "");
        AssertFields(events, StandingPhotoLinkActs.Replaced, before: "slug", after: "slug");
        AssertFields(events, StandingPhotoLinkActs.DefaultsSaved, before: "forwardedMode liveCameraOnly linkLifetimeDays", after: "forwardedMode liveCameraOnly linkLifetimeDays");
        AssertFields(events, StandingPhotoLinkActs.AddressAdded, before: "", after: "name address");
        AssertFields(events, StandingPhotoLinkActs.AddressChanged, before: "name", after: "name");
        AssertFields(events, StandingPhotoLinkActs.AddressRemoved, before: "name address", after: "");
        AssertFields(events, StandingPhotoLinkActs.Retired, before: "", after: "retiredAtUtc");
        var replacedEvent = Assert.Single(events, item => item.EventKind == StandingPhotoLinkActs.Replaced);
        Assert.Equal(resumed.Slug, FieldText(replacedEvent.BeforeJson, "slug"));
        Assert.Equal(replaced.Slug, FieldText(replacedEvent.AfterJson, "slug"));
        var defaultsEvent = Assert.Single(events, item => item.EventKind == StandingPhotoLinkActs.DefaultsSaved);
        Assert.Equal(PhotoCaptureCodes.ToCode(PhotoCaptureMode.Quick), FieldText(defaultsEvent.AfterJson, "forwardedMode"));

        var logs = await IntakeWebDriver.GetHtmlAsync(client, $"/Administration/Logs?{LogsPeriod}&Area={EfStandingPhotoLinkStore.ContactHistoryArea}");
        foreach (var name in new[]
                 {
                     "Photo link created", "Photo link paused", "Photo link resumed", "Photo link replaced",
                     "Photo link retired", "Photo link defaults saved", "Photo link address added",
                     "Photo link address changed", "Photo link address removed"
                 })
        {
            Assert.Contains(">Contact<", LogRow(logs, name), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The website link has no contact: its events sit under the area
    /// "Website photo link", filed under the link itself, and the same create
    /// form sent again writes nothing (R846).
    /// </summary>
    [Fact]
    public async Task TheWebsiteLinksChangesAreRecordedUnderWebsitePhotoLink()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var createKey = NewOperationKey();
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var created = await PostStaffFormAsync(
                client, WebsiteLinkPage + "?handler=CreateWebsiteLink", createKey, "WebsiteSlug", "ce-vehicle-photos");
            Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        }

        var website = await WebsiteLinkAsync(factory)
            ?? throw new InvalidOperationException("The website link was not created.");
        var pauseKey = NewOperationKey();
        using (var paused = await PostStaffFormAsync(client, WebsiteLinkPage + "?handler=PauseWebsiteLink", new Dictionary<string, string>
        {
            ["OperationKey"] = pauseKey,
            ["ExpectedVersion"] = website.Version.ToString(CultureInfo.InvariantCulture)
        }))
        {
            Assert.Equal(HttpStatusCode.Redirect, paused.StatusCode);
        }

        var events = await StandingLinkHistoryAsync(factory, EfStandingPhotoLinkStore.WebsiteHistoryArea, website.Id.ToString("D"));
        Assert.Equal(2, events.Length);
        Assert.Equal(createKey, Assert.Single(events, item => item.EventKind == StandingPhotoLinkActs.Created).CorrelationId);
        Assert.Equal(pauseKey, Assert.Single(events, item => item.EventKind == StandingPhotoLinkActs.Paused).CorrelationId);

        var logs = await IntakeWebDriver.GetHtmlAsync(client, $"/Administration/Logs?{LogsPeriod}&Area={EfStandingPhotoLinkStore.WebsiteHistoryArea}");
        Assert.Contains(">Website photo link<", LogRow(logs, "Photo link created"), StringComparison.Ordinal);
        Assert.Contains(">Website photo link<", LogRow(logs, "Photo link paused"), StringComparison.Ordinal);
    }

    /// <summary>
    /// An inactive contact's standing link carries the amber chip "Contact
    /// inactive", paused or not; an active contact's reads Active or Paused
    /// as before (R865).
    /// </summary>
    [Fact]
    public async Task AnInactiveContactsLinkReadsContactInactiveEvenWhenPaused()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var link = await StandingCaptureDriver.CreateContactLinkAsync(factory);
        var organizationId = link.OrganizationId
            ?? throw new InvalidOperationException("A contact's link names no contact.");
        var tab = $"/Administration/Contacts/Edit/{organizationId:D}?tab=photo-links";
        const string inactiveChip = "<span class=\"status status--amber\">Contact inactive</span>";

        Assert.Contains(
            "<span class=\"status status--green\">Active</span>",
            StandingPanel(await IntakeWebDriver.GetHtmlAsync(client, tab)),
            StringComparison.Ordinal);

        await SetContactActiveAsync(factory, organizationId, active: false);
        var inactive = StandingPanel(await IntakeWebDriver.GetHtmlAsync(client, tab));
        Assert.Contains(inactiveChip, inactive, StringComparison.Ordinal);
        Assert.DoesNotContain(">Active</span>", inactive, StringComparison.Ordinal);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<StandingPhotoLinkAdministration>()
                .PauseAsync(Issuer, NewOperationKey(), link.Id, link.Version, CancellationToken.None);
        }

        var pausedAndInactive = StandingPanel(await IntakeWebDriver.GetHtmlAsync(client, tab));
        Assert.Contains(inactiveChip, pausedAndInactive, StringComparison.Ordinal);
        Assert.DoesNotContain(">Paused</span>", pausedAndInactive, StringComparison.Ordinal);

        await SetContactActiveAsync(factory, organizationId, active: true);
        Assert.Contains(
            "<span class=\"status status--amber\">Paused</span>",
            StandingPanel(await IntakeWebDriver.GetHtmlAsync(client, tab)),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The Website link panel says "Pause the link" and "Resume the link"; a
    /// contact's panel keeps "Pause their link" (R867). Its address box has
    /// the visible label "Website address", and Retire works as "Retiring…"
    /// on both panels (R869).
    /// </summary>
    [Fact]
    public async Task TheWebsitePanelHasItsOwnWordsAndBothPanelsTheirLabels()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        const string addressLabel = "<label for=\"website-address\">Website address</label>";
        var retiring = $"data-busy-label=\"{HtmlEncoder.Default.Encode("Retiring…")}\"";

        var empty = await IntakeWebDriver.GetHtmlAsync(client, WebsiteLinkPage);
        Assert.Contains(addressLabel, empty, StringComparison.Ordinal);
        Assert.Contains("id=\"website-address\" name=\"WebsiteSlug\"", empty, StringComparison.Ordinal);

        using (var created = await PostStaffFormAsync(
            client, WebsiteLinkPage + "?handler=CreateWebsiteLink", NewOperationKey(), "WebsiteSlug", "ce-vehicle-photos"))
        {
            Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        }

        var active = await IntakeWebDriver.GetHtmlAsync(client, WebsiteLinkPage);
        Assert.Contains("Pause the link", active, StringComparison.Ordinal);
        Assert.DoesNotContain("Pause their link", active, StringComparison.Ordinal);
        Assert.Contains(addressLabel, active, StringComparison.Ordinal);
        Assert.Contains(retiring, active, StringComparison.Ordinal);

        var website = await WebsiteLinkAsync(factory)
            ?? throw new InvalidOperationException("The website link was not created.");
        using (var paused = await PostStaffFormAsync(client, WebsiteLinkPage + "?handler=PauseWebsiteLink", new Dictionary<string, string>
        {
            ["OperationKey"] = NewOperationKey(),
            ["ExpectedVersion"] = website.Version.ToString(CultureInfo.InvariantCulture)
        }))
        {
            Assert.Equal(HttpStatusCode.Redirect, paused.StatusCode);
        }

        var pausedPage = await IntakeWebDriver.GetHtmlAsync(client, WebsiteLinkPage);
        Assert.Contains("Resume the link", pausedPage, StringComparison.Ordinal);
        Assert.DoesNotContain("Resume their link", pausedPage, StringComparison.Ordinal);

        var contactLink = await StandingCaptureDriver.CreateContactLinkAsync(factory);
        var contactId = contactLink.OrganizationId
            ?? throw new InvalidOperationException("A contact's link names no contact.");
        var contactTab = await IntakeWebDriver.GetHtmlAsync(
            client, $"/Administration/Contacts/Edit/{contactId:D}?tab=photo-links");
        Assert.Contains("Pause their link", contactTab, StringComparison.Ordinal);
        Assert.Contains(retiring, contactTab, StringComparison.Ordinal);
    }

    /// <summary>
    /// A link's History shows one "Reminded · name" line for each reminder
    /// (R870), folded in its Summary (v51 G; R948).
    /// </summary>
    [Fact]
    public async Task EachReminderIsItsOwnHistoryLineWithItsSender()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        var link = await IssueLinkAsync(factory, caseId);
        var page = $"/PhotoCapture/{link.Capture.Id:D}";
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var actions = scope.ServiceProvider.GetRequiredService<PhotoLinkActions>();
            for (var reminder = 0; reminder < 2; reminder++)
            {
                await actions.ReissueAsync(
                    Issuer, link.Capture.Id, PhotoLinkTokenPurpose.Copy, isReminder: true, NewOperationKey(), CancellationToken.None);
            }
        }

        Assert.Equal(2, (await StandingCaptureDriver.CaptureAsync(factory, link.Capture.Id)).ReminderCount);
        var name = await StaffNameAsync(factory, DevelopmentOfflineIdentity.AdministratorId);
        var details = await IntakeWebDriver.GetHtmlAsync(client, page);
        var line = HtmlEncoder.Default.Encode($"Reminded · {name}");
        Assert.Equal(2, Regex.Count(details, Regex.Escape(line)));
    }

    /// <summary>
    /// Remind is gone as an act of its own (R914): neither the link's page nor
    /// Links we sent offers it. A link due a reminder leads its page with
    /// Resend, which opens the composer and returns to the page (R913, R915).
    /// </summary>
    [Fact]
    public async Task NeitherTheLinksPageNorTheListOffersRemind()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        var link = await IssueLinkAsync(factory, caseId);
        await using (var db = await (await factory.DatabaseAsync).CreateContextAsync())
        {
            await db.PhotoCaptures.Where(value => value.Id == link.Capture.Id).ExecuteUpdateAsync(update => update
                .SetProperty(value => value.CreatedAtUtc, IntakeWebApplicationFactory.FixedUtcNow.AddDays(-2)));
        }

        var details = await IntakeWebDriver.GetHtmlAsync(client, $"/PhotoCapture/{link.Capture.Id:D}");
        var list = await IntakeWebDriver.GetHtmlAsync(client, "/PhotoCapture?scope=all");

        foreach (var html in new[] { details, list })
        {
            Assert.DoesNotContain("handler=Remind", html, StringComparison.Ordinal);
            Assert.DoesNotContain(">Remind<", html, StringComparison.Ordinal);
        }

        Assert.Contains(
            $"<a class=\"btn btn--primary\" href=\"/PhotoCapture/{link.Capture.Id:D}/Resend?returnUrl=%2FPhotoCapture%2F{link.Capture.Id:D}\" data-photo-link-composer>",
            details,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The composer (R915) opened from Links we sent draws From, the link's
    /// address in To, the subject and the standard Photo link wording with
    /// the link still a placeholder (R806). Send by e-mail makes the link,
    /// sends the message as staff and returns to the list with "Link sent to
    /// … by e-mail"; a fresh link with no reminder due is not counted as one
    /// (R914).
    /// </summary>
    [Fact]
    public async Task TheComposerSendsByEmailAndReturnsWithTheNotice()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(baseFactory);
        await SeedCaseRegistrationAsync(baseFactory, caseId, "AB12 CDE");
        await SeedSendableMailboxAsync(baseFactory);
        var link = await IssueLinkToAsync(baseFactory, caseId, "Sam Taylor", "sam.taylor@example.com");
        var mail = new SentStaffMailSend();
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            CaseWebTestSupport.Substitute<Pegasus.Core.Operations.IStaffMailSend>(services, mail)));
        using var client = IntakeWebDriver.CreateClient(factory);
        var listPath = "/PhotoCapture?scope=all";
        var composer = $"/PhotoCapture/{link.Capture.Id:D}/Resend";

        var html = await IntakeWebDriver.GetHtmlAsync(client, $"{composer}?returnUrl={Uri.EscapeDataString(listPath)}");

        Assert.Contains($">{OperatorLabels.PhotoCapture.Resend}</h1>", html, StringComparison.Ordinal);
        var from = Regex.Match(html, "<option value=\"(?<id>[^\"]+)\"[^>]*>photo-link-sender@collisionengineers.co.uk</option>");
        Assert.True(from.Success, "From does not offer the sending mailbox.");
        Assert.Matches("<input id=\"photo-link-compose-to\" type=\"text\" value=\"sam.taylor@example.com\" readonly", html);
        var subject = InputValue(html, "Subject");
        Assert.Equal("Photos of AB12 CDE for Collision Engineers", subject);
        var body = TextareaValue(html, "Body");
        Assert.Contains("Collision Engineers need a few photos of AB12 CDE to assess the damage.", body, StringComparison.Ordinal);
        Assert.Contains(OperatorLabels.PhotoCapture.PhotoLinkPlaceholder, body, StringComparison.Ordinal);
        Assert.Contains(OperatorLabels.PhotoCapture.CreateLinkToCopy, html, StringComparison.Ordinal);
        Assert.Contains(HtmlEncoder.Default.Encode(OperatorLabels.PhotoCapture.SendByEmail), html, StringComparison.Ordinal);
        Assert.Empty(mail.Commands);

        using (var sent = await PostStaffFormAsync(client, composer, new Dictionary<string, string>
        {
            ["OperationKey"] = InputValue(html, "OperationKey"),
            ["ReturnUrl"] = listPath,
            ["FromMailboxId"] = from.Groups["id"].Value,
            ["Subject"] = subject,
            ["Body"] = body,
            ["StandardBody"] = body,
            ["Submit"] = Pegasus.Web.Pages.PhotoCapture.ResendModel.ByEmail
        }))
        {
            Assert.Equal(HttpStatusCode.Redirect, sent.StatusCode);
            Assert.Equal(listPath, sent.Headers.Location?.OriginalString);
        }

        var command = Assert.Single(mail.Commands);
        Assert.Equal("sam.taylor@example.com", Assert.Single(command.To).Address);
        Assert.Equal(subject, command.Subject);
        Assert.Equal(caseId, command.ContextId);
        Assert.DoesNotContain(OperatorLabels.PhotoCapture.PhotoLinkPlaceholder, command.Body, StringComparison.Ordinal);
        Assert.Contains("/c/", command.Body, StringComparison.Ordinal);
        Assert.Contains(
            $"<span>{OperatorLabels.PhotoCapture.LinkSentTo("Sam Taylor")}</span>",
            await IntakeWebDriver.GetHtmlAsync(client, listPath),
            StringComparison.Ordinal);
        Assert.Equal(0, (await StandingCaptureDriver.CaptureAsync(baseFactory, link.Capture.Id)).ReminderCount);
    }

    /// <summary>
    /// A photo link e-mail whose provider answer is ambiguous (a Graph server
    /// error or a timeout, which the engine records Unknown and throws) keeps
    /// staff on Send a photo link with the link, "The send result is not yet
    /// known." and its Unknown Send status, never the error page (R1123).
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SendByEmailWithAnUnknownOutcomeShowsTheLinkAndItsStatus(bool timedOut)
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(baseFactory);
        await SeedCaseRegistrationAsync(baseFactory, caseId, "AB12 CDE");
        await SeedSendableMailboxAsync(baseFactory);
        var mail = new SentStaffMailSend { ProviderFault = AmbiguousProviderAnswer(timedOut) };
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            CaseWebTestSupport.Substitute<Pegasus.Core.Operations.IStaffMailSend>(services, mail)));
        using var client = IntakeWebDriver.CreateClient(factory);
        var send = $"/PhotoCapture/Send/{caseId:D}";
        var html = await IntakeWebDriver.GetHtmlAsync(client, send);
        var from = Regex.Match(html, "<option value=\"(?<id>[^\"]+)\"[^>]*>photo-link-sender@collisionengineers.co.uk</option>");
        Assert.True(from.Success, "From does not offer the sending mailbox.");
        var body = TextareaValue(html, "Body");

        using var posted = await PostStaffFormAsync(client, send, new Dictionary<string, string>
        {
            ["OperationKey"] = InputValue(html, "OperationKey"),
            ["CustomerName"] = "Sam Taylor",
            ["Email"] = "sam.taylor@example.com",
            ["Mode"] = nameof(PhotoCaptureMode.Guided),
            ["Days"] = "7",
            ["LiveCameraOnly"] = "true",
            ["OriginalsInBackground"] = "true",
            ["FromMailboxId"] = from.Groups["id"].Value,
            ["Subject"] = InputValue(html, "Subject"),
            ["Body"] = body,
            ["StandardBody"] = body,
            ["Submit"] = Pegasus.Web.Pages.PhotoCapture.SendModel.ByEmail
        });

        Assert.Equal(HttpStatusCode.OK, posted.StatusCode);
        var page = WebUtility.HtmlDecode(await posted.Content.ReadAsStringAsync());
        AssertUnknownSendShown(page);
        Assert.Single(mail.Commands);
    }

    /// <summary>
    /// The Resend composer's e-mail, its provider answer ambiguous, keeps
    /// staff on the composer with the link, the sentence and its Unknown Send
    /// status, never the error page (R1123).
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResendByEmailWithAnUnknownOutcomeShowsTheLinkAndItsStatus(bool timedOut)
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(baseFactory);
        await SeedCaseRegistrationAsync(baseFactory, caseId, "AB12 CDE");
        await SeedSendableMailboxAsync(baseFactory);
        var link = await IssueLinkToAsync(baseFactory, caseId, "Sam Taylor", "sam.taylor@example.com");
        var mail = new SentStaffMailSend { ProviderFault = AmbiguousProviderAnswer(timedOut) };
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            CaseWebTestSupport.Substitute<Pegasus.Core.Operations.IStaffMailSend>(services, mail)));
        using var client = IntakeWebDriver.CreateClient(factory);
        var listPath = "/PhotoCapture?scope=all";
        var composer = $"/PhotoCapture/{link.Capture.Id:D}/Resend";
        var html = await IntakeWebDriver.GetHtmlAsync(client, $"{composer}?returnUrl={Uri.EscapeDataString(listPath)}");
        var from = Regex.Match(html, "<option value=\"(?<id>[^\"]+)\"[^>]*>photo-link-sender@collisionengineers.co.uk</option>");
        Assert.True(from.Success, "From does not offer the sending mailbox.");
        var body = TextareaValue(html, "Body");

        using var posted = await PostStaffFormAsync(client, composer, new Dictionary<string, string>
        {
            ["OperationKey"] = InputValue(html, "OperationKey"),
            ["ReturnUrl"] = listPath,
            ["FromMailboxId"] = from.Groups["id"].Value,
            ["Subject"] = InputValue(html, "Subject"),
            ["Body"] = body,
            ["StandardBody"] = body,
            ["Submit"] = Pegasus.Web.Pages.PhotoCapture.ResendModel.ByEmail
        });

        Assert.Equal(HttpStatusCode.OK, posted.StatusCode);
        var page = WebUtility.HtmlDecode(await posted.Content.ReadAsStringAsync());
        AssertUnknownSendShown(page);
        Assert.Single(mail.Commands);
    }

    /// <summary>The link, the Unknown sentence and the Send status beside it, and no sent notice.</summary>
    private static void AssertUnknownSendShown(string page)
    {
        Assert.Contains("data-photo-link-issued", page, StringComparison.Ordinal);
        Assert.Contains(CaseWorkspaceLabels.ReportDelivery.SendUnknown, page, StringComparison.Ordinal);
        Assert.Contains(OperatorLabels.PhotoCapture.SendStatus, page, StringComparison.Ordinal);
        Assert.Contains(
            OperatorLabels.StaffMail.State(Pegasus.Core.Operations.StaffMailState.Unknown),
            page,
            StringComparison.Ordinal);
        Assert.DoesNotContain(OperatorLabels.PhotoCapture.LinkSentTo("Sam Taylor"), page, StringComparison.Ordinal);
    }

    /// <summary>What Graph answers that leaves a send's outcome unknown: a 503, or no answer in time.</summary>
    private static Exception AmbiguousProviderAnswer(bool timedOut) => timedOut
        ? new TaskCanceledException("The request timed out.", new TimeoutException())
        : new HttpRequestException("Microsoft Graph returned 503.", null, HttpStatusCode.ServiceUnavailable);

    /// <summary>
    /// Create link to copy on the composer makes a fresh link and shows it
    /// once, sending nothing; with no staff send composed the composer offers
    /// no Send by e-mail. A Case link with no reminder due is not counted as
    /// one (R914).
    /// </summary>
    [Fact]
    public async Task TheComposerMakesALinkToCopy()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        var link = await IssueLinkAsync(factory, caseId);
        var composer = $"/PhotoCapture/{link.Capture.Id:D}/Resend";

        var html = await IntakeWebDriver.GetHtmlAsync(client, composer);
        Assert.Contains(OperatorLabels.PhotoCapture.CreateLinkToCopy, html, StringComparison.Ordinal);
        Assert.DoesNotContain(HtmlEncoder.Default.Encode(OperatorLabels.PhotoCapture.SendByEmail), html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-photo-link-issued", html, StringComparison.Ordinal);

        using var copied = await PostStaffFormAsync(client, composer, new Dictionary<string, string>
        {
            ["OperationKey"] = NewOperationKey(),
            ["Submit"] = Pegasus.Web.Pages.PhotoCapture.ResendModel.ToCopy
        });

        Assert.Equal(HttpStatusCode.OK, copied.StatusCode);
        var page = await copied.Content.ReadAsStringAsync();
        Assert.Contains("data-photo-link-issued", page, StringComparison.Ordinal);
        Assert.Matches("<code id=\"photo-link-issued-url\" class=\"photo-link-url\">[^<]*/c/[^<]+</code>", page);
        Assert.Equal(0, (await StandingCaptureDriver.CaptureAsync(factory, link.Capture.Id)).ReminderCount);
    }

    /// <summary>
    /// Log call on a Links we sent row that needs a phone call returns to the
    /// list it was asked from with its notice (v50 P; R912).
    /// </summary>
    [Fact]
    public async Task LogCallFromTheListReturnsToTheList()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        var link = await IssueLinkAsync(factory, caseId);
        var now = IntakeWebApplicationFactory.FixedUtcNow;
        await using (var db = await (await factory.DatabaseAsync).CreateContextAsync())
        {
            await db.PhotoCaptures.Where(value => value.Id == link.Capture.Id).ExecuteUpdateAsync(update => update
                .SetProperty(value => value.CreatedAtUtc, now.AddDays(-3))
                .SetProperty(value => value.ReminderCount, PhotoCaptureStatus.RemindersBeforeCall)
                .SetProperty(value => value.LastRemindedAtUtc, (DateTimeOffset?)now.AddHours(-1)));
        }
        var listPath = "/PhotoCapture?scope=all";

        var row = ListRow(await IntakeWebDriver.GetHtmlAsync(client, listPath), link.Capture.Id);
        Assert.Contains(
            $">{OperatorLabels.PhotoCapture.State(PhotoCaptureState.NeedsCall)}</span>",
            row,
            StringComparison.Ordinal);
        Assert.Contains("handler=LogCall", row, StringComparison.Ordinal);
        Assert.Contains($"name=\"returnUrl\" value=\"{listPath}\"", row, StringComparison.Ordinal);

        using (var logged = await PostStaffFormAsync(
            client, $"/PhotoCapture/{link.Capture.Id:D}?handler=LogCall", NewOperationKey(), "returnUrl", listPath))
        {
            Assert.Equal(HttpStatusCode.Redirect, logged.StatusCode);
            Assert.Equal(listPath, logged.Headers.Location?.OriginalString);
        }

        var after = await IntakeWebDriver.GetHtmlAsync(client, listPath);
        Assert.Contains($"<span>{OperatorLabels.PhotoCapture.CallLogged}</span>", after, StringComparison.Ordinal);
        Assert.NotNull((await StandingCaptureDriver.CaptureAsync(factory, link.Capture.Id)).CallLoggedAtUtc);
    }

    /// <summary>
    /// A Direct links row carries at most the one act its state calls for, as
    /// its own button: Resend on a live link still waiting, with no More, no
    /// Cancel link and no View photos (v51 N; R948; R912). The list has no
    /// Sent by and no Next reminder column; a link with photos previews them.
    /// </summary>
    [Fact]
    public async Task ADirectLinksRowCarriesOneActAndNoMore()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        var unopened = await IssueLinkAsync(factory, caseId);
        var withPhotos = await IssueLinkAsync(factory, caseId);
        var now = IntakeWebApplicationFactory.FixedUtcNow;
        await using (var db = await (await factory.DatabaseAsync).CreateContextAsync())
        {
            await db.PhotoCaptures.Where(value => value.Id == withPhotos.Capture.Id).ExecuteUpdateAsync(update => update
                .SetProperty(value => value.OpenedAtUtc, (DateTimeOffset?)now.AddHours(-5))
                .SetProperty(value => value.AcceptedItemCount, 1));
        }

        var list = await IntakeWebDriver.GetHtmlAsync(client, "/PhotoCapture?scope=all");

        Assert.DoesNotContain($">{OperatorLabels.PhotoCapture.SentBy}</th>", list, StringComparison.Ordinal);
        Assert.DoesNotContain($">{OperatorLabels.PhotoCapture.NextReminder}</th>", list, StringComparison.Ordinal);
        foreach (var row in new[] { ListRow(list, unopened.Capture.Id), ListRow(list, withPhotos.Capture.Id) })
        {
            Assert.Contains($">{OperatorLabels.PhotoCapture.Resend}</a>", row, StringComparison.Ordinal);
            Assert.DoesNotContain("<details", row, StringComparison.Ordinal);
            Assert.DoesNotContain("handler=Cancel", row, StringComparison.Ordinal);
            Assert.DoesNotContain(OperatorLabels.PhotoCapture.ViewPhotos, row, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Replace link's address box is in its dialog on Photo links; a refused
    /// address reopens the dialog on load with the sentence beside the box
    /// (R868; v50 H) and the link keeps its address.
    /// </summary>
    [Fact]
    public async Task ARefusedReplaceAddressReopensItsDialog()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        StandingPhotoLink website;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            website = await scope.ServiceProvider.GetRequiredService<StandingPhotoLinkAdministration>()
                .CreateWebsiteAsync(Issuer, NewOperationKey(), "ce-vehicle-photos", CancellationToken.None);
        }

        Assert.Contains(
            "data-dialog=\"replace-website-link\" hidden",
            await IntakeWebDriver.GetHtmlAsync(client, WebsiteLinkPage),
            StringComparison.Ordinal);

        using (var refused = await PostStaffFormAsync(client, WebsiteLinkPage + "?handler=ReplaceWebsiteLink", new Dictionary<string, string>
        {
            ["OperationKey"] = NewOperationKey(),
            ["ExpectedVersion"] = website.Version.ToString(CultureInfo.InvariantCulture),
            ["WebsiteSlug"] = "Not a slug!"
        }))
        {
            Assert.Equal(HttpStatusCode.Redirect, refused.StatusCode);
            Assert.Equal(WebsiteLinkPage, refused.Headers.Location?.OriginalString);
        }

        var html = await IntakeWebDriver.GetHtmlAsync(client, WebsiteLinkPage);
        Assert.Contains("data-dialog=\"replace-website-link\" data-dialog-open-on-load=\"true\"", html, StringComparison.Ordinal);
        Assert.Contains(
            HtmlEncoder.Default.Encode("A website address is 6 to 60 lower-case letters, numbers and hyphens, and cannot start or end with a hyphen."),
            FormFor(html, "handler=ReplaceWebsiteLink"),
            StringComparison.Ordinal);
        Assert.Equal("ce-vehicle-photos", (await WebsiteLinkAsync(factory))?.Slug);
    }

    /// <summary>
    /// A typed website address of the wrong shape, and one a link already
    /// holds, are each refused with the operator's sentence beside the box
    /// it was typed in (R868).
    /// </summary>
    [Fact]
    public async Task ARefusedWebsiteAddressIsSaidBesideItsBox()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var contactLink = await StandingCaptureDriver.CreateContactLinkAsync(factory);

        foreach (var (typed, sentence) in new[]
                 {
                     ("Not a slug!", "A website address is 6 to 60 lower-case letters, numbers and hyphens, and cannot start or end with a hyphen."),
                     (contactLink.Slug, "That website address is already in use.")
                 })
        {
            using (var refused = await PostStaffFormAsync(
                client, WebsiteLinkPage + "?handler=CreateWebsiteLink", NewOperationKey(), "WebsiteSlug", typed))
            {
                Assert.Equal(HttpStatusCode.Redirect, refused.StatusCode);
            }

            var html = await IntakeWebDriver.GetHtmlAsync(client, WebsiteLinkPage);
            var form = FormFor(html, "handler=CreateWebsiteLink");
            Assert.Contains(HtmlEncoder.Default.Encode(sentence), form, StringComparison.Ordinal);
            Assert.DoesNotContain(HtmlEncoder.Default.Encode(OperatorLabels.ActionNotApplied), html, StringComparison.Ordinal);
        }

        Assert.Null(await WebsiteLinkAsync(factory));
    }

    /// <summary>
    /// A saved address with no name or no address is refused beside its form
    /// with the operator's sentence (R868), once: beside the add form, or
    /// beside the one saved address's form that was sent, however many
    /// addresses the contact has saved.
    /// </summary>
    [Fact]
    public async Task ASavedAddressWithNoAddressIsSaidBesideItsForm()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var link = await StandingCaptureDriver.CreateContactLinkAsync(factory);
        var organizationId = link.OrganizationId
            ?? throw new InvalidOperationException("A contact's link names no contact.");
        var contactPage = $"/Administration/Contacts/Edit/{organizationId:D}";
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var administration = scope.ServiceProvider.GetRequiredService<StandingPhotoLinkAdministration>();
            link = await administration.AddAddressAsync(
                Issuer, NewOperationKey(), link.Id, link.Version, "Back yard", "1 Example Street, Exampleton", CancellationToken.None);
            link = await administration.AddAddressAsync(
                Issuer, NewOperationKey(), link.Id, link.Version, "Front yard", "2 Example Street, Exampleton", CancellationToken.None);
        }

        var saved = (await AddressBookAsync(factory, link.Id)).Saved;
        Assert.Equal(2, saved.Count);
        var sentence = HtmlEncoder.Default.Encode("A saved address needs a name and an address.");

        using (var refused = await PostStaffFormAsync(
            client,
            $"{contactPage}?handler=AddPhotoLinkAddress&savedAddress=new",
            new Dictionary<string, string>
            {
                ["PhotoLinkOperationKey"] = NewOperationKey(),
                ["StandingLinkVersion"] = link.Version.ToString(CultureInfo.InvariantCulture),
                ["SavedAddressName"] = "Side gate",
                ["SavedAddressText"] = "   "
            }))
        {
            Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
            var html = await refused.Content.ReadAsStringAsync();
            Assert.Contains(sentence, FormFor(html, "handler=AddPhotoLinkAddress"), StringComparison.Ordinal);
            Assert.Equal(1, Regex.Count(html, Regex.Escape(sentence)));
        }

        var changing = saved[0];
        using (var refused = await PostStaffFormAsync(
            client,
            $"{contactPage}?handler=ChangePhotoLinkAddress&savedAddress={changing.Id:D}",
            new Dictionary<string, string>
            {
                ["PhotoLinkOperationKey"] = NewOperationKey(),
                ["StandingLinkVersion"] = link.Version.ToString(CultureInfo.InvariantCulture),
                ["SavedAddressId"] = changing.Id.ToString("D"),
                ["SavedAddressName"] = "   ",
                ["SavedAddressText"] = "1 Example Street, Exampleton"
            }))
        {
            Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
            var html = await refused.Content.ReadAsStringAsync();
            var form = FormFor(html, "handler=ChangePhotoLinkAddress");
            Assert.Contains(changing.Id.ToString("D"), form, StringComparison.Ordinal);
            Assert.Contains(sentence, form, StringComparison.Ordinal);
            Assert.Equal(1, Regex.Count(html, Regex.Escape(sentence)));
        }

        // Neither refusal changed what was saved.
        Assert.Equal(
            saved.OrderBy(address => address.Id),
            (await AddressBookAsync(factory, link.Id)).Saved.OrderBy(address => address.Id));
    }

    /// <summary>The contact's current standing link.</summary>
    private static async Task<StandingPhotoLink> ContactLinkAsync(IntakeWebApplicationFactory factory, Guid organizationId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IStandingPhotoLinkStore>()
            .FindForOrganizationAsync(organizationId, CancellationToken.None)
            ?? throw new InvalidOperationException("The contact has no standing link.");
    }

    /// <summary>The standing link history events filed under the area and subject.</summary>
    private static async Task<ActionHistoryEntity[]> StandingLinkHistoryAsync(
        IntakeWebApplicationFactory factory,
        string area,
        string subject)
    {
        await using var db = await (await factory.DatabaseAsync).CreateContextAsync();
        var rows = await db.ActionHistory.AsNoTracking()
            .Where(item => item.AggregateType == area && item.AggregateId == subject)
            .ToArrayAsync();
        return [.. rows.Where(item => item.EventKind.StartsWith("photo_link_", StringComparison.Ordinal))];
    }

    /// <summary>The event's before and after hold exactly the fields named, space-separated; none is an absent side.</summary>
    private static void AssertFields(ActionHistoryEntity[] events, string act, string before, string after)
    {
        var recorded = Assert.Single(events, item => item.EventKind == act);
        Assert.Equal(Names(before), FieldNames(recorded.BeforeJson));
        Assert.Equal(Names(after), FieldNames(recorded.AfterJson));
    }

    private static string[] Names(string names) =>
        [.. names.Split(' ', StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal)];

    private static string? FieldText(string? json, string field)
    {
        Assert.NotNull(json);
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty(field).GetString();
    }

    private static string[] FieldNames(string? json)
    {
        if (json is null)
        {
            return [];
        }

        using var document = JsonDocument.Parse(json);
        return [.. document.RootElement.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)];
    }

    private static async Task SetContactActiveAsync(IntakeWebApplicationFactory factory, Guid organizationId, bool active)
    {
        await using var db = await (await factory.DatabaseAsync).CreateContextAsync();
        await db.Organizations.Where(value => value.Id == organizationId)
            .ExecuteUpdateAsync(update => update.SetProperty(value => value.Active, active));
    }

    /// <summary>The contact's standing link panel, from its title to the panel's end.</summary>
    private static string StandingPanel(string html)
    {
        var start = html.IndexOf("id=\"photo-link-standing-title\"", StringComparison.Ordinal);
        Assert.True(start >= 0, "The Photo links tab shows no standing link panel.");
        var end = html.IndexOf("</section>", start, StringComparison.Ordinal);
        return html[start..end];
    }

    /// <summary>The Action logs row holding the words.</summary>
    private static string LogRow(string html, string words)
    {
        var row = Regex.Match(
            html,
            $"<tr>(?:(?!</tr>).)*>{Regex.Escape(words)}<(?:(?!</tr>).)*</tr>",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);
        Assert.True(row.Success, $"Administration › Logs shows no row for \"{words}\".");
        return row.Value;
    }

    /// <summary>Issues a Case link to a named customer at an e-mail address, as Send does with both filled in.</summary>
    private static async Task<IssuedPhotoLink> IssueLinkToAsync(
        IntakeWebApplicationFactory factory,
        Guid caseId,
        string customerName,
        string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IssuePhotoLink>().ExecuteAsync(
            new IssuePhotoLinkRequest(
                caseId,
                Issuer,
                customerName,
                Mobile: null,
                email,
                IncidentDate: null,
                PhotoCaptureMode.Guided,
                QuickLinkFor: null,
                LiveCameraOnly: false,
                OriginalsInBackground: false,
                CaptureLimits.DefaultLifetimeDays,
                PhotoLinkTokenPurpose.Copy,
                NewOperationKey()),
            CancellationToken.None);
    }

    /// <summary>An input's value as the page drew it.</summary>
    private static string InputValue(string html, string name)
    {
        var tag = Regex.Match(html, "<input[^>]*name=\"" + Regex.Escape(name) + "\"[^>]*>");
        Assert.True(tag.Success, $"The page draws no {name} box.");
        return WebUtility.HtmlDecode(Regex.Match(tag.Value, "value=\"([^\"]*)\"").Groups[1].Value);
    }

    /// <summary>The form whose action carries the marker, from its tag to its end.</summary>
    private static string FormFor(string html, string actionMarker)
    {
        var marker = html.IndexOf(actionMarker, StringComparison.Ordinal);
        Assert.True(marker >= 0, $"The page has no form for {actionMarker}.");
        var start = html.LastIndexOf("<form", marker, StringComparison.Ordinal);
        var end = html.IndexOf("</form>", marker, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, $"The form for {actionMarker} does not end.");
        return html[start..end];
    }

    /// <summary>
    /// A staff send that records each message and reports it sent at once;
    /// with <see cref="ProviderFault"/>, it records it Unknown and throws as
    /// the engine does when the provider's answer is ambiguous.
    /// </summary>
    private sealed class SentStaffMailSend : Pegasus.Core.Operations.IStaffMailSend
    {
        private readonly List<Pegasus.Core.Operations.StaffMailSendCommand> commands = [];

        public Exception? ProviderFault { get; init; }

        public IReadOnlyList<Pegasus.Core.Operations.StaffMailSendCommand> Commands
        {
            get
            {
                lock (commands)
                {
                    return [.. commands];
                }
            }
        }

        public Task<Pegasus.Core.Operations.StaffMailOperation> SendAsync(
            Pegasus.Core.Operations.StaffMailSendCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);
            lock (commands)
            {
                commands.Add(command);
            }

            var now = IntakeWebApplicationFactory.FixedUtcNow;
            var sent = new Pegasus.Core.Operations.StaffMailOperation(
                Guid.NewGuid(),
                Pegasus.Core.Operations.StaffMailState.Sent,
                Pegasus.Core.Operations.StaffMailAttemptStage.ObserveSent,
                1,
                now,
                now,
                now,
                null,
                command.ApprovedMailboxId,
                command.ExpectedMailboxGeneration,
                new string('A', 64),
                now,
                null,
                command.Purpose,
                command.ContextId,
                command.ExpectedContextVersion,
                null);
            if (ProviderFault is { } fault)
            {
                throw new Pegasus.Core.Operations.StaffMailOutcomeUnknownException(
                    sent with
                    {
                        State = Pegasus.Core.Operations.StaffMailState.Unknown,
                        AttemptStage = Pegasus.Core.Operations.StaffMailAttemptStage.Send,
                        SubmittedAtUtc = null,
                        ObservedSentAtUtc = null,
                        FailureCode = "provider_outcome_unknown"
                    },
                    fault);
            }
            return Task.FromResult(sent);
        }

        public Task<Pegasus.Core.Operations.StaffMailOperation?> GetAsync(
            ActionActor actor,
            Guid operationId,
            CancellationToken cancellationToken) =>
            Task.FromResult<Pegasus.Core.Operations.StaffMailOperation?>(null);

        public Task<Pegasus.Core.Operations.StaffMailOperation?> GetLatestForOriginalAsync(
            ActionActor actor,
            Guid retainedMessageId,
            CancellationToken cancellationToken) =>
            Task.FromResult<Pegasus.Core.Operations.StaffMailOperation?>(null);

        public Task<Pegasus.Core.Operations.StaffMailOperation> CancelAsync(
            ActionActor actor,
            Guid operationId,
            long expectedVersion,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("The composer cancels nothing.");
    }
}
