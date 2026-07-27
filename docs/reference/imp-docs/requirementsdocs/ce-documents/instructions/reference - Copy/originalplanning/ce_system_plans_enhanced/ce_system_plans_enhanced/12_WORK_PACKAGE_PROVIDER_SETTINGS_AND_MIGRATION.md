# Work Package 12 - Provider Settings and Migration

## Purpose

Move provider, principal, garage, and mapping configuration out of scattered spreadsheets/JSON into a central, auditable admin area.

## Source files

- `Backup of CE Job Sheet 260429.xlsm`
- `Mapped Principals.xlsx`
- `handover.docx`
- `claudechat.md`
- `Final Format Example 02.json`
- `phase_new_system.md`
- `collision_project_context_pack.zip` / `09_DATA_MODEL_AND_JSON_CONTRACTS.md`

## Configuration sources

### Job sheet `Principals`

Columns to import/represent:

- Solicitor / Work Provider.
- EVA Code.
- Box Code.
- Inbox.
- Solicitors Instructions.
- Drag in to EVA?
- Sent Mino.
- Images location.
- Image based or address.
- Sending Report.

### Job sheet `Garages`

Columns to import/represent:

- Garage.
- Address.
- Email.
- Phone.
- Figures.

### CE Document Mapper `providers.json`

Expected concepts:

- Provider name.
- Detected phrases.
- Field rules.
- Mapping methods/config.
- Engineer Report flag.
- Force postcode/other normalization settings if present.

### `Mapped Principals.xlsx`

Use as onboarding/reference list, including lost causes and known problem providers.

## Step-by-step implementation

### Step 1 - Extract and normalize configuration

1. Export `Principals` sheet to a structured table.
2. Export `Garages` sheet to a structured table.
3. Export `providers.json` once latest file is obtained.
4. Normalize provider names/codes.
5. Match provider names between spreadsheet, JSON presets, and mapped principals.
6. Identify duplicates and mismatches.
7. Create migration report.

### Step 2 - Define provider model

Minimum fields:

- Provider display name.
- Internal code.
- EVA code.
- Box code.
- Default inbox.
- Detection phrases.
- Mapping rules.
- Required files.
- Required fields.
- Image location rule.
- Address/image-based assessment rule.
- Engineer Report flag.
- Active/inactive.
- Notes/instructions.

### Step 3 - Define mapping rule model

Mapping rule fields:

- Provider ID.
- Field key.
- Method.
- Config text.
- Secondary config where needed.
- Rule order.
- Active flag.
- Last tested sample.
- Last success/failure.
- Notes.

Supported methods should initially mirror CE Document Mapper.

### Step 4 - Build admin UI

Admin screens:

1. Provider list.
2. Provider detail.
3. Detection phrases.
4. Field mapping rules.
5. Required fields/files.
6. Engineer Report toggle.
7. Garage list and contacts.
8. Test mapping against sample document.
9. Import/export configuration.
10. Change log.

### Step 5 - Add migration tooling

1. Import existing provider settings.
2. Import Principals sheet.
3. Import Garages sheet.
4. Import mapped principals list.
5. Produce mismatch report.
6. Allow admin to merge/resolve.
7. Export rollback copy.

### Step 6 - Add provider onboarding flow

For a new provider:

1. Add provider name/code.
2. Add detected phrases.
3. Upload sample instruction.
4. Configure field mapping rules.
5. Run extraction test.
6. Confirm required files/fields.
7. Confirm EVA/Box codes.
8. Activate provider.
9. Monitor first live cases.

### Step 7 - Add configuration versioning

1. Every provider setting change creates an audit event.
2. Every rule has version or timestamp.
3. Case extraction stores provider rule version used.
4. Admin can compare old/new rule versions.
5. Admin can revert recent changes.

## Acceptance criteria

1. Provider/principal/garage data can be edited without direct spreadsheet/JSON edits.
2. Existing job sheet and mapper settings can be imported or manually reconciled.
3. Every provider has clear EVA/Box settings where applicable.
4. Mapping rules can be tested against sample documents before activation.
5. Engineer Report providers remain configurable.
6. Settings changes are auditable and reversible.
