# Post-implementation report — INTK-015

Branch `task/intk-015-group-registration-unit` (worktree `../pegasus-worktrees/intk-015`), 5 commits `86fa5a63..0605c431`, PR → `dev`.

## What shipped

**The submission group is now the ImageIntake registration unit.**

- `ImageIntakeAutomation.TryApplyGroupAsync` no longer loops `TryRegisterAndAssociateAsync` per member. It consumes the single `ImageIntakeGroupRoutingPolicy` decision once and calls `TryRegisterGroupAsync`: one registration under the group-scoped operation key `image-intake-register:group:{groupId:N}`, origin = the group's lowest-ordinal image-material member (deterministic across racing siblings — the member filter is on retained material, not current decision, so an already-registered member cannot shift the primary). Suggestions across all members confirm against the one reference; per-member `AutoLinkAsync` happens only under the group's `AssociateExistingCase` decision (fail-closed group authority unchanged); members still at Needs sorting after a replay are re-asserted through the existing `EnsureRegisteredReceiptDecisionAsync` convention. A failed group registration reports `GroupPending` — the INTK-011 deferral/reconciliation contract is unchanged.
- `RegisterImageIntakeRequest` gained `Guid? SubmissionGroupId = null`; `ImageIntakeRecord` exposes it.
- `EfImageIntakeStore.RegisterAsync`: dedupes by `SubmissionGroupId` (a row already stamped with the group IS the registration, however keyed); stamps the column on fresh rows; **flips every image-only Needs-sorting member receipt to `image_intake_registered` naming the one reference in the same serializable transaction** (`RegisterGroupMemberReceiptsAsync`, members resolved group-members → latest evaluation → processed receipt — the existing seam, no parallel store; history rows keyed `{opKey}:{receiptId:N}`); **adopts** an identity-consistent single-receipt row into the group (the ordinal-0-lookup-miss convergence, see "left out"). Fingerprints append the group id only when present so pre-deploy replays stay valid.
- `GetByOriginReceiptAsync` / `EnsureRegisteredReceiptDecisionAsync` resolve the group intake for ANY member receipt (`FindForReceiptAsync`), which makes `SynchronizeUnidentifiedAsync`, `ImageIntakeCasePairing.SyncMergeAfterLinkAsync`, `UploadOutcomeQueries`, and `Intake/Details` correct for non-origin members with zero caller changes.
- Migration `20260820034652_ImageIntakeSubmissionGroup`: nullable `SubmissionGroupId` on `ImageIntakes` + filtered unique index (`WHERE [SubmissionGroupId] IS NOT NULL`) + FK Restrict. No new table → no new runtime grants; census untouched.
- `UploadGroupStatus` reports ONE group-level outcome row when every member resolved to the same Image-initiated registration; per-file rows otherwise.
- Promptness: `PendingWorkDispatchSchedule` `0 * * * * *` → `*/15 * * * * *` (platform.bicep + local example). Fresh work is already due at `StagedAtUtc` (`EfIntakeWorkStore.cs:190`), so the minute-quantized poll WAS the ~21 s idle; push-on-submit would be new machinery (Web never touches the transport queue) and was deliberately not built.
- FRD-02 grouped-routing case 3 now states: one Image Intake Reference per group, every member recorded against it.

## Test evidence (exact counts)

- Core: `Pegasus.Core.Tests` full — **692/692** (ImageIntake filter 93/93 re-run after simplification).
- Integration (focused): `ImageIntakePersistenceTests` **8/8** (incl. new `GroupRegistrationCreatesOneIntakeAndMovesEveryMemberReceipt`); `GroupedImageIntakeConcurrencyTests` **2/2** — concurrency test now asserts ONE reference and ONE intake id across members over all 12 iterations (exercising the adopt path, since ordinal-0 group lookup still misses pre-INTK-012); `GroupedIntakeWebTests` + `UploadOutcomeQueriesTests` + `ImageIntakeWebTests` **12/12**.
- `Pegasus.ArchitectureTests` **97/97**.
- `scripts/Test-MigrationGrants.ps1` — 55 migration files checked, pass. `scripts/Test-AzureDeploymentPlan.ps1 -Mode Local` — pass.
- Build `dotnet build ./Pegasus.slnx -c Release` — 0 warnings, 0 errors.

## Deliberately left out / for the reviewer

- **Production AU17SEO-01…-07 / G6KDL-01 data remediation is NOT in code.** Consolidating or closing the seven existing production rows is deploy-verification work (operator-directed closure through the product's own lifecycle), not a code-side migration.
- **Ordinal-0 `FindForMemberSourceAsync` miss is [[INTK-012]]** (same lane, next ticket). Until it lands, an ordinal-0 member's own work item still takes the single-receipt path; the store's adopt-by-origin branch makes the two paths converge on one row (proved by the concurrency test), instead of diverging into two references.
- **Unidentified resolution/staging is [[INTK-018]].** This ticket removes the needs_sorting fan-out for resolvable groups; stale U-row closure is next.
- Telemetry gap (from simplification pass): the group path does not set `image_intake.case_candidates`; the single path does. Cosmetic; add if reviewer wants it.
- Verification item "Production readback: AU17SEO consolidated" remains unticked — it is a deploy-time action, recorded above.

## Governing docs

- `docs/frd/frd-02-intake-and-source-identity.md` updated (grouped routing case 3) — the only doc whose meaning this changes; operator-notes untouched.
