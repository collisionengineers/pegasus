using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Web.Presentation;

using static Pegasus.IntegrationTests.AutomationMcpTestSupport;
using static Pegasus.IntegrationTests.CaseWebTestSupport;
using static Pegasus.IntegrationTests.DesktopSignInTestSupport;

using Research = Pegasus.IntegrationTests.MarketResearchSubmissionIngressTests;
using ValuationLabels = Pegasus.Web.Presentation.CaseWorkspaceLabels.Valuation;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The Valuation section's market research space (issue 2166; operator, 26
/// and 27 November 2026; R1313, R1316, R1317), over the real stores: each
/// submission is filed from Pegasus Desktop through the connector, as its own
/// tests file one, and the Case page is read by a member of staff signed in
/// to Pegasus.
/// </summary>
public sealed partial class CaseValuationSectionWebTests
{
    private const string Submitter = "desktop-space";

    /// <summary>
    /// A Case with no submission draws no space. With one it says who
    /// submitted it and when, the proposed value, the adverts under the eight
    /// approved column heads, and links the report, the pack and each
    /// advert's screenshot; with two the newer stands first, and a
    /// submission filed with no report has no report link. Nothing is said of
    /// acceptance or review, there is no control, and the space is the same
    /// while editing and as a mounted section. Its reads do not grow with
    /// the submissions.
    /// </summary>
    [Fact]
    public async Task TheMarketResearchSpaceShowsEachSubmissionNewestFirstAndNothingOfItsReview()
    {
        var counter = new SqlStatementCounter();
        using var factory = new IntakeWebApplicationFactory(
            "Development", true, TimeProvider.System, commandInterceptor: counter);
        using var host = WithDesktopSignIn(factory);
        var caseId = await Research.SeedCaseWithFolderAsync(factory, host);
        using var desktop = CreateConnector(host);
        var (_, token) = await SignedInStaffAsync(host, desktop, Submitter, StaffRole.Engineer);
        using var viewer = await SignedInViewerAsync(host, "space-viewer");
        var sectionPage = $"/Cases/{caseId:D}?section=valuation";
        var mountedSection = $"/Cases/{caseId:D}/Section?section=valuation";

        // No submission: the section draws no space, on the page or mounted.
        Assert.DoesNotContain("data-market-research", await GetHtmlAsync(viewer, sectionPage), StringComparison.Ordinal);
        Assert.DoesNotContain("data-market-research", await GetHtmlAsync(viewer, mountedSection), StringComparison.Ordinal);

        var first = await SubmitAsync(
            desktop, token, caseId, "space-1", "Accepted", proposed: 10150, withReport: true, adverts: 2);

        var html = await GetHtmlAsync(viewer, sectionPage);
        var space = Space(html);
        Assert.Contains($">{ValuationLabels.MarketResearch}</h3>", space, StringComparison.Ordinal);
        var block = SubmissionBlock(space, first.SubmissionId);
        Assert.Contains(
            ">" + ValuationLabels.SubmittedBy(Submitter, first.SubmittedAtUtc) + "<",
            block,
            StringComparison.Ordinal);
        Assert.Matches(
            $"<dt>{Regex.Escape(ValuationLabels.ProposedValue)}</dt>\\s*<dd[^>]*>£10,150\\.00</dd>",
            block);
        // The eight approved heads, in order; the screenshots' column is named aloud only.
        Assert.Equal(
            [
                ValuationLabels.AdvertNumber,
                ValuationLabels.AdvertSite,
                ValuationLabels.AdvertVehicle,
                ValuationLabels.AdvertYear,
                ValuationLabels.AdvertMileage,
                ValuationLabels.AdvertPrice,
                ValuationLabels.AdvertSeller,
                ValuationLabels.AdvertComment,
                $"<span class=\"sr-only\">{ValuationLabels.Screenshot}</span>"
            ],
            Cells(block, "th").AsEnumerable());
        // The operator's words themselves (R1317), read from the page and
        // not from the constants that hold them.
        Assert.Equal(
            ["No.", "Site", "Vehicle", "Year", "Mileage", "Price", "Seller", "Comment"],
            Cells(block, "th").Take(8));
        foreach (var words in new[]
        {
            ">Market research</h3>",
            $">Submitted by {Submitter}, {OperatorLabels.OfficeDate(first.SubmittedAtUtc)}<",
            "<dt>Proposed value</dt>",
            "<span>Market Valuation Evidence</span>",
            "<span>Advert Evidence Pack</span>",
            "<span>Screenshot</span>"
        })
        {
            Assert.Contains(words, space, StringComparison.Ordinal);
        }
        Assert.Equal(2, Regex.Count(block, "data-market-research-advert="));
        Assert.Equal(
            ["1", "Auto Trader", "Ford Focus 1.0 Zetec", "2019", "40,000", "£9,900.00", "Trade, Leeds", "Close match."],
            Cells(AdvertRow(block, 1), "td").Take(8));
        Assert.Equal(
            ["2", "Auto Trader", "Ford Focus 1.0 Zetec", "2019", "40,000", "£10,000.00", "Trade, Leeds", "Close match."],
            Cells(AdvertRow(block, 2), "td").Take(8));

        // The report, marked as the AI's, the pack, and one screenshot in each advert's row.
        var reportLink = FileLink(block, "report");
        Assert.Contains($"<span>{ValuationLabels.MarketValuationEvidence}</span>", reportLink, StringComparison.Ordinal);
        Assert.Contains("data-evidence-item", reportLink, StringComparison.Ordinal);
        Assert.Contains(OperatorLabels.SourceTagWord.Ai.CssClass, block, StringComparison.Ordinal);
        var packLink = FileLink(block, "pack");
        Assert.Contains($"<span>{ValuationLabels.AdvertEvidencePack}</span>", packLink, StringComparison.Ordinal);
        Assert.Contains("data-evidence-item", packLink, StringComparison.Ordinal);
        Assert.Contains("data-file-name=\"space-1-pack.pdf\"", packLink, StringComparison.Ordinal);
        Assert.Contains("data-evidence-set", block, StringComparison.Ordinal);
        foreach (var number in new[] { 1, 2 })
        {
            var screenshotLink = FileLink(AdvertRow(block, number), "screenshot");
            Assert.Contains($"<span>{ValuationLabels.Screenshot}</span>", screenshotLink, StringComparison.Ordinal);
            Assert.Contains($"data-file-name=\"space-1-advert-{number}.jpg\"", screenshotLink, StringComparison.Ordinal);
            Assert.Contains("data-media-type=\"image/jpeg\"", screenshotLink, StringComparison.Ordinal);
        }
        AssertSaysNothingOfReviewAndHoldsNoControl(space);

        counter.Reset();
        Assert.Equal(space, Space(await GetHtmlAsync(viewer, mountedSection)));
        var readsWithOne = counter.CountMentioning("[MarketResearchSubmission");

        // A second submission, filed with no report and never reviewed (R1310, R1311).
        var second = await SubmitAsync(
            desktop, token, caseId, "space-2", "NotReviewed", proposed: 10400, withReport: false, adverts: 1);

        counter.Reset();
        space = Space(await GetHtmlAsync(viewer, mountedSection));
        var readsWithTwo = counter.CountMentioning("[MarketResearchSubmission");
        Assert.True(readsWithOne > 0, "The mounted section did not read the submissions.");
        Assert.True(
            readsWithTwo == readsWithOne,
            $"Two submissions cost {readsWithTwo} statements where one cost {readsWithOne}."
            + Environment.NewLine + counter.Describe());

        // Newest first.
        Assert.True(
            space.IndexOf(SubmissionHook(second.SubmissionId), StringComparison.Ordinal)
            < space.IndexOf(SubmissionHook(first.SubmissionId), StringComparison.Ordinal),
            "The newer submission does not stand first.");
        var newer = SubmissionBlock(space, second.SubmissionId);
        Assert.Contains(
            ">" + ValuationLabels.SubmittedBy(Submitter, second.SubmittedAtUtc) + "<",
            newer,
            StringComparison.Ordinal);
        Assert.Contains("£10,400.00", newer, StringComparison.Ordinal);
        Assert.DoesNotContain(ValuationLabels.MarketValuationEvidence, newer, StringComparison.Ordinal);
        Assert.DoesNotContain(OperatorLabels.SourceTagWord.Ai.CssClass, newer, StringComparison.Ordinal);
        Assert.Contains($"<span>{ValuationLabels.AdvertEvidencePack}</span>", FileLink(newer, "pack"), StringComparison.Ordinal);
        Assert.Single(Regex.Matches(newer, "data-market-research-advert="));
        // The earlier submission still has its report.
        Assert.Contains(
            ValuationLabels.MarketValuationEvidence,
            SubmissionBlock(space, first.SubmissionId),
            StringComparison.Ordinal);
        AssertSaysNothingOfReviewAndHoldsNoControl(space);

        // While editing the space is the same, and still holds no control.
        await CaseVehicleSaveWebTests.ClaimLeaseAsync(
            viewer, caseId, await GetHtmlAsync(viewer, $"/Cases/{caseId:D}"));
        var editing = await GetHtmlAsync(viewer, $"/Cases/{caseId:D}");
        Assert.Contains("data-case-editing=\"true\"", editing, StringComparison.Ordinal);
        Assert.Equal(space, Space(editing));
    }

