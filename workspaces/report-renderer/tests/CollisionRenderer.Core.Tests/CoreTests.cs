using System.Text.Json;
using CollisionRenderer.Core.Design;
using CollisionRenderer.Core.Models;
using CollisionRenderer.Core.Rendering;
using CollisionRenderer.Core.Templating;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using Xunit;

namespace CollisionRenderer.Core.Tests;

public class CatalogTests
{
    [Fact]
    public void Lists_the_builtin_templates()
    {
        var ids = CollisionRendererFactory.Catalog.List().Select(t => t.Id).ToArray();
        Assert.Contains("market-valuation-evidence", ids);
        Assert.Contains("advert-evidence-pack", ids);
        Assert.Contains("fee-note", ids);
        Assert.Contains("expert-report", ids);
        Assert.Contains("total-loss-report", ids);
        Assert.Contains("repairable-contract-repair-report", ids);
        Assert.Contains("addendum-report", ids);
        Assert.Contains("diminution-rebuttal", ids);
        Assert.Contains("roadworthy-criminal-report", ids);
        Assert.Contains("part-35-response", ids);
        Assert.Contains("response-letter", ids);
    }

    [Theory]
    [MemberData(nameof(AllTemplateIds))]
    public void Every_sample_payload_deserialises_and_validates(string id)
    {
        var descriptor = CollisionRendererFactory.Catalog.Get(id);
        var json = CollisionRendererFactory.AuthoringCatalog.GetStarterJson(id);

        var model = JsonSerializer.Deserialize(json, descriptor.ModelType, CrJson.Options);
        Assert.NotNull(model);

        var result = new PayloadValidator().Validate(id, model!);
        Assert.True(result.Ok, string.Join("; ", result.Errors));
    }

    public static IEnumerable<object[]> AllTemplateIds() =>
        CollisionRendererFactory.Catalog.List().Select(t => new object[] { t.Id });
}

public class AuthoringCatalogTests
{
    [Fact]
    public void Lists_reference_backed_authoring_templates()
    {
        var ids = CollisionRendererFactory.AuthoringCatalog.List().Select(t => t.Id).ToArray();

        Assert.Contains("market-valuation-evidence", ids);
        Assert.Contains("advert-evidence-pack", ids);
        Assert.Contains("fee-note", ids);
        Assert.Contains("total-loss-report", ids);
        Assert.Contains("repairable-contract-repair-report", ids);
        Assert.Contains("addendum-report", ids);
        Assert.Contains("diminution-rebuttal", ids);
        Assert.Contains("roadworthy-criminal-report", ids);
        Assert.Contains("part-35-response", ids);
        Assert.Contains("response-letter", ids);
    }

    [Fact]
    public void Every_authoring_template_maps_to_a_render_template_and_blank_payload()
    {
        foreach (var authoring in CollisionRendererFactory.AuthoringCatalog.List())
        {
            var renderDescriptor = CollisionRendererFactory.Catalog.Get(authoring.RenderTemplateId);
            var form = CollisionRendererFactory.AuthoringCatalog.GetForm(authoring.Id);
            var blankJson = CollisionRendererFactory.AuthoringCatalog.GetBlankJson(authoring.Id);

            Assert.Equal(authoring.Id, form.TemplateId);
            Assert.Equal(authoring.RenderTemplateId, form.RenderTemplateId);
            Assert.NotEmpty(form.Sections);
            Assert.All(Flatten(form.Sections), field =>
            {
                Assert.False(string.IsNullOrWhiteSpace(field.Id));
                Assert.False(string.IsNullOrWhiteSpace(field.Label));
                Assert.False(string.IsNullOrWhiteSpace(field.Path));
            });

            var model = JsonSerializer.Deserialize(blankJson, renderDescriptor.ModelType, CrJson.Options);
            Assert.NotNull(model);
        }
    }

