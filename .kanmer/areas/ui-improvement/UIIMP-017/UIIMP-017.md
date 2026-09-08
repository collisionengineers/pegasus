---
id: UIIMP-017
type: ticket
title: Use one office-time display and one reproducible Health snapshot state
status: done
area: ui-improvement
assignee: codex-mcp-client
profile: fix
stageEntered:
  preparing: '2026-09-08T00:42:19.213Z'
  review: '2026-09-08T02:33:30.782Z'
  verifying: '2026-09-08T02:38:06.618Z'
  done: '2026-09-08T02:45:34.531Z'
labels: []
groups:
  - EPIC-014
links:
  - UIIMP-005
  - PLAT-069
refs:
  - docs/frd/frd-12-operator-experience.md
commits:
  - cdaa02584c38ecc27d3bd24784f59da189138bc1
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/693'
deployment: not-deployed
delivery_state: integrated
delivery_branch: dev
delivery_sha: cdaa02584c38ecc27d3bd24784f59da189138bc1
delivery_recorded_at: '2026-09-08T02:48:54.003Z'
archived: false
created: '2026-09-08T00:39:38.232Z'
updated: '2026-09-08T02:50:48.373Z'
---

## What

Correct the confirmed Health metrics UTC/ambient-culture rendering and ambiguous Test UI default selector identified in the supplied PR675 reviews and pegasus_pack/current/uiimp-005-snapshot-diagnosis.md. Reuse OperatorLabels.OfficeTime and the existing named populated-mailbox Health scenario. No broad timestamp normalization, global clock change, new harness or full recapture.

## Acceptance

The five metrics instants use the same Europe/London display as the service rows; the snapshot requires the recorded graph_unavailable mailbox scenario and rejects incompatible successful responses. Focused actual page checks and scoped fresh capture/verify/catalogue pass.

## Scope and preservation

Historical UIIMP-005 implemented the underlying Test UI gate via merged PR609 but retains an old foreign claim and superseded PR588 pointer. Preserve it; this ticket owns only the newly diagnosed remaining Health defect. Root alone verifies. No live/cloud/mail changes.

## Outcome

Completed in PR693, merged to dev at
cdaa02584c38ecc27d3bd24784f59da189138bc1 on 8 September 2026 02:37:21Z.
Health's five metrics use the existing London formatter; the named populated
snapshot state and existing route/selector tests agree. Root independently
reviewed and verified the exact merge; Done accepted with PASS. Earlier
CA1859 build and exact-whitespace assertion failures remain in the report and
proof, with separate failing/passing evidence retained under
pegasus_pack/current/proofs/UIIMP-017. No manual visual, non-UK request-culture,
hosted CI, live mailbox/provider, cloud or deployment claim. Delivery is
integrated/dev, not deployed; final converged release remains separately owed.
No implementation-scope deviation or new successor. [[UIIMP-005]] historical
record remains untouched; [[TICK-085]] can use the generated index once this
claim is released after scoped Git cleanup.
