---
id: INTK-065
type: ticket
title: Refresh principal evidence source inventory after policy consolidation
status: implementing
area: intake-processing
assignee: codex-mcp-client
profile: fix
stageEntered:
  preparing: '2026-09-08T07:15:57.942Z'
taken_at: '2026-09-08T13:43:28.206Z'
branch: INTK-065-principal-evidence-inventory
worktree: .worktrees/intk-065
claim_expires_at: '2026-09-08T14:04:43.342Z'
claim_controller: codex-mcp-client
lease_id: 67bf01af-2c70-47df-a295-5ddca0f3fd7e
lease_revision: 2
lease_workspace: 'worktree:c:\users\alex\documents\github\pegasus\.worktrees\intk-065'
lease_phase: running-command
lease_heartbeat_at: '2026-09-08T13:49:43.342Z'
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
archived: false
created: '2026-09-08T07:15:35.565Z'
updated: '2026-09-08T13:49:43.342Z'
---

## What

Correct the principal-identification evidence generator and tracked package's current source inventory after the accepted policy consolidation. Preserve immutable originals and historical evaluation/cohort data.

## Why

PR700's unit lane exposes source inventory drift inherited from [[TICK-035]]: three deleted policy paths and a changed classification-contract hash. The otherwise hash-equal QDOS extraction source also retains an obsolete v7 ID after [[INTK-060]] moved it to v8. One correction must update generator mappings, reference IDs, generated metadata and the existing verification expectations together.

## Approach

- Reuse the existing generator and hash-mode conventions; no runtime or corpus-original changes.
- Distinguish current source links from the dossier's historical review baseline; do not promote evidence or rewrite the v5 evaluation.
- Limit the proposed change to generator, tracked JSON, existing Core test and principal-rules README clarification.
- This is an EPIC-014 supplemental follow-up. The original frozen 218-ticket roster stays unchanged; root owns the new single-ticket supplemental execution record. Preparation only until root approves the complete plan and current ownership.

## Verification

- [ ] Existing-helper affected-inventory regeneration is deterministic; every tracked source path/hash/byte count resolves, all evidenceRefs resolve, and historical sections/original hashes remain unchanged. Full original-input regeneration is unavailable on this host, not PASS; root approval of this evidence boundary is required.
- [ ] Focused PrincipalIdentificationCorpusTests pass without dropped coverage, exclusions or fabricated old paths; root owns build/test execution.

## Outcome
