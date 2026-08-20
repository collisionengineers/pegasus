# Files — INTK-015 group registration unit

Verified read-only against the worktree at `origin/dev` (8812b278) and against production evidence in the roster diagnostics (group 520b2f69: 7 ImageIntakes AU17SEO-01…-07 from one 0.82 hit, 3 siblings to needs_sorting).

## Root cause (verified in code)

- `src/Pegasus.Core/ImageIntake/ImageIntakeAutomation.cs:249-263` — `TryApplyGroupAsync` loops `foreach (memberReceipt, suggestions) in scans` calling `TryRegisterAndAssociateAsync` per member with receipt-scoped operation key `image-intake-register:{receipt.Id:N}` (line 460).
- `src/Pegasus.Infrastructure/Persistence/EfImageIntakeStore.cs:54-81` — `RegisterAsync` dedupes only by `CreationOperationKey` or `OriginReceiptId`/(SourceChannel, ExternalReceiptToken); `:117-133` increments `ImageIntakeSequences.LastAllocatedSequence` per registration → `VRM-01…-07`.
- `ImageIntakeGroupRoutingPolicy` already yields ONE decision per group; the loop then applies it N times.

## Change set

- `src/Pegasus.Core/ImageIntake/ImageIntakeContracts.cs` — add `Guid? SubmissionGroupId = null` to `RegisterImageIntakeRequest`.
- `src/Pegasus.Core/ImageIntake/ImageIntakeAutomation.cs` — replace the per-member registration loop with one group-scoped registration (`image-intake-register:group:{group.Id:N}`, `SubmissionGroupId = group.Id`, origin = lowest-ordinal image-only member, deterministic across racing siblings); confirm suggestions across all scans; per-member `AutoLinkAsync` only for the `AssociateExistingCase` decision (unchanged rule); after register/replay, `EnsureRegisteredReceiptDecisionAsync` per still-NeedsSorting member.
- `src/Pegasus.Infrastructure/Persistence/ImageIntakeEntities.cs` — `SubmissionGroupId` (nullable) on `ImageIntakeEntity`.
- `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs` — unique filtered index on `SubmissionGroupId` (`WHERE [SubmissionGroupId] IS NOT NULL`), FK → `IntakeSubmissionGroups`.
- `src/Pegasus.Infrastructure/Persistence/EfImageIntakeStore.cs` —
  - `RegisterAsync`: group path — dedupe/adopt by `SubmissionGroupId`, stamp the column, flip EVERY image-only NeedsSorting member receipt to `image_intake_registered` in the same transaction (members resolved intake→`IntakeSubmissionGroupMembers`→`IntakeEvaluations.ProcessedReceiptId`; the existing member/asset association seam — no parallel store), one `IntakeMutationHistory` row per member (`{opKey}:{receiptId:N}`).
  - `GetByOriginReceiptAsync` / `EnsureRegisteredReceiptDecisionAsync`: group-aware fallback — resolve the group intake for ANY member receipt via evaluations→members→SubmissionGroupId.
  - Fingerprint: append the group id only when present (old fingerprints stay replayable).
- `src/Pegasus.Infrastructure/Migrations/` — new migration `ImageIntakeSubmissionGroup` (column + filtered unique index + FK; no new table → no new runtime grants; census untouched, verified by `scripts/Test-MigrationGrants.ps1`).
- `src/Pegasus.Web/Pages/UploadGroupStatus.cshtml(.cs)` — collapse identical `ImageCaseRegistered` member outcomes into ONE group outcome row (the group registered one reference; per-file rows for everything else unchanged).
- `infra/modules/platform.bicep`, `src/Pegasus.Worker/local.settings.example.json` — `PendingWorkDispatchSchedule` `0 * * * * *` → `*/15 * * * * *` (promptness: dispatch quantization was the ~21 s idle; due-at is already "now" — `EfIntakeWorkStore.cs:190` `DueAtUtc = receipt.StagedAtUtc` — so the poll cadence is the only lever that stays inside existing conventions).
- `docs/frd/frd-02-intake-and-source-identity.md` — case 3 of grouped routing: the group allocates exactly ONE Image Intake Reference; every member's evidence and receipt records against it.

## Tests

- `tests/Pegasus.Core.Tests/ImageIntake/AutomaticImageIntakeTests.cs` — group tests re-pointed at one registration per group (group-scoped key, SubmissionGroupId), association per member preserved, pending-on-failure preserved.
- `tests/Pegasus.IntegrationTests/GroupedImageIntakeConcurrencyTests.cs` — `references` assertion 2→1 (one reference across concurrent members, all 12 iterations); stranded-member test reworked to the new shape.
- `tests/Pegasus.IntegrationTests/ImageIntakePersistenceTests.cs` — store tests: group registration flips every member, unique SubmissionGroupId index, group-aware origin lookup.

## Out of scope (stated)

- Production AU17SEO-01…-07 row remediation — deploy-verification task, not code migration.
- Ordinal-0 `FindForMemberSourceAsync` miss — [[INTK-012]] (next in this lane); until it lands an ordinal-0 trigger still takes the single-receipt path; the store's adopt-by-origin branch converges the two paths on the same row.
- Unidentified resolution/staging — [[INTK-018]].