    private static IEnumerable<DocumentFormField> Flatten(IEnumerable<DocumentFormSection> sections) =>
        sections.SelectMany(s => Flatten(s.Fields));

    private static IEnumerable<DocumentFormField> Flatten(IEnumerable<DocumentFormField> fields)
    {
        foreach (var field in fields)
        {
            yield return field;
            foreach (var child in Flatten(field.Fields))
            {
                yield return child;
            }
        }
    }
}

public class FormatTests
{
    [Theory]
    [InlineData("24750", true, "£24,750.00")]
    [InlineData("24750", false, "£24,750")]
    [InlineData("£1,200.50", true, "£1,200.50")]
    [InlineData("GBP 999", false, "£999")]
    public void Money_normalises(string input, bool decimals, string expected) =>
        Assert.Equal(expected, Format.Money(input, decimals));

    [Theory]
    [InlineData("31450", "31,450")]
    [InlineData("62,000 miles", "62,000 miles")]
    public void Mileage_groups_digits(string input, string expected) =>
        Assert.Equal(expected, Format.Mileage(input));

    [Theory]
    [InlineData("March 2022", "2022")]
    [InlineData("2021", "2021")]
    public void Year_extracts_four_digits(string input, string expected) =>
        Assert.Equal(expected, Format.Year(input));

    [Fact]
    public void VehicleHistory_defaults_when_blank() =>
        Assert.Equal("Assumed full service history unless stated otherwise", Format.VehicleHistory(""));

    [Fact]
    public void VehicleHistory_preserves_material_markers() =>
        Assert.Contains("Cat S", Format.VehicleHistory("Recorded Cat S"));

    [Theory]
    [InlineData("https://x/a", "https://x/a")]
    [InlineData("http://x", "http://x")]
    [InlineData("mailto:a@b.com", "mailto:a@b.com")]
    [InlineData("/relative/path", "/relative/path")]
    [InlineData("javascript:alert(1)", "")]
    [InlineData("data:text/html,x", "")]
    [InlineData("file:///etc/passwd", "")]
    public void SafeUrl_only_allows_safe_schemes(string input, string expected) =>
        Assert.Equal(expected, Format.SafeUrl(input));
}

public class ValidationTests
{
    [Fact]
    public void Valuation_requires_assessed_value()
    {
        var model = new MarketValuationEvidenceDocument { Subject = new SubjectVehicle { Registration = "AB12CDE" } };
        var result = new PayloadValidator().Validate("market-valuation-evidence", model);
        Assert.False(result.Ok);
        Assert.Contains(result.Errors, e => e.Contains("assessedRetailValue"));
    }

    [Fact]
    public void EvidencePack_requires_adverts()
    {
        var model = new AdvertEvidencePackDocument { Subject = new SubjectVehicle { Registration = "AB12CDE" } };
        var result = new PayloadValidator().Validate("advert-evidence-pack", model);
        Assert.False(result.Ok);
    }

    [Fact]
    public void ExpertReport_rejects_unknown_block_type()
    {
        var model = new ExpertReportDocument
        {
            Title = "X",
            Sections = { new ReportSection { Blocks = { new ContentBlock { Type = "wat" } } } },
        };
        var result = new PayloadValidator().Validate("expert-report", model);
        Assert.False(result.Ok);
        Assert.Contains(result.Errors, e => e.Contains("unknown type"));
    }

