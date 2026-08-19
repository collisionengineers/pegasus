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

    public async Task<AssessmentReportDraft> RenderAsync(
        AssessmentReportSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var activeBrowser = await GetBrowserAsync().ConfigureAwait(false);
            var presentation = snapshot.Presentation();
            var assessment = CommonContext(snapshot);
            assessment["title"] = presentation.Title;
            assessment["badge"] = presentation.Badge;
            assessment["vehicle_rows"] = VehicleRows(snapshot.Vehicle);
            assessment["outcome_wording"] = $"{Encode(presentation.SettlementText)} {Money(presentation.RecommendedSettlement!.Value)}";
            assessment["roadworthiness"] = Roadworthiness(snapshot);
            assessment["history"] = Encode(snapshot.HistoryCheck);
            assessment["comments"] = Paragraph(snapshot.EngineerComments);
            assessment["worklists"] = Worklists(snapshot);
            assessment["figure_rows"] = FigureRows(snapshot);
            assessment["signature"] = ResourceDataUri($"brand.signatures.{snapshot.Engineer.SignatureKey}.png", "image/png");
            assessment["engineer"] = Encode(snapshot.Engineer.Name);
            assessment["qualifications"] = Encode(snapshot.Engineer.Qualifications);
            var fee = CommonContext(snapshot);
            fee["registration"] = Encode(snapshot.Vehicle.Registration);
            fee["fee_rows"] = FeeRows(snapshot);
            fee["total"] = Money(snapshot.AgreedFee);

            var assessmentPdf = await RenderPdfAsync(activeBrowser, "assessment_report.scriban", assessment, cancellationToken).ConfigureAwait(false);
            var feePdf = await RenderPdfAsync(activeBrowser, "assessment_fee_note.scriban", fee, cancellationToken).ConfigureAwait(false);
            return new AssessmentReportDraft(
                Artifact($"{Slug(snapshot.OurReference)}_assessment.pdf", assessmentPdf),
                Artifact($"{Slug(snapshot.OurReference)}_fee_note.pdf", feePdf));
        }
        finally
        {
            gate.Release();
        }
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
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var template = Templates.GetOrAdd(templateName, static name =>
            Template.Parse(ResourceText($"templates.{name}")));
        if (template.HasErrors)
        {
            throw new InvalidOperationException(string.Join("; ", template.Messages));
        }
        var context = new TemplateContext();
        context.PushGlobal(values);
        var html = await template.RenderAsync(context).ConfigureAwait(false);
        if (html.Contains("{{", StringComparison.Ordinal) || html.Contains('«'))
        {
            throw new ReportRenderRejectedException("The composed report contains an unresolved placeholder.");
        }
        await using var page = await activeBrowser.NewPageAsync().ConfigureAwait(false);
        await page.SetContentAsync(html, new PageSetContentOptions { WaitUntil = WaitUntilState.Load }).ConfigureAwait(false);
        return await page.PdfAsync(new PagePdfOptions
        {
            Format = "A4",
            PrintBackground = true,
            Margin = new Margin { Top = "8mm", Right = "12mm", Bottom = "18mm", Left = "12mm" },
        }).ConfigureAwait(false);
    }

    private static RenderedReportArtifact Artifact(string fileName, byte[] pdf)
    {
        using var document = PdfReader.Open(new MemoryStream(pdf), PdfDocumentOpenMode.Import);
        return new RenderedReportArtifact(
            fileName,
            pdf,
            document.PageCount,
            Convert.ToHexStringLower(SHA256.HashData(pdf)),
            AssessmentReportContract.TemplateVersion,
            $"Playwright/{typeof(Playwright).Assembly.GetName().Version}; Chromium");
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

    private static string Roadworthiness(AssessmentReportSnapshot snapshot) =>
        snapshot.LegalStatus.Equals("unroadworthy", StringComparison.OrdinalIgnoreCase)
            ? $"The vehicle is unroadworthy: {Encode(snapshot.UnroadworthyReason)}"
            : "The vehicle is roadworthy.";

    private static string VehicleRows(ReportVehicle vehicle) => Rows(
        ("Registration", vehicle.Registration), ("Make / Model", $"{vehicle.Make} {vehicle.Model}"),
        ("Year", vehicle.Year), ("Type", vehicle.VehicleType), ("Condition", vehicle.Condition),
        ("Mileage", vehicle.MileageDescription));

    private static string FigureRows(AssessmentReportSnapshot snapshot) => Rows(
        ("Engineer value", Money(snapshot.EngineerValue)), ("Retail value", Money(snapshot.RetailValue)),
        ("Trade value", Money(snapshot.TradeValue)), ("Labour", Money(snapshot.Costs.Labour)),
        ("Subtotal", Money(snapshot.Costs.Subtotal)), ("VAT", Money(snapshot.Costs.Vat)),
        ("Repair total", Money(snapshot.Costs.Total)));

    private static string Worklists(AssessmentReportSnapshot snapshot) =>
        List("New parts", snapshot.NewParts) + List("Repairs", snapshot.Repairs) + List("Operations", snapshot.Operations);

    private static string FeeRows(AssessmentReportSnapshot snapshot)
    {
        var descriptions = snapshot.FeeDescriptionLines.Count == 0
            ? new[] { "Independent automotive engineering assessment" }
            : snapshot.FeeDescriptionLines;
        return string.Join(string.Empty, descriptions.Select((x, i) =>
            $"<tr><td>{Encode(x)}</td><td>{(i == 0 ? Money(snapshot.AgreedFee) : string.Empty)}</td></tr>"));
    }

    private static string Rows(params (string Label, string Value)[] rows) => string.Join(
        string.Empty,
        rows.Select(x => $"<tr><th>{Encode(x.Label)}</th><td>{Encode(x.Value)}</td></tr>"));
    private static string List(string title, IReadOnlyList<string> items) => items.Count == 0
        ? string.Empty
        : $"<h3>{Encode(title)}</h3><ul>{string.Join(string.Empty, items.Select(x => $"<li>{Encode(x)}</li>"))}</ul>";
    private static string Paragraph(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : $"<p>{Encode(value)}</p>";
    private static string Money(decimal value) => value.ToString("£#,##0.00", CultureInfo.GetCultureInfo("en-GB"));
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
