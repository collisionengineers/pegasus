using System.Text.Json;
using CollisionRenderer.Core.Models;

namespace CollisionRenderer.Core;

public interface IAuthoringTemplateCatalog
{
    IReadOnlyList<AuthoringTemplateDescriptor> List();
    AuthoringTemplateDescriptor Get(string id);
    bool TryGet(string id, out AuthoringTemplateDescriptor? descriptor);
    DocumentFormDefinition GetForm(string id);
    string GetBlankJson(string id);

    /// <summary>A starter draft: the blank pre-filled with placeholder prompts and
    /// example text for the user to overwrite.</summary>
    string GetStarterJson(string id);
}

public enum FormFieldKind
{
    Text,
    MultilineText,
    Date,
    Money,
    Number,
    Select,
    Checkbox,
    Table,
    Repeater,
    QuestionAnswer,
    SignatureSelect,
    ImageUpload,
    PdfUpload,
}

public sealed record FormOption
{
    public required string Value { get; init; }
    public required string Label { get; init; }
}

public sealed record AttachmentPolicy
{
    public bool AllowsImages { get; init; }
    public bool AllowsPdfs { get; init; }
    public int MaxAttachmentBytes { get; init; } = 15_000_000;
    public IReadOnlyList<string> ContentTypes { get; init; } = Array.Empty<string>();
}

public sealed record AuthoringTemplateDescriptor
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string RenderTemplateId { get; init; }
    public required string Category { get; init; }
    public required string ReferenceFamily { get; init; }
    public AttachmentPolicy AttachmentPolicy { get; init; } = new();
}

public sealed record DocumentFormDefinition
{
    public required string TemplateId { get; init; }
    public required string RenderTemplateId { get; init; }
    public required IReadOnlyList<DocumentFormSection> Sections { get; init; }
}

public sealed record DocumentFormSection
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required IReadOnlyList<DocumentFormField> Fields { get; init; }
}

public sealed record DocumentFormField
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public required FormFieldKind Kind { get; init; }
    public required string Path { get; init; }
    public bool Required { get; init; }
    public string? Placeholder { get; init; }
    public IReadOnlyList<FormOption> Options { get; init; } = Array.Empty<FormOption>();
    public IReadOnlyList<DocumentFormField> Fields { get; init; } = Array.Empty<DocumentFormField>();
}

public sealed record DocumentDraft
{
    public required string AuthoringTemplateId { get; init; }
    public required string RenderTemplateId { get; init; }
    public required string Json { get; init; }
    public IReadOnlyList<AttachmentDescriptor> Attachments { get; init; } = Array.Empty<AttachmentDescriptor>();
}

public sealed record AttachmentDescriptor
{
    public required string Id { get; init; }
    public required string Path { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public string? TargetPath { get; init; }
    public string? Caption { get; init; }
    public string? Note { get; init; }
}

public sealed class AuthoringTemplateCatalog : IAuthoringTemplateCatalog
{
    public static readonly AuthoringTemplateCatalog Default = new(TemplateCatalog.Default);

    private readonly Dictionary<string, Entry> _byId;
    private readonly ITemplateCatalog _renderCatalog;

