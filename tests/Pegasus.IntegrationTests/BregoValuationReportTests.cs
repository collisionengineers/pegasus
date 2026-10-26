using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Pegasus.Core.Assessment;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Brego;
using Pegasus.Infrastructure.Intake;
using SkiaSharp;
using UglyToad.PdfPig;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Brego's Valuation Report PDF (ADR-0084): factory option configuration
/// filtering, <c>POST /platform/api/pdf/{organizationId}</c> with
/// <c>featuredMonthNav: "high"</c> and <c>showPdfReportImages: true</c>,
/// automatic 14×16 broken-image repair using <c>view=front</c> and
/// <c>view=side</c> studio stock images from <c>assets.brego.io</c>, and
/// end-to-end custody filing through <see cref="FileGuideValuationReport"/>.
/// </summary>
public sealed class BregoValuationReportTests
{
    private const string OptionsHash = "hash-2024-corolla-icon";

    private const string OptionConfigurationJson = $$$"""
        {
          "optionsHash": "{{{OptionsHash}}}",
          "versionNumber": 3,
          "options": [
            {
              "optionId": "opt-default-active",
              "default": true,
              "dateEffectiveFrom": "2023-01-01T00:00:00.000Z",
              "dateEffectiveTo": "2025-12-31T00:00:00.000Z"
            },
            {
              "optionId": "opt-default-no-dates",
              "default": true
            },
            {
              "optionId": "opt-default-expired",
              "default": true,
              "dateEffectiveFrom": "2020-01-01T00:00:00.000Z",
              "dateEffectiveTo": "2023-12-31T00:00:00.000Z"
            },
            {
              "optionId": "opt-default-future",
              "default": true,
              "dateEffectiveFrom": "2024-06-01T00:00:00.000Z",
              "dateEffectiveTo": "2026-12-31T00:00:00.000Z"
            },
            {
              "optionId": "opt-non-default",
              "default": false,
              "dateEffectiveFrom": "2023-01-01T00:00:00.000Z",
              "dateEffectiveTo": "2025-12-31T00:00:00.000Z"
            }
          ]
        }
        """;

