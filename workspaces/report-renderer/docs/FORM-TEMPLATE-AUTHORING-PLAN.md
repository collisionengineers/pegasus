# Form Template Authoring Plan

Status: Done

This document records the reference-backed form-authoring implementation for Collision Renderer.
The desktop app was originally a JSON payload editor; it is now a form-driven document builder:

1. The user selects a Collision Engineers reference document type.
2. The app opens a blank template made of labelled text boxes, tables, repeaters and upload slots.
3. The user types the case-specific text and attaches images or evidence files where the template
   calls for them.
4. The user presses Render and the shared Core renderer produces the branded PDF.

The renderer must still obey the existing golden rules: Core remains the single source of truth,
the CLI, GUI and API stay at parity, reference material remains local only, and no customer data is
copied into tracked samples, tests or documentation.

## Reference Material Boundary

The reference root for this planning pass is:

`../../../research/collisionrenderer-reference-material`

That folder is outside this repository. It is not product source and must not become a build input.
The tracked repository may refer to it, but must not copy its customer PDFs, extracted text, claim
details, registrations or other client data.

A full exact local inventory, including sensitive PDF filenames, has been generated here:

`artifacts/reference-material-inventory.md`

That file is intentionally under ignored `artifacts/`. It is the exact manifest for local use. Do
not commit it. Tracked docs refer to sensitive PDFs by folder and aggregate counts only.

Implementation status:

- Core owns `IAuthoringTemplateCatalog`, `DocumentFormDefinition`, blank draft generation,
  attachment policies and upload validation.
- The GUI defaults to generated blank forms and keeps JSON as an advanced diagnostic tab.
- CLI and API expose the same blank form catalogue and render the same payloads.
- Image uploads are supported as local paths/data URIs. Advert evidence PDFs are validated and
  appended after the generated evidence pack.
- Batch rendering is available from CLI, API and GUI.
- Visual regression tooling lives in `scripts/visual-regression.ps1` and stores generated assets
  under ignored `artifacts/visual-regression/`.

Reference set covered by the inventory:

| Reference area | Files | Purpose in this plan |
| --- | ---: | --- |
| `collision-engineers-design-dev/` | 70 | Brand, writing, print-document design kit and UI kit. |
| `documentexamples/` | 18 PDFs, 110 pages | Real local example reports. Sensitive; exact filenames only in the ignored manifest. |
| `stylexamples/` | 5 PDFs, 14 pages | Local style examples for valuation/evidence outputs. Sensitive; exact filenames only in the ignored manifest. |
| `report-renderer/` | 43 | Prior Python renderer, valuation/evidence templates, validators and artifact handling. |

Specific non-sensitive reference files that drive the implementation:

