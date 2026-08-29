---
id: AUTO-013
type: ticket
title: >-
  API-01 residuals: provider principal absent from the case-data snapshot, and
  paused credentials read the body
status: backlog
area: automation-integrations
assignee: ''
profile: fix
labels:
  - API-01
groups:
  - EPIC-011
links:
  - TICK-058
  - AUTO-012
archived: false
created: '2026-08-29T08:35:46.076Z'
updated: '2026-08-29T08:35:46.076Z'
---

## What

Two confirmed-live defects in the API-01 surface that [[TICK-058]] deferred.
They were recorded as deferrals with no ticket, which rule 22 does not allow;
this is that ticket.

### 1. A provider-created case records no work provider

`CaseDataSnapshotFactory.AddProviderFact` returns early unless the receipt
carries an **accepted `MailRouteDecision` with a work-provider code**. A
Provider API receipt has no mail route by design — its Principal comes from the
credential — so `WorkProviderCode` is never written to the case-data snapshot,
and the EVA export reports Work Provider as unrecorded even though allocation
established the Principal from the submission binding.

Fixing it means deciding the snapshot row's source kind, policy key and version
and source label for a declared instruction (`CaseDataSourceKind.ProviderApi`
already exists and is rendered), and covering it with a persistence test. It
touches `CaseDataSnapshotFactory`, which other EPIC-011 lanes edit this wave.

### 2. A paused credential is refused only after the body is read

`ProviderApiEndpoints` enforces 413 and 415 before the read, and the read is
bounded and streaming, but `MaySubmit` is checked inside
`SubmitProviderInstruction` **after** the body has been read and parsed. A
paused caller can therefore still force a bounded read and a JSON parse per
request. Bounded, not unbounded — but the check belongs before the read.

## Open question to put to the operator

`ProcessIntake` returns a declared assessment for the `provider_api` channel
before `EvaluateIntakeCaseMatch`, so a declared instruction never reaches
existing-case matching. A repeat instruction on the same claim allocates a
**new** case rather than matching the existing one. This may be intended — a
declared instruction is definitive and states its own claim number — but no
document settles it. Raised as a P1 duplicate-case risk in the PR #594 review
and deliberately left unchanged.

## Verification

- [ ] A provider-created case's snapshot carries its work provider, proven by
      a persistence test, and the EVA export reports it.
- [ ] A paused credential is refused before the request body is read.
- [ ] The existing-case-matching question has an operator answer recorded in
      FRD-09.

## Notes

- Neither is reachable today: `Features:ProviderApi` is closed and no
  credential has been issued.
- The other API-01 residual, the non-atomic accept path, is [[AUTO-012]].
