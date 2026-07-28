# Template authoring guide

This guide explains the JSON payload shape for each of the four base render-template families,
the eight expert-report authoring presets, the content-block catalogue used by expert reports,
written for engineers extending `CollisionRenderer.Core`. New documents must follow the existing
style exactly; the sections below set out what that means in practice.

The single most important property to preserve when authoring a new template is this: **the
letterhead and footer are not part of any template body.** They are assembled in C# by
`HtmlComposer`, so every document — old or new — carries identical brand furniture by construction.
A `.scriban` body never draws the logo, the Our/Your Ref/Date block, the page numbers or the
running strapline. It supplies only the content between them.

## How a document is built

Each render runs through three parts, defined in ADR 0004:

1. **A typed C# model record** (in `src/CollisionRenderer.Core/Models/Documents.cs`) — the payload
   schema. JSON is deserialised into this with camelCase property names, case-insensitive matching,
   trailing commas allowed and enums written as strings (`CrJson.Options` in `Contracts.cs`).
2. **A Scriban body template** (in `Assets/templates/*.scriban`) — the per-document HTML body. The
   composer pre-encodes every value, so templates emit content with no further escaping.
3. **The C#-built letterhead shell** (`HtmlComposer.Shell` and `HtmlComposer.Letterhead`) — wraps
   the rendered body in the shared letterhead, applies the embedded brand stylesheet
   (`Assets/templates/report.css`), and hands Chromium the running header/footer templates.

Templates, the stylesheet, the logo and the signature images are embedded resources in
`CollisionRenderer.Core` (see the `.csproj` `EmbeddedResource` items), so the engine is
self-contained and renders identically from the CLI, the desktop app or a Linux container.

## The shared shell guarantees identical letterhead and footer

`HtmlComposer.Compose` produces a `DocChrome` for the chosen template, then wraps it through one
common path. The relevant guarantees:

- **Letterhead.** `Letterhead` emits the master gear-"C" logo (inlined as a data URI) followed by a
  reference table containing `Our Ref:`, an optional `Your Ref:`, and `Date:`. Every template gets
  the same markup. `Our Ref` falls back to a template-specific value (the registration, the fee-note
  number, or `REPORT`) when `meta.ourRef` is absent; `Date` falls back to today's date when
  `meta.date` is absent.
- **Footer.** `FooterTemplate` builds the Chromium running footer: a thin red rule, the centred
  strapline, and a right-aligned `— n of N —` page marker that repeats on every page. The standard
  strapline is
  `Collision Engineers Ltd | www.CollisionEngineers.co.uk | engineers@collisionengineers.co.uk`. The
  fee note swaps the email for the VAT registration number; everything else uses the standard
  strapline.
- **Title and section furniture.** The centred UPPERCASE title and the section headings with a red
  rule under them come from the shared stylesheet, not from any individual body.

A new template that follows the recipe below inherits all of this without writing any of it. Do not
reproduce letterhead or footer markup inside a `.scriban` body.

## Payload conventions

These apply to every template:

- **All fields are strings where the document is free text**, including figures such as mileage and
  prices. The renderer formats on output (currency as `£24,750.00`, mileage with thousands
  separators, years and vehicle history normalised), so a payload may carry `"31450"` or
  `"31,450 miles"` and both render correctly. The fee note is the exception: line-item amounts and
  the VAT rate are numeric (`decimal`).
- **Property names are camelCase** in JSON and map to the PascalCase record properties.
- **Text is HTML-encoded by the composer.** Payload values cannot inject markup; you pass plain
  text, not HTML.
- **`meta` is shared** across all render templates (`ourRef`, `yourRef`, `date`, `preparedBy`).

---

## Template payloads

The four base render templates and their identifiers:

| Id | Model record | Body template | Density profile |
| --- | --- | --- | --- |
| `market-valuation-evidence` | `MarketValuationEvidenceDocument` | `market_valuation_evidence.scriban` | `FitToPages`, target 1 page |
| `advert-evidence-pack` | `AdvertEvidencePackDocument` | `advert_evidence_pack.scriban` | `None` |
| `fee-note` | `FeeNoteDocument` | `fee_note.scriban` | `None` |
| `expert-report` | `ExpertReportDocument` | `expert_report.scriban` | `None` |

