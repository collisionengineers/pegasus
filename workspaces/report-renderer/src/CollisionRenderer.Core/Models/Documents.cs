namespace CollisionRenderer.Core.Models;

// ---------------------------------------------------------------------------
// Shared building blocks. All string-typed so payloads stay easy to author by
// hand and tolerant of "62,000 miles" vs 62000; the renderer formats on output.
// ---------------------------------------------------------------------------

/// <summary>Letterhead reference block + date that appears on every CE document.</summary>
public sealed record DocumentMeta
{
    public string? OurRef { get; init; }
    public string? YourRef { get; init; }
    public string? Date { get; init; }
    public string? PreparedBy { get; init; }
}

public sealed record SubjectVehicle
{
    public string Registration { get; init; } = "";
    public string? Make { get; init; }
    public string? Model { get; init; }
    public string? Derivative { get; init; }
    public string? VehicleDescription { get; init; }
    public string? BodyType { get; init; }
    public string? Fuel { get; init; }
    public string? Transmission { get; init; }
    public string? Engine { get; init; }
    public string? FirstRegistered { get; init; }
    public string? Mileage { get; init; }
    public string? Colour { get; init; }
    public string? VehicleHistory { get; init; }
    public string? Vin { get; init; }
}

public sealed record Advert
{
    public string? AdvertId { get; init; }
    public string? Make { get; init; }
    public string? Model { get; init; }
    public string? DerivativeOrEngine { get; init; }
    public string? RegistrationYear { get; init; }
    public string? Mileage { get; init; }
    public string? Price { get; init; }
    public string? Source { get; init; }
    public string? Url { get; init; }
    public string? DateAccessed { get; init; }
    public string? Fuel { get; init; }
    public string? Transmission { get; init; }
    public string? BodyStyle { get; init; }
    public string? SellerType { get; init; }
    public string? Location { get; init; }
    public string? ComparabilityNote { get; init; }
    public string? DifferencesNote { get; init; }
    public string? EvidenceRole { get; init; }
    public string? ReportComment { get; init; }
    public string? ScreenshotPath { get; init; }
    public string? CapturedPdfPath { get; init; }
    public bool? SupportsAssessedValue { get; init; }
    public bool? IsMateriallyComparable { get; init; }
    public string? VatStatus { get; init; }
    public string? AdminFee { get; init; }
    public string? DeliveryFee { get; init; }
}

public sealed record SignatureBlock
{
    // Name, Role and Org are tri-state: omitted keeps the default, explicit JSON null
    // restores the firm default, and an explicit "" suppresses that line entirely
    // (e.g. the firm-only rebuttal sign-off "Yours faithfully, / Collision Engineers Ltd").
    public string Name { get; init; } = "";
    public string? Role { get; init; } = "Independent Automotive Engineer";
    public string? Org { get; init; } = "Collision Engineers Ltd";
    public string? Qualifications { get; init; }
    public string? AqpNumber { get; init; }

    /// <summary>Bundled signature key: andy_patterson | ed_mawdsley | neil_oreilly.</summary>
    public string? SignatureImage { get; init; }

    /// <summary>Optional external signature image path or data URI, used without rebuilding Core.</summary>
    public string? CustomSignaturePath { get; init; }
    public string? Closing { get; init; } = "Yours faithfully,";
}

// ---------------------------------------------------------------------------
// Template 1 — Market Valuation Evidence (port of report.html.j2)
// ---------------------------------------------------------------------------

public sealed record MarketValuationEvidenceDocument
{
    public DocumentMeta Meta { get; init; } = new();
    public string? ValuationMode { get; init; }
    public string? GuideValueUnavailableReason { get; init; }
    public SubjectVehicle Subject { get; init; } = new();
    public EvidenceAssessment? EvidenceAssessment { get; init; }
    public string? Intro { get; init; }
    public string? MarketResearch { get; init; }
    public string AssessedRetailValue { get; init; } = "";
    public string? GuideValue { get; init; }
    public List<Advert> Adverts { get; init; } = new();
    public List<string> ValuationCommentary { get; init; } = new();
    public string? Conclusion { get; init; }
    public string? VatNote { get; init; }
    public bool? IsCommercialVehicle { get; init; }
    public SignatureBlock? Signature { get; init; }
}

public sealed record EvidenceAssessment
{
    public bool SufficientForPdf { get; init; }
    public string? Basis { get; init; }
}

