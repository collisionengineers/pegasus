# 8.10 Test Corpus and Regression Harness

## Purpose

Create a controlled set of sample instructions, reports, emails, images, estimates and expected outputs so every change to extraction, mapping, provider config, EVA mapping or AI prompts can be tested before release.

## Why this matters

The project has already seen format-specific improvements, such as PDF block extraction and legacy DOC header/footer extraction. A regression harness protects those gains and prevents new changes from silently breaking existing providers.

## Step-by-step plan

### Step 1 — Build the corpus

1. Collect representative examples for each major provider.
2. Include PDFs, DOCX, DOC, EML, MSG and estimate files.
3. Include Engineer Report companion documents.
4. Include negative examples and low-quality/OCR-problem files.
5. Redact or create synthetic equivalents where required.

### Step 2 — Define expected outputs

1. Store expected canonical JSON for each sample.
2. Store expected warnings/missing fields.
3. Store expected extracted image counts where relevant.
4. Store expected provider detection outcome.
5. Store known acceptable variations where strict equality is inappropriate.

### Step 3 — Create automated tests

1. Run extraction for each sample.
2. Compare to expected outputs.
3. Test normalisation rules for VRM, address, mileage, VAT and mileage unit.
4. Test Engineer Report merge/overwrite behaviour.
5. Test JSON export blocking rules and batch behaviours.

### Step 4 — Add provider configuration tests

1. Run each active provider config against its sample documents.
2. Fail the test if required fields are missing unexpectedly.
3. Flag any output change after provider config edits.
4. Require sign-off before activating a changed provider config.

### Step 5 — Add integration tests

1. Mock Microsoft Graph/Outlook intake.
2. Mock Box file storage.
3. Mock EVA/Sentry API responses.
4. Test retries, duplicate prevention and validation errors.
5. Test full flow from source file to ready_for_eva.

### Step 6 — Make testing part of release

1. Every code release runs the test harness.
2. Every provider-config release runs provider tests.
3. Every prompt/template release runs sample-case review checks.
4. Produce a release summary listing pass/fail results.
5. Keep old results for audit.

## Deliverables

- Redacted/synthetic test corpus.
- Expected-output JSON files.
- Automated regression runner.
- Provider-config test suite.
- Release test report template.

## Acceptance criteria

- A developer can run the full test suite before shipping a new version.
- Extraction changes show exactly which fields changed.
- Provider config changes cannot be activated without sample validation.
- Edge cases such as Engineer Report merging and missing Work Provider are tested.

## Risks and controls

| Risk | Control |
|---|---|
| Corpus contains personal data | Redact, minimise and secure test data. |
| Tests become stale | Add new failure examples as they occur. |
| Tests only cover “easy” cases | Include difficult providers and known broken formats. |

## Suggested priority

P0. Required before expanding automation beyond the desktop mapping tool.
