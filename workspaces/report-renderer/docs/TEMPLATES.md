# Template and form authoring guide

> **Design authority:** [`../../../design/README.md`](../../../design/README.md) is authoritative for shared brand and design governance. Template-specific rules in this document refine that authority for this renderer; they do not replace it.

This guide defines the current payload, form, attachment, asset, density and accessibility invariants for Collision Renderer. Core is the contract owner. GUI, CLI, API and MCP hosts may present the contract differently, but must not create divergent template rules.

## Authoring layers

Every rendered document has three layers:

1. **Typed model** — the C# payload contract deserialised from JSON.
2. **Scriban body** — first-party embedded body markup for document-specific content.
3. **Common shell** — C#-built letterhead, reference block, title treatment, stylesheet and running footer.

A body template must not emit its own logo, reference table, running strapline or page numbers. Keeping page furniture in the common shell is what makes all hosts and document families consistent.

## JSON and payload conventions

- JSON property names are camelCase. Deserialisation is case-insensitive, permits trailing commas and represents enums as strings.
- Free text is normally a string, including user-entered mileage, years and values where the model does not explicitly use a numeric type.
- Fee-note item amounts and VAT rate are numeric decimals.
- Pass plain text, not HTML. The composer HTML-encodes payload text before output.
- Shared document metadata uses `meta`, including `ourRef`, `yourRef`, `date` and `preparedBy` where the model supports them.
- Blank drafts are intentionally allowed to be incomplete. Required-field validation belongs to validate/render, not to opening a new blank form.
- Render failures use the common validation error contract; advisory conditions are returned as warnings.
- Currency, mileage, year and vehicle-history display should go through Core formatters rather than being formatted independently by a host or body template.

## Exact render catalogue

The render catalogue contains exactly 12 IDs.

| ID | Purpose | Payload/body family | Auto-fit |
| --- | --- | --- | --- |
| `market-valuation-evidence` | Pre-accident retail valuation supported by comparable adverts. | Valuation | Target 1 page |
| `advert-evidence-pack` | Linked advert references and optional captured evidence. | Evidence pack | No |
| `fee-note` | VAT fee note with computed totals and payment details. | Fee note | No |
| `expert-report` | Flexible report assembled from content blocks. | Expert report | No |
| `blank-letterhead` | Branded blank letterhead/correspondence starting point. | Letter/report shell | No |
| `repairable-contract-repair-report` | Repairability and contract-repair assessment. | Fixed expert-report form | No |
| `total-loss-report` | Total-loss assessment and settlement material. | Fixed expert-report form | No |
| `addendum-report` | Addendum to an earlier opinion/report. | Fixed expert-report form | No |
| `diminution-rebuttal` | Rebuttal concerning diminution or an opposing assessment. | Fixed expert-report form | No |
| `roadworthy-criminal-report` | Roadworthiness, defect or criminal-matter engineering report. | Fixed expert-report form | No |
| `part-35-response` | Part 35 question-and-response schedule. | Fixed expert-report form | No |
| `response-letter` | Branded response correspondence. | Fixed letter/report form | No |

The catalogue descriptor in Core remains authoritative for the concrete model type, resource name, filename suffix and density profile.

## Core-owned form contract

Each selectable authoring template has:

- an authoring descriptor;
- a `DocumentFormDefinition` containing ordered sections and controls;
- a blank payload factory;
- a generated starter payload;
- an attachment policy;
- a mapping to a valid render-template ID.

Supported form concepts include single-line text, multiline text, dates, money, numbers, selections, checkboxes, tables, repeaters, question/answer repeaters, signature selection, image uploads and PDF uploads. Hosts render these definitions; they do not hard-code a separate schema.

Shared report-style forms should expose the applicable Our Ref, Your Ref, date, addressee/FAO, salutation, matter or RE line, prepared-by/signatory, subtitle and closing fields. A field may be omitted where it is not part of the concrete model.

## Family payload requirements

### Market valuation evidence

The form and payload cover:

- shared metadata;
- full subject-vehicle details;
- introduction and market-research text;
- assessed retail value, optional guide value and valuation basis;
- comparable advert rows;
- evidence assessment and commentary;
- conclusion and VAT note where applicable;
- signature.

Comparable adverts may carry source, URL, advert ID, access date, price, vehicle details, registration year, mileage, fuel, transmission, body style, seller type, location, comparability/difference notes, report comment and evidence-role flags. `assessedRetailValue` is required. Core validation decides which advert fields are required or advisory.