    /// <summary>
    /// The pack and a screenshot are kept in the Case folder's subfolder and
    /// listed nowhere else, so the space's links are the way to them: each
    /// opens its file for a member of staff through the route every Case
    /// document is read by, and is refused exactly as the Case's own document
    /// is, to someone not signed in and under another Case. A submission
    /// whose record cannot be read keeps its line and its links and has no
    /// table; the section and the other submission are drawn as before.
    /// </summary>
    [Fact]
    public async Task TheSpacesLinksOpenTheSubfoldersFilesAsAnyCaseDocumentAndAnUnreadableRecordDrawsNoTable()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var host = WithDesktopSignIn(factory);
        var caseId = await Research.SeedCaseWithFolderAsync(factory, host);
        using var desktop = CreateConnector(host);
        var (_, token) = await SignedInStaffAsync(host, desktop, Submitter, StaffRole.Engineer);
        using var viewer = await SignedInViewerAsync(host, "space-reader");
        using var stranger = CreateBrowser(host);
        var first = await SubmitAsync(
            desktop, token, caseId, "files-1", "Accepted", proposed: 10150, withReport: true, adverts: 1);
        var second = await SubmitAsync(
            desktop, token, caseId, "files-2", "Accepted", proposed: 10400, withReport: false, adverts: 1);
        var sectionPage = $"/Cases/{caseId:D}?section=valuation";

