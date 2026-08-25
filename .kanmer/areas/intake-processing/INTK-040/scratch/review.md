# Independent PR review — 2026-08-25

## Changes

- `MailboxImageIntakeSubmission.cs` selects only direct `image/*` attachment assets from otherwise-Unidentified mailbox receipts, verifies retained bytes, and submits one stable mailbox-provenance group through `IGroupedIntakeSubmission`.
- `DurableIntake.cs`, `ProcessIntake.cs`, and `ReconcileGroupedImageIntake.cs` defer the parent U outcome, submit before work completion, suppress replay duplication, and converge incomplete-group terminal failures on one group-scoped technical U.
- `GroupedIntake.cs`, the EF store/model/migration, Worker composition, and the manual Upload caller preserve a single grouped route while carrying explicit source channel and nullable parent receipt provenance.
- Tests cover direct-photo selection, exclusions, replay, incomplete/complete terminal states, group source identity, SQL persistence/custody, Worker composition, migration census, and the U35-shaped three-JPEG outcome.
- `operator-notes.md` and FRD-02 record the authorised future-mail-only behavior. `Invoke-AzureDatabaseBootstrap.ps1` includes the migration's two Worker INSERT grants in the existing least-privilege census.

## Comments

1. **Blocking (resolved):** a transient final group-read failure after every child was already durable could register a technical U alongside the complete group's normal Image Intake outcome.
2. **Blocking (resolved):** the grant-carrying migration initially omitted its Worker INSERT grants from the Azure bootstrap permission census, causing the CI change gate to fail.
3. No unresolved blocking or non-blocking code comments remain.

## Disposition

1. **fixed-in-PR** by `2440f1a6`: only a transient failure with complete durable membership is treated as handled; incomplete groups still register group-scoped technical failure, while non-transient identity conflicts remain fail-closed. A focused regression was added.
2. **fixed-in-PR** by `af50a650`: the existing bootstrap matrix now expects Worker INSERT and continues to omit UPDATE/DELETE. Local deployment-plan validation passes and CI's migration/runtime-grant checks pass.
3. **won't-do-because:** no further action is warranted; report, plan, files ripple, governing docs, and diff agree.

## Verdict

**Pass.** This was an independent review. I checked the full PR #548 diff at head `af50a6504deaf1bd5ae638750af21039b13d00c8`, the ticket plan/files/open questions/report, FRD-02, FRD-05, FRD-12, ADR-0029, operator-note authorization, persistence and runtime grants, replay/terminal paths, and test assertions. Focused review runs passed (23 Core routing/group tests, 7 mailbox-submission tests after the fix, and the SQL-backed U35 scenario). GitHub CI is fully green: changes, documentation, local scripts, reference data, infrastructure, unit, browser, SQL shards 1-3, and SQL coverage.