In auto mode this template tries Normal, Compact and UltraCompact, stopping at the first one-page result. It must warn rather than clip when the supplied content cannot fit the target.

### Advert evidence pack

The payload covers shared metadata, subject vehicle, introduction, search summary and one or more comparable adverts. Each advert can carry a source URL and a captured evidence attachment.

The generated linked-reference table comes first. Validated uploaded advert evidence is appended in advert order. PDF captures are appended as PDF pages after the branded section; supported image captures are rendered through the governed image path. Missing captures warn or fail according to the concrete attachment/evidence policy.

### Fee note

The payload covers:

- fee-note number and references;
- date and bill-to party;
- matter and subject-vehicle summary;
- line items with description, optional detail and numeric amount;
- VAT rate and VAT registration number;
- payment details and notes.

Core computes subtotal, VAT and total. A body template or host must not accept caller-supplied totals as a substitute for this calculation. The running footer replaces the standard email segment with `VAT Reg No. <number>` when a VAT number is present; absence of an expected number is advisory and produces the reduced strapline.

### Expert report and fixed report IDs

The generic `expert-report` accepts a title, optional subtitle and title flags, salutation, RE line, introductory paragraphs, sections, blocks and signature. The fixed report IDs use the same governed report vocabulary with form structures suited to their document family.

The only accepted block types are:

| Type | Contract | Output |
| --- | --- | --- |
| `paragraph` | `text` | Prose paragraph. |
| `bullets` | `items[]` | Unordered list. |
| `datatable` | label/value rows | Two-pair summary data table. |
| `keyvalue` | label/value rows | Label/value table. |
| `evidencetable` | columns and cell rows | Free-form evidence table with repeated header. |
| `valuebox` | label and value | Branded headline value. |
| `mediarow` | media slots | Two-up image/placeholder row. |

Block names are case-insensitive at validation, but authors should use the lowercase canonical spelling. Unknown block types are errors.

### Blank letterhead

`blank-letterhead` is the minimal branded correspondence surface. It still uses the shared shell and must not be implemented by copying page-furniture markup into a payload or body template. It is not a mechanism for runtime user-authored Scriban or arbitrary HTML.

### Repairable/contract repair and total loss

Forms include shared report fields, subject vehicle, assessment summary, narrative findings, conclusion and signature. Image slots may include the vehicle, impact area and additional damage. Total-loss forms may also capture category, salvage, repair cost, engineer value and a settlement figure or its source values; computation policy remains in Core rather than the GUI.

### Addendum, diminution rebuttal and response letter

These are correspondence/report forms over the common shell. Addenda capture the earlier report reference and the new instruction/challenge. Rebuttals provide fixed editable sections and optional additions. Response letters capture recipient, matter line, opening/body paragraphs and applicable correspondence flags. None requires an image by default.

### Roadworthy/criminal report

The form captures instructing or court details, subject vehicle, inspection circumstances, findings, roadworthiness/compliance conclusion, declaration and signature. Governed media slots may carry vehicle, defect, damage or impact images.

### Part 35 response

The form captures the original report reference/date, schedule date, question/reply repeater, optional reviewed-document list, closing statement and signature. Preserve each question/reply pair as a unit and avoid splitting it unnecessarily over a page boundary.

## Attachment and image rules

Attachments are case data and must never become tracked source assets.

### Core embedded-image inputs

The supported embedded image formats are exactly:

- PNG;
- JPEG (`.jpg`/`.jpeg`);
- WebP.

An allowed Core input may be represented by a validated local path or an accepted data URI, according to the template's attachment policy. The renderer resolves it to an embeddable representation before Chromium layout. Unsupported formats, missing paths and malformed data fail with a field-specific validation message. Do not document GIF as supported.

Bundled brand signatures are selected by governed key. Custom signatures are case attachments and follow image validation; they do not become new brand assets.

### Evidence PDFs

PDF uploads are permitted only where the attachment policy calls for them, principally captured advert evidence. Accepted PDFs are validated before rendering and appended after the generated evidence pack in advert order. They are not embedded as arbitrary HTML and PDFsharp is not used to lay out report content.

### API restrictions

The API is stricter than an in-process Core caller:

- multipart files must bind to declared model paths;
- each part must satisfy the endpoint and template attachment policy;
- content type, extension/signature and configured size limits are checked before render;
- PDF is accepted only in PDF-designated slots;
- clients must not assume that a JSON server-local path is reachable or permitted;
- remote callers do not receive general filesystem access merely because a desktop/CLI draft can hold a local path.

