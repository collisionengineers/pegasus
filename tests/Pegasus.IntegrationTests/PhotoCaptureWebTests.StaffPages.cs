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
/// "Matched by staff" (R864), the "Contact inactive" chip (R865), the Website
/// link panel's own pause words (R867), the four labels (R869), one
/// "Reminded · name" line per reminder (R870), and the refusal sentences
/// shown beside their forms (R868).
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
                client, "/PhotoCapture?handler=CreateWebsiteLink", createKey, "WebsiteSlug", "ce-vehicle-photos");
            Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        }

        var website = await WebsiteLinkAsync(factory)
            ?? throw new InvalidOperationException("The website link was not created.");
        var pauseKey = NewOperationKey();
        using (var paused = await PostStaffFormAsync(client, "/PhotoCapture?handler=PauseWebsiteLink", new Dictionary<string, string>
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

    /// <summary>A Case a member of staff chose with Match to a case reads "Matched by staff" beside "Matched automatically" (R864).</summary>
    [Fact]
    public async Task ACaseChosenByStaffReadsMatchedByStaff()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var link = await StandingCaptureDriver.CreateContactLinkAsync(factory);
        var started = await StandingCaptureDriver.StartAsync(factory, client, link, "ZZ99 QQQ");

        string? How(PhotoCapturePairing pairing) => Pegasus.Web.Pages.PhotoCapture.IndexModel.CaseHow(
            new PhotoCaptureListRow(started.Capture, null, null, null, [], null, pairing, false, 0, false));

        Assert.Equal("Matched by staff", How(PhotoCapturePairing.ChosenByStaff));
        Assert.Equal(OperatorLabels.PhotoCapture.MatchedAutomatically, How(PhotoCapturePairing.MatchedAutomatically));
        Assert.Equal(OperatorLabels.PhotoCapture.AlreadyInstructed, How(PhotoCapturePairing.FiledAsItArrived));
        Assert.Null(How(PhotoCapturePairing.None));
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

        var empty = await IntakeWebDriver.GetHtmlAsync(client, "/PhotoCapture");
        Assert.Contains(addressLabel, empty, StringComparison.Ordinal);
        Assert.Contains("id=\"website-address\" name=\"WebsiteSlug\"", empty, StringComparison.Ordinal);

        using (var created = await PostStaffFormAsync(
            client, "/PhotoCapture?handler=CreateWebsiteLink", NewOperationKey(), "WebsiteSlug", "ce-vehicle-photos"))
        {
            Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        }

        var active = await IntakeWebDriver.GetHtmlAsync(client, "/PhotoCapture");
        Assert.Contains("Pause the link", active, StringComparison.Ordinal);
        Assert.DoesNotContain("Pause their link", active, StringComparison.Ordinal);
        Assert.Contains(addressLabel, active, StringComparison.Ordinal);
        Assert.Contains(retiring, active, StringComparison.Ordinal);

        var website = await WebsiteLinkAsync(factory)
            ?? throw new InvalidOperationException("The website link was not created.");
        using (var paused = await PostStaffFormAsync(client, "/PhotoCapture?handler=PauseWebsiteLink", new Dictionary<string, string>
        {
            ["OperationKey"] = NewOperationKey(),
            ["ExpectedVersion"] = website.Version.ToString(CultureInfo.InvariantCulture)
        }))
        {
            Assert.Equal(HttpStatusCode.Redirect, paused.StatusCode);
        }

        var pausedPage = await IntakeWebDriver.GetHtmlAsync(client, "/PhotoCapture");
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

    /// <summary>A link's History shows one "Reminded · name" line for each reminder (R870).</summary>
    [Fact]
    public async Task EachReminderIsItsOwnHistoryLineWithItsSender()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        var link = await IssueLinkAsync(factory, caseId);
        var page = $"/PhotoCapture/{link.Capture.Id:D}";

        for (var reminder = 0; reminder < 2; reminder++)
        {
            using var reminded = await PostStaffFormAsync(client, $"{page}?handler=Remind", NewOperationKey());
            Assert.Equal(HttpStatusCode.OK, reminded.StatusCode);
        }

        var name = await StaffNameAsync(factory, DevelopmentOfflineIdentity.AdministratorId);
        var details = await IntakeWebDriver.GetHtmlAsync(client, page);
        var line = HtmlEncoder.Default.Encode($"Reminded · {name}");
        Assert.Equal(2, Regex.Count(details, Regex.Escape(line)));
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
                client, "/PhotoCapture?handler=CreateWebsiteLink", NewOperationKey(), "WebsiteSlug", typed))
            {
                Assert.Equal(HttpStatusCode.Redirect, refused.StatusCode);
            }

            var html = await IntakeWebDriver.GetHtmlAsync(client, "/PhotoCapture");
            var form = FormFor(html, "handler=CreateWebsiteLink");
            Assert.Contains(HtmlEncoder.Default.Encode(sentence), form, StringComparison.Ordinal);
            Assert.DoesNotContain(HtmlEncoder.Default.Encode(OperatorLabels.ActionNotApplied), html, StringComparison.Ordinal);
        }

        Assert.Null(await WebsiteLinkAsync(factory));
    }

    /// <summary>A saved address with no name or no address is refused beside its form with the operator's sentence (R868).</summary>
    [Fact]
    public async Task ASavedAddressWithNoAddressIsSaidBesideItsForm()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var link = await StandingCaptureDriver.CreateContactLinkAsync(factory);
        var organizationId = link.OrganizationId
            ?? throw new InvalidOperationException("A contact's link names no contact.");
        var contactPage = $"/Administration/Contacts/Edit/{organizationId:D}";

        using var refused = await PostStaffFormAsync(
            client,
            $"{contactPage}?handler=AddPhotoLinkAddress&savedAddress=new",
            new Dictionary<string, string>
            {
                ["PhotoLinkOperationKey"] = NewOperationKey(),
                ["StandingLinkVersion"] = link.Version.ToString(CultureInfo.InvariantCulture),
                ["SavedAddressName"] = "Back yard",
                ["SavedAddressText"] = "   "
            });

        Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
        var html = await refused.Content.ReadAsStringAsync();
        var sentence = HtmlEncoder.Default.Encode("A saved address needs a name and an address.");
        Assert.Contains(sentence, FormFor(html, "handler=AddPhotoLinkAddress"), StringComparison.Ordinal);
        Assert.Single(Regex.Matches(html, Regex.Escape(sentence)));
        Assert.Empty((await AddressBookAsync(factory, link.Id)).Saved);
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
}
