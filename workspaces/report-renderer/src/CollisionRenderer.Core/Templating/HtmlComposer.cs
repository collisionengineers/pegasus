using System.Collections.Concurrent;
using System.IO;
using CollisionRenderer.Core.Design;
using CollisionRenderer.Core.Models;
using Scriban;
using Scriban.Runtime;

namespace CollisionRenderer.Core.Templating;

/// <summary>Builds the print-ready HTML (letterhead shell + Scriban body) for a document.</summary>
public interface IHtmlComposer
{
    ComposedDocument Compose(TemplateDescriptor descriptor, object model, Density density);
}

public sealed class HtmlComposer : IHtmlComposer
{
    private const string StandardStrapline =
        "Collision Engineers Ltd | www.CollisionEngineers.co.uk | engineers@collisionengineers.co.uk";

    private const string DefaultIntro =
        "We have undertaken a review of comparable vehicles currently advertised in the retail market.";

    private const string DefaultMarketResearch =
        "The following comparable retail market evidence has been reviewed having regard to make, model, age, " +
        "mileage, engine, transmission, specification and general condition.";

    private const string DefaultSearchSummary =
        "Searches were conducted using live retail market evidence for vehicles considered comparable to the subject vehicle.";

    private static readonly ConcurrentDictionary<string, Template> TemplateCache = new();

    private readonly BrandAssets _brand;
    private readonly ITemplateCatalog _catalog;

    public HtmlComposer(BrandAssets brand, ITemplateCatalog catalog)
    {
        _brand = brand;
        _catalog = catalog;
    }

    public ComposedDocument Compose(TemplateDescriptor descriptor, object model, Density density)
    {
        // Each template maps to exactly one document model, and the model determines the
        // letterhead body — so route by model type. New report variants that reuse
        // ExpertReportDocument (e.g. blank-letterhead) need no change here.
        var chrome = model switch
        {
            MarketValuationEvidenceDocument m => Valuation(m, density),
            AdvertEvidencePackDocument a => EvidencePack(a),
            FeeNoteDocument f => FeeNote(f),
            ExpertReportDocument e => ExpertReport(e),
            _ => throw new NotSupportedException($"No composer for template '{descriptor.Id}'."),
        };

        var html = Shell(chrome);
        var page = new PdfPageSettings
        {
            HeaderHtml = "<div></div>",
            FooterHtml = FooterTemplate(chrome.FooterStrapline),
        };

        return new ComposedDocument { Html = html, Page = page };
    }

    // ---------------------------------------------------------------- Valuation

    private DocChrome Valuation(MarketValuationEvidenceDocument m, Density density)
    {
        var subject = m.Subject;
        var ctx = new ScriptObject
        {
            ["intro"] = Enc(Coalesce(m.Intro, DefaultIntro)),
            ["market_research"] = Enc(Coalesce(m.MarketResearch, DefaultMarketResearch)),
            ["subject_display"] = Enc(Format.SubjectDisplayName(subject)),
            ["registration"] = Enc(subject.Registration),
            ["assessed_value"] = Format.Money(m.AssessedRetailValue),
            ["guide_value"] = Format.OptionalMoney(m.GuideValue),
            ["subject_rows"] = SubjectPairRows(subject),
            ["adverts"] = ReportAdverts(m.Adverts),
            ["valuation_commentary"] = m.ValuationCommentary
                .Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => (object)Enc(p)).ToList(),
            ["conclusion"] = Enc(m.Conclusion),
            ["vat_note"] = string.IsNullOrWhiteSpace(m.VatNote) ? null : Enc(m.VatNote),
            ["signature"] = SignatureCtx(m.Signature),
        };

