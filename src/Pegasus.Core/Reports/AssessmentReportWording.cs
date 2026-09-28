using System.Globalization;

namespace Pegasus.Core.Reports;

/// <summary>One printed label and the value beside it.</summary>
public sealed record ReportRow(string Label, string Value);

/// <summary>
/// One figure tile on page 1. The template prints the last tile of each
/// outcome in red: <see cref="Highlight"/> marks it.
/// </summary>
public sealed record ReportTile(string Label, string Value, bool Highlight);

/// <summary>
/// What a row of a money table is: a figure, one of the sums the template
/// prints in bold beneath the figures, or the total it shades.
/// </summary>
public enum ReportCostRowKind
{
    Figure,
    Subtotal,
    Total,
}

/// <summary>One row of the repair cost table or of the fee note's totals.</summary>
public sealed record ReportCostRow(string Label, string Value, ReportCostRowKind Kind);

/// <summary>One of the report's work lists: its title and its items in order.</summary>
public sealed record ReportWorkList(string Title, IReadOnlyList<string> Items);

/// <summary>
/// Every word the printed report and fee note carry outside the narrative
/// blocks, and the rows and tiles they print, so the layout decides geometry
/// and nothing else. Each label, heading and sentence is copied word for word
/// from the template (<c>reference/rendererref1</c>: DESIGN_SPEC.md and the
/// four sample reports); nothing is worded here that the template does not
/// word (operator, 27 September 2026). The narrative blocks are
/// <see cref="ReportWordingComposition"/>'s; the statement of truth, the fee
/// terms and the payment details are <see cref="AssessmentReportContract"/>'s.
/// </summary>
public static class AssessmentReportWording
{
    private static readonly CultureInfo Gb = CultureInfo.GetCultureInfo("en-GB");

    // ---- The company block, at the top right of every page -----------------

    public const string CompanyName = "Collision Engineers Ltd";
    public const string CompanyStrapline = "Independent Automotive Experts";
    public const string CompanyEmail = "Engineers@CollisionEngineers.co.uk";
    public const string CompanyWebsite = "www.CollisionEngineers.co.uk";

    /// <summary>The VAT number as printed. It appears on the fee note's pages only.</summary>
    public const string VatNumberLine = "VAT No: " + AssessmentReportContract.VatNumber;

    /// <summary>The company block's lines in print order; the fee note's carries the VAT number.</summary>
    public static IReadOnlyList<string> CompanyBlock(bool feeNote)
    {
        if (feeNote)
        {
            return [CompanyName, CompanyStrapline, VatNumberLine, CompanyEmail, CompanyWebsite];
        }
        return [CompanyName, CompanyStrapline, CompanyEmail, CompanyWebsite];
    }

    // ---- The footer, on every page -----------------------------------------

    /// <summary>
    /// What stands between the footer's three parts. The template writes a
    /// space, a no-break space, a bar, a no-break space and a space.
    /// </summary>
    public const string FooterSeparator = " \u00a0|\u00a0 ";

    public const string PageNumberLead = "Page ";
    public const string PageNumberJoin = " of ";

