---
id: INTK-061
type: ticket
title: Restore durable intake custody and exactly one destination after failures
status: done
area: intake-processing
assignee: codex-mcp-client
profile: fix
stageEntered:
  preparing: '2026-09-07T20:06:18.099Z'
  review: '2026-09-07T21:04:33.926Z'
  verifying: '2026-09-07T21:12:13.772Z'
  done: '2026-09-07T22:01:35.155Z'
taken_at: '2026-09-07T20:09:50.926Z'
branch: INTK-061-intake-recovery
worktree: .worktrees/intk-061
claim_expires_at: '2026-09-07T22:27:00.686Z'
claim_controller: /root
lease_id: caff30a5-8e3d-4009-820d-f471bec4d716
lease_revision: 13
lease_controller_run: 20260907T200500Z-v1-remediation
lease_worker_run: intake_audit
lease_workspace: 'worktree:c:\users\alex\documents\github\pegasus\.worktrees\intk-061'
lease_provider: codex
lease_phase: verifying
lease_heartbeat_at: '2026-09-07T21:57:00.686Z'
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
  - 783b537f189ead88553f940d03df0d1f9558ef75
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/679'
deployment: not-deployed
delivery_state: integrated
delivery_branch: dev
delivery_sha: 783b537f189ead88553f940d03df0d1f9558ef75
delivery_recorded_at: '2026-09-07T22:01:34.348Z'
archived: false
created: '2026-09-07T19:58:09.516Z'
updated: '2026-09-07T22:01:35.155Z'
---

## What

Repair the confirmed PR675 intake regressions at dev 3da60bd0: route custody claims using the incoming artifact identity to its actual table; preserve/retry destination work until association, allocation, Triage and Unidentified registration complete; fail closed on a failed unique Case match; retain exactly one group-level Unidentified outcome and canonical reason; select eligible old grouped-image reconciliation candidates before paging; allow OCR analysis retry after OCR completion.

## Acceptance

Existing production Core/store/Worker paths are repaired without a parallel pipeline or broader Worker permissions. Focused tests reproduce SQL runtime-role custody execution, unique-match failure without duplicate allocation, retry after durable processing, one group-level unidentified result, old-group progress and OCR analysis recovery. Relevant canonical docs and callers agree. No soak/capacity suite. Operator authorized remediation and deployment on 7 September; this ticket prepares an independently reviewable correction before release.

## Outcome

Integrated in dev by PR #679 at 783b537f189ead88553f940d03df0d1f9558ef75.
Independent review passed; root read and approved exact-merge proof
28d0135c21e2f502 (114 Core plus 18 real SQL tests, all exit 0). Historical
compiler/fixture failures remain in the report and proof. Root authorized
Done and validated closeout. Not deployed; no full v1 or provider activation
claim. Existing durable recovery owners were repaired without new grants,
queues, schema or Worker changes. ImageIntakeGroupRouting was the bounded
existing canonical-reason owner; prospective ProcessIntake/Worker changes
were unnecessary. [[TICK-035]] owns additional principal routes; formal Triage
linking, handoff and release remain separately owned in [[EPIC-014]].