    [Fact]
    public void Upload_validation_rejects_missing_and_unsupported_files()
    {
        // A real file with an unsupported extension is needed to reach the extension check:
        // ValidateLocalFile tests File.Exists first, so a non-existent path can only ever
        // exercise the missing-file branch, not the extension-rejection branch.
        var unsupported = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cr_test_{System.Guid.NewGuid():N}.bmp");
        System.IO.File.WriteAllBytes(unsupported, new byte[] { 1, 2, 3 });
        try
        {
            var model = new ExpertReportDocument
            {
                Title = "Upload Test",
                Sections =
                {
                    new ReportSection
                    {
                        Blocks =
                        {
                            new ContentBlock
                            {
                                Type = "mediarow",
                                Media = new List<MediaItem>
                                {
                                    new() { Caption = "Missing", ImagePath = "C:\\missing\\image.png" },
                                    new() { Caption = "Unsupported", ImagePath = unsupported },
                                },
                            },
                        },
                    },
                },
            };

            var result = new PayloadValidator().Validate("expert-report", model);

            Assert.False(result.Ok);
            Assert.Contains(result.Errors, e =>
                e.Contains("sections[0].blocks[0].media[0].imagePath") && e.Contains("file was not found"));
            Assert.Contains(result.Errors, e =>
                e.Contains("sections[0].blocks[0].media[1].imagePath") && e.Contains("must reference"));
        }
        finally
        {
            try { System.IO.File.Delete(unsupported); } catch { /* best-effort cleanup */ }
        }
    }

    [Fact]
    public void Api_context_rejects_raw_local_attachment_paths()
    {
        var model = new AdvertEvidencePackDocument
        {
            Subject = new SubjectVehicle { Registration = "AB12 CDE", Make = "Test" },
            Adverts = { new Advert { ScreenshotPath = "C:\\nope\\missing.png" } },
        };

        // Desktop/CLI default: local paths are allowed (checked for existence, not rejected).
        var desktop = new PayloadValidator().Validate("advert-evidence-pack", model);
        Assert.Contains(desktop.Errors, e => e.Contains("file was not found"));

        // API context: raw local paths are rejected outright, before any disk access.
        var api = new PayloadValidator().Validate("advert-evidence-pack", model, allowLocalFilePaths: false);
        Assert.Contains(api.Errors, e => e.Contains("raw local file paths are not accepted"));
    }

    [Fact]
    public void Api_context_rejects_remote_image_urls()
    {
        var model = new AdvertEvidencePackDocument
        {
            Subject = new SubjectVehicle { Registration = "AB12 CDE", Make = "Test" },
            Adverts = { new Advert { ScreenshotPath = "http://169.254.169.254/latest/meta-data/" } },
        };

        var result = new PayloadValidator().Validate(
            "advert-evidence-pack",
            model,
            allowLocalFilePaths: false);

        Assert.False(result.Ok);
        Assert.Contains(result.Errors, error => error.Contains("remote image URLs are not accepted"));
    }

