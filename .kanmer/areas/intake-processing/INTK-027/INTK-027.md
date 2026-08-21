---
id: INTK-027
type: ticket
title: Make policy re-evaluation work after transient staging cleanup
status: backlog
area: intake-processing
assignee: ''
profile: fix
labels:
  - defect
  - intake
  - reevaluation
  - live-found
links: []
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
archived: false
created: '2026-08-21T15:04:57.139Z'
updated: '2026-08-21T15:04:57.139Z'
---

# Why

Found during release-16 live verification (2026-08-21): "Re-evaluate with current policy" on `/Received/{id}` queues the receipt's `IntakeWorkItems` row back to pending, but the Worker's re-processing needs the staged source blob (`staging/{stagedReceiptId}/{hash}` in `transient-intake`) — and `DeleteCompletedStagedAsync` deletes that blob when processing completes, by design. Result: re-evaluation of any completed receipt fails after 2 attempts with `staged_artifact_integrity_failure`, and the receipt is left `blocked_intake` with `reevaluation_pending` → a cryptic failed state. Observed live on receipt `48311398-C284-4000-BD38-15F4449CE05B` (EREF24 shape); `transient-intake` holds 0 `staging/` blobs, so every processed receipt is affected.

The control's contract (versioned re-evaluation retained in permanent history) is sound; the source lifecycle contradicts it.

# Direction (for research)

Either re-stage the source from the retained custody/search copy of the original `.eml` before dispatch (the durable retained source exists and is hash-verified), or refuse the control honestly when no staged source exists (no doomed queue, no blocked_intake side effect). Fail-closed stays; the silent-degradation is the defect.

# How to verify

Re-evaluating a completed receipt either completes under the current policy versions (draft re-resolved, history appended) or is refused with an honest operator-visible reason before any state change; a receipt is never stranded in `blocked_intake` by a re-evaluation that cannot run.

# Outcome

(open)
