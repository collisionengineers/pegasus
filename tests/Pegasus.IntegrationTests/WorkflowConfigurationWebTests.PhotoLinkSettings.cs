using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Core.PhotoCapture;
using Pegasus.Core.Workflow;
using Pegasus.Web.Authentication;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Send a photo link's settings on Administration › Photo links (FRD-17,
/// FRD-28; R813, R817; moved from Workflow configuration, v50 G, R911): the
/// mode, the two toggles Send starts with, and which of 3, 7 and 14 days it
/// offers with the one it starts on. Send reads them; Extend keeps all three
/// lifetimes. Each page's panel saves only its own settings.
/// </summary>
public sealed partial class WorkflowConfigurationWebTests
{
    private const string PhotoLinksRoute = "/Administration/PhotoLinks";

    private static readonly ActionActor PhotoLinkIssuer =
        ActionActor.Staff(DevelopmentOfflineIdentity.AdministratorId, [StaffRole.Administrator]);

    private static readonly int[] SavedLifetimes = [3, 14];
    private static readonly string[] SendLifetimes = ["3", "14"];
    private static readonly string[] AllLifetimes = ["3", "7", "14"];

    /// <summary>
    /// Saving the photo link settings raises the configuration's version by
    /// one and they read back as saved; a later Chasing and due targets save
    /// on Workflow configuration, which does not carry them, keeps them.
    /// </summary>
    [Fact]
    public async Task ThePhotoLinkSettingsSaveOnPhotoLinksAndAConfigurationSaveKeepsThem()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var html = await GetPhotoLinksPageAsync(client);
        var before = int.Parse(Field(html, "WorkflowExpectedVersion"), System.Globalization.CultureInfo.InvariantCulture);

        using (var saved = await client.PostAsync(
            PhotoLinksRoute + "?handler=SaveSendSettings",
            new FormUrlEncodedContent(SendSettingsForm(html, QuickFrom3And14()))))
        {
            Assert.True(
                saved.StatusCode == HttpStatusCode.Redirect,
                $"Expected a redirect but got {saved.StatusCode}. {await ValidationMessagesAsync(saved)}");
        }

        var settings = await CurrentSettingsAsync(factory);
        Assert.Equal(before + 1, settings.PolicyVersion);
        AssertQuickFrom3And14(settings);

        var reloaded = await GetPageAsync(client);
        // Another workflow setting changes, so the post writes.
        var chaseInterval = Field(reloaded, "ChaseIntervalDays") == "9" ? "8" : "9";
        using (var other = await client.PostAsync(Route + "?handler=SaveTargets", new FormUrlEncodedContent(TargetsForm(reloaded, chaseInterval))))
        {
            Assert.True(
                other.StatusCode == HttpStatusCode.Redirect,
                $"Expected a redirect but got {other.StatusCode}. {await ValidationMessagesAsync(other)}");
        }