    [Fact]
    public async Task ValuationReportSelectsEffectiveDefaultOptionsPostsHighFeaturedNavAndFilesThroughFileGuideValuationReport()
    {
        var cleanPdf = BuildSkiaStylePdf(includeBroken14x16Image: false);
        var harness = CreateReportHarness(cleanPdf);

        var quote = await harness.Provider.GetAsync(
            new GuideValuationRequest(
                ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]),
                Guid.NewGuid(),
                "AB12 CDE",
                33000,
                null,
                BregoGuideValuationProviderTests.April),
            default);

        Assert.NotNull(quote.Report);
        var custody = new RecordingArtifactCustody();
        var filer = new FileGuideValuationReport(custody, new PdfPigPageTextExtractor());
        var caseId = Guid.NewGuid();

        var result = await filer.ExecuteAsync(
            new FileGuideValuationReportRequest(
                ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]),
                caseId,
                ValuationSource.Brego,
                "AB12 CDE",
                quote.GuideMonth,
                quote.Report!),
            default);

        Assert.Equal(CaseArtifactCustodyDisposition.Confirmed, result.Disposition);
        var retained = Assert.Single(custody.Requests);
        Assert.Equal(caseId, retained.CaseId);
        Assert.Equal("Brego valuation AB12 CDE 2031-04.pdf", retained.FileName);
        Assert.Equal(DocumentSemanticRole.ValuationEvidence, retained.SemanticRole);
        Assert.Equal(DocumentSource.Generated, retained.Source);
        Assert.Equal(cleanPdf, custody.RetainedBytes);

        // No 14x16 image in cleanPdf, so assets.brego.io was never called.
        Assert.DoesNotContain(harness.Requests, request => request.Uri.Host == "assets.brego.io");

        var pdfRequest = Assert.Single(
            harness.Requests,
            request => request.Uri.AbsolutePath == $"/platform/api/pdf/{BregoGuideValuationProviderTests.OrganizationId}");
        using var payload = JsonDocument.Parse(pdfRequest.Body!);
        var root = payload.RootElement;
        Assert.Equal("toyota", root.GetProperty("manufacturerUrl").GetString());
        Assert.Equal("corolla", root.GetProperty("rangeUrl").GetString());
        Assert.Equal("valuation", root.GetProperty("reportType").GetString());
        Assert.Equal(BregoGuideValuationProviderTests.DerivativeId, root.GetProperty("derivativeId").GetString());
        Assert.Equal(33000, root.GetProperty("mileage").GetInt64());
        Assert.Equal(2, root.GetProperty("previousOwners").GetInt32());
        Assert.Equal(7800, root.GetProperty("annualMileage").GetInt32());
        Assert.Equal("high", root.GetProperty("featuredMonthNav").GetString());
        Assert.Equal(BregoGuideValuationProviderTests.Vin, root.GetProperty("vin").GetString());
        Assert.Equal("AB12CDE", root.GetProperty("fallbackVrm").GetString());
        Assert.Equal("2024-01-04", root.GetProperty("registrationDate").GetString());
        Assert.True(root.GetProperty("showPdfReportImages").GetBoolean());
        Assert.Equal(
            ["opt-default-active", "opt-default-no-dates"],
            root.GetProperty("selectedOptions").EnumerateArray().Select(item => item.GetString()!).ToArray());
        Assert.Equal(
            OptionsHash,
            root.GetProperty("optionSelectionInput").GetProperty("optionConfigurationHash").GetString());
    }

    [Fact]
    public async Task Broken14x16ListingImagesOnPages1And2AreReplacedInPlaceWithFrontAndSideStudioStockImages()
    {
        var brokenPdf = BuildSkiaStylePdf(includeBroken14x16Image: true);
        var frontPng = CreatePngBytes(width: 64, height: 48, new SKColor(30, 90, 180, 255));
        var sidePng = CreatePngBytes(width: 80, height: 40, new SKColor(40, 140, 90, 220));
        var harness = CreateReportHarness(brokenPdf, frontPng, sidePng);

        var quote = await harness.Provider.GetAsync(
            new GuideValuationRequest(
                ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]),
                Guid.NewGuid(),
                "AB12CDE",
                33000,
                null,
                BregoGuideValuationProviderTests.April),
            default);

        var repairedPdf = await quote.Report!.FetchPdfAsync(default);

        Assert.NotEqual(brokenPdf, repairedPdf);
        Assert.Contains(
            harness.Requests,
            request => request.Uri.Host == "assets.brego.io" && request.Uri.Query.Contains("view=front", StringComparison.Ordinal));
        Assert.Contains(
            harness.Requests,
            request => request.Uri.Host == "assets.brego.io" && request.Uri.Query.Contains("view=side", StringComparison.Ordinal));

        using (var document = PdfDocument.Open(repairedPdf))
        {
            Assert.Equal(2, document.NumberOfPages);
            var page1Images = document.GetPage(1).GetImages().ToArray();
            var page2Images = document.GetPage(2).GetImages().ToArray();

            var hero = Assert.Single(page1Images);
            Assert.Equal(64, hero.WidthInSamples);
            Assert.Equal(48, hero.HeightInSamples);

            var side = Assert.Single(page2Images);
            Assert.Equal(80, side.WidthInSamples);
            Assert.Equal(40, side.HeightInSamples);
        }

        // Verify PdfPigPageTextExtractor still extracts the registration text from Page 2 after repair.
        var extracted = await new PdfPigPageTextExtractor().ExtractAsync(
            repairedPdf,
            20_000,
            default,
            text => GlassRepairEstimateSessionPolicy.NamesCompactedRegistration(text, "AB12CDE"));
        Assert.NotNull(extracted);
        Assert.Equal(2, extracted.Pages.Count);
        Assert.Contains("AB12CDE", extracted.Pages[1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StockImageFetchFailureLeavesOriginalPdfBytesIntactSoFilingStillSucceeds()
    {
        var brokenPdf = BuildSkiaStylePdf(includeBroken14x16Image: true);
        var harness = CreateReportHarness(brokenPdf, frontPng: null, sidePng: null);

        var quote = await harness.Provider.GetAsync(
            new GuideValuationRequest(
                ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]),
                Guid.NewGuid(),
                "AB12CDE",
                33000,
                null,
                BregoGuideValuationProviderTests.April),
            default);

        var pdf = await quote.Report!.FetchPdfAsync(default);

        Assert.Equal(brokenPdf, pdf);
    }

    [Fact]
    public async Task ReportRefreshesSessionOnceWhenInitialSessionExpiredBeforeBackgroundExecution()
    {
        var cleanPdf = BuildSkiaStylePdf(includeBroken14x16Image: false);
        var failFirstOptionsCall = true;
        var harness = CreateReportHarness(
            cleanPdf,
            overrideRequest: request =>
            {
                if (failFirstOptionsCall
                    && request.RequestUri!.AbsolutePath.EndsWith("/option-configuration", StringComparison.Ordinal))
                {
                    failFirstOptionsCall = false;
                    return BregoGuideValuationProviderTests.Json(
                        HttpStatusCode.Unauthorized,
                        """{"message":"Session expired"}""");
                }

                return null;
            });

        var quote = await harness.Provider.GetAsync(
            new GuideValuationRequest(
                ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]),
                Guid.NewGuid(),
                "AB12CDE",
                33000,
                null,
                BregoGuideValuationProviderTests.April),
            default);

        var pdf = await quote.Report!.FetchPdfAsync(default);

        Assert.Equal(cleanPdf, pdf);
        Assert.Equal(
            2,
            harness.Requests.Count(request => request.Uri.AbsolutePath == "/login/api/brego/v1/sessions"));
    }

    private static BregoGuideValuationProviderTests.Harness CreateReportHarness(
        byte[] pdfBytes,
        byte[]? frontPng = null,
        byte[]? sidePng = null,
        Func<HttpRequestMessage, HttpResponseMessage?>? overrideRequest = null)
    {
        return BregoGuideValuationProviderTests.Harness.Create(request =>
        {
            if (overrideRequest?.Invoke(request) is { } custom)
            {
                return custom;
            }

            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/option-configuration", StringComparison.Ordinal))
            {
                return BregoGuideValuationProviderTests.Json(HttpStatusCode.OK, OptionConfigurationJson);
            }

            if (path == $"/platform/api/pdf/{BregoGuideValuationProviderTests.OrganizationId}")
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(pdfBytes),
                };
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
                return response;
            }

            if (request.RequestUri.Host == "assets.brego.io")
            {
                if (request.RequestUri.Query.Contains("view=front", StringComparison.Ordinal) && frontPng is not null)
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(frontPng),
                    };
                }

                if (request.RequestUri.Query.Contains("view=side", StringComparison.Ordinal) && sidePng is not null)
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(sidePng),
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            return null;
        });
    }

    private static byte[] CreatePngBytes(int width, int height, SKColor color)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        bitmap.Erase(color);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>
    /// Builds a valid 2-page PDF matching Skia/PDF m156's structure, with Page 1
    /// cover text, Page 2 vehicle summary text containing <c>AB12CDE</c>, and
    /// either a 14×16 broken-image placeholder XObject or a 32×24 valid image XObject.
    /// </summary>
    private static byte[] BuildSkiaStylePdf(bool includeBroken14x16Image)
    {
        var imgWidth = includeBroken14x16Image ? 14 : 32;
        var imgHeight = includeBroken14x16Image ? 16 : 24;
        var rawRgb = new byte[imgWidth * imgHeight * 3];
        var compressedRgb = CompressZlib(rawRgb);

        var p1Text =
            "BT /F1 12 Tf 36 780 Td (Brego Valuation Report Cover Page - Market Intelligence Summary and Overview For Vehicle Valuation) Tj ET\n" +
            $"q\n{imgWidth} 0 0 -{imgHeight} 200 480 cm\n0 0 0 RG 0 0 0 rg\n/G3 gs\nEMC\n/X13 Do\nQ\n";
        var p2Text =
            "BT /F1 12 Tf 36 780 Td (Brego Vehicle Valuation Summary for Registration AB12CDE - Toyota Corolla 1.8 VVT-h Icon Touring Sports) Tj ET\n" +
            $"q\n{imgWidth} 0 0 -{imgHeight} 168.75 825 cm\n0 0 0 RG 0 0 0 rg\n/G3 gs\n/X13 Do\nQ\n";

        var c1Compressed = CompressZlib(Encoding.Latin1.GetBytes(p1Text));
        var c2Compressed = CompressZlib(Encoding.Latin1.GetBytes(p2Text));

        var objects = new List<(int Id, byte[] Bytes)>
        {
            (1, AsciiObject(1, "<</Title (Brego Valuation Report)>>")),
            (3, AsciiObject(3, "<</ca 1\n/BM /Normal>>")),
            (9, AsciiObject(9, "<</Type /Font\n/Subtype /Type1\n/BaseFont /Helvetica>>")),
            (13, StreamObject(
                13,
                $"<</Type /XObject\n/Subtype /Image\n/Width {imgWidth}\n/Height {imgHeight}\n/ColorSpace /DeviceRGB\n/BitsPerComponent 8\n/Filter /FlateDecode\n/Length {compressedRgb.Length}>>",
                compressedRgb)),
            (2, AsciiObject(
                2,
                "<</Type /Page\n/Resources <</ExtGState <</G3 3 0 R>>\n/XObject <</X13 13 0 R>>\n/Font <</F1 9 0 R>>>>\n/MediaBox [0 0 595.92 841.92]\n/Contents 37 0 R\n/Parent 125 0 R>>")),
            (37, StreamObject(37, $"<</Filter /FlateDecode\n/Length {c1Compressed.Length}>>", c1Compressed)),
            (38, AsciiObject(
                38,
                "<</Type /Page\n/Resources <</ExtGState <</G3 3 0 R>>\n/XObject <</X13 13 0 R>>\n/Font <</F1 9 0 R>>>>\n/MediaBox [0 0 595.92 841.92]\n/Contents 51 0 R\n/Parent 125 0 R>>")),
            (51, StreamObject(51, $"<</Filter /FlateDecode\n/Length {c2Compressed.Length}>>", c2Compressed)),
            (125, AsciiObject(125, "<</Type /Pages\n/Count 2\n/Kids [2 0 R 38 0 R]>>")),
            (128, AsciiObject(128, "<</Type /Catalog\n/Pages 125 0 R>>")),
        };

        const int size = 129;
        var offsets = new long[size];
        using var output = new MemoryStream();
        var header = "%PDF-1.4\n%\xD3\xEB\xE9\xE1\n";
        var headerBytes = Encoding.Latin1.GetBytes(header);
        output.Write(headerBytes, 0, headerBytes.Length);

        foreach (var (id, bytes) in objects)
        {
            offsets[id] = output.Position;
            output.Write(bytes, 0, bytes.Length);
        }

        var xrefPos = output.Position;
        var trailer = new StringBuilder();
        trailer.Append(CultureInfo.InvariantCulture, $"xref\n0 {size}\n0000000000 65535 f \n");
        for (var id = 1; id < size; id++)
        {
            if (offsets[id] > 0)
            {
                trailer.Append(CultureInfo.InvariantCulture, $"{offsets[id]:D10} 00000 n \n");
            }
            else
            {
                trailer.Append("0000000000 65535 f \n");
            }
        }

        trailer.Append(CultureInfo.InvariantCulture, $"trailer\n<</Size {size}\n/Root 128 0 R\n/Info 1 0 R>>\nstartxref\n{xrefPos}\n%%EOF\n");
        var trailerBytes = Encoding.Latin1.GetBytes(trailer.ToString());
        output.Write(trailerBytes, 0, trailerBytes.Length);
        return output.ToArray();
    }

    private static byte[] AsciiObject(int id, string dictionary) =>
        Encoding.Latin1.GetBytes(string.Create(CultureInfo.InvariantCulture, $"{id} 0 obj\n{dictionary}\nendobj\n"));

    private static byte[] StreamObject(int id, string dictionary, byte[] streamBytes)
    {
        var prefix = Encoding.Latin1.GetBytes(string.Create(CultureInfo.InvariantCulture, $"{id} 0 obj\n{dictionary} stream\n"));
        var suffix = Encoding.Latin1.GetBytes("\nendstream\nendobj\n");
        var total = new byte[prefix.Length + streamBytes.Length + suffix.Length];
        Buffer.BlockCopy(prefix, 0, total, 0, prefix.Length);
        Buffer.BlockCopy(streamBytes, 0, total, prefix.Length, streamBytes.Length);
        Buffer.BlockCopy(suffix, 0, total, prefix.Length + streamBytes.Length, suffix.Length);
        return total;
    }

    private static byte[] CompressZlib(ReadOnlySpan<byte> raw)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Fastest, leaveOpen: true))
        {
            zlib.Write(raw);
        }

        return output.ToArray();
    }

    private sealed class RecordingArtifactCustody : ICaseArtifactCustody
    {
        public List<CaseArtifactCustodyRequest> Requests { get; } = [];

        public byte[] RetainedBytes { get; private set; } = [];

        public async Task<CaseArtifactCustodyResult> RetainAsync(
            CaseArtifactCustodyRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            using var ms = new MemoryStream();
            await request.Content.CopyToAsync(ms, cancellationToken);
            RetainedBytes = ms.ToArray();
            return new CaseArtifactCustodyResult(
                CaseArtifactCustodyDisposition.Confirmed,
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                "box-file-id",
                "box-version-id",
                request.Sha256,
                request.ContentLength,
                request.MediaType,
                null,
                null);
        }
    }
}
