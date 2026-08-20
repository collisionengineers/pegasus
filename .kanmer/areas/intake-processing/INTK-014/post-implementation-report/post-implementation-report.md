# INTK-014 post-implementation report

PR: https://github.com/collisionengineers/pegasus/pull/462 (task/intk-014-image-case-box → dev). Commits: 562ac23d (feature), fe5c22ea (tests), 5bc72eea (simplification pass), 819b28bc (merge origin/dev), 4a7b42a4 (migration re-scaffold on merged model).

## Delivered vs plan

Every plan step delivered, one deviation:
- Custody state is recorded on `ImageIntakes` columns only; `ImageIntakeDetail` (Core) was NOT extended — nothing above Infrastructure reads it yet, and the files doc's tentative "surface custody state" line would have been an abstraction without a caller. The columns are the honest record; a future operator surface reads them then.
- Migration timestamp moved (20260820051758 → 20260820055900): dev gained `SendToAiConnectorSettings` mid-task, so the migration was re-scaffolded after the merge to keep the designer/snapshot sequence true.

## Behaviour summary

- Register (single or group) → `create_image_case_custody` outbox row in the registration transaction → processor creates the bound `{reference}` folder under the approved root (reuses `CreateCaseRootAsync` staged-create) and uploads every registered group image ordinal-named; `CustodyState pending→confirmed`, `CustodyRootRemoteId` stamped under the processing lease.
- Merge → `merge_image_case_custody` row (carries the case id → visible/retryable on Operations) → contents move to case root `Evidence/Images`, binding deleted, emptied folder removed non-recursively (unexpected content fails closed); `CustodyState merged`, `CaseHistory image_custody_merged`.
- Failures: dependency/lease/cancel codes re-arm pending with backoff (Core `ImageCustodyRetryPolicy`, cap 6); integrity/scope/unexpected are terminal with `CustodyState failed`. Poison path replays completed effects. Blob custody untouched in every failure test.

## Evidence

Release build 0 warnings. Tests (exact counts): ProductionBoxCustodyTests 13 (3 new), ImageCaseCustodyIntegrationTests 2 (new), custody outbox + local custody + names 26, image intake persistence + group concurrency 10, migration suites 13 (+ post-merge rerun incl. UnidentifiedReconciliation 19), operations + vehicle 29, image intake web 3, Core.Tests 715, ArchitectureTests 97. `scripts/Test-MigrationGrants.ps1` (57 files) and `scripts/Test-AzureDeploymentPlan.ps1 -Mode Local` pass.

## Known bounds (recorded, deliberate)

- Group adoption after the create work already processed (rare race: single-receipt registration adopted into a group post-custody) can leave later-adopted members' images out of the Box folder; membership is resolved at processing time so the normal path includes all members. Blob custody remains authoritative.
- A merge whose create-side work terminally failed retries with backoff to its cap, then needs the create retried (Operations page re-arm) — recorded honestly rather than inventing content.
- Production Box verification (ticket Verification item 1) is deploy-stage: no live Box calls were made from this task; the real-path proof happens after release against api.box.com.

## Addendum — 2026-08-20

Commit 42dd427c (pushed to PR #462): FRD-05's staging-and-custody section disclaimed any per-Image-intake Box custody root ("none is claimed as delivered"); with INTK-014 that statement became false, so the paragraph now states the delivered behaviour — registration folder under the approved root, fold into the formal Case's custody on merge with non-recursive removal, fail-safe queued retries, blob custody authoritative throughout. Governing-doc change only; no code.
