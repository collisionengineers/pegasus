using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Core.PhotoCapture;
using Pegasus.Core.Workflow;
using Pegasus.Web.Authentication;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Send a photo link's settings on Administration › Configuration (FRD-17,
/// FRD-28; R813, R817): the mode, the two toggles Send starts with, and which
/// of 3, 7 and 14 days it offers with the one it starts on. Send reads them;
/// Extend keeps all three lifetimes.
/// </summary>
public sealed partial class WorkflowConfigurationWebTests
{
    private static readonly ActionActor PhotoLinkIssuer =
        ActionActor.Staff(DevelopmentOfflineIdentity.AdministratorId, [StaffRole.Administrator]);

    private static readonly int[] SavedLifetimes = [3, 14];
    private static readonly string[] SendLifetimes = ["3", "14"];
    private static readonly string[] AllLifetimes = ["3", "7", "14"];

    /// <summary>
    /// Saving the photo link settings raises the configuration's version by
    /// one and they read back as saved; a later workflow post that does not
    /// carry them keeps them.
    /// </summary>
    [Fact]
    public async Task ThePhotoLinkSettingsSaveWithTheWorkflowAndAPostWithoutThemKeepsThem()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var html = await GetPageAsync(client);
        var before = int.Parse(Field(html, "WorkflowExpectedVersion"), System.Globalization.CultureInfo.InvariantCulture);

        using (var saved = await client.PostAsync(
            Route + "?handler=SaveWorkflow",
            new FormUrlEncodedContent(WorkflowForm(html, QuickFrom3And14()))))
        {
            Assert.True(
                saved.StatusCode == HttpStatusCode.Redirect,
                $"Expected a redirect but got {saved.StatusCode}. {await ValidationMessagesAsync(saved)}");
        }

        var settings = await CurrentSettingsAsync(factory);
        Assert.Equal(before + 1, settings.PolicyVersion);
        AssertQuickFrom3And14(settings);

        var reloaded = await GetPageAsync(client);
        var withoutThem = WorkflowForm(reloaded, []);
        var chaseInterval = withoutThem.FindIndex(field => field.Key == "ChaseIntervalDays");
        // Another workflow setting changes, so the post writes.
        withoutThem[chaseInterval] = new("ChaseIntervalDays", Field(reloaded, "ChaseIntervalDays") == "9" ? "8" : "9");
        using (var other = await client.PostAsync(Route + "?handler=SaveWorkflow", new FormUrlEncodedContent(withoutThem)))
        {
            Assert.True(
                other.StatusCode == HttpStatusCode.Redirect,
                $"Expected a redirect but got {other.StatusCode}. {await ValidationMessagesAsync(other)}");
        }

        AssertQuickFrom3And14(await CurrentSettingsAsync(factory));
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
        var html = await GetPageAsync(client);
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
                Route + "?handler=SaveWorkflow",
                new FormUrlEncodedContent(WorkflowForm(html, photoLinkFields)));
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
    /// the page's own post. Extend still offers all three.
    /// </summary>
    [Fact]
    public async Task SendStartsFromTheSettingsAndOffersOnlyTheirLifetimesWhileExtendOffersAll()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var html = await GetPageAsync(client);
        using (var saved = await client.PostAsync(
            Route + "?handler=SaveWorkflow",
            new FormUrlEncodedContent(WorkflowForm(html, QuickFrom3And14()))))
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
                    LifetimeDays: 14, PhotoLinkTokenPurpose.Copy),
                CancellationToken.None);
        }

        var link = await IntakeWebDriver.GetHtmlAsync(client, $"/PhotoCapture/{issued.Capture.Id:D}");
        Assert.Equal(AllLifetimes, OptionValues(SelectElement(link, "id=\"photo-link-extend-days\"")));
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

    /// <summary>The workflow form as the page holds it, with the photo link fields given.</summary>
    private static List<KeyValuePair<string, string>> WorkflowForm(
        string html,
        List<KeyValuePair<string, string>> photoLinkFields)
    {
        List<KeyValuePair<string, string>> fields =
        [
            new("WorkflowExpectedVersion", Field(html, "WorkflowExpectedVersion")),
            new("WorkflowOperationKey", Field(html, "WorkflowOperationKey")),
            new("__RequestVerificationToken", Field(html, "__RequestVerificationToken")),
            new("RequireInstructions", "true"),
            new("RequireImages", "true"),
            new("ChaseIntervalDays", Field(html, "ChaseIntervalDays")),
            new("UnidentifiedTargetDays", Field(html, "UnidentifiedTargetDays")),
            new("TriageTargetDays", Field(html, "TriageTargetDays")),
            new("HeldTargetDays", Field(html, "HeldTargetDays")),
            new("ReviewTargetDays", Field(html, "ReviewTargetDays")),
            new("AiDraftTargetDays", Field(html, "AiDraftTargetDays"))
        ];
        fields.AddRange(photoLinkFields);
        return fields;
    }

    private static async Task<CaseWorkflowConfiguration> CurrentSettingsAsync(IntakeWebApplicationFactory factory)
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