        var block = SubmissionBlock(Space(await GetHtmlAsync(viewer, sectionPage)), first.SubmissionId);
        var report = Href(FileLink(block, "report"));
        var pack = Href(FileLink(block, "pack"));
        var screenshot = Href(FileLink(AdvertRow(block, 1), "screenshot"));

        // A member of staff reads each file, in the viewer and as the saved download.
        using (var opened = await viewer.GetAsync(pack))
        {
            Assert.Equal(HttpStatusCode.OK, opened.StatusCode);
            Assert.Equal("application/pdf", opened.Content.Headers.ContentType?.MediaType);
            Assert.Equal("inline", opened.Content.Headers.ContentDisposition?.DispositionType);
            Assert.Equal(Bytes("files-1", "pack"), await opened.Content.ReadAsByteArrayAsync());
        }
        using (var opened = await viewer.GetAsync(screenshot))
        {
            Assert.Equal(HttpStatusCode.OK, opened.StatusCode);
            Assert.Equal("image/jpeg", opened.Content.Headers.ContentType?.MediaType);
            Assert.Equal(Bytes("files-1", "advert-1"), await opened.Content.ReadAsByteArrayAsync());
        }
        using (var saved = await viewer.GetAsync(Attribute(FileLink(block, "pack"), "data-download-href")))
        {
            Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
            Assert.Equal("attachment", saved.Content.Headers.ContentDisposition?.DispositionType);
            Assert.Equal(Bytes("files-1", "pack"), await saved.Content.ReadAsByteArrayAsync());
        }