        var body = Render("templates/market_valuation_evidence.scriban", ctx);
        return new DocChrome(
            "MARKET VALUATION EVIDENCE", body,
            OurRef(m.Meta, subject.Registration), m.Meta.YourRef, ResolveDate(m.Meta),
            StandardStrapline, BodyClass(density));
    }

    // ------------------------------------------------------------ Evidence pack

    private DocChrome EvidencePack(AdvertEvidencePackDocument m)
    {
        var subject = m.Subject;
        var captures = EvidenceCaptures(m.Adverts);
        var ctx = new ScriptObject
        {
            ["intro"] = Enc(Coalesce(m.Intro,
                "Comparable advert references corresponding with the market valuation evidence report.")),
            ["subject_display"] = Enc(Format.SubjectDisplayName(subject)),
            ["registration"] = Enc(subject.Registration),
            ["adverts"] = EvidenceAdverts(m.Adverts),
            ["captures"] = captures.Count == 0 ? null : captures,
            ["search_summary"] = Enc(Coalesce(m.SearchSummary, DefaultSearchSummary)),
        };

        var body = Render("templates/advert_evidence_pack.scriban", ctx);
        return new DocChrome(
            "ADVERT EVIDENCE PACK", body,
            OurRef(m.Meta, subject.Registration), m.Meta.YourRef, ResolveDate(m.Meta),
            StandardStrapline, string.Empty);
    }

    // ----------------------------------------------------------------- Fee note

    private DocChrome FeeNote(FeeNoteDocument m)
    {
        var subtotal = m.Items.Sum(i => i.Amount);
        var vat = decimal.Round(subtotal * m.VatRate, 2, MidpointRounding.AwayFromZero);
        var total = subtotal + vat;

        var ctx = new ScriptObject
        {
            ["fee_note_number"] = Enc(m.FeeNoteNumber),
            ["date"] = Enc(ResolveDate(m.Meta)),
            ["bill_to_name"] = Enc(m.BillTo.Name),
            ["bill_to_address"] = m.BillTo.AddressLines.Select(l => (object)Enc(l)).ToList(),
            ["bill_to_reference"] = string.IsNullOrWhiteSpace(m.BillTo.Reference) ? null : Enc(m.BillTo.Reference),
            ["matter_reference"] = string.IsNullOrWhiteSpace(m.MatterReference) ? null : Enc(m.MatterReference),
            ["subject_line"] = SubjectLine(m.Subject),
            ["items"] = m.Items.Select(i => (object)D(
                ("description", Enc(i.Description)),
                ("detail", string.IsNullOrWhiteSpace(i.Detail) ? null : Enc(i.Detail)),
                ("amount", Format.Money(i.Amount)))).ToList(),
            ["subtotal"] = Format.Money(subtotal),
            ["vat_rate"] = FormatPercent(m.VatRate),
            ["vat"] = Format.Money(vat),
            ["total"] = Format.Money(total),
            ["payment"] = PaymentCtx(m.Payment),
            ["notes"] = string.IsNullOrWhiteSpace(m.Notes) ? null : Enc(m.Notes),
        };

        var body = Render("templates/fee_note.scriban", ctx);
        var footer = string.IsNullOrWhiteSpace(m.VatNumber)
            ? "Collision Engineers Ltd | www.CollisionEngineers.co.uk"
            : $"Collision Engineers Ltd | www.CollisionEngineers.co.uk | VAT Reg No. {m.VatNumber}";

        return new DocChrome(
            "FEE NOTE", body,
            OurRef(m.Meta, m.FeeNoteNumber), m.Meta.YourRef, ResolveDate(m.Meta),
            footer, string.Empty);
    }

    // ------------------------------------------------------------- Expert report

    private DocChrome ExpertReport(ExpertReportDocument m)
    {
        var titleClass = new List<string> { "title" };
        if (m.TitleRed)
        {
            titleClass.Add("red");
        }

        if (m.TitleUnderlined)
        {
            titleClass.Add("underlined");
        }

        var ctx = new ScriptObject
        {
            ["title"] = string.IsNullOrWhiteSpace(m.Title) ? null : Enc(m.Title.ToUpperInvariant()),
            ["title_class"] = string.Join(" ", titleClass),
            ["subtitle"] = string.IsNullOrWhiteSpace(m.Subtitle) ? null : Enc(m.Subtitle),
            ["salutation"] = string.IsNullOrWhiteSpace(m.Salutation) ? null : Enc(m.Salutation),
            ["re_line"] = string.IsNullOrWhiteSpace(m.ReLine) ? null : Enc(m.ReLine),
            ["red_intro"] = m.RedIntro,
            ["intro"] = m.Intro.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => (object)Enc(p)).ToList(),
            ["sections"] = m.Sections.Select(s => (object)D(
                ("heading", string.IsNullOrWhiteSpace(s.Heading) ? null : Enc(s.Heading.ToUpperInvariant())),
                ("blocks", s.Blocks.Select(BlockCtx).ToList()))).ToList(),
            ["signature"] = SignatureCtx(m.Signature),
        };

        var body = Render("templates/expert_report.scriban", ctx);
        return new DocChrome(
            m.Title, body,
            OurRef(m.Meta, m.Meta.OurRef ?? "REPORT"), m.Meta.YourRef, ResolveDate(m.Meta),
            StandardStrapline, string.Empty);
    }

    private object BlockCtx(ContentBlock b)
    {
        var type = b.Type.ToLowerInvariant();
        return type switch
        {
            "bullets" => D(
                ("type", "bullets"),
                ("items", (b.Items ?? new List<string>()).Select(i => (object)Enc(i)).ToList())),

            "datatable" => D(
                ("type", "datatable"),
                ("pairs", PairRows(b.Rows ?? new List<KeyValueRow>()))),

            "keyvalue" => D(
                ("type", "keyvalue"),
                ("pairs", PairRows(b.Rows ?? new List<KeyValueRow>()))),

            "evidencetable" => D(
                ("type", "evidencetable"),
                ("columns", (b.Table?.Columns ?? new List<EvidenceColumn>()).Select(c => (object)D(
                    ("header", Enc(c.Header)),
                    ("align", SafeAlign(c.Align)),
                    ("width", string.IsNullOrWhiteSpace(c.Width) ? null : Attr(c.Width)))).ToList()),
                ("rows", (b.Table?.Rows ?? new List<List<string>>())
                    .Select(r => (object)r.Select(cell => (object)Enc(cell)).ToList()).ToList())),

            "valuebox" => D(
                ("type", "valuebox"),
                ("label", Enc(b.Value?.Label)),
                ("value", Enc(b.Value?.Value))),

            "mediarow" => D(
                ("type", "mediarow"),
                ("media", (b.Media ?? new List<MediaItem>()).Select(mi => (object)D(
                    ("caption", Enc(mi.Caption)),
                    ("image", ResolveImage(mi.ImagePath)),
                    ("note", string.IsNullOrWhiteSpace(mi.Note) ? null : Enc(mi.Note)))).ToList())),

            _ => D(
                ("type", "paragraph"),
                ("text", Enc(b.Text))),
        };
    }

    // ------------------------------------------------------------------- Shell

    private string Shell(DocChrome c)
    {
        return "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\">" +
               $"<title>{Enc(c.Title)}</title><style>{_brand.Css}</style></head>" +
               $"<body class=\"{c.BodyClass}\">{Letterhead(c)}{c.Body}</body></html>";
    }

    private string Letterhead(DocChrome c)
    {
        var rows = $"<tr><th>Our Ref:</th><td>{Enc(c.OurRef)}</td></tr>";
        if (!string.IsNullOrWhiteSpace(c.YourRef))
        {
            rows += $"<tr><th>Your Ref:</th><td>{Enc(c.YourRef)}</td></tr>";
        }

        rows += $"<tr><th>Date:</th><td>{Enc(c.Date)}</td></tr>";

        return "<header class=\"document-header\">" +
               $"<img class=\"logo\" src=\"{_brand.LogoDataUri}\" alt=\"Collision Engineers Ltd\">" +
               $"<table class=\"reference-table\"><tbody>{rows}</tbody></table></header>";
    }

    private static string FooterTemplate(string strapline)
    {
        return
            "<div style=\"width:100%;font-family:Arial,Helvetica,sans-serif;font-size:8.5pt;color:#555;" +
            "-webkit-print-color-adjust:exact;print-color-adjust:exact;\">" +
            "<div style=\"border-top:0.6pt solid #c80a32;margin:0 17mm 1.4mm 17mm;\"></div>" +
            "<div style=\"position:relative;text-align:center;padding:0 12mm;\">" +
            $"<span>{Enc(strapline)}</span>" +
            "<span style=\"position:absolute;right:12mm;top:0;\">&mdash; " +
            "<span class=\"pageNumber\"></span> of <span class=\"totalPages\"></span> &mdash;</span>" +
            "</div></div>";
    }

    // -------------------------------------------------------------- Context bits

    private static List<object> SubjectPairRows(SubjectVehicle s)
    {
        var rows = new List<KeyValueRow>
        {
            new() { Label = "Registration", Value = Or(s.Registration) },
            new() { Label = "Make / Model", Value = Or(Format.SubjectDisplayName(s)) },
            new() { Label = "Body Type", Value = Or(s.BodyType) },
            new() { Label = "Fuel / Transmission", Value = Or(Join(" / ", s.Fuel, s.Transmission)) },
            new() { Label = "Engine", Value = Or(s.Engine) },
            new() { Label = "First Registered", Value = Or(s.FirstRegistered) },
            new() { Label = "Mileage", Value = Format.SubjectMileage(s.Mileage) },
            new() { Label = "Colour", Value = Or(s.Colour) },
            new() { Label = "Vehicle History", Value = Format.VehicleHistory(s.VehicleHistory) },
            new() { Label = "VIN", Value = Or(s.Vin) },
        };

        return PairRows(rows);
    }

    private static List<object> PairRows(List<KeyValueRow> rows)
    {
        var result = new List<object>();
        for (var i = 0; i < rows.Count; i += 2)
        {
            var cells = new List<object>
            {
                D(("label", Enc(rows[i].Label)), ("value", Enc(rows[i].Value))),
            };
            if (i + 1 < rows.Count)
            {
                cells.Add(D(("label", Enc(rows[i + 1].Label)), ("value", Enc(rows[i + 1].Value))));
            }

            result.Add(D(("cells", cells)));
        }

        return result;
    }

    private static List<object> ReportAdverts(List<Advert> adverts)
    {
        return adverts.Select((a, idx) => (object)D(
            ("index", idx + 1),
            ("vehicle", Enc(Format.VehicleLabel(a))),
            ("year", Format.Year(a.RegistrationYear)),
            ("mileage", Format.Mileage(a.Mileage)),
            ("seller_type", Enc(a.SellerType)),
            ("price", Format.Money(a.Price, decimals: false)),
            ("report_comment", Enc(ReportComment(a))))).ToList();
    }

    private static List<object> EvidenceAdverts(List<Advert> adverts)
    {
        return adverts.Select((a, idx) => (object)D(
            ("index", idx + 1),
            ("advert_id", Enc(string.IsNullOrWhiteSpace(a.AdvertId) ? "Not stated" : a.AdvertId)),
            ("vehicle", Enc(Format.VehicleLabel(a))),
            ("year", Format.Year(a.RegistrationYear)),
            ("mileage", Format.Mileage(a.Mileage)),
            ("price", Format.Money(a.Price, decimals: false)),
            ("url", Format.SafeUrl(a.Url)),
            ("screenshot", ResolveImage(a.ScreenshotPath)),
            ("capture_pdf", CapturePdfLabel(a.CapturedPdfPath)))).ToList();
    }

    private static List<object> EvidenceCaptures(List<Advert> adverts)
    {
        // Only build capture blocks for adverts that carry an inline screenshot to display.
        // PDF-only captures need no block: the ADVERT REFERENCES table already lists them and
        // PdfEvidenceAppender appends the PDFs regardless. When no advert has a screenshot this
        // returns empty, so the CAPTURED EVIDENCE section drops out and the pack cover stays one page.
        return adverts.Select((a, idx) => new { Advert = a, Index = idx + 1 })
            .Where(x => !string.IsNullOrWhiteSpace(x.Advert.ScreenshotPath))
            .Select(x => (object)D(
                ("index", x.Index),
                ("advert_id", Enc(string.IsNullOrWhiteSpace(x.Advert.AdvertId) ? "Not stated" : x.Advert.AdvertId)),
                ("screenshot", ResolveImage(x.Advert.ScreenshotPath)),
                ("capture_pdf", CapturePdfLabel(x.Advert.CapturedPdfPath))))
            .ToList();
    }

    private static string ReportComment(Advert a)
    {
        if (!string.IsNullOrWhiteSpace(a.ReportComment))
        {
            return a.ReportComment!;
        }

        return Join(" ", a.ComparabilityNote, a.DifferencesNote);
    }

    private static string? CapturePdfLabel(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (path.StartsWith("data:application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            return "Attached PDF";
        }

        try
        {
            return Enc(Path.GetFileName(path));
        }
        catch
        {
            return "Attached PDF";
        }
    }

    private object? SignatureCtx(SignatureBlock? s)
    {
        if (s is null)
        {
            return null;
        }

        return D(
            ("closing", Enc(Coalesce(s.Closing, "Yours faithfully,"))),
            // Attribute-encode the resolved URI so a payload-supplied data:image value
            // cannot break out of src="..."; base64 of bundled bytes is unaffected.
            ("image", AttrUri(BrandAssets.CustomSignatureDataUri(s.CustomSignaturePath) ?? _brand.SignatureDataUri(s.SignatureImage))),
            // Name, role and org lines render only when non-empty: null falls back to the
            // firm default, an explicit empty string suppresses the line so a firm-only
            // sign-off ("Yours faithfully, / Collision Engineers Ltd") is expressible.
            ("name", Opt(s.Name)),
            ("role", Opt(s.Role ?? "Independent Automotive Engineer")),
            ("org", Opt(s.Org ?? "Collision Engineers Ltd")),
            ("qualifications", string.IsNullOrWhiteSpace(s.Qualifications) ? null : Enc(s.Qualifications)),
            ("aqp", string.IsNullOrWhiteSpace(s.AqpNumber) ? null : Enc(s.AqpNumber)));
    }

    private static object PaymentCtx(PaymentDetails p) => D(
        ("bank_name", Opt(p.BankName)),
        ("account_name", Opt(p.AccountName)),
        ("sort_code", Opt(p.SortCode)),
        ("account_number", Opt(p.AccountNumber)),
        ("terms", Opt(p.Terms)));

    private static object? SubjectLine(SubjectVehicle? s)
    {
        if (s is null || string.IsNullOrWhiteSpace(s.Registration) && string.IsNullOrWhiteSpace(Format.SubjectDisplayName(s)))
        {
            return null;
        }

        var name = Format.SubjectDisplayName(s);
        var reg = s.Registration;
        var label = string.IsNullOrWhiteSpace(name) ? reg : $"{name} — {reg}";
        return Enc(label);
    }

    private static string? ResolveImage(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        // Attribute-encode any payload-supplied URL so it cannot break out of the
        // src="..." attribute and inject markup into the Chromium-rendered page.
        if (path.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return Format.Attr(path);
        }

        if (File.Exists(path))
        {
            var ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
            var mime = ext switch { "png" => "image/png", "jpg" or "jpeg" => "image/jpeg", "webp" => "image/webp", _ => "image/png" };
            // base64 of our own bytes contains no attribute-breaking characters.
            return $"data:{mime};base64," + Convert.ToBase64String(File.ReadAllBytes(path));
        }

        return null;
    }

    // ------------------------------------------------------------------ Helpers

    private static string Render(string resource, ScriptObject ctx)
    {
        var template = TemplateCache.GetOrAdd(resource, r =>
        {
            var parsed = Template.Parse(EmbeddedResources.ReadText(r));
            if (parsed.HasErrors)
            {
                throw new InvalidOperationException(
                    $"Template '{r}' has parse errors: {string.Join("; ", parsed.Messages)}");
            }

            return parsed;
        });

        return template.Render(ctx);
    }

    private static string BodyClass(Density density) => density switch
    {
        Density.Compact => "report-compact",
        Density.UltraCompact => "report-ultra-compact",
        _ => string.Empty,
    };

    private static string OurRef(DocumentMeta meta, string fallback) =>
        string.IsNullOrWhiteSpace(meta.OurRef) ? fallback : meta.OurRef!;

    private static string ResolveDate(DocumentMeta meta) =>
        string.IsNullOrWhiteSpace(meta.Date) ? Format.Today() : meta.Date!;

    private static string FormatPercent(decimal rate)
    {
        var pct = rate * 100m;
        return pct == decimal.Truncate(pct) ? $"{pct:0}%" : $"{pct:0.##}%";
    }

    private static string SafeAlign(string? align) => align?.ToLowerInvariant() switch
    {
        "right" => "right",
        "center" or "centre" => "center",
        _ => "left",
    };

    private static string Enc(string? value) => Format.Enc(value);

    private static string Attr(string? value) => Format.Attr(value);

    // Attribute-encode an image src URI, preserving null so the template can omit the tag.
    private static string? AttrUri(string? uri) => uri is null ? null : Format.Attr(uri);

    private static string Coalesce(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value!;

    private static string? Opt(string? value) => string.IsNullOrWhiteSpace(value) ? null : Enc(value);

    private static string Or(string? value) => string.IsNullOrWhiteSpace(value) ? "Not stated" : value!;

    private static string Join(string sep, params string?[] parts) =>
        string.Join(sep, parts.Where(p => !string.IsNullOrWhiteSpace(p)));

    private static Dictionary<string, object?> D(params (string Key, object? Value)[] pairs)
    {
        var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, value) in pairs)
        {
            dict[key] = value;
        }

        return dict;
    }

    private sealed record DocChrome(
        string Title,
        string Body,
        string OurRef,
        string? YourRef,
        string Date,
        string FooterStrapline,
        string BodyClass);
}