    /// <summary>
    /// The footer's three parts: registration and Our Ref, the company, then
    /// the website or, on a fee note page, the VAT number. The Repair Spec
    /// printout carries the same footer from its own registration and
    /// reference.
    /// </summary>
    public static IReadOnlyList<string> FooterParts(string registration, string ourReference, bool feeNote)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(ourReference);
        return
        [
            $"{registration} · {ourReference}",
            CompanyName,
            feeNote ? VatNumberLine : CompanyWebsite,
        ];
    }

    /// <summary>The report's footer parts, from its own registration and Our Ref.</summary>
    public static IReadOnlyList<string> FooterParts(AssessmentReportSnapshot snapshot, bool feeNote)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return FooterParts(snapshot.Vehicle.Registration, snapshot.OurReference, feeNote);
    }

    /// <summary>The footer as one line.</summary>
    public static string Footer(string registration, string ourReference, bool feeNote) =>
        string.Join(FooterSeparator, FooterParts(registration, ourReference, feeNote));

    /// <summary>The report's footer as one line.</summary>
    public static string Footer(AssessmentReportSnapshot snapshot, bool feeNote) =>
        string.Join(FooterSeparator, FooterParts(snapshot, feeNote));

    /// <summary>"Page 1 of 7".</summary>
    public static string PageNumber(int page, int pages) =>
        PageNumberLead + page.ToString(CultureInfo.InvariantCulture)
        + PageNumberJoin + pages.ToString(CultureInfo.InvariantCulture);

    // ---- Page 1 ------------------------------------------------------------

    public const string ReportForLabel = "Report For:";
    public const string MatterLabel = "Matter:";
    public const string Roadworthy = "ROADWORTHY";
    public const string Unroadworthy = "UNROADWORTHY";
    public const string ImageBasedAssessment = "Image Based Assessment";
    public const string VehicleDetailsHeading = "Vehicle Details";

    /// <summary>What the template prints where a fact is not recorded.</summary>
    public const string NotRecorded = "—";

    /// <summary>What the template prints as the odometer of a vehicle with no mileage.</summary>
    public const string NoMileage = "TBC";

    /// <summary>Date, Our Ref and Your Ref, in the template's order. The fee note prints the same three.</summary>
    public static IReadOnlyList<ReportRow> References(AssessmentReportSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return
        [
            new("Date:", Date(snapshot.ReportDate)),
            new("Our Ref:", snapshot.OurReference),
            new("Your Ref:", snapshot.YourReference),
        ];
    }

    /// <summary>The matter, composed and never typed. <see cref="MatterLabel"/> and a space stand before it.</summary>
    public static string Matter(AssessmentReportSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return $"Road Traffic Accident: {snapshot.ClaimantName}: {Date(snapshot.IncidentDate)}";
    }

    /// <summary>The roadworthiness badge. <see cref="AssessmentReportSnapshot.IsUnroadworthy"/> says which the template prints in red.</summary>
    public static string RoadworthinessBadge(AssessmentReportSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return snapshot.IsUnroadworthy ? Unroadworthy : Roadworthy;
    }

    /// <summary>
    /// The figure tiles of each outcome as the sample reports print them:
    /// four for a total loss, three for the others, the last one red. The
    /// Labour Hours tile and the Labour Hours row print the same figure.
    /// </summary>
    public static IReadOnlyList<ReportTile> Tiles(AssessmentReportSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var preAccidentValue = new ReportTile("PRE-ACCIDENT VALUE", Money(snapshot.EngineerValue), false);
        var repairCost = Money(snapshot.Costs.Total);
        if (snapshot.Outcome == AssessmentReportOutcome.TotalLoss)
        {
            return
            [
                preAccidentValue,
                new("REPAIR COST INC VAT", repairCost, false),
                new("SALVAGE VALUE", Money(snapshot.SalvageValue!.Value), false),
                new("RECOMMENDED SETTLEMENT", Money(snapshot.Presentation().RecommendedSettlement!.Value), true),
            ];
        }
        return
        [
            preAccidentValue,
            new("LABOUR HOURS", Hours(snapshot.Costs.TotalLabourHours), false),
            new(
                snapshot.Outcome == AssessmentReportOutcome.CashInLieu
                    ? "CASH IN LIEU SETTLEMENT"
                    : "REPAIR COST INC VAT",
                repairCost,
                true),
        ];
    }

    /// <summary>The introduction, composed from the two dates and where the vehicle was assessed.</summary>
    public static string Introduction(AssessmentReportSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var location = snapshot.IsImageBased ? ImageBasedAssessment : snapshot.LocationAddress;
        return $"In accordance with your instructions received on {Date(snapshot.InstructionsReceived)} requesting us to provide an independent accident damage report, we assessed the damage on {Date(snapshot.Assessed)}. Vehicle located at: {location}. Our findings are as detailed below.";
    }

    /// <summary>
    /// The template's eight Vehicle Details cells, in reading order across its
    /// grid: Make and Registration, Model and VIN, Odometer and Engine / Fuel,
    /// Pre-Incident Condition and Impact Magnitude. Every cell prints; a fact
    /// that is not recorded prints <see cref="NotRecorded"/>.
    /// </summary>
    public static IReadOnlyList<ReportRow> VehicleDetails(AssessmentReportSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var vehicle = snapshot.Vehicle;
        return
        [
            new("Make", Recorded(vehicle.Make)),
            new("Registration", Recorded(vehicle.Registration)),
            new("Model", Recorded(vehicle.Model)),
            new("VIN", Recorded(vehicle.Vin)),
            new("Odometer", Recorded(vehicle.MileageDescription)),
            new("Engine / Fuel", EngineAndFuel(vehicle)),
            new("Pre-Incident Condition", ReportWordingComposition.LeadingWords(vehicle.Condition)),
            new("Impact Magnitude", ImpactMagnitude(snapshot.ImpactSeverity, snapshot.ImpactLocation)),
        ];
    }

    /// <summary>"Moderate — right rear": the severity opens the cell and the location follows in small letters.</summary>
    public static string ImpactMagnitude(string impactSeverity, string impactLocation) =>
        $"{ReportWordingComposition.LeadingWords(impactSeverity)} — {ReportWordingComposition.Words(impactLocation)}";

    /// <summary>
    /// The engine size as the template prints it: a plain number of cubic
    /// centimetres reads "1,248 cc". Anything else prints as recorded, and
    /// nothing recorded is null.
    /// </summary>
    public static string? EngineSize(string? recorded)
    {
        if (string.IsNullOrWhiteSpace(recorded))
        {
            return null;
        }
        var value = recorded.Trim();
        return long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var cubicCentimetres)
            ? cubicCentimetres.ToString("N0", Gb) + " cc"
            : value;
    }

    /// <summary>"1,248 cc · Petrol": the engine size and the fuel, whichever are recorded.</summary>
    public static string EngineAndFuel(ReportVehicle vehicle)
    {
        ArgumentNullException.ThrowIfNull(vehicle);
        string?[] parts = [EngineSize(vehicle.Engine), vehicle.Fuel?.Trim()];
        var recorded = string.Join(" · ", parts.Where(part => !string.IsNullOrEmpty(part)));
        return Recorded(recorded);
    }

    // ---- Page 2 ------------------------------------------------------------

    public const string DesktopAssessmentHeading = "Desktop Assessment";
    public const string DesktopAssessment =
        "This report has been compiled from a desktop review of the information available relating to this claim.";

    /// <summary>The value box beneath the settlement sentence: its label and its figure.</summary>
    public static ReportRow ValueBox(AssessmentReportSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var presentation = snapshot.Presentation();
        return new(presentation.SettlementLabel, Money(presentation.RecommendedSettlement!.Value));
    }

    // ---- Page 3 ------------------------------------------------------------

    public const string VehicleDataHeading = "Vehicle Data";
    public const string RepairCostHeading = "Repair Cost Calculation";
    public const string AmountHeading = "Amount";

    /// <summary>
    /// The template's nine Vehicle Data rows, in its order. It leaves a row
    /// with nothing recorded out of this table (DESIGN_SPEC.md § layout
    /// decisions), so a vehicle with no VIN, year, engine size or fuel prints
    /// fewer.
    /// </summary>
    public static IReadOnlyList<ReportRow> VehicleData(AssessmentReportSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var vehicle = snapshot.Vehicle;
        ReportRow[] rows =
        [
            new("Retail Value", Money(snapshot.RetailValue)),
            new("Trade Value", Money(snapshot.TradeValue)),
            new("Engineer's Value", Money(snapshot.EngineerValue)),
            new("VIN", vehicle.Vin?.Trim() ?? string.Empty),
            new("Year", vehicle.Year.Trim()),
            new("Odometer", vehicle.MileageDescription.Trim()),
            new("Engine", EngineSize(vehicle.Engine) ?? string.Empty),
            new("Fuel", vehicle.Fuel?.Trim() ?? string.Empty),
            new("Condition", ReportWordingComposition.LeadingWords(vehicle.Condition)),
        ];
        return [.. rows.Where(row => row.Value.Length > 0)];
    }

    /// <summary>
    /// The repair cost table's nine rows. Labour Hours is panel and paint
    /// hours together and Total Labour is their printed money, so the six
    /// figures from Total Labour to Specialist / Other add up to Sub Total.
    /// </summary>
    public static IReadOnlyList<ReportCostRow> RepairCosts(ReportRepairCosts costs)
    {
        ArgumentNullException.ThrowIfNull(costs);
        var vat = costs.VatLabel
            ?? throw new ReportRenderRejectedException(
                "The report cannot print the VAT on this repair spec. "
                + "Record the repairer's VAT status on the Repair Spec section.");
        return
        [
            new("Labour Hours", Hours(costs.TotalLabourHours), ReportCostRowKind.Figure),
            new("Hourly Rate", Money(costs.HourlyRate), ReportCostRowKind.Figure),
            new("Total Labour", Money(costs.TotalLabour), ReportCostRowKind.Figure),
            new("Parts", Money(costs.Printed.Parts), ReportCostRowKind.Figure),
            new("Paint / Materials", Money(costs.Printed.Materials), ReportCostRowKind.Figure),
            new("Specialist / Other", Money(costs.Printed.Specialist), ReportCostRowKind.Figure),
            new("Sub Total", Money(costs.Printed.Net), ReportCostRowKind.Subtotal),
            new(vat, Money(costs.Printed.Vat), ReportCostRowKind.Subtotal),
            new("Total Estimated Repair Cost", Money(costs.Total), ReportCostRowKind.Total),
        ];
    }

    // ---- The work lists, the images and the statement of truth -------------

    public const string NewPartsHeading = "Main New Parts Required";
    public const string RepairsHeading = "Repairs Required";
    public const string OperationsHeading = "Additional Operations";
    public const string VehicleImagesHeading = "Vehicle Images";

    /// <summary>The image pack's title: the template's images heading, as it prints a title.</summary>
    public const string ImagePackTitle = "VEHICLE IMAGES";

    public const string StatementOfTruthHeading = "Statement of Truth";
    public const string Valediction = "Yours faithfully,";
    public const string SignatoryRole = "Independent Automotive Engineer, " + CompanyName;

    /// <summary>The three work lists in the template's order. A list with no items is not printed.</summary>
    public static IReadOnlyList<ReportWorkList> WorkLists(AssessmentReportSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ReportWorkList[] lists =
        [
            new(NewPartsHeading, snapshot.NewParts),
            new(RepairsHeading, snapshot.Repairs),
            new(OperationsHeading, snapshot.Operations),
        ];
        return [.. lists.Where(list => list.Items.Count > 0)];
    }

    /// <summary>"A Patterson — M.Inst.IAEA", or the name alone when the account records no qualifications.</summary>
    public static string SignatoryLine(ReportSignatory signatory)
    {
        ArgumentNullException.ThrowIfNull(signatory);
        return string.IsNullOrWhiteSpace(signatory.Qualifications)
            ? signatory.PrintedName
            : $"{signatory.PrintedName} — {signatory.Qualifications}";
    }

    // ---- The fee note ------------------------------------------------------

    public const string FeeNoteTitle = "FEE NOTE";
    public const string BillToLabel = "Bill To:";
    public const string FeeDescriptionHeading = "Description";
    public const string FeeAmountHeading = "Amount (£)";
    public const string PaymentDetailsHeading = "Payment Details";
    public const string TermsHeading = "Terms";
    public const string ThankYou = "Thank you for your business.";

    /// <summary>The two paragraphs of terms, in print order.</summary>
    public static IReadOnlyList<string> Terms { get; } =
        [AssessmentReportContract.FeeTerms, AssessmentReportContract.AdditionalFeeTerms];

    /// <summary>
    /// The fee's own line, printed in bold above the recorded fee description
    /// lines (<see cref="AssessmentReportSnapshot.FeeDescriptionLines"/>). A
    /// Case that records no description prints this line alone.
    /// </summary>
    public static string FeeTitle(AssessmentReportSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return $"Vehicle Damage Assessment Report — {snapshot.Vehicle.Registration}";
    }

    /// <summary>The fee as the Amount (£) column prints it: a figure with no pound sign.</summary>
    public static string FeeAmount(AssessmentReportSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return Number(snapshot.FeeNet);
    }

    /// <summary>Subtotal (Net), VAT and TOTAL DUE. Only the total carries a pound sign.</summary>
    public static IReadOnlyList<ReportCostRow> FeeTotals(AssessmentReportSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var percent = (AssessmentReportContract.FeeVatRate * 100m).ToString("0.##", CultureInfo.InvariantCulture);
        return
        [
            new("Subtotal (Net)", Number(snapshot.FeeNet), ReportCostRowKind.Subtotal),
            new($"VAT @ {percent}%", Number(snapshot.FeeVat), ReportCostRowKind.Subtotal),
            new("TOTAL DUE", Money(snapshot.FeeTotal), ReportCostRowKind.Total),
        ];
    }

    /// <summary>The Payment Details grid's six cells, in reading order across it. The payment reference is Our Ref.</summary>
    public static IReadOnlyList<ReportRow> PaymentDetails(AssessmentReportSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return
        [
            new("Account Name", AssessmentReportContract.AccountName),
            new("Bank", AssessmentReportContract.BankName),
            new("Sort Code", AssessmentReportContract.SortCode),
            new("Account Number", AssessmentReportContract.AccountNumber),
            new("Payment Reference", snapshot.OurReference),
            new("Remittance Email", AssessmentReportContract.RemittanceEmail),
        ];
    }

    // ---- Who the report is for ---------------------------------------------

    /// <summary>
    /// The Report For block: the principal's name, then each line of its
    /// address, then its postcode unless the address already ends with it. A
    /// principal with no address prints its name alone (operator, 27 September
    /// 2026). A principal with no name has no addressee, and the report is
    /// refused for want of one.
    /// </summary>
    public static IReadOnlyList<string> ReportFor(string? name, string? address, string? postcode)
    {
        var lines = new List<string>();
        if (string.IsNullOrWhiteSpace(name))
        {
            return lines;
        }
        lines.Add(name.Trim());
        var addressLines = (address ?? string.Empty).Split(
            '\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        lines.AddRange(addressLines);
        var code = postcode?.Trim();
        if (!string.IsNullOrEmpty(code)
            && (addressLines.Length == 0
                || !Compact(addressLines[^1]).EndsWith(Compact(code), StringComparison.OrdinalIgnoreCase)))
        {
            lines.Add(code);
        }
        return lines;
    }

    /// <summary>
    /// The fee note's Bill To block, as the template sets the same address:
    /// the name on its own line, then the address two lines to a row, joined
    /// by a comma.
    /// </summary>
    public static IReadOnlyList<string> BillTo(IReadOnlyList<string> reportFor)
    {
        ArgumentNullException.ThrowIfNull(reportFor);
        var lines = new List<string>();
        if (reportFor.Count == 0)
        {
            return lines;
        }
        lines.Add(reportFor[0]);
        for (var index = 1; index < reportFor.Count; index += 2)
        {
            lines.Add(index + 1 < reportFor.Count
                ? $"{reportFor[index]}, {reportFor[index + 1]}"
                : reportFor[index]);
        }
        return lines;
    }

    // ---- How a value prints ------------------------------------------------

    /// <summary>"16/07/2026".</summary>
    public static string Date(DateOnly value) => value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    /// <summary>"£4,405.73".</summary>
    public static string Money(decimal value) => value.ToString("£#,##0.00", Gb);

    /// <summary>"4,405.73": money where the column's heading carries the pound sign.</summary>
    public static string Number(decimal value) => value.ToString("#,##0.00", Gb);

    /// <summary>"25.90".</summary>
    public static string Hours(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string Recorded(string? value) =>
        string.IsNullOrWhiteSpace(value) ? NotRecorded : value.Trim();

    private static string Compact(string value) =>
        string.Concat(value.Where(character => !char.IsWhiteSpace(character)));
}
