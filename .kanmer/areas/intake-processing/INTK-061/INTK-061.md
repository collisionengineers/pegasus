---
id: INTK-061
type: ticket
title: Restore durable intake custody and exactly one destination after failures
status: verifying
area: intake-processing
assignee: codex-mcp-client
profile: fix
stageEntered:
  preparing: '2026-09-07T20:06:18.099Z'
  review: '2026-09-07T21:04:33.926Z'
  verifying: '2026-09-07T21:12:13.772Z'
taken_at: '2026-09-07T20:09:50.926Z'
branch: INTK-061-intake-recovery
worktree: .worktrees/intk-061
claim_expires_at: '2026-09-07T21:40:32.358Z'
claim_controller: /root
lease_id: caff30a5-8e3d-4009-820d-f471bec4d716
lease_revision: 11
lease_controller_run: 20260907T200500Z-v1-remediation
lease_worker_run: intake_audit
lease_workspace: 'worktree:c:\users\alex\documents\github\pegasus\.worktrees\intk-061'
lease_provider: codex
lease_phase: review
lease_heartbeat_at: '2026-09-07T21:10:32.358Z'
labels: []
groups:
  - EPIC-014
links:
  - INTK-060
  - INTK-033
  - INTK-039
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
  - docs/frd/frd-05-documents-extraction-and-custody.md
commits:
  - 3d58d8c58fe57aac5af6091f7ebf0e0b81b5b560
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/679'
archived: false
created: '2026-09-07T19:58:09.516Z'
updated: '2026-09-07T21:12:13.772Z'
---

## What

Repair the confirmed PR675 intake regressions at dev 3da60bd0: route custody claims using the incoming artifact identity to its actual table; preserve/retry destination work until association, allocation, Triage and Unidentified registration complete; fail closed on a failed unique Case match; retain exactly one group-level Unidentified outcome and canonical reason; select eligible old grouped-image reconciliation candidates before paging; allow OCR analysis retry after OCR completion.

## Acceptance

Existing production Core/store/Worker paths are repaired without a parallel pipeline or broader Worker permissions. Focused tests reproduce SQL runtime-role custody execution, unique-match failure without duplicate allocation, retry after durable processing, one group-level unidentified result, old-group progress and OCR analysis recovery. Relevant canonical docs and callers agree. No soak/capacity suite. Operator authorized remediation and deployment on 7 September; this ticket prepares an independently reviewable correction before release.

## Outcome