Use `collisionrenderer forms starter --template <id>` to generate an overwriteable
starting payload from the Core-owned blank draft and form definition. The shapes
below are abbreviated contract references; no case payload examples are committed.

### `market-valuation-evidence`

Retail pre-accident value evidenced by a comparable advert table, with a red value box and a
signature. This template uses the `FitToPages` density profile (target one page): under
`DensityFit.Auto` the renderer tries Normal, then Compact, then Ultra-compact until the document
lands on the page target.

Shape (selected fields; `subject` carries the full `SubjectVehicle` record):

```json
{
  "meta": { "ourRef": "PCH25309", "yourRef": "KLZ-2025-184", "date": "20/06/2026" },
  "subject": {
    "registration": "LV25KLZ",
    "make": "BMW", "model": "3 Series", "derivative": "320d M Sport",
    "bodyType": "Saloon", "fuel": "Diesel", "transmission": "Automatic",
    "engine": "1995cc", "firstRegistered": "01/03/2022",
    "mileage": "31450", "colour": "Mineral Grey Metallic",
    "vehicleHistory": "No adverse history recorded", "vin": "WBA5E11020K123456"
  },
  "intro": "We have undertaken a review of comparable vehicles ...",
  "marketResearch": "The following comparable retail market evidence ...",
  "assessedRetailValue": "24750",
  "guideValue": "23900",
  "adverts": [
    {
      "make": "BMW", "model": "3 Series", "derivativeOrEngine": "320d M Sport",
      "registrationYear": "2022", "mileage": "28000", "price": "25495",
      "sellerType": "Franchise", "advertId": "A-100231",
      "url": "https://example.com/advert/100231",
      "comparabilityNote": "Closely comparable specification and age.",
      "differencesNote": "Slightly lower mileage than the subject vehicle."
    }
  ],
  "valuationCommentary": [ "The subject vehicle is a well-specified ...", "..." ],
  "conclusion": "It is our professional opinion that the pre-accident retail value ...",
  "signature": {
    "name": "A. Patterson", "qualifications": "M.Inst.AEA",
    "aqpNumber": "AQP01234", "signatureImage": "andy_patterson"
  }
}
```

Notes:

- `assessedRetailValue` is required. `intro`, `marketResearch` and the search summary have sensible
  built-in defaults when omitted.
- `adverts[]` are rendered into the comparable evidence table; each row shows vehicle, year, mileage,
  seller type, price and a report comment (the explicit `reportComment`, otherwise the comparability
  and differences notes joined).
- `signatureImage` is a bundled key: `andy_patterson`, `ed_mawdsley` or `neil_oreilly`. An unknown
  key simply renders no signature image.

### `advert-evidence-pack`

A linked, comparable-advert reference table that accompanies the valuation evidence. No density
auto-fit; the table flows to as many pages as needed with a repeating header.

```json
{
  "meta": { "ourRef": "PCH25309", "yourRef": "KLZ-2025-184", "date": "20/06/2026" },
  "subject": {
    "registration": "LV25KLZ",
    "make": "BMW", "model": "3 Series", "derivative": "320d M Sport"
  },
  "intro": "Comparable advert references corresponding with the market valuation evidence report.",
  "adverts": [
    {
      "make": "BMW", "model": "3 Series", "derivativeOrEngine": "320d M Sport",
      "registrationYear": "2022", "mileage": "28000", "price": "25495",
      "advertId": "A-100231", "url": "https://example.com/advert/100231"
    }
  ],
  "searchSummary": "Searches were conducted using live retail market evidence ..."
}
```

Notes:

- At least one advert is required. Each row links to the advert `url`; a missing `advertId` renders
  as `Not stated`.

### `fee-note`

A VAT fee note / invoice. The footer swaps the standard email strapline for the VAT registration
number (`VAT Reg No. {vatNumber}`). The composer computes the subtotal from the line items, applies
`vatRate`, and renders subtotal, VAT and total.

