using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Pegasus.Core.Assessment;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Reports;
using SkiaSharp;

namespace Pegasus.IntegrationTests.Reports;

/// <summary>
/// The figures the template's generator derives from a sample job
/// (DESIGN_SPEC.md, reliability rule 1), rounded half up to pence.
/// </summary>
internal sealed record TemplateSampleMoney(
    decimal LabourHours,
    decimal HourlyRate,
    decimal TotalLabour,
    decimal Parts,
    decimal PaintMaterials,
    decimal SpecialistOther,
    decimal SubTotal,
    decimal Vat,
    decimal Total);

/// <summary>One of the template's sample jobs, the report the template printed for it, and the job as a snapshot.</summary>
internal sealed record TemplateSample(
    string Name,
    string ReferencePdf,
    AssessmentReportSnapshot Snapshot,
    TemplateSampleMoney Money);

/// <summary>
/// The template's four sample jobs (<c>reference/rendererref1/sample_job_*.json</c>)
/// mapped onto report snapshots. The costs are built as a repair
/// specification and passed through <see cref="ReportRepairCosts.For"/>, as a
/// Case's are. The sample photographs are not in the repository, so the
/// photographs are drawn here: a Close-up for page 1, then an Overview and
/// five supporting images, which fill one image page as the samples do. The
/// signature is the template's own.
/// </summary>
internal static class TemplateSampleJobs
{
    internal static readonly (string Name, string Job, string ReferencePdf)[] All =
    [
        ("total-loss", "sample_job_PK12TMZ.json", "Sample - Total Loss Report.pdf"),
        ("repairable", "sample_job_repairable.json", "Sample - Repairable Report.pdf"),
        ("cash-in-lieu", "sample_job_cash_in_lieu.json", "Sample - Cash in Lieu Report.pdf"),
        ("contract-repair", "sample_job_contract_repair.json", "Sample - Contract Repair Report.pdf"),
    ];

    private static readonly DateTimeOffset RecordedAtUtc = new(2026, 7, 15, 9, 0, 0, TimeSpan.Zero);
    private static readonly CultureInfo Gb = CultureInfo.GetCultureInfo("en-GB");

