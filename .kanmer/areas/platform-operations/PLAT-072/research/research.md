# Research — PLAT-072: remove dead staff-confirmation state

## Question

Which obsolete confirmation fields remain after D44, and what is the smallest
coherent removal that preserves actual completeness and existing handoff gates?

## Findings

- Research source is fetched origin/dev522e67f270ab4d6086d9fba04095988db3598888,
  read from its exact detached workspace; shared checkout is not authoritative.
  PLAT-070 is Done, merged60fc84dc0ef7e1c4746dd9b3961d287598845871: review
  configuration was already removed. No replacement configuration is needed.
- CaseContracts.cs:123-130 retains four CaseCompleteness constructor fields
  although IsReadyForReview already reads only InstructionComplete and
  ImagesComplete. CaseDataOperations.cs:50-74 carries unused
  automaticallyDefinitive parameters; AcceptIntake.cs:93-97 supplies one.
  ValidateCompleteness is already only a null guard; its comment claiming
  the flags are unwritten is false. Keep the null guard and correct the comment.
- Create.cshtml:242,250 still displays both confirmation checkboxes and its
  PageModel writes the values. Details.cshtml.cs:905-924 and
  Shared/_CaseWorkflow.cshtml:139-140 retain posted/hidden fields. Remove
  those only; retain native instruction/image factual completeness controls,
  required identity/instruction/reason fields, validation and action semantics.
- There are FOUR stored columns, not two: Cases in PegasusDbContext.cs:1165
  and IntakeAllocationAttempts in IntakeAllocationEntities.cs:16-17 each
  carry both flags. Follow reconstruction, acceptance command serialization,
  allocation retry and linked replacement to the same two-field contract.
  EfQueuedCustodyProcessor.cs:617 still describes an obsolete staff waiver.
- Explicit field-name plus four-boolean constructor searches find Core tests,
  shared fixtures, browser scenarios and latest-schema raw SQL across many
  files. Their mechanical signature/column updates are necessary callers;
  scenario assertions stay intact. The files index names the observed union.
- Acceptance command fingerprint material in EfCaseAcceptanceStore.cs:610-660
  includes these flags. The new material format removes them and advances its
  existing SchemaVersion from4 to5; no dual-hash or legacy-read mechanism.
  Existing permanent JSON/audit/operation history is not rewritten.
- Broad ConfirmedByStaff grep is not a valid deletion instruction:
  CaseContracts.cs:146 ConfirmedByStaffId belongs to standalone Audit evidence,
  not instruction/image review. Keep it. Historical migration fixtures in
  CaseWorkflowMigrationTests, RepairSpecificationMigrationTests and
  TypedCaseDataMigrationTests explicitly target earlier schemas and must
  retain earlier column names when seeding those schemas. Historical
  migrations/designers and intentional absence assertions also remain.
- CaseWorkflowMigrationTests.cs:127 and IntakePersistenceIntegrationTests.cs:130
  use exact migration lists and currently omit the already-merged DOCS020
  report permission migration. Adding the PLAT072 migration necessarily
  reconciles both lists to all actual migrations without weakening equality.
  Root was notified of this pre-existing migration-census verification gap.
- FRD-01:67-73 and FRD-12:410-412 already require persisted-fact completeness
  and no staff-review checkbox. EPIC012 D44 plus current EPIC014 agrees.
  The handoff and Glass gates remain separately owned. Canonical behavior docs
  already state the intended result; no protected operator-notes edit.
- Root confirms TICK041 does not touch schema/model; DOCS020 is source-frozen/
  merged but its retained claim must close before edits. PLAT028 also retains
  CaseContracts and provider-fixture ownership. ENG041 owns the Glass callback
  test fixture; only two obsolete assignments will need later reconciliation.
  No foreign claim is forced; no code, branch or worktree has been created.
- Source registry declares0 sources. Current repo mechanisms suffice; no
  outside package, source, service or framework is needed.

## Implications

Drop the four dead columns with the existing EF migration mechanism and
update the model snapshot. Existing table-level runtime grants remain valid:
no new table/action/permission is introduced, so no broad grant or bootstrap
rewrite is justified. Normal migration grants check and actual persistence
callers prove that. Down can restore columns defaultfalse but cannot recover
discarded dead values; no row/history/reference or real completeness value
is removed. This is within the user's explicit obsolete-field removal.

Use one unchanged semantic fieldset containing the two real completeness
controls; do not replace removed checkboxes with explanatory copy or a dialog.
Root alone captures case-create/case-details and runs focused checks.
No app edit until dependency/claim clearance and a fresh execution packet.

## Open questions

Only orchestration clearance remains, recorded in open-questions. There is
no unresolved product choice; current user explicitly confirms handoff IS review.