`/v1/render.multipart` carries `templateId`, optional density, JSON `data` and named file fields. Use the form definition and endpoint contract to choose field names rather than inventing paths.

### Draft storage

A local draft may store external paths or embedded base64 according to the host's supported save mode. Path references are preferable for large local case files, but moving or deleting the source file will make later validation fail. Temporary uploads and generated intermediates must remain outside tracked source.

## Design-system invariants

### Palette

| Token | Value | Use |
| --- | --- | --- |
| Documents red | `#C80A32` | Rules, table headers, title variants, total/value emphasis. |
| Body charcoal | `#222` | Printed body and primary headings. |
| Brand-reference charcoal | `#2C2A27` | Reference brand value; do not silently substitute as a new body rule. |
| Label grey | `#F2F2F2` | Key cells and total backgrounds. |
| Zebra grey | `#F5F5F5` | Alternating table rows. |
| Grid grey | `#BEBEBE` | Table and placeholder borders. |
| Link blue | `#0057B8` | Meaningful links. |
| Muted grey | `#555` | Footer and secondary notes. |
| Placeholder fill | `#F4F4F3` | Empty media slots. |

Do not introduce decorative gradients, sparkle/magic symbols, emoji or an “AI” visual theme. Output should remain calm, factual and suitable for courts, solicitors and insurers.

### Typography

- Data-register documents use an 8.8pt base with compact tabular spacing.
- Letter/report documents use a 10pt prose register with approximately 1.5 line height.
- Body faces are Arial/Helvetica or a metric-compatible substitute.
- Titles are centred and uppercase; newer variants may use documents red.
- Section headings are uppercase in intent and carry a red rule.
- Expert-report prose may be justified; tabular data must remain legible rather than forced into prose styling.

### Letterhead and running footer

The common first-page letterhead contains the master gear-“C” logo and an Our Ref / optional Your Ref / Date table. Core supplies template-appropriate fallbacks only where defined by the model/composer.

Every page reserves space for the Chromium running footer. The standard strapline is:

```text
Collision Engineers Ltd | www.CollisionEngineers.co.uk | engineers@collisionengineers.co.uk
```

Fee notes replace the final segment with their VAT registration number when supplied. The right side carries live `— n of N —` numbering.

### Components

Reuse the governed CSS classes rather than creating near-duplicates:

- `kv-table` for compact key/value data;
- `data-table` for red-bordered report summaries;
- `value-box` for a headline figure;
- advert/evidence tables for comparable data;
- `fee-table`, fee totals and payment grid for fee notes;
- `sig-block` for non-splitting sign-off;
- `media-row` for non-splitting image/placeholder groups.

Tables should use semantic headers. Long-table headers repeat; rows, value boxes, media rows and signatures avoid internal page breaks.

## Density rules

| Density | Body class | Typical data base |
| --- | --- | --- |
| Normal | none | 8.8pt |
| Compact | `report-compact` | 8pt |
| UltraCompact | `report-ultra-compact` | 7.5pt |

Do not use density to conceal missing content or validation errors. Fit-to-page is a bounded layout strategy, not permission to shrink indefinitely.

## Accessibility and content quality

- Use a logical title and heading order; do not simulate headings with arbitrary bold paragraphs.
- Give tables real header cells and concise headings.
- Links must have meaningful visible text and remain printable as references where appropriate.
- Every supplied image should have a useful caption or contextual label; decorative brand images remain shell-owned.
- Do not use colour as the only carrier of meaning.
- Keep language factual, use British English and avoid unexplained decorative symbols.
- Keep rows and question/answer units together where practical, but allow content to flow rather than clipping.
- The renderer does not currently claim tagged-PDF or PDF/UA output. Do not represent visual/HTML semantics as a certification the PDF pipeline does not provide.

## Adding or changing a template

1. Add or reuse a typed model in Core.
2. Add the body Scriban resource under the governed design asset tree; body content only.
3. Register one `TemplateDescriptor` with a unique ID, model, resource, density profile and filename suffix.
4. Extend `HtmlComposer` only for the template-specific context and chrome values; do not fork the render pipeline.
5. Add a validator case with errors for required data and warnings for advisory data.
6. Add a Core-owned form, blank, starter and attachment policy.
7. Add synthetic, non-PII tests through the fake engine and real Chromium where layout matters.
8. Exercise the template through relevant hosts to verify that all surfaces resolve the same catalogue entry.

Never copy customer PDFs, extracted text, registrations, claims or local reference filenames into a template, sample, test or tracked document.