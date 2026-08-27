# Plan — MAIL-017

## Diagnosis (verified read-only, 2026-08-27 ~10:00Z)

- Prod `ApprovedMailboxes`: one row, Approved, identities bound, `ActivatedAtUtc = NULL`.
- Prod `ApprovedMailboxSubscriptions`: 0 rows. `ApprovedInboxPollStates.LastCompletedAtUtc = 2026-08-26 18:33Z`.
- App Insights: `InboxRecoveryFunction` never executed before 09:30Z (MAIL-015 cron defect, fixed by release 34); after 09:53Z it is scheduled but finds no pollable mailbox; no request has ever reached `/hooks/microsoft-graph/mail`.
- Cause: migration `20260826151807` emits `UpdateData(ActivatedAtUtc = null)` for the seeded mailbox; every intake consumer filters on `ActivatedAtUtc != null`. The Graph-notification code path is correctly wired.

## Steps

1. `dotnet ef migrations add ReactivateBoundApprovedMailboxes` (repo-pinned tool, `.config/dotnet-tools.json`), then replace the empty `Up` with one `migrationBuilder.Sql` — the pattern already used in `20260826151807` (:46-69):
   `UPDATE [dbo].[ApprovedMailboxes] SET [ActivatedAtUtc] = SYSDATETIMEOFFSET() WHERE [State] = N'Approved' AND [ActivatedAtUtc] IS NULL AND [MailboxIdentity] IS NOT NULL AND [InboxFolderIdentity] IS NOT NULL;`
   `Down` is empty (activation time is not recoverable and is never rolled back). No model change → `PegasusDbContextModelSnapshot.cs` must show no diff.
2. Update the migration-head assertion in `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs:103` to the new id.
3. `docs/operations.md` release-33 entry: name the de-activation defect and MAIL-017 as its repair (no deploy in this ticket, so current-state docs otherwise unchanged).
4. Canonical gate: `dotnet restore --locked-mode`, `dotnet build -c Release --no-restore`, `dotnet test -c Release --no-build --filter "Category!=Corpus"`; plus the focused integration test.

Reuse: `migrationBuilder.Sql` pattern from `20260826151807`; no new helper, abstraction, or config.

## Acceptance

- Migration applies on an empty database and on the prod-shaped one (integration test exercises the chain).
- Prod after release: `ActivatedAtUtc` non-null (or already set by the operator re-save — then the `UPDATE` matches nothing), one `Active` subscription row within 5 min, `LastCompletedAtUtc` advancing, Mail banner `current`.

## Operator interim action (outside this ticket)

Re-save the mailbox in Administration › Mailboxes (Approved), then send a fresh test e-mail: `MailboxIntake.cs:421` skips mail received before `ActivatedAtUtc`, and the activation change resets the poll cursor.

## Simplification pass

_(recorded before the PR)_