    internal static TemplateSample Load(string referenceFolder, string name)
    {
        var sample = All.Single(item => item.Name == name);
        using var job = JsonDocument.Parse(File.ReadAllText(Path.Combine(referenceFolder, sample.Job)));
        var root = job.RootElement;
        var refs = root.GetProperty("refs");
        var vehicle = root.GetProperty("vehicle");
        var incident = root.GetProperty("incident");
        var assessment = root.GetProperty("assessment");
        var values = assessment.GetProperty("values");
        var worklists = root.GetProperty("worklists");
        var engineer = root.GetProperty("engineer");
        var fee = root.GetProperty("fee");

        var outcome = Text(assessment, "outcome") switch
        {
            "total_loss" => AssessmentReportOutcome.TotalLoss,
            "repairable" => AssessmentReportOutcome.Repairable,
            "cash_in_lieu" => AssessmentReportOutcome.CashInLieu,
            "contract_repair" => AssessmentReportOutcome.ContractRepair,
            var other => throw new InvalidOperationException($"The sample job has the outcome '{other}'."),
        };
        var money = Money(root.GetProperty("costs"));
        var costs = Costs(root.GetProperty("costs"));
        var location = Text(assessment, "impact_location");
        var signature = File.ReadAllBytes(Path.Combine(referenceFolder, Text(engineer, "signature") + ".png"));
        var category = Optional(assessment, "category");

        var snapshot = new AssessmentReportSnapshot(
            OurReference: Text(refs, "our_ref"),
            YourReference: Text(refs, "your_ref"),
            ReportDate: Date(refs, "date"),
            ClaimantName: Text(refs, "claimant_name"),
            IncidentDate: Date(refs, "incident_date"),
            InstructionsReceived: Date(incident, "instructions_received"),
            Assessed: Date(incident, "assessed"),
            ReportFor: Lines(refs, "report_for"),
            Vehicle: new ReportVehicle(
                Text(vehicle, "registration"),
                Text(vehicle, "make"),
                Text(vehicle, "model"),
                Text(vehicle, "year"),
                Text(vehicle, "condition"),
                vehicle.GetProperty("odometer_miles").GetInt32().ToString("N0", Gb) + " miles",
                Text(vehicle, "mileage_source"),
                Text(vehicle, "vin"),
                vehicle.GetProperty("engine_cc").GetInt32().ToString(CultureInfo.InvariantCulture),
                Text(vehicle, "fuel")),
            Outcome: outcome,
            LegalStatus: Text(assessment, "legal_status"),
            UnroadworthyReason: Optional(assessment, "unroadworthy_reason"),
            ImpactSeverity: Text(assessment, "impact_severity"),
            ImpactLocation: location,
            AssessmentMethod: Text(assessment, "method"),
            LocationAddress: null,
            EngineerValue: values.GetProperty("engineer").GetDecimal(),
            RetailValue: values.GetProperty("retail").GetDecimal(),
            TradeValue: values.GetProperty("trade").GetDecimal(),
            SalvageCategory: category,
            SalvageValue: assessment.TryGetProperty("salvage_value", out var salvage) ? salvage.GetDecimal() : null,
            Costs: costs,
            NewParts: Lines(worklists, "new_parts"),
            Repairs: Lines(worklists, "repairs"),
            Operations: Lines(worklists, "operations"),
            Damage: new ReportDamage([new ReportImpact([location])], null),
            Settlement: outcome == AssessmentReportOutcome.ContractRepair
                ? new ReportSettlement(costs.Total)
                : new ReportSettlement(),
            HistoryCheck: Text(root.GetProperty("narrative"), "history_check"),
            EngineerComments: null,
            Signatory: new ReportSignatory(
                Text(engineer, "name"), Optional(engineer, "qualifications"), signature, "image/png"),
            AgreedFee: fee.GetProperty("agreed_fee").GetDecimal(),
            FeeDescriptionLines: Lines(fee, "description_lines"),
            Photos: Photographs(),
            Sources: [new AcceptedReportSource(sample.Job, "1", new string('a', 64))],
            // The samples print the Glass's sentence in their statement of truth.
            Content: new CaseReportContentSwitches(
                DiscloseGuideSource: true, IncludeValuationCommentary: false, IncludeUnrelatedDamage: false),
            Guides: new ReportGuideSources([ValuationSource.Glasses]),
            // The samples end with their fee note.
            IncludeFeeNote: true);
        return new(sample.Name, Path.Combine(referenceFolder, sample.ReferencePdf), snapshot, money);
    }

