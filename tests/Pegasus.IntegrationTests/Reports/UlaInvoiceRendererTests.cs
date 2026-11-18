using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Reports;
using Pegasus.Infrastructure;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using static Pegasus.IntegrationTests.Reports.PrintedPages;

namespace Pegasus.IntegrationTests.Reports;

/// <summary>
/// The Principal's own invoice, the ULA Invoice, rendered by the integrated
/// renderer from the ready fixture's snapshot (R1147 to R1150): named for the
/// claim it bills, printing the issuer, the claimant, the vehicle, its rows
/// and the report's own totals, its VAT number and terms only when the
/// Principal has them, and refused for an outcome that has no invoice.
/// </summary>
public sealed class UlaInvoiceRendererTests
{
    private const string ClaimReference = "257922";

    private static readonly string[] Issuer =
        ["United Legal Assistance Limited", "130c Lord Street", "Southport PR9 0AE"];

    private static readonly string[] Addressee = ["Alex Example", "1 High Street", "Southport", "PR8 1AA"];

    [Fact]
    public async Task TheInvoicePrintsTheReportsFactsAndFigures()
    {
        await using var provider = RendererProvider();
        var renderer = provider.GetRequiredService<IAssessmentReportRenderer>();
        var snapshot = InvoiceSnapshot(new UlaInvoiceSnapshot(Issuer, Addressee, "FORD FOCUS"));

        var artifact = await new GenerateAssessmentReportDraft(renderer)
            .ExecuteAsync(snapshot, CaseReportArtifactKind.PrincipalInvoice);

        Assert.Equal("ULA_Invoice_257922.pdf", artifact.SuggestedFileName);
        Assert.Equal(1, artifact.PageCount);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(artifact.Pdf)), artifact.Sha256);
        var text = Squeeze(Text(artifact.Pdf));
        foreach (var expected in Issuer.Concat(Addressee))
        {
            Assert.Contains(Squeeze(expected), text, StringComparison.Ordinal);
        }
        foreach (var expected in new[]
        {
            UlaInvoiceWording.Title,
            UlaInvoiceWording.InvoiceNumberLabel,
            UlaInvoiceWording.InvoiceDateLabel,
            UlaInvoiceWording.SubjectLabel,
            UlaInvoiceWording.ReferenceLabel,
            "19/08/2026",
            "FORD FOCUS",
            snapshot.Vehicle.Registration,
            UlaInvoiceCalculation.TotalLabour,
            UlaInvoiceCalculation.Parts,
            UlaInvoiceCalculation.PaintMaterials,
            UlaInvoiceCalculation.AdditionalOperations,
            UlaInvoiceWording.SubTotalLabel,
            snapshot.Costs.VatLabel!,
            UlaInvoiceWording.TotalLabel,
            "£225.00",
            "£45.00",
            "£270.00",
        })
        {
            Assert.Contains(Squeeze(expected), text, StringComparison.Ordinal);
        }
        // The claim reference prints as Invoice No and again as Ref (R1148).
        Assert.Equal(2, text.Split(ClaimReference, StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain(UlaInvoiceWording.VatNumberLead, text, StringComparison.Ordinal);
        Assert.DoesNotContain(UlaInvoiceWording.TermsLabel, text, StringComparison.Ordinal);
        // No Collision Engineers wording: the invoice is the Principal's.
        Assert.DoesNotContain(Squeeze(AssessmentReportWording.CompanyName), text, StringComparison.Ordinal);
    }

    /// <summary>The Principal's Invoice VAT number and payment terms print when it has them (R1148).</summary>
    [Fact]
    public async Task TheVatNumberAndTermsPrintWhenThePrincipalHasThem()
    {
        await using var provider = RendererProvider();
        var renderer = provider.GetRequiredService<IAssessmentReportRenderer>();
        var snapshot = InvoiceSnapshot(new UlaInvoiceSnapshot(
            Issuer, Addressee, "FORD FOCUS", VatNumber: "GB 123 4567 89", PaymentTerms: "30 days"));

        var artifact = await new GenerateAssessmentReportDraft(renderer)
            .ExecuteAsync(snapshot, CaseReportArtifactKind.PrincipalInvoice);

        var text = Squeeze(Text(artifact.Pdf));
        Assert.Contains(Squeeze($"{UlaInvoiceWording.VatNumberLead} GB 123 4567 89"), text, StringComparison.Ordinal);
        Assert.Contains(Squeeze($"{UlaInvoiceWording.TermsLabel} 30 days"), text, StringComparison.Ordinal);
    }

    /// <summary>A Total loss has no invoice (R1143), so the renderer refuses one.</summary>
    [Fact]
    public async Task ATotalLossInvoiceIsRefused()
    {
        await using var provider = RendererProvider();
        var renderer = provider.GetRequiredService<IAssessmentReportRenderer>();
        var snapshot = InvoiceSnapshot(new UlaInvoiceSnapshot(Issuer, Addressee, "FORD FOCUS")) with
        {
            Outcome = AssessmentReportOutcome.TotalLoss,
            SalvageCategory = "S",
            SalvageValue = 500m,
        };

        await Assert.ThrowsAsync<ReportRenderRejectedException>(
            () => new GenerateAssessmentReportDraft(renderer)
                .ExecuteAsync(snapshot, CaseReportArtifactKind.PrincipalInvoice));
    }

    /// <summary>A snapshot frozen without the Principal's invoice facts has no invoice to print.</summary>
    [Fact]
    public async Task ASnapshotWithoutInvoiceFactsIsRefused()
    {
        await using var provider = RendererProvider();
        var renderer = provider.GetRequiredService<IAssessmentReportRenderer>();
        var snapshot = InvoiceSnapshot(new UlaInvoiceSnapshot(Issuer, Addressee, "FORD FOCUS")) with
        {
            PrincipalInvoice = null,
        };

        await Assert.ThrowsAsync<ReportRenderRejectedException>(
            () => new GenerateAssessmentReportDraft(renderer)
                .ExecuteAsync(snapshot, CaseReportArtifactKind.PrincipalInvoice));
    }

    /// <summary>The ready fixture's repairable snapshot, for a claim numbered as the draft's.</summary>
    private static AssessmentReportSnapshot InvoiceSnapshot(UlaInvoiceSnapshot invoice) =>
        AssessmentReportRendererTests.ReadySnapshot() with
        {
            YourReference = ClaimReference,
            PrincipalInvoice = invoice,
        };

    /// <summary>The document's text, page after page.</summary>
    private static string Text(byte[] pdf)
    {
        using var document = PdfDocument.Open(pdf);
        return string.Join(" ", document.GetPages().Select(page => ContentOrderTextExtractor.GetText(page)));
    }

    private static ServiceProvider RendererProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPegasusReportRendering();
        return services.BuildServiceProvider();
    }
}
