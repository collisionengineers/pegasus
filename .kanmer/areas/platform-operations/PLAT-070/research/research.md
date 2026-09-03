# Research — PLAT-070 (2026-09-03, gpt-5.6-terra medium, wrapper-checked)

PLAT-070 is a read-only research result. The working tree is clean at
`897db9530a45063e8f684f2800685afbfdced006`.

## Premises

| Premise | Status |
| --- | --- |
| PLAT-070 is backlog, profile `fix`, blocks CASE-038, and has no existing research/files documents. | VERIFIED — `mcp__kanmer__get_item({id:"PLAT-070"})`, `get_doc_gates`, and `get_links`. |
| EPIC-012 binds D44/D45 and assigns the shared locks named in the ticket. | VERIFIED — `get_group_doc(EPIC-012, context.md)` and `rg -n ... EPIC-012/context.md`. |
| The local mockup sources and notes are available. | VERIFIED — `Get-ChildItem C:/Users/PC/Downloads/Pegasus_UI_v2_src/src` and `Test-Path` checks. |
| The workflow review configuration is persisted. | VERIFIED — `rg -n ... AdministrationPolicyEntities.cs AdministrationPolicyModelConfiguration.cs EfWorkflowConfigurationStore.cs PegasusDbContextModelSnapshot.cs`. |
| A migration is required. | VERIFIED — the persisted configuration columns exist in the EF model/snapshot; removing them requires a forward EF migration. |
| SQL migration grants must change. | ASSUMED — the existing Worker permissions remain `SELECT,UPDATE` on `WorkflowConfigurations`; the migration changes only columns, not table access. Run `scripts/Test-MigrationGrants.ps1` because the ticket requires it. |
| The installed SDK supports the repository. | VERIFIED — `dotnet --list-sdks` reports 10.0.204 and 10.0.303. |
| Both a "staff instruction review" and "staff image review" mechanism exist in Core (not only the image-named one the ticket's grep example names). | VERIFIED (wrapper spot-check) — `RequireStaffInstructionReviewBeforeEngineerAssignment` and `InstructionsReviewedByStaff` exist alongside the image-named pair in `CaseWorkflowContracts.cs`, `CaseLifecycle.cs` (line 575-576), and `AdministrationPolicyEntities.cs`/`AdministrationPolicyModelConfiguration.cs` (both seeded `true`). |
| `CaseCompleteness` persists `InstructionConfirmedByStaff` / `ImagesConfirmedByStaff` separately from the readiness-evidence review flags, and these are copied through intake, replacement and queue processing. | VERIFIED (wrapper spot-check) — both symbols appear in `src/Pegasus.Core/Cases/CaseContracts.cs`, `CaseDataOperations.cs`, and `src/Pegasus.Core/Intake/IntakeAllocation.cs`. |
| `scripts/Test-MigrationGrants.ps1` exists as the reuse target for grant proof. | VERIFIED (wrapper spot-check) — file present at repo root `scripts/`. |

## Current behaviour and gap

Core has two overlapping staff-review mechanisms:

- `CaseLifecycleRules.ValidateReadiness` requires completeness plus configured
  instruction/image review evidence before engineer assignment.
- `CaseCompletenessPolicy.Evaluate` uses the same configuration to decide
  whether a Not ready case satisfies policy.
- `CaseReadinessEvidence` carries `InstructionsReviewedByStaff` and
  `ImagesReviewedByStaff`.
- `CaseCompleteness` separately persists `InstructionConfirmedByStaff` /
  `ImagesConfirmedByStaff`; the UI uses those values to populate the
  readiness hidden inputs. **This is the important hidden scope: D44 and its
  wording ("no staff act of reviewing instructions or images") retire both
  instruction and image review acts, not merely the image-named member the
  ticket's grep example names.**

The persisted workflow configuration has both
`RequireStaffInstructionReviewBeforeEngineerAssignment` and
`RequireStaffImageReviewBeforeEngineerAssignment`, seeded true. The
Administration page renders a "Staff review requirements" panel containing
both checkboxes and an audited/versioned update path.

The Case pages post both review fields through the shared partial, then bind
them in Details, Workflow, Closure, and the common readiness factory.
`OperatorLabels.WorkflowConfiguration` owns the visible review-panel labels.
The current Test UI snapshot reproduces that panel.

The mockup confirms the intended gap:

- `20-case.js` currently models "Confirm requirements" with instruction/image
  reviewed flags, but `Pegasus_UI_v2_notes.md` explicitly says those flags,
  dialog, and the Workflow configuration review checkboxes are superseded.
- `17-admin.js` currently models a Review panel with two checkboxes; the
  notes say the shipped app currently has only those review checkboxes,
  whereas the intended configuration is completeness-item rules plus chase
  interval.
- `23-damage-diagram.js` models zone markers/severity. The notes' 3
  September amendment removes damage type; D45 therefore changes governing
  documents only in PLAT-070. No engineering damage-model/UI/report
  implementation belongs here.

## Reuse and implementation direction

Reuse:

- `CaseLifecycleRules.ValidateReviewReadiness` as the single
  completeness-only guard for Not ready → Review/reopen-to-Review.
- `CaseCompletenessPolicy.Evaluate` as the single owner of automatic
  completeness evaluation; simplify it to instruction/image completeness,
  rather than retaining a configuration bypass.
- EF's existing `AdministrationPolicyModelConfiguration`,
  `EfWorkflowConfigurationStore`, migration conventions, and
  `scripts/Test-MigrationGrants.ps1`.
- `Update-TestUiSnapshots.ps1` and `Test-UiCatalogue.ps1` for the one
  affected Administration snapshot.

D44 requires deleting both review configuration flags and both
`CaseReadinessEvidence` review values. Leaving the instruction
flag/confirmed path would contradict "no staff act of reviewing
instructions or images," even though the ticket's grep example names only
the image member.

## Risks

- Removing only `RequireStaffImageReview...` would leave the instruction
  review gate and the retired panel concept in place.
- `CaseCompleteness` confirmation fields are persisted and copied through
  intake, replacement, queue processing, and tests. Research recommends
  deleting them, since they are the actual source of the retired UI review
  state, consistent with greenfield rule 6 (no deprecation path).
- Generated migration designer files and the model snapshot must be updated
  together. Do not edit historical migrations.
- `Pages/Cases/Details.cshtml` and the Test UI directory are shared-lock
  paths; PLAT-070 must remain serial before CASE-038.
- D45 must not pull ENG-035/ENG-036 damage implementation into this ticket.
  Existing EVA reference documents containing external `DamageType` fields
  are not Pegasus damage-zone UI/model scope and should remain untouched.

## Open questions

None. D44 explicitly resolves the only material ambiguity: review is a
stage, not an operator action, and readiness is completeness-only — and that
wording covers both the instruction and image review acts, confirmed by the
wrapper's own spot-check of the Core symbols.