    [Fact]
    public void Api_context_accepts_only_server_trusted_local_attachment_paths()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
        try
        {
            var model = new AdvertEvidencePackDocument
            {
                Subject = new SubjectVehicle { Registration = "AB12 CDE", Make = "Test" },
                Adverts = { new Advert { ScreenshotPath = path } },
            };
            var trusted = new HashSet<string>(
                OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
            {
                path,
            };

            var result = new PayloadValidator().Validate(
                "advert-evidence-pack",
                model,
                allowLocalFilePaths: false,
                trustedLocalFilePaths: trusted);

            Assert.DoesNotContain(result.Errors, error => error.Contains("screenshotPath"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Malformed_pdf_data_uri_fails_validation()
    {
        var model = new AdvertEvidencePackDocument
        {
            Subject = new SubjectVehicle { Registration = "AB12 CDE", Make = "Test" },
            Adverts = { new Advert { CapturedPdfPath = "data:application/pdf;base64,@@@not-base64" } },
        };

        var result = new PayloadValidator().Validate("advert-evidence-pack", model);
        Assert.False(result.Ok);
        Assert.Contains(result.Errors, e => e.Contains("not valid base64 PDF data"));
    }
}

public class ComposerTests
{
    private static readonly HtmlComposer Composer = new(BrandAssets.Default, TemplateCatalog.Default);

    [Fact]
    public void Valuation_html_carries_brand_and_values()
    {
        var descriptor = TemplateCatalog.Default.Get("market-valuation-evidence");
        var model = new MarketValuationEvidenceDocument
        {
            Subject = new SubjectVehicle { Registration = "AB12 CDE" },
            AssessedRetailValue = "24750",
        };

        var composed = Composer.Compose(descriptor, model, Density.Normal);

        Assert.Contains("MARKET VALUATION EVIDENCE", composed.Html);
        Assert.Contains("data:image/png;base64,", composed.Html); // letterhead logo embedded
        Assert.Contains("£24,750.00", composed.Html);
        Assert.Contains("class=\"value\"", composed.Html);
        Assert.Contains("CollisionEngineers.co.uk", composed.Page.FooterHtml!);
    }

    [Fact]
    public void Payload_text_is_html_encoded()
    {
        var model = new MarketValuationEvidenceDocument
        {
            Subject = new SubjectVehicle { Registration = "AB12CDE" },
            AssessedRetailValue = "1000",
            Conclusion = "<script>alert('x')</script>",
        };
        var descriptor = TemplateCatalog.Default.Get("market-valuation-evidence");

        var composed = Composer.Compose(descriptor, model, Density.Normal);

        Assert.DoesNotContain("<script>alert", composed.Html);
        Assert.Contains("&lt;script&gt;", composed.Html);
    }

    [Fact]
    public void Mediarow_image_url_cannot_break_out_of_attribute()
    {
        var model = new ExpertReportDocument
        {
            Title = "T",
            Sections =
            {
                new ReportSection
                {
                    Heading = "H",
                    Blocks =
                    {
                        new ContentBlock
                        {
                            Type = "mediarow",
                            Media = new List<MediaItem>
                            {
                                new() { Caption = "c", ImagePath = "https://x/a.png\"><script>alert(1)</script>" },
                            },
                        },
                    },
                },
            },
        };

        var composed = Composer.Compose(TemplateCatalog.Default.Get("expert-report"), model, Density.Normal);

        Assert.DoesNotContain("\"><script>", composed.Html);
        Assert.Contains("&lt;script&gt;", composed.Html);
    }

    [Fact]
    public void Fee_note_footer_carries_vat_number()
    {
        var descriptor = TemplateCatalog.Default.Get("fee-note");
        var model = new FeeNoteDocument
        {
            Items = { new FeeLineItem { Description = "Inspection", Amount = 350m } },
            VatRate = 0.20m,
            VatNumber = "GB 123 4567 89",
        };

        var composed = Composer.Compose(descriptor, model, Density.Normal);

        Assert.Contains("VAT Reg No.", composed.Page.FooterHtml!);
        Assert.Contains("£420.00", composed.Html); // 350 + 20% VAT
    }

    [Fact]
    public void Custom_signature_path_is_inlined_without_rebuilding_core()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ce_signature_{Guid.NewGuid():N}.png");
        File.WriteAllBytes(path, new byte[] { 0x89, 0x50, 0x4E, 0x47, 1, 2, 3, 4 });
        try
        {
            var model = new ExpertReportDocument
            {
                Title = "Signature Test",
                Sections = { new ReportSection { Heading = "Body", Blocks = { new ContentBlock { Text = "Test." } } } },
                Signature = new SignatureBlock
                {
                    Name = "Example Signatory",
                    SignatureImage = "andy_patterson",
                    CustomSignaturePath = path,
                },
            };

            var composed = Composer.Compose(TemplateCatalog.Default.Get("expert-report"), model, Density.Normal);

            Assert.Contains("data:image/png;base64,iVBORwECAwQ=", composed.Html);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Empty_signature_name_and_role_render_a_firm_only_sign_off()
    {
        var json = """
            {
              "title": "Rebuttal of Claim for Diminution in Value",
              "sections": [ { "heading": "Conclusion", "blocks": [ { "type": "paragraph", "text": "Body." } ] } ],
              "signature": { "name": "", "role": "" }
            }
            """;
        var model = (ExpertReportDocument)JsonSerializer.Deserialize(json, typeof(ExpertReportDocument), CrJson.Options)!;

        var composed = Composer.Compose(TemplateCatalog.Default.Get("diminution-rebuttal"), model, Density.Normal);

        Assert.Contains("Yours faithfully,", composed.Html);
        Assert.Contains("class=\"sig-org\">Collision Engineers Ltd", composed.Html);
        Assert.DoesNotContain("class=\"sig-name\"", composed.Html);
        Assert.DoesNotContain("class=\"sig-role\"", composed.Html);
        Assert.DoesNotContain("Independent Automotive Engineer", composed.Html);
    }

    [Fact]
    public void Null_signature_role_and_org_fall_back_to_firm_defaults()
    {
        var json = """
            {
              "title": "Expert Report",
              "sections": [ { "heading": "H", "blocks": [ { "type": "paragraph", "text": "Body." } ] } ],
              "signature": { "name": "E. Xample", "role": null, "org": null }
            }
            """;
        var model = (ExpertReportDocument)JsonSerializer.Deserialize(json, typeof(ExpertReportDocument), CrJson.Options)!;

        var composed = Composer.Compose(TemplateCatalog.Default.Get("expert-report"), model, Density.Normal);

        Assert.Contains("class=\"sig-name\">E. Xample", composed.Html);
        Assert.Contains("Independent Automotive Engineer", composed.Html);
        Assert.Contains("class=\"sig-org\">Collision Engineers Ltd", composed.Html);
    }

    [Fact]
    public void Evidence_pack_shows_uploaded_capture_evidence()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ce_capture_{Guid.NewGuid():N}.png");
        File.WriteAllBytes(path, new byte[] { 0x89, 0x50, 0x4E, 0x47, 1, 2, 3, 4 });
        try
        {
            var model = new AdvertEvidencePackDocument
            {
                Subject = new SubjectVehicle { Registration = "AB12CDE", Make = "Example", Model = "Car" },
                Adverts =
                {
                    new Advert
                    {
                        AdvertId = "A1",
                        Url = "https://example.test/advert",
                        Price = "12000",
                        ScreenshotPath = path,
                        CapturedPdfPath = Path.Combine(Path.GetTempPath(), "advert-capture.pdf"),
                    },
                },
            };

            var composed = Composer.Compose(TemplateCatalog.Default.Get("advert-evidence-pack"), model, Density.Normal);

            Assert.Contains("CAPTURED EVIDENCE", composed.Html);
            Assert.Contains("data:image/png;base64,iVBORwECAwQ=", composed.Html);
            Assert.Contains("advert-capture.pdf", composed.Html);
        }
        finally
        {
            File.Delete(path);
        }
    }
}

public class RendererTests
{
    private static string Sample(string id) => id switch
    {
        "market-valuation-evidence" => JsonSerializer.Serialize(
            new MarketValuationEvidenceDocument
            {
                Subject = new SubjectVehicle { Registration = "AB12 CDE" },
                AssessedRetailValue = "24750",
            },
            CrJson.Options),
        "advert-evidence-pack" => JsonSerializer.Serialize(
            new AdvertEvidencePackDocument
            {
                Subject = new SubjectVehicle { Registration = "AB12 CDE", Make = "Example" },
                Adverts =
                {
                    new Advert
                    {
                        Url = "https://example.test/advert",
                        Price = "12000",
                    },
                },
            },
            CrJson.Options),
        _ => CollisionRendererFactory.AuthoringCatalog.GetStarterJson(id),
    };

    [Fact]
    public async Task Auto_fit_shrinks_density_until_it_fits()
    {
        var fake = new FakePdfEngine(); // normal=3, compact=2, ultra=1 pages
        await using var renderer = CollisionRendererFactory.CreateRenderer(fake);

        var result = await renderer.RenderAsync(new RenderRequest
        {
            TemplateId = "market-valuation-evidence",
            Json = Sample("market-valuation-evidence"),
            Options = new RenderOptions { Fit = DensityFit.Auto },
        });

        Assert.Equal(Density.UltraCompact, result.Density);
        Assert.Equal(1, result.PageCount);
        Assert.Equal(3, fake.RenderedHtml.Count); // tried normal -> compact -> ultra
    }

    [Fact]
    public async Task Fixed_density_does_not_shrink_and_warns_on_overflow()
    {
        var fake = new FakePdfEngine();
        await using var renderer = CollisionRendererFactory.CreateRenderer(fake);

        var result = await renderer.RenderAsync(new RenderRequest
        {
            TemplateId = "market-valuation-evidence",
            Json = Sample("market-valuation-evidence"),
            Options = new RenderOptions { Fit = DensityFit.Fixed, Density = Density.Normal },
        });

        Assert.Equal(Density.Normal, result.Density);
        Assert.Equal(3, result.PageCount);
        Assert.Single(fake.RenderedHtml);
        Assert.Contains(result.Warnings, w => w.Contains("exceeds"));
    }

    [Fact]
    public async Task Filename_is_derived_from_registration()
    {
        var fake = new FakePdfEngine { PageCountForHtml = _ => 1 };
        await using var renderer = CollisionRendererFactory.CreateRenderer(fake);

        var result = await renderer.RenderAsync(new RenderRequest
        {
            TemplateId = "advert-evidence-pack",
            Json = Sample("advert-evidence-pack"),
        });

        Assert.Equal("AB12_CDE_advert_evidence_pack.pdf", result.SuggestedFileName);
        Assert.Equal(64, result.Sha256.Length); // hex sha256
    }

    [Fact]
    public async Task Invalid_json_throws_validation_exception()
    {
        var fake = new FakePdfEngine();
        await using var renderer = CollisionRendererFactory.CreateRenderer(fake);

        await Assert.ThrowsAsync<RenderValidationException>(() => renderer.RenderAsync(new RenderRequest
        {
            TemplateId = "fee-note",
            Json = "{ not valid json ",
        }));
    }

    [Fact]
    public async Task Evidence_pack_appends_captured_pdf_pages()
    {
        var capturedPath = Path.Combine(Path.GetTempPath(), $"ce_capture_{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(capturedPath, CreateOnePagePdf());
        try
        {
            var payload = new AdvertEvidencePackDocument
            {
                Subject = new SubjectVehicle { Registration = "AB12CDE", Make = "Example", Model = "Car" },
                Adverts =
                {
                    new Advert
                    {
                        AdvertId = "A1",
                        Url = "https://example.test/advert",
                        Price = "12000",
                        CapturedPdfPath = capturedPath,
                    },
                },
            };

            var engine = new PdfBytesEngine(CreateOnePagePdf());
            await using var renderer = CollisionRendererFactory.CreateRenderer(engine);
            var result = await renderer.RenderAsync(new RenderRequest
            {
                TemplateId = "advert-evidence-pack",
                Json = JsonSerializer.Serialize(payload, CrJson.Options),
            });

            Assert.Equal(2, result.PageCount);
            Assert.Contains(result.Warnings, w => w.Contains("Appended 1 captured advert PDF"));
        }
        finally
        {
            File.Delete(capturedPath);
        }
    }

    private static byte[] CreateOnePagePdf()
    {
        using var doc = new PdfDocument();
        doc.AddPage();
        using var ms = new MemoryStream();
        doc.Save(ms, closeStream: false);
        return ms.ToArray();
    }

    private sealed class PdfBytesEngine(byte[] pdf) : IPdfEngine
    {
        public string EngineVersion => "pdf-bytes/1.0";

        public Task<byte[]> RenderHtmlToPdfAsync(string html, PdfPageSettings settings, CancellationToken ct = default) =>
            Task.FromResult(pdf);

        public int CountPages(byte[] bytes)
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using var doc = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
            return doc.PageCount;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
