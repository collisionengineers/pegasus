---
id: PLAT-031
type: ticket
title: Stop warning about EVA hand-off when it is switched off
status: done
area: platform-operations
order: 1640
assignee: ''
profile: fix
stageEntered:
  implementing: '2026-08-21T21:37:53.048Z'
  review: '2026-08-21T22:07:01.563Z'
  verifying: '2026-08-22T03:44:22.682Z'
  done: '2026-08-22T03:44:30.197Z'
labels:
  - regression
  - qdos26008
  - ui
links: []
docs_todo: true
deployment: production
archived: false
created: '2026-08-21T18:17:19.411Z'
updated: '2026-09-01T14:44:33.234Z'
---

## Why

Every editable case shows "EVA hand-off is not switched on." The operator reasonably feared it was blocking progression to review and export.

**It is not.** Verified against the code: review is reached in `EfQueuedCustodyProcessor.CompleteCaseCustodyAsync:445-452` with no EVA condition, and `Pages/Cases/Documents/Export` contains no EVA reference. The reason is `CaseEvaMapping.ActivationGateReason`, emitted whenever `EvaMappingAcceptance` is `Unaccepted` — i.e. the connector is not configured — and it gates only EVA bundle *generation*. It is pure UI noise.

The EVA API is not functional and may never be, so the panel should not be shown at all until it is switched on.

## Fix direction

`EvaHandoffStore.GetPreparationAsync`: when the mapping acceptance is `Unaccepted` and the case has no existing revisions, return `null` so the panel does not render. Where revisions exist, keep showing them — that history stays visible. Leave `ActivationGateReason` in place as the server-side guard: this changes what is *displayed*, not what is *enforced*.

## How to verify

A case with EVA switched off shows no EVA panel; review and export both still work. Record the citations above so the record shows the warning was noise, not a block.
