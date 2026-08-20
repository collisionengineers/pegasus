# Plan — INTK-015: the submission group is the registration unit

One ImageIntake per `IntakeSubmissionGroup` when the group resolves to registration; every member receipt records against it; group outcome surface reports one registration; dispatch cadence tightened. Branch `task/intk-015-group-registration-unit`, worktree `../pegasus-worktrees/intk-015`, PR → `dev`.

## Steps

1. **Contract** — add `Guid? SubmissionGroupId = null` to `RegisterImageIntakeRequest` (`ImageIntakeContracts.cs`). Reuses the existing request record; no new port.
2. **Store** (`EfImageIntakeStore.RegisterAsync`) — reusing the existing single serializable transaction:
   - probe order: op-key replay (unchanged) → row with same `SubmissionGroupId` (return if identity-consistent, conflict otherwise) → existing-by-origin (unchanged, plus *adopt*: identity-consistent row without a group stamps `SubmissionGroupId` and falls through to the member flip, converging the single-receipt path with the group path).
   - stamp `SubmissionGroupId` on the new row; flip every image-only NeedsSorting member receipt (via `IntakeSubmissionGroupMembers` → latest `IntakeEvaluations.ProcessedReceiptId` — the existing seam, reduced in memory) to `image_intake_registered` referencing the ONE reference, each with its own `IntakeMutationHistory` row keyed `{opKey}:{receiptId:N}` (≤100 chars).
   - fingerprint gains the group id only when present, so pre-deploy replays keep their stored fingerprints.
   - `GetByOriginReceiptAsync` + `EnsureRegisteredReceiptDecisionAsync` get a group-aware fallback (receipt → evaluations → member rows → `SubmissionGroupId`); this is what makes `SynchronizeUnidentifiedAsync`, `ImageIntakeCasePairing.SyncMergeAfterLinkAsync`, `UploadOutcomeQueries`, and `Intake/Details` correct for non-origin members with zero caller changes.
3. **Migration** — `ImageIntakeSubmissionGroup`: nullable column, filtered unique index, FK Restrict to `IntakeSubmissionGroups`. No new table → no new grants; `Test-MigrationGrants.ps1` + `Test-AzureDeploymentPlan.ps1 -Mode Local` must stay green.
4. **Automation** (`ImageIntakeAutomation.TryApplyGroupAsync`) — consume the single `ImageIntakeGroupRoutingPolicy` decision once: pick primary = lowest-ordinal image-only member (deterministic across racing siblings), resolve its origin, compute the target-case/one-missing-character completion once (same rule as today, reusing the eligible-case list already fetched for `eligibleCaseCount`), register once with `image-intake-register:group:{group.Id:N}`. Then confirm used suggestions across all members' scans (existing `ConfirmUsedSuggestionsAsync`), auto-link each member receipt only under `AssociateExistingCase` (existing `TryAssociateAsync`, receipt-scoped keys unchanged), and `EnsureRegisteredReceiptDecisionAsync` any member still NeedsSorting (covers the replay/straggler path). Registration failure → `GroupPending`, exactly the INTK-011 deferral contract; `ReconcileGroupedImageIntake` remains the bounded retry. The single-receipt path and its `TryRegisterAndAssociateAsync` stay for non-group receipts (the `groupDecision` parameter goes away — the group path no longer uses it).
5. **Group outcome surface** (`UploadGroupStatus.cshtml(.cs)`) — when every member's terminal outcome is `ImageCaseRegistered` with the same target, render the outcome once with the file list under it; everything else keeps the existing per-file rows. No new query — reuses the outcomes already built.
6. **Promptness** — `PendingWorkDispatchSchedule` `0 * * * * *` → `*/15 * * * * *` (platform.bicep + local.settings.example.json). Fresh work is already due at `StagedAtUtc`; the minute-quantized dispatch poll was the ~21 s idle in production. A push-on-submit path would be new machinery (Web currently never touches the transport queue) — deliberately not built.
7. **FRD-02** — grouped routing case 3: one Image Intake Reference per group, every member recorded against it (aligns the written contract with the settled operator truth "the group — never an individual image — is the unit").
8. **Tests** — update `AutomaticImageIntakeTests` group tests (one registration, group key, association preserved, pending-on-failure); `GroupedImageIntakeConcurrencyTests` now asserts ONE reference across concurrent members over all iterations and reworks the stranded-member recovery to the new shape; `ImageIntakePersistenceTests` adds group registration flipping every member + unique index + group-aware lookup. Focused filters + full ImageIntake/GroupedIntake classes; `Test-MigrationGrants.ps1`; `Test-AzureDeploymentPlan.ps1 -Mode Local`.

## Acceptance

- A multi-image single-VRM group yields exactly one ImageIntake row (one reference, one sequence increment) with every member's receipt at `image_intake_registered` naming that reference — repeatedly and under the concurrency test.
- `AssociateExistingCase` still links every member to the one eligible case; fail-closed group decisions still never associate.
- No new Unidentified detour for a group that resolves (unchanged INTK-007 scope for genuinely unresolved groups).
- Build zero-warning; focused suites green; migration scripts green.

## Notes

- Production AU17SEO-01…-07 consolidation is deploy-verification work, not code — recorded for the post-implementation report.
- Ordinal-0 group-lookup miss is [[INTK-012]] (this lane, next); the store's adopt-by-origin branch keeps the interim window convergent instead of divergent.