        // Someone not signed in is refused the subfolder's files as the Case's own document is.
        using (var refusedDocument = await stranger.GetAsync(report))
        {
            Assert.NotEqual(HttpStatusCode.OK, refusedDocument.StatusCode);
            foreach (var address in new[] { pack, screenshot })
            {
                using var refused = await stranger.GetAsync(address);
                Assert.Equal(refusedDocument.StatusCode, refused.StatusCode);
                Assert.Equal(WithoutQuery(refusedDocument.Headers.Location), WithoutQuery(refused.Headers.Location));
            }
        }

        // Under another Case the files are not found, as the Case's own document is not.
        var otherCase = $"/Cases/{Guid.NewGuid():D}/";
        foreach (var address in new[] { report, pack, screenshot })
        {
            using var missing = await viewer.GetAsync(address.Replace($"/Cases/{caseId:D}/", otherCase, StringComparison.Ordinal));
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        }

        // A record that cannot be read: the line and the links stand, the table does not.
        await (await factory.DatabaseAsync).ExecuteAsync(
            $"UPDATE MarketResearchSubmissions SET RecordJson = N'{{\"schemaVersion\":1,\"adverts\":[' WHERE Id = '{second.SubmissionId:D}'");
        var space = Space(await GetHtmlAsync(viewer, sectionPage));
        var unreadable = SubmissionBlock(space, second.SubmissionId);
        Assert.Contains(
            ">" + ValuationLabels.SubmittedBy(Submitter, second.SubmittedAtUtc) + "<",
            unreadable,
            StringComparison.Ordinal);
        Assert.Contains("£10,400.00", unreadable, StringComparison.Ordinal);
        Assert.DoesNotContain("<table", unreadable, StringComparison.Ordinal);
        using (var opened = await viewer.GetAsync(Href(FileLink(unreadable, "pack"))))
        {
            Assert.Equal(Bytes("files-2", "pack"), await opened.Content.ReadAsByteArrayAsync());
        }
        using (var opened = await viewer.GetAsync(Href(FileLink(unreadable, "screenshot"))))
        {
            Assert.Equal(Bytes("files-2", "advert-1"), await opened.Content.ReadAsByteArrayAsync());
        }
        Assert.Single(Regex.Matches(SubmissionBlock(space, first.SubmissionId), "data-market-research-advert="));
        Assert.Contains("data-valuation-cards", await GetHtmlAsync(viewer, sectionPage), StringComparison.Ordinal);
    }

    private sealed record FiledSubmission(Guid SubmissionId, DateTimeOffset SubmittedAtUtc);

    /// <summary>A member of staff who did not submit the research, signed in to Pegasus in a browser of their own.</summary>
    private static async Task<HttpClient> SignedInViewerAsync(WebApplicationFactory<Program> host, string userName)
    {
        await CreateStaffAsync(host, userName, StaffRole.Administrator);
        var browser = CreateBrowser(host);
        await SignInAsync(browser, userName);
        return browser;
    }

    /// <summary>
    /// Files one submission through the connector under <paramref name="key"/>:
    /// a screenshot for each advert, the pack and, when asked, the report.
    /// </summary>
    private static async Task<FiledSubmission> SubmitAsync(
        HttpClient desktop,
        string token,
        Guid caseId,
        string key,
        string review,
        int proposed,
        bool withReport,
        int adverts)
    {
        var files = new List<object>();
        var named = new (int Number, string Screenshot)[adverts];
        for (var number = 1; number <= adverts; number++)
        {
            named[number - 1] = (number, $"{key}-advert-{number}.jpg");
            files.Add(Research.StagedFile(
                await StageUploadAsync(desktop, token, Bytes(key, $"advert-{number}"), $"mcp:{key}-u-{number}"),
                named[number - 1].Screenshot,
                "image/jpeg",
                nameof(MarketResearchFileKind.Screenshot)));
        }
        files.Add(Research.StagedFile(
            await StageUploadAsync(desktop, token, Bytes(key, "pack"), $"mcp:{key}-u-pack"),
            $"{key}-pack.pdf",
            "application/pdf",
            nameof(MarketResearchFileKind.EvidencePack)));
        if (withReport)
        {
            files.Add(Research.StagedFile(
                await StageUploadAsync(desktop, token, Bytes(key, "report"), $"mcp:{key}-u-report"),
                $"{key}-report.pdf",
                "application/pdf",
                nameof(MarketResearchFileKind.Report)));
        }

        using var submitted = await PostMcpAsync(
            desktop,
            token,
            Research.Submission(
                1, caseId, $"mcp:{key}", review, Research.Record(proposed, $"{key}-pack.pdf", named), [.. files]));
        var result = await ReadStructuredContentAsync(submitted);
        return new(
            result.GetProperty("submissionId").GetGuid(),
            result.GetProperty("submittedAtUtc").GetDateTimeOffset());
    }

    /// <summary>Where a refusal sends the browser, without the address it was refused.</summary>
    private static string? WithoutQuery(Uri? location) => location?.OriginalString.Split('?')[0];

    /// <summary>One file's bytes, its own for each submission and file.</summary>
    private static byte[] Bytes(string key, string file) => System.Text.Encoding.UTF8.GetBytes($"{key} {file}");

    /// <summary>
    /// The space says who submitted and when and nothing of how a report was
    /// approved (R1316), and it is read-only: no form and no control.
    /// </summary>
    private static void AssertSaysNothingOfReviewAndHoldsNoControl(string space)
    {
        Assert.DoesNotMatch(ReviewWords(), space);
        foreach (var control in new[] { "<form", "<input", "<button", "<select", "<textarea" })
        {
            Assert.DoesNotContain(control, space, StringComparison.OrdinalIgnoreCase);
        }
    }

    // A word of acceptance or review, whole or as the stored mark is spelt.
    [GeneratedRegex(@"\b(accept|review|approv|reject|filed\b)|notreviewed", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReviewWords();

    /// <summary>
    /// The space's markup, decoded as the browser reads it: it is the last
    /// thing in the Valuation section.
    /// </summary>
    private static string Space(string html)
    {
        var start = html.IndexOf("data-market-research ", StringComparison.Ordinal);
        Assert.True(start >= 0, "The Valuation section draws no market research space.");
        var end = html.IndexOf("</section>", start, StringComparison.Ordinal);
        Assert.True(end > start, "The market research space is not inside a section.");
        return WebUtility.HtmlDecode(html[start..end]);
    }

    private static string SubmissionHook(Guid submissionId) =>
        $"data-market-research-submission=\"{submissionId:D}\"";

    /// <summary>One submission's markup within the space, up to the next submission.</summary>
    private static string SubmissionBlock(string space, Guid submissionId)
    {
        var start = space.IndexOf(SubmissionHook(submissionId), StringComparison.Ordinal);
        Assert.True(start >= 0, $"The space does not show submission {submissionId:D}.");
        var next = space.IndexOf("data-market-research-submission=", start + 1, StringComparison.Ordinal);
        return next < 0 ? space[start..] : space[start..next];
    }

    private static string AdvertRow(string block, int number)
    {
        var row = Regex.Match(
            block,
            $"<tr data-market-research-advert=\"{number}\">.*?</tr>",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);
        Assert.True(row.Success, $"The submission has no row for advert {number}.");
        return row.Value;
    }

    /// <summary>The inner markup of each <paramref name="tag"/> cell, trimmed, in order.</summary>
    private static string[] Cells(string markup, string tag) =>
    [
        .. Regex.Matches(markup, $"<{tag}\\b[^>]*>(.*?)</{tag}>", RegexOptions.Singleline | RegexOptions.CultureInvariant)
            .Select(cell => cell.Groups[1].Value.Trim())
    ];

    /// <summary>The one link of <paramref name="kind"/> in the markup, as its opening tag and label.</summary>
    private static string FileLink(string markup, string kind)
    {
        var links = Regex.Matches(
            markup,
            $"<a\\b[^>]*data-market-research-file=\"{kind}\"[^>]*>.*?</a>",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);
        Assert.True(links.Count == 1, $"Expected one {kind} link and found {links.Count}.");
        return links[0].Value;
    }

    private static string Href(string link) => Attribute(link, "href");

    /// <summary>One attribute of a link in the decoded space.</summary>
    private static string Attribute(string tag, string name)
    {
        var value = Regex.Match(tag, $"\\s{name}=\"([^\"]*)\"", RegexOptions.CultureInvariant);
        Assert.True(value.Success, $"The link has no {name}.");
        return value.Groups[1].Value;
    }
}
