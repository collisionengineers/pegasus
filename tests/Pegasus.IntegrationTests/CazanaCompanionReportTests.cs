using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Pegasus.Core.Assessment;
using Pegasus.Infrastructure.Cazana;
using Pegasus.Infrastructure.Intake;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Cazana's Companion Report (ADR-0079): the lookup's timeline, comparable
/// listings and stock image are read over the signed-in session, rendered
/// through Companion's own Mustache templates with Companion's own mapping —
/// no private value, and Companion's quirks kept — and turned into a PDF by
/// <c>POST /app/pdf/create</c>.
/// </summary>
public sealed class CazanaCompanionReportTests
{
    private const string Jwt = "synthetic-jwt";
    private const string Uuid = "8e3e5efb-6539-434c-b5f1-c1180d09e7f1";
    private const string ImageUrl = "https://cazana-image-store-production.s3-eu-west-1.amazonaws.com/ab12cde.jpg";

    private static readonly DateTimeOffset Now = new(2031, 4, 15, 10, 0, 0, TimeSpan.Zero);
    private static readonly byte[] PdfBytes = "%PDF-1.7 synthetic"u8.ToArray();

    private const string TimelineJson = """
        {
          "response": {
            "vrm": "AB12CDE",
            "vin": "SB1ZB3AE40E045642",
            "firstRegistration": "2024-01-04T00:00:00Z",
            "events": [
              {
                "type": "Mot",
                "date": "2026-05-07T16:01:43Z",
                "expiryDate": "2027-05-08T00:00:00Z",
                "testNumber": 717619210402,
                "odometer": { "unit": "mi", "value": 20283 },
                "passed": true,
                "rfcAndComments": []
              },
              {
                "type": "PlateChange",
                "date": "2024-01-15T00:00:00Z",
                "previousVRM": "SA73HYT",
                "currentVRM": "AB12CDE"
              }
            ]
          }
        }
        """;

    private const string SimilarJson = """
        {
          "total": 479,
          "listings": [
            {
              "vrm": "CD34EFG",
              "year": 2024,
              "colour": "Silver",
              "mileage": { "value": 31000, "display": "31,000" },
              "price": { "value": 13500, "display": "£13,500" },
              "dealer": { "name": "Example Motors", "type": "Franchise" },
              "firstSeen": "2031-04-01"
            },
            {
              "vrm": "EF56GHJ",
              "year": 2024,
              "colour": "Black",
              "mileage": { "value": 34000, "display": "34,000" },
              "price": { "value": 13700, "display": "£13,700" },
              "dealer": { "name": "Town Cars", "type": "Independent" },
              "firstSeen": "2031-03-28"
            },
            {
              "vrm": "GH78JKL",
              "year": 2023,
              "colour": "White",
              "mileage": { "value": 36000, "display": "36,000" },
              "price": { "value": 13200, "display": "£13,200" },
              "dealer": { "name": "City Auto", "type": "Supermarket" },
              "firstSeen": "2031-03-20"
            }
          ]
        }
        """;

