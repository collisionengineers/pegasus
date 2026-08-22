# Research

## What exists today

`Cases/Details.cshtml:142` renders a **History** tab from `details.History`, a read-only
list of `CaseHistoryEntry` — event type, actor, actor kind, occurred-at, reason, before and
after version. `_CaseHistory.cshtml` renders it as a table and already gives an Automation
actor its own chip, so entries are already visually attributable.

Rows are written by several stores (`EfCaseAcceptanceStore`, `EfQueuedCustodyProcessor`,
`EfLinkedCaseReplacementStore`) into one `CaseHistory` table with a `CaseId`, `EventType`,
`Actor`, `Reason`, `OccurredAtUtc`, `OperationKey` and version pair. Nothing deletes or
updates them.

## The design question

The operator wants operator-written notes *alongside* system messages, in one place. Two
ways to do that:

| | New `CaseNotes` table | A note as a history row |
| --- | --- | --- |
| Migration | required | none |
| Ordering across both kinds | a merge in the query | already one ordered list |
| Attribution | duplicated | already there, with the automation chip |
| Append-only guarantee | to be rebuilt | inherited |
| Risk of the two drifting | real | none — there is one list |

The second wins on every line, and it is what the operator described: one timeline.

`CaseHistoryEntity.Reason` is the natural home for the note text — every other row uses it
for the human-readable account of what happened.

## Rules this must not break

- Case history is **permanent action history** and append-only. A note must not become a
  way to edit or reinterpret the record, so nothing may edit or delete one.
- Adding a note is itself a material action and belongs in the record — which it
  automatically is, being a history row.
- `docs/design/README.md` bans explanatory copy, so the surface is a label, a control and a
  button. No guidance sentence.

## The authorisation question, found by testing

`StaffAuthorization.Require(actor, PerformCasework)` **admits the Automation Actor** — a
first draft relying on it alone let automation author a note, and a test written for that
case caught it. The operator asked for notes a *user* writes; automation already records
what it does on this timeline under its own event types. So notes are staff-only, checked
explicitly on the actor kind.

## Lease question

A note does not change the case — no version bump, no data mutation. Requiring the case
edit lease would make writing a note contend with an engineer editing the same case, for no
safety gain. Decided: no lease, no expected version, idempotent by operation key like every
other mutation.