| Path under reference root | Use |
| --- | --- |
| `collision-engineers-design-dev/README.md` | Overall brand context and print surface rules. |
| `collision-engineers-design-dev/WRITING.md` | Document families, tone, sign-off and governance. |
| `collision-engineers-design-dev/collision-engineers-design/SKILL.md` | Quick map and non-negotiable visual constraints. |
| `collision-engineers-design-dev/collision-engineers-design/colors_and_type.css` | Brand tokens; especially document red and typography policy. |
| `collision-engineers-design-dev/collision-engineers-design/references/document-letterhead.md` | Canonical A4 letterhead, title, section, table, value box, footer, signature and image-slot specification. |
| `collision-engineers-design-dev/collision-engineers-design/references/palette-and-type.md` | Palette, type scale, spacing and radii reference. |
| `collision-engineers-design-dev/collision-engineers-design/references/iconography.md` | Lucide icon policy for UI controls; no emoji or hand-drawn icons. |
| `collision-engineers-design-dev/collision-engineers-design/ui_kits/documents/README.md` | Names the reference document set and explains the intended document kit. |
| `collision-engineers-design-dev/collision-engineers-design/ui_kits/documents/doc.css` | Print CSS for total loss, valuation, rebuttal, response letter and fee note examples. |
| `collision-engineers-design-dev/collision-engineers-design/ui_kits/documents/documents.jsx` | Reference component layouts and field groupings for the blank templates. |
| `collision-engineers-design-dev/collision-engineers-design/ui_kits/documents/letterhead.jsx` | Shared letterhead, data table, value callout, media placeholder, footer and signature components. |
| `collision-engineers-design-dev/collision-engineers-design/assets/logo_no_margin.png` | Master logo reference. Product assets are already embedded in Core; do not copy from the reference folder blindly. |
| `collision-engineers-design-dev/collision-engineers-design/assets/signatures/*.png` | Signature reference. Product signature assets are already embedded in Core. |
| `report-renderer/README.md` | Prior renderer contract and artifact shape. |
| `report-renderer/server/server.py` | Prior MCP/API flow, artifact storage shape and gated future report tools. |
| `report-renderer/server/valuation_engine.py` | Prior valuation/evidence render orchestration. |
| `report-renderer/server/engines/valuation/assets/templates/_base.html.j2` | Prior base letterhead shell. |
| `report-renderer/server/engines/valuation/assets/templates/report.html.j2` | Prior market valuation report body. |
| `report-renderer/server/engines/valuation/assets/templates/evidence_pack.html.j2` | Prior advert evidence-pack body. |
| `report-renderer/server/engines/valuation/assets/templates/styles.css` | Prior print CSS and density classes. |
| `report-renderer/server/engines/valuation/scripts/_pdf_common.py` | Formatting, density fallback, output naming and prior WeasyPrint/ReportLab behaviour. |
| `report-renderer/server/engines/valuation/scripts/validate_evidence_pack.py` | Prior valuation policy checks and required advert fields. |
| `report-renderer/server/contracts/generated/python/contracts/valuation/v1/evidence_pack_payload.py` | Prior valuation payload contract. |

## Original Mismatch

The GUI originally exposed the renderer as:

- A document-type rail backed by `CollisionRendererFactory.Catalog`.
- A "Load sample" button.
- A raw JSON editor.
- A Render button and WebView2 PDF preview.

That was useful for developers, but it was not the intended authoring surface for non-technical
users. The implemented GUI now opens on generated blank forms; the JSON editor remains available as
an advanced diagnostic view.

## Target Document Set

The selectable authoring catalogue should be based on the reference document families, not only on
the current four render payloads.

| Authoring template id | User-facing name | Render model/template approach | Upload slots |
| --- | --- | --- | --- |
| `repairable-contract-repair-report` | Repairable / Contract Repair Report | First-class report template or expert-report preset with fixed form schema. | Vehicle image, impact-area image, optional damage images. |
| `total-loss-report` | Total Loss Report | First-class report template matching the reference total-loss layout. | Vehicle image, impact-area image, optional damage images. |
| `market-valuation-evidence` | Market Valuation Evidence | Existing first-class renderer, with a form schema replacing JSON entry. | None required; advert evidence links/files handled by evidence pack. |
| `advert-evidence-pack` | Advert Evidence Pack | Existing first-class renderer extended to attach captured advert PDFs/images where supplied. | Per-advert capture PDF or image evidence. |
| `addendum-report` | Addendum Report | Expert-report based authoring preset with fixed headings and editable body sections. | Optional supporting images/evidence. |
| `diminution-rebuttal` | Diminution Rebuttal | Expert-report based or first-class letter template matching the rebuttal reference. | None by default. |
| `roadworthy-criminal-report` | Roadworthy / Criminal Report | First-class report template or expert-report preset with safety/compliance sections. | Vehicle image, defect/damage images, optional impact-area image. |
| `part-35-response` | Part 35 Responses | Expert-report based question/answer repeater template. | None by default. |
| `response-letter` | Response Letter | Letter-style expert-report preset for dispute/correspondence replies. | None by default. |
| `fee-note` | Fee Note | Existing first-class renderer, with a form schema replacing JSON entry. | None. |

The existing generic `expert-report` can remain as an advanced/custom report type, but it should not
be the primary way a non-technical user creates a Total Loss, Rebuttal, Part 35 or Roadworthy
document.

