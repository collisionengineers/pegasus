---
id: CASE-031
type: ticket
title: Send the canonical claimant address in EVA API submissions
status: review
area: case-reference-workflow
order: 40
assignee: intake_audit
profile: feature
stageEntered:
  preparing: '2026-08-28T17:08:43.378Z'
  review: '2026-09-08T03:13:22.847Z'
taken_at: '2026-09-08T02:31:14.238Z'
branch: CASE-031-eva-claimant-address
worktree: .worktrees/case-031
claim_expires_at: '2026-09-08T03:42:09.405Z'
claim_controller: /root
lease_id: 6841f0dc-8036-4424-85e4-fa38fda4c5ee
lease_revision: 8
lease_controller_run: 20260907T200500Z-v1-remediation
lease_worker_run: intake_audit
lease_workspace: 'worktree:c:\users\alex\documents\github\pegasus\.worktrees\case-031'
lease_provider: codex
lease_phase: review
lease_heartbeat_at: '2026-09-08T03:12:09.405Z'
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
commits:
  - 9863dd4264440ef228a0766d3e2949faf6a4e12b
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/694'
deployment: not-deployed
archived: false
created: '2026-08-28T16:26:37.834Z'
updated: '2026-09-08T03:13:22.847Z'
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

Root has reviewed the complete preparation and actual write-map intersection.
CASE-031 may take its fresh isolated branch/worktree from accepted dev
56566371a5b80ef59c4f98e377c8e8ff6469b5f7. TICK-085 does not write FRD-07;
only this ticket's API paragraph is authorized. No product choice is unresolved.

## Outcome
