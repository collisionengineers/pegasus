---
id: PLAT-063
type: ticket
title: >-
  OperationsSnapshot.CaseActivity, TriageCount and DueWork cost three EF queries
  per Work Centre load and are rendered nowhere
status: backlog
area: platform-operations
assignee: ''
profile: fix
labels:
  - backend
  - wave-5
  - cleanup
  - rule-14
  - efficiency
groups:
  - EPIC-011
links:
  - PLAT-058
  - UIIMP-008
  - UIIMP-009
refs:
  - docs/frd/frd-12-operator-experience.md
archived: false
created: '2026-08-29T17:34:50.641Z'
updated: '2026-08-29T17:34:50.641Z'
---

## What

`OperationsSnapshot` computes `CaseActivity`, `TriageCount` and `DueWork`, and
**no Web reader consumes any of them.** The projection runs on every Work Centre
load, so three EF queries are paid per request for values that reach no operator.

Either wire each to a real rendered surface, or remove it from the projection.

## Why

Found by the EPIC-011 closeout board reconciliation while auditing the
`verifying` tickets against the strict rule 14 settled in D20. It is the same
shape as [[PLAT-058]] (`MailActivityCounts.ReceivedToday` — queried every Work
Centre load, rendered nowhere), which that reconciliation also re-confirmed as
still present. **The two should almost certainly be worked together**: one
projection, one pass, one decision about what the Work Centre actually needs.

This is not only a rule-14 tidiness point. It is a per-request cost on the
application's landing page, paid on every load by every operator.

## Approach

- **Establish the reader first.** `git grep` each symbol across `src/` and
  confirm there is genuinely no non-test consumer before removing anything — the
  reconciliation's finding is the starting point, not the proof.
- Check the design contract and FRD-12 before deleting: if the Work Centre is
  *meant* to show one of these and simply never got its control, the correct fix
  is to render it, and that belongs to the surface's owning lane rather than here.
- If they are genuinely dead, remove them from the projection **and** from the
  query that populates them, so the EF cost goes away rather than the field
  alone.
- Take [[PLAT-058]] in the same pass if the two prove to be one decision. Say so
  explicitly rather than absorbing it silently.

## Owns

`src/Pegasus.Core/Operations/OperationsSnapshot.cs` and its Infrastructure query,
plus whatever tests pin the removed fields.

## Verification

- [ ] Every field named here either has a rendered production caller with a
      quoted `file:line`, or is gone from both the projection and its query.
- [ ] The Work Centre issues fewer EF queries per load than before, evidenced.
- [ ] No assertion weakened to accommodate the removal; tests that pinned a
      removed field are deleted with their field, not neutered.