        var after = await CurrentSettingsAsync(factory);
        Assert.Equal(settings.PolicyVersion + 1, after.PolicyVersion);
        Assert.Equal(int.Parse(chaseInterval, System.Globalization.CultureInfo.InvariantCulture), after.ChaseIntervalDays);
        AssertQuickFrom3And14(after);
    }

    /// <summary>
    /// A panel save keeps every other panel's settings as stored, wherever
    /// they are drawn (v50; R910, R911): Chasing and due targets keeps
    /// Required before Review and the photo link settings, and Send a photo
    /// link's save on Photo links keeps Required before Review and the targets.
    /// </summary>
    [Fact]
    public async Task APanelSaveKeepsTheOtherPanelsStoredValues()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        // Images no longer required: the unticked box posts nothing.
        var page = await GetPageAsync(client);
        using (var review = await client.PostAsync(Route + "?handler=SaveReview", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["WorkflowExpectedVersion"] = Field(page, "WorkflowExpectedVersion"),
            ["WorkflowOperationKey"] = Field(page, "WorkflowOperationKey"),
            ["__RequestVerificationToken"] = Field(page, "__RequestVerificationToken"),
            ["RequireInstructions"] = "true"
        })))
        {
            Assert.Equal(HttpStatusCode.Redirect, review.StatusCode);
        }

        var afterReview = await CurrentSettingsAsync(factory);
        Assert.True(afterReview.RequireInstructions);
        Assert.False(afterReview.RequireImages);

        var photoLinks = await GetPhotoLinksPageAsync(client);
        using (var send = await client.PostAsync(
            PhotoLinksRoute + "?handler=SaveSendSettings",
            new FormUrlEncodedContent(SendSettingsForm(photoLinks, QuickFrom3And14()))))
        {
            Assert.Equal(HttpStatusCode.Redirect, send.StatusCode);
        }

        page = await GetPageAsync(client);
        var chaseInterval = Field(page, "ChaseIntervalDays") == "9" ? "8" : "9";
        using (var targets = await client.PostAsync(Route + "?handler=SaveTargets", new FormUrlEncodedContent(TargetsForm(page, chaseInterval))))
        {
            Assert.Equal(HttpStatusCode.Redirect, targets.StatusCode);
        }

        var afterTargets = await CurrentSettingsAsync(factory);
        Assert.Equal(int.Parse(chaseInterval, System.Globalization.CultureInfo.InvariantCulture), afterTargets.ChaseIntervalDays);
        Assert.True(afterTargets.RequireInstructions);
        Assert.False(afterTargets.RequireImages);
        Assert.Equal(afterReview.TriageTargetDays, afterTargets.TriageTargetDays);
        AssertQuickFrom3And14(afterTargets);

        photoLinks = await GetPhotoLinksPageAsync(client);
        List<KeyValuePair<string, string>> guidedFor7Days =
        [
            new("PhotoLinkStartMode", nameof(PhotoCaptureMode.Guided)),
            new("PhotoLinkLifetimesOffered", "7"),
            new("PhotoLinkDefaultLifetimeDays", "7")
        ];
        using (var send = await client.PostAsync(
            PhotoLinksRoute + "?handler=SaveSendSettings",
            new FormUrlEncodedContent(SendSettingsForm(photoLinks, guidedFor7Days))))
        {
            Assert.Equal(HttpStatusCode.Redirect, send.StatusCode);
        }

        var afterSend = await CurrentSettingsAsync(factory);
        Assert.Equal(PhotoCaptureMode.Guided, afterSend.PhotoLinkStartMode);
        Assert.Equal(7, afterSend.PhotoLinkDefaultLifetimeDays);
        Assert.Equal(afterTargets.ChaseIntervalDays, afterSend.ChaseIntervalDays);
        Assert.Equal(afterTargets.TriageTargetDays, afterSend.TriageTargetDays);
        Assert.True(afterSend.RequireInstructions);
        Assert.False(afterSend.RequireImages);
    }

    /// <summary>
    /// Offering no lifetime, or starting on one not offered, is refused and
    /// nothing is saved.
    /// </summary>
    [Fact]
    public async Task PhotoLinkSettingsThatOfferNoLifetimeOrStartOnOneNotOfferedAreRefused()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var html = await GetPhotoLinksPageAsync(client);
        var before = await CurrentSettingsAsync(factory);

        List<KeyValuePair<string, string>> noneOffered =
        [
            new("PhotoLinkStartMode", nameof(PhotoCaptureMode.Guided)),
            new("PhotoLinkDefaultLifetimeDays", "7")
        ];
        List<KeyValuePair<string, string>> startsOnOneNotOffered =
        [
            new("PhotoLinkStartMode", nameof(PhotoCaptureMode.Guided)),
            new("PhotoLinkLifetimesOffered", "3"),
            new("PhotoLinkLifetimesOffered", "7"),
            new("PhotoLinkDefaultLifetimeDays", "14")
        ];
        foreach (var photoLinkFields in new[] { noneOffered, startsOnOneNotOffered })
        {
            using var refused = await client.PostAsync(
                PhotoLinksRoute + "?handler=SaveSendSettings",
                new FormUrlEncodedContent(SendSettingsForm(html, photoLinkFields)));
            Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
        }

        var after = await CurrentSettingsAsync(factory);
        Assert.Equal(before.PolicyVersion, after.PolicyVersion);
        Assert.Equal(before.PhotoLinkLifetimesOffered, after.PhotoLinkLifetimesOffered);
        Assert.Equal(before.PhotoLinkDefaultLifetimeDays, after.PhotoLinkDefaultLifetimeDays);
    }

    /// <summary>
    /// Send starts on the configured mode, toggles and lifetime and offers
    /// only the configured lifetimes; a lifetime it does not offer is not
    /// the page's own post. Extend, on the link's page, still offers all
    /// three, one form each (R913).
    /// </summary>
    [Fact]
    public async Task SendStartsFromTheSettingsAndOffersOnlyTheirLifetimesWhileExtendOffersAll()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var html = await GetPhotoLinksPageAsync(client);
        using (var saved = await client.PostAsync(
            PhotoLinksRoute + "?handler=SaveSendSettings",
            new FormUrlEncodedContent(SendSettingsForm(html, QuickFrom3And14()))))
        {
            Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        }

        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        var send = await IntakeWebDriver.GetHtmlAsync(client, $"/PhotoCapture/Send/{caseId:D}");

        Assert.Contains($"name=\"Mode\" value=\"{nameof(PhotoCaptureMode.Quick)}\" checked=\"checked\"", send, StringComparison.Ordinal);
        Assert.DoesNotContain("checked", FirstInput(send, "LiveCameraOnly"), StringComparison.Ordinal);
        Assert.DoesNotContain("checked", FirstInput(send, "OriginalsInBackground"), StringComparison.Ordinal);
        var days = SelectElement(send, "name=\"Days\"");
        Assert.Equal(SendLifetimes, OptionValues(days));
        Assert.Matches("<option[^>]*value=\"14\"[^>]*selected", days);

        List<KeyValuePair<string, string>> notOffered =
        [
            new("__RequestVerificationToken", Field(send, "__RequestVerificationToken")),
            new("Mode", nameof(PhotoCaptureMode.Guided)),
            new("Days", "7"),
            new("Submit", Pegasus.Web.Pages.PhotoCapture.SendModel.ToCopy)
        ];
        using (var refused = await client.PostAsync($"/PhotoCapture/Send/{caseId:D}", new FormUrlEncodedContent(notOffered)))
        {
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        }

        IssuedPhotoLink issued;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            issued = await scope.ServiceProvider.GetRequiredService<IssuePhotoLink>().ExecuteAsync(
                new IssuePhotoLinkRequest(
                    caseId, PhotoLinkIssuer, CustomerName: null, Mobile: null, Email: null, IncidentDate: null,
                    PhotoCaptureMode.Guided, QuickLinkFor: null, LiveCameraOnly: false, OriginalsInBackground: false,
                    LifetimeDays: 14, PhotoLinkTokenPurpose.Copy, OperationKey: Guid.NewGuid().ToString("N")),
                CancellationToken.None);
        }

        var link = await IntakeWebDriver.GetHtmlAsync(client, $"/PhotoCapture/{issued.Capture.Id:D}");
        Assert.Equal(AllLifetimes, ExtendDays(link));
    }

    /// <summary>Quick, both toggles off, 3 and 14 days offered and 14 the one Send starts on.</summary>
    private static List<KeyValuePair<string, string>> QuickFrom3And14() =>
    [
        new("PhotoLinkStartMode", nameof(PhotoCaptureMode.Quick)),
        new("PhotoLinkStartLiveCameraOnly", "false"),
        new("PhotoLinkStartOriginals", "false"),
        new("PhotoLinkLifetimesOffered", "3"),
        new("PhotoLinkLifetimesOffered", "14"),
        new("PhotoLinkDefaultLifetimeDays", "14")
    ];

    private static void AssertQuickFrom3And14(CaseWorkflowConfiguration settings)
    {
        Assert.Equal(PhotoCaptureMode.Quick, settings.PhotoLinkStartMode);
        Assert.False(settings.PhotoLinkStartLiveCameraOnly);
        Assert.False(settings.PhotoLinkStartOriginals);
        Assert.Equal(SavedLifetimes, settings.PhotoLinkLifetimesOffered);
        Assert.Equal(14, settings.PhotoLinkDefaultLifetimeDays);
    }

    /// <summary>Send a photo link's settings form as Photo links holds it, with the photo link fields given.</summary>
    private static List<KeyValuePair<string, string>> SendSettingsForm(
        string html,
        List<KeyValuePair<string, string>> photoLinkFields)
    {
        List<KeyValuePair<string, string>> fields =
        [
            new("WorkflowExpectedVersion", Field(html, "WorkflowExpectedVersion")),
            new("WorkflowOperationKey", Field(html, "WorkflowOperationKey")),
            new("__RequestVerificationToken", Field(html, "__RequestVerificationToken"))
        ];
        fields.AddRange(photoLinkFields);
        return fields;
    }

    private static async Task<string> GetPhotoLinksPageAsync(HttpClient client)
    {
        using var response = await client.GetAsync(PhotoLinksRoute);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>The lifetimes the link page's Extend forms post, one form each, in order.</summary>
    private static string[] ExtendDays(string html) =>
    [
        .. Regex.Matches(html, "handler=Extend\"[\\s\\S]*?name=\"Days\" value=\"(?<days>\\d+)\"")
            .Select(match => match.Groups["days"].Value)
    ];

    private static async Task<CaseWorkflowConfiguration> CurrentSettingsAsync(WebApplicationFactory<Program> factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ICaseWorkflowConfiguration>()
            .GetCurrentAsync(CancellationToken.None);
    }

    private static string FirstInput(string html, string name)
    {
        var tag = Regex.Match(html, "<input[^>]*name=\"" + Regex.Escape(name) + "\"[^>]*>");
        Assert.True(tag.Success, "Missing field " + name);
        return tag.Value;
    }

    private static string SelectElement(string html, string attribute)
    {
        var select = Regex.Match(html, "<select[^>]*" + Regex.Escape(attribute) + "[^>]*>[\\s\\S]*?</select>");
        Assert.True(select.Success, "Missing select " + attribute);
        return select.Value;
    }

    private static string[] OptionValues(string select) =>
        [.. Regex.Matches(select, "<option[^>]*value=\"(?<value>[^\"]*)\"").Select(match => match.Groups["value"].Value)];
}
