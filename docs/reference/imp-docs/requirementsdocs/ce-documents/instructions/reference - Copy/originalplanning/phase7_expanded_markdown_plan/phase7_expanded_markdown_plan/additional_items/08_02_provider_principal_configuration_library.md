# 8.2 Provider and Principal Configuration Library

## Purpose

Create one structured library for provider/principal configuration, replacing scattered knowledge across CE Document Mapper presets, the Job Sheet principal tab, EVA codes, Box folder naming rules and staff memory.

## Why this matters

Most automation errors will come from provider-specific differences: instruction format, principal/EVA code, image rules, report delivery rules, VAT/mileage wording and preferred status language. These should be managed as configuration, not hardcoded logic.

## Step-by-step plan

### Step 1 — Consolidate existing sources

1. Export the CE Document Mapper provider presets.
2. Extract principal settings from the Job Sheet principal table.
3. Pull mapped provider examples from the mapped principals workbook.
4. Capture known exceptions, such as providers with inconsistent formats or poor OCR quality.
5. Record any manual notes currently used by staff.

### Step 2 — Define provider configuration fields

1. Provider/principal display name.
2. Internal shorthand/code.
3. EVA principal code.
4. Box folder code or naming rule.
5. Primary inbox/source channel.
6. Instruction file patterns.
7. Engineer-report flag.
8. Document Mapper rules.
9. Image location rules.
10. Report sending rules.
11. Chaser/contact details.
12. SLA/due-date rule.
13. Exceptions and confidence thresholds.

### Step 3 — Add versioning

1. Give every provider configuration a version number.
2. Record who changed it and when.
3. Store a short reason for each change.
4. Preserve old versions so historical cases can be interpreted correctly.
5. Allow rollback if a new mapping breaks extraction.

### Step 4 — Build validation checks

1. Validate required fields before a provider config can be activated.
2. Flag duplicate EVA codes or conflicting provider names.
3. Test extraction rules against a provider-specific sample corpus.
4. Confirm that required output fields match the canonical JSON schema.
5. Require review before activating high-impact changes.

### Step 5 — Provide a configuration UI

1. Keep the UI simple and staff-friendly.
2. Show provider name, code, active/inactive status and last modified date.
3. Allow mapping rules to be edited using the existing CE Document Mapper concepts where practical.
4. Make advanced fields collapsible.
5. Include a test button that runs the config against a sample file.

### Step 6 — Connect to downstream systems

1. Intake uses provider config for classification and routing.
2. Extraction uses provider config for field mapping.
3. Box storage uses provider config for folder naming.
4. EVA adapter uses provider config for principal code and default values.
5. Communications automation uses provider config for recipient and tone/template rules.

## Deliverables

- Provider configuration schema.
- Initial consolidated provider library.
- Versioning and audit log.
- Provider config test harness.
- Simple configuration UI.

## Acceptance criteria

- A provider can be updated without code changes.
- Provider mappings can be tested before activation.
- Every automated decision can show which provider config version was used.
- Inconsistent providers can be marked as review-only instead of fully automated.

## Risks and controls

| Risk | Control |
|---|---|
| Staff accidentally break a provider config | Draft/active versions, test-before-activate and rollback. |
| Provider aliases create duplicates | Alias table with one canonical provider record. |
| Too much configuration complexity | Start with minimum fields required for intake, extraction and EVA. |

## Suggested priority

P0. Provider configuration is a prerequisite for reliable automation at scale.