    /// <summary>The sample's figures as the template's generator derives them.</summary>
    private static TemplateSampleMoney Money(JsonElement costs)
    {
        static decimal Pence(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
        var hours = costs.GetProperty("labour_hours").GetDecimal();
        var rate = costs.GetProperty("hourly_rate").GetDecimal();
        var parts = costs.GetProperty("parts").GetDecimal();
        var paint = costs.GetProperty("paint_materials").GetDecimal();
        var specialist = costs.GetProperty("specialist_other").GetDecimal();
        var labour = Pence(hours * rate);
        var subTotal = labour + parts + paint + specialist;
        var vat = Pence(subTotal * 0.20m);
        return new(hours, rate, labour, parts, paint, specialist, subTotal, vat, subTotal + vat);
    }

    /// <summary>The sample's costs as a Case holds them: a repair specification's lines and details.</summary>
    private static ReportRepairCosts Costs(JsonElement costs)
    {
        if (!costs.GetProperty("repairer_vat_registered").GetBoolean())
        {
            throw new InvalidOperationException("The sample jobs are all of a registered repairer.");
        }
        return ReportRepairCosts.For(new RepairSpecificationVersion(
            Guid.NewGuid(), Guid.NewGuid(), 1, RepairSpecificationState.Draft,
            new(RepairSpecificationSourceRoute.Manual, null, null, null),
            [
                Line(1, "repair", "Labour") with { WorkUnits = costs.GetProperty("labour_hours").GetDecimal() },
                Line(2, "paint_repair", "Paint / Materials")
                    with { Materials = costs.GetProperty("paint_materials").GetDecimal() },
                Line(3, "new_part", "Parts") with { Price = costs.GetProperty("parts").GetDecimal(), Quantity = 1 },
            ],
            "engineer-1", RecordedAtUtc,
            new EstimateDetails(
                "Repairer",
                costs.GetProperty("hourly_rate").GetDecimal(),
                costs.GetProperty("specialist_other").GetDecimal(),
                20m,
                Vat: EstimateVatPolicy.For(RepairerVatStatus.Registered)),
            IsCurrent: true));
    }

    private static CaseEstimateLineRecord Line(int position, string type, string description) => new(
        Guid.NewGuid(), position, type, null, description, null, null, false, null, null,
        "case", "Template sample job",
        ActorKind.Staff, "engineer-1", RecordedAtUtc);

    /// <summary>
    /// Seven drawn photographs in the sizes a telephone takes them: the
    /// Close-up, the Overview, and five supporting images in order.
    /// </summary>
    private static ReportImageEvidence[] Photographs()
    {
        (CaseAssetReportRole Role, int? Order, int Width, int Height)[] plan =
        [
            (CaseAssetReportRole.CloseUp, null, 1600, 1200),
            (CaseAssetReportRole.Overview, null, 1600, 1200),
            (CaseAssetReportRole.Supporting, 1, 1600, 900),
            (CaseAssetReportRole.Supporting, 2, 1200, 1600),
            (CaseAssetReportRole.Supporting, 3, 1600, 1200),
            (CaseAssetReportRole.Supporting, 4, 2000, 1125),
            (CaseAssetReportRole.Supporting, 5, 1600, 1200),
        ];
        return [.. plan.Select((photo, index) =>
        {
            var content = Photograph(index, photo.Width, photo.Height);
            return new ReportImageEvidence(
                $"sample-{index + 1}.jpg",
                "image/jpeg",
                content,
                Convert.ToHexStringLower(SHA256.HashData(content)),
                photo.Role,
                photo.Order);
        })];
    }

    /// <summary>
    /// A drawn photograph: a sky, a road, and a block the colour of its
    /// number, with a mark in each corner so a trimmed edge can be seen.
    /// </summary>
    private static byte[] Photograph(int index, int width, int height)
    {
        SKColor[] bodies =
        [
            new(0xb9, 0xb4, 0xa6), new(0x5b, 0x7d, 0x9c), new(0x9c, 0x5b, 0x5b), new(0x5b, 0x9c, 0x7a),
            new(0x8a, 0x6f, 0xa8), new(0xc2, 0x8e, 0x3c), new(0x4f, 0x5a, 0x63),
        ];
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque));
        using (var canvas = new SKCanvas(bitmap))
        using (var paint = new SKPaint { IsAntialias = true })
        {
            canvas.Clear(new SKColor(0xa9, 0xc4, 0xdc));
            paint.Color = new SKColor(0x6e, 0x6a, 0x66);
            canvas.DrawRect(new SKRect(0, height * 0.62f, width, height), paint);
            paint.Color = bodies[index % bodies.Length];
            canvas.DrawRoundRect(
                new SKRect(width * 0.18f, height * 0.3f, width * 0.82f, height * 0.74f), width * 0.04f, width * 0.04f, paint);
            paint.Color = new SKColor(0x22, 0x22, 0x22);
            canvas.DrawCircle(width * 0.32f, height * 0.74f, height * 0.09f, paint);
            canvas.DrawCircle(width * 0.68f, height * 0.74f, height * 0.09f, paint);
            paint.Color = new SKColor(0xc8, 0x0a, 0x32);
            foreach (var (x, y) in new[] { (0f, 0f), (1f, 0f), (0f, 1f), (1f, 1f) })
            {
                canvas.DrawCircle(x * width, y * height, height * 0.06f, paint);
            }
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        return encoded.ToArray();
    }

    private static string Text(JsonElement element, string name) =>
        element.GetProperty(name).GetString()
        ?? throw new InvalidOperationException($"The sample job holds no '{name}'.");

    private static string? Optional(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()
            : null;

    private static DateOnly Date(JsonElement element, string name) =>
        DateOnly.ParseExact(Text(element, name), "dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static string[] Lines(JsonElement element, string name) =>
        [.. element.GetProperty(name).EnumerateArray().Select(line => line.GetString()!)];
}
