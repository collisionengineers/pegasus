---
id: CASE-031
type: ticket
title: Send the canonical claimant address in EVA API submissions
status: preparing
area: case-reference-workflow
order: 40
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
groups:
  - EPIC-014
links:
  - DOCS-015
  - TICK-085
refs:
  - docs/frd/frd-01-case-identity-and-lifecycle.md
  - docs/frd/frd-02-intake-and-source-identity.md
  - docs/frd/frd-07-eva-and-external-engineering-handoff.md
deployment: not-deployed
archived: false
created: '2026-08-28T16:26:37.834Z'
updated: '2026-09-08T02:25:46.530Z'
---

# Send the canonical claimant address in EVA API submissions

## What and why

EVA requires ClmAdd (maximum 40 characters). Claimant address already extracts,
retains provenance, persists and supports normal Case display/editing. The
remaining defect is that the manual API payload does not carry that existing
canonical value.

## Remaining scope

- Select the existing accepted Case claimant address (Confirmed before Fact)
  through Core's existing status/precedence model.
- Add typed EvaInstructionPayload.ClaimantAddress, the API-only mapping input
  and version bump, and exact ClmAdd serialization.
- Reject missing, unaccepted/suggestion-only, unresolved, whitespace-only,
  control/format-containing and over-limit values before external images or
  EVA submission, after returning any known operation replay.
- Preserve text exactly. No truncation or inspection/other-party substitute;
  ordinary address commas, hyphens and apostrophes remain valid.
- Prove the existing manual store caller, exact JSON, zero calls/mutations on
  refusal and known replay, with existing focused fixtures.
- Clarify only the direct-API prerequisite in FRD-07 after root sequences its
  shared ownership with [[TICK-085]].

## Exclusions and historical disposition

No schema, extractor, Case-data or UI overhaul. No change to the 13-field
EvaReplayFields/operator ZIP, its bytes or mapping. No automatic EVA path
(ADR-0038), new readiness policy, credentials/InstEmail/Principal activation,
deployment or live EVA request.

The prior broader research/plan remain historical board versions; their
already-implemented work and automatic/once-only submission promises are not
current instructions. [[DOCS-015]] supplies the vendor guide. Historical
punctuation-only failure probes are not a ban on ordinary address punctuation.

## Acceptance

Accepted canonical value reaches ClmAdd unchanged; invalid input performs no
external image read, transport call, attempt/history write or Case mutation.
Known replay remains the original outcome even when current address is now
unusable. Manual outcomes/re-send/lease/version behavior and the deterministic
ZIP remain unchanged.

## Execution boundary

Preparing and untaken. Root's preparation assignment authorizes board documents
only; execution awaits root sequencing of FRD-07. No product choice is unresolved.

## Outcome