    [Fact]
    public async Task TheCompanionReportRendersCompanionsOwnLayoutAndPostsTheHtmlToPdfCreate()
    {
        var (report, requests) = CreateReport();

        var pdf = await report.FetchPdfAsync(default);

        Assert.Equal(PdfBytes, pdf);

        // The three reads are asked for together, so their order is the
        // network's; the page is posted once all three have answered.
        var calls = requests.Select(request => $"{request.Method} {request.Uri}").ToArray();
        Assert.Equal(
            [
                "GET https://api.cazana.com/app/companion/" + Uuid + "/timeline",
                "GET https://api.cazana.com/cz/image/2.0?vrm=AB12CDE",
                "POST https://api.cazana.com/app/listings/similar",
            ],
            calls[..3].Order(StringComparer.Ordinal).ToArray());
        Assert.Equal("POST https://api.cazana.com/app/pdf/create", calls[3]);

        using var listingsRequest = JsonDocument.Parse(RequestTo(requests, "/app/listings/similar").Body!);
        Assert.Equal(9946663, listingsRequest.RootElement.GetProperty("cid").GetInt64());
        Assert.Equal(10, listingsRequest.RootElement.GetProperty("limit").GetInt32());
        Assert.Equal("daysOnSale", listingsRequest.RootElement.GetProperty("sort").GetString());

        var html = PostedHtml(RequestTo(requests, "/app/pdf/create"));
        Assert.Contains("For internal use only, not to be shared with any 3rd party", html, StringComparison.Ordinal);
        Assert.Contains("Companion Report", html, StringComparison.Ordinal);
        Assert.Contains("AB12CDE", html, StringComparison.Ordinal);
        Assert.Contains("£13,657", html, StringComparison.Ordinal);
        Assert.Contains("£11,062", html, StringComparison.Ordinal);
        Assert.Contains("Original cost new", html, StringComparison.Ordinal);
        Assert.Contains("£N&#x2F;A", html, StringComparison.Ordinal);
        Assert.Contains("7th May 2026", html, StringComparison.Ordinal);
        Assert.Contains("Passed MOT Test", html, StringComparison.Ordinal);
        Assert.Contains("20,283 miles", html, StringComparison.Ordinal);
        Assert.Contains("Numberplate change", html, StringComparison.Ordinal);
        Assert.Contains("SA73HYT", html, StringComparison.Ordinal);
        Assert.Contains("4th January 2024", html, StringComparison.Ordinal);
        Assert.Contains("3 of 479 vehicles currently in the", html, StringComparison.Ordinal);
        Assert.Contains("Newest", html, StringComparison.Ordinal);
        Assert.Contains("cazana-logotype-dark.svg", html, StringComparison.Ordinal);
        Assert.Contains(
            "https:&#x2F;&#x2F;cazana-image-store-production.s3-eu-west-1.amazonaws.com&#x2F;ab12cde.jpg",
            html,
            StringComparison.Ordinal);

        Assert.DoesNotContain("Private value", html, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Vehicle advert full details", html, StringComparison.Ordinal);
    }

    private const string SaleTimelineJson = """
        {
          "response": {
            "vrm": "AB12CDE",
            "vin": "SB1ZB3AE40E045642",
            "firstRegistration": "2024-01-04T00:00:00Z",
            "events": [
              {
                "type": "Mot",
                "date": "2026-05-07T16:01:43Z",
                "odometer": { "unit": "mi", "value": 20283 },
                "passed": false,
                "rfcAndComments": [ { "text": "Tyre worn" }, { "text": "Brake pad <thin>" } ]
              },
              { "type": "KeeperChange", "date": "2025-02-03" },
              {
                "type": "Sale",
                "date": "2031-03-04",
                "lastSeen": "2031-04-20",
                "listings": [
                  {
                    "firstSeen": "2031-03-04",
                    "price": 12500,
                    "mileage": 31000,
                    "adText": "One owner",
                    "images": [ "https://img.example/a.jpg", "https://img.example/b.jpg" ]
                  },
                  { "firstSeen": "2031-03-10", "price": 12250, "mileage": 31500, "adText": "Price drop" }
                ]
              }
            ]
          }
        }
        """;

    /// <summary>
    /// A MOT's advisories, an owner change and a Sale print as Companion maps
    /// them: the Sale's two dates as one range, each advert with its price,
    /// mileage and text, and only the first image of an advert that has any.
    /// </summary>
    [Fact]
    public async Task AMotWithAdvisoriesAnOwnerChangeAndASaleRenderTheirTextsDatesAndAdverts()
    {
        var (report, requests) = CreateReport(path => path.EndsWith("/timeline", StringComparison.Ordinal)
            ? CazanaGuideValuationProviderTests.Json(HttpStatusCode.OK, SaleTimelineJson)
            : null);

        await report.FetchPdfAsync(default);

        var html = PostedHtml(RequestTo(requests, "/app/pdf/create"));
        Assert.Contains("Failed MOT Test", html, StringComparison.Ordinal);
        Assert.Contains("<li>Tyre worn</li>", html, StringComparison.Ordinal);
        Assert.Contains("<li>Brake pad &lt;thin&gt;</li>", html, StringComparison.Ordinal);
        Assert.Contains("3rd February 2025", html, StringComparison.Ordinal);
        Assert.Contains("4th March 2031 - 20th April 2031", html, StringComparison.Ordinal);
        Assert.Contains("10th March 2031", html, StringComparison.Ordinal);
        Assert.Contains("£12,500", html, StringComparison.Ordinal);
        Assert.Contains("£12,250", html, StringComparison.Ordinal);
        Assert.Contains("31,500", html, StringComparison.Ordinal);
        Assert.Contains("One owner", html, StringComparison.Ordinal);
        Assert.Contains("Price drop", html, StringComparison.Ordinal);
        Assert.Contains("Vehicle advert full details", html, StringComparison.Ordinal);
        Assert.Contains("https:&#x2F;&#x2F;img.example&#x2F;a.jpg", html, StringComparison.Ordinal);
        Assert.DoesNotContain("img.example&#x2F;b.jpg", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ATimelineWithoutAVinLeavesTimelineEntriesOutAndTakesFirstRegistrationFromSpecs()
    {
        var (report, requests) = CreateReport(path => path.EndsWith("/timeline", StringComparison.Ordinal)
            ? CazanaGuideValuationProviderTests.Json(HttpStatusCode.OK, """{"response":{"vrm":"AB12CDE","events":[]}}""")
            : null);

        await report.FetchPdfAsync(default);

        var html = PostedHtml(RequestTo(requests, "/app/pdf/create"));
        Assert.DoesNotContain("Passed MOT Test", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Numberplate change", html, StringComparison.Ordinal);
        Assert.Contains("4th January 2024", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFailedImageLookupFallsBackToTheWhiteLogotypeOnTheDarkTile()
    {
        var (report, requests) = CreateReport(path => path == "/cz/image/2.0"
            ? CazanaGuideValuationProviderTests.Json(HttpStatusCode.InternalServerError, "{}")
            : null);

        await report.FetchPdfAsync(default);

        var html = PostedHtml(RequestTo(requests, "/app/pdf/create"));
        Assert.Contains("cazana-logotype-white.svg", html, StringComparison.Ordinal);
        Assert.Contains("justify-center bg-tertiary border-transparent", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// A connection that drops while the image answer is being read is the
    /// same silent fallback as a refused image lookup: only the image is lost.
    /// </summary>
    [Fact]
    public async Task AnImageAnswerThatDropsMidBodyFallsBackToTheWhiteLogotypeOnTheDarkTile()
    {
        var (report, requests) = CreateReport(path => path == "/cz/image/2.0"
            ? CazanaGuideValuationProviderTests.Dropped(HttpStatusCode.OK, """{"image":["https://""")
            : null);

        var pdf = await report.FetchPdfAsync(default);

        Assert.Equal(PdfBytes, pdf);
        var html = PostedHtml(RequestTo(requests, "/app/pdf/create"));
        Assert.Contains("cazana-logotype-white.svg", html, StringComparison.Ordinal);
        Assert.Contains("justify-center bg-tertiary border-transparent", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ATimelineAnswerThatDropsMidBodyIsNamedByItsStageAndNothingIsPosted()
    {
        var (report, requests) = CreateReport(path => path.EndsWith("/timeline", StringComparison.Ordinal)
            ? CazanaGuideValuationProviderTests.Dropped(HttpStatusCode.OK, """{"response":{"vrm":""")
            : null);

        var failed = await Assert.ThrowsAsync<CazanaCompanionFailure>(() => report.FetchPdfAsync(default));

        Assert.Equal("timeline", failed.Stage);
        Assert.Null(failed.Status);
        Assert.Equal("body=unreadable", failed.Detail);
        Assert.DoesNotContain(requests, request => request.Uri.AbsolutePath == "/app/pdf/create");
    }

    [Fact]
    public async Task APdfCreateFailureIsNamedAsThePdfStage()
    {
        var (report, _) = CreateReport(path => path == "/app/pdf/create"
            ? CazanaGuideValuationProviderTests.Json(HttpStatusCode.InternalServerError, """{"error":"Chromium failed"}""")
            : null);

        var failed = await Assert.ThrowsAsync<CazanaCompanionFailure>(() => report.FetchPdfAsync(default));

        Assert.Equal("pdf", failed.Stage);
        Assert.Equal(HttpStatusCode.InternalServerError, failed.Status);
    }

    /// <summary>
    /// Cazana timing out on the timeline is no timeline, as Companion treats it;
    /// any other timeline or listings refusal stops the report at its stage.
    /// </summary>
    [Fact]
    public async Task ATimelineThatTimesOutLeavesTheTimelineOutOfTheReport()
    {
        var (report, requests) = CreateReport(path => path.EndsWith("/timeline", StringComparison.Ordinal)
            ? CazanaGuideValuationProviderTests.Json(HttpStatusCode.RequestTimeout, "{}")
            : null);

        await report.FetchPdfAsync(default);

        var html = PostedHtml(RequestTo(requests, "/app/pdf/create"));
        Assert.DoesNotContain("Passed MOT Test", html, StringComparison.Ordinal);
        Assert.Contains("4th January 2024", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("timeline", "/app/companion/" + Uuid + "/timeline")]
    [InlineData("listings", "/app/listings/similar")]
    public async Task ATimelineOrListingsFailureIsNamedByItsStageAndNothingIsPosted(string stage, string failingPath)
    {
        var (report, requests) = CreateReport(path => path == failingPath
            ? CazanaGuideValuationProviderTests.Json(HttpStatusCode.InternalServerError, """{"error":"Server Error"}""")
            : null);

        var failed = await Assert.ThrowsAsync<CazanaCompanionFailure>(() => report.FetchPdfAsync(default));

        Assert.Equal(stage, failed.Stage);
        Assert.DoesNotContain(requests, request => request.Uri.AbsolutePath == "/app/pdf/create");
    }

    /// <summary>
    /// A request that fails, or a call that runs past its bound, on a report
    /// read or on the PDF service is that stage's transport failure: named by
    /// its stage, carrying the exception, and nothing is posted after a read.
    /// </summary>
    [Theory]
    [InlineData("timeline", "/app/companion/" + Uuid + "/timeline")]
    [InlineData("listings", "/app/listings/similar")]
    [InlineData("pdf", "/app/pdf/create")]
    public async Task ATransportFailureOrATimeoutOnAReportCallIsNamedByItsStage(string stage, string failingPath)
    {
        foreach (Exception cause in new Exception[]
        {
            new HttpRequestException("connection refused"),
            new TaskCanceledException("timed out"),
        })
        {
            var (report, requests) = CreateReport(path =>
            {
                if (path == failingPath)
                {
                    throw cause;
                }

                return null;
            });

            var failed = await Assert.ThrowsAsync<CazanaCompanionFailure>(() => report.FetchPdfAsync(default));

            Assert.Equal(stage, failed.Stage);
            Assert.True(failed.Transport);
            Assert.Null(failed.Status);
            Assert.Equal("transport=" + cause.GetType().Name, failed.Detail);
            Assert.IsType(cause.GetType(), failed.InnerException);
            Assert.Equal(
                stage == "pdf" ? 1 : 0,
                requests.Count(request => request.Uri.AbsolutePath == "/app/pdf/create"));
        }
    }

    /// <summary>
    /// A stock image lookup that gets no answer is the same silent fallback as
    /// a refused one: only the image is lost.
    /// </summary>
    [Theory]
    [InlineData("connection")]
    [InlineData("timeout")]
    public async Task AnImageLookupThatGetsNoAnswerFallsBackToTheWhiteLogotypeOnTheDarkTile(string kind)
    {
        Exception cause = kind == "connection"
            ? new HttpRequestException("connection refused")
            : new TaskCanceledException("timed out");
        var (report, requests) = CreateReport(path =>
        {
            if (path == "/cz/image/2.0")
            {
                throw cause;
            }

            return null;
        });

        var pdf = await report.FetchPdfAsync(default);

        Assert.Equal(PdfBytes, pdf);
        var html = PostedHtml(RequestTo(requests, "/app/pdf/create"));
        Assert.Contains("cazana-logotype-white.svg", html, StringComparison.Ordinal);
        Assert.Contains("justify-center bg-tertiary border-transparent", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Every kind of tag renders to exactly these bytes: each of the eight
    /// escaped characters at the start, middle and end of a value and side by
    /// side, non-ASCII text, whole numbers and decimals written as they are,
    /// flags, lists with a nested list, inverted sections, a partial and a
    /// comment.
    /// </summary>
    [Fact]
    public void MustacheRendersEveryKindOfTagToTheSameBytes()
    {
        const string printed = "£13,657 café 😀";
        var view = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["a"] = "&&a<b>c\"d'e/f`g=",
            ["a2"] = printed,
            ["n"] = 42,
            ["l"] = -9876543210L,
            ["d"] = 12.50m,
            ["dn"] = -0.5m,
            ["t"] = true,
            ["f"] = false,
            ["e"] = "",
            ["items"] = new List<object?>
            {
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["name"] = "a&b",
                    ["n"] = 1,
                    ["tags"] = new List<object?> { "x", "y/z" },
                },
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "<c>" },
            },
            ["empty"] = new List<object?>(),
            ["flag"] = true,
            ["obj"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["k"] = "v/w" },
        };
        var partials = new Dictionary<string, CazanaMustache.Template>(StringComparer.Ordinal)
        {
            ["part"] = CazanaMustache.Parse("<{{a2}}>"),
        };

        var rendered = CazanaMustache.Render(
            CazanaMustache.Parse(
                "A{{a}}|{{a2}}|{{n}}|{{l}}|{{d}}|{{dn}}|{{t}}|{{f}}|{{missing}}|"
                + "{{#items}}[{{name}}:{{n}}{{#tags}}<{{.}}>{{/tags}}]{{/items}}|{{^items}}none{{/items}}|"
                + "{{^empty}}none{{/empty}}|{{#flag}}yes{{/flag}}|{{#obj}}{{k}}{{/obj}}|{{>part}}|{{! note }}{{e}}Z"),
            view,
            partials);

        Assert.Equal(
            "A&amp;&amp;a&lt;b&gt;c&quot;d&#39;e&#x2F;f&#x60;g&#x3D;|" + printed
            + "|42|-9876543210|12.50|-0.5|true|false||[a&amp;b:1<x><y&#x2F;z>][&lt;c&gt;:42]||none|yes|v&#x2F;w|<"
            + printed + ">|Z",
            rendered);
    }

    [Fact]
    public void MustacheEscapesAsMustacheJsAndResolvesLengthsListsAndParentFrames()
    {
        var view = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["a"] = "<&\"'/=`",
            ["s"] = "",
            ["list"] = new List<object?> { "one", "two" },
            ["colour"] = "#581c87",
            ["rows"] = new List<object?>
            {
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["label"] = "Row" },
            },
        };

        var rendered = CazanaMustache.Render(
            CazanaMustache.Parse(
                "{{a}}|{{#s.length}}x{{/s.length}}|{{#list}}{{.}},{{/list}}|{{#rows}}{{label}}:{{colour}}{{/rows}}"),
            view);

        Assert.Equal("&lt;&amp;&quot;&#39;&#x2F;&#x3D;&#x60;||one,two,|Row:#581c87", rendered);
    }

    /// <summary>
    /// A report of several pages is read only as far as the first page that
    /// names the registration, not to its last page.
    /// </summary>
    [Fact]
    public async Task TheTextReadStopsAtTheFirstPageThatNamesTheRegistration()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        string[] printedPages = ["Market overview", "Companion Report AB12CDE", "Vehicle advert full details"];
        foreach (var printed in printedPages)
        {
            builder.AddPage(PageSize.A4).AddText(printed, 12, new PdfPoint(36, 780), font);
        }

        var read = await new PdfPigPageTextExtractor().ExtractAsync(
            builder.Build(),
            20_000,
            default,
            text => GlassRepairEstimateSessionPolicy.NamesCompactedRegistration(text, "AB12CDE"));

        Assert.NotNull(read);
        Assert.Equal(3, read.PageCount);
        Assert.Equal(2, read.Pages.Count);
        Assert.Equal(2, read.Pages[^1].Number);
    }

    /// <summary>
    /// A page holding fewer readable characters than the floor (80, not
    /// counting whitespace) is flagged as needing OCR; one holding the floor is not.
    /// </summary>
    [Theory]
    [InlineData(79, true)]
    [InlineData(80, false)]
    public async Task APageIsFlaggedAsNeedingOcrBelowTheReadableFloor(int readable, bool needsOcr)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        builder.AddPage(PageSize.A4).AddText(
            string.Concat(Enumerable.Repeat("abcdefghij", 8))[..readable], 12, new PdfPoint(36, 780), font);

        var read = await new PdfPigPageTextExtractor().ExtractAsync(builder.Build(), 20_000, default);

        Assert.NotNull(read);
        Assert.Equal(needsOcr, Assert.Single(read.Pages).NeedsOcr);
    }

    private static CazanaGuideValuationProviderTests.RecordedRequest RequestTo(
        ConcurrentQueue<CazanaGuideValuationProviderTests.RecordedRequest> requests, string path) =>
        requests.Single(request => request.Uri.AbsolutePath == path);

    private static (CazanaCompanionReport Report, ConcurrentQueue<CazanaGuideValuationProviderTests.RecordedRequest> Requests) CreateReport(
        Func<string, HttpResponseMessage?>? overridePath = null)
    {
        var requests = new ConcurrentQueue<CazanaGuideValuationProviderTests.RecordedRequest>();
        using var document = JsonDocument.Parse(CazanaGuideValuationProviderTests.LookupJson());
        var response = document.RootElement.GetProperty("response").Clone();
        var http = new HttpClient(new CazanaGuideValuationProviderTests.Answering(async request =>
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync();
            requests.Enqueue(new(
                request.Method,
                request.RequestUri!,
                request.Headers.Authorization?.ToString(),
                request.Headers.ToDictionary(header => header.Key, header => header.Value.ToArray(), StringComparer.OrdinalIgnoreCase),
                body));
            var path = request.RequestUri!.AbsolutePath;
            if (overridePath?.Invoke(path) is { } overridden)
            {
                return overridden;
            }

            return path switch
            {
                "/app/companion/" + Uuid + "/timeline" =>
                    CazanaGuideValuationProviderTests.Json(HttpStatusCode.OK, TimelineJson),
                "/app/listings/similar" =>
                    CazanaGuideValuationProviderTests.Json(HttpStatusCode.OK, SimilarJson),
                "/cz/image/2.0" =>
                    CazanaGuideValuationProviderTests.Json(
                        HttpStatusCode.OK, $$$"""{"image":["{{{ImageUrl}}}"]}"""),
                "/app/pdf/create" =>
                    CazanaGuideValuationProviderTests.Json(
                        HttpStatusCode.OK, $$$"""{"data":"{{{Convert.ToBase64String(PdfBytes)}}}"}"""),
                _ => CazanaGuideValuationProviderTests.Json(HttpStatusCode.NotFound, "{}"),
            };
        }));
        var report = new CazanaCompanionReport(
            new CazanaCompanionClient(http),
            Jwt,
            Uuid,
            response,
            new CazanaGuideValuationProviderTests.FixedClock(Now));
        return (report, requests);
    }

    private static string PostedHtml(CazanaGuideValuationProviderTests.RecordedRequest pdfRequest)
    {
        using var body = JsonDocument.Parse(pdfRequest.Body!);
        return Encoding.UTF8.GetString(Convert.FromBase64String(body.RootElement.GetProperty("html").GetString()!));
    }
}
