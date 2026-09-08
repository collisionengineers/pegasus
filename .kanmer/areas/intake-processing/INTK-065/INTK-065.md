---
id: INTK-065
type: ticket
title: Refresh principal evidence source inventory after policy consolidation
status: done
area: intake-processing
order: 0
assignee: codex-mcp-client
profile: fix
stageEntered:
  preparing: '2026-09-08T07:15:57.942Z'
  review: '2026-09-08T14:45:48.793Z'
  verifying: '2026-09-08T15:28:33.019Z'
  done: '2026-09-08T16:10:29.158Z'
labels:
  - regression
  - source-inventory
groups:
  - EPIC-014
links:
  - TICK-035
  - INTK-060
refs:
  - docs/frd/frd-09-provider-and-intermediary-routes.md
commits:
  - d76de2534ec6651c1a434a55f76593b7b140bf1c
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/706'
deployment: n/a
delivery_state: integrated
delivery_branch: dev
delivery_sha: d76de2534ec6651c1a434a55f76593b7b140bf1c
delivery_recorded_at: '2026-09-08T16:11:45.657Z'
archived: false
created: '2026-09-08T07:15:35.565Z'
updated: '2026-09-08T16:12:22.861Z'
---

## What

Correct the principal-identification evidence generator and tracked package's current source inventory after the accepted policy consolidation. Preserve immutable originals and historical evaluation/cohort data.

## Why

PR700's unit lane exposes source inventory drift inherited from [[TICK-035]]: three deleted policy paths and a changed classification-contract hash. The otherwise hash-equal QDOS extraction source also retains an obsolete v7 ID after [[INTK-060]] moved it to v8. One correction must update generator mappings, reference IDs, generated metadata and the existing verification expectations together.

## Approach

- Reuse the existing generator and hash-mode conventions; no runtime or corpus-original changes.
- Distinguish current source links from the dossier's historical review baseline; do not promote evidence or rewrite the v5 evaluation.
- Limit the change to generator, tracked JSON, existing Core test and principal-rules README clarification.
- This is an EPIC-014 supplemental follow-up. The original frozen 218-ticket roster stays unchanged; root owns the new single-ticket supplemental execution record.

## Verification

- [x] Existing-helper affected-inventory regeneration is deterministic; tracked current-source paths/hashes/byte counts and evidenceRefs resolve, while historical sections/original hashes remain unchanged.
- [x] Focused PrincipalIdentificationCorpusTests passed without dropped coverage, exclusions or fabricated old paths; exact-merge proof retains the full-original-input regeneration limitation.

## Outcome

PR #706 squash-merged into `dev` as
`d76de2534ec6651c1a434a55f76593b7b140bf1c`. The author commit
`5e0aeb47b9cb87258e12f66967e1efee4743b3f3` remains provenance; the merged
SHA is the reachable integration record. The schema-2 proof records all nine
typed attempts, including the retained harness exit 1 caused by an incorrect
reference-count premise and its later inspected correction that passed against
the same merged code/package.

The review erratum, pre-merge failures, materialization supersession, and
report/command transcription corrections remain in their recorded history.
Full original-input corpus regeneration remains unavailable on this host and is
not claimed as PASS. The material pre-merge refresh harness and focused TRX,
plus exact-merge Core TRX and corrected harness, are retained outside the
disposable worktree with verified hashes. This is a non-deployable inventory
correction (`n/a`): no deployment, release promotion, or main update occurred.
