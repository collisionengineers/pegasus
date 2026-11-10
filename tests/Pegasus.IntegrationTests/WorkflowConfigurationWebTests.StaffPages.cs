using System.Net;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Pegasus.Core.PhotoCapture;
using Pegasus.Web.PhotoCapture;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Send a photo link's settings on Administration › Photo links in the
/// product owner's words of 10 November 2026: "Send a photo link" over them,
/// since "Photo links" is the page's own title (v50 G; R911), "Lifetimes
/// offered" over the ticked lifetimes and "Link lasts" over the starting one
/// (R869), and each refused rule said beside its own setting (R868). The page
/// is a photo capture page: absent while photo capture is off.
/// </summary>
public sealed partial class WorkflowConfigurationWebTests
{
    /// <summary>The settings carry the operator's headings (R869, R911).</summary>
    [Fact]
    public async Task ThePhotoLinkSettingsCarryTheOperatorsHeadings()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        var settings = PhotoLinkSettings(await GetPhotoLinksPageAsync(client));

        Assert.Contains("<h2 id=\"send-settings-title\">Send a photo link</h2>", settings, StringComparison.Ordinal);
        Assert.Contains("<legend>Lifetimes offered</legend>", settings, StringComparison.Ordinal);
        Assert.Contains("<label for=\"PhotoLinkDefaultLifetimeDays\">Link lasts</label>", settings, StringComparison.Ordinal);
        Assert.Contains("handler=SaveSendSettings", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("Links last", settings, StringComparison.Ordinal);
    }

    /// <summary>
    /// While photo capture is on, Photo links is listed in the Administration
    /// hub and rail and holds the website link and Send a photo link's
    /// settings; while it is off the page answers 404 and neither lists it
    /// (v50 F; R911; FRD-28 Feature off).
    /// </summary>
    [Fact]
    public async Task PhotoLinksIsListedOnlyWhilePhotoCaptureIsOn()
    {
        using var factory = new IntakeWebApplicationFactory();
        using (var client = IntakeWebDriver.CreateClient(factory))
        {
            var page = await GetPhotoLinksPageAsync(client);
            Assert.Contains("id=\"website-link-title\"", page, StringComparison.Ordinal);
            Assert.Contains("data-photo-link-settings", page, StringComparison.Ordinal);
            Assert.Contains("href=\"/Administration/PhotoLinks\"", await AdminRailAsync(client, Route), StringComparison.Ordinal);
            Assert.Contains("href=\"/Administration/PhotoLinks\"", await IntakeWebDriver.GetHtmlAsync(client, "/Administration"), StringComparison.Ordinal);
        }

        using var off = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting(PhotoCaptureSurface.FeatureFlag, "false");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { [PhotoCaptureSurface.FeatureFlag] = "false" }));
        });
        using var offClient = IntakeWebDriver.CreateClient(off);

        using (var missing = await offClient.GetAsync(PhotoLinksRoute))
        {
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        }

        Assert.DoesNotContain("/Administration/PhotoLinks", await AdminRailAsync(offClient, Route), StringComparison.Ordinal);
        Assert.DoesNotContain("/Administration/PhotoLinks", await IntakeWebDriver.GetHtmlAsync(offClient, "/Administration"), StringComparison.Ordinal);
    }

    /// <summary>
    /// Offering no lifetime is refused beside the lifetimes offered, and
    /// starting on one not offered beside the starting lifetime, each in the
    /// operator's sentence and never the generic one (R868).
    /// </summary>
    [Fact]
    public async Task EachRefusedPhotoLinkSettingIsSaidBesideItsSetting()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var html = await GetPhotoLinksPageAsync(client);
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

        foreach (var (fields, sentence, beside) in new[]
                 {
                     (noneOffered, "Offer at least one lifetime.", "data-valmsg-for=\"PhotoLinkLifetimesOffered\""),
                     (startsOnOneNotOffered, "The lifetime links start on must be one of those offered.", "data-valmsg-for=\"PhotoLinkDefaultLifetimeDays\"")
                 })
        {
            using var refused = await client.PostAsync(
                PhotoLinksRoute + "?handler=SaveSendSettings",
                new FormUrlEncodedContent(SendSettingsForm(html, fields)));
            Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
            var page = await refused.Content.ReadAsStringAsync();
            var message = Regex.Match(page, "<span[^>]*" + Regex.Escape(beside) + "[^>]*>(?<text>[^<]*)</span>");
            Assert.True(message.Success, $"No message beside {beside}.");
            Assert.Equal(HtmlEncoder.Default.Encode(sentence), message.Groups["text"].Value);
            Assert.DoesNotContain(
                HtmlEncoder.Default.Encode(Pegasus.Web.Presentation.OperatorLabels.ActionNotApplied),
                page,
                StringComparison.Ordinal);
        }
    }

    /// <summary>Send a photo link's settings panel, from its marker to the panel's end.</summary>
    private static string PhotoLinkSettings(string html)
    {
        var start = html.IndexOf("data-photo-link-settings", StringComparison.Ordinal);
        Assert.True(start >= 0, "The page draws no photo link settings.");
        var end = html.IndexOf("</section>", start, StringComparison.Ordinal);
        Assert.True(end > start, "The photo link settings panel does not end.");
        return html[start..end];
    }
}
