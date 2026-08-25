---
id: CASE-020
type: ticket
title: 'Read the case header and list from the case, not the intake draft'
status: backlog
area: case-reference-workflow
assignee: ''
profile: fix
labels:
  - qdos26011
  - found-during-qa
links:
  - CASE-018
  - ENG-013
deployment: not-deployed
archived: false
created: '2026-08-22T21:58:43.680Z'
updated: '2026-08-25T06:38:46.290Z'
---

## The defect

`EfCaseQueryStore`'s search projection reads a case's registration, claimant, claim number and instruction date from `InstructionDraftEntity` — the draft attached to the origin intake receipt:

```csharp
Registration = draft == null ? null : draft.VehicleRegistration,
Claimant     = draft == null ? null : draft.ClaimantName,
ClaimNumber  = draft == null ? null : draft.ClaimNumber,
```

Everything else on the case page reads `CaseDataFields` through `CaseDataProjection`, where `CaseField.Current` resolves `Confirmed ?? Fact ?? Suggestion`. So the same four facts have **two independent sources**, and they can disagree.

## Where it shows

`SearchRow` feeds both the case list and the dark identity band at the top of the case page. A case whose draft is absent or stale renders `No registration` / `No claimant recorded` in the header while the Vehicle and Case identity blocks twelve pixels below show the real values.

Observed 2026-08-22 while running the case page locally against a hand-seeded fixture with no draft row: header read `QDOS26011 · QDOS · No registration · No claimant recorded · Inspection and audit` over a page that plainly carried `ST66BCE` and `Mr Harry Sykes`. The same shape appears in `OperatorJourneyTests`' fixture (`QDOS31001`).

Production is **not** currently affected — all three live cases have draft rows carrying values that match their case fields — so this is latent, not live. It becomes visible the moment a case is corrected: a staff correction writes `CaseDataFields`, never the draft, so the header would keep showing the superseded value indefinitely.

## Why it matters beyond cosmetics

The draft is intake's *reading* of the instruction. The case's fields are what the case holds after extraction, provider settings, lookups and staff correction. Presenting the first as if it were the second means the list an operator searches and scans can disagree with the case they open.

## Scope

- Read the four fields from the case's own data, so the header and the list agree with the page.
- Keep the intake draft where it belongs — as intake evidence on the receipt.
- Check the search and sort paths (`CaseSearchOrder.RegistrationAsc/Desc`, the registration filter) still work when the value moves source.

## How to verify

A case whose registration was corrected by staff shows the corrected value in the list, in the header band and in the Vehicle block — one value, three places, no disagreement.

## Not in scope

Whether the header should repeat registration and claimant at all, given [[CASE-018]] made the blocks the one place a case fact is read. That is an operator decision, raised separately.
