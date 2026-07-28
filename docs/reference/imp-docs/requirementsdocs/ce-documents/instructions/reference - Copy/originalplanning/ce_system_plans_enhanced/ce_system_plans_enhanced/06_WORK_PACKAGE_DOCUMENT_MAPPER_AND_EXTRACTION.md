# Work Package 06 - Document Mapper and Extraction

## Purpose

Preserve the value of the CE Document Mapper while moving extraction into a platform-ready service with source evidence, validation, and review workflows.

## Source files

- `handover.docx`
- `claudechat.md`
- `Final Format Example 02.json`
- `Mapped Principals.xlsx`
- `collision_project_context_pack.zip` / `08_DATA_EXTRACTION_AND_AI_STRATEGY.md`, `13_TESTING_QA_ACCEPTANCE_CRITERIA.md`
- `collision-engineers-context-pack.zip` / `10_ai_modules_and_prompts.md`, `15_demo_dataset_and_test_plan.md`

## Current mapper behaviours to preserve

From the available source material, CE Document Mapper currently or recently supports:

- Drag-and-drop import.
- PDF, DOC, DOCX, MSG; EML may be supported and should be verified in code.
- Provider presets stored in `providers.json`.
- `app_settings.json` and `providers.json` stored in `Documents\CE Document Mapper`.
- Minimal and expanded views.
- Field order matching `Final Format Example 02.json`.
- Mapping methods including Single Label, Two Labels, Fixed Position, Fixed Position + Label, Single Label +/-, Email Date, Manual Input.
- Batch mode for multiple imports.
- Image extraction from PDF/DOC/DOCX.
- Engineer Report / Audit Mode that overwrites mapped non-blank fields.
- Red outline for overwritten fields in Audit Mode.
- OCR threshold for short image-only PDFs.
- Special normalization rules for VRM, dates, inspection address, VAT status, mileage, and mileage unit.
- OneDrive path handling for Desktop/Documents in later builds.

## Extraction architecture

### Recommended layers

1. **Raw input layer**: original file and source metadata.
2. **Raw text layer**: extracted text exactly as parser sees it.
3. **Candidate extraction layer**: possible field values with snippets.
4. **Normalized field layer**: final values for case model and export.
5. **Evidence layer**: page/line/block/snippet/source document.
6. **Review layer**: user corrections and confidence decisions.

### Parser strategy by file type

| File type | Primary strategy | Fallback | Notes |
|---|---|---|---|
| PDF | PyMuPDF text blocks where available | OCR for image-only, pypdf as fallback | Block mode improves table extraction. |
| DOCX | `python-docx` and ZIP media extraction | LibreOffice conversion if needed | Preserve headers/footers where relevant. |
| DOC | Word automation on Windows or LibreOffice conversion | antiword as last resort | Legacy DOC headers/footers must be tested. |
| MSG | extract-msg / Outlook fallback | Graph message metadata where available | Email Date method handles headers. |
| EML | Python email parser | HTML-to-text conversion | Verify actual need. |
| Images | OCR only when instruction-like | Do not OCR bulk vehicle image sets by default | Prevent expensive false work. |

## Step-by-step implementation

### Step 1 - Obtain actual mapper source

1. Locate latest `app.py`, `README.md`, `requirements.txt`, and `providers.json` from Matty Files / CE Document Mapper.
2. Confirm current version number and differences against `handover.docx` and `claudechat.md`.
3. Add code to source control if not already present.
4. Create a branch/tag representing the current production mapper.

**Exit criteria:** current mapper is reproducible from source.

### Step 2 - Build parser test harness

1. Create automated tests for each supported file type.
2. Add the real sample corpus from discovery.
3. Add gold expected raw text snippets and expected final field values.
4. Add regression tests for known fixes: DOC header/footer, PDF table block extraction, Email Date, mileage extraction, VAT/mileage-unit presence checks, inspection address normalization.
5. Add image extraction tests.
6. Add batch/audit mode tests.

**Exit criteria:** parser behaviour can be changed safely.

### Step 3 - Wrap mapper logic as a service

Choose one of two approaches:

**Option A - Bridge the existing Python mapper**

- Import its extraction engine into a backend service.
- Expose `parse_document(file, provider_context)` returning structured output.
- Keep UI-specific Tkinter code out of the service.

**Option B - Rebuild extraction rules into platform service**

- Re-implement mapping methods as backend functions.
- Import existing `providers.json` into a new ProviderRule table.
- Use CE Document Mapper only as the behavioural reference.

Recommended: start with Option A if source is cleanly separable; move to Option B if Tkinter coupling makes the bridge fragile.

### Step 4 - Define extraction output contract

Example:

```json
{
  "schema_version": "1.0",
  "document_id": "doc_123",
  "provider_detected": "SBL",
  "document_type": "instruction",
  "raw_text_id": "raw_123",
  "fields": {
    "vrm": {
      "raw_value": "RJ62 RTU",
      "normalized_value": "RJ62RTU",
      "confidence": 0.96,
      "status": "found",
      "source": {
        "document_id": "doc_123",
        "page": 1,
        "snippet": "Vehicle Registration: RJ62 RTU"
      }
    }
  },
  "warnings": []
}
```

### Step 5 - Add provider detection

1. Import detected phrases from existing provider presets.
2. Add exact phrase matching first.
3. Add provider-specific fallback only when explicitly configured.
4. Track provider detection confidence.
5. Handle ambiguous provider detection with review flag.

### Step 6 - Add document classification

Classify:

- Instruction.
- Estimate.
- Engineer report.
- Image bundle.
- Email body/offline email.
- Invoice.
- Unrelated.

Use deterministic rules first:

- File type.
- Provider phrases.
- Document headings.
- Known EVA/engineer report phrases.
- Estimate terms (Audatex, parts/labour/paint, net/VAT/gross).

Add AI classifier later only as an assist, with required evidence.

### Step 7 - Validate and normalize fields

Rules to carry forward:

- VRM normalized to uppercase, spaces removed.
- Dates normalized for export/payload to DD/MM/YYYY or API-required ISO format.
- Inspection address normalized into controlled lines if still required by downstream process.
- Mileage captures digits and strips commas.
- Mileage Unit output limited to Miles, Km, or blank.
- VAT Status output limited to Yes, No, or blank.

Add validation flags:

- Missing required field.
- Invalid date.
- Mileage value looks unrealistic.
- VRM conflict.
- Provider unknown.
- Address absent and no image-based assessment decision.

### Step 8 - Add evidence matching and conflicts

1. Store all candidate field values from all documents.
2. Compare instruction vs estimate vs engineer report.
3. If engineer report rules apply, mark overwritten fields and preserve original values.
4. For conflicts, do not silently choose unless configured; create review flag.
5. Store who approved the final selected value.

### Step 9 - Add AI-assisted extraction carefully

AI can assist with:

- Document classification.
- Extracting accident circumstances from messy text.
- Identifying missing information.
- Suggesting chaser language.
- Matching images/estimates to cases.

Controls:

- AI output must include quoted/evidence snippets.
- AI cannot submit to EVA directly without review in Phase 2.
- Log prompt version, model/provider, input hash, and output.
- Do not send unnecessary personal data to external AI services.

## Acceptance criteria

1. Existing mapper export fields can be reproduced from the new service.
2. Every extracted field has raw, normalized, status, confidence, and evidence attributes.
3. Provider rules can be migrated from `providers.json` or equivalent.
4. Engineer Report overwrite behaviour is represented in the data model and UI.
5. Parser regression suite passes before any release.
6. Poor OCR or inconsistent provider formats are flagged for review rather than silently accepted.