// ---------------------------------------------------------------------------
// Template 2 — Advert Evidence Pack (port of evidence_pack.html.j2)
// ---------------------------------------------------------------------------

public sealed record AdvertEvidencePackDocument
{
    public DocumentMeta Meta { get; init; } = new();
    public SubjectVehicle Subject { get; init; } = new();
    public string? Intro { get; init; }
    public List<Advert> Adverts { get; init; } = new();
    public string? SearchSummary { get; init; }
}

// ---------------------------------------------------------------------------
// Template 3 — Fee Note (VAT invoice)
// ---------------------------------------------------------------------------

public sealed record BillingParty
{
    public string Name { get; init; } = "";
    public List<string> AddressLines { get; init; } = new();
    public string? Reference { get; init; }
}

public sealed record FeeLineItem
{
    public string Description { get; init; } = "";
    public string? Detail { get; init; }
    public decimal Amount { get; init; }
}

public sealed record PaymentDetails
{
    public string? BankName { get; init; }
    public string? AccountName { get; init; }
    public string? SortCode { get; init; }
    public string? AccountNumber { get; init; }
    public string? Terms { get; init; } = "Payment due within 30 days of the date of this note.";
}

public sealed record FeeNoteDocument
{
    public DocumentMeta Meta { get; init; } = new();
    public string FeeNoteNumber { get; init; } = "";
    public BillingParty BillTo { get; init; } = new();
    public string? MatterReference { get; init; }
    public SubjectVehicle? Subject { get; init; }
    public List<FeeLineItem> Items { get; init; } = new();
    public decimal VatRate { get; init; } = 0.20m;
    public string VatNumber { get; init; } = "";
    public PaymentDetails Payment { get; init; } = new();
    public string? Notes { get; init; }
}

// ---------------------------------------------------------------------------
// Template 4 — Expert Report (flexible, block-based: Total Loss, Addendum,
// Diminution Rebuttal, Part 35 Response, Roadworthy, ... all share this shape).
// ---------------------------------------------------------------------------

public sealed record KeyValueRow
{
    public string Label { get; init; } = "";
    public string Value { get; init; } = "";
}

public sealed record EvidenceColumn
{
    public string Header { get; init; } = "";

    /// <summary>left | right | center — controls cell alignment (currency columns = right).</summary>
    public string Align { get; init; } = "left";

    /// <summary>Optional fixed width, e.g. "22mm". Empty = auto.</summary>
    public string? Width { get; init; }
}

public sealed record EvidenceTableBlock
{
    public List<EvidenceColumn> Columns { get; init; } = new();
    public List<List<string>> Rows { get; init; } = new();
}

public sealed record ValueBoxBlock
{
    public string Label { get; init; } = "";
    public string Value { get; init; } = "";
}

public sealed record MediaItem
{
    public string Caption { get; init; } = "";

    /// <summary>Optional embedded image (data URI or absolute path). Empty = placeholder slot.</summary>
    public string? ImagePath { get; init; }
    public string? Note { get; init; }
}

/// <summary>
/// One block of report body content. A flat, optional-field shape (rather than a
/// polymorphic union) so payloads are trivial to author and deserialize.
/// </summary>
public sealed record ContentBlock
{
    /// <summary>paragraph | bullets | datatable | keyvalue | evidencetable | valuebox | mediarow.</summary>
    public string Type { get; init; } = "paragraph";
    public string? Text { get; init; }
    public List<string>? Items { get; init; }
    public List<KeyValueRow>? Rows { get; init; }
    public EvidenceTableBlock? Table { get; init; }
    public ValueBoxBlock? Value { get; init; }
    public List<MediaItem>? Media { get; init; }
}

public sealed record ReportSection
{
    public string? Heading { get; init; }
    public List<ContentBlock> Blocks { get; init; } = new();
}

public sealed record ExpertReportDocument
{
    public DocumentMeta Meta { get; init; } = new();
    public string Title { get; init; } = "";
    public string? Subtitle { get; init; }
    public bool TitleRed { get; init; } = true;
    public bool TitleUnderlined { get; init; }
    public string? Salutation { get; init; }
    public string? ReLine { get; init; }
    public bool RedIntro { get; init; }
    public List<string> Intro { get; init; } = new();
    public List<ReportSection> Sections { get; init; } = new();
    public SignatureBlock? Signature { get; init; }
}