## Shared Blank Form Shape

Every authoring template needs two Core-owned artefacts:

- A blank payload factory: creates a payload with the correct structure and empty values.
- A form definition: describes the fields the GUI, CLI helpers and API clients should present.

These must live in `CollisionRenderer.Core`, not the GUI. A host may render the fields differently,
but it must not invent template-specific data rules.

Core additions:

| Type | Responsibility |
| --- | --- |
| `IAuthoringTemplateCatalog` | Lists selectable authoring templates and returns form definitions and blank payloads. |
| `AuthoringTemplateDescriptor` | `Id`, `Name`, `Description`, `RenderTemplateId`, `Category`, `FormResource`, `BlankPayloadResource`, `ReferenceFamily`, `AttachmentPolicy`. |
| `DocumentFormDefinition` | Ordered sections, fields, repeaters, upload slots and model-path bindings. |
| `DocumentFormField` | Field metadata: label, kind, model path, placeholder, required flag, options, help text, validation hints. |
| `DocumentDraft` | A host-neutral draft payload plus attachment descriptors before render. |
| `AttachmentDescriptor` | File name, content type, source path or base64, caption, note, and the model path it fills. |

Field kinds:

| Field kind | GUI control | Payload output |
| --- | --- | --- |
| `text` | Single-line text box | string |
| `multilineText` | Multi-line text box | string |
| `date` | Date picker plus text override | string in rendered UK format |
| `money` | Text box with currency formatting on render | string or decimal depending on existing model |
| `number` | Numeric box | decimal/int where required, otherwise string |
| `select` | Combo box | string or enum |
| `checkbox` | Toggle/check box | bool |
| `table` | Fixed grid | list of rows or key/value rows |
| `repeater` | Add/remove row group | list |
| `questionAnswer` | Part 35 question/reply repeater | list of question/reply blocks |
| `signatureSelect` | Engineer selector | `SignatureBlock` |
| `imageUpload` | File picker with thumbnail, caption and remove/replace | image path/base64 resolved by Core |
| `pdfUpload` | File picker with file badge | PDF evidence attachment for evidence packs |

Blank payloads are allowed to be incomplete. Opening a blank template should not show validation
errors immediately. Render-time validation still fails if required fields are empty.

## Template Field Requirements

### Shared letter/report fields

All report-style templates should expose:

- Our Ref
- Your Ref
- Date
- Addressee or FAO line
- Salutation
- Matter / RE line
- Prepared by / signatory
- Optional subtitle
- Optional closing wording

Core continues to render the shared letterhead and footer. Body templates do not draw page
furniture.

### Repairable / Contract Repair Report

Fields:

- Shared letter/report fields.
- Subject vehicle: registration, make, model, derivative, body type, fuel, transmission, engine,
  first registered, mileage, colour, vehicle history, VIN.
- Assessment summary table: status, repair cost, legal status, impact magnitude, estimate basis,
  contract repair position, engineer value where needed.
- Images: vehicle image, impact-area image, optional damage images with captions.
- Narrative text boxes: instruction paragraph, nature of incident, engineer comments, repair
  position, conclusion.
- Signature.

### Total Loss Report

Fields:

- Shared letter/report fields.
- Subject vehicle.
- Assessment summary table: status, category, salvage value, repair cost, legal status, engineer
  value, impact magnitude.
- Images: vehicle image, impact-area image, optional damage images with captions.
- Narrative text boxes: instruction paragraph, nature of incident, engineer comments, settlement.
- Computed or manually entered settlement figure: engineer value less salvage.
- Signature.

### Market Valuation Evidence

Fields:

- Shared meta fields.
- Subject vehicle.
- Intro text.
- Assessed retail value.
- Guide value and valuation mode.
- Market research text.
- Comparable advert repeater:
  source, URL, advert ID, date accessed, price, make, model, derivative/engine, registration year,
  mileage, fuel, transmission, body style, seller type, location, comparability note, differences
  note, report comment, evidence role, materially comparable flag, supports assessed value flag.
