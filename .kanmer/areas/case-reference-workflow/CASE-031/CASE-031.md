---
id: CASE-031
type: ticket
title: 'Extract, retain, display and submit claimant addresses'
status: preparing
area: case-reference-workflow
assignee: ''
profile: feature
stageEntered:
  preparing: '2026-08-28T17:08:43.378Z'
labels:
  - claimant-address
  - intake
  - case-data
  - eva
  - api-submission
links:
  - DOCS-015
refs:
  - docs/frd/frd-01-case-identity-and-lifecycle.md
  - docs/frd/frd-02-intake-and-source-identity.md
  - docs/frd/frd-07-eva-and-external-engineering-handoff.md
deployment: not-deployed
archived: false
created: '2026-08-28T16:26:37.834Z'
updated: '2026-08-28T17:48:41.298Z'
---

# Extract, retain, display and submit claimant addresses

## Why

EVA requires ClmAdd on POST /Instruction/Inspection, but Pegasus does not
currently extract, retain or send a claimant address. Where received evidence
contains one, Pegasus should preserve it through intake and Case storage so
operators can review it and EVA API submission can use the same canonical
value. Absence or ambiguity remains explicit; no value is fabricated to satisfy
EVA.

## Scope

- Extract claimant-address evidence from supported intake sources where it is
  explicitly identified.
- Retain the value and provenance through intake processing and Case
  persistence.
- Display and edit claimant address on the existing Case surface under normal
  concurrency and audit rules.
- Send the canonical claimant address as EVA ClmAdd.
- Block EVA API submission locally when the address is absent, ambiguous,
  whitespace-only or outside the API contract.
- Do not change the operator ZIP/export format.

## Acceptance

- A supported fixture with an unambiguous claimant address proves extraction,
  provenance and durable storage.
- The Case surface displays the stored value and audited edits use existing
  Case rules.
- API contract tests prove the canonical value is emitted as ClmAdd.
- Missing, conflicting, whitespace-only and over-limit values produce no
  fabricated substitute and no EVA network request.
- Regression tests prove intake absence, normalization, persistence, display
  and API mapping.
- The existing EVA ZIP remains byte-for-byte and schema compatible.

## Evidence context

Controlled EVA test-environment requests on 2026-08-28 confirmed ClmAdd is
required. Null, empty and whitespace values receive HTTP 400; punctuation and
invisible control/format characters produce opaque HTTP 500 and are not
acceptable placeholders. See [[DOCS-015]] for the normalized vendor guide.

## Outcome
