---
id: UIIMP-017
type: ticket
title: Use one office-time display and one reproducible Health snapshot state
status: backlog
area: ui-improvement
assignee: ''
profile: fix
labels: []
groups:
  - EPIC-014
links:
  - UIIMP-005
  - PLAT-069
refs:
  - docs/frd/frd-12-operator-experience.md
archived: false
created: '2026-09-08T00:39:38.232Z'
updated: '2026-09-08T00:39:38.232Z'
---

## What

Correct the confirmed Health metrics UTC/ambient-culture rendering and ambiguous Test UI default selector identified in the supplied PR675 reviews and pegasus_pack/current/uiimp-005-snapshot-diagnosis.md. Reuse OperatorLabels.OfficeTime and the existing named populated-mailbox Health scenario. No broad timestamp normalization, global clock change, new harness or full recapture.

## Acceptance

The five metrics instants use the same Europe/London display as the service rows; the snapshot requires the recorded graph_unavailable mailbox scenario and rejects incompatible successful responses. Focused actual page checks and scoped fresh capture/verify/catalogue pass.

## Scope and preservation

Historical UIIMP-005 implemented the underlying Test UI gate via merged PR609 but retains an old foreign claim and superseded PR588 pointer. Preserve it; this ticket owns only the newly diagnosed remaining Health defect. Root alone verifies. No live/cloud/mail changes.