- Evidence assessment basis and sufficient-for-PDF flag.
- Valuation commentary paragraphs.
- Conclusion.
- VAT note.
- Signature.

The prior validation policy in `report-renderer/.../validate_evidence_pack.py` is the reference for
required valuation and advert fields.

### Advert Evidence Pack

Fields:

- Shared meta fields.
- Subject vehicle.
- Intro text.
- Search summary.
- Comparable advert repeater matching the valuation fields.
- Per-advert evidence upload:
  captured advert PDF, screenshot/image, date captured, source URL.

Rendering behaviour:

- The first section remains the linked advert-reference table.
- Uploaded advert PDFs/images are appended in advert order after the table when present.
- Missing captures should warn or fail according to the selected evidence-pack mode.

### Addendum Report

Fields:

- Shared letter/report fields.
- Original report reference/date.
- Challenge or instruction summary.
- Editable section repeater: heading, paragraph blocks, bullet blocks, optional evidence table.
- Optional supporting images/evidence.
- Conclusion.
- Signature.

### Diminution Rebuttal

Fields:

- Shared letter/report fields.
- Opposing assessment summary.
- Intro/opinion text.
- Fixed default section headings from the rebuttal reference, each with editable paragraph/bullet
  text.
- Optional additional section repeater.
- Closing position.
- Signature.

No image slot is mandatory for the rebuttal template.

### Roadworthy / Criminal Report

Fields:

- Shared letter/report fields.
- Court/instructing-party details.
- Subject vehicle.
- Inspection date/location/method.
- Defect or damage findings repeater.
- Roadworthiness/compliance conclusion.
- Images: vehicle image, defect/damage images, optional impact-area image.
- Declaration/signature.

### Part 35 Responses

Fields:

- Shared letter/report fields.
- Original report reference/date.
- Schedule date.
- Question/reply repeater.
- Optional documents reviewed list.
- Closing statement.
- Signature.

### Response Letter

Fields:

- Shared letter/report fields.
- Without-prejudice toggle.
- Recipient/addressee.
- Matter / RE line.
- Opening paragraph.
- Body paragraph repeater.
- Optional bold independence line.
- Closing and signature.

### Fee Note

Fields:

- Fee note number.
- Date, Our Ref, Your Ref.
- Bill-to party: name, address lines, reference.
- Matter reference.
- Subject vehicle summary.
- Line-item repeater: description, detail, amount.
- VAT rate.
- VAT number.
- Payment details.
- Notes/terms.

The renderer computes subtotal, VAT and total from line items.

## Upload Handling

Uploads are case data. They must never be copied into the repository.

Rules:

- GUI uploads are selected from disk and held in the draft as external paths or session-local temp
  copies.
- Render-time Core resolves supported image files to data URIs, as `mediarow` does today.
- Supported image formats: PNG, JPEG and WebP if Chromium renders it reliably; GIF only if there is
  a clear need.
- PDF evidence uploads are accepted for advert evidence packs and appended after the evidence table
  in advert order.
- Missing files fail validation with a clear field path.
- Oversized uploads produce a validation warning or failure before Chromium render.
- The saved draft may store paths or embedded base64 according to a user-selected mode. Default is
  path references to avoid duplicating large client material.
- API clients can submit image/PDF attachments as base64 descriptors or multipart form data.
- CLI clients can submit attachment paths in JSON.

## GUI Target Experience

The desktop app now works as a document authoring workspace:

1. Left rail: selectable authoring templates grouped by Reports, Valuation, Correspondence and Fee
   Notes.
2. Main pane: blank form sections generated from Core form definitions.
3. Tables/repeaters: add/remove rows for adverts, fee items, findings, sections and questions.
4. Upload slots: file picker, thumbnail or file badge, caption/note fields, remove/replace.
5. Render controls: density, Render, Save PDF, Open PDF.
6. Draft controls: New blank, Open draft, Save draft. "Load sample" moves to an advanced/developer
   menu, not the primary authoring flow.