```json
{
  "meta": { "ourRef": "QCL24257-P35", "date": "20/06/2026" },
  "feeNoteNumber": "QCL24257-P35-1",
  "billTo": {
    "name": "Example Law LLP",
    "addressLines": [ "1 Library Street", "Manchester", "M2 3AB" ],
    "reference": "EL/2025/0451"
  },
  "matterReference": "Claimant v Defendant Insurance plc",
  "subject": { "registration": "LV25KLZ", "make": "BMW", "model": "3 Series" },
  "items": [
    {
      "description": "Independent market valuation report",
      "detail": "Preparation of pre-accident retail valuation with comparable advert evidence.",
      "amount": 175.00
    },
    { "description": "Administration and report production", "amount": 25.00 }
  ],
  "vatRate": 0.20,
  "vatNumber": "GB 123 4567 89",
  "payment": {
    "bankName": "Example Bank plc", "accountName": "Collision Engineers Ltd",
    "sortCode": "01-02-03", "accountNumber": "12345678",
    "terms": "Payment due within 30 days of the date of this note."
  },
  "notes": "This fee note relates to engineering work completed in accordance with your instructions."
}
```

Notes:

- `feeNoteNumber`, `billTo.name` and at least one line item are required.
- `amount` and `vatRate` are numeric, not strings. `vatRate` defaults to `0.20` and is rendered as a
  percentage in the document.
- `vatNumber` is expected for the footer; omitting it produces a warning and a footer without the VAT
  number.

### `expert-report`

A flexible letter-style report assembled from content blocks. The same shape serves Total Loss
reports, Addenda, Diminution Rebuttals, Part 35 responses and Roadworthy reports — the document type
is just a `title` plus an arrangement of sections and blocks.

```json
{
  "meta": { "ourRef": "PCH25427", "yourRef": "CK25AEA-2025", "date": "20/06/2026" },
  "title": "Total Loss Report",
  "subtitle": "RE: BMW 3 Series 320d M Sport — Registration CK25AEA",
  "titleRed": true,
  "titleUnderlined": false,
  "salutation": "Dear Sirs,",
  "reLine": "RE: Assessment of the above vehicle following a road traffic incident",
  "redIntro": false,
  "intro": [
    "In accordance with your instructions, we have inspected the above vehicle ...",
    "This report is provided on an independent basis ..."
  ],
  "sections": [
    {
      "heading": "Subject Vehicle Details",
      "blocks": [
        {
          "type": "datatable",
          "rows": [
            { "label": "Registration", "value": "CK25AEA" },
            { "label": "Make / Model", "value": "BMW 3 Series 320d M Sport" }
          ]
        }
      ]
    },
    {
      "heading": "Engineer's Comments",
      "blocks": [
        { "type": "paragraph", "text": "Having inspected the vehicle ..." },
        { "type": "bullets", "items": [ "Structural damage to the front nearside chassis leg.", "..." ] }
      ]
    }
  ],
  "signature": {
    "name": "E. Mawdsley", "qualifications": "M.Inst.AEA",
    "aqpNumber": "AQP05678", "signatureImage": "ed_mawdsley", "closing": "Yours faithfully,"
  }
}
```

Notes:

- `title` is required, and a report needs at least one section. Headings are rendered in UPPERCASE.
- `titleRed` (default `true`) and `titleUnderlined` (default `false`) toggle the title styling.
  `redIntro` styles the salutation and RE line in the documents red.
- `signature.name`, `signature.role` and `signature.org` are tri-state: omit (or send JSON
  `null`) for the built-in default, send a value to override, or send an explicit `""` to
  suppress that line. The firm-only rebuttal sign-off is
  `"signature": { "name": "", "role": "" }`, which prints `Yours faithfully,` followed by
  `Collision Engineers Ltd` and nothing else.
- Block content is described in the catalogue below.

---

## Expert-report content-block catalogue

An expert report is a list of `sections`, each with an optional `heading` and a list of `blocks`.
Every block has a `type` and the fields that type uses; unused fields are omitted. The seven block
types are below, with the C# model (`ContentBlock` and its companions in `Documents.cs`) and the
markup `expert_report.scriban` emits.

The validator rejects any block whose `type` is not one of these seven (case-insensitive).

