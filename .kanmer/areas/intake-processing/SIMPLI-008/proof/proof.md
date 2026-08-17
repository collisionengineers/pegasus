# Proof — SIMPLI-008 (verified on merged `dev`)

Delivered with [[SIMPLI-009]] in PR #385, merged into `dev` as **`fc144848`** on 2026-08-17. The full verification table (locked restore, Release build 0/0, Core 572, Architecture 94, **full IntegrationTests 530 passed / 16 skipped / 0 failed**, CI 10/10 on the identical tree) is on SIMPLI-009's `proof`; this document isolates the SIMPLI-008 evidence.

## Behaviour proven (ticket Verification: "A queued upload exposes its current state and destination to staff")

- `QdosIntakeWebTests.ReadableManualUploadStagesPendingWorkAndOpensItsStatusPage` — a manual upload 302s to `/Upload/Status/{id}`; the page shows `<h1>Received</h1>` with the file name and `data-auto-refresh="2000"`; after the Worker drains it, `<h1>Complete</h1>` without auto-refresh and an "Open receipt" link to `/Received/{id}`.
- `QdosIntakeWebTests.CompletedAllocatedUploadStatusLinksOnlyToItsCase` — an allocated upload's status page offers "Open case" and not the receipt link.
- `QdosIntakeWebTests.UploadStatusIsStaffOnlyAndUnknownReceiptsReturnNotFound` — anonymous is denied; unknown ids return 404.
- `RecoveryTests.QueuedStatusProjectsAnActiveProcessingLease` — an item under a processing lease reads **Processing**.
- `RecoveryTests.UnexpectedProcessingFailureIsPersistedThenRethrown` — a failed item reads **Failed** with bounded wording (no failure code leaks into HTML).
- `InstructionDraftWebTests` — a replayed upload lands with `?duplicate=true` and the receipt page says "was already received".
- Design system: page uses `page-heading` / `panel` / `detail-list` / `button-row` / `primary-action` / `secondary-action` (review B2 fixed); `docs/design/README.md` intake row updated.

## Not claimed

No deployment or live-runtime claim. Follow-up [[INTK-001]] covers retry-scheduled honesty/refresh policy and the auto-associated case link.