7. Preview pane/tab: existing WebView2 PDF preview after render.
8. Validation: field-level messages for missing required fields and a summary bar before render.

The GUI may keep a raw JSON view as an advanced diagnostic view, but it must not be the default
workflow.

## CLI And API Parity

Core owns the authoring catalogue, so every surface can expose the same capabilities.

CLI additions delivered:

```text
collisionrenderer forms list
collisionrenderer forms blank --template <authoring-id> [--out draft.json]
collisionrenderer forms schema --template <authoring-id> [--out schema.json]
collisionrenderer render --template <render-id> --data draft.json [--out file.pdf]
```

API additions delivered:

| Method | Path | Purpose |
| --- | --- | --- |
| `GET` | `/v1/authoring-templates` | List selectable blank templates. |
| `GET` | `/v1/authoring-templates/{id}/form` | Return the Core-owned form definition. |
| `GET` | `/v1/authoring-templates/{id}/blank` | Return the blank draft payload. |
| `POST` | `/v1/render` | Continue to render JSON/base64 payloads. |
| `POST` | `/v1/render.multipart` | Render a payload plus uploaded image/PDF parts. |
| `POST` | `/v1/render/batch` | Render many payloads in one request. |

The existing template endpoints can remain for backwards compatibility.

## Implementation Phases

### Phase 1 - Reference-backed authoring catalogue

Status: Done

- Added `IAuthoringTemplateCatalog` and descriptor types in Core.
- Added authoring descriptors for the target document set.
- Added blank payload factories and form definitions for market valuation, advert evidence pack,
  fee note, generic expert report and the report/correspondence presets.
- Added tests that every authoring descriptor maps to a valid render template and has a blank
  payload.

### Phase 2 - GUI form renderer

Status: Done

- Replaced the default JSON editor with a schema-driven WinUI form renderer.
- Implemented text, multiline, date, money, select, checkbox, repeater, table, Part 35
  question/answer, signature and upload controls.
- Added New blank, Open draft and Save draft controls.
- Kept JSON as an advanced view with sample loading moved there.
- Added UI tests for the guided form surface, draft controls, JSON diagnostics and render path.

### Phase 3 - Upload pipeline

Status: Done

- Added attachment descriptors and attachment policies to Core.
- Extended image resolution to support local paths and data URIs for reports, evidence captures and
  custom signatures.
- Added PDF append/merge support for advert evidence packs.
- Added GUI upload slots and API multipart support.
- Added validation and tests for missing, unsupported and oversized files.

### Phase 4 - Reference document templates

Status: Done

- Implemented `total-loss-report`, `diminution-rebuttal`, `part-35-response`, `response-letter`,
  `addendum-report`, `repairable-contract-repair-report` and `roadworthy-criminal-report` as
  first-class render ids.
- Reused the existing expert-report block renderer where the letter/report layout matches the
  reference set, with fixed authoring schemas per document family.
- Added synthetic, non-PII completed payloads for tests.
- Added real-Chromium integration renders for every template.

### Phase 5 - Fidelity and regression checks

Status: Done

- Added visual regression tooling that can compare local renders against approved synthetic
  snapshots or local reference PDFs without committing the reference files.
- Stored generated candidates and approvals under ignored `artifacts/visual-regression/`.
- Kept exact reference PDFs and exact filenames in ignored local material only.

## Acceptance Criteria

The work is complete because:

- A non-technical user can open the GUI, select a reference document type and see blank labelled
  fields rather than JSON.
- The user can attach required images/evidence in templates that need them.
- Pressing Render produces the correct branded PDF with the shared letterhead, footer, section
  rules, tables, value boxes, signatures and page behaviour.
- Blank drafts, form schemas and render rules are all owned by Core.
- CLI and API can obtain the same blank templates and render the same payloads.
- No reference PDFs, extracted client text, claim details or registration identifiers are committed.
- The exact local reference inventory remains available at `artifacts/reference-material-inventory.md`
  for the current workstation only.