    public AuthoringTemplateCatalog(ITemplateCatalog renderCatalog)
    {
        _renderCatalog = renderCatalog;
        var entries = BuildEntries();
        _byId = entries.ToDictionary(e => e.Descriptor.Id, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<AuthoringTemplateDescriptor> List() => _byId.Values
        .Select(e => e.Descriptor)
        .OrderBy(d => d.Category, StringComparer.Ordinal)
        .ThenBy(d => d.Name, StringComparer.Ordinal)
        .ToList();

    public AuthoringTemplateDescriptor Get(string id) =>
        _byId.TryGetValue(id, out var entry)
            ? entry.Descriptor
            : throw new KeyNotFoundException(
                $"Unknown authoring template '{id}'. Known: {string.Join(", ", _byId.Keys)}");

    public bool TryGet(string id, out AuthoringTemplateDescriptor? descriptor)
    {
        var found = _byId.TryGetValue(id, out var entry);
        descriptor = entry?.Descriptor;
        return found;
    }

    public DocumentFormDefinition GetForm(string id) => EntryFor(id).Form;

    public string GetBlankJson(string id) => EntryFor(id).BlankJson();

    public string GetStarterJson(string id)
    {
        var entry = EntryFor(id);
        return StarterComposer.Wash(entry.BlankJson(), entry.Form);
    }

    private Entry EntryFor(string id) =>
        _byId.TryGetValue(id, out var entry)
            ? entry
            : throw new KeyNotFoundException(
                $"Unknown authoring template '{id}'. Known: {string.Join(", ", _byId.Keys)}");

    private IReadOnlyList<Entry> BuildEntries()
    {
        var entries = new List<Entry>
        {
            Existing("market-valuation-evidence", "Market Valuation Evidence",
                "Retail pre-accident value evidenced by comparable adverts.",
                "Valuation", "Market valuation evidence", ValuationForm, MarketValuationBlank),

            Existing("advert-evidence-pack", "Advert Evidence Pack",
                "Comparable advert references and optional captured advert evidence.",
                "Valuation", "Advert evidence pack", EvidencePackForm, EvidencePackBlank,
                new AttachmentPolicy
                {
                    AllowsImages = true,
                    AllowsPdfs = true,
                    ContentTypes = new[] { "application/pdf", "image/png", "image/jpeg", "image/webp" },
                }),

            Existing("fee-note", "Fee Note",
                "VAT fee note / invoice for completed engineering work.",
                "Fee Notes", "Fee note", FeeNoteForm, FeeNoteBlank),

            Existing("expert-report", "Custom Expert Report",
                "Flexible letter-style report assembled from typed content blocks.",
                "Reports", "Expert report", ExpertReportForm, ExpertReportBlank),

            Existing("blank-letterhead", "Blank Letterhead",
                "A minimal Collision Engineers letterhead with a free-text body — the generic branded document.",
                "General", "Blank letterhead", BlankLetterheadForm, BlankLetterheadBlank),
        };

        entries.Add(ExpertPreset("repairable-contract-repair-report", "Repairable / Contract Repair Report",
            "Independent accident damage report for a repairable outcome.",
            "Repairable / Contract Repair Report", RepairableReportBlank, RepairableReportForm, ImagePolicy()));

        entries.Add(ExpertPreset("total-loss-report", "Total Loss Report",
            "Damage report where the vehicle is a write-off and settlement is engineer value less salvage.",
            "Total Loss Report", TotalLossBlank, TotalLossForm, ImagePolicy()));

        entries.Add(ExpertPreset("addendum-report", "Addendum Report",
            "Further commentary defending or clarifying a prior report.",
            "Addendum Report", AddendumBlank, AddendumForm, ImagePolicy(optional: true)));

        entries.Add(ExpertPreset("diminution-rebuttal", "Diminution Rebuttal",
            "Letter-style rebuttal of a third-party diminution-in-value claim.",
            "Diminution Rebuttal", DiminutionBlank, DiminutionForm));

        entries.Add(ExpertPreset("roadworthy-criminal-report", "Roadworthy / Criminal Report",
            "Safety, compliance or criminal-matter report with defect findings.",
            "Roadworthy / Criminal Report", RoadworthyBlank, RoadworthyForm, ImagePolicy()));

        entries.Add(ExpertPreset("part-35-response", "Part 35 Responses",
            "Written answers to a Schedule of Questions to the Engineer.",
            "Part 35 Responses", Part35Blank, Part35Form));

        entries.Add(ExpertPreset("response-letter", "Response Letter",
            "Letter-style dispute or correspondence response.",
            "Response Letter", ResponseLetterBlank, ResponseLetterForm));

        foreach (var entry in entries)
        {
            _renderCatalog.Get(entry.Descriptor.RenderTemplateId);
        }

        return entries;
    }

    private static Entry Existing(
        string id,
        string name,
        string description,
        string category,
        string referenceFamily,
        Func<string, DocumentFormDefinition> form,
        Func<string> blank,
        AttachmentPolicy? attachmentPolicy = null)
    {
        var descriptor = new AuthoringTemplateDescriptor
        {
            Id = id,
            Name = name,
            Description = description,
            RenderTemplateId = id,
            Category = category,
            ReferenceFamily = referenceFamily,
            AttachmentPolicy = attachmentPolicy ?? new AttachmentPolicy(),
        };

        return new Entry(descriptor, form(id), blank);
    }

    // A "Reports"-category expert-report preset. Same as Existing(); the blank/form
    // parameters are swapped purely for call-site readability, then delegated.
    private static Entry ExpertPreset(
        string id,
        string name,
        string description,
        string referenceFamily,
        Func<string> blank,
        Func<string, DocumentFormDefinition> form,
        AttachmentPolicy? attachmentPolicy = null)
        => Existing(id, name, description, "Reports", referenceFamily, form, blank, attachmentPolicy);

    private static AttachmentPolicy ImagePolicy(bool optional = false) => new()
    {
        AllowsImages = true,
        ContentTypes = new[] { "image/png", "image/jpeg", "image/webp" },
        MaxAttachmentBytes = optional ? 10_000_000 : 15_000_000,
    };

    // ---------------------------------------------------------------- Forms

    private static DocumentFormDefinition ValuationForm(string id) => Form(id, "market-valuation-evidence",
        Section("meta", "Reference", MetaFields()),
        Section("subject", "Subject vehicle", SubjectFields("subject")),
        Section("valuation", "Valuation", new[]
        {
            Field("valuation-mode", "Valuation mode", FormFieldKind.Select, "valuationMode",
                options: Options(("guide_supported", "Guide supported"), ("market_only", "Market only"))),
            Field("guide-value", "Guide value", FormFieldKind.Money, "guideValue"),
            Field("guide-unavailable", "Guide value unavailable reason", FormFieldKind.MultilineText, "guideValueUnavailableReason"),
            Field("assessed-retail-value", "Assessed retail value", FormFieldKind.Money, "assessedRetailValue", required: true),
            Field("evidence-basis", "Evidence assessment basis", FormFieldKind.MultilineText, "evidenceAssessment.basis"),
            Field("sufficient-for-pdf", "Evidence sufficient for PDF", FormFieldKind.Checkbox, "evidenceAssessment.sufficientForPdf"),
            Field("is-commercial", "Commercial vehicle", FormFieldKind.Checkbox, "isCommercialVehicle"),
        }),
        Section("narrative", "Narrative", new[]
        {
            Field("intro", "Intro", FormFieldKind.MultilineText, "intro"),
            Field("market-research", "Market research", FormFieldKind.MultilineText, "marketResearch", required: true),
            Field("valuation-commentary", "Valuation commentary", FormFieldKind.Repeater, "valuationCommentary",
                fields: new[] { Field("paragraph", "Paragraph", FormFieldKind.MultilineText, "$") }),
            Field("conclusion", "Conclusion", FormFieldKind.MultilineText, "conclusion", required: true),
            Field("vat-note", "VAT note", FormFieldKind.MultilineText, "vatNote"),
        }),
        Section("adverts", "Comparable adverts", new[]
        {
            AdvertRepeater("adverts"),
        }),
        Section("signature", "Signature", SignatureFields("signature")));

    private static DocumentFormDefinition EvidencePackForm(string id) => Form(id, "advert-evidence-pack",
        Section("meta", "Reference", MetaFields()),
        Section("subject", "Subject vehicle", SubjectFields("subject")),
        Section("summary", "Evidence pack", new[]
        {
            Field("intro", "Intro", FormFieldKind.MultilineText, "intro"),
            Field("search-summary", "Search summary", FormFieldKind.MultilineText, "searchSummary"),
        }),
        Section("adverts", "Advert references and captures", new[]
        {
            AdvertRepeater("adverts", includeCaptureFields: true),
        }));

    private static DocumentFormDefinition FeeNoteForm(string id) => Form(id, "fee-note",
        Section("meta", "Reference", MetaFields()),
        Section("invoice", "Invoice", new[]
        {
            Field("fee-note-number", "Fee note number", FormFieldKind.Text, "feeNoteNumber", required: true),
            Field("matter-reference", "Matter reference", FormFieldKind.Text, "matterReference"),
            Field("vat-rate", "VAT rate", FormFieldKind.Number, "vatRate"),
            Field("vat-number", "VAT number", FormFieldKind.Text, "vatNumber"),
        }),
        Section("bill-to", "Bill to", new[]
        {
            Field("name", "Name", FormFieldKind.Text, "billTo.name", required: true),
            Field("address-lines", "Address lines", FormFieldKind.Repeater, "billTo.addressLines",
                fields: new[] { Field("line", "Line", FormFieldKind.Text, "$") }),
            Field("reference", "Reference", FormFieldKind.Text, "billTo.reference"),
        }),
        Section("subject", "Subject vehicle", SubjectFields("subject")),
        Section("items", "Line items", new[]
        {
            Field("items", "Items", FormFieldKind.Repeater, "items", required: true, fields: new[]
            {
                Field("description", "Description", FormFieldKind.Text, "description", required: true),
                Field("detail", "Detail", FormFieldKind.MultilineText, "detail"),
                Field("amount", "Amount", FormFieldKind.Money, "amount", required: true),
            }),
        }),
        Section("payment", "Payment", new[]
        {
            Field("bank-name", "Bank name", FormFieldKind.Text, "payment.bankName"),
            Field("account-name", "Account name", FormFieldKind.Text, "payment.accountName"),
            Field("sort-code", "Sort code", FormFieldKind.Text, "payment.sortCode"),
            Field("account-number", "Account number", FormFieldKind.Text, "payment.accountNumber"),
            Field("terms", "Terms", FormFieldKind.MultilineText, "payment.terms"),
            Field("notes", "Notes", FormFieldKind.MultilineText, "notes"),
        }));

    private static DocumentFormDefinition BlankLetterheadForm(string id) => Form(id, "blank-letterhead",
        Section("meta", "Reference", MetaFields()),
        Section("heading", "Heading", new[]
        {
            Field("title", "Title", FormFieldKind.Text, "title"),
            Field("subtitle", "Subtitle", FormFieldKind.Text, "subtitle"),
            Field("salutation", "Salutation / FAO", FormFieldKind.Text, "salutation"),
            Field("re-line", "Matter / RE line", FormFieldKind.Text, "reLine"),
        }, "An optional heading and addressee. Leave the title blank for a plain letterhead."),
        Section("body", "Body", new[]
        {
            Field("body", "Body paragraphs", FormFieldKind.Repeater, "intro",
                fields: new[] { Field("paragraph", "Paragraph", FormFieldKind.MultilineText, "$") }),
        }),
        Section("signature", "Signature", SignatureFields("signature")));

    private static DocumentFormDefinition ExpertReportForm(string id) => Form(id, "expert-report",
        Section("meta", "Reference", MetaFields()),
        Section("title", "Title", ReportTitleFields()),
        Section("body", "Body", new[]
        {
            Field("intro", "Intro paragraphs", FormFieldKind.Repeater, "intro",
                fields: new[] { Field("paragraph", "Paragraph", FormFieldKind.MultilineText, "$") }),
            Field("sections", "Sections", FormFieldKind.Repeater, "sections", required: true, fields: new[]
            {
                Field("heading", "Heading", FormFieldKind.Text, "heading"),
                Field("blocks", "Blocks", FormFieldKind.Repeater, "blocks"),
            }),
        }),
        Section("signature", "Signature", SignatureFields("signature")));

    private static DocumentFormDefinition RepairableReportForm(string id) => ReportWithVehicleAndImagesForm(id, "Repair position",
        "sections[4].blocks[0].text");

    private static DocumentFormDefinition TotalLossForm(string id) => ReportWithVehicleAndImagesForm(id, "Settlement",
        "sections[4].blocks[0].text");

    private static DocumentFormDefinition RoadworthyForm(string id) => ReportWithVehicleAndImagesForm(id, "Roadworthiness conclusion",
        "sections[4].blocks[0].text");

    private static DocumentFormDefinition AddendumForm(string id) => Form(id, id,
        Section("meta", "Reference", MetaFields()),
        Section("title", "Title", ReportTitleFields()),
        Section("addendum", "Addendum", new[]
        {
            Field("original-reference", "Original report reference/date", FormFieldKind.Text, "sections[0].blocks[0].rows[0].value"),
            Field("instruction-summary", "Instruction or challenge summary", FormFieldKind.MultilineText, "intro[0]"),
            Field("commentary", "Commentary sections", FormFieldKind.Repeater, "sections",
                fields: SectionRepeaterFields()),
            Field("supporting-image", "Supporting image", FormFieldKind.ImageUpload, "sections[1].blocks[0].media[0].imagePath"),
        }),
        Section("signature", "Signature", SignatureFields("signature")));

    private static DocumentFormDefinition DiminutionForm(string id) => Form(id, id,
        Section("meta", "Reference", MetaFields()),
        Section("title", "Title", ReportTitleFields()),
        Section("rebuttal", "Rebuttal text", new[]
        {
            Field("opposing-summary", "Opposing assessment summary", FormFieldKind.MultilineText, "intro[0]"),
            Field("damage-repairs", "Damage and repairs argument", FormFieldKind.MultilineText, "sections[0].blocks[0].text"),
            Field("inspection", "Inspection argument", FormFieldKind.MultilineText, "sections[1].blocks[0].text"),
            Field("methodology", "Methodology argument", FormFieldKind.MultilineText, "sections[2].blocks[0].text"),
            Field("additional-sections", "Additional sections", FormFieldKind.Repeater, "sections",
                fields: SectionRepeaterFields()),
        }),
        Section("signature", "Signature", SignatureFields("signature")));

    private static DocumentFormDefinition Part35Form(string id) => Form(id, id,
        Section("meta", "Reference", MetaFields()),
        Section("title", "Title", ReportTitleFields()),
        Section("matter", "Matter", new[]
        {
            Field("original-report", "Original report reference/date", FormFieldKind.Text, "sections[0].blocks[0].rows[0].value"),
            Field("schedule-date", "Schedule date", FormFieldKind.Date, "sections[0].blocks[0].rows[1].value"),
            Field("documents-reviewed", "Documents reviewed", FormFieldKind.Repeater, "sections[0].blocks[1].items",
                fields: new[] { Field("item", "Item", FormFieldKind.Text, "$") }),
            Field("questions", "Questions and replies", FormFieldKind.QuestionAnswer, "sections[1].blocks[0].table.rows", required: true),
            Field("closing", "Closing statement", FormFieldKind.MultilineText, "sections[2].blocks[0].text"),
        }),
        Section("signature", "Signature", SignatureFields("signature")));

    private static DocumentFormDefinition ResponseLetterForm(string id) => Form(id, id,
        Section("meta", "Reference", MetaFields()),
        Section("letter", "Letter", new[]
        {
            Field("without-prejudice", "Without-prejudice line", FormFieldKind.Text, "subtitle"),
            Field("addressee", "Recipient / FAO", FormFieldKind.Text, "salutation"),
            Field("re-line", "Matter / RE line", FormFieldKind.Text, "reLine"),
            Field("opening", "Opening paragraph", FormFieldKind.MultilineText, "intro[0]"),
            Field("body", "Body paragraphs", FormFieldKind.Repeater, "sections[0].blocks",
                fields: new[] { Field("paragraph", "Paragraph", FormFieldKind.MultilineText, "text") }),
            Field("independence", "Independence line", FormFieldKind.MultilineText, "sections[1].blocks[0].text"),
        }),
        Section("signature", "Signature", SignatureFields("signature")));

    private static DocumentFormDefinition ReportWithVehicleAndImagesForm(string id, string finalLabel, string finalPath) => Form(id, id,
        Section("meta", "Reference", MetaFields()),
        Section("title", "Title", ReportTitleFields()),
        Section("summary", "Vehicle summary", new[]
        {
            Field("summary-table", "Summary table", FormFieldKind.Table, "sections[0].blocks[0].rows"),
        }),
        Section("images", "Images", new[]
        {
            Field("vehicle-image", "Vehicle image", FormFieldKind.ImageUpload, "sections[1].blocks[0].media[0].imagePath"),
            Field("impact-image", "Impact-area image", FormFieldKind.ImageUpload, "sections[1].blocks[0].media[1].imagePath"),
            Field("damage-images", "Additional damage images", FormFieldKind.Repeater, "sections[1].blocks[0].media",
                fields: new[]
                {
                    Field("caption", "Caption", FormFieldKind.Text, "caption"),
                    Field("image", "Image", FormFieldKind.ImageUpload, "imagePath"),
                    Field("note", "Note", FormFieldKind.Text, "note"),
                }),
        }),
        Section("narrative", "Narrative", new[]
        {
            Field("instruction", "Instruction paragraph", FormFieldKind.MultilineText, "intro[0]"),
            Field("incident", "Nature of incident", FormFieldKind.MultilineText, "sections[2].blocks[0].text"),
            Field("comments", "Engineer's comments", FormFieldKind.MultilineText, "sections[3].blocks[0].text"),
            Field("final", finalLabel, FormFieldKind.MultilineText, finalPath),
        }),
        Section("signature", "Signature", SignatureFields("signature")));

    // ---------------------------------------------------------------- Blanks

    private static string MarketValuationBlank() => Json(new MarketValuationEvidenceDocument
    {
        Meta = new DocumentMeta(),
        ValuationMode = "guide_supported",
        Subject = new SubjectVehicle(),
        EvidenceAssessment = new EvidenceAssessment(),
        Adverts = { new Advert() },
        ValuationCommentary = { "" },
        Signature = new SignatureBlock(),
    });

    private static string EvidencePackBlank() => Json(new AdvertEvidencePackDocument
    {
        Meta = new DocumentMeta(),
        Subject = new SubjectVehicle(),
        Adverts = { new Advert() },
    });

    private static string FeeNoteBlank() => Json(new FeeNoteDocument
    {
        Meta = new DocumentMeta(),
        BillTo = new BillingParty { AddressLines = { "" } },
        Subject = new SubjectVehicle(),
        Items = { new FeeLineItem() },
        Payment = new PaymentDetails(),
    });

    private static string ExpertReportBlank() => Json(new ExpertReportDocument
    {
        Meta = new DocumentMeta(),
        Title = "",
        Intro = { "" },
        Sections = { new ReportSection { Blocks = { new ContentBlock() } } },
        Signature = new SignatureBlock(),
    });

    private static string BlankLetterheadBlank() => Json(new ExpertReportDocument
    {
        Meta = new DocumentMeta(),
        Title = "",
        TitleRed = false,
        Intro = { "" },
        Signature = new SignatureBlock(),
    });

    private static string RepairableReportBlank() => Json(ReportBlank("Repairable / Contract Repair Report", "Repair position", RepairableRows()));

    private static string TotalLossBlank() => Json(ReportBlank("Total Loss Report", "Settlement", TotalLossRows()));

    private static string RoadworthyBlank() => Json(ReportBlank("Roadworthy / Criminal Report", "Roadworthiness Conclusion", RoadworthyRows()));

    private static string AddendumBlank() => Json(new ExpertReportDocument
    {
        Meta = new DocumentMeta(),
        Title = "Addendum Report",
        TitleRed = false,
        TitleUnderlined = true,
        Salutation = "Dear Sirs,",
        Intro = { "" },
        Sections =
        {
            new ReportSection
            {
                Heading = "Original Report",
                Blocks = { DataTable(("Original report reference/date", "")) },
            },
            new ReportSection
            {
                Heading = "Supporting Material",
                Blocks = { MediaRow(("Supporting image", "", "")) },
            },
            new ReportSection { Heading = "Addendum", Blocks = { Paragraph("") } },
            new ReportSection { Heading = "Conclusion", Blocks = { Paragraph("") } },
        },
        Signature = new SignatureBlock(),
    });

    private static string DiminutionBlank() => Json(new ExpertReportDocument
    {
        Meta = new DocumentMeta(),
        Title = "Rebuttal of Claim for Diminution in Value",
        TitleRed = false,
        TitleUnderlined = true,
        Salutation = "FAO: The Instructing Solicitor",
        Intro = { "" },
        Sections =
        {
            new ReportSection { Heading = "The Damage and Repairs Were Superficial", Blocks = { Paragraph("") } },
            new ReportSection { Heading = "No Physical Inspection of the Vehicle", Blocks = { Paragraph("") } },
            new ReportSection { Heading = "The Stigma Scale Is Not a Recognised Methodology", Blocks = { Paragraph("") } },
            new ReportSection { Heading = "Conclusion", Blocks = { Paragraph("") } },
        },
        Signature = new SignatureBlock(),
    });

    private static string Part35Blank() => Json(new ExpertReportDocument
    {
        Meta = new DocumentMeta(),
        Title = "Responses to Part 35 Questions",
        TitleRed = false,
        TitleUnderlined = true,
        Salutation = "Dear Sirs,",
        Sections =
        {
            new ReportSection
            {
                Heading = "Matter",
                Blocks =
                {
                    DataTable(("Original report reference/date", ""), ("Schedule date", "")),
                    new ContentBlock { Type = "bullets", Items = new List<string> { "" } },
                },
            },
            new ReportSection
            {
                Heading = "Questions and Responses",
                Blocks =
                {
                    new ContentBlock
                    {
                        Type = "evidencetable",
                        Table = new EvidenceTableBlock
                        {
                            Columns =
                            {
                                new EvidenceColumn { Header = "Question", Width = "45%" },
                                new EvidenceColumn { Header = "Response" },
                            },
                            Rows = { new List<string> { "", "" } },
                        },
                    },
                },
            },
            new ReportSection { Heading = "Closing Statement", Blocks = { Paragraph("") } },
        },
        Signature = new SignatureBlock(),
    });

    private static string ResponseLetterBlank() => Json(new ExpertReportDocument
    {
        Meta = new DocumentMeta(),
        Title = "Response Letter",
        Subtitle = "",
        TitleRed = false,
        Salutation = "",
        ReLine = "",
        Intro = { "" },
        Sections =
        {
            new ReportSection { Blocks = { Paragraph("") } },
            new ReportSection { Blocks = { Paragraph("") } },
        },
        Signature = new SignatureBlock(),
    });

    private static ExpertReportDocument ReportBlank(string title, string finalSection, List<KeyValueRow> rows) => new()
    {
        Meta = new DocumentMeta(),
        Title = title,
        TitleRed = false,
        TitleUnderlined = true,
        Salutation = "Dear Sirs,",
        Intro = { "" },
        Sections =
        {
            new ReportSection { Heading = "Vehicle Summary", Blocks = { new ContentBlock { Type = "datatable", Rows = rows } } },
            new ReportSection { Heading = "Images", Blocks = { MediaRow(("Vehicle", "", ""), ("Impact Area", "", "")) } },
            new ReportSection { Heading = "Nature of Incident", Blocks = { Paragraph("") } },
            new ReportSection { Heading = "Engineer's Comments", Blocks = { Paragraph("") } },
            new ReportSection { Heading = finalSection, Blocks = { Paragraph("") } },
        },
        Signature = new SignatureBlock(),
    };

    // -------------------------------------------------------------- Shared bits

    private static IReadOnlyList<DocumentFormField> MetaFields() => new[]
    {
        Field("our-ref", "Our Ref", FormFieldKind.Text, "meta.ourRef"),
        Field("your-ref", "Your Ref", FormFieldKind.Text, "meta.yourRef"),
        Field("date", "Date", FormFieldKind.Date, "meta.date"),
        Field("prepared-by", "Prepared by", FormFieldKind.Text, "meta.preparedBy"),
    };

    private static IReadOnlyList<DocumentFormField> SubjectFields(string prefix) => new[]
    {
        Field("registration", "Registration", FormFieldKind.Text, $"{prefix}.registration", required: true),
        Field("make", "Make", FormFieldKind.Text, $"{prefix}.make"),
        Field("model", "Model", FormFieldKind.Text, $"{prefix}.model"),
        Field("derivative", "Derivative", FormFieldKind.Text, $"{prefix}.derivative"),
        Field("body-type", "Body type", FormFieldKind.Text, $"{prefix}.bodyType"),
        Field("fuel", "Fuel", FormFieldKind.Text, $"{prefix}.fuel"),
        Field("transmission", "Transmission", FormFieldKind.Text, $"{prefix}.transmission"),
        Field("engine", "Engine", FormFieldKind.Text, $"{prefix}.engine"),
        Field("first-registered", "First registered", FormFieldKind.Text, $"{prefix}.firstRegistered"),
        Field("mileage", "Mileage", FormFieldKind.Text, $"{prefix}.mileage"),
        Field("colour", "Colour", FormFieldKind.Text, $"{prefix}.colour"),
        Field("vehicle-history", "Vehicle history", FormFieldKind.Text, $"{prefix}.vehicleHistory"),
        Field("vin", "VIN", FormFieldKind.Text, $"{prefix}.vin"),
    };

    private static IReadOnlyList<DocumentFormField> ReportTitleFields() => new[]
    {
        Field("title", "Title", FormFieldKind.Text, "title", required: true),
        Field("subtitle", "Subtitle", FormFieldKind.Text, "subtitle"),
        Field("title-red", "Red title", FormFieldKind.Checkbox, "titleRed"),
        Field("title-underlined", "Underlined title", FormFieldKind.Checkbox, "titleUnderlined"),
        Field("salutation", "Salutation / FAO", FormFieldKind.Text, "salutation"),
        Field("re-line", "Matter / RE line", FormFieldKind.Text, "reLine"),
        Field("red-intro", "Red intro", FormFieldKind.Checkbox, "redIntro"),
    };

    private static IReadOnlyList<DocumentFormField> SignatureFields(string prefix) => new[]
    {
        Field("signature", "Engineer", FormFieldKind.SignatureSelect, $"{prefix}.signatureImage",
            options: Options(("andy_patterson", "A. Patterson"), ("ed_mawdsley", "E. Mawdsley"), ("neil_oreilly", "N. D. O'Reilly"))),
        Field("custom-signature", "Custom signature image", FormFieldKind.ImageUpload, $"{prefix}.customSignaturePath"),
        Field("name", "Typed name", FormFieldKind.Text, $"{prefix}.name"),
        Field("qualifications", "Qualifications", FormFieldKind.Text, $"{prefix}.qualifications"),
        Field("aqp", "AQP number", FormFieldKind.Text, $"{prefix}.aqpNumber"),
        Field("closing", "Closing", FormFieldKind.Text, $"{prefix}.closing"),
    };

    private static DocumentFormField AdvertRepeater(string path, bool includeCaptureFields = false)
    {
        var fields = new List<DocumentFormField>
        {
            Field("source", "Source", FormFieldKind.Text, "source"),
            Field("url", "URL", FormFieldKind.Text, "url", required: true),
            Field("advert-id", "Advert ID", FormFieldKind.Text, "advertId"),
            Field("date-accessed", "Date accessed", FormFieldKind.Date, "dateAccessed"),
            Field("price", "Price", FormFieldKind.Money, "price", required: true),
            Field("make", "Make", FormFieldKind.Text, "make"),
            Field("model", "Model", FormFieldKind.Text, "model"),
            Field("derivative", "Derivative / engine", FormFieldKind.Text, "derivativeOrEngine"),
            Field("year", "Registration year", FormFieldKind.Text, "registrationYear"),
            Field("mileage", "Mileage", FormFieldKind.Text, "mileage"),
            Field("fuel", "Fuel", FormFieldKind.Text, "fuel"),
            Field("transmission", "Transmission", FormFieldKind.Text, "transmission"),
            Field("body-style", "Body style", FormFieldKind.Text, "bodyStyle"),
            Field("seller-type", "Seller type", FormFieldKind.Text, "sellerType"),
            Field("location", "Location", FormFieldKind.Text, "location"),
            Field("comparability", "Comparability note", FormFieldKind.MultilineText, "comparabilityNote"),
            Field("differences", "Differences note", FormFieldKind.MultilineText, "differencesNote"),
            Field("report-comment", "Report comment", FormFieldKind.MultilineText, "reportComment"),
            Field("evidence-role", "Evidence role", FormFieldKind.Select, "evidenceRole",
                options: Options(("supportive", "Supportive"), ("limiting", "Limiting"), ("contextual", "Contextual"), ("excluded", "Excluded"))),
            Field("supports-value", "Supports assessed value", FormFieldKind.Checkbox, "supportsAssessedValue"),
            Field("materially-comparable", "Materially comparable", FormFieldKind.Checkbox, "isMateriallyComparable"),
            Field("vat-status", "VAT status", FormFieldKind.Text, "vatStatus"),
            Field("admin-fee", "Admin fee", FormFieldKind.Money, "adminFee"),
            Field("delivery-fee", "Delivery fee", FormFieldKind.Money, "deliveryFee"),
        };

        if (includeCaptureFields)
        {
            fields.Add(Field("screenshot", "Screenshot / image", FormFieldKind.ImageUpload, "screenshotPath"));
            fields.Add(Field("captured-pdf", "Captured advert PDF", FormFieldKind.PdfUpload, "capturedPdfPath"));
        }

        return Field("adverts", "Adverts", FormFieldKind.Repeater, path, required: true, fields: fields);
    }

    private static IReadOnlyList<DocumentFormField> SectionRepeaterFields() => new[]
    {
        Field("heading", "Heading", FormFieldKind.Text, "heading"),
        Field("paragraph", "Paragraph", FormFieldKind.MultilineText, "blocks[0].text"),
    };

    private static DocumentFormDefinition Form(string id, string renderId, params DocumentFormSection[] sections) => new()
    {
        TemplateId = id,
        RenderTemplateId = renderId,
        Sections = sections,
    };

    private static DocumentFormSection Section(string id, string title, IReadOnlyList<DocumentFormField> fields, string? description = null) => new()
    {
        Id = id,
        Title = title,
        Description = description,
        Fields = fields,
    };

    private static DocumentFormField Field(
        string id,
        string label,
        FormFieldKind kind,
        string path,
        bool required = false,
        string? placeholder = null,
        IReadOnlyList<FormOption>? options = null,
        IReadOnlyList<DocumentFormField>? fields = null) => new()
        {
            Id = id,
            Label = label,
            Kind = kind,
            Path = path,
            Required = required,
            Placeholder = placeholder,
            Options = options ?? Array.Empty<FormOption>(),
            Fields = fields ?? Array.Empty<DocumentFormField>(),
        };

    private static IReadOnlyList<FormOption> Options(params (string Value, string Label)[] options) =>
        options.Select(o => new FormOption { Value = o.Value, Label = o.Label }).ToList();

    private static ContentBlock Paragraph(string text) => new()
    {
        Type = "paragraph",
        Text = text,
    };

    private static ContentBlock DataTable(params (string Label, string Value)[] rows) => new()
    {
        Type = "datatable",
        Rows = rows.Select(r => new KeyValueRow { Label = r.Label, Value = r.Value }).ToList(),
    };

    private static ContentBlock MediaRow(params (string Caption, string ImagePath, string Note)[] media) => new()
    {
        Type = "mediarow",
        Media = media.Select(m => new MediaItem { Caption = m.Caption, ImagePath = m.ImagePath, Note = m.Note }).ToList(),
    };

    private static List<KeyValueRow> RepairableRows() => Rows(
        ("Make", ""), ("Registration", ""), ("Model", ""), ("Status", "Repairable"),
        ("Repair Cost", ""), ("Legal Status", ""), ("Engineer Value", ""), ("Impact Magnitude", ""));

    private static List<KeyValueRow> TotalLossRows() => Rows(
        ("Make", ""), ("Registration", ""), ("Model", ""), ("Status", "T/Loss"),
        ("Category", ""), ("Salvage Value", ""), ("Repair Cost", ""), ("Legal Status", ""),
        ("Engineer Value", ""), ("Impact Magnitude", ""));

    private static List<KeyValueRow> RoadworthyRows() => Rows(
        ("Make", ""), ("Registration", ""), ("Model", ""), ("Inspection Date", ""),
        ("Inspection Location", ""), ("Legal Status", ""), ("Roadworthy Conclusion", ""), ("Impact Magnitude", ""));

    private static List<KeyValueRow> Rows(params (string Label, string Value)[] rows) =>
        rows.Select(r => new KeyValueRow { Label = r.Label, Value = r.Value }).ToList();

    private static string Json<T>(T value) =>
        JsonSerializer.Serialize(value, CrJson.Options);

    private sealed record Entry(
        AuthoringTemplateDescriptor Descriptor,
        DocumentFormDefinition Form,
        Func<string> BlankJson);
}
