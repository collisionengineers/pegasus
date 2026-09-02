---
id: INTK-033
type: ticket
title: >-
  A triage-request email creates no Triage and no Unidentified item — it is
  stranded
status: verifying
area: intake-processing
order: 30
assignee: claude-code
profile: feature
stageEntered:
  preparing: '2026-08-24T07:14:47.726Z'
  review: '2026-08-24T08:01:44.102Z'
  verifying: '2026-08-24T14:57:13.097Z'
taken_at: '2026-08-24T07:16:57.081Z'
branch: task/intk-033-triage-from-intake
worktree: ../pegasus-worktrees/intk-033-triage-from-intake
labels:
  - production-defect
  - found-during-qa
  - triage
  - closed-composition-gate
links: []
refs:
  - docs/frd/frd-03-triage.md
  - docs/frd/frd-09-provider-and-intermediary-routes.md
commits:
  - 7d4c8f005261d3963cdecf806b3e06c17552be9b
  - 3f0bba39
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/525'
deployment: production
archived: false
created: '2026-08-23T15:18:47.553Z'
updated: '2026-09-01T14:44:16.288Z'
---

## What the operator saw

> *"E-mail 3 - Triage Request E-mail. Identified in the inbox as Triage. Did not
> create a triage case. Did not show in the triage queue."*

Half of that is [[MAIL-012]] working. The other half is a capability that has
never run in production.

## What actually happened, from production

Receipt `d42a5515-a962-42e0-88f7-57a63501d106`, 2026-08-23 14:57:29Z:

| Record | Value |
| --- | --- |
| Classification | `classified` · `pre-instruction-emails` / `triage-request` · policy v4 |
| Classification `CaseType` | *(none — correct; a triage is not a case)* |
| Receipt decision | **`case_created`** — "A definitive instruction was identified and is eligible for case allocation." |
| Allocation attempt | **`failed`** |
| `FailureKind` | **`case_type_unavailable`** |
| `RecoveryDisposition` | `manual_review` |
| `Triage` rows | **0** |
| `UnidentifiedItems` rows | **0** |

So intake classified it as a triage request, then **still attempted automatic
case allocation**, which failed for want of a case type — and produced nothing
at all. No case, no Triage, no Unidentified item. The message is visible only
in the inbox; it appears in no queue anyone works.

## The required behaviour is already written

`operator-notes.md` § Stage 0 — Triage, step 2, verbatim:

> *"keep it as **Unidentified** (formerly `Needs sorting`) **until a vehicle
> registration is known, then open the Triage**"*

Operator, 2026-08-23, confirming: *"Its not a question on the triage, its
explicitly defined in my notes. Since the registration is known, its not
unidentified."*

So the rule is a branch on one fact, and Unidentified is the holding state for a
**missing** registration only:

| Registration on the triage request | Outcome |
| --- | --- |
| known | **open the Triage** |
| not known | **Unidentified**, until it is |

Email 3's subject carries `GD65TVY`. It should have opened a Triage.

## Three faults

**1. Classification is not consulted before allocation.** A `triage-request`
carries no `CaseType` by design, yet `AllocateIntake.AttemptAutomaticAsync`
runs anyway and fails on its absence. A classification that says "this is not a
case" must route to the Triage path, not into case allocation.

**2. Triage creation is behind a closed composition gate.**
`ProcessQueuedIntake.CreateTriageIfQualifyingAsync` (`DurableIntake.cs:893`)
requires an `AcceptedTriageMatch` evidence finding of `Strong` strength with a
matcher key and version. That finding can only come from `IIntakeTriageMatcher`
— and production composes:

```csharp
services.TryAddSingleton<IIntakeTriageMatcher, NoAcceptedIntakeTriageMatcher>();
```

The null matcher, which by name and construction never accepts anything. **The
gate can never pass.** No Triage has ever been created from intake in
production, and none can be until this is composed.

Note also that `CreateTriageIfQualifyingAsync` keys off *evidence findings*,
never off the triage **classification** — so even a working matcher would be
answering a different question from the one the operator's email asks. The
qualifying condition has to become "this message is a triage request", which is
now a recorded classification.

**3. The registration is not extracted from a triage request.** The branch above
turns entirely on whether a registration is known, and today nothing reads one
off a triage request — `CreateTriageIfQualifyingAsync` reads
`receipt.InstructionDraft?.VehicleRegistration`, which is populated by the
*instruction* extraction path. A triage request is not an instruction. Getting
the registration out of the subject (`… Vehicle registration GD65TVY`) or body
is in scope, because without it every triage request falls to Unidentified and
the rule's first branch never fires.

## Repository position

CLAUDE.md: *"A closed composition or feature gate is a disabled flag, not a
partially shipped feature. Do not ship, release, merge as delivered, claim, or
document a feature behind one as delivered."*

Triage-from-intake is therefore **not delivered**, and this is a feature ticket,
not a bug fix. The operator reasonably expected otherwise, because the inbox
labels the message "Triage" — the label is real and the work behind it is not.
That gap is the most important thing here.

## Governing docs

`operator-notes.md` § Stage 0 is the authority above; `docs/frd/frd-03-triage.md`
§ Normal workflow and completion evidence holds the canonical transitions. Read
both before planning — the rule is settled and does not need re-deciding, only
implementing.
