using Pegasus.Core.Assessment;

namespace Pegasus.Core.Reports;

/// <summary>
/// What the Principal's own invoice (the ULA Invoice) prints beyond the
/// report's facts, frozen with the report's snapshot when the generation is
/// made (R1147 to R1149). The claim reference, the registration, the report
/// date and every figure are the report's own, so the invoice can never
/// disagree with the report it accompanies.
/// </summary>
/// <param name="Issuer">The Principal's name and address lines, as they print at the top left.</param>
/// <param name="Addressee">
/// The claimant's name, then any recorded claimant address lines: the name
/// alone when no address is recorded, never a refusal (R1149, as R576).
/// </param>
/// <param name="SubjectVehicle">The Case's make and model in capitals (R1149), <see cref="UlaInvoiceCalculation.SubjectVehicle"/>.</param>
/// <param name="VatNumber">The Principal's Invoice VAT number; its row is left out when blank (R1148).</param>
/// <param name="PaymentTerms">The Principal's Invoice payment terms; its row is left out when blank (R1148).</param>
public sealed record UlaInvoiceSnapshot(
    IReadOnlyList<string> Issuer,
    IReadOnlyList<string> Addressee,
    string SubjectVehicle,
    string? VatNumber = null,
    string? PaymentTerms = null)
{
    /// <summary>
    /// The issuer block as the draft prints it: the Principal's name, then
    /// each line of its stored address, trimmed and with blank lines dropped,
    /// with the postcode after the last address line ("Southport PR9 0AE").
    /// With no address line the postcode is a line of its own; with neither,
    /// the name alone. A Principal with no name has no issuer, and the
    /// invoice is refused for want of one, as the report is for want of an
    /// addressee (R1147).
    /// </summary>
    public static IReadOnlyList<string> IssuerLines(string? name, string? address, string? postcode)
    {
        var lines = new List<string>();
        if (string.IsNullOrWhiteSpace(name))
        {
            return lines;
        }
        lines.Add(name.Trim());
        lines.AddRange((address ?? string.Empty).Split(
            '\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        var code = postcode?.Trim();
        if (!string.IsNullOrEmpty(code))
        {
            if (lines.Count == 1)
            {
                lines.Add(code);
            }
            else if (!Compact(lines[^1]).EndsWith(Compact(code), StringComparison.OrdinalIgnoreCase))
            {
                // An address already ending with its postcode does not print
                // it twice, as the report's own address block does not.
                lines[^1] = $"{lines[^1]} {code}";
            }
        }
        return lines;
    }

    private static string Compact(string value) =>
        string.Concat(value.Where(character => !char.IsWhiteSpace(character)));

    /// <summary>
    /// Fails closed on an invoice with no issuer or no addressee, or with a
    /// blank line in either: the invoice prints each line as it stands.
    /// </summary>
    public void Validate()
    {
        // Worded as AcceptedReportSource.Required words a missing input.
        if (Issuer is not { Count: > 0 })
        {
            throw new ReportRenderRejectedException($"{nameof(Issuer)} is required.");
        }
        if (Addressee is not { Count: > 0 })
        {
            throw new ReportRenderRejectedException($"{nameof(Addressee)} is required.");
        }
        if (Issuer.Any(string.IsNullOrWhiteSpace) || Addressee.Any(string.IsNullOrWhiteSpace))
        {
            throw new ReportRenderRejectedException("Report inputs cannot contain blank entries.");
        }
    }
}

/// <summary>
/// Every word the Principal's own invoice prints that is not data, copied
/// from the draft <c>ULA_Invoice_257922_DRAFT.docx</c> (R1147), so its layout
/// decides geometry and nothing else, as the report's does. The row names are
/// <see cref="UlaInvoiceCalculation"/>'s; the VAT total's label is the
/// report's own <see cref="ReportRepairCosts.VatLabel"/>.
/// </summary>
public static class UlaInvoiceWording
{
    public const string Title = "Invoice";
    public const string InvoiceNumberLabel = "Invoice No";
    public const string InvoiceDateLabel = "Invoice Date";
    public const string TermsLabel = "Terms";
    public const string VatNumberLead = "VAT:";
    public const string SubjectLabel = "Subject";
    public const string ReferenceLabel = "Ref";
    public const string SubTotalLabel = "Sub Total";
    public const string TotalLabel = "Total";

    /// <summary>The line table's seven column headings, in print order.</summary>
    public static IReadOnlyList<string> ColumnHeadings { get; } =
        ["#", "Description", "Qty", "Rate", "VAT %", "VAT", "Amount"];
}

/// <summary>One row of the invoice's table, numbered from 1 in print order.</summary>
public sealed record UlaInvoiceRow(
    int Number,
    string Description,
    decimal Quantity,
    decimal Rate,
    decimal VatPercent,
    decimal Vat,
    decimal Amount);

/// <summary>
/// The invoice's rows and its three totals. Sub Total, VAT and Total are the
/// Audit Report's own to the penny (R1150).
/// </summary>
public sealed record UlaInvoiceFigures(
    IReadOnlyList<UlaInvoiceRow> Rows,
    decimal SubTotal,
    decimal Vat,
    decimal Total);

/// <summary>
/// The one calculation of the Principal's own invoice (R1150, R1157, R1160):
/// pure, from the report's frozen repair costs, so the invoice's figures are
/// derived from the same <see cref="EstimateTotals"/> the report prints and
/// never from a second sum of the estimate. Core owns the row names; the
/// layout prints them.
/// </summary>
public static class UlaInvoiceCalculation
{
    /// <summary>The report's Total Labour: printed panel labour plus printed paint labour.</summary>
    public const string TotalLabour = "Total Labour";

    /// <summary>The report's Parts.</summary>
    public const string Parts = "Parts";

    /// <summary>The report's Materials.</summary>
    public const string PaintMaterials = "Paint / Materials";

    /// <summary>The report's Specialist figure (R1157).</summary>
    public const string AdditionalOperations = "Additional Operations";

    /// <summary>The one row of a Contract repair: the agreed contract sum (R1150).</summary>
    public const string ContractRepair = "Contract Repair";

    /// <summary>Every row prints a quantity of one.</summary>
    private const decimal One = 1m;

    /// <summary>
    /// Whether a frozen report has an invoice to make (R1143, R1147, R1148):
    /// its Principal's invoice facts were frozen with it, it has the claim
    /// reference the invoice is numbered by, and its outcome has something to
    /// bill, a Repairable with a sub total or a Contract repair with an agreed
    /// sum. It reads stored facts and computes nothing, so the Case page can
    /// ask it of any generation.
    /// </summary>
    public static bool IsDue(AssessmentReportSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return snapshot.PrincipalInvoice is not null
            && !string.IsNullOrWhiteSpace(snapshot.YourReference)
            && snapshot.Outcome switch
            {
                AssessmentReportOutcome.Repairable => snapshot.Costs.Printed.Net > 0m,
                AssessmentReportOutcome.ContractRepair => snapshot.Settlement.ContractSum is > 0m,
                _ => false,
            };
    }

    /// <summary>The invoice of a snapshot: its outcome, its repair costs and its agreed contract sum.</summary>
    public static UlaInvoiceFigures? For(AssessmentReportSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return For(snapshot.Outcome, snapshot.Costs, snapshot.Settlement.ContractSum);
    }

    /// <summary>
    /// The invoice's figures, or null when the outcome has no invoice: a
    /// Total loss and a Cash in lieu omit it (R1143, R1147), and a Contract
    /// repair with no positive agreed sum has nothing to bill.
    /// </summary>
    public static UlaInvoiceFigures? For(AssessmentReportOutcome outcome, ReportRepairCosts costs, decimal? contractSum)
    {
        ArgumentNullException.ThrowIfNull(costs);
        return outcome switch
        {
            AssessmentReportOutcome.Repairable => Repairable(costs),
            AssessmentReportOutcome.ContractRepair =>
                contractSum is { } sum && sum > 0m ? ContractRepaired(costs, sum) : null,
            AssessmentReportOutcome.TotalLoss or AssessmentReportOutcome.CashInLieu => null,
            _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
        };
    }

    /// <summary>
    /// The Subject bar's vehicle: the Case's make and model in capitals
    /// (R1149). A model recorded with its make already in front does not
    /// print the make twice.
    /// </summary>
    public static string SubjectVehicle(string? make, string? model)
    {
        var maker = make?.Trim() ?? string.Empty;
        var named = model?.Trim() ?? string.Empty;
        if (maker.Length > 0
            && named.StartsWith(maker, StringComparison.OrdinalIgnoreCase)
            && (named.Length == maker.Length || char.IsWhiteSpace(named[maker.Length])))
        {
            return named.ToUpperInvariant();
        }
        var vehicle = maker.Length == 0 ? named
            : named.Length == 0 ? maker
            : $"{maker} {named}";
        return vehicle.ToUpperInvariant();
    }

    /// <summary>
    /// The report's four printed components, in the draft's order, each
    /// taxed as the Current repair spec's VAT policy taxes its category. A
    /// row of 0.00 is left out and the rest are numbered 1..N. Each row's VAT
    /// is rounded to the penny as the estimate rounds its printed figures;
    /// whatever that leaves between the rows and the report's printed VAT
    /// goes on the largest taxable row, so the totals are the report's
    /// (R1150).
    /// </summary>
    private static UlaInvoiceFigures? Repairable(ReportRepairCosts costs)
    {
        var printed = costs.Printed;
        var policy = costs.Totals.VatPolicy;
        (string Description, decimal Rate, EstimateVatCategories Category)[] components =
        [
            (TotalLabour, costs.TotalLabour, EstimateVatCategories.Labour),
            (Parts, printed.Parts, EstimateVatCategories.Parts),
            (PaintMaterials, printed.Materials, EstimateVatCategories.Materials),
            (AdditionalOperations, printed.Specialist, EstimateVatCategories.Specialist),
        ];

        var rows = new List<UlaInvoiceRow>();
        var taxable = new List<bool>();
        foreach (var (description, rate, category) in components)
        {
            if (rate == 0m)
            {
                continue;
            }
            var charged = policy.Charges(category);
            var percent = charged ? costs.VatPercent : 0m;
            rows.Add(new(rows.Count + 1, description, One, rate, percent, Pence(rate * percent / 100m), 0m));
            taxable.Add(charged);
        }
        if (rows.Count == 0)
        {
            return null;
        }

        var difference = printed.Vat - rows.Sum(row => row.Vat);
        if (difference != 0m)
        {
            var largest = -1;
            for (var index = 0; index < rows.Count; index++)
            {
                if (taxable[index] && (largest < 0 || rows[index].Rate > rows[largest].Rate))
                {
                    largest = index;
                }
            }
            if (largest < 0)
            {
                throw new InvalidOperationException(
                    "The report charges VAT on no row the invoice prints.");
            }
            rows[largest] = rows[largest] with { Vat = rows[largest].Vat + difference };
        }

        var priced = rows.Select(row => row with { Amount = row.Rate + row.Vat }).ToArray();
        var subTotal = priced.Sum(row => row.Rate);
        if (subTotal != printed.Net)
        {
            throw new InvalidOperationException(
                "The invoice's rows do not reconcile to the report's printed sub total.");
        }
        return new(priced, subTotal, printed.Vat, printed.Gross);
    }

    /// <summary>
    /// One Contract Repair row whose Amount is the agreed sum, VAT included
    /// (R1150). Where the repair spec charges VAT on every category the sum
    /// is split at the estimate's percentage. Otherwise the row carries VAT
    /// in the same share as the report's repair total does, and its VAT %
    /// is the rate that results (R1160).
    /// </summary>
    private static UlaInvoiceFigures ContractRepaired(ReportRepairCosts costs, decimal contractSum)
    {
        var printed = costs.Printed;
        decimal rate;
        decimal vat;
        decimal percent;
        if (costs.Totals.VatPolicy.Categories == EstimateVatCategories.All)
        {
            percent = costs.VatPercent;
            rate = Pence(contractSum / (1m + percent / 100m));
            vat = contractSum - rate;
        }
        else
        {
            vat = printed.Gross == 0m ? 0m : Pence(contractSum * printed.Vat / printed.Gross);
            rate = contractSum - vat;
            percent = rate == 0m ? 0m : Pence(vat / rate * 100m);
        }
        return new(
            [new UlaInvoiceRow(1, ContractRepair, One, rate, percent, vat, contractSum)],
            rate,
            vat,
            contractSum);
    }

    /// <summary>
    /// To the penny, half away from zero: the rounding
    /// <see cref="EstimateTotals.Compute"/> gives every printed figure.
    /// </summary>
    private static decimal Pence(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
