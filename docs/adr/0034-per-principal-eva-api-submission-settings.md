---
id: ADR-0034
status: accepted
date: 2026-08-27
supersedes: []
superseded_by: []
related_capabilities: [EXT-04]
related_frd: [frd-07]
tags: [eva, configuration, principals]
---

# ADR-0034: Per-Principal EVA API submission settings

## Status

Accepted, 2026-08-27.

## Context

EXT-04 adds a second send-to-Engineer route: Pegasus submits a case to EVA over
its API rather than producing a package for an operator to drag in. The
operator directed that the route be switchable per Principal, with two
independent choices — submit by hand from the case, and submit automatically
when the case reaches `Review`.

Two existing decisions bear on this.

**ADR-0008** made provider configuration code-owned rather than
database-authored. **ADR-0018** took a scoped exception to that for one
attribute, the Principal's inspection mode, because the product owner required
it changeable without a code change and a deploy. ADR-0018 also deferred a
post-creation edit, leaving a production change as a runbook action.

FRD-07 further records that the manual export deliberately has *no* activation
switch, and `docs/operations.md` records that an earlier `EvaMappingAcceptance`
gate was removed. Adding a switch is therefore a reversal that needs stating,
not a gap being filled.

## Decision

1. Each Principal carries two persisted boolean settings,
   `EvaManualSubmission` and `EvaAutomaticSubmission`, on the `Principals`
   table. Both default to `0`, so adding them switches nothing on.

2. They are **independent**. All four combinations are legal, including
   automatic without manual — a Principal that submits unattended and offers no
   button. That Principal has no manual recovery from a failed submission;
   recovery is the reconciliation sweep re-arming the work.

3. **There is a post-creation edit operation**, departing from ADR-0018
   Decision 5. A delivery route that could only be chosen while creating a
   Principal could never be switched on for the Principals that already exist,
   and every Principal in production already exists. The edit changes the
   settings and nothing else; it writes attributed permanent history like every
   other administration operation.

4. A **replacement Principal inherits its predecessor's settings**, the same
   way it already inherits the inspection mode. The work arriving for the
   successor is the same work.

5. A **disabled Principal's settings are frozen.** They are a record of what it
   did while it was active, and its successor is what decides current
   behaviour.

6. The settings **join the create command's request hash**, so an idempotent
   replay whose settings changed fails closed rather than silently reusing the
   receipt. This mirrors ADR-0018 Decision 4.

7. This decision governs **whether a delivery route is offered for a
   Principal's cases, and nothing else.** It selects no route policy, no
   provider identity, and no case-data default. ADR-0008's consequence that
   provider configuration is code-owned continues to govern route selection.

## Consequences

- `Principals` gains two `bit` columns, both defaulting to `0`, and no check
  constraint: two independent booleans have no illegal combination to exclude.
- The Principal create and replace commands carry the settings, and a new
  administration operation changes them in place. The Principals list shows the
  effective setting.
- FRD-07 gains the API route and must state that the export's "no activation
  switch" rule does not extend to it.
- Switching automatic submission on for a Principal causes its `Review` cases
  to be submitted without further operator action. That is the point of the
  setting, and it is why the default is off and the change is attributed.
- A future Principal-scoped setting has a shape to follow. Two exceptions to
  ADR-0008 now exist; a third should prompt asking whether the general rule
  still holds rather than taking a third exception.

## Options considered

**A code-owned allow-list of Principal codes.** Consistent with ADR-0008 and
needing no schema change, but switching a Principal on would be a deploy. The
operator's stated requirement is that they control it, which is the same reason
ADR-0018 went to the database.

**One mode column (`off` / `manual` / `automatic`) with a check constraint.**
Closer to ADR-0018's shape and makes an illegal combination unrepresentable.
Rejected because it cannot express "automatic, no button", which the operator
chose deliberately; a three-value mode would have silently added a manual
route to every automatic Principal.

**A single deployment-wide switch.** Simplest, and wrong: Principals are the
boundary at which work provision differs, and one Principal moving to the API
must not move the others.

## Links

- [FRD-07 — Direct EVA API submission](../frd/frd-07-eva-and-external-engineering-handoff.md#direct-eva-api-submission)
- [ADR-0018 — Provider inspection mode as a database setting](0018-provider-inspection-mode-database-setting.md)
- [ADR-0008 — Direct provider and intermediary email policies](0008-separate-direct-provider-and-intermediary-email-policies.md)
