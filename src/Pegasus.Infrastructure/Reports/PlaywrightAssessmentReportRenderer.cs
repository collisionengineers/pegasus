using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Collections.Concurrent;
using Microsoft.Playwright;
using Pegasus.Core.Reports;
using PdfSharp.Pdf.IO;
using Scriban;
using Scriban.Runtime;

namespace Pegasus.Infrastructure.Reports;

internal sealed class PlaywrightAssessmentReportRenderer : IAssessmentReportRenderer, IAsyncDisposable
{
    private static readonly ConcurrentDictionary<string, Template> Templates = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, string> TextResources = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, string> DataResources = new(StringComparer.Ordinal);

    private readonly SemaphoreSlim gate = new(1, 1);
    private IPlaywright? playwright;
    private IBrowser? browser;

    public string EngineVersion { get; } =
        $"Playwright/{typeof(Playwright).Assembly.GetName().Version}; Chromium";

    public async Task<RenderedReportArtifact> RenderAsync(
        AssessmentReportSnapshot snapshot,
        CaseReportArtifactKind kind,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        AssessmentReportRenderPolicy.RequireBoundedImages(snapshot.Photos);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var activeBrowser = await GetBrowserAsync().ConfigureAwait(false);
            return kind switch
            {
                CaseReportArtifactKind.AssessmentReport => Artifact(
                    $"{Slug(snapshot.OurReference)}_assessment.pdf",
                    await RenderPdfAsync(
                        activeBrowser,
                        "assessment_report.scriban",
                        AssessmentContext(snapshot),
                        Footer(snapshot, false),
                        cancellationToken).ConfigureAwait(false)),
                CaseReportArtifactKind.FeeNote => Artifact(
                    $"{Slug(snapshot.OurReference)}_fee_note.pdf",
                    await RenderPdfAsync(
                        activeBrowser,
                        "assessment_fee_note.scriban",
                        FeeNoteContext(snapshot),
                        Footer(snapshot, true),
                        cancellationToken).ConfigureAwait(false)),
                _ => throw new ReportRenderRejectedException(
                    $"Unsupported report artifact kind '{kind}'."),
            };
        }
        finally
        {
            gate.Release();
        }
    }

    private static ScriptObject AssessmentContext(AssessmentReportSnapshot snapshot)
    {
        var presentation = snapshot.Presentation();
        var assessment = CommonContext(snapshot);
        assessment["title"] = presentation.Title;
        assessment["badge"] = presentation.Badge;
        assessment["legal_badge"] = snapshot.LegalStatus.ToUpperInvariant();
        assessment["tiles"] = Tiles(snapshot, presentation);
        assessment["introduction"] = Introduction(snapshot);
        assessment["vehicle_rows"] = VehicleRows(snapshot);
        assessment["impact_rows"] = ImpactRows(snapshot.Damage.Impacts);
        assessment["damage_rows"] = DamageRows(snapshot);
        assessment["restraint_rows"] = RestraintRows(snapshot.Damage);
        assessment["settlement_rows"] = SettlementRows(snapshot.Settlement);
        assessment["desktop_assessment"] = snapshot.AssessmentMethod == "image_based"
            ? "<section class=\"section\"><h2>Desktop Assessment</h2><p>This report has been compiled from a desktop review of the information available relating to this claim.</p></section>"
            : string.Empty;
        assessment["nature"] = $"The vehicle has suffered {Encode(Display(snapshot.ImpactSeverity))} collision/impact damage to the {Encode(Display(snapshot.ImpactLocation))}.";
        assessment["engineer_comments"] = EngineerComments(snapshot);
        assessment["history"] = Encode(snapshot.HistoryCheck);
        assessment["condition_text"] = $"The vehicle is considered to be in {Encode(Display(snapshot.Vehicle.Condition))} condition for its age and type.";
        assessment["settlement_heading"] = presentation.SettlementHeading;
        assessment["settlement_label"] = presentation.SettlementLabel;
        assessment["settlement_text"] = Encode(presentation.SettlementText);
        assessment["settlement_value"] = Money(presentation.RecommendedSettlement!.Value);
        assessment["salvage"] = Salvage(snapshot);
        assessment["vehicle_data_rows"] = VehicleDataRows(snapshot);
        assessment["cost_rows"] = CostRows(snapshot.Costs);
        assessment["valuation_commentary"] = snapshot.Content.IncludeValuationCommentary
            ? Paragraph(snapshot.ValuationCommentary)
            : string.Empty;
        assessment["worklists"] = Worklists(snapshot);
        assessment["photos"] = Photos(snapshot.OrderedPhotos);
        assessment["statement_of_truth"] = StatementOfTruth(snapshot);
        assessment["signature"] = ImageDataUri(
            snapshot.Signatory.SignatureContent,
            snapshot.Signatory.SignatureContentType);
        assessment["engineer"] = Encode(snapshot.Signatory.PrintedName);
        assessment["qualifications"] = string.IsNullOrWhiteSpace(snapshot.Signatory.Qualifications)
            ? null
            : Encode(snapshot.Signatory.Qualifications);
        return assessment;
    }

    private static ScriptObject FeeNoteContext(AssessmentReportSnapshot snapshot)
    {
        var fee = CommonContext(snapshot);
        fee["registration"] = Encode(snapshot.Vehicle.Registration);
        fee["fee_rows"] = FeeRows(snapshot);
        fee["fee_net_number"] = Number(snapshot.FeeNet);
        fee["fee_vat_number"] = Number(snapshot.FeeVat);
        fee["fee_total"] = Money(snapshot.FeeTotal);
        fee["vat_number"] = AssessmentReportContract.VatNumber;
        fee["account_name"] = AssessmentReportContract.AccountName;
        fee["bank"] = AssessmentReportContract.BankName;
        fee["sort_code"] = AssessmentReportContract.SortCode;
        fee["account_number"] = AssessmentReportContract.AccountNumber;
        fee["remittance_email"] = AssessmentReportContract.RemittanceEmail;
        fee["fee_terms"] = Encode(AssessmentReportContract.FeeTerms);
        fee["additional_fee_terms"] = Encode(AssessmentReportContract.AdditionalFeeTerms);
        return fee;
    }

    private async Task<IBrowser> GetBrowserAsync()
    {
        if (browser?.IsConnected == true)
        {
            return browser;
        }
        playwright ??= await Playwright.CreateAsync().ConfigureAwait(false);
        browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true }).ConfigureAwait(false);
        return browser;
    }

    private static async Task<byte[]> RenderPdfAsync(
        IBrowser activeBrowser,
        string templateName,
        ScriptObject values,
        string footer,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var template = Templates.GetOrAdd(templateName, static name =>
            Template.Parse(ResourceText($"templates.{name}")));
        if (template.HasErrors)
        {
            throw new InvalidOperationException(string.Join("; ", template.Messages));
        }
        var context = new TemplateContext { LimitToString = 0 };
        context.PushGlobal(values);
        var html = await template.RenderAsync(context).ConfigureAwait(false);
        if (html.Contains("{{", StringComparison.Ordinal) || html.Contains('«'))
        {
            throw new ReportRenderRejectedException("The composed report contains an unresolved placeholder.");
        }
        // Every browser step carries an explicit budget and the caller's
        // cancellation: a hung page never blocks the process-wide gate.
        var budget = (float)AssessmentReportRenderPolicy.RenderTimeout.TotalMilliseconds;
        await using var page = await activeBrowser.NewPageAsync().ConfigureAwait(false);
        page.SetDefaultTimeout(budget);
        await using var cancellation = cancellationToken.Register(() => _ = page.CloseAsync());
        await page.SetContentAsync(
            html,
            new PageSetContentOptions { WaitUntil = WaitUntilState.Load, Timeout = budget })
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return await page.PdfAsync(new PagePdfOptions
        {
            Format = "A4",
            PrintBackground = true,
            DisplayHeaderFooter = true,
            HeaderTemplate = "<span></span>",
            FooterTemplate = footer,
            Margin = new Margin { Top = "8mm", Right = "12mm", Bottom = "22mm", Left = "12mm" },
        }).ConfigureAwait(false);
    }

    private RenderedReportArtifact Artifact(string fileName, byte[] pdf)
    {
        using var document = PdfReader.Open(new MemoryStream(pdf), PdfDocumentOpenMode.Import);
        return new RenderedReportArtifact(
            fileName,
            pdf,
            document.PageCount,
            Convert.ToHexStringLower(SHA256.HashData(pdf)),
            AssessmentReportContract.TemplateVersion,
            EngineVersion);
    }

    private static ScriptObject CommonContext(AssessmentReportSnapshot snapshot)
    {
        var result = new ScriptObject();
        result["css"] = ResourceText("templates.report.css");
        result["logo"] = ResourceDataUri("brand.logo.png", "image/png");
        result["our_ref"] = Encode(snapshot.OurReference);
        result["your_ref"] = Encode(snapshot.YourReference);
        result["date"] = snapshot.ReportDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        result["claimant"] = Encode(snapshot.ClaimantName);
        result["incident_date"] = snapshot.IncidentDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        result["report_for"] = string.Join("<br>", snapshot.ReportFor.Select(Encode));
        return result;
    }

    private static string VehicleRows(AssessmentReportSnapshot snapshot) => Rows(
        ("Make", snapshot.Vehicle.Make), ("Registration", snapshot.Vehicle.Registration),
        ("Model", snapshot.Vehicle.Model), ("VIN", snapshot.Vehicle.Vin ?? "—"),
        ("Odometer", snapshot.Vehicle.MileageDescription),
        ("Engine / Fuel", Join(" · ", snapshot.Vehicle.Engine, snapshot.Vehicle.Fuel)),
        ("VIN Checked", Flag(snapshot.Vehicle.VinChecked)),
        ("Transmission", snapshot.Vehicle.Transmission ?? "—"),
        ("Colour / Body", Join(" · ", snapshot.Vehicle.Colour, snapshot.Vehicle.Body)),
        ("Tax Expiry", Date(snapshot.Vehicle.TaxExpiry)), ("MOT Expiry", Date(snapshot.Vehicle.MotExpiry)),
        ("Airbags Deployed", snapshot.Vehicle.AirbagsDeployed ?? "—"),
        ("Fault Codes", snapshot.Vehicle.FaultCodes ?? "—"),
        ("Temporary Repairs Possible", Flag(snapshot.Vehicle.TemporaryRepairsPossible)),
        ("Temporary Repair Method", snapshot.Vehicle.TemporaryRepairMethod ?? "—"),
        ("Temporary Repair Cost", OptionalMoney(snapshot.Vehicle.TemporaryRepairCost)),
        ("Pre-Incident Condition", Display(snapshot.Vehicle.Condition)),
        ("Impact Magnitude", $"{Display(snapshot.ImpactSeverity)} — {Display(snapshot.ImpactLocation)}"));

    private static string VehicleDataRows(AssessmentReportSnapshot snapshot) => Rows(
        ("Retail Value", Money(snapshot.RetailValue)), ("Trade Value", Money(snapshot.TradeValue)),
        ("Engineer's Value", Money(snapshot.EngineerValue)), ("VIN", snapshot.Vehicle.Vin ?? "—"),
        ("Year", snapshot.Vehicle.Year), ("Odometer", snapshot.Vehicle.MileageDescription),
        ("Engine", snapshot.Vehicle.Engine ?? "—"), ("Fuel", snapshot.Vehicle.Fuel ?? "—"),
        ("Condition", Display(snapshot.Vehicle.Condition)));

    private static string ImpactRows(IReadOnlyList<ReportImpact> impacts) => impacts.Count == 0
        ? "<tr><td colspan=\"3\">—</td></tr>"
        : string.Join(string.Empty, impacts.Select(impact =>
            $"<tr><td>{Encode(impact.Zone)}</td><td>{Encode(impact.Severity)}</td><td>{Encode(impact.Note)}</td></tr>"));

    /// <summary>
    /// Unrelated damage is an output choice: with "Include unrelated damage"
    /// off the two unrelated rows are omitted, not blanked. The evidence
    /// itself is untouched.
    /// </summary>
    private static string DamageRows(AssessmentReportSnapshot snapshot)
    {
        var damage = snapshot.Damage;
        var rows = new List<(string, string)>();
        if (snapshot.Content.IncludeUnrelatedDamage)
        {
            rows.Add(("Unrelated Damage", damage.Unrelated ?? "—"));
            rows.Add(("Unrelated Damage Deduction", OptionalMoney(damage.UnrelatedDeduction)));
        }
        rows.Add(("Paint / Material Transfer", damage.MaterialTransfer ?? "—"));
        return Rows([.. rows]);
    }

    private static string RestraintRows(ReportDamage damage) => Rows(
        ("Right Front Tyre / Belt", Join(" / ", damage.RightFrontTyre, damage.RightFrontBelt)),
        ("Left Front Tyre / Belt", Join(" / ", damage.LeftFrontTyre, damage.LeftFrontBelt)),
        ("Right Rear Tyre / Belt", Join(" / ", damage.RightRearTyre, damage.RightRearBelt)),
        ("Left Rear Tyre / Belt", Join(" / ", damage.LeftRearTyre, damage.LeftRearBelt)),
        ("Spare Tyre", damage.SpareTyre ?? "—"), ("Centre Belt", damage.CentreBelt ?? "—"));

    private static string SettlementRows(ReportSettlement settlement) => Rows(
        ("Excess", OptionalMoney(settlement.Excess)), ("Betterment", OptionalMoney(settlement.Betterment)),
        ("Claimant VAT Registered", Flag(settlement.ClaimantVatRegistered)), ("Reserve", OptionalMoney(settlement.Reserve)),
        ("Equity", Money(settlement.Equity)), ("Repair Duration", settlement.RepairDays is { } days ? $"{days} days" : "—"),
        ("Repair Delays", settlement.RepairDelays ?? "—"), ("Report Delay", settlement.ReportDelay ?? "—"),
        ("Storage Per Day", OptionalMoney(settlement.StoragePerDay)), ("Recovery", OptionalMoney(settlement.Recovery)),
        ("Hire Start", Date(settlement.HireStart)), ("Hire Daily Cost", OptionalMoney(settlement.HireDailyCost)),
        ("Diminution", OptionalMoney(settlement.Diminution)), ("Salvage At", settlement.SalvageAt ?? "—"),
        ("Salvage Agent", settlement.SalvageAgent ?? "—"), ("Salvage Agent Reference", settlement.SalvageAgentReference ?? "—"),
        ("Salvage Moved", Flag(settlement.SalvageMoved)), ("Owner Retains Salvage", Flag(settlement.SalvageOwnerRetains)),
        ("Salvage Value Agreed", Flag(settlement.SalvageValueAgreed)), ("Salvage Settled", Date(settlement.SalvageSettled)));

    /// <summary>
    /// The Current estimate's canonical printed breakdown. Hours and the
    /// hourly rate are descriptive; the five printed components, the printed
    /// sub total, the printed VAT and the printed total are the estimate's
    /// own figures and reconcile exactly.
    /// </summary>
    private static string CostRows(ReportRepairCosts costs) => AmountRows(
        ("Labour Hours", Hours(costs.LabourHours)),
        ("Paint Hours", Hours(costs.PaintHours)),
        ("Hourly Rate", Money(costs.HourlyRate)),
        ("Parts", Money(costs.Printed.Parts)),
        ("Panel Labour", Money(costs.Printed.PanelLabour)),
        ("Paint Labour", Money(costs.Printed.PaintLabour)),
        ("Paint Materials", Money(costs.Printed.Materials)),
        ("Specialist / Other", Money(costs.Printed.Specialist)),
        ("Sub Total", Money(costs.Printed.Net)),
        (costs.VatLabel, Money(costs.Printed.Vat)),
        ("Total Estimated Repair Cost", Money(costs.Total)));

    private static string Worklists(AssessmentReportSnapshot snapshot) =>
        List("Main New Parts Required", snapshot.NewParts) + List("Repairs Required", snapshot.Repairs) + List("Additional Operations", snapshot.Operations);

    private static string FeeRows(AssessmentReportSnapshot snapshot)
    {
        var descriptions = snapshot.FeeDescriptionLines.Count == 0
            ? new[] { "Independent automotive engineering assessment" }
            : snapshot.FeeDescriptionLines;
        return $"<div class=\"fee-detail\">{string.Join("<br>", descriptions.Select(Encode))}</div>";
    }

    private static string Introduction(AssessmentReportSnapshot snapshot)
    {
        var location = snapshot.AssessmentMethod == "image_based"
            ? "Image Based Assessment"
            : snapshot.LocationAddress!;
        return $"In accordance with your instructions received on {snapshot.InstructionsReceived:dd/MM/yyyy} requesting us to provide an independent accident damage report, we assessed the damage on {snapshot.Assessed:dd/MM/yyyy}. Vehicle located at: {Encode(location)}. Our findings are as detailed below.";
    }

    private static string EngineerComments(AssessmentReportSnapshot snapshot)
    {
        var parts = new List<string> { $"<p>{MileageSentence(snapshot.Vehicle.MileageSource)}</p>" };
        if (snapshot.LegalStatus.Equals("unroadworthy", StringComparison.OrdinalIgnoreCase))
        {
            parts.Add($"<p>Please note the vehicle is unroadworthy due to {Encode(snapshot.UnroadworthyReason)}.</p>");
        }
        if (!string.IsNullOrWhiteSpace(snapshot.EngineerComments))
        {
            parts.Add(Paragraph(snapshot.EngineerComments));
        }
        return string.Join(string.Empty, parts);
    }

    private static string MileageSentence(string source) => source switch
    {
        "online_data" => "The mileage has been calculated from online data.",
        "owner" => "The mileage has been provided by the owner.",
        "repairer" => "The mileage has been provided by the repairer.",
        "principal" => "The mileage has been provided by the instructing principal.",
        "average" => "The mileage has been calculated from average mileage data.",
        "tbc" => "The mileage is to be confirmed.",
        _ => throw new ReportRenderRejectedException("Unsupported mileage source."),
    };

    private static string Tiles(AssessmentReportSnapshot snapshot, AssessmentReportPresentation presentation)
    {
        (string Label, string Value, bool Highlight)[] tiles = snapshot.Outcome == AssessmentReportOutcome.TotalLoss
            ?
            [
                ("Pre-Accident Value", Money(snapshot.EngineerValue), false),
                ("Repair Cost inc VAT", Money(snapshot.Costs.Total), false),
                ("Salvage Value", Money(snapshot.SalvageValue!.Value), false),
                ("Recommended Settlement", Money(presentation.RecommendedSettlement!.Value), true),
            ]
            :
            [
                ("Pre-Accident Value", Money(snapshot.EngineerValue), false),
                ("Labour Hours", Hours(snapshot.Costs.LabourHours), false),
                (snapshot.Outcome == AssessmentReportOutcome.CashInLieu ? "Cash in Lieu Settlement" : "Repair Cost inc VAT", Money(snapshot.Costs.Total), true),
            ];
        return string.Join(string.Empty, tiles.Select(tile =>
            $"<div class=\"tile{(tile.Highlight ? " red" : string.Empty)}\"><span class=\"tile-label\">{Encode(tile.Label)}</span><span class=\"tile-value\">{Encode(tile.Value)}</span></div>"));
    }

    private static string Salvage(AssessmentReportSnapshot snapshot) => snapshot.Outcome == AssessmentReportOutcome.TotalLoss
        ? $"<section class=\"section\"><h2>Salvage</h2><p>Under the current salvage categorisation matrix, within the scope of our inspection, we consider that this is Category S (structural damage) and can be sold as repairable salvage. Further information is available at www.abi.org.uk. We suggest that the sale of the salvage will realise in the order of {Encode(Money(snapshot.SalvageValue!.Value))}. We have not taken any action towards removal of the salvage at this time.</p></section>"
        : string.Empty;

    /// <summary>
    /// Emits each prepared image inside a square frame that honours its
    /// persisted rotation and fractional crop. The crop fractions are of the
    /// rotated source, so the rotation is applied to the image and the crop
    /// to the frame it sits in. Order is the snapshot's: Close-up first,
    /// Overview second, Supporting by its persisted order.
    /// </summary>
    private static string Photos(IReadOnlyList<ReportImageEvidence> photos) => string.Join(
        string.Empty,
        photos.Select(photo =>
        {
            var crop = photo.AppliedCrop;
            var style = string.Create(
                CultureInfo.InvariantCulture,
                $"width:{100m / crop.Width:0.###}%;height:{100m / crop.Height:0.###}%;left:{-100m * crop.Left / crop.Width:0.###}%;top:{-100m * crop.Top / crop.Height:0.###}%");
            return $"<figure class=\"photo-frame\"><div class=\"photo-crop\" style=\"{style}\">"
                + $"<img class=\"vehicle-photo rot{(int)photo.Rotation}\" src=\"{ImageDataUri(photo.Content, photo.ContentType)}\" alt=\"Vehicle image\">"
                + "</div></figure>";
        }));

    private static string ImageDataUri(byte[] content, string contentType) =>
        $"data:{contentType};base64,{Convert.ToBase64String(content)}";

    /// <summary>
    /// The accepted statement of truth, source-aware. The Glass's sentence is
    /// printed only when the operator turned "Disclose guide source" on and a
    /// Glass's valuation guide was actually used; otherwise it is omitted. No
    /// substitute sentence is written — the approved v3 specification supplies
    /// none (H5), and it names no other guide.
    /// </summary>
    private static string StatementOfTruth(AssessmentReportSnapshot snapshot)
    {
        var paragraphs = new List<string>
        {
            AssessmentReportContract.StatementOfTruth1,
            AssessmentReportContract.StatementOfTruth2,
        };
        if (snapshot.PrintsGuideDisclosure)
        {
            paragraphs.Add(AssessmentReportContract.StatementOfTruthGuide);
        }
        paragraphs.Add(AssessmentReportContract.StatementOfTruth3);
        paragraphs.Add(AssessmentReportContract.StatementOfTruth4);
        return string.Join(string.Empty, paragraphs.Select(Paragraph));
    }

    private static string Footer(AssessmentReportSnapshot snapshot, bool feeNote)
    {
        var centre = feeNote
            ? $"{Encode(snapshot.Vehicle.Registration)} · {Encode(snapshot.OurReference)} &nbsp;|&nbsp; Collision Engineers Ltd &nbsp;|&nbsp; VAT No: {AssessmentReportContract.VatNumber}"
            : $"{Encode(snapshot.Vehicle.Registration)} · {Encode(snapshot.OurReference)} &nbsp;|&nbsp; Collision Engineers Ltd &nbsp;|&nbsp; www.CollisionEngineers.co.uk";
        return $"<div style=\"width:100%;font:8pt Arial;color:#555;padding:0 12mm;position:relative\"><span>{centre}</span><span style=\"position:absolute;right:12mm\">Page <span class=\"pageNumber\"></span> of <span class=\"totalPages\"></span></span></div>";
    }

    private static string Display(string value) =>
        CultureInfo.GetCultureInfo("en-GB").TextInfo.ToTitleCase(value.Replace('_', ' ').ToLowerInvariant());
    private static string Hours(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
    private static string Flag(bool? value) => value switch { true => "Yes", false => "No", null => "—" };
    private static string Date(DateOnly? value) => value?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "—";
    private static string OptionalMoney(decimal? value) => value is { } amount ? Money(amount) : "—";
    private static string Join(string separator, params string?[] values) =>
        string.Join(separator, values.Where(x => !string.IsNullOrWhiteSpace(x)));

    private static string Rows(params (string Label, string Value)[] rows) => string.Join(
        string.Empty,
        rows.Select(x => $"<tr><th>{Encode(x.Label)}</th><td>{Encode(x.Value)}</td></tr>"));
    private static string AmountRows(params (string Label, string Value)[] rows) => string.Join(
        string.Empty,
        rows.Select(x => $"<tr><td>{Encode(x.Label)}</td><td class=\"price\">{Encode(x.Value)}</td></tr>"));
    private static string List(string title, IReadOnlyList<string> items) => items.Count == 0
        ? string.Empty
        : $"<section class=\"section work-list\"><h2>{Encode(title)}</h2><table><tbody>{string.Join(string.Empty, items.Select(x => $"<tr><td>{Encode(x)}</td></tr>"))}</tbody></table></section>";
    private static string Paragraph(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : $"<p>{Encode(value)}</p>";
    private static string Money(decimal value) => value.ToString("£#,##0.00", CultureInfo.GetCultureInfo("en-GB"));
    private static string Number(decimal value) => value.ToString("#,##0.00", CultureInfo.GetCultureInfo("en-GB"));
    private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
    private static string Slug(string value) => new(value.ToUpperInvariant().Select(x => char.IsLetterOrDigit(x) ? x : '_').ToArray());

    private static string ResourceText(string suffix) => TextResources.GetOrAdd(suffix, static name => ReadResourceText(name));

    private static string ReadResourceText(string suffix)
    {
        using var stream = ResourceStream(suffix);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string ResourceDataUri(string suffix, string contentType) => DataResources.GetOrAdd(
        suffix,
        name => ReadResourceDataUri(name, contentType));

    private static string ReadResourceDataUri(string suffix, string contentType)
    {
        using var stream = ResourceStream(suffix);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return $"data:{contentType};base64,{Convert.ToBase64String(memory.ToArray())}";
    }

    private static Stream ResourceStream(string suffix)
    {
        var assembly = typeof(PlaywrightAssessmentReportRenderer).Assembly;
        var name = $"Pegasus.Infrastructure.Reports.Assets.{suffix}";
        return assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Required report resource '{name}' is missing.");
    }

    public async ValueTask DisposeAsync()
    {
        if (browser is not null)
        {
            await browser.DisposeAsync().ConfigureAwait(false);
        }
        playwright?.Dispose();
        gate.Dispose();
    }
}
