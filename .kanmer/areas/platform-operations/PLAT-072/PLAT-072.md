---
id: PLAT-072
type: ticket
title: >-
  Remove the intake staff-confirmation checkboxes and the CaseCompleteness
  *ConfirmedByStaff properties (D44 residual)
status: implementing
area: platform-operations
assignee: principal_delivery_audit
profile: fix
stageEntered:
  preparing: '2026-09-07T22:02:01.834Z'
taken_at: '2026-09-07T23:30:33.773Z'
branch: PLAT-072-remove-staff-confirmation
worktree: .worktrees/plat-072
claim_expires_at: '2026-09-08T00:21:56.078Z'
claim_controller: codex-v1-remediation-root
lease_id: 9f035aab-2144-4995-800a-8cf6494c077c
lease_revision: 4
lease_controller_run: 20260907T200500Z-v1-remediation
lease_worker_run: principal-delivery-plat072
lease_workspace: 'worktree:c:\users\alex\documents\github\pegasus\.worktrees\plat-072'
lease_provider: codex
lease_phase: implementing
lease_heartbeat_at: '2026-09-07T23:51:56.078Z'
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
  - 278f605333f7569fb1927c3d4ed360d080903453
archived: false
created: '2026-09-03T16:29:07.376Z'
updated: '2026-09-07T23:54:27.978Z'
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