| `type` | Payload fields | Renders as |
| --- | --- | --- |
| `paragraph` | `text` | A body paragraph (`p.doc-p`). |
| `bullets` | `items[]` | An unordered list (`ul.doc-ul`). |
| `datatable` | `rows[]` of `{ label, value }` | A two-pairs-per-row data table (`table.data-table`). |
| `keyvalue` | `rows[]` of `{ label, value }` | A label/value table with grey label cells (`table.kv-table`). |
| `evidencetable` | `table.columns[]`, `table.rows[]` | A bordered table with a red header row (`table.advert-table`). |
| `valuebox` | `value.label`, `value.value` | The red-bordered summary value box (`table.value-box`). |
| `mediarow` | `media[]` of `{ caption, imagePath, note }` | A row of image columns or placeholder slots (`div.media-row`). |

### `paragraph`

```json
{ "type": "paragraph", "text": "Having inspected the vehicle and assessed the damage ..." }
```

### `bullets`

```json
{
  "type": "bullets",
  "items": [
    "Structural damage to the front nearside chassis leg.",
    "Deployment of the front and side restraint systems."
  ]
}
```

### `datatable`

A compact data grid. Rows are laid out two label/value pairs across, so a list of `{ label, value }`
fills the table left to right then down.

```json
{
  "type": "datatable",
  "rows": [
    { "label": "Registration", "value": "CK25AEA" },
    { "label": "First Registered", "value": "01/03/2022" },
    { "label": "Mileage", "value": "31,450 miles" },
    { "label": "Colour", "value": "Mineral Grey Metallic" }
  ]
}
```

### `keyvalue`

The same `{ label, value }` row shape as `datatable`, rendered with grey label header cells. Use it
where the label/value distinction should read as a heading rather than a plain grid.

```json
{
  "type": "keyvalue",
  "rows": [
    { "label": "Inspection date", "value": "14/05/2026" },
    { "label": "Location", "value": "Manchester" }
  ]
}
```

### `evidencetable`

A free-form bordered table for comparable evidence or any tabular data. Define the columns (header,
alignment, optional fixed width) and supply rows as arrays of cell strings in column order. Set
`align` to `left`, `right` or `center` (`centre` is accepted); currency columns are conventionally
right-aligned. `width` is an optional CSS length such as `22mm`.

```json
{
  "type": "evidencetable",
  "table": {
    "columns": [
      { "header": "Vehicle", "align": "left" },
      { "header": "Year", "align": "left", "width": "18mm" },
      { "header": "Mileage", "align": "right", "width": "22mm" },
      { "header": "Price", "align": "right", "width": "24mm" }
    ],
    "rows": [
      [ "BMW 3 Series 320d M Sport", "2022", "28,000", "£25,495" ],
      [ "BMW 3 Series 320d M Sport", "2021", "34,500", "£23,990" ]
    ]
  }
}
```

### `valuebox`

The red-bordered summary figure used for headline values such as a settlement figure.

```json
{
  "type": "valuebox",
  "value": { "label": "Pre-accident value, less salvage", "value": "£21,950.00" }
}
```

### `mediarow`

A row of image columns. Each item has a `caption`, an optional `imagePath`, and an optional `note`.
`imagePath` accepts a data URI, an `http(s)` URL, or an absolute path to a file on disk (PNG/JPG/GIF,
inlined as a data URI). When `imagePath` is empty, a placeholder slot is rendered with the caption.

```json
{
  "type": "mediarow",
  "media": [
    { "caption": "Front nearside impact", "imagePath": "C:\\inspections\\ck25aea\\front_ns.jpg", "note": "Crush to wing and bumper." },
    { "caption": "Deployed airbags", "note": "Image to follow." }
  ]
}
```

---

## Robustness across long documents

Long documents keep the house style without garbling, by design:

- Pages are A4 (`@page`), with Chromium running header/footer templates repeating on every page,
  including the page number.
- Table headers repeat across page breaks (`thead { display: table-header-group }`).
- Rows, value boxes and media rows are kept whole (`break-inside: avoid`), so nothing splits across a
  page boundary.

A 36-row valuation flows to three pages with a repeating header and footer and no garbling. The
sample valuation auto-fits to Compact to stay on one page. Any new template that produces long
tables or block sequences inherits this behaviour through the shared shell and stylesheet — provided
it uses the existing table and block CSS classes rather than inventing its own.

---

## Add a new template

