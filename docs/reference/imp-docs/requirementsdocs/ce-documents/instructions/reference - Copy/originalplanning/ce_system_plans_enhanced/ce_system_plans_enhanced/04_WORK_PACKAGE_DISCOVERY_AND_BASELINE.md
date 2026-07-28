# Work Package 04 - Discovery and Baseline

## Purpose

Establish the factual baseline before build work. This work package prevents the new platform from encoding assumptions that differ from the real Collision Engineers workflow.

## Source files

- `handover.docx`
- `Backup of CE Job Sheet 260429.xlsm`
- `Backup of Conditional Formatting 260202.txt`
- `Mapped Principals.xlsx`
- `Final Format Example 02.json`
- `EVA User Guide.pdf`
- `Sentry_API_Complete_Guide.md`
- `evaapidocs.pdf`
- `CE Communication Style & Tone Profile.docx`
- `Collision Engineers Whiteboard.jam`
- `collision_project_context_pack.zip` / `02_CURRENT_STATE_AND_MANUAL_PROCESS.md`, `17_OPEN_QUESTIONS_AND_DECISION_LOG.md`
- `collision-engineers-context-pack.zip` / `14_discovery_questions_and_call_prep.md`, `15_demo_dataset_and_test_plan.md`

## Outputs

1. Current-state workflow map.
2. System inventory and integration inventory.
3. Data-field catalogue.
4. Provider/principal/garage catalogue.
5. Case sample corpus.
6. Risk and decision log.
7. MVP scope confirmation.

## Step-by-step plan

### Step 1 - Confirm the operational workflow

1. Sit with intake/admin staff and observe a normal instruction from email receipt to EVA entry.
2. Record what is copied from email, instruction document, image email, estimate, Box, job sheet, and EVA.
3. Record all “judgment” decisions: when a case is put into awaiting information, when images are good enough, when address is image-based, when a chaser is sent, when a case is ready for EVA.
4. Record all exception paths: missing images, estimate only, wrong VRM, duplicate VRM, engineer report/audit mode, provider not mapped, OCR poor quality.
5. Compare observed steps against `EVA User Guide.pdf` and `handover.docx`.

**Acceptance:** current workflow can be drawn as a step sequence with actors, systems, and handoffs.

### Step 2 - Catalogue the job sheet

1. Open `Backup of CE Job Sheet 260429.xlsm` using a spreadsheet-aware tool or Excel Desktop.
2. Document `Jobs` sheet sections, visible columns, hidden/helper columns, formulas, buttons, and table locators.
3. Record the meaning of `T1_START` and `T2_START` from column AA.
4. Document `Principals` sheet columns and how they map to provider settings.
5. Document `Garages` sheet columns and how they map to garage/contact settings.
6. Capture ageing, duplicate, and due-date logic from `Backup of Conditional Formatting 260202.txt`.

**Acceptance:** a replacement dashboard can be traced back to every relevant job-sheet column or rule.

### Step 3 - Catalogue CE Document Mapper behaviour

1. Review `handover.docx` and `claudechat.md` for implemented mapper behaviour.
2. Confirm supported input types: PDF, DOC, DOCX, MSG, and any EML support in the actual code if code becomes available.
3. Record canonical export fields from `Final Format Example 02.json`.
4. Record mapping methods: Single Label, Two Labels, Fixed Position, Fixed Position + Label, Single Label +/-, Email Date, Manual Input.
5. Record special behaviours: provider detection, batch mode, export images, engineer-report/audit mode, OCR threshold, date/mileage/VAT/mileage-unit normalization, OneDrive Documents/Desktop path handling.
6. Identify what cannot be verified because the actual latest `app.py` is missing.

**Acceptance:** the platform can reproduce or call out every mapper behaviour that must be preserved.

### Step 4 - Build the sample corpus

Create a controlled corpus covering:

1. Standard complete instruction.
2. Instruction with separate image email.
3. Instruction with missing images.
4. Instruction with estimate attached.
5. Estimate or image email arriving before instruction.
6. Duplicate VRM/case reference.
7. Wrong or conflicting VRM between instruction and estimate.
8. Engineer report overwriting selected fields.
9. Scanned/OCR PDF.
10. Image-only DOCX/PDF.
11. MSG with header date.
12. Provider with poor OCR or inconsistent format from `Mapped Principals.xlsx` lost causes.
13. Current key providers from `Mapped Principals.xlsx` and `Principals` sheet.

For each sample, store:

- Source documents.
- Expected JSON fields.
- Expected document classification.
- Expected missing-info flags.
- Expected Box folder/file outcome.
- Expected EVA payload fields.
- Expected human-review outcome.

**Acceptance:** at least 20 real or realistic sample cases with gold expected outputs.

### Step 5 - Catalogue EVA/Sentry requirements

1. List which manual EVA fields are essential from `EVA User Guide.pdf`.
2. Map CE field names to Sentry API fields using `Sentry_API_Complete_Guide.md` and `evaapidocs.pdf`.
3. Confirm whether production supports API submission, sandbox submission, or only manual/JSON export initially.
4. Confirm accepted values, required field combinations, duplicate behaviour, and error responses.
5. Decide whether Phase 2 will submit to EVA or only generate a validated EVA-ready payload.

**Acceptance:** EVA field map and integration decision recorded.

### Step 6 - Catalogue communication requirements

1. Review `CE Communication Style & Tone Profile.docx`.
2. Draft chaser templates for missing images, missing estimate, unclear repairer/garage contact, and mismatched reference.
3. Confirm which messages are safe to auto-draft and which require human approval.
4. Confirm shared mailbox access and whether Copilot/delegated inbox patterns affect the build.

**Acceptance:** chaser templates and approval rules agreed.

### Step 7 - Decide MVP boundaries

Use the discovery data to mark each feature as:

- Phase 2 must-have.
- Phase 2 should-have.
- Phase 6 expansion.
- Out of scope.

Recommended Phase 2 must-haves:

1. Intake/upload.
2. Store files and metadata.
3. Extract canonical fields.
4. Holding pen/review dashboard.
5. Missing information flags.
6. Evidence matching by VRM/reference/provider.
7. Box folder/file management or at minimum Box metadata plan.
8. EVA-ready payload preview.
9. Audit log.

Recommended Phase 6 expansions:

1. Fully automated multi-mailbox Graph ingestion.
2. Engineer PWA.
3. Report submission.
4. Advanced AI classification/matching.
5. Analytics and decommissioning.

## Deliverables checklist

- [ ] Current-state workflow diagram.
- [ ] Source systems and integrations inventory.
- [ ] Field map: CE Document Mapper export -> case model -> EVA/Sentry field.
- [ ] Provider/principal/garage catalogue.
- [ ] Sample corpus and expected outputs.
- [ ] Decision log.
- [ ] Phase 2 MVP scope statement.
- [ ] Phase 6 expansion backlog.

## Risks

| Risk | Mitigation |
|---|---|
| Discovery misses informal workarounds | Observe real cases and interview more than one user. |
| Sample corpus is too clean | Include negative and messy examples. |
| Actual latest mapper code is unavailable | Treat `handover.docx` and `claudechat.md` as behavioural source, but mark code-level items as unverified. |
| EVA API differs from docs | Validate with sandbox or vendor before committing automation. |
