---
id: INTK-033
type: ticket
title: >-
  A triage-request email creates no Triage and no Unidentified item — it is
  stranded
status: backlog
area: intake-processing
assignee: ''
profile: feature
labels:
  - production-defect
  - found-during-qa
  - triage
  - closed-composition-gate
links: []
docs_todo: true
deployment: not-deployed
archived: false
created: '2026-08-23T15:18:47.553Z'
updated: '2026-08-23T15:18:47.553Z'
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

## Two distinct faults

**1. Classification is not consulted before allocation.** A `triage-request`
carries no `CaseType` by design, yet `AllocateIntake.AttemptAutomaticAsync`
runs anyway and fails on its absence. A classification that says "this is not a
case" should route away from case allocation, not into it.

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
production, and none can be until a real matcher exists.

Note also that `CreateTriageIfQualifyingAsync` keys off *evidence findings and
a vehicle registration*, never off the triage **classification** — so even a
working matcher would be answering a different question from the one the
operator's email asks.

## Repository position

CLAUDE.md: *"A closed composition or feature gate is a disabled flag, not a
partially shipped feature. Do not ship, release, merge as delivered, claim, or
document a feature behind one as delivered."*

Triage-from-intake is therefore **not delivered**, and this ticket is a feature,
not a bug fix. The operator reasonably expected otherwise, because the inbox
labels the message "Triage" — the label is real and the work behind it is not.
That gap is the most important thing here.

## Open question for the operator

`operator-notes.md` §Stage 0 says a Triage is kept as **Unidentified** until a
vehicle registration is known, then the Triage is opened. This message's subject
carries `GD65TVY`.

**Does a triage request with a registration in the subject open a Triage
directly, or does it land in Unidentified for a human to confirm the
registration first?** The answer decides whether this ticket needs registration
extraction from a triage subject as well. Recommendation: land it in
Unidentified with the registration pre-filled — it matches the written rule and
keeps a human between an email and a new unit of work — but this is operator
truth, not mine to choose.
