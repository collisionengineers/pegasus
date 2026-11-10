using System.Net;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Pegasus.Core.PhotoCapture;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Administration › Configuration's photo link settings in the product
/// owner's words of 10 November 2026: "Photo links" over them, "Lifetimes
/// offered" over the ticked lifetimes and "Link lasts" over the starting one
/// (R869), and each refused rule said beside its own setting (R868).
/// </summary>
public sealed partial class WorkflowConfigurationWebTests
{
    /// <summary>The settings carry the operator's headings (R869).</summary>
    [Fact]
    public async Task ThePhotoLinkSettingsCarryTheOperatorsHeadings()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        var settings = PhotoLinkSettings(await GetPageAsync(client));

        Assert.Contains("<legend>Photo links</legend>", settings, StringComparison.Ordinal);
        Assert.Contains("<legend>Lifetimes offered</legend>", settings, StringComparison.Ordinal);
        Assert.Contains("<label for=\"PhotoLinkDefaultLifetimeDays\">Link lasts</label>", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("Send a photo link", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("Links last", settings, StringComparison.Ordinal);
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
        var html = await GetPageAsync(client);
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
                Route + "?handler=SaveWorkflow",
                new FormUrlEncodedContent(WorkflowForm(html, fields)));
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

    /// <summary>The photo link settings' fieldset, from its marker to its end.</summary>
    private static string PhotoLinkSettings(string html)
    {
        var start = html.IndexOf("data-photo-link-settings", StringComparison.Ordinal);
        Assert.True(start >= 0, "The page draws no photo link settings.");
        var end = html.IndexOf("Save workflow settings", start, StringComparison.Ordinal);
        return html[start..end];
    }
}