Adding a template is five small steps and **no engine change**. Work through them in order. The
running example is a hypothetical `inspection-summary` template.

### 1. Add a model record

In `src/CollisionRenderer.Core/Models/Documents.cs`, add a sealed record for the payload. Carry
`DocumentMeta` so the new document gets the shared letterhead reference block, reuse the existing
shared records (`SubjectVehicle`, `SignatureBlock`, and so on) where they fit, and keep free-text
fields as strings so authors can write figures naturally.

```csharp
public sealed record InspectionSummaryDocument
{
    public DocumentMeta Meta { get; init; } = new();
    public SubjectVehicle Subject { get; init; } = new();
    public string Summary { get; init; } = "";
    public List<string> Findings { get; init; } = new();
    public SignatureBlock? Signature { get; init; }
}
```

### 2. Add a `.scriban` body

Create `Assets/templates/inspection_summary.scriban`. Write only the body — no letterhead, no
footer, no page furniture; the shell provides all of that. Use the existing CSS classes
(`doc-p`, `doc-ul`, `data-table`, `kv-table`, `advert-table`, `value-box`, `sec-h`, and so on) so the
document matches the house style. Values handed to the template are already HTML-encoded, so emit
them directly.

The new template's context is built in `HtmlComposer`: add a branch to the `descriptor.Id` switch in
`Compose` and a private method (following the existing `Valuation`/`FeeNote`/`ExpertReport` methods)
that assembles a `ScriptObject` and returns a `DocChrome`. Pass `StandardStrapline` as the footer
unless the document needs a different one. This is the one C# touch-point; the rendering pipeline
itself is unchanged.

### 3. Register a `TemplateDescriptor`

In `src/CollisionRenderer.Core/TemplateCatalog.cs`, add a `TemplateDescriptor` to the array in the
constructor:

```csharp
new TemplateDescriptor
{
    Id = "inspection-summary",
    Name = "Inspection Summary",
    Description = "Concise summary of inspection findings.",
    ModelType = typeof(InspectionSummaryDocument),
    TemplateResource = "templates/inspection_summary.scriban",
    DensityProfile = DensityFitProfile.None,
    FileNameSuffix = "inspection_summary",
},
```

Set `DensityProfile = DensityFitProfile.FitToPages` (with `FitTargetPages`) only if the document
should auto-shrink to a page target, as the valuation does; otherwise leave it `None` and let content
flow. `FileNameSuffix` becomes the trailing part of the generated file name.

### 4. Add an authoring definition

Register the template in `AuthoringCatalog` with a Core-owned form definition and
blank-draft factory. `GetStarterJson` derives overwriteable prompts from those two
definitions, so the GUI and CLI can start a document without a committed case payload.

The `.csproj` embeds `Assets\templates\**\*`, and `EmbeddedResources` resolves template
resources by their trailing path, so no `.csproj` edit is needed.

### 5. Add a validator case

In `src/CollisionRenderer.Core/Validators.cs`, add a `case` to the switch in
`PayloadValidator.Validate` for the new model type. Add errors for genuinely required fields (a blank
required field should fail before render) and warnings for things that are merely advisable. Reuse
`RequireSubject` if the document carries a subject vehicle.

```csharp
case InspectionSummaryDocument d:
    RequireSubject(d.Subject, r);
    if (string.IsNullOrWhiteSpace(d.Summary))
    {
        r.Errors.Add("summary is required.");
    }

    if (d.Findings.Count == 0)
    {
        r.Warnings.Add("No findings supplied.");
    }

    break;
```

A model type with no matching case produces the error `No validator registered for template '...'`,
so this step is required, not optional.

### Verify

Build and exercise the new template through the existing surfaces:

```bash
dotnet build CollisionRenderer.sln -c Release
collisionrenderer list
collisionrenderer forms starter --template inspection-summary --out starter.json
collisionrenderer validate --template inspection-summary --data starter.json
collisionrenderer render   --template inspection-summary --data starter.json --out out.pdf
dotnet test
```

Because every host builds the renderer through `CollisionRendererFactory.CreateRenderer`, the new
template is available identically from the CLI, the desktop app and the API as soon as it is
registered — and it carries the same letterhead and footer as every other document.
