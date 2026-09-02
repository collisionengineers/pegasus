---
id: AUTO-013
type: ticket
title: >-
  A case created through the Provider API records no Work Provider, and a paused
  credential is refused only after the body is read
status: done
area: automation-integrations
order: 70
assignee: ''
profile: fix
stageEntered:
  preparing: '2026-08-29T22:39:53.501Z'
  implementing: '2026-08-29T22:40:03.540Z'
  review: '2026-08-29T22:40:36.930Z'
  verifying: '2026-08-29T22:40:42.099Z'
  done: '2026-09-02T17:01:29.719Z'
labels:
  - API-01
groups:
  - EPIC-011
links:
  - TICK-058
  - AUTO-012
commits:
  - 2090104a2caecb5bd620c4d810afed4f0261134a
  - 8b6d41345ee3afd1d7a1eb875ed3416516d50375
prs:
  - '634'
deployment: production
delivery_state: integrated
delivery_branch: dev
delivery_sha: 8b6d41345ee3afd1d7a1eb875ed3416516d50375
delivery_recorded_at: '2026-09-02T16:09:59.592Z'
archived: false
created: '2026-08-29T08:35:46.076Z'
updated: '2026-09-02T17:02:08.699Z'
---

## What

Two confirmed-live defects in the API-01 surface that [[TICK-058]] deferred.
They were recorded as deferrals with no ticket, which rule 22 does not allow;
this is that ticket.

### Terminology, corrected 2026-08-29

This ticket was originally titled "provider principal absent from the
case-data snapshot". **That is redundant and it obscured the defect.**
`docs/operator-notes.md:219` is explicit:

> | Work Provider | Also referred to as the principal. |

Principal *is* the work provider. There is no separate "provider principal".
The defect is that the Work Provider is not recorded at all.

### 1. A case created through the Provider API records no Work Provider

`CaseDataSnapshotFactory.AddProviderFact` returns early unless the receipt
carries an **accepted `MailRouteDecision` with a work-provider code**. A
Provider API receipt has no mail route by design — its Principal comes from the
credential — so `WorkProviderCode` is never written to the case-data snapshot,
and the EVA export reports Work Provider as unrecorded **even though allocation
established the Principal from the submission binding**.

The system knows exactly who the work provider is, and does not write it down.

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

## Priority raised 2026-08-29 — the API is being enabled

The original ticket said "neither is reachable today: `Features:ProviderApi` is
closed and no credential has been issued". **The operator has decided the flag
ships enabled in release 37.**

Defect 1 therefore stops being latent: from the first issued credential, every
case a provider creates carries no Work Provider, and its EVA export says so.
That is a data gap on the identity of the party sending the work, on the exact
route being opened.

Defect 2 stays low — bounded read, no unbounded cost.

**Defect 1 should land before or with the first issued credential**, which is a
separate operator-approved step (`docs/capabilities.md:227`) and gives a natural
window between enabling the flag and any provider actually calling.

## Verification

- [ ] A case created through the Provider API carries its Work Provider in the
      snapshot, proven by a persistence test, and the EVA export reports it.
- [ ] A paused credential is refused before the request body is read.
- [ ] The existing-case-matching question has an operator answer recorded in
      FRD-09.

## Notes

- The other API-01 residual, the non-atomic accept path, is [[AUTO-012]].


## Outcome

Merged as [PR #634](https://github.com/collisionengineers/pegasus/pull/634)
on 2026-08-29 at `8b6d41345ee3afd1d7a1eb875ed3416516d50375`.
Exact-SHA verification passed on 2026-09-02: 2,483 non-corpus tests passed,
3 corpus-dependent tests skipped, and none failed.

The Provider API automatic path now records Work Provider from the authenticated
credential binding, while staff correction does not claim that provenance. A
paused credential is rejected before its request body is read. The missing EVA
export regression pin remains explicitly deferred to [[DOCS-016]]; the
existing-case matching policy question remains outside this ticket.
