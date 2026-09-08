---
id: PLAT-072
type: ticket
title: >-
  Remove the intake staff-confirmation checkboxes and the CaseCompleteness
  *ConfirmedByStaff properties (D44 residual)
status: done
area: platform-operations
assignee: principal_delivery_audit
profile: fix
stageEntered:
  preparing: '2026-09-07T22:02:01.834Z'
  review: '2026-09-07T23:54:46.238Z'
  verifying: '2026-09-07T23:59:13.701Z'
  done: '2026-09-08T00:10:39.568Z'
labels:
  - case-workspace-v2
  - d44
  - follow-up
groups:
  - EPIC-012
  - EPIC-014
links:
  - PLAT-070
refs:
  - docs/frd/frd-01-case-identity-and-lifecycle.md
  - docs/frd/frd-12-operator-experience.md
commits:
  - d442366787d452da22d36719272d4eb79dc1afde
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/688'
deployment: not-deployed
delivery_state: integrated
delivery_branch: dev
delivery_sha: d442366787d452da22d36719272d4eb79dc1afde
delivery_recorded_at: '2026-09-08T00:13:01.265Z'
archived: false
created: '2026-09-03T16:29:07.376Z'
updated: '2026-09-08T00:14:59.327Z'
---

## What

Deferred from [[PLAT-070]] review (2026-09-03, PR #649). PLAT-070 removed the
staff-review readiness gate everywhere inside its owned paths, but two
operator-visible staff-confirmation checkboxes survive at intake:

- `src/Pegasus.Web/Pages/Cases/Create.cshtml:242` — `InstructionConfirmedByStaff`,
  labelled "I have confirmed the instruction evidence".
- `src/Pegasus.Web/Pages/Cases/Create.cshtml:250` — `ImagesConfirmedByStaff`,
  labelled "I have confirmed the image evidence".

They still write `CaseCompleteness.InstructionConfirmedByStaff` /
`ImagesConfirmedByStaff`, which after PLAT-070 gate nothing: neither
`CaseCompleteness.IsReadyForReview` nor `CaseCompletenessPolicy.Evaluate`
reads them, and no other surface displays them.

## Why

D44: "There is no staff act of reviewing instructions or images: no review
flag, checkbox, dialog or history line." A checkbox that records a staff
confirmation and gates nothing is exactly the retired act, and repository rule
21 deletes a gate that gates nothing.

PLAT-070 could not take this: `Create.cshtml(.cs)` is outside its owned paths,
and roughly fifteen unowned test files (including raw-SQL `INSERT INTO Cases`
fixtures such as `RailCountsWebTests.cs`, `VehicleLookupGapFillTests.cs`,
`ImageIntakeWebTests.cs`, `CaseCreateWebTests.cs`) construct
`CaseCompleteness` or name the two columns positionally — a runtime blast
radius no `dotnet build` catches.

## Approach

- Delete the two checkboxes, their bound properties and their `CaseCompleteness`
  construction in `Create.cshtml(.cs)`.
- Delete `CaseCompleteness.InstructionConfirmedByStaff` /
  `ImagesConfirmedByStaff` and the `CaseDataPolicy.ValidateCompleteness`
  "confirmed implies complete" guard that exists only for them.
- Follow the columns through `Persistence` (the `Cases` mapping,
  `EfCaseDataStore`, `EfCaseAcceptanceStore`, `EfIntakeAllocationStore`,
  `EfLinkedCaseReplacementStore`, `EfQueuedCustodyProcessor`) and drop them in
  one migration that ships with its grants and `Test-MigrationGrants.ps1`.
- Sweep every unowned test fixture and raw-SQL insert that names the columns.
- Regenerate the affected Test UI snapshots.

Also in scope: the now-unused `automaticallyDefinitive` parameter on
`CaseCompleteness.IsReadyForReview` / `CaseCompletenessPolicy.Evaluate` and its
caller `src/Pegasus.Core/Intake/AcceptIntake.cs:93`, and the stale CASE-013
comment at `src/Pegasus.Infrastructure/Persistence/EfQueuedCustodyProcessor.cs:582`
which still describes a staff-confirmation waiver that no longer exists.

## Verification

- [ ] `git grep -i "ConfirmedByStaff"` returns nothing outside historical migrations.
- [ ] `/Cases/Create` renders no confirmation checkbox.
- [ ] Migration ships with grants; `./scripts/Test-MigrationGrants.ps1` passes.
- [ ] Full filtered `dotnet test` green.

## Outcome

Completed and accepted on dev through [PR688](https://github.com/collisionengineers/pegasus/pull/688), merge d442366787d452da22d36719272d4eb79dc1afde (2026-09-07T23:59:10Z). Independent review318c3bd04afeb794 and root's exact-merge PASS3549238d2498d1db prove removal of the obsolete staff-review flags/controls/columns while factual completeness and real authorization/replay/history remain. The historical Verification list above is refined by approved plan9c7d2ec687171469: standalone Audit attribution and explicit historical/absence fixtures remain; focused existing tests replace repeated full suites before the final converged release gate. Root merged verification passed restore/build, Core24, Integration36, fresh snapshots2, catalogue60/67/0 and grants102. Original missing-capture failure remains recorded. Five hash-checked TRXs are retained in ignored pegasus_pack/current/proofs/PLAT-072/. No production migration/deployment or manual visual claim. [[CASE-049]] separately owns native handoff/access; no other implementation scope was absorbed.
