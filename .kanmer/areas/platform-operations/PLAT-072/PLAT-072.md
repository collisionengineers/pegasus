---
id: PLAT-072
type: ticket
title: >-
  Remove the intake staff-confirmation checkboxes and the CaseCompleteness
  *ConfirmedByStaff properties (D44 residual)
status: backlog
area: platform-operations
assignee: ''
profile: fix
labels:
  - case-workspace-v2
  - d44
  - follow-up
groups:
  - EPIC-012
links:
  - PLAT-070
refs:
  - docs/frd/frd-01-case-identity-and-lifecycle.md
  - docs/frd/frd-12-operator-experience.md
archived: false
created: '2026-09-03T16:29:07.376Z'
updated: '2026-09-03T16:29:07.376Z'
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
